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
public sealed class SalesCrmController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IDocumentNumberingService documentNumberingService,
    IAuditLogService auditLogService,
    IUserAccessService userAccessService,
    ILicenseGuardService licenseGuardService) : TenantAwareControllerBase(tenantContext)
{
    private const string FallbackCurrency = "KES";

    // ── Pipeline ─────────────────────────────────────────────────────────────

    [HttpGet("opportunities")]
    public async Task<ActionResult<IReadOnlyCollection<SalesOpportunityListItem>>> ListOpportunities(
        [FromQuery] string? stage,
        [FromQuery] string? search,
        [FromQuery] Guid? ownerUserId,
        [FromQuery] Guid? clientId,
        CancellationToken cancellationToken)
    {
        EnsureCanView();

        var query = dbContext.SalesOpportunities
            .AsNoTracking()
            .Include(x => x.Client)
            .Where(x => x.TenantId == TenantId);

        if (!string.IsNullOrWhiteSpace(stage))
        {
            if (string.Equals(stage, "Open", StringComparison.OrdinalIgnoreCase))
                query = query.Where(x => x.Stage != SalesStages.Won && x.Stage != SalesStages.Lost);
            else
                query = query.Where(x => x.Stage == SalesStages.Normalize(stage));
        }
        if (ownerUserId.HasValue)
            query = query.Where(x => x.OwnerUserId == ownerUserId.Value);
        if (clientId.HasValue)
            query = query.Where(x => x.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(x =>
                x.Title.ToLower().Contains(s) ||
                (x.CompanyName != null && x.CompanyName.ToLower().Contains(s)) ||
                (x.ContactName != null && x.ContactName.ToLower().Contains(s)) ||
                (x.Client != null && x.Client.ClientName.ToLower().Contains(s)));
        }

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

        return Ok(rows.Select(x => MapListItem(x.Opportunity, x.WorkOrderCount, x.NextFollowUpAt)).ToList());
    }

    [HttpGet("summary")]
    public async Task<ActionResult<SalesPipelineSummary>> GetSummary(CancellationToken cancellationToken)
    {
        EnsureCanView();

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var winRateWindowStart = now.AddDays(-90);

        var opportunities = await dbContext.SalesOpportunities
            .AsNoTracking()
            .Where(x => x.TenantId == TenantId)
            .Select(x => new { x.Stage, x.EstimatedValue, x.Probability, x.WonAt, x.LostAt })
            .ToListAsync(cancellationToken);

        var open = opportunities.Where(x => !SalesStages.IsClosed(x.Stage)).ToList();
        var wonRecently = opportunities.Count(x => x.Stage == SalesStages.Won && x.WonAt >= winRateWindowStart);
        var lostRecently = opportunities.Count(x => x.Stage == SalesStages.Lost && x.LostAt >= winRateWindowStart);
        var wonThisMonth = opportunities.Where(x => x.Stage == SalesStages.Won && x.WonAt >= monthStart).ToList();

        var overdueFollowUps = await dbContext.SalesActivities
            .AsNoTracking()
            .CountAsync(x => x.TenantId == TenantId && x.DueAt != null && x.CompletedAt == null && x.DueAt < now, cancellationToken);

        var stages = SalesStages.All
            .Select(stage =>
            {
                var inStage = opportunities.Where(x => x.Stage == stage).ToList();
                return new SalesStageSummary(stage, inStage.Count, inStage.Sum(x => x.EstimatedValue));
            })
            .ToList();

        return Ok(new SalesPipelineSummary(
            OpenCount: open.Count,
            OpenValue: open.Sum(x => x.EstimatedValue),
            WeightedValue: Math.Round(open.Sum(x => x.EstimatedValue * x.Probability / 100m), 2),
            WonThisMonthCount: wonThisMonth.Count,
            WonThisMonthValue: wonThisMonth.Sum(x => x.EstimatedValue),
            WinRate: wonRecently + lostRecently == 0 ? null : Math.Round(wonRecently * 100m / (wonRecently + lostRecently), 1),
            OverdueFollowUps: overdueFollowUps,
            Stages: stages));
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

        var opportunity = new SalesOpportunity { TenantId = TenantId, Stage = SalesStages.Lead };
        await ApplyAsync(opportunity, request, cancellationToken);
        if (!request.OwnerUserId.HasValue)
        {
            opportunity.OwnerUserId = UserId;
            opportunity.OwnerName = await ResolveUserNameAsync(UserId, cancellationToken);
        }
        opportunity.Stage = SalesStages.Normalize(request.Stage ?? SalesStages.Lead);
        if (SalesStages.IsClosed(opportunity.Stage))
            throw new BusinessRuleException("Create the opportunity first, then mark it won or lost.");
        opportunity.Probability = request.Probability ?? SalesStages.DefaultProbability(opportunity.Stage);

        dbContext.SalesOpportunities.Add(opportunity);
        await AddSystemActivityAsync(opportunity, "Opportunity created.", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "crm.opportunity.created", nameof(SalesOpportunity), opportunity.Id.ToString(), $"Created opportunity '{opportunity.Title}'.", cancellationToken);

        return CreatedAtAction(nameof(GetOpportunity), new { id = opportunity.Id }, await LoadDetailAsync(opportunity.Id, cancellationToken));
    }

    [HttpPut("opportunities/{id:guid}")]
    public async Task<ActionResult<SalesOpportunityDetailResponse>> UpdateOpportunity(
        Guid id, [FromBody] UpsertSalesOpportunityRequest request, CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(cancellationToken);
        var opportunity = await LoadAsync(id, cancellationToken);

        await ApplyAsync(opportunity, request, cancellationToken);
        if (request.Probability.HasValue)
            opportunity.Probability = request.Probability.Value;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await LoadDetailAsync(id, cancellationToken));
    }

    [HttpPost("opportunities/{id:guid}/stage")]
    public async Task<ActionResult<SalesOpportunityDetailResponse>> ChangeStage(
        Guid id, [FromBody] ChangeSalesStageRequest request, CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(cancellationToken);
        var opportunity = await LoadAsync(id, cancellationToken);

        var stage = SalesStages.Normalize(request.Stage);
        if (stage == opportunity.Stage)
            return Ok(await LoadDetailAsync(id, cancellationToken));

        if (stage == SalesStages.Lost && string.IsNullOrWhiteSpace(request.LostReason))
            throw new BusinessRuleException("Give a reason when marking an opportunity as lost.");

        var previous = opportunity.Stage;
        opportunity.Stage = stage;
        opportunity.Probability = SalesStages.DefaultProbability(stage);
        opportunity.WonAt = stage == SalesStages.Won ? DateTime.UtcNow : null;
        opportunity.LostAt = stage == SalesStages.Lost ? DateTime.UtcNow : null;
        opportunity.LostReason = stage == SalesStages.Lost ? request.LostReason!.Trim() : null;

        var note = stage == SalesStages.Lost
            ? $"Stage changed from {previous} to Lost: {opportunity.LostReason}"
            : $"Stage changed from {previous} to {stage}.";
        await AddSystemActivityAsync(opportunity, note, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "crm.opportunity.stage_changed", nameof(SalesOpportunity), opportunity.Id.ToString(), note, cancellationToken);

        return Ok(await LoadDetailAsync(id, cancellationToken));
    }

    [HttpDelete("opportunities/{id:guid}")]
    public async Task<IActionResult> DeleteOpportunity(Guid id, CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(cancellationToken);
        var opportunity = await LoadAsync(id, cancellationToken);

        var hasLinkedWork = await dbContext.WorkOrders.AnyAsync(x => x.TenantId == TenantId && x.SalesOpportunityId == id, cancellationToken)
            || await dbContext.TenantQuotations.AnyAsync(x => x.TenantId == TenantId && x.SalesOpportunityId == id, cancellationToken);
        if (hasLinkedWork)
            throw new BusinessRuleException("This opportunity has quotations or work orders linked to it. Mark it lost instead of deleting it.");

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

        dbContext.SalesActivities.Add(activity);
        opportunity.LastActivityAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapActivity(activity));
    }

    [HttpPost("activities/{activityId:guid}/complete")]
    public async Task<ActionResult<SalesActivityResponse>> CompleteActivity(Guid activityId, CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(cancellationToken);
        var activity = await dbContext.SalesActivities
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == activityId, cancellationToken)
            ?? throw new NotFoundException("Activity not found.");

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
            return Ok(await LoadDetailAsync(id, cancellationToken));

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
        if (SalesStages.Rank(opportunity.Stage) < SalesStages.Rank(SalesStages.Proposal))
        {
            opportunity.Stage = SalesStages.Proposal;
            opportunity.Probability = SalesStages.DefaultProbability(SalesStages.Proposal);
        }
        await AddSystemActivityAsync(opportunity, $"Draft quotation {quote.QuoteNumber} created.", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "finance.quotation.created", nameof(TenantQuotation), quote.Id.ToString(), $"Created quotation {quote.QuoteNumber} from sales opportunity '{opportunity.Title}'.", cancellationToken);

        return Ok(new { quotationId = quote.Id, quoteNumber = quote.QuoteNumber });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// CRM has no dedicated permission column yet, so it follows the office roles that sell and
    /// quote: Admin, Manager and Finance manage the pipeline; Viewer can read it.
    /// </summary>
    private void EnsureCanView()
    {
        userAccessService.EnsureTenantOperationalAccess();
        if (CanManageRole() || tenantContext.HasRole(AppRoles.Viewer))
            return;
        throw new ForbiddenException("You do not have access to the sales pipeline.");
    }

    private async Task EnsureCanManageAsync(CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        if (!CanManageRole())
            throw new ForbiddenException("You do not have permission to manage the sales pipeline.");
        await licenseGuardService.EnsureTenantCanMutateAsync(TenantId, cancellationToken);
    }

    private bool CanManageRole() =>
        tenantContext.IsAdmin
        || tenantContext.HasRole(AppRoles.Manager)
        || tenantContext.HasRole(AppRoles.FinanceUser);

    private async Task<SalesOpportunity> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.SalesOpportunities
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
        ?? throw new NotFoundException("Opportunity not found.");

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
        if (request.OwnerUserId.HasValue)
        {
            opportunity.OwnerUserId = request.OwnerUserId.Value;
            opportunity.OwnerName = await ResolveUserNameAsync(request.OwnerUserId.Value, cancellationToken)
                ?? throw new NotFoundException("Owner not found.");
        }

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
        opportunity.Currency = string.IsNullOrWhiteSpace(request.Currency) ? FallbackCurrency : request.Currency.Trim().ToUpper();
        opportunity.ExpectedCloseDate = request.ExpectedCloseDate.HasValue
            ? DateTime.SpecifyKind(request.ExpectedCloseDate.Value, DateTimeKind.Utc)
            : null;
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

    private async Task<SalesOpportunityDetailResponse> LoadDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var opportunity = await dbContext.SalesOpportunities
            .AsNoTracking()
            .Include(x => x.Client)
            .Include(x => x.Site)
            .Include(x => x.Activities)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Opportunity not found.");

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
            quotations);
    }

    private static SalesOpportunityListItem MapListItem(SalesOpportunity x, int workOrderCount, DateTime? nextFollowUpAt) =>
        new(x.Id, x.Title, x.Stage, x.ClientId, x.Client?.ClientName, x.CompanyName, x.ContactName,
            x.EstimatedValue, x.Currency, x.Probability, x.ExpectedCloseDate, x.OwnerUserId, x.OwnerName,
            x.ServiceType, workOrderCount, nextFollowUpAt, x.LastActivityAt, x.CreatedAt);

    private static SalesActivityResponse MapActivity(SalesActivity x) =>
        new(x.Id, x.OpportunityId, x.Type, x.Summary, x.OccurredAt, x.DueAt, x.CompletedAt, x.LoggedByName);
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

    private static readonly string[] UserTypes = ["Call", "Email", "Meeting", "SiteVisit", "Note", "FollowUp"];

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
    [StringLength(40)] string? Stage = null);

