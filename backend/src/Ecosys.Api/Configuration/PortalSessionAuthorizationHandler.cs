using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Configuration;

public sealed class PortalSessionRequirement : IAuthorizationRequirement;

public sealed class PortalSessionAuthorizationHandler(AppDbContext dbContext)
    : AuthorizationHandler<PortalSessionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PortalSessionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true
            || !Guid.TryParse(context.User.FindFirst(TenantClaimTypes.PortalUserId)?.Value, out var portalId)
            || !Guid.TryParse(context.User.FindFirst(TenantClaimTypes.ClientId)?.Value, out var clientId)
            || !Guid.TryParse(context.User.FindFirst(TenantClaimTypes.TenantId)?.Value, out var tenantId))
        {
            return;
        }

        var cancellationToken = (context.Resource as HttpContext)?.RequestAborted ?? CancellationToken.None;
        // The signed identity must still refer to an enabled contact and its current
        // client/tenant. Disabling or reassigning a contact revokes existing access.
        var active = await dbContext.ClientPortalUsers.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(user => user.Id == portalId && user.ClientId == clientId && user.TenantId == tenantId
                && user.IsActive && user.Client != null && user.Client.IsActive
                && user.Client.TenantId == tenantId && user.Tenant != null && user.Tenant.IsActive,
                cancellationToken);
        if (active)
        {
            context.Succeed(requirement);
        }
    }
}
