using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/finance")]
public sealed class TenantFinanceController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IAuditLogService auditLogService,
    IUserAccessService userAccessService,
    IPdfRenderer pdfRenderer,
    IEmailSender emailSender,
    IEmailOutboxService emailOutbox,
    IFinancialRecipientResolver financialRecipientResolver,
    IWebHostEnvironment environment) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet("overview")]
    public async Task<ActionResult<TenantFinanceOverviewResponse>> GetOverview(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceView);

        var rates = await dbContext.ClientPreferentialRates.Where(x => x.TenantId == TenantId).ToListAsync(cancellationToken);
        var approvalRules = await dbContext.FinanceApprovalRules.Where(x => x.TenantId == TenantId && x.IsActive).ToListAsync(cancellationToken);
        var materialIssues = await dbContext.MaterialIssues.Where(x => x.TenantId == TenantId).ToListAsync(cancellationToken);

        return Ok(new TenantFinanceOverviewResponse(
            rates.Count,
            rates.Count(x => string.Equals(x.Status, "Active", StringComparison.OrdinalIgnoreCase)),
            approvalRules.Count,
            rates.Count(x => string.Equals(x.Status, "PendingApproval", StringComparison.OrdinalIgnoreCase)),
            materialIssues.Count(x => string.Equals(x.IssueStatus, "pending_approval", StringComparison.OrdinalIgnoreCase))));
    }

    [HttpGet("summary")]
    public Task<ActionResult<TenantFinanceOverviewResponse>> GetSummary(CancellationToken cancellationToken) =>
        GetOverview(cancellationToken);

    // ── Preferential Rates ───────────────────────────────────────────────────

    [HttpGet("preferential-rates")]
    public async Task<ActionResult<IReadOnlyCollection<PreferentialRateResponse>>> ListPreferentialRates(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceViewRates);
        var rates = await dbContext.ClientPreferentialRates
            .Include(x => x.Client)
            .Include(x => x.CreatedByUser)
            .Include(x => x.ApprovedByUser)
            .Where(x => x.TenantId == TenantId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return Ok(rates.Select(MapRate).ToList());
    }

    [HttpPost("preferential-rates")]
    public async Task<ActionResult<PreferentialRateResponse>> CreatePreferentialRate([FromBody] UpsertPreferentialRateRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageRates);
        var client = await dbContext.Clients.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == request.ClientId, cancellationToken)
            ?? throw new BusinessRuleException("Client was not found for this tenant.");

        var approvalRule = await FindApplicableApprovalRuleAsync("preferential_rate", request.PreferentialRate, request.Currency, cancellationToken);
        var requiresApproval = request.RequiresApproval || approvalRule is not null;
        var rate = new ClientPreferentialRate
        {
            TenantId = TenantId,
            ClientId = client.Id,
            RateName = request.RateName.Trim(),
            RateType = request.RateType.Trim(),
            ItemType = NormalizeItemType(request.ItemType),
            Unit = request.Unit.Trim(),
            DefaultRate = request.DefaultRate,
            PreferentialRate = request.PreferentialRate,
            Currency = NormalizeCurrency(request.Currency),
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Status = requiresApproval ? "PendingApproval" : NormalizeRateStatus(request.Status),
            RequiresApproval = requiresApproval,
            CreatedByUserId = UserId
        };

        dbContext.ClientPreferentialRates.Add(rate);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "finance.preferential_rate.created", nameof(ClientPreferentialRate), rate.Id.ToString(), $"Created preferential rate '{rate.RateName}'.", cancellationToken);

        await dbContext.Entry(rate).Reference(x => x.Client).LoadAsync(cancellationToken);
        await dbContext.Entry(rate).Reference(x => x.CreatedByUser).LoadAsync(cancellationToken);
        return CreatedAtAction(nameof(ListPreferentialRates), new { id = rate.Id }, MapRate(rate));
    }

    [HttpPut("preferential-rates/{id:guid}")]
    public async Task<ActionResult<PreferentialRateResponse>> UpdatePreferentialRate(Guid id, [FromBody] UpsertPreferentialRateRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageRates);
        var rate = await dbContext.ClientPreferentialRates.Include(x => x.Client).Include(x => x.CreatedByUser).Include(x => x.ApprovedByUser)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Preferential rate was not found.");
        var approvalRule = await FindApplicableApprovalRuleAsync("preferential_rate", request.PreferentialRate, request.Currency, cancellationToken);
        rate.ClientId = request.ClientId;
        rate.RateName = request.RateName.Trim();
        rate.RateType = request.RateType.Trim();
        rate.ItemType = NormalizeItemType(request.ItemType);
        rate.Unit = request.Unit.Trim();
        rate.DefaultRate = request.DefaultRate;
        rate.PreferentialRate = request.PreferentialRate;
        rate.Currency = NormalizeCurrency(request.Currency);
        rate.EffectiveFrom = request.EffectiveFrom;
        rate.EffectiveTo = request.EffectiveTo;
        rate.RequiresApproval = request.RequiresApproval || approvalRule is not null;
        rate.Status = rate.RequiresApproval ? "PendingApproval" : NormalizeRateStatus(request.Status);
        rate.ApprovedAt = null;
        rate.ApprovedByUserId = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "finance.preferential_rate.updated", nameof(ClientPreferentialRate), rate.Id.ToString(), $"Updated preferential rate '{rate.RateName}'.", cancellationToken);
        return Ok(MapRate(rate));
    }

    [HttpPost("preferential-rates/{id:guid}/approve")]
    public async Task<ActionResult<PreferentialRateResponse>> ApprovePreferentialRate(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageApprovals);
        var rate = await dbContext.ClientPreferentialRates.Include(x => x.Client).Include(x => x.CreatedByUser).Include(x => x.ApprovedByUser)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Preferential rate was not found.");
        rate.Status = "Active";
        rate.ApprovedByUserId = UserId;
        rate.ApprovedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "finance.preferential_rate.approved", nameof(ClientPreferentialRate), rate.Id.ToString(), $"Approved preferential rate '{rate.RateName}'.", cancellationToken);
        return Ok(MapRate(rate));
    }

    // ── Approval Rules ───────────────────────────────────────────────────────

    [HttpGet("approvals")]
    public async Task<ActionResult<IReadOnlyCollection<FinanceApprovalRuleResponse>>> ListApprovalRules(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceViewApprovals);
        var rules = await dbContext.FinanceApprovalRules
            .Include(x => x.RequiredApproverUser)
            .Include(x => x.RequiredAssignmentGroup)
            .Where(x => x.TenantId == TenantId)
            .OrderBy(x => x.ApprovalArea)
            .ThenBy(x => x.Sequence)
            .ToListAsync(cancellationToken);
        return Ok(rules.Select(MapRule).ToList());
    }

    [HttpGet("approval-rules")]
    public Task<ActionResult<IReadOnlyCollection<FinanceApprovalRuleResponse>>> ListApprovalRulesAlias(CancellationToken cancellationToken) =>
        ListApprovalRules(cancellationToken);

    [HttpPost("approvals")]
    public async Task<ActionResult<FinanceApprovalRuleResponse>> CreateApprovalRule([FromBody] UpsertFinanceApprovalRuleRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageApprovals);
        var entity = new FinanceApprovalRule
        {
            TenantId = TenantId,
            ApprovalArea = NormalizeApprovalArea(request.ApprovalArea),
            ThresholdAmount = request.ThresholdAmount,
            Currency = NormalizeCurrency(request.Currency),
            RequiredRole = request.RequiredRole.Trim(),
            RequiredApproverUserId = request.RequiredApproverUserId,
            RequiredAssignmentGroupId = request.RequiredAssignmentGroupId,
            Sequence = request.Sequence,
            IsActive = request.IsActive
        };
        dbContext.FinanceApprovalRules.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await dbContext.Entry(entity).Reference(x => x.RequiredApproverUser).LoadAsync(cancellationToken);
        await dbContext.Entry(entity).Reference(x => x.RequiredAssignmentGroup).LoadAsync(cancellationToken);
        return CreatedAtAction(nameof(ListApprovalRules), new { id = entity.Id }, MapRule(entity));
    }

    [HttpPost("approval-rules")]
    public Task<ActionResult<FinanceApprovalRuleResponse>> CreateApprovalRuleAlias([FromBody] UpsertFinanceApprovalRuleRequest request, CancellationToken cancellationToken) =>
        CreateApprovalRule(request, cancellationToken);

    [HttpPut("approvals/{id:guid}")]
    public async Task<ActionResult<FinanceApprovalRuleResponse>> UpdateApprovalRule(Guid id, [FromBody] UpsertFinanceApprovalRuleRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageApprovals);
        var entity = await dbContext.FinanceApprovalRules.Include(x => x.RequiredApproverUser).Include(x => x.RequiredAssignmentGroup)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Approval rule was not found.");
        entity.ApprovalArea = NormalizeApprovalArea(request.ApprovalArea);
        entity.ThresholdAmount = request.ThresholdAmount;
        entity.Currency = NormalizeCurrency(request.Currency);
        entity.RequiredRole = request.RequiredRole.Trim();
        entity.RequiredApproverUserId = request.RequiredApproverUserId;
        entity.RequiredAssignmentGroupId = request.RequiredAssignmentGroupId;
        entity.Sequence = request.Sequence;
        entity.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapRule(entity));
    }

    [HttpPut("approval-rules/{id:guid}")]
    public Task<ActionResult<FinanceApprovalRuleResponse>> UpdateApprovalRuleAlias(Guid id, [FromBody] UpsertFinanceApprovalRuleRequest request, CancellationToken cancellationToken) =>
        UpdateApprovalRule(id, request, cancellationToken);

    // ── Rate Cards ───────────────────────────────────────────────────────────

    [HttpGet("rate-cards")]
    public async Task<ActionResult<IReadOnlyCollection<RateCardResponse>>> ListRateCards(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceViewRates);
        var cards = await dbContext.RateCards
            .Where(x => x.TenantId == TenantId)
            .OrderBy(x => x.Category)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return Ok(cards.Select(MapRateCard).ToList());
    }

    [HttpPost("rate-cards")]
    public async Task<ActionResult<RateCardResponse>> CreateRateCard([FromBody] UpsertRateCardRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageRates);
        var card = new RateCard
        {
            TenantId = TenantId,
            Name = request.Name.Trim(),
            Category = NormalizeRateCardCategory(request.Category),
            Description = request.Description?.Trim(),
            UnitRate = request.UnitRate,
            Unit = NormalizeRateCardUnit(request.Unit),
            SkillLevel = string.IsNullOrWhiteSpace(request.SkillLevel) ? null : request.SkillLevel.Trim(),
            TimeType = NormalizeRateCardTimeType(request.TimeType),
            IsDefault = request.IsDefault,
            TaxRate = request.TaxRate,
            IsActive = true,
        };
        dbContext.RateCards.Add(card);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "finance.rate_card.created", nameof(RateCard), card.Id.ToString(), $"Created rate card '{card.Name}'.", cancellationToken);
        return CreatedAtAction(nameof(ListRateCards), new { id = card.Id }, MapRateCard(card));
    }

    [HttpPut("rate-cards/{id:guid}")]
    public async Task<ActionResult<RateCardResponse>> UpdateRateCard(Guid id, [FromBody] UpsertRateCardRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageRates);
        var card = await dbContext.RateCards.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Rate card was not found.");
        card.Name = request.Name.Trim();
        card.Category = NormalizeRateCardCategory(request.Category);
        card.Description = request.Description?.Trim();
        card.UnitRate = request.UnitRate;
        card.Unit = NormalizeRateCardUnit(request.Unit);
        card.SkillLevel = string.IsNullOrWhiteSpace(request.SkillLevel) ? null : request.SkillLevel.Trim();
        card.TimeType = NormalizeRateCardTimeType(request.TimeType);
        card.IsDefault = request.IsDefault;
        card.TaxRate = request.TaxRate;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "finance.rate_card.updated", nameof(RateCard), card.Id.ToString(), $"Updated rate card '{card.Name}'.", cancellationToken);
        return Ok(MapRateCard(card));
    }

    [HttpDelete("rate-cards/{id:guid}")]
    public async Task<IActionResult> DeactivateRateCard(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageRates);
        var card = await dbContext.RateCards.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Rate card was not found.");
        card.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "finance.rate_card.deactivated", nameof(RateCard), card.Id.ToString(), $"Deactivated rate card '{card.Name}'.", cancellationToken);
        return NoContent();
    }

    [HttpGet("rate-cards/lookup")]
    public async Task<ActionResult<RateCardResponse?>> LookupRateCard(
        [FromQuery] string? category,
        [FromQuery] string? skillLevel,
        [FromQuery] string? timeType,
        CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceViewRates);
        var normalizedCategory = string.IsNullOrWhiteSpace(category) ? null : NormalizeRateCardCategory(category);
        var normalizedTimeType = string.IsNullOrWhiteSpace(timeType) ? "Standard" : NormalizeRateCardTimeType(timeType);

        var query = dbContext.RateCards.Where(x => x.TenantId == TenantId && x.IsActive);
        if (normalizedCategory is not null) query = query.Where(x => x.Category == normalizedCategory);
        query = query.Where(x => x.TimeType == normalizedTimeType);

        var candidates = await query.ToListAsync(cancellationToken);
        var card = candidates.FirstOrDefault(x => x.SkillLevel == skillLevel)
            ?? candidates.FirstOrDefault(x => x.SkillLevel == null)
            ?? candidates.FirstOrDefault(x => x.IsDefault)
            ?? candidates.FirstOrDefault();

        return Ok(card is null ? null : MapRateCard(card));
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task<FinanceApprovalRule?> FindApplicableApprovalRuleAsync(string area, decimal amount, string currency, CancellationToken cancellationToken) =>
        await dbContext.FinanceApprovalRules
            .Where(x => x.TenantId == TenantId && x.IsActive && x.ApprovalArea == area && x.Currency == NormalizeCurrency(currency) && amount >= x.ThresholdAmount)
            .OrderBy(x => x.Sequence)
            .FirstOrDefaultAsync(cancellationToken);

    private static PreferentialRateResponse MapRate(ClientPreferentialRate rate) =>
        new(rate.Id, rate.TenantId, rate.ClientId, rate.Client?.ClientName, rate.RateName, rate.RateType, rate.ItemType, rate.Unit, rate.DefaultRate, rate.PreferentialRate, rate.Currency, rate.EffectiveFrom, rate.EffectiveTo, rate.Status, rate.RequiresApproval, rate.CreatedByUser?.FullName, rate.ApprovedByUser?.FullName, rate.ApprovedAt, rate.CreatedAt);

    private static FinanceApprovalRuleResponse MapRule(FinanceApprovalRule rule) =>
        new(rule.Id, rule.TenantId, rule.ApprovalArea, rule.ThresholdAmount, rule.Currency, rule.RequiredRole, rule.RequiredApproverUserId, rule.RequiredApproverUser?.FullName, rule.RequiredAssignmentGroupId, rule.RequiredAssignmentGroup?.Name, rule.Sequence, rule.IsActive, rule.CreatedAt);

    private static RateCardResponse MapRateCard(RateCard card) =>
        new(card.Id, card.TenantId, card.Name, card.Category, card.Description, card.UnitRate, card.Unit,
            card.SkillLevel, card.TimeType, card.IsDefault, card.TaxRate, card.IsActive, card.CreatedAt);

    private static string NormalizeItemType(string value) => value.Trim().ToLowerInvariant() switch
    {
        "labour" or "material" or "callout" or "transport" or "service" or "other" => value.Trim().ToLowerInvariant(),
        _ => throw new BusinessRuleException("Item type must be labour, material, callout, transport, service, or other.")
    };

    private static string NormalizeApprovalArea(string value) => value.Trim().ToLowerInvariant() switch
    {
        "preferential_rate" or "invoice" or "quotation" or "expense" or "material_issue" or "billable_work" => value.Trim().ToLowerInvariant(),
        _ => throw new BusinessRuleException("Invalid approval area.")
    };

    private static string NormalizeRateStatus(string value) => string.IsNullOrWhiteSpace(value) ? "Active" : value.Trim() switch
    {
        "Active" or "Inactive" or "PendingApproval" => value.Trim(),
        _ => throw new BusinessRuleException("Rate status must be Active, Inactive, or PendingApproval.")
    };

    private static string NormalizeCurrency(string value) => string.IsNullOrWhiteSpace(value) ? "KES" : value.Trim().ToUpperInvariant();

    private static string NormalizeRateCardCategory(string value) => value.Trim() switch
    {
        "Labour" or "Callout" or "Service" or "Transport" or "Material" or "Other" => value.Trim(),
        _ => throw new BusinessRuleException("Category must be Labour, Callout, Service, Transport, Material, or Other.")
    };

    private static string NormalizeRateCardUnit(string value) => value.Trim() switch
    {
        "hour" or "visit" or "km" or "unit" or "day" or "job" => value.Trim(),
        _ => throw new BusinessRuleException("Unit must be hour, visit, km, unit, day, or job.")
    };

    private static string NormalizeRateCardTimeType(string value) => value.Trim() switch
    {
        "Standard" or "Overtime" or "Emergency" or "Weekend" or "Holiday" => value.Trim(),
        _ => throw new BusinessRuleException("TimeType must be Standard, Overtime, Emergency, Weekend, or Holiday.")
    };

    // ── Finance Dashboard (updated) ──────────────────────────────────────────

    [HttpGet("dashboard")]
    public async Task<ActionResult<FinanceDashboardResponse>> GetDashboard(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceView);
        var startOfYear = new DateTime(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = DateTime.UtcNow;

        var invoices = await dbContext.TenantInvoices
            .Where(x => x.TenantId == TenantId)
            .ToListAsync(cancellationToken);

        var ytdInvoices = invoices.Where(x => x.InvoiceDate >= startOfYear).ToList();
        var totalInvoicedYtd = ytdInvoices.Sum(x => x.TotalAmount);
        var totalCollectedYtd = ytdInvoices.Sum(x => x.AmountPaid);
        var outstandingBalance = invoices.Where(x => x.Status != "Paid" && x.Status != "Void").Sum(x => x.Balance);
        var overdueBalance = invoices.Where(x => x.DueDate < now && x.Status != "Paid" && x.Status != "Void").Sum(x => x.Balance);

        var totalQuotations = await dbContext.TenantQuotations.CountAsync(x => x.TenantId == TenantId, cancellationToken);
        var convertedQuotations = await dbContext.TenantQuotations.CountAsync(x => x.TenantId == TenantId && x.Status == "Converted", cancellationToken);
        var conversionRate = totalQuotations > 0 ? Math.Round((decimal)convertedQuotations / totalQuotations * 100, 1) : 0;

        return Ok(new FinanceDashboardResponse(
            totalInvoicedYtd, totalCollectedYtd, outstandingBalance, overdueBalance,
            conversionRate, totalQuotations, convertedQuotations,
            ytdInvoices.FirstOrDefault()?.Currency ?? invoices.FirstOrDefault()?.Currency ?? "KES"));
    }

    // ── Revenue Report ───────────────────────────────────────────────────────

    [HttpGet("reports/revenue")]
    public async Task<ActionResult<IReadOnlyCollection<RevenueReportRow>>> GetRevenueReport(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? groupBy,
        CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceView);

        var query = dbContext.TenantInvoices
            .Include(x => x.Client)
            .Where(x => x.TenantId == TenantId && x.Status != "Void");

        var reportFrom = DateTimeNormalizationHelper.NormalizeNullableToUtc(from);
        var reportTo = DateTimeNormalizationHelper.NormalizeNullableToUtc(to);

        if (reportFrom.HasValue)
            query = query.Where(x => x.InvoiceDate >= reportFrom.Value);
        if (reportTo.HasValue)
            query = query.Where(x => x.InvoiceDate <= reportTo.Value);

        var invoices = await query.ToListAsync(cancellationToken);

        var rows = (groupBy?.ToLower() ?? "month") switch
        {
            "client" => invoices
                .GroupBy(x => x.Client?.ClientName ?? "Unknown")
                .Select(g => new RevenueReportRow(g.Key, g.Sum(x => x.TotalAmount), g.Sum(x => x.AmountPaid), g.Sum(x => x.Balance), g.Count()))
                .OrderByDescending(r => r.Invoiced)
                .ToList(),
            "month" => invoices
                .GroupBy(x => $"{x.InvoiceDate.Year}-{x.InvoiceDate.Month:D2}")
                .Select(g => new RevenueReportRow(g.Key, g.Sum(x => x.TotalAmount), g.Sum(x => x.AmountPaid), g.Sum(x => x.Balance), g.Count()))
                .OrderBy(r => r.Group)
                .ToList(),
            _ => invoices
                .GroupBy(x => x.Client?.ClientName ?? "Unknown")
                .Select(g => new RevenueReportRow(g.Key, g.Sum(x => x.TotalAmount), g.Sum(x => x.AmountPaid), g.Sum(x => x.Balance), g.Count()))
                .OrderByDescending(r => r.Invoiced)
                .ToList(),
        };

        return Ok(rows);
    }

    // ── Credit Summaries (batch) ─────────────────────────────────────────────

    [HttpGet("credit-summaries")]
    public async Task<ActionResult<IReadOnlyCollection<ClientCreditSummaryRow>>> GetCreditSummaries(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceView);

        var clients = await dbContext.Clients
            .Include(x => x.DefaultCreditTerm)
            .Where(x => x.TenantId == TenantId && x.IsActive)
            .OrderBy(x => x.ClientName)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var openInvoices = await dbContext.TenantInvoices
            .Where(x => x.TenantId == TenantId && x.Balance > 0 && x.Status != "Void" && x.Status != "Paid")
            .Select(x => new { x.ClientId, x.Balance, x.DueDate, x.InvoiceDate })
            .ToListAsync(cancellationToken);

        var byClient = openInvoices.GroupBy(x => x.ClientId).ToDictionary(g => g.Key, g => g.ToList());

        var rows = clients.Select(client =>
        {
            byClient.TryGetValue(client.Id, out var invoices);
            var overdueAmount = invoices?.Where(x => x.DueDate < now).Sum(x => x.Balance) ?? 0m;
            var oldestInvoice = invoices?.OrderBy(x => x.InvoiceDate).Select(x => (DateTime?)x.InvoiceDate).FirstOrDefault();
            var effectiveLimit = client.CreditLimit ?? client.DefaultCreditTerm?.CreditLimit;
            var availableCredit = effectiveLimit.HasValue ? effectiveLimit.Value - client.CurrentBalance : (decimal?)null;

            return new ClientCreditSummaryRow(
                client.Id,
                client.ClientName,
                client.CurrentBalance,
                effectiveLimit,
                availableCredit,
                overdueAmount,
                oldestInvoice?.ToString("yyyy-MM-dd"),
                client.DefaultCreditTerm?.Name);
        }).ToList();

        return Ok(rows);
    }

    // ── Aging Analysis ────────────────────────────────────────────────────────

    [HttpGet("reports/aging")]
    public async Task<ActionResult<IReadOnlyCollection<AgingRow>>> GetAgingReport(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceView);

        var invoices = await dbContext.TenantInvoices
            .Include(x => x.Client)
            .Where(x => x.TenantId == TenantId && x.Balance > 0 && x.Status != "Void" && x.Status != "Paid")
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;

        var rows = invoices
            .GroupBy(x => x.ClientId)
            .Select(g =>
            {
                var clientName = g.First().Client?.ClientName ?? "Unknown";
                var current = g.Where(x => x.DueDate >= now).Sum(x => x.Balance);
                var d1to30 = g.Where(x => x.DueDate < now && (now - x.DueDate).TotalDays <= 30).Sum(x => x.Balance);
                var d31to60 = g.Where(x => (now - x.DueDate).TotalDays > 30 && (now - x.DueDate).TotalDays <= 60).Sum(x => x.Balance);
                var d61to90 = g.Where(x => (now - x.DueDate).TotalDays > 60 && (now - x.DueDate).TotalDays <= 90).Sum(x => x.Balance);
                var d90plus = g.Where(x => (now - x.DueDate).TotalDays > 90).Sum(x => x.Balance);
                return new AgingRow(g.Key, clientName, current, d1to30, d31to60, d61to90, d90plus, current + d1to30 + d31to60 + d61to90 + d90plus);
            })
            .OrderByDescending(r => r.Total)
            .ToList();

        return Ok(rows);
    }

    // ── Client Statement ──────────────────────────────────────────────────────

    [HttpGet("reports/client-statement")]
    public async Task<ActionResult<ClientStatementResponse>> GetClientStatement(
        [FromQuery] Guid clientId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceView);

        var client = await dbContext.Clients
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == clientId, cancellationToken)
            ?? throw new NotFoundException("Client not found.");

        var statementFrom = DateTimeNormalizationHelper.NormalizeNullableToUtc(from);
        var statementTo = DateTimeNormalizationHelper.NormalizeNullableToUtc(to);

        var invQuery = dbContext.TenantInvoices.Where(x => x.TenantId == TenantId && x.ClientId == clientId);
        if (statementFrom.HasValue) invQuery = invQuery.Where(x => x.InvoiceDate >= statementFrom.Value);
        if (statementTo.HasValue) invQuery = invQuery.Where(x => x.InvoiceDate <= statementTo.Value);

        var invoices = await invQuery.OrderBy(x => x.InvoiceDate).ToListAsync(cancellationToken);

        var payQuery = dbContext.TenantPayments.Where(x => x.TenantId == TenantId && x.ClientId == clientId && x.Status == "Confirmed");
        if (statementFrom.HasValue) payQuery = payQuery.Where(x => x.PaymentDate >= statementFrom.Value);
        if (statementTo.HasValue) payQuery = payQuery.Where(x => x.PaymentDate <= statementTo.Value);

        var payments = await payQuery.OrderBy(x => x.PaymentDate).ToListAsync(cancellationToken);

        var totalInvoiced = invoices.Sum(x => x.TotalAmount);
        var totalPaid = payments.Sum(x => x.Amount);

        var invEntries = invoices.Select(x => new StatementEntry("invoice", x.InvoiceDate, x.InvoiceNumber, x.TotalAmount, 0, x.Status)).ToList();
        var payEntries = payments.Select(x => new StatementEntry("payment", x.PaymentDate, x.PaymentNumber, 0, x.Amount, x.Method)).ToList();

        var allEntries = invEntries.Concat(payEntries).OrderBy(x => x.Date).ToList();

        return Ok(new ClientStatementResponse(
            clientId, client.ClientName, statementFrom, statementTo,
            totalInvoiced, totalPaid, totalInvoiced - totalPaid,
            allEntries));
    }

    // ── Statement PDF Preview ────────────────────────────────────────────────

    [HttpGet("client-statement/preview-pdf")]
    public async Task<IActionResult> PreviewStatementPdf(
        [FromQuery] Guid clientId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] bool overdueOnly = false,
        CancellationToken cancellationToken = default)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceView);
        var (pdfModel, clientCode) = await BuildStatementPdfModelAsync(clientId, from, to, overdueOnly, cancellationToken);
        var bytes = pdfRenderer.RenderClientStatementPdf(pdfModel);
        var filename = $"statement_{clientCode}_{DateTime.UtcNow:yyyyMMdd}.pdf";
        return File(bytes, "application/pdf", filename);
    }

    // ── Statement Email ──────────────────────────────────────────────────────

    [HttpPost("client-statement/email")]
    public async Task<IActionResult> EmailClientStatement(
        [FromBody] EmailClientStatementRequest request,
        CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceView);

        var (pdfModel, clientCode) = await BuildStatementPdfModelAsync(
            request.ClientId, request.From, request.To, request.OverdueOnly, cancellationToken);

        var pdfBytes = pdfRenderer.RenderClientStatementPdf(pdfModel);
        var filename = $"statement_{clientCode}_{DateTime.UtcNow:yyyyMMdd}.pdf";

        var period = $"{pdfModel.PeriodFrom:d MMM yyyy} – {pdfModel.PeriodTo:d MMM yyyy}";
        var subject = $"Statement of Account — {pdfModel.CompanyName} — {period}";

        var outstanding = pdfModel.ClosingBalance;
        var currency = (await dbContext.Tenants.AsNoTracking().Where(x => x.Id == TenantId).Select(x => x.Currency).SingleOrDefaultAsync(cancellationToken)) ?? "KES";
        var defaultBody = $"<p>Please find your Statement of Account attached for the period {period}.</p>" +
                          $"<p><strong>Total Outstanding: {currency} {outstanding:N2}</strong></p>" +
                          $"<p>Please contact us if you have any queries.</p>";

        var htmlBody = string.IsNullOrWhiteSpace(request.Message)
            ? defaultBody
            : $"<p>{System.Net.WebUtility.HtmlEncode(request.Message)}</p>{defaultBody}";

        var recipients = await financialRecipientResolver.ResolveAsync(TenantId, request.ClientId, "Statement", cancellationToken);
        var additional = financialRecipientResolver.ValidateAdditionalRecipients(request.AdditionalRecipients);
        var allTo = recipients.To.Concat(additional).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (allTo.Length == 0)
            throw new BusinessRuleException("Statement cannot be sent because this client has no email address. Add one to the client and try again.");

        var cc = allTo.Skip(1).Concat(recipients.Cc).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        await emailOutbox.QueueEmailAsync(
            new QueueEmailRequest(
                TenantId, "finance.statement.issued", "statement.issued",
                allTo[0], pdfModel.CompanyName, null, null, null,
                subject, htmlBody, $"Please find your Statement of Account attached for {period}.", UserId,
                Attachments: [new EmailAttachment(filename, pdfBytes, "application/pdf")],
                CcAddresses: cc,
                DocumentType: "Statement",
                DocumentId: request.ClientId,
                TriggeredAutomatically: false),
            cancellationToken);

        await auditLogService.LogAsync(
            TenantId, UserId,
            "finance.client_statement.emailed",
            "Client", request.ClientId.ToString(),
            $"Queued statement of account delivery to {allTo.Length} recipient(s) for period {period}.",
            cancellationToken);

        return NoContent();
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task<(ClientStatementPdfModel model, string clientCode)> BuildStatementPdfModelAsync(
        Guid clientId,
        DateTime? from,
        DateTime? to,
        bool overdueOnly,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var normalizedFrom = DateTimeNormalizationHelper.NormalizeNullableToUtc(from);
        var normalizedTo = DateTimeNormalizationHelper.NormalizeNullableToUtc(to);
        var periodFrom = normalizedFrom ?? now.AddMonths(-6);
        var periodTo   = normalizedTo ?? now;

        var client = await dbContext.Clients
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == clientId, cancellationToken)
            ?? throw new NotFoundException("Client not found.");

        var tenant = await dbContext.Tenants
            .SingleOrDefaultAsync(x => x.Id == TenantId, cancellationToken);

        // Invoices: Sent, PartiallyPaid, Overdue (exclude Draft/Void/Paid)
        var activeStatuses = new[] { "Sent", "PartiallyPaid", "Overdue" };
        var invQuery = dbContext.TenantInvoices
            .Where(x => x.TenantId == TenantId && x.ClientId == clientId
                     && activeStatuses.Contains(x.Status));

        if (normalizedFrom.HasValue) invQuery = invQuery.Where(x => x.InvoiceDate >= periodFrom);
        if (normalizedTo.HasValue)   invQuery = invQuery.Where(x => x.InvoiceDate <= periodTo);
        if (overdueOnly)   invQuery = invQuery.Where(x => x.DueDate < now);

        var invoices = await invQuery.OrderBy(x => x.InvoiceDate).ToListAsync(cancellationToken);

        var payQuery = dbContext.TenantPayments
            .Where(x => x.TenantId == TenantId && x.ClientId == clientId && x.Status == "Confirmed");

        if (normalizedFrom.HasValue) payQuery = payQuery.Where(x => x.PaymentDate >= periodFrom);
        if (normalizedTo.HasValue)   payQuery = payQuery.Where(x => x.PaymentDate <= periodTo);

        var payments = await payQuery.OrderBy(x => x.PaymentDate).ToListAsync(cancellationToken);

        var totalInvoiced = invoices.Sum(x => x.TotalAmount);
        var totalPaid     = payments.Sum(x => x.Amount);
        var closing       = totalInvoiced - totalPaid;

        var invoiceRows = invoices.Select(inv => new StatementInvoiceRow(
            inv.InvoiceNumber,
            inv.InvoiceDate,
            inv.DueDate,
            inv.TotalAmount,
            inv.AmountPaid,
            inv.Balance,
            inv.Status,
            inv.DueDate < now && inv.Status != "Paid" && inv.Status != "Void"
        )).ToList();

        var paymentRows = payments.Select(pmt => new StatementPaymentRow(
            pmt.PaymentNumber,
            pmt.PaymentDate,
            pmt.Method,
            pmt.Amount
        )).ToList();

        var clientCode = client.ClientName
            .ToUpperInvariant()
            .Replace(" ", "_")[..Math.Min(12, client.ClientName.Length)];

        var templateConfig = PdfTemplateHelper.ResolveForDocument(tenant?.ReportTemplateConfigJson, tenant?.DocumentDesignsJson, "statement");

        var model = new ClientStatementPdfModel(
            client.ClientName,
            client.Email,
            client.Location,
            now,
            periodFrom,
            periodTo,
            OpeningBalance: 0,
            totalInvoiced,
            totalPaid,
            closing,
            invoiceRows,
            paymentRows,
            PdfTemplateHelper.CompanyName(tenant),
            PdfTemplateHelper.CompanyAddress(tenant),
            PdfTemplateHelper.CompanyPhone(tenant),
            tenant?.LogoUrl,
            templateConfig?.ReportPrimaryColor ?? tenant?.PrimaryColor ?? "#0F4C81",
            FontFamily: templateConfig?.FontFamily ?? "Helvetica",
            FooterText: PdfTemplateHelper.ResolveFooterText(templateConfig),
            WatermarkText: templateConfig?.WatermarkText,
            TemplateConfig: templateConfig,
            Company: PdfTemplateHelper.CompanyDetails(tenant, environment),
            ClientContact: PdfTemplateHelper.ClientContact(client),
            Currency: invoices.Select(x => x.Currency).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)) ?? tenant?.Currency
        );

        return (model, clientCode);
    }

}

