using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests.Factory;

/// <summary>Wood fuel: what a delivery is worth, the yard ledger, wood fed to boilers and the yard board.</summary>
public sealed class WoodFuelTests
{
    private static readonly WoodActor Clerk = new(Guid.NewGuid(), "Yard Clerk", false);
    private static readonly WoodActor Manager = new(Guid.NewGuid(), "Wood Manager", true);
    private static readonly VisitActor GateOfficer = new(Guid.NewGuid(), "Gate Officer");
    private static readonly VisitActor GateManager = new(Guid.NewGuid(), "Security Manager", true);

    // ------------------------------------------------------------------ the arithmetic

    [Fact]
    public void TheWeightComesFromTheBasisTheLoadWasMeasuredOn()
    {
        Assert.Equal(7200m, WoodFuelRules.NetKg(WoodMeasureBasis.Weighbridge, 12500, 5300, null, null, null, null));
        Assert.Equal(3800m, WoodFuelRules.NetKg(WoodMeasureBasis.StackedVolume, null, null, 10m, null, null, 380m));
        Assert.Equal(6000m, WoodFuelRules.NetKg(WoodMeasureBasis.LoadCount, null, null, null, 3m, 2000m, null));

        Assert.Throws<BusinessRuleException>(() => WoodFuelRules.NetKg(WoodMeasureBasis.Weighbridge, 5000, 6000, null, null, null, null));
        Assert.Throws<BusinessRuleException>(() => WoodFuelRules.NetKg(WoodMeasureBasis.Weighbridge, 5000, null, null, null, null, null));
        Assert.Throws<BusinessRuleException>(() => WoodFuelRules.NetKg(WoodMeasureBasis.StackedVolume, null, null, 10m, null, null, null));
        Assert.Throws<BusinessRuleException>(() => WoodFuelRules.NetKg(WoodMeasureBasis.LoadCount, null, null, null, 3m, null, null));
        Assert.Throws<BusinessRuleException>(() => WoodFuelRules.NetKg("Guess", null, null, null, null, null, null));
    }

    [Fact]
    public void DryWood_IsPaidInFull()
    {
        var a = WoodFuelRules.Assess(new WoodFuelSettings(), 10_000m, 22m, 5000m, false, null);
        Assert.Equal((WoodDecisions.Accepted, 0m, 10_000m, 100m, 50_000m), (a.Decision, a.DeductedKg, a.PayableKg, a.PricePercent, a.Amount));

        var unmeasured = WoodFuelRules.Assess(new WoodFuelSettings(), 10_000m, null, 5000m, false, null);
        Assert.Equal(WoodDecisions.Accepted, unmeasured.Decision);
    }

    [Fact]
    public void TheDeductKgRule_TakesAShareOffForEachPointOverTheBase()
    {
        // Base 25 %, 1.5 % per point: 33 % moisture is 8 points over, so 12 % of the weight is not paid.
        var a = WoodFuelRules.Assess(new WoodFuelSettings(), 10_000m, 33m, 5000m, false, null);
        Assert.Equal(WoodDecisions.AcceptedWithDeduction, a.Decision);
        Assert.Equal(1200m, a.DeductedKg);
        Assert.Equal(8800m, a.PayableKg);
        Assert.Equal(44_000m, a.Amount);
        Assert.Contains("1200", a.Reason);
    }

    [Fact]
    public void ThePriceBandsRule_PaysTheWholeLoadAtAShareOfThePrice()
    {
        var settings = new WoodFuelSettings { MoistureRule = WoodMoistureRules.PriceBands };
        var inFirstBand = WoodFuelRules.Assess(settings, 10_000m, 28m, 5000m, false, null);
        Assert.Equal((WoodDecisions.Accepted, 100m, 50_000m), (inFirstBand.Decision, inFirstBand.PricePercent, inFirstBand.Amount));

        var wet = WoodFuelRules.Assess(settings, 10_000m, 38m, 5000m, false, null);
        Assert.Equal((WoodDecisions.AcceptedWithDeduction, 0m, 10_000m, 80m, 40_000m), (wet.Decision, wet.DeductedKg, wet.PayableKg, wet.PricePercent, wet.Amount));

        var custom = new WoodFuelSettings { MoistureRule = WoodMoistureRules.PriceBands, PriceBandsJson = WoodFuelRules.SerialiseBands([new(30, 100), new(45, 50)]) };
        Assert.Equal(50m, WoodFuelRules.Assess(custom, 1000m, 40m, 1000m, false, null).PricePercent);
    }

    [Fact]
    public void WoodWetterThanTheLimit_IsRejected_UnlessAManagerOverrides()
    {
        var settings = new WoodFuelSettings();
        var rejected = WoodFuelRules.Assess(settings, 10_000m, 52m, 5000m, false, null);
        Assert.Equal((WoodDecisions.Rejected, 10_000m, 0m, 0m), (rejected.Decision, rejected.RejectedKg, rejected.PayableKg, rejected.Amount));
        Assert.Contains("limit", rejected.Reason);

        var forced = WoodFuelRules.Assess(settings, 10_000m, 52m, 5000m, false, null, allowAboveRejectLimit: true);
        Assert.NotEqual(WoodDecisions.Rejected, forced.Decision);

        var byClerk = WoodFuelRules.Assess(settings, 10_000m, 20m, 5000m, true, "Rotten, full of soil");
        Assert.Equal((WoodDecisions.Rejected, "Rotten, full of soil"), (byClerk.Decision, byClerk.Reason));
    }

