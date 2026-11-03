using Ecosys.ServiceOps.Enums;
using Ecosys.Shared.Common;

namespace Ecosys.ServiceOps.Entities;

public sealed class MaterialRequest : TenantEntity
{
    public string Number { get; set; } = string.Empty;
    public Guid WorkOrderId { get; set; }
    public MaterialRequestStatus Status { get; set; } = MaterialRequestStatus.Requested;
    public ICollection<MaterialRequestLine> Lines { get; set; } = new List<MaterialRequestLine>();

    public WorkOrder? WorkOrder { get; set; }
}
