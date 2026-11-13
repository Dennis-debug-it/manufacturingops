using System.Text.Json;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services.Factory;

public sealed record PatrolActor(Guid UserId, string Name);

public sealed record ScanAnswer(string Question, bool Ok);

/// <summary>A checkpoint scan from the guard's phone. <paramref name="Code"/> is the QR content ("CHECKPOINT:…") or the NFC tag's serial.</summary>
public sealed record NewScan(
    Guid ClientScanId,
    string Code,
    DateTime? ScannedAt = null,
    Guid? RunId = null,
    double? Latitude = null,
    double? Longitude = null,
    double? AccuracyM = null,
    string? DeviceId = null,
    IReadOnlyList<ScanAnswer>? Answers = null,
    string? Note = null,
    bool ByNfc = false);

public sealed record ScanOutcome(CheckpointScan Scan, Checkpoint Checkpoint, PatrolRun? Run, Guid? IncidentId);

public sealed record NewSecurityIncident(
    Guid FactoryId,
    string Kind,
    string Title,
    string? Description = null,
    string Severity = "Moderate",
    DateTime? OccurredAt = null,
    Guid? CheckpointId = null,
    Guid? RunId = null,
    Guid? VisitId = null,
    double? Latitude = null,
    double? Longitude = null,
    string? PersonsInvolved = null,
    string? ImmediateAction = null);

public sealed record NewHandover(Guid FactoryId, string? ShiftName, Guid? IncomingUserId, string? OpenIssues, string? KeysAndEquipment);

public sealed record PatrolCloseRun(int Closed, int Alerts);

public interface IPatrolService
{
    /// <summary>Raises the patrols due from now to <paramref name="ahead"/> for every active schedule; existing ones are left alone.</summary>
    Task<int> GenerateRunsAsync(Guid tenantId, DateTime utcNow, TimeSpan ahead, CancellationToken cancellationToken = default);

    Task<PatrolRun> StartAsync(Guid tenantId, Guid runId, PatrolActor actor, CancellationToken cancellationToken = default);

    /// <summary>A patrol the guard starts on their own, outside the schedule.</summary>
    Task<PatrolRun> StartAdHocAsync(Guid tenantId, Guid routeId, PatrolActor actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a scan and judges it: right tag, on the route, in order, not too fast after the last one, near
    /// the point. A scan that fails a check is kept and flagged, never dropped. A scan synced twice is stored once.
    /// </summary>
    Task<ScanOutcome> ScanAsync(Guid tenantId, NewScan scan, PatrolActor actor, CancellationToken cancellationToken = default);

    /// <summary>Closes patrols past their deadline as Completed, Partial or Missed and alerts supervisors about the last two.</summary>
    Task<PatrolCloseRun> CloseOverdueAsync(Guid? tenantId, DateTime utcNow, CancellationToken cancellationToken = default);

    Task<Incident> ReportIncidentAsync(Guid tenantId, NewSecurityIncident request, PatrolActor actor, CancellationToken cancellationToken = default);

    Task<ShiftHandover> HandOverAsync(Guid tenantId, NewHandover request, PatrolActor actor, CancellationToken cancellationToken = default);

    Task<ShiftHandover> AcknowledgeHandoverAsync(Guid tenantId, Guid handoverId, PatrolActor actor, CancellationToken cancellationToken = default);

    /// <summary>The usual checkpoints of the factory's industry and a night round of them every two hours, for a factory with none.</summary>
    Task<int> SeedCheckpointsAsync(Guid tenantId, Guid factoryId, CancellationToken cancellationToken = default);
}

internal sealed class PatrolService(
    AppDbContext dbContext,
    INotificationDispatcher? notificationDispatcher = null,
    ILogger<PatrolService>? logger = null) : IPatrolService
{
    public const string MissedKey = "security.patrol-missed";
    public const string IncidentKey = "security.incident-reported";

    /// <summary>A scan stamped further ahead of the server than this has an untrustworthy clock.</summary>
    public static readonly TimeSpan MaxClockAhead = TimeSpan.FromMinutes(2);
    /// <summary>Walking pace allowance between checkpoints with known positions (m/s, a brisk run).</summary>
    public const double MaxSpeedMps = 4.0;
    /// <summary>A patrol can be started (or picked up by a scan) this long before it is due.</summary>
    public static readonly TimeSpan EarlyStart = TimeSpan.FromMinutes(30);

    // ── Runs ──────────────────────────────────────────────────────────────────────────────────

    public async Task<int> GenerateRunsAsync(Guid tenantId, DateTime utcNow, TimeSpan ahead, CancellationToken cancellationToken = default)
    {
        var schedules = await dbContext.PatrolSchedules.IgnoreQueryFilters().AsNoTracking()
            .Include(x => x.Route!).ThenInclude(x => x.Checkpoints)
            .Where(x => x.TenantId == tenantId && x.IsActive && x.Route!.IsActive)
            .ToListAsync(cancellationToken);
        var users = await dbContext.Users.IgnoreQueryFilters().AsNoTracking().Where(x => x.TenantId == tenantId).ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);
        var added = 0;
        foreach (var schedule in schedules)
        {
            var route = schedule.Route!;
            var slots = Slots(schedule, utcNow.AddHours(-12), utcNow.Add(ahead))
                .Where(start => start.AddMinutes(route.MaxDurationMinutes + schedule.GraceMinutes) > utcNow)
                .ToList();
            if (slots.Count == 0) continue;
            var first = slots[0];
            var last = slots[^1];
            var existing = (await dbContext.PatrolRuns.IgnoreQueryFilters().AsNoTracking()
                    .Where(x => x.ScheduleId == schedule.Id && x.DueFrom >= first && x.DueFrom <= last)
                    .Select(x => x.DueFrom).ToListAsync(cancellationToken))
                .ToHashSet();
            var required = route.Checkpoints.Count(x => x.Required);
            foreach (var start in slots.Where(x => !existing.Contains(x)))
            {
                var dueBy = start.AddMinutes(route.MaxDurationMinutes);
                dbContext.PatrolRuns.Add(new PatrolRun
                {
                    TenantId = tenantId, FactoryId = route.FactoryId, RouteId = route.Id, ScheduleId = schedule.Id,
                    DueFrom = start, DueBy = dueBy, CloseAt = dueBy.AddMinutes(schedule.GraceMinutes), RequiredCount = required,
                    GuardUserId = schedule.AssignedUserId, GuardName = schedule.AssignedUserId is { } g ? users.GetValueOrDefault(g) : null,
                });
                added++;
            }
        }

        if (added > 0) await dbContext.SaveChangesAsync(cancellationToken);
        return added;
    }

