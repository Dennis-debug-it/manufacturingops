using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class WorkflowDefinition : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
    public ICollection<WorkflowStatus> Statuses { get; set; } = new List<WorkflowStatus>();
}
