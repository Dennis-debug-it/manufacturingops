using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class SlaRule : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid SlaDefinitionId { get; set; }
    public string? Name { get; set; }
    public string Priority { get; set; } = "Medium";
    public decimal ResponseTargetHours { get; set; }
    public decimal ResolutionTargetHours { get; set; }
    public bool BusinessHoursOnly { get; set; }
    public TimeSpan? BusinessHoursStart { get; set; }
    public TimeSpan? BusinessHoursEnd { get; set; }

    public Tenant? Tenant { get; set; }
    public SlaDefinition? SlaDefinition { get; set; }
    public ICollection<SlaEscalation> Escalations { get; set; } = new List<SlaEscalation>();
}
