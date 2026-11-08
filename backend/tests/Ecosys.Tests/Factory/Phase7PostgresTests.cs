using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
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

    private static T Ok<T>(ActionResult<T> result) => Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);
}
