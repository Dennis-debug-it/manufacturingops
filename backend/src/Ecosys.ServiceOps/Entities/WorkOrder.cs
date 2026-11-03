using Ecosys.ServiceOps.Enums;
using Ecosys.Shared.Common;

namespace Ecosys.ServiceOps.Entities;

public sealed class WorkOrder : TenantEntity
{
    public string Number { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? AssetId { get; set; }
    public Guid WorkOrderTypeId { get; set; }
    public Guid? AssignmentGroupId { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkOrderPriority Priority { get; set; } = WorkOrderPriority.Medium;
    public WorkOrderStatus Status { get; set; } = WorkOrderStatus.Open;
    public string? ResolutionNotes { get; set; }
    public DateTime? ResponseDueUtc { get; set; }
    public DateTime? ResolutionDueUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public SlaStatus SlaStatus { get; set; } = SlaStatus.WithinTarget;

    public Customer? Customer { get; set; }
    public Asset? Asset { get; set; }
    public Location? Location { get; set; }
    public WorkOrderType? WorkOrderType { get; set; }
    public AssignmentGroup? AssignmentGroup { get; set; }
    public ICollection<WorkOrderUpdate> Updates { get; set; } = new List<WorkOrderUpdate>();
    public ICollection<WorkOrderImage> Images { get; set; } = new List<WorkOrderImage>();
}