public sealed record TenantFinanceOverviewResponse(
    int PreferentialRatesCount,
    int ActivePreferentialRatesCount,
    int ApprovalRulesCount,
    int PendingRateApprovalsCount,
    int PendingMaterialIssueApprovalsCount);

public sealed record UpsertPreferentialRateRequest(
    Guid ClientId,
    string RateName,
    string RateType,
    string ItemType,
    string Unit,
    decimal DefaultRate,
    decimal PreferentialRate,
    string Currency,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    string Status,
    bool RequiresApproval);

public sealed record PreferentialRateResponse(
    Guid Id,
    Guid TenantId,
    Guid ClientId,
    string? ClientName,
    string RateName,
    string RateType,
    string ItemType,
    string Unit,
    decimal DefaultRate,
    decimal PreferentialRate,
    string Currency,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    string Status,
    bool RequiresApproval,
    string? CreatedBy,
    string? ApprovedBy,
    DateTime? ApprovedAt,
    DateTime CreatedAt);

public sealed record UpsertFinanceApprovalRuleRequest(
    string ApprovalArea,
    decimal ThresholdAmount,
    string Currency,
    string RequiredRole,
    Guid? RequiredApproverUserId,
    Guid? RequiredAssignmentGroupId,
    int Sequence,
    bool IsActive);

