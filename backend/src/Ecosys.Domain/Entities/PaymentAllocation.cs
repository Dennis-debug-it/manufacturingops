using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// Records how a money source (a <see cref="TenantPayment"/> or a <see cref="CreditNote"/>) is
/// applied against a specific invoice. A single payment or credit note can be split across many
/// invoices; the unallocated remainder stays as customer credit. This table is the single source
/// of truth for an invoice's paid amount — invoice balances are recomputed from active allocations.
/// </summary>
public sealed class PaymentAllocation : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    public string SourceType { get; set; } = "Payment"; // Payment | CreditNote
    /// <summary>TenantPayment.Id when SourceType = Payment, CreditNote.Id when SourceType = CreditNote.</summary>
    public Guid SourceId { get; set; }

    public Guid InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public DateTime AllocatedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Active"; // Active | Reversed

    public Tenant? Tenant { get; set; }
    public TenantInvoice? Invoice { get; set; }
}

public static class AllocationSourceTypes
{
    public const string Payment = "Payment";
    public const string CreditNote = "CreditNote";
    public const string Withholding = "Withholding";
}
