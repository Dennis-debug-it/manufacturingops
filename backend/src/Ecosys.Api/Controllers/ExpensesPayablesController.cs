using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class ExpensesPayablesController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IDocumentNumberingService documentNumberingService,
    IAuditLogService auditLogService,
    IApprovalWorkflowService approvalWorkflowService,
    IUserAccessService userAccessService) : TenantAwareControllerBase(tenantContext)
{
    // ── Expense claims ─────────────────────────────────────────────────────────

    [HttpGet("expenses")]
    public async Task<ActionResult<IReadOnlyCollection<ExpenseListItemResponse>>> ListExpenses(
        [FromQuery] string? status, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.FinanceView, PermissionNames.FinanceManage);

        var query = dbContext.Expenses.Where(x => x.TenantId == TenantId);
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.Status == status.Trim());

        var expenses = await query.OrderByDescending(x => x.IncurredAt).Take(500).ToListAsync(cancellationToken);
        return Ok(expenses.Select(MapExpenseListItem).ToList());
    }

    [HttpGet("expenses/{id:guid}")]
    public async Task<ActionResult<ExpenseDetailResponse>> GetExpense(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.FinanceView, PermissionNames.FinanceManage);
        var expense = await LoadExpenseAsync(id, cancellationToken);
        return Ok(new ExpenseDetailResponse(
            expense.Id, expense.ClaimNumber, expense.SubmittedByName, expense.Category, expense.Amount,
            expense.Currency, expense.IncurredAt, expense.Status,
            expense.Description, expense.ReceiptUrl, expense.ApprovedByName, expense.ApprovedAt));
    }

    [HttpPost("expenses")]
    public async Task<ActionResult<ExpenseListItemResponse>> CreateExpense(
        [FromBody] CreateExpenseRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManage);

        if (string.IsNullOrWhiteSpace(request.Category))
            throw new BusinessRuleException("Category is required.");
        if (request.Amount <= 0)
            throw new BusinessRuleException("Amount must be greater than zero.");

        var submittedByName = await dbContext.Users
            .Where(x => x.Id == UserId).Select(x => x.FullName).FirstOrDefaultAsync(cancellationToken) ?? "Unknown";

        var expense = new Expense
        {
            TenantId = TenantId,
            ClaimNumber = await documentNumberingService.GenerateAsync(TenantId, null, DocumentTypes.Expense, cancellationToken),
            SubmittedByUserId = UserId,
            SubmittedByName = submittedByName,
            Category = request.Category.Trim(),
            Amount = request.Amount,
            IncurredAt = DateTime.SpecifyKind(request.IncurredAt, DateTimeKind.Utc),
            Status = "Submitted",
            Description = request.Description?.Trim(),
        };
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "finance.expense.submitted", nameof(Expense), expense.Id.ToString(), $"Expense {expense.ClaimNumber} submitted ({expense.Currency} {expense.Amount:N2})", cancellationToken);

        // If an approval matrix is configured for expenses, start the chain. Otherwise the
        // existing single-decision flow (POST decision) continues to apply unchanged.
        await approvalWorkflowService.StartAsync(
            TenantId, ApprovalDocumentTypes.Expense, expense.Id, expense.ClaimNumber,
            expense.Amount, expense.Currency, UserId, cancellationToken);

        return Ok(MapExpenseListItem(expense));
    }

    [HttpPost("expenses/{id:guid}/decision")]
    public async Task<IActionResult> DecideExpense(
        Guid id, [FromBody] ExpenseDecisionRequest request, CancellationToken cancellationToken)
    {
        var expense = await LoadExpenseAsync(id, cancellationToken);

        if (expense.Status is not ("Submitted" or "Draft"))
            throw new BusinessRuleException($"A {expense.Status.ToLowerInvariant()} expense cannot be decided.");

        // When an approval matrix governs this expense, route the decision through the
        // workflow (which enforces the per-level approver rules and applies the outcome).
        var activeRequest = await approvalWorkflowService.GetActiveForDocumentAsync(
            TenantId, ApprovalDocumentTypes.Expense, expense.Id, cancellationToken);
        if (activeRequest is not null)
        {
            await approvalWorkflowService.DecideAsync(TenantId, activeRequest.Id, request.Approve, null, cancellationToken);
            return NoContent();
        }

        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.FinanceManageApprovals, PermissionNames.FinanceManage);

        var approverName = await dbContext.Users
            .Where(x => x.Id == UserId).Select(x => x.FullName).FirstOrDefaultAsync(cancellationToken) ?? "Unknown";

        expense.Status = request.Approve ? "Approved" : "Rejected";
        expense.ApprovedByUserId = UserId;
        expense.ApprovedByName = approverName;
        expense.ApprovedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "finance.expense.decided", nameof(Expense), expense.Id.ToString(), $"Expense {expense.ClaimNumber} {expense.Status.ToLowerInvariant()}", cancellationToken);

        return NoContent();
    }

    // ── Payables (supplier bills) ──────────────────────────────────────────────

    [HttpGet("payables")]
    public async Task<ActionResult<IReadOnlyCollection<PayableListItemResponse>>> ListPayables(
        [FromQuery] string? status, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.FinanceView, PermissionNames.FinanceManage);

        var bills = await dbContext.SupplierBills
            .Include(x => x.Supplier)
            .Where(x => x.TenantId == TenantId)
            .OrderByDescending(x => x.DueDate)
            .Take(500)
            .ToListAsync(cancellationToken);

        var items = bills.Select(MapPayableListItem);
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
            items = items.Where(x => x.Status == status.Trim());

        return Ok(items.ToList());
    }

    [HttpGet("payables/{id:guid}")]
    public async Task<ActionResult<PayableDetailResponse>> GetPayable(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.FinanceView, PermissionNames.FinanceManage);
        var bill = await LoadBillAsync(id, cancellationToken);
        var item = MapPayableListItem(bill);
        return Ok(new PayableDetailResponse(
            item.Id, item.BillNumber, item.SupplierName, item.Amount, item.AmountPaid, item.Currency,
            item.DueDate, item.Status, bill.Notes,
            bill.Payments.OrderByDescending(p => p.PaidAt)
                .Select(p => new PayablePaymentResponse(p.PaidAt, p.Amount, p.Method, p.Reference))
                .ToList()));
    }

    [HttpPost("payables")]
    public async Task<ActionResult<PayableListItemResponse>> CreatePayable(
        [FromBody] CreatePayableRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManage);

        var supplier = await dbContext.Suppliers
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == request.SupplierId, cancellationToken)
            ?? throw new NotFoundException("Supplier not found.");
        if (request.Amount <= 0)
            throw new BusinessRuleException("Amount must be greater than zero.");

        var bill = new SupplierBill
        {
            TenantId = TenantId,
            BillNumber = await documentNumberingService.GenerateAsync(TenantId, null, DocumentTypes.SupplierBill, cancellationToken),
            SupplierId = supplier.Id,
            Amount = request.Amount,
            AmountPaid = 0,
            DueDate = DateTime.SpecifyKind(request.DueDate, DateTimeKind.Utc),
            Status = "Open",
            Notes = request.Notes?.Trim(),
        };
        dbContext.SupplierBills.Add(bill);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "finance.payable.created", nameof(SupplierBill), bill.Id.ToString(), $"Bill {bill.BillNumber} for {supplier.SupplierName} ({bill.Currency} {bill.Amount:N2})", cancellationToken);

        bill.Supplier = supplier;
        return Ok(MapPayableListItem(bill));
    }

    [HttpPost("payables/{id:guid}/payments")]
    public async Task<IActionResult> RecordPayablePayment(
        Guid id, [FromBody] RecordPayablePaymentRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManage);
        var bill = await LoadBillAsync(id, cancellationToken);

        if (bill.Status == "Paid")
            throw new BusinessRuleException("This bill is already fully paid.");
        if (request.Amount <= 0)
            throw new BusinessRuleException("Payment amount must be greater than zero.");
        var outstanding = bill.Amount - bill.AmountPaid;
        if (request.Amount > outstanding)
            throw new BusinessRuleException($"Only {bill.Currency} {outstanding:N2} remains outstanding.");

        dbContext.SupplierBillPayments.Add(new SupplierBillPayment
        {
            TenantId = TenantId,
            SupplierBillId = bill.Id,
            Amount = request.Amount,
            Method = string.IsNullOrWhiteSpace(request.Method) ? "Bank Transfer" : request.Method.Trim(),
            Reference = request.Reference?.Trim(),
            PaidAt = DateTime.UtcNow,
        });

        bill.AmountPaid += request.Amount;
        bill.Status = bill.AmountPaid >= bill.Amount ? "Paid" : "PartiallyPaid";
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "finance.payable.paid", nameof(SupplierBill), bill.Id.ToString(), $"Paid {bill.Currency} {request.Amount:N2} on {bill.BillNumber}", cancellationToken);

        return NoContent();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<Expense> LoadExpenseAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.Expenses.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
        ?? throw new NotFoundException("Expense not found.");

    private async Task<SupplierBill> LoadBillAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.SupplierBills
            .Include(x => x.Supplier)
            .Include(x => x.Payments)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
        ?? throw new NotFoundException("Payable not found.");

    private static ExpenseListItemResponse MapExpenseListItem(Expense e) => new(
        e.Id, e.ClaimNumber, e.SubmittedByName, e.Category, e.Amount, e.Currency, e.IncurredAt, e.Status);

    private static PayableListItemResponse MapPayableListItem(SupplierBill b)
    {
        // Overdue is derived: unpaid balance past the due date.
        var status = b.Status != "Paid" && b.DueDate.Date < DateTime.UtcNow.Date ? "Overdue" : b.Status;
        return new PayableListItemResponse(
            b.Id, b.BillNumber, b.Supplier?.SupplierName ?? "Unknown", b.Amount, b.AmountPaid, b.Currency, b.DueDate, status);
    }
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public sealed record ExpenseListItemResponse(
    Guid Id, string ClaimNumber, string SubmittedByName, string Category, decimal Amount,
    string Currency, DateTime IncurredAt, string Status);

public sealed record ExpenseDetailResponse(
    Guid Id, string ClaimNumber, string SubmittedByName, string Category, decimal Amount,
    string Currency, DateTime IncurredAt, string Status,
    string? Description, string? ReceiptUrl, string? ApprovedByName, DateTime? ApprovedAt);

public sealed record CreateExpenseRequest(string Category, decimal Amount, DateTime IncurredAt, string? Description = null);

public sealed record ExpenseDecisionRequest(bool Approve);

public sealed record PayableListItemResponse(
    Guid Id, string BillNumber, string SupplierName, decimal Amount, decimal AmountPaid,
    string Currency, DateTime DueDate, string Status);

public sealed record PayablePaymentResponse(DateTime PaidAt, decimal Amount, string Method, string? Reference);

public sealed record PayableDetailResponse(
    Guid Id, string BillNumber, string SupplierName, decimal Amount, decimal AmountPaid,
    string Currency, DateTime DueDate, string Status, string? Notes,
    IReadOnlyList<PayablePaymentResponse> PaymentHistory);

public sealed record CreatePayableRequest(Guid SupplierId, decimal Amount, DateTime DueDate, string? Notes = null);

public sealed record RecordPayablePaymentRequest(decimal Amount, string Method, string? Reference = null);
