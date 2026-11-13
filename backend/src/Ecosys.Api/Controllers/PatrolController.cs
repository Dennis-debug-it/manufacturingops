using System.Text.Json;
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
/// Security Patrol: the guard's app (today's patrols, scans, incidents, panic, handover) and the
/// supervisor's board, run details, security incidents and reports.
/// </summary>
[ApiController]
[Authorize]
[Route("api/security")]
public sealed class PatrolController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IOrgScopeService orgScopeService,
    IPatrolService patrolService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    private bool CanManage => IsAdmin || HasPermission(PermissionNames.SecurityManage);
    private bool CanPatrol => CanManage || HasPermission(PermissionNames.SecurityPatrol);
    private PatrolActor Actor => new(UserId, UserDisplayName);

    [HttpGet("me")]
    public ActionResult<PatrolAccessResponse> Me() => Ok(new PatrolAccessResponse(CanPatrol, CanManage));

    // ── Guard ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Everything the guard's phone keeps for a shift with no signal: the patrols due in the next 12 hours (and
    /// those running), their routes, every checkpoint with its tag, and the routes for a patrol of their own.
    /// </summary>
    [HttpGet("guard")]
    public async Task<ActionResult<GuardBundle>> Guard([FromQuery] Guid? factoryId, CancellationToken cancellationToken)
    {
        EnsurePatrol();
        var nodes = await orgScopeService.VisibleNodesAsync(TenantId, UserId, factoryId, cancellationToken);
        var now = DateTime.UtcNow;
        var me = UserId;
        var runs = await dbContext.PatrolRuns.AsNoTracking().Include(x => x.Route).Include(x => x.Factory)
            .Where(x => x.TenantId == TenantId && (nodes == null || nodes.Contains(x.FactoryId))
                && (x.Status == PatrolRunStatuses.Due || x.Status == PatrolRunStatuses.InProgress)
                && x.DueFrom <= now.AddHours(12) && (x.GuardUserId == null || x.GuardUserId == me))
            .OrderBy(x => x.DueFrom).Take(50).ToListAsync(cancellationToken);
        var runIds = runs.Select(x => x.Id).ToList();
        var scans = (await dbContext.CheckpointScans.AsNoTracking().Where(x => x.RunId != null && runIds.Contains(x.RunId.Value)).ToListAsync(cancellationToken))
            .ToLookup(x => x.RunId!.Value);
        var routes = await dbContext.PatrolRoutes.AsNoTracking().Include(x => x.Checkpoints)
            .Where(x => x.TenantId == TenantId && x.IsActive && (nodes == null || nodes.Contains(x.FactoryId)))
            .OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var checkpoints = await dbContext.Checkpoints.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.IsActive && (nodes == null || nodes.Contains(x.FactoryId)))
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Code).ToListAsync(cancellationToken);
        return Ok(new GuardBundle(
            now,
            runs.Select(r => new GuardRun(r.Id, r.RouteId, r.Route!.Name, r.FactoryId, r.Factory!.Name, r.Route.Ordered, r.DueFrom, r.DueBy, r.CloseAt, r.Status,
                r.GuardName, r.RequiredCount, r.ValidCount,
                scans[r.Id].OrderBy(s => s.ScannedAt).Select(s => new GuardScan(s.ClientScanId, s.CheckpointId, s.ScannedAt, s.Validation, s.ValidationNote)).ToList())).ToList(),
            routes.Select(r => new GuardRoute(r.Id, r.Name, r.FactoryId, r.Ordered, r.MinGapSeconds,
                r.Checkpoints.OrderBy(c => c.Sequence).Select(c => new GuardRouteStop(c.CheckpointId, c.Sequence, c.Required)).ToList())).ToList(),
            checkpoints.Select(c => new GuardCheckpoint(c.Id, c.Code, c.Name, c.FactoryId, c.Method, c.TagToken, c.NfcSerial, c.Latitude, c.Longitude, c.RadiusM,
                c.Instructions, SplitQuestions(c.Questions))).ToList()));
    }

    [HttpPost("runs/{id:guid}/start")]
    public async Task<ActionResult<RunRow>> Start(Guid id, CancellationToken cancellationToken)
    {
        EnsurePatrol();
        await EnsureRunVisibleAsync(id, cancellationToken);
        var run = await patrolService.StartAsync(TenantId, id, Actor, cancellationToken);
        return Ok(await RunRowAsync(run.Id, cancellationToken));
    }

    [HttpPost("routes/{id:guid}/start")]
    public async Task<ActionResult<RunRow>> StartOwn(Guid id, CancellationToken cancellationToken)
    {
        EnsurePatrol();
        var factoryId = await dbContext.PatrolRoutes.AsNoTracking().Where(x => x.TenantId == TenantId && x.Id == id).Select(x => (Guid?)x.FactoryId).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Route not found.");
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, factoryId, cancellationToken);
        var run = await patrolService.StartAdHocAsync(TenantId, id, Actor, cancellationToken);
        return Ok(await RunRowAsync(run.Id, cancellationToken));
    }

    /// <summary>A checkpoint scan. Safe to send twice (the phone's scan id makes it idempotent), so offline scans sync without doubles.</summary>
    [HttpPost("scans")]
    public async Task<ActionResult<PatrolScanResponse>> Scan([FromBody] ScanRequest request, CancellationToken cancellationToken)
    {
        EnsurePatrol();
        if (request.ClientScanId == Guid.Empty) throw new BusinessRuleException("Each scan needs its id from the phone.");
        if (string.IsNullOrWhiteSpace(request.Code)) throw new BusinessRuleException("Scan the checkpoint's label or tap its tag.");
        var outcome = await patrolService.ScanAsync(TenantId, new NewScan(request.ClientScanId, request.Code, request.ScannedAt, request.RunId, request.Latitude, request.Longitude,
            request.AccuracyM, request.DeviceId, request.Answers?.Select(x => new ScanAnswer(x.Question, x.Ok)).ToList(), request.Note, request.ByNfc), Actor, cancellationToken);
        var visible = await orgScopeService.VisibleNodesAsync(TenantId, UserId, null, cancellationToken);
        if (visible is not null && !visible.Contains(outcome.Checkpoint.FactoryId)) throw new NotFoundException("This is not one of our checkpoints.");
        var run = outcome.Run;
        return Ok(new PatrolScanResponse(outcome.Scan.Id, outcome.Scan.ClientScanId, outcome.Checkpoint.Id, outcome.Checkpoint.Code, outcome.Checkpoint.Name,
            outcome.Scan.ScannedAt, outcome.Scan.Validation, outcome.Scan.ValidationNote, outcome.Scan.DistanceM,
            run?.Id, run?.Status, run?.ValidCount, run?.RequiredCount, outcome.IncidentId));
    }

    [HttpPost("incidents")]
    public async Task<ActionResult<SecurityIncidentRow>> Report([FromBody] ReportSecurityIncidentRequest request, CancellationToken cancellationToken)
    {
        EnsurePatrol();
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, request.FactoryId, cancellationToken);
        var incident = await patrolService.ReportIncidentAsync(TenantId, new NewSecurityIncident(request.FactoryId, request.Kind ?? "", request.Title ?? "", request.Description,
            request.Severity ?? "Moderate", request.OccurredAt, request.CheckpointId, request.RunId, request.VisitId, request.Latitude, request.Longitude,
            request.PersonsInvolved, request.ImmediateAction), Actor, cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "security.incident.reported", nameof(Incident), incident.Id.ToString(), $"{incident.IncidentNumber}: {incident.Title}", cancellationToken);
        return Ok(await IncidentRowAsync(incident.Id, cancellationToken));
    }

    /// <summary>The panic button: an emergency incident with the guard's position, sent to every supervisor at once.</summary>
    [HttpPost("panic")]
    public async Task<ActionResult<SecurityIncidentRow>> Panic([FromBody] PanicRequest request, CancellationToken cancellationToken)
    {
        EnsurePatrol();
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, request.FactoryId, cancellationToken);
        var incident = await patrolService.ReportIncidentAsync(TenantId, new NewSecurityIncident(request.FactoryId, SecurityIncidentKinds.Emergency,
            $"Panic button: {UserDisplayName}", request.Note ?? "The guard pressed the panic button.", "Serious", null, null, null, null, request.Latitude, request.Longitude),
            Actor, cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "security.panic", nameof(Incident), incident.Id.ToString(), $"Panic button pressed ({incident.IncidentNumber}).", cancellationToken);
        return Ok(await IncidentRowAsync(incident.Id, cancellationToken));
    }

    [HttpGet("handovers")]
    public async Task<ActionResult<IReadOnlyList<HandoverRow>>> Handovers([FromQuery] Guid? factoryId, CancellationToken cancellationToken)
    {
        EnsurePatrol();
        var nodes = await orgScopeService.VisibleNodesAsync(TenantId, UserId, factoryId, cancellationToken);
        return Ok(await dbContext.ShiftHandovers.AsNoTracking()
            .Where(x => x.TenantId == TenantId && (nodes == null || nodes.Contains(x.FactoryId)))
            .OrderByDescending(x => x.HandedOverAt).Take(30)
            .Select(x => new HandoverRow(x.Id, x.FactoryId, x.Factory!.Name, x.ShiftName, x.OutgoingName, x.IncomingName, x.HandedOverAt, x.OpenIssues, x.KeysAndEquipment, x.AcknowledgedAt))
            .ToListAsync(cancellationToken));
    }

    [HttpPost("handovers")]
    public async Task<ActionResult<HandoverRow>> HandOver([FromBody] HandoverRequest request, CancellationToken cancellationToken)
    {
        EnsurePatrol();
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, request.FactoryId, cancellationToken);
        var handover = await patrolService.HandOverAsync(TenantId, new NewHandover(request.FactoryId, request.ShiftName, request.IncomingUserId, request.OpenIssues, request.KeysAndEquipment), Actor, cancellationToken);
        return Ok(await HandoverRowAsync(handover.Id, cancellationToken));
    }

    [HttpPost("handovers/{id:guid}/acknowledge")]
    public async Task<ActionResult<HandoverRow>> Acknowledge(Guid id, CancellationToken cancellationToken)
    {
        EnsurePatrol();
        await patrolService.AcknowledgeHandoverAsync(TenantId, id, Actor, cancellationToken);
        return Ok(await HandoverRowAsync(id, cancellationToken));
    }

    // ── Supervisor ────────────────────────────────────────────────────────────────────────────

    /// <summary>The patrol board for a day (factory local): every patrol with its progress, flagged scans and open incidents.</summary>
    [HttpGet("board")]
    public async Task<ActionResult<PatrolBoard>> Board([FromQuery] Guid? factoryId, [FromQuery] DateTime? date, CancellationToken cancellationToken)
    {
        EnsureManage();
        var nodes = await orgScopeService.VisibleNodesAsync(TenantId, UserId, factoryId, cancellationToken);
        // A security day runs noon to noon, so a night's patrols sit together: a date means that date's night.
        var localNow = DateTime.UtcNow.AddHours(3);
        var start = date is { } d ? d.Date.AddHours(12) : localNow.Hour >= 12 ? localNow.Date.AddHours(12) : localNow.Date.AddHours(-12);
        var localDay = start.Date;
        var from = DateTime.SpecifyKind(start.AddHours(-3), DateTimeKind.Utc);
        var to = from.AddDays(1);
        var runs = await dbContext.PatrolRuns.AsNoTracking()
            .Where(x => x.TenantId == TenantId && (nodes == null || nodes.Contains(x.FactoryId)) && x.DueFrom >= from && x.DueFrom < to)
            .OrderBy(x => x.DueFrom)
            .Select(x => new RunRow(x.Id, x.RouteId, x.Route!.Name, x.FactoryId, x.Factory!.Name, x.ScheduleId == null, x.DueFrom, x.DueBy, x.CloseAt, x.Status,
                x.GuardName, x.StartedAt, x.FinishedAt, x.RequiredCount, x.ValidCount, x.ScanCount, x.FlaggedCount))
            .ToListAsync(cancellationToken);
        var since = DateTime.UtcNow.AddHours(-24);
        var flagged = await dbContext.CheckpointScans.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.ScannedAt >= since && x.Validation != ScanValidations.Valid && x.Validation != ScanValidations.Repeat
                && (nodes == null || nodes.Contains(x.Checkpoint!.FactoryId)))
            .OrderByDescending(x => x.ScannedAt).Take(50)
            .Select(x => new FlaggedScanRow(x.Id, x.RunId, x.CheckpointId, x.Checkpoint!.Code, x.Checkpoint.Name, x.GuardName, x.ScannedAt, x.Validation, x.ValidationNote))
            .ToListAsync(cancellationToken);
        var openIncidents = await dbContext.Incidents.AsNoTracking()
            .CountAsync(x => x.TenantId == TenantId && x.Category == IncidentCategories.Security && x.Status != IncidentStatuses.Closed && (nodes == null || nodes.Contains(x.OrgNodeId)), cancellationToken);
        var lastScan = await dbContext.CheckpointScans.AsNoTracking()
            .Where(x => x.TenantId == TenantId && (nodes == null || nodes.Contains(x.Checkpoint!.FactoryId)))
            .OrderByDescending(x => x.ScannedAt)
            .Select(x => new LastScanRow(x.Checkpoint!.Code, x.Checkpoint.Name, x.GuardName, x.ScannedAt, x.Latitude, x.Longitude))
            .FirstOrDefaultAsync(cancellationToken);
        var closed = runs.Where(x => x.Status is PatrolRunStatuses.Completed or PatrolRunStatuses.Partial or PatrolRunStatuses.Missed).ToList();
        return Ok(new PatrolBoard(localDay, runs, flagged, openIncidents, lastScan,
            closed.Count == 0 ? null : (int)Math.Round(100.0 * closed.Sum(x => x.ValidCount) / Math.Max(1, closed.Sum(x => x.RequiredCount))),
            runs.Count(x => x.Status == PatrolRunStatuses.Missed), runs.Count(x => x.Status == PatrolRunStatuses.Partial), runs.Count(x => x.Status == PatrolRunStatuses.InProgress)));
    }

    [HttpGet("runs/{id:guid}")]
    public async Task<ActionResult<RunDetail>> Run(Guid id, CancellationToken cancellationToken)
    {
        if (!CanManage)
        {
            EnsurePatrol();
            var guard = await dbContext.PatrolRuns.AsNoTracking().Where(x => x.TenantId == TenantId && x.Id == id).Select(x => x.GuardUserId).SingleOrDefaultAsync(cancellationToken);
            if (guard is not null && guard != UserId) throw new ForbiddenException("Only supervisors can see other guards' patrols.");
        }

        await EnsureRunVisibleAsync(id, cancellationToken);
        var row = await RunRowAsync(id, cancellationToken);
        var stops = await dbContext.PatrolRouteCheckpoints.AsNoTracking().Where(x => x.RouteId == row.RouteId).OrderBy(x => x.Sequence)
            .Select(x => new RunStop(x.CheckpointId, x.Checkpoint!.Code, x.Checkpoint.Name, x.Sequence, x.Required))
            .ToListAsync(cancellationToken);
        var scans = await dbContext.CheckpointScans.AsNoTracking().Where(x => x.RunId == id).OrderBy(x => x.ScannedAt)
            .Select(x => new RunScan(x.Id, x.CheckpointId, x.Checkpoint!.Code, x.Checkpoint.Name, x.GuardName, x.Method, x.ScannedAt, x.ReceivedAt, x.Latitude, x.Longitude,
                x.AccuracyM, x.DistanceM, x.Validation, x.ValidationNote, x.AnswersJson, x.Note))
            .ToListAsync(cancellationToken);
        return Ok(new RunDetail(row, stops, scans));
    }

    [HttpGet("incidents")]
    public async Task<ActionResult<IReadOnlyList<SecurityIncidentRow>>> Incidents([FromQuery] Guid? factoryId, [FromQuery] bool openOnly, CancellationToken cancellationToken)
    {
        EnsurePatrol();
        var nodes = await orgScopeService.VisibleNodesAsync(TenantId, UserId, factoryId, cancellationToken);
        var query = dbContext.Incidents.AsNoTracking().Where(x => x.TenantId == TenantId && x.Category == IncidentCategories.Security && (nodes == null || nodes.Contains(x.OrgNodeId)));
        if (openOnly) query = query.Where(x => x.Status != IncidentStatuses.Closed);
        if (!CanManage) query = query.Where(x => x.ReportedByName == UserDisplayName || x.OwnerUserId == UserId);
        return Ok(await query.OrderByDescending(x => x.OccurredAt).Take(200).Select(IncidentProjection).ToListAsync(cancellationToken));
    }

    /// <summary>Moves a security incident on: who owns it, its response status, the police OB number, what was done; Closed closes it.</summary>
    [HttpPut("incidents/{id:guid}")]
    public async Task<ActionResult<SecurityIncidentRow>> UpdateIncident(Guid id, [FromBody] UpdateSecurityIncidentRequest request, CancellationToken cancellationToken)
    {
        EnsureManage();
        var incident = await dbContext.Incidents.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id && x.Category == IncidentCategories.Security, cancellationToken)
            ?? throw new NotFoundException("Security incident not found.");
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, incident.OrgNodeId, cancellationToken);
        if (request.ResponseStatus is { } status)
            incident.ResponseStatus = SecurityResponseStatuses.All.FirstOrDefault(x => string.Equals(x, status, StringComparison.OrdinalIgnoreCase))
                ?? throw new BusinessRuleException($"Response status must be one of: {string.Join(", ", SecurityResponseStatuses.All)}.");
        if (request.OwnerUserId is { } owner)
        {
            incident.OwnerName = await dbContext.Users.AsNoTracking().Where(x => x.TenantId == TenantId && x.Id == owner && x.IsActive).Select(x => x.FullName).SingleOrDefaultAsync(cancellationToken)
                ?? throw new BusinessRuleException("The owner must be an active user.");
            incident.OwnerUserId = owner;
        }

        if (request.PoliceObNumber is not null) incident.PoliceObNumber = GateText.Trim(request.PoliceObNumber, 50);
        if (request.ImmediateAction is not null) incident.ImmediateAction = GateText.Trim(request.ImmediateAction, 2000);
        if (request.RootCause is not null) incident.RootCause = GateText.Trim(request.RootCause, 2000);
        if (request.ReportedToPolice is { } police) incident.ReportedToAuthority = police;
        if (incident.ResponseStatus == SecurityResponseStatuses.Closed && incident.Status != IncidentStatuses.Closed)
        {
            incident.Status = IncidentStatuses.Closed;
            incident.ClosedAt = DateTime.UtcNow;
            incident.ClosedByName = UserDisplayName;
        }
        else if (incident.ResponseStatus is SecurityResponseStatuses.Responding or SecurityResponseStatuses.Contained && incident.Status == IncidentStatuses.Open)
        {
            incident.Status = IncidentStatuses.Investigating;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "security.incident.updated", nameof(Incident), id.ToString(), $"{incident.IncidentNumber}: {incident.ResponseStatus}.", cancellationToken);
        return Ok(await IncidentRowAsync(id, cancellationToken));
    }

    /// <summary>Patrol completion over a period: by route, by guard and by checkpoint (which points get skipped).</summary>
    [HttpGet("report")]
    public async Task<ActionResult<PatrolReport>> Report([FromQuery] Guid? factoryId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        EnsureManage();
        var end = to ?? DateTime.UtcNow;
        var start = from ?? end.AddDays(-30);
        var nodes = await orgScopeService.VisibleNodesAsync(TenantId, UserId, factoryId, cancellationToken);
        var runs = await dbContext.PatrolRuns.AsNoTracking()
            .Where(x => x.TenantId == TenantId && (nodes == null || nodes.Contains(x.FactoryId)) && x.DueFrom >= start && x.DueFrom < end
                && x.Status != PatrolRunStatuses.Due && x.Status != PatrolRunStatuses.InProgress)
            .Select(x => new { x.Id, x.RouteId, Route = x.Route!.Name, x.GuardName, x.Status, x.RequiredCount, x.ValidCount, x.FlaggedCount })
            .ToListAsync(cancellationToken);
        static int Percent(int valid, int required) => required == 0 ? 0 : (int)Math.Round(100.0 * valid / required);
        var byRoute = runs.GroupBy(x => x.Route).OrderBy(x => x.Key)
            .Select(g => new CompletionRow(g.Key, g.Count(), g.Count(x => x.Status == PatrolRunStatuses.Completed), g.Count(x => x.Status == PatrolRunStatuses.Missed),
                Percent(g.Sum(x => x.ValidCount), g.Sum(x => x.RequiredCount)), g.Sum(x => x.FlaggedCount))).ToList();
        var byGuard = runs.Where(x => x.GuardName != null).GroupBy(x => x.GuardName!).OrderBy(x => x.Key)
            .Select(g => new CompletionRow(g.Key, g.Count(), g.Count(x => x.Status == PatrolRunStatuses.Completed), g.Count(x => x.Status == PatrolRunStatuses.Missed),
                Percent(g.Sum(x => x.ValidCount), g.Sum(x => x.RequiredCount)), g.Sum(x => x.FlaggedCount))).ToList();

        var runIds = runs.Select(x => x.Id).ToList();
        var routeIds = runs.Select(x => x.RouteId).Distinct().ToList();
        var stops = await dbContext.PatrolRouteCheckpoints.AsNoTracking().Where(x => routeIds.Contains(x.RouteId) && x.Required)
            .Select(x => new { x.RouteId, x.CheckpointId, x.Checkpoint!.Code, x.Checkpoint.Name }).ToListAsync(cancellationToken);
        var validScans = (await dbContext.CheckpointScans.AsNoTracking().Where(x => x.RunId != null && runIds.Contains(x.RunId.Value) && x.Validation == ScanValidations.Valid)
                .Select(x => new { RunId = x.RunId!.Value, x.CheckpointId }).ToListAsync(cancellationToken))
            .Select(x => (x.RunId, x.CheckpointId)).ToHashSet();
        var byCheckpoint = stops.GroupBy(x => (x.CheckpointId, x.Code, x.Name))
            .Select(g =>
            {
                var expected = runs.Where(r => g.Any(s => s.RouteId == r.RouteId)).ToList();
                var reached = expected.Count(r => validScans.Contains((r.Id, g.Key.CheckpointId)));
                return new CheckpointCompletionRow(g.Key.CheckpointId, g.Key.Code, g.Key.Name, expected.Count, reached, expected.Count - reached);
            })
            .OrderByDescending(x => x.Missed).ThenBy(x => x.Code).ToList();
        var incidents = await dbContext.Incidents.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.Category == IncidentCategories.Security && x.OccurredAt >= start && x.OccurredAt < end && (nodes == null || nodes.Contains(x.OrgNodeId)))
            .GroupBy(x => x.Kind).Select(g => new KindCount(g.Key, g.Count())).ToListAsync(cancellationToken);
        return Ok(new PatrolReport(start, end, runs.Count, runs.Count(x => x.Status == PatrolRunStatuses.Completed), runs.Count(x => x.Status == PatrolRunStatuses.Partial),
            runs.Count(x => x.Status == PatrolRunStatuses.Missed), Percent(runs.Sum(x => x.ValidCount), runs.Sum(x => x.RequiredCount)),
            byRoute, byGuard, byCheckpoint, incidents.Select(x => x with { Kind = SecurityKindLabels.Describe(x.Kind) }).ToList()));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────────────

    private void EnsurePatrol()
    {
        if (!CanPatrol) throw new ForbiddenException("This is for security guards and supervisors.");
    }

    private void EnsureManage()
    {
        if (!CanManage) throw new ForbiddenException("This is for security supervisors.");
    }

    internal static IReadOnlyList<string> SplitQuestions(string? questions) =>
        (questions ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private async Task EnsureRunVisibleAsync(Guid id, CancellationToken cancellationToken)
    {
        var factoryId = await dbContext.PatrolRuns.AsNoTracking().Where(x => x.TenantId == TenantId && x.Id == id).Select(x => (Guid?)x.FactoryId).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Patrol not found.");
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, factoryId, cancellationToken);
    }

    private async Task<RunRow> RunRowAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.PatrolRuns.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new RunRow(x.Id, x.RouteId, x.Route!.Name, x.FactoryId, x.Factory!.Name, x.ScheduleId == null, x.DueFrom, x.DueBy, x.CloseAt, x.Status,
                x.GuardName, x.StartedAt, x.FinishedAt, x.RequiredCount, x.ValidCount, x.ScanCount, x.FlaggedCount))
            .SingleAsync(cancellationToken);

    private static readonly System.Linq.Expressions.Expression<Func<Incident, SecurityIncidentRow>> IncidentProjection = x => new SecurityIncidentRow(
        x.Id, x.IncidentNumber, x.Kind, x.Severity, x.OccurredAt, x.OrgNodeId, x.OrgNode!.Name, x.Title, x.Description, x.ReportedByName, x.Status, x.ResponseStatus,
        x.OwnerUserId, x.OwnerName, x.PoliceObNumber, x.ReportedToAuthority, x.ImmediateAction, x.CheckpointId, x.PatrolRunId, x.VisitId, x.Latitude, x.Longitude, x.ClosedAt);

    private async Task<SecurityIncidentRow> IncidentRowAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.Incidents.AsNoTracking().Where(x => x.Id == id).Select(IncidentProjection).SingleAsync(cancellationToken);

    private async Task<HandoverRow> HandoverRowAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.ShiftHandovers.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new HandoverRow(x.Id, x.FactoryId, x.Factory!.Name, x.ShiftName, x.OutgoingName, x.IncomingName, x.HandedOverAt, x.OpenIssues, x.KeysAndEquipment, x.AcknowledgedAt))
            .SingleAsync(cancellationToken);
}

