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
/// RFP and RFQ tracking for the sales CRM: what has come in, what is due when, the bid / no-bid
/// call, submission and award. A tender the tenant decides to bid on gets an opportunity
/// (<c>create-opportunity</c>), and its award or loss is mirrored onto that opportunity so a won
/// tender reaches the same work order hand-off as any other won deal.
/// </summary>
[ApiController]
[Authorize]
[Route("api/crm/tenders")]
public sealed class SalesTendersController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IAuditLogService auditLogService,
    IUserAccessService userAccessService,
    ILicenseGuardService licenseGuardService,
    IRolePermissionService rolePermissionService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<SalesTenderListItem>>> List(
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] Guid? ownerUserId,
        [FromQuery] Guid? clientId,
        CancellationToken cancellationToken)
    {
        EnsureCanView();
        var scope = await OwnerScopeAsync(cancellationToken);
        var query = dbContext.SalesTenders.AsNoTracking().Include(x => x.Client).Where(x => x.TenantId == TenantId && (scope == null || x.OwnerUserId == scope));

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(x => x.Type == SalesTenderTypes.Normalize(type));
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (string.Equals(status, "Open", StringComparison.OrdinalIgnoreCase))
                query = query.Where(x => SalesTenderStatuses.OpenStatuses.Contains(x.Status));
            else
                query = query.Where(x => x.Status == SalesTenderStatuses.Normalize(status));
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
                (x.Reference != null && x.Reference.ToLower().Contains(s)) ||
                (x.IssuerName != null && x.IssuerName.ToLower().Contains(s)) ||
                (x.Client != null && x.Client.ClientName.ToLower().Contains(s)));
        }

        var tenders = await query.OrderBy(x => x.SubmissionDeadline).ToListAsync(cancellationToken);
        var converter = await SalesCurrencyConverter.LoadAsync(dbContext, TenantId, cancellationToken);
        return Ok(tenders.Select(x => MapListItem(x, converter)).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SalesTenderDetailResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanView();
        return Ok(await LoadDetailAsync(id, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<SalesTenderDetailResponse>> Create([FromBody] UpsertSalesTenderRequest request, CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(cancellationToken);
        var tender = new SalesTender { TenantId = TenantId, Status = SalesTenderStatuses.Received };
        await ApplyAsync(tender, request, cancellationToken);
        if (!request.OwnerUserId.HasValue)
        {
            tender.OwnerUserId = UserId;
            tender.OwnerName = await dbContext.Users.Where(x => x.TenantId == TenantId && x.Id == UserId).Select(x => x.FullName).FirstOrDefaultAsync(cancellationToken);
        }

        dbContext.SalesTenders.Add(tender);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "crm.tender.created", nameof(SalesTender), tender.Id.ToString(), $"Logged {tender.Type} '{tender.Title}'.", cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = tender.Id }, await LoadDetailAsync(tender.Id, cancellationToken, checkVisibility: false));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SalesTenderDetailResponse>> Update(Guid id, [FromBody] UpsertSalesTenderRequest request, CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(cancellationToken);
        var tender = await LoadAsync(id, cancellationToken);
        await ApplyAsync(tender, request, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await LoadDetailAsync(id, cancellationToken, checkVisibility: false));
    }

    /// <summary>
    /// Moves a tender through its life. Submitted records when and for how much; NoBid, Lost and
    /// Cancelled need a reason. Won and Lost are mirrored onto the linked opportunity.
    /// </summary>
    [HttpPost("{id:guid}/status")]
    public async Task<ActionResult<SalesTenderDetailResponse>> ChangeStatus(Guid id, [FromBody] ChangeSalesTenderStatusRequest request, CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(cancellationToken);
        var tender = await LoadAsync(id, cancellationToken);
        var status = SalesTenderStatuses.Normalize(request.Status);
        if (status == tender.Status)
            return Ok(await LoadDetailAsync(id, cancellationToken));

        var notes = request.Notes?.Trim();
        if (SalesTenderStatuses.NeedsReason(status) && string.IsNullOrWhiteSpace(notes))
            throw new BusinessRuleException("Give a reason for this outcome.");
        if (request.SubmittedValue is < 0)
            throw new BusinessRuleException("Submitted value cannot be negative.");

        var previous = tender.Status;
        tender.Status = status;
        if (status == SalesTenderStatuses.Submitted)
        {
            tender.SubmittedAt = DateTime.UtcNow;
            tender.SubmittedValue = request.SubmittedValue ?? tender.SubmittedValue ?? tender.EstimatedValue;
        }
        if (SalesTenderStatuses.IsClosed(status))
        {
            tender.DecidedAt = DateTime.UtcNow;
            tender.OutcomeNotes = notes;
        }
        else
        {
            tender.DecidedAt = null;
            tender.OutcomeNotes = null;
        }

        var summary = $"{tender.Type} {tender.Reference ?? tender.Title}: {SalesTenderStatuses.Label(previous)} → {SalesTenderStatuses.Label(status)}"
            + (string.IsNullOrWhiteSpace(notes) ? "." : $" ({notes})");
        await MirrorOntoOpportunityAsync(tender, status, notes, summary, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "crm.tender.status_changed", nameof(SalesTender), tender.Id.ToString(), summary, cancellationToken);
        return Ok(await LoadDetailAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/owner")]
    public async Task<ActionResult<SalesTenderDetailResponse>> ReassignOwner(Guid id, [FromBody] ReassignSalesOwnerRequest request, CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(cancellationToken);
        var tender = await LoadAsync(id, cancellationToken);
        if (tender.OwnerUserId != request.OwnerUserId)
        {
            var previous = tender.OwnerName ?? "nobody";
            tender.OwnerName = await SalesCrmAccess.ResolveOwnerNameAsync(dbContext, rolePermissionService, TenantId, request.OwnerUserId, cancellationToken);
            tender.OwnerUserId = request.OwnerUserId;
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditLogService.LogAsync(TenantId, UserId, "crm.tender.owner_changed", nameof(SalesTender), tender.Id.ToString(), $"Owner changed from {previous} to {tender.OwnerName}.", cancellationToken);
        }
        return Ok(await LoadDetailAsync(id, cancellationToken, checkVisibility: false));
    }

    /// <summary>
    /// The bid decision: opens a pipeline opportunity for this tender, pre-filled from it, at the
    /// Proposal stage, and links the two. Moves a tender still under review on to Preparing.
    /// </summary>
    [HttpPost("{id:guid}/create-opportunity")]
    public async Task<ActionResult<SalesTenderDetailResponse>> CreateOpportunity(Guid id, CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(cancellationToken);
        var tender = await LoadAsync(id, cancellationToken);
        if (tender.OpportunityId.HasValue)
            throw new BusinessRuleException("This tender already has an opportunity.");
        if (SalesTenderStatuses.IsClosed(tender.Status))
            throw new BusinessRuleException("Reopen the tender before creating an opportunity for it.");

        var opportunity = new SalesOpportunity
        {
            TenantId = TenantId,
            Title = tender.Title,
            Stage = SalesStages.Proposal,
            Probability = SalesStages.DefaultProbability(SalesStages.Proposal),
            ClientId = tender.ClientId,
            CompanyName = tender.ClientId.HasValue ? null : tender.IssuerName,
            ContactName = tender.ContactName,
            ContactEmail = tender.ContactEmail,
            ContactPhone = tender.ContactPhone,
            Source = "Tender",
            Description = tender.Description,
            EstimatedValue = tender.SubmittedValue ?? tender.EstimatedValue,
            Currency = tender.Currency,
            ExpectedCloseDate = tender.SubmissionDeadline,
            OwnerUserId = tender.OwnerUserId,
            OwnerName = tender.OwnerName,
            LastActivityAt = DateTime.UtcNow,
        };
        dbContext.SalesOpportunities.Add(opportunity);
        dbContext.SalesActivities.Add(new SalesActivity
        {
            TenantId = TenantId,
            OpportunityId = opportunity.Id,
            Type = SalesActivityTypes.System,
            Summary = $"Opened from {tender.Type} {tender.Reference ?? tender.Title}.",
            OccurredAt = DateTime.UtcNow,
            LoggedByUserId = UserId,
        });
        tender.OpportunityId = opportunity.Id;
        if (tender.Status is SalesTenderStatuses.Received or SalesTenderStatuses.Reviewing)
            tender.Status = SalesTenderStatuses.Preparing;

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "crm.opportunity.created", nameof(SalesOpportunity), opportunity.Id.ToString(), $"Created opportunity '{opportunity.Title}' from {tender.Type} {tender.Reference ?? tender.Title}.", cancellationToken);
        return Ok(await LoadDetailAsync(id, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(cancellationToken);
        var tender = await LoadAsync(id, cancellationToken);
        if (tender.OpportunityId.HasValue)
            throw new BusinessRuleException("This tender is linked to an opportunity. Mark it cancelled or no-bid instead of deleting it.");
        if (await dbContext.Attachments.AnyAsync(x => x.TenantId == TenantId && x.EntityType == "SalesTender" && x.EntityId == id, cancellationToken))
            throw new BusinessRuleException("Delete this request's documents first, or mark it cancelled to keep them.");

        dbContext.SalesTenders.Remove(tender);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "crm.tender.deleted", nameof(SalesTender), id.ToString(), $"Deleted {tender.Type} '{tender.Title}'.", cancellationToken);
        return NoContent();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

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
            throw new NotFoundException("RFP/RFQ not found.");
    }

    private async Task<SalesTender> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        var tender = await dbContext.SalesTenders.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("RFP/RFQ not found.");
        await EnsureVisibleAsync(tender.OwnerUserId, cancellationToken);
        return tender;
    }

    /// <summary>
    /// Keeps the linked deal in step with the tender's outcome, and logs every status change on the
    /// deal's timeline. A deal already closed the other way is left alone rather than flipped.
    /// </summary>
    private async Task MirrorOntoOpportunityAsync(SalesTender tender, string status, string? notes, string summary, CancellationToken cancellationToken)
    {
        if (!tender.OpportunityId.HasValue) return;
        var opportunity = await dbContext.SalesOpportunities
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == tender.OpportunityId.Value, cancellationToken);
        if (opportunity is null) return;

        if (status == SalesTenderStatuses.Won && !SalesStages.IsClosed(opportunity.Stage))
        {
            opportunity.Stage = SalesStages.Won;
            opportunity.Probability = SalesStages.DefaultProbability(SalesStages.Won);
            opportunity.WonAt = DateTime.UtcNow;
            if (tender.SubmittedValue.HasValue) opportunity.EstimatedValue = tender.SubmittedValue.Value;
            await ProspectClients.PromoteAsync(dbContext, TenantId, opportunity.ClientId, cancellationToken);
        }
        else if (status is SalesTenderStatuses.Lost or SalesTenderStatuses.NoBid or SalesTenderStatuses.Cancelled
            && !SalesStages.IsClosed(opportunity.Stage))
        {
            opportunity.Stage = SalesStages.Lost;
            opportunity.Probability = 0;
            opportunity.LostAt = DateTime.UtcNow;
            opportunity.LostReason = $"{SalesTenderStatuses.Label(status)}: {notes}";
        }
        else if (status == SalesTenderStatuses.Submitted && SalesStages.Rank(opportunity.Stage) < SalesStages.Rank(SalesStages.Negotiation)
            && !SalesStages.IsClosed(opportunity.Stage))
        {
            opportunity.Stage = SalesStages.Proposal;
            if (tender.SubmittedValue.HasValue) opportunity.EstimatedValue = tender.SubmittedValue.Value;
        }

        dbContext.SalesActivities.Add(new SalesActivity
        {
            TenantId = TenantId,
            OpportunityId = opportunity.Id,
            Type = SalesActivityTypes.System,
            Summary = summary,
            OccurredAt = DateTime.UtcNow,
            LoggedByUserId = UserId,
        });
        opportunity.LastActivityAt = DateTime.UtcNow;
    }

    private async Task ApplyAsync(SalesTender tender, UpsertSalesTenderRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new BusinessRuleException("Title is required.");
        if (!request.ClientId.HasValue && string.IsNullOrWhiteSpace(request.IssuerName))
            throw new BusinessRuleException("Pick the client or enter who issued the request.");
        if (request.EstimatedValue < 0 || request.BidBondAmount is < 0)
            throw new BusinessRuleException("Amounts cannot be negative.");
        var received = DateTime.SpecifyKind(request.ReceivedAt ?? tender.ReceivedAt, DateTimeKind.Utc);
        var deadline = DateTime.SpecifyKind(request.SubmissionDeadline, DateTimeKind.Utc);
        if (deadline < received.Date)
            throw new BusinessRuleException("The submission deadline cannot be before the date it was received.");
        DateTime? clarification = request.ClarificationDeadline.HasValue ? DateTime.SpecifyKind(request.ClarificationDeadline.Value, DateTimeKind.Utc) : null;
        if (clarification > deadline)
            throw new BusinessRuleException("The clarification deadline must be on or before the submission deadline.");
        var currency = SalesCurrencyConverter.Normalize(request.Currency);
        if (currency.Length != 0 && (currency.Length != 3 || !currency.All(char.IsAsciiLetterUpper)))
            throw new BusinessRuleException("Use a 3-letter currency code, e.g. KES or USD.");

        if (request.ClientId.HasValue && !await dbContext.Clients.AnyAsync(x => x.TenantId == TenantId && x.Id == request.ClientId.Value, cancellationToken))
            throw new NotFoundException("Client not found.");
        if (request.OwnerUserId.HasValue && request.OwnerUserId != tender.OwnerUserId)
        {
            tender.OwnerName = await SalesCrmAccess.ResolveOwnerNameAsync(dbContext, rolePermissionService, TenantId, request.OwnerUserId.Value, cancellationToken);
            tender.OwnerUserId = request.OwnerUserId.Value;
        }

        tender.Type = SalesTenderTypes.Normalize(request.Type);
        tender.Reference = request.Reference?.Trim();
        tender.Title = request.Title.Trim();
        tender.ClientId = request.ClientId;
        tender.IssuerName = request.IssuerName?.Trim();
        tender.ContactName = request.ContactName?.Trim();
        tender.ContactEmail = request.ContactEmail?.Trim();
        tender.ContactPhone = request.ContactPhone?.Trim();
        tender.ReceivedAt = received;
        tender.ClarificationDeadline = clarification;
        if (tender.SubmissionDeadline != deadline)
            tender.DeadlineReminderStage = 0;
        tender.SubmissionDeadline = deadline;
        tender.SubmissionMethod = request.SubmissionMethod?.Trim();
        tender.BidBondRequired = request.BidBondRequired;
        tender.BidBondAmount = request.BidBondRequired ? request.BidBondAmount : null;
        tender.Description = request.Description?.Trim();
        tender.EstimatedValue = request.EstimatedValue;
        tender.Currency = currency.Length == 0 ? "KES" : currency;
    }

    /// <param name="checkVisibility">False only straight after the caller created or handed over the request, which they may now not own.</param>
    private async Task<SalesTenderDetailResponse> LoadDetailAsync(Guid id, CancellationToken cancellationToken, bool checkVisibility = true)
    {
        var tender = await dbContext.SalesTenders
            .AsNoTracking()
            .Include(x => x.Client)
            .Include(x => x.Opportunity)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("RFP/RFQ not found.");
        if (checkVisibility)
            await EnsureVisibleAsync(tender.OwnerUserId, cancellationToken);
        var converter = await SalesCurrencyConverter.LoadAsync(dbContext, TenantId, cancellationToken);

        return new SalesTenderDetailResponse(
            tender.Id, tender.Type, tender.Reference, tender.Title, tender.Status,
            tender.ClientId, tender.Client?.ClientName, tender.IssuerName,
            tender.ContactName, tender.ContactEmail, tender.ContactPhone,
            tender.ReceivedAt, tender.ClarificationDeadline, tender.SubmissionDeadline,
            tender.SubmissionMethod, tender.BidBondRequired, tender.BidBondAmount,
            tender.Description, tender.EstimatedValue, tender.Currency,
            tender.SubmittedValue, tender.SubmittedAt, tender.DecidedAt, tender.OutcomeNotes,
            tender.OwnerUserId, tender.OwnerName,
            tender.OpportunityId, tender.Opportunity?.Title, tender.Opportunity?.Stage,
            converter.Convert(tender.SubmittedValue ?? tender.EstimatedValue, tender.Currency),
            converter.ReportingCurrency,
            tender.CreatedAt, tender.UpdatedAt);
    }

    private static SalesTenderListItem MapListItem(SalesTender x, SalesCurrencyConverter converter) =>
        new(x.Id, x.Type, x.Reference, x.Title, x.Status,
            x.ClientId, x.Client?.ClientName, x.IssuerName,
            x.ReceivedAt, x.ClarificationDeadline, x.SubmissionDeadline,
            x.EstimatedValue, x.SubmittedValue, x.Currency,
            converter.Convert(x.SubmittedValue ?? x.EstimatedValue, x.Currency),
            x.OwnerUserId, x.OwnerName, x.OpportunityId, x.BidBondRequired);
}

public static class SalesTenderTypes
{
    public const string Rfp = "RFP";
    public const string Rfq = "RFQ";

    public static string Normalize(string? type) => type?.Trim().ToUpperInvariant() switch
    {
        Rfp => Rfp,
        Rfq => Rfq,
        _ => throw new BusinessRuleException("Type must be RFP or RFQ."),
    };
}

public static class SalesTenderStatuses
{
    public const string Received = "Received";
    public const string Reviewing = "Reviewing";
    public const string Preparing = "Preparing";
    public const string Submitted = "Submitted";
    public const string Won = "Won";
    public const string Lost = "Lost";
    public const string NoBid = "NoBid";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlyList<string> All = [Received, Reviewing, Preparing, Submitted, Won, Lost, NoBid, Cancelled];

    /// <summary>Still in play: waiting on us (before submission) or on the issuer (after it).</summary>
    public static readonly string[] OpenStatuses = [Received, Reviewing, Preparing, Submitted];

    public static string Normalize(string? status) =>
        All.FirstOrDefault(x => string.Equals(x, status?.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? throw new BusinessRuleException($"Unknown RFP/RFQ status '{status}'.");

    public static bool IsClosed(string status) => status is Won or Lost or NoBid or Cancelled;

    public static bool NeedsReason(string status) => status is Lost or NoBid or Cancelled;

    public static string Label(string status) => status == NoBid ? "No bid" : status;
}

// ── Request / response records ───────────────────────────────────────────────

public sealed record UpsertSalesTenderRequest(
    [StringLength(10)] string Type,
    [StringLength(120)] string? Reference,
    [StringLength(200)] string Title,
    Guid? ClientId,
    [StringLength(200)] string? IssuerName,
    [StringLength(150)] string? ContactName,
    [StringLength(256)] string? ContactEmail,
    [StringLength(50)] string? ContactPhone,
    DateTime? ReceivedAt,
    DateTime? ClarificationDeadline,
    DateTime SubmissionDeadline,
    [StringLength(120)] string? SubmissionMethod,
    bool BidBondRequired,
    decimal? BidBondAmount,
    [StringLength(4000)] string? Description,
    decimal EstimatedValue,
    [StringLength(10)] string? Currency,
    Guid? OwnerUserId);

public sealed record ChangeSalesTenderStatusRequest(
    [StringLength(40)] string Status,
    [StringLength(1000)] string? Notes,
    decimal? SubmittedValue);

public sealed record SalesTenderListItem(
    Guid Id, string Type, string? Reference, string Title, string Status,
    Guid? ClientId, string? ClientName, string? IssuerName,
    DateTime ReceivedAt, DateTime? ClarificationDeadline, DateTime SubmissionDeadline,
    decimal EstimatedValue, decimal? SubmittedValue, string Currency, decimal? ConvertedValue,
    Guid? OwnerUserId, string? OwnerName, Guid? OpportunityId, bool BidBondRequired);

public sealed record SalesTenderDetailResponse(
    Guid Id, string Type, string? Reference, string Title, string Status,
    Guid? ClientId, string? ClientName, string? IssuerName,
    string? ContactName, string? ContactEmail, string? ContactPhone,
    DateTime ReceivedAt, DateTime? ClarificationDeadline, DateTime SubmissionDeadline,
    string? SubmissionMethod, bool BidBondRequired, decimal? BidBondAmount,
    string? Description, decimal EstimatedValue, string Currency,
    decimal? SubmittedValue, DateTime? SubmittedAt, DateTime? DecidedAt, string? OutcomeNotes,
    Guid? OwnerUserId, string? OwnerName,
    Guid? OpportunityId, string? OpportunityTitle, string? OpportunityStage,
    decimal? ConvertedValue, string ReportingCurrency,
    DateTime CreatedAt, DateTime? UpdatedAt);
