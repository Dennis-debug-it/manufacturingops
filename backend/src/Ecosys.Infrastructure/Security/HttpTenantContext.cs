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
    public Guid? ImpersonatedByUserId => TryGetGuid(TenantClaimTypes.ImpersonatedByUserId);
    public string? ImpersonatedByName => User?.FindFirstValue(TenantClaimTypes.ImpersonatedByName);
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;
    /// <summary>
    /// Platform staff: bypasses the tenant query filters, so a role claim alone is not enough — the
    /// token must also belong to the platform root tenant (the same rule the Platform* authorization
    /// policies apply). Platform and tenant roles share a string namespace, so without this a single
    /// role-assignment bug inside a tenant would expose every tenant's data.
    /// </summary>
    public bool IsSuperAdmin =>
        TenantId == PlatformConstants.RootTenantId
        && IsPlatformRole;

    private bool IsPlatformRole =>
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

    /// <summary>
    /// Client-portal logins are customers of the tenant, never staff of it. Stated explicitly on the
    /// concrete context (not just inherited from the interface default) so it is visible next to
    /// <see cref="IsSuperAdmin"/> — the two together are the "not tenant staff" test.
    /// </summary>
    public bool IsPortalClient => HasRole(AppRoles.PortalClient);

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
