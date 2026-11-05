using Ecosys.Domain.Entities;
using Ecosys.Domain.Enums;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// An asset's contracts, from the asset's side: which of its client's contracts could cover it, and
/// adding it to one. Before this, an asset could only be put on a contract from the contract itself.
/// </summary>
[ApiController]
[Authorize]
[Route("api/assets/{assetId:guid}/contract-options")]
public sealed class AssetContractsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IContractAccessService contractAccessService,
    IBranchAccessService branchAccessService,
    IContractOperationalService contractOperationalService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    /// <summary>Contracts that are over, or never will run, are not offered.</summary>
    private static readonly ContractStatus[] OpenStatuses =
    [
        ContractStatus.Draft, ContractStatus.PendingApproval, ContractStatus.Active,
        ContractStatus.ExpiringSoon, ContractStatus.RenewalInProgress, ContractStatus.Suspended,
    ];

    /// <summary>
    /// The client's open contracts and, for each, whether its scope already reaches this asset and
    /// whether the asset can be added to it.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<AssetContractOptionResponse>>> List(Guid assetId, CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanViewContracts();
        var asset = await LoadAssetAsync(assetId, cancellationToken);
        var contracts = await LoadContractsAsync(asset.ClientId, cancellationToken);
        return Ok(contracts.Select(contract => Option(contract, asset)).ToList());
    }

    /// <summary>
    /// Lists the asset on a contract. Only a contract that already lists specific assets accepts one:
    /// a contract covering by category, site or the whole client would, once it lists any asset, cover
    /// only its listed assets — so adding one there would quietly drop every other asset it covered.
    /// Open work orders on the asset are re-checked for coverage straight away.
    /// </summary>
    [HttpPost("{contractId:guid}")]
    public async Task<ActionResult<AssetContractOptionResponse>> Add(Guid assetId, Guid contractId, CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanManageContracts();
        var asset = await LoadAssetAsync(assetId, cancellationToken);
        var contract = (await LoadContractsAsync(asset.ClientId, cancellationToken)).SingleOrDefault(x => x.Id == contractId)
            ?? throw new NotFoundException("No open contract for this asset's client was found.");
        var option = Option(contract, asset);
        if (!option.CanAdd)
            throw new BusinessRuleException(option.Reason ?? "This asset can't be added to that contract.");

        dbContext.ContractAssets.Add(new ContractAsset { TenantId = TenantId, ContractId = contract.Id, AssetId = asset.Id });
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "contract.asset_added", nameof(Contract), contract.Id.ToString(),
            $"Added asset '{asset.AssetName}' ({asset.AssetCode}) to contract '{contract.ContractNumber}' from the asset page.", cancellationToken);

        var openWorkOrderIds = await dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId && x.AssetId == asset.Id
                && !WorkOrderStatuses.ClosedStatuses.Contains(x.Status) && x.Status != WorkOrderStatuses.Completed)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        foreach (var workOrderId in openWorkOrderIds)
            await contractOperationalService.RecalculateWorkOrderAsync(TenantId, workOrderId, cancellationToken);

        var reloaded = (await LoadContractsAsync(asset.ClientId, cancellationToken)).Single(x => x.Id == contract.Id);
        return Ok(Option(reloaded, asset));
    }

    private async Task<Asset> LoadAssetAsync(Guid assetId, CancellationToken cancellationToken)
    {
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        return await dbContext.Assets
            .Include(x => x.AssetCategory)
            .Where(x => x.TenantId == TenantId && x.Id == assetId)
            .WhereAccessible(scope, x => x.BranchId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Asset was not found.");
    }

    private async Task<List<Contract>> LoadContractsAsync(Guid? clientId, CancellationToken cancellationToken) =>
        // Factory assets have no client, so no client contract can cover them.
        clientId is null ? [] : await dbContext.Contracts
            .Include(x => x.Assets)
            .Include(x => x.Sites).ThenInclude(x => x.Site)
            .Include(x => x.AssetCategories).ThenInclude(x => x.AssetCategory)
            .Where(x => x.TenantId == TenantId && x.ClientId == clientId && !x.IsArchived && OpenStatuses.Contains(x.Status))
            .OrderByDescending(x => x.EndDate)
            .ToListAsync(cancellationToken);

    private static AssetContractOptionResponse Option(Contract contract, Asset asset)
    {
        var category = asset.AssetCategory?.Name ?? asset.AssetType;
        string scope;
        string scopeLabel;
        bool covers;
        bool canAdd;
        string? reason = null;

        if (contract.Assets.Count > 0)
        {
            scope = ContractCoverScopes.Asset;
            scopeLabel = $"{contract.Assets.Count} listed asset{(contract.Assets.Count == 1 ? "" : "s")}";
            covers = contract.Assets.Any(x => x.AssetId == asset.Id);
            canAdd = !covers;
            if (covers) reason = "This asset is already on the contract.";
        }
        else if (contract.AssetCategories.Count > 0)
        {
            scope = ContractCoverScopes.Category;
            var names = contract.AssetCategories.Select(x => x.AssetCategory?.Name ?? x.AssetCategoryName).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            scopeLabel = $"Categories: {string.Join(", ", names)}";
            covers = !string.IsNullOrWhiteSpace(category) && names.Any(x => string.Equals(x, category, StringComparison.OrdinalIgnoreCase));
            canAdd = false;
            reason = covers
                ? "Already covered: the contract covers this asset's category."
                : "This contract covers by asset category. Adding one asset would limit it to that asset alone, so add the asset's category to the contract instead.";
        }
        else if (contract.Sites.Count > 0)
        {
            scope = ContractCoverScopes.Site;
            scopeLabel = $"Sites: {string.Join(", ", contract.Sites.Select(x => x.Site?.SiteName).Where(x => !string.IsNullOrWhiteSpace(x)))}";
            covers = asset.SiteId.HasValue && contract.Sites.Any(x => x.SiteId == asset.SiteId.Value);
            canAdd = false;
            reason = covers
                ? "Already covered: the contract covers this asset's site."
                : "This contract covers by site. Adding one asset would limit it to that asset alone, so add the asset's site to the contract instead.";
        }
        else
        {
            scope = ContractCoverScopes.Client;
            scopeLabel = "All the client's assets";
            covers = true;
            canAdd = false;
            reason = "Already covered: the contract covers all of this client's assets.";
        }

        return new AssetContractOptionResponse(
            contract.Id, contract.ContractNumber, contract.Name, contract.Status.ToString(), contract.StartDate, contract.EndDate,
            scope, scopeLabel, covers, canAdd, reason);
    }
}

/// <param name="Scope">How the contract covers: Asset (a list of assets), Category, Site, or Client (everything).</param>
/// <param name="ScopeCoversAsset">The contract's scope reaches this asset (its dates and status still apply).</param>
/// <param name="Reason">Why the asset can't be added, or that it is already covered.</param>
public sealed record AssetContractOptionResponse(
    Guid ContractId, string ContractNumber, string ContractName, string Status, DateTime StartDate, DateTime EndDate,
    string Scope, string ScopeLabel, bool ScopeCoversAsset, bool CanAdd, string? Reason);
