using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests.Factory;

/// <summary>Phase 7 queries (condition monitoring, sensors, reliability, fleet) against real PostgreSQL.</summary>
public sealed class Phase7PostgresTests
{
    [Fact]
    public async Task Postgres_ConditionMonitoring_AllTranslate()
    {
        await FactoryPostgres.RunAsync("condition", async connection =>
        {
            var (db, context) = await FactoryPostgres.SeedAsync(connection);
            await using var _ = db;
            var factory = new OrgNode { TenantId = context.TenantId!.Value, Level = OrgNodeLevels.Factory, Name = "Chelal", Code = "CHL" };
            var asset = new Asset { TenantId = context.TenantId!.Value, AssetName = "Boiler", AssetCode = "CHL-BLR", OrgNodeId = factory.Id, Status = "Active" };
            db.AddRange(factory, asset);
            await db.SaveChangesAsync();

            var meters = FactoryAssetRegisterTests.WithHttp(new MetersController(db, context, FactoryTestSupport.Access(context), FactoryTestSupport.Scope(db, context),
                FactoryTestSupport.Settings(db), new MeterReadingService(db), FactoryTestSupport.Monitor(db, context), FactoryTestSupport.Audit(db)));
            var meter = Assert.IsType<MeterResponse>(Assert.IsType<CreatedAtActionResult>((await meters.Create(asset.Id,
                new UpsertMeterRequest("Steam pressure", MeterTypes.Pressure, WarningMax: 9m, MaxThreshold: 10m, ConditionAction: ConditionActions.WorkOrder), default)).Result).Value);
            var start = DateTime.UtcNow.AddHours(-8);
            foreach (var (value, hour) in new[] { (8m, 0), (8.5m, 1), (9.4m, 2), (10.6m, 3), (11m, 4), (8m, 5), (10.8m, 6) })
                await meters.Record(meter.Id, new RecordMeterReadingRequest(value, start.AddHours(hour)), default);

            Assert.Equal(2, await db.ConditionEvents.CountAsync());
            Assert.Single(await db.WorkOrders.ToListAsync());

            var condition = FactoryAssetRegisterTests.WithHttp(new ConditionMonitoringController(db, context, FactoryTestSupport.Access(context),
                FactoryTestSupport.Scope(db, context), FactoryTestSupport.Audit(db)));
            var overview = Ok(await condition.Overview(factory.Id, default));
            Assert.Equal(1, overview.Alarms);
            Assert.NotNull(overview.Meters[0].WorkOrderNumber);
            Assert.Equal(2, Ok(await condition.Events(null, null, factory.Id, DateTime.UtcNow.AddDays(-1), 10, default)).Count);
            var open = Assert.Single(Ok(await condition.Events("open", meter.Id, null, null, null, default)));
            Ok(await condition.Acknowledge(open.Id, new AcknowledgeConditionEventRequest("Checked"), default));
            var detail = Ok(await condition.MeterCondition(meter.Id, 30, default));
            Assert.Equal(7, detail.Readings.Count);
            Assert.Equal(7, detail.Trend.Readings);
        });
    }

    [Fact]
    public async Task Postgres_SensorFeed_AllTranslate()
    {
        await FactoryPostgres.RunAsync("sensors", async connection =>
        {
            var (db, context) = await FactoryPostgres.SeedAsync(connection);
            await using var _ = db;
            var tenantId = context.TenantId!.Value;
            var factory = new OrgNode { TenantId = tenantId, Level = OrgNodeLevels.Factory, Name = "Chelal", Code = "CHL" };
            var asset = new Asset { TenantId = tenantId, AssetName = "Boiler", AssetCode = "CHL-BLR", OrgNodeId = factory.Id, Status = "Active" };
            db.AddRange(factory, asset, new Meter { TenantId = tenantId, AssetId = asset.Id, Name = "Steam pressure", MeterType = MeterTypes.Pressure, Unit = "bar", IsCumulative = false, ExternalId = "BLR.P", MaxThreshold = 10m });
            await db.SaveChangesAsync();

            var gateways = FactoryAssetRegisterTests.WithHttp(new SensorGatewaysController(db, context, FactoryTestSupport.Access(context), FactoryTestSupport.Audit(db)));
            var created = Assert.IsType<SensorGatewayCreatedResponse>(Assert.IsType<OkObjectResult>((await gateways.Create(new SaveSensorGatewayRequest("PLC", OrgNodeId: factory.Id, SampleSeconds: 0), default)).Result).Value);

            // The feed runs without a session: a fresh context with no tenant.
            await using var anonymous = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
            var feed = new SensorFeed(anonymous, new MeterReadingService(anonymous), FactoryTestSupport.Monitor(anonymous, context), FactoryTestSupport.Scope(anonymous, context));
            var gateway = await feed.AuthenticateAsync(created.Key);
            Assert.NotNull(gateway);
            var result = await feed.IngestAsync(gateway!, [new SensorSample("BLR.P", 8m, DateTime.UtcNow.AddMinutes(-2)), new SensorSample("BLR.P", 11m, DateTime.UtcNow.AddMinutes(-1))]);
            Assert.Equal(2, result.Accepted);
            Assert.Equal(1, await anonymous.ConditionEvents.CountAsync());

            var meters = Ok(await gateways.Meters(created.Gateway.Id, default, FactoryTestSupport.Scope(db, context)));
            Assert.NotNull(Assert.Single(meters).LastSensorReadingAt);
            Assert.Equal(2, Assert.Single(Ok(await gateways.List(default))).AcceptedCount);
        });
    }

