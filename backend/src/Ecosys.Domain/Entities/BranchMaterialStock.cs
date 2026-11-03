using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class BranchMaterialStock : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid MaterialId { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal? UnitCost { get; set; }

    public Tenant? Tenant { get; set; }
    public Branch? Branch { get; set; }
    public MaterialItem? Material { get; set; }
}
