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
[Route("api/quotations")]
public sealed class TenantQuotationController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IDocumentNumberingService documentNumberingService,
    IPdfRenderer pdfRenderer,
    IAuditLogService auditLogService,
    IUserAccessService userAccessService,
    IEmailOutboxService emailOutbox,
    IEmailTemplateService emailTemplateService,
    IEmailSubjectRuleService emailSubjectRuleService,
    ILogger<TenantQuotationController> logger) : TenantAwareControllerBase(tenantContext)
{
    // No platform-wide currency to inherit here: the tenant's own configured currency (derived from
    // its country, same as the frontend's getActiveCurrency()) is what the create/edit forms send
    // explicitly — this is only the last-resort default for a request that omits it outright.
    private const string FallbackCurrency = "KES";

    // ── List ─────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<TenantQuotationListItem>>> ListQuotations(
        [FromQuery] string? status,
        [FromQuery] Guid? clientId,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceView);

        var query = dbContext.TenantQuotations
            .Include(x => x.Client)
            .Where(x => x.TenantId == TenantId);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status.Trim());
        if (clientId.HasValue)
            query = query.Where(x => x.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(x =>
                x.QuoteNumber.ToLower().Contains(s) ||
                (x.Client != null && x.Client.ClientName.ToLower().Contains(s)) ||
                (x.Subject != null && x.Subject.ToLower().Contains(s)));
        }

        var list = await query.OrderByDescending(x => x.QuoteDate).ToListAsync(cancellationToken);
        return Ok(list.Select(MapListItem).ToList());
    }

    // ── Detail ───────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TenantQuotationDetailResponse>> GetQuotation(
        Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceView);
        var quote = await LoadQuoteAsync(id, cancellationToken);
        return Ok(MapDetail(quote));
    }

    // ── Create ───────────────────────────────────────────────────────────────

    [HttpPost]
    public async Task<ActionResult<TenantQuotationDetailResponse>> CreateQuotation(
        [FromBody] UpsertQuotationRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);

        var quote = new TenantQuotation
        {
            TenantId = TenantId,
            ClientId = request.ClientId,
            SiteId = request.SiteId,
            QuoteNumber = await documentNumberingService.GenerateAsync(TenantId, null, DocumentTypes.Quotation, cancellationToken),
            Status = "Draft",
            QuoteDate = DateTime.SpecifyKind(request.QuoteDate, DateTimeKind.Utc),
            ValidUntil = DateTime.SpecifyKind(request.ValidUntil, DateTimeKind.Utc),
            Subject = request.Subject?.Trim(),
            Notes = request.Notes?.Trim(),
            Currency = string.IsNullOrWhiteSpace(request.Currency)
                ? FallbackCurrency
                : request.Currency.Trim().ToUpper(),
        };

        BuildLines(quote, request.Lines);
        RecalcTotals(quote);
        dbContext.TenantQuotations.Add(quote);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "finance.quotation.created", nameof(TenantQuotation), quote.Id.ToString(), $"Created quotation {quote.QuoteNumber}", cancellationToken);
        await LoadQuoteNavPropsAsync(quote, cancellationToken);
        return CreatedAtAction(nameof(GetQuotation), new { id = quote.Id }, MapDetail(quote));
    }

    // ── Update ───────────────────────────────────────────────────────────────

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TenantQuotationDetailResponse>> UpdateQuotation(
        Guid id, [FromBody] UpsertQuotationRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);
        var quote = await LoadQuoteAsync(id, cancellationToken);

        if (quote.Status != "Draft")
            throw new BusinessRuleException("Only Draft quotations can be edited.");

        quote.ClientId = request.ClientId;
        quote.SiteId = request.SiteId;
        quote.QuoteDate = DateTime.SpecifyKind(request.QuoteDate, DateTimeKind.Utc);
        quote.ValidUntil = DateTime.SpecifyKind(request.ValidUntil, DateTimeKind.Utc);
        quote.Subject = request.Subject?.Trim();
        quote.Notes = request.Notes?.Trim();
        quote.Currency = string.IsNullOrWhiteSpace(request.Currency)
            ? FallbackCurrency
            : request.Currency.Trim().ToUpper();

        dbContext.TenantQuotationLines.RemoveRange(quote.Lines);
        quote.Lines.Clear();
        BuildLines(quote, request.Lines);
        RecalcTotals(quote);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapDetail(quote));
    }

    // ── Send ─────────────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/send")]
    public async Task<ActionResult<TenantQuotationDetailResponse>> SendQuotation(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);
        var quote = await LoadQuoteAsync(id, cancellationToken);
        if (quote.Status != "Draft") throw new BusinessRuleException("Only Draft quotations can be sent.");
        quote.Status = "Sent";
        await dbContext.SaveChangesAsync(cancellationToken);
        await TryQueueQuotationSentEmailAsync(quote, cancellationToken);
        return Ok(MapDetail(quote));
    }

    // ── Accept ───────────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/accept")]
    public async Task<ActionResult<TenantQuotationDetailResponse>> AcceptQuotation(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);
        var quote = await LoadQuoteAsync(id, cancellationToken);
        if (quote.Status != "Sent") throw new BusinessRuleException("Only Sent quotations can be accepted.");
        quote.Status = "Accepted";
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapDetail(quote));
    }

    // ── Reject ───────────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<TenantQuotationDetailResponse>> RejectQuotation(
        Guid id, [FromBody] RejectQuotationRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);
        var quote = await LoadQuoteAsync(id, cancellationToken);
        quote.Status = "Rejected";
        quote.RejectionReason = request.Reason?.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapDetail(quote));
    }

    // ── Convert to Invoice ────────────────────────────────────────────────────

    [HttpPost("{id:guid}/convert-to-invoice")]
    public async Task<ActionResult<TenantInvoiceDetailResponse>> ConvertToInvoice(
        Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);
        var quote = await LoadQuoteAsync(id, cancellationToken);

        if (quote.Status == "Converted")
            throw new BusinessRuleException("Quotation already converted.");

        var invoice = new TenantInvoice
        {
            TenantId = TenantId,
            ClientId = quote.ClientId,
            InvoiceNumber = await documentNumberingService.GenerateAsync(TenantId, null, DocumentTypes.Invoice, cancellationToken),
            Status = "Draft",
            InvoiceDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30),
            Currency = quote.Currency,
            Notes = $"Converted from quotation {quote.QuoteNumber}",
        };

        foreach (var ql in quote.Lines)
        {
            var lineTotal = ql.LineTotal;
            var taxAmt = lineTotal * (ql.TaxRate / 100m);
            invoice.Lines.Add(new TenantInvoiceLine
            {
                Description = ql.Description,
                ItemType = ql.ItemType,
                Quantity = ql.Quantity,
                Unit = ql.Unit,
                UnitRate = ql.UnitRate,
                LineTotal = lineTotal,
                TaxRate = ql.TaxRate,
                TaxAmount = taxAmt,
                GrandTotal = ql.GrandTotal,
            });
        }

        RecalcInvoiceTotals(invoice);
        dbContext.TenantInvoices.Add(invoice);
        await dbContext.SaveChangesAsync(cancellationToken);

        quote.Status = "Converted";
        quote.ConvertedToInvoiceId = invoice.Id;
        await dbContext.SaveChangesAsync(cancellationToken);

        await dbContext.Entry(invoice).Reference(x => x.Client).LoadAsync(cancellationToken);
        return CreatedAtAction("GetInvoice", "TenantInvoice", new { id = invoice.Id }, MapInvoiceDetail(invoice));
    }

    // ── Convert to Work Order ─────────────────────────────────────────────────

    [HttpPost("{id:guid}/convert-to-work-order")]
    public async Task<IActionResult> ConvertToWorkOrder(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);
        var quote = await LoadQuoteAsync(id, cancellationToken);

        if (quote.Status == "Converted")
            throw new BusinessRuleException("Quotation already converted.");

        var workOrderNumber = await documentNumberingService.GenerateAsync(TenantId, null, DocumentTypes.WorkOrder, cancellationToken);
        var workOrder = new WorkOrder
        {
            TenantId = TenantId,
            ClientId = quote.ClientId,
            SiteId = quote.SiteId,
            WorkOrderNumber = workOrderNumber,
            Title = quote.Subject ?? $"Work from quotation {quote.QuoteNumber}",
            Description = quote.Notes,
            Status = "Open",
            Priority = "Medium",
            WorkType = "CorrectiveMaintenance",
            IsBillable = true,
        };

        dbContext.WorkOrders.Add(workOrder);
        await dbContext.SaveChangesAsync(cancellationToken);

        quote.Status = "Converted";
        quote.ConvertedToWorkOrderId = workOrder.Id;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { workOrderId = workOrder.Id, workOrderNumber = workOrder.WorkOrderNumber });
    }

    // ── PDF ──────────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> GetQuotationPdf(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceView);
        var quote = await LoadQuoteAsync(id, cancellationToken);

        var tenant = await dbContext.Tenants
            .SingleOrDefaultAsync(x => x.Id == TenantId, cancellationToken);

        var templateConfig = PdfTemplateHelper.ResolveForDocument(tenant?.ReportTemplateConfigJson, tenant?.DocumentDesignsJson, "quotation");
        var termsText = templateConfig?.ShowTerms == true
            ? templateConfig.QuoteTerms ?? templateConfig.DefaultTermsText
            : null;

        var model = new QuotationPdfModel(
            QuoteNumber:    quote.QuoteNumber,
            Status:         quote.Status,
            QuoteDate:      quote.QuoteDate,
            ValidUntil:     quote.ValidUntil,
            Subject:        quote.Subject,
            ClientName:     quote.Client?.ClientName ?? "Unknown",
            Notes:          quote.Notes,
            Lines:          quote.Lines.Select(l => new InvoiceLinePdfModel(
                                l.Description, l.ItemType, l.Quantity, l.Unit,
                                l.UnitRate, l.LineTotal, l.TaxRate, l.GrandTotal)).ToList(),
            Subtotal:       quote.Subtotal,
            TaxAmount:      quote.TaxAmount,
            TotalAmount:    quote.TotalAmount,
            Currency:       quote.Currency,
            CompanyName:    tenant?.CompanyName ?? "Ecosys",
            CompanyAddress: tenant?.Address,
            CompanyPhone:   tenant?.Phone,
            PrimaryColor:   templateConfig?.ReportPrimaryColor ?? tenant?.PrimaryColor ?? "#0F4C81",
            FontFamily:     templateConfig?.FontFamily ?? "Helvetica",
            FooterText:     PdfTemplateHelper.ResolveFooterText(templateConfig),
            WatermarkText:  templateConfig?.WatermarkText,
            BankDetails:    PdfTemplateHelper.ComposeBankDetails(templateConfig),
            PaymentInstructions: templateConfig?.ShowBankDetails == true ? templateConfig.PaymentInstructions : null,
            TermsText:      termsText,
            TemplateConfig: templateConfig
        );

        var bytes = pdfRenderer.RenderQuotationPdf(model);
        return File(bytes, "application/pdf", $"Quotation-{quote.QuoteNumber}.pdf");
    }

    // ── Email helpers ─────────────────────────────────────────────────────────

    private async Task TryQueueQuotationSentEmailAsync(TenantQuotation quote, CancellationToken cancellationToken)
    {
        try
        {
            var clientEmail = quote.Client?.Email;
            if (string.IsNullOrWhiteSpace(clientEmail))
                return;

            var template = await emailTemplateService.RenderTenantTemplateAsync(
                TenantId,
                "quotation.sent",
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["QuoteNumber"] = quote.QuoteNumber,
                    ["CompanyName"] = quote.Client?.ClientName ?? "N/A",
                    ["TotalAmount"] = $"{quote.Currency} {quote.TotalAmount:N2}",
                    ["ValidUntil"] = quote.ValidUntil.ToString("yyyy-MM-dd"),
                },
                cancellationToken);

            if (!template.Enabled)
                return;

            var subject = await emailSubjectRuleService.BuildFinalSubjectAsync(
                TenantId, "quotation.sent", template.Subject, cancellationToken: cancellationToken);

            await emailOutbox.QueueEmailAsync(
                new QueueEmailRequest(
                    TenantId, "quotation.sent", "quotation-sent",
                    clientEmail, quote.Client?.ClientName,
                    template.SenderNameOverride, null, null,
                    subject, template.HtmlBody, template.TextBody, null),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to queue quotation.sent email for quotation {QuotationId}.", quote.Id);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<TenantQuotation> LoadQuoteAsync(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.TenantQuotations
            .Include(x => x.Client)
            .Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Quotation not found.");
    }

    private async Task LoadQuoteNavPropsAsync(TenantQuotation quote, CancellationToken cancellationToken)
    {
        await dbContext.Entry(quote).Reference(x => x.Client).LoadAsync(cancellationToken);
        await dbContext.Entry(quote).Collection(x => x.Lines).LoadAsync(cancellationToken);
    }

    private static void BuildLines(TenantQuotation quote, IReadOnlyCollection<UpsertQuotationLineRequest> lines)
    {
        foreach (var line in lines)
        {
            var lineTotal = line.Quantity * line.UnitRate;
            var grandTotal = lineTotal + lineTotal * (line.TaxRate / 100m);
            quote.Lines.Add(new TenantQuotationLine
            {
                Description = line.Description.Trim(),
                ItemType = line.ItemType.Trim(),
                Quantity = line.Quantity,
                Unit = line.Unit.Trim(),
                UnitRate = line.UnitRate,
                LineTotal = lineTotal,
                TaxRate = line.TaxRate,
                GrandTotal = grandTotal,
                RateCardId = line.RateCardId,
            });
        }
    }

    private static void RecalcTotals(TenantQuotation quote)
    {
        quote.Subtotal = quote.Lines.Sum(x => x.LineTotal);
        quote.TaxAmount = quote.Lines.Sum(x => x.GrandTotal - x.LineTotal);
        quote.TotalAmount = quote.Lines.Sum(x => x.GrandTotal);
    }

    private static void RecalcInvoiceTotals(TenantInvoice invoice)
    {
        invoice.Subtotal = invoice.Lines.Sum(x => x.LineTotal);
        invoice.TaxAmount = invoice.Lines.Sum(x => x.TaxAmount);
        invoice.TotalAmount = invoice.Lines.Sum(x => x.GrandTotal);
        invoice.Balance = invoice.TotalAmount;
    }

    private static TenantQuotationListItem MapListItem(TenantQuotation q) =>
        new(q.Id, q.TenantId, q.ClientId, q.Client?.ClientName,
            q.QuoteNumber, q.Status, q.Subject, q.QuoteDate, q.ValidUntil,
            q.Subtotal, q.TaxAmount, q.TotalAmount, q.Currency,
            q.ConvertedToInvoiceId, q.ConvertedToWorkOrderId, q.CreatedAt);

    private static TenantQuotationDetailResponse MapDetail(TenantQuotation q) =>
        new(q.Id, q.TenantId, q.ClientId, q.Client?.ClientName,
            q.SiteId, q.QuoteNumber, q.Status, q.Subject, q.QuoteDate, q.ValidUntil,
            q.Notes, q.Subtotal, q.TaxAmount, q.TotalAmount, q.Currency,
            q.ConvertedToInvoiceId, q.ConvertedToWorkOrderId, q.RejectionReason, q.CreatedAt,
            q.Lines.Select(MapQLine).ToList());

    private static TenantQuotationLineResponse MapQLine(TenantQuotationLine l) =>
        new(l.Id, l.QuotationId, l.RateCardId, l.Description, l.ItemType,
            l.Quantity, l.Unit, l.UnitRate, l.LineTotal, l.TaxRate, l.GrandTotal);

    private static TenantInvoiceDetailResponse MapInvoiceDetail(TenantInvoice inv) =>
        new(inv.Id, inv.TenantId, inv.ClientId, inv.Client?.ClientName,
            inv.InvoiceNumber, inv.DocumentType, inv.Status, inv.InvoiceDate, inv.DueDate,
            inv.Notes, inv.TermsAndConditions,
            inv.Subtotal, inv.TaxAmount, inv.TotalAmount, inv.AmountPaid,
            inv.Balance, inv.Currency, inv.CreatedAt,
            inv.Lines.Select(l => new TenantInvoiceLineResponse(l.Id, l.InvoiceId, l.BillableItemId, l.WorkOrderId,
                l.Description, l.ItemType, l.Quantity, l.Unit, l.UnitRate,
                l.LineTotal, l.TaxRate, l.TaxAmount, l.GrandTotal)).ToList(),
            []);
}

