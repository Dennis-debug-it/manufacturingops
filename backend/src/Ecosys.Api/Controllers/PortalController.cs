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
[Authorize(Policy = "PortalAccess")]
[Route("api/portal")]
public sealed class PortalController(
    AppDbContext dbContext,
    IPortalContext portalContext,
    IDocumentNumberingService documentNumberingService,
    IAuditLogService auditLogService) : ControllerBase
{
    private Guid TenantId => portalContext.GetRequiredTenantId();
    private Guid ClientId => portalContext.GetRequiredClientId();

    // ── Dashboard ────────────────────────────────────────────────────────────

    [HttpGet("settings")]
    public async Task<ActionResult<PortalSettingsResponse>> GetSettings(CancellationToken cancellationToken)
    {
        var settings = await LoadSettingsAsync(cancellationToken);
        return Ok(new PortalSettingsResponse(
            settings.AllowRequestSubmission,
            settings.AllowQuoteResponses,
            settings.ShowInvoices,
            settings.ShowContracts,
            settings.ShowAssetRegister,
            settings.WelcomeMessage));
    }

    [HttpGet("dashboard/summary")]
    public async Task<ActionResult<PortalDashboardSummaryResponse>> GetDashboardSummary(CancellationToken cancellationToken)
    {
        var settings = await LoadSettingsAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var openWorkOrders = await OpenWorkOrders().CountAsync(cancellationToken);
        var overdueWorkOrders = await OpenWorkOrders().CountAsync(x => x.DueDate.HasValue && x.DueDate < now, cancellationToken);
        var assetCount = settings.ShowAssetRegister
            ? await dbContext.Assets.CountAsync(x => x.TenantId == TenantId && x.ClientId == ClientId, cancellationToken)
            : 0;
        var upcomingPm = await dbContext.PreventiveMaintenancePlans
            .Include(x => x.Asset)
            .CountAsync(x => x.TenantId == TenantId && x.Asset != null && x.Asset.ClientId == ClientId && x.Status == "Active" && x.NextPmDate.HasValue && x.NextPmDate < now.AddDays(30), cancellationToken);
        var outstandingBalances = settings.ShowInvoices
            ? await dbContext.TenantInvoices
                .Where(x => x.TenantId == TenantId && x.ClientId == ClientId && x.Balance > 0 && x.Status != "Void")
                .GroupBy(x => x.Currency)
                .Select(g => new PortalCurrencyAmount(g.Key, g.Sum(x => x.Balance)))
                .ToListAsync(cancellationToken)
            : [];
        var pendingQuotations = await dbContext.TenantQuotations
            .CountAsync(x => x.TenantId == TenantId && x.ClientId == ClientId && x.Status == "Sent", cancellationToken);

        return Ok(new PortalDashboardSummaryResponse(
            openWorkOrders,
            overdueWorkOrders,
            assetCount,
            upcomingPm,
            outstandingBalances.OrderBy(x => x.Currency).ToList(),
            pendingQuotations,
            await GetTenantCurrencyAsync(cancellationToken)));
    }

    // ── Reports ──────────────────────────────────────────────────────────────

    [HttpGet("reports/summary")]
    public async Task<ActionResult<PortalReportSummaryResponse>> GetReportSummary(CancellationToken cancellationToken)
    {
        var settings = await LoadSettingsAsync(cancellationToken);
        var now = DateTime.UtcNow;

        var statusCounts = await dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId && x.ClientId == ClientId)
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        int CountOf(params string[] statuses) => statusCounts.Where(x => statuses.Contains(x.Status)).Sum(x => x.Count);

        // Every request lands in exactly one of open / completed / closed / cancelled, so the four add up to the
        // total. Overdue is the subset of open requests that are past due.
        var totalRequests = statusCounts.Sum(x => x.Count);
        var completed = CountOf(CompletedStatuses);
        var closed = CountOf(WorkOrderStatuses.Closed);
        var cancelled = CountOf(CancelledStatuses);
        var overdue = await OpenWorkOrders().CountAsync(x => x.DueDate.HasValue && x.DueDate < now, cancellationToken);

        var invoiceTotals = settings.ShowInvoices
            ? await dbContext.TenantInvoices
                .Where(x => x.TenantId == TenantId && x.ClientId == ClientId && x.Status != "Void")
                .GroupBy(x => x.Currency)
                .Select(g => new PortalInvoiceCurrencyTotals(g.Key, g.Sum(x => x.TotalAmount), g.Sum(x => x.AmountPaid), g.Sum(x => x.Balance)))
                .ToListAsync(cancellationToken)
            : [];

        return Ok(new PortalReportSummaryResponse(
            totalRequests,
            totalRequests - completed - closed - cancelled,
            completed,
            closed,
            cancelled,
            overdue,
            await GetTenantCurrencyAsync(cancellationToken),
            invoiceTotals.OrderBy(x => x.Currency).ToList()));
    }

    // ── Work Orders (Requests) ───────────────────────────────────────────────

    [HttpGet("work-orders")]
    public async Task<ActionResult<IReadOnlyCollection<PortalWorkOrderListItem>>> ListWorkOrders([FromQuery] string? status, CancellationToken cancellationToken)
    {
        var query = dbContext.WorkOrders
            .Include(x => x.Site)
            .Include(x => x.Asset)
            .Where(x => x.TenantId == TenantId && x.ClientId == ClientId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status.Trim());
        }

        var list = await query.OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
        return Ok(list.Select(MapWorkOrderListItem).ToList());
    }

    [HttpGet("work-orders/{id:guid}")]
    public async Task<ActionResult<PortalWorkOrderDetailResponse>> GetWorkOrder(Guid id, CancellationToken cancellationToken)
    {
        var workOrder = await dbContext.WorkOrders
            .Include(x => x.Site)
            .Include(x => x.Asset)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ClientId == ClientId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Request was not found.");

        return Ok(MapWorkOrderDetail(workOrder));
    }

    [HttpGet("work-orders/{id:guid}/notes")]
    public async Task<ActionResult<IReadOnlyCollection<PortalWorkOrderNoteResponse>>> GetWorkOrderNotes(Guid id, CancellationToken cancellationToken)
    {
        var workOrderExists = await dbContext.WorkOrders
            .AnyAsync(x => x.TenantId == TenantId && x.ClientId == ClientId && x.Id == id, cancellationToken);
        if (!workOrderExists)
        {
            throw new NotFoundException("Request was not found.");
        }

        var notes = await dbContext.WorkOrderNotes
            .Include(x => x.CreatedByUser)
            .Where(x => x.TenantId == TenantId && x.WorkOrderId == id && x.IsClientVisible)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(notes.Select(x => new PortalWorkOrderNoteResponse(
            x.Id, x.Content, x.CreatedByUser != null ? x.CreatedByUser.FullName : null, x.CreatedAt)).ToList());
    }

    [HttpPost("work-orders")]
    public async Task<ActionResult<PortalWorkOrderDetailResponse>> CreateWorkOrder([FromBody] PortalCreateWorkOrderRequest request, CancellationToken cancellationToken)
    {
        if (!(await LoadSettingsAsync(cancellationToken)).AllowRequestSubmission)
        {
            throw new ForbiddenException("Service requests cannot be submitted through this portal. Please contact your service provider.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new BusinessRuleException("A short title describing the issue is required.");
        }

        if (request.SiteId.HasValue)
        {
            var siteBelongsToClient = await dbContext.Sites.AnyAsync(x => x.TenantId == TenantId && x.ClientId == ClientId && x.Id == request.SiteId.Value, cancellationToken);
            if (!siteBelongsToClient)
            {
                throw new BusinessRuleException("Site was not found for this account.");
            }
        }

        if (request.AssetId.HasValue)
        {
            var assetBelongsToClient = await dbContext.Assets.AnyAsync(x => x.TenantId == TenantId && x.ClientId == ClientId && x.Id == request.AssetId.Value, cancellationToken);
            if (!assetBelongsToClient)
            {
                throw new BusinessRuleException("Asset was not found for this account.");
            }
        }

        var portalUser = await dbContext.ClientPortalUsers.SingleOrDefaultAsync(x => x.Id == portalContext.GetRequiredPortalUserId(), cancellationToken);

        var workOrder = new WorkOrder
        {
            TenantId = TenantId,
            ClientId = ClientId,
            SiteId = request.SiteId,
            AssetId = request.AssetId,
            WorkOrderNumber = await documentNumberingService.GenerateAsync(TenantId, null, DocumentTypes.WorkOrder, cancellationToken),
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description)
                ? $"Submitted via client portal by {portalUser?.FullName ?? "a client contact"}."
                : $"{request.Description.Trim()}\n\n(Submitted via client portal by {portalUser?.FullName ?? "a client contact"}.)",
            Priority = WorkOrderPriorities.Normalize(request.Priority),
            Status = WorkOrderStatuses.New,
            WorkType = WorkOrderTypes.CorrectiveMaintenance,
        };

        dbContext.WorkOrders.Add(workOrder);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            null,
            "portal.work-order.requested",
            nameof(WorkOrder),
            workOrder.Id.ToString(),
            $"Client portal request '{workOrder.WorkOrderNumber}' submitted by '{portalUser?.FullName ?? portalContext.Email}'.",
            actorName: portalUser?.FullName,
            cancellationToken: cancellationToken);

        await dbContext.Entry(workOrder).Reference(x => x.Site).LoadAsync(cancellationToken);
        await dbContext.Entry(workOrder).Reference(x => x.Asset).LoadAsync(cancellationToken);

        return CreatedAtAction(nameof(GetWorkOrder), new { id = workOrder.Id }, MapWorkOrderDetail(workOrder));
    }

    // ── Assets ───────────────────────────────────────────────────────────────

    [HttpGet("assets")]
    public async Task<ActionResult<IReadOnlyCollection<PortalAssetListItem>>> ListAssets(CancellationToken cancellationToken)
    {
        await EnsureEnabledAsync(x => x.ShowAssetRegister, "The asset register is", cancellationToken);
        var assets = await dbContext.Assets
            .Include(x => x.Site)
            .Where(x => x.TenantId == TenantId && x.ClientId == ClientId)
            .OrderBy(x => x.AssetName)
            .ToListAsync(cancellationToken);

        return Ok(assets.Select(x => new PortalAssetListItem(
            x.Id,
            x.AssetName,
            x.AssetCode,
            x.AssetType,
            x.Site != null ? x.Site.SiteName : null,
            x.Manufacturer,
            x.Model,
            x.Status,
            x.WarrantyExpiryDate,
            x.NextPmDate)).ToList());
    }

    // ── Maintenance ──────────────────────────────────────────────────────────

    [HttpGet("maintenance")]
    public async Task<ActionResult<IReadOnlyCollection<PortalMaintenancePlanItem>>> ListMaintenancePlans(CancellationToken cancellationToken)
    {
        var plans = await dbContext.PreventiveMaintenancePlans
            .Include(x => x.Asset)
            .Where(x => x.TenantId == TenantId && x.Asset != null && x.Asset.ClientId == ClientId)
            .OrderBy(x => x.NextPmDate)
            .ToListAsync(cancellationToken);

        return Ok(plans.Select(x => new PortalMaintenancePlanItem(
            x.Id,
            x.Asset?.AssetName ?? "—",
            x.Frequency,
            x.Status,
            x.LastPmDate,
            x.NextPmDate)).ToList());
    }

    // ── Invoices ─────────────────────────────────────────────────────────────

    [HttpGet("invoices")]
    public async Task<ActionResult<IReadOnlyCollection<PortalInvoiceListItem>>> ListInvoices(CancellationToken cancellationToken)
    {
        await EnsureEnabledAsync(x => x.ShowInvoices, "Invoices are", cancellationToken);
        var invoices = await dbContext.TenantInvoices
            .Where(x => x.TenantId == TenantId && x.ClientId == ClientId)
            .OrderByDescending(x => x.InvoiceDate)
            .ToListAsync(cancellationToken);

        return Ok(invoices.Select(x => new PortalInvoiceListItem(x.Id, x.InvoiceNumber, x.Status, x.InvoiceDate, x.DueDate, x.TotalAmount, x.AmountPaid, x.Balance, x.Currency)).ToList());
    }

    [HttpGet("invoices/{id:guid}")]
    public async Task<ActionResult<PortalInvoiceDetailResponse>> GetInvoice(Guid id, CancellationToken cancellationToken)
    {
        await EnsureEnabledAsync(x => x.ShowInvoices, "Invoices are", cancellationToken);
        var invoice = await dbContext.TenantInvoices
            .Include(x => x.Lines)
            .Include(x => x.Payments)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ClientId == ClientId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Invoice was not found.");

        return Ok(new PortalInvoiceDetailResponse(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.Status,
            invoice.InvoiceDate,
            invoice.DueDate,
            invoice.Subtotal,
            invoice.TaxAmount,
            invoice.TotalAmount,
            invoice.AmountPaid,
            invoice.Balance,
            invoice.Currency,
            invoice.Notes,
            invoice.Lines.Select(l => new PortalInvoiceLineItem(l.Description, l.Quantity, l.Unit, l.UnitRate, l.LineTotal, l.TaxAmount, l.GrandTotal)).ToList(),
            invoice.Payments.Select(p => new PortalPaymentItem(p.Id, p.PaymentDate, p.Amount, p.Method, p.Reference)).ToList()));
    }

    // ── Quotations ───────────────────────────────────────────────────────────

    [HttpGet("quotations")]
    public async Task<ActionResult<IReadOnlyCollection<PortalQuotationListItem>>> ListQuotations(CancellationToken cancellationToken)
    {
        var quotes = await dbContext.TenantQuotations
            .Where(x => x.TenantId == TenantId && x.ClientId == ClientId)
            .OrderByDescending(x => x.QuoteDate)
            .ToListAsync(cancellationToken);

        return Ok(quotes.Select(x => new PortalQuotationListItem(x.Id, x.QuoteNumber, x.Status, x.QuoteDate, x.ValidUntil, x.TotalAmount, x.Currency)).ToList());
    }

    [HttpGet("quotations/{id:guid}")]
    public async Task<ActionResult<PortalQuotationDetailResponse>> GetQuotation(Guid id, CancellationToken cancellationToken)
    {
        var quote = await LoadQuotationAsync(id, cancellationToken);
        return Ok(MapQuotationDetail(quote));
    }

    [HttpPost("quotations/{id:guid}/accept")]
    public async Task<ActionResult<PortalQuotationDetailResponse>> AcceptQuotation(Guid id, CancellationToken cancellationToken)
    {
        await EnsureQuoteResponsesAllowedAsync(cancellationToken);
        var quote = await LoadQuotationAsync(id, cancellationToken);
        if (quote.Status != "Sent")
        {
            throw new BusinessRuleException("Only a sent quotation can be accepted.");
        }

        quote.Status = "Accepted";
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, null, "portal.quotation.accepted", nameof(TenantQuotation), quote.Id.ToString(), $"Quotation '{quote.QuoteNumber}' accepted via client portal.", actorName: portalContext.FullName, cancellationToken: cancellationToken);
        return Ok(MapQuotationDetail(quote));
    }

    [HttpPost("quotations/{id:guid}/reject")]
    public async Task<ActionResult<PortalQuotationDetailResponse>> RejectQuotation(Guid id, [FromBody] PortalRejectQuotationRequest? request, CancellationToken cancellationToken)
    {
        await EnsureQuoteResponsesAllowedAsync(cancellationToken);

        var reason = request?.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new BusinessRuleException("Please tell us why you are declining this quotation.");
        }

        if (reason.Length > MaxRejectionReasonLength)
        {
            throw new BusinessRuleException($"The reason must be {MaxRejectionReasonLength} characters or fewer.");
        }

        var quote = await LoadQuotationAsync(id, cancellationToken);
        if (quote.Status != "Sent")
        {
            throw new BusinessRuleException("Only a sent quotation can be rejected.");
        }

        quote.Status = "Rejected";
        quote.RejectionReason = reason;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, null, "portal.quotation.rejected", nameof(TenantQuotation), quote.Id.ToString(), $"Quotation '{quote.QuoteNumber}' rejected via client portal. Reason: {reason}", actorName: portalContext.FullName, cancellationToken: cancellationToken);
        return Ok(MapQuotationDetail(quote));
    }

    // ── Contracts ────────────────────────────────────────────────────────────

    [HttpGet("contracts")]
    public async Task<ActionResult<IReadOnlyCollection<PortalContractListItem>>> ListContracts(CancellationToken cancellationToken)
    {
        await EnsureEnabledAsync(x => x.ShowContracts, "Contracts are", cancellationToken);
        var contracts = await dbContext.Contracts
            .Where(x => x.TenantId == TenantId && x.ClientId == ClientId)
            .OrderByDescending(x => x.StartDate)
            .ToListAsync(cancellationToken);

        return Ok(contracts.Select(x => new PortalContractListItem(x.Id, x.ContractNumber, x.Name, x.Status.ToString(), x.ContractType.ToString(), x.StartDate, x.EndDate)).ToList());
    }

    // ── Profile ──────────────────────────────────────────────────────────────

    [HttpGet("profile")]
    public async Task<ActionResult<PortalProfileResponse>> GetProfile(CancellationToken cancellationToken)
    {
        var portalUserId = portalContext.GetRequiredPortalUserId();
        var portalUser = await dbContext.ClientPortalUsers
            .Include(x => x.Client)
            .SingleOrDefaultAsync(x => x.Id == portalUserId, cancellationToken)
            ?? throw new NotFoundException("Portal profile was not found.");

        return Ok(new PortalProfileResponse(portalUser.FullName, portalUser.Email, portalUser.Phone, portalUser.Client?.ClientName ?? "—", portalUser.LastLoginAt));
    }

    [HttpPut("profile")]
    public async Task<ActionResult<PortalProfileResponse>> UpdateProfile([FromBody] PortalUpdateProfileRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new BusinessRuleException("Full name is required.");
        }

        var portalUserId = portalContext.GetRequiredPortalUserId();
        var portalUser = await dbContext.ClientPortalUsers
            .Include(x => x.Client)
            .SingleOrDefaultAsync(x => x.Id == portalUserId, cancellationToken)
            ?? throw new NotFoundException("Portal profile was not found.");

        portalUser.FullName = request.FullName.Trim();
        portalUser.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            null,
            "portal.profile.updated",
            nameof(ClientPortalUser),
            portalUser.Id.ToString(),
            $"Client portal contact '{portalUser.FullName}' updated their profile.",
            actorName: portalUser.FullName,
            cancellationToken: cancellationToken);

        return Ok(new PortalProfileResponse(portalUser.FullName, portalUser.Email, portalUser.Phone, portalUser.Client?.ClientName ?? "—", portalUser.LastLoginAt));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private const int MaxRejectionReasonLength = 1000;

    private static readonly string[] CompletedStatuses = [WorkOrderStatuses.Completed, WorkOrderStatuses.ClientAccepted];
    // A rejected request never went ahead, so the client sees it with the cancelled ones.
    private static readonly string[] CancelledStatuses = [WorkOrderStatuses.Cancelled, WorkOrderStatuses.Rejected];
    private static readonly string[] NotOpenStatuses = [.. CompletedStatuses, WorkOrderStatuses.Closed, .. CancelledStatuses];

    /// <summary>Requests still being worked on: not completed, closed, cancelled or rejected.</summary>
    private IQueryable<WorkOrder> OpenWorkOrders() =>
        dbContext.WorkOrders.Where(x => x.TenantId == TenantId && x.ClientId == ClientId && !NotOpenStatuses.Contains(x.Status));

    private Task<PortalModuleSettings> LoadSettingsAsync(CancellationToken cancellationToken) =>
        PortalModuleSettings.LoadAsync(dbContext, TenantId, cancellationToken);

    private async Task EnsureEnabledAsync(Func<PortalModuleSettings, bool> isEnabled, string featureIs, CancellationToken cancellationToken)
    {
        if (!isEnabled(await LoadSettingsAsync(cancellationToken)))
        {
            throw new ForbiddenException($"{featureIs} not available in this portal.");
        }
    }

    private async Task EnsureQuoteResponsesAllowedAsync(CancellationToken cancellationToken)
    {
        if (!(await LoadSettingsAsync(cancellationToken)).AllowQuoteResponses)
        {
            throw new ForbiddenException("Quotations cannot be accepted or declined through this portal. Please contact your service provider.");
        }
    }

    private async Task<string> GetTenantCurrencyAsync(CancellationToken cancellationToken) =>
        await dbContext.Tenants.Where(x => x.Id == TenantId).Select(x => x.Currency).SingleOrDefaultAsync(cancellationToken) ?? "KES";

    private async Task<TenantQuotation> LoadQuotationAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.TenantQuotations
            .Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ClientId == ClientId && x.Id == id, cancellationToken)
        ?? throw new NotFoundException("Quotation was not found.");

    private static PortalWorkOrderListItem MapWorkOrderListItem(WorkOrder x) => new(
        x.Id, x.WorkOrderNumber, x.Title, x.Status, x.Priority, x.Site != null ? x.Site.SiteName : null, x.Asset != null ? x.Asset.AssetName : null, x.DueDate, x.CreatedAt);

    private static PortalWorkOrderDetailResponse MapWorkOrderDetail(WorkOrder x) => new(
        x.Id, x.WorkOrderNumber, x.Title, x.Description, x.Status, x.Priority, x.WorkType,
        x.Site != null ? x.Site.SiteName : null, x.Asset != null ? x.Asset.AssetName : null,
        x.DueDate, x.ScheduledStartAt, x.CompletedAt, x.WorkDoneNotes, x.CreatedAt);

    private static PortalQuotationDetailResponse MapQuotationDetail(TenantQuotation x) => new(
        x.Id, x.QuoteNumber, x.Status, x.QuoteDate, x.ValidUntil, x.Subject, x.Subtotal, x.TaxAmount, x.TotalAmount, x.Currency, x.RejectionReason,
        x.Lines.Select(l => new PortalQuotationLineItem(l.Description, l.Quantity, l.Unit, l.UnitRate, l.LineTotal, l.GrandTotal)).ToList());
}

