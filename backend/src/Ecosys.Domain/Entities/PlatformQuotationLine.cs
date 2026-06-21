using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class PlatformQuotationLine : AuditableEntity
{
    public Guid QuotationId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public bool Taxable { get; set; } = true;
    public decimal LineTotal { get; set; }

    public PlatformQuotation? Quotation { get; set; }
}
