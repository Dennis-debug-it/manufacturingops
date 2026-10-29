using System.ComponentModel.DataAnnotations;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// The tenant's own sales pipeline: opportunities, the activity log against them, and the hand-off
/// of a won deal into operations (client, quotation, work order). Work-order creation itself lives
/// on <see cref="WorkOrdersController"/> (<c>POST /api/workorders/from-opportunity</c>) so it goes
/// through the same SLA, workflow and lifecycle path as every other work order.
/// </summary>
[ApiController]
[Authorize]
[Route("api/crm")]
public sealed partial class SalesCrmController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IDocumentNumberingService documentNumberingService,
    IAuditLogService auditLogService,
    IUserAccessService userAccessService,
    ILicenseGuardService licenseGuardService,
    IRolePermissionService rolePermissionService,
    ISalesOwnerResolver salesOwnerResolver,
    IEmailOutboxService? emailOutbox = null) : TenantAwareControllerBase(tenantContext)
{
    private const string FallbackCurrency = "KES";
    private Guid? _ownerScope;
    private bool _ownerScopeLoaded;

    /// <summary>The user this caller is limited to when the workspace shows people only their own deals; null when they see all.</summary>
    private async Task<Guid?> OwnerScopeAsync(CancellationToken cancellationToken)
    {
        if (!_ownerScopeLoaded)
        {
            _ownerScope = await SalesVisibility.OwnerScopeAsync(dbContext, tenantContext, TenantId, cancellationToken);
            _ownerScopeLoaded = true;
        }
        return _ownerScope;
    }

    private async Task EnsureVisibleAsync(Guid? ownerUserId, CancellationToken cancellationToken)
    {
        var scope = await OwnerScopeAsync(cancellationToken);
        if (scope.HasValue && ownerUserId != scope)
            throw new NotFoundException("Opportunity not found.");
    }

    private IReadOnlyList<SalesPipelineView>? _pipelines;

    /// <summary>The pipeline a deal is being put in: the one asked for (it must exist and be active), else the default.</summary>
    private static SalesPipelineView PipelineForWrite(IReadOnlyList<SalesPipelineView> pipelines, Guid? pipelineId)
    {
        if (!pipelineId.HasValue)
            return SalesPipelineCatalog.Default(pipelines);
        var pipeline = pipelines.FirstOrDefault(x => x.Id == pipelineId) ?? throw new NotFoundException("Pipeline not found.");
        if (!pipeline.IsActive)
            throw new BusinessRuleException($"The {pipeline.Name} pipeline is switched off.");
        return pipeline;
    }

    /// <summary>The workspace's pipelines (the default is created on first use). Load before making other changes.</summary>
    private async Task<IReadOnlyList<SalesPipelineView>> PipelinesAsync(CancellationToken cancellationToken) =>
        _pipelines ??= await SalesPipelineCatalog.LoadAsync(dbContext, TenantId, cancellationToken);

    /// <summary>Deals in this pipeline; deals with no pipeline are in the default one.</summary>
    private static IQueryable<SalesOpportunity> InPipeline(IQueryable<SalesOpportunity> query, SalesPipelineView pipeline) =>
        pipeline.IsDefault
            ? query.Where(x => x.PipelineId == pipeline.Id || x.PipelineId == null)
            : query.Where(x => x.PipelineId == pipeline.Id);

    // ── Pipeline ─────────────────────────────────────────────────────────────

    [HttpGet("opportunities")]
    public async Task<ActionResult<IReadOnlyCollection<SalesOpportunityListItem>>> ListOpportunities(
        [FromQuery] string? stage,
        [FromQuery] string? search,
        [FromQuery] Guid? ownerUserId,
        [FromQuery] Guid? clientId,
        CancellationToken cancellationToken,
        [FromQuery] Guid? pipelineId = null,
        [FromQuery] string? field = null,
        [FromQuery] string? fieldValue = null)
    {
        EnsureCanView();
        var pipelines = await PipelinesAsync(cancellationToken);

        var query = dbContext.SalesOpportunities
            .AsNoTracking()
            .Include(x => x.Client)
            .Where(x => x.TenantId == TenantId);
        if (pipelineId.HasValue)
            query = InPipeline(query, SalesPipelineCatalog.For(pipelines, pipelineId));

        query = await ApplyStageFilterAsync(query, stage, cancellationToken);
        if (ownerUserId.HasValue)
            query = query.Where(x => x.OwnerUserId == ownerUserId.Value);
        var scope = await OwnerScopeAsync(cancellationToken);
        if (scope.HasValue)
            query = query.Where(x => x.OwnerUserId == scope);
        if (clientId.HasValue)
            query = query.Where(x => x.ClientId == clientId.Value);
        query = await ApplyFieldFilterAsync(query, field, fieldValue, cancellationToken);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(x =>
                x.Title.ToLower().Contains(s) ||
                (x.CompanyName != null && x.CompanyName.ToLower().Contains(s)) ||
                (x.ContactName != null && x.ContactName.ToLower().Contains(s)) ||
                (x.Client != null && x.Client.ClientName.ToLower().Contains(s)));
        }

        var converter = await SalesCurrencyConverter.LoadAsync(dbContext, TenantId, cancellationToken);
        var rows = await query
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Select(x => new
            {
                Opportunity = x,
                WorkOrderCount = x.WorkOrders.Count,
                NextFollowUpAt = x.Activities
                    .Where(a => a.DueAt != null && a.CompletedAt == null)
                    .OrderBy(a => a.DueAt)
                    .Select(a => a.DueAt)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var stalledAfter = await SalesStalls.ThresholdDaysAsync(dbContext, TenantId, cancellationToken);
        var defaultPipelineId = SalesPipelineCatalog.Default(pipelines).Id;
        var scoring = await SalesScoring.LoadAsync(dbContext, TenantId, pipelines, cancellationToken);
        var now = DateTime.UtcNow;
        return Ok(rows.Select(x => MapListItem(x.Opportunity, x.WorkOrderCount, x.NextFollowUpAt, converter, stalledAfter) with
        {
            PipelineId = x.Opportunity.PipelineId ?? defaultPipelineId,
            SuggestedProbability = scoring.Score(x.Opportunity, now)?.Probability,
        }).ToList());
    }

    [HttpGet("summary")]
    public async Task<ActionResult<SalesPipelineSummary>> GetSummary(CancellationToken cancellationToken, [FromQuery] Guid? pipelineId = null)
    {
        EnsureCanView();
        var pipelines = await PipelinesAsync(cancellationToken);
        var pipeline = SalesPipelineCatalog.For(pipelines, pipelineId);

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var winRateWindowStart = now.AddDays(-90);
        var converter = await SalesCurrencyConverter.LoadAsync(dbContext, TenantId, cancellationToken);
        var scope = await OwnerScopeAsync(cancellationToken);

        var dealQuery = dbContext.SalesOpportunities.AsNoTracking().Where(x => x.TenantId == TenantId && (scope == null || x.OwnerUserId == scope));
        if (pipelineId.HasValue)
            dealQuery = InPipeline(dealQuery, pipeline);
        var opportunities = (await dealQuery
            .Select(x => new { x.Stage, x.EstimatedValue, x.Currency, x.Probability, x.WonAt, x.LostAt })
            .ToListAsync(cancellationToken))
            .Select(x => new { x.Stage, x.EstimatedValue, x.Currency, x.Probability, x.WonAt, x.LostAt, Value = converter.Convert(x.EstimatedValue, x.Currency) })
            .ToList();

        var open = opportunities.Where(x => !SalesStages.IsClosed(x.Stage)).ToList();
        var wonRecently = opportunities.Count(x => x.Stage == SalesStages.Won && x.WonAt >= winRateWindowStart);
        var lostRecently = opportunities.Count(x => x.Stage == SalesStages.Lost && x.LostAt >= winRateWindowStart);
        var wonThisMonth = opportunities.Where(x => x.Stage == SalesStages.Won && x.WonAt >= monthStart).ToList();

        // Closing a deal stops its reminders, so its leftover follow-ups are not overdue work either.
        var overdueFollowUps = await dbContext.SalesActivities
            .AsNoTracking()
            .CountAsync(x => x.TenantId == TenantId && x.DueAt != null && x.CompletedAt == null && x.DueAt < now
                && x.Opportunity != null && x.Opportunity.Stage != SalesStages.Won && x.Opportunity.Stage != SalesStages.Lost
                && (scope == null || x.Opportunity.OwnerUserId == scope), cancellationToken);

        var stalledAfter = await SalesStalls.ThresholdDaysAsync(dbContext, TenantId, cancellationToken);
        var stalledDeals = stalledAfter <= 0 ? 0 : await dbContext.SalesOpportunities.AsNoTracking()
            .CountAsync(x => x.TenantId == TenantId && x.Stage != SalesStages.Won && x.Stage != SalesStages.Lost
                && (scope == null || x.OwnerUserId == scope)
                && (x.LastActivityAt ?? x.CreatedAt) <= now.AddDays(-stalledAfter), cancellationToken);

        var weekAhead = now.AddDays(7);
        var openTenders = await dbContext.SalesTenders
            .AsNoTracking()
            .Where(x => x.TenantId == TenantId && SalesTenderStatuses.OpenStatuses.Contains(x.Status) && (scope == null || x.OwnerUserId == scope))
            .Select(x => x.SubmissionDeadline)
            .ToListAsync(cancellationToken);

        var stages = pipeline.Stages
            .Select(stage =>
            {
                var inStage = opportunities.Where(x => x.Stage == stage.Key).ToList();
                return new SalesStageSummary(stage.Key, inStage.Count, inStage.Sum(x => x.Value ?? 0));
            })
            .ToList();

        // Deals in a currency with no rate are left out of every total above and listed here, so the
        // page can say "excludes 2 USD deals — add a rate" instead of silently under-reporting.
        var unconverted = opportunities
            .Where(x => x.Value is null && !SalesStages.IsClosed(x.Stage))
            .GroupBy(x => SalesCurrencyConverter.Normalize(x.Currency))
            .Select(g => new SalesUnconvertedCurrency(g.Key, g.Count(), g.Sum(x => x.EstimatedValue)))
            .OrderBy(x => x.Currency)
            .ToList();

        return Ok(new SalesPipelineSummary(
            ReportingCurrency: converter.ReportingCurrency,
            OpenCount: open.Count,
            OpenValue: open.Sum(x => x.Value ?? 0),
            WeightedValue: Math.Round(open.Sum(x => (x.Value ?? 0) * x.Probability / 100m), 2),
            WonThisMonthCount: wonThisMonth.Count,
            WonThisMonthValue: wonThisMonth.Sum(x => x.Value ?? 0),
            WinRate: wonRecently + lostRecently == 0 ? null : Math.Round(wonRecently * 100m / (wonRecently + lostRecently), 1),
            OverdueFollowUps: overdueFollowUps,
            OpenTenders: openTenders.Count,
            TendersDueThisWeek: openTenders.Count(x => x >= now && x <= weekAhead),
            Stages: stages,
            UnconvertedCurrencies: unconverted,
            StalledDeals: stalledDeals,
            StalledAfterDays: stalledAfter));
    }

    // ── Owners & currency ────────────────────────────────────────────────────

    /// <summary>The people a deal or tender can be assigned to.</summary>
    [HttpGet("owners")]
    public async Task<ActionResult<IReadOnlyCollection<SalesOwnerOption>>> ListOwners(CancellationToken cancellationToken)
    {
        EnsureCanView();
        return Ok(await SalesCrmAccess.ListEligibleOwnersAsync(dbContext, rolePermissionService, TenantId, cancellationToken));
    }

    /// <summary>
    /// The reporting currency, the rate in use for every other currency, and the currencies open
    /// deals or tenders use that still have no rate.
    /// </summary>
    [HttpGet("settings")]
    public async Task<ActionResult<SalesSettingsResponse>> GetSettings(CancellationToken cancellationToken)
    {
        EnsureCanView();
        return Ok(await LoadSettingsResponseAsync(cancellationToken));
    }

    /// <summary>Workspace-wide sales settings; admin only, since deal visibility is an access rule.</summary>
    [HttpPut("settings")]
    public async Task<ActionResult<SalesSettingsResponse>> UpdateSettings([FromBody] UpdateSalesSettingsRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        await licenseGuardService.EnsureTenantCanMutateAsync(TenantId, cancellationToken);
        if (request.DefaultOwnerUserId.HasValue)
            await SalesCrmAccess.ResolveOwnerNameAsync(dbContext, rolePermissionService, TenantId, request.DefaultOwnerUserId.Value, cancellationToken);

        var settings = await dbContext.TenantSalesSettings.SingleOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken);
        if (settings is null)
        {
            settings = new TenantSalesSettings { TenantId = TenantId };
            dbContext.TenantSalesSettings.Add(settings);
        }
        var previousVisibility = settings.DealVisibility;
        settings.DefaultOwnerUserId = request.DefaultOwnerUserId;
        settings.DealVisibility = SalesVisibility.Normalize(request.DealVisibility);
        settings.CreateRenewalDeals = request.CreateRenewalDeals;
        if (request.StalledAfterDays is < 0 or > 365)
            throw new BusinessRuleException("Stalled after must be between 0 (off) and 365 days.");
        settings.StalledAfterDays = request.StalledAfterDays ?? settings.StalledAfterDays;
        settings.SendWeeklySummary = request.SendWeeklySummary ?? settings.SendWeeklySummary;
        if (request.LeadEmailAddress is not null)
        {
            var leadAddress = request.LeadEmailAddress.Trim().ToLowerInvariant();
            if (leadAddress.Length > 0 && !System.Net.Mail.MailAddress.TryCreate(leadAddress, out _))
                throw new BusinessRuleException($"'{request.LeadEmailAddress}' is not an email address.");
            settings.LeadEmailAddress = leadAddress.Length == 0 ? null : leadAddress;
        }
        if (request.WebFormEnabled.HasValue)
        {
            settings.WebFormEnabled = request.WebFormEnabled.Value;
            if (settings.WebFormEnabled)
                settings.WebFormKey ??= SalesLeadFormController.NewKey();
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "crm.settings.updated", nameof(TenantSalesSettings), settings.Id.ToString(),
            $"Sales settings updated: deal visibility {settings.DealVisibility}" + (previousVisibility != settings.DealVisibility ? $" (was {previousVisibility})" : "") + $", renewal deals {(settings.CreateRenewalDeals ? "on" : "off")}.",
            cancellationToken);
        return Ok(await LoadSettingsResponseAsync(cancellationToken));
    }

    private async Task<SalesSettingsResponse> LoadSettingsResponseAsync(CancellationToken cancellationToken)
    {
        var settings = await dbContext.TenantSalesSettings.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken)
            ?? new TenantSalesSettings { TenantId = TenantId };
        var ownerName = settings.DefaultOwnerUserId is null ? null : await ResolveUserNameAsync(settings.DefaultOwnerUserId.Value, cancellationToken);
        var intakeAddress = await dbContext.EmailIntakeSettings.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.IsEnabled)
            .Select(x => x.IntakeEmailAddress)
            .FirstOrDefaultAsync(cancellationToken);
        return new SalesSettingsResponse(settings.DefaultOwnerUserId, ownerName, SalesVisibility.Normalize(settings.DealVisibility), settings.CreateRenewalDeals, settings.StalledAfterDays, settings.SendWeeklySummary,
            settings.LeadEmailAddress, intakeAddress, settings.WebFormEnabled, settings.WebFormEnabled ? settings.WebFormKey : null);
    }

    [HttpGet("currency")]
    public async Task<ActionResult<SalesCurrencySettings>> GetCurrencySettings(CancellationToken cancellationToken)
    {
        EnsureCanView();
        var converter = await SalesCurrencyConverter.LoadAsync(dbContext, TenantId, cancellationToken);
        var used = (await dbContext.SalesOpportunities.AsNoTracking()
                .Where(x => x.TenantId == TenantId && x.Stage != SalesStages.Won && x.Stage != SalesStages.Lost)
                .Select(x => x.Currency).Distinct().ToListAsync(cancellationToken))
            .Concat(await dbContext.SalesTenders.AsNoTracking()
                .Where(x => x.TenantId == TenantId && SalesTenderStatuses.OpenStatuses.Contains(x.Status))
                .Select(x => x.Currency).Distinct().ToListAsync(cancellationToken))
            .Select(SalesCurrencyConverter.Normalize)
            .Distinct();

        return Ok(new SalesCurrencySettings(
            converter.ReportingCurrency,
            converter.Rates,
            used.Where(x => !converter.CanConvert(x)).OrderBy(x => x).ToList()));
    }

    /// <summary>
    /// Records today's rate for a currency: 1 <c>Currency</c> = <c>Rate</c> reporting-currency units.
    /// Stored in the tenant's exchange-rate history (Finance › Exchange Rates shows the same rows);
    /// earlier rates are kept, the newest one is used from now on.
    /// </summary>
    [HttpPost("exchange-rates")]
    public async Task<ActionResult<SalesCurrencySettings>> SetExchangeRate([FromBody] SetSalesExchangeRateRequest request, CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(cancellationToken);
        var converter = await SalesCurrencyConverter.LoadAsync(dbContext, TenantId, cancellationToken);
        var currency = SalesCurrencyConverter.Normalize(request.Currency);
        if (currency.Length != 3 || !currency.All(char.IsAsciiLetterUpper))
            throw new BusinessRuleException("Use a 3-letter currency code, e.g. USD.");
        if (currency == converter.ReportingCurrency)
            throw new BusinessRuleException($"{currency} is already the reporting currency.");
        if (request.Rate <= 0)
            throw new BusinessRuleException("Rate must be greater than zero.");

        var rate = new ExchangeRate
        {
            TenantId = TenantId,
            BaseCurrency = currency,
            QuoteCurrency = converter.ReportingCurrency,
            Rate = request.Rate,
            EffectiveFrom = DateTime.UtcNow,
            UpdatedByUserId = UserId,
            UpdatedByName = await ResolveUserNameAsync(UserId, cancellationToken) ?? "Unknown",
        };
        dbContext.ExchangeRates.Add(rate);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "finance.exchange_rate.set", nameof(ExchangeRate), rate.Id.ToString(), $"Set {currency}/{converter.ReportingCurrency} = {request.Rate} from the sales pipeline", cancellationToken);

        return await GetCurrencySettings(cancellationToken);
    }

    [HttpGet("opportunities/{id:guid}")]
    public async Task<ActionResult<SalesOpportunityDetailResponse>> GetOpportunity(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanView();
        return Ok(await LoadDetailAsync(id, cancellationToken));
    }

    [HttpPost("opportunities")]
    public async Task<ActionResult<SalesOpportunityDetailResponse>> CreateOpportunity(
        [FromBody] UpsertSalesOpportunityRequest request, CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(cancellationToken);
        var pipeline = PipelineForWrite(await PipelinesAsync(cancellationToken), request.PipelineId);

        var opportunity = new SalesOpportunity { TenantId = TenantId, Stage = pipeline.FirstOpen.Key, PipelineId = pipeline.Id };
        await ApplyAsync(opportunity, request, cancellationToken);
        await ProspectClients.LinkAsync(dbContext, opportunity, null, cancellationToken);
        await LinkDealFieldsContactAsync(opportunity, cancellationToken);
        if (!request.OwnerUserId.HasValue)
        {
            opportunity.OwnerUserId = UserId;
            opportunity.OwnerName = await ResolveUserNameAsync(UserId, cancellationToken);
        }
        var stage = request.Stage is null ? pipeline.FirstOpen : pipeline.Find(request.Stage)
            ?? throw new BusinessRuleException($"'{request.Stage}' is not a stage of the {pipeline.Name} pipeline.");
        if (stage.IsClosed)
            throw new BusinessRuleException("Create the opportunity first, then mark it won or lost.");
        SalesStageRules.EnsureCanEnter(opportunity, pipeline, stage,
            await SalesStageRules.LoadFactsAsync(dbContext, TenantId, opportunity.Id, cancellationToken));
        opportunity.Stage = stage.Key;
        opportunity.Probability = request.Probability ?? stage.Probability;

        dbContext.SalesOpportunities.Add(opportunity);
        await AddSystemActivityAsync(opportunity, "Opportunity created.", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "crm.opportunity.created", nameof(SalesOpportunity), opportunity.Id.ToString(), $"Created opportunity '{opportunity.Title}'.", cancellationToken);

        return CreatedAtAction(nameof(GetOpportunity), new { id = opportunity.Id }, await LoadDetailAsync(opportunity.Id, cancellationToken, checkVisibility: false));
    }

    [HttpPut("opportunities/{id:guid}")]
    public async Task<ActionResult<SalesOpportunityDetailResponse>> UpdateOpportunity(
        Guid id, [FromBody] UpsertSalesOpportunityRequest request, CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(cancellationToken);
        var opportunity = await LoadAsync(id, cancellationToken);
        var previousOwnerId = opportunity.OwnerUserId;
        var previousOwnerName = opportunity.OwnerName ?? "nobody";
        var previousClientId = opportunity.ClientId;
        var pipelines = await PipelinesAsync(cancellationToken);
        var currentPipeline = SalesPipelineCatalog.For(pipelines, opportunity.PipelineId);

        await ApplyAsync(opportunity, request, cancellationToken);
        if (request.PipelineId.HasValue && request.PipelineId != currentPipeline.Id)
        {
            // Moving pipelines keeps the stage where the new pipeline has it (Won and Lost always), else starts it at the first stage.
            var target = PipelineForWrite(pipelines, request.PipelineId);
            var stage = target.Find(opportunity.Stage) ?? target.FirstOpen;
            opportunity.PipelineId = target.Id;
            if (stage.Key != opportunity.Stage)
            {
                opportunity.Stage = stage.Key;
                opportunity.Probability = stage.Probability;
            }
            await AddSystemActivityAsync(opportunity, $"Moved from the {currentPipeline.Name} pipeline to {target.Name} ({stage.Name}).", cancellationToken);
        }
        await ProspectClients.LinkAsync(dbContext, opportunity, previousClientId, cancellationToken);
        await LinkDealFieldsContactAsync(opportunity, cancellationToken);
        if (opportunity.OwnerUserId != previousOwnerId)
            await AddSystemActivityAsync(opportunity, $"Owner changed from {previousOwnerName} to {opportunity.OwnerName}.", cancellationToken);
        if (request.Probability.HasValue)
            opportunity.Probability = request.Probability.Value;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await LoadDetailAsync(id, cancellationToken, checkVisibility: false));
    }

    [HttpPost("opportunities/{id:guid}/stage")]
    public async Task<ActionResult<SalesOpportunityDetailResponse>> ChangeStage(
        Guid id, [FromBody] ChangeSalesStageRequest request, CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(cancellationToken);
        var opportunity = await LoadAsync(id, cancellationToken);

        var pipeline = SalesPipelineCatalog.For(await PipelinesAsync(cancellationToken), opportunity.PipelineId);
        var target = pipeline.Find(request.Stage)
            ?? throw new BusinessRuleException($"'{request.Stage}' is not a stage of the {pipeline.Name} pipeline.");
        var stage = target.Key;
        if (stage == opportunity.Stage)
            return Ok(await LoadDetailAsync(id, cancellationToken));

        if (stage == SalesStages.Lost && string.IsNullOrWhiteSpace(request.LostReason))
            throw new BusinessRuleException("Give a reason when marking an opportunity as lost.");
        SalesStageRules.EnsureCanEnter(opportunity, pipeline, target,
            await SalesStageRules.LoadFactsAsync(dbContext, TenantId, opportunity.Id, cancellationToken));

        var previous = pipeline.Find(opportunity.Stage)?.Name ?? opportunity.Stage;
        opportunity.Stage = stage;
        opportunity.Probability = target.Probability;
        opportunity.WonAt = stage == SalesStages.Won ? DateTime.UtcNow : null;
        opportunity.LostAt = stage == SalesStages.Lost ? DateTime.UtcNow : null;
        opportunity.LostReason = stage == SalesStages.Lost ? request.LostReason!.Trim() : null;

        var note = stage == SalesStages.Lost
            ? $"Stage changed from {previous} to Lost: {opportunity.LostReason}"
            : $"Stage changed from {previous} to {target.Name}.";
        if (stage == SalesStages.Won && await ProspectClients.PromoteAsync(dbContext, TenantId, opportunity.ClientId, cancellationToken))
            note += " The prospect is now a client.";
        if (target.IsClosed && await SalesSequences.StopAsync(dbContext, TenantId, opportunity.Id, $"The deal was {stage.ToLowerInvariant()}.", DateTime.UtcNow, cancellationToken) > 0)
            note += " Its follow-up sequence stopped.";
        await AddSystemActivityAsync(opportunity, note, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "crm.opportunity.stage_changed", nameof(SalesOpportunity), opportunity.Id.ToString(), note, cancellationToken);

        return Ok(await LoadDetailAsync(id, cancellationToken));
    }

    [HttpPost("opportunities/{id:guid}/owner")]
    public async Task<ActionResult<SalesOpportunityDetailResponse>> ReassignOwner(
        Guid id, [FromBody] ReassignSalesOwnerRequest request, CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(cancellationToken);
        var opportunity = await LoadAsync(id, cancellationToken);
        if (opportunity.OwnerUserId == request.OwnerUserId)
            return Ok(await LoadDetailAsync(id, cancellationToken));

        var previous = opportunity.OwnerName ?? "nobody";
        opportunity.OwnerUserId = request.OwnerUserId;
        opportunity.OwnerName = await SalesCrmAccess.ResolveOwnerNameAsync(dbContext, rolePermissionService, TenantId, request.OwnerUserId, cancellationToken);
        var note = $"Owner changed from {previous} to {opportunity.OwnerName}.";
        await AddSystemActivityAsync(opportunity, note, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "crm.opportunity.owner_changed", nameof(SalesOpportunity), opportunity.Id.ToString(), note, cancellationToken);
        return Ok(await LoadDetailAsync(id, cancellationToken, checkVisibility: false));
    }

    [HttpDelete("opportunities/{id:guid}")]
    public async Task<IActionResult> DeleteOpportunity(Guid id, CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(cancellationToken);
        var opportunity = await LoadAsync(id, cancellationToken);

        var hasLinkedWork = await dbContext.WorkOrders.AnyAsync(x => x.TenantId == TenantId && x.SalesOpportunityId == id, cancellationToken)
            || await dbContext.TenantQuotations.AnyAsync(x => x.TenantId == TenantId && x.SalesOpportunityId == id, cancellationToken)
            || await dbContext.SalesTenders.AnyAsync(x => x.TenantId == TenantId && x.OpportunityId == id, cancellationToken);
        if (hasLinkedWork)
            throw new BusinessRuleException("This opportunity has quotations, work orders or an RFP/RFQ linked to it. Mark it lost instead of deleting it.");

        dbContext.SalesOpportunities.Remove(opportunity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "crm.opportunity.deleted", nameof(SalesOpportunity), id.ToString(), $"Deleted opportunity '{opportunity.Title}'.", cancellationToken);
        return NoContent();
    }

    // ── Activities ───────────────────────────────────────────────────────────

    [HttpPost("opportunities/{id:guid}/activities")]
    public async Task<ActionResult<SalesActivityResponse>> AddActivity(
        Guid id, [FromBody] CreateSalesActivityRequest request, CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(cancellationToken);
        var opportunity = await LoadAsync(id, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Summary))
            throw new BusinessRuleException("Activity summary is required.");

        var type = SalesActivityTypes.Normalize(request.Type);
        var activity = new SalesActivity
        {
            TenantId = TenantId,
            OpportunityId = opportunity.Id,
            Type = type,
            Summary = request.Summary.Trim(),
            OccurredAt = request.OccurredAt.HasValue ? DateTime.SpecifyKind(request.OccurredAt.Value, DateTimeKind.Utc) : DateTime.UtcNow,
            DueAt = request.DueAt.HasValue ? DateTime.SpecifyKind(request.DueAt.Value, DateTimeKind.Utc) : null,
            LoggedByUserId = UserId,
            LoggedByName = await ResolveUserNameAsync(UserId, cancellationToken),
        };
        if (request.Latitude.HasValue && request.Longitude.HasValue)
        {
            if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180)
                throw new BusinessRuleException("That location is not valid.");
            activity.Latitude = Math.Round(request.Latitude.Value, 6);
            activity.Longitude = Math.Round(request.Longitude.Value, 6);
            activity.LocationAccuracyMeters = request.AccuracyMeters is > 0 ? Math.Round(request.AccuracyMeters.Value, 1) : null;
        }

        dbContext.SalesActivities.Add(activity);
        opportunity.LastActivityAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapActivity(activity));
    }

    /// <summary>
    /// Every open follow-up across the pipeline, soonest first: the sales to-do list. Follow-ups on
    /// closed deals are left out, as they are by the reminders. <paramref name="ownerUserId"/> narrows
    /// it to the deals one person owns, which is who the reminders go to.
    /// </summary>
    [HttpGet("follow-ups")]
    public async Task<ActionResult<IReadOnlyCollection<SalesFollowUpItem>>> ListFollowUps(
        [FromQuery] Guid? ownerUserId, CancellationToken cancellationToken)
    {
        EnsureCanView();

        var query = dbContext.SalesActivities
            .AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.DueAt != null && x.CompletedAt == null
                && x.Opportunity != null && x.Opportunity.Stage != SalesStages.Won && x.Opportunity.Stage != SalesStages.Lost);
        if (ownerUserId.HasValue)
            query = query.Where(x => x.Opportunity!.OwnerUserId == ownerUserId.Value);
        var scope = await OwnerScopeAsync(cancellationToken);
        if (scope.HasValue)
            query = query.Where(x => x.Opportunity!.OwnerUserId == scope);

        var rows = await query
            .OrderBy(x => x.DueAt)
            .Take(500)
            .Select(x => new SalesFollowUpItem(
                x.Id, x.Type, x.Summary, x.DueAt!.Value, x.LoggedByName,
                x.OpportunityId, x.Opportunity!.Title, x.Opportunity.Stage,
                x.Opportunity.Client != null ? x.Opportunity.Client.ClientName : x.Opportunity.CompanyName ?? x.Opportunity.ContactName,
                x.Opportunity.OwnerUserId, x.Opportunity.OwnerName))
            .ToListAsync(cancellationToken);
        return Ok(rows);
    }

    /// <summary>
    /// Quotations sent and still waiting for the client's answer, longest-waiting first: the sales
    /// chase list. Quotes raised from a deal are listed for anyone who can see the pipeline; other
    /// quotes are finance documents, so they are listed only for people who can see finance.
    /// <paramref name="ownerUserId"/> narrows it to the deal owner, or whoever sent a quote with no deal.
    /// </summary>
    [HttpGet("quotes-awaiting-answer")]
    public async Task<ActionResult<IReadOnlyCollection<SalesQuoteAwaitingAnswer>>> ListQuotesAwaitingAnswer(
        [FromQuery] Guid? ownerUserId, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var includeUnlinked = tenantContext.IsAdmin || tenantContext.HasPermission(PermissionNames.FinanceView);

        var query = dbContext.TenantQuotations
            .AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.Status == "Sent" && (includeUnlinked || x.SalesOpportunityId != null));
        var scope = await OwnerScopeAsync(cancellationToken);
        foreach (var owner in new[] { ownerUserId, scope }.Where(x => x.HasValue))
            query = query.Where(x => x.SalesOpportunity != null
                ? x.SalesOpportunity.OwnerUserId == owner
                : x.SentByUserId == owner);

        var rows = await query
            .OrderBy(x => x.SentAt ?? x.QuoteDate)
            .Take(500)
            .Select(x => new SalesQuoteAwaitingAnswer(
                x.Id, x.QuoteNumber, x.Subject, x.Client != null ? x.Client.ClientName : null,
                x.TotalAmount, x.Currency, x.SentAt ?? x.QuoteDate, x.ValidUntil,
                x.SalesOpportunityId, x.SalesOpportunity != null ? x.SalesOpportunity.Title : null,
                x.SalesOpportunity != null ? x.SalesOpportunity.OwnerUserId : x.SentByUserId,
                x.SalesOpportunity != null
                    ? x.SalesOpportunity.OwnerName
                    : dbContext.Users.Where(u => u.Id == x.SentByUserId).Select(u => u.FullName).FirstOrDefault()))
            .ToListAsync(cancellationToken);
        return Ok(rows);
    }

    // ── People on the deal ───────────────────────────────────────────────────

    /// <summary>
    /// Adds a person to the deal: an existing contact of the deal's client, or a new one (matched to
    /// an existing contact by email, then name, so nobody is entered twice).
    /// </summary>
    [HttpPost("opportunities/{id:guid}/contacts")]
    public async Task<ActionResult<SalesOpportunityDetailResponse>> AddContact(Guid id, [FromBody] AddDealContactRequest request, CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(cancellationToken);
        var opportunity = await LoadAsync(id, cancellationToken);
        if (!opportunity.ClientId.HasValue)
            throw new BusinessRuleException("Give the deal a client or prospect first.");

        ClientContact contact;
        if (request.ContactId.HasValue)
        {
            contact = await dbContext.ClientContacts.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == request.ContactId && x.ClientId == opportunity.ClientId && x.IsActive, cancellationToken)
                ?? throw new NotFoundException("Contact not found for this client.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.FullName))
                throw new BusinessRuleException("Pick a contact or enter a name.");
            contact = await ClientContacts.FindOrAddAsync(dbContext, TenantId, opportunity.ClientId.Value, request.FullName, request.Email, request.Phone, cancellationToken);
            contact.JobTitle ??= string.IsNullOrWhiteSpace(request.JobTitle) ? null : request.JobTitle.Trim();
            if (!string.IsNullOrWhiteSpace(request.Role))
                contact.Role = ClientContacts.NormalizeRole(request.Role);
        }
        if (await LinkContactAsync(opportunity, contact, cancellationToken))
            await AddSystemActivityAsync(opportunity, $"{contact.FullName} added to the deal.", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await LoadDetailAsync(id, cancellationToken));
    }

    [HttpDelete("opportunities/{id:guid}/contacts/{contactId:guid}")]
    public async Task<ActionResult<SalesOpportunityDetailResponse>> RemoveContact(Guid id, Guid contactId, CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(cancellationToken);
        await LoadAsync(id, cancellationToken);
        var links = await dbContext.SalesOpportunityContacts.Where(x => x.TenantId == TenantId && x.OpportunityId == id && x.ContactId == contactId).ToListAsync(cancellationToken);
        dbContext.SalesOpportunityContacts.RemoveRange(links);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await LoadDetailAsync(id, cancellationToken));
    }

    /// <summary>The contact typed on the deal form (name, email, phone) becomes a contact of its client, on the deal.</summary>
    private async Task LinkDealFieldsContactAsync(SalesOpportunity opportunity, CancellationToken cancellationToken)
    {
        if (!opportunity.ClientId.HasValue || string.IsNullOrWhiteSpace(opportunity.ContactName))
            return;
        var contact = await ClientContacts.FindOrAddAsync(dbContext, TenantId, opportunity.ClientId.Value, opportunity.ContactName, opportunity.ContactEmail, opportunity.ContactPhone, cancellationToken);
        await LinkContactAsync(opportunity, contact, cancellationToken);
    }

    private async Task<bool> LinkContactAsync(SalesOpportunity opportunity, ClientContact contact, CancellationToken cancellationToken)
    {
        var linked = dbContext.SalesOpportunityContacts.Local.Any(x => x.OpportunityId == opportunity.Id && x.ContactId == contact.Id)
            || await dbContext.SalesOpportunityContacts.AnyAsync(x => x.OpportunityId == opportunity.Id && x.ContactId == contact.Id, cancellationToken);
        if (linked)
            return false;
        dbContext.SalesOpportunityContacts.Add(new SalesOpportunityContact { TenantId = TenantId, OpportunityId = opportunity.Id, ContactId = contact.Id });
        return true;
    }

    [HttpPost("activities/{activityId:guid}/complete")]
    public async Task<ActionResult<SalesActivityResponse>> CompleteActivity(Guid activityId, CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(cancellationToken);
        var activity = await dbContext.SalesActivities
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == activityId, cancellationToken)
            ?? throw new NotFoundException("Activity not found.");
        await EnsureVisibleAsync(await dbContext.SalesOpportunities
            .Where(x => x.TenantId == TenantId && x.Id == activity.OpportunityId)
            .Select(x => x.OwnerUserId)
            .FirstOrDefaultAsync(cancellationToken), cancellationToken);

        activity.CompletedAt ??= DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapActivity(activity));
    }

    // ── Conversion ───────────────────────────────────────────────────────────

    /// <summary>
    /// Turns the prospect on a deal into a client record and links it. No-op when the deal already
    /// has a client, so the UI can call it unconditionally before creating a work order.
    /// </summary>
    [HttpPost("opportunities/{id:guid}/convert-to-client")]
    public async Task<ActionResult<SalesOpportunityDetailResponse>> ConvertToClient(Guid id, CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(cancellationToken);
        // Same gate as creating a client from the Clients screen.
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageAssets);
        var opportunity = await LoadAsync(id, cancellationToken);

        if (opportunity.ClientId.HasValue)
        {
            if (await ProspectClients.PromoteAsync(dbContext, TenantId, opportunity.ClientId, cancellationToken))
            {
                await AddSystemActivityAsync(opportunity, "The prospect is now a client.", cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                await auditLogService.LogAsync(TenantId, UserId, "Client created", nameof(Client), opportunity.ClientId.Value.ToString(), $"Prospect from sales opportunity '{opportunity.Title}' made a client.", cancellationToken);
            }
            return Ok(await LoadDetailAsync(id, cancellationToken));
        }

        var name = opportunity.CompanyName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            name = opportunity.ContactName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new BusinessRuleException("Add a company or contact name to the opportunity before creating the client.");

        var lowered = name.ToLower();
        var nameTaken = await dbContext.Clients.AnyAsync(x => x.TenantId == TenantId && x.ClientName.ToLower() == lowered, cancellationToken);
        if (nameTaken)
            throw new BusinessRuleException($"A client named '{name}' already exists. Edit the opportunity and pick that client instead.");

        var client = new Client
        {
            TenantId = TenantId,
            ClientName = name,
            Email = opportunity.ContactEmail,
            Phone = opportunity.ContactPhone,
            Location = opportunity.Location,
            ContactPerson = opportunity.ContactName,
            ContactPhone = opportunity.ContactPhone,
            Notes = $"Created from sales opportunity '{opportunity.Title}'.",
            IsActive = true,
        };
        dbContext.Clients.Add(client);
        opportunity.ClientId = client.Id;
        await AddSystemActivityAsync(opportunity, $"Client '{client.ClientName}' created from this opportunity.", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "Client created", nameof(Client), client.Id.ToString(), $"Created client '{client.ClientName}' from sales opportunity '{opportunity.Title}'.", cancellationToken);

        return Ok(await LoadDetailAsync(id, cancellationToken));
    }

    /// <summary>Starts a draft quotation for the deal, pre-filled with its title and estimated value.</summary>
    [HttpPost("opportunities/{id:guid}/convert-to-quotation")]
    public async Task<ActionResult<object>> ConvertToQuotation(Guid id, CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(cancellationToken);
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);
        var opportunity = await LoadAsync(id, cancellationToken);
        var dealPipeline = SalesPipelineCatalog.For(await PipelinesAsync(cancellationToken), opportunity.PipelineId);

        if (!opportunity.ClientId.HasValue)
            throw new BusinessRuleException("Link or create the client for this opportunity before quoting.");

        var now = DateTime.UtcNow;
        var quote = new TenantQuotation
        {
            TenantId = TenantId,
            ClientId = opportunity.ClientId.Value,
            SiteId = opportunity.SiteId,
            SalesOpportunityId = opportunity.Id,
            QuoteNumber = await documentNumberingService.GenerateAsync(TenantId, null, DocumentTypes.Quotation, cancellationToken),
            Status = "Draft",
            QuoteDate = now,
            ValidUntil = now.AddDays(30),
            Subject = opportunity.Title,
            Notes = opportunity.Description,
            Currency = opportunity.Currency,
        };
        if (opportunity.EstimatedValue > 0)
        {
            quote.Lines.Add(new TenantQuotationLine
            {
                Description = opportunity.Title,
                ItemType = "Service",
                Quantity = 1,
                Unit = "lot",
                UnitRate = opportunity.EstimatedValue,
                LineTotal = opportunity.EstimatedValue,
                TaxRate = 0,
                GrandTotal = opportunity.EstimatedValue,
            });
        }
        quote.Subtotal = quote.Lines.Sum(x => x.LineTotal);
        quote.TotalAmount = quote.Lines.Sum(x => x.GrandTotal);

        dbContext.TenantQuotations.Add(quote);
        // Quoting moves an earlier deal to the pipeline's Proposal stage, where it has one.
        if (dealPipeline.Find(SalesStages.Proposal) is { } proposal && dealPipeline.Rank(opportunity.Stage) < dealPipeline.Rank(proposal.Key) && !SalesStages.IsClosed(opportunity.Stage))
        {
            opportunity.Stage = proposal.Key;
            opportunity.Probability = proposal.Probability;
        }
        await AddSystemActivityAsync(opportunity, $"Draft quotation {quote.QuoteNumber} created.", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "finance.quotation.created", nameof(TenantQuotation), quote.Id.ToString(), $"Created quotation {quote.QuoteNumber} from sales opportunity '{opportunity.Title}'.", cancellationToken);

        return Ok(new { quotationId = quote.Id, quoteNumber = quote.QuoteNumber });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>See <see cref="SalesCrmAccess"/> for who may read and manage the pipeline.</summary>
    private void EnsureCanView()
    {
        userAccessService.EnsureTenantOperationalAccess();
        if (!SalesCrmAccess.CanView(tenantContext))
            throw new ForbiddenException("You do not have access to the sales pipeline.");
    }

    private async Task EnsureCanManageAsync(CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        if (!SalesCrmAccess.CanManage(tenantContext))
            throw new ForbiddenException("You do not have permission to manage the sales pipeline.");
        await licenseGuardService.EnsureTenantCanMutateAsync(TenantId, cancellationToken);
    }

    /// <summary>A stage name, "Open" (not won or lost), or "Stalled" (open and quiet for the stall period).</summary>
    private async Task<IQueryable<SalesOpportunity>> ApplyStageFilterAsync(IQueryable<SalesOpportunity> query, string? stage, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(stage))
            return query;
        if (string.Equals(stage, "Open", StringComparison.OrdinalIgnoreCase))
            return query.Where(x => x.Stage != SalesStages.Won && x.Stage != SalesStages.Lost);
        if (string.Equals(stage, "Stalled", StringComparison.OrdinalIgnoreCase))
        {
            var days = await SalesStalls.ThresholdDaysAsync(dbContext, TenantId, cancellationToken);
            var quietSince = DateTime.UtcNow.AddDays(-Math.Max(days, 0));
            return days <= 0
                ? query.Where(x => false)
                : query.Where(x => x.Stage != SalesStages.Won && x.Stage != SalesStages.Lost && (x.LastActivityAt ?? x.CreatedAt) <= quietSince);
        }
        var key = stage.Trim();
        return query.Where(x => x.Stage == key);
    }

    private async Task<SalesOpportunity> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        var opportunity = await dbContext.SalesOpportunities
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Opportunity not found.");
        await EnsureVisibleAsync(opportunity.OwnerUserId, cancellationToken);
        return opportunity;
    }

    private async Task ApplyAsync(SalesOpportunity opportunity, UpsertSalesOpportunityRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new BusinessRuleException("Opportunity title is required.");
        if (!request.ClientId.HasValue && string.IsNullOrWhiteSpace(request.CompanyName) && string.IsNullOrWhiteSpace(request.ContactName))
            throw new BusinessRuleException("Pick an existing client or enter the prospect's company or contact name.");
        if (request.EstimatedValue < 0)
            throw new BusinessRuleException("Estimated value cannot be negative.");
        if (request.Probability is < 0 or > 100)
            throw new BusinessRuleException("Probability must be between 0 and 100.");

        if (request.ClientId.HasValue)
        {
            var clientExists = await dbContext.Clients.AnyAsync(x => x.TenantId == TenantId && x.Id == request.ClientId.Value, cancellationToken);
            if (!clientExists) throw new NotFoundException("Client not found.");
        }
        if (request.SiteId.HasValue)
        {
            if (!request.ClientId.HasValue)
                throw new BusinessRuleException("A site can only be set when the opportunity has a client.");
            var siteBelongs = await dbContext.Sites.AnyAsync(x => x.TenantId == TenantId && x.Id == request.SiteId.Value && x.ClientId == request.ClientId.Value, cancellationToken);
            if (!siteBelongs) throw new BusinessRuleException("The selected site does not belong to the selected client.");
        }
        if (request.OwnerUserId.HasValue && request.OwnerUserId != opportunity.OwnerUserId)
        {
            opportunity.OwnerName = await SalesCrmAccess.ResolveOwnerNameAsync(dbContext, rolePermissionService, TenantId, request.OwnerUserId.Value, cancellationToken);
            opportunity.OwnerUserId = request.OwnerUserId.Value;
        }
        var currency = SalesCurrencyConverter.Normalize(request.Currency);
        if (currency.Length != 0 && (currency.Length != 3 || !currency.All(char.IsAsciiLetterUpper)))
            throw new BusinessRuleException("Use a 3-letter currency code, e.g. KES or USD.");

        opportunity.Title = request.Title.Trim();
        opportunity.ClientId = request.ClientId;
        opportunity.SiteId = request.SiteId;
        opportunity.CompanyName = request.CompanyName?.Trim();
        opportunity.ContactName = request.ContactName?.Trim();
        opportunity.ContactEmail = request.ContactEmail?.Trim();
        opportunity.ContactPhone = request.ContactPhone?.Trim();
        opportunity.Location = request.Location?.Trim();
        opportunity.Source = request.Source?.Trim();
        opportunity.ServiceType = string.IsNullOrWhiteSpace(request.ServiceType) ? null : WorkOrderTypes.Normalize(request.ServiceType);
        opportunity.Description = request.Description?.Trim();
        opportunity.EstimatedValue = request.EstimatedValue;
        opportunity.Currency = currency.Length == 0 ? FallbackCurrency : currency;
        opportunity.ExpectedCloseDate = request.ExpectedCloseDate.HasValue
            ? DateTime.SpecifyKind(request.ExpectedCloseDate.Value, DateTimeKind.Utc)
            : null;
        if (request.CustomFields is { Count: > 0 })
            await SalesCustomFields.ApplyAsync(dbContext, TenantId, opportunity, request.CustomFields,
                await SalesCustomFields.LoadAsync(dbContext, TenantId, cancellationToken), cancellationToken);
    }

    private async Task<string?> ResolveUserNameAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.Users
            .Where(x => x.TenantId == TenantId && x.Id == userId)
            .Select(x => x.FullName)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task AddSystemActivityAsync(SalesOpportunity opportunity, string summary, CancellationToken cancellationToken)
    {
        dbContext.SalesActivities.Add(new SalesActivity
        {
            TenantId = TenantId,
            OpportunityId = opportunity.Id,
            Type = SalesActivityTypes.System,
            Summary = summary,
            OccurredAt = DateTime.UtcNow,
            LoggedByUserId = UserId,
            LoggedByName = await ResolveUserNameAsync(UserId, cancellationToken),
        });
        opportunity.LastActivityAt = DateTime.UtcNow;
    }

    /// <param name="checkVisibility">False only straight after the caller created or handed over the deal, which they may now not own.</param>
    private async Task<SalesOpportunityDetailResponse> LoadDetailAsync(Guid id, CancellationToken cancellationToken, bool checkVisibility = true)
    {
        var opportunity = await dbContext.SalesOpportunities
            .AsNoTracking()
            .Include(x => x.Client)
            .Include(x => x.Site)
            .Include(x => x.Activities)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Opportunity not found.");
        if (checkVisibility)
            await EnsureVisibleAsync(opportunity.OwnerUserId, cancellationToken);

        var workOrders = await dbContext.WorkOrders
            .AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.SalesOpportunityId == id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new SalesLinkedWorkOrder(x.Id, x.WorkOrderNumber, x.Title, x.Status, x.CreatedAt))
            .ToListAsync(cancellationToken);

        var quotations = await dbContext.TenantQuotations
            .AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.SalesOpportunityId == id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new SalesLinkedQuotation(x.Id, x.QuoteNumber, x.Status, x.TotalAmount, x.Currency, x.CreatedAt))
            .ToListAsync(cancellationToken);

        var tenders = await dbContext.SalesTenders
            .AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.OpportunityId == id)
            .OrderByDescending(x => x.SubmissionDeadline)
            .Select(x => new SalesLinkedTender(x.Id, x.Type, x.Reference, x.Title, x.Status, x.SubmissionDeadline))
            .ToListAsync(cancellationToken);

        var converter = await SalesCurrencyConverter.LoadAsync(dbContext, TenantId, cancellationToken);
        var dealPipeline = SalesPipelineCatalog.For(await PipelinesAsync(cancellationToken), opportunity.PipelineId);
        var contacts = await dbContext.SalesOpportunityContacts.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.OpportunityId == id && x.Contact!.IsActive)
            .Select(x => x.Contact!)
            .OrderByDescending(x => x.IsPrimary).ThenBy(x => x.FullName)
            .ToListAsync(cancellationToken);
        var renewalContractNumber = opportunity.RenewalOfContractId is null ? null : await dbContext.Contracts.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.Id == opportunity.RenewalOfContractId)
            .Select(x => x.ContractNumber)
            .FirstOrDefaultAsync(cancellationToken);
        var sourceWorkOrderNumber = opportunity.SourceWorkOrderId is null ? null : await dbContext.WorkOrders.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.Id == opportunity.SourceWorkOrderId)
            .Select(x => x.WorkOrderNumber)
            .FirstOrDefaultAsync(cancellationToken);

        return new SalesOpportunityDetailResponse(
            opportunity.Id, opportunity.Title, opportunity.Stage,
            opportunity.ClientId, opportunity.Client?.ClientName,
            opportunity.SiteId, opportunity.Site?.SiteName,
            opportunity.CompanyName, opportunity.ContactName, opportunity.ContactEmail, opportunity.ContactPhone, opportunity.Location,
            opportunity.Source, opportunity.ServiceType, opportunity.Description,
            opportunity.EstimatedValue, opportunity.Currency, opportunity.Probability, opportunity.ExpectedCloseDate,
            opportunity.OwnerUserId, opportunity.OwnerName, opportunity.LostReason,
            opportunity.WonAt, opportunity.LostAt, opportunity.CreatedAt, opportunity.UpdatedAt,
            opportunity.Activities.OrderByDescending(x => x.OccurredAt).Select(MapActivity).ToList(),
            workOrders,
            quotations,
            tenders,
            converter.Convert(opportunity.EstimatedValue, opportunity.Currency),
            converter.ReportingCurrency,
            opportunity.RenewalOfContractId, renewalContractNumber,
            opportunity.SourceWorkOrderId, sourceWorkOrderNumber,
            opportunity.Client?.IsProspect ?? false,
            contacts.Select(ClientContacts.Map).ToList(),
            dealPipeline.Id,
            dealPipeline.Name,
            await DealFieldValuesAsync(id, dealPipeline.Id, cancellationToken),
            await StageBlockersAsync(opportunity, dealPipeline, cancellationToken),
            await DealSequenceAsync(id, cancellationToken),
            SalesStages.IsClosed(opportunity.Stage) ? null
                : (await SalesScoring.LoadAsync(dbContext, TenantId, await PipelinesAsync(cancellationToken), cancellationToken)).Score(opportunity, DateTime.UtcNow));
    }

    /// <summary>For each stage the deal could move to, what it is still missing (stages with nothing missing are left out).</summary>
    private async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> StageBlockersAsync(SalesOpportunity opportunity, SalesPipelineView pipeline, CancellationToken cancellationToken)
    {
        if (pipeline.Stages.All(x => x.Required.Count == 0))
            return new Dictionary<string, IReadOnlyList<string>>();
        var facts = await SalesStageRules.LoadFactsAsync(dbContext, TenantId, opportunity.Id, cancellationToken);
        return pipeline.Stages
            .Where(x => x.Key != SalesStages.Lost)
            .Select(x => (x.Key, Missing: SalesStageRules.Missing(opportunity, pipeline.Id, SalesStageRules.RequiredFor(pipeline, x.Key), facts)))
            .Where(x => x.Missing.Count > 0)
            .ToDictionary(x => x.Key, x => x.Missing);
    }

    private static SalesOpportunityListItem MapListItem(SalesOpportunity x, int workOrderCount, DateTime? nextFollowUpAt, SalesCurrencyConverter converter, int stalledAfterDays)
    {
        var idleDays = SalesStalls.IdleDays(x, DateTime.UtcNow);
        return new(x.Id, x.Title, x.Stage, x.ClientId, x.Client?.ClientName, x.CompanyName, x.ContactName,
            x.EstimatedValue, x.Currency, x.Probability, x.ExpectedCloseDate, x.OwnerUserId, x.OwnerName,
            x.ServiceType, workOrderCount, nextFollowUpAt, x.LastActivityAt, x.CreatedAt,
            converter.Convert(x.EstimatedValue, x.Currency), x.Client?.IsProspect ?? false,
            idleDays, SalesStalls.IsStalled(x.Stage, idleDays, stalledAfterDays), x.StageChangedAt);
    }

    private static SalesActivityResponse MapActivity(SalesActivity x) =>
        new(x.Id, x.OpportunityId, x.Type, x.Summary, x.OccurredAt, x.DueAt, x.CompletedAt, x.LoggedByName, x.Latitude, x.Longitude, x.LocationAccuracyMeters);
}

