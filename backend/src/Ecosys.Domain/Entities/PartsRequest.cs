using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class PartsRequest : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid TechnicianId { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Notes { get; set; }
    public string? RejectionReason { get; set; }

    public Tenant? Tenant { get; set; }
    public Branch? Branch { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public User? Technician { get; set; }
    public ICollection<PartsRequestItem> Items { get; set; } = new List<PartsRequestItem>();
}
