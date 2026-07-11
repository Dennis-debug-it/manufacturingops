using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>Per-(van, material) stock balance — the mobile equivalent of <see cref="BranchMaterialStock"/>.</summary>
public sealed class VanMaterialStock : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VanId { get; set; }
    public Guid MaterialId { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal? UnitCost { get; set; }

    public Tenant? Tenant { get; set; }
    public Van? Van { get; set; }
    public MaterialItem? Material { get; set; }
}
