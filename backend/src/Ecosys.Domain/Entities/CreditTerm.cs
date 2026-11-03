using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class CreditTerm : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;          // "Net 30", "Net 60", "Due on Receipt"
    public string Code { get; set; } = string.Empty;          // "NET30", "NET60", "DOR"
    public int NetDays { get; set; } = 30;                     // days from invoice date to due date
    public bool IsDepositRequired { get; set; } = false;       // requires upfront payment
    public decimal DepositPercent { get; set; } = 0;           // e.g. 50 = 50% upfront
    public decimal? CreditLimit { get; set; }                  // max outstanding balance allowed
    public string? Description { get; set; }                   // "Payment due within 30 days"
    public bool IsDefault { get; set; } = false;               // tenant's default term
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
    public ICollection<Client> Clients { get; set; } = new List<Client>();
    public ICollection<TenantInvoice> Invoices { get; set; } = new List<TenantInvoice>();
}
