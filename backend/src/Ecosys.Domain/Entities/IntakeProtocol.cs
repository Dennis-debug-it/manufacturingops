using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class IntakeProtocol : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SourceType { get; set; } = "Email";
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
    public string CriteriaJson { get; set; } = "[]";
    public string ActionsJson { get; set; } = "{}";
    public string SourceConfigJson { get; set; } = "{}";
    public DateTime? LastTriggeredAt { get; set; }
    public string? LastTriggerStatus { get; set; }
    public string? LastError { get; set; }

    public Tenant? Tenant { get; set; }
}
