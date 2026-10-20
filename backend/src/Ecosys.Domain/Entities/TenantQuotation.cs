using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class TenantQuotation : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ClientId { get; set; }
    public Guid? SiteId { get; set; }
    public string QuoteNumber { get; set; } = string.Empty; // QT-2026-00001
    public string Status { get; set; } = "Draft"; // Draft|Sent|Accepted|Rejected|Expired|Converted
    public DateTime QuoteDate { get; set; }
    public DateTime ValidUntil { get; set; }
    public string? Subject { get; set; }
    public string? Notes { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "KES";
    public Guid? ConvertedToInvoiceId { get; set; }
    public Guid? ConvertedToWorkOrderId { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? SalesOpportunityId { get; set; }

    public Tenant? Tenant { get; set; }
    public Client? Client { get; set; }
    public Site? Site { get; set; }
    public TenantInvoice? ConvertedToInvoice { get; set; }
    public WorkOrder? ConvertedToWorkOrder { get; set; }
    public SalesOpportunity? SalesOpportunity { get; set; }
    public ICollection<TenantQuotationLine> Lines { get; set; } = new List<TenantQuotationLine>();
}

public sealed class TenantQuotationLine : AuditableEntity
{
    public Guid QuotationId { get; set; }
    public Guid? RateCardId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ItemType { get; set; } = "Service";
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "unit";
    public decimal UnitRate { get; set; }
    public decimal LineTotal { get; set; }
    public decimal TaxRate { get; set; }
    public decimal GrandTotal { get; set; }

    public TenantQuotation? Quotation { get; set; }
    public RateCard? RateCard { get; set; }
}
