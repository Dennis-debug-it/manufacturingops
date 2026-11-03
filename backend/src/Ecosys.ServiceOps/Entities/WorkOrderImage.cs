using Ecosys.ServiceOps.Enums;
using Ecosys.Shared.Common;

namespace Ecosys.ServiceOps.Entities;

public sealed class WorkOrderImage : TenantEntity
{
    public Guid WorkOrderId { get; set; }
    public WorkOrderImageType Type { get; set; }
    public string Url { get; set; } = string.Empty;

    public WorkOrder? WorkOrder { get; set; }
}
