using System.Net.Mail;
using System.Text.RegularExpressions;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

public static class TenantEmailIdentity
{
    public const string Domain = "serviceops.ecosysdigital.co.ke";
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "api", "app", "mail", "support", "help", "serviceops", "ecosys", "system", "root", "postmaster"
    };

    public static string NormalizeSlug(string value, bool fromCompanyName = false)
    {
        var normalized = value.Trim().ToLowerInvariant();
        if (fromCompanyName)
            normalized = Regex.Replace(normalized, @"\s+(ltd|limited|inc|incorporated|llc)\.?$", "");
        normalized = Regex.Replace(normalized.Replace("'", ""), "[^a-z0-9]+", "-").Trim('-');
        if (normalized.Length is < 3 or > 63)
            throw new BusinessRuleException("Tenant slug must contain 3 to 63 letters, numbers, or hyphens.");
        if (Reserved.Contains(normalized))
            throw new BusinessRuleException("This tenant slug is reserved. Please choose another.");
        return normalized;
    }

    public static string Address(string slug) => $"{NormalizeSlug(slug)}@{Domain}";

    public static EmailSetting Create(Tenant tenant) => new()
    {
        TenantId = tenant.Id, Tenant = tenant,
        SenderName = tenant.CompanyName[..Math.Min(tenant.CompanyName.Length, 150)], SenderAddress = Address(tenant.Slug),
        ManagedEmailAddress = Address(tenant.Slug), InboundEnabled = true, IsEnabled = true,
        // Writing this row does not make the address sendable — the mail server has to know it too.
        ManagedEmailStatus = ManagedEmailStatuses.Pending
    };

    /// <summary>
    /// The one rule for "who does tenant mail come from". Every tenant-scoped send — invitations, resets,
    /// work orders, finance documents, notifications — resolves through here so modules cannot drift onto
    /// different senders. From is always the tenant's managed address (never the platform address and
    /// never the SMTP login, which is a separate transport concern); Reply-To defaults to that same
    /// address unless an explicit override was configured. Returns null when the tenant has no usable
    /// identity, and callers must fail the send rather than borrow another sender.
    /// Reply-To order: explicit override, then the category's reply-to (finance/operations), then the
    /// tenant's general reply-to, then the managed address. From never varies by category.
    /// </summary>
    public static TenantSender? ResolveSender(Tenant? tenant, EmailSetting? identity, string? replyToOverride = null,
        EmailCategory category = EmailCategory.General)
    {
        var address = NormalizeAddress(identity?.ManagedEmailAddress) ?? TryAddress(tenant?.Slug);
        if (address is null) return null;
        var name = FirstNonBlank(identity?.SenderName, tenant?.CompanyName, tenant?.Name) ?? address;
        var categoryReplyTo = category switch
        {
            EmailCategory.Finance => identity?.FinanceReplyToEmail,
            EmailCategory.Operations => identity?.OperationsReplyToEmail,
            _ => null,
        };
        var replyTo = NormalizeAddress(replyToOverride)
            ?? NormalizeAddress(categoryReplyTo)
            ?? NormalizeAddress(identity?.ReplyToEmail)
            ?? address;
        return new TenantSender(name, address, replyTo);
    }

    public static async Task<TenantSender?> ResolveSenderAsync(AppDbContext db, Guid tenantId, string? replyToOverride = null,
        EmailCategory category = EmailCategory.General, CancellationToken cancellationToken = default)
    {
        var tenant = await db.Tenants.SingleOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        var identity = await db.EmailSettings.SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        return ResolveSender(tenant, identity, replyToOverride, category);
    }

    private static readonly string[] FinanceDocumentTypes = ["Invoice", "Receipt", "Statement", "Quotation", "CreditNote", "Payment"];
    private static readonly string[] FinanceEventPrefixes = ["invoice", "receipt", "statement", "quotation", "quote", "finance", "credit-note", "payment", "scheduled-report.finance"];
    private static readonly string[] OperationsEventPrefixes = ["work-order", "workorder", "sla", "escalation", "eta.", "pm.", "pm-", "contract", "materials", "asset", "scheduled-report.operations"];

    /// <summary>
    /// Which reply-to a message uses, from what the send already records: the finance document type, or
    /// the event key's prefix. Anything unrecognised (invitations, resets, portal links, reports) is General.
    /// </summary>
    public static EmailCategory Categorize(string? eventKey, string? documentType)
    {
        if (!string.IsNullOrWhiteSpace(documentType)
            && FinanceDocumentTypes.Contains(documentType.Trim(), StringComparer.OrdinalIgnoreCase))
            return EmailCategory.Finance;
        var key = eventKey?.Trim() ?? string.Empty;
        if (FinanceEventPrefixes.Any(p => key.StartsWith(p, StringComparison.OrdinalIgnoreCase))) return EmailCategory.Finance;
        if (OperationsEventPrefixes.Any(p => key.StartsWith(p, StringComparison.OrdinalIgnoreCase))) return EmailCategory.Operations;
        return EmailCategory.General;
    }

    /// <summary>
    /// Records what the mail server said about the managed address. Only a sender refusal demotes it —
    /// a timeout or a bad recipient says nothing about whether the address itself is provisioned.
    /// Returns true when the row changed and needs saving.
    /// </summary>
    public static bool RecordSenderOutcome(EmailSetting? identity, bool accepted, string? errorCategory, string? detail, DateTime utcNow)
    {
        if (identity?.ManagedEmailAddress is null) return false;
        if (accepted)
        {
            if (identity.ManagedEmailStatus == ManagedEmailStatuses.Verified) return false;
            identity.ManagedEmailStatus = ManagedEmailStatuses.Verified;
            identity.ManagedEmailStatusCheckedAt = utcNow;
            identity.ManagedEmailStatusDetail = null;
            return true;
        }

        if (errorCategory != Ecosys.Shared.Contracts.Integration.EmailErrorCategories.SenderRejected) return false;
        identity.ManagedEmailStatus = ManagedEmailStatuses.Rejected;
        identity.ManagedEmailStatusCheckedAt = utcNow;
        identity.ManagedEmailStatusDetail = Truncate(
            $"The mail server refused {identity.ManagedEmailAddress} as a sender. The address must be provisioned on the mail server " +
            $"(catch-all, alias or forwarder for @{Domain}) and the platform SMTP account allowed to send as it. Server said: {detail}", 1000);
        return true;
    }

    public const string NoReplyNotice = "This is an automated message. Please do not reply to this email.";

    /// <summary>
    /// Adds the do-not-reply notice to mail sent from a generated @serviceops address when a reply would
    /// land back on that same system address. A tenant that configured its own Reply-To (a real mailbox)
    /// is not told to discourage replies. Applied at send time, so outbox retries never stack it.
    /// </summary>
    public static string AppendNoReplyNotice(string body, bool isHtml, string? fromAddress, string? replyToAddress)
    {
        if (!IsManagedAddress(fromAddress)
            || (!string.IsNullOrWhiteSpace(replyToAddress) && !IsManagedAddress(replyToAddress))
            || body.Contains(NoReplyNotice, StringComparison.Ordinal))
        {
            return body;
        }

        if (!isHtml)
        {
            return $"{body.TrimEnd()}\r\n\r\n--\r\n{NoReplyNotice}";
        }

        const string notice = "<p style=\"margin:24px 0 0;padding-top:12px;border-top:1px solid #e5e7eb;color:#6b7280;font-size:12px;\">"
            + NoReplyNotice + "</p>";
        var bodyEnd = body.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return bodyEnd >= 0 ? body.Insert(bodyEnd, notice) : body + notice;
    }

    public static bool IsManagedAddress(string? address) =>
        !string.IsNullOrWhiteSpace(address)
        && address.Trim().EndsWith("@" + Domain, StringComparison.OrdinalIgnoreCase);

    /// <summary>A new or renamed address has not been proven against the mail server.</summary>
    public static void MarkPending(EmailSetting identity)
    {
        identity.ManagedEmailStatus = ManagedEmailStatuses.Pending;
        identity.ManagedEmailStatusCheckedAt = null;
        identity.ManagedEmailStatusDetail = null;
    }

    private static string? TryAddress(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        try { return Address(slug); }
        catch (BusinessRuleException) { return null; }
    }

    private static string? NormalizeAddress(string? value) =>
        !string.IsNullOrWhiteSpace(value) && MailAddress.TryCreate(value.Trim(), out var parsed) ? parsed.Address.ToLowerInvariant() : null;

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim();

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];

    /// <summary>Resolves only within the caller's tenant scope, or across tenants for a platform intake worker.</summary>
    public static async Task<Guid?> ResolveRecipientAsync(AppDbContext db, string recipient, CancellationToken cancellationToken = default)
    {
        if (!MailAddress.TryCreate(recipient, out var address)) return null;
        var normalized = address.Address.ToLowerInvariant();
        return await db.EmailSettings.Where(x => x.InboundEnabled && x.ManagedEmailAddress == normalized && x.Tenant != null && x.Tenant.IsActive)
            .Select(x => (Guid?)x.TenantId).SingleOrDefaultAsync(cancellationToken);
    }

    public static string DefaultCurrency(string? country) => country?.Trim().ToLowerInvariant() switch
    {
        "kenya" or "ke" => "KES", "uganda" or "ug" => "UGX", "tanzania" or "tz" => "TZS",
        "rwanda" or "rw" => "RWF", "nigeria" or "ng" => "NGN", "south africa" or "za" => "ZAR",
        "united kingdom" or "uk" or "gb" => "GBP", _ => "USD"
    };
}

/// <summary>Selects the tenant's Reply-To for a message; the From address is the same for every category.</summary>
public enum EmailCategory
{
    General,
    Finance,
    Operations,
}

/// <summary>The resolved tenant From/Reply-To — see <see cref="TenantEmailIdentity.ResolveSender"/>.</summary>
public sealed record TenantSender(string FromName, string FromAddress, string ReplyTo);
