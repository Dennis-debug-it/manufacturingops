using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// A part bought straight from a supplier for a job (the technician picked it up at the counter)
/// rather than taken from stores. Logging its use on the job takes the branch balance down — below
/// zero if stores had none — and this record stays Pending until stores reconciles it against the
/// supplier's invoice, which books the stock back in. Until then the negative balance is explained.
/// </summary>
public sealed class DirectPurchase : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid MaterialItemId { get; set; }
    /// <summary>The branch whose balance the use was taken from.</summary>
    public Guid? BranchId { get; set; }
    public decimal Quantity { get; set; }
    /// <summary>What was paid per unit, when known.</summary>
    public decimal? UnitCost { get; set; }
    public Guid? WorkOrderId { get; set; }
    public Guid? WorkOrderMaterialUsageId { get; set; }
    public Guid? SupplierId { get; set; }
    /// <summary>The supplier as typed, when it isn't (yet) a supplier record.</summary>
    public string? SupplierName { get; set; }
    /// <summary>The supplier's receipt or cash-sale number from the counter.</summary>
    public string? SupplierReference { get; set; }
    public Guid PurchasedByUserId { get; set; }
    public string Status { get; set; } = DirectPurchaseStatuses.Pending;
    /// <summary>The supplier invoice it was reconciled against.</summary>
    public string? InvoiceReference { get; set; }
    public Guid? ReconciledByUserId { get; set; }
    public DateTime? ReconciledAt { get; set; }
    public Tenant? Tenant { get; set; }
    public MaterialItem? MaterialItem { get; set; }
    public Branch? Branch { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public Supplier? Supplier { get; set; }
    public User? PurchasedByUser { get; set; }
    public User? ReconciledByUser { get; set; }
}

public static class DirectPurchaseStatuses
{
    public const string Pending = "Pending";
    public const string Reconciled = "Reconciled";
}
