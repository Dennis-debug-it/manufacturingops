using System.Net.Mail;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

public sealed record FinancialDocumentRecipients(
    IReadOnlyCollection<string> To,
    IReadOnlyCollection<string> Cc);

public interface IFinancialRecipientResolver
{
    Task<FinancialDocumentRecipients> ResolveAsync(
        Guid tenantId,
        Guid clientId,
        string documentType,
        CancellationToken cancellationToken = default);

    IReadOnlyCollection<string> ValidateAdditionalRecipients(IEnumerable<string>? recipients);
}

/// <summary>
/// Resolves billing recipients from tenant-scoped configuration. Dedicated recipients
/// take precedence over the client's generic address, and all results are validated and
/// de-duplicated before they reach the outbox.
/// </summary>
public sealed class FinancialRecipientResolver(AppDbContext dbContext) : IFinancialRecipientResolver
{
    public async Task<FinancialDocumentRecipients> ResolveAsync(
        Guid tenantId,
        Guid clientId,
        string documentType,
        CancellationToken cancellationToken = default)
    {
        var client = await dbContext.Clients
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == clientId, cancellationToken);
        if (client is null)
        {
            return new FinancialDocumentRecipients([], []);
        }

        var normalizedType = NormalizeGroup(documentType);
        var recipients = await dbContext.TenantNotificationRecipients
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Email)
            .ToListAsync(cancellationToken);

        var to = new List<string>();
        var cc = new List<string>();
        foreach (var recipient in recipients)
        {
            var email = NormalizeEmail(recipient.Email);
            if (email is null) continue;

            var group = NormalizeGroup(recipient.RecipientGroup);
            if (IsCcGroup(group, normalizedType))
            {
                AddDistinct(cc, email);
            }
            else if (IsToGroup(group, normalizedType))
            {
                AddDistinct(to, email);
            }
        }

        if (to.Count == 0)
        {
            var fallback = NormalizeEmail(client.Email);
            if (fallback is not null) to.Add(fallback);
        }

        cc.RemoveAll(address => to.Contains(address, StringComparer.OrdinalIgnoreCase));
        return new FinancialDocumentRecipients(to, cc);
    }

    public IReadOnlyCollection<string> ValidateAdditionalRecipients(IEnumerable<string>? recipients)
    {
        if (recipients is null) return [];

        var valid = new List<string>();
        foreach (var recipient in recipients)
        {
            var email = NormalizeEmail(recipient);
            if (email is not null) AddDistinct(valid, email);
        }

        return valid;
    }

    private static bool IsToGroup(string group, string documentType) =>
        group is "billing" or "finance" or "billingcontact" or "financecontact"
        || group == documentType
        || group == $"{documentType}recipient"
        || group == $"{documentType}recipients";

    private static bool IsCcGroup(string group, string documentType) =>
        group is "cc" or "copy" or "copied" or "additional" or "additionalrecipient" or "additionalrecipients"
        || group == $"{documentType}cc"
        || group == $"{documentType}copy";

    private static string NormalizeGroup(string? value) =>
        new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static string? NormalizeEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var candidate = value.Trim();
        try
        {
            var parsed = new MailAddress(candidate);
            return string.Equals(parsed.Address, candidate, StringComparison.OrdinalIgnoreCase)
                ? parsed.Address.ToLowerInvariant()
                : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static void AddDistinct(ICollection<string> target, string value)
    {
        if (!target.Contains(value, StringComparer.OrdinalIgnoreCase)) target.Add(value);
    }
}
