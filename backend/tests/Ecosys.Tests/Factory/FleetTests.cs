using System.Net;
using System.Text;
using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ecosys.Tests.Factory;

public sealed class FleetTests
{
    [Fact]
    public async Task AVehicle_IsAnAssetCodedByItsRegistration_WithATaggedOdometer()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var fleet = Fleet(db, site);

        var profile = await fleet.CreateAsync(site.TenantId, Vehicle(site, "kca 123a", odometer: 152_340m, engineHours: true), site.Actor);

        var asset = await db.Assets.SingleAsync();
        Assert.Equal(("KCA123A", "Isuzu FVR KCA123A", site.FactoryId, "Vehicle (Lorry)"), (asset.AssetCode, asset.AssetName, asset.OrgNodeId, asset.AssetType));
        var meters = await db.Meters.OrderBy(x => x.Name).ToListAsync();
        Assert.Equal([("Engine hours", "KCA123A.HRS", false), ("Odometer", "KCA123A.ODO", true)], meters.Select(x => (x.Name, x.ExternalId!, x.IsPrimary)));
        Assert.Equal(152_340m, meters[1].CurrentValue);
        Assert.Equal(meters[1].Id, profile.OdometerMeterId);

        await Assert.ThrowsAsync<ConflictException>(() => fleet.CreateAsync(site.TenantId, Vehicle(site, "KCA-123A"), site.Actor));
        await Assert.ThrowsAsync<BusinessRuleException>(() => fleet.CreateAsync(site.TenantId, Vehicle(site, "KDB 456B") with { OrgNodeId = null }, site.Actor));
        await Assert.ThrowsAsync<BusinessRuleException>(() => fleet.CreateAsync(site.TenantId, Vehicle(site, "X"), site.Actor));
    }

    [Fact]
    public void FuelEconomy_RunsBetweenFullTanks_AndFlagsAVehicleUsingMore()
    {
        static FuelLog Fill(int day, decimal litres, decimal? km, bool full = true) =>
            new() { FilledAt = new DateTime(2026, 10, day, 8, 0, 0, DateTimeKind.Utc), Litres = litres, OdometerKm = km, FullTank = full };

        // 500 km on 20 + 30 litres (a part fill in between) = 10 km/l; then 600 km on 60 l = 10 km/l.
        var steady = FleetService.Efficiency([Fill(1, 80, 1000), Fill(3, 20, null, full: false), Fill(5, 30, 1500), Fill(9, 60, 2100)]);
        Assert.Equal((10m, 10m, false), (steady.LatestKmPerLitre, steady.AverageKmPerLitre, steady.UsingMore));

        // Then 400 km on 60 l = 6.67 km/l: a fifth worse than the average.
        var worse = FleetService.Efficiency([Fill(1, 80, 1000), Fill(5, 50, 1500), Fill(9, 60, 2100), Fill(13, 60, 2500)]);
        Assert.Equal(6.67m, worse.LatestKmPerLitre);
        Assert.True(worse.UsingMore);

        Assert.Null(FleetService.Efficiency([Fill(1, 80, 1000)]).LatestKmPerLitre);
    }

    [Fact]
    public async Task AFillUp_PutsItsOdometerOnTheMeter_UnlessTheMeterHasALaterReading()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var fleet = Fleet(db, site);
        var profile = await fleet.CreateAsync(site.TenantId, Vehicle(site, "KCA 123A", odometer: 1000m), site.Actor);

        await fleet.AddFuelAsync(site.TenantId, profile.AssetId, new NewFuelLog(null, 60, 11_000, 1450, true, "Kericho Total", null), site.Actor);
        Assert.Equal(1450m, (await db.Meters.SingleAsync()).CurrentValue);

        await fleet.AddFuelAsync(site.TenantId, profile.AssetId, new NewFuelLog(DateTime.UtcNow.AddDays(-3), 40, null, 1200, true, null, null), site.Actor);
        Assert.Equal(1450m, (await db.Meters.SingleAsync()).CurrentValue);
        Assert.Equal(2, await db.FuelLogs.CountAsync());
        Assert.Equal(2, await db.MeterReadings.CountAsync()); // opening + the first fill only

        await Assert.ThrowsAsync<BusinessRuleException>(() => fleet.AddFuelAsync(site.TenantId, profile.AssetId, new NewFuelLog(null, 0, null, null, true, null, null), site.Actor));
    }

    [Fact]
    public async Task TrackerPositions_MoveTheVehicle_AndFeedItsMeters_AtMostEveryFifteenMinutes()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var fleet = Fleet(db, site);
        // No opening readings: they would be stamped "now", after these positions.
        var byDevice = await fleet.CreateAsync(site.TenantId, Vehicle(site, "KCA 123A", tracker: "359632100000001", engineHours: true), site.Actor);
        var byName = await fleet.CreateAsync(site.TenantId, Vehicle(site, "KDB 456B"), site.Actor);
        var start = DateTime.UtcNow.AddHours(-2);

        var result = await fleet.ApplyPositionsAsync(site.TenantId, [
            new VehiclePosition("359632100000001", start, -0.37, 35.28, 42, 1010m, 3000m),
            new VehiclePosition("359632100000001", start.AddMinutes(5), -0.38, 35.29, 50, 1014m, 3000.1m),   // too soon for the meters
            new VehiclePosition("359632100000001", start.AddMinutes(20), -0.39, 35.30, 0, 1025m, 3000.4m),
            new VehiclePosition("unknown-imei", start, 1, 1),
            new VehiclePosition("883", start, -0.4, 35.3, 30, 520m, Name: "KDB 456B"),
        ], "Traccar");

        Assert.Equal((2, 5), (result.Vehicles, result.Readings));
        Assert.Equal(["unknown-imei"], result.UnknownDevices);
        var tracked = await db.VehicleProfiles.SingleAsync(x => x.Id == byDevice.Id);
        Assert.Equal((-0.39, 35.30, 0m, "Traccar"), (tracked.LastLatitude!.Value, tracked.LastLongitude!.Value, tracked.LastSpeedKph!.Value, tracked.LastPositionSource));
        Assert.Equal(1025m, (await db.Meters.SingleAsync(x => x.Id == byDevice.OdometerMeterId)).CurrentValue);
        Assert.Equal(3000.4m, (await db.Meters.SingleAsync(x => x.Id == byDevice.EngineHoursMeterId)).CurrentValue);
        Assert.Equal(520m, (await db.Meters.SingleAsync(x => x.Id == byName.OdometerMeterId)).CurrentValue);
        Assert.All(await db.MeterReadings.ToListAsync(), x => Assert.Equal((MeterReadingSources.Sensor, "Traccar"), (x.Source, x.RecordedByName)));

        // An older position does not move the vehicle back; a new tracker reading from zero is a reset.
        await fleet.ApplyPositionsAsync(site.TenantId, [new VehiclePosition("359632100000001", start.AddMinutes(-30), 5, 5), new VehiclePosition("359632100000001", start.AddMinutes(60), null, null, null, 12m)], "Traccar");
        Assert.Equal(-0.39, (await db.VehicleProfiles.SingleAsync(x => x.Id == byDevice.Id)).LastLatitude);
        Assert.True((await db.MeterReadings.Where(x => x.MeterId == byDevice.OdometerMeterId).OrderBy(x => x.ReadAt).LastAsync()).IsReset);
    }

    [Fact]
    public async Task TheRegister_ShowsKilometresToService_PapersAndOpenJobs()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var fleet = Fleet(db, site);
        var profile = await fleet.CreateAsync(site.TenantId, Vehicle(site, "KCA 123A", odometer: 14_200m) with
        {
            InsuranceExpiresOn = DateTime.UtcNow.Date.AddDays(12), InspectionExpiresOn = DateTime.UtcNow.Date.AddDays(200),
        }, site.Actor);
        db.PreventiveMaintenancePlans.Add(new PreventiveMaintenancePlan
        {
            TenantId = site.TenantId, AssetId = profile.AssetId, OrgNodeId = site.FactoryId, Name = "10,000 km service", Status = "Active",
            TriggerType = PmTriggerTypes.Meter, MeterId = profile.OdometerMeterId, MeterInterval = 10_000, NextMeterDue = 15_000, Frequency = "Every 10000 km",
        });
        db.WorkOrders.Add(new WorkOrder { TenantId = site.TenantId, AssetId = profile.AssetId, WorkOrderNumber = "WO-1", Title = "Brakes squeal", Status = WorkOrderStatuses.Approved });
        await db.SaveChangesAsync();

        var row = Assert.Single(await fleet.ListAsync(site.TenantId, null));

        Assert.Equal((800m, "10,000 km service", 1), (row.KmToService, row.ServicePlanName, row.OpenJobs));
        Assert.Equal([("Insurance", 12), ("Inspection", 200)], row.Documents.Select(x => (x.Type, x.DaysLeft)));
        Assert.Equal(("KCA123A", "Lorry", ConditionLevels.Normal), (row.RegistrationNumber, row.VehicleType, row.Condition));
    }

    [Fact]
    public async Task PapersEndingSoon_AreWarnedOfOnce_AndAgainOnTheDay()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var fleet = Fleet(db, site);
        var today = DateTime.UtcNow.Date;
        await fleet.CreateAsync(site.TenantId, Vehicle(site, "KCA 123A") with { InsuranceExpiresOn = today.AddDays(10), SpeedGovernorExpiresOn = today.AddDays(90) }, site.Actor);
        await fleet.CreateAsync(site.TenantId, Vehicle(site, "KDB 456B") with { InspectionExpiresOn = today.AddDays(3) }, site.Actor);
        var dispatcher = new RecordingDispatcher();
        var runner = new FleetAlertRunner(db, dispatcher, NullLogger<FleetAlertRunner>.Instance);

        Assert.Equal(1, await runner.RunAsync(today.AddHours(7)));
        var message = Assert.Single(dispatcher.Sent);
        Assert.Equal(("fleet.document-expiring", "2 vehicle papers ending soon"), (message.Key, message.Title));
        Assert.Contains("KDB456B: Inspection ends", message.Body);
        Assert.DoesNotContain("Speed governor", message.Body);

        dispatcher.Sent.Clear();
        Assert.Equal(0, await runner.RunAsync(today.AddHours(8)));
        Assert.Equal(1, await runner.RunAsync(today.AddDays(3)));
        Assert.Contains("ends today", Assert.Single(dispatcher.Sent).Body);
    }

    [Fact]
    public async Task Traccar_PositionsAreRead_AndAFailedSyncIsRecorded()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var fleet = Fleet(db, site);
        await fleet.CreateAsync(site.TenantId, Vehicle(site, "KCA 123A", tracker: "359632100000001", engineHours: true), site.Actor);
        var secrets = new SecretEncryptionService(new EphemeralDataProtectionProvider());
        var connection = new TelematicsConnection { TenantId = site.TenantId, BaseUrl = "https://traccar.test", Username = "fleet", EncryptedPassword = secrets.Encrypt("s3cret") };
        db.TelematicsConnections.Add(connection);
        await db.SaveChangesAsync();
        var fixTime = DateTime.UtcNow.AddMinutes(-3).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var handler = new FakeTraccar(new Dictionary<string, string>
        {
            ["/api/devices"] = """[{"id": 7, "uniqueId": "359632100000001", "name": "KCA 123A"}, {"id": 8, "uniqueId": "999", "name": "Spare"}]""",
            ["/api/positions"] = $$$"""[{"deviceId": 7, "latitude": -0.367, "longitude": 35.283, "speed": 20.0, "fixTime": "{{{fixTime}}}", "attributes": {"totalDistance": 1234567.0, "hours": 36000000}}, {"deviceId": 8, "latitude": 0, "longitude": 0, "fixTime": "{{{fixTime}}}"}]""",
        });
        var sync = new TelematicsSync(db, fleet, secrets, [new TraccarProvider(new FakeHttpClientFactory(handler))], NullLogger<TelematicsSync>.Instance);

        var result = await sync.SyncAsync(connection);

        Assert.Equal((2, 1), (result.Fetched, result.Applied.Vehicles));
        Assert.Equal("Basic ZmxlZXQ6czNjcmV0", handler.Authorization); // fleet:s3cret
        var profile = await db.VehicleProfiles.Include(x => x.OdometerMeter).Include(x => x.EngineHoursMeter).SingleAsync();
        Assert.Equal((1234.6m, 10m, 37.0m), (profile.OdometerMeter!.CurrentValue!.Value, profile.EngineHoursMeter!.CurrentValue!.Value, profile.LastSpeedKph!.Value));
        Assert.Contains("999", connection.LastSyncError);

        handler.Status = HttpStatusCode.Unauthorized;
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => sync.SyncAsync(connection));
        Assert.Contains("user name and password", error.Message);
        Assert.Contains("401", (await db.TelematicsConnections.SingleAsync()).LastSyncError);
    }

    [Fact]
    public async Task PushedPositions_NeedAGatewayKey()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var fleet = Fleet(db, site);
        await fleet.CreateAsync(site.TenantId, Vehicle(site, "KCA 123A"), site.Actor);
        var (key, prefix, hash) = SensorKeys.Generate();
        db.SensorGateways.Add(new SensorGateway { TenantId = site.TenantId, Name = "Wialon forwarder", KeyPrefix = prefix, KeyHash = hash });
        await db.SaveChangesAsync();
        var feed = new SensorFeed(db, new MeterReadingService(db), FactoryTestSupport.Monitor(db, site.Context), FactoryTestSupport.Scope(db, site.Context));
        var controller = new FleetPositionsController(feed, fleet, db) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var request = new VehiclePositionsRequest([new VehiclePositionInput(null, "KCA 123A", DateTime.UtcNow.AddMinutes(-1), -0.36, 35.28, 35, 1080, null)]);

        Assert.IsType<UnauthorizedObjectResult>((await controller.Post(request, default)).Result);

        controller.ControllerContext.HttpContext.Request.Headers[SensorsController.KeyHeader] = key;
        var result = Assert.IsType<PositionResult>(Assert.IsType<OkObjectResult>((await controller.Post(request, default)).Result).Value);
        Assert.Equal((1, 1), (result.Vehicles, result.Readings));
        Assert.Equal("Wialon forwarder", (await db.VehicleProfiles.SingleAsync()).LastPositionSource);
        Assert.Equal(1L, (await db.SensorGateways.SingleAsync()).AcceptedCount);
    }

    private sealed record Site(Guid TenantId, Guid FactoryId, FactoryTenantContext Context, FactoryActor Actor);

    private static async Task<Site> SeedAsync(AppDbContext db)
    {
        var (tenant, admin) = await FactoryTestSupport.SeedTenantAsync(db);
        var factory = new OrgNode { TenantId = tenant.Id, Level = OrgNodeLevels.Factory, Name = "Chelal", Code = "CHL" };
        db.OrgNodes.Add(factory);
        await db.SaveChangesAsync();
        return new Site(tenant.Id, factory.Id, new FactoryTenantContext(tenant.Id, admin.Id), new FactoryActor(admin.Id, "Fleet manager"));
    }

    private static SaveVehicle Vehicle(Site site, string registration, decimal? odometer = null, string? tracker = null, bool engineHours = false) =>
        new(registration, VehicleTypes.Lorry, site.FactoryId, "Isuzu", "FVR", 2019, 7000, "Diesel", "Kapkatet route", "J. Kirui", "0712000000",
            null, null, null, tracker, odometer, engineHours);

    private static FleetService Fleet(AppDbContext db, Site site) =>
        new(db, new MeterReadingService(db), FactoryTestSupport.Monitor(db, site.Context));

    private sealed class RecordingDispatcher : INotificationDispatcher
    {
        public List<NotificationRequest> Sent { get; } = [];

        public Task NotifyAsync(NotificationRequest request, CancellationToken cancellationToken = default)
        {
            Sent.Add(request);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTraccar(Dictionary<string, string> responses) : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public string? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            var body = Status == HttpStatusCode.OK && responses.TryGetValue(request.RequestUri!.AbsolutePath, out var json) ? json : "{}";
            return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