// ── Response / request DTOs ─────────────────────────────────────────────────

public sealed record PortalSettingsResponse(bool AllowRequestSubmission, bool AllowQuoteResponses, bool ShowInvoices, bool ShowContracts, bool ShowAssetRegister, string? WelcomeMessage);

public sealed record PortalCurrencyAmount(string Currency, decimal Amount);
public sealed record PortalDashboardSummaryResponse(int OpenWorkOrders, int OverdueWorkOrders, int AssetCount, int UpcomingPmCount, IReadOnlyCollection<PortalCurrencyAmount> OutstandingBalances, int PendingQuotations, string Currency);

public sealed record PortalInvoiceCurrencyTotals(string Currency, decimal Invoiced, decimal Paid, decimal Outstanding);
public sealed record PortalReportSummaryResponse(int TotalRequests, int OpenRequests, int CompletedRequests, int ClosedRequests, int CancelledRequests, int OverdueRequests, string Currency, IReadOnlyCollection<PortalInvoiceCurrencyTotals> InvoiceTotals);

public sealed record PortalWorkOrderListItem(Guid Id, string WorkOrderNumber, string Title, string Status, string Priority, string? SiteName, string? AssetName, DateTime? DueDate, DateTime CreatedAt);
public sealed record PortalWorkOrderDetailResponse(Guid Id, string WorkOrderNumber, string Title, string? Description, string Status, string Priority, string WorkType, string? SiteName, string? AssetName, DateTime? DueDate, DateTime? ScheduledStartAt, DateTime? CompletedAt, string? WorkDoneNotes, DateTime CreatedAt);
public sealed record PortalCreateWorkOrderRequest(string Title, string? Description, string? Priority, Guid? SiteId, Guid? AssetId);
public sealed record PortalWorkOrderNoteResponse(Guid Id, string Content, string? CreatedByUserName, DateTime CreatedAt);