    [Fact]
    public async Task Postgres_Reliability_AllTranslate()
    {
        await FactoryPostgres.RunAsync("reliability", async connection =>
        {
            var (db, context) = await FactoryPostgres.SeedAsync(connection);
            await using var _ = db;
            var tenantId = context.TenantId!.Value;
            var now = DateTime.UtcNow;
            var factory = new OrgNode { TenantId = tenantId, Level = OrgNodeLevels.Factory, Name = "Chelal", Code = "CHL" };
            var pump = new Asset { TenantId = tenantId, AssetName = "Feed pump", AssetCode = "CHL-P1", OrgNodeId = factory.Id, Status = "Active" };
            var seal = new FailureCode { TenantId = tenantId, Kind = FailureCodeKinds.Problem, Code = "SEAL", Name = "Seal leak" };
            var hours = new Meter { TenantId = tenantId, AssetId = pump.Id, Name = "Run hours", MeterType = MeterTypes.RunHours, Unit = "h", IsCumulative = true, IsPrimary = true, CurrentValue = 900 };
            db.AddRange(factory, pump, seal, hours);
            var i = 0;
            foreach (var daysAgo in new[] { 120, 90, 60, 30 })
            {
                var at = now.AddDays(-daysAgo);
                db.MeterReadings.Add(new MeterReading { TenantId = tenantId, MeterId = hours.Id, AssetId = pump.Id, Value = i * 300, ReadAt = at.AddHours(-1), Source = MeterReadingSources.Manual });
                db.WorkOrders.Add(new WorkOrder { TenantId = tenantId, AssetId = pump.Id, OrgNodeId = factory.Id, WorkOrderNumber = $"WO-{++i}", Title = "Leak", IsBreakdown = true,
                    Status = WorkOrderStatuses.Closed, CreatedAt = at, DowntimeStartedAt = at, DowntimeEndedAt = at.AddHours(2), FailureProblemCodeId = seal.Id });
            }

            await db.SaveChangesAsync();
            var scope = FactoryTestSupport.Scope(db, context);
            var service = new ReliabilityService(db, new FactoryPmService(db, FactoryTestSupport.WorkOrders(db, context), FactoryTestSupport.Settings(db)));
            var controller = FactoryAssetRegisterTests.WithHttp(new ReliabilityController(context, FactoryTestSupport.Access(context), scope, service, FactoryTestSupport.Settings(db), FactoryTestSupport.Audit(db)));

            var assets = Ok(await controller.Assets(factory.Id, 365, default));
            Assert.Equal((4, 300m), (assets[0].Failures, assets[0].MeanRunBetween));
            var suggestion = Assert.Single(Ok(await controller.Suggestions(null, 365, default)));
            Assert.Equal(PmTriggerTypes.Meter, suggestion.TriggerType);
            Assert.Equal(PmTriggerTypes.Meter, Ok(await controller.Apply(pump.Id, 365, default)).TriggerType);
            Assert.Equal(3, Ok(await controller.RepeatFailures(factory.Id, 365, 45, default)).Summary.Repeats);
        });
    }

    private static T Ok<T>(ActionResult<T> result) => Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);
}
