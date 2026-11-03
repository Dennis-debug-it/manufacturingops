using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class PlatformQuotation : AuditableEntity
{
    public string QuotationNumber { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string Currency { get; set; } = "KES";
    public decimal Subtotal { get; set; }
    public decimal DiscountRate { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime? ValidUntil { get; set; }
    public string? Notes { get; set; }
    public Guid? ConvertedInvoiceId { get; set; }

    public Tenant? Tenant { get; set; }
    public ICollection<PlatformQuotationLine> Lines { get; set; } = new List<PlatformQuotationLine>();
}
