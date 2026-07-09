using System.Security.Claims;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Http;

namespace Ecosys.Infrastructure.Security;

internal sealed class HttpPortalContext(IHttpContextAccessor httpContextAccessor) : IPortalContext
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid? TenantId => TryGetGuid(TenantClaimTypes.TenantId);
    public Guid? PortalUserId => TryGetGuid(TenantClaimTypes.PortalUserId);
    public Guid? ClientId => TryGetGuid(TenantClaimTypes.ClientId);
    public string? Email => User?.FindFirstValue(ClaimTypes.Email);
    public string? FullName => User?.FindFirstValue(ClaimTypes.Name);
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true && User.IsInRole(AppRoles.PortalClient);

    public Guid GetRequiredTenantId() =>
        TenantId ?? throw new ForbiddenException("Tenant context is required.");

    public Guid GetRequiredPortalUserId() =>
        PortalUserId ?? throw new ForbiddenException("Portal user context is required.");

    public Guid GetRequiredClientId() =>
        ClientId ?? throw new ForbiddenException("Client context is required.");

    private Guid? TryGetGuid(string claimType)
    {
        var value = User?.FindFirstValue(claimType);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
