using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

public sealed record EffectiveCapability(string Capability, bool Allowed, string Source, IReadOnlyList<AccessScope> Scopes, decimal? MaximumAmount, string? Currency);

public interface IDynamicAccessService
{
    Task<IReadOnlyList<EffectiveCapability>> GetEffectiveAccessAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<UserPermissionsModel> ResolveLegacyClaimsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
}

internal sealed class DynamicAccessService(AppDbContext dbContext, IRolePermissionService rolePermissionService) : IDynamicAccessService
{
    /// <summary>
    /// Effective access is layered: the tenant's role-permission matrix supplies the baseline for
    /// the user's role, permission-group membership adds to it, and a per-user override is the last
    /// word either way. The legacy <c>user_permissions</c> row is deliberately not consulted — the
    /// role matrix replaced it as the source of truth, and reading it here made the Settings matrix
    /// a no-op for everyone but Admin.
    /// </summary>
    public async Task<IReadOnlyList<EffectiveCapability>> GetEffectiveAccessAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.AsNoTracking().SingleAsync(x => x.TenantId == tenantId && x.Id == userId, cancellationToken);
        var memberships = await dbContext.UserPermissionGroups.AsNoTracking().Where(x => x.TenantId == tenantId && x.UserId == userId).Select(x => x.PermissionGroupId).ToListAsync(cancellationToken);
        var groupGrants = await dbContext.GroupCapabilities.AsNoTracking().Where(x => x.TenantId == tenantId && memberships.Contains(x.PermissionGroupId)).Select(x => x.Capability).Distinct().ToListAsync(cancellationToken);
        var overrides = await dbContext.UserPermissionOverrides.AsNoTracking().Where(x => x.TenantId == tenantId && x.UserId == userId).ToListAsync(cancellationToken);
        var scopes = await dbContext.AccessScopes.AsNoTracking().Where(x => x.TenantId == tenantId && (x.UserId == userId || (x.PermissionGroupId.HasValue && memberships.Contains(x.PermissionGroupId.Value)))).ToListAsync(cancellationToken);
        var thresholds = await dbContext.PermissionThresholds.AsNoTracking().Where(x => x.TenantId == tenantId && (x.UserId == userId || (x.PermissionGroupId.HasValue && memberships.Contains(x.PermissionGroupId.Value)))).ToListAsync(cancellationToken);
        var roleBaseline = await rolePermissionService.ResolveAsync(tenantId, user.Role, cancellationToken);
        var roleGrants = PermissionMap.FromModel(roleBaseline).Where(x => x.Value).Select(x => x.Key)
            .Concat(CapabilityRoleDefaults.For(user.Role)).ToArray();
        var all = PermissionNames.All.Concat(groupGrants).Concat(roleGrants).Concat(overrides.Select(x => x.Capability)).Distinct(StringComparer.OrdinalIgnoreCase);

        return all.Select(capability =>
        {
            var userOverride = overrides.FirstOrDefault(x => x.Capability.Equals(capability, StringComparison.OrdinalIgnoreCase));
            var inherited = groupGrants.Contains(capability, StringComparer.OrdinalIgnoreCase);
            var fromRole = roleGrants.Contains(capability, StringComparer.OrdinalIgnoreCase);
            var tenantOwner = user.Role.Equals(AppRoles.Admin, StringComparison.OrdinalIgnoreCase);
            var allowed = userOverride?.IsAllowed ?? (tenantOwner || fromRole || inherited);
            var source = userOverride is not null ? (userOverride.IsAllowed ? "Direct allow" : "Explicit deny") : tenantOwner ? "Tenant owner" : fromRole ? "Role permission" : inherited ? "Permission group" : "Not granted";
            var matchingScopes = scopes.Where(x => x.Capability == "*" || x.Capability.Equals(capability, StringComparison.OrdinalIgnoreCase)).ToArray();
            var limit = thresholds.Where(x => x.Capability.Equals(capability, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.MaximumAmount).FirstOrDefault();
            return new EffectiveCapability(capability, allowed, source, matchingScopes, limit?.MaximumAmount, limit?.Currency);
        }).OrderBy(x => x.Capability).ToArray();
    }

