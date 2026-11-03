using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class EscalationRule : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string TriggerCondition { get; set; } = string.Empty; // UnassignedAfterMins | InProgressOverEstimatePercent | StatusUnchangedMins | OverdueSinceMins
    public int TriggerValue { get; set; }
    public string Action { get; set; } = string.Empty; // NotifyDispatcher | NotifyManager | NotifyTechnician | ReassignToUserId | ChangePriority
    public string? ActionValue { get; set; } // userId or priority
    public int CooldownMins { get; set; } = 60;

    public Tenant? Tenant { get; set; }
    public ICollection<EscalationFired> FiredEvents { get; set; } = new List<EscalationFired>();
}
