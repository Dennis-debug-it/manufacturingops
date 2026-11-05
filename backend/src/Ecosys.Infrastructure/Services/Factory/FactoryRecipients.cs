using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services.Factory;

/// <summary>Who hears about something happening in one part of the factory.</summary>
public static class FactoryRecipients
{
    /// <summary>
    /// Active supervisors (admins, and roles given "assign work orders") who can see the given org
    /// node; those limited to other factories are left out.
    /// </summary>
    public static async Task<IReadOnlyList<Guid>> SupervisorsAsync(AppDbContext dbContext, Guid tenantId, Guid? orgNodeId, CancellationToken cancellationToken)
    {
        var grantedRoles = await dbContext.RolePermissions.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CanAssignWorkOrders).Select(x => x.Role).ToListAsync(cancellationToken);
        var roles = grantedRoles.Append(AppRoles.Admin).Distinct().ToList();
        var candidates = await dbContext.Users.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive && roles.Contains(x.Role)).Select(x => x.Id).ToListAsync(cancellationToken);
        if (candidates.Count == 0)
        {
            return [];
        }

        var assignments = await dbContext.UserOrgNodeAssignments.AsNoTracking()
            .Where(x => x.TenantId == tenantId && candidates.Contains(x.UserId)).ToListAsync(cancellationToken);
        if (assignments.Count == 0)
        {
            return candidates;
        }

        var edges = (await dbContext.OrgNodes.AsNoTracking().Where(x => x.TenantId == tenantId).Select(x => new { x.Id, x.ParentId }).ToListAsync(cancellationToken))
            .Select(x => (x.Id, x.ParentId)).ToList();
        return candidates.Where(id =>
        {
            var mine = assignments.Where(a => a.UserId == id).Select(a => a.OrgNodeId).ToList();
            return mine.Count == 0 || (orgNodeId.HasValue && OrgScopeService.Descendants(edges, mine).Contains(orgNodeId.Value));
        }).ToList();
    }
}
