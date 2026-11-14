using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Ecosys.Tests.Factory.VisitorTests;

namespace Ecosys.Tests.Factory;

public sealed class PatrolTests
{
    // ------------------------------------------------------------------ schedules

    [Fact]
    public void ANightSchedule_RaisesAPatrolEveryTwoHours_FromSixToSix()
    {
        var schedule = new PatrolSchedule { EveryMinutes = 120, WindowFrom = new TimeOnly(18, 0), WindowTo = new TimeOnly(6, 0), Days = "1,2,3,4,5,6,7" };
        // Monday 16 Nov 2026, noon local (09:00 UTC) to Tuesday noon.
        var from = new DateTime(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
        var slots = PatrolService.Slots(schedule, from, from.AddDays(1)).ToList();

        // 18:00, 20:00 … 04:00 local = 15:00 … 01:00 UTC.
        Assert.Equal(6, slots.Count);
        Assert.Equal(new DateTime(2026, 11, 16, 15, 0, 0), slots[0]);
        Assert.Equal(new DateTime(2026, 11, 17, 1, 0, 0), slots[^1]);
    }

    [Fact]
    public void FixedTimes_AfterMidnight_BelongToTheNightBefore()
    {
        var schedule = new PatrolSchedule { FixedTimes = "22:00,02:00", WindowFrom = new TimeOnly(18, 0), WindowTo = new TimeOnly(6, 0), Days = "1" };
        var monday = new DateTime(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
        var slots = PatrolService.Slots(schedule, monday, monday.AddDays(2)).ToList();

        // Monday 22:00 and Tuesday 02:00 local, and nothing for Tuesday night (Mondays only).
        Assert.Equal([new DateTime(2026, 11, 16, 19, 0, 0), new DateTime(2026, 11, 16, 23, 0, 0)], slots);
    }

    [Fact]
    public async Task Seeding_AddsTheTeaCheckpointsAndANightRound_AndRunsAreRaisedOnce()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var patrol = new PatrolService(db);

        Assert.Equal(12, await patrol.SeedCheckpointsAsync(site.TenantId, site.FactoryId));
        Assert.Equal("CP-012", (await db.Checkpoints.OrderBy(x => x.Code).LastAsync()).Code);
        Assert.Equal("Fuel and firewood store", (await db.Checkpoints.Include(x => x.AccessZone).SingleAsync(x => x.Code == "CP-008")).AccessZone!.Name);
        await Assert.ThrowsAsync<BusinessRuleException>(() => patrol.SeedCheckpointsAsync(site.TenantId, site.FactoryId));

        var added = await patrol.GenerateRunsAsync(site.TenantId, DateTime.UtcNow, TimeSpan.FromHours(24));
        Assert.InRange(added, 5, 7);
        Assert.Equal(0, await patrol.GenerateRunsAsync(site.TenantId, DateTime.UtcNow, TimeSpan.FromHours(24)));
        Assert.All(await db.PatrolRuns.ToListAsync(), x => Assert.Equal((12, PatrolRunStatuses.Due), (x.RequiredCount, x.Status)));
    }

    // ------------------------------------------------------------------ scans

    [Fact]
    public async Task AGuardWalkingTheRoute_InOrder_CompletesThePatrol_AndAReplayedScanIsStoredOnce()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (site, route, points) = await RouteAsync(db);
        var patrol = new PatrolService(db);
        var run = await DueRunAsync(db, site, route);
        var guard = Guard(site);
        var t = DateTime.UtcNow.AddMinutes(-10);

        var first = await patrol.ScanAsync(site.TenantId, Scan(points[0], t, near: true), guard);
        Assert.Equal((ScanValidations.Valid, run.Id, PatrolRunStatuses.InProgress), (first.Scan.Validation, first.Run!.Id, first.Run.Status));
        Assert.Equal("Gate Officer", first.Run.GuardName);

        var replay = await patrol.ScanAsync(site.TenantId, Scan(points[0], t, near: true) with { ClientScanId = first.Scan.ClientScanId }, guard);
        Assert.Equal(first.Scan.Id, replay.Scan.Id);
        Assert.Equal(1, await db.CheckpointScans.CountAsync());

        await patrol.ScanAsync(site.TenantId, Scan(points[1], t.AddMinutes(3), near: true), guard);
        // The substation is tapped with its NFC tag.
        var last = await patrol.ScanAsync(site.TenantId, new NewScan(Guid.NewGuid(), "04A22B9C1180", t.AddMinutes(6), Latitude: points[2].Latitude, Longitude: points[2].Longitude, ByNfc: true), guard);
        Assert.Equal((PatrolRunStatuses.Completed, 3, 3, 0), (last.Run!.Status, last.Run.ValidCount, last.Run.RequiredCount, last.Run.FlaggedCount));
    }

