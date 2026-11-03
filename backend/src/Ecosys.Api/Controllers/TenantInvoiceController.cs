using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Ecosys.Shared.Contracts.Integration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
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
    IEmailDeliveryLogService emailDeliveryLogs,
    IEmailTemplateService emailTemplateService,
    IEmailSubjectRuleService emailSubjectRuleService,
    ILogger<TenantInvoiceController> logger,
    IFinancialRecipientResolver financialRecipientResolver,
    IWebHostEnvironment environment) : TenantAwareControllerBase(tenantContext)
{
    // No platform-wide currency to inherit here: the tenant's own configured currency (derived from
    // its country, same as the frontend's getActiveCurrency()) is what the create/edit forms send
    // explicitly — this is only the last-resort default for a request that omits it outright.
    private const string FallbackCurrency = "KES";

    // ── List ─────────────────────────────────────────────────────────────────

    /// <param name="age">
    /// Receivables only: <c>overdue</c>, <c>current</c> (not yet due) or an ageing bucket (<c>1-30</c>,
    /// <c>31-60</c>, <c>61-90</c>, <c>90-plus</c> days past due, against the tenant's today), exactly as
    /// the finance dashboard widgets count them (<see cref="ReceivableAges"/>).
    /// </param>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<TenantInvoiceListItem>>> ListInvoices(
        [FromQuery] string? status,
        [FromQuery] string? documentType,
        [FromQuery] Guid? clientId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] string? search,
        [FromQuery] string? age,
        [FromQuery] string? currency,
        [FromServices] ITenantTimeZoneService tenantTimeZoneService,
        CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceView);

        var query = dbContext.TenantInvoices
            .Include(x => x.Client)
            .Where(x => x.TenantId == TenantId);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status.Trim());
        if (!string.IsNullOrWhiteSpace(documentType))
        {
            var normalizedDocumentType = DocumentTypes.Normalize(documentType.Trim());
            query = query.Where(x => x.DocumentType == normalizedDocumentType);
        }
        if (clientId.HasValue)
            query = query.Where(x => x.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(age))
        {
            var zone = await tenantTimeZoneService.GetTimeZoneAsync(TenantId, cancellationToken);
            query = ReceivableAges.Apply(query, age, TenantDay.StartUtc(zone, DateTime.UtcNow));
        }
        if (!string.IsNullOrWhiteSpace(currency))
            query = query.Where(x => x.Currency == currency.Trim().ToUpper());
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

        var sourceQuotations = await LoadSourceQuotationsAsync(invoice.Id, cancellationToken);
        return Ok(MapDetail(invoice, sourceQuotations: sourceQuotations));
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
            ProjectId = await ProjectLinks.ResolveAsync(dbContext, TenantId, request.ProjectId, request.ClientId, cancellationToken),
            CreditTermId = creditTerm?.Id,
            InvoiceNumber = await documentNumberingService.GenerateAsync(TenantId, null, DocumentTypes.Invoice, cancellationToken),
            DocumentType = DocumentTypes.Invoice,
            Status = "Draft",
            InvoiceDate = invoiceDate,
            DueDate = dueDate,
            Notes = request.Notes?.Trim(),
            TermsAndConditions = request.TermsAndConditions?.Trim(),
            Currency = string.IsNullOrWhiteSpace(request.Currency)
                ? FallbackCurrency
                : request.Currency.Trim().ToUpper(),
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

        await ProjectFinanceRules.EnsureProjectCurrencyAsync(dbContext, TenantId, invoice.ProjectId, invoice.Currency, cancellationToken);
        dbContext.TenantInvoices.Add(invoice);

        // Update the client's running balance from the single source of truth (sum of open invoice
        // balances, including this not-yet-saved one) instead of incrementing it, so the value is
        // self-correcting rather than drifting further whenever it is already wrong.
        await InvoiceBalanceCalculator.RecalcClientBalanceAsync(dbContext, client, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "finance.invoice.created", nameof(TenantInvoice), invoice.Id.ToString(), $"Created invoice {invoice.InvoiceNumber}", cancellationToken);

        await dbContext.Entry(invoice).Reference(x => x.Client).LoadAsync(cancellationToken);

        // Credit limit soft warning (not a hard block - just signals to UI)
        var effectiveLimit = client.CreditLimit ?? creditTerm?.CreditLimit;
        var creditLimitWarning = effectiveLimit.HasValue && client.CurrentBalance > effectiveLimit.Value;

        return CreatedAtAction(nameof(GetInvoice), new { id = invoice.Id },
            MapDetail(invoice, creditLimitWarning, client.CurrentBalance, effectiveLimit));
    }

    [HttpPost("build-proforma-from-quotations")]
    public async Task<ActionResult<TenantInvoiceDetailResponse>> BuildProformaFromQuotations(
        [FromBody] BuildProformaInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);

        var quotationIds = request.QuotationIds?
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList() ?? [];

        if (quotationIds.Count == 0)
            throw new BusinessRuleException("Select at least one quotation to build a proforma invoice.");

        var quotations = await dbContext.TenantQuotations
            .Include(x => x.Client)
            .Include(x => x.Lines)
            .Where(x => x.TenantId == TenantId && quotationIds.Contains(x.Id))
            .OrderBy(x => x.QuoteDate)
            .ToListAsync(cancellationToken);

        if (quotations.Count != quotationIds.Count)
            throw new NotFoundException("One or more quotations could not be found.");

        if (quotations.Any(x => x.Status != "Accepted"))
            throw new BusinessRuleException("Only accepted quotations can be built into a proforma invoice.");

        if (quotations.Any(x => x.ConvertedToInvoiceId.HasValue))
            throw new BusinessRuleException("One or more selected quotations have already been converted.");

        var clientId = quotations[0].ClientId;
        var currency = quotations[0].Currency;
        if (quotations.Any(x => x.ClientId != clientId))
            throw new BusinessRuleException("Selected quotations must belong to the same client.");

        if (quotations.Any(x => !string.Equals(x.Currency, currency, StringComparison.OrdinalIgnoreCase)))
            throw new BusinessRuleException("Selected quotations must use the same currency.");

        var invoiceDate = DateTime.SpecifyKind(request.InvoiceDate, DateTimeKind.Utc);
        var dueDate = DateTime.SpecifyKind(request.DueDate, DateTimeKind.Utc);
        var sourceQuoteNumbers = quotations.Select(x => x.QuoteNumber).ToList();

        var invoice = new TenantInvoice
        {
            TenantId = TenantId,
            ClientId = clientId,
            InvoiceNumber = await documentNumberingService.GenerateAsync(TenantId, null, DocumentTypes.ProformaInvoice, cancellationToken),
            DocumentType = DocumentTypes.ProformaInvoice,
            Status = "Draft",
            InvoiceDate = invoiceDate,
            DueDate = dueDate,
            Notes = string.IsNullOrWhiteSpace(request.Notes)
                ? $"Proforma invoice built from quotations {string.Join(", ", sourceQuoteNumbers)}"
                : request.Notes.Trim(),
            TermsAndConditions = request.TermsAndConditions?.Trim(),
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? currency : request.Currency.Trim().ToUpperInvariant(),
        };

        foreach (var quotation in quotations)
        {
            foreach (var line in quotation.Lines)
            {
                var taxAmount = line.LineTotal * (line.TaxRate / 100m);
                invoice.Lines.Add(new TenantInvoiceLine
                {
                    Description = sourceQuoteNumbers.Count > 1
                        ? $"[{quotation.QuoteNumber}] {line.Description}"
                        : line.Description,
                    ItemType = line.ItemType,
                    Quantity = line.Quantity,
                    Unit = line.Unit,
                    UnitRate = line.UnitRate,
                    LineTotal = line.LineTotal,
                    TaxRate = line.TaxRate,
                    TaxAmount = taxAmount,
                    GrandTotal = line.GrandTotal,
                });
            }
        }

        RecalcTotals(invoice);
        dbContext.TenantInvoices.Add(invoice);
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var quotation in quotations)
        {
            quotation.Status = "Converted";
            quotation.ConvertedToInvoiceId = invoice.Id;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "finance.proforma.created",
            nameof(TenantInvoice),
            invoice.Id.ToString(),
            $"Built proforma invoice {invoice.InvoiceNumber} from quotations {string.Join(", ", sourceQuoteNumbers)}",
            cancellationToken);

        await dbContext.Entry(invoice).Reference(x => x.Client).LoadAsync(cancellationToken);
        return CreatedAtAction(nameof(GetInvoice), new { id = invoice.Id }, MapDetail(invoice));
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

        // Invoices Projects raised (valuation, advance, retention release) keep the project's client,
        // currency and amount; everything else on them can still be edited.
        await ProjectFinanceRules.EnsureProjectInvoiceEditAllowedAsync(
            dbContext, TenantId, invoice, request.ClientId,
            string.IsNullOrWhiteSpace(request.Currency) ? FallbackCurrency : request.Currency,
            request.Lines.Sum(x => x.Quantity * x.UnitRate), cancellationToken);

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
        invoice.Currency = string.IsNullOrWhiteSpace(request.Currency)
            ? FallbackCurrency
            : request.Currency.Trim().ToUpper();

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

        // An edit can change the total — and even move the invoice to another client — so refresh the
        // running balance of both the new and the previous client before saving (EC-012 family).
        var previousClientId = dbContext.Entry(invoice).OriginalValues.GetValue<Guid>(nameof(TenantInvoice.ClientId));
        await InvoiceBalanceCalculator.RecalcClientBalancesAsync(
            dbContext, TenantId, [invoice], cancellationToken, previousClientId);

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
        try
        {
            await QueueInvoiceSentEmailAsync(invoice, cancellationToken);
        }
        catch
        {
            invoice.Status = "Draft";
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
        await auditLogService.LogAsync(TenantId, UserId, "finance.invoice.sent", nameof(TenantInvoice), invoice.Id.ToString(), $"Sent invoice {invoice.InvoiceNumber}; delivery queued.", cancellationToken);
        // A WhatsApp note to the client's opted-in contacts (Release 3 design 4.2). Never fails the send.
        if (HttpContext?.RequestServices.GetService(typeof(IClientWhatsAppNotifier)) is IClientWhatsAppNotifier whatsApp)
            await whatsApp.NotifyClientAsync(TenantId, invoice.ClientId, ClientWhatsAppKeys.InvoiceIssued, new Dictionary<string, string?>
            {
                ["InvoiceNumber"] = invoice.InvoiceNumber,
                ["TotalAmount"] = $"{invoice.Currency} {invoice.TotalAmount:N2}",
                ["DueDate"] = invoice.DueDate.ToString("yyyy-MM-dd"),
            }, nameof(TenantInvoice), invoice.Id, onceScope: invoice.Id.ToString("N"), cancellationToken: cancellationToken);
        return Ok(MapDetail(invoice));
    }

    [HttpPost("{id:guid}/convert-to-invoice")]
    public async Task<ActionResult<TenantInvoiceDetailResponse>> ConvertProformaToInvoice(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);
        var invoice = await LoadInvoiceWithDetailsAsync(id, cancellationToken);

        if (!string.Equals(invoice.DocumentType, DocumentTypes.ProformaInvoice, StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("Only proforma invoices can be converted to final invoices.");

        if (invoice.Status == "Void")
            throw new BusinessRuleException("Void proforma invoices cannot be converted.");

        if (invoice.AmountPaid > 0)
            throw new BusinessRuleException("Remove recorded payments before converting this proforma invoice.");

        invoice.DocumentType = DocumentTypes.Invoice;
        invoice.InvoiceNumber = await documentNumberingService.GenerateAsync(TenantId, null, DocumentTypes.Invoice, cancellationToken);
        invoice.Status = "Draft";

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "finance.proforma.converted",
            nameof(TenantInvoice),
            invoice.Id.ToString(),
            $"Converted proforma to invoice {invoice.InvoiceNumber}",
            cancellationToken);

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

        // A voided invoice no longer counts as outstanding debt, so the client's running balance has
        // to drop by its balance — it used to keep carrying it forever (EC-012 family).
        await InvoiceBalanceCalculator.RecalcClientBalancesAsync(
            dbContext, TenantId, [invoice], cancellationToken);

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

        var bytes = await RenderInvoicePdfAsync(invoice, tenant, cancellationToken);
        return File(bytes, "application/pdf", $"Invoice-{invoice.InvoiceNumber}.pdf");
    }

    // ── Payments ─────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/payments")]
    public async Task<ActionResult<IReadOnlyCollection<TenantPaymentResponse>>> ListPayments(
        Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceView);
        await EnsureInvoiceAccessAsync(id, cancellationToken);

        // Payments touching this invoice: those directly linked (legacy) or actively allocated to it.
        var allocatedPaymentIds = await dbContext.PaymentAllocations
            .Where(a => a.TenantId == TenantId && a.InvoiceId == id
                        && a.SourceType == AllocationSourceTypes.Payment && a.Status == "Active")
            .Select(a => a.SourceId)
            .ToListAsync(cancellationToken);

        var payments = await dbContext.TenantPayments
            .Where(x => x.TenantId == TenantId && (x.InvoiceId == id || allocatedPaymentIds.Contains(x.Id)))
            .OrderByDescending(x => x.PaymentDate)
            .ToListAsync(cancellationToken);
        return Ok(payments.Select(MapPayment).ToList());
    }

    [HttpGet("/api/payments")]
    public async Task<ActionResult<IReadOnlyCollection<TenantPaymentListItemResponse>>> ListAllPayments(
        [FromQuery] Guid? clientId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceView);

        var query = dbContext.TenantPayments.Where(x => x.TenantId == TenantId);
        if (clientId.HasValue)
            query = query.Where(x => x.ClientId == clientId.Value);

        var paymentsFrom = DateTimeNormalizationHelper.NormalizeNullableToUtc(from);
        var paymentsTo = DateTimeNormalizationHelper.NormalizeNullableToUtc(to);
        if (paymentsFrom.HasValue)
            query = query.Where(x => x.PaymentDate >= paymentsFrom.Value);
        if (paymentsTo.HasValue)
            query = query.Where(x => x.PaymentDate <= paymentsTo.Value);

        var rows = await query
            .OrderByDescending(x => x.PaymentDate)
            .Select(p => new TenantPaymentListItemResponse(
                p.Id, p.TenantId, p.InvoiceId, p.ClientId, p.PaymentNumber,
                p.Amount, p.PaymentDate, p.Method, p.Reference, p.Notes,
                p.Status, p.CreatedAt,
                dbContext.TenantInvoices.Where(i => i.Id == p.InvoiceId).Select(i => i.InvoiceNumber).FirstOrDefault(),
                dbContext.Clients.Where(c => c.Id == p.ClientId).Select(c => c.ClientName).FirstOrDefault(),
                dbContext.PaymentAllocations
                    .Where(a => a.SourceType == "Payment" && a.SourceId == p.Id && a.Status == "Active")
                    .Sum(a => (decimal?)a.Amount) ?? 0m,
                p.Amount - (dbContext.PaymentAllocations
                    .Where(a => a.SourceType == "Payment" && a.SourceId == p.Id && a.Status == "Active")
                    .Sum(a => (decimal?)a.Amount) ?? 0m)))
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }

    [HttpPost("{id:guid}/payments")]
    public async Task<ActionResult<TenantPaymentResponse>> RecordPayment(
        Guid id, [FromBody] RecordPaymentRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);
        var invoice = await LoadInvoiceWithDetailsAsync(id, cancellationToken);

        if (invoice.Status == "Void")
            throw new BusinessRuleException("Cannot record payment on a voided invoice.");
        if (request.Amount <= 0)
            throw new BusinessRuleException("Payment amount must be greater than zero.");

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

        // Apply against this invoice up to its outstanding balance; any excess is retained as
        // customer credit (an unallocated payment) that can be applied to other invoices later.
        var applyAmount = Math.Min(payment.Amount, Math.Max(invoice.Balance, 0m));
        if (applyAmount > 0)
        {
            await InvoiceBalanceCalculator.ApplyAllocationsAsync(
                dbContext, TenantId, AllocationSourceTypes.Payment, payment.Id,
                new[] { (invoice, applyAmount) }, cancellationToken);
        }

        await InvoiceBalanceCalculator.RecalcClientBalancesAsync(
            dbContext, TenantId, [invoice], cancellationToken);

        // Payment, allocations, invoice state and the client's running balance are persisted by this
        // single save, so they can never diverge (EC-012).
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(TenantId, UserId, "finance.payment.recorded", nameof(TenantPayment), payment.Id.ToString(), $"Recorded payment {payment.PaymentNumber} for invoice {invoice.InvoiceNumber}", cancellationToken);
        await TryQueueReceiptEmailAsync(payment.Id, cancellationToken);

        return CreatedAtAction(nameof(ListPayments), new { id }, MapPayment(payment));
    }

    // ── Receive & allocate a payment across multiple invoices ────────────────

    [HttpPost("/api/payments")]
    public async Task<ActionResult<TenantPaymentResponse>> ReceivePayment(
        [FromBody] ReceivePaymentRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);

        if (request.Amount <= 0)
            throw new BusinessRuleException("Payment amount must be greater than zero.");

        var client = await dbContext.Clients
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == request.ClientId, cancellationToken)
            ?? throw new NotFoundException("Client not found.");

        var requested = (request.Allocations ?? [])
            .Where(a => a.Amount > 0)
            .ToList();
        if (requested.Sum(a => a.Amount) > request.Amount)
            throw new BusinessRuleException("Allocations exceed the payment amount.");

        var payment = new TenantPayment
        {
            TenantId = TenantId,
            InvoiceId = requested.Count == 1 ? requested[0].InvoiceId : null,
            ClientId = client.Id,
            PaymentNumber = await documentNumberingService.GenerateAsync(TenantId, null, DocumentTypes.Receipt, cancellationToken),
            Amount = request.Amount,
            PaymentDate = DateTime.SpecifyKind(request.PaymentDate, DateTimeKind.Utc),
            Method = NormalizePaymentMethod(request.Method),
            Reference = request.Reference?.Trim(),
            Notes = request.Notes?.Trim(),
            Status = "Confirmed",
        };
        dbContext.TenantPayments.Add(payment);

        var items = await ResolveAllocationItemsAsync(client.Id, requested, cancellationToken);
        if (items.Count > 0)
            await InvoiceBalanceCalculator.ApplyAllocationsAsync(
                dbContext, TenantId, AllocationSourceTypes.Payment, payment.Id, items, cancellationToken);

        await InvoiceBalanceCalculator.RecalcClientBalanceAsync(dbContext, client, cancellationToken);

        // Single save: payment, allocations, invoice balances and the client balance commit together.
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(TenantId, UserId, "finance.payment.received", nameof(TenantPayment), payment.Id.ToString(),
            $"Received payment {payment.PaymentNumber} ({payment.Amount:N2}) for {client.ClientName}, allocated to {items.Count} invoice(s)", cancellationToken);
        await TryQueueReceiptEmailAsync(payment.Id, cancellationToken);

        return Ok(MapPayment(payment));
    }

    [HttpPost("/api/payments/{paymentId:guid}/allocate")]
    public async Task<ActionResult<TenantPaymentResponse>> AllocatePayment(
        Guid paymentId, [FromBody] AllocatePaymentRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);

        var payment = await dbContext.TenantPayments
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == paymentId, cancellationToken)
            ?? throw new NotFoundException("Payment not found.");
        if (payment.Status != "Confirmed")
            throw new BusinessRuleException("Only confirmed payments can be allocated.");

        var alreadyAllocated = await dbContext.PaymentAllocations
            .Where(a => a.TenantId == TenantId && a.SourceType == AllocationSourceTypes.Payment
                        && a.SourceId == payment.Id && a.Status == "Active")
            .SumAsync(a => (decimal?)a.Amount, cancellationToken) ?? 0m;

        var requested = request.Allocations.Where(a => a.Amount > 0).ToList();
        if (alreadyAllocated + requested.Sum(a => a.Amount) > payment.Amount)
            throw new BusinessRuleException("Allocations exceed the payment's unallocated balance.");

        var items = await ResolveAllocationItemsAsync(payment.ClientId, requested, cancellationToken);
        if (items.Count == 0)
            throw new BusinessRuleException("Nothing to allocate.");

        await InvoiceBalanceCalculator.ApplyAllocationsAsync(
            dbContext, TenantId, AllocationSourceTypes.Payment, payment.Id, items, cancellationToken);

        await InvoiceBalanceCalculator.RecalcClientBalancesAsync(
            dbContext, TenantId, items.Select(i => i.Invoice), cancellationToken, payment.ClientId);

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(TenantId, UserId, "finance.payment.allocated", nameof(TenantPayment), payment.Id.ToString(),
            $"Allocated payment {payment.PaymentNumber} to {items.Count} invoice(s)", cancellationToken);
        return Ok(MapPayment(payment));
    }

    [HttpGet("/api/clients/{clientId:guid}/credit-balance")]
    public async Task<ActionResult<ClientCreditBalanceResponse>> GetClientCreditBalance(
        Guid clientId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceView);
        var client = await dbContext.Clients
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == clientId, cancellationToken)
            ?? throw new NotFoundException("Client not found.");

        var available = await InvoiceBalanceCalculator.GetAvailableCreditAsync(dbContext, TenantId, clientId, cancellationToken);
        return Ok(new ClientCreditBalanceResponse(client.Id, client.ClientName, client.CurrentBalance, available, FallbackCurrency));
    }

    /// <summary>
    /// Validates requested allocations against the client's open invoices and caps each at the
    /// invoice's outstanding balance. Returns (invoice, cappedAmount) pairs ready to allocate.
    /// </summary>
    private async Task<IReadOnlyList<(TenantInvoice Invoice, decimal Amount)>> ResolveAllocationItemsAsync(
        Guid clientId, IReadOnlyList<PaymentAllocationInput> requested, CancellationToken cancellationToken)
    {
        if (requested.Count == 0)
            return [];

        var invoiceIds = requested.Select(a => a.InvoiceId).Distinct().ToList();
        var invoices = await dbContext.TenantInvoices
            .Where(i => i.TenantId == TenantId && invoiceIds.Contains(i.Id))
            .ToListAsync(cancellationToken);

        var items = new List<(TenantInvoice, decimal)>();
        foreach (var input in requested)
        {
            var invoice = invoices.SingleOrDefault(i => i.Id == input.InvoiceId)
                ?? throw new NotFoundException($"Invoice {input.InvoiceId} not found.");
            if (invoice.ClientId != clientId)
                throw new BusinessRuleException("All invoices must belong to the paying client.");
            if (invoice.Status == "Void")
                throw new BusinessRuleException($"Invoice {invoice.InvoiceNumber} is voided.");

            var capped = Math.Min(input.Amount, Math.Max(invoice.Balance, 0m));
            if (capped > 0)
                items.Add((invoice, capped));
        }
        return items;
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

        // Reverse every allocation this payment made and recompute the invoices it touched.
        var affected = await InvoiceBalanceCalculator.ReverseAllocationsForSourceAsync(
            dbContext, TenantId, AllocationSourceTypes.Payment, payment.Id, cancellationToken);

        await InvoiceBalanceCalculator.RecalcClientBalancesAsync(
            dbContext, TenantId, affected, cancellationToken, payment.ClientId);

        // Single save: reversal, allocations, invoice balances and client balances commit together.
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "finance.payment.reversed", nameof(TenantPayment), payment.Id.ToString(), $"Reversed payment {payment.PaymentNumber}", cancellationToken);
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
        var bytes = RenderReceiptPdf(payment, client, tenant);
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

        // A work order on a project bills to that project, in the project's currency, so the invoice
        // shows in the project's Financials and closing checks.
        var projectCurrency = workOrder.ProjectId is { } projectId
            ? await dbContext.Projects.Where(x => x.TenantId == TenantId && x.Id == projectId).Select(x => x.Currency).SingleOrDefaultAsync(cancellationToken)
            : null;
        var invoice = new TenantInvoice
        {
            TenantId = TenantId,
            ClientId = workOrder.ClientId,
            ProjectId = projectCurrency is null ? null : workOrder.ProjectId,
            InvoiceNumber = await documentNumberingService.GenerateAsync(TenantId, null, DocumentTypes.Invoice, cancellationToken),
            DocumentType = DocumentTypes.Invoice,
            Status = "Draft",
            InvoiceDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30),
            Currency = projectCurrency ?? FallbackCurrency,
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

        dbContext.WorkOrderEvents.Add(new Ecosys.Domain.Entities.WorkOrderEvent
        {
            TenantId = TenantId,
            WorkOrderId = workOrderId,
            ActorUserId = UserId,
            EventType = "InvoiceCreated",
            Status = workOrder.Status,
            Message = $"Invoice {invoice.InvoiceNumber} was generated.",
            OccurredAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(TenantId, UserId, "finance.invoice.generated_from_wo", nameof(TenantInvoice), invoice.Id.ToString(), $"Generated invoice {invoice.InvoiceNumber} from WO {workOrder.WorkOrderNumber}", cancellationToken);

        await dbContext.Entry(invoice).Reference(x => x.Client).LoadAsync(cancellationToken);
        await dbContext.Entry(invoice).Collection(x => x.Lines).LoadAsync(cancellationToken);
        return CreatedAtAction(nameof(GetInvoice), new { id = invoice.Id }, MapDetail(invoice));
    }

    // ── Email helpers ─────────────────────────────────────────────────────────

    private async Task QueueInvoiceSentEmailAsync(TenantInvoice invoice, CancellationToken cancellationToken)
    {
        var recipients = await financialRecipientResolver.ResolveAsync(TenantId, invoice.ClientId, "Invoice", cancellationToken);
        if (recipients.To.Count == 0)
            throw new BusinessRuleException("Invoice cannot be sent because this client has no email address. Add one to the client and try again.");

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
            throw new BusinessRuleException("Invoice delivery is disabled for the invoice email template.");

        var subject = await emailSubjectRuleService.BuildFinalSubjectAsync(
            TenantId, "invoice.sent", template.Subject, cancellationToken: cancellationToken);
        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(x => x.Id == TenantId, cancellationToken);
        var pdf = await RenderInvoicePdfAsync(invoice, tenant, cancellationToken);
        var cc = recipients.To.Skip(1).Concat(recipients.Cc).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        await emailOutbox.QueueEmailAsync(
            new QueueEmailRequest(
                TenantId, "invoice.sent", "invoice.sent",
                recipients.To.First(), invoice.Client?.ClientName,
                template.SenderNameOverride, null, template.ReplyToOverride,
                subject, template.HtmlBody, template.TextBody, UserId,
                Attachments: [new EmailAttachment($"Invoice-{invoice.InvoiceNumber}.pdf", pdf, "application/pdf")],
                CcAddresses: cc,
                DocumentType: "Invoice",
                DocumentId: invoice.Id,
                TriggeredAutomatically: true,
                IdempotencyKey: $"invoice.sent:{invoice.Id:N}"),
            cancellationToken);
    }

    /// <summary>
    /// Builds the invoice PDF used by both the download endpoint and the "invoice sent" email, pulling in
    /// the client contact, credit terms, linked work orders and the payments applied to the invoice.
    /// </summary>
    private async Task<byte[]> RenderInvoicePdfAsync(TenantInvoice invoice, Tenant? tenant, CancellationToken cancellationToken)
    {
        var templateConfig = PdfTemplateHelper.ResolveForDocument(tenant?.ReportTemplateConfigJson, tenant?.DocumentDesignsJson, "invoice");
        var termsText = templateConfig?.ShowTerms == true
            ? (!string.IsNullOrWhiteSpace(invoice.TermsAndConditions) ? invoice.TermsAndConditions : templateConfig.InvoiceTerms ?? templateConfig.DefaultTermsText)
            : null;

        var client = invoice.Client
            ?? await dbContext.Clients.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == invoice.ClientId, cancellationToken);

        var creditTerm = invoice.CreditTermId is { } creditTermId
            ? await dbContext.CreditTerms
                .Where(x => x.TenantId == TenantId && x.Id == creditTermId)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var workOrderIds = invoice.Lines
            .Where(l => l.WorkOrderId.HasValue)
            .Select(l => l.WorkOrderId!.Value)
            .Distinct()
            .ToList();
        var workOrderRefs = workOrderIds.Count == 0
            ? new List<string>()
            : await dbContext.WorkOrders
                .Where(w => w.TenantId == TenantId && workOrderIds.Contains(w.Id))
                .Select(w => w.WorkOrderNumber)
                .OrderBy(n => n)
                .ToListAsync(cancellationToken);

        // Payments applied to this invoice: actively allocated (showing the allocated share) or, for
        // legacy payments without allocations, linked directly (showing the full amount).
        var allocations = await dbContext.PaymentAllocations
            .Where(a => a.TenantId == TenantId && a.InvoiceId == invoice.Id
                        && a.SourceType == AllocationSourceTypes.Payment && a.Status == "Active")
            .Select(a => new { a.SourceId, a.Amount })
            .ToListAsync(cancellationToken);
        var allocatedPaymentIds = allocations.Select(a => a.SourceId).Distinct().ToList();
        var payments = await dbContext.TenantPayments
            .Where(x => x.TenantId == TenantId && x.Status == "Confirmed"
                        && (x.InvoiceId == invoice.Id || allocatedPaymentIds.Contains(x.Id)))
            .OrderBy(x => x.PaymentDate)
            .ToListAsync(cancellationToken);
        var paymentRows = payments.Select(p =>
        {
            var applied = allocations.Where(a => a.SourceId == p.Id).ToList();
            return new PdfPaymentRow(p.PaymentNumber, p.PaymentDate, p.Method, p.Reference,
                applied.Count > 0 ? applied.Sum(a => a.Amount) : p.Amount);
        }).ToList();

        var model = new InvoicePdfModel(
            InvoiceNumber:  invoice.InvoiceNumber,
            Status:         invoice.Status,
            InvoiceDate:    invoice.InvoiceDate,
            DueDate:        invoice.DueDate,
            ClientName:     client?.ClientName ?? "Unknown",
            ClientAddress:  client?.Location,
            ClientEmail:    client?.Email,
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
            CompanyName:    PdfTemplateHelper.CompanyName(tenant),
            CompanyAddress: PdfTemplateHelper.CompanyAddress(tenant),
            CompanyPhone:   PdfTemplateHelper.CompanyPhone(tenant),
            LogoUrl:        tenant?.LogoUrl,
            PrimaryColor:   templateConfig?.ReportPrimaryColor ?? tenant?.PrimaryColor ?? "#0F4C81",
            FontFamily:     templateConfig?.FontFamily ?? "Helvetica",
            FooterText:     PdfTemplateHelper.ResolveFooterText(templateConfig),
            WatermarkText:  templateConfig?.WatermarkText,
            BankDetails:    PdfTemplateHelper.ComposeBankDetails(templateConfig),
            PaymentInstructions: templateConfig?.ShowBankDetails == true ? templateConfig.PaymentInstructions : null,
            TermsText:      termsText,
            TemplateConfig: templateConfig,
            Company:        PdfTemplateHelper.CompanyDetails(tenant, environment),
            ClientContact:  PdfTemplateHelper.ClientContact(client),
            CreditTermsLabel: creditTerm,
            WorkOrderRefs:  workOrderRefs,
            Payments:       paymentRows,
            DepositAmount:  invoice.DepositAmount,
            DepositPaid:    invoice.DepositPaid,
            IsProforma:     invoice.DocumentType == DocumentTypes.ProformaInvoice);
        return pdfRenderer.RenderInvoicePdf(model);
    }

    private async Task TryQueueReceiptEmailAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        try
        {
            var payment = await dbContext.TenantPayments
                .Include(x => x.Invoice).ThenInclude(x => x!.Client)
                .Include(x => x.Client)
                .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == paymentId, cancellationToken);
            if (payment is null || !string.Equals(payment.Status, "Confirmed", StringComparison.OrdinalIgnoreCase)) return;

            var client = payment.Client ?? payment.Invoice?.Client;
            if (client is null) return;
            var recipients = await financialRecipientResolver.ResolveAsync(TenantId, payment.ClientId, "Receipt", cancellationToken);
            if (recipients.To.Count == 0) throw new BusinessRuleException("Receipt delivery was skipped because this client has no email address.");

            var tenant = await dbContext.Tenants.SingleOrDefaultAsync(x => x.Id == TenantId, cancellationToken);
            var pdf = RenderReceiptPdf(payment, client, tenant);
            var subject = $"Receipt {payment.PaymentNumber} — {client.ClientName}";
            var body = $"<p>Thank you. Receipt <strong>{System.Net.WebUtility.HtmlEncode(payment.PaymentNumber)}</strong> for <strong>{payment.Amount:N2}</strong> has been recorded.</p>";
            var cc = recipients.To.Skip(1).Concat(recipients.Cc).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

            await emailOutbox.QueueEmailAsync(
                new QueueEmailRequest(
                    TenantId, "receipt.issued", "receipt.issued",
                    recipients.To.First(), client.ClientName,
                    null, null, null, subject, body,
                    $"Receipt {payment.PaymentNumber} has been recorded.", UserId,
                    Attachments: [new EmailAttachment($"Receipt-{payment.PaymentNumber}.pdf", pdf, "application/pdf")],
                    CcAddresses: cc,
                    DocumentType: "Receipt",
                    DocumentId: payment.Id,
                    TriggeredAutomatically: true,
                    IdempotencyKey: $"receipt.issued:{payment.Id:N}"),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Receipt delivery could not be queued for payment {PaymentId}.", paymentId);
            await emailDeliveryLogs.LogAsync(
                new EmailDeliveryLogWriteRequest(
                    TenantId, "receipt.issued", "receipt.issued", "unknown@local",
                    $"Receipt delivery {paymentId}", EmailOutboxStatuses.Failed,
                    "QueueingFailed", "Receipt delivery could not be queued.", UserId,
                    DocumentType: "Receipt", DocumentId: paymentId, TriggeredAutomatically: true),
                cancellationToken);
        }
    }

    /// <summary>Builds the receipt PDF used by both the download endpoint and the receipt email.</summary>
    private byte[] RenderReceiptPdf(TenantPayment payment, Client? client, Tenant? tenant)
    {
        var templateConfig = PdfTemplateHelper.ResolveForDocument(tenant?.ReportTemplateConfigJson, tenant?.DocumentDesignsJson, "receipt");
        var invoice = payment.Invoice;
        return pdfRenderer.RenderReceiptPdf(new ReceiptPdfModel(
            payment.PaymentNumber,
            invoice?.InvoiceNumber ?? "N/A",
            client?.ClientName ?? "Unknown",
            payment.PaymentDate,
            payment.Amount,
            invoice?.Currency ?? tenant?.Currency ?? FallbackCurrency,
            payment.Method,
            payment.Reference,
            PdfTemplateHelper.CompanyName(tenant),
            tenant?.LogoUrl,
            templateConfig?.ReportPrimaryColor ?? tenant?.PrimaryColor ?? "#0F4C81",
            FontFamily: templateConfig?.FontFamily ?? "Helvetica",
            FooterText: PdfTemplateHelper.ResolveFooterText(templateConfig),
            WatermarkText: templateConfig?.WatermarkText,
            TemplateConfig: templateConfig,
            Company: PdfTemplateHelper.CompanyDetails(tenant, environment),
            ClientContact: PdfTemplateHelper.ClientContact(client),
            CompanyAddress: PdfTemplateHelper.CompanyAddress(tenant),
            CompanyPhone: PdfTemplateHelper.CompanyPhone(tenant),
            InvoiceDate: invoice?.InvoiceDate,
            InvoiceTotal: invoice?.TotalAmount,
            InvoiceBalanceAfter: invoice?.Balance,
            Notes: payment.Notes));
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

    private static void RecalcTotals(TenantInvoice invoice)
    {
        invoice.Subtotal = invoice.Lines.Sum(x => x.LineTotal);
        invoice.TaxAmount = invoice.Lines.Sum(x => x.TaxAmount);
        invoice.TotalAmount = invoice.Lines.Sum(x => x.GrandTotal);
        invoice.Balance = invoice.TotalAmount - invoice.AmountPaid;
    }

    private static TenantInvoiceListItem MapListItem(TenantInvoice inv) =>
        new(inv.Id, inv.TenantId, inv.ClientId, inv.Client?.ClientName,
            inv.InvoiceNumber, inv.DocumentType, inv.Status, inv.InvoiceDate, inv.DueDate,
            inv.Subtotal, inv.TaxAmount, inv.TotalAmount, inv.AmountPaid,
            inv.Balance, inv.Currency, inv.CreatedAt);

    private static TenantInvoiceDetailResponse MapDetail(
        TenantInvoice inv,
        bool creditLimitWarning = false,
        decimal clientCurrentBalance = 0,
        decimal? clientCreditLimit = null,
        IReadOnlyCollection<TenantInvoiceSourceQuotationResponse>? sourceQuotations = null) =>
        new(inv.Id, inv.TenantId, inv.ClientId, inv.Client?.ClientName,
            inv.InvoiceNumber, inv.DocumentType, inv.Status, inv.InvoiceDate, inv.DueDate,
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
            ClientCreditLimit: clientCreditLimit,
            SourceQuotations: sourceQuotations ?? []);

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

    private async Task<IReadOnlyCollection<TenantInvoiceSourceQuotationResponse>> LoadSourceQuotationsAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        return await dbContext.TenantQuotations
            .Where(x => x.TenantId == TenantId && x.ConvertedToInvoiceId == invoiceId)
            .OrderBy(x => x.QuoteDate)
            .Select(x => new TenantInvoiceSourceQuotationResponse(
                x.Id,
                x.QuoteNumber,
                x.Status,
                x.Subject,
                x.QuoteDate,
                x.TotalAmount,
                x.Currency))
            .ToListAsync(cancellationToken);
    }
}
