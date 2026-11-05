using System.ComponentModel.DataAnnotations;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// Factory asset lifecycle: history, components, commissioning, transfers between sections or
/// factories, and disposal. Retiring and reactivating stay on <see cref="AssetsController"/>.
/// </summary>
[ApiController]
[Authorize]
[Route("api/assets/{assetId:guid}")]
public sealed class AssetLifecycleController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IOrgScopeService orgScopeService,
    IAssetLifecycleService assetLifecycleService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet("lifecycle")]
    public async Task<ActionResult<IReadOnlyCollection<AssetLifecycleEventResponse>>> History(Guid assetId, CancellationToken cancellationToken)
    {
        await LoadAsync(assetId, cancellationToken);
        var events = await dbContext.AssetLifecycleEvents
            .Where(x => x.TenantId == TenantId && x.AssetId == assetId)
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(events.Select(Map).ToList());
    }

    /// <summary>Components at every depth, each with its parent, so the client can draw the tree.</summary>
    [HttpGet("components")]
    public async Task<ActionResult<IReadOnlyCollection<AssetComponentResponse>>> Components(Guid assetId, CancellationToken cancellationToken)
    {
        await LoadAsync(assetId, cancellationToken);
        var components = await assetLifecycleService.LoadComponentTreeAsync(TenantId, assetId, cancellationToken);
        return Ok(components
            .OrderBy(x => x.AssetName)
            .Select(x => new AssetComponentResponse(x.Id, x.ParentAssetId, x.AssetCode, x.AssetName, x.Criticality, x.Status, x.OperationalStatus))
            .ToList());
    }

    [HttpPost("commission")]
    public async Task<ActionResult<AssetLifecycleEventResponse>> Commission(Guid assetId, [FromBody] CommissionAssetRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var asset = await LoadAsync(assetId, cancellationToken);
        EnsureActive(asset);
        var date = (request.CommissioningDate ?? DateTime.UtcNow).Date;
        if (date > DateTime.UtcNow.Date.AddDays(1))
        {
            throw new BusinessRuleException("Commissioning date cannot be in the future.");
        }

        asset.CommissioningDate = date;
        var entry = assetLifecycleService.Record(asset, AssetLifecycleEventTypes.Commissioned, UserId, UserDisplayName, request.Notes, date);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "asset.commissioned", nameof(Asset), asset.Id.ToString(),
            $"Commissioned asset '{asset.AssetName}' on {date:yyyy-MM-dd}.", cancellationToken);

        return Ok(Map(entry));
    }

    /// <summary>Moves the asset, and the components sitting with it, to another factory, section or sub-section.</summary>
    [HttpPost("transfer")]
    public async Task<ActionResult<TransferAssetResponse>> Transfer(Guid assetId, [FromBody] TransferAssetRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var asset = await LoadAsync(assetId, cancellationToken);
        EnsureActive(asset);
        if (asset.ParentAssetId.HasValue)
        {
            throw new BusinessRuleException("This is a component: detach it from its parent first, or transfer the parent.");
        }

        if (request.EffectiveDate.HasValue && request.EffectiveDate.Value.Date > DateTime.UtcNow.Date.AddDays(1))
        {
            throw new BusinessRuleException("Transfer date cannot be in the future.");
        }

        var destination = await assetLifecycleService.EnsureLocationAsync(TenantId, UserId, request.ToOrgNodeId, cancellationToken);
        if (destination.Id == asset.OrgNodeId)
        {
            throw new BusinessRuleException($"The asset is already at {destination.Name}.");
        }

        var moved = await assetLifecycleService.TransferAsync(asset, destination, UserId, UserDisplayName, request.Reason, request.EffectiveDate, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "asset.transferred", nameof(Asset), asset.Id.ToString(),
            $"Transferred asset '{asset.AssetName}' to {destination.Name} ({moved} asset(s) moved).", cancellationToken);

        return Ok(new TransferAssetResponse(asset.Id, destination.Id, destination.Name, moved));
    }

    /// <summary>Records final disposal of a retired asset (scrapped, sold, returned). It cannot be reactivated after this.</summary>
    [HttpPost("dispose")]
    public async Task<ActionResult<AssetLifecycleEventResponse>> Dispose(Guid assetId, [FromBody] DisposeAssetRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var asset = await LoadAsync(assetId, cancellationToken);
        if (!string.Equals(asset.Status, "Inactive", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("Retire the asset before recording its disposal.");
        }

        if (asset.DisposalDate.HasValue)
        {
            throw new BusinessRuleException("The asset's disposal is already recorded.");
        }

        if (string.IsNullOrWhiteSpace(request.Method))
        {
            throw new BusinessRuleException("Say how the asset was disposed of (for example scrapped, sold or returned to supplier).");
        }

        var date = (request.DisposalDate ?? DateTime.UtcNow).Date;
        if (date > DateTime.UtcNow.Date.AddDays(1))
        {
            throw new BusinessRuleException("Disposal date cannot be in the future.");
        }

        var activeComponents = (await assetLifecycleService.LoadComponentTreeAsync(TenantId, asset.Id, cancellationToken))
            .Count(x => !string.Equals(x.Status, "Inactive", StringComparison.OrdinalIgnoreCase));
        if (activeComponents > 0)
        {
            throw new BusinessRuleException($"{activeComponents} component(s) are still active. Detach or retire them first.");
        }

        asset.DisposalDate = date;
        asset.DisposalMethod = request.Method.Trim();
        var entry = assetLifecycleService.Record(asset, AssetLifecycleEventTypes.Disposed, UserId, UserDisplayName, request.Notes, date);
        entry.ToValue = asset.DisposalMethod;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "asset.disposed", nameof(Asset), asset.Id.ToString(),
            $"Disposed of asset '{asset.AssetName}' ({asset.DisposalMethod}).", cancellationToken);

        return Ok(Map(entry));
    }

    private void EnsureCanManage() => userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageAssets);

    private static void EnsureActive(Asset asset)
    {
        if (string.Equals(asset.Status, "Inactive", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("The asset is retired. Reactivate it first.");
        }
    }

    private async Task<Asset> LoadAsync(Guid assetId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var asset = await dbContext.Assets.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == assetId, cancellationToken)
            ?? throw new NotFoundException("Asset was not found.");

        var scope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        if (!scope.Allows(asset.OrgNodeId))
        {
            // Same answer as a missing asset, so scoped users can't probe other factories.
            throw new NotFoundException("Asset was not found.");
        }

        return asset;
    }

    private static AssetLifecycleEventResponse Map(AssetLifecycleEvent entry) => new(
        entry.Id,
        entry.EventType,
        entry.OccurredAt,
        entry.FromOrgNodeId,
        entry.ToOrgNodeId,
        entry.FromParentAssetId,
        entry.ToParentAssetId,
        entry.FromValue,
        entry.ToValue,
        entry.Notes,
        entry.PerformedByName);
}

public sealed record AssetLifecycleEventResponse(
    Guid Id,
    string EventType,
    DateTime OccurredAt,
    Guid? FromOrgNodeId,
    Guid? ToOrgNodeId,
    Guid? FromParentAssetId,
    Guid? ToParentAssetId,
    string? FromValue,
    string? ToValue,
    string? Notes,
    string? PerformedByName);

public sealed record AssetComponentResponse(Guid Id, Guid? ParentAssetId, string AssetCode, string AssetName, string? Criticality, string Status, string OperationalStatus);

public sealed record CommissionAssetRequest(DateTime? CommissioningDate, [StringLength(2000)] string? Notes);

public sealed record TransferAssetRequest(Guid ToOrgNodeId, DateTime? EffectiveDate, [StringLength(2000)] string? Reason);

public sealed record TransferAssetResponse(Guid AssetId, Guid ToOrgNodeId, string ToOrgNodeName, int AssetsMoved);

public sealed record DisposeAssetRequest(DateTime? DisposalDate, [StringLength(200)] string? Method, [StringLength(2000)] string? Notes);
