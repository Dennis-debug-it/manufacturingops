using Ecosys.Domain.Entities;
using Ecosys.Domain.Enums;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

/// <summary>An active user who can manage the sales pipeline, and so can own a deal or RFP/RFQ.</summary>
public sealed record SalesEligibleOwner(Guid Id, string FullName, string Role);

public static class SalesOwnerDirectory
{
    /// <summary>
    /// The active users who can manage the pipeline. Resolved in a handful of set queries with the
    /// same layering as <c>DynamicAccessService</c>: the role matrix is the baseline, a permission
    /// group adds to it, and a per-user override has the last word either way. Queries filter by
    /// tenant explicitly, so this also works from background jobs.
    /// </summary>
    public static async Task<IReadOnlyList<SalesEligibleOwner>> ListAsync(
        AppDbContext dbContext, IRolePermissionService rolePermissionService, Guid tenantId, CancellationToken cancellationToken)
    {
        var capability = PermissionNames.CrmManage.ToLower();
        var users = await dbContext.Users
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .Select(x => new { x.Id, x.FullName, x.Role })
            .ToListAsync(cancellationToken);

        var matrix = await rolePermissionService.GetMatrixAsync(tenantId, cancellationToken);
        var viewerRow = matrix.FirstOrDefault(x => string.Equals(x.Role, AppRoles.Viewer, StringComparison.OrdinalIgnoreCase));
        bool RoleGrants(string role)
        {
            if (string.Equals(role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase)) return true;
            // Unknown and legacy roles resolve as Viewer, exactly as RolePermissionService does.
            var row = matrix.FirstOrDefault(x => string.Equals(x.Role, role, StringComparison.OrdinalIgnoreCase)) ?? viewerRow;
            return row?.Permissions.CanManageCrm == true;
        }

        var grantingGroups = await dbContext.GroupCapabilities
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Capability.ToLower() == capability)
            .Select(x => x.PermissionGroupId)
            .ToListAsync(cancellationToken);
        var groupMembers = (await dbContext.UserPermissionGroups
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && grantingGroups.Contains(x.PermissionGroupId))
                .Select(x => x.UserId)
                .ToListAsync(cancellationToken))
            .ToHashSet();
        var overrides = (await dbContext.UserPermissionOverrides
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.Capability.ToLower() == capability)
                .Select(x => new { x.UserId, x.IsAllowed })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.First().IsAllowed);

        return users
            .Where(user => overrides.TryGetValue(user.Id, out var allowed)
                ? allowed
                : RoleGrants(user.Role) || groupMembers.Contains(user.Id))
            .OrderBy(x => x.FullName)
            .Select(x => new SalesEligibleOwner(x.Id, x.FullName, x.Role))
            .ToList();
    }
}

public interface ISalesOwnerResolver
{
    /// <summary>
    /// Picks the owner for a deal the system creates, so it never lands unowned. The first person in
    /// this list who can still manage the pipeline wins:
    /// <list type="number">
    /// <item>whoever the caller prefers (e.g. a contract's renewal owner, then its account manager);</item>
    /// <item>the owner of the client's most recent open deal, else its most recent won deal;</item>
    /// <item>the account manager, then the renewal owner, on the client's newest live contract;</item>
    /// <item>the workspace's default sales owner (CRM settings);</item>
    /// <item>any admin, then anyone else who can manage the pipeline.</item>
    /// </list>
    /// Returns null only when nobody in the workspace can manage the pipeline.
    /// </summary>
    Task<SalesEligibleOwner?> ResolveAsync(Guid tenantId, IEnumerable<Guid?> preferred, Guid? clientId, CancellationToken cancellationToken = default);
}

internal sealed class SalesOwnerResolver(AppDbContext dbContext, IRolePermissionService rolePermissionService) : ISalesOwnerResolver
{
    private static readonly ContractStatus[] LiveContractStatuses =
        [ContractStatus.Active, ContractStatus.ExpiringSoon, ContractStatus.RenewalInProgress];

    public async Task<SalesEligibleOwner?> ResolveAsync(Guid tenantId, IEnumerable<Guid?> preferred, Guid? clientId, CancellationToken cancellationToken = default)
    {
        var eligible = await SalesOwnerDirectory.ListAsync(dbContext, rolePermissionService, tenantId, cancellationToken);
        if (eligible.Count == 0)
            return null;

        var candidates = new List<Guid?>(preferred);
        if (clientId.HasValue)
        {
            candidates.Add(await dbContext.SalesOpportunities.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.ClientId == clientId && x.Stage != "Won" && x.Stage != "Lost" && x.OwnerUserId != null)
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .Select(x => x.OwnerUserId)
                .FirstOrDefaultAsync(cancellationToken));
            candidates.Add(await dbContext.SalesOpportunities.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.ClientId == clientId && x.Stage == "Won" && x.OwnerUserId != null)
                .OrderByDescending(x => x.WonAt)
                .Select(x => x.OwnerUserId)
                .FirstOrDefaultAsync(cancellationToken));
            var contract = await dbContext.Contracts.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.ClientId == clientId && !x.IsArchived && LiveContractStatuses.Contains(x.Status))
                .OrderByDescending(x => x.EndDate)
                .Select(x => new { x.AccountManagerUserId, x.RenewalOwnerUserId })
                .FirstOrDefaultAsync(cancellationToken);
            candidates.Add(contract?.AccountManagerUserId);
            candidates.Add(contract?.RenewalOwnerUserId);
        }
        candidates.Add(await dbContext.TenantSalesSettings.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => x.DefaultOwnerUserId)
            .FirstOrDefaultAsync(cancellationToken));

        foreach (var candidate in candidates)
        {
            var match = candidate is null ? null : eligible.FirstOrDefault(x => x.Id == candidate.Value);
            if (match is not null)
                return match;
        }
        return eligible.FirstOrDefault(x => string.Equals(x.Role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase)) ?? eligible[0];
    }
}