public static class SalesStages
{
    public const string Lead = "Lead";
    public const string Qualified = "Qualified";
    public const string SiteSurvey = "SiteSurvey";
    public const string Proposal = "Proposal";
    public const string Negotiation = "Negotiation";
    public const string Won = "Won";
    public const string Lost = "Lost";

    public static readonly IReadOnlyList<string> All = [Lead, Qualified, SiteSurvey, Proposal, Negotiation, Won, Lost];

    public static string Normalize(string? stage)
    {
        var match = All.FirstOrDefault(x => string.Equals(x, stage?.Trim(), StringComparison.OrdinalIgnoreCase));
        return match ?? throw new BusinessRuleException($"Unknown sales stage '{stage}'.");
    }

    public static bool IsClosed(string stage) => stage is Won or Lost;

    public static int Rank(string stage) => All.ToList().IndexOf(stage);

    public static int DefaultProbability(string stage) => stage switch
    {
        Lead => 10,
        Qualified => 25,
        SiteSurvey => 40,
        Proposal => 60,
        Negotiation => 80,
        Won => 100,
        _ => 0,
    };
}

public static class SalesActivityTypes
{
    public const string System = "System";

    private static readonly string[] UserTypes = ["Call", "Email", "WhatsApp", "Meeting", "SiteVisit", "Note", "FollowUp"];

