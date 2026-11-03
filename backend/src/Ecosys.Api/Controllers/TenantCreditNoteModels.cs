namespace Ecosys.Api.Controllers;

// ── Request models ───────────────────────────────────────────────────────────

public sealed record CreateCreditNoteLineRequest(
    string Description,
    string ItemType,
    decimal Quantity,
    string Unit,
    decimal UnitRate,
    decimal TaxRate);

public sealed record CreateCreditNoteRequest(
    Guid ClientId,
    Guid? InvoiceId,
    string Reason,
    DateTime IssueDate,
    string? Notes,
    string? Currency,
    List<CreateCreditNoteLineRequest> Lines);

public sealed record VoidCreditNoteRequest(string? Reason);

public sealed record ApplyCreditNoteRequest(List<PaymentAllocationInput> Allocations);

// ── Response models ──────────────────────────────────────────────────────────

public sealed record CreditNoteListItem(
    Guid Id,
    Guid ClientId,
    string? ClientName,
    string CreditNoteNumber,
    Guid? InvoiceId,
    string? InvoiceNumber,
    string Reason,
    string Status,
    DateTime IssueDate,
    decimal TotalAmount,
    decimal AmountApplied,
    decimal Balance,
    string Currency,
    DateTime CreatedAt);

public sealed record CreditNoteLineResponse(
    Guid Id,
    string Description,
    string ItemType,
    decimal Quantity,
    string Unit,
    decimal UnitRate,
    decimal LineTotal,
    decimal TaxRate,
    decimal TaxAmount,
    decimal GrandTotal);

public sealed record CreditNoteDetailResponse(
    Guid Id,
    Guid ClientId,
    string? ClientName,
    string CreditNoteNumber,
    Guid? InvoiceId,
    string? InvoiceNumber,
    string Reason,
    string Status,
    DateTime IssueDate,
    string? Notes,
    decimal Subtotal,
    decimal TaxAmount,
    decimal TotalAmount,
    decimal AmountApplied,
    decimal Balance,
    string Currency,
    DateTime CreatedAt,
    IReadOnlyCollection<CreditNoteLineResponse> Lines,
    IReadOnlyCollection<CreditNoteApplicationResponse> Applications);

public sealed record CreditNoteApplicationResponse(
    Guid InvoiceId,
    string? InvoiceNumber,
    decimal Amount,
    DateTime AllocatedAt);