    [Fact]
    public async Task ScansThatFailACheck_AreKeptAndFlagged()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (site, route, points) = await RouteAsync(db);
        var patrol = new PatrolService(db);
        var run = await DueRunAsync(db, site, route);
        var guard = Guard(site);
        var t = DateTime.UtcNow.AddMinutes(-20);

        // Skipping the gate to the store.
        var skipped = await patrol.ScanAsync(site.TenantId, Scan(points[1], t, near: true) with { RunId = run.Id }, guard);
        Assert.Equal(ScanValidations.OutOfSequence, skipped.Scan.Validation);
        Assert.Contains("CP-001", skipped.Scan.ValidationNote);

        await patrol.ScanAsync(site.TenantId, Scan(points[0], t.AddMinutes(3), near: true), guard);
        // 10 s later at a point 300 m away: a photographed label, not a walk.
        var fast = await patrol.ScanAsync(site.TenantId, Scan(points[1], t.AddMinutes(3).AddSeconds(10), near: true), guard);
        Assert.Equal(ScanValidations.TooFast, fast.Scan.Validation);
        // Scanned from 400 m away.
        var far = await patrol.ScanAsync(site.TenantId, Scan(points[1], t.AddMinutes(8), near: false), guard);
        Assert.Equal(ScanValidations.OutsideGeofence, far.Scan.Validation);
        Assert.True(far.Scan.DistanceM > 300);
        var ok = await patrol.ScanAsync(site.TenantId, Scan(points[1], t.AddMinutes(9), near: true), guard);
        Assert.Equal(ScanValidations.Valid, ok.Scan.Validation);
        Assert.Equal(ScanValidations.Repeat, (await patrol.ScanAsync(site.TenantId, Scan(points[1], t.AddMinutes(10), near: true), guard)).Scan.Validation);

        // The substation needs its NFC tag: a QR scan is the wrong tag, a tap of the registered tag counts.
        var qr = await patrol.ScanAsync(site.TenantId, Scan(points[2], t.AddMinutes(14), near: true), guard);
        Assert.Equal(ScanValidations.WrongTag, qr.Scan.Validation);
        var tap = await patrol.ScanAsync(site.TenantId, new NewScan(Guid.NewGuid(), "04:a2:2b:9c:11:80", t.AddMinutes(15), Latitude: points[2].Latitude, Longitude: points[2].Longitude, ByNfc: true), guard);
        Assert.Equal((ScanValidations.Valid, CheckpointMethods.Nfc), (tap.Scan.Validation, tap.Scan.Method));

