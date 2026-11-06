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
    public Guid? ConvertedToProjectId { get; set; }
    public string? RejectionReason { get; set; }
    /// <summary>The client's acceptance in the portal: the name they typed as their signature, when, and from where.</summary>
    public string? AcceptedByName { get; set; }
    public string? AcceptedByTitle { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public string? AcceptedFromIp { get; set; }
    public Guid? AcceptedByPortalUserId { get; set; }
    /// <summary>When the quotation was sent to the client, and by whom; the sales chase list counts from here.</summary>
    public DateTime? SentAt { get; set; }
    public Guid? SentByUserId { get; set; }
    /// <summary>Chase reminders already sent while it waits for an answer: 1 once it has waited a few days, 2 as it nears expiry.</summary>
    public int ChaseReminderStage { get; set; }

    public Tenant? Tenant { get; set; }
    public Client? Client { get; set; }
    public Site? Site { get; set; }
    public TenantInvoice? ConvertedToInvoice { get; set; }
    public WorkOrder? ConvertedToWorkOrder { get; set; }
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
