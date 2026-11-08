using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests.Factory;

public sealed class ConditionMonitoringTests
{
    [Fact]
    public async Task AnExcursion_WarnsThenAlarms_RaisesOneJob_AndClearsWhenBackInRange()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var dispatcher = new RecordingDispatcher();
        var meters = Meters(db, site.Context, dispatcher);
        var meter = await CreateAsync(meters, site.Asset.Id, new UpsertMeterRequest("Bearing temperature", MeterTypes.Temperature,
            WarningMax: 70m, MaxThreshold: 85m, ConditionAction: ConditionActions.WorkOrder, AlarmPriority: "Critical"));
        var start = DateTime.UtcNow.AddHours(-5);

        await Record(meters, meter.Id, 60m, start);
        Assert.Empty(dispatcher.Sent);
        Assert.Empty(await db.ConditionEvents.ToListAsync());

        // Drifting: a warning, one alert.
        var warning = await Record(meters, meter.Id, 74m, start.AddHours(1));
        Assert.Equal(ConditionLevels.Warning, warning.Condition);
        Assert.False(warning.IsOutOfRange);
        var opened = await db.ConditionEvents.SingleAsync();
        Assert.Equal((ConditionLevels.Warning, ConditionDirections.High, ConditionEventStatuses.Open), (opened.Level, opened.Direction, opened.Status));
        Assert.Equal("Warning: Kiln dryer Bearing temperature 74 °C", Assert.Single(dispatcher.Sent).Title);

        // Still in the warning band: no new alert.
        await Record(meters, meter.Id, 76m, start.AddHours(2));
        Assert.Single(dispatcher.Sent);

        // Past the alarm limit: the same excursion gets worse, a critical alert and a job.
        var alarm = await Record(meters, meter.Id, 91m, start.AddHours(3));
        Assert.True(alarm.IsOutOfRange);
        var escalated = await db.ConditionEvents.SingleAsync();
        Assert.Equal((ConditionLevels.Alarm, 91m, 3), (escalated.Level, escalated.PeakValue, escalated.ReadingCount));
        var job = await db.WorkOrders.SingleAsync();
        Assert.Equal(escalated.WorkOrderId, job.Id);
        Assert.Equal((FactoryRequestSources.Condition, "Critical", site.Asset.Id), (job.RequestSource, job.Priority, job.AssetId));
        Assert.StartsWith("Condition alarm: Kiln dryer Bearing temperature 91", job.Title);
        var critical = dispatcher.Sent[1];
        Assert.Equal(NotificationSeverities.Critical, critical.Severity);
        Assert.Contains(job.WorkOrderNumber, critical.Body);

        // Still past it: no second job, no third alert.
        await Record(meters, meter.Id, 95m, start.AddHours(4));
        Assert.Single(await db.WorkOrders.ToListAsync());
        Assert.Equal(2, dispatcher.Sent.Count);