        var saved = await db.PatrolRuns.SingleAsync();
        Assert.Equal((PatrolRunStatuses.Completed, 3, 4), (saved.Status, saved.ValidCount, saved.FlaggedCount));
        Assert.Equal(8, await db.CheckpointScans.CountAsync());
    }

    [Fact]
    public async Task APhoneClockAhead_IsFlagged_AndAnUnknownCodeIsRefused()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (site, route, points) = await RouteAsync(db);
        var patrol = new PatrolService(db);
        await DueRunAsync(db, site, route);

        var ahead = await patrol.ScanAsync(site.TenantId, Scan(points[0], DateTime.UtcNow.AddMinutes(30), near: true), Guard(site));
        Assert.Equal(ScanValidations.ClockSkew, ahead.Scan.Validation);
        Assert.True(ahead.Scan.ScannedAt <= DateTime.UtcNow);
        await Assert.ThrowsAsync<NotFoundException>(() => patrol.ScanAsync(site.TenantId, new NewScan(Guid.NewGuid(), "CHECKPOINT:not-a-real-token-123"), Guard(site)));
    }

    [Fact]
    public async Task ANoAtACheckpoint_RaisesASecurityIncident_ForTheSupervisors()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (site, route, points) = await RouteAsync(db);
        var dispatcher = new RecordingDispatcher();
        var patrol = new PatrolService(db, dispatcher);
        await DueRunAsync(db, site, route);

        var outcome = await patrol.ScanAsync(site.TenantId, Scan(points[1], DateTime.UtcNow, near: true) with
        {
            Answers = [new ScanAnswer("Padlock intact?", false), new ScanAnswer("No fuel leaks?", true)], Note = "Hasp cut",
        }, Guard(site));

        var incident = await db.Incidents.SingleAsync();
        Assert.Equal(outcome.IncidentId, incident.Id);
        Assert.Equal((IncidentCategories.Security, SecurityIncidentKinds.CheckpointIssue, SecurityResponseStatuses.Reported, points[1].Id),
            (incident.Category, incident.Kind, incident.ResponseStatus, incident.CheckpointId));
        Assert.Contains("Padlock intact?", incident.Title);
        var sent = Assert.Single(dispatcher.Requests);
        Assert.Equal(PatrolService.IncidentKey, sent.Key);
        Assert.Contains(site.Manager.UserId, sent.UserIds);
        Assert.DoesNotContain(site.Gate.UserId, sent.UserIds);
    }

    // ------------------------------------------------------------------ closing

    [Fact]
    public async Task OverduePatrols_CloseAsMissedOrPartial_WithOneAlertPerFactory_AndALateSyncedScanStillCounts()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (site, route, points) = await RouteAsync(db);
        var dispatcher = new RecordingDispatcher();
        var patrol = new PatrolService(db, dispatcher);
        var start = DateTime.UtcNow.AddHours(-3);
        var missed = await RunAtAsync(db, site, route, start);
        var partial = await RunAtAsync(db, site, route, start.AddMinutes(30));
        await patrol.ScanAsync(site.TenantId, Scan(points[0], start.AddMinutes(35), near: true) with { RunId = partial.Id }, Guard(site));

        var result = await patrol.CloseOverdueAsync(site.TenantId, DateTime.UtcNow);
        Assert.Equal((2, 1), (result.Closed, result.Alerts));
        Assert.Equal(PatrolRunStatuses.Missed, (await db.PatrolRuns.SingleAsync(x => x.Id == missed.Id)).Status);
        Assert.Equal(PatrolRunStatuses.Partial, (await db.PatrolRuns.SingleAsync(x => x.Id == partial.Id)).Status);
        var alert = Assert.Single(dispatcher.Requests);
        Assert.Equal((PatrolService.MissedKey, NotificationSeverities.Critical), (alert.Key, alert.Severity));
        Assert.Contains("missed", alert.Body);
        var again = await patrol.CloseOverdueAsync(site.TenantId, DateTime.UtcNow);
        Assert.Equal((0, 0), (again.Closed, again.Alerts));

        // The guard was there; the phone had no signal and synced later. It moves the patrol from missed to partial.
        await patrol.ScanAsync(site.TenantId, Scan(points[0], start.AddMinutes(5), near: true) with { RunId = missed.Id }, Guard(site));
        Assert.Equal(PatrolRunStatuses.Partial, (await db.PatrolRuns.SingleAsync(x => x.Id == missed.Id)).Status);
    }

    // ------------------------------------------------------------------ guard and supervisor

    [Fact]
    public async Task OnlyGuardsAndSupervisors_UsePatrol_AndThePanicButtonAlertsEveryone()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (site, _, _) = await RouteAsync(db);
        var dispatcher = new RecordingDispatcher();
        var staff = Controller(db, new FactoryTenantContext(site.TenantId, site.Host.UserId, isAdmin: false), dispatcher);
        await Assert.ThrowsAsync<ForbiddenException>(() => staff.Guard(null, default));

        var guard = Controller(db, new FactoryTenantContext(site.TenantId, site.Gate.UserId, isAdmin: false, PermissionNames.SecurityPatrol), dispatcher);
        Assert.Equal((true, false), (Ok(guard.Me()).CanPatrol, Ok(guard.Me()).CanManage));
        await Assert.ThrowsAsync<ForbiddenException>(() => guard.Board(null, null, default));
        var panic = Ok(await guard.Panic(new PanicRequest(site.FactoryId, -0.3672, 35.2831), default));
        Assert.Equal((SecurityIncidentKinds.Emergency, "Serious"), (panic.Kind, panic.Severity));
        var alert = Assert.Single(dispatcher.Requests);
        Assert.StartsWith("EMERGENCY", alert.Title);
        Assert.Contains("maps.google.com/?q=-0.367200,35.283100", alert.Body);

        var handover = Ok(await guard.HandOver(new HandoverRequest(site.FactoryId, "Night", site.Manager.UserId, "Fence breach near CP-012 reported", "2 radios, 1 torch"), default));
        Assert.Equal(("Pat Manager", (DateTime?)null), (handover.IncomingName, handover.AcknowledgedAt));
        await Assert.ThrowsAsync<BusinessRuleException>(() => guard.Acknowledge(handover.Id, default));
        var supervisor = Controller(db, new FactoryTenantContext(site.TenantId, site.Manager.UserId, isAdmin: false, PermissionNames.SecurityManage), dispatcher);
        Assert.NotNull(Ok(await supervisor.Acknowledge(handover.Id, default)).AcknowledgedAt);

        var closed = Ok(await supervisor.UpdateIncident(panic.Id, new UpdateSecurityIncidentRequest(SecurityResponseStatuses.Closed, site.Manager.UserId, "OB 12/13/11/2026", true), default));
        Assert.Equal((IncidentStatuses.Closed, "Pat Manager", "OB 12/13/11/2026", true), (closed.Status, closed.OwnerName, closed.PoliceObNumber, closed.ReportedToPolice));
    }

    [Fact]
    public async Task ACheckpointThatAsksForAPhoto_TakesItAfterTheScan_AndTheSupervisorSeesIt()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (site, route, points) = await RouteAsync(db);
        points[1].RequirePhoto = true;
        await db.SaveChangesAsync();
        var run = await DueRunAsync(db, site, route);
        var storage = new MemoryStorage();
        PatrolController As(Guid userId, string permission)
        {
            var context = new FactoryTenantContext(site.TenantId, userId, isAdmin: false, permission);
            return WithUser(new PatrolController(db, context, FactoryTestSupport.Scope(db, context), new PatrolService(db), FactoryTestSupport.Audit(db), storage));
        }
        var guard = As(site.Gate.UserId, PermissionNames.SecurityPatrol);

        var bundle = Ok(await guard.Guard(null, default));
        Assert.Equal([false, true, false], points.Select(p => bundle.Checkpoints.Single(x => x.Id == p.Id).RequirePhoto));

        var t = DateTime.UtcNow.AddMinutes(-10);
        var scanId = Guid.NewGuid();
        await Assert.ThrowsAsync<NotFoundException>(() => guard.ScanPhoto(scanId, Jpeg(), default));
        Ok(await guard.Scan(new ScanRequest(scanId, $"CHECKPOINT:{points[1].TagToken}", t, run.Id, points[1].Latitude, points[1].Longitude, 5), default));

        await Assert.ThrowsAsync<BusinessRuleException>(() => guard.ScanPhoto(scanId, Photo("fake.jpg", "image/jpeg", "<script>"u8.ToArray()), default));
        var other = As(site.Host.UserId, PermissionNames.SecurityPatrol);
        await Assert.ThrowsAsync<ForbiddenException>(() => other.ScanPhoto(scanId, Jpeg(), default));

        var photo = Ok(await guard.ScanPhoto(scanId, Jpeg(), default));
        // Sent again from the phone's queue: the first photo stands.
        Assert.Equal(photo.PhotoUrl, Ok(await guard.ScanPhoto(scanId, Jpeg(), default)).PhotoUrl);
        var attachment = Assert.Single(await db.Attachments.ToListAsync());
        Assert.Equal(("CheckpointScan", "CP-002"), (attachment.EntityType, attachment.FileName[..6]));

        var supervisor = As(site.Manager.UserId, PermissionNames.SecurityManage);
        var scan = Assert.Single(Ok(await supervisor.Run(run.Id, default)).Scans);
        Assert.Equal((true, photo.PhotoUrl), (scan.PhotoRequired, scan.PhotoUrl));
    }

    private static Microsoft.AspNetCore.Http.IFormFile Jpeg() => Photo("checkpoint.jpg", "image/jpeg", [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1]);

    private static Microsoft.AspNetCore.Http.IFormFile Photo(string name, string type, byte[] content) =>
        new Microsoft.AspNetCore.Http.FormFile(new MemoryStream(content), 0, content.Length, "file", name) { Headers = new Microsoft.AspNetCore.Http.HeaderDictionary(), ContentType = type };

    [Fact]
    public void Patrol_IsTheSecurityAddOn()
    {
        Assert.Equal([ServiceOpsPlans.Security], ServiceOpsPlans.RequiredModules("Patrol", "/api/security/guard"));
        Assert.Equal([ServiceOpsPlans.Security], ServiceOpsPlans.RequiredModules("PatrolSetup", "/api/security/checkpoints"));
        Assert.Contains(PermissionNames.SecurityPatrol, PermissionNames.CapabilityOnly);
    }

    // ------------------------------------------------------------------ PostgreSQL

    [Fact]
    public async Task Postgres_PatrolQueries_AllTranslate()
    {
        await FactoryPostgres.RunAsync("patrol", async connection =>
        {
            var (db, context) = await FactoryPostgres.SeedAsync(connection);
            await using var _ = db;
            var tenantId = context.TenantId!.Value;
            var factory = new OrgNode { TenantId = tenantId, Level = OrgNodeLevels.Factory, Name = "Chelal", Code = "CHL" };
            db.Add(factory);
            await db.SaveChangesAsync();
            var patrol = new PatrolService(db);
            var setup = WithUser(new PatrolSetupController(db, context, FactoryTestSupport.Scope(db, context), patrol, FactoryTestSupport.Audit(db), new AssetLabelPdfRenderer()));
            var checkpoints = Ok(await setup.Seed(new SeedCheckpointsRequest(factory.Id), default));
            Assert.Equal(12, checkpoints.Count);
            var gate = checkpoints[0];
            Ok(await setup.SetPosition(gate.Id, new CheckpointPositionRequest(-0.3672, 35.2831, 8), default));
            Ok(await setup.SetNfc(checkpoints[1].Id, new CheckpointNfcRequest("04:A2:2B:9C:11:80"), default));
            await Assert.ThrowsAsync<ConflictException>(() => setup.SetNfc(checkpoints[2].Id, new CheckpointNfcRequest("04a22b9c1180"), default));
            var labels = Assert.IsType<FileContentResult>(await setup.Labels(new CheckpointLabelsRequest(factory.Id), default));
            Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(labels.FileContents, 0, 4));

            var route = Assert.Single(Ok(await setup.Routes(null, default)));
            Assert.Equal(("Night round", 12), (route.Name, route.Checkpoints.Count));
            Assert.Contains("every 2 h 18:00–06:00", Assert.Single(route.Schedules).Description);
            var shortRoute = Ok(await setup.CreateRoute(new SaveRouteRequest(factory.Id, "Gate and stores", [new RouteStopRequest(gate.Id), new RouteStopRequest(checkpoints[1].Id, false)], MaxDurationMinutes: 30), default));
            Ok(await setup.AddSchedule(shortRoute.Id, new SaveScheduleRequest(FixedTimes: "22:00, 02:00"), default));

            var guard = WithUser(new PatrolController(db, context, FactoryTestSupport.Scope(db, context), patrol, FactoryTestSupport.Audit(db)));
            var own = Ok(await guard.StartOwn(shortRoute.Id, default));
            var token = (await db.Checkpoints.SingleAsync(x => x.Id == gate.Id)).TagToken;
            var scan = Ok(await guard.Scan(new ScanRequest(Guid.NewGuid(), $"CHECKPOINT:{token}", Latitude: -0.3672, Longitude: 35.2831, AccuracyM: 5), default));
            Assert.Equal((ScanValidations.Valid, own.Id, PatrolRunStatuses.Completed), (scan.Validation, scan.RunId, scan.RunStatus));

            var bundle = Ok(await guard.Guard(null, default));
            Assert.Equal(12, bundle.Checkpoints.Count);
            Assert.Equal(2, bundle.Routes.Count);
            Assert.Contains("Gate locked?", bundle.Checkpoints.Single(x => x.Id == gate.Id).Questions);
            var board = Ok(await guard.Board(null, null, default));
            Assert.Contains(board.Runs, x => x.Id == own.Id);
            Assert.Equal(gate.Code, board.LastScan!.Code);
            var detail = Ok(await guard.Run(own.Id, default));
            Assert.Equal((2, 1), (detail.Stops.Count, detail.Scans.Count));

            var incident = Ok(await guard.Report(new ReportSecurityIncidentRequest(factory.Id, "PerimeterDamage", "Fence cut behind the boiler house"), default));
            Assert.Single(Ok(await guard.Incidents(null, true, default)));
            Ok(await guard.UpdateIncident(incident.Id, new UpdateSecurityIncidentRequest(SecurityResponseStatuses.Responding), default));
            Ok(await guard.HandOver(new HandoverRequest(factory.Id, "Night"), default));
            Assert.Single(Ok(await guard.Handovers(null, default)));

            await patrol.CloseOverdueAsync(tenantId, DateTime.UtcNow.AddHours(1));
            var report = Ok(await guard.Report(null, null, DateTime.UtcNow.AddDays(1), default));
            Assert.Equal((1, 1, 100), (report.Patrols, report.Completed, report.CheckpointPercent));
            Assert.Equal("Perimeter damage", Assert.Single(report.Incidents).Kind);
            Assert.Equal(gate.Code, report.ByCheckpoint[0].Code);
        });
    }

    // ------------------------------------------------------------------ helpers

    private static PatrolActor Guard(VisitorTests.Site site) => new(site.Gate.UserId, site.Gate.Name);

    /// <summary>A gate, a fuel store 300 m away and an NFC-only substation, walked in order.</summary>
    private static async Task<(VisitorTests.Site Site, PatrolRoute Route, Checkpoint[] Points)> RouteAsync(AppDbContext db)
    {
        var site = await SeedAsync(db);
        Checkpoint Point(string code, string name, double lat, string method = CheckpointMethods.QrOrNfc, string? serial = null) => new()
        {
            TenantId = site.TenantId, FactoryId = site.FactoryId, Code = code, Name = name, Method = method, TagToken = GateText.NewToken(),
            Latitude = lat, Longitude = 35.2831, RadiusM = 50, NfcSerial = serial,
        };
        var points = new[]
        {
            Point("CP-001", "Main gate", -0.3672),
            Point("CP-002", "Fuel store", -0.3699),
            Point("CP-003", "Substation", -0.3702, CheckpointMethods.Nfc, "04A22B9C1180"),
        };
        var route = new PatrolRoute { TenantId = site.TenantId, FactoryId = site.FactoryId, Name = "Night round", Ordered = true, MaxDurationMinutes = 60, MinGapSeconds = 20 };
        db.AddRange(points);
        db.Add(route);
        for (var i = 0; i < points.Length; i++)
            db.Add(new PatrolRouteCheckpoint { TenantId = site.TenantId, RouteId = route.Id, CheckpointId = points[i].Id, Sequence = i + 1 });
        await db.SaveChangesAsync();
        return (site, route, points);
    }

    private static Task<PatrolRun> DueRunAsync(AppDbContext db, VisitorTests.Site site, PatrolRoute route) => RunAtAsync(db, site, route, DateTime.UtcNow.AddMinutes(-30));

    private static async Task<PatrolRun> RunAtAsync(AppDbContext db, VisitorTests.Site site, PatrolRoute route, DateTime start)
    {
        var run = new PatrolRun
        {
            TenantId = site.TenantId, FactoryId = site.FactoryId, RouteId = route.Id, DueFrom = start, DueBy = start.AddMinutes(60), CloseAt = start.AddMinutes(75), RequiredCount = 3,
        };
        db.PatrolRuns.Add(run);
        await db.SaveChangesAsync();
        return run;
    }

    private static NewScan Scan(Checkpoint point, DateTime at, bool near) => new(
        Guid.NewGuid(), $"CHECKPOINT:{point.TagToken}", at, Latitude: near ? point.Latitude : point.Latitude! + 0.0036, Longitude: point.Longitude, AccuracyM: 8);

    private static PatrolController Controller(AppDbContext db, ITenantContext context, INotificationDispatcher dispatcher) =>
        WithUser(new PatrolController(db, context, FactoryTestSupport.Scope(db, context), new PatrolService(db, dispatcher), FactoryTestSupport.Audit(db)));

    private sealed class RecordingDispatcher : INotificationDispatcher
    {
        public List<NotificationRequest> Requests { get; } = [];

        public Task NotifyAsync(NotificationRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.CompletedTask;
        }
    }
}