    public static string Normalize(string? type) =>
        UserTypes.FirstOrDefault(x => string.Equals(x, type?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "Note";
}

// ── Request / response records ───────────────────────────────────────────────

public sealed record UpsertSalesOpportunityRequest(
    [StringLength(200)] string Title,
    Guid? ClientId,
    Guid? SiteId,
    [StringLength(200)] string? CompanyName,
    [StringLength(150)] string? ContactName,
    [StringLength(256)] string? ContactEmail,
    [StringLength(50)] string? ContactPhone,
    [StringLength(250)] string? Location,
    [StringLength(60)] string? Source,
    [StringLength(50)] string? ServiceType,
    [StringLength(4000)] string? Description,
    decimal EstimatedValue,
    [StringLength(10)] string? Currency,
    int? Probability,
    DateTime? ExpectedCloseDate,
    Guid? OwnerUserId,
    [StringLength(40)] string? Stage = null,
    Guid? PipelineId = null,
    IReadOnlyDictionary<string, string?>? CustomFields = null);

public sealed record ChangeSalesStageRequest([StringLength(40)] string Stage, [StringLength(500)] string? LostReason);

/// <param name="Latitude">With <paramref name="Longitude"/>: where a site visit was logged from.</param>
public sealed record CreateSalesActivityRequest(
    [StringLength(40)] string? Type,
    [StringLength(2000)] string Summary,
    DateTime? OccurredAt,
    DateTime? DueAt,
    double? Latitude = null,
    double? Longitude = null,
    double? AccuracyMeters = null);

public sealed record SalesOpportunityListItem(
    Guid Id, string Title, string Stage, Guid? ClientId, string? ClientName,
    string? CompanyName, string? ContactName,
    decimal EstimatedValue, string Currency, int Probability, DateTime? ExpectedCloseDate,
    Guid? OwnerUserId, string? OwnerName, string? ServiceType,
    int WorkOrderCount, DateTime? NextFollowUpAt, DateTime? LastActivityAt, DateTime CreatedAt,
    decimal? ConvertedValue, bool ClientIsProspect = false,
    int DaysIdle = 0, bool IsStalled = false, DateTime? StageChangedAt = null, Guid? PipelineId = null,
    int? SuggestedProbability = null);

public sealed record SalesOpportunityDetailResponse(
    Guid Id, string Title, string Stage,
    Guid? ClientId, string? ClientName, Guid? SiteId, string? SiteName,
    string? CompanyName, string? ContactName, string? ContactEmail, string? ContactPhone, string? Location,
    string? Source, string? ServiceType, string? Description,
    decimal EstimatedValue, string Currency, int Probability, DateTime? ExpectedCloseDate,
    Guid? OwnerUserId, string? OwnerName, string? LostReason,
    DateTime? WonAt, DateTime? LostAt, DateTime CreatedAt, DateTime? UpdatedAt,
    IReadOnlyCollection<SalesActivityResponse> Activities,
    IReadOnlyCollection<SalesLinkedWorkOrder> WorkOrders,
    IReadOnlyCollection<SalesLinkedQuotation> Quotations,
    IReadOnlyCollection<SalesLinkedTender> Tenders,
    decimal? ConvertedValue,
    string ReportingCurrency,
    Guid? RenewalOfContractId = null,
    string? RenewalContractNumber = null,
    Guid? SourceWorkOrderId = null,
    string? SourceWorkOrderNumber = null,
    bool ClientIsProspect = false,
    IReadOnlyCollection<ClientContactResponse>? Contacts = null,
    Guid? PipelineId = null,
    string? PipelineName = null,
    IReadOnlyList<SalesDealFieldValue>? CustomFields = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? StageBlockers = null,
    SalesDealSequence? Sequence = null,
    SalesDealScore? Score = null);

public sealed record AddDealContactRequest(
    Guid? ContactId,
    [StringLength(150)] string? FullName,
    [StringLength(256)] string? Email,
    [StringLength(50)] string? Phone,
    [StringLength(120)] string? JobTitle,
    [StringLength(30)] string? Role);

public sealed record SalesActivityResponse(
    Guid Id, Guid OpportunityId, string Type, string Summary,
    DateTime OccurredAt, DateTime? DueAt, DateTime? CompletedAt, string? LoggedByName,
    double? Latitude = null, double? Longitude = null, double? AccuracyMeters = null);

/// <summary>An open follow-up with enough of its deal to act on it from a list.</summary>
public sealed record SalesFollowUpItem(
    Guid Id, string Type, string Summary, DateTime DueAt, string? LoggedByName,
    Guid OpportunityId, string OpportunityTitle, string OpportunityStage, string? PartyName,
    Guid? OwnerUserId, string? OwnerName);

/// <summary>A sent quotation still waiting for the client. <see cref="SentAt"/> falls back to the quote date for quotes sent before it was recorded.</summary>
public sealed record SalesQuoteAwaitingAnswer(
    Guid Id, string QuoteNumber, string? Subject, string? ClientName,
    decimal TotalAmount, string Currency, DateTime SentAt, DateTime ValidUntil,
    Guid? OpportunityId, string? OpportunityTitle, Guid? OwnerUserId, string? OwnerName);

public sealed record SalesLinkedWorkOrder(Guid Id, string WorkOrderNumber, string Title, string Status, DateTime CreatedAt);

public sealed record SalesLinkedQuotation(Guid Id, string QuoteNumber, string Status, decimal TotalAmount, string Currency, DateTime CreatedAt);

public sealed record SalesLinkedTender(Guid Id, string Type, string? Reference, string Title, string Status, DateTime SubmissionDeadline);

/// <summary>Stage totals are in <see cref="SalesPipelineSummary.ReportingCurrency"/>.</summary>
public sealed record SalesStageSummary(string Stage, int Count, decimal Value);

/// <summary>Open deals in a currency with no exchange rate, left out of the converted totals.</summary>
public sealed record SalesUnconvertedCurrency(string Currency, int Count, decimal Value);

/// <summary>All values are converted to <see cref="ReportingCurrency"/>.</summary>
public sealed record SalesPipelineSummary(
    string ReportingCurrency,
    int OpenCount, decimal OpenValue, decimal WeightedValue,
    int WonThisMonthCount, decimal WonThisMonthValue,
    decimal? WinRate, int OverdueFollowUps,
    int OpenTenders, int TendersDueThisWeek,
    IReadOnlyCollection<SalesStageSummary> Stages,
    IReadOnlyCollection<SalesUnconvertedCurrency> UnconvertedCurrencies,
    int StalledDeals = 0,
    int StalledAfterDays = 0);

/// <param name="IntakeEmailAddress">The workspace's intake mailbox (Settings › Email intake), when on: replies to deal emails come back through it.</param>
public sealed record SalesSettingsResponse(Guid? DefaultOwnerUserId, string? DefaultOwnerName, string DealVisibility, bool CreateRenewalDeals, int StalledAfterDays = 14, bool SendWeeklySummary = true,
    string? LeadEmailAddress = null, string? IntakeEmailAddress = null, bool WebFormEnabled = false, string? WebFormKey = null);

/// <param name="LeadEmailAddress">Null leaves it; empty clears it.</param>
public sealed record UpdateSalesSettingsRequest(Guid? DefaultOwnerUserId, [StringLength(20)] string? DealVisibility, bool CreateRenewalDeals = true, int? StalledAfterDays = null, bool? SendWeeklySummary = null,
    [StringLength(256)] string? LeadEmailAddress = null, bool? WebFormEnabled = null);

public sealed record SalesOwnerOption(Guid Id, string FullName, string Role);

public sealed record ReassignSalesOwnerRequest(Guid OwnerUserId);

public sealed record SalesCurrencySettings(
    string ReportingCurrency,
    IReadOnlyCollection<SalesCurrencyRate> Rates,
    IReadOnlyCollection<string> MissingCurrencies);

public sealed record SetSalesExchangeRateRequest([StringLength(10)] string Currency, decimal Rate);
