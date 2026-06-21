using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class TenantNotificationSetting : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string NotificationKey { get; set; } = string.Empty;
    public bool EmailEnabled { get; set; } = true;
    public bool InAppEnabled { get; set; }
    public bool SmsEnabled { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
}
