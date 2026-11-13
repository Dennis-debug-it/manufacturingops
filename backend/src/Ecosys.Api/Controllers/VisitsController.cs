using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// Visitor Management: visits raised ahead by hosts or at the gate, host approval, gate checks,
/// check-in and check-out by badge, who is on site now (the emergency roll call), and the register.
/// Gate staff need <see cref="PermissionNames.VisitorsGate"/>; any staff member can host a visit.
/// </summary>
[ApiController]
[Authorize]
[Route("api/visitors")]
public sealed class VisitsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IOrgScopeService orgScopeService,
    IVisitorService visitorService,
    IFactorySettingsService settingsService,
    IAuditLogService auditLogService,
    IVisitorBadgePdfRenderer? badgeRenderer = null) : TenantAwareControllerBase(tenantContext)
{
    private bool CanManage => IsAdmin || HasPermission(PermissionNames.VisitorsManage);
    private bool CanGate => CanManage || HasPermission(PermissionNames.VisitorsGate);
    private VisitActor Actor => new(UserId, UserDisplayName, CanManage);

    /// <summary>What the person may do here, so the screen shows the gate desk or only "my visits".</summary>
    [HttpGet("me")]
    public ActionResult<VisitorAccessResponse> Me() => Ok(new VisitorAccessResponse(CanGate, CanManage));

    /// <summary>
    /// Today's board: visits expected today or still open, with each person's state. Gate staff see
    /// every factory they may see; other staff see the visits they host.
    /// </summary>
    [HttpGet("board")]
    public async Task<ActionResult<VisitBoardResponse>> Board([FromQuery] Guid? factoryId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var dayStart = EatDayStart(now);
        var dayEnd = dayStart.AddDays(1);
        var query = await ScopedVisitsAsync(factoryId, cancellationToken);
        var visits = await query
            .Where(x => x.Status == VisitStatuses.OnSite
                || x.Status == VisitStatuses.PendingApproval
                || (x.ExpectedFrom < dayEnd && x.ExpectedTo >= dayStart && x.Status != VisitStatuses.Cancelled && x.Status != VisitStatuses.Rejected)
                || (x.CompletedAt != null && x.CompletedAt >= dayStart))
            .OrderBy(x => x.ExpectedFrom)
            .Take(500)
            .ToListAsync(cancellationToken);
        var rows = await MapAsync(visits, now, cancellationToken);
        var people = rows.SelectMany(x => x.Attendees).ToList();
        return Ok(new VisitBoardResponse(
            rows,
            people.Count(x => x.State == AttendeeStates.OnSite || x.State == AttendeeStates.Overstayed),
            people.Count(x => x.State == AttendeeStates.Expected),
            rows.Count(x => x.Status == VisitStatuses.PendingApproval),
            people.Count(x => x.State == AttendeeStates.Overstayed),
            people.Count(x => x.State == AttendeeStates.Left && x.CheckedOutAt >= dayStart)));
    }

    /// <summary>The visitor register: every visit in a period, searchable by name, company, phone, number or vehicle.</summary>
    [HttpGet("visits")]
    public async Task<ActionResult<IReadOnlyList<VisitRow>>> Register(
        [FromQuery] Guid? factoryId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? status, [FromQuery] string? q, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var start = from ?? now.AddDays(-30);
        var end = to ?? now.AddDays(30);
        var query = (await ScopedVisitsAsync(factoryId, cancellationToken)).Where(x => x.ExpectedFrom < end && x.ExpectedTo >= start);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            var digits = new string(term.Where(char.IsDigit).ToArray());
            var compact = term.Replace(" ", "");
            query = query.Where(x => x.VisitNumber.ToLower().Contains(term)
                || (x.VehicleRegistration != null && x.VehicleRegistration.ToLower().Contains(compact))
                || x.HostName.ToLower().Contains(term)
                || x.Attendees.Any(a => a.Visitor!.FullName.ToLower().Contains(term)
                    || (a.Visitor.Company != null && a.Visitor.Company.ToLower().Contains(term))
                    || (digits.Length >= 4 && a.Visitor.Phone != null && a.Visitor.Phone.Contains(digits))));
        }

        var visits = await query.OrderByDescending(x => x.ExpectedFrom).Take(300).ToListAsync(cancellationToken);
        return Ok(await MapAsync(visits, now, cancellationToken));
    }

    [HttpGet("visits/{id:guid}")]
    public async Task<ActionResult<VisitRow>> Get(Guid id, CancellationToken cancellationToken)
    {
        var visit = await LoadVisibleAsync(id, cancellationToken);
        return Ok((await MapAsync([visit], DateTime.UtcNow, cancellationToken)).Single());
    }

    [HttpPost("visits")]
    public async Task<ActionResult<VisitRow>> Create([FromBody] CreateVisitRequest request, CancellationToken cancellationToken)
    {
        var hostId = request.HostUserId ?? UserId;
        if ((hostId != UserId || request.WalkIn) && !CanGate)
            throw new ForbiddenException("Only gate staff can register walk-ins or visits for another host. Invite your own visitors with yourself as host.");
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, request.FactoryId, cancellationToken);
        var visit = await visitorService.CreateAsync(TenantId, new NewVisit(
            request.FactoryId, request.Purpose,
            (request.Visitors ?? []).Select(x => new VisitorDetails(x.FullName ?? "", x.Phone, x.Email, x.Company, x.IdType, x.IdNumber, x.ContractorId, x.VisitorId)).ToList(),
            hostId, request.ExpectedFrom, request.ExpectedTo, request.ZoneIds, request.WorkOrderId, request.WorkPermitId, request.ContractorId,
            request.VehicleRegistration, request.Notes, request.WalkIn), Actor, cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "visitors.visit.created", nameof(Visit), visit.Id.ToString(),
            $"{(request.WalkIn ? "Walk-in" : "Pre-registered")} visit {visit.VisitNumber} for {visit.HostName} ({visit.Status}).", cancellationToken);
        return Ok(await GetRowAsync(visit.Id, cancellationToken));
    }

    [HttpPost("visits/{id:guid}/decide")]
    public async Task<ActionResult<VisitRow>> Decide(Guid id, [FromBody] DecideVisitRequest request, CancellationToken cancellationToken)
    {
        await LoadVisibleAsync(id, cancellationToken);
        var visit = await visitorService.DecideAsync(TenantId, id, request.Approve, request.Note, Actor, cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, request.Approve ? "visitors.visit.approved" : "visitors.visit.rejected", nameof(Visit), id.ToString(),
            $"Visit {visit.VisitNumber} {(request.Approve ? "approved" : "turned down")}{(request.Note is null ? "" : $": {request.Note}")}.", cancellationToken);
        return Ok(await GetRowAsync(id, cancellationToken));
    }

    [HttpPost("visits/{id:guid}/cancel")]
    public async Task<ActionResult<VisitRow>> Cancel(Guid id, [FromBody] CancelVisitRequest request, CancellationToken cancellationToken)
    {
        var existing = await LoadVisibleAsync(id, cancellationToken);
        if (existing.HostUserId != UserId && !CanGate) throw new ForbiddenException("Only the host or gate staff can cancel a visit.");
        var visit = await visitorService.CancelAsync(TenantId, id, request.Reason, Actor, cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "visitors.visit.cancelled", nameof(Visit), id.ToString(), $"Visit {visit.VisitNumber} cancelled.", cancellationToken);
        return Ok(await GetRowAsync(id, cancellationToken));
    }

    /// <summary>The gate checks for one person with what the gate has ticked so far; nothing is saved.</summary>
    [HttpPost("attendees/{id:guid}/check")]
    public async Task<ActionResult<GateCheckResult>> Check(Guid id, [FromBody] GateConfirmationRequest request, CancellationToken cancellationToken)
    {
        EnsureGate();
        await EnsureAttendeeVisibleAsync(id, cancellationToken);
        return Ok(await visitorService.CheckAsync(TenantId, id, request.ToConfirmation(), DateTime.UtcNow, cancellationToken));
    }

    [HttpPost("attendees/{id:guid}/check-in")]
    public async Task<ActionResult<VisitRow>> CheckIn(Guid id, [FromBody] GateConfirmationRequest request, CancellationToken cancellationToken)
    {
        EnsureGate();
        await EnsureAttendeeVisibleAsync(id, cancellationToken);
        var attendee = await visitorService.CheckInAsync(TenantId, id, request.ToConfirmation(), Actor, cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, attendee.OverrideReason is null ? "visitors.checked-in" : "visitors.checked-in.override", nameof(VisitAttendee), id.ToString(),
            $"{attendee.Visitor!.FullName} checked in on {attendee.Visit!.VisitNumber}{(attendee.OverrideReason is null ? "" : $" (override: {attendee.OverrideReason})")}.", cancellationToken);
        return Ok(await GetRowAsync(attendee.VisitId, cancellationToken));
    }

    [HttpPost("attendees/{id:guid}/check-out")]
    public async Task<ActionResult<VisitRow>> CheckOut(Guid id, CancellationToken cancellationToken)
    {
        EnsureGate();
        await EnsureAttendeeVisibleAsync(id, cancellationToken);
        var attendee = await visitorService.CheckOutAsync(TenantId, id, Actor, cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "visitors.checked-out", nameof(VisitAttendee), id.ToString(),
            $"{attendee.Visitor!.FullName} left ({attendee.Visit!.VisitNumber}).", cancellationToken);
        return Ok(await GetRowAsync(attendee.VisitId, cancellationToken));
    }

    /// <summary>Printable badges for the people on a visit (or one of them), each with a QR code to scan out at the gate.</summary>
    [HttpGet("visits/{id:guid}/badges")]
    public async Task<IActionResult> Badges(Guid id, [FromQuery] Guid? attendeeId, CancellationToken cancellationToken)
    {
        EnsureGate();
        var visit = await LoadVisibleAsync(id, cancellationToken);
        if (visit.Status is not (VisitStatuses.Approved or VisitStatuses.OnSite)) throw new BusinessRuleException("Badges are printed for approved visits.");
        var row = (await MapAsync([visit], DateTime.UtcNow, cancellationToken)).Single();
        var company = await dbContext.Tenants.Where(x => x.Id == TenantId).Select(x => x.CompanyName).SingleOrDefaultAsync(cancellationToken) ?? string.Empty;
        var escortZones = await dbContext.VisitZones.AsNoTracking().Where(x => x.VisitId == id && x.AccessZone!.RequiresEscort).Select(x => x.AccessZone!.Name).ToListAsync(cancellationToken);
        var badges = row.Attendees
            .Where(x => (attendeeId == null || x.Id == attendeeId) && x.State is AttendeeStates.Expected or AttendeeStates.OnSite or AttendeeStates.Overstayed)
            .Select(x => new VisitorBadge(company, row.FactoryName, x.BadgeNumber, $"VISITOR:{x.BadgeToken}", x.FullName, x.Company, row.HostName, row.Purpose,
                row.ExpectedTo.AddHours(3), row.Zones.Select(z => z.Name).ToList(),
                escortZones.Count == 0 ? null : $"Escort required in: {string.Join(", ", escortZones)}"))
            .ToList();
        var pdf = (badgeRenderer ?? throw new InvalidOperationException("Badge printing is not set up.")).Render(badges);
        return File(pdf, "application/pdf", $"badges-{row.VisitNumber}.pdf");
    }

    /// <summary>A scanned badge: who it is, their host and zones, and whether they are inside.</summary>
    [HttpGet("badges/{token}")]
    public async Task<ActionResult<BadgeResponse>> Badge(string token, CancellationToken cancellationToken)
    {
        EnsureGate();
        var attendee = await visitorService.FindBadgeAsync(TenantId, token, cancellationToken)
            ?? throw new NotFoundException("This badge is not one of ours.");
        var visible = await orgScopeService.VisibleNodesAsync(TenantId, UserId, null, cancellationToken);
        if (visible is not null && !visible.Contains(attendee.Visit!.FactoryId)) throw new NotFoundException("This badge is not one of ours.");
        var row = await GetRowAsync(attendee.VisitId, cancellationToken);
        return Ok(new BadgeResponse(row.Attendees.Single(x => x.Id == attendee.Id), row));
    }

    /// <summary>Everyone inside now: the emergency roll call list.</summary>
    [HttpGet("on-site")]
    public async Task<ActionResult<IReadOnlyList<OnSiteRow>>> OnSite([FromQuery] Guid? factoryId, CancellationToken cancellationToken)
    {
        EnsureGate();
        var nodes = await orgScopeService.VisibleNodesAsync(TenantId, UserId, factoryId, cancellationToken);
        var now = DateTime.UtcNow;
        var grace = (await settingsService.GetOrCreateAsync(TenantId, cancellationToken)).VisitOverstayGraceMinutes;
        var rows = await dbContext.VisitAttendees.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.CheckedInAt != null && x.CheckedOutAt == null && (nodes == null || nodes.Contains(x.Visit!.FactoryId)))
            .OrderBy(x => x.Visit!.Factory!.Name).ThenBy(x => x.Visitor!.FullName)
            .Select(x => new
            {
                x.Id, x.VisitId, x.Visit!.VisitNumber, x.Visit.FactoryId, Factory = x.Visit.Factory!.Name, x.Visitor!.FullName, x.Visitor.Company, x.Visitor.Phone,
                x.Visit.HostName, x.Visit.Purpose, x.Visit.VehicleRegistration, x.BadgeNumber, CheckedInAt = x.CheckedInAt!.Value, x.Visit.ExpectedTo, x.EscortName,
                x.AccountedForAt, x.AccountedForByName, Zones = x.Visit.Zones.Select(z => z.AccessZone!.Name).ToList(),
            })
            .ToListAsync(cancellationToken);
        return Ok(rows.Select(x => new OnSiteRow(x.Id, x.VisitId, x.VisitNumber, x.FactoryId, x.Factory, x.FullName, x.Company, x.Phone, x.HostName, x.Purpose,
            x.VehicleRegistration, x.BadgeNumber, x.CheckedInAt, x.ExpectedTo, now > x.ExpectedTo.AddMinutes(grace), x.EscortName, x.Zones, x.AccountedForAt, x.AccountedForByName)).ToList());
    }

    [HttpPost("attendees/{id:guid}/accounted")]
    public async Task<IActionResult> Accounted(Guid id, [FromBody] AccountedRequest request, CancellationToken cancellationToken)
    {
        EnsureGate();
        await EnsureAttendeeVisibleAsync(id, cancellationToken);
        await visitorService.MarkAccountedAsync(TenantId, id, request.Accounted, Actor, cancellationToken);
        return NoContent();
    }

    /// <summary>Starts a new roll call at a factory (or every factory the person can see).</summary>
    [HttpPost("roll-call/reset")]
    public async Task<ActionResult<int>> ResetRollCall([FromBody] ResetRollCallRequest request, CancellationToken cancellationToken)
    {
        EnsureGate();
        var nodes = await orgScopeService.VisibleNodesAsync(TenantId, UserId, request.FactoryId, cancellationToken);
        var factories = await dbContext.OrgNodes.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.Level == OrgNodeLevels.Factory && (nodes == null || nodes.Contains(x.Id)))
            .Select(x => x.Id).ToListAsync(cancellationToken);
        var cleared = await visitorService.ResetRollCallAsync(TenantId, factories, cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "visitors.roll-call.started", "VisitAttendee", request.FactoryId?.ToString() ?? "all", "Roll call started.", cancellationToken);
        return Ok(cleared);
    }

    /// <summary>Finds returning visitors by name, company, phone or ID number (matched by hash; never shown in full).</summary>
    [HttpGet("people")]
    public async Task<ActionResult<IReadOnlyList<VisitorRow>>> People([FromQuery] string? q, [FromQuery] bool blockedOnly, CancellationToken cancellationToken)
    {
        EnsureGate();
        var query = dbContext.Visitors.AsNoTracking().Where(x => x.TenantId == TenantId && x.AnonymisedAt == null);
        if (blockedOnly) query = query.Where(x => x.IsBlocked);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            var digits = new string(term.Where(char.IsDigit).ToArray());
            string? idHash = null;
            try { idHash = VisitorIdentity.Hash(TenantId, q); } catch (BusinessRuleException) { }
            query = query.Where(x => x.FullName.ToLower().Contains(term)
                || (x.Company != null && x.Company.ToLower().Contains(term))
                || (digits.Length >= 4 && x.Phone != null && x.Phone.Contains(digits))
                || (idHash != null && x.IdNumberHash == idHash));
        }
        else if (!blockedOnly)
        {
            query = query.Where(x => x.LastVisitAt != null);
        }

        var people = await query.OrderByDescending(x => x.LastVisitAt).Take(50)
            .Select(x => new VisitorRow(x.Id, x.FullName, x.Phone, x.Email, x.Company, x.IdType, x.IdNumberMasked, x.ContractorId, x.Contractor != null ? x.Contractor.Name : null,
                x.IsBlocked, x.BlockedReason, x.BlockedByName, x.LastVisitAt))
            .ToListAsync(cancellationToken);
        return Ok(people);
    }

    [HttpPost("people/{id:guid}/block")]
    public async Task<ActionResult<VisitorRow>> Block(Guid id, [FromBody] BlockVisitorRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) throw new ForbiddenException("Only visitor managers can block or unblock visitors.");
        var visitor = await visitorService.SetBlockedAsync(TenantId, id, request.Blocked, request.Reason, Actor, cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, request.Blocked ? "visitors.blocked" : "visitors.unblocked", nameof(Visitor), id.ToString(),
            request.Blocked ? $"{visitor.FullName} blocked: {visitor.BlockedReason}" : $"{visitor.FullName} unblocked.", cancellationToken);
        return Ok(new VisitorRow(visitor.Id, visitor.FullName, visitor.Phone, visitor.Email, visitor.Company, visitor.IdType, visitor.IdNumberMasked, visitor.ContractorId, null,
            visitor.IsBlocked, visitor.BlockedReason, visitor.BlockedByName, visitor.LastVisitAt));
    }

    /// <summary>Staff who can host or escort, for the pickers.</summary>
    [HttpGet("staff")]
    public async Task<ActionResult<IReadOnlyList<StaffOption>>> Staff([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var query = dbContext.Users.AsNoTracking().Where(x => x.TenantId == TenantId && x.IsActive);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(x => x.FullName.ToLower().Contains(term) || x.Email.ToLower().Contains(term));
        }

        return Ok(await query.OrderBy(x => x.FullName).Take(50).Select(x => new StaffOption(x.Id, x.FullName, x.JobTitle)).ToListAsync(cancellationToken));
    }

    /// <summary>Visitor numbers for a period: per day, by purpose, time on site, overstays and gate overrides.</summary>
    [HttpGet("report")]
    public async Task<ActionResult<VisitorReport>> Report([FromQuery] Guid? factoryId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        EnsureGate();
        var end = to ?? DateTime.UtcNow;
        var start = from ?? end.AddDays(-30);
        var nodes = await orgScopeService.VisibleNodesAsync(TenantId, UserId, factoryId, cancellationToken);
        var grace = (await settingsService.GetOrCreateAsync(TenantId, cancellationToken)).VisitOverstayGraceMinutes;
        var entries = await dbContext.VisitAttendees.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.CheckedInAt != null && x.CheckedInAt >= start && x.CheckedInAt < end && (nodes == null || nodes.Contains(x.Visit!.FactoryId)))
            .Select(x => new
            {
                In = x.CheckedInAt!.Value, Out = x.CheckedOutAt, x.Visit!.Purpose, x.Visit.ExpectedTo, x.OverrideReason, x.OverriddenByName,
                x.Visitor!.FullName, x.Visit.VisitNumber, x.Visit.ContractorId,
            })
            .ToListAsync(cancellationToken);
        var visits = await dbContext.Visits.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.ExpectedFrom >= start && x.ExpectedFrom < end && (nodes == null || nodes.Contains(x.FactoryId)))
            .Select(x => new { x.Status, Arrived = x.Attendees.Any(a => a.CheckedInAt != null), x.ExpectedTo })
            .ToListAsync(cancellationToken);
        var finished = entries.Where(x => x.Out != null).Select(x => (x.Out!.Value - x.In).TotalMinutes).ToList();
        return Ok(new VisitorReport(
            start, end,
            entries.Count,
            entries.Count(x => x.ContractorId != null),
            finished.Count == 0 ? null : (int)Math.Round(finished.Average()),
            entries.Count(x => (x.Out ?? DateTime.UtcNow) > x.ExpectedTo.AddMinutes(grace)),
            visits.Count(x => !x.Arrived && x.ExpectedTo < DateTime.UtcNow && x.Status is VisitStatuses.Approved or VisitStatuses.Completed),
            entries.GroupBy(x => x.In.AddHours(3).Date).OrderBy(x => x.Key).Select(x => new DayCount(x.Key, x.Count())).ToList(),
            entries.GroupBy(x => x.Purpose).OrderByDescending(x => x.Count()).Select(x => new PurposeCount(x.Key, x.Count())).ToList(),
            entries.Where(x => x.OverrideReason != null).OrderByDescending(x => x.In)
                .Select(x => new OverrideRow(x.In, x.VisitNumber, x.FullName, x.OverrideReason!, x.OverriddenByName)).ToList()));
    }

    private void EnsureGate()
    {
        if (!CanGate) throw new ForbiddenException("This is for gate and security staff.");
    }

    private async Task<IQueryable<Visit>> ScopedVisitsAsync(Guid? factoryId, CancellationToken cancellationToken)
    {
        var nodes = await orgScopeService.VisibleNodesAsync(TenantId, UserId, factoryId, cancellationToken);
        var query = dbContext.Visits.AsNoTracking().Where(x => x.TenantId == TenantId && (nodes == null || nodes.Contains(x.FactoryId)));
        if (!CanGate)
        {
            var me = UserId;
            query = query.Where(x => x.HostUserId == me);
        }

        return query;
    }

    private async Task<Visit> LoadVisibleAsync(Guid id, CancellationToken cancellationToken) =>
        await (await ScopedVisitsAsync(null, cancellationToken)).SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
        ?? throw new NotFoundException("Visit not found.");

    private async Task EnsureAttendeeVisibleAsync(Guid attendeeId, CancellationToken cancellationToken)
    {
        var visitId = await dbContext.VisitAttendees.AsNoTracking().Where(x => x.TenantId == TenantId && x.Id == attendeeId).Select(x => (Guid?)x.VisitId).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Visitor not found on any visit.");
        await LoadVisibleAsync(visitId, cancellationToken);
    }

    private async Task<VisitRow> GetRowAsync(Guid visitId, CancellationToken cancellationToken)
    {
        var visit = await dbContext.Visits.AsNoTracking().SingleAsync(x => x.TenantId == TenantId && x.Id == visitId, cancellationToken);
        return (await MapAsync([visit], DateTime.UtcNow, cancellationToken)).Single();
    }

    private async Task<IReadOnlyList<VisitRow>> MapAsync(IReadOnlyList<Visit> visits, DateTime now, CancellationToken cancellationToken)
    {
        if (visits.Count == 0) return [];
        var ids = visits.Select(x => x.Id).ToList();
        var grace = (await settingsService.GetOrCreateAsync(TenantId, cancellationToken)).VisitOverstayGraceMinutes;
        var attendees = (await dbContext.VisitAttendees.AsNoTracking().Include(x => x.Visitor).ThenInclude(x => x!.Contractor)
                .Where(x => ids.Contains(x.VisitId)).OrderBy(x => x.BadgeNumber).ToListAsync(cancellationToken))
            .ToLookup(x => x.VisitId);
        var zones = (await dbContext.VisitZones.AsNoTracking().Where(x => ids.Contains(x.VisitId))
                .OrderBy(x => x.AccessZone!.DisplayOrder)
                .Select(x => new { x.VisitId, Zone = new ZoneRef(x.AccessZoneId, x.AccessZone!.Name, x.AccessZone.Level) })
                .ToListAsync(cancellationToken))
            .ToLookup(x => x.VisitId, x => x.Zone);
        var factoryIds = visits.Select(x => x.FactoryId).Distinct().ToList();
        var factories = await dbContext.OrgNodes.AsNoTracking().Where(x => factoryIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var jobIds = visits.Where(x => x.WorkOrderId != null).Select(x => x.WorkOrderId!.Value).ToList();
        var jobs = await dbContext.WorkOrders.AsNoTracking().Where(x => jobIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.WorkOrderNumber, cancellationToken);
        var permitIds = visits.Where(x => x.WorkPermitId != null).Select(x => x.WorkPermitId!.Value).ToList();
        var permits = await dbContext.WorkPermits.AsNoTracking().Where(x => permitIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.PermitNumber, cancellationToken);
        var contractorIds = visits.Where(x => x.ContractorId != null).Select(x => x.ContractorId!.Value).ToList();
        var contractors = await dbContext.Contractors.AsNoTracking().Where(x => contractorIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var me = UserId;

        return visits.Select(v =>
        {
            var overdue = now > v.ExpectedTo.AddMinutes(grace);
            var people = attendees[v.Id].Select(a => new AttendeeRow(
                a.Id, a.VisitorId, a.Visitor!.FullName, a.Visitor.Company, a.Visitor.Phone, a.Visitor.IdType, a.Visitor.IdNumberMasked, a.Visitor.IsBlocked, a.Visitor.BlockedReason,
                a.Visitor.Contractor?.Name, a.BadgeNumber, a.BadgeToken,
                a.CheckedOutAt is not null ? AttendeeStates.Left
                    : a.CheckedInAt is not null ? (overdue ? AttendeeStates.Overstayed : AttendeeStates.OnSite)
                    : v.Status is VisitStatuses.Cancelled or VisitStatuses.Rejected ? AttendeeStates.Cancelled
                    : overdue || v.Status == VisitStatuses.Completed ? AttendeeStates.NoShow
                    : AttendeeStates.Expected,
                a.CheckedInAt, a.CheckedInByName, a.CheckedOutAt, a.CheckedOutByName, a.IdVerified, a.InductionConfirmed, a.PpeConfirmed, a.EscortName,
                a.OverrideReason, a.OverriddenByName, a.AccountedForAt)).ToList();
            return new VisitRow(
                v.Id, v.VisitNumber, v.FactoryId, factories.GetValueOrDefault(v.FactoryId) ?? "", v.Purpose, v.HostUserId, v.HostName, v.HostUserId == me,
                v.ExpectedFrom, v.ExpectedTo, v.Status, v.Source, v.WorkOrderId, v.WorkOrderId is { } j ? jobs.GetValueOrDefault(j) : null,
                v.WorkPermitId, v.WorkPermitId is { } p ? permits.GetValueOrDefault(p) : null, v.ContractorId, v.ContractorId is { } c ? contractors.GetValueOrDefault(c) : null,
                v.VehicleRegistration, v.Notes, v.CreatedByName, v.DecidedByName, v.DecidedAt, v.DecisionNote, v.CompletedAt, zones[v.Id].ToList(), people);
        }).ToList();
    }

    /// <summary>Start of today in East Africa Time, as UTC.</summary>
    private static DateTime EatDayStart(DateTime utcNow) => DateTime.SpecifyKind(utcNow.AddHours(3).Date.AddHours(-3), DateTimeKind.Utc);
}

public static class AttendeeStates
{
    public const string Expected = "Expected";
    public const string OnSite = "OnSite";
    public const string Overstayed = "Overstayed";
    public const string Left = "Left";
    public const string NoShow = "NoShow";
    public const string Cancelled = "Cancelled";
}

public sealed record VisitorAccessResponse(bool CanGate, bool CanManage);

public sealed record ZoneRef(Guid Id, string Name, string Level);

public sealed record AttendeeRow(
    Guid Id, Guid VisitorId, string FullName, string? Company, string? Phone, string? IdType, string? IdNumberMasked, bool IsBlocked, string? BlockedReason,
    string? ContractorName, string BadgeNumber, string BadgeToken, string State,
    DateTime? CheckedInAt, string? CheckedInByName, DateTime? CheckedOutAt, string? CheckedOutByName,
    bool IdVerified, bool InductionConfirmed, bool PpeConfirmed, string? EscortName, string? OverrideReason, string? OverriddenByName, DateTime? AccountedForAt);

public sealed record VisitRow(
    Guid Id, string VisitNumber, Guid FactoryId, string FactoryName, string Purpose, Guid HostUserId, string HostName, bool IAmHost,
    DateTime ExpectedFrom, DateTime ExpectedTo, string Status, string Source, Guid? WorkOrderId, string? WorkOrderNumber,
    Guid? WorkPermitId, string? PermitNumber, Guid? ContractorId, string? ContractorName, string? VehicleRegistration, string? Notes,
    string CreatedByName, string? DecidedByName, DateTime? DecidedAt, string? DecisionNote, DateTime? CompletedAt,
    IReadOnlyList<ZoneRef> Zones, IReadOnlyList<AttendeeRow> Attendees);

public sealed record VisitBoardResponse(IReadOnlyList<VisitRow> Visits, int OnSite, int Expected, int AwaitingApproval, int Overstayed, int LeftToday);

public sealed record BadgeResponse(AttendeeRow Attendee, VisitRow Visit);

public sealed record OnSiteRow(
    Guid AttendeeId, Guid VisitId, string VisitNumber, Guid FactoryId, string FactoryName, string FullName, string? Company, string? Phone, string HostName, string Purpose,
    string? VehicleRegistration, string BadgeNumber, DateTime CheckedInAt, DateTime ExpectedTo, bool Overstayed, string? EscortName, IReadOnlyList<string> Zones,
    DateTime? AccountedForAt, string? AccountedForByName);

public sealed record VisitorRow(
    Guid Id, string FullName, string? Phone, string? Email, string? Company, string? IdType, string? IdNumberMasked, Guid? ContractorId, string? ContractorName,
    bool IsBlocked, string? BlockedReason, string? BlockedByName, DateTime? LastVisitAt);

public sealed record StaffOption(Guid Id, string FullName, string? JobTitle);

public sealed record DayCount(DateTime Day, int Count);

public sealed record PurposeCount(string Purpose, int Count);

public sealed record OverrideRow(DateTime At, string VisitNumber, string FullName, string Reason, string? ByName);

public sealed record VisitorReport(
    DateTime From, DateTime To, int Entries, int ContractorEntries, int? AverageMinutesOnSite, int Overstays, int NoShowVisits,
    IReadOnlyList<DayCount> PerDay, IReadOnlyList<PurposeCount> ByPurpose, IReadOnlyList<OverrideRow> Overrides);

public sealed record VisitorDetailsRequest(
    string? FullName, string? Phone = null, string? Email = null, string? Company = null, string? IdType = null, string? IdNumber = null,
    Guid? ContractorId = null, Guid? VisitorId = null);

public sealed record CreateVisitRequest(
    Guid FactoryId,
    string Purpose,
    IReadOnlyList<VisitorDetailsRequest>? Visitors,
    Guid? HostUserId = null,
    DateTime? ExpectedFrom = null,
    DateTime? ExpectedTo = null,
    IReadOnlyList<Guid>? ZoneIds = null,
    Guid? WorkOrderId = null,
    Guid? WorkPermitId = null,
    Guid? ContractorId = null,
    string? VehicleRegistration = null,
    string? Notes = null,
    bool WalkIn = false);

public sealed record DecideVisitRequest(bool Approve, string? Note = null);

public sealed record CancelVisitRequest(string? Reason = null);

public sealed record GateConfirmationRequest(bool IdVerified = false, bool PpeConfirmed = false, bool RecordInduction = false, Guid? EscortUserId = null, string? OverrideReason = null)
{
    public GateConfirmation ToConfirmation() => new(IdVerified, PpeConfirmed, RecordInduction, EscortUserId, OverrideReason);
}

public sealed record AccountedRequest(bool Accounted);

public sealed record ResetRollCallRequest(Guid? FactoryId = null);

public sealed record BlockVisitorRequest(bool Blocked, string? Reason = null);