/// <summary>Plain-language names for security incident kinds.</summary>
public static class SecurityKindLabels
{
    public static string Describe(string kind) => kind switch
    {
        SecurityIncidentKinds.UnauthorisedAccess => "Unauthorised access",
        SecurityIncidentKinds.AttemptedTheft => "Attempted theft",
        SecurityIncidentKinds.PerimeterDamage => "Perimeter damage",
        SecurityIncidentKinds.SuspiciousPerson => "Suspicious person",
        SecurityIncidentKinds.SuspiciousVehicle => "Suspicious vehicle",
        SecurityIncidentKinds.CheckpointIssue => "Checkpoint issue",
        _ => kind,
    };
}

public sealed record PatrolAccessResponse(bool CanPatrol, bool CanManage);

public sealed record GuardScan(Guid ClientScanId, Guid CheckpointId, DateTime ScannedAt, string Validation, string? Note);

public sealed record GuardRun(
    Guid Id, Guid RouteId, string RouteName, Guid FactoryId, string FactoryName, bool Ordered, DateTime DueFrom, DateTime DueBy, DateTime CloseAt, string Status,
    string? GuardName, int RequiredCount, int ValidCount, IReadOnlyList<GuardScan> Scans);

public sealed record GuardRouteStop(Guid CheckpointId, int Sequence, bool Required);

