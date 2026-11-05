using System.ComponentModel.DataAnnotations;
using Ecosys.Api.Common;
using Ecosys.Domain.Entities;
using Ecosys.Domain.Enums;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/assets")]
public sealed class AssetsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IPreventiveMaintenancePlanService preventiveMaintenancePlanService,
    IAuditLogService auditLogService,
    IUserAccessService userAccessService,
    IBranchAccessService branchAccessService,
    IDocumentNumberingService documentNumberingService,
    ILicenseGuardService licenseGuardService,
    IOrgScopeService orgScopeService,
    IAssetLifecycleService assetLifecycleService,
    IAssetClassService assetClassService) : TenantAwareControllerBase(tenantContext)
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Active",
        "Inactive"
    };

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? branchId,
        [FromQuery] Guid? clientId,
        [FromQuery] Guid? siteId,
        [FromQuery] Guid? systemId,
        [FromQuery] Guid? categoryId,
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken,
        [FromQuery] string? support = null,
        [FromQuery] Guid? orgNodeId = null,
        [FromQuery] string? criticality = null,
        [FromQuery] Guid? parentAssetId = null,
        [FromQuery] bool topLevelOnly = false)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, branchId, cancellationToken);
        var outOfSupportOnly = NormalizeSupportFilter(support);
        // Only assets still in service can be "out of support"; retired ones are already handled.
        var normalizedStatus = outOfSupportOnly ? "active" : NormalizeStatusFilter(status);
        var normalizedSearch = NormalizeSearch(search);

        var query = dbContext.Assets
            .Include(x => x.Client)
            .Include(x => x.Branch)
            .Include(x => x.Site)
            .Include(x => x.System)
            .Include(x => x.AssetCategory)
            .Include(x => x.OrgNode)
            .Include(x => x.ParentAsset)
            .Include(x => x.CustomFieldValues)
                .ThenInclude(x => x.FieldDefinition)
            .Where(x => x.TenantId == TenantId)
            .WhereAccessible(scope, x => x.BranchId)
            .AsQueryable();
        query = await ApplyOrgScopeAsync(query, cancellationToken);

        if (orgNodeId.HasValue)
        {
            // A factory or section shows everything beneath it as well.
            var subtree = (await orgScopeService.GetSubtreeAsync(TenantId, orgNodeId.Value, cancellationToken)).ToList();
            query = query.Where(x => x.OrgNodeId.HasValue && subtree.Contains(x.OrgNodeId.Value));
        }

        if (!string.IsNullOrWhiteSpace(criticality))
        {
            var level = NormalizeCriticality(criticality);
            query = query.Where(x => x.Criticality == level);
        }

        if (parentAssetId.HasValue)
        {
            query = query.Where(x => x.ParentAssetId == parentAssetId.Value);
        }
        else if (topLevelOnly)
        {
            query = query.Where(x => x.ParentAssetId == null);
        }

        if (clientId.HasValue)
        {
            query = query.Where(x => x.ClientId == clientId.Value);
        }

        if (siteId.HasValue)
        {
            query = query.Where(x => x.SiteId == siteId.Value);
        }

        if (systemId.HasValue)
        {
            query = query.Where(x => x.SystemId == systemId.Value);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(x => x.AssetCategoryId == categoryId.Value);
        }

        if (normalizedStatus == "active")
        {
            query = query.Where(x => x.Status != "Inactive");
        }
        else if (normalizedStatus == "inactive")
        {
            query = query.Where(x => x.Status == "Inactive");
        }

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var searchLower = normalizedSearch.ToLower();
            query = query.Where(x =>
                x.AssetName.ToLower().Contains(searchLower)
                || x.AssetCode.ToLower().Contains(searchLower)
                || (x.SerialNumber != null && x.SerialNumber.ToLower().Contains(searchLower))
                || (x.Manufacturer != null && x.Manufacturer.ToLower().Contains(searchLower))
                || (x.Model != null && x.Model.ToLower().Contains(searchLower))
                || (x.Location != null && x.Location.ToLower().Contains(searchLower))
                || (x.Client != null && x.Client.ClientName.ToLower().Contains(searchLower)));
        }

        var ordered = query.OrderBy(x => x.AssetName);

        if (outOfSupportOnly)
        {
            var today = DateTime.UtcNow.Date;
            var candidates = await ordered
                .Where(x => !x.WarrantyExpiryDate.HasValue || x.WarrantyExpiryDate.Value < today)
                .ToListAsync(cancellationToken);
            var contracts = await LoadSupportingContractsAsync(candidates.Select(x => x.ClientId), cancellationToken);
            var unsupported = candidates
                .Where(x => !IsCoveredByContract(x, contracts))
                .Select(x => Map(x, hasContractCover: false))
                .ToList();

            return QueryablePagingExtensions.IsPagingRequested(page, pageSize)
                ? Ok(unsupported.ToPagedResult(page, pageSize))
                : Ok(unsupported);
        }

        if (QueryablePagingExtensions.IsPagingRequested(page, pageSize))
        {
            var paged = await ordered.ToPagedResultAsync(page, pageSize, cancellationToken);
            var pageContracts = await LoadSupportingContractsAsync(paged.Items.Select(x => x.ClientId), cancellationToken);
            return Ok(paged.Map(x => Map(x, IsCoveredByContract(x, pageContracts))));
        }

        var assets = await ordered.ToListAsync(cancellationToken);
        var allContracts = await LoadSupportingContractsAsync(assets.Select(x => x.ClientId), cancellationToken);
        return Ok(assets.Select(x => Map(x, IsCoveredByContract(x, allContracts))).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AssetResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);

        var asset = await (await ApplyOrgScopeAsync(LoadAssetForResponse()
            .Where(x => x.TenantId == TenantId && x.Id == id)
            .WhereAccessible(scope, x => x.BranchId), cancellationToken))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Asset was not found.");

        return Ok(await MapWithCoverageAsync(asset, cancellationToken));
    }

    /// <summary>
    /// Retires (decommissions) an asset that is no longer supported: it leaves the active register,
    /// its automatic PM plan is stopped, and the date and reason are kept for history. The record
    /// and its work order history are preserved and it can be reactivated.
    /// </summary>
    [HttpPost("{id:guid}/retire")]
    public async Task<ActionResult<AssetResponse>> Retire(Guid id, [FromBody] RetireAssetRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageAssets);
        var reason = ValidateRetireRequest(request);
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);

        var asset = await LoadAssetForResponse()
            .Where(x => x.TenantId == TenantId && x.Id == id)
            .WhereAccessible(scope, x => x.BranchId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Asset was not found.");

        if (string.Equals(asset.Status, "Inactive", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("Asset is already retired.");
        }

        await RetireAsync(asset, request.DecommissionDate, reason, cancellationToken);
        return Ok(await MapWithCoverageAsync(asset, cancellationToken));
    }

    /// <summary>
    /// Turns the asset's automatic preventive maintenance on or off without editing the whole asset.
    /// On needs a maintenance frequency and a next due date; it starts (or restarts) the asset's own
    /// PM schedule. Off stops it.
    /// </summary>
    [HttpPost("{id:guid}/auto-schedule-pm")]
    public async Task<ActionResult<AssetResponse>> SetAutoSchedulePm(Guid id, [FromBody] SetAutoScheduleRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageAssets);
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var asset = await LoadAssetForResponse()
            .Where(x => x.TenantId == TenantId && x.Id == id)
            .WhereAccessible(scope, x => x.BranchId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Asset was not found.");

        if (request.Enabled)
        {
            if (string.Equals(asset.Status, "Inactive", StringComparison.OrdinalIgnoreCase))
                throw new BusinessRuleException("A retired asset can't be scheduled. Reactivate it first.");
            if (string.IsNullOrWhiteSpace(asset.RecommendedPmFrequency) || !asset.NextPmDate.HasValue)
                throw new BusinessRuleException("Set a maintenance frequency and a last maintenance or installation date on the asset first (Edit), so the next due date is known.");
        }

        if (asset.AutoSchedulePm != request.Enabled)
        {
            asset.AutoSchedulePm = request.Enabled;
            await dbContext.SaveChangesAsync(cancellationToken);
            await preventiveMaintenancePlanService.SyncForAssetAsync(asset, cancellationToken);
            await auditLogService.LogAsync(TenantId, UserId, request.Enabled ? "asset.auto-schedule-pm.on" : "asset.auto-schedule-pm.off",
                nameof(Asset), asset.Id.ToString(),
                $"Automatic PM turned {(request.Enabled ? "on" : "off")} for asset '{asset.AssetName}'.", cancellationToken);
        }

        return Ok(await MapWithCoverageAsync(asset, cancellationToken));
    }

    /// <summary>Retires several assets at once, e.g. everything the "out of support" view lists.</summary>
    [HttpPost("retire")]
    public async Task<ActionResult<BulkRetireAssetsResponse>> RetireMany([FromBody] BulkRetireAssetsRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageAssets);
        var reason = ValidateRetireRequest(new RetireAssetRequest(request.DecommissionDate, request.Reason));
        var ids = (request.AssetIds ?? []).Distinct().ToList();
        if (ids.Count == 0)
        {
            throw new BusinessRuleException("Select at least one asset to retire.");
        }

        if (ids.Count > MaxBulkRetire)
        {
            throw new BusinessRuleException($"Retire at most {MaxBulkRetire} assets at a time.");
        }

        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var assets = await dbContext.Assets
            .Where(x => x.TenantId == TenantId && ids.Contains(x.Id) && x.Status != "Inactive")
            .WhereAccessible(scope, x => x.BranchId)
            .ToListAsync(cancellationToken);

        foreach (var asset in assets)
        {
            await RetireAsync(asset, request.DecommissionDate, reason, cancellationToken);
        }

        return Ok(new BulkRetireAssetsResponse(assets.Count, ids.Count - assets.Count));
    }

    [HttpPost("{id:guid}/reactivate")]
    public async Task<ActionResult<AssetResponse>> Reactivate(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageAssets);
        await licenseGuardService.EnsureTenantCanMutateAsync(TenantId, cancellationToken);
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);

        var asset = await LoadAssetForResponse()
            .Where(x => x.TenantId == TenantId && x.Id == id)
            .WhereAccessible(scope, x => x.BranchId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Asset was not found.");

        if (!string.Equals(asset.Status, "Inactive", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("Asset is not retired.");
        }

        if (asset.DisposalDate.HasValue)
        {
            throw new BusinessRuleException("A disposed asset cannot be reactivated.");
        }

        asset.Status = "Active";
        asset.DecommissionDate = null;
        asset.DecommissionReason = null;
        assetLifecycleService.Record(asset, AssetLifecycleEventTypes.Reactivated, UserId, UserDisplayName);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Asset reactivated",
            nameof(Asset),
            asset.Id.ToString(),
            $"Reactivated asset '{asset.AssetName}'.",
            cancellationToken);

        return Ok(await MapWithCoverageAsync(asset, cancellationToken));
    }

    private const int MaxBulkRetire = 500;

    private static string ValidateRetireRequest(RetireAssetRequest request)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new BusinessRuleException("Give a reason for retiring the asset.");
        }

        if (request.DecommissionDate.HasValue && request.DecommissionDate.Value.Date > DateTime.UtcNow.Date.AddDays(1))
        {
            throw new BusinessRuleException("Decommission date cannot be in the future.");
        }

        return reason;
    }

    private async Task RetireAsync(Asset asset, DateTime? decommissionDate, string reason, CancellationToken cancellationToken)
    {
        asset.Status = "Inactive";
        asset.DecommissionDate = decommissionDate ?? DateTime.UtcNow.Date;
        asset.DecommissionReason = reason;
        asset.AutoSchedulePm = false;
        assetLifecycleService.Record(asset, AssetLifecycleEventTypes.Decommissioned, UserId, UserDisplayName, reason, asset.DecommissionDate);
        await dbContext.SaveChangesAsync(cancellationToken);
        await preventiveMaintenancePlanService.SyncForAssetAsync(asset, cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Asset retired",
            nameof(Asset),
            asset.Id.ToString(),
            $"Retired asset '{asset.AssetName}': {reason}",
            cancellationToken);
    }

    private IQueryable<Asset> LoadAssetForResponse() =>
        dbContext.Assets
            .Include(x => x.Client)
            .Include(x => x.Branch)
            .Include(x => x.Site)
            .Include(x => x.System)
            .Include(x => x.AssetCategory)
            .Include(x => x.OrgNode)
            .Include(x => x.ParentAsset)
            .Include(x => x.CustomFieldValues)
                .ThenInclude(x => x.FieldDefinition);

    /// <summary>Users limited to some factories or sections only see assets located there.</summary>
    private async Task<IQueryable<Asset>> ApplyOrgScopeAsync(IQueryable<Asset> query, CancellationToken cancellationToken)
    {
        var orgScope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        if (orgScope.Nodes is null)
        {
            return query;
        }

        var nodes = orgScope.Nodes.ToList();
        return query.Where(x => x.OrgNodeId.HasValue && nodes.Contains(x.OrgNodeId.Value));
    }

    private static string? NormalizeCriticality(string? value)
    {
        try
        {
            return AssetCriticality.Normalize(value);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }
    }

    private static string NormalizeOperationalStatus(string? value, string current) =>
        string.IsNullOrWhiteSpace(value)
            ? current
            : AssetOperationalStatuses.Find(value)
              ?? throw new BusinessRuleException($"Operational status must be one of: {string.Join(", ", AssetOperationalStatuses.All)}.");

    /// <summary>
    /// Checks the factory fields of a create or update and returns the location to use. A component
    /// with no location of its own takes its parent's.
    /// </summary>
    private async Task<(OrgNode? Location, Asset? Parent)> ResolveFactoryPlacementAsync(Guid? assetId, UpsertAssetRequest request, CancellationToken cancellationToken)
    {
        if (request.LostProductionPerHour is < 0)
        {
            throw new BusinessRuleException("Lost production per hour cannot be negative.");
        }

        var parent = request.ParentAssetId.HasValue
            ? await assetLifecycleService.EnsureParentAsync(TenantId, assetId, request.ParentAssetId.Value, cancellationToken)
            : null;
        var locationId = request.OrgNodeId ?? parent?.OrgNodeId;
        var location = locationId.HasValue
            ? await assetLifecycleService.EnsureLocationAsync(TenantId, UserId, locationId.Value, cancellationToken)
            : null;

        if (!request.ClientId.HasValue && location is null)
        {
            throw new BusinessRuleException("Choose where the asset is located (factory, section or sub-section).");
        }

        if (location is null)
        {
            // A client asset (ServiceOps) is visible only to users who see the whole organisation.
            var orgScope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
            if (!orgScope.IsUnrestricted)
            {
                throw new BusinessRuleException("Choose where the asset is located (factory, section or sub-section).");
            }
        }

        return (location, parent);
    }

    /// <summary>
    /// Contracts that currently provide cover: in term (or within their grace period) and in a
    /// lifecycle state that allows service. Mirrors the contract coverage service's eligibility
    /// rules, without the work-order-type matching that only applies to a specific job.
    /// </summary>
    private async Task<List<Contract>> LoadSupportingContractsAsync(IEnumerable<Guid?> clientIds, CancellationToken cancellationToken)
    {
        var ids = clientIds.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var today = DateTime.UtcNow.Date;
        var contracts = await dbContext.Contracts
            .AsNoTracking()
            .Include(x => x.Sites)
            .Include(x => x.Assets)
            .Include(x => x.AssetCategories)
                .ThenInclude(x => x.AssetCategory)
            .Where(x => x.TenantId == TenantId
                && ids.Contains(x.ClientId)
                && !x.IsArchived
                && x.StartDate <= today.AddDays(1)
                && (x.Status == ContractStatus.Active
                    || x.Status == ContractStatus.ExpiringSoon
                    || x.Status == ContractStatus.RenewalInProgress
                    || x.Status == ContractStatus.Expired))
            .ToListAsync(cancellationToken);

        return contracts.Where(x => ContractAssetScope.IsInForce(x, today)).ToList();
    }

    private static bool IsCoveredByContract(Asset asset, IReadOnlyCollection<Contract> contracts) =>
        FindCoveringContract(asset, contracts) is not null;

    /// <summary>The contract covering this asset; see <see cref="ContractAssetScope.FindCovering"/>.</summary>
    public static (Contract Contract, string Scope)? FindCoveringContract(Asset asset, IReadOnlyCollection<Contract> contracts) =>
        ContractAssetScope.FindCovering(asset, contracts);

    private async Task<AssetResponse> MapWithCoverageAsync(Asset asset, CancellationToken cancellationToken)
    {
        var contracts = await LoadSupportingContractsAsync([asset.ClientId], cancellationToken);
        var cover = FindCoveringContract(asset, contracts);
        return Map(asset, cover is not null) with
        {
            CoveredBy = cover is { } found
                ? new AssetContractCoverResponse(found.Contract.Id, found.Contract.ContractNumber, found.Contract.Name, found.Contract.Status.ToString(), found.Contract.EndDate, found.Scope)
                : null,
        };
    }

    private static bool NormalizeSupportFilter(string? support)
    {
        if (string.IsNullOrWhiteSpace(support))
        {
            return false;
        }

        return support.Trim().ToLowerInvariant() switch
        {
            "out" or "out-of-support" or "unsupported" => true,
            "all" => false,
            _ => throw new BusinessRuleException("Support filter must be out or all.")
        };
    }

    [HttpPost]
    public async Task<ActionResult<AssetResponse>> Create([FromBody] UpsertAssetRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageAssets);
        await licenseGuardService.EnsureTenantCanMutateAsync(TenantId, cancellationToken);
        Validate(request);
        await EnsureClientPlacementAsync(request, cancellationToken);
        await EnsureCategoryRequestIsValidAsync(request.AssetCategoryId, request.CustomFieldValues, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.AssetCode))
        {
            await EnsureUniqueAssetCodeAsync(request.AssetCode, null, cancellationToken);
        }

        var (location, parent) = await ResolveFactoryPlacementAsync(null, request, cancellationToken);

        var branchId = await branchAccessService.ResolveBranchIdForWriteAsync(TenantId, request.BranchId, cancellationToken);
        var (frequencyValue, frequencyUnit) = ResolvePmFrequency(request);

        var asset = new Asset
        {
            TenantId = TenantId,
            BranchId = branchId,
            ClientId = request.ClientId,
            SiteId = request.SiteId,
            SystemId = request.SystemId,
            AssetCategoryId = request.AssetCategoryId,
            AssetName = request.AssetName.Trim(),
            AssetCode = string.IsNullOrWhiteSpace(request.AssetCode)
                ? await GenerateUniqueAssetCodeAsync(branchId, cancellationToken)
                : request.AssetCode.Trim(),
            AssetType = request.AssetType?.Trim(),
            OwnershipType = NormalizeOwnershipType(request.OwnershipType),
            Location = request.Location?.Trim(),
            SerialNumber = request.SerialNumber?.Trim(),
            Manufacturer = request.Manufacturer?.Trim(),
            Model = request.Model?.Trim(),
            InstallationDate = request.InstallationDate,
            WarrantyStartDate = ResolveWarrantyStartDate(request),
            WarrantyPeriodMonths = request.WarrantyPeriodMonths,
            WarrantyExpiryDate = ResolveWarrantyExpiryDate(request),
            RecommendedPmFrequency = BuildPmFrequencyLabel(request.RecommendedPmFrequency, frequencyValue, frequencyUnit),
            PmFrequencyValue = frequencyValue,
            PmFrequencyUnit = NormalizePmFrequencyUnit(frequencyUnit),
            AutoSchedulePm = request.AutoSchedulePm,
            LastPmDate = request.LastPmDate,
            NextPmDate = request.NextPmDate ?? CalculateNextPmDate(request.LastPmDate, request.InstallationDate, frequencyValue, frequencyUnit),
            Notes = request.Notes?.Trim(),
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Active" : request.Status.Trim(),
            OrgNodeId = location?.Id,
            ParentAssetId = parent?.Id,
            Criticality = NormalizeCriticality(request.Criticality),
            OperationalStatus = NormalizeOperationalStatus(request.OperationalStatus, AssetOperationalStatuses.Operational),
            LostProductionPerHour = request.LostProductionPerHour,
            CommissioningDate = request.CommissioningDate,
            ExternalId = request.ExternalId?.Trim() is { Length: > 0 } externalId ? externalId : null,
            ExternalSystem = request.ExternalSystem?.Trim() is { Length: > 0 } externalSystem ? externalSystem : null
        };

        dbContext.Assets.Add(asset);
        await assetClassService.ApplyDefaultsAsync(asset, cancellationToken);
        var registered = assetLifecycleService.Record(asset, AssetLifecycleEventTypes.Registered, UserId, UserDisplayName,
            location is null ? null : $"Registered at {location.Name}.");
        registered.ToValue = location?.Name;
        if (parent is not null)
        {
            var attached = assetLifecycleService.Record(asset, AssetLifecycleEventTypes.ComponentAttached, UserId, UserDisplayName, $"Component of {parent.AssetCode}.");
            attached.ToValue = parent.AssetCode;
        }

        if (asset.CommissioningDate.HasValue)
        {
            assetLifecycleService.Record(asset, AssetLifecycleEventTypes.Commissioned, UserId, UserDisplayName, null, asset.CommissioningDate);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await SaveCustomFieldValuesAsync(asset, request.CustomFieldValues, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await preventiveMaintenancePlanService.SyncForAssetAsync(asset, cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Asset created",
            nameof(Asset),
            asset.Id.ToString(),
            $"Created asset '{asset.AssetName}'.",
            cancellationToken);

        await dbContext.Entry(asset).Reference(x => x.Client).LoadAsync(cancellationToken);
        if (asset.OrgNodeId.HasValue)
        {
            await dbContext.Entry(asset).Reference(x => x.OrgNode).LoadAsync(cancellationToken);
        }
        if (asset.ParentAssetId.HasValue)
        {
            await dbContext.Entry(asset).Reference(x => x.ParentAsset).LoadAsync(cancellationToken);
        }
        if (asset.BranchId.HasValue)
        {
            await dbContext.Entry(asset).Reference(x => x.Branch).LoadAsync(cancellationToken);
        }
        if (asset.SiteId.HasValue)
        {
            await dbContext.Entry(asset).Reference(x => x.Site).LoadAsync(cancellationToken);
        }
        if (asset.SystemId.HasValue)
        {
            await dbContext.Entry(asset).Reference(x => x.System).LoadAsync(cancellationToken);
        }
        if (asset.AssetCategoryId.HasValue)
        {
            await dbContext.Entry(asset).Reference(x => x.AssetCategory).LoadAsync(cancellationToken);
        }
        await dbContext.Entry(asset).Collection(x => x.CustomFieldValues).Query().Include(x => x.FieldDefinition).LoadAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = asset.Id }, await MapWithCoverageAsync(asset, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AssetResponse>> Update(Guid id, [FromBody] UpsertAssetRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageAssets);
        Validate(request);
        var asset = await (await ApplyOrgScopeAsync(dbContext.Assets
            .Include(x => x.Client)
            .Include(x => x.Branch)
            .Include(x => x.Site)
            .Include(x => x.System)
            .Include(x => x.AssetCategory)
            .Include(x => x.CustomFieldValues)
            .Where(x => x.TenantId == TenantId && x.Id == id), cancellationToken))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Asset was not found.");

        await EnsureClientPlacementAsync(request, cancellationToken);
        await EnsureCategoryRequestIsValidAsync(request.AssetCategoryId, request.CustomFieldValues, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.AssetCode))
        {
            await EnsureUniqueAssetCodeAsync(request.AssetCode, asset.Id, cancellationToken);
        }

        var (location, parent) = await ResolveFactoryPlacementAsync(asset.Id, request, cancellationToken);
        var previousParent = asset.ParentAssetId;
        var previousStatus = asset.OperationalStatus;
        if (location is not null && location.Id != asset.OrgNodeId)
        {
            await assetLifecycleService.TransferAsync(asset, location, UserId, UserDisplayName, null, null, cancellationToken);
        }
        else if (location is null)
        {
            asset.OrgNodeId = null;
        }

        asset.ParentAssetId = parent?.Id;
        if (previousParent != asset.ParentAssetId)
        {
            if (previousParent.HasValue)
            {
                var detached = assetLifecycleService.Record(asset, AssetLifecycleEventTypes.ComponentDetached, UserId, UserDisplayName);
                detached.FromParentAssetId = previousParent;
                detached.FromValue = await dbContext.Assets.Where(x => x.Id == previousParent).Select(x => x.AssetCode).SingleOrDefaultAsync(cancellationToken);
            }

            if (parent is not null)
            {
                var attached = assetLifecycleService.Record(asset, AssetLifecycleEventTypes.ComponentAttached, UserId, UserDisplayName, $"Component of {parent.AssetCode}.");
                attached.ToValue = parent.AssetCode;
            }
        }

        asset.Criticality = NormalizeCriticality(request.Criticality);
        asset.OperationalStatus = NormalizeOperationalStatus(request.OperationalStatus, asset.OperationalStatus);
        if (previousStatus != asset.OperationalStatus)
        {
            var changed = assetLifecycleService.Record(asset, AssetLifecycleEventTypes.StatusChanged, UserId, UserDisplayName);
            changed.FromValue = previousStatus;
            changed.ToValue = asset.OperationalStatus;
        }

        asset.LostProductionPerHour = request.LostProductionPerHour;
        if (request.CommissioningDate.HasValue && asset.CommissioningDate != request.CommissioningDate)
        {
            assetLifecycleService.Record(asset, AssetLifecycleEventTypes.Commissioned, UserId, UserDisplayName, null, request.CommissioningDate);
        }

        asset.CommissioningDate = request.CommissioningDate ?? asset.CommissioningDate;
        asset.ExternalId = request.ExternalId?.Trim() is { Length: > 0 } externalId ? externalId : null;
        asset.ExternalSystem = request.ExternalSystem?.Trim() is { Length: > 0 } externalSystem ? externalSystem : null;

        var branchId = await branchAccessService.ResolveBranchIdForWriteAsync(TenantId, request.BranchId ?? asset.BranchId, cancellationToken);

        asset.BranchId = branchId;
        asset.ClientId = request.ClientId;
        asset.SiteId = request.SiteId;
        asset.SystemId = request.SystemId;
        asset.AssetCategoryId = request.AssetCategoryId;
        asset.AssetName = request.AssetName.Trim();
        asset.AssetCode = string.IsNullOrWhiteSpace(request.AssetCode) ? asset.AssetCode : request.AssetCode.Trim();
        asset.AssetType = request.AssetType?.Trim();
        asset.OwnershipType = NormalizeOwnershipType(request.OwnershipType);
        asset.Location = request.Location?.Trim();
        asset.SerialNumber = request.SerialNumber?.Trim();
        asset.Manufacturer = request.Manufacturer?.Trim();
        asset.Model = request.Model?.Trim();
        asset.InstallationDate = request.InstallationDate;
        asset.WarrantyStartDate = ResolveWarrantyStartDate(request);
        asset.WarrantyPeriodMonths = request.WarrantyPeriodMonths;
        asset.WarrantyExpiryDate = ResolveWarrantyExpiryDate(request);
        var (frequencyValue, frequencyUnit) = ResolvePmFrequency(request);
        asset.RecommendedPmFrequency = BuildPmFrequencyLabel(request.RecommendedPmFrequency, frequencyValue, frequencyUnit);
        asset.PmFrequencyValue = frequencyValue;
        asset.PmFrequencyUnit = NormalizePmFrequencyUnit(frequencyUnit);
        asset.AutoSchedulePm = request.AutoSchedulePm;
        asset.LastPmDate = request.LastPmDate;
        asset.NextPmDate = request.NextPmDate ?? CalculateNextPmDate(request.LastPmDate, request.InstallationDate, frequencyValue, frequencyUnit);
        asset.Notes = request.Notes?.Trim();
        asset.Status = string.IsNullOrWhiteSpace(request.Status) ? asset.Status : request.Status.Trim();

        await SaveCustomFieldValuesAsync(asset, request.CustomFieldValues, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await preventiveMaintenancePlanService.SyncForAssetAsync(asset, cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Asset updated",
            nameof(Asset),
            asset.Id.ToString(),
            $"Updated asset '{asset.AssetName}'.",
            cancellationToken);

        await dbContext.Entry(asset).Reference(x => x.Client).LoadAsync(cancellationToken);
        if (asset.OrgNodeId.HasValue)
        {
            await dbContext.Entry(asset).Reference(x => x.OrgNode).LoadAsync(cancellationToken);
        }
        if (asset.ParentAssetId.HasValue)
        {
            await dbContext.Entry(asset).Reference(x => x.ParentAsset).LoadAsync(cancellationToken);
        }
        if (asset.BranchId.HasValue)
        {
            await dbContext.Entry(asset).Reference(x => x.Branch).LoadAsync(cancellationToken);
        }
        if (asset.SiteId.HasValue)
        {
            await dbContext.Entry(asset).Reference(x => x.Site).LoadAsync(cancellationToken);
        }
        if (asset.SystemId.HasValue)
        {
            await dbContext.Entry(asset).Reference(x => x.System).LoadAsync(cancellationToken);
        }
        if (asset.AssetCategoryId.HasValue)
        {
            await dbContext.Entry(asset).Reference(x => x.AssetCategory).LoadAsync(cancellationToken);
        }
        await dbContext.Entry(asset).Collection(x => x.CustomFieldValues).Query().Include(x => x.FieldDefinition).LoadAsync(cancellationToken);

        return Ok(await MapWithCoverageAsync(asset, cancellationToken));
    }

    /// <summary>ServiceOps client placement (client, site, system). Factory assets have no client and no site or system.</summary>
    private async Task EnsureClientPlacementAsync(UpsertAssetRequest request, CancellationToken cancellationToken)
    {
        if (request.ClientId is not { } clientId)
        {
            if (request.SiteId.HasValue || request.SystemId.HasValue)
            {
                throw new BusinessRuleException("Sites and systems belong to clients; factory assets use their factory location instead.");
            }

            return;
        }

        await EnsureClientExistsAsync(clientId, cancellationToken);
        await EnsureSiteIsValidAsync(clientId, request.SiteId, cancellationToken);
        await EnsureSystemIsValidAsync(clientId, request.SiteId, request.SystemId, cancellationToken);
    }

    private async Task EnsureSiteIsValidAsync(Guid clientId, Guid? siteId, CancellationToken cancellationToken)
    {
        if (!siteId.HasValue)
        {
            return;
        }

        var site = await dbContext.Sites
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == siteId.Value, cancellationToken)
            ?? throw new BusinessRuleException("Site does not exist for this tenant.");

        if (site.ClientId != clientId)
        {
            throw new BusinessRuleException("Site does not belong to the selected client.");
        }

        if (!string.Equals(site.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("Select an active site for this asset.");
        }
    }

    private async Task EnsureSystemIsValidAsync(Guid clientId, Guid? siteId, Guid? systemId, CancellationToken cancellationToken)
    {
        if (!systemId.HasValue)
        {
            return;
        }

        var system = await dbContext.Systems.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == systemId.Value, cancellationToken)
            ?? throw new BusinessRuleException("System does not exist for this tenant.");

        if (system.ClientId != clientId)
        {
            throw new BusinessRuleException("System does not belong to the selected client.");
        }

        if (siteId.HasValue && system.SiteId != siteId.Value)
        {
            throw new BusinessRuleException("System does not belong to the selected site.");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageAssets);

        var asset = await dbContext.Assets.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Asset was not found.");

        asset.Status = "Inactive";
        asset.DecommissionDate ??= DateTime.UtcNow.Date;
        asset.AutoSchedulePm = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        await preventiveMaintenancePlanService.SyncForAssetAsync(asset, cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Asset deleted",
            nameof(Asset),
            asset.Id.ToString(),
            $"Soft deleted asset '{asset.AssetName}'.",
            cancellationToken);

        return NoContent();
    }

    private static void Validate(UpsertAssetRequest request)
    {
        if (request.ClientId == Guid.Empty)
        {
            throw new BusinessRuleException("Client is invalid.");
        }

        if (string.IsNullOrWhiteSpace(request.AssetName))
        {
            throw new BusinessRuleException("Asset name is required.");
        }

        if (!string.IsNullOrWhiteSpace(request.Status) && !AllowedStatuses.Contains(request.Status.Trim()))
        {
            throw new BusinessRuleException("Invalid asset status.");
        }

        if (request.PmFrequencyValue.HasValue && request.PmFrequencyValue.Value <= 0)
        {
            throw new BusinessRuleException("PM frequency value must be greater than zero.");
        }

        if (request.LastPmDate.HasValue && request.LastPmDate.Value.Date > DateTime.UtcNow.Date.AddDays(1))
        {
            throw new BusinessRuleException("Last maintenance date cannot be in the future.");
        }

        if (request.WarrantyPeriodMonths is <= 0 or > MaxWarrantyPeriodMonths)
        {
            throw new BusinessRuleException($"Warranty period must be between 1 and {MaxWarrantyPeriodMonths} months.");
        }

        var warrantyStart = ResolveWarrantyStartDate(request);
        var warrantyExpiry = ResolveWarrantyExpiryDate(request);
        if (warrantyStart.HasValue && warrantyExpiry.HasValue && warrantyExpiry.Value.Date < warrantyStart.Value.Date)
        {
            throw new BusinessRuleException("Warranty expiry date cannot be before the warranty start date.");
        }
    }

    private const int MaxWarrantyPeriodMonths = 600;

    /// <summary>Warranty cover normally starts at installation unless a separate start date is given.</summary>
    private static DateTime? ResolveWarrantyStartDate(UpsertAssetRequest request) =>
        request.WarrantyStartDate
        ?? (request.WarrantyPeriodMonths.HasValue || request.WarrantyExpiryDate.HasValue ? request.InstallationDate : null);

    /// <summary>
    /// An explicit expiry date wins; otherwise the expiry is derived from the warranty start plus the
    /// warranty period, so entering "24 months" is enough to track the cover.
    /// </summary>
    private static DateTime? ResolveWarrantyExpiryDate(UpsertAssetRequest request)
    {
        if (request.WarrantyExpiryDate.HasValue)
        {
            return request.WarrantyExpiryDate;
        }

        var start = ResolveWarrantyStartDate(request);
        return start.HasValue && request.WarrantyPeriodMonths is > 0
            ? start.Value.AddMonths(request.WarrantyPeriodMonths.Value)
            : null;
    }

    private static string ResolveWarrantyStatus(DateTime? warrantyExpiryDate)
    {
        if (!warrantyExpiryDate.HasValue)
        {
            return "No Warranty";
        }

        var today = DateTime.UtcNow.Date;
        var expiry = warrantyExpiryDate.Value.Date;
        if (expiry < today)
        {
            return "Warranty Expired";
        }

        return expiry <= today.AddDays(30) ? "Warranty Expiring Soon" : "Under Warranty";
    }

    private static string NormalizeOwnershipType(string? ownershipType) =>
        string.IsNullOrWhiteSpace(ownershipType) ? "ClientOwned" : ownershipType.Trim();

    private static string? NormalizePmFrequencyUnit(string? unit)
    {
        if (string.IsNullOrWhiteSpace(unit))
        {
            return null;
        }

        return unit.Trim().ToLowerInvariant() switch
        {
            "day" or "days" => "days",
            "week" or "weeks" => "weeks",
            "month" or "months" => "months",
            "quarter" or "quarters" => "quarters",
            "year" or "years" => "years",
            _ => throw new BusinessRuleException("PM frequency unit must be days, weeks, months, quarters, or years.")
        };
    }

    private static string? BuildPmFrequencyLabel(string? currentLabel, int? value, string? unit)
    {
        if (value.HasValue && !string.IsNullOrWhiteSpace(unit))
        {
            return $"{value.Value} {NormalizePmFrequencyUnit(unit)}";
        }

        return string.IsNullOrWhiteSpace(currentLabel) ? null : currentLabel.Trim();
    }

    /// <summary>
    /// The next PM is one interval after the last one. With no PM on record the schedule is anchored
    /// to the installation date and rolled forward to the first due date from today, so an asset
    /// installed years ago is not reported as overdue for a service nobody has missed.
    /// </summary>
    private static DateTime? CalculateNextPmDate(DateTime? lastPmDate, DateTime? installationDate, int? frequencyValue, string? frequencyUnit)
    {
        if (!frequencyValue.HasValue || string.IsNullOrWhiteSpace(frequencyUnit))
        {
            return null;
        }

        if (frequencyValue.Value <= 0)
        {
            throw new BusinessRuleException("PM frequency value must be greater than zero.");
        }

        var unit = NormalizePmFrequencyUnit(frequencyUnit);
        if (lastPmDate.HasValue)
        {
            return AddPmInterval(lastPmDate.Value, frequencyValue.Value, unit);
        }

        if (!installationDate.HasValue)
        {
            return null;
        }

        var today = DateTime.UtcNow.Date;
        var anchor = installationDate.Value;
        DateTime? next = null;
        // Step from the anchor (rather than adding repeatedly) so month-end dates do not drift.
        for (var periods = 1; periods <= 100_000; periods++)
        {
            next = AddPmInterval(anchor, frequencyValue.Value * periods, unit);
            if (next is null || next.Value.Date >= today)
            {
                break;
            }
        }

        return next;
    }

    private static DateTime? AddPmInterval(DateTime from, int value, string? unit) => unit switch
    {
        "days" => from.AddDays(value),
        "weeks" => from.AddDays(value * 7),
        "months" => from.AddMonths(value),
        "quarters" => from.AddMonths(value * 3),
        "years" => from.AddYears(value),
        _ => null
    };

    /// <summary>
    /// Structured value + unit win; otherwise a free-text frequency such as "Monthly" or
    /// "Every 2 weeks" (older records, other clients) is converted so it drives the schedule.
    /// </summary>
    private static (int? Value, string? Unit) ResolvePmFrequency(UpsertAssetRequest request)
    {
        if (request.PmFrequencyValue.HasValue || !string.IsNullOrWhiteSpace(request.PmFrequencyUnit))
        {
            return (request.PmFrequencyValue, request.PmFrequencyUnit);
        }

        return PmFrequencyLabelParser.TryParse(request.RecommendedPmFrequency, out var value, out var unit)
            ? (value, unit)
            : (null, null);
    }

    private async Task EnsureClientExistsAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var clientExists = await dbContext.Clients.AnyAsync(x => x.TenantId == TenantId && x.IsActive && x.Id == clientId, cancellationToken);
        if (!clientExists)
        {
            throw new BusinessRuleException("Client does not exist for this tenant.");
        }
    }

    /// <summary>
    /// The numbering counter knows nothing about codes typed by hand or brought in by an import, so
    /// its next value can already be taken — and the (TenantId, AssetCode) unique index then fails the
    /// insert. Skip past any taken values; each call advances the counter, so this converges.
    /// </summary>
    private async Task<string> GenerateUniqueAssetCodeAsync(Guid? branchId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var candidate = await documentNumberingService.GenerateAsync(TenantId, branchId, DocumentTypes.Asset, cancellationToken);
            var normalized = candidate.ToLowerInvariant();
            if (!await dbContext.Assets.AnyAsync(x => x.TenantId == TenantId && x.AssetCode.ToLower() == normalized, cancellationToken))
            {
                return candidate;
            }
        }

        throw new BusinessRuleException("Could not generate a free asset code. Enter an asset code manually or adjust the Asset numbering rule.");
    }

    private async Task EnsureUniqueAssetCodeAsync(string assetCode, Guid? currentId, CancellationToken cancellationToken)
    {
        var normalizedCode = assetCode.Trim().ToLowerInvariant();
        var exists = await dbContext.Assets.AnyAsync(
            x => x.TenantId == TenantId
                && x.Id != currentId
                && x.AssetCode.ToLower() == normalizedCode,
            cancellationToken);

        if (exists)
        {
            throw new BusinessRuleException("Asset code must be unique per tenant.");
        }
    }

    private async Task EnsureCategoryRequestIsValidAsync(
        Guid? assetCategoryId,
        IReadOnlyCollection<UpsertAssetCustomFieldValueRequest>? customFieldValues,
        CancellationToken cancellationToken)
    {
        if (!assetCategoryId.HasValue)
        {
            if (customFieldValues is { Count: > 0 })
            {
                throw new BusinessRuleException("Asset category is required when custom field values are provided.");
            }

            return;
        }

        var category = await dbContext.AssetCategories
            .Include(x => x.Fields)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == assetCategoryId.Value && x.IsActive, cancellationToken)
            ?? throw new BusinessRuleException("Asset category does not exist for this tenant.");

        var requestedValues = customFieldValues ?? [];
        var fieldDefinitions = category.Fields.ToDictionary(x => x.Id, x => x);

        foreach (var item in requestedValues)
        {
            if (!fieldDefinitions.TryGetValue(item.FieldDefinitionId, out var definition))
            {
                throw new BusinessRuleException("One or more custom fields do not belong to the selected asset category.");
            }

            var normalizedValue = item.Value?.Trim() ?? string.Empty;
            if (definition.IsRequired && string.IsNullOrWhiteSpace(normalizedValue))
            {
                throw new BusinessRuleException($"Custom field '{definition.FieldLabel}' is required.");
            }
        }

        // A field that has been switched off is no longer asked for, so it cannot be required.
        foreach (var requiredDefinition in fieldDefinitions.Values.Where(x => x.IsRequired && x.IsActive))
        {
            var provided = requestedValues.Any(x => x.FieldDefinitionId == requiredDefinition.Id && !string.IsNullOrWhiteSpace(x.Value));
            if (!provided)
            {
                throw new BusinessRuleException($"Custom field '{requiredDefinition.FieldLabel}' is required.");
            }
        }
    }

    private async Task SaveCustomFieldValuesAsync(
        Asset asset,
        IReadOnlyCollection<UpsertAssetCustomFieldValueRequest>? customFieldValues,
        CancellationToken cancellationToken)
    {
        var existingValues = await dbContext.AssetCustomFieldValues
            .Where(x => x.TenantId == TenantId && x.AssetId == asset.Id)
            .ToListAsync(cancellationToken);

        dbContext.AssetCustomFieldValues.RemoveRange(existingValues);

        foreach (var item in customFieldValues ?? [])
        {
            var normalizedValue = item.Value?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedValue))
            {
                continue;
            }

            dbContext.AssetCustomFieldValues.Add(new AssetCustomFieldValue
            {
                TenantId = TenantId,
                AssetId = asset.Id,
                FieldDefinitionId = item.FieldDefinitionId,
                Value = normalizedValue
            });
        }
    }

    private static string ResolveSupportStatus(Asset asset, bool hasContractCover)
    {
        if (string.Equals(asset.Status, "Inactive", StringComparison.OrdinalIgnoreCase))
        {
            return "Retired";
        }

        if (asset.WarrantyExpiryDate.HasValue && asset.WarrantyExpiryDate.Value.Date >= DateTime.UtcNow.Date)
        {
            return "Under Warranty";
        }

        return hasContractCover ? "Under Contract" : "Out of Support";
    }

    private static AssetResponse Map(Asset asset, bool hasContractCover) =>
        new(
            asset.Id,
            asset.BranchId,
            asset.Branch?.Name,
            asset.ClientId,
            asset.Client?.ClientName,
            asset.SiteId,
            asset.Site?.SiteName,
            asset.SystemId,
            asset.System?.SystemName,
            asset.System?.SystemType,
            asset.AssetCategoryId,
            asset.AssetCategory?.Name,
            asset.AssetName,
            asset.AssetCode,
            asset.AssetType,
            asset.OwnershipType,
            asset.Location,
            asset.SerialNumber,
            asset.Manufacturer,
            asset.Model,
            asset.InstallationDate,
            asset.WarrantyExpiryDate,
            asset.WarrantyStartDate,
            asset.WarrantyPeriodMonths,
            asset.WarrantyExpiryDate.HasValue && asset.WarrantyExpiryDate.Value.Date >= DateTime.UtcNow.Date,
            ResolveWarrantyStatus(asset.WarrantyExpiryDate),
            asset.RecommendedPmFrequency,
            asset.PmFrequencyValue,
            asset.PmFrequencyUnit,
            asset.AutoSchedulePm,
            asset.LastPmDate,
            asset.NextPmDate,
            asset.Notes,
            asset.Status,
            asset.CreatedAt,
            asset.DecommissionDate,
            asset.DecommissionReason,
            hasContractCover,
            ResolveSupportStatus(asset, hasContractCover),
            asset.CustomFieldValues
                .OrderBy(x => x.FieldDefinition?.DisplayOrder ?? int.MaxValue)
                .ThenBy(x => x.FieldDefinition?.FieldLabel)
                .Select(x => new AssetCustomFieldValueResponse(
                    x.FieldDefinitionId,
                    x.FieldDefinition?.FieldName ?? string.Empty,
                    x.FieldDefinition?.FieldLabel ?? string.Empty,
                    x.FieldDefinition?.FieldType ?? "Text",
                    x.FieldDefinition?.Unit,
                    x.Value))
                .ToList())
        {
            OrgNodeId = asset.OrgNodeId,
            OrgNodeName = asset.OrgNode?.Name,
            OrgNodeLevel = asset.OrgNode?.Level,
            ParentAssetId = asset.ParentAssetId,
            ParentAssetCode = asset.ParentAsset?.AssetCode,
            ParentAssetName = asset.ParentAsset?.AssetName,
            Criticality = asset.Criticality,
            OperationalStatus = asset.OperationalStatus,
            LostProductionPerHour = asset.LostProductionPerHour,
            CommissioningDate = asset.CommissioningDate,
            DisposalDate = asset.DisposalDate,
            DisposalMethod = asset.DisposalMethod,
            ExternalId = asset.ExternalId,
            ExternalSystem = asset.ExternalSystem
        };

    private static string NormalizeStatusFilter(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "active";
        }

        return status.Trim().ToLowerInvariant() switch
        {
            "active" => "active",
            "inactive" => "inactive",
            "all" => "all",
            _ => throw new BusinessRuleException("Status must be active, inactive, or all.")
        };
    }

    private static string? NormalizeSearch(string? search) =>
        string.IsNullOrWhiteSpace(search) ? null : search.Trim();
}

