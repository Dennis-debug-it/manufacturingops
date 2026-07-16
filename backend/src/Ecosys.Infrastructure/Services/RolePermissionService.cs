using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

/// <summary>One role and the capabilities it grants.</summary>
public sealed record RolePermissionEntry(string Role, UserPermissionsModel Permissions);

public interface IRolePermissionService
{
    /// <summary>The tenant's editable matrix, seeding default rows the first time it is read.</summary>
    Task<IReadOnlyList<RolePermissionEntry>> GetMatrixAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Upserts the capabilities granted to each assignable role.</summary>
    Task SaveMatrixAsync(Guid tenantId, IReadOnlyDictionary<string, UserPermissionsModel> updates, CancellationToken cancellationToken = default);

    /// <summary>The capabilities a role grants. Roles are the source of truth for permissions.</summary>
    Task<UserPermissionsModel> ResolveAsync(Guid tenantId, string role, CancellationToken cancellationToken = default);
}

internal sealed class RolePermissionService(AppDbContext dbContext) : IRolePermissionService
{
    /// <summary>
    /// Canonicalises a stored role string. Legacy <c>User</c> rows predate the role matrix and
    /// are treated as <see cref="AppRoles.Viewer"/>; anything unrecognised also falls back to
    /// Viewer so an unknown role can never accidentally grant more than the least-privileged one.
    /// </summary>
    public static string NormalizeRole(string role)
    {
        var match = AppRoles.AssignableTenantRoles
            .FirstOrDefault(candidate => string.Equals(candidate, role, StringComparison.OrdinalIgnoreCase));
        return match ?? AppRoles.Viewer;
    }

    /// <summary>
    /// Seed defaults. These mirror the capabilities each persona already had under the previous
    /// hard-coded job-title templates, so switching resolution over to the matrix grants nobody
    /// more than they had. Viewer intentionally matches the old catch-all default, because
    /// existing legacy "User" accounts land there.
    /// </summary>
    private static UserPermissionsModel DefaultsFor(string role) => role switch
    {
        AppRoles.Manager => new UserPermissionsModel
        {
            CanViewWorkOrders = true,
            CanCreateWorkOrders = true,
            CanAssignWorkOrders = true,
            CanCompleteWorkOrders = true,
            CanViewKnowledge = true,
            CanManageKnowledge = true,
            CanViewMaterials = true,
            CanRequestMaterials = true,
            CanViewReports = true,
            CanViewFinance = true,
            CanViewFinanceRates = true,
            CanViewFinanceApprovals = true,
            CanViewBillableWork = true
        },
        AppRoles.Dispatcher => new UserPermissionsModel
        {
            CanViewWorkOrders = true,
            CanCreateWorkOrders = true,
            CanAssignWorkOrders = true,
            CanViewKnowledge = true,
            CanViewMaterials = true,
            CanRequestMaterials = true
        },
        AppRoles.Technician => new UserPermissionsModel
        {
            CanViewWorkOrders = true,
            CanCompleteWorkOrders = true,
            CanViewKnowledge = true,
            CanViewMaterials = true,
            CanRequestMaterials = true
        },
        AppRoles.FinanceUser => new UserPermissionsModel
        {
            CanViewWorkOrders = true,
            CanViewKnowledge = true,
            CanViewReports = true,
            CanViewFinance = true,
            CanManageFinance = true,
            CanViewFinanceRates = true,
            CanManageFinanceRates = true,
            CanViewFinanceApprovals = true,
            CanManageFinanceApprovals = true,
            CanViewBillableWork = true,
            CanManageBillableWork = true
        },
        // Viewer == the previous catch-all default, so legacy "User" accounts keep exactly what they had.
        _ => new UserPermissionsModel
        {
            CanViewWorkOrders = true,
            CanViewKnowledge = true,
            CanViewMaterials = true,
            CanRequestMaterials = true
        }
    };

