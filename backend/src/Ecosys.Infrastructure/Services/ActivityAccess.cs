using Ecosys.Domain.Entities;
using Ecosys.Domain.Enums;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

/// <summary>
/// Who is looking, for activities and the calendar: the tenant, the user, what they are granted and
/// which modules the tenant has. A request builds one from its token; the anonymous calendar feed
/// builds one from the feed owner's effective access, so both answer "may this person open it" the same way.
/// </summary>
public sealed class AccessViewer(Guid tenantId, Guid userId, bool isAdmin, Func<string, bool> hasPermission, IReadOnlyCollection<string> modules)
{
    public Guid TenantId { get; } = tenantId;
    public Guid UserId { get; } = userId;

    public bool Can(string permission) => isAdmin || hasPermission(permission);

    public bool HasModule(string module) => modules.Contains(module, StringComparer.OrdinalIgnoreCase);

    public void Ensure(string permission)
    {
        if (!Can(permission))
            throw new ForbiddenException("You do not have permission to perform this action.");
    }
}

public interface IAccessViewerFactory
{
    /// <summary>The signed-in caller.</summary>
    Task<AccessViewer> CurrentAsync(CancellationToken cancellationToken = default);

    /// <summary>Any active user, from their effective access (role defaults, groups and overrides).</summary>
    Task<AccessViewer> ForUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
}

internal sealed class AccessViewerFactory(
    ITenantContext tenantContext,
    ILicenseGuardService licenseGuardService,
    IDynamicAccessService dynamicAccessService) : IAccessViewerFactory
{
    public async Task<AccessViewer> CurrentAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var modules = (await licenseGuardService.GetSnapshotAsync(tenantId, cancellationToken)).Modules;
        return new AccessViewer(tenantId, tenantContext.GetRequiredUserId(), tenantContext.IsAdmin, tenantContext.HasPermission, modules);
    }

    public async Task<AccessViewer> ForUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var modules = (await licenseGuardService.GetSnapshotAsync(tenantId, cancellationToken)).Modules;
        var granted = (await dynamicAccessService.GetEffectiveAccessAsync(tenantId, userId, cancellationToken))
            .Where(x => x.Allowed).Select(x => x.Capability).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new AccessViewer(tenantId, userId, false, granted.Contains, modules);
    }
}

/// <summary>A record an activity or action item is linked to, as the viewer sees it.</summary>
public sealed record LinkedRecord(string EntityType, Guid EntityId, string Label, string Url);

/// <summary>
/// Linked records (project, client, contract, work order): their labels, and whether a viewer may
/// open them. A record is open to a viewer only when the tenant has its module and the viewer has
/// the permission its own screen needs, so a meeting never leaks a record its reader can't see.
/// </summary>
public interface ILinkedRecordService
{
    Task<bool> CanOpenAsync(AccessViewer viewer, string entityType, Guid entityId, CancellationToken cancellationToken = default);

    /// <summary>Labels and links for display; records that no longer exist are left out.</summary>
    Task<IReadOnlyDictionary<(string EntityType, Guid EntityId), LinkedRecord>> DescribeAsync(
        Guid tenantId, IEnumerable<(string? EntityType, Guid? EntityId)> records, CancellationToken cancellationToken = default);

    /// <summary>Projects the viewer may see (none without the Projects module and projects.view), honouring members-only visibility.</summary>
    Task<IQueryable<Project>> VisibleProjectsAsync(AccessViewer viewer, CancellationToken cancellationToken = default);
}

public static class LinkedRecords
{
    /// <summary>The canonical entity type name, or null when it is not one of <see cref="ActivityEntityTypes.All"/>.</summary>
    public static string? Normalize(string? entityType) =>
        ActivityEntityTypes.All.FirstOrDefault(x => string.Equals(x, entityType?.Trim(), StringComparison.OrdinalIgnoreCase));
}

internal sealed class LinkedRecordService(AppDbContext dbContext, IProjectSettingsService projectSettingsService) : ILinkedRecordService
{
    public async Task<bool> CanOpenAsync(AccessViewer viewer, string entityType, Guid entityId, CancellationToken cancellationToken = default)
    {
        var tenantId = viewer.TenantId;
        switch (LinkedRecords.Normalize(entityType))
        {
            case ActivityEntityTypes.Project:
                return await (await VisibleProjectsAsync(viewer, cancellationToken)).AnyAsync(x => x.Id == entityId, cancellationToken);
            case ActivityEntityTypes.Client:
                return await dbContext.Clients.AnyAsync(x => x.TenantId == tenantId && x.Id == entityId, cancellationToken);
            case ActivityEntityTypes.Contract:
                return viewer.HasModule(ServiceOpsPlans.Maintenance)
                    && await dbContext.Contracts.AnyAsync(x => x.TenantId == tenantId && x.Id == entityId, cancellationToken);
            case ActivityEntityTypes.WorkOrder:
                return viewer.HasModule(ServiceOpsPlans.Operations) && viewer.Can(PermissionNames.CanViewWorkOrders)
                    && await dbContext.WorkOrders.AnyAsync(x => x.TenantId == tenantId && x.Id == entityId, cancellationToken);
            default:
                return false;
        }
    }