public sealed record UpsertAssetRequest(
    Guid? ClientId,
    Guid? BranchId,
    Guid? SiteId,
    Guid? SystemId,
    Guid? AssetCategoryId,
    [StringLength(200)] string AssetName,
    [StringLength(100)] string? AssetCode,
    [StringLength(100)] string? AssetType,
    [StringLength(50)] string? OwnershipType,
    [StringLength(200)] string? Location,
    [StringLength(100)] string? SerialNumber,
    [StringLength(150)] string? Manufacturer,
    [StringLength(150)] string? Model,
    DateTime? InstallationDate,
    DateTime? WarrantyExpiryDate,
    [StringLength(50)] string? RecommendedPmFrequency,
    int? PmFrequencyValue,
    [StringLength(30)] string? PmFrequencyUnit,
    bool AutoSchedulePm,
    DateTime? LastPmDate,
    DateTime? NextPmDate,
    [StringLength(2000)] string? Notes,
    [StringLength(50)] string? Status,
    IReadOnlyCollection<UpsertAssetCustomFieldValueRequest>? CustomFieldValues,
    DateTime? WarrantyStartDate = null,
    int? WarrantyPeriodMonths = null,
    Guid? OrgNodeId = null,
    Guid? ParentAssetId = null,
    [StringLength(1)] string? Criticality = null,
    [StringLength(30)] string? OperationalStatus = null,
    decimal? LostProductionPerHour = null,
    DateTime? CommissioningDate = null,
    [StringLength(100)] string? ExternalId = null,
    [StringLength(50)] string? ExternalSystem = null);

