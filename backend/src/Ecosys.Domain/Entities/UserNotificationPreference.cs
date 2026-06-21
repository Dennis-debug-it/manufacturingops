using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class UserNotificationPreference : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string NotificationKey { get; set; } = string.Empty;
    public bool EmailEnabled { get; set; } = true;
    public bool InAppEnabled { get; set; } = true;

    public User? User { get; set; }
}
