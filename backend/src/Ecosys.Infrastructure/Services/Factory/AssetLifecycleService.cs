using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services.Factory;

public interface IAssetLifecycleService
{
    /// <summary>Adds a lifecycle event to the context; the caller saves.</summary>
    AssetLifecycleEvent Record(Asset asset, string eventType, Guid? userId, string? userName, string? notes = null, DateTime? occurredAt = null);

    /// <summary>
    /// Validates a factory location for an asset: the node exists, is active, is a factory, section
    /// or sub-section (not a group) and is inside the user's scope.
    /// </summary>
    Task<OrgNode> EnsureLocationAsync(Guid tenantId, Guid userId, Guid orgNodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a parent for a component: it exists in the tenant, is not the asset itself and is
    /// not one of the asset's own components (no cycles).
    /// </summary>
    Task<Asset> EnsureParentAsync(Guid tenantId, Guid? assetId, Guid parentAssetId, CancellationToken cancellationToken = default);

    /// <summary>The asset's components at every depth.</summary>
    Task<IReadOnlyList<Asset>> LoadComponentTreeAsync(Guid tenantId, Guid assetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves an asset to another factory location, with every component that was at the same
    /// location, and records a transfer event for each. Returns how many assets moved. The caller saves.
    /// </summary>
    Task<int> TransferAsync(Asset asset, OrgNode destination, Guid? userId, string? userName, string? notes, DateTime? occurredAt, CancellationToken cancellationToken = default);
}

internal sealed class AssetLifecycleService(AppDbContext dbContext, IOrgScopeService orgScopeService) : IAssetLifecycleService
{
    public AssetLifecycleEvent Record(Asset asset, string eventType, Guid? userId, string? userName, string? notes = null, DateTime? occurredAt = null)
    {
        var entry = new AssetLifecycleEvent
        {
            TenantId = asset.TenantId,
            AssetId = asset.Id,
            EventType = eventType,
            OccurredAt = EffectiveAt(occurredAt),
            ToOrgNodeId = asset.OrgNodeId,
            ToParentAssetId = asset.ParentAssetId,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            PerformedByUserId = userId,
            PerformedByName = userName
        };
        dbContext.AssetLifecycleEvents.Add(entry);
        return entry;
    }

    /// <summary>
    /// Forms send dates without a time. A date of today means "now", so events recorded the same
    /// day keep their real order; a back-dated event keeps its date.
    /// </summary>
    internal static DateTime EffectiveAt(DateTime? occurredAt)
    {
        var now = DateTime.UtcNow;
        return occurredAt is not { } at || (at.Date == now.Date && at.TimeOfDay == TimeSpan.Zero) ? now : at;
    }

    public async Task<OrgNode> EnsureLocationAsync(Guid tenantId, Guid userId, Guid orgNodeId, CancellationToken cancellationToken = default)
    {
        var node = await dbContext.OrgNodes.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == orgNodeId, cancellationToken)
            ?? throw new BusinessRuleException("The selected factory location was not found.");

        if (!node.IsActive)
        {
            throw new BusinessRuleException($"'{node.Name}' is inactive.");
        }

        if (node.Level == OrgNodeLevels.Group)
        {
            throw new BusinessRuleException("Assets sit in a factory, section or sub-section, not a group.");
        }

        await orgScopeService.EnsureCanAccessAsync(tenantId, userId, orgNodeId, cancellationToken);
        return node;
    }

    public async Task<Asset> EnsureParentAsync(Guid tenantId, Guid? assetId, Guid parentAssetId, CancellationToken cancellationToken = default)
    {
        if (assetId == parentAssetId)
        {
            throw new BusinessRuleException("An asset cannot be its own component.");
        }

        var parent = await dbContext.Assets.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == parentAssetId, cancellationToken)
            ?? throw new BusinessRuleException("The parent asset was not found.");

        if (assetId.HasValue)
        {
            var descendants = await LoadComponentTreeAsync(tenantId, assetId.Value, cancellationToken);
            if (descendants.Any(x => x.Id == parentAssetId))
            {
                throw new BusinessRuleException("An asset cannot be placed under one of its own components.");
            }
        }

        return parent;
    }

    public async Task<IReadOnlyList<Asset>> LoadComponentTreeAsync(Guid tenantId, Guid assetId, CancellationToken cancellationToken = default)
    {
        var result = new List<Asset>();
        var frontier = new List<Guid> { assetId };
        var seen = new HashSet<Guid> { assetId };

        // Component trees are shallow (machine → assembly → part), so a query per level is fine.
        while (frontier.Count > 0)
        {
            var level = await dbContext.Assets
                .Where(x => x.TenantId == tenantId && x.ParentAssetId.HasValue && frontier.Contains(x.ParentAssetId.Value))
                .ToListAsync(cancellationToken);
            frontier = level.Where(x => seen.Add(x.Id)).Select(x => x.Id).ToList();
            result.AddRange(level.Where(x => frontier.Contains(x.Id)));
        }

        return result;
    }

    public async Task<int> TransferAsync(Asset asset, OrgNode destination, Guid? userId, string? userName, string? notes, DateTime? occurredAt, CancellationToken cancellationToken = default)
    {
        var origin = asset.OrgNodeId;
        if (origin == destination.Id)
        {
            return 0;
        }

        var names = await dbContext.OrgNodes
            .Where(x => x.TenantId == asset.TenantId && (x.Id == origin || x.Id == destination.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var moving = new List<Asset> { asset };
        moving.AddRange((await LoadComponentTreeAsync(asset.TenantId, asset.Id, cancellationToken)).Where(x => x.OrgNodeId == origin));

        foreach (var item in moving)
        {
            item.OrgNodeId = destination.Id;
            var entry = Record(item, AssetLifecycleEventTypes.Transferred, userId, userName,
                item.Id == asset.Id ? notes : $"Moved with parent asset {asset.AssetCode}.", occurredAt);
            entry.FromOrgNodeId = origin;
            entry.FromValue = origin.HasValue ? names.GetValueOrDefault(origin.Value) : null;
            entry.ToValue = names.GetValueOrDefault(destination.Id);
        }

        return moving.Count;
    }
}
