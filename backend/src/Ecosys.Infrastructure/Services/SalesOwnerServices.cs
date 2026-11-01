using Ecosys.Domain.Entities;
using Ecosys.Domain.Enums;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

/// <summary>What is known about a new lead, for the assignment rules.</summary>
public sealed record SalesLeadFacts(string? Source, string? ServiceType, string? Location);

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
    /// <item>the first lead assignment rule that matches <paramref name="lead"/> (a fixed person or the next in a rotation);</item>
    /// <item>the workspace's default sales owner (CRM settings);</item>
    /// <item>any admin, then anyone else who can manage the pipeline.</item>
    /// </list>
    /// Returns null only when nobody in the workspace can manage the pipeline.
    /// </summary>
    Task<SalesEligibleOwner?> ResolveAsync(Guid tenantId, IEnumerable<Guid?> preferred, Guid? clientId, SalesLeadFacts? lead, CancellationToken cancellationToken = default);

    Task<SalesEligibleOwner?> ResolveAsync(Guid tenantId, IEnumerable<Guid?> preferred, Guid? clientId, CancellationToken cancellationToken = default) =>
        ResolveAsync(tenantId, preferred, clientId, null, cancellationToken);

    /// <summary>The owner a lead assignment rule gives this lead, or null when no rule matches.</summary>
    Task<SalesEligibleOwner?> PickByRuleAsync(Guid tenantId, SalesLeadFacts lead, CancellationToken cancellationToken = default);
}

internal sealed class SalesOwnerResolver(AppDbContext dbContext, IRolePermissionService rolePermissionService) : ISalesOwnerResolver
{
    private static readonly ContractStatus[] LiveContractStatuses =
        [ContractStatus.Active, ContractStatus.ExpiringSoon, ContractStatus.RenewalInProgress];

    public async Task<SalesEligibleOwner?> ResolveAsync(Guid tenantId, IEnumerable<Guid?> preferred, Guid? clientId, SalesLeadFacts? lead = null, CancellationToken cancellationToken = default)
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
        if (lead is not null)
            candidates.Add(await PickByRuleAsync(tenantId, lead, eligible, cancellationToken));
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

    public async Task<SalesEligibleOwner?> PickByRuleAsync(Guid tenantId, SalesLeadFacts lead, CancellationToken cancellationToken = default)
    {
        var eligible = await SalesOwnerDirectory.ListAsync(dbContext, rolePermissionService, tenantId, cancellationToken);
        var id = await PickByRuleAsync(tenantId, lead, eligible, cancellationToken);
        return id is null ? null : eligible.First(x => x.Id == id);
    }

    /// <summary>
    /// The owner the first matching rule gives this lead. A rotation skips people who can no longer
    /// manage the pipeline and remembers where it got to (saved with the caller's changes).
    /// </summary>
    private async Task<Guid?> PickByRuleAsync(Guid tenantId, SalesLeadFacts lead, IReadOnlyList<SalesEligibleOwner> eligible, CancellationToken cancellationToken)
    {
        var rules = await dbContext.SalesAssignmentRules
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        foreach (var rule in rules.Where(rule => Matches(rule, lead)))
        {
            var owners = SalesAssignmentRules.ParseOwners(rule.OwnerUserIds)
                .Where(id => eligible.Any(x => x.Id == id))
                .ToList();
            if (owners.Count == 0)
                continue;
            if (!string.Equals(rule.Method, SalesAssignmentRules.RoundRobin, StringComparison.OrdinalIgnoreCase))
                return owners[0];
            var all = SalesAssignmentRules.ParseOwners(rule.OwnerUserIds);
            for (var step = 1; step <= all.Count; step++)
            {
                var index = (rule.LastAssignedIndex + step) % all.Count;
                if (owners.Contains(all[index]))
                {
                    rule.LastAssignedIndex = index;
                    return all[index];
                }
            }
        }
        return null;
    }

    private static bool Matches(SalesAssignmentRule rule, SalesLeadFacts lead) =>
        (rule.Source is null || string.Equals(rule.Source, lead.Source?.Trim(), StringComparison.OrdinalIgnoreCase))
        && (rule.ServiceType is null || string.Equals(rule.ServiceType, lead.ServiceType?.Trim(), StringComparison.OrdinalIgnoreCase))
        && (rule.LocationContains is null || (lead.Location ?? string.Empty).Contains(rule.LocationContains, StringComparison.OrdinalIgnoreCase));
}

public static class SalesAssignmentRules
{
    public const string Fixed = "Fixed";
    public const string RoundRobin = "RoundRobin";

    public static List<Guid> ParseOwners(string? value) =>
        (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => Guid.TryParse(x, out var id) ? id : Guid.Empty)
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();
}
