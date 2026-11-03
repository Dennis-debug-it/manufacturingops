using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class EscalationFired : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid RuleId { get; set; }
    public Guid WorkOrderId { get; set; }
    public DateTime FiredAt { get; set; } = DateTime.UtcNow;

    public Tenant? Tenant { get; set; }
    public EscalationRule? Rule { get; set; }
    public WorkOrder? WorkOrder { get; set; }
}