public sealed record ChangeSalesStageRequest([StringLength(40)] string Stage, [StringLength(500)] string? LostReason);

public sealed record CreateSalesActivityRequest(
    [StringLength(40)] string? Type,
    [StringLength(2000)] string Summary,
    DateTime? OccurredAt,
    DateTime? DueAt);

public sealed record SalesOpportunityListItem(
    Guid Id, string Title, string Stage, Guid? ClientId, string? ClientName,
    string? CompanyName, string? ContactName,
    decimal EstimatedValue, string Currency, int Probability, DateTime? ExpectedCloseDate,
    Guid? OwnerUserId, string? OwnerName, string? ServiceType,
    int WorkOrderCount, DateTime? NextFollowUpAt, DateTime? LastActivityAt, DateTime CreatedAt);

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
    IReadOnlyCollection<SalesLinkedQuotation> Quotations);

public sealed record SalesActivityResponse(
    Guid Id, Guid OpportunityId, string Type, string Summary,
    DateTime OccurredAt, DateTime? DueAt, DateTime? CompletedAt, string? LoggedByName);

public sealed record SalesLinkedWorkOrder(Guid Id, string WorkOrderNumber, string Title, string Status, DateTime CreatedAt);

public sealed record SalesLinkedQuotation(Guid Id, string QuoteNumber, string Status, decimal TotalAmount, string Currency, DateTime CreatedAt);

public sealed record SalesStageSummary(string Stage, int Count, decimal Value);

public sealed record SalesPipelineSummary(
    int OpenCount, decimal OpenValue, decimal WeightedValue,
    int WonThisMonthCount, decimal WonThisMonthValue,
    decimal? WinRate, int OverdueFollowUps,
    IReadOnlyCollection<SalesStageSummary> Stages);
