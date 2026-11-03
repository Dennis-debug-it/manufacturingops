using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// A reversing financial document that reduces what a client owes. Can be linked to a specific
/// invoice or issued standalone. Its <see cref="Balance"/> is available customer credit that can
/// be applied against open invoices via <see cref="PaymentAllocation"/> rows (SourceType = CreditNote).
/// </summary>
public sealed class CreditNote : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ClientId { get; set; }
    public string CreditNoteNumber { get; set; } = string.Empty;

    /// <summary>Optional originating invoice. Null for a standalone credit.</summary>
    public Guid? InvoiceId { get; set; }

    public string Reason { get; set; } = "Other"; // Return | Overcharge | Goodwill | Cancellation | Other
    public string Status { get; set; } = "Draft";  // Draft | Issued | PartiallyApplied | Applied | Void
    public DateTime IssueDate { get; set; }
    public string? Notes { get; set; }

    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    /// <summary>How much of this credit note has been applied against invoices.</summary>
    public decimal AmountApplied { get; set; } = 0;
    /// <summary>Unapplied credit still available (TotalAmount - AmountApplied).</summary>
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "KES";

    public Tenant? Tenant { get; set; }
    public Client? Client { get; set; }
    public TenantInvoice? Invoice { get; set; }
    public ICollection<CreditNoteLine> Lines { get; set; } = new List<CreditNoteLine>();
}

public sealed class CreditNoteLine : AuditableEntity
{
    public Guid CreditNoteId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ItemType { get; set; } = "Service";
    public string Unit { get; set; } = "unit";
    public decimal Quantity { get; set; }
    public decimal UnitRate { get; set; }
    public decimal LineTotal { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }

    public CreditNote? CreditNote { get; set; }
}
