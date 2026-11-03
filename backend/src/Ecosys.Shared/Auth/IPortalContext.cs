namespace Ecosys.Shared.Auth;

/// <summary>
/// Client-portal-only auth context. Portal tokens carry the same tenant_id claim as
/// staff tokens (so the existing global EF tenant query filter applies for free), but
/// portal users are not rows in the Users table — they're a separate ClientPortalUser
/// identity, always additionally scoped to a single Client within that tenant. Portal
/// controllers must use this instead of ITenantContext for "who is calling" checks.
/// </summary>
public interface IPortalContext
{
    Guid? TenantId { get; }
    Guid? PortalUserId { get; }
    Guid? ClientId { get; }
    string? Email { get; }
    string? FullName { get; }
    bool IsAuthenticated { get; }
    Guid GetRequiredTenantId();
    Guid GetRequiredPortalUserId();
    Guid GetRequiredClientId();
}
