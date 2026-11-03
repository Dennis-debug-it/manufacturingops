using Ecosys.Shared.Common;

namespace Ecosys.ServiceOps.Entities;

public sealed class WorkOrderAcknowledgement : TenantEntity
{
    public Guid WorkOrderId { get; set; }
    public string AcknowledgedBy { get; set; } = string.Empty;
    public DateTime AcknowledgedUtc { get; set; } = DateTime.UtcNow;

    public WorkOrder? WorkOrder { get; set; }
}
