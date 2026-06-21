namespace Ecosys.Shared.Auth;

public interface ITenantContext
{
    Guid? TenantId { get; }
    Guid? UserId { get; }
    Guid? SessionId { get; }
    string? Email { get; }
    string? Role { get; }
    string? JobTitle { get; }
    bool IsAuthenticated { get; }
    bool IsSuperAdmin { get; }
    bool IsAdmin { get; }
    bool HasRole(string role);
    bool HasPermission(string permissionName);
    Guid GetRequiredTenantId();
    Guid GetRequiredUserId();
    Guid GetRequiredSessionId();
}
