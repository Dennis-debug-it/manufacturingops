using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class SlaRecord : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid? SlaRuleId { get; set; }
    public DateTime? ResponseBreachedAt { get; set; }
    public DateTime? ResolutionBreachedAt { get; set; }
    public bool IsPaused { get; set; }
    public string? PauseReason { get; set; }
    public int? LastResponseEscalationPercent { get; set; }
    public int? LastResolutionEscalationPercent { get; set; }

    public Tenant? Tenant { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public SlaRule? SlaRule { get; set; }
}
