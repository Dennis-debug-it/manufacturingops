namespace Ecosys.Shared.Auth;

public interface ITenantContext
{
    Guid? TenantId { get; }
    Guid? UserId { get; }
    Guid? SessionId { get; }
    string? Email { get; }
    string? Role { get; }
    string? JobTitle { get; }
    Guid? ImpersonatedByUserId => null;
    string? ImpersonatedByName => null;
    bool IsAuthenticated { get; }
    bool IsSuperAdmin { get; }
    bool IsAdmin { get; }

    /// <summary>
    /// Whether the caller is a client-portal login (<see cref="AppRoles.PortalClient"/>) rather than
    /// staff of the tenant. Portal tokens deliberately carry a tenant_id claim so portal queries can be
    /// scoped to that tenant — which means every tenant-internal endpoint would otherwise resolve a
    /// tenant for them and hand back the whole tenant's rows (EC-001). Anything that answers
    /// "may this caller use a tenant-internal endpoint" must consult this, not just
    /// <see cref="IsSuperAdmin"/>. Default implementation so existing implementers keep compiling;
    /// it is expressed in terms of <see cref="HasRole"/> so every implementer gets it right.
    /// </summary>
    bool IsPortalClient => HasRole(AppRoles.PortalClient);
    bool HasRole(string role);
    bool HasPermission(string permissionName);
    Guid GetRequiredTenantId();
    Guid GetRequiredUserId();
    Guid GetRequiredSessionId();
}
