using System.Text.Json;
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
[Authorize(Policy = "PlatformFinanceAccess")]
[Route("api/platform/finance")]
public sealed class PlatformFinanceController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IAuditLogService auditLogService) : ControllerBase
{
    private const string TaxSettingsCategory = "platform-finance-taxes";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpGet("dashboard")]
    public async Task<ActionResult<PlatformFinanceDashboardResponse>> GetDashboard(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var totalRevenue = await dbContext.PlatformPayments.Where(x => x.Status == "Paid").SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var outstandingInvoices = await dbContext.PlatformInvoices.Where(x => x.Status != "Paid" && x.Status != "Void").SumAsync(x => (decimal?)x.Balance, cancellationToken) ?? 0m;
        var overdueInvoices = await dbContext.PlatformInvoices.Where(x => x.Status == "Overdue" || (x.DueDate.HasValue && x.DueDate.Value.Date < now.Date && x.Status != "Paid" && x.Status != "Void")).SumAsync(x => (decimal?)x.Balance, cancellationToken) ?? 0m;
        var expensesThisMonth = await dbContext.PlatformExpenses.Where(x => x.ExpenseDate >= monthStart).SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0m;
        var quotationCount = await dbContext.PlatformQuotations.CountAsync(cancellationToken);
        var convertedQuotationCount = await dbContext.PlatformQuotations.CountAsync(x => x.Status == "Converted", cancellationToken);
        var conversionRate = quotationCount == 0 ? 0m : Math.Round((decimal)convertedQuotationCount / quotationCount * 100m, 2);

        var recentPaymentEntities = await dbContext.PlatformPayments.OrderByDescending(x => x.PaidAt).Take(8).ToListAsync(cancellationToken);
        var recentPayments = recentPaymentEntities.Select(MapPayment).ToList();
        var recentInvoices = await dbContext.PlatformInvoices.Include(x => x.Lines).OrderByDescending(x => x.IssueDate).Take(8).ToListAsync(cancellationToken);
        var overdueAccounts = await dbContext.PlatformInvoices.Include(x => x.Lines).Where(x => x.DueDate.HasValue && x.DueDate.Value.Date < now.Date && x.Status != "Paid" && x.Status != "Void").OrderBy(x => x.DueDate).Take(10).ToListAsync(cancellationToken);

        // Receivables aging (4c): bucket unpaid balances by days past due.
        var unpaid = await dbContext.PlatformInvoices
            .Where(x => x.Status != "Paid" && x.Status != "Void")
            .Select(x => new { x.DueDate, x.Balance })
            .ToListAsync(cancellationToken);

        int DaysPastDue(DateTime? due) => due.HasValue ? (now.Date - due.Value.Date).Days : 0;
        var aging = new FinanceAging(
            unpaid.Where(i => !i.DueDate.HasValue || DaysPastDue(i.DueDate) <= 0).Sum(i => i.Balance),
            unpaid.Where(i => i.DueDate.HasValue && DaysPastDue(i.DueDate) is >= 1 and <= 30).Sum(i => i.Balance),
            unpaid.Where(i => i.DueDate.HasValue && DaysPastDue(i.DueDate) is >= 31 and <= 60).Sum(i => i.Balance),
            unpaid.Where(i => i.DueDate.HasValue && DaysPastDue(i.DueDate) is >= 61 and <= 90).Sum(i => i.Balance),
            unpaid.Where(i => i.DueDate.HasValue && DaysPastDue(i.DueDate) > 90).Sum(i => i.Balance));

        return Ok(new PlatformFinanceDashboardResponse(
            totalRevenue,
            outstandingInvoices,
            overdueInvoices,
            expensesThisMonth,
            totalRevenue - expensesThisMonth,
            conversionRate,
            recentPayments,
            recentInvoices.Select(MapInvoice).ToList(),
            overdueAccounts.Select(MapInvoice).ToList(),
            aging));
    }

    [HttpGet("summary")]
    public Task<ActionResult<PlatformFinanceDashboardResponse>> GetSummary(CancellationToken cancellationToken) =>
        GetDashboard(cancellationToken);

    [HttpGet("reports/overview")]
    public Task<ActionResult<PlatformFinanceDashboardResponse>> GetOverviewReport(CancellationToken cancellationToken) =>
        GetDashboard(cancellationToken);

    [HttpGet("quotations")]
    public async Task<ActionResult<IReadOnlyCollection<PlatformQuotationResponse>>> GetQuotations(CancellationToken cancellationToken)
    {
        var rows = await dbContext.PlatformQuotations.Include(x => x.Lines).OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
        return Ok(rows.Select(MapQuotation).ToList());
    }

    [HttpGet("quotations/{id:guid}")]
    public async Task<ActionResult<PlatformQuotationResponse>> GetQuotation(Guid id, CancellationToken cancellationToken)
    {
        var quotation = await LoadQuotationAsync(id, cancellationToken);
        return Ok(MapQuotation(quotation));
    }

    [HttpPost("quotations")]
    public async Task<ActionResult<PlatformQuotationResponse>> CreateQuotation([FromBody] UpsertPlatformQuotationRequest request, CancellationToken cancellationToken)
    {
        var quotation = new PlatformQuotation();
        await ApplyQuotationAsync(quotation, request, true, cancellationToken);
        dbContext.PlatformQuotations.Add(quotation);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(quotation.TenantId, tenantContext.GetRequiredUserId(), "quotation.created", nameof(PlatformQuotation), quotation.Id.ToString(), $"Quotation {quotation.QuotationNumber} created.", cancellationToken: cancellationToken);
        return CreatedAtAction(nameof(GetQuotation), new { id = quotation.Id }, MapQuotation(quotation));
    }

    [HttpPut("quotations/{id:guid}")]
    public async Task<ActionResult<PlatformQuotationResponse>> UpdateQuotation(Guid id, [FromBody] UpsertPlatformQuotationRequest request, CancellationToken cancellationToken)
    {
        var quotation = await LoadQuotationAsync(id, cancellationToken);
        await ApplyQuotationAsync(quotation, request, false, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapQuotation(quotation));
    }

    [HttpPost("quotations/{id:guid}/approve")]
    public Task<ActionResult<PlatformQuotationResponse>> ApproveQuotation(Guid id, CancellationToken cancellationToken) => UpdateQuotationStatusAsync(id, "Accepted", cancellationToken);

    [HttpPost("quotations/{id:guid}/send")]
    public Task<ActionResult<PlatformQuotationResponse>> SendQuotation(Guid id, CancellationToken cancellationToken) => UpdateQuotationStatusAsync(id, "Sent", cancellationToken);

    [HttpPost("quotations/{id:guid}/convert-to-invoice")]
    public async Task<ActionResult<PlatformInvoiceResponse>> ConvertQuotationToInvoice(Guid id, CancellationToken cancellationToken)
    {
        var quotation = await LoadQuotationAsync(id, cancellationToken);
        if (quotation.Status == "Converted" && quotation.ConvertedInvoiceId.HasValue)
        {
            var existing = await LoadInvoiceAsync(quotation.ConvertedInvoiceId.Value, cancellationToken);
            return Ok(MapInvoice(existing));
        }

        var invoice = new PlatformInvoice
        {
            InvoiceNumber = await GenerateDocumentNumberAsync("Invoice", "INV", cancellationToken),
            TenantId = quotation.TenantId,
            QuotationId = quotation.Id,
            CustomerName = quotation.CustomerName,
            CustomerEmail = quotation.CustomerEmail,
            Currency = quotation.Currency,
            Subtotal = quotation.Subtotal,
            DiscountRate = quotation.DiscountRate,
            DiscountAmount = quotation.DiscountAmount,
            TaxRate = quotation.TaxRate,
            TaxAmount = quotation.TaxAmount,
            Total = quotation.Total,
            AmountPaid = 0,
            Balance = quotation.Total,
            Status = "Draft",
            IssueDate = DateTime.UtcNow,
            DueDate = quotation.ValidUntil,
            Notes = quotation.Notes,
            Lines = quotation.Lines.Select(x => new PlatformInvoiceLine { Description = x.Description, Quantity = x.Quantity, UnitPrice = x.UnitPrice, Taxable = x.Taxable, LineTotal = x.LineTotal }).ToList()
        };

        dbContext.PlatformInvoices.Add(invoice);
        quotation.Status = "Converted";
        await dbContext.SaveChangesAsync(cancellationToken);
        quotation.ConvertedInvoiceId = invoice.Id;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapInvoice(invoice));
    }

    [HttpGet("invoices")]
    public async Task<ActionResult<IReadOnlyCollection<PlatformInvoiceResponse>>> GetInvoices(CancellationToken cancellationToken)
    {
        var rows = await dbContext.PlatformInvoices.Include(x => x.Lines).OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
        return Ok(rows.Select(MapInvoice).ToList());
    }

    [HttpGet("invoices/{id:guid}")]
    public async Task<ActionResult<PlatformInvoiceResponse>> GetInvoice(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await LoadInvoiceAsync(id, cancellationToken);
        return Ok(MapInvoice(invoice));
    }

    [HttpPost("invoices")]
    public async Task<ActionResult<PlatformInvoiceResponse>> CreateInvoice([FromBody] UpsertPlatformInvoiceRequest request, CancellationToken cancellationToken)
    {
        var invoice = new PlatformInvoice();
        await ApplyInvoiceAsync(invoice, request, true, cancellationToken);
        dbContext.PlatformInvoices.Add(invoice);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetInvoice), new { id = invoice.Id }, MapInvoice(invoice));
    }

    [HttpPut("invoices/{id:guid}")]
    public async Task<ActionResult<PlatformInvoiceResponse>> UpdateInvoice(Guid id, [FromBody] UpsertPlatformInvoiceRequest request, CancellationToken cancellationToken)
    {
        var invoice = await LoadInvoiceAsync(id, cancellationToken);
        await ApplyInvoiceAsync(invoice, request, false, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapInvoice(invoice));
    }

    [HttpPost("invoices/{id:guid}/send")]
    public Task<ActionResult<PlatformInvoiceResponse>> SendInvoice(Guid id, CancellationToken cancellationToken) => UpdateInvoiceStatusAsync(id, "Sent", cancellationToken);

    [HttpPost("invoices/{id:guid}/mark-paid")]
    public async Task<ActionResult<PlatformInvoiceResponse>> MarkInvoicePaid(Guid id, [FromBody] MarkInvoicePaidRequest? request, CancellationToken cancellationToken)
    {
        var invoice = await LoadInvoiceAsync(id, cancellationToken);
        var amount = request?.AmountPaid ?? invoice.Balance;
        if (amount <= 0) throw new BusinessRuleException("Paid amount must be greater than zero.");

        invoice.AmountPaid += amount;
        if (invoice.AmountPaid >= invoice.Total)
        {
            invoice.AmountPaid = invoice.Total;
            invoice.Balance = 0;
            invoice.Status = "Paid";
        }
        else
        {
            invoice.Balance = invoice.Total - invoice.AmountPaid;
            invoice.Status = "PartiallyPaid";
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapInvoice(invoice));
    }

    [HttpPost("invoices/{id:guid}/void")]
    public Task<ActionResult<PlatformInvoiceResponse>> VoidInvoice(Guid id, CancellationToken cancellationToken) => UpdateInvoiceStatusAsync(id, "Void", cancellationToken);

    [HttpGet("payments")]
    public async Task<ActionResult<IReadOnlyCollection<PlatformPaymentResponse>>> GetPayments(CancellationToken cancellationToken)
    {
        var rows = await dbContext.PlatformPayments.OrderByDescending(x => x.PaidAt).ToListAsync(cancellationToken);
        return Ok(rows.Select(MapPayment).ToList());
    }

    [HttpGet("payments/{id:guid}")]
    public async Task<ActionResult<PlatformPaymentResponse>> GetPayment(Guid id, CancellationToken cancellationToken)
    {
        var payment = await dbContext.PlatformPayments.SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new NotFoundException("Payment was not found.");
        return Ok(MapPayment(payment));
    }

    [HttpPost("payments")]
    public async Task<ActionResult<PlatformPaymentResponse>> CreatePayment([FromBody] CreatePlatformPaymentRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0) throw new BusinessRuleException("Payment amount must be greater than zero.");

        PlatformInvoice? invoice = null;
        if (request.InvoiceId.HasValue)
        {
            invoice = await dbContext.PlatformInvoices.SingleOrDefaultAsync(x => x.Id == request.InvoiceId.Value, cancellationToken) ?? throw new NotFoundException("Invoice was not found.");
        }

        var payment = new PlatformPayment
        {
            PaymentNumber = await GenerateDocumentNumberAsync("Receipt", "RCT", cancellationToken),
            InvoiceId = request.InvoiceId,
            TenantId = request.TenantId ?? invoice?.TenantId,
            Amount = request.Amount,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "KES" : request.Currency.Trim().ToUpperInvariant(),
            Method = NormalizePaymentMethod(request.Method),
            Status = NormalizePaymentStatus(request.Status),
            Reference = NormalizeOptional(request.Reference),
            PaidAt = request.PaidAt ?? DateTime.UtcNow,
            Notes = NormalizeOptional(request.Notes)
        };
        dbContext.PlatformPayments.Add(payment);

        if (invoice is not null)
        {
            invoice.AmountPaid += payment.Amount;
            if (invoice.AmountPaid >= invoice.Total)
            {
                invoice.AmountPaid = invoice.Total;
                invoice.Balance = 0;
                invoice.Status = "Paid";
            }
            else
            {
                invoice.Balance = invoice.Total - invoice.AmountPaid;
                invoice.Status = "PartiallyPaid";
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetPayment), new { id = payment.Id }, MapPayment(payment));
    }

    [HttpGet("expenses")]
    public async Task<ActionResult<IReadOnlyCollection<PlatformExpenseResponse>>> GetExpenses(CancellationToken cancellationToken)
    {
        var rows = await dbContext.PlatformExpenses.OrderByDescending(x => x.ExpenseDate).ToListAsync(cancellationToken);
        return Ok(rows.Select(MapExpense).ToList());
    }

    [HttpGet("expenses/{id:guid}")]
    public async Task<ActionResult<PlatformExpenseResponse>> GetExpense(Guid id, CancellationToken cancellationToken)
    {
        var expense = await dbContext.PlatformExpenses.SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new NotFoundException("Expense was not found.");
        return Ok(MapExpense(expense));
    }

    [HttpPost("expenses")]
    public async Task<ActionResult<PlatformExpenseResponse>> CreateExpense([FromBody] UpsertPlatformExpenseRequest request, CancellationToken cancellationToken)
    {
        var expense = new PlatformExpense();
        ApplyExpense(expense, request);
        dbContext.PlatformExpenses.Add(expense);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetExpense), new { id = expense.Id }, MapExpense(expense));
    }

    [HttpPut("expenses/{id:guid}")]
    public async Task<ActionResult<PlatformExpenseResponse>> UpdateExpense(Guid id, [FromBody] UpsertPlatformExpenseRequest request, CancellationToken cancellationToken)
    {
        var expense = await dbContext.PlatformExpenses.SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new NotFoundException("Expense was not found.");
        ApplyExpense(expense, request);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapExpense(expense));
    }

    [HttpPost("expenses/{id:guid}/approve")]
    public async Task<ActionResult<PlatformExpenseResponse>> ApproveExpense(Guid id, CancellationToken cancellationToken)
    {
        var expense = await dbContext.PlatformExpenses.SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new NotFoundException("Expense was not found.");
        expense.Status = "Approved";
        expense.ApprovedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapExpense(expense));
    }

    [HttpGet("taxes")]
    public async Task<ActionResult<IReadOnlyCollection<PlatformTaxResponse>>> GetTaxes(CancellationToken cancellationToken)
    {
        return Ok(await LoadTaxesAsync(cancellationToken));
    }

    [HttpPost("taxes")]
    public async Task<ActionResult<PlatformTaxResponse>> CreateTax([FromBody] UpsertPlatformTaxRequest request, CancellationToken cancellationToken)
    {
        var taxes = await LoadTaxesAsync(cancellationToken);
        var tax = new PlatformTaxResponse(Guid.NewGuid(), request.Name.Trim(), request.Rate, request.Mode, request.IsDefault);
        if (tax.IsDefault)
        {
            taxes = taxes.Select(x => x with { IsDefault = false }).ToList();
        }

        taxes.Add(tax);
        await SaveTaxesAsync(taxes, cancellationToken);
        return CreatedAtAction(nameof(GetTaxes), new { id = tax.Id }, tax);
    }

    [HttpPut("taxes/{id:guid}")]
    public async Task<ActionResult<PlatformTaxResponse>> UpdateTax(Guid id, [FromBody] UpsertPlatformTaxRequest request, CancellationToken cancellationToken)
    {
        var taxes = await LoadTaxesAsync(cancellationToken);
        var current = taxes.SingleOrDefault(x => x.Id == id) ?? throw new NotFoundException("Tax setting was not found.");
        if (request.IsDefault)
        {
            taxes = taxes.Select(x => x with { IsDefault = false }).ToList();
        }

        var updated = current with { Name = request.Name.Trim(), Rate = request.Rate, Mode = request.Mode, IsDefault = request.IsDefault };
        taxes[taxes.FindIndex(x => x.Id == id)] = updated;
        await SaveTaxesAsync(taxes, cancellationToken);
        return Ok(updated);
    }

    [HttpGet("templates")]
    public async Task<ActionResult<IReadOnlyCollection<PlatformTemplateResponse>>> GetTemplates(CancellationToken cancellationToken)
    {
        var rows = await dbContext.PlatformDocumentTemplates.OrderBy(x => x.Type).ThenBy(x => x.Name).ToListAsync(cancellationToken);
        return Ok(rows.Select(MapTemplate).ToList());
    }

    [HttpGet("templates/{id:guid}")]
    public async Task<ActionResult<PlatformTemplateResponse>> GetTemplate(Guid id, CancellationToken cancellationToken)
    {
        var row = await dbContext.PlatformDocumentTemplates.SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new NotFoundException("Template was not found.");
        return Ok(MapTemplate(row));
    }

    [HttpPost("templates")]
    public async Task<ActionResult<PlatformTemplateResponse>> CreateTemplate([FromBody] UpsertPlatformTemplateRequest request, CancellationToken cancellationToken)
    {
        var resolvedBody = NormalizeOptional(request.BodyHtml) ?? request.PreviewText?.Trim() ?? string.Empty;
        var resolvedPreview = BuildPreviewText(request.PreviewText, resolvedBody);
        var row = new PlatformDocumentTemplate
        {
            Name = request.Name.Trim(),
            Type = request.Type.Trim(),
            PreviewText = resolvedPreview,
            Subject = NormalizeOptional(request.Subject),
            HeaderHtml = NormalizeOptional(request.HeaderHtml),
            BodyHtml = resolvedBody,
            FooterHtml = NormalizeOptional(request.FooterHtml),
            TermsHtml = NormalizeOptional(request.TermsHtml),
            SignatureHtml = NormalizeOptional(request.SignatureHtml),
            IsDefault = request.IsDefault,
            IsActive = request.IsActive,
            PageSize = NormalizePageSize(request.PageSize),
            Orientation = NormalizeOrientation(request.Orientation),
            ShowLogo = request.ShowLogo ?? true,
            ShowTenantBranding = request.ShowTenantBranding ?? true,
            ShowPoweredByEcosys = request.ShowPoweredByEcosys ?? true,
            CreatedByUserId = tenantContext.GetRequiredUserId(),
            UpdatedByUserId = tenantContext.GetRequiredUserId()
        };
        if (row.IsDefault)
        {
            await ResetDefaultTemplatesAsync(row.Type, cancellationToken);
        }

        dbContext.PlatformDocumentTemplates.Add(row);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetTemplate), new { id = row.Id }, MapTemplate(row));
    }

    [HttpPut("templates/{id:guid}")]
    public async Task<ActionResult<PlatformTemplateResponse>> UpdateTemplate(Guid id, [FromBody] UpsertPlatformTemplateRequest request, CancellationToken cancellationToken)
    {
        var row = await dbContext.PlatformDocumentTemplates.SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new NotFoundException("Template was not found.");
        var resolvedBody = NormalizeOptional(request.BodyHtml) ?? request.PreviewText?.Trim() ?? row.BodyHtml ?? row.PreviewText;
        row.Name = request.Name.Trim();
        row.Type = request.Type.Trim();
        row.PreviewText = BuildPreviewText(request.PreviewText, resolvedBody);
        row.Subject = NormalizeOptional(request.Subject);
        row.HeaderHtml = NormalizeOptional(request.HeaderHtml);
        row.BodyHtml = resolvedBody;
        row.FooterHtml = NormalizeOptional(request.FooterHtml);
        row.TermsHtml = NormalizeOptional(request.TermsHtml);
        row.SignatureHtml = NormalizeOptional(request.SignatureHtml);
        row.IsActive = request.IsActive;
        row.PageSize = NormalizePageSize(request.PageSize);
        row.Orientation = NormalizeOrientation(request.Orientation);
        row.ShowLogo = request.ShowLogo ?? row.ShowLogo;
        row.ShowTenantBranding = request.ShowTenantBranding ?? row.ShowTenantBranding;
        row.ShowPoweredByEcosys = request.ShowPoweredByEcosys ?? row.ShowPoweredByEcosys;
        row.UpdatedByUserId = tenantContext.GetRequiredUserId();

        if (request.IsDefault)
        {
            await ResetDefaultTemplatesAsync(row.Type, cancellationToken);
            row.IsDefault = true;
        }
        else
        {
            row.IsDefault = false;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapTemplate(row));
    }

    private async Task<PlatformQuotation> LoadQuotationAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.PlatformQuotations.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
        ?? throw new NotFoundException("Quotation was not found.");

    private async Task<PlatformInvoice> LoadInvoiceAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.PlatformInvoices.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
        ?? throw new NotFoundException("Invoice was not found.");

    private async Task ApplyQuotationAsync(PlatformQuotation quotation, UpsertPlatformQuotationRequest request, bool isCreate, CancellationToken cancellationToken)
    {
        ValidateLines(request.Lines);
        if (isCreate)
        {
            quotation.QuotationNumber = await GenerateDocumentNumberAsync("Quotation", "QUO", cancellationToken);
        }

        quotation.TenantId = request.TenantId;
        quotation.CustomerName = request.CustomerName.Trim();
        quotation.CustomerEmail = NormalizeOptional(request.CustomerEmail);
        quotation.Currency = string.IsNullOrWhiteSpace(request.Currency) ? "KES" : request.Currency.Trim().ToUpperInvariant();
        quotation.Status = NormalizeQuotationStatus(request.Status);
        quotation.ValidUntil = DateTimeNormalizationHelper.NormalizeNullableToUtc(request.ValidUntil);
        quotation.Notes = NormalizeOptional(request.Notes);

        var subtotal = request.Lines.Sum(x => x.Quantity * x.UnitPrice);
        var discountRate = Math.Max(0m, request.DiscountRate);
        var discountAmount = Math.Round(subtotal * discountRate / 100m, 2);
        var taxableBase = Math.Max(0m, subtotal - discountAmount);
        var taxRate = Math.Max(0m, request.TaxRate);
        var taxAmount = Math.Round(taxableBase * taxRate / 100m, 2);
        quotation.Subtotal = subtotal;
        quotation.DiscountRate = discountRate;
        quotation.DiscountAmount = discountAmount;
        quotation.TaxRate = taxRate;
        quotation.TaxAmount = taxAmount;
        quotation.Total = taxableBase + taxAmount;

        dbContext.PlatformQuotationLines.RemoveRange(quotation.Lines);
        quotation.Lines = request.Lines.Select(x => new PlatformQuotationLine
        {
            Description = x.Description.Trim(),
            Quantity = x.Quantity,
            UnitPrice = x.UnitPrice,
            Taxable = x.Taxable,
            LineTotal = Math.Round(x.Quantity * x.UnitPrice, 2)
        }).ToList();
    }

    private async Task ApplyInvoiceAsync(PlatformInvoice invoice, UpsertPlatformInvoiceRequest request, bool isCreate, CancellationToken cancellationToken)
    {
        ValidateLines(request.Lines);
        if (isCreate)
        {
            invoice.InvoiceNumber = await GenerateDocumentNumberAsync("Invoice", "INV", cancellationToken);
        }

        invoice.TenantId = request.TenantId;
        invoice.QuotationId = request.QuotationId;
        invoice.CustomerName = request.CustomerName.Trim();
        invoice.CustomerEmail = NormalizeOptional(request.CustomerEmail);
        invoice.Currency = string.IsNullOrWhiteSpace(request.Currency) ? "KES" : request.Currency.Trim().ToUpperInvariant();
        invoice.Status = NormalizeInvoiceStatus(request.Status);
        invoice.IssueDate = DateTimeNormalizationHelper.NormalizeToUtc(request.IssueDate);
        invoice.DueDate = DateTimeNormalizationHelper.NormalizeNullableToUtc(request.DueDate);
        invoice.Notes = NormalizeOptional(request.Notes);

        var subtotal = request.Lines.Sum(x => x.Quantity * x.UnitPrice);
        var discountRate = Math.Max(0m, request.DiscountRate);
        var discountAmount = Math.Round(subtotal * discountRate / 100m, 2);
        var taxableBase = Math.Max(0m, subtotal - discountAmount);
        var taxRate = Math.Max(0m, request.TaxRate);
        var taxAmount = Math.Round(taxableBase * taxRate / 100m, 2);
        var total = taxableBase + taxAmount;
        invoice.Subtotal = subtotal;
        invoice.DiscountRate = discountRate;
        invoice.DiscountAmount = discountAmount;
        invoice.TaxRate = taxRate;
        invoice.TaxAmount = taxAmount;
        invoice.Total = total;

        if (invoice.AmountPaid > total) invoice.AmountPaid = total;
        invoice.Balance = total - invoice.AmountPaid;

        dbContext.PlatformInvoiceLines.RemoveRange(invoice.Lines);
        invoice.Lines = request.Lines.Select(x => new PlatformInvoiceLine
        {
            Description = x.Description.Trim(),
            Quantity = x.Quantity,
            UnitPrice = x.UnitPrice,
            Taxable = x.Taxable,
            LineTotal = Math.Round(x.Quantity * x.UnitPrice, 2)
        }).ToList();
    }

    private void ApplyExpense(PlatformExpense expense, UpsertPlatformExpenseRequest request)
    {
        if (request.Amount < 0 || request.TaxAmount < 0) throw new BusinessRuleException("Expense amount and tax cannot be negative.");
        expense.ExpenseDate = DateTimeNormalizationHelper.NormalizeToUtc(request.ExpenseDate);
        expense.Category = request.Category.Trim();
        expense.Vendor = NormalizeOptional(request.Vendor);
        expense.Description = request.Description.Trim();
        expense.Amount = request.Amount;
        expense.TaxAmount = request.TaxAmount;
        expense.TotalAmount = request.Amount + request.TaxAmount;
        expense.Currency = string.IsNullOrWhiteSpace(request.Currency) ? "KES" : request.Currency.Trim().ToUpperInvariant();
        expense.PaymentMethod = NormalizePaymentMethod(request.PaymentMethod);
        expense.AttachmentUrl = NormalizeOptional(request.AttachmentUrl);
        expense.Status = NormalizeExpenseStatus(request.Status);
        expense.TenantId = request.TenantId;
    }

    private async Task<ActionResult<PlatformQuotationResponse>> UpdateQuotationStatusAsync(Guid id, string status, CancellationToken cancellationToken)
    {
        var quotation = await LoadQuotationAsync(id, cancellationToken);
        quotation.Status = NormalizeQuotationStatus(status);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapQuotation(quotation));
    }

    private async Task<ActionResult<PlatformInvoiceResponse>> UpdateInvoiceStatusAsync(Guid id, string status, CancellationToken cancellationToken)
    {
        var invoice = await LoadInvoiceAsync(id, cancellationToken);
        invoice.Status = NormalizeInvoiceStatus(status);
        if (invoice.Status == "Void") invoice.Balance = 0;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapInvoice(invoice));
    }

    private async Task<string> GenerateDocumentNumberAsync(string documentType, string defaultPrefix, CancellationToken cancellationToken)
    {
        var setting = await dbContext.NumberingSettings.SingleOrDefaultAsync(
            x => x.TenantId == PlatformConstants.RootTenantId && x.BranchId == null && x.DocumentType == documentType,
            cancellationToken);
        if (setting is null)
        {
            setting = new NumberingSetting
            {
                TenantId = PlatformConstants.RootTenantId,
                DocumentType = documentType,
                Prefix = defaultPrefix,
                NextNumber = 1,
                PaddingLength = 6,
                ResetFrequency = "Never",
                IncludeYear = true,
                IsActive = true
            };
            dbContext.NumberingSettings.Add(setting);
        }

        var number = setting.NextNumber;
        setting.NextNumber += 1;
        return $"{setting.Prefix.ToUpperInvariant()}-{DateTime.UtcNow:yyyy}-{number.ToString().PadLeft(Math.Max(3, setting.PaddingLength), '0')}";
    }

    private async Task<List<PlatformTaxResponse>> LoadTaxesAsync(CancellationToken cancellationToken)
    {
        var setting = await dbContext.PlatformSettings.SingleOrDefaultAsync(x => x.Category == TaxSettingsCategory, cancellationToken);
        if (setting is null)
        {
            return [new PlatformTaxResponse(Guid.Parse("00000000-0000-0000-0000-000000000001"), "VAT", 16m, "Exclusive", true)];
        }

        return JsonSerializer.Deserialize<List<PlatformTaxResponse>>(setting.JsonValue, JsonOptions)
            ?? [new PlatformTaxResponse(Guid.Parse("00000000-0000-0000-0000-000000000001"), "VAT", 16m, "Exclusive", true)];
    }

    private async Task SaveTaxesAsync(List<PlatformTaxResponse> taxes, CancellationToken cancellationToken)
    {
        var setting = await dbContext.PlatformSettings.SingleOrDefaultAsync(x => x.Category == TaxSettingsCategory, cancellationToken);
        if (setting is null)
        {
            dbContext.PlatformSettings.Add(new PlatformSetting { Category = TaxSettingsCategory, JsonValue = JsonSerializer.Serialize(taxes, JsonOptions) });
        }
        else
        {
            setting.JsonValue = JsonSerializer.Serialize(taxes, JsonOptions);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ResetDefaultTemplatesAsync(string type, CancellationToken cancellationToken)
    {
        await dbContext.PlatformDocumentTemplates
            .Where(x => x.Type == type)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsDefault, false), cancellationToken);
    }

    private static PlatformQuotationResponse MapQuotation(PlatformQuotation x) =>
        new(
            x.Id, x.QuotationNumber, x.TenantId, x.CustomerName, x.CustomerEmail, x.Currency, x.Subtotal, x.DiscountRate, x.DiscountAmount,
            x.TaxRate, x.TaxAmount, x.Total, x.Status, x.ValidUntil, x.Notes, x.ConvertedInvoiceId,
            x.Lines.Select(y => new PlatformQuotationLineResponse(y.Id, y.Description, y.Quantity, y.UnitPrice, y.Taxable, y.LineTotal)).ToList(),
            x.CreatedAt, x.UpdatedAt);

    private static PlatformInvoiceResponse MapInvoice(PlatformInvoice x) =>
        new(
            x.Id, x.InvoiceNumber, x.TenantId, x.QuotationId, x.CustomerName, x.CustomerEmail, x.Currency, x.Subtotal, x.DiscountRate, x.DiscountAmount,
            x.TaxRate, x.TaxAmount, x.Total, x.AmountPaid, x.Balance, x.Status, x.IssueDate, x.DueDate, x.Notes,
            x.Lines.Select(y => new PlatformInvoiceLineResponse(y.Id, y.Description, y.Quantity, y.UnitPrice, y.Taxable, y.LineTotal)).ToList(),
            x.CreatedAt, x.UpdatedAt);

    private static PlatformPaymentResponse MapPayment(PlatformPayment x) =>
        new(x.Id, x.PaymentNumber, x.InvoiceId, x.TenantId, x.Amount, x.Currency, x.Method, x.Status, x.Reference, x.PaidAt, x.Notes, x.CreatedAt, x.UpdatedAt);

    private static PlatformExpenseResponse MapExpense(PlatformExpense x) =>
        new(x.Id, x.ExpenseDate, x.Category, x.Vendor, x.Description, x.Amount, x.TaxAmount, x.TotalAmount, x.Currency, x.PaymentMethod, x.AttachmentUrl, x.Status, x.ApprovedAt, x.TenantId, x.CreatedAt, x.UpdatedAt);

    private static PlatformTemplateResponse MapTemplate(PlatformDocumentTemplate x) =>
        new(
            x.Id,
            x.Name,
            x.Type,
            x.PreviewText,
            x.IsDefault,
            x.IsActive,
            x.Subject,
            x.HeaderHtml,
            x.BodyHtml,
            x.FooterHtml,
            x.TermsHtml,
            x.SignatureHtml,
            x.PageSize,
            x.Orientation,
            x.ShowLogo,
            x.ShowTenantBranding,
            x.ShowPoweredByEcosys,
            x.CreatedByUserId,
            x.UpdatedByUserId,
            x.CreatedAt,
            x.UpdatedAt);

    private static void ValidateLines(IReadOnlyCollection<UpsertFinanceLineRequest> lines)
    {
        if (lines.Count == 0) throw new BusinessRuleException("At least one line item is required.");
        if (lines.Any(x => string.IsNullOrWhiteSpace(x.Description) || x.Quantity <= 0 || x.UnitPrice < 0))
        {
            throw new BusinessRuleException("Line items must include a description, positive quantity, and non-negative unit price.");
        }
    }

    private static string NormalizeQuotationStatus(string? status)
    {
        var normalized = string.IsNullOrWhiteSpace(status) ? "Draft" : status.Trim();
        return normalized switch
        {
            "Draft" => "Draft",
            "Sent" => "Sent",
            "Accepted" => "Accepted",
            "Rejected" => "Rejected",
            "Expired" => "Expired",
            "Converted" => "Converted",
            _ => throw new BusinessRuleException("Quotation status must be Draft, Sent, Accepted, Rejected, Expired, or Converted.")
        };
    }

    private static string NormalizeInvoiceStatus(string? status)
    {
        var normalized = string.IsNullOrWhiteSpace(status) ? "Draft" : status.Trim();
        return normalized switch
        {
            "Draft" => "Draft",
            "Sent" => "Sent",
            "PartiallyPaid" => "PartiallyPaid",
            "Paid" => "Paid",
            "Overdue" => "Overdue",
            "Void" => "Void",
            _ => throw new BusinessRuleException("Invoice status must be Draft, Sent, PartiallyPaid, Paid, Overdue, or Void.")
        };
    }

    private static string NormalizePaymentMethod(string? method)
    {
        var normalized = string.IsNullOrWhiteSpace(method) ? "Other" : method.Trim();
        return normalized switch
        {
            "Cash" => "Cash",
            "M-Pesa" => "M-Pesa",
            "Bank Transfer" => "Bank Transfer",
            "Cheque" => "Cheque",
            "Card" => "Card",
            "Other" => "Other",
            _ => "Other"
        };
    }

    private static string NormalizePaymentStatus(string? status)
    {
        var normalized = string.IsNullOrWhiteSpace(status) ? "Paid" : status.Trim();
        return normalized switch
        {
            "Pending" => "Pending",
            "Paid" => "Paid",
            "Failed" => "Failed",
            "Reversed" => "Reversed",
            _ => "Paid"
        };
    }

    private static string NormalizeExpenseStatus(string? status)
    {
        var normalized = string.IsNullOrWhiteSpace(status) ? "Draft" : status.Trim();
        return normalized switch
        {
            "Draft" => "Draft",
            "Submitted" => "Submitted",
            "Approved" => "Approved",
            "Rejected" => "Rejected",
            _ => "Draft"
        };
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string BuildPreviewText(string? requestedPreview, string fallbackBody)
    {
        if (!string.IsNullOrWhiteSpace(requestedPreview))
        {
            return requestedPreview.Trim();
        }

        if (string.IsNullOrWhiteSpace(fallbackBody))
        {
            return "Template";
        }

        var text = fallbackBody.Replace("\r", string.Empty).Replace("\n", " ").Trim();
        return text.Length <= 240 ? text : text[..240];
    }

    private static string NormalizePageSize(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "A4" : value.Trim();
        return normalized switch
        {
            "A4" => "A4",
            "Letter" => "Letter",
            "Receipt" => "Receipt",
            _ => "A4"
        };
    }

    private static string NormalizeOrientation(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "Portrait" : value.Trim();
        return normalized switch
        {
            "Portrait" => "Portrait",
            "Landscape" => "Landscape",
            _ => "Portrait"
        };
    }
}