public sealed record GuardRoute(Guid Id, string Name, Guid FactoryId, bool Ordered, int MinGapSeconds, IReadOnlyList<GuardRouteStop> Stops);

public sealed record GuardCheckpoint(
    Guid Id, string Code, string Name, Guid FactoryId, string Method, string TagToken, string? NfcSerial, double? Latitude, double? Longitude, int RadiusM,
    string? Instructions, IReadOnlyList<string> Questions);

public sealed record GuardBundle(DateTime GeneratedAt, IReadOnlyList<GuardRun> Runs, IReadOnlyList<GuardRoute> Routes, IReadOnlyList<GuardCheckpoint> Checkpoints);

public sealed record ScanAnswerRequest(string Question, bool Ok);

public sealed record ScanRequest(
    Guid ClientScanId, string? Code, DateTime? ScannedAt = null, Guid? RunId = null, double? Latitude = null, double? Longitude = null, double? AccuracyM = null,
    string? DeviceId = null, IReadOnlyList<ScanAnswerRequest>? Answers = null, string? Note = null, bool ByNfc = false);

public sealed record PatrolScanResponse(
    Guid ScanId, Guid ClientScanId, Guid CheckpointId, string CheckpointCode, string CheckpointName, DateTime ScannedAt, string Validation, string? Note, double? DistanceM,
    Guid? RunId, string? RunStatus, int? ValidCount, int? RequiredCount, Guid? IncidentId);