    [Fact]
    public void Bands_AreCheckedAndFallBackToTheDefaults()
    {
        Assert.Equal(WoodFuelRules.DefaultBands, WoodFuelRules.ParseBands(null));
        Assert.Equal(WoodFuelRules.DefaultBands, WoodFuelRules.ParseBands("not json"));
        Assert.Null(WoodFuelRules.SerialiseBands(null));
        Assert.Throws<BusinessRuleException>(() => WoodFuelRules.SerialiseBands([new(30, 100), new(30, 90)]));
        Assert.Throws<BusinessRuleException>(() => WoodFuelRules.SerialiseBands([new(30, 120)]));
    }

    // ------------------------------------------------------------------ receiving

    [Fact]
    public async Task AWeighedDelivery_IsPriced_AndGoesIntoTheStackAndTheLedger()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var service = Service(db);
        db.FuelPriceAgreements.Add(new FuelPriceAgreement { TenantId = site.TenantId, PricePerTonne = 5000m, ValidFrom = DateTime.UtcNow.AddDays(-30) });
        await db.SaveChangesAsync();

        var delivery = await service.ReceiveAsync(site.TenantId, Weighed(site, gross: 12_500, tare: 5_300, moisture: [30, 32, 34]), Clerk);

        Assert.Equal("WF-000001", delivery.DeliveryNumber);
        Assert.Equal((7200m, 32m, WoodDecisions.AcceptedWithDeduction), (delivery.NetKg, delivery.AverageMoisturePct, delivery.Decision));
        Assert.Equal(7200m * 0.895m, delivery.PayableKg);
        Assert.Equal(Math.Round(delivery.PayableKg / 1000m * 5000m, 2), delivery.AmountPayable);
        Assert.Equal(3, await db.WoodMoistureSamples.CountAsync());
        Assert.Equal(7200m, await service.StockKgAsync(site.TenantId, site.FactoryId, site.StackId));
        var stack = await db.WoodStacks.SingleAsync();
        Assert.True(stack.ReadyFrom > DateTime.UtcNow.AddDays(27));
        Assert.Equal("WF-000002", (await service.ReceiveAsync(site.TenantId, Weighed(site, 9_000, 5_000, [20]), Clerk)).DeliveryNumber);
    }

    [Fact]
    public async Task ARejectedDelivery_IsRecorded_ButAddsNothingToTheYard()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var service = Service(db);

        var delivery = await service.ReceiveAsync(site.TenantId, Weighed(site, 12_500, 5_300, [55, 58]), Clerk);

        Assert.Equal((WoodDecisions.Rejected, 7200m, 0m), (delivery.Decision, delivery.RejectedKg, delivery.AmountPayable));
        Assert.Null(delivery.StackId);
        Assert.Empty(db.WoodStockMovements);
        Assert.Equal(0m, await service.StockKgAsync(site.TenantId, site.FactoryId, null));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.ReceiveAsync(site.TenantId, Weighed(site, 12_500, 5_300, [55]) with { AcceptAboveMoistureLimit = true }, Clerk));
        var forced = await service.ReceiveAsync(site.TenantId, Weighed(site, 12_500, 5_300, [55]) with { AcceptAboveMoistureLimit = true }, Manager);
        Assert.NotEqual(WoodDecisions.Rejected, forced.Decision);
    }

    [Fact]
    public async Task StackedVolumeAndLoadCount_WorkWithoutAWeighbridge()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var service = Service(db);
        var species = await db.FuelSpecies.SingleAsync(x => x.Name == "Eucalyptus");

        var stacked = await service.ReceiveAsync(site.TenantId, Blank(site) with { MeasureBasis = WoodMeasureBasis.StackedVolume, StackedM3 = 12m, SpeciesId = species.Id, MoistureSamples = [24] }, Clerk);
        Assert.Equal(12m * species.KgPerStackedM3, stacked.NetKg);
        Assert.Null(stacked.GrossKg);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ReceiveAsync(site.TenantId, Blank(site) with { MeasureBasis = WoodMeasureBasis.LoadCount, Loads = 2 }, Clerk));
        var settings = new WoodFuelSettings { TenantId = site.TenantId, FactoryId = site.FactoryId, DefaultKgPerLoad = 2500m, DefaultMeasureBasis = WoodMeasureBasis.LoadCount };
        db.WoodFuelSettings.Add(settings);
        await db.SaveChangesAsync();
        var loads = await service.ReceiveAsync(site.TenantId, Blank(site) with { MeasureBasis = null, Loads = 2m }, Clerk);
        Assert.Equal((WoodMeasureBasis.LoadCount, 5000m), (loads.MeasureBasis, loads.NetKg));
    }

    [Fact]
    public async Task ThePrice_IsTheMostSpecificLiveAgreement()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var service = Service(db);
        var supplier = await db.FuelSuppliers.SingleAsync();
        var species = await db.FuelSpecies.FirstAsync();
        var now = DateTime.UtcNow;
        db.FuelPriceAgreements.AddRange(
            new FuelPriceAgreement { TenantId = site.TenantId, PricePerTonne = 4000m, ValidFrom = now.AddDays(-60) },
            new FuelPriceAgreement { TenantId = site.TenantId, SpeciesId = species.Id, PricePerTonne = 4500m, ValidFrom = now.AddDays(-60) },
            new FuelPriceAgreement { TenantId = site.TenantId, SupplierId = supplier.Id, PricePerTonne = 5200m, ValidFrom = now.AddDays(-10) },
            new FuelPriceAgreement { TenantId = site.TenantId, SupplierId = supplier.Id, PricePerTonne = 9999m, ValidFrom = now.AddDays(-60), ValidTo = now.AddDays(-30) },
            new FuelPriceAgreement { TenantId = site.TenantId, SupplierId = supplier.Id, PricePerTonne = 1m, ValidFrom = now.AddDays(-5), IsActive = false });
        await db.SaveChangesAsync();

        Assert.Equal(5200m, (await service.FindPriceAsync(site.TenantId, supplier.Id, species.Id, null, now))!.PricePerTonne);
        Assert.Equal(4500m, (await service.FindPriceAsync(site.TenantId, null, species.Id, null, now))!.PricePerTonne);
        Assert.Equal(4000m, (await service.FindPriceAsync(site.TenantId, null, null, null, now))!.PricePerTonne);
    }

    // ------------------------------------------------------------------ the boiler and the yard

    [Fact]
    public async Task AnIssue_TakesWoodOutOfTheLedger_AndAddsToTheBoilersWoodMeter()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var service = Service(db);
        await service.ReceiveAsync(site.TenantId, Weighed(site, 15_000, 5_000, [20]), Clerk);

        var first = await service.IssueAsync(site.TenantId, new NewWoodIssue(site.FactoryId, site.BoilerId, site.StackId, 1800m, null, null, "Day", null), Clerk);
        var second = await service.IssueAsync(site.TenantId, new NewWoodIssue(site.FactoryId, site.BoilerId, site.StackId, 1200m, null, null, "Night", null), Clerk);

        Assert.Equal(7000m, await service.StockKgAsync(site.TenantId, site.FactoryId, null));
        Assert.Equal(7000m, await service.StockKgAsync(site.TenantId, site.FactoryId, site.StackId));
        var meter = await db.Meters.SingleAsync();
        Assert.Equal((MeterTypes.Fuel, "kg", 3000m), (meter.MeterType, meter.Unit, meter.CurrentValue));
        Assert.NotNull(first.MeterReadingId);
        Assert.NotNull(second.MeterReadingId);
        Assert.Equal(2, await db.MeterReadings.CountAsync());
    }

    [Fact]
    public async Task AnIssue_CannotTakeMoreThanIsInTheYard_OrFeedAnAssetOutsideTheFactory()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var service = Service(db);
        await service.ReceiveAsync(site.TenantId, Weighed(site, 8_000, 5_000, [20]), Clerk);

        var tooMuch = await Assert.ThrowsAsync<BusinessRuleException>(() => service.IssueAsync(site.TenantId, new NewWoodIssue(site.FactoryId, site.BoilerId, site.StackId, 3500m, null, null, null, null), Clerk));
        Assert.Contains("3000", tooMuch.Message);
        // With no stack named, only wood received without a stack can go.
        var noStack = await Assert.ThrowsAsync<BusinessRuleException>(() => service.IssueAsync(site.TenantId, new NewWoodIssue(site.FactoryId, site.BoilerId, null, 100m, null, null, null, null), Clerk));
        Assert.Contains("Choose the stack", noStack.Message);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.IssueAsync(site.TenantId, new NewWoodIssue(site.FactoryId, site.BoilerId, site.StackId, null, null, null, null, null), Clerk));

        var elsewhere = new Asset { TenantId = site.TenantId, AssetName = "Other boiler", AssetCode = "OB-1", OrgNodeId = site.OtherFactoryId };
        db.Assets.Add(elsewhere);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.IssueAsync(site.TenantId, new NewWoodIssue(site.FactoryId, elsewhere.Id, site.StackId, 100m, null, null, null, null), Clerk));
        Assert.Equal(3000m, await service.StockKgAsync(site.TenantId, site.FactoryId, null));
    }

    [Fact]
    public async Task AnIssueDatedBeforeTheMetersLastReading_IsKept_WithoutAMeterReading()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var service = Service(db);
        await service.ReceiveAsync(site.TenantId, Weighed(site, 15_000, 5_000, [20]), Clerk);
        await service.IssueAsync(site.TenantId, new NewWoodIssue(site.FactoryId, site.BoilerId, site.StackId, 500m, null, null, null, null), Clerk);

        // A phone that was offline sends an issue from before the reading just made.
        var late = await At(DateTime.UtcNow.AddHours(-6),
            () => service.IssueAsync(site.TenantId, new NewWoodIssue(site.FactoryId, site.BoilerId, site.StackId, 300m, null, null, null, null), Clerk));

        Assert.Null(late.MeterReadingId);
        Assert.Equal(9200m, await service.StockKgAsync(site.TenantId, site.FactoryId, null));
        Assert.Equal(1, await db.MeterReadings.CountAsync());
    }

    [Fact]
    public async Task ACount_PostsTheDifference_AndAZeroCountClosesTheStack()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var service = Service(db);
        await service.ReceiveAsync(site.TenantId, Weighed(site, 15_000, 5_000, [20]), Clerk);

        var count = await service.CountAsync(site.TenantId, new NewWoodCount(site.StackId, 9_400m, null, WoodAdjustmentReasons.DryingLoss, "Dried out"), Clerk);
        Assert.Equal((10_000m, 9_400m, -600m, "Weighed"), (count.LedgerKg, count.CountedKg, count.VarianceKg, count.Method));
        Assert.Equal(9_400m, await service.StockKgAsync(site.TenantId, site.FactoryId, site.StackId));

        var eucalyptus = await db.FuelSpecies.SingleAsync(x => x.Name == "Eucalyptus");
        (await db.WoodStacks.SingleAsync()).SpeciesId = eucalyptus.Id;
        await db.SaveChangesAsync();
        var measured = await service.CountAsync(site.TenantId, new NewWoodCount(site.StackId, null, 20m, null, null), Clerk);
        Assert.Equal((20m * eucalyptus.KgPerStackedM3, "Measured"), (measured.CountedKg, measured.Method));

        await service.CountAsync(site.TenantId, new NewWoodCount(site.StackId, 0m, null, null, null), Clerk);
        Assert.Equal(WoodStackStatuses.Depleted, (await db.WoodStacks.SingleAsync()).Status);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CountAsync(site.TenantId, new NewWoodCount(site.StackId, null, null, null, null), Clerk));
    }

    [Fact]
    public async Task TheYardBoard_ShowsStockCoverAndWoodPerKgOfMadeTea()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var service = Service(db);
        db.FuelPriceAgreements.Add(new FuelPriceAgreement { TenantId = site.TenantId, PricePerTonne = 5000m, ValidFrom = DateTime.UtcNow.AddDays(-90) });
        db.WoodFuelSettings.Add(new WoodFuelSettings { TenantId = site.TenantId, FactoryId = site.FactoryId, BurnWindowDays = 2, LowCoverDays = 5, TargetKgPerUnit = 1.5m });
        await db.SaveChangesAsync();
        // 10 t at 20 % (full price, no deduction), 2 t burnt on each of two days, 1 t of made tea each day.
        await service.ReceiveAsync(site.TenantId, Weighed(site, 15_000, 5_000, [20]), Clerk);
        await service.IssueAsync(site.TenantId, new NewWoodIssue(site.FactoryId, site.BoilerId, site.StackId, 2000m, null, null, null, null), Clerk);
        await At(DateTime.UtcNow.AddHours(-26), () => service.IssueAsync(site.TenantId, new NewWoodIssue(site.FactoryId, site.BoilerId, site.StackId, 2000m, null, null, null, null), Clerk));
        foreach (var day in new[] { DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(-1) })
            db.ProductionDays.Add(new ProductionDay { TenantId = site.TenantId, FactoryId = site.FactoryId, Date = day, MadeTeaKg = 1000m, EnteredByName = "Manager" });
        await db.SaveChangesAsync();

        var board = await service.SummaryAsync(site.TenantId, site.FactoryId, DateTime.UtcNow.Date.AddDays(-7), DateTime.UtcNow.Date.AddDays(1));

        Assert.Equal(6000m, board.StockKg);
        Assert.Equal((10_000m, 4000m, 0m), (board.ReceivedKg, board.IssuedKg, board.AdjustmentKg));
        Assert.Equal(board.StockKg, board.ClosingKg);
        Assert.Equal(2000m, board.AverageDailyBurnKg);
        Assert.Equal(3m, board.DaysOfCover);
        Assert.True(board.LowCover);
        Assert.Equal((2000m, 2.0m, 1.5m), (board.MadeTeaKg, board.KgPerUnit, board.TargetKgPerUnit));
        Assert.Equal(10.0m, board.CostPerUnit); // 4 000 kg burnt at 5 per kg paid is 20 000, over 2 000 kg of made tea
        var stackRow = Assert.Single(board.Stacks);
        Assert.Equal((6000m, 20m), (stackRow.Kg, stackRow.AverageMoisturePct));
        var supplier = Assert.Single(board.Suppliers);
        Assert.Equal(1, supplier.Deliveries);
    }

    // ------------------------------------------------------------------ the gate

    [Fact]
    public async Task ATruckWeighedInOnly_HoldsNothingInTheLedger_UntilItsTareIsWeighedOut()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var sent = new RecordingDispatcher();
        var service = Service(db, sent);
        var gate = new VehicleLogService(db);
        var truck = await gate.EnterAsync(site.TenantId, new NewVehicleEntry(site.FactoryId, "kca 123a", "Delivery", "Peter Otieno"), GateOfficer);

        var waiting = await service.ReceiveAsync(site.TenantId, Blank(site) with { VehicleMovementId = truck.Id, VehicleRegistration = null, DriverName = null, MeasureBasis = WoodMeasureBasis.Weighbridge, GrossKg = 12_500, TareLater = true, MoistureSamples = [30] }, Clerk);

        Assert.Equal((WoodDecisions.Pending, 0m, 0m, 0m), (waiting.Decision, waiting.NetKg, waiting.PayableKg, waiting.AmountPayable));
        Assert.Equal(("KCA123A", "Peter Otieno", truck.Id), (waiting.VehicleRegistration, waiting.DriverName, waiting.VehicleMovementId));
        Assert.Null(waiting.TareKg);
        Assert.Empty(db.WoodStockMovements);
        Assert.Empty(sent.Sent);
        Assert.Equal(1, (await service.SummaryAsync(site.TenantId, site.FactoryId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1))).AwaitingTare);

        var done = await service.CompleteAsync(site.TenantId, waiting.Id, new CompleteWoodDelivery(5_300, [32, 34], false, null, false, 5000m, site.StackId, null), Clerk);

        Assert.Equal((7200m, 32m, WoodDecisions.AcceptedWithDeduction), (done.NetKg, done.AverageMoisturePct, done.Decision));
        Assert.Equal(3, await db.WoodMoistureSamples.CountAsync());
        Assert.Equal(7200m, await service.StockKgAsync(site.TenantId, site.FactoryId, site.StackId));
        Assert.Equal(5_300m, done.TareKg);
        Assert.True(done.AmountPayable > 0);
        Assert.Equal(0, (await service.SummaryAsync(site.TenantId, site.FactoryId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1))).AwaitingTare);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CompleteAsync(site.TenantId, waiting.Id, new CompleteWoodDelivery(5_300, null, false, null, false, null, null, null), Clerk));
    }

    [Fact]
    public async Task TheGate_HoldsATruckWhoseWoodIsNotWeighedOut_UntilItIs()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var service = Service(db);
        var gate = new VehicleLogService(db);
        var truck = await gate.EnterAsync(site.TenantId, new NewVehicleEntry(site.FactoryId, "KCA 123A", "Delivery"), GateOfficer);
        var waiting = await service.ReceiveAsync(site.TenantId, Blank(site) with { VehicleMovementId = truck.Id, MeasureBasis = WoodMeasureBasis.Weighbridge, GrossKg = 12_500, TareLater = true }, Clerk);

        var held = await Assert.ThrowsAsync<BusinessRuleException>(() => gate.ExitAsync(site.TenantId, truck.Id, null, GateOfficer));
        Assert.Contains(waiting.DeliveryNumber, held.Message);
        Assert.Null((await db.VehicleMovements.SingleAsync()).ExitedAt);

        await service.CompleteAsync(site.TenantId, waiting.Id, new CompleteWoodDelivery(5_300, [20], false, null, false, null, site.StackId, null), Clerk);
        Assert.NotNull((await gate.ExitAsync(site.TenantId, truck.Id, null, GateOfficer)).ExitedAt);
    }

    [Fact]
    public async Task AVisitorManager_CanLetTheTruckGo_AndATurnedAwayTruckIsClosedWithNoTare()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var service = Service(db);
        var gate = new VehicleLogService(db);

        var first = await gate.EnterAsync(site.TenantId, new NewVehicleEntry(site.FactoryId, "KCA 123A", "Delivery"), GateOfficer);
        await service.ReceiveAsync(site.TenantId, Blank(site) with { VehicleMovementId = first.Id, MeasureBasis = WoodMeasureBasis.Weighbridge, GrossKg = 9_000, TareLater = true }, Clerk);
        Assert.NotNull((await gate.ExitAsync(site.TenantId, first.Id, "Manager let it go", GateManager)).ExitedAt);

        var second = await gate.EnterAsync(site.TenantId, new NewVehicleEntry(site.FactoryId, "KDB 456B", "Delivery"), GateOfficer);
        var turned = await service.ReceiveAsync(site.TenantId, Blank(site) with { VehicleMovementId = second.Id, MeasureBasis = WoodMeasureBasis.Weighbridge, GrossKg = 9_000, TareLater = true }, Clerk);
        var closed = await service.CompleteAsync(site.TenantId, turned.Id, new CompleteWoodDelivery(null, null, true, "Wet and full of soil", false, null, null, null), Clerk);

        Assert.Equal((WoodDecisions.Rejected, 0m, "Wet and full of soil"), (closed.Decision, closed.NetKg, closed.DecisionReason));
        Assert.Empty(db.WoodStockMovements);
        Assert.NotNull((await gate.ExitAsync(site.TenantId, second.Id, null, GateOfficer)).ExitedAt);
    }

    [Fact]
    public async Task ALinkedVehicle_MustBeAtThisFactoryGate_AndATareCanOnlyWaitOnTheWeighbridge()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var service = Service(db);
        var gate = new VehicleLogService(db);
        var elsewhere = await gate.EnterAsync(site.TenantId, new NewVehicleEntry(site.OtherFactoryId, "KCA 123A", "Delivery"), GateOfficer);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ReceiveAsync(site.TenantId, Weighed(site, 12_500, 5_300, [20]) with { VehicleMovementId = elsewhere.Id }, Clerk));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ReceiveAsync(site.TenantId, Weighed(site, 12_500, 5_300, [20]) with { VehicleMovementId = Guid.NewGuid() }, Clerk));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ReceiveAsync(site.TenantId, Blank(site) with { MeasureBasis = WoodMeasureBasis.LoadCount, Loads = 2, KgPerLoad = 2000, TareLater = true }, Clerk));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ReceiveAsync(site.TenantId, Blank(site) with { MeasureBasis = WoodMeasureBasis.Weighbridge, TareLater = true }, Clerk));
        Assert.Empty(db.WoodDeliveries);
    }

    [Fact]
    public async Task TheClerk_SeesTrucksAtTheGate_AndTheGateRegisterShowsAWaitingDelivery()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var gate = new VehicleLogService(db);
        var truck = await gate.EnterAsync(site.TenantId, new NewVehicleEntry(site.FactoryId, "KCA 123A", "Delivery", "Peter"), GateOfficer);
        await gate.EnterAsync(site.TenantId, new NewVehicleEntry(site.FactoryId, "KDB 456B", "Visitor"), GateOfficer);
        var clerk = Controller(db, site, PermissionNames.WoodFuelReceive);

        var before = Assert.IsAssignableFrom<IReadOnlyList<WoodGateVehicle>>(((OkObjectResult)(await clerk.GateVehicles(site.FactoryId, default)).Result!).Value);
        var listed = Assert.Single(before); // visitors' cars are not offered
        Assert.Equal(("KCA123A", false), (listed.Registration, listed.AwaitingTare));

        var made = (WoodDeliveryDto)((OkObjectResult)(await clerk.Receive(Request(site, 12_500, 5_300) with { VehicleMovementId = truck.Id, TareKg = null, TareLater = true }, default)).Result!).Value!;
        Assert.Equal(WoodDecisions.Pending, made.Decision);
        var after = Assert.Single(((IReadOnlyList<WoodGateVehicle>)((OkObjectResult)(await clerk.GateVehicles(site.FactoryId, default)).Result!).Value!));
        Assert.Equal((true, made.Id), (after.AwaitingTare, after.PendingDeliveryId));

        var weighed = (WoodDeliveryDto)((OkObjectResult)(await clerk.WeighOut(made.Id, new WeighOutRequest(5_300m, [20m], StackId: site.StackId), default)).Result!).Value!;
        Assert.Equal(WoodDecisions.Accepted, weighed.Decision);
        await Assert.ThrowsAsync<ForbiddenException>(() => Controller(db, site, PermissionNames.WoodFuelIssue).WeighOut(made.Id, new WeighOutRequest(5_300m), default));
    }

    // ------------------------------------------------------------------ alerts

    [Fact]
    public async Task ARejectedLoad_TellsTheSupervisors_WithTheSupplierAndTheReason()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var sent = new RecordingDispatcher();
        var service = Service(db, sent);

        await service.ReceiveAsync(site.TenantId, Weighed(site, 12_500, 5_300, [55]), Clerk);

        var alert = Assert.Single(sent.Sent);
        Assert.Equal(WoodFuelNotifications.DeliveryRejected, alert.Key);
        Assert.Contains("Kiprop Woodlot", alert.Title);
        Assert.Contains("above the 45% limit", alert.Body);
        Assert.Equal("Chelal", alert.TemplateValues["Factory"]);
        Assert.NotEmpty(alert.UserIds);
        Assert.Equal(NotificationSeverities.Warning, alert.Severity);
    }

    [Fact]
    public async Task WetWood_OnlyTellsAnyoneAtOrAboveTheAlertLevel()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var sent = new RecordingDispatcher();
        var service = Service(db, sent);

        await service.ReceiveAsync(site.TenantId, Weighed(site, 15_000, 5_000, [30]), Clerk);   // accepted, below 35 %
        Assert.Empty(sent.Sent);

        await service.ReceiveAsync(site.TenantId, Weighed(site, 15_000, 5_000, [36]), Clerk);   // accepted at 36 %: paid less
        var alert = Assert.Single(sent.Sent);
        Assert.Equal(WoodFuelNotifications.WetWood, alert.Key);
        Assert.Contains("36%", alert.Title);
        Assert.Contains("not paid", alert.Body);

        await service.ReceiveAsync(site.TenantId, Weighed(site, 15_000, 5_000, [20]), Clerk);   // dry: nothing more
        Assert.Single(sent.Sent);
    }

    [Fact]
    public async Task LowCover_IsSentOncePerDip_AndAgainOnlyAfterCoverRecovers()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var sent = new RecordingDispatcher();
        var service = Service(db, sent);
        db.WoodFuelSettings.Add(new WoodFuelSettings { TenantId = site.TenantId, FactoryId = site.FactoryId, BurnWindowDays = 1, LowCoverDays = 3 });
        await db.SaveChangesAsync();

        await service.ReceiveAsync(site.TenantId, Weighed(site, 15_000, 5_000, [20]), Clerk);                       // 10 t
        await service.IssueAsync(site.TenantId, new NewWoodIssue(site.FactoryId, site.BoilerId, site.StackId, 2000m, null, null, null, null), Clerk); // 8 t at 2 t a day: 4 days
        Assert.Empty(sent.Sent.Where(x => x.Key == WoodFuelNotifications.LowCover));

        await service.IssueAsync(site.TenantId, new NewWoodIssue(site.FactoryId, site.BoilerId, site.StackId, 3000m, null, null, null, null), Clerk); // 5 t at 5 t a day: 1 day
        var first = Assert.Single(sent.Sent, x => x.Key == WoodFuelNotifications.LowCover);
        Assert.Equal(NotificationSeverities.Warning, first.Severity);
        Assert.Contains("5 t", first.Body);
        Assert.NotNull((await db.WoodFuelSettings.SingleAsync()).LowCoverAlertSentAt);

        // Still low: no second alert for the same dip.
        await service.IssueAsync(site.TenantId, new NewWoodIssue(site.FactoryId, site.BoilerId, site.StackId, 1000m, null, null, null, null), Clerk);
        Assert.Single(sent.Sent, x => x.Key == WoodFuelNotifications.LowCover);

        // A big delivery ends the dip and clears the flag ...
        await service.ReceiveAsync(site.TenantId, Weighed(site, 60_000, 5_000, [20]), Clerk);
        Assert.Null((await db.WoodFuelSettings.SingleAsync()).LowCoverAlertSentAt);

        // ... so the next dip is told again, and under a day is critical.
        await service.IssueAsync(site.TenantId, new NewWoodIssue(site.FactoryId, site.BoilerId, site.StackId, 50_000m, null, null, null, null), Clerk);
        var again = sent.Sent.Where(x => x.Key == WoodFuelNotifications.LowCover).ToList();
        Assert.Equal(2, again.Count);
        Assert.Equal(NotificationSeverities.Critical, again[1].Severity);
    }

    [Fact]
    public async Task ADeliveryIsKept_EvenWhenTheAlertCannotBeSent()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var service = Service(db, new ThrowingDispatcher());

        var delivery = await service.ReceiveAsync(site.TenantId, Weighed(site, 12_500, 5_300, [55]), Clerk);

        Assert.Equal(WoodDecisions.Rejected, delivery.Decision);
        Assert.Equal(1, await db.WoodDeliveries.CountAsync());
    }

    [Fact]
    public async Task TheWetWoodAlertLevel_StaysBetweenTheBaseAndTheRejectLimit()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var manager = Controller(db, site, PermissionNames.WoodFuelManage);

        var saved = (WoodSettingsDto)((OkObjectResult)(await manager.SaveSettings(Settings(site) with { WetAlertMoisturePct = 38m }, default)).Result!).Value!;
        Assert.Equal(38m, saved.WetAlertMoisturePct);
        await Assert.ThrowsAsync<BusinessRuleException>(() => manager.SaveSettings(Settings(site) with { WetAlertMoisturePct = 50m }, default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => manager.SaveSettings(Settings(site) with { WetAlertMoisturePct = 20m }, default));

        // Left out, it is kept, and brought inside new limits.
        var kept = (WoodSettingsDto)((OkObjectResult)(await manager.SaveSettings(Settings(site), default)).Result!).Value!;
        Assert.Equal(38m, kept.WetAlertMoisturePct);
        var narrowed = (WoodSettingsDto)((OkObjectResult)(await manager.SaveSettings(Settings(site) with { RejectMoisturePct = 36m }, default)).Result!).Value!;
        Assert.Equal(36m, narrowed.WetAlertMoisturePct);
    }

    // ------------------------------------------------------------------ the module

    [Fact]
    public async Task TheController_KeepsCountsAndMoneyBehindThePermissions()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var clerk = Controller(db, site, PermissionNames.WoodFuelReceive);
        var boiler = Controller(db, site, PermissionNames.WoodFuelIssue);
        var nobody = Controller(db, site);
        var manager = Controller(db, site, PermissionNames.WoodFuelManage);

        var made = Assert.IsType<WoodDeliveryDto>(Assert.IsType<OkObjectResult>((await clerk.Receive(Request(site, 12_500, 5_300), default)).Result).Value);
        Assert.Empty(made.SupplierPhone); // payment and contact details are for managers
        await Assert.ThrowsAsync<ForbiddenException>(() => boiler.Receive(Request(site, 12_500, 5_300), default));
        await Assert.ThrowsAsync<ForbiddenException>(() => nobody.Deliveries(null, null, null, null, null, null, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => clerk.SaveSettings(Settings(site), default));
        await Assert.ThrowsAsync<ForbiddenException>(() => clerk.SaveProduction(new SaveProductionRequest(site.FactoryId, DateTime.UtcNow, 1000m), default));
        await Assert.ThrowsAsync<ForbiddenException>(() => clerk.ExportDeliveries(null, null, null, null, default));

        var issued = Assert.IsType<WoodIssueDto>(Assert.IsType<OkObjectResult>((await boiler.Issue(new IssueWoodRequest(site.FactoryId, site.BoilerId, site.StackId, 500m, null, null), default)).Result).Value);
        Assert.True(issued.OnMeter);
        Assert.IsType<OkObjectResult>((await manager.SaveSettings(Settings(site), default)).Result);
        Assert.IsType<OkObjectResult>((await manager.SaveProduction(new SaveProductionRequest(site.FactoryId, DateTime.UtcNow, 1200m), default)).Result);
        Assert.IsType<OkObjectResult>((await manager.SaveProduction(new SaveProductionRequest(site.FactoryId, DateTime.UtcNow, 1300m), default)).Result);
        Assert.Equal(1300m, (await db.ProductionDays.SingleAsync()).MadeTeaKg);
        var manager2 = (WoodDeliveryDto)((OkObjectResult)(await manager.Receive(Request(site, 9_000, 5_000), default)).Result!).Value!;
        Assert.Equal("+254 700 111 222", manager2.SupplierPhone);
        Assert.IsType<FileContentResult>(await manager.ExportDeliveries(null, null, null, null, default));

        await Assert.ThrowsAsync<BusinessRuleException>(() => manager.SaveSettings(Settings(site) with { RejectMoisturePct = 10m }, default));
        var lookups = (WoodLookups)((OkObjectResult)(await manager.Lookups(null, default)).Result!).Value!;
        Assert.Contains(lookups.Assets, x => x.Id == site.BoilerId);
        Assert.Equal(8, lookups.Species.Count);
    }

    [Fact]
    public async Task TheSupplierKeepsOnlyTheLastFourOfTheNationalId()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var manager = Controller(db, site, PermissionNames.WoodFuelManage);

        var made = (WoodSupplierDto)((OkObjectResult)(await manager.CreateSupplier(new SaveSupplierRequest("Joseph Kiprop", FuelSupplierTypes.Farmer, NationalId: "23 456 789", PermitExpiresOn: DateTime.UtcNow.AddDays(-2)), default)).Result!).Value!;

        Assert.Equal("6789", made.IdLast4);
        Assert.True(made.PermitExpired);
        Assert.DoesNotContain("23456", System.Text.Json.JsonSerializer.Serialize(made));
        await Assert.ThrowsAsync<BusinessRuleException>(() => manager.CreateSupplier(new SaveSupplierRequest("X Y", "Pirate"), default));
    }

    [Fact]
    public void TheModuleIsAnAddOn_WithItsOwnPermissions()
    {
        Assert.Contains(ServiceOpsPlans.WoodFuel, ServiceOpsPlans.OperationsAddOnModules);
        Assert.Equal([ServiceOpsPlans.WoodFuel], ServiceOpsPlans.RequiredModules("WoodFuel", "/api/wood-fuel/summary"));
        Assert.Equal("woodfuel", ServiceOpsPlans.NormalizeOperationsAddOns(["woodfuel"]));
        Assert.True(ServiceOpsPlans.IsClassified("WoodFuel"));
        Assert.Contains(PermissionNames.WoodFuelManage, PermissionNames.CapabilityOnly);
        Assert.Contains(PermissionNames.WoodFuelReceive, CapabilityDefaultsFor(AppRoles.Manager));
        Assert.DoesNotContain(PermissionNames.WoodFuelReceive, CapabilityDefaultsFor(AppRoles.Technician));
    }

    // ------------------------------------------------------------------ fixtures

    private sealed record Site(Guid TenantId, Guid FactoryId, Guid OtherFactoryId, Guid StackId, Guid BoilerId, Guid SupplierId, FactoryTenantContext Context);

    private static IWoodFuelService Service(AppDbContext db, INotificationDispatcher? dispatcher = null) =>
        new WoodFuelService(db, new MeterReadingService(db), FactoryTestSupport.Scope(db, new FactoryTenantContext(Guid.NewGuid(), Guid.NewGuid())), dispatcher);

    private sealed class RecordingDispatcher : INotificationDispatcher
    {
        public List<NotificationRequest> Sent { get; } = [];

        public Task NotifyAsync(NotificationRequest request, CancellationToken cancellationToken = default)
        {
            Sent.Add(request);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingDispatcher : INotificationDispatcher
    {
        public Task NotifyAsync(NotificationRequest request, CancellationToken cancellationToken = default) => throw new InvalidOperationException("The mail server is down.");
    }

    private static async Task<Site> SeedAsync(AppDbContext db)
    {
        var (tenant, admin) = await FactoryTestSupport.SeedTenantAsync(db);
        var factory = new OrgNode { TenantId = tenant.Id, Level = OrgNodeLevels.Factory, Name = "Chelal", Code = "CHL" };
        var other = new OrgNode { TenantId = tenant.Id, Level = OrgNodeLevels.Factory, Name = "Momul", Code = "MML" };
        var boilerHouse = new OrgNode { TenantId = tenant.Id, ParentId = factory.Id, Level = OrgNodeLevels.Section, Name = "Boiler house", Code = "CHL-BLR" };
        db.OrgNodes.AddRange(factory, other, boilerHouse);
        var boiler = new Asset { TenantId = tenant.Id, AssetName = "Boiler 1", AssetCode = "BLR-1", AssetType = "Boiler", OrgNodeId = boilerHouse.Id };
        var stack = new WoodStack { TenantId = tenant.Id, FactoryId = factory.Id, Code = "Y1-S01" };
        var supplier = new FuelSupplier { TenantId = tenant.Id, Name = "Kiprop Woodlot", Type = FuelSupplierTypes.Farmer, Phone = "+254 700 111 222", PaymentDetails = "M-Pesa 0700 111 222" };
        db.Assets.Add(boiler);
        db.WoodStacks.Add(stack);
        db.FuelSuppliers.Add(supplier);
        await db.SaveChangesAsync();
        await new WoodFuelService(db, new MeterReadingService(db), FactoryTestSupport.Scope(db, new FactoryTenantContext(tenant.Id, admin.Id))).EnsureSpeciesAsync(tenant.Id);
        return new Site(tenant.Id, factory.Id, other.Id, stack.Id, boiler.Id, supplier.Id, new FactoryTenantContext(tenant.Id, admin.Id));
    }

    private static NewWoodDelivery Blank(Site site) =>
        new(site.FactoryId, site.SupplierId, null, null, null, null, null, "kca 123a", "Peter", null, null, null, null, null, null, null, null, false, null, false, null, site.StackId, null);

    private static NewWoodDelivery Weighed(Site site, decimal gross, decimal tare, decimal[] moisture) =>
        Blank(site) with { MeasureBasis = WoodMeasureBasis.Weighbridge, GrossKg = gross, TareKg = tare, MoistureSamples = moisture };

    private static ReceiveDeliveryRequest Request(Site site, decimal gross, decimal tare) =>
        new(site.FactoryId, site.SupplierId, null, null, null, null, null, "KCA 123A", "Peter", null, WoodMeasureBasis.Weighbridge, gross, tare, null, null, null, [20m], StackId: site.StackId);

    private static SaveWoodSettingsRequest Settings(Site site) =>
        new(site.FactoryId, WoodMeasureBasis.Weighbridge, WoodMoistureRules.DeductKg, 25m, 45m, 1.5m, null, 2500m, 14, 5, 28, 1.6m);

    private static WoodFuelController Controller(AppDbContext db, Site site, params string[] permissions)
    {
        var context = new FactoryTenantContext(site.TenantId, Guid.NewGuid(), isAdmin: false, permissions);
        return FactoryAssetRegisterTests.WithHttp(new WoodFuelController(db, context, FactoryTestSupport.Scope(db, context), new WoodFuelService(db, new MeterReadingService(db), FactoryTestSupport.Scope(db, context))));
    }

    private static IReadOnlyList<string> CapabilityDefaultsFor(string role) => CapabilityRoleDefaults.For(role);

    /// <summary>Runs the action as a phone that was offline would: dated when it happened, not when it syncs.</summary>
    private static async Task<T> At<T>(DateTime when, Func<Task<T>> action)
    {
        Assert.True(OccurredAt.TrySet(when));
        try { return await action(); }
        finally { OccurredAt.Clear(); }
    }
}
