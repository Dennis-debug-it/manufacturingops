using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class MaterialItem : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal QuantityOnHand { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal? UnitCost { get; set; }
    /// <summary>Barcode or manufacturer part number, for scanning and lookup.</summary>
    public string? Barcode { get; set; }
    /// <summary>The supplier this part is normally bought from; reorder suggestions draft POs to it.</summary>
    public Guid? PreferredSupplierId { get; set; }
    /// <summary>The usual order size; with none, a reorder tops stock back up to twice the reorder level.</summary>
    public decimal? ReorderQuantity { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
    public Supplier? PreferredSupplier { get; set; }
    public ICollection<BranchMaterialStock> BranchStocks { get; set; } = new List<BranchMaterialStock>();
    public ICollection<VanMaterialStock> VanStocks { get; set; } = new List<VanMaterialStock>();
    public ICollection<MaterialRequestLine> MaterialRequestLines { get; set; } = new List<MaterialRequestLine>();
    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
    public ICollection<StockTransfer> StockTransfers { get; set; } = new List<StockTransfer>();
}