public sealed record RunRow(
    Guid Id, Guid RouteId, string RouteName, Guid FactoryId, string FactoryName, bool AdHoc, DateTime DueFrom, DateTime DueBy, DateTime CloseAt, string Status,
    string? GuardName, DateTime? StartedAt, DateTime? FinishedAt, int RequiredCount, int ValidCount, int ScanCount, int FlaggedCount);

public sealed record RunStop(Guid CheckpointId, string Code, string Name, int Sequence, bool Required);

public sealed record RunScan(
    Guid Id, Guid CheckpointId, string Code, string Name, string GuardName, string Method, DateTime ScannedAt, DateTime ReceivedAt, double? Latitude, double? Longitude,
    double? AccuracyM, double? DistanceM, string Validation, string? Note, string? AnswersJson, string? GuardNote);

public sealed record RunDetail(RunRow Run, IReadOnlyList<RunStop> Stops, IReadOnlyList<RunScan> Scans);

public sealed record FlaggedScanRow(Guid Id, Guid? RunId, Guid CheckpointId, string Code, string Name, string GuardName, DateTime ScannedAt, string Validation, string? Note);

public sealed record LastScanRow(string Code, string Name, string GuardName, DateTime ScannedAt, double? Latitude, double? Longitude);

public sealed record PatrolBoard(
    DateTime Day, IReadOnlyList<RunRow> Runs, IReadOnlyList<FlaggedScanRow> Flagged, int OpenIncidents, LastScanRow? LastScan, int? CompletionPercent,
    int Missed, int Partial, int InProgress);

