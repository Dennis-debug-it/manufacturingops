using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>A staff expense claim moving through a submit → approve/reject → reimburse workflow.</summary>
public sealed class Expense : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string ClaimNumber { get; set; } = string.Empty;
    public Guid? SubmittedByUserId { get; set; }
    public string SubmittedByName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "KES";
    public DateTime IncurredAt { get; set; }
    public string Status { get; set; } = "Submitted"; // Draft | Submitted | Approved | Rejected | Reimbursed
    public string? Description { get; set; }
    public string? ReceiptUrl { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public Tenant? Tenant { get; set; }
}

/// <summary>A bill owed to a supplier (accounts payable), settled by one or more payments.</summary>
public sealed class SupplierBill : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string BillNumber { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public Guid? ProjectId { get; set; }
    public decimal Amount { get; set; }
    public decimal AmountPaid { get; set; }
    public string Currency { get; set; } = "KES";
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = "Open"; // Open | PartiallyPaid | Paid
    public string? Notes { get; set; }

    public Tenant? Tenant { get; set; }
    public Supplier? Supplier { get; set; }
    public ICollection<SupplierBillPayment> Payments { get; set; } = new List<SupplierBillPayment>();
}

public sealed class SupplierBillPayment : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid SupplierBillId { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;

    public Tenant? Tenant { get; set; }
    public SupplierBill? SupplierBill { get; set; }
}
