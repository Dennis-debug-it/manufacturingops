namespace Ecosys.Api.Controllers;

// ── Request models ───────────────────────────────────────────────────────────

public sealed record CreateInvoiceRequest(
    Guid ClientId,
    DateTime InvoiceDate,
    DateTime DueDate,
    string? Notes,
    string? TermsAndConditions,
    string? Currency,
    Guid? CreditTermId,
    List<CreateInvoiceLineRequest> Lines);

public sealed record CreateInvoiceLineRequest(
    string Description,
    string ItemType,
    decimal Quantity,
    string Unit,
    decimal UnitRate,
    decimal TaxRate,
    Guid? BillableItemId,
    Guid? WorkOrderId);

public sealed record VoidInvoiceRequest(string? Reason);

public sealed record BuildProformaInvoiceRequest(
    List<Guid>? QuotationIds,
    DateTime InvoiceDate,
    DateTime DueDate,
    string? Notes,
    string? TermsAndConditions,
    string? Currency);

public sealed record RecordPaymentRequest(
    decimal Amount,
    DateTime PaymentDate,
    string Method,
    string? Reference,
    string? Notes);

// ── Response models ──────────────────────────────────────────────────────────

public sealed record TenantInvoiceListItem(
    Guid Id,
    Guid TenantId,
    Guid ClientId,
    string? ClientName,
    string InvoiceNumber,
    string DocumentType,
    string Status,
    DateTime InvoiceDate,
    DateTime DueDate,
    decimal Subtotal,
    decimal TaxAmount,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal Balance,
    string Currency,
    DateTime CreatedAt,
    Guid? CreditTermId = null,
    string? CreditTermName = null);

public sealed record TenantInvoiceDetailResponse(
    Guid Id,
    Guid TenantId,
    Guid ClientId,
    string? ClientName,
    string InvoiceNumber,
    string DocumentType,
    string Status,
    DateTime InvoiceDate,
    DateTime DueDate,
    string? Notes,
    string? TermsAndConditions,
    decimal Subtotal,
    decimal TaxAmount,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal Balance,
    string Currency,
    DateTime CreatedAt,
    IReadOnlyCollection<TenantInvoiceLineResponse> Lines,
    IReadOnlyCollection<TenantPaymentResponse> Payments,
    Guid? CreditTermId = null,
    string? CreditTermName = null,
    decimal? DepositAmount = null,
    bool DepositPaid = false,
    DateTime? DepositDueDate = null,
    bool CreditLimitWarning = false,
    decimal ClientCurrentBalance = 0,
    decimal? ClientCreditLimit = null,
    IReadOnlyCollection<TenantInvoiceSourceQuotationResponse>? SourceQuotations = null);

public sealed record TenantInvoiceSourceQuotationResponse(
    Guid Id,
    string QuoteNumber,
    string Status,
    string? Subject,
    DateTime QuoteDate,
    decimal TotalAmount,
    string Currency);

public sealed record TenantInvoiceLineResponse(
    Guid Id,
    Guid InvoiceId,
    Guid? BillableItemId,
    Guid? WorkOrderId,
    string Description,
    string ItemType,
    decimal Quantity,
    string Unit,
    decimal UnitRate,
    decimal LineTotal,
    decimal TaxRate,
    decimal TaxAmount,
    decimal GrandTotal);

public sealed record TenantPaymentListItemResponse(
    Guid Id,
    Guid TenantId,
    Guid? InvoiceId,
    Guid ClientId,
    string PaymentNumber,
    decimal Amount,
    DateTime PaymentDate,
    string Method,
    string? Reference,
    string? Notes,
    string Status,
    DateTime CreatedAt,
    string? InvoiceNumber,
    string? ClientName,
    decimal AmountAllocated = 0,
    decimal UnallocatedAmount = 0);

public sealed record TenantPaymentResponse(
    Guid Id,
    Guid TenantId,
    Guid? InvoiceId,
    Guid ClientId,
    string PaymentNumber,
    decimal Amount,
    DateTime PaymentDate,
    string Method,
    string? Reference,
    string? Notes,
    string Status,
    DateTime CreatedAt);

// ── Payment allocation ───────────────────────────────────────────────────────

public sealed record PaymentAllocationInput(Guid InvoiceId, decimal Amount);

/// <summary>Receive a payment from a client and allocate it across one or more invoices.</summary>
public sealed record ReceivePaymentRequest(
    Guid ClientId,
    decimal Amount,
    DateTime PaymentDate,
    string Method,
    string? Reference,
    string? Notes,
    List<PaymentAllocationInput>? Allocations);

/// <summary>Allocate (or top up allocations of) an existing payment across invoices.</summary>
public sealed record AllocatePaymentRequest(List<PaymentAllocationInput> Allocations);

public sealed record ClientCreditBalanceResponse(
    Guid ClientId,
    string? ClientName,
    decimal CurrentBalance,
    decimal AvailableCredit,
    string Currency);
