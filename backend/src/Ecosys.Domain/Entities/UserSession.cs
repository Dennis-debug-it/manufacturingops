using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class UserSession : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string? JwtId { get; set; }
    public DateTime LoginAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime? LogoutAt { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime? RevokedAt { get; set; }

    public Tenant? Tenant { get; set; }
    public User? User { get; set; }
}