public sealed record UpsertFinanceLineRequest(string Description, decimal Quantity, decimal UnitPrice, bool Taxable);
public sealed record UpsertPlatformQuotationRequest(Guid? TenantId, string CustomerName, string? CustomerEmail, string? Currency, decimal TaxRate, decimal DiscountRate, string? Status, DateTime? ValidUntil, string? Notes, IReadOnlyCollection<UpsertFinanceLineRequest> Lines);
public sealed record UpsertPlatformInvoiceRequest(Guid? TenantId, Guid? QuotationId, string CustomerName, string? CustomerEmail, string? Currency, decimal TaxRate, decimal DiscountRate, string? Status, DateTime IssueDate, DateTime? DueDate, string? Notes, IReadOnlyCollection<UpsertFinanceLineRequest> Lines);
public sealed record CreatePlatformPaymentRequest(Guid? InvoiceId, Guid? TenantId, decimal Amount, string? Currency, string? Method, string? Status, string? Reference, DateTime? PaidAt, string? Notes);
public sealed record UpsertPlatformExpenseRequest(DateTime ExpenseDate, string Category, string? Vendor, string Description, decimal Amount, decimal TaxAmount, string? Currency, string? PaymentMethod, string? AttachmentUrl, string? Status, Guid? TenantId);
public sealed record UpsertPlatformTaxRequest(string Name, decimal Rate, string Mode, bool IsDefault);
public sealed record UpsertPlatformTemplateRequest(
    string Name,
    string Type,
    string? PreviewText,
    bool IsDefault,
    bool IsActive,
    string? Subject,
    string? HeaderHtml,
    string? BodyHtml,
    string? FooterHtml,
    string? TermsHtml,
    string? SignatureHtml,
    string? PageSize,
    string? Orientation,
    bool? ShowLogo,
    bool? ShowTenantBranding,
    bool? ShowPoweredByEcosys);
