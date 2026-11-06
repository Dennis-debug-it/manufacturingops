using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class TenantInvoice : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ClientId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string DocumentType { get; set; } = "Invoice"; // Invoice|ProformaInvoice
    public string Status { get; set; } = "Draft"; // Draft|Sent|PartiallyPaid|Paid|Overdue|Void
    public DateTime InvoiceDate { get; set; }
    public DateTime DueDate { get; set; }
    public string? Notes { get; set; }
    public string? TermsAndConditions { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; } = 0;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "KES";

    public Guid? CreditTermId { get; set; }
    public decimal? DepositAmount { get; set; }
    public bool DepositPaid { get; set; } = false;
    public DateTime? DepositDueDate { get; set; }

    public Tenant? Tenant { get; set; }
    public Client? Client { get; set; }
    public CreditTerm? CreditTerm { get; set; }
    public ICollection<TenantInvoiceLine> Lines { get; set; } = new List<TenantInvoiceLine>();
    public ICollection<TenantPayment> Payments { get; set; } = new List<TenantPayment>();
}

public sealed class TenantInvoiceLine : AuditableEntity
{
    public Guid InvoiceId { get; set; }
    public Guid? BillableItemId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ItemType { get; set; } = "Service";
    public string Unit { get; set; } = "unit";
    public decimal Quantity { get; set; }
    public decimal UnitRate { get; set; }
    public decimal LineTotal { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }

    public TenantInvoice? Invoice { get; set; }
    public WorkOrderBillableItem? BillableItem { get; set; }
    public WorkOrder? WorkOrder { get; set; }
}
