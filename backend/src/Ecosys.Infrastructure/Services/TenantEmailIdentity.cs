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
        TenantId = tenant.Id, Tenant = tenant, UsePlatformDefaults = true, OverrideSmtpSettings = false,
        SenderName = tenant.CompanyName[..Math.Min(tenant.CompanyName.Length, 150)], SenderAddress = Address(tenant.Slug),
        ManagedEmailAddress = Address(tenant.Slug), InboundEnabled = true, IsEnabled = true
    };

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
