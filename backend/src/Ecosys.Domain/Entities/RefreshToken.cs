using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// A rotating, single-use refresh token bound to a <see cref="UserSession"/>. The raw value is
/// never stored — only its SHA-256 hash. Each successful refresh revokes the presented token and
/// issues a replacement (<see cref="ReplacedByTokenHash"/>); presenting an already-revoked token
/// signals reuse/theft and triggers revocation of the whole session family.
/// </summary>
public sealed class RefreshToken : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid SessionId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public DateTime? RevokedUtc { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    public User? User { get; set; }
    public UserSession? Session { get; set; }
}
