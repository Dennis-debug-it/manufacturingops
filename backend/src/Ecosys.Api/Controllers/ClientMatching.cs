using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// Finding the same company or person entered twice. Names are compared on a key that ignores case,
/// punctuation and legal suffixes ("Baraka Logistics Ltd." = "baraka logistics"); company email
/// domains (never public mail providers) and the last nine digits of a phone number (so +2547… and
/// 07… agree) count as matches too.
/// </summary>
public static class ClientMatching
{
    private static readonly HashSet<string> LegalSuffixes = new(StringComparer.Ordinal)
    {
        "ltd", "limited", "plc", "inc", "incorporated", "llc", "llp", "co", "company", "corp", "corporation", "the", "kenya", "k",
    };

    private static readonly HashSet<string> PublicMailDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "gmail.com", "yahoo.com", "yahoo.co.uk", "hotmail.com", "outlook.com", "live.com", "icloud.com", "aol.com", "proton.me", "protonmail.com", "ymail.com", "msn.com",
    };

    public static string NameKey(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var cleaned = new string(name.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray());
        var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => !LegalSuffixes.Contains(w)).ToArray();
        return words.Length == 0 ? cleaned.Trim() : string.Join(' ', words);
    }

    public static string? CompanyDomain(string? email)
    {
        var at = email?.Trim().LastIndexOf('@') ?? -1;
        if (at < 0) return null;
        var domain = email!.Trim()[(at + 1)..].ToLowerInvariant();
        return domain.Length == 0 || PublicMailDomains.Contains(domain) ? null : domain;
    }

    public static string? PhoneKey(string? phone)
    {
        var digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length < 9 ? null : digits[^9..];
    }

    /// <summary>Clients and prospects that look like the given company, best match first (at most five).</summary>
    public static async Task<IReadOnlyList<PossibleDuplicateClient>> FindAsync(
        AppDbContext dbContext, Guid tenantId, string? name, string? email, string? phone, Guid? excludeId, CancellationToken cancellationToken)
    {
        var key = NameKey(name);
        var domain = CompanyDomain(email);
        var phoneKey = PhoneKey(phone);
        if (key.Length == 0 && domain is null && phoneKey is null)
            return [];

        var clients = await dbContext.Clients.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive && x.Id != excludeId)
            .Select(x => new { x.Id, x.ClientName, x.IsProspect, x.Email, x.Phone, x.ContactPhone })
            .ToListAsync(cancellationToken);
        var contacts = domain is null && phoneKey is null ? [] : await dbContext.ClientContacts.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .Select(x => new { x.ClientId, x.Email, x.Phone })
            .ToListAsync(cancellationToken);

        var results = new List<(PossibleDuplicateClient Match, int Score)>();
        foreach (var client in clients)
        {
            var clientKey = NameKey(client.ClientName);
            string? reason = null;
            var score = 0;
            if (key.Length > 0 && clientKey == key) { reason = "Same name"; score = 3; }
            else if (key.Length >= 4 && clientKey.Length >= 4 && (clientKey.StartsWith(key + " ", StringComparison.Ordinal) || key.StartsWith(clientKey + " ", StringComparison.Ordinal)))
            { reason = "Similar name"; score = 1; }
            if (score < 2 && domain is not null && (CompanyDomain(client.Email) == domain || contacts.Any(c => c.ClientId == client.Id && CompanyDomain(c.Email) == domain)))
            { reason = $"Same email domain ({domain})"; score = 2; }
            if (score < 2 && phoneKey is not null && (PhoneKey(client.Phone) == phoneKey || PhoneKey(client.ContactPhone) == phoneKey || contacts.Any(c => c.ClientId == client.Id && PhoneKey(c.Phone) == phoneKey)))
            { reason = "Same phone number"; score = 2; }
            if (reason is not null)
                results.Add((new PossibleDuplicateClient(client.Id, client.ClientName, client.IsProspect, reason), score));
        }
        return results.OrderByDescending(x => x.Score).ThenBy(x => x.Match.IsProspect).ThenBy(x => x.Match.ClientName).Take(5).Select(x => x.Match).ToList();
    }

    /// <summary>
    /// Folds a prospect into another client or prospect: its deals, RFP/RFQs, quotes, sites, portal
    /// users and contacts move across (a contact already there by email or name is reused), and the
    /// prospect is deleted. Only prospects can be merged away: a client with jobs, assets, contracts or
    /// invoices behind it is never folded automatically. Changes are added to the context.
    /// </summary>
    public static async Task MergeProspectAsync(AppDbContext dbContext, Guid tenantId, Guid sourceId, Guid targetId, CancellationToken cancellationToken)
    {
        if (sourceId == targetId)
            throw new BusinessRuleException("Pick a different client to merge into.");
        var source = await dbContext.Clients.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == sourceId, cancellationToken)
            ?? throw new NotFoundException("Client not found.");
        var target = await dbContext.Clients.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == targetId, cancellationToken)
            ?? throw new NotFoundException("Client to merge into not found.");
        if (!source.IsProspect)
            throw new BusinessRuleException("Only a prospect can be merged into another client.");
        var hasHistory = await dbContext.WorkOrders.AnyAsync(x => x.TenantId == tenantId && x.ClientId == sourceId, cancellationToken)
            || await dbContext.Assets.AnyAsync(x => x.TenantId == tenantId && x.ClientId == sourceId, cancellationToken)
            || await dbContext.Contracts.AnyAsync(x => x.TenantId == tenantId && x.ClientId == sourceId, cancellationToken)
            || await dbContext.TenantInvoices.AnyAsync(x => x.TenantId == tenantId && x.ClientId == sourceId, cancellationToken)
            || await dbContext.TenantPayments.AnyAsync(x => x.TenantId == tenantId && x.ClientId == sourceId, cancellationToken);
        if (hasHistory)
            throw new BusinessRuleException($"{source.ClientName} already has jobs, assets, contracts or invoices, so it cannot be merged automatically.");

        await dbContext.TenantQuotations.Where(x => x.TenantId == tenantId && x.ClientId == sourceId).ForEachAsync(x => x.ClientId = targetId, cancellationToken);
        await dbContext.Sites.Where(x => x.TenantId == tenantId && x.ClientId == sourceId).ForEachAsync(x => x.ClientId = targetId, cancellationToken);
        await dbContext.ClientPortalUsers.Where(x => x.TenantId == tenantId && x.ClientId == sourceId).ForEachAsync(x => x.ClientId = targetId, cancellationToken);

        var targetContacts = await dbContext.ClientContacts.Where(x => x.TenantId == tenantId && x.ClientId == targetId && x.IsActive).ToListAsync(cancellationToken);
        var sourceContacts = await dbContext.ClientContacts.Where(x => x.TenantId == tenantId && x.ClientId == sourceId).ToListAsync(cancellationToken);
        foreach (var contact in sourceContacts)
        {
            var same = targetContacts.FirstOrDefault(x => (contact.Email != null && x.Email == contact.Email)
                || string.Equals(x.FullName.Trim(), contact.FullName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (same is null)
            {
                contact.ClientId = targetId;
                contact.IsPrimary = contact.IsPrimary && !targetContacts.Any(x => x.IsPrimary);
                continue;
            }
            same.Email ??= contact.Email;
            same.Phone ??= contact.Phone;
            same.JobTitle ??= contact.JobTitle;
            dbContext.ClientContacts.Remove(contact);
        }

        target.Email ??= source.Email;
        target.Phone ??= source.Phone;
        target.ContactPerson ??= source.ContactPerson;
        target.ContactPhone ??= source.ContactPhone;
        target.Location ??= source.Location;
        dbContext.Clients.Remove(source);
    }
}

public sealed record PossibleDuplicateClient(Guid Id, string ClientName, bool IsProspect, string Reason);
