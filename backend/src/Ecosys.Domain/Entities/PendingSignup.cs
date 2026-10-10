using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// A self-service sign-up waiting for its email to be confirmed. Nothing exists in the workspace
/// tables until the emailed link is opened; the workspace and its trial are created then.
/// </summary>
public sealed class PendingSignup : AuditableEntity
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? Industry { get; set; }
    /// <summary>The password chosen on the form, already hashed; the plain password is never stored.</summary>
    public string PasswordHash { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? TenantId { get; set; }
    public string? RequestedIp { get; set; }
}
