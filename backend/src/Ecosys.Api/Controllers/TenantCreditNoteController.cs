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
[Route("api/credit-notes")]
public sealed class TenantCreditNoteController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IDocumentNumberingService documentNumberingService,
    IAuditLogService auditLogService,
    IUserAccessService userAccessService) : TenantAwareControllerBase(tenantContext)
{
    private static readonly HashSet<string> Reasons =
        new(StringComparer.OrdinalIgnoreCase) { "Return", "Overcharge", "Goodwill", "Cancellation", "Other" };

    // ── List ─────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<CreditNoteListItem>>> List(
        [FromQuery] string? status,
        [FromQuery] Guid? clientId,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceView);

        var query = dbContext.CreditNotes.Include(x => x.Client).Where(x => x.TenantId == TenantId);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status.Trim());
        if (clientId.HasValue)
            query = query.Where(x => x.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(x => x.CreditNoteNumber.ToLower().Contains(s)
                                     || (x.Client != null && x.Client.ClientName.ToLower().Contains(s)));
        }

        var notes = await query.OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
        var invoiceNumbers = await LoadInvoiceNumbersAsync(notes.Select(x => x.InvoiceId), cancellationToken);
        return Ok(notes.Select(x => MapListItem(x, invoiceNumbers)).ToList());
    }

    // ── Detail ─────────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CreditNoteDetailResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceView);
        var note = await LoadAsync(id, cancellationToken);
        return Ok(await MapDetailAsync(note, cancellationToken));
    }

    // ── Create draft ────────────────────────────────────────────────────────────

    [HttpPost]
    public async Task<ActionResult<CreditNoteDetailResponse>> Create(
        [FromBody] CreateCreditNoteRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);

        if (!Reasons.Contains(request.Reason))
            throw new BusinessRuleException("Reason must be Return, Overcharge, Goodwill, Cancellation, or Other.");
        if (request.Lines is null || request.Lines.Count == 0)
            throw new BusinessRuleException("A credit note needs at least one line.");

        var client = await dbContext.Clients
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == request.ClientId, cancellationToken)
            ?? throw new NotFoundException("Client not found.");

        var currency = string.IsNullOrWhiteSpace(request.Currency) ? "KES" : request.Currency.Trim().ToUpper();

        if (request.InvoiceId.HasValue)
        {
            var invoice = await dbContext.TenantInvoices
                .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == request.InvoiceId.Value, cancellationToken)
                ?? throw new NotFoundException("Linked invoice not found.");
            if (invoice.ClientId != client.Id)
                throw new BusinessRuleException("The linked invoice belongs to a different client.");
            currency = invoice.Currency;
        }

        var note = new CreditNote
        {
            TenantId = TenantId,
            ClientId = client.Id,
            InvoiceId = request.InvoiceId,
            CreditNoteNumber = await documentNumberingService.GenerateAsync(TenantId, null, DocumentTypes.CreditNote, cancellationToken),
            Reason = request.Reason.Trim(),
            Status = "Draft",
            IssueDate = DateTime.SpecifyKind(request.IssueDate, DateTimeKind.Utc),
            Notes = request.Notes?.Trim(),
            Currency = currency,
        };
        AddLines(note, request.Lines);
        RecalcTotals(note);

        dbContext.CreditNotes.Add(note);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "finance.credit_note.created", nameof(CreditNote), note.Id.ToString(), $"Created credit note {note.CreditNoteNumber}", cancellationToken);

        await dbContext.Entry(note).Reference(x => x.Client).LoadAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = note.Id }, await MapDetailAsync(note, cancellationToken));
    }

    // ── Update draft ────────────────────────────────────────────────────────────

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CreditNoteDetailResponse>> Update(
        Guid id, [FromBody] CreateCreditNoteRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);
        var note = await LoadAsync(id, cancellationToken);
        if (note.Status != "Draft")
            throw new BusinessRuleException("Only draft credit notes can be edited.");
        if (!Reasons.Contains(request.Reason))
            throw new BusinessRuleException("Reason must be Return, Overcharge, Goodwill, Cancellation, or Other.");
        if (request.Lines is null || request.Lines.Count == 0)
            throw new BusinessRuleException("A credit note needs at least one line.");

        note.Reason = request.Reason.Trim();
        note.IssueDate = DateTime.SpecifyKind(request.IssueDate, DateTimeKind.Utc);
        note.Notes = request.Notes?.Trim();

        dbContext.CreditNoteLines.RemoveRange(note.Lines);
        note.Lines.Clear();
        AddLines(note, request.Lines);
        RecalcTotals(note);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await MapDetailAsync(note, cancellationToken));
    }

    // ── Issue ────────────────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/issue")]
    public async Task<ActionResult<CreditNoteDetailResponse>> Issue(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);
        var note = await LoadAsync(id, cancellationToken);
        if (note.Status != "Draft")
            throw new BusinessRuleException("Only draft credit notes can be issued.");
        if (note.TotalAmount <= 0)
            throw new BusinessRuleException("A credit note must have a positive total to be issued.");

        note.Status = "Issued";
        note.AmountApplied = 0;
        note.Balance = note.TotalAmount;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "finance.credit_note.issued", nameof(CreditNote), note.Id.ToString(), $"Issued credit note {note.CreditNoteNumber}", cancellationToken);
        return Ok(await MapDetailAsync(note, cancellationToken));
    }

    // ── Apply to invoices ─────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/apply")]
    public async Task<ActionResult<CreditNoteDetailResponse>> Apply(
        Guid id, [FromBody] ApplyCreditNoteRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);
        var note = await LoadAsync(id, cancellationToken);
        if (note.Status is not ("Issued" or "PartiallyApplied"))
            throw new BusinessRuleException("Only issued credit notes can be applied.");
        if (note.Balance <= 0)
            throw new BusinessRuleException("This credit note has no remaining balance.");

        var requested = (request.Allocations ?? []).Where(a => a.Amount > 0).ToList();
        if (requested.Count == 0)
            throw new BusinessRuleException("Nothing to apply.");

        var invoiceIds = requested.Select(a => a.InvoiceId).Distinct().ToList();
        var invoices = await dbContext.TenantInvoices
            .Where(i => i.TenantId == TenantId && invoiceIds.Contains(i.Id))
            .ToListAsync(cancellationToken);

        var items = new List<(TenantInvoice Invoice, decimal Amount)>();
        var remaining = note.Balance;
        foreach (var input in requested)
        {
            if (remaining <= 0) break;
            var invoice = invoices.SingleOrDefault(i => i.Id == input.InvoiceId)
                ?? throw new NotFoundException($"Invoice {input.InvoiceId} not found.");
            if (invoice.ClientId != note.ClientId)
                throw new BusinessRuleException("Credit notes can only be applied to the same client's invoices.");
            if (invoice.Status == "Void")
                throw new BusinessRuleException($"Invoice {invoice.InvoiceNumber} is voided.");
            if (!string.Equals(invoice.Currency, note.Currency, StringComparison.OrdinalIgnoreCase))
                throw new BusinessRuleException($"Invoice {invoice.InvoiceNumber} currency differs from the credit note.");

            var capped = Math.Min(Math.Min(input.Amount, Math.Max(invoice.Balance, 0m)), remaining);
            if (capped > 0)
            {
                items.Add((invoice, capped));
                remaining -= capped;
            }
        }

        if (items.Count == 0)
            throw new BusinessRuleException("Nothing to apply — the selected invoices are already settled.");

        await InvoiceBalanceCalculator.ApplyAllocationsAsync(
            dbContext, TenantId, AllocationSourceTypes.CreditNote, note.Id, items, cancellationToken);

        var applied = items.Sum(i => i.Amount);
        note.AmountApplied += applied;
        note.Balance = note.TotalAmount - note.AmountApplied;
        note.Status = note.Balance <= 0 ? "Applied" : "PartiallyApplied";

        var client = await dbContext.Clients.SingleOrDefaultAsync(c => c.TenantId == TenantId && c.Id == note.ClientId, cancellationToken);
        if (client is not null)
            await InvoiceBalanceCalculator.RecalcClientBalanceAsync(dbContext, client, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "finance.credit_note.applied", nameof(CreditNote), note.Id.ToString(),
            $"Applied {applied:N2} of credit note {note.CreditNoteNumber} to {items.Count} invoice(s)", cancellationToken);
        return Ok(await MapDetailAsync(note, cancellationToken));
    }

    // ── Void ────────────────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/void")]
    public async Task<ActionResult<CreditNoteDetailResponse>> Void(
        Guid id, [FromBody] VoidCreditNoteRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);
        var note = await LoadAsync(id, cancellationToken);
        if (note.Status == "Void")
            throw new BusinessRuleException("Credit note is already voided.");
        if (note.AmountApplied > 0)
            throw new BusinessRuleException("Reverse the applications before voiding this credit note.");

        note.Status = "Void";
        note.Balance = 0;
        if (!string.IsNullOrWhiteSpace(request.Reason))
            note.Notes = $"VOIDED: {request.Reason.Trim()}\n{note.Notes}".Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "finance.credit_note.voided", nameof(CreditNote), note.Id.ToString(), $"Voided credit note {note.CreditNoteNumber}", cancellationToken);
        return Ok(await MapDetailAsync(note, cancellationToken));
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<CreditNote> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.CreditNotes
            .Include(x => x.Client)
            .Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
        ?? throw new NotFoundException("Credit note not found.");

    private static void AddLines(CreditNote note, IEnumerable<CreateCreditNoteLineRequest> lines)
    {
        foreach (var line in lines)
        {
            var lineTotal = line.Quantity * line.UnitRate;
            var taxAmt = lineTotal * (line.TaxRate / 100m);
            note.Lines.Add(new CreditNoteLine
            {
                Description = line.Description.Trim(),
                ItemType = line.ItemType.Trim(),
                Quantity = line.Quantity,
                Unit = line.Unit.Trim(),
                UnitRate = line.UnitRate,
                LineTotal = lineTotal,
                TaxRate = line.TaxRate,
                TaxAmount = taxAmt,
                GrandTotal = lineTotal + taxAmt,
            });
        }
    }

    private static void RecalcTotals(CreditNote note)
    {
        note.Subtotal = note.Lines.Sum(x => x.LineTotal);
        note.TaxAmount = note.Lines.Sum(x => x.TaxAmount);
        note.TotalAmount = note.Lines.Sum(x => x.GrandTotal);
        note.Balance = note.TotalAmount - note.AmountApplied;
    }

    private async Task<Dictionary<Guid, string>> LoadInvoiceNumbersAsync(
        IEnumerable<Guid?> invoiceIds, CancellationToken cancellationToken)
    {
        var ids = invoiceIds.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, string>();
        return await dbContext.TenantInvoices
            .Where(i => i.TenantId == TenantId && ids.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.InvoiceNumber, cancellationToken);
    }

    private static CreditNoteListItem MapListItem(CreditNote x, IReadOnlyDictionary<Guid, string> invoiceNumbers) =>
        new(x.Id, x.ClientId, x.Client?.ClientName, x.CreditNoteNumber, x.InvoiceId,
            x.InvoiceId.HasValue && invoiceNumbers.TryGetValue(x.InvoiceId.Value, out var n) ? n : null,
            x.Reason, x.Status, x.IssueDate, x.TotalAmount, x.AmountApplied, x.Balance, x.Currency, x.CreatedAt);

    private async Task<CreditNoteDetailResponse> MapDetailAsync(CreditNote x, CancellationToken cancellationToken)
    {
        var invoiceNumbers = await LoadInvoiceNumbersAsync(new[] { x.InvoiceId }, cancellationToken);

        var applications = await dbContext.PaymentAllocations
            .Where(a => a.TenantId == TenantId && a.SourceType == AllocationSourceTypes.CreditNote
                        && a.SourceId == x.Id && a.Status == "Active")
            .OrderBy(a => a.AllocatedAt)
            .Select(a => new
            {
                a.InvoiceId,
                a.Amount,
                a.AllocatedAt,
                InvoiceNumber = dbContext.TenantInvoices.Where(i => i.Id == a.InvoiceId).Select(i => i.InvoiceNumber).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return new CreditNoteDetailResponse(
            x.Id, x.ClientId, x.Client?.ClientName, x.CreditNoteNumber, x.InvoiceId,
            x.InvoiceId.HasValue && invoiceNumbers.TryGetValue(x.InvoiceId.Value, out var n) ? n : null,
            x.Reason, x.Status, x.IssueDate, x.Notes,
            x.Subtotal, x.TaxAmount, x.TotalAmount, x.AmountApplied, x.Balance, x.Currency, x.CreatedAt,
            x.Lines.Select(l => new CreditNoteLineResponse(
                l.Id, l.Description, l.ItemType, l.Quantity, l.Unit, l.UnitRate,
                l.LineTotal, l.TaxRate, l.TaxAmount, l.GrandTotal)).ToList(),
            applications.Select(a => new CreditNoteApplicationResponse(a.InvoiceId, a.InvoiceNumber, a.Amount, a.AllocatedAt)).ToList());
    }
}
