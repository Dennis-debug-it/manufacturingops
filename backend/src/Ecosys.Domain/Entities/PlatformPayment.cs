using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class PlatformPayment : AuditableEntity
{
    public string PaymentNumber { get; set; } = string.Empty;
    public Guid? InvoiceId { get; set; }
    public Guid? TenantId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "KES";
    public string Method { get; set; } = "Mpesa";
    public string Status { get; set; } = "Pending";
    public string? Reference { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }

    public PlatformInvoice? Invoice { get; set; }
    public Tenant? Tenant { get; set; }
}
