using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class PlatformExpense : AuditableEntity
{
    public DateTime ExpenseDate { get; set; } = DateTime.UtcNow;
    public string Category { get; set; } = string.Empty;
    public string? Vendor { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "KES";
    public string PaymentMethod { get; set; } = "Mpesa";
    public string? AttachmentUrl { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime? ApprovedAt { get; set; }
    public Guid? TenantId { get; set; }

    public Tenant? Tenant { get; set; }
}
