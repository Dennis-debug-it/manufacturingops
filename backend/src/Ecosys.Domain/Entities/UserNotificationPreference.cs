using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class UserNotificationPreference : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string NotificationKey { get; set; } = string.Empty;
    public bool EmailEnabled { get; set; } = true;
    /// <summary>Show it in the bell (the in-app inbox).</summary>
    public bool InAppEnabled { get; set; } = true;
    /// <summary>Web push to the user's devices.</summary>
    public bool PushEnabled { get; set; } = true;
    /// <summary>WhatsApp (Release 3 part 3). Opt-in, so off unless the user turns it on.</summary>
    public bool WhatsAppEnabled { get; set; }

    public User? User { get; set; }
}