public sealed record AssetResponse(
    Guid Id,
    Guid? BranchId,
    string? BranchName,
    Guid? ClientId,
    string? ClientName,
    Guid? SiteId,
    string? SiteName,
    Guid? SystemId,
    string? SystemName,
    string? SystemType,
    Guid? AssetCategoryId,
    string? AssetCategoryName,
    string AssetName,
    string AssetCode,
    string? AssetType,
    string? OwnershipType,
    string? Location,
    string? SerialNumber,
    string? Manufacturer,
    string? Model,
    DateTime? InstallationDate,
    DateTime? WarrantyExpiryDate,
    DateTime? WarrantyStartDate,
    int? WarrantyPeriodMonths,
    bool IsUnderWarranty,
    string WarrantyStatus,
    string? RecommendedPmFrequency,
    int? PmFrequencyValue,
    string? PmFrequencyUnit,
    bool AutoSchedulePm,
    DateTime? LastPmDate,
    DateTime? NextPmDate,
    string? Notes,
    string Status,
    DateTime CreatedAt,
    DateTime? DecommissionDate,
    string? DecommissionReason,
    bool HasContractCover,
    // "Under Warranty", "Under Contract", "Out of Support", or "Retired".
    string SupportStatus,
    IReadOnlyCollection<AssetCustomFieldValueResponse> CustomFieldValues)
{
    /// <summary>The contract covering this asset (detail views only; lists leave it null).</summary>
    public AssetContractCoverResponse? CoveredBy { get; init; }

    // Factory register
    public Guid? OrgNodeId { get; init; }
    public string? OrgNodeName { get; init; }
    public string? OrgNodeLevel { get; init; }
    public Guid? ParentAssetId { get; init; }
    public string? ParentAssetCode { get; init; }
    public string? ParentAssetName { get; init; }
    public string? Criticality { get; init; }
    public string OperationalStatus { get; init; } = AssetOperationalStatuses.Operational;
    public decimal? LostProductionPerHour { get; init; }
    public DateTime? CommissioningDate { get; init; }
    public DateTime? DisposalDate { get; init; }
    public string? DisposalMethod { get; init; }
    public string? ExternalId { get; init; }
    public string? ExternalSystem { get; init; }
}

/// <param name="Scope">How the contract reaches the asset: Asset (listed by name), Category, Site, or Client (all the client's assets).</param>
public sealed record AssetContractCoverResponse(Guid ContractId, string ContractNumber, string ContractName, string Status, DateTime EndDate, string Scope);

public sealed record SetAutoScheduleRequest(bool Enabled);

public sealed record RetireAssetRequest(
    DateTime? DecommissionDate,
    [StringLength(500)] string? Reason);

public sealed record BulkRetireAssetsRequest(
    IReadOnlyCollection<Guid>? AssetIds,
    DateTime? DecommissionDate,
    [StringLength(500)] string? Reason);

public sealed record BulkRetireAssetsResponse(int Retired, int Skipped);

public sealed record UpsertAssetCustomFieldValueRequest(
    Guid FieldDefinitionId,
    string? Value);

public sealed record AssetCustomFieldValueResponse(
    Guid FieldDefinitionId,
    string FieldName,
    string FieldLabel,
    string FieldType,
    string? Unit,
    string Value);
