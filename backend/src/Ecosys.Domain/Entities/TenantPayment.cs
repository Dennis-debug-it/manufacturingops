using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class TenantPayment : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid ClientId { get; set; }
    public string PaymentNumber { get; set; } = string.Empty; // RCT-2026-00001
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public string Method { get; set; } = "Cash"; // Cash|MPesa|BankTransfer|Cheque|Card
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "Confirmed"; // Confirmed|Reversed

    public Tenant? Tenant { get; set; }
    public TenantInvoice? Invoice { get; set; }
    public Client? Client { get; set; }
}