    /// <summary>Loads the tenant's rows, creating any that are missing from the seed defaults.</summary>
    private async Task<List<RolePermission>> GetOrSeedRowsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        // Always filter by tenant explicitly: the global tenant filter is bypassed when the
        // caller is unauthenticated (e.g. during login).
        var existing = await dbContext.RolePermissions
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var missing = AppRoles.AssignableTenantRoles
            .Where(role => !existing.Any(row => string.Equals(row.Role, role, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (missing.Count > 0)
        {
            foreach (var role in missing)
            {
                var seeded = ToEntity(tenantId, role, DefaultsFor(role));
                dbContext.RolePermissions.Add(seeded);
                existing.Add(seeded);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return existing
            .Where(row => AppRoles.AssignableTenantRoles.Contains(row.Role, StringComparer.OrdinalIgnoreCase))
            .OrderBy(row => Array.IndexOf(AppRoles.AssignableTenantRoles, NormalizeRole(row.Role)))
            .ToList();
    }

    public async Task<IReadOnlyList<RolePermissionEntry>> GetMatrixAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var rows = await GetOrSeedRowsAsync(tenantId, cancellationToken);
        return rows.Select(row => new RolePermissionEntry(row.Role, FromEntity(row))).ToList();
    }

    public async Task SaveMatrixAsync(Guid tenantId, IReadOnlyDictionary<string, UserPermissionsModel> updates, CancellationToken cancellationToken = default)
    {
        var rows = await GetOrSeedRowsAsync(tenantId, cancellationToken);

        foreach (var (role, permissions) in updates)
        {
            var normalized = NormalizeRole(role);
            var row = rows.FirstOrDefault(x => string.Equals(x.Role, normalized, StringComparison.OrdinalIgnoreCase));
            if (row is null)
            {
                continue;
            }

            Apply(row, permissions);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserPermissionsModel> ResolveAsync(Guid tenantId, string role, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeRole(role);

        var row = await dbContext.RolePermissions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Role == normalized, cancellationToken);

        // Not seeded yet (tenant created before the matrix existed) — seed on first use.
        if (row is null)
        {
            row = ToEntity(tenantId, normalized, DefaultsFor(normalized));
            dbContext.RolePermissions.Add(row);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return FromEntity(row);
    }

    private static RolePermission ToEntity(Guid tenantId, string role, UserPermissionsModel permissions)
    {
        var entity = new RolePermission { TenantId = tenantId, Role = role };
        Apply(entity, permissions);
        return entity;
    }

    private static void Apply(RolePermission entity, UserPermissionsModel p)
    {
        entity.CanViewWorkOrders = p.CanViewWorkOrders;
        entity.CanCreateWorkOrders = p.CanCreateWorkOrders;
        entity.CanAssignWorkOrders = p.CanAssignWorkOrders;
        entity.CanCompleteWorkOrders = p.CanCompleteWorkOrders;
        entity.CanViewKnowledge = p.CanViewKnowledge;
        entity.CanManageKnowledge = p.CanManageKnowledge;
        entity.CanViewMaterials = p.CanViewMaterials;
        entity.CanRequestMaterials = p.CanRequestMaterials;
        entity.CanManageMaterials = p.CanManageMaterials;
        entity.CanApproveMaterials = p.CanApproveMaterials;
        entity.CanIssueMaterials = p.CanIssueMaterials;
        entity.CanManageAssets = p.CanManageAssets;
        entity.CanManageSettings = p.CanManageSettings;
        entity.CanViewReports = p.CanViewReports;
        entity.CanViewFinance = p.CanViewFinance;
        entity.CanManageFinance = p.CanManageFinance;
        entity.CanViewFinanceRates = p.CanViewFinanceRates;
        entity.CanManageFinanceRates = p.CanManageFinanceRates;
        entity.CanViewFinanceApprovals = p.CanViewFinanceApprovals;
        entity.CanManageFinanceApprovals = p.CanManageFinanceApprovals;
        entity.CanViewBillableWork = p.CanViewBillableWork;
        entity.CanManageBillableWork = p.CanManageBillableWork;
        entity.CanOverrideMaterialStock = p.CanOverrideMaterialStock;
    }

    private static UserPermissionsModel FromEntity(RolePermission e) => new()
    {
        CanViewWorkOrders = e.CanViewWorkOrders,
        CanCreateWorkOrders = e.CanCreateWorkOrders,
        CanAssignWorkOrders = e.CanAssignWorkOrders,
        CanCompleteWorkOrders = e.CanCompleteWorkOrders,
        CanViewKnowledge = e.CanViewKnowledge,
        CanManageKnowledge = e.CanManageKnowledge,
        CanViewMaterials = e.CanViewMaterials,
        CanRequestMaterials = e.CanRequestMaterials,
        CanManageMaterials = e.CanManageMaterials,
        CanApproveMaterials = e.CanApproveMaterials,
        CanIssueMaterials = e.CanIssueMaterials,
        CanManageAssets = e.CanManageAssets,
        CanManageSettings = e.CanManageSettings,
        CanViewReports = e.CanViewReports,
        CanViewFinance = e.CanViewFinance,
        CanManageFinance = e.CanManageFinance,
        CanViewFinanceRates = e.CanViewFinanceRates,
        CanManageFinanceRates = e.CanManageFinanceRates,
        CanViewFinanceApprovals = e.CanViewFinanceApprovals,
        CanManageFinanceApprovals = e.CanManageFinanceApprovals,
        CanViewBillableWork = e.CanViewBillableWork,
        CanManageBillableWork = e.CanManageBillableWork,
        CanOverrideMaterialStock = e.CanOverrideMaterialStock
    };
}
