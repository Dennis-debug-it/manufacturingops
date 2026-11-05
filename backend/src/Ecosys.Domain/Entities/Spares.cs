using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// A stores room in a factory (main stores, boiler house stores, workshop). A factory can have
/// several; one is its main store, where parts for its jobs are issued from unless another is chosen.
/// </summary>
public sealed class Store : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    /// <summary>The factory (org node at factory level) the store belongs to.</summary>
    public Guid FactoryId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool IsMain { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
    public OrgNode? Factory { get; set; }
    public ICollection<StoreBin> Bins { get; set; } = new List<StoreBin>();
}

/// <summary>A shelf, rack or bin in a store (e.g. "A-03-2").</summary>
public sealed class StoreBin : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid StoreId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public Store? Store { get; set; }
}

/// <summary>
/// One part in one store: what is on the shelf, what is promised to jobs, the levels that trigger
/// reordering, and the moving average cost (tenant currency) used to value it.
/// </summary>
public sealed class StoreStock : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid StoreId { get; set; }
    public Guid MaterialItemId { get; set; }
    public Guid? BinId { get; set; }
    public decimal OnHand { get; set; }
    /// <summary>Held for planned jobs (reserved or kitted, not yet issued).</summary>
    public decimal Reserved { get; set; }
    /// <summary>Safety stock: below this, the part is short.</summary>
    public decimal MinLevel { get; set; }
    /// <summary>At or below this, reorder.</summary>
    public decimal ReorderPoint { get; set; }
    /// <summary>Order back up to this level; with none, to twice the reorder point.</summary>
    public decimal? MaxLevel { get; set; }
    public decimal AverageCost { get; set; }
    public DateTime? LastCountedAt { get; set; }
    /// <summary>When the low-stock alert went for the current dip; cleared once restocked above the reorder point.</summary>
    public DateTime? LowAlertSentAt { get; set; }

    public Store? Store { get; set; }
    public MaterialItem? MaterialItem { get; set; }
    public StoreBin? Bin { get; set; }
}

/// <summary>A part fitted to an asset (its parts list); read the other way it is the part's where-used list.</summary>
public sealed class AssetPart : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid AssetId { get; set; }
    public Guid MaterialItemId { get; set; }
    public decimal QuantityFitted { get; set; } = 1;
    /// <summary>Where on the asset, e.g. "Drive end bearing".</summary>
    public string? Position { get; set; }
    public string? Notes { get; set; }

    public Asset? Asset { get; set; }
    public MaterialItem? MaterialItem { get; set; }
}

public static class ReservationStatuses
{
    public const string Reserved = "Reserved";
    /// <summary>Picked from the shelf and set aside in the job's kit.</summary>
    public const string Kitted = "Kitted";
    public const string Issued = "Issued";
    public const string Released = "Released";

    public static bool IsOpen(string status) => status is Reserved or Kitted;
}

/// <summary>Parts held in a store for a planned job, so they are there on the day.</summary>
public sealed class StockReservation : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid StoreId { get; set; }
    public Guid MaterialItemId { get; set; }
    public Guid WorkOrderId { get; set; }
    public decimal Quantity { get; set; }
    public decimal IssuedQuantity { get; set; }
    public string Status { get; set; } = ReservationStatuses.Reserved;
    public string? ReservedByName { get; set; }
    public DateTime? KittedAt { get; set; }
    public string? KittedByName { get; set; }
    public string? Notes { get; set; }

    public Store? Store { get; set; }
    public MaterialItem? MaterialItem { get; set; }
    public WorkOrder? WorkOrder { get; set; }
}

public static class SpareUnitStatuses
{
    /// <summary>Serviceable, on the shelf.</summary>
    public const string InStore = "InStore";
    public const string Installed = "Installed";
    /// <summary>Taken off a machine, waiting to go out for repair.</summary>
    public const string AwaitingRepair = "AwaitingRepair";
    /// <summary>Out with a repairer (e.g. CTC segments for re-cutting).</summary>
    public const string AtRepair = "AtRepair";
    public const string Scrapped = "Scrapped";

    public static readonly IReadOnlyList<string> All = [InStore, Installed, AwaitingRepair, AtRepair, Scrapped];
}

/// <summary>
/// One serialised spare (a CTC roller segment, a gearbox, a motor), tracked through its life:
/// in store, fitted, removed for repair, at the repairer and back, or scrapped.
/// </summary>
public sealed class SpareUnit : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid MaterialItemId { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string Status { get; set; } = SpareUnitStatuses.InStore;
    /// <summary>The store it is in, or will return to.</summary>
    public Guid? StoreId { get; set; }
    public Guid? AssetId { get; set; }
    /// <summary>The repairer it is with while at repair.</summary>
    public Guid? RepairSupplierId { get; set; }
    public string? RepairReference { get; set; }
    public DateTime? SentForRepairAt { get; set; }
    public DateTime? ExpectedBackAt { get; set; }
    public int RepairCount { get; set; }
    public decimal TotalRepairCost { get; set; }
    public decimal? UnitCost { get; set; }
    public string? Notes { get; set; }

    public MaterialItem? MaterialItem { get; set; }
    public Store? Store { get; set; }
    public Asset? Asset { get; set; }
    public Supplier? RepairSupplier { get; set; }
    public ICollection<SpareUnitEvent> Events { get; set; } = new List<SpareUnitEvent>();
}

