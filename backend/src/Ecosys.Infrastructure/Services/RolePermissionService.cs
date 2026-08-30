using System.Data.Common;
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

    /// <summary>PostgreSQL <c>unique_violation</c> — someone else inserted the row first.</summary>
    private const string UniqueViolation = "23505";

    private const int MaxSeedAttempts = 3;

    /// <summary>
    /// True when the exception chain carries a unique-key violation. The only unique key these
    /// writes can trip is <c>IX_role_permissions_TenantId_Role</c>, and hitting it means a
    /// concurrent request has already seeded the row we were trying to create — a race we can
    /// recover from by reading what the winner wrote, not a server fault.
    /// </summary>
    private static bool IsUniqueViolation(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException { SqlState: UniqueViolation })
            {
                return true;
            }
        }

        return false;
    }

    private static void Detach(AppDbContext dbContext, IEnumerable<RolePermission> entities)
    {
        foreach (var entity in entities)
        {
            dbContext.Entry(entity).State = EntityState.Detached;
        }
    }

    // Always filter by tenant explicitly: the global tenant filter is bypassed when the
    // caller is unauthenticated (e.g. during login).
    private Task<List<RolePermission>> LoadRowsAsync(Guid tenantId, CancellationToken cancellationToken) =>
        dbContext.RolePermissions
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(cancellationToken);

    private static List<RolePermission> InAssignableOrder(IEnumerable<RolePermission> rows) =>
        rows
            .Where(row => AppRoles.AssignableTenantRoles.Contains(row.Role, StringComparer.OrdinalIgnoreCase))
            .OrderBy(row => Array.IndexOf(AppRoles.AssignableTenantRoles, NormalizeRole(row.Role)))
            .ToList();

    /// <summary>
    /// Loads the tenant's rows, creating any that are missing from the seed defaults. Two callers
    /// can seed the same tenant at the same moment and one of them will lose on the unique index;
    /// re-read what the winner committed and seed only whatever is still absent, rather than
    /// letting a duplicate-key error escape as a 500.
    /// </summary>
    private async Task<List<RolePermission>> GetOrSeedRowsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var existing = await LoadRowsAsync(tenantId, cancellationToken);

            var missing = AppRoles.AssignableTenantRoles
                .Where(role => !existing.Any(row => string.Equals(row.Role, role, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (missing.Count == 0)
            {
                return InAssignableOrder(existing);
            }

            var seeded = missing.Select(role => ToEntity(tenantId, role, DefaultsFor(role))).ToList();
            dbContext.RolePermissions.AddRange(seeded);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                existing.AddRange(seeded);
                return InAssignableOrder(existing);
            }
            catch (Exception exception) when (IsUniqueViolation(exception) && attempt < MaxSeedAttempts)
            {
                // Nothing of this attempt was written, so drop our copies and go round again: the
                // re-read picks up the winner's rows and `missing` shrinks to what is genuinely
                // still absent. Bounded, because a race that survives three rounds is not a race.
                Detach(dbContext, seeded);
            }
        }
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

    /// <summary>
    /// Resolves a role's capabilities. This runs on the login path (a non-Admin login resolves its
    /// permissions through here), so it must never turn a first-use seed into a failed login.
    /// </summary>
    public async Task<UserPermissionsModel> ResolveAsync(Guid tenantId, string role, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeRole(role);

        var row = await FindRowAsync(tenantId, normalized, cancellationToken);
        if (row is not null)
        {
            return FromEntity(row);
        }

        // Not seeded yet (tenant created before the matrix existed) — seed on first use.
        var defaults = DefaultsFor(normalized);

        // SaveChanges flushes everything this request-scoped context is tracking, not just our row.
        // On the login path that can include the caller's own work (reset lockout counters, the new
        // session), and if our insert loses the race on IX_role_permissions_TenantId_Role the whole
        // batch rolls back with it. So when anything unrelated is pending we do not write at all:
        // the row is only a stored copy of DefaultsFor(...), so returning it unsaved gives the caller
        // the identical answer, and the next read with a clean context — or GetMatrixAsync, which the
        // settings screen calls — persists it. Login keeps its own save (MvpAuthService saves the
        // session immediately afterwards), so nothing is lost by our not saving here.
        if (HasPendingChangesOutsideRolePermissions())
        {
            return defaults;
        }

        var seeded = ToEntity(tenantId, normalized, defaults);
        dbContext.RolePermissions.Add(seeded);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return FromEntity(seeded);
        }
        catch (Exception exception) when (IsUniqueViolation(exception))
        {
            // Two first logins for the same tenant and role at the same instant: both saw no row,
            // both inserted, and this one lost. The winner's row is authoritative and nothing of
            // ours was written, so drop our copy and read theirs. Deliberately no second
            // SaveChanges — a blind retry would replay whatever else the context is tracking.
            Detach(dbContext, [seeded]);

            var winner = await FindRowAsync(tenantId, normalized, cancellationToken);
            return winner is null ? defaults : FromEntity(winner);
        }
    }

    private Task<RolePermission?> FindRowAsync(Guid tenantId, string normalizedRole, CancellationToken cancellationToken) =>
        dbContext.RolePermissions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Role == normalizedRole, cancellationToken);

    private bool HasPendingChangesOutsideRolePermissions() =>
        dbContext.ChangeTracker.Entries().Any(entry =>
            entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
            && entry.Entity is not RolePermission);

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