public sealed record PortalAssetListItem(Guid Id, string AssetName, string AssetCode, string? AssetType, string? SiteName, string? Manufacturer, string? Model, string Status, DateTime? WarrantyExpiryDate, DateTime? NextPmDate);

public sealed record PortalMaintenancePlanItem(Guid Id, string AssetName, string Frequency, string Status, DateTime? LastPmDate, DateTime? NextPmDate);

public sealed record PortalInvoiceListItem(Guid Id, string InvoiceNumber, string Status, DateTime InvoiceDate, DateTime DueDate, decimal TotalAmount, decimal AmountPaid, decimal Balance, string Currency);
public sealed record PortalInvoiceLineItem(string Description, decimal Quantity, string Unit, decimal UnitRate, decimal LineTotal, decimal TaxAmount, decimal GrandTotal);
public sealed record PortalPaymentItem(Guid Id, DateTime PaymentDate, decimal Amount, string PaymentMethod, string? ReferenceNumber);
public sealed record PortalInvoiceDetailResponse(Guid Id, string InvoiceNumber, string Status, DateTime InvoiceDate, DateTime DueDate, decimal Subtotal, decimal TaxAmount, decimal TotalAmount, decimal AmountPaid, decimal Balance, string Currency, string? Notes, IReadOnlyCollection<PortalInvoiceLineItem> Lines, IReadOnlyCollection<PortalPaymentItem> Payments);

public sealed record PortalQuotationListItem(Guid Id, string QuoteNumber, string Status, DateTime QuoteDate, DateTime ValidUntil, decimal TotalAmount, string Currency);
public sealed record PortalQuotationLineItem(string Description, decimal Quantity, string Unit, decimal UnitRate, decimal LineTotal, decimal GrandTotal);
public sealed record PortalQuotationDetailResponse(Guid Id, string QuoteNumber, string Status, DateTime QuoteDate, DateTime ValidUntil, string? Subject, decimal Subtotal, decimal TaxAmount, decimal TotalAmount, string Currency, string? RejectionReason, IReadOnlyCollection<PortalQuotationLineItem> Lines);
public sealed record PortalRejectQuotationRequest(string? Reason);

public sealed record PortalContractListItem(Guid Id, string ContractNumber, string Name, string Status, string ContractType, DateTime StartDate, DateTime EndDate);

public sealed record PortalProfileResponse(string FullName, string Email, string? Phone, string ClientName, DateTime? LastLoginAt);
public sealed record PortalUpdateProfileRequest(string FullName, string? Phone);
