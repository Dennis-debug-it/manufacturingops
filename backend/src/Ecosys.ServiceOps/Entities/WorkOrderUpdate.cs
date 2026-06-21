using Ecosys.Shared.Common;

namespace Ecosys.ServiceOps.Entities;

public sealed class WorkOrderUpdate : TenantEntity
{
    public Guid WorkOrderId { get; set; }
    public Guid? UserId { get; set; }
    public string Message { get; set; } = string.Empty;

    public WorkOrder? WorkOrder { get; set; }
}