public sealed record SecurityIncidentRow(
    Guid Id, string IncidentNumber, string Kind, string Severity, DateTime OccurredAt, Guid FactoryId, string FactoryName, string Title, string Description,
    string ReportedByName, string Status, string? ResponseStatus, Guid? OwnerUserId, string? OwnerName, string? PoliceObNumber, bool ReportedToPolice,
    string? ImmediateAction, Guid? CheckpointId, Guid? RunId, Guid? VisitId, double? Latitude, double? Longitude, DateTime? ClosedAt);

public sealed record ReportSecurityIncidentRequest(
    Guid FactoryId, string? Kind, string? Title, string? Description = null, string? Severity = null, DateTime? OccurredAt = null, Guid? CheckpointId = null,
    Guid? RunId = null, Guid? VisitId = null, double? Latitude = null, double? Longitude = null, string? PersonsInvolved = null, string? ImmediateAction = null);

public sealed record PanicRequest(Guid FactoryId, double? Latitude = null, double? Longitude = null, string? Note = null);

public sealed record UpdateSecurityIncidentRequest(
    string? ResponseStatus = null, Guid? OwnerUserId = null, string? PoliceObNumber = null, bool? ReportedToPolice = null, string? ImmediateAction = null, string? RootCause = null);

public sealed record HandoverRow(
    Guid Id, Guid FactoryId, string FactoryName, string? ShiftName, string OutgoingName, string? IncomingName, DateTime HandedOverAt, string? OpenIssues, string? KeysAndEquipment,
    DateTime? AcknowledgedAt);

public sealed record HandoverRequest(Guid FactoryId, string? ShiftName = null, Guid? IncomingUserId = null, string? OpenIssues = null, string? KeysAndEquipment = null);

public sealed record CompletionRow(string Name, int Patrols, int Completed, int Missed, int CheckpointPercent, int Flagged);

public sealed record CheckpointCompletionRow(Guid CheckpointId, string Code, string Name, int Expected, int Reached, int Missed);

public sealed record KindCount(string Kind, int Count);

public sealed record PatrolReport(
    DateTime From, DateTime To, int Patrols, int Completed, int Partial, int Missed, int CheckpointPercent,
    IReadOnlyList<CompletionRow> ByRoute, IReadOnlyList<CompletionRow> ByGuard, IReadOnlyList<CheckpointCompletionRow> ByCheckpoint, IReadOnlyList<KindCount> Incidents);
