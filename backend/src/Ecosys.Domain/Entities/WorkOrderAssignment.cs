using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class WorkOrderAssignment : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid? AssignmentGroupId { get; set; }
    public string AssignmentStatus { get; set; } = "Unassigned";
    public Guid? AssignedByUserId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }

    public Tenant? Tenant { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public AssignmentGroup? AssignmentGroup { get; set; }
    public User? AssignedByUser { get; set; }
}