        // Back in range: the excursion clears and the meter is normal again.
        await Record(meters, meter.Id, 62m, start.AddHours(5));
        var cleared = await db.ConditionEvents.SingleAsync();
        Assert.Equal((ConditionEventStatuses.Cleared, 95m), (cleared.Status, cleared.PeakValue));
        var after = await db.Meters.SingleAsync(x => x.Id == meter.Id);
        Assert.Equal((ConditionLevels.Normal, 0), (after.ConditionState, after.AbnormalStreak));
    }

    [Fact]
    public async Task OneSpike_IsIgnored_UntilEnoughReadingsInARow()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var dispatcher = new RecordingDispatcher();
        var meters = Meters(db, site.Context, dispatcher);
        var meter = await CreateAsync(meters, site.Asset.Id, new UpsertMeterRequest("Steam pressure", MeterTypes.Pressure,
            MinThreshold: 6m, TriggerAfterReadings: 3));
        var start = DateTime.UtcNow.AddHours(-6);

        await Record(meters, meter.Id, 5.2m, start);
        await Record(meters, meter.Id, 5.1m, start.AddHours(1));
        await Record(meters, meter.Id, 7.5m, start.AddHours(2));
        Assert.Empty(await db.ConditionEvents.ToListAsync());
        Assert.Empty(dispatcher.Sent);

        await Record(meters, meter.Id, 5.4m, start.AddHours(3));
        await Record(meters, meter.Id, 5.3m, start.AddHours(4));
        Assert.Empty(await db.ConditionEvents.ToListAsync());
        await Record(meters, meter.Id, 5.0m, start.AddHours(5));
        var confirmed = await db.ConditionEvents.SingleAsync();
        Assert.Equal((ConditionLevels.Alarm, ConditionDirections.Low, 5.0m, 3), (confirmed.Level, confirmed.Direction, confirmed.PeakValue, confirmed.ReadingCount));
        // Alert only: no job unless the meter is set to raise one.
        Assert.Empty(await db.WorkOrders.ToListAsync());
        Assert.Single(dispatcher.Sent);
    }

    [Fact]
    public async Task ABackdatedReading_DoesNotChangeTheCondition()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var dispatcher = new RecordingDispatcher();
        var meters = Meters(db, site.Context, dispatcher);
        var meter = await CreateAsync(meters, site.Asset.Id, new UpsertMeterRequest("Vibration", MeterTypes.Vibration, MaxThreshold: 7.1m));

        await Record(meters, meter.Id, 3m, DateTime.UtcNow.AddHours(-1));
        var old = await Record(meters, meter.Id, 9m, DateTime.UtcNow.AddDays(-2));

        Assert.True(old.IsOutOfRange);
        Assert.Empty(await db.ConditionEvents.ToListAsync());
        Assert.Empty(dispatcher.Sent);
        Assert.Equal(ConditionLevels.Normal, (await db.Meters.SingleAsync(x => x.Id == meter.Id)).ConditionState);
    }

    [Fact]
    public async Task ANewExcursion_ReusesTheJobStillOpenFromTheLastOne()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var meters = Meters(db, site.Context, new RecordingDispatcher());
        var meter = await CreateAsync(meters, site.Asset.Id, new UpsertMeterRequest("Motor current", MeterTypes.Other,
            Unit: "A", MaxThreshold: 40m, ConditionAction: ConditionActions.WorkOrder));
        var start = DateTime.UtcNow.AddHours(-4);

        await Record(meters, meter.Id, 45m, start);
        await Record(meters, meter.Id, 30m, start.AddHours(1));
        await Record(meters, meter.Id, 48m, start.AddHours(2));

        var events = await db.ConditionEvents.OrderBy(x => x.StartedAt).ToListAsync();
        Assert.Equal(2, events.Count);
        Assert.Equal(events[0].WorkOrderId, events[1].WorkOrderId);
        Assert.Single(await db.WorkOrders.ToListAsync());

        // Once that job is done, the next alarm raises a new one.
        (await db.WorkOrders.SingleAsync()).Status = WorkOrderStatuses.Completed;
        await db.SaveChangesAsync();
        await Record(meters, meter.Id, 30m, start.AddHours(3));
        await Record(meters, meter.Id, 50m, start.AddHours(4));
        Assert.Equal(2, await db.WorkOrders.CountAsync());
    }

    [Fact]
    public async Task Limits_AreValidated()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var meters = Meters(db, site.Context, new RecordingDispatcher());

        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateAsync(meters, site.Asset.Id,
            new UpsertMeterRequest("A", MeterTypes.Temperature, WarningMax: 90m, MaxThreshold: 85m)));
        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateAsync(meters, site.Asset.Id,
            new UpsertMeterRequest("B", MeterTypes.Temperature, WarningMin: 50m, WarningMax: 40m)));
        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateAsync(meters, site.Asset.Id,
            new UpsertMeterRequest("C", MeterTypes.Temperature, MaxThreshold: 85m, ConditionAction: "Siren")));
        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateAsync(meters, site.Asset.Id,
            new UpsertMeterRequest("D", MeterTypes.Temperature, MaxThreshold: 85m, TriggerAfterReadings: 0)));
    }

    [Fact]
    public void Trend_ProjectsWhenARisingGaugeReachesItsNextLimit()
    {
        var meter = new Meter { WarningMax = 70m, MaxThreshold = 85m, Unit = "°C" };
        var start = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        // One degree a day from 60.
        var rising = Enumerable.Range(0, 6).Select(i => (start.AddDays(i), 60m + i)).ToList();

        var trend = ConditionTrends.Fit(meter, rising);

        Assert.Equal(("Rising", ConditionLevels.Warning, 70m), (trend.Heading, trend.ApproachingLevel, trend.ApproachingLimit));
        Assert.Equal(1m, trend.SlopePerDay);
        Assert.Equal(5m, trend.DaysToLimit);

        // Already past the warning band: the alarm limit is next.
        var hot = Enumerable.Range(0, 6).Select(i => (start.AddDays(i), 72m + i)).ToList();
        Assert.Equal((ConditionLevels.Alarm, 8m), (ConditionTrends.Fit(meter, hot).ApproachingLevel, ConditionTrends.Fit(meter, hot).DaysToLimit));

        // Flat, falling away from every limit, or too few points: nothing to reach.
        Assert.Equal("Steady", ConditionTrends.Fit(meter, Enumerable.Range(0, 6).Select(i => (start.AddDays(i), 60m)).ToList()).Heading);
        Assert.Null(ConditionTrends.Fit(meter, Enumerable.Range(0, 6).Select(i => (start.AddDays(i), 60m - i)).ToList()).DaysToLimit);
        Assert.Null(ConditionTrends.Fit(meter, rising.Take(3).ToList()).SlopePerDay);
    }

    [Fact]
    public async Task Overview_ListsMonitoredMetersWorstFirst_AndEventsCanBeAcknowledged()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var meters = Meters(db, site.Context, new RecordingDispatcher());
        var hot = await CreateAsync(meters, site.Asset.Id, new UpsertMeterRequest("Bearing temperature", MeterTypes.Temperature, MaxThreshold: 85m));
        var calm = await CreateAsync(meters, site.Asset.Id, new UpsertMeterRequest("Oil pressure", MeterTypes.Pressure, MinThreshold: 2m));
        await CreateAsync(meters, site.Asset.Id, new UpsertMeterRequest("Run hours", MeterTypes.RunHours));
        await Record(meters, calm.Id, 3m, DateTime.UtcNow.AddMinutes(-20));
        await Record(meters, hot.Id, 90m, DateTime.UtcNow.AddMinutes(-10));
        var controller = Condition(db, site.Context);

        var overview = Ok(await controller.Overview(null, CancellationToken.None));
        Assert.Equal((2, 1, 0), (overview.Monitored, overview.Alarms, overview.Warnings));
        Assert.Equal(["Bearing temperature", "Oil pressure"], overview.Meters.Select(x => x.MeterName));
        Assert.NotNull(overview.Meters[0].OpenEventId);

        var open = Assert.Single(Ok(await controller.Events("open", null, null, null, null, CancellationToken.None)));
        var acknowledged = Ok(await controller.Acknowledge(open.Id, new AcknowledgeConditionEventRequest("Fitter on the way"), CancellationToken.None));
        Assert.Equal("Fitter on the way", acknowledged.AcknowledgementNote);
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Acknowledge(open.Id, new AcknowledgeConditionEventRequest(null), CancellationToken.None));

        var detail = Ok(await controller.MeterCondition(hot.Id, 7, CancellationToken.None));
        Assert.Equal((ConditionLevels.Alarm, 1), (detail.ConditionState, detail.Readings.Count));
        Assert.Single(detail.Events);
    }

    private static T Ok<T>(ActionResult<T> result) => Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private sealed record Site(FactoryTenantContext Context, Asset Asset);

    private static async Task<Site> SeedAsync(AppDbContext db)
    {
        var (tenant, admin) = await FactoryTestSupport.SeedTenantAsync(db);
        var factory = new OrgNode { TenantId = tenant.Id, Level = OrgNodeLevels.Factory, Name = "Chelal", Code = "CHL" };
        var asset = new Asset { TenantId = tenant.Id, AssetName = "Kiln dryer", AssetCode = "CHL-KD1", OrgNodeId = factory.Id, Status = "Active" };
        db.OrgNodes.Add(factory);
        db.Assets.Add(asset);
        await db.SaveChangesAsync();
        return new Site(new FactoryTenantContext(tenant.Id, admin.Id), asset);
    }

    private static MetersController Meters(AppDbContext db, FactoryTenantContext context, INotificationDispatcher dispatcher) =>
        FactoryAssetRegisterTests.WithHttp(new MetersController(db, context, FactoryTestSupport.Access(context), FactoryTestSupport.Scope(db, context),
            FactoryTestSupport.Settings(db), new MeterReadingService(db), FactoryTestSupport.Monitor(db, context, dispatcher), FactoryTestSupport.Audit(db)));

    private static ConditionMonitoringController Condition(AppDbContext db, FactoryTenantContext context) =>
        FactoryAssetRegisterTests.WithHttp(new ConditionMonitoringController(db, context, FactoryTestSupport.Access(context),
            FactoryTestSupport.Scope(db, context), FactoryTestSupport.Audit(db)));

    private static async Task<MeterResponse> CreateAsync(MetersController controller, Guid assetId, UpsertMeterRequest request) =>
        Assert.IsType<MeterResponse>(Assert.IsType<CreatedAtActionResult>((await controller.Create(assetId, request, CancellationToken.None)).Result).Value);

    private static async Task<MeterReadingResponse> Record(MetersController controller, Guid meterId, decimal value, DateTime at) =>
        Assert.IsType<MeterReadingResponse>(Assert.IsType<OkObjectResult>((await controller.Record(meterId, new RecordMeterReadingRequest(value, at), CancellationToken.None)).Result).Value);

    private sealed class RecordingDispatcher : INotificationDispatcher
    {
        public List<NotificationRequest> Sent { get; } = [];

        public Task NotifyAsync(NotificationRequest request, CancellationToken cancellationToken = default)
        {
            Sent.Add(request);
            return Task.CompletedTask;
        }
    }
}
