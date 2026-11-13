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

/// <summary>The vehicle log, standing passes and offline-dated check-outs.</summary>
public sealed class GateTests
{
    // ------------------------------------------------------------------ vehicle log

    [Fact]
    public async Task AVehicle_ComesInOnce_AndGoesOut()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var log = new VehicleLogService(db);

        var lorry = await log.EnterAsync(site.TenantId, new NewVehicleEntry(site.FactoryId, "kbz 442q", "delivery", "Peter Otieno", "0722 000 111", "Isuzu FRR, fertiliser"), site.Gate);
        Assert.Equal(("KBZ442Q", VehiclePurposes.Delivery, "+254722000111", "Gate Officer"), (lorry.Registration, lorry.Purpose, lorry.DriverPhone, lorry.EnteredByName));
        await Assert.ThrowsAsync<ConflictException>(() => log.EnterAsync(site.TenantId, new NewVehicleEntry(site.FactoryId, "KBZ-442Q", "Delivery"), site.Gate));
        await Assert.ThrowsAsync<BusinessRuleException>(() => log.EnterAsync(site.TenantId, new NewVehicleEntry(site.FactoryId, "KBZ 1", "Lorry"), site.Gate));

        var out1 = await log.ExitAsync(site.TenantId, lorry.Id, "Empty, searched", site.Gate);
        Assert.NotNull(out1.ExitedAt);
        await Assert.ThrowsAsync<BusinessRuleException>(() => log.ExitAsync(site.TenantId, lorry.Id, null, site.Gate));
        // Once out it may come back.
        await log.EnterAsync(site.TenantId, new NewVehicleEntry(site.FactoryId, "KBZ 442Q", "Delivery"), site.Gate);
        Assert.Equal(2, await db.VehicleMovements.CountAsync());
    }

    [Fact]
    public async Task CheckingInAVisitorWithAVehicle_OpensItsEntry_AndTheLogFlagsItWhenTheVisitIsOver()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var visitors = Service(db);
        var visit = await visitors.CreateAsync(site.TenantId,
            new NewVisit(site.FactoryId, "Contractor work", [new VisitorDetails("Sam Kiprono", "0711222333")], VehicleRegistration: "KCD 900X"), site.Host);
        var attendee = await db.VisitAttendees.SingleAsync();

        await visitors.CheckInAsync(site.TenantId, attendee.Id, new GateConfirmation(), site.Gate);
        var entry = await db.VehicleMovements.SingleAsync();
        Assert.Equal(("KCD900X", VehiclePurposes.Contractor, visit.Id, "Sam Kiprono"), (entry.Registration, entry.Purpose, entry.VisitId, entry.DriverName));

        await visitors.CheckOutAsync(site.TenantId, attendee.Id, site.Gate);
        var gate = GateController(db, site.Context);
        var inside = Assert.Single(Ok(await gate.Vehicles(null, true, null, null, null, default)));
        Assert.True(inside.VisitOver);
        Assert.Equal(visit.VisitNumber, inside.VisitNumber);
    }

    // ------------------------------------------------------------------ standing passes

    [Fact]
    public void APass_IsGoodOnItsDaysAndHours_Only()
    {
        var pass = new VisitorPass
        {
            PassNumber = "PASS-00001", ValidFrom = new DateTime(2026, 11, 1), ValidTo = new DateTime(2026, 11, 30), Days = "1,2,3,4,5",
            DailyFrom = new TimeOnly(7, 0), DailyTo = new TimeOnly(17, 0),
        };
        // Times are UTC; the factory is UTC+3. Monday 16 Nov 2026, 09:00 local.
        var monday = new DateTime(2026, 11, 16, 6, 0, 0, DateTimeKind.Utc);
        var window = VisitorPassRules.Window(pass, monday);
        Assert.True(window.Usable);
        Assert.Equal((new DateTime(2026, 11, 16, 4, 0, 0), new DateTime(2026, 11, 16, 14, 0, 0)), (window.From, window.To));

        Assert.Contains("Saturdays", VisitorPassRules.Window(pass, monday.AddDays(5)).Reason);
        Assert.Contains("07:00–17:00", VisitorPassRules.Window(pass, monday.AddHours(10)).Reason);   // 19:00 local
        Assert.True(VisitorPassRules.Window(pass, monday.AddHours(-2.5)).Usable);                    // 06:30: up to an hour early
        Assert.Contains("expired", VisitorPassRules.Window(pass, monday.AddDays(15)).Reason);
        Assert.Contains("starts", VisitorPassRules.Window(pass, monday.AddDays(-21)).Reason);   // Monday 26 Oct
        pass.Status = VisitorPassStatuses.Revoked;
        pass.RevokedByName = "Pat";
        pass.RevokedReason = "Contract ended";
        Assert.Contains("Contract ended", VisitorPassRules.Window(pass, monday).Reason);
    }

    [Fact]
    public async Task ScanningAPass_RaisesTodaysApprovedVisit_OncePerStay_AndTheGateChecksStillRun()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var passes = Passes(db);
        var boiler = await db.AccessZones.SingleAsync(x => x.Name == "Boiler house");
        var contractor = new Contractor { TenantId = site.TenantId, Name = "Boilerworks Ltd" };
        db.Contractors.Add(contractor);
        db.ContractorDocuments.AddRange(
            new ContractorDocument { TenantId = site.TenantId, ContractorId = contractor.Id, DocumentType = ContractorDocumentTypes.Insurance, ExpiresOn = DateTime.UtcNow.AddDays(60) },
            new ContractorDocument { TenantId = site.TenantId, ContractorId = contractor.Id, DocumentType = ContractorDocumentTypes.TaxCompliance, ExpiresOn = DateTime.UtcNow.AddDays(60) });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() => passes.CreateAsync(site.TenantId, Pass(site, contractor.Id, boiler.Id), site.Gate));
        var pass = await passes.CreateAsync(site.TenantId, Pass(site, contractor.Id, boiler.Id), site.Manager);
        Assert.Equal(("PASS-00001", "1,2,3,4,5,6,7", "Grace Host"), (pass.PassNumber, pass.Days, pass.HostName));
        await Assert.ThrowsAsync<ConflictException>(() => passes.CreateAsync(site.TenantId, Pass(site, contractor.Id, boiler.Id) with { Person = new VisitorDetails("Sam Kiprono", "0711222333") }, site.Manager));

        // The badge token resolves to today's visit, raised approved with the pass's zones and contractor.
        var visitors = Service(db);
        Assert.Null(await visitors.FindBadgeAsync(site.TenantId, $"VISITOR:{pass.BadgeToken}"));
        var today = await passes.TodayAsync(site.TenantId, pass.Id, site.Gate);
        var visit = await db.Visits.Include(x => x.Zones).SingleAsync();
        Assert.Equal((VisitStatuses.Approved, VisitSources.Pass, pass.Id, contractor.Id, "KCD900X"), (visit.Status, visit.Source, visit.PassId, visit.ContractorId, visit.VehicleRegistration));
        Assert.Equal([boiler.Id], visit.Zones.Select(x => x.AccessZoneId));
        Assert.Equal(today.Id, (await passes.TodayAsync(site.TenantId, pass.Id, site.Gate)).Id);

        // Every day's checks still apply: the boiler house needs an induction and an escort.
        var check = await visitors.CheckAsync(site.TenantId, today.Id, new GateConfirmation(PpeConfirmed: true), DateTime.UtcNow);
        Assert.Equal(["induction", "escort"], check.Issues.Select(x => x.Code));
        await visitors.CheckInAsync(site.TenantId, today.Id, new GateConfirmation(PpeConfirmed: true, RecordInduction: true, EscortUserId: site.Host.UserId), site.Gate);
        Assert.Equal(today.Id, (await passes.TodayAsync(site.TenantId, pass.Id, site.Gate)).Id);

        // Out and back in the same day: a fresh visit, and the induction is remembered.
        await visitors.CheckOutAsync(site.TenantId, today.Id, site.Gate);
        var again = await passes.TodayAsync(site.TenantId, pass.Id, site.Gate);
        Assert.NotEqual(today.Id, again.Id);
        Assert.Equal(["escort"], (await visitors.CheckAsync(site.TenantId, again.Id, new GateConfirmation(PpeConfirmed: true), DateTime.UtcNow)).Issues.Select(x => x.Code));
        Assert.Equal(["VIS-000001", "VIS-000002"], await db.Visits.OrderBy(x => x.VisitNumber).Select(x => x.VisitNumber).ToListAsync());

        // Papers lapsing stop the pass at the gate.
        (await db.ContractorDocuments.FirstAsync(x => x.DocumentType == ContractorDocumentTypes.Insurance)).ExpiresOn = DateTime.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();
        Assert.Contains("contractor", (await visitors.CheckAsync(site.TenantId, again.Id, new GateConfirmation(PpeConfirmed: true), DateTime.UtcNow)).Issues.Select(x => x.Code));

        // A revoked pass no longer raises visits.
        await Assert.ThrowsAsync<BusinessRuleException>(() => passes.RevokeAsync(site.TenantId, pass.Id, "", site.Manager));
        await passes.RevokeAsync(site.TenantId, pass.Id, "Agreement ended", site.Manager);
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => passes.TodayAsync(site.TenantId, pass.Id, site.Gate));
        Assert.Contains("Agreement ended", ex.Message);
    }

    [Fact]
    public async Task ThePassBadge_ScannedAtTheGate_FindsTodaysVisit()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var pass = await Passes(db).CreateAsync(site.TenantId, Pass(site, null, null), site.Manager);
        var visits = WithUser(new VisitsController(db, site.Context, FactoryTestSupport.Scope(db, site.Context), Service(db), FactoryTestSupport.Settings(db),
            FactoryTestSupport.Audit(db), new VisitorBadgePdfRenderer(), Passes(db)));

        var badge = Ok(await visits.Badge($"VISITOR:{pass.BadgeToken}", default));
        Assert.Equal(("Sam Kiprono", AttendeeStates.Expected, VisitSources.Pass), (badge.Attendee.FullName, badge.Attendee.State, badge.Visit.Source));

        var gate = GateController(db, site.Context);
        var row = Assert.Single(Ok(await gate.Passes(null, false, "sam", default)));
        Assert.Equal(("Every day", "00:00", "23:59", true), (row.DaysLabel, row.DailyFrom, row.DailyTo, row.UsableNow));
        var pdf = Assert.IsType<FileContentResult>(await gate.PassBadge(pass.Id, default));
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdf.FileContents, 0, 4));
    }

    // ------------------------------------------------------------------ offline

    [Fact]
    public async Task ACheckOutScannedOffline_IsDatedWhenItWasScanned()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var visitors = Service(db);
        await visitors.CreateAsync(site.TenantId, new NewVisit(site.FactoryId, "Meeting", [new VisitorDetails("Ann One")]), site.Host);
        var attendee = await db.VisitAttendees.SingleAsync();
        await visitors.CheckInAsync(site.TenantId, attendee.Id, new GateConfirmation(), site.Gate);
        var checkedIn = (await db.VisitAttendees.SingleAsync()).CheckedInAt!.Value;

        var scannedAt = DateTime.UtcNow.AddMinutes(-20);
        try
        {
            Assert.True(OccurredAt.TrySet(scannedAt));
            await visitors.CheckOutAsync(site.TenantId, attendee.Id, site.Gate);
        }
        finally
        {
            OccurredAt.Clear();
        }

        // Never before they came in.
        Assert.Equal(checkedIn, (await db.VisitAttendees.SingleAsync()).CheckedOutAt);
    }

    // ------------------------------------------------------------------ PostgreSQL

    [Fact]
    public async Task Postgres_GateQueries_AllTranslate()
    {
        await FactoryPostgres.RunAsync("gate", async connection =>
        {
            var (db, context) = await FactoryPostgres.SeedAsync(connection);
            await using var _ = db;
            var tenantId = context.TenantId!.Value;
            var factory = new OrgNode { TenantId = tenantId, Level = OrgNodeLevels.Factory, Name = "Chelal", Code = "CHL" };
            db.Add(factory);
            await db.SaveChangesAsync();
            await Service(db).SeedZonesAsync(tenantId, factory.Id);
            var hall = await db.AccessZones.SingleAsync(x => x.Name == "Processing hall");

            var gate = GateController(db, context);
            var lorry = Ok(await gate.Enter(new VehicleEntryRequest(factory.Id, "kbz 442q", "Delivery", "Peter", Description: "Fertiliser"), default));
            Assert.Single(Ok(await gate.Vehicles(null, true, null, null, "kbz 44", default)));
            Assert.Single(Ok(await gate.Vehicles(factory.Id, false, null, null, "peter", default)));
            Assert.NotNull(Ok(await gate.Exit(lorry.Id, new VehicleExitRequest("Empty"), default)).ExitedAt);
            Assert.Empty(Ok(await gate.Vehicles(null, true, null, null, null, default)));

            var pass = Ok(await gate.CreatePass(new CreateVisitorPassRequest(factory.Id, new VisitorDetailsRequest("Sam Kiprono", "0711222333", Company: "Boilerworks"),
                DateTime.UtcNow.Date.AddDays(-1), DateTime.UtcNow.Date.AddDays(90), Days: [1, 2, 3, 4, 5, 6, 7], DailyFrom: "00:00", DailyTo: "23:59", ZoneIds: [hall.Id]), default));
            Assert.Equal(["Processing hall"], pass.Zones.Select(x => x.Name));
            Assert.Single(Ok(await gate.Passes(null, false, "boiler", default)));
            var today = Ok(await gate.Today(pass.Id, default));
            Assert.Equal(today.AttendeeId, Ok(await gate.Today(pass.Id, default)).AttendeeId);
            var visits = Controller(db, context);
            Assert.Equal(1, Ok(await visits.Board(null, default)).Expected);
            var index = Ok(await visits.GateIndex(null, default));
            Assert.Equal("Sam Kiprono", Assert.Single(index.People).FullName);
            Assert.Equal(["Processing hall"], index.People[0].Zones);
            Assert.Equal(pass.PassNumber, Assert.Single(index.Passes).PassNumber);
            Ok(await gate.RevokePass(pass.Id, new RevokeVisitorPassRequest("Ended"), default));
            Assert.Empty(Ok(await gate.Passes(null, false, null, default)));
            Assert.Single(Ok(await gate.Passes(null, true, null, default)));
        });
    }

    // ------------------------------------------------------------------ helpers

    private static VisitorPassService Passes(AppDbContext db) => new(db, Service(db));

    private static NewVisitorPass Pass(VisitorTests.Site site, Guid? contractorId, Guid? zoneId) => new(
        site.FactoryId, new VisitorDetails("Sam Kiprono", "0711 222 333", Company: "Boilerworks Ltd"), site.Host.UserId,
        DateTime.UtcNow.Date.AddDays(-1), DateTime.UtcNow.Date.AddDays(180), [1, 2, 3, 4, 5, 6, 7], new TimeOnly(0, 0), new TimeOnly(23, 59),
        zoneId is { } z ? [z] : null, contractorId, VehicleRegistration: "kcd 900x");

    private static VisitorGateController GateController(AppDbContext db, ITenantContext context) =>
        WithUser(new VisitorGateController(db, context, FactoryTestSupport.Scope(db, context), new VehicleLogService(db), Passes(db), FactoryTestSupport.Audit(db), new VisitorBadgePdfRenderer()));
}
