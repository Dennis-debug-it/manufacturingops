using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Ecosys.Shared.Contracts.Integration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/invoices")]
public sealed class TenantInvoiceController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IDocumentNumberingService documentNumberingService,
    IPdfRenderer pdfRenderer,
    IAuditLogService auditLogService,
    IUserAccessService userAccessService,
    IEmailOutboxService emailOutbox,
    IEmailTemplateService emailTemplateService,
    IEmailSubjectRuleService emailSubjectRuleService,
    ILogger<TenantInvoiceController> logger) : TenantAwareControllerBase(tenantContext)
{
    // ── List ─────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<TenantInvoiceListItem>>> ListInvoices(
        [FromQuery] string? status,
        [FromQuery] Guid? clientId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceView);

        var query = dbContext.TenantInvoices
            .Include(x => x.Client)
            .Where(x => x.TenantId == TenantId);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status.Trim());
        if (clientId.HasValue)
            query = query.Where(x => x.ClientId == clientId.Value);
        if (dateFrom.HasValue)
            query = query.Where(x => x.InvoiceDate >= dateFrom.Value);
        if (dateTo.HasValue)
            query = query.Where(x => x.InvoiceDate <= dateTo.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(x =>
                x.InvoiceNumber.ToLower().Contains(s) ||
                (x.Client != null && x.Client.ClientName.ToLower().Contains(s)));
        }

        var invoices = await query.OrderByDescending(x => x.InvoiceDate).ToListAsync(cancellationToken);
        return Ok(invoices.Select(MapListItem).ToList());
    }

    // ── Detail ───────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TenantInvoiceDetailResponse>> GetInvoice(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceView);

        var invoice = await dbContext.TenantInvoices
            .Include(x => x.Client)
            .Include(x => x.Lines).ThenInclude(x => x.WorkOrder)
            .Include(x => x.Payments)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Invoice not found.");

        return Ok(MapDetail(invoice));
    }

    // ── Create ───────────────────────────────────────────────────────────────

    [HttpPost]
    public async Task<ActionResult<TenantInvoiceDetailResponse>> CreateInvoice(
        [FromBody] CreateInvoiceRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);

        var client = await dbContext.Clients
            .Include(x => x.DefaultCreditTerm)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == request.ClientId, cancellationToken)
            ?? throw new NotFoundException("Client not found.");

        // Resolve credit term: explicit on request → client default → null
        CreditTerm? creditTerm = null;
        var resolvedCreditTermId = request.CreditTermId ?? client.DefaultCreditTermId;
        if (resolvedCreditTermId.HasValue)
        {
            creditTerm = await dbContext.CreditTerms
                .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == resolvedCreditTermId.Value && x.IsActive, cancellationToken);
        }

        var invoiceDate = DateTime.SpecifyKind(request.InvoiceDate, DateTimeKind.Utc);
        // Auto-calculate DueDate from NetDays when a credit term is resolved
        var dueDate = creditTerm != null
            ? invoiceDate.AddDays(creditTerm.NetDays)
            : DateTime.SpecifyKind(request.DueDate, DateTimeKind.Utc);

        var invoice = new TenantInvoice
        {
            TenantId = TenantId,
            ClientId = request.ClientId,
            CreditTermId = creditTerm?.Id,
            InvoiceNumber = await documentNumberingService.GenerateAsync(TenantId, null, DocumentTypes.Invoice, cancellationToken),
            Status = "Draft",
            InvoiceDate = invoiceDate,
            DueDate = dueDate,
            Notes = request.Notes?.Trim(),
            TermsAndConditions = request.TermsAndConditions?.Trim(),
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "KES" : request.Currency.Trim().ToUpper(),
        };

        // Deposit fields
        if (creditTerm is { IsDepositRequired: true, DepositPercent: > 0 })
        {
            invoice.DepositDueDate = invoiceDate; // deposit due same day as invoice
        }

        foreach (var line in request.Lines)
        {
            var lineTotal = line.Quantity * line.UnitRate;
            var taxAmt = lineTotal * (line.TaxRate / 100m);
            invoice.Lines.Add(new TenantInvoiceLine
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
                BillableItemId = line.BillableItemId,
                WorkOrderId = line.WorkOrderId,
            });
        }

        RecalcTotals(invoice);

        // Set deposit amount after totals are calculated
        if (creditTerm is { IsDepositRequired: true, DepositPercent: > 0 })
            invoice.DepositAmount = Math.Round(invoice.TotalAmount * creditTerm.DepositPercent / 100m, 2);

        dbContext.TenantInvoices.Add(invoice);

        // Update client's running balance
        client.CurrentBalance += invoice.TotalAmount;

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "finance.invoice.created", nameof(TenantInvoice), invoice.Id.ToString(), $"Created invoice {invoice.InvoiceNumber}", cancellationToken);

        await dbContext.Entry(invoice).Reference(x => x.Client).LoadAsync(cancellationToken);

        // Credit limit soft warning (not a hard block — just signals to UI)
        var effectiveLimit = client.CreditLimit ?? creditTerm?.CreditLimit;
        var creditLimitWarning = effectiveLimit.HasValue && client.CurrentBalance > effectiveLimit.Value;

        return CreatedAtAction(nameof(GetInvoice), new { id = invoice.Id },
            MapDetail(invoice, creditLimitWarning, client.CurrentBalance, effectiveLimit));
    }

    // ── Update ───────────────────────────────────────────────────────────────

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TenantInvoiceDetailResponse>> UpdateInvoice(
        Guid id, [FromBody] CreateInvoiceRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);

        var invoice = await dbContext.TenantInvoices
            .Include(x => x.Client)
            .Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Invoice not found.");

        if (invoice.Status != "Draft")
            throw new BusinessRuleException("Only draft invoices can be edited.");

        invoice.ClientId = request.ClientId;

        // Resolve credit term for update
        CreditTerm? updatedCreditTerm = null;
        if (request.CreditTermId.HasValue)
        {
            updatedCreditTerm = await dbContext.CreditTerms
                .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == request.CreditTermId.Value && x.IsActive, cancellationToken);
        }
        invoice.CreditTermId = updatedCreditTerm?.Id;

        var updatedInvoiceDate = DateTime.SpecifyKind(request.InvoiceDate, DateTimeKind.Utc);
        invoice.InvoiceDate = updatedInvoiceDate;
        invoice.DueDate = updatedCreditTerm != null
            ? updatedInvoiceDate.AddDays(updatedCreditTerm.NetDays)
            : DateTime.SpecifyKind(request.DueDate, DateTimeKind.Utc);

        invoice.Notes = request.Notes?.Trim();
        invoice.TermsAndConditions = request.TermsAndConditions?.Trim();
        invoice.Currency = string.IsNullOrWhiteSpace(request.Currency) ? "KES" : request.Currency.Trim().ToUpper();

        dbContext.TenantInvoiceLines.RemoveRange(invoice.Lines);
        invoice.Lines.Clear();

        foreach (var line in request.Lines)
        {
            var lineTotal = line.Quantity * line.UnitRate;
            var taxAmt = lineTotal * (line.TaxRate / 100m);
            invoice.Lines.Add(new TenantInvoiceLine
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
                BillableItemId = line.BillableItemId,
                WorkOrderId = line.WorkOrderId,
            });
        }

        RecalcTotals(invoice);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapDetail(invoice));
    }

    // ── Send ─────────────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/send")]
    public async Task<ActionResult<TenantInvoiceDetailResponse>> SendInvoice(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);
        var invoice = await LoadInvoiceWithDetailsAsync(id, cancellationToken);
        if (invoice.Status != "Draft")
            throw new BusinessRuleException("Only Draft invoices can be sent.");
        invoice.Status = "Sent";
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "finance.invoice.sent", nameof(TenantInvoice), invoice.Id.ToString(), $"Sent invoice {invoice.InvoiceNumber}", cancellationToken);
        await TryQueueInvoiceSentEmailAsync(invoice, cancellationToken);
        return Ok(MapDetail(invoice));
    }

    // ── Void ─────────────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/void")]
    public async Task<ActionResult<TenantInvoiceDetailResponse>> VoidInvoice(
        Guid id, [FromBody] VoidInvoiceRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);
        var invoice = await LoadInvoiceWithDetailsAsync(id, cancellationToken);
        if (invoice.Status == "Void")
            throw new BusinessRuleException("Invoice is already voided.");
        invoice.Status = "Void";
        if (!string.IsNullOrWhiteSpace(request.Reason))
            invoice.Notes = $"VOIDED: {request.Reason.Trim()}\n{invoice.Notes}".Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "finance.invoice.voided", nameof(TenantInvoice), invoice.Id.ToString(), $"Voided invoice {invoice.InvoiceNumber}", cancellationToken);
        return Ok(MapDetail(invoice));
    }

    // ── PDF ──────────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> GetInvoicePdf(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceView);

        var invoice = await dbContext.TenantInvoices
            .Include(x => x.Client)
            .Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Invoice not found.");

        var tenant = await dbContext.Tenants
            .SingleOrDefaultAsync(x => x.Id == TenantId, cancellationToken);

        var model = new InvoicePdfModel(
            InvoiceNumber:  invoice.InvoiceNumber,
            Status:         invoice.Status,
            InvoiceDate:    invoice.InvoiceDate,
            DueDate:        invoice.DueDate,
            ClientName:     invoice.Client?.ClientName ?? "Unknown",
            ClientAddress:  null,
            ClientEmail:    null,
            Notes:          invoice.Notes,
            Lines:          invoice.Lines.Select(l => new InvoiceLinePdfModel(
                                l.Description, l.ItemType, l.Quantity, l.Unit,
                                l.UnitRate, l.LineTotal, l.TaxRate, l.GrandTotal)).ToList(),
            Subtotal:       invoice.Subtotal,
            TaxAmount:      invoice.TaxAmount,
            TotalAmount:    invoice.TotalAmount,
            AmountPaid:     invoice.AmountPaid,
            Balance:        invoice.Balance,
            Currency:       invoice.Currency,
            CompanyName:    tenant?.CompanyName ?? "Ecosys",
            CompanyAddress: tenant?.Address,
            CompanyPhone:   tenant?.Phone,
            LogoUrl:        tenant?.LogoUrl,
            PrimaryColor:   tenant?.PrimaryColor ?? "#0F4C81"
        );

        var bytes = pdfRenderer.RenderInvoicePdf(model);
        return File(bytes, "application/pdf", $"Invoice-{invoice.InvoiceNumber}.pdf");
    }

    // ── Payments ─────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/payments")]
    public async Task<ActionResult<IReadOnlyCollection<TenantPaymentResponse>>> ListPayments(
        Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceView);
        await EnsureInvoiceAccessAsync(id, cancellationToken);
        var payments = await dbContext.TenantPayments
            .Where(x => x.TenantId == TenantId && x.InvoiceId == id)
            .OrderByDescending(x => x.PaymentDate)
            .ToListAsync(cancellationToken);
        return Ok(payments.Select(MapPayment).ToList());
    }

    [HttpPost("{id:guid}/payments")]
    public async Task<ActionResult<TenantPaymentResponse>> RecordPayment(
        Guid id, [FromBody] RecordPaymentRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);
        var invoice = await LoadInvoiceWithDetailsAsync(id, cancellationToken);

        if (invoice.Status == "Void")
            throw new BusinessRuleException("Cannot record payment on a voided invoice.");

        var payment = new TenantPayment
        {
            TenantId = TenantId,
            InvoiceId = id,
            ClientId = invoice.ClientId,
            PaymentNumber = await documentNumberingService.GenerateAsync(TenantId, null, DocumentTypes.Receipt, cancellationToken),
            Amount = request.Amount,
            PaymentDate = DateTime.SpecifyKind(request.PaymentDate, DateTimeKind.Utc),
            Method = NormalizePaymentMethod(request.Method),
            Reference = request.Reference?.Trim(),
            Notes = request.Notes?.Trim(),
            Status = "Confirmed",
        };

        dbContext.TenantPayments.Add(payment);
        await RecalcInvoicePaymentStatusAsync(invoice, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "finance.payment.recorded", nameof(TenantPayment), payment.Id.ToString(), $"Recorded payment {payment.PaymentNumber} for invoice {invoice.InvoiceNumber}", cancellationToken);

        return CreatedAtAction(nameof(ListPayments), new { id }, MapPayment(payment));
    }

    // ── Reverse Payment ───────────────────────────────────────────────────────

    [HttpDelete("/api/payments/{paymentId:guid}")]
    public async Task<IActionResult> ReversePayment(Guid paymentId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);
        var payment = await dbContext.TenantPayments
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == paymentId, cancellationToken)
            ?? throw new NotFoundException("Payment not found.");

        if (payment.Status == "Reversed")
            throw new BusinessRuleException("Payment is already reversed.");

        payment.Status = "Reversed";
        var invoice = await LoadInvoiceWithDetailsAsync(payment.InvoiceId, cancellationToken);
        await RecalcInvoicePaymentStatusAsync(invoice, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    // ── Receipt PDF ───────────────────────────────────────────────────────────

    [HttpGet("/api/payments/{paymentId:guid}/receipt-pdf")]
    public async Task<IActionResult> GetReceiptPdf(Guid paymentId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceView);

        var payment = await dbContext.TenantPayments
            .Include(x => x.Invoice).ThenInclude(x => x!.Client)
            .Include(x => x.Client)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == paymentId, cancellationToken)
            ?? throw new NotFoundException("Payment not found.");

        var tenant = await dbContext.Tenants
            .SingleOrDefaultAsync(x => x.Id == TenantId, cancellationToken);

        var client = payment.Client ?? payment.Invoice?.Client;

        var model = new ReceiptPdfModel(
            ReceiptNumber:  payment.PaymentNumber,
            InvoiceNumber:  payment.Invoice?.InvoiceNumber ?? "N/A",
            ClientName:     client?.ClientName ?? "Unknown",
            PaymentDate:    payment.PaymentDate,
            Amount:         payment.Amount,
            Currency:       payment.Invoice?.Currency ?? "KES",
            Method:         payment.Method,
            Reference:      payment.Reference,
            CompanyName:    tenant?.CompanyName ?? "Ecosys",
            LogoUrl:        tenant?.LogoUrl,
            PrimaryColor:   tenant?.PrimaryColor ?? "#0F4C81"
        );

        var bytes = pdfRenderer.RenderReceiptPdf(model);
        return File(bytes, "application/pdf", $"Receipt-{payment.PaymentNumber}.pdf");
    }

    // ── Generate invoice from work order ─────────────────────────────────────

    [HttpPost("/api/work-orders/{workOrderId:guid}/generate-invoice")]
    public async Task<ActionResult<TenantInvoiceDetailResponse>> GenerateFromWorkOrder(
        Guid workOrderId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);

        var workOrder = await dbContext.WorkOrders
            .Include(x => x.Client)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == workOrderId, cancellationToken)
            ?? throw new NotFoundException("Work order not found.");

        if (!workOrder.IsBillable)
            throw new BusinessRuleException("This work order is not marked as billable.");

        var unbilledItems = await dbContext.WorkOrderBillableItems
            .Where(x => x.TenantId == TenantId && x.WorkOrderId == workOrderId && !x.IsBilled)
            .ToListAsync(cancellationToken);

        if (unbilledItems.Count == 0)
            throw new BusinessRuleException("No unbilled items found on this work order.");

        var invoice = new TenantInvoice
        {
            TenantId = TenantId,
            ClientId = workOrder.ClientId,
            InvoiceNumber = await documentNumberingService.GenerateAsync(TenantId, null, DocumentTypes.Invoice, cancellationToken),
            Status = "Draft",
            InvoiceDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30),
            Currency = "KES",
            Notes = $"Invoice for work order {workOrder.WorkOrderNumber}",
        };

        foreach (var item in unbilledItems)
        {
            invoice.Lines.Add(new TenantInvoiceLine
            {
                Description = item.Description,
                ItemType = item.ItemType,
                Quantity = item.Quantity,
                Unit = item.Unit,
                UnitRate = item.UnitRate,
                LineTotal = item.LineTotal,
                TaxRate = item.TaxRate,
                TaxAmount = item.TaxAmount,
                GrandTotal = item.GrandTotal,
                BillableItemId = item.Id,
                WorkOrderId = workOrderId,
            });
        }

        RecalcTotals(invoice);
        dbContext.TenantInvoices.Add(invoice);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Mark billable items as billed
        foreach (var item in unbilledItems)
        {
            item.IsBilled = true;
            item.InvoiceId = invoice.Id;
        }

        // Mark the originating time logs as billed so re-running auto-populate skips them
        var billedTimeLogIds = unbilledItems
            .Where(x => x.SourceTimeLogId.HasValue)
            .Select(x => x.SourceTimeLogId!.Value)
            .ToHashSet();

        if (billedTimeLogIds.Count > 0)
        {
            await dbContext.WorkOrderTimeLogs
                .Where(x => x.WorkOrderId == workOrderId && billedTimeLogIds.Contains(x.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsBilled, true), cancellationToken);
        }

        // Update work order billing status
        workOrder.BillingStatus = "Billed";
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(TenantId, UserId, "finance.invoice.generated_from_wo", nameof(TenantInvoice), invoice.Id.ToString(), $"Generated invoice {invoice.InvoiceNumber} from WO {workOrder.WorkOrderNumber}", cancellationToken);

        await dbContext.Entry(invoice).Reference(x => x.Client).LoadAsync(cancellationToken);
        await dbContext.Entry(invoice).Collection(x => x.Lines).LoadAsync(cancellationToken);
        return CreatedAtAction(nameof(GetInvoice), new { id = invoice.Id }, MapDetail(invoice));
    }

    // ── Email helpers ─────────────────────────────────────────────────────────

    private async Task TryQueueInvoiceSentEmailAsync(TenantInvoice invoice, CancellationToken cancellationToken)
    {
        try
        {
            var clientEmail = invoice.Client?.Email;
            if (string.IsNullOrWhiteSpace(clientEmail))
                return;

            var template = await emailTemplateService.RenderTenantTemplateAsync(
                TenantId,
                "invoice.sent",
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["InvoiceNumber"] = invoice.InvoiceNumber,
                    ["CompanyName"] = invoice.Client?.ClientName ?? "N/A",
                    ["TotalAmount"] = $"{invoice.Currency} {invoice.TotalAmount:N2}",
                    ["DueDate"] = invoice.DueDate.ToString("yyyy-MM-dd"),
                },
                cancellationToken);

            if (!template.Enabled)
                return;

            var subject = await emailSubjectRuleService.BuildFinalSubjectAsync(
                TenantId, "invoice.sent", template.Subject, cancellationToken: cancellationToken);

            await emailOutbox.QueueEmailAsync(
                new QueueEmailRequest(
                    TenantId, "invoice.sent", "invoice-sent",
                    clientEmail, invoice.Client?.ClientName,
                    template.SenderNameOverride, null, null,
                    subject, template.HtmlBody, template.TextBody, null),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to queue invoice.sent email for invoice {InvoiceId}.", invoice.Id);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<TenantInvoice> LoadInvoiceWithDetailsAsync(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.TenantInvoices
            .Include(x => x.Client)
            .Include(x => x.Lines)
            .Include(x => x.Payments)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Invoice not found.");
    }

    private async Task EnsureInvoiceAccessAsync(Guid id, CancellationToken cancellationToken)
    {
        var exists = await dbContext.TenantInvoices.AnyAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken);
        if (!exists) throw new NotFoundException("Invoice not found.");
    }

    private async Task RecalcInvoicePaymentStatusAsync(TenantInvoice invoice, CancellationToken cancellationToken)
    {
        var confirmedPayments = await dbContext.TenantPayments
            .Where(x => x.InvoiceId == invoice.Id && x.Status == "Confirmed")
            .SumAsync(x => x.Amount, cancellationToken);

        invoice.AmountPaid = confirmedPayments;
        invoice.Balance = invoice.TotalAmount - confirmedPayments;
        if (invoice.TotalAmount > 0 && confirmedPayments >= invoice.TotalAmount)
            invoice.Status = "Paid";
        else if (confirmedPayments > 0)
            invoice.Status = "PartiallyPaid";
        else if (invoice.DueDate < DateTime.UtcNow && invoice.Status != "Draft")
            invoice.Status = "Overdue";
        else if (invoice.Status != "Draft")
            invoice.Status = "Sent";
    }

    private static void RecalcTotals(TenantInvoice invoice)
    {
        invoice.Subtotal = invoice.Lines.Sum(x => x.LineTotal);
        invoice.TaxAmount = invoice.Lines.Sum(x => x.TaxAmount);
        invoice.TotalAmount = invoice.Lines.Sum(x => x.GrandTotal);
        invoice.Balance = invoice.TotalAmount - invoice.AmountPaid;
    }

    private static TenantInvoiceListItem MapListItem(TenantInvoice inv) =>
        new(inv.Id, inv.TenantId, inv.ClientId, inv.Client?.ClientName,
            inv.InvoiceNumber, inv.Status, inv.InvoiceDate, inv.DueDate,
            inv.Subtotal, inv.TaxAmount, inv.TotalAmount, inv.AmountPaid,
            inv.Balance, inv.Currency, inv.CreatedAt);

    private static TenantInvoiceDetailResponse MapDetail(
        TenantInvoice inv,
        bool creditLimitWarning = false,
        decimal clientCurrentBalance = 0,
        decimal? clientCreditLimit = null) =>
        new(inv.Id, inv.TenantId, inv.ClientId, inv.Client?.ClientName,
            inv.InvoiceNumber, inv.Status, inv.InvoiceDate, inv.DueDate,
            inv.Notes, inv.TermsAndConditions,
            inv.Subtotal, inv.TaxAmount, inv.TotalAmount, inv.AmountPaid,
            inv.Balance, inv.Currency, inv.CreatedAt,
            inv.Lines.Select(MapLine).ToList(),
            inv.Payments.Select(MapPayment).ToList(),
            CreditTermId: inv.CreditTermId,
            CreditTermName: inv.CreditTerm?.Name,
            DepositAmount: inv.DepositAmount,
            DepositPaid: inv.DepositPaid,
            DepositDueDate: inv.DepositDueDate,
            CreditLimitWarning: creditLimitWarning,
            ClientCurrentBalance: clientCurrentBalance,
            ClientCreditLimit: clientCreditLimit);

    private static TenantInvoiceLineResponse MapLine(TenantInvoiceLine l) =>
        new(l.Id, l.InvoiceId, l.BillableItemId, l.WorkOrderId,
            l.Description, l.ItemType, l.Quantity, l.Unit, l.UnitRate,
            l.LineTotal, l.TaxRate, l.TaxAmount, l.GrandTotal);

    private static TenantPaymentResponse MapPayment(TenantPayment p) =>
        new(p.Id, p.TenantId, p.InvoiceId, p.ClientId, p.PaymentNumber,
            p.Amount, p.PaymentDate, p.Method, p.Reference, p.Notes,
            p.Status, p.CreatedAt);

    private static string NormalizePaymentMethod(string value) => value.Trim() switch
    {
        "Cash" or "MPesa" or "BankTransfer" or "Cheque" or "Card" => value.Trim(),
        _ => throw new BusinessRuleException("Method must be Cash, MPesa, BankTransfer, Cheque, or Card.")
    };
}
