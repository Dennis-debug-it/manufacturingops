using System.Text.Json;
using Ecosys.Domain.Entities;
using Ecosys.Shared.Contracts.Integration;
using Microsoft.AspNetCore.Hosting;

namespace Ecosys.Api.Controllers;

/// <summary>
/// Shared helpers for applying the tenant's report template config to generated PDFs.
/// </summary>
internal static class PdfTemplateHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Parses the tenant's stored template JSON; returns null (renderer defaults) on missing/malformed config.</summary>
    public static ReportTemplateConfig? Parse(string? reportTemplateConfigJson)
    {
        if (string.IsNullOrWhiteSpace(reportTemplateConfigJson))
            return null;
        try
        {
            return JsonSerializer.Deserialize<ReportTemplateConfig>(reportTemplateConfigJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Parses the per-document-type overrides map; returns an empty map on missing/malformed data.</summary>
    public static Dictionary<string, ReportTemplateConfig> ParseDesigns(string? documentDesignsJson)
    {
        if (string.IsNullOrWhiteSpace(documentDesignsJson))
            return new(StringComparer.OrdinalIgnoreCase);
        try
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, ReportTemplateConfig>>(documentDesignsJson, JsonOptions);
            return map is null
                ? new(StringComparer.OrdinalIgnoreCase)
                : new(map, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Resolves the effective config for a document type: the per-type override if present,
    /// otherwise the shared/base config. Returns null only when neither is configured.
    /// </summary>
    public static ReportTemplateConfig? ResolveForDocument(string? sharedJson, string? documentDesignsJson, string documentTypeSlug)
    {
        var designs = ParseDesigns(documentDesignsJson);
        if (designs.TryGetValue(documentTypeSlug, out var typeConfig) && typeConfig is not null)
            return typeConfig;
        return Parse(sharedJson);
    }

    /// <summary>Composes the multi-line bank details block, or null when bank details are disabled or empty.</summary>
    public static string? ComposeBankDetails(ReportTemplateConfig? config)
    {
        if (config?.ShowBankDetails != true)
            return null;

        var parts = new[]
        {
            string.IsNullOrWhiteSpace(config.BankName) ? null : $"Bank: {config.BankName}",
            string.IsNullOrWhiteSpace(config.AccountName) ? null : $"Account Name: {config.AccountName}",
            string.IsNullOrWhiteSpace(config.AccountNumber) ? null : $"Account No: {config.AccountNumber}",
            string.IsNullOrWhiteSpace(config.Branch) ? null : $"Branch: {config.Branch}",
            string.IsNullOrWhiteSpace(config.Swift) ? null : $"SWIFT: {config.Swift}",
            string.IsNullOrWhiteSpace(config.Paybill) ? null : $"Paybill: {config.Paybill}",
        }.Where(x => x is not null);

        var details = string.Join("\n", parts);
        return string.IsNullOrWhiteSpace(details) ? null : details;
    }

    public static string? ResolveFooterText(ReportTemplateConfig? config) =>
        config?.FooterText ?? config?.DefaultFooterNote;

    // ── Finance document identity ────────────────────────────────────────────

    /// <summary>Display name for the issuing company: company name, falling back to the tenant name.</summary>
    public static string CompanyName(Tenant? tenant) =>
        !string.IsNullOrWhiteSpace(tenant?.CompanyName) ? tenant.CompanyName
        : !string.IsNullOrWhiteSpace(tenant?.Name) ? tenant.Name
        : "Ecosys";

    /// <summary>Single-line street address: physical (or legacy) address, city and country.</summary>
    public static string? CompanyAddress(Tenant? tenant)
    {
        if (tenant is null)
            return null;
        var parts = new[] { tenant.PhysicalAddress ?? tenant.Address, tenant.City, tenant.Country }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return parts.Length == 0 ? null : string.Join(", ", parts);
    }

    public static string? CompanyPhone(Tenant? tenant) => tenant?.Phone ?? tenant?.ContactPhone;

    /// <summary>Tax registration, extra contact lines and the embedded logo for finance document headers.</summary>
    public static PdfCompanyDetails CompanyDetails(Tenant? tenant, IWebHostEnvironment? environment) => new(
        LegalName: tenant?.LegalName,
        Email: tenant?.ContactEmail ?? tenant?.Email,
        Website: tenant?.Website,
        PostalAddress: tenant?.PostalAddress,
        TaxPin: tenant?.TaxPin,
        VatNumber: tenant?.VatNumber,
        RegistrationNumber: tenant?.RegistrationNumber,
        LogoBytes: TryLoadLogoBytes(tenant?.LogoUrl, environment));

    public static PdfContactDetails ClientContact(Client? client) => new(
        ContactPerson: client?.ContactPerson,
        Phone: client?.ContactPhone ?? client?.Phone,
        Email: client?.Email,
        Address: client?.Location);

    /// <summary>
    /// Loads the tenant logo for embedding. Supports base64 data URLs and logos uploaded through tenant
    /// settings (served from wwwroot/uploads/logos), which are read from disk rather than fetched over
    /// HTTP. Anything else — or any failure — returns null so the document renders without a logo.
    /// </summary>
    public static byte[]? TryLoadLogoBytes(string? logoUrl, IWebHostEnvironment? environment)
    {
        if (string.IsNullOrWhiteSpace(logoUrl) || environment is null)
            return null;

        try
        {
            var value = logoUrl.Trim();
            if (value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                var comma = value.IndexOf(',');
                return comma > 0 && value[..comma].EndsWith(";base64", StringComparison.OrdinalIgnoreCase)
                    ? Convert.FromBase64String(value[(comma + 1)..])
                    : null;
            }

            const string uploadPrefix = "/uploads/logos/";
            var path = Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri.AbsolutePath : value;
            if (!path.StartsWith(uploadPrefix, StringComparison.OrdinalIgnoreCase))
                return null;

            // GetFileName strips any directory components, so the lookup cannot leave the logos folder.
            var fileName = Path.GetFileName(Uri.UnescapeDataString(path[uploadPrefix.Length..]));
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            var webRoot = string.IsNullOrWhiteSpace(environment.WebRootPath)
                ? Path.Combine(environment.ContentRootPath, "wwwroot")
                : environment.WebRootPath;
            var fullPath = Path.Combine(webRoot, "uploads", "logos", fileName);
            return System.IO.File.Exists(fullPath) ? System.IO.File.ReadAllBytes(fullPath) : null;
        }
        catch
        {
            // Logo is best-effort; never block document generation on it.
            return null;
        }
    }
}
