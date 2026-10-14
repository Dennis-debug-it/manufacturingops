using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// A browser's Web Push subscription for one signed-in user (one row per device/browser).
/// Endpoint, P256dh and Auth are exactly what <c>PushSubscription.toJSON()</c> returns in the browser.
/// </summary>
public sealed class PushSubscription : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public DateTime? LastSuccessAt { get; set; }

    public User? User { get; set; }
}
