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

/// <summary>Security Patrol set-up: checkpoints and their labels, routes, and the schedules patrols are raised from.</summary>
[ApiController]
[Authorize]
[Route("api/security")]
public sealed class PatrolSetupController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IOrgScopeService orgScopeService,
    IPatrolService patrolService,
    IAuditLogService auditLogService,
    IAssetLabelPdfRenderer? labelRenderer = null) : TenantAwareControllerBase(tenantContext)
{
    private bool CanManage => IsAdmin || HasPermission(PermissionNames.SecurityManage);

    // ── Checkpoints ───────────────────────────────────────────────────────────────────────────

    [HttpGet("checkpoints")]
    public async Task<ActionResult<IReadOnlyList<CheckpointRow>>> Checkpoints([FromQuery] Guid? factoryId, [FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        EnsureManage();
        var nodes = await orgScopeService.VisibleNodesAsync(TenantId, UserId, factoryId, cancellationToken);
        var since = DateTime.UtcNow.AddDays(-30);
        return Ok(await dbContext.Checkpoints.AsNoTracking()
            .Where(x => x.TenantId == TenantId && (includeInactive || x.IsActive) && (nodes == null || nodes.Contains(x.FactoryId)))
            .OrderBy(x => x.Factory!.Name).ThenBy(x => x.DisplayOrder).ThenBy(x => x.Code)
            .Select(x => new CheckpointRow(x.Id, x.Code, x.Name, x.FactoryId, x.Factory!.Name, x.AccessZoneId, x.AccessZone != null ? x.AccessZone.Name : null, x.Method,
                x.NfcSerial, x.Latitude, x.Longitude, x.RadiusM, x.Instructions, x.Questions, x.DisplayOrder, x.IsActive,
                dbContext.CheckpointScans.Where(s => s.CheckpointId == x.Id).Max(s => (DateTime?)s.ScannedAt),
                dbContext.CheckpointScans.Count(s => s.CheckpointId == x.Id && s.ScannedAt >= since)) { RequirePhoto = x.RequirePhoto })
            .ToListAsync(cancellationToken));
    }

    [HttpPost("checkpoints")]
    public async Task<ActionResult<CheckpointRow>> CreateCheckpoint([FromBody] SaveCheckpointRequest request, CancellationToken cancellationToken)
    {
        EnsureManage();
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, request.FactoryId, cancellationToken);
        var count = await dbContext.Checkpoints.CountAsync(x => x.TenantId == TenantId, cancellationToken);
        var checkpoint = new Checkpoint { TenantId = TenantId, FactoryId = request.FactoryId, Code = $"CP-{count + 1:000}", TagToken = GateText.NewToken() };
        await ApplyAsync(checkpoint, request, cancellationToken);
        checkpoint.DisplayOrder = request.DisplayOrder ?? count + 1;
        dbContext.Checkpoints.Add(checkpoint);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "security.checkpoint.created", nameof(Checkpoint), checkpoint.Id.ToString(), $"{checkpoint.Code} {checkpoint.Name} added.", cancellationToken);
        return Ok(await CheckpointRowAsync(checkpoint.Id, cancellationToken));
    }

    [HttpPut("checkpoints/{id:guid}")]
    public async Task<ActionResult<CheckpointRow>> UpdateCheckpoint(Guid id, [FromBody] SaveCheckpointRequest request, CancellationToken cancellationToken)
    {
        EnsureManage();
        var checkpoint = await dbContext.Checkpoints.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("Checkpoint not found.");
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, checkpoint.FactoryId, cancellationToken);
        if (request.FactoryId != checkpoint.FactoryId) throw new BusinessRuleException("A checkpoint cannot move to another factory; add it there instead.");
        await ApplyAsync(checkpoint, request, cancellationToken);
        if (request.DisplayOrder is { } order) checkpoint.DisplayOrder = order;
        checkpoint.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await CheckpointRowAsync(id, cancellationToken));
    }

    /// <summary>Records where the checkpoint is from a supervisor standing at it (their phone's GPS), for the distance check.</summary>
    [HttpPost("checkpoints/{id:guid}/position")]
    public async Task<ActionResult<CheckpointRow>> SetPosition(Guid id, [FromBody] CheckpointPositionRequest request, CancellationToken cancellationToken)
    {
        EnsureManage();
        var checkpoint = await dbContext.Checkpoints.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("Checkpoint not found.");
        if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180) throw new BusinessRuleException("That is not a valid position.");
        if (request.AccuracyM is > 100) throw new BusinessRuleException($"The phone's position is only accurate to {request.AccuracyM:0} m; step outside and try again.");
        checkpoint.Latitude = request.Latitude;
        checkpoint.Longitude = request.Longitude;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await CheckpointRowAsync(id, cancellationToken));
    }

    /// <summary>Registers the NFC tag fixed at the checkpoint (tap it with the supervisor's phone); a new tag replaces the old one.</summary>
    [HttpPost("checkpoints/{id:guid}/nfc")]
    public async Task<ActionResult<CheckpointRow>> SetNfc(Guid id, [FromBody] CheckpointNfcRequest request, CancellationToken cancellationToken)
    {
        EnsureManage();
        var checkpoint = await dbContext.Checkpoints.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("Checkpoint not found.");
        var serial = PatrolSerials.Normalise(request.Serial);
        if (request.Serial is not null && serial is null) throw new BusinessRuleException("That is not an NFC tag serial number.");
        if (serial is not null && await dbContext.Checkpoints.AnyAsync(x => x.TenantId == TenantId && x.NfcSerial == serial && x.Id != id, cancellationToken))
            throw new ConflictException("That tag is already registered to another checkpoint.");
        checkpoint.NfcSerial = serial;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "security.checkpoint.nfc", nameof(Checkpoint), id.ToString(), serial is null ? $"{checkpoint.Code} NFC tag removed." : $"{checkpoint.Code} NFC tag registered.", cancellationToken);
        return Ok(await CheckpointRowAsync(id, cancellationToken));
    }

    /// <summary>A new QR label for a checkpoint (the old one was photographed or damaged): the old label stops working.</summary>
    [HttpPost("checkpoints/{id:guid}/new-label")]
    public async Task<ActionResult<CheckpointRow>> NewLabel(Guid id, CancellationToken cancellationToken)
    {
        EnsureManage();
        var checkpoint = await dbContext.Checkpoints.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("Checkpoint not found.");
        checkpoint.TagToken = GateText.NewToken();
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "security.checkpoint.relabelled", nameof(Checkpoint), id.ToString(), $"{checkpoint.Code} given a new label; the old one no longer scans.", cancellationToken);
        return Ok(await CheckpointRowAsync(id, cancellationToken));
    }

    /// <summary>A PDF sheet of QR labels for checkpoints (all of a factory's, or the ones given).</summary>
    [HttpPost("checkpoints/labels")]
    public async Task<IActionResult> Labels([FromBody] CheckpointLabelsRequest request, CancellationToken cancellationToken)
    {
        EnsureManage();
        var nodes = await orgScopeService.VisibleNodesAsync(TenantId, UserId, request.FactoryId, cancellationToken);
        var ids = request.CheckpointIds ?? [];
        var checkpoints = await dbContext.Checkpoints.AsNoTracking().Include(x => x.Factory).Include(x => x.AccessZone)
            .Where(x => x.TenantId == TenantId && x.IsActive && (nodes == null || nodes.Contains(x.FactoryId)) && (ids.Count == 0 || ids.Contains(x.Id)))
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Code).ToListAsync(cancellationToken);
        var company = await dbContext.Tenants.Where(x => x.Id == TenantId).Select(x => x.CompanyName).SingleOrDefaultAsync(cancellationToken) ?? string.Empty;
        var labels = checkpoints.Select(x => new AssetLabel(x.Code, $"CHECKPOINT:{x.TagToken}", x.Name, "Security checkpoint", x.AccessZone?.Name ?? x.Factory?.Name, null)).ToList();
        var pdf = (labelRenderer ?? throw new InvalidOperationException("Label printing is not set up.")).Render(company, labels, string.IsNullOrWhiteSpace(request.Layout) ? AssetLabelLayouts.Large : request.Layout);
        return File(pdf, "application/pdf", "checkpoint-labels.pdf");
    }

    /// <summary>The usual tea factory checkpoints and a night round of them every two hours.</summary>
    [HttpPost("checkpoints/seed")]
    public async Task<ActionResult<IReadOnlyList<CheckpointRow>>> Seed([FromBody] SeedCheckpointsRequest request, CancellationToken cancellationToken)
    {
        EnsureManage();
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, request.FactoryId, cancellationToken);
        var added = await patrolService.SeedCheckpointsAsync(TenantId, request.FactoryId, cancellationToken);
        await patrolService.GenerateRunsAsync(TenantId, DateTime.UtcNow, TimeSpan.FromHours(12), cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "security.checkpoint.seeded", nameof(OrgNode), request.FactoryId.ToString(), $"{added} standard checkpoints and a night round added.", cancellationToken);
        return await Checkpoints(request.FactoryId, false, cancellationToken);
    }

    // ── Routes ────────────────────────────────────────────────────────────────────────────────

    [HttpGet("routes")]
    public async Task<ActionResult<IReadOnlyList<RouteRow>>> Routes([FromQuery] Guid? factoryId, CancellationToken cancellationToken)
    {
        EnsureManage();
        var nodes = await orgScopeService.VisibleNodesAsync(TenantId, UserId, factoryId, cancellationToken);
        var routes = await dbContext.PatrolRoutes.AsNoTracking().Include(x => x.Factory).Include(x => x.Checkpoints).ThenInclude(x => x.Checkpoint)
            .Where(x => x.TenantId == TenantId && (nodes == null || nodes.Contains(x.FactoryId)))
            .OrderBy(x => x.Factory!.Name).ThenBy(x => x.Name).ToListAsync(cancellationToken);
        var routeIds = routes.Select(x => x.Id).ToList();
        var schedules = (await dbContext.PatrolSchedules.AsNoTracking().Where(x => routeIds.Contains(x.RouteId)).ToListAsync(cancellationToken)).ToLookup(x => x.RouteId);
        var users = await dbContext.Users.AsNoTracking().Where(x => x.TenantId == TenantId).ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);
        return Ok(routes.Select(r => MapRoute(r, schedules[r.Id], users)).ToList());
    }

    [HttpPost("routes")]
    public async Task<ActionResult<RouteRow>> CreateRoute([FromBody] SaveRouteRequest request, CancellationToken cancellationToken)
    {
        EnsureManage();
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, request.FactoryId, cancellationToken);
        var route = new PatrolRoute { TenantId = TenantId, FactoryId = request.FactoryId };
        dbContext.PatrolRoutes.Add(route);
        await ApplyAsync(route, request, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "security.route.created", nameof(PatrolRoute), route.Id.ToString(), $"Route {route.Name} added.", cancellationToken);
        return Ok(await RouteRowAsync(route.Id, cancellationToken));
    }

    [HttpPut("routes/{id:guid}")]
    public async Task<ActionResult<RouteRow>> UpdateRoute(Guid id, [FromBody] SaveRouteRequest request, CancellationToken cancellationToken)
    {
        EnsureManage();
        var route = await dbContext.PatrolRoutes.Include(x => x.Checkpoints).SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("Route not found.");
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, route.FactoryId, cancellationToken);
        if (request.FactoryId != route.FactoryId) throw new BusinessRuleException("A route cannot move to another factory.");
        await ApplyAsync(route, request, cancellationToken);
        route.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await RouteRowAsync(id, cancellationToken));
    }

    // ── Schedules ─────────────────────────────────────────────────────────────────────────────

    [HttpPost("routes/{routeId:guid}/schedules")]
    public async Task<ActionResult<RouteRow>> AddSchedule(Guid routeId, [FromBody] SaveScheduleRequest request, CancellationToken cancellationToken)
    {
        EnsureManage();
        var route = await dbContext.PatrolRoutes.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == routeId, cancellationToken) ?? throw new NotFoundException("Route not found.");
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, route.FactoryId, cancellationToken);
        var schedule = new PatrolSchedule { TenantId = TenantId, RouteId = routeId };
        await ApplyAsync(schedule, request, cancellationToken);
        dbContext.PatrolSchedules.Add(schedule);
        await dbContext.SaveChangesAsync(cancellationToken);
        await patrolService.GenerateRunsAsync(TenantId, DateTime.UtcNow, TimeSpan.FromHours(12), cancellationToken);
        return Ok(await RouteRowAsync(routeId, cancellationToken));
    }

    [HttpPut("schedules/{id:guid}")]
    public async Task<ActionResult<RouteRow>> UpdateSchedule(Guid id, [FromBody] SaveScheduleRequest request, CancellationToken cancellationToken)
    {
        EnsureManage();
        var schedule = await dbContext.PatrolSchedules.Include(x => x.Route).SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("Schedule not found.");
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, schedule.Route!.FactoryId, cancellationToken);
        await ApplyAsync(schedule, request, cancellationToken);
        schedule.IsActive = request.IsActive;
        // Patrols not yet started follow the new schedule: drop them and raise them again.
        var future = await dbContext.PatrolRuns.Where(x => x.ScheduleId == id && x.Status == PatrolRunStatuses.Due && x.DueFrom > DateTime.UtcNow).ToListAsync(cancellationToken);
        dbContext.PatrolRuns.RemoveRange(future);
        await dbContext.SaveChangesAsync(cancellationToken);
        await patrolService.GenerateRunsAsync(TenantId, DateTime.UtcNow, TimeSpan.FromHours(12), cancellationToken);
        return Ok(await RouteRowAsync(schedule.RouteId, cancellationToken));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────────────

    private void EnsureManage()
    {
        if (!CanManage) throw new ForbiddenException("This is for security supervisors.");
    }

    private async Task ApplyAsync(Checkpoint checkpoint, SaveCheckpointRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 150) throw new BusinessRuleException("Give the checkpoint a name (up to 150 characters).");
        if (!await dbContext.OrgNodes.AnyAsync(x => x.TenantId == TenantId && x.Id == request.FactoryId && x.Level == OrgNodeLevels.Factory, cancellationToken))
            throw new BusinessRuleException("Choose the factory the checkpoint is in.");
        if (request.AccessZoneId is { } zone && !await dbContext.AccessZones.AnyAsync(x => x.TenantId == TenantId && x.Id == zone && x.FactoryId == request.FactoryId, cancellationToken))
            throw new BusinessRuleException("The zone must be in the checkpoint's factory.");
        if (request.RadiusM is < 10 or > 500) throw new BusinessRuleException("The allowed distance must be between 10 and 500 m.");
        checkpoint.Name = name;
        checkpoint.AccessZoneId = request.AccessZoneId;
        checkpoint.Method = CheckpointMethods.Find(request.Method ?? CheckpointMethods.QrOrNfc) ?? throw new BusinessRuleException($"Method must be one of: {string.Join(", ", CheckpointMethods.All)}.");
        checkpoint.RadiusM = request.RadiusM ?? checkpoint.RadiusM;
        if (request.Latitude is not null || request.Longitude is not null)
        {
            if (request.Latitude is null or < -90 or > 90 || request.Longitude is null or < -180 or > 180) throw new BusinessRuleException("Enter both latitude and longitude, or neither.");
            checkpoint.Latitude = request.Latitude;
            checkpoint.Longitude = request.Longitude;
        }

        checkpoint.RequirePhoto = request.RequirePhoto;
        checkpoint.Instructions = GateText.Trim(request.Instructions, 1000);
        var questions = PatrolController.SplitQuestions(request.Questions);
        if (questions.Count > 6) throw new BusinessRuleException("Keep it to six questions or fewer at a checkpoint.");
        checkpoint.Questions = questions.Count == 0 ? null : string.Join("\n", questions.Select(x => x.Length > 150 ? x[..150] : x));
    }

    private async Task ApplyAsync(PatrolRoute route, SaveRouteRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 150) throw new BusinessRuleException("Give the route a name.");
        if (await dbContext.PatrolRoutes.AnyAsync(x => x.TenantId == TenantId && x.FactoryId == request.FactoryId && x.Id != route.Id && x.Name.ToLower() == name.ToLower(), cancellationToken))
            throw new ConflictException($"This factory already has a route called {name}.");
        if (request.MaxDurationMinutes is < 5 or > 720) throw new BusinessRuleException("A patrol must take between 5 minutes and 12 hours.");
        if (request.MinGapSeconds is < 0 or > 1800) throw new BusinessRuleException("The minimum time between checkpoints must be up to 30 minutes.");
        var stops = request.Checkpoints ?? [];
        if (stops.Count == 0) throw new BusinessRuleException("Add the checkpoints the route walks.");
        if (stops.Select(x => x.CheckpointId).Distinct().Count() != stops.Count) throw new BusinessRuleException("Each checkpoint can be on a route once.");
        var ids = stops.Select(x => x.CheckpointId).ToList();
        var found = await dbContext.Checkpoints.AsNoTracking().CountAsync(x => x.TenantId == TenantId && x.FactoryId == request.FactoryId && ids.Contains(x.Id), cancellationToken);
        if (found != stops.Count) throw new BusinessRuleException("Choose checkpoints of the route's factory.");
        if (!stops.Any(x => x.Required)) throw new BusinessRuleException("At least one checkpoint must be required.");

        route.Name = name;
        route.Ordered = request.Ordered;
        route.MaxDurationMinutes = request.MaxDurationMinutes ?? route.MaxDurationMinutes;
        route.MinGapSeconds = request.MinGapSeconds ?? route.MinGapSeconds;
        dbContext.PatrolRouteCheckpoints.RemoveRange(route.Checkpoints);
        var sequence = 0;
        foreach (var stop in stops)
            dbContext.PatrolRouteCheckpoints.Add(new PatrolRouteCheckpoint { TenantId = TenantId, RouteId = route.Id, CheckpointId = stop.CheckpointId, Sequence = ++sequence, Required = stop.Required });

        // Patrols not yet started take the new required count.
        var required = stops.Count(x => x.Required);
        foreach (var run in await dbContext.PatrolRuns.Where(x => x.RouteId == route.Id && x.Status == PatrolRunStatuses.Due).ToListAsync(cancellationToken))
            run.RequiredCount = required;
    }

    private async Task ApplyAsync(PatrolSchedule schedule, SaveScheduleRequest request, CancellationToken cancellationToken)
    {
        static TimeOnly Time(string? value, string label) =>
            TimeOnly.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var time) ? time : throw new BusinessRuleException($"Enter {label} as HH:mm.");
        var fixedTimes = (request.FixedTimes ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => Time(x, "each time")).OrderBy(x => x).ToList();
        if (fixedTimes.Count == 0 && request.EveryMinutes is null) throw new BusinessRuleException("Give either how often (every N minutes) or the fixed times.");
        if (request.EveryMinutes is < 15 or > 1440) throw new BusinessRuleException("Patrols can be every 15 minutes to once a day.");
        var days = (request.Days ?? [1, 2, 3, 4, 5, 6, 7]).Distinct().OrderBy(x => x).ToList();
        if (days.Count == 0 || days.Any(x => x is < 1 or > 7)) throw new BusinessRuleException("Choose the days.");
        if (request.GraceMinutes is < 0 or > 240) throw new BusinessRuleException("Grace must be up to 4 hours.");
        if (request.AssignedUserId is { } user && !await dbContext.Users.AnyAsync(x => x.TenantId == TenantId && x.Id == user && x.IsActive, cancellationToken))
            throw new BusinessRuleException("The assigned guard must be an active user.");
        schedule.EveryMinutes = fixedTimes.Count > 0 ? null : request.EveryMinutes;
        schedule.FixedTimes = fixedTimes.Count > 0 ? string.Join(",", fixedTimes.Select(x => x.ToString("HH:mm"))) : null;
        schedule.WindowFrom = Time(request.WindowFrom ?? "18:00", "the start");
        schedule.WindowTo = Time(request.WindowTo ?? "06:00", "the end");
        schedule.Days = string.Join(",", days);
        schedule.GraceMinutes = request.GraceMinutes ?? 15;
        schedule.AssignedUserId = request.AssignedUserId;
    }

    private static RouteRow MapRoute(PatrolRoute r, IEnumerable<PatrolSchedule> schedules, IReadOnlyDictionary<Guid, string> users) => new(
        r.Id, r.Name, r.FactoryId, r.Factory?.Name ?? "", r.Ordered, r.MaxDurationMinutes, r.MinGapSeconds, r.IsActive,
        r.Checkpoints.OrderBy(x => x.Sequence).Select(x => new RouteStopRow(x.CheckpointId, x.Checkpoint?.Code ?? "", x.Checkpoint?.Name ?? "", x.Sequence, x.Required)).ToList(),
        schedules.Select(s => new ScheduleRow(s.Id, s.EveryMinutes, s.FixedTimes, s.WindowFrom.ToString("HH:mm"), s.WindowTo.ToString("HH:mm"),
            s.Days.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList(), s.GraceMinutes, s.AssignedUserId,
            s.AssignedUserId is { } u ? users.GetValueOrDefault(u) : null, s.IsActive, Describe(s))).ToList());

    internal static string Describe(PatrolSchedule s)
    {
        var days = VisitorGateController.DaysLabel(s.Days);
        return s.FixedTimes is { Length: > 0 } times ? $"{days} at {times.Replace(",", ", ")}"
            : $"{days}, every {(s.EveryMinutes % 60 == 0 ? $"{s.EveryMinutes / 60} h" : $"{s.EveryMinutes} min")} {s.WindowFrom:HH:mm}–{s.WindowTo:HH:mm}";
    }

    private async Task<RouteRow> RouteRowAsync(Guid id, CancellationToken cancellationToken)
    {
        var route = await dbContext.PatrolRoutes.AsNoTracking().Include(x => x.Factory).Include(x => x.Checkpoints).ThenInclude(x => x.Checkpoint).SingleAsync(x => x.Id == id, cancellationToken);
        var schedules = await dbContext.PatrolSchedules.AsNoTracking().Where(x => x.RouteId == id).ToListAsync(cancellationToken);
        var users = await dbContext.Users.AsNoTracking().Where(x => x.TenantId == TenantId).ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);
        return MapRoute(route, schedules, users);
    }

    private async Task<CheckpointRow> CheckpointRowAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.Checkpoints.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new CheckpointRow(x.Id, x.Code, x.Name, x.FactoryId, x.Factory!.Name, x.AccessZoneId, x.AccessZone != null ? x.AccessZone.Name : null, x.Method,
                x.NfcSerial, x.Latitude, x.Longitude, x.RadiusM, x.Instructions, x.Questions, x.DisplayOrder, x.IsActive, null, 0) { RequirePhoto = x.RequirePhoto })
            .SingleAsync(cancellationToken);
}