    public async Task<UserPermissionsModel> ResolveLegacyClaimsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var effective = await GetEffectiveAccessAsync(tenantId, userId, cancellationToken);
        var grants = effective.Where(x => x.Allowed).Select(x => x.Capability).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return PermissionMap.ToModel(grants) with
        {
            Capabilities = PermissionNames.CapabilityOnly.Where(grants.Contains).ToArray()
        };
    }
}

/// <summary>
/// Role baseline for <see cref="PermissionNames.CapabilityOnly"/> capabilities. These have no
/// role-matrix column, so the defaults live here; a tenant adjusts them per user or per group
/// through Access Control (grants and explicit denies). Admin is granted everything elsewhere.
/// </summary>
internal static class CapabilityRoleDefaults
{
    private static readonly string[] ProjectViewers = [PermissionNames.ProjectsView];

    private static readonly string[] ProjectManagers =
    [
        PermissionNames.ProjectsView,
        PermissionNames.ProjectsManage,
        PermissionNames.ProjectsVariationsSubmit,
        PermissionNames.ProjectsVariationsApprove,
        PermissionNames.ProjectsClose,
        PermissionNames.ProjectsMusterMark,
        PermissionNames.ProjectsFinancialsView,
        PermissionNames.ProjectsFinancialsManage
    ];

    private static readonly string[] ProjectFinance =
    [
        PermissionNames.ProjectsView,
        PermissionNames.ProjectsFinancialsView,
        PermissionNames.ProjectsFinancialsManage
    ];

    // Field staff mark the muster roll only on projects where they are a Supervisor or Project
    // Manager member; the capability alone opens nothing else.
    private static readonly string[] FieldSupervisors = [PermissionNames.ProjectsMusterMark];

    public static IReadOnlyList<string> For(string? role) => [.. ProjectDefaults(role), .. ActivityDefaults(role), .. CrmDefaults(role)];

    private static IReadOnlyList<string> ProjectDefaults(string? role)
    {
        if (string.Equals(role, AppRoles.Manager, StringComparison.OrdinalIgnoreCase)) return ProjectManagers;
        if (string.Equals(role, AppRoles.FinanceUser, StringComparison.OrdinalIgnoreCase)) return ProjectFinance;
        if (string.Equals(role, AppRoles.Dispatcher, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, AppRoles.Viewer, StringComparison.OrdinalIgnoreCase)) return ProjectViewers;
        if (string.Equals(role, AppRoles.Technician, StringComparison.OrdinalIgnoreCase)) return FieldSupervisors;
        return [];
    }

    /// <summary>
    /// Sales managers see the whole pipeline even when the workspace limits reps to their own deals,
    /// as in HubSpot's "all records" vs "owned only" access. Admin is granted everything elsewhere.
    /// </summary>
    private static IReadOnlyList<string> CrmDefaults(string? role) =>
        string.Equals(role, AppRoles.Manager, StringComparison.OrdinalIgnoreCase) ? [PermissionNames.CrmViewAll] : [];

    /// <summary>
    /// Every staff role sees meetings and action items and can organise its own; managers organise any
    /// meeting; managers and dispatchers get the calendar's team view.
    /// </summary>
    private static IReadOnlyList<string> ActivityDefaults(string? role)
    {
        if (string.Equals(role, AppRoles.Manager, StringComparison.OrdinalIgnoreCase))
            return [PermissionNames.ActivitiesView, PermissionNames.ActivitiesManage, PermissionNames.CalendarTeam];
        if (string.Equals(role, AppRoles.Dispatcher, StringComparison.OrdinalIgnoreCase))
            return [PermissionNames.ActivitiesView, PermissionNames.CalendarTeam];
        if (AppRoles.TenantRoles.Contains(role ?? "", StringComparer.OrdinalIgnoreCase))
            return [PermissionNames.ActivitiesView];
        return [];
    }
}

