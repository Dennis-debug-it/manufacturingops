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
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
    public ICollection<BranchMaterialStock> BranchStocks { get; set; } = new List<BranchMaterialStock>();
    public ICollection<MaterialRequestLine> MaterialRequestLines { get; set; } = new List<MaterialRequestLine>();
    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
    public ICollection<StockTransfer> StockTransfers { get; set; } = new List<StockTransfer>();
}