public sealed record CheckpointRow(
    Guid Id, string Code, string Name, Guid FactoryId, string FactoryName, Guid? AccessZoneId, string? ZoneName, string Method, string? NfcSerial,
    double? Latitude, double? Longitude, int RadiusM, string? Instructions, string? Questions, int DisplayOrder, bool IsActive, DateTime? LastScanAt, int ScansLast30Days)
{
    public bool RequirePhoto { get; init; }
}

public sealed record SaveCheckpointRequest(
    Guid FactoryId, string? Name, string? Method = null, Guid? AccessZoneId = null, double? Latitude = null, double? Longitude = null, int? RadiusM = null,
    string? Instructions = null, string? Questions = null, int? DisplayOrder = null, bool IsActive = true, bool RequirePhoto = false);

public sealed record CheckpointPositionRequest(double Latitude, double Longitude, double? AccuracyM = null);

public sealed record CheckpointNfcRequest(string? Serial);

public sealed record CheckpointLabelsRequest(Guid? FactoryId = null, IReadOnlyList<Guid>? CheckpointIds = null, string? Layout = null);

public sealed record SeedCheckpointsRequest(Guid FactoryId);

public sealed record RouteStopRow(Guid CheckpointId, string Code, string Name, int Sequence, bool Required);

public sealed record ScheduleRow(
    Guid Id, int? EveryMinutes, string? FixedTimes, string WindowFrom, string WindowTo, IReadOnlyList<int> Days, int GraceMinutes, Guid? AssignedUserId, string? AssignedName,
    bool IsActive, string Description);

public sealed record RouteRow(
    Guid Id, string Name, Guid FactoryId, string FactoryName, bool Ordered, int MaxDurationMinutes, int MinGapSeconds, bool IsActive,
    IReadOnlyList<RouteStopRow> Checkpoints, IReadOnlyList<ScheduleRow> Schedules);

public sealed record RouteStopRequest(Guid CheckpointId, bool Required = true);

public sealed record SaveRouteRequest(
    Guid FactoryId, string? Name, IReadOnlyList<RouteStopRequest>? Checkpoints, bool Ordered = true, int? MaxDurationMinutes = null, int? MinGapSeconds = null, bool IsActive = true);

public sealed record SaveScheduleRequest(
    int? EveryMinutes = null, string? FixedTimes = null, string? WindowFrom = null, string? WindowTo = null, IReadOnlyList<int>? Days = null, int? GraceMinutes = null,
    Guid? AssignedUserId = null, bool IsActive = true);
