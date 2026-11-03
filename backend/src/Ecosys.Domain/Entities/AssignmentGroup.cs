using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class AssignmentGroup : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SkillArea { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
    public Branch? Branch { get; set; }
    public ICollection<AssignmentGroupMember> Members { get; set; } = new List<AssignmentGroupMember>();
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
    public ICollection<WorkOrderAssignment> WorkOrderAssignments { get; set; } = new List<WorkOrderAssignment>();
    public ICollection<WorkOrderAssignmentHistory> AssignmentHistoryFromGroups { get; set; } = new List<WorkOrderAssignmentHistory>();
    public ICollection<WorkOrderAssignmentHistory> AssignmentHistoryToGroups { get; set; } = new List<WorkOrderAssignmentHistory>();
}
