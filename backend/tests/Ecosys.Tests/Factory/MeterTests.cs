using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests.Factory;

public sealed class MeterTests
{
    [Fact]
    public async Task ProductionMeter_DefaultsToTheTenantsProductionUnit()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (context, asset) = await SeedAsync(db);
        var meters = Controller(db, context);

        var meter = await CreateAsync(meters, asset.Id, new UpsertMeterRequest("Throughput", "production", IsPrimary: true, InitialValue: 1200m));

        Assert.Equal(MeterTypes.Production, meter.MeterType);
        Assert.Equal("kg made tea", meter.Unit);
        Assert.True(meter.IsCumulative);
        Assert.True(meter.IsPrimary);
        Assert.Equal(1200m, meter.CurrentValue);
        Assert.Equal(1200m, (await db.Assets.SingleAsync(x => x.Id == asset.Id)).CurrentMeterReading);
    }

    [Fact]
    public async Task Counter_CannotGoDown_UnlessReset_AndBackdatedReadingsMustFit()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (context, asset) = await SeedAsync(db);
        var meters = Controller(db, context);
        var meter = await CreateAsync(meters, asset.Id, new UpsertMeterRequest("Run hours", MeterTypes.RunHours));
        var today = DateTime.UtcNow;

        await RecordAsync(meters, meter.Id, 100m, today.AddDays(-10));
        var latest = await RecordAsync(meters, meter.Id, 160m, today.AddDays(-1));
        Assert.Equal(60m, latest.Delta);

        await Assert.ThrowsAsync<BusinessRuleException>(() => RecordAsync(meters, meter.Id, 150m, today));
        // Between the two readings, it must lie between their values.
        await Assert.ThrowsAsync<BusinessRuleException>(() => RecordAsync(meters, meter.Id, 170m, today.AddDays(-5)));
        var middle = await RecordAsync(meters, meter.Id, 130m, today.AddDays(-5));
        Assert.Equal(30m, middle.Delta);
        Assert.Equal(30m, (await db.MeterReadings.SingleAsync(x => x.Value == 160m)).Delta);

        // The meter's current value stays the latest one.
        Assert.Equal(160m, (await db.Meters.SingleAsync(x => x.Id == meter.Id)).CurrentValue);

        var reset = await RecordAsync(meters, meter.Id, 5m, today, isReset: true);
        Assert.Equal(0m, reset.Delta);
        Assert.Equal(5m, (await db.Meters.SingleAsync(x => x.Id == meter.Id)).CurrentValue);

        await Assert.ThrowsAsync<BusinessRuleException>(() => RecordAsync(meters, meter.Id, 10m, today.AddDays(2)));
    }

    [Fact]
    public async Task Gauge_FlagsReadingsOutsideItsLimits()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (context, asset) = await SeedAsync(db);
        var meters = Controller(db, context);
        var meter = await CreateAsync(meters, asset.Id, new UpsertMeterRequest("Bearing vibration", MeterTypes.Vibration, MaxThreshold: 7.1m));

        Assert.False(meter.IsCumulative);
        Assert.Equal("mm/s", meter.Unit);
        Assert.False((await RecordAsync(meters, meter.Id, 4.5m, null)).IsOutOfRange);
        Assert.True((await RecordAsync(meters, meter.Id, 9.2m, null)).IsOutOfRange);
        // Gauges may go down.
        Assert.False((await RecordAsync(meters, meter.Id, 3m, null)).IsOutOfRange);

        // A gauge can't be the asset's main (PM) meter.
        await Assert.ThrowsAsync<BusinessRuleException>(() => meters.Update(meter.Id, new UpsertMeterRequest("Bearing vibration", MeterTypes.Vibration, IsPrimary: true), CancellationToken.None));
    }

    [Fact]
    public async Task OnlyOneMainMeter_AndNamesAreUniquePerAsset()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (context, asset) = await SeedAsync(db);
        var meters = Controller(db, context);

        var hours = await CreateAsync(meters, asset.Id, new UpsertMeterRequest("Run hours", MeterTypes.RunHours, IsPrimary: true));
        var output = await CreateAsync(meters, asset.Id, new UpsertMeterRequest("Output", MeterTypes.Production, IsPrimary: true));

        Assert.False((await db.Meters.SingleAsync(x => x.Id == hours.Id)).IsPrimary);
        Assert.True((await db.Meters.SingleAsync(x => x.Id == output.Id)).IsPrimary);
        await Assert.ThrowsAsync<ConflictException>(() => CreateAsync(meters, asset.Id, new UpsertMeterRequest("run HOURS", MeterTypes.RunHours)));
        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateAsync(meters, asset.Id, new UpsertMeterRequest("Flux", "Capacitor")));
    }

    [Fact]
    public async Task Technician_CanRecordReadings_ButNotAddMeters()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (context, asset) = await SeedAsync(db);
        var meter = await CreateAsync(Controller(db, context), asset.Id, new UpsertMeterRequest("Run hours", MeterTypes.RunHours));

        var tech = FactoryTestSupport.NewUser(context.TenantId!.Value, "tech@chelal.test");
        db.Users.Add(tech);
        await db.SaveChangesAsync();
        var techMeters = Controller(db, new FactoryTenantContext(context.TenantId.Value, tech.Id, false, PermissionNames.CanCompleteWorkOrders));

        Assert.Equal(12m, (await RecordAsync(techMeters, meter.Id, 12m, null)).Value);
        await Assert.ThrowsAsync<ForbiddenException>(() => CreateAsync(techMeters, asset.Id, new UpsertMeterRequest("Extra", MeterTypes.RunHours)));
    }

    private static async Task<(FactoryTenantContext Context, Asset Asset)> SeedAsync(AppDbContext db)
    {
        var (tenant, admin) = await FactoryTestSupport.SeedTenantAsync(db);
        var factory = new OrgNode { TenantId = tenant.Id, Level = OrgNodeLevels.Factory, Name = "Chelal", Code = "CHL" };
        var asset = new Asset { TenantId = tenant.Id, AssetName = "CTC line 1", AssetCode = "CTC-01", OrgNodeId = factory.Id };
        db.OrgNodes.Add(factory);
        db.Assets.Add(asset);
        await db.SaveChangesAsync();
        return (new FactoryTenantContext(tenant.Id, admin.Id), asset);
    }

    private static async Task<MeterResponse> CreateAsync(MetersController controller, Guid assetId, UpsertMeterRequest request)
    {
        var result = await controller.Create(assetId, request, CancellationToken.None);
        return Assert.IsType<MeterResponse>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
    }

    private static async Task<MeterReadingResponse> RecordAsync(MetersController controller, Guid meterId, decimal value, DateTime? at, bool isReset = false)
    {
        var result = await controller.Record(meterId, new RecordMeterReadingRequest(value, at, IsReset: isReset), CancellationToken.None);
        return Assert.IsType<MeterReadingResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    private static MetersController Controller(AppDbContext db, FactoryTenantContext context) =>
        FactoryAssetRegisterTests.WithHttp(new MetersController(
            db,
            context,
            FactoryTestSupport.Access(context),
            FactoryTestSupport.Scope(db, context),
            FactoryTestSupport.Settings(db),
            new MeterReadingService(db),
            FactoryTestSupport.Monitor(db, context),
            FactoryTestSupport.Audit(db)));
}
