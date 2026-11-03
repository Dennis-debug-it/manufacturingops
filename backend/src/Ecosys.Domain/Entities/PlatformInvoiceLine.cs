using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class PlatformInvoiceLine : AuditableEntity
{
    public Guid InvoiceId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public bool Taxable { get; set; } = true;
    public decimal LineTotal { get; set; }

    public PlatformInvoice? Invoice { get; set; }
}