    public async Task<IReadOnlyDictionary<(string EntityType, Guid EntityId), LinkedRecord>> DescribeAsync(
        Guid tenantId, IEnumerable<(string? EntityType, Guid? EntityId)> records, CancellationToken cancellationToken = default)
    {
        var wanted = records.Where(x => x.EntityType is not null && x.EntityId.HasValue)
            .Select(x => (Type: LinkedRecords.Normalize(x.EntityType), Id: x.EntityId!.Value))
            .Where(x => x.Type is not null).Distinct().ToList();
        List<Guid> Ids(string type) => wanted.Where(x => x.Type == type).Select(x => x.Id).ToList();
        var result = new Dictionary<(string, Guid), LinkedRecord>();

        var projectIds = Ids(ActivityEntityTypes.Project);
        if (projectIds.Count > 0)
            foreach (var x in await dbContext.Projects.Where(x => x.TenantId == tenantId && projectIds.Contains(x.Id))
                         .Select(x => new { x.Id, x.ProjectNumber, x.Name }).ToListAsync(cancellationToken))
                result[(ActivityEntityTypes.Project, x.Id)] = new(ActivityEntityTypes.Project, x.Id, $"{x.ProjectNumber} {x.Name}", $"/projects/{x.Id}");
        var clientIds = Ids(ActivityEntityTypes.Client);
        if (clientIds.Count > 0)
            foreach (var x in await dbContext.Clients.Where(x => x.TenantId == tenantId && clientIds.Contains(x.Id))
                         .Select(x => new { x.Id, x.ClientName }).ToListAsync(cancellationToken))
                result[(ActivityEntityTypes.Client, x.Id)] = new(ActivityEntityTypes.Client, x.Id, x.ClientName, $"/clients/{x.Id}");
        var contractIds = Ids(ActivityEntityTypes.Contract);
        if (contractIds.Count > 0)
            foreach (var x in await dbContext.Contracts.Where(x => x.TenantId == tenantId && contractIds.Contains(x.Id))
                         .Select(x => new { x.Id, x.ContractNumber, x.Name }).ToListAsync(cancellationToken))
                result[(ActivityEntityTypes.Contract, x.Id)] = new(ActivityEntityTypes.Contract, x.Id, $"{x.ContractNumber} {x.Name}", $"/contracts/{x.Id}");
        var workOrderIds = Ids(ActivityEntityTypes.WorkOrder);
        if (workOrderIds.Count > 0)
            foreach (var x in await dbContext.WorkOrders.Where(x => x.TenantId == tenantId && workOrderIds.Contains(x.Id))
                         .Select(x => new { x.Id, x.WorkOrderNumber, x.Title }).ToListAsync(cancellationToken))
                result[(ActivityEntityTypes.WorkOrder, x.Id)] = new(ActivityEntityTypes.WorkOrder, x.Id, $"{x.WorkOrderNumber} {x.Title}", $"/work-orders/{x.Id}");
        return result;
    }

    public async Task<IQueryable<Project>> VisibleProjectsAsync(AccessViewer viewer, CancellationToken cancellationToken = default)
    {
        var tenantId = viewer.TenantId;
        var query = dbContext.Projects.Where(x => x.TenantId == tenantId);
        if (!viewer.HasModule(ServiceOpsPlans.Projects) || !viewer.Can(PermissionNames.ProjectsView))
            return query.Where(_ => false);
        if (viewer.Can(PermissionNames.ProjectsManage))
            return query;
        var settings = await projectSettingsService.GetAsync(tenantId, cancellationToken);
        if (settings.Visibility != ProjectVisibility.MembersOnly)
            return query;
        var userId = viewer.UserId;
        return query.Where(x => x.ProjectManagerUserId == userId || x.Members.Any(m => m.UserId == userId));
    }
}

internal static class ContractCalendarRules
{
    /// <summary>Contracts whose expiry and renewal-notice dates are still worth showing.</summary>
    public static readonly ContractStatus[] Live = [ContractStatus.Active, ContractStatus.ExpiringSoon, ContractStatus.RenewalInProgress];
}
