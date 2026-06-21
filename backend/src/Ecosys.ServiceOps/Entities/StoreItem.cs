using Ecosys.Shared.Common;

namespace Ecosys.ServiceOps.Entities;

public sealed class StoreItem : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal QuantityOnHand { get; set; }
    public string UnitOfMeasure { get; set; } = "Each";
}
