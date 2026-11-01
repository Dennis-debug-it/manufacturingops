using Ecosys.Domain.Entities;
using Ecosys.Domain.Enums;

namespace Ecosys.Infrastructure.Services;

/// <summary>How a contract reaches an asset.</summary>
public static class ContractCoverScopes
{
    public const string Asset = "Asset";
    public const string Category = "Category";
    public const string Site = "Site";
    public const string Client = "Client";
}

/// <summary>
/// Which assets a contract's scope reaches, by the same rules as work-order coverage: a contract that
/// lists assets covers only those; else one listing categories covers assets in them; else one
/// listing sites covers assets there; a contract listing none covers all the client's assets.
/// Contracts need Assets, AssetCategories (with AssetCategory) and Sites loaded.
/// </summary>
public static class ContractAssetScope
{
    /// <summary>How <paramref name="contract"/> covers the asset (Asset, Category, Site or Client), or null if it doesn't.</summary>
    public static string? ScopeFor(Contract contract, Asset asset)
    {
        if (contract.ClientId != asset.ClientId)
            return null;
        if (contract.Assets.Count > 0)
            return contract.Assets.Any(x => x.AssetId == asset.Id) ? ContractCoverScopes.Asset : null;
        var category = asset.AssetCategory?.Name ?? asset.AssetType;
        if (contract.AssetCategories.Count > 0)
            return !string.IsNullOrWhiteSpace(category) && contract.AssetCategories.Any(x =>
                string.Equals(x.AssetCategory?.Name, category, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.AssetCategoryName, category, StringComparison.OrdinalIgnoreCase))
                ? ContractCoverScopes.Category : null;
        if (contract.Sites.Count > 0)
            return asset.SiteId.HasValue && contract.Sites.Any(x => x.SiteId == asset.SiteId.Value) ? ContractCoverScopes.Site : null;
        return ContractCoverScopes.Client;
    }

    /// <summary>
    /// Among <paramref name="contracts"/>, the one whose scope reaches the asset: the most specific
    /// wins, then the one running longest. Status and dates are the caller's filter.
    /// </summary>
    public static (Contract Contract, string Scope)? FindCovering(Asset asset, IEnumerable<Contract> contracts) =>
        contracts
            .Select(contract => (Contract: contract, Scope: ScopeFor(contract, asset)))
            .Where(x => x.Scope is not null)
            .OrderBy(x => ScopeRank(x.Scope!))
            .ThenByDescending(x => x.Contract.EndDate)
            .Select(x => ((Contract, string)?)(x.Contract, x.Scope!))
            .FirstOrDefault();

    /// <summary>
    /// The contract is in force on <paramref name="asOf"/>: within its term, or in its grace period
    /// once it has ended, and in a status that gives cover. Matches work-order coverage.
    /// </summary>
    public static bool IsInForce(Contract contract, DateTime asOf)
    {
        var day = asOf.Date;
        var withinTerm = contract.StartDate.Date <= day && contract.EndDate.Date >= day;
        var withinGrace = contract.EndDate.Date < day
            && contract.GracePeriodDays > 0
            && contract.EndDate.Date.AddDays(contract.GracePeriodDays) >= day;
        if (!withinTerm && !withinGrace)
            return false;
        return contract.Status is ContractStatus.Active or ContractStatus.ExpiringSoon or ContractStatus.RenewalInProgress
            || (withinGrace && contract.Status == ContractStatus.Expired);
    }

    private static int ScopeRank(string scope) => scope switch
    {
        ContractCoverScopes.Asset => 0,
        ContractCoverScopes.Category => 1,
        ContractCoverScopes.Site => 2,
        _ => 3,
    };
}
