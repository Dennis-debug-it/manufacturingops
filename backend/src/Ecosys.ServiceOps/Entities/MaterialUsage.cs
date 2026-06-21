using Ecosys.Shared.Common;

namespace Ecosys.ServiceOps.Entities;

public sealed class MaterialUsage : TenantEntity
{
    public Guid WorkOrderId { get; set; }
    public Guid StoreItemId { get; set; }
    public decimal QuantityUsed { get; set; }
}
