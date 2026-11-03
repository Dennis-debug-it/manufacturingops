using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class NotificationSetting : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public bool EmailAlertsEnabled { get; set; } = true;
    public bool SmsAlertsEnabled { get; set; }
    public bool WorkOrderAssignmentAlerts { get; set; } = true;
    public bool LicenseExpiryAlerts { get; set; } = true;
    public bool DailyDigestEnabled { get; set; }

    public Tenant? Tenant { get; set; }
}
