using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class PasswordResetToken : AuditableEntity
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public string Purpose { get; set; } = PasswordLinkPurposes.PasswordReset;
    public DateTime? SupersededAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RequestedIp { get; set; }
    public string? UserAgent { get; set; }

    public User? User { get; set; }
}

public static class PasswordLinkPurposes
{
    public const string Invitation = "Invitation";
    public const string PasswordReset = "PasswordReset";
}
