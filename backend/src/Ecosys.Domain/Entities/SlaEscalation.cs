using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class SlaEscalation : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid SlaRuleId { get; set; }
    public int TriggerAtPercent { get; set; } = 80;
    public string? NotifyRoles { get; set; }
    public Guid? EscalateToUserId { get; set; }

    public Tenant? Tenant { get; set; }
    public SlaRule? SlaRule { get; set; }
    public User? EscalateToUser { get; set; }
}
