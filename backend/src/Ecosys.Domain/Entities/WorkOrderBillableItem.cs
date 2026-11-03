using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class WorkOrderBillableItem : AuditableEntity, ITenantEntity
{
    public Guid WorkOrderId { get; set; }
    public Guid TenantId { get; set; }
    public string ItemType { get; set; } = "Material";      // Labour|Material|Callout|Service|Transport|Other
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "unit";
    public decimal UnitRate { get; set; }
    public Guid? RateCardId { get; set; }
    public decimal LineTotal { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public bool IsManualEntry { get; set; } = false;
    public bool IsBilled { get; set; } = false;
    public Guid? InvoiceId { get; set; }
    public string? Notes { get; set; }
    /// <summary>Links this item back to the material usage record it was generated from.</summary>
    public Guid? SourceMaterialUsageId { get; set; }
    /// <summary>Links this item back to the time-log record it was generated from.</summary>
    public Guid? SourceTimeLogId { get; set; }

    public Tenant? Tenant { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public RateCard? RateCard { get; set; }
}
