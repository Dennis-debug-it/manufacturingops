using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class PasswordResetToken : AuditableEntity
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? RequestedIp { get; set; }
    public string? UserAgent { get; set; }

    public User? User { get; set; }
}
