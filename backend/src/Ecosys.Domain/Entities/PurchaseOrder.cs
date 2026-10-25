using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class PurchaseOrder : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Status { get; set; } = "Draft"; // Draft | Sent | PartiallyReceived | Received | Cancelled
    public DateTime OrderedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpectedAt { get; set; }
    public string Currency { get; set; } = "KES";
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }

    public Tenant? Tenant { get; set; }
    public Supplier? Supplier { get; set; }
    public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
    public ICollection<PurchaseOrderReceiptEvent> ReceiptEvents { get; set; } = new List<PurchaseOrderReceiptEvent>();
}

public sealed class PurchaseOrderLine : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal QuantityOrdered { get; set; }
    public decimal QuantityReceived { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }

    public Tenant? Tenant { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
}

public sealed class PurchaseOrderReceiptEvent : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public Guid PurchaseOrderLineId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public Guid? ReceivedByUserId { get; set; }
    public string ReceivedByName { get; set; } = string.Empty;

    public Tenant? Tenant { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
    public PurchaseOrderLine? PurchaseOrderLine { get; set; }
}