    /// <summary>Patrol start times (UTC) of a schedule between two moments. Factory local time is UTC+3.</summary>
    internal static IEnumerable<DateTime> Slots(PatrolSchedule schedule, DateTime fromUtc, DateTime toUtc)
    {
        var days = schedule.Days.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse).ToHashSet();
        var fixedTimes = (schedule.FixedTimes ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => TimeOnly.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).OrderBy(x => x).ToList();
        var result = new SortedSet<DateTime>();
        for (var day = Local(fromUtc).Date.AddDays(-1); day <= Local(toUtc).Date; day = day.AddDays(1))
        {
            var iso = day.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)day.DayOfWeek;
            if (!days.Contains(iso)) continue;
            if (fixedTimes.Count > 0)
            {
                foreach (var time in fixedTimes)
                {
                    // A time before the window opens belongs to the night that started the day before.
                    var date = schedule.WindowTo <= schedule.WindowFrom && time < schedule.WindowFrom && time <= schedule.WindowTo ? day.AddDays(1) : day;
                    result.Add(Utc(date.Add(time.ToTimeSpan())));
                }

                continue;
            }

            var every = Math.Max(15, schedule.EveryMinutes ?? 120);
            var start = day.Add(schedule.WindowFrom.ToTimeSpan());
            var end = day.Add(schedule.WindowTo.ToTimeSpan());
            if (end <= start) end = end.AddDays(1);
            for (var t = start; t < end; t = t.AddMinutes(every)) result.Add(Utc(t));
        }

        return result.Where(x => x >= fromUtc && x <= toUtc);
    }

    public async Task<PatrolRun> StartAsync(Guid tenantId, Guid runId, PatrolActor actor, CancellationToken cancellationToken = default)
    {
        var run = await dbContext.PatrolRuns.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == runId, cancellationToken)
            ?? throw new NotFoundException("Patrol not found.");
        var now = OccurredAt.UtcNow;
        if (!PatrolRunStatuses.Open.Contains(run.Status)) throw new BusinessRuleException($"This patrol is already {run.Status.ToLowerInvariant()}.");
        if (now < run.DueFrom - EarlyStart) throw new BusinessRuleException($"This patrol starts at {Local(run.DueFrom):HH:mm}.");
        if (run.GuardUserId is { } guard && guard != actor.UserId && run.Status == PatrolRunStatuses.InProgress)
            throw new BusinessRuleException($"{run.GuardName} is already walking this patrol.");
        run.GuardUserId = actor.UserId;
        run.GuardName = actor.Name;
        if (run.Status == PatrolRunStatuses.Due)
        {
            run.Status = PatrolRunStatuses.InProgress;
            run.StartedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return run;
    }

    public async Task<PatrolRun> StartAdHocAsync(Guid tenantId, Guid routeId, PatrolActor actor, CancellationToken cancellationToken = default)
    {
        var route = await dbContext.PatrolRoutes.Include(x => x.Checkpoints).SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == routeId && x.IsActive, cancellationToken)
            ?? throw new NotFoundException("Route not found.");
        var now = OccurredAt.UtcNow;
        var run = new PatrolRun
        {
            TenantId = tenantId, FactoryId = route.FactoryId, RouteId = route.Id, DueFrom = now, DueBy = now.AddMinutes(route.MaxDurationMinutes),
            CloseAt = now.AddMinutes(route.MaxDurationMinutes + 15), GuardUserId = actor.UserId, GuardName = actor.Name,
            Status = PatrolRunStatuses.InProgress, StartedAt = now, RequiredCount = route.Checkpoints.Count(x => x.Required),
        };
        dbContext.PatrolRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);
        return run;
    }

    // ── Scans ─────────────────────────────────────────────────────────────────────────────────

    public async Task<ScanOutcome> ScanAsync(Guid tenantId, NewScan request, PatrolActor actor, CancellationToken cancellationToken = default)
    {
        var replay = await dbContext.CheckpointScans.Include(x => x.Checkpoint).Include(x => x.Run)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.ClientScanId == request.ClientScanId, cancellationToken);
        if (replay is not null) return new ScanOutcome(replay, replay.Checkpoint!, replay.Run, null);

        var (checkpoint, byNfc) = await ResolveCheckpointAsync(tenantId, request.Code, request.ByNfc, cancellationToken);
        var now = DateTime.UtcNow;
        var scannedAt = request.ScannedAt is { } at ? DateTime.SpecifyKind(at.Kind == DateTimeKind.Local ? at.ToUniversalTime() : at, DateTimeKind.Utc) : OccurredAt.UtcNow;
        string? clockNote = null;
        if (scannedAt > now + MaxClockAhead)
        {
            clockNote = $"The phone's clock was {(scannedAt - now).TotalMinutes:0} min ahead; the server time was used.";
            scannedAt = now;
        }

        if (scannedAt < now - OccurredAt.MaxAge) throw new BusinessRuleException("This scan is more than a week old and can no longer be recorded.");

        var run = await FindRunAsync(tenantId, request.RunId, checkpoint, scannedAt, actor, cancellationToken);
        var route = run is null ? null : await dbContext.PatrolRoutes.AsNoTracking().Include(x => x.Checkpoints).ThenInclude(x => x.Checkpoint)
            .SingleAsync(x => x.Id == run.RouteId, cancellationToken);
        var previous = run is null ? [] : await dbContext.CheckpointScans.AsNoTracking().Include(x => x.Checkpoint)
            .Where(x => x.RunId == run.Id).OrderBy(x => x.ScannedAt).ToListAsync(cancellationToken);

        var distance = checkpoint.Latitude is { } lat && checkpoint.Longitude is { } lng && request.Latitude is { } slat && request.Longitude is { } slng
            ? Metres(lat, lng, slat, slng) : (double?)null;
        var (validation, note) = Judge(checkpoint, byNfc, run, route, previous, scannedAt, distance, request.AccuracyM);
        if (clockNote is not null && validation == ScanValidations.Valid) (validation, note) = (ScanValidations.ClockSkew, clockNote);

        var scan = new CheckpointScan
        {
            TenantId = tenantId,
            ClientScanId = request.ClientScanId,
            RunId = run?.Id,
            CheckpointId = checkpoint.Id,
            GuardUserId = actor.UserId,
            GuardName = actor.Name,
            DeviceId = VisitorService.Trim(request.DeviceId, 100),
            Method = byNfc ? CheckpointMethods.Nfc : CheckpointMethods.Qr,
            NfcSerial = byNfc ? NormaliseSerial(request.Code) : null,
            ScannedAt = scannedAt,
            ReceivedAt = now,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            AccuracyM = request.AccuracyM,
            DistanceM = distance is null ? null : Math.Round(distance.Value, 1),
            Validation = validation,
            ValidationNote = note is null ? null : VisitorService.Trim(note, 500),
            AnswersJson = request.Answers is { Count: > 0 } answers ? JsonSerializer.Serialize(answers.Select(x => new { question = x.Question, ok = x.Ok })) : null,
            Note = VisitorService.Trim(request.Note, 1000),
        };
        dbContext.CheckpointScans.Add(scan);

        if (run is not null)
        {
            if (run.Status == PatrolRunStatuses.Due)
            {
                run.Status = PatrolRunStatuses.InProgress;
                run.StartedAt = scannedAt;
            }

            run.GuardUserId ??= actor.UserId;
            run.GuardName ??= actor.Name;
            Recount(run, route!, [.. previous, scan]);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        Guid? incidentId = null;
        var failed = request.Answers?.Where(x => !x.Ok).Select(x => x.Question.Trim()).ToList() ?? [];
        if (failed.Count > 0)
        {
            var incident = await ReportIncidentAsync(tenantId, new NewSecurityIncident(
                checkpoint.FactoryId, SecurityIncidentKinds.CheckpointIssue, $"{checkpoint.Name}: {string.Join("; ", failed)}",
                $"Found on patrol at {checkpoint.Code} {checkpoint.Name}: {string.Join("; ", failed.Select(x => $"{x} No"))}." + (scan.Note is null ? "" : $" {scan.Note}"),
                "Moderate", scannedAt, checkpoint.Id, run?.Id, Latitude: request.Latitude, Longitude: request.Longitude), actor, cancellationToken);
            incidentId = incident.Id;
        }

        return new ScanOutcome(scan, checkpoint, run, incidentId);
    }

    /// <summary>The first check a scan fails, in order of how much it says about the guard not being there.</summary>
    internal static (string Validation, string? Note) Judge(
        Checkpoint checkpoint, bool byNfc, PatrolRun? run, PatrolRoute? route, IReadOnlyList<CheckpointScan> previous, DateTime scannedAt, double? distance, double? accuracy)
    {
        if (checkpoint.Method == CheckpointMethods.Nfc && !byNfc)
            return (ScanValidations.WrongTag, $"{checkpoint.Code} must be tapped with the NFC tag, not scanned from a label.");

        if (run is not null && route is not null)
        {
            var stop = route.Checkpoints.FirstOrDefault(x => x.CheckpointId == checkpoint.Id);
            if (stop is null) return (ScanValidations.NotOnRoute, $"{checkpoint.Code} is not on {route.Name}.");
            if (previous.Any(x => x.CheckpointId == checkpoint.Id && ScanValidations.Counts(x.Validation)))
                return (ScanValidations.Repeat, $"{checkpoint.Code} was already scanned on this patrol.");

            if (route.Ordered && stop.Required)
            {
                var done = previous.Where(x => ScanValidations.Counts(x.Validation)).Select(x => x.CheckpointId).ToHashSet();
                var expected = route.Checkpoints.Where(x => x.Required && !done.Contains(x.CheckpointId)).OrderBy(x => x.Sequence).FirstOrDefault();
                if (expected is not null && expected.Sequence < stop.Sequence)
                    return (ScanValidations.OutOfSequence, $"Expected {expected.Checkpoint?.Code} {expected.Checkpoint?.Name} first.");
            }

            var last = previous.Where(x => x.Validation != ScanValidations.ClockSkew && x.ScannedAt <= scannedAt).OrderByDescending(x => x.ScannedAt).FirstOrDefault();
            if (last is not null && last.CheckpointId != checkpoint.Id)
            {
                var seconds = (scannedAt - last.ScannedAt).TotalSeconds;
                if (seconds < route.MinGapSeconds)
                    return (ScanValidations.TooFast, $"{seconds:0} s after {last.Checkpoint?.Code}; at least {route.MinGapSeconds} s expected.");
                if (last.Checkpoint is { Latitude: { } la, Longitude: { } lo } && checkpoint is { Latitude: { } lb, Longitude: { } ob })
                {
                    var metres = Metres(la, lo, lb, ob);
                    if (seconds > 0 && metres / seconds > MaxSpeedMps)
                        return (ScanValidations.TooFast, $"{metres:0} m from {last.Checkpoint.Code} in {seconds:0} s.");
                }
            }
        }

        if (distance is { } d && d > checkpoint.RadiusM + Math.Min(accuracy ?? 0, 50))
            return (ScanValidations.OutsideGeofence, $"{d:0} m from {checkpoint.Code} (allowed {checkpoint.RadiusM} m).");

        return (ScanValidations.Valid, distance is null && checkpoint.Latitude is not null ? "No GPS position with the scan." : null);
    }

    private static void Recount(PatrolRun run, PatrolRoute route, IReadOnlyList<CheckpointScan> scans)
    {
        var required = route.Checkpoints.Where(x => x.Required).Select(x => x.CheckpointId).ToHashSet();
        run.RequiredCount = required.Count;
        run.ScanCount = scans.Count;
        run.FlaggedCount = scans.Count(x => x.Validation is not (ScanValidations.Valid or ScanValidations.Repeat));
        run.ValidCount = scans.Where(x => ScanValidations.Counts(x.Validation) && required.Contains(x.CheckpointId)).Select(x => x.CheckpointId).Distinct().Count();
        if (run.ValidCount >= run.RequiredCount && run.RequiredCount > 0)
        {
            run.Status = PatrolRunStatuses.Completed;
            run.FinishedAt = scans.Where(x => ScanValidations.Counts(x.Validation)).Max(x => x.ScannedAt);
        }
        else if (run.Status is PatrolRunStatuses.Missed or PatrolRunStatuses.Partial)
        {
            // A scan made in time but synced after the patrol closed still counts.
            run.Status = run.ValidCount > 0 || run.ScanCount > 0 ? PatrolRunStatuses.Partial : PatrolRunStatuses.Missed;
        }
    }

    private async Task<PatrolRun?> FindRunAsync(Guid tenantId, Guid? runId, Checkpoint checkpoint, DateTime scannedAt, PatrolActor actor, CancellationToken cancellationToken)
    {
        if (runId is { } id)
        {
            var run = await dbContext.PatrolRuns.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken)
                ?? throw new NotFoundException("Patrol not found.");
            // A scan made after the patrol closed is recorded on its own, not on the patrol.
            return scannedAt <= run.CloseAt ? run : null;
        }

        var candidates = await dbContext.PatrolRuns
            .Where(x => x.TenantId == tenantId && x.FactoryId == checkpoint.FactoryId
                && (x.Status == PatrolRunStatuses.Due || x.Status == PatrolRunStatuses.InProgress)
                && x.DueFrom <= scannedAt.Add(EarlyStart) && x.CloseAt >= scannedAt
                && (x.GuardUserId == null || x.GuardUserId == actor.UserId)
                && dbContext.PatrolRouteCheckpoints.Any(r => r.RouteId == x.RouteId && r.CheckpointId == checkpoint.Id))
            .ToListAsync(cancellationToken);
        return candidates
            .OrderByDescending(x => x.Status == PatrolRunStatuses.InProgress && x.GuardUserId == actor.UserId)
            .ThenBy(x => x.DueFrom)
            .FirstOrDefault();
    }

    private async Task<(Checkpoint Checkpoint, bool ByNfc)> ResolveCheckpointAsync(Guid tenantId, string code, bool byNfc, CancellationToken cancellationToken)
    {
        var value = code.Trim();
        if (!byNfc)
        {
            if (value.StartsWith("CHECKPOINT:", StringComparison.OrdinalIgnoreCase)) value = value["CHECKPOINT:".Length..];
            var slash = value.LastIndexOf('/');
            if (slash >= 0) value = value[(slash + 1)..];
            var byToken = await dbContext.Checkpoints.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.TagToken == value && x.IsActive, cancellationToken);
            if (byToken is not null) return (byToken, false);
        }

        var serial = NormaliseSerial(code);
        var bySerial = serial is null ? null : await dbContext.Checkpoints.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.NfcSerial == serial && x.IsActive, cancellationToken);
        return bySerial is not null ? (bySerial, true) : throw new NotFoundException("This is not one of our checkpoints.");
    }

    /// <summary>NFC serials arrive as "04:a2:…" or "04A2…"; stored as upper-case hex without separators.</summary>
    internal static string? NormaliseSerial(string? serial)
    {
        if (string.IsNullOrWhiteSpace(serial)) return null;
        var hex = new string(serial.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
        return hex.Length is >= 8 and <= 64 ? hex : null;
    }

    // ── Closing and alerts ────────────────────────────────────────────────────────────────────

    public async Task<PatrolCloseRun> CloseOverdueAsync(Guid? tenantId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var due = await dbContext.PatrolRuns.IgnoreQueryFilters()
            .Include(x => x.Route).Include(x => x.Factory)
            .Where(x => (tenantId == null || x.TenantId == tenantId) && (x.Status == PatrolRunStatuses.Due || x.Status == PatrolRunStatuses.InProgress) && x.CloseAt < utcNow)
            .ToListAsync(cancellationToken);
        foreach (var run in due)
        {
            run.Status = run.RequiredCount > 0 && run.ValidCount >= run.RequiredCount ? PatrolRunStatuses.Completed
                : run.ScanCount == 0 ? PatrolRunStatuses.Missed : PatrolRunStatuses.Partial;
            run.FinishedAt ??= run.Status == PatrolRunStatuses.Completed ? run.CloseAt : null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var alerts = 0;
        foreach (var group in due.Where(x => x.Status != PatrolRunStatuses.Completed && x.AlertSentAt == null).GroupBy(x => (x.TenantId, x.FactoryId)))
        {
            var recipients = await SupervisorsAsync(group.Key.TenantId, cancellationToken);
            var lines = group.OrderBy(x => x.DueFrom).Select(x =>
                $"{x.Route!.Name} at {Local(x.DueFrom):HH:mm}: {(x.Status == PatrolRunStatuses.Missed ? "missed" : $"{x.ValidCount} of {x.RequiredCount} checkpoints")}{(x.GuardName is null ? "" : $" ({x.GuardName})")}").ToList();
            var factory = group.First().Factory?.Name ?? "the factory";
            if (recipients.Count > 0)
            {
                await NotifyAsync(new NotificationRequest(group.Key.TenantId, MissedKey, recipients,
                    group.Count() == 1 ? $"Patrol {(group.First().Status == PatrolRunStatuses.Missed ? "missed" : "incomplete")}: {group.First().Route!.Name}" : $"{group.Count()} patrols missed or incomplete at {factory}",
                    string.Join("; ", lines), "/security", nameof(PatrolRun), group.Count() == 1 ? group.First().Id : null,
                    new Dictionary<string, string?> { ["Factory"] = factory, ["Patrols"] = string.Join("\n", lines), ["Count"] = group.Count().ToString() },
                    NotificationSeverities.Critical), cancellationToken);
                alerts++;
            }

            foreach (var run in group) run.AlertSentAt = utcNow;
        }

        if (alerts > 0 || due.Count > 0) await dbContext.SaveChangesAsync(cancellationToken);
        return new PatrolCloseRun(due.Count, alerts);
    }

    // ── Incidents and handover ────────────────────────────────────────────────────────────────

    public async Task<Incident> ReportIncidentAsync(Guid tenantId, NewSecurityIncident request, PatrolActor actor, CancellationToken cancellationToken = default)
    {
        var kind = SecurityIncidentKinds.Find(request.Kind) ?? throw new BusinessRuleException($"Kind must be one of: {string.Join(", ", SecurityIncidentKinds.All)}.");
        var severity = IncidentSeverities.All.FirstOrDefault(x => string.Equals(x, request.Severity, StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessRuleException($"Severity must be one of: {string.Join(", ", IncidentSeverities.All)}.");
        var factory = await dbContext.OrgNodes.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == request.FactoryId, cancellationToken)
            ?? throw new BusinessRuleException("Choose the factory where it happened.");
        var title = request.Title?.Trim();
        if (string.IsNullOrEmpty(title)) throw new BusinessRuleException("Say briefly what happened.");

        var count = await dbContext.Incidents.CountAsync(x => x.TenantId == tenantId, cancellationToken);
        var incident = new Incident
        {
            TenantId = tenantId,
            IncidentNumber = $"INC-{count + 1:000000}",
            Category = IncidentCategories.Security,
            Kind = kind,
            Severity = severity,
            OccurredAt = request.OccurredAt ?? OccurredAt.UtcNow,
            OrgNodeId = factory.Id,
            Title = VisitorService.Trim(title, 200)!,
            Description = VisitorService.Trim(request.Description, 4000) ?? title,
            PersonsInvolved = VisitorService.Trim(request.PersonsInvolved, 1000),
            ImmediateAction = VisitorService.Trim(request.ImmediateAction, 2000),
            ReportedByName = actor.Name,
            ResponseStatus = SecurityResponseStatuses.Reported,
            CheckpointId = request.CheckpointId,
            PatrolRunId = request.RunId,
            VisitId = request.VisitId,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
        };
        dbContext.Incidents.Add(incident);
        await dbContext.SaveChangesAsync(cancellationToken);

        var recipients = (await SupervisorsAsync(tenantId, cancellationToken)).Where(x => x != actor.UserId).ToList();
        if (recipients.Count > 0)
        {
            var where = request.Latitude is { } lat && request.Longitude is { } lng ? $" Location: https://maps.google.com/?q={lat:0.000000},{lng:0.000000}" : "";
            await NotifyAsync(new NotificationRequest(tenantId, IncidentKey, recipients,
                kind == SecurityIncidentKinds.Emergency ? $"EMERGENCY at {factory.Name}: {actor.Name}" : $"{incident.IncidentNumber}: {Describe(kind)} at {factory.Name}",
                incident.Title + where, $"/security?tab=incidents&incident={incident.Id}", nameof(Incident), incident.Id,
                new Dictionary<string, string?>
                {
                    ["Kind"] = Describe(kind), ["Severity"] = severity, ["Title"] = incident.Title, ["IncidentNumber"] = incident.IncidentNumber,
                    ["ReportedBy"] = actor.Name, ["Place"] = factory.Name, ["Description"] = incident.Description + where,
                },
                kind == SecurityIncidentKinds.Emergency || severity is "Serious" or "Fatal" ? NotificationSeverities.Critical : NotificationSeverities.Warning), cancellationToken);
        }

        return incident;
    }

    public async Task<ShiftHandover> HandOverAsync(Guid tenantId, NewHandover request, PatrolActor actor, CancellationToken cancellationToken = default)
    {
        if (!await dbContext.OrgNodes.AnyAsync(x => x.TenantId == tenantId && x.Id == request.FactoryId && x.Level == OrgNodeLevels.Factory, cancellationToken))
            throw new BusinessRuleException("Choose the factory.");
        string? incomingName = null;
        if (request.IncomingUserId is { } incoming)
        {
            incomingName = await dbContext.Users.AsNoTracking().Where(x => x.TenantId == tenantId && x.Id == incoming && x.IsActive).Select(x => x.FullName).SingleOrDefaultAsync(cancellationToken)
                ?? throw new BusinessRuleException("The incoming guard must be an active user.");
        }

        var handover = new ShiftHandover
        {
            TenantId = tenantId, FactoryId = request.FactoryId, ShiftName = VisitorService.Trim(request.ShiftName, 50), OutgoingUserId = actor.UserId, OutgoingName = actor.Name,
            IncomingUserId = request.IncomingUserId, IncomingName = incomingName, HandedOverAt = OccurredAt.UtcNow,
            OpenIssues = VisitorService.Trim(request.OpenIssues, 4000), KeysAndEquipment = VisitorService.Trim(request.KeysAndEquipment, 2000),
        };
        dbContext.ShiftHandovers.Add(handover);
        await dbContext.SaveChangesAsync(cancellationToken);
        return handover;
    }

    public async Task<ShiftHandover> AcknowledgeHandoverAsync(Guid tenantId, Guid handoverId, PatrolActor actor, CancellationToken cancellationToken = default)
    {
        var handover = await dbContext.ShiftHandovers.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == handoverId, cancellationToken)
            ?? throw new NotFoundException("Handover not found.");
        if (handover.AcknowledgedAt is not null) return handover;
        if (handover.OutgoingUserId == actor.UserId) throw new BusinessRuleException("The incoming guard acknowledges the handover, not the one leaving.");
        handover.IncomingUserId ??= actor.UserId;
        handover.IncomingName ??= actor.Name;
        handover.AcknowledgedAt = OccurredAt.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return handover;
    }

    // ── Set-up ────────────────────────────────────────────────────────────────────────────────

    public async Task<int> SeedCheckpointsAsync(Guid tenantId, Guid factoryId, CancellationToken cancellationToken = default)
    {
        if (!await dbContext.OrgNodes.AnyAsync(x => x.TenantId == tenantId && x.Id == factoryId && x.Level == OrgNodeLevels.Factory, cancellationToken))
            throw new BusinessRuleException("Choose a factory.");
        if (await dbContext.Checkpoints.AnyAsync(x => x.TenantId == tenantId && x.FactoryId == factoryId, cancellationToken))
            throw new BusinessRuleException("This factory already has checkpoints; add or edit them one by one.");
        var zones = await dbContext.AccessZones.AsNoTracking().Where(x => x.TenantId == tenantId && x.FactoryId == factoryId).ToListAsync(cancellationToken);
        var next = await dbContext.Checkpoints.CountAsync(x => x.TenantId == tenantId, cancellationToken);
        var route = new PatrolRoute { TenantId = tenantId, FactoryId = factoryId, Name = "Night round", Ordered = true, MaxDurationMinutes = 60, MinGapSeconds = 20 };
        dbContext.PatrolRoutes.Add(route);
        var industry = await IndustryTemplates.ForFactoryAsync(dbContext, tenantId, factoryId, cancellationToken);
        var sequence = 0;
        foreach (var (name, zone, questions) in industry.Checkpoints)
        {
            var checkpoint = new Checkpoint
            {
                TenantId = tenantId, FactoryId = factoryId, Code = $"CP-{++next:000}", Name = name, Method = CheckpointMethods.QrOrNfc,
                TagToken = VisitorService.NewBadgeToken(), Questions = questions, DisplayOrder = ++sequence,
                AccessZoneId = zone is null ? null : zones.FirstOrDefault(x => x.Name == zone)?.Id,
            };
            dbContext.Checkpoints.Add(checkpoint);
            dbContext.PatrolRouteCheckpoints.Add(new PatrolRouteCheckpoint { TenantId = tenantId, RouteId = route.Id, CheckpointId = checkpoint.Id, Sequence = sequence });
        }

        dbContext.PatrolSchedules.Add(new PatrolSchedule { TenantId = tenantId, RouteId = route.Id, EveryMinutes = 120, WindowFrom = new TimeOnly(18, 0), WindowTo = new TimeOnly(6, 0), GraceMinutes = 15 });
        await dbContext.SaveChangesAsync(cancellationToken);
        return industry.Checkpoints.Count;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<Guid>> SupervisorsAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await dbContext.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive && (x.Role == AppRoles.Admin || x.Role == AppRoles.Manager
                || dbContext.UserPermissionOverrides.IgnoreQueryFilters().Any(o => o.UserId == x.Id && o.Capability == PermissionNames.SecurityManage && o.IsAllowed)))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

    private async Task NotifyAsync(NotificationRequest request, CancellationToken cancellationToken)
    {
        if (notificationDispatcher is null) return;
        try
        {
            await notificationDispatcher.NotifyAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogWarning(ex, "Security notification {Key} failed.", request.Key);
        }
    }

    internal static string Describe(string kind) => kind switch
    {
        SecurityIncidentKinds.UnauthorisedAccess => "Unauthorised access",
        SecurityIncidentKinds.AttemptedTheft => "Attempted theft",
        SecurityIncidentKinds.PerimeterDamage => "Perimeter damage",
        SecurityIncidentKinds.SuspiciousPerson => "Suspicious person",
        SecurityIncidentKinds.SuspiciousVehicle => "Suspicious vehicle",
        SecurityIncidentKinds.CheckpointIssue => "Checkpoint issue",
        _ => kind,
    };

    /// <summary>Great-circle distance in metres.</summary>
    internal static double Metres(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6_371_000;
        static double Rad(double deg) => deg * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * r * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    private static DateTime Local(DateTime utc) => utc.AddHours(3);

    private static DateTime Utc(DateTime local) => DateTime.SpecifyKind(local.AddHours(-3), DateTimeKind.Utc);
}

/// <summary>How NFC tag serials are stored.</summary>
public static class PatrolSerials
{
    public static string? Normalise(string? serial) => PatrolService.NormaliseSerial(serial);
}

/// <summary>Raises the next 12 hours of patrols and closes overdue ones (with alerts), every minute.</summary>
public sealed class PatrolWorker(IServiceScopeFactory serviceScopeFactory, ILogger<PatrolWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        var lastGenerated = DateTime.MinValue;
        do
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var patrol = scope.ServiceProvider.GetRequiredService<IPatrolService>();
                if (DateTime.UtcNow - lastGenerated >= TimeSpan.FromMinutes(15))
                {
                    var tenants = await db.PatrolSchedules.IgnoreQueryFilters().Where(x => x.IsActive).Select(x => x.TenantId).Distinct().ToListAsync(stoppingToken);
                    foreach (var tenantId in tenants) await patrol.GenerateRunsAsync(tenantId, DateTime.UtcNow, TimeSpan.FromHours(12), stoppingToken);
                    lastGenerated = DateTime.UtcNow;
                }

                await patrol.CloseOverdueAsync(null, DateTime.UtcNow, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Patrol cycle failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
