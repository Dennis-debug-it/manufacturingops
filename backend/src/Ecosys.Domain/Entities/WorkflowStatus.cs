using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class WorkflowStatus : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#64748b";
    public int SortOrder { get; set; }
    public bool IsInitial { get; set; }
    public bool IsFinal { get; set; }
    public string? AllowedNextStatusIdsJson { get; set; }

    public Tenant? Tenant { get; set; }
    public WorkflowDefinition? WorkflowDefinition { get; set; }
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
}
