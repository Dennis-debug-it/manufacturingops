using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class PlatformInvoice : AuditableEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public Guid? QuotationId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string Currency { get; set; } = "KES";
    public decimal Subtotal { get; set; }
    public decimal DiscountRate { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Balance { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }
    public string? Notes { get; set; }

    public Tenant? Tenant { get; set; }
    public PlatformQuotation? Quotation { get; set; }
    public ICollection<PlatformInvoiceLine> Lines { get; set; } = new List<PlatformInvoiceLine>();
    public ICollection<PlatformPayment> Payments { get; set; } = new List<PlatformPayment>();
}
