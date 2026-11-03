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
/// Who a finance document (invoice, receipt, statement) goes to. It always goes To the client
/// itself; the tenant's own finance addresses (Settings → Finance Defaults) are only ever copied.
/// These addresses are tenant-wide, so letting them replace the client — as they used to — sent
/// every client's invoices to the tenant's inbox and none to the clients. A recipient tagged for one
/// document type ("Receipt CC") is copied on that type only.
/// </summary>
public sealed class FinancialRecipientResolver(AppDbContext dbContext) : IFinancialRecipientResolver
{
    private static readonly string[] DocumentTypePrefixes = ["invoice", "receipt", "statement", "quotation", "creditnote", "payment"];

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

        var to = new List<string>();
        var clientEmail = NormalizeEmail(client.Email);
        if (clientEmail is not null) to.Add(clientEmail);

        var normalizedType = NormalizeGroup(documentType);
        var recipients = await dbContext.TenantNotificationRecipients
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Email)
            .ToListAsync(cancellationToken);

        var cc = new List<string>();
        foreach (var recipient in recipients)
        {
            var email = NormalizeEmail(recipient.Email);
            if (email is null || !AppliesTo(NormalizeGroup(recipient.RecipientGroup), normalizedType)) continue;
            AddDistinct(cc, email);
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

    /// <summary>Generic groups (cc, billing, finance…) apply to every document; "receiptcc" only to receipts.</summary>
    private static bool AppliesTo(string group, string documentType)
    {
        var targeted = DocumentTypePrefixes.FirstOrDefault(prefix => group.StartsWith(prefix, StringComparison.Ordinal));
        return targeted is null || targeted == documentType;
    }

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
