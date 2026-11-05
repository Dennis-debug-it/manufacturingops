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

    /// <summary>A spare the factory cannot run without (or with a long lead time); running out raises an alert.</summary>
    public bool IsCriticalSpare { get; set; }
    /// <summary>Each unit has a serial number and is tracked on its own (see <see cref="SpareUnit"/>).</summary>
    public bool IsSerialised { get; set; }
    /// <summary>Sent out for repair or reconditioning and returned to stock, rather than thrown away.</summary>
    public bool IsRepairable { get; set; }
    /// <summary>The unit it is bought in (e.g. a drum of 20 litres) and how many stock units one holds.</summary>
    public string? PurchaseUnitOfMeasure { get; set; }
    public decimal PurchaseUnitFactor { get; set; } = 1;
    public string? ExternalId { get; set; }
    public string? ExternalSystem { get; set; }

    /// <summary>Chemicals register: lubricants, boiler water treatment, cleaning agents.</summary>
    public bool IsChemical { get; set; }
    public string? ChemicalKind { get; set; }
    public string? HazardClass { get; set; }
    /// <summary>NSF food-grade class for lubricants: H1, H2 or H3.</summary>
    public string? FoodGradeClass { get; set; }
    public Guid? SdsAttachmentId { get; set; }
    public DateTime? SdsIssuedOn { get; set; }

    public Tenant? Tenant { get; set; }
    public Supplier? PreferredSupplier { get; set; }
    public ICollection<BranchMaterialStock> BranchStocks { get; set; } = new List<BranchMaterialStock>();
    public ICollection<VanMaterialStock> VanStocks { get; set; } = new List<VanMaterialStock>();
    public ICollection<MaterialRequestLine> MaterialRequestLines { get; set; } = new List<MaterialRequestLine>();
    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
    public ICollection<StockTransfer> StockTransfers { get; set; } = new List<StockTransfer>();
}
