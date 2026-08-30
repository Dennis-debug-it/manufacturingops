using System.Text.Json;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Common;

/// <summary>
/// Resolves the workspace currency that money without an explicit currency should be rendered in.
///
/// The value lives in the tenant's report-template settings, whose own endpoint is admin-only. Left
/// there, a finance user or technician cannot read it and the client falls back to a hardcoded
/// default — so the same invoice renders in different currencies depending on who is looking. It is
/// therefore surfaced on the session payload, which every role receives.
/// </summary>
public static class TenantCurrency
{
    /// <summary>
    /// Returns the tenant's configured ISO-4217 currency, or <c>null</c> when none is set. Null is
    /// meaningful: it tells the client to fall back rather than adopt another workspace's currency.
    /// </summary>
    public static async Task<string?> ResolveAsync(AppDbContext dbContext, Guid tenantId, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
        {
            return null;
        }

        var configJson = await dbContext.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(x => x.Id == tenantId)
            .Select(x => x.ReportTemplateConfigJson)
            .SingleOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(configJson))
        {
            return null;
        }

        try
        {
            // Parsed rather than deserialized into the settings record so that contract can evolve
            // without breaking sign-in.
            using var document = JsonDocument.Parse(configJson);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("DefaultCurrency", out var currency)
                && currency.ValueKind == JsonValueKind.String)
            {
                var value = currency.GetString();
                return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
            }
        }
        catch (JsonException)
        {
            // Malformed settings must never break sign-in; the client falls back.
        }

        return null;
    }
}
