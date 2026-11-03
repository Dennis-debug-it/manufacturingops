using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class MaterialRequest : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";

    public Tenant? Tenant { get; set; }
    public Branch? Branch { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public User? RequestedByUser { get; set; }
    public ICollection<MaterialRequestLine> Lines { get; set; } = new List<MaterialRequestLine>();
}
