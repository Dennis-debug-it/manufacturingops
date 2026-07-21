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

internal sealed class DynamicAccessService(AppDbContext dbContext) : IDynamicAccessService
{
    public async Task<IReadOnlyList<EffectiveCapability>> GetEffectiveAccessAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.AsNoTracking().SingleAsync(x => x.TenantId == tenantId && x.Id == userId, cancellationToken);
        var memberships = await dbContext.UserPermissionGroups.AsNoTracking().Where(x => x.TenantId == tenantId && x.UserId == userId).Select(x => x.PermissionGroupId).ToListAsync(cancellationToken);
        var groupGrants = await dbContext.GroupCapabilities.AsNoTracking().Where(x => x.TenantId == tenantId && memberships.Contains(x.PermissionGroupId)).Select(x => x.Capability).Distinct().ToListAsync(cancellationToken);
        var overrides = await dbContext.UserPermissionOverrides.AsNoTracking().Where(x => x.TenantId == tenantId && x.UserId == userId).ToListAsync(cancellationToken);
        var scopes = await dbContext.AccessScopes.AsNoTracking().Where(x => x.TenantId == tenantId && (x.UserId == userId || (x.PermissionGroupId.HasValue && memberships.Contains(x.PermissionGroupId.Value)))).ToListAsync(cancellationToken);
        var thresholds = await dbContext.PermissionThresholds.AsNoTracking().Where(x => x.TenantId == tenantId && (x.UserId == userId || (x.PermissionGroupId.HasValue && memberships.Contains(x.PermissionGroupId.Value)))).ToListAsync(cancellationToken);
        var legacyDirect = await dbContext.UserPermissions.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        var directGrants = legacyDirect is null ? [] : PermissionMap.FromLegacy(legacyDirect).Where(x => x.Value).Select(x => x.Key).ToArray();
        var all = PermissionNames.All.Concat(groupGrants).Concat(directGrants).Concat(overrides.Select(x => x.Capability)).Distinct(StringComparer.OrdinalIgnoreCase);

        return all.Select(capability =>
        {
            var userOverride = overrides.FirstOrDefault(x => x.Capability.Equals(capability, StringComparison.OrdinalIgnoreCase));
            var inherited = groupGrants.Contains(capability, StringComparer.OrdinalIgnoreCase);
            var direct = directGrants.Contains(capability, StringComparer.OrdinalIgnoreCase);
            var tenantOwner = user.Role.Equals(AppRoles.Admin, StringComparison.OrdinalIgnoreCase);
            var allowed = userOverride?.IsAllowed ?? (tenantOwner || direct || inherited);
            var source = userOverride is not null ? (userOverride.IsAllowed ? "Direct allow" : "Explicit deny") : tenantOwner ? "Tenant owner" : direct ? "Direct permission" : inherited ? "Permission group" : "Not granted";
            var matchingScopes = scopes.Where(x => x.Capability == "*" || x.Capability.Equals(capability, StringComparison.OrdinalIgnoreCase)).ToArray();
            var limit = thresholds.Where(x => x.Capability.Equals(capability, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.MaximumAmount).FirstOrDefault();
            return new EffectiveCapability(capability, allowed, source, matchingScopes, limit?.MaximumAmount, limit?.Currency);
        }).OrderBy(x => x.Capability).ToArray();
    }

    public async Task<UserPermissionsModel> ResolveLegacyClaimsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var effective = await GetEffectiveAccessAsync(tenantId, userId, cancellationToken);
        var grants = effective.Where(x => x.Allowed).Select(x => x.Capability).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return PermissionMap.ToModel(grants);
    }
}

internal static class PermissionMap
{
    public static Dictionary<string, bool> FromLegacy(UserPermission p) => new(StringComparer.OrdinalIgnoreCase)
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
        [PermissionNames.MaterialsOverrideStock] = p.CanOverrideMaterialStock
    };

    public static UserPermissionsModel ToModel(HashSet<string> p) => new()
    {
        CanViewWorkOrders=p.Contains(PermissionNames.CanViewWorkOrders), CanCreateWorkOrders=p.Contains(PermissionNames.CanCreateWorkOrders), CanAssignWorkOrders=p.Contains(PermissionNames.CanAssignWorkOrders), CanCompleteWorkOrders=p.Contains(PermissionNames.CanCompleteWorkOrders),
        CanViewKnowledge=p.Contains(PermissionNames.CanViewKnowledge), CanManageKnowledge=p.Contains(PermissionNames.CanManageKnowledge), CanViewMaterials=p.Contains(PermissionNames.CanViewMaterials), CanRequestMaterials=p.Contains(PermissionNames.CanRequestMaterials),
        CanManageMaterials=p.Contains(PermissionNames.CanManageMaterials), CanApproveMaterials=p.Contains(PermissionNames.CanApproveMaterials), CanIssueMaterials=p.Contains(PermissionNames.CanIssueMaterials), CanManageAssets=p.Contains(PermissionNames.CanManageAssets),
        CanManageSettings=p.Contains(PermissionNames.CanManageSettings), CanViewReports=p.Contains(PermissionNames.CanViewReports), CanViewFinance=p.Contains(PermissionNames.FinanceView), CanManageFinance=p.Contains(PermissionNames.FinanceManage),
        CanViewFinanceRates=p.Contains(PermissionNames.FinanceViewRates), CanManageFinanceRates=p.Contains(PermissionNames.FinanceManageRates), CanViewFinanceApprovals=p.Contains(PermissionNames.FinanceViewApprovals), CanManageFinanceApprovals=p.Contains(PermissionNames.FinanceManageApprovals),
        CanViewBillableWork=p.Contains(PermissionNames.FinanceViewBillableWork), CanManageBillableWork=p.Contains(PermissionNames.FinanceManageBillableWork), CanOverrideMaterialStock=p.Contains(PermissionNames.MaterialsOverrideStock)
    };
}