internal static class PermissionMap
{
    public static Dictionary<string, bool> FromModel(UserPermissionsModel p) => new(StringComparer.OrdinalIgnoreCase)
    {
        [PermissionNames.CanViewWorkOrders] = p.CanViewWorkOrders, [PermissionNames.CanCreateWorkOrders] = p.CanCreateWorkOrders,
        [PermissionNames.CanAssignWorkOrders] = p.CanAssignWorkOrders, [PermissionNames.CanCompleteWorkOrders] = p.CanCompleteWorkOrders,
        [PermissionNames.CanViewKnowledge] = p.CanViewKnowledge, [PermissionNames.CanManageKnowledge] = p.CanManageKnowledge,
        [PermissionNames.CanViewMaterials] = p.CanViewMaterials, [PermissionNames.CanRequestMaterials] = p.CanRequestMaterials,
        [PermissionNames.CanManageMaterials] = p.CanManageMaterials, [PermissionNames.CanApproveMaterials] = p.CanApproveMaterials,
        [PermissionNames.CanIssueMaterials] = p.CanIssueMaterials, [PermissionNames.CanManageAssets] = p.CanManageAssets,
        [PermissionNames.CanManageSettings] = p.CanManageSettings, [PermissionNames.CanViewReports] = p.CanViewReports,
        [PermissionNames.FinanceView] = p.CanViewFinance, [PermissionNames.FinanceManage] = p.CanManageFinance,
        [PermissionNames.FinanceViewRates] = p.CanViewFinanceRates, [PermissionNames.FinanceManageRates] = p.CanManageFinanceRates,
        [PermissionNames.FinanceViewApprovals] = p.CanViewFinanceApprovals, [PermissionNames.FinanceManageApprovals] = p.CanManageFinanceApprovals,
        [PermissionNames.FinanceViewBillableWork] = p.CanViewBillableWork, [PermissionNames.FinanceManageBillableWork] = p.CanManageBillableWork,
        [PermissionNames.MaterialsOverrideStock] = p.CanOverrideMaterialStock,
        [PermissionNames.CrmView] = p.CanViewCrm, [PermissionNames.CrmManage] = p.CanManageCrm
    };

    public static UserPermissionsModel ToModel(HashSet<string> p) => new()
    {
        CanViewWorkOrders=p.Contains(PermissionNames.CanViewWorkOrders), CanCreateWorkOrders=p.Contains(PermissionNames.CanCreateWorkOrders), CanAssignWorkOrders=p.Contains(PermissionNames.CanAssignWorkOrders), CanCompleteWorkOrders=p.Contains(PermissionNames.CanCompleteWorkOrders),
        CanViewKnowledge=p.Contains(PermissionNames.CanViewKnowledge), CanManageKnowledge=p.Contains(PermissionNames.CanManageKnowledge), CanViewMaterials=p.Contains(PermissionNames.CanViewMaterials), CanRequestMaterials=p.Contains(PermissionNames.CanRequestMaterials),
        CanManageMaterials=p.Contains(PermissionNames.CanManageMaterials), CanApproveMaterials=p.Contains(PermissionNames.CanApproveMaterials), CanIssueMaterials=p.Contains(PermissionNames.CanIssueMaterials), CanManageAssets=p.Contains(PermissionNames.CanManageAssets),
        CanManageSettings=p.Contains(PermissionNames.CanManageSettings), CanViewReports=p.Contains(PermissionNames.CanViewReports), CanViewFinance=p.Contains(PermissionNames.FinanceView), CanManageFinance=p.Contains(PermissionNames.FinanceManage),
        CanViewFinanceRates=p.Contains(PermissionNames.FinanceViewRates), CanManageFinanceRates=p.Contains(PermissionNames.FinanceManageRates), CanViewFinanceApprovals=p.Contains(PermissionNames.FinanceViewApprovals), CanManageFinanceApprovals=p.Contains(PermissionNames.FinanceManageApprovals),
        CanViewBillableWork=p.Contains(PermissionNames.FinanceViewBillableWork), CanManageBillableWork=p.Contains(PermissionNames.FinanceManageBillableWork), CanOverrideMaterialStock=p.Contains(PermissionNames.MaterialsOverrideStock),
        CanViewCrm=p.Contains(PermissionNames.CrmView), CanManageCrm=p.Contains(PermissionNames.CrmManage)
    };
}
