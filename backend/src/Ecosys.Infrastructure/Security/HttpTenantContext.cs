using System.Security.Claims;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Http;

namespace Ecosys.Infrastructure.Security;

internal sealed class HttpTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid? TenantId => TryGetGuid(TenantClaimTypes.TenantId);
    public Guid? UserId => TryGetGuid(TenantClaimTypes.UserId);
    public Guid? SessionId => TryGetGuid(TenantClaimTypes.SessionId);
    public string? Email => User?.FindFirstValue(ClaimTypes.Email);
    public string? Role => User?.FindFirstValue(ClaimTypes.Role);
    public string? JobTitle => User?.FindFirstValue(TenantClaimTypes.JobTitle);
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;
    public bool IsSuperAdmin =>
        HasRole(AppRoles.SuperAdmin)
        || HasRole(AppRoles.PlatformOwner)
        || HasRole(AppRoles.PlatformSuperAdmin)
        || HasRole(AppRoles.PlatformAdminRole)
        || HasRole(AppRoles.PlatformAdmin)
        || HasRole(AppRoles.SupportAdmin)
        || HasRole(AppRoles.FinanceAdmin)
        || HasRole(AppRoles.ReadOnlyAuditor)
        || HasRole(AppRoles.Support)
        || HasRole(AppRoles.Finance)
        || HasRole(AppRoles.Auditor);
    public bool IsAdmin => HasRole(AppRoles.Admin);

    public bool HasRole(string role) => User?.IsInRole(role) == true;
    public bool HasPermission(string permissionName) =>
        string.Equals(User?.FindFirstValue(permissionName), bool.TrueString, StringComparison.OrdinalIgnoreCase);

    public Guid GetRequiredTenantId() =>
        TenantId ?? throw new ForbiddenException("Tenant context is required.");

    public Guid GetRequiredUserId() =>
        UserId ?? throw new ForbiddenException("User context is required.");

    public Guid GetRequiredSessionId() =>
        SessionId ?? throw new ForbiddenException("Session context is required.");

    private Guid? TryGetGuid(string claimType)
    {
        var value = User?.FindFirstValue(claimType);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