public sealed record FinanceApprovalRuleResponse(
    Guid Id,
    Guid TenantId,
    string ApprovalArea,
    decimal ThresholdAmount,
    string Currency,
    string RequiredRole,
    Guid? RequiredApproverUserId,
    string? RequiredApproverUserName,
    Guid? RequiredAssignmentGroupId,
    string? RequiredAssignmentGroupName,
    int Sequence,
    bool IsActive,
    DateTime CreatedAt);

public sealed record UpsertRateCardRequest(
    string Name,
    string Category,
    string? Description,
    decimal UnitRate,
    string Unit,
    string? SkillLevel,
    string TimeType,
    bool IsDefault,
    decimal? TaxRate);

public sealed record RateCardResponse(
    Guid Id,
    Guid TenantId,
    string Name,
    string Category,
    string? Description,
    decimal UnitRate,
    string Unit,
    string? SkillLevel,
    string TimeType,
    bool IsDefault,
    decimal? TaxRate,
    bool IsActive,
    DateTime CreatedAt);

public sealed record EmailClientStatementRequest(
    Guid ClientId,
    DateTime? From,
    DateTime? To,
    string[]? AdditionalRecipients,
    string? Message,
    bool OverdueOnly = false);

public sealed record FinanceDashboardResponse(
    decimal TotalInvoicedYtd,
    decimal TotalCollectedYtd,
    decimal OutstandingBalance,
    decimal OverdueBalance,
    decimal QuoteConversionRate,
    int TotalQuotations,
    int ConvertedQuotations,
    string Currency);

public sealed record RevenueReportRow(
    string Group,
    decimal Invoiced,
    decimal Collected,
    decimal Outstanding,
    int Count);

public sealed record AgingRow(
    Guid ClientId,
    string ClientName,
    decimal Current,
    decimal Days1To30,
    decimal Days31To60,
    decimal Days61To90,
    decimal Days90Plus,
    decimal Total);

public sealed record ClientStatementResponse(
    Guid ClientId,
    string ClientName,
    DateTime? From,
    DateTime? To,
    decimal TotalInvoiced,
    decimal TotalPaid,
    decimal Balance,
    IReadOnlyCollection<StatementEntry> Entries);

public sealed record StatementEntry(
    string EntryType,
    DateTime Date,
    string Reference,
    decimal Debit,
    decimal Credit,
    string? Notes);

public sealed record ClientCreditSummaryRow(
    Guid ClientId,
    string ClientName,
    decimal CurrentBalance,
    decimal? CreditLimit,
    decimal? AvailableCredit,
    decimal OverdueAmount,
    string? OldestInvoiceDate,
    string? TermName);