public sealed record MarkInvoicePaidRequest(decimal? AmountPaid);

public sealed record FinanceAging(decimal Current, decimal Days1To30, decimal Days31To60, decimal Days61To90, decimal Days90Plus);
public sealed record PlatformFinanceDashboardResponse(decimal TotalRevenue, decimal OutstandingInvoices, decimal OverdueInvoices, decimal ExpensesThisMonth, decimal ProfitEstimate, decimal QuotationConversionRate, IReadOnlyCollection<PlatformPaymentResponse> RecentPayments, IReadOnlyCollection<PlatformInvoiceResponse> RecentInvoices, IReadOnlyCollection<PlatformInvoiceResponse> OverdueAccounts, FinanceAging Aging);
public sealed record PlatformQuotationResponse(Guid Id, string QuotationNumber, Guid? TenantId, string CustomerName, string? CustomerEmail, string Currency, decimal Subtotal, decimal DiscountRate, decimal DiscountAmount, decimal TaxRate, decimal TaxAmount, decimal Total, string Status, DateTime? ValidUntil, string? Notes, Guid? ConvertedInvoiceId, IReadOnlyCollection<PlatformQuotationLineResponse> Lines, DateTime CreatedAt, DateTime? UpdatedAt);
public sealed record PlatformQuotationLineResponse(Guid Id, string Description, decimal Quantity, decimal UnitPrice, bool Taxable, decimal LineTotal);
public sealed record PlatformInvoiceResponse(Guid Id, string InvoiceNumber, Guid? TenantId, Guid? QuotationId, string CustomerName, string? CustomerEmail, string Currency, decimal Subtotal, decimal DiscountRate, decimal DiscountAmount, decimal TaxRate, decimal TaxAmount, decimal Total, decimal AmountPaid, decimal Balance, string Status, DateTime IssueDate, DateTime? DueDate, string? Notes, IReadOnlyCollection<PlatformInvoiceLineResponse> Lines, DateTime CreatedAt, DateTime? UpdatedAt);
public sealed record PlatformInvoiceLineResponse(Guid Id, string Description, decimal Quantity, decimal UnitPrice, bool Taxable, decimal LineTotal);
public sealed record PlatformPaymentResponse(Guid Id, string PaymentNumber, Guid? InvoiceId, Guid? TenantId, decimal Amount, string Currency, string Method, string Status, string? Reference, DateTime PaidAt, string? Notes, DateTime CreatedAt, DateTime? UpdatedAt);
public sealed record PlatformExpenseResponse(Guid Id, DateTime ExpenseDate, string Category, string? Vendor, string Description, decimal Amount, decimal TaxAmount, decimal TotalAmount, string Currency, string PaymentMethod, string? AttachmentUrl, string Status, DateTime? ApprovedAt, Guid? TenantId, DateTime CreatedAt, DateTime? UpdatedAt);
public sealed record PlatformTaxResponse(Guid Id, string Name, decimal Rate, string Mode, bool IsDefault);
public sealed record PlatformTemplateResponse(
    Guid Id,
    string Name,
    string Type,
    string PreviewText,
    bool IsDefault,
    bool IsActive,
    string? Subject,
    string? HeaderHtml,
    string? BodyHtml,
    string? FooterHtml,
    string? TermsHtml,
    string? SignatureHtml,
    string PageSize,
    string Orientation,
    bool ShowLogo,
    bool ShowTenantBranding,
    bool ShowPoweredByEcosys,
    Guid? CreatedByUserId,
    Guid? UpdatedByUserId,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