// ── Response / Request records ────────────────────────────────────────────────

public sealed record TenantQuotationListItem(
    Guid Id, Guid TenantId, Guid ClientId, string? ClientName,
    string QuoteNumber, string Status, string? Subject,
    DateTime QuoteDate, DateTime ValidUntil,
    decimal Subtotal, decimal TaxAmount, decimal TotalAmount, string Currency,
    Guid? ConvertedToInvoiceId, Guid? ConvertedToWorkOrderId, DateTime CreatedAt);

public sealed record TenantQuotationDetailResponse(
    Guid Id, Guid TenantId, Guid ClientId, string? ClientName,
    Guid? SiteId, string QuoteNumber, string Status, string? Subject,
    DateTime QuoteDate, DateTime ValidUntil, string? Notes,
    decimal Subtotal, decimal TaxAmount, decimal TotalAmount, string Currency,
    Guid? ConvertedToInvoiceId, Guid? ConvertedToWorkOrderId,
    string? RejectionReason, DateTime CreatedAt,
    IReadOnlyCollection<TenantQuotationLineResponse> Lines);

public sealed record TenantQuotationLineResponse(
    Guid Id, Guid QuotationId, Guid? RateCardId,
    string Description, string ItemType, decimal Quantity, string Unit,
    decimal UnitRate, decimal LineTotal, decimal TaxRate, decimal GrandTotal);

public sealed record UpsertQuotationRequest(
    Guid ClientId,
    Guid? SiteId,
    DateTime QuoteDate,
    DateTime ValidUntil,
    string? Subject,
    string? Notes,
    string? Currency,
    IReadOnlyCollection<UpsertQuotationLineRequest> Lines);

public sealed record UpsertQuotationLineRequest(
    Guid? RateCardId,
    string Description,
    string ItemType,
    decimal Quantity,
    string Unit,
    decimal UnitRate,
    decimal TaxRate);

public sealed record RejectQuotationRequest(string? Reason);
