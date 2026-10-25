using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// Who may use the sales CRM: the <see cref="PermissionNames.CrmView"/> and
/// <see cref="PermissionNames.CrmManage"/> capabilities, granted per role in the role matrix (and
/// adjustable per user or permission group like any other capability). Admin holds both.
/// </summary>
internal static class SalesCrmAccess
{
    public static bool CanManage(ITenantContext tenantContext) =>
        tenantContext.IsAdmin || tenantContext.HasPermission(PermissionNames.CrmManage);

    public static bool CanView(ITenantContext tenantContext) =>
        CanManage(tenantContext) || tenantContext.HasPermission(PermissionNames.CrmView);

    /// <summary>
    /// The active users who can manage the pipeline, and so can own a deal or RFP/RFQ. Resolved in
    /// a handful of set queries with the same layering as <c>DynamicAccessService</c>: the role
    /// matrix is the baseline, a permission group adds to it, and a per-user override has the last
    /// word either way.
    /// </summary>
    public static async Task<IReadOnlyList<SalesOwnerOption>> ListEligibleOwnersAsync(
        AppDbContext dbContext, IRolePermissionService rolePermissionService, Guid tenantId, CancellationToken cancellationToken)
    {
        var capability = PermissionNames.CrmManage.ToLower();
        var users = await dbContext.Users
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .Select(x => new { x.Id, x.FullName, x.Role })
            .ToListAsync(cancellationToken);

        var matrix = await rolePermissionService.GetMatrixAsync(tenantId, cancellationToken);
        var viewerRow = matrix.FirstOrDefault(x => string.Equals(x.Role, AppRoles.Viewer, StringComparison.OrdinalIgnoreCase));
        bool RoleGrants(string role)
        {
            if (string.Equals(role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase)) return true;
            // Unknown and legacy roles resolve as Viewer, exactly as RolePermissionService does.
            var row = matrix.FirstOrDefault(x => string.Equals(x.Role, role, StringComparison.OrdinalIgnoreCase)) ?? viewerRow;
            return row?.Permissions.CanManageCrm == true;
        }

        var grantingGroups = await dbContext.GroupCapabilities
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Capability.ToLower() == capability)
            .Select(x => x.PermissionGroupId)
            .ToListAsync(cancellationToken);
        var groupMembers = (await dbContext.UserPermissionGroups
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && grantingGroups.Contains(x.PermissionGroupId))
                .Select(x => x.UserId)
                .ToListAsync(cancellationToken))
            .ToHashSet();
        var overrides = (await dbContext.UserPermissionOverrides
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.Capability.ToLower() == capability)
                .Select(x => new { x.UserId, x.IsAllowed })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.First().IsAllowed);

        return users
            .Where(user => overrides.TryGetValue(user.Id, out var allowed)
                ? allowed
                : RoleGrants(user.Role) || groupMembers.Contains(user.Id))
            .OrderBy(x => x.FullName)
            .Select(x => new SalesOwnerOption(x.Id, x.FullName, x.Role))
            .ToList();
    }

    /// <summary>Resolves an owner for a deal or tender: an active user of this tenant who can manage the pipeline.</summary>
    public static async Task<string> ResolveOwnerNameAsync(
        AppDbContext dbContext, IRolePermissionService rolePermissionService, Guid tenantId, Guid ownerUserId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Users.AnyAsync(x => x.TenantId == tenantId && x.Id == ownerUserId, cancellationToken))
            throw new NotFoundException("Owner not found.");

        var eligible = await ListEligibleOwnersAsync(dbContext, rolePermissionService, tenantId, cancellationToken);
        return eligible.FirstOrDefault(x => x.Id == ownerUserId)?.FullName
            ?? throw new BusinessRuleException("The owner must be an active user who can manage the sales pipeline.");
    }
}

/// <summary>
/// Converts deal and tender values into the tenant's reporting currency (<c>Tenant.Currency</c>)
/// using the tenant's own exchange rates — the same table Finance › Exchange Rates maintains, so
/// one rate is entered once and used everywhere. The latest rate already in effect wins, and a
/// rate entered either way round (USD→KES or KES→USD) is used. A currency with no rate converts
/// to null: totals leave it out and say so instead of adding foreign amounts as if they were local.
/// </summary>
internal sealed class SalesCurrencyConverter
{
    private readonly Dictionary<string, SalesCurrencyRate> _rates;

    private SalesCurrencyConverter(string reportingCurrency, Dictionary<string, SalesCurrencyRate> rates)
    {
        ReportingCurrency = reportingCurrency;
        _rates = rates;
    }

    public string ReportingCurrency { get; }

    public IReadOnlyCollection<SalesCurrencyRate> Rates => _rates.Values.OrderBy(x => x.Currency).ToList();

    public static async Task<SalesCurrencyConverter> LoadAsync(AppDbContext dbContext, Guid tenantId, CancellationToken cancellationToken)
    {
        var configured = await dbContext.Tenants
            .AsNoTracking()
            .Where(x => x.Id == tenantId)
            .Select(x => x.Currency)
            .FirstOrDefaultAsync(cancellationToken);
        var reporting = Normalize(configured);
        if (reporting.Length == 0) reporting = "KES";

        var now = DateTime.UtcNow;
        var rows = await dbContext.ExchangeRates
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.EffectiveFrom <= now && x.Rate > 0
                && (x.BaseCurrency == reporting || x.QuoteCurrency == reporting))
            .ToListAsync(cancellationToken);

        var rates = rows
            .Where(x => x.BaseCurrency != x.QuoteCurrency)
            .Select(x => x.QuoteCurrency == reporting
                // 1 USD = 129 KES: a USD amount times 129 is KES.
                ? new SalesCurrencyRate(x.BaseCurrency, x.Rate, x.EffectiveFrom, x.CreatedAt, x.UpdatedByName)
                // Entered the other way round (1 KES = 0.0077 USD): invert it.
                : new SalesCurrencyRate(x.QuoteCurrency, 1m / x.Rate, x.EffectiveFrom, x.CreatedAt, x.UpdatedByName))
            .GroupBy(x => x.Currency)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.EnteredAt).First(),
                StringComparer.OrdinalIgnoreCase);

        return new SalesCurrencyConverter(reporting, rates);
    }

    public bool CanConvert(string currency) =>
        string.Equals(Normalize(currency), ReportingCurrency, StringComparison.Ordinal) || _rates.ContainsKey(Normalize(currency));

    public decimal? Convert(decimal amount, string currency)
    {
        var code = Normalize(currency);
        if (code == ReportingCurrency) return amount;
        return _rates.TryGetValue(code, out var rate) ? Math.Round(amount * rate.RateToReporting, 2) : null;
    }

    public static string Normalize(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();
}

/// <summary>How many units of the reporting currency one unit of <see cref="Currency"/> is worth.</summary>
public sealed record SalesCurrencyRate(string Currency, decimal RateToReporting, DateTime EffectiveFrom, DateTime EnteredAt, string UpdatedByName);
