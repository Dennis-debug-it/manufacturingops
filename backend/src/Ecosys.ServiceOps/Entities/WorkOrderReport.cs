using Ecosys.Shared.Common;

namespace Ecosys.ServiceOps.Entities;

public sealed class WorkOrderReport : TenantEntity
{
    public Guid WorkOrderId { get; set; }
    public bool IsApproved { get; set; }
    public DateTime? ApprovedUtc { get; set; }
    public string Content { get; set; } = string.Empty;

    public WorkOrder? WorkOrder { get; set; }
}
