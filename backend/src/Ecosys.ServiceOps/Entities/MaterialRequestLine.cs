using Ecosys.Shared.Common;

namespace Ecosys.ServiceOps.Entities;

public sealed class MaterialRequestLine : TenantEntity
{
    public Guid MaterialRequestId { get; set; }
    public Guid StoreItemId { get; set; }
    public decimal RequestedQuantity { get; set; }
    public decimal IssuedQuantity { get; set; }
    public decimal UsedQuantity { get; set; }
    public decimal ReturnedQuantity { get; set; }

    public MaterialRequest? MaterialRequest { get; set; }
    public StoreItem? StoreItem { get; set; }
}
