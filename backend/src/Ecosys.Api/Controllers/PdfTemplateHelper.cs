using System.Text.Json;
using Ecosys.Shared.Contracts.Integration;

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
}
