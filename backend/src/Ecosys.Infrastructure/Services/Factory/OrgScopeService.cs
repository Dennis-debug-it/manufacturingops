using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services.Factory;

/// <summary>
/// The set of org nodes a user may see. <see cref="Nodes"/> is <c>null</c> when the user is not
/// restricted (tenant admins, and users with no factory/section assignment).
/// </summary>
public sealed record OrgScope(IReadOnlySet<Guid>? Nodes)
{
    public bool IsUnrestricted => Nodes is null;
    public bool Allows(Guid? nodeId) => Nodes is null || (nodeId.HasValue && Nodes.Contains(nodeId.Value));
}

/// <summary>
/// Resolves permission scoping down to factory and section (scope Phase 0). An assignment to a
/// node grants that node and everything beneath it.
/// </summary>
public interface IOrgScopeService
{
    Task<OrgScope> GetScopeAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task EnsureCanAccessAsync(Guid tenantId, Guid userId, Guid orgNodeId, CancellationToken cancellationToken = default);

    /// <summary>The node and all of its descendants, active or not.</summary>
    Task<IReadOnlySet<Guid>> GetSubtreeAsync(Guid tenantId, Guid orgNodeId, CancellationToken cancellationToken = default);
}

internal sealed class OrgScopeService(AppDbContext dbContext, ITenantContext tenantContext) : IOrgScopeService
{
    public async Task<OrgScope> GetScopeAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (tenantContext.IsAdmin && tenantContext.UserId == userId)
        {
            return new OrgScope(null);
        }

        var assigned = await dbContext.UserOrgNodeAssignments
            .Where(x => x.TenantId == tenantId && x.UserId == userId)
            .Select(x => x.OrgNodeId)
            .ToListAsync(cancellationToken);

        if (assigned.Count == 0)
        {
            return new OrgScope(null);
        }

        var edges = await LoadEdgesAsync(tenantId, cancellationToken);
        return new OrgScope(Descendants(edges, assigned));
    }

    public async Task EnsureCanAccessAsync(Guid tenantId, Guid userId, Guid orgNodeId, CancellationToken cancellationToken = default)
    {
        var scope = await GetScopeAsync(tenantId, userId, cancellationToken);
        if (!scope.Allows(orgNodeId))
        {
            throw new ForbiddenException("You do not have access to this part of the organisation.");
        }
    }

    public async Task<IReadOnlySet<Guid>> GetSubtreeAsync(Guid tenantId, Guid orgNodeId, CancellationToken cancellationToken = default)
    {
        var edges = await LoadEdgesAsync(tenantId, cancellationToken);
        return Descendants(edges, [orgNodeId]);
    }

    private async Task<List<(Guid Id, Guid? ParentId)>> LoadEdgesAsync(Guid tenantId, CancellationToken cancellationToken) =>
        (await dbContext.OrgNodes
            .Where(x => x.TenantId == tenantId)
            .Select(x => new { x.Id, x.ParentId })
            .ToListAsync(cancellationToken))
        .Select(x => (x.Id, x.ParentId))
        .ToList();

    internal static HashSet<Guid> Descendants(IReadOnlyCollection<(Guid Id, Guid? ParentId)> edges, IEnumerable<Guid> roots)
    {
        var children = edges
            .Where(x => x.ParentId.HasValue)
            .ToLookup(x => x.ParentId!.Value, x => x.Id);

        var result = new HashSet<Guid>();
        var pending = new Stack<Guid>(roots);
        while (pending.Count > 0)
        {
            var id = pending.Pop();
            if (!result.Add(id))
            {
                continue;
            }

            foreach (var child in children[id])
            {
                pending.Push(child);
            }
        }

        return result;
    }
}