public sealed class SpareUnitEvent : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid SpareUnitId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string? ByName { get; set; }
    public Guid? WorkOrderId { get; set; }
    public Guid? AssetId { get; set; }
    public Guid? StoreId { get; set; }
    public decimal? Cost { get; set; }
    public string? Note { get; set; }

    public SpareUnit? SpareUnit { get; set; }
}

public static class StoreTransferStatuses
{
    public const string Requested = "Requested";
    /// <summary>Left the sending store; in transit.</summary>
    public const string Dispatched = "Dispatched";
    public const string Received = "Received";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";
}

/// <summary>Parts moved between stores, in the same factory or to another factory of the group.</summary>
public sealed class StoreTransfer : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public Guid FromStoreId { get; set; }
    public Guid ToStoreId { get; set; }
    public Guid MaterialItemId { get; set; }
    public decimal Quantity { get; set; }
    /// <summary>Serial numbers moved, comma separated, for serialised parts.</summary>
    public string? SerialNumbers { get; set; }
    public string Status { get; set; } = StoreTransferStatuses.Requested;
    public bool IsInterFactory { get; set; }
    public decimal? UnitCost { get; set; }
    public string? Reason { get; set; }
    public Guid? WorkOrderId { get; set; }
    public string? RequestedByName { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public string? DispatchedByName { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public string? ReceivedByName { get; set; }
    public string? RejectReason { get; set; }

    public Store? FromStore { get; set; }
    public Store? ToStore { get; set; }
    public MaterialItem? MaterialItem { get; set; }
}

/// <summary>
/// A cycle count of one store (all of it, one bin, or only critical spares). Opening it snapshots
/// the book quantity; posting adjusts each counted line by counted − expected.
/// </summary>
public sealed class StoreCount : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string CountNumber { get; set; } = string.Empty;
    public Guid StoreId { get; set; }
    public Guid? BinId { get; set; }
    public bool CriticalOnly { get; set; }
    public bool IsBlind { get; set; }
    public string Status { get; set; } = StockCountStatuses.Open;
    public string? StartedByName { get; set; }
    public DateTime? PostedAt { get; set; }
    public string? PostedByName { get; set; }
    public decimal? VarianceValue { get; set; }

    public Store? Store { get; set; }
    public ICollection<StoreCountLine> Lines { get; set; } = new List<StoreCountLine>();
}

public sealed class StoreCountLine : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid StoreCountId { get; set; }
    public Guid MaterialItemId { get; set; }
    public decimal ExpectedQuantity { get; set; }
    public decimal? CountedQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public string? Notes { get; set; }

    public StoreCount? StoreCount { get; set; }
    public MaterialItem? MaterialItem { get; set; }
}

public static class RequisitionStatuses
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    /// <summary>Turned into purchase orders.</summary>
    public const string Ordered = "Ordered";
    public const string Cancelled = "Cancelled";
}

/// <summary>A request to buy, raised by stores or a planner and approved before it becomes purchase orders.</summary>
public sealed class PurchaseRequisition : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string RequisitionNumber { get; set; } = string.Empty;
    public Guid FactoryId { get; set; }
    /// <summary>The store the parts are for; receipts go there.</summary>
    public Guid? StoreId { get; set; }
    public string Status { get; set; } = RequisitionStatuses.Draft;
    public string? Reason { get; set; }
    public DateTime? NeededBy { get; set; }
    public Guid RequestedByUserId { get; set; }
    public string? RequestedByName { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecidedByName { get; set; }
    public string? RejectReason { get; set; }

    public OrgNode? Factory { get; set; }
    public Store? Store { get; set; }
    public ICollection<PurchaseRequisitionLine> Lines { get; set; } = new List<PurchaseRequisitionLine>();
}

public sealed class PurchaseRequisitionLine : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid RequisitionId { get; set; }
    public Guid? MaterialItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public Guid? SupplierId { get; set; }
    public decimal? UnitPrice { get; set; }
    public string Currency { get; set; } = "KES";
    public Guid? WorkOrderId { get; set; }
    public Guid? PurchaseOrderId { get; set; }

    public PurchaseRequisition? Requisition { get; set; }
    public MaterialItem? MaterialItem { get; set; }
    public Supplier? Supplier { get; set; }
}

/// <summary>A supplier's price for a part: their part number, price, currency, lead time and minimum order.</summary>
public sealed class VendorCatalogueItem : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid SupplierId { get; set; }
    public Guid MaterialItemId { get; set; }
    public string? VendorPartNumber { get; set; }
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; } = "KES";
    public int? LeadTimeDays { get; set; }
    public decimal? MinOrderQuantity { get; set; }
    public DateTime? ValidUntil { get; set; }
    public bool IsPreferred { get; set; }

    public Supplier? Supplier { get; set; }
    public MaterialItem? MaterialItem { get; set; }
}

public static class ChemicalKinds
{
    public const string Lubricant = "Lubricant";
    public const string WaterTreatment = "Water treatment";
    public const string Cleaning = "Cleaning";
    public const string Fuel = "Fuel";
    public const string Other = "Other";

    public static readonly IReadOnlyList<string> All = [Lubricant, WaterTreatment, Cleaning, Fuel, Other];

    public static string? Find(string? value) => All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>NSF food-grade lubricant classes: H1 incidental food contact; H2 no contact; H3 soluble oils.</summary>
public static class FoodGradeClasses
{
    public static readonly IReadOnlyList<string> All = ["H1", "H2", "H3"];

    public static string? Find(string? value) => All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}
