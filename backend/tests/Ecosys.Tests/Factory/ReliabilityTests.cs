using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests.Factory;

public sealed class ReliabilityTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    [Fact]
    public async Task MtbfAndMttr_ComeFromTheBreakdowns_AndThePatternFromTheirIntervals()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var belt = await AddCodeAsync(db, site, "Belt slip");
        var conveyor = await AddAssetAsync(db, site, "Rolling table conveyor", "CHL-RT1");
        foreach (var daysAgo in new[] { 150, 120, 90, 60, 30 })
            AddBreakdown(db, site, conveyor, Now.AddDays(-daysAgo), hours: 4, problem: belt);
        AddBreakdown(db, site, conveyor, Now.AddDays(-10), hours: 2, problem: belt, status: WorkOrderStatuses.Cancelled); // dropped
        await db.SaveChangesAsync();

        var row = Assert.Single(await Service(db, site).AssetsAsync(site.TenantId, null, Now.AddDays(-365), Now));

        Assert.Equal((5, 4m, 20m, 30m, "Regular", "Belt slip", 5), (row.Failures, row.MttrHours, row.DowntimeHours, row.MeanDaysBetween, row.Pattern, row.TopProblem, row.TopProblemCount));
        Assert.Equal(Math.Round((365m * 24 - 20) / 5, 1), row.MtbfHours);
        Assert.Equal(0m, row.Variation);
    }

    [Fact]
    public async Task ARegularFailer_WithoutPm_IsSuggestedAPlan_AndApplyingItCreatesOne()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var belt = await AddCodeAsync(db, site, "Belt slip");
        var conveyor = await AddAssetAsync(db, site, "Rolling table conveyor", "CHL-RT1");
        foreach (var daysAgo in new[] { 150, 120, 90, 60, 30 })
            AddBreakdown(db, site, conveyor, Now.AddDays(-daysAgo), hours: 4, problem: belt);
        await db.SaveChangesAsync();
        var service = Service(db, site);

        var suggestion = Assert.Single(await service.SuggestionsAsync(site.TenantId, null, Now.AddDays(-365), Now));

        Assert.Equal((PmSuggestionKinds.NoPlan, PmTriggerTypes.Calendar, PmFrequencyUnits.Weeks, 3), (suggestion.Kind, suggestion.TriggerType, suggestion.FrequencyUnit, suggestion.FrequencyInterval));
        Assert.Contains("about every 30 days", suggestion.Reason);
        Assert.Contains("belt slip", suggestion.Reason);

        var plan = await service.ApplyAsync(site.TenantId, conveyor.Id, Now.AddDays(-365), Now);
        Assert.Equal((conveyor.Id, PmTriggerTypes.Calendar, PmFrequencyUnits.Weeks, 3), (plan.AssetId, plan.TriggerType, plan.FrequencyUnit, plan.FrequencyInterval));
        Assert.StartsWith("Reliability: 5 breakdowns", plan.Basis);
        Assert.Equal("Rolling table conveyor: belt slip check", plan.Name);

        // Now that a 3-weekly plan exists and is shorter than the breakdown interval, the history says
        // PM is not preventing the fault, and there is nothing more to schedule.
        var after = Assert.Single(await service.SuggestionsAsync(site.TenantId, null, Now.AddDays(-365), Now));
        Assert.Equal(PmSuggestionKinds.PlanNotPreventing, after.Kind);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ApplyAsync(site.TenantId, conveyor.Id, Now.AddDays(-365), Now));
    }

    [Fact]
    public async Task APlanLongerThanTheBreakdownInterval_IsShortened()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var dryer = await AddAssetAsync(db, site, "Fluid bed dryer", "CHL-FBD");
        foreach (var daysAgo in new[] { 200, 160, 120, 80, 40 })
            AddBreakdown(db, site, dryer, Now.AddDays(-daysAgo), hours: 6);
        db.PreventiveMaintenancePlans.Add(new PreventiveMaintenancePlan
        {
            TenantId = site.TenantId, AssetId = dryer.Id, OrgNodeId = dryer.OrgNodeId, Name = "Quarterly service", TriggerType = PmTriggerTypes.Calendar, Status = "Active",
            FrequencyUnit = PmFrequencyUnits.Months, FrequencyInterval = 3, Frequency = "Every 3 months", NextPmDate = Now.AddDays(20),
            ScheduleMode = PmScheduleModes.Floating, Priority = "Medium",
        });
        await db.SaveChangesAsync();
        var service = Service(db, site);

        var suggestion = Assert.Single(await service.SuggestionsAsync(site.TenantId, null, Now.AddDays(-365), Now));
        Assert.Equal((PmSuggestionKinds.PlanTooLong, "Quarterly service", "every 3 months", PmFrequencyUnits.Weeks, 4),
            (suggestion.Kind, suggestion.PlanName, suggestion.CurrentInterval, suggestion.FrequencyUnit, suggestion.FrequencyInterval));

        var plan = await service.ApplyAsync(site.TenantId, dryer.Id, Now.AddDays(-365), Now);
        Assert.Equal(("Quarterly service", PmFrequencyUnits.Weeks, 4), (plan.Name, plan.FrequencyUnit, plan.FrequencyInterval));
        Assert.Single(await db.PreventiveMaintenancePlans.ToListAsync());
    }

    [Fact]
    public async Task RandomBreakdowns_PointToConditionMonitoring_NotACalendar()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var fan = await AddAssetAsync(db, site, "Withering fan 4", "CHL-WF4");
        foreach (var daysAgo in new[] { 300, 298, 180, 178, 20 })
            AddBreakdown(db, site, fan, Now.AddDays(-daysAgo), hours: 1);
        await db.SaveChangesAsync();

        var suggestion = Assert.Single(await Service(db, site).SuggestionsAsync(site.TenantId, null, Now.AddDays(-365), Now));

        Assert.Equal(PmSuggestionKinds.Random, suggestion.Kind);
        Assert.Null(suggestion.TriggerType);
        Assert.Contains("condition monitoring", suggestion.Reason);
    }

    [Fact]
    public async Task WithRunHoursRecorded_TheSuggestionIsMeterBased()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var genset = await AddAssetAsync(db, site, "Standby generator", "CHL-GEN");
        var hours = new Meter { TenantId = site.TenantId, AssetId = genset.Id, Name = "Run hours", MeterType = MeterTypes.RunHours, Unit = "h", IsCumulative = true, IsPrimary = true, CurrentValue = 2100 };
        db.Meters.Add(hours);
        // 500 run hours between breakdowns, at irregular calendar spacing that still averages ~30 days.
        var failures = new[] { (120, 0m), (95, 500m), (60, 1000m), (35, 1500m), (5, 2000m) };
        foreach (var (daysAgo, run) in failures)
        {
            db.MeterReadings.Add(new MeterReading { TenantId = site.TenantId, MeterId = hours.Id, AssetId = genset.Id, Value = run, ReadAt = Now.AddDays(-daysAgo).AddHours(-1), Source = MeterReadingSources.Manual });
            AddBreakdown(db, site, genset, Now.AddDays(-daysAgo), hours: 3);
        }

        await db.SaveChangesAsync();

        var row = Assert.Single(await Service(db, site).AssetsAsync(site.TenantId, null, Now.AddDays(-365), Now));
        Assert.Equal((500m, "h"), (row.MeanRunBetween, row.MeterUnit));
        var suggestion = Assert.Single(await Service(db, site).SuggestionsAsync(site.TenantId, null, Now.AddDays(-365), Now));
        Assert.Equal((PmSuggestionKinds.NoPlan, PmTriggerTypes.Meter, hours.Id, 350m), (suggestion.Kind, suggestion.TriggerType, suggestion.MeterId, suggestion.MeterInterval));

        var plan = await Service(db, site).ApplyAsync(site.TenantId, genset.Id, Now.AddDays(-365), Now);
        Assert.Equal((PmTriggerTypes.Meter, 350m, 2450m), (plan.TriggerType, plan.MeterInterval, plan.NextMeterDue));
    }

    [Fact]
    public async Task RepeatFailures_FollowTheSameFaultOnTheSameAssetWithinTheWindow()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var seal = await AddCodeAsync(db, site, "Seal leak");
        var noise = await AddCodeAsync(db, site, "Bearing noise");
        var worn = await AddCodeAsync(db, site, "Worn seal", FailureCodeKinds.Cause);
        var pump = await AddAssetAsync(db, site, "Feed pump", "CHL-P1");
        var other = await AddAssetAsync(db, site, "Return pump", "CHL-P2");
        AddBreakdown(db, site, pump, Now.AddDays(-90), 2, seal, worn);
        AddBreakdown(db, site, pump, Now.AddDays(-80), 2, seal, worn);
        AddBreakdown(db, site, pump, Now.AddDays(-70), 2, seal);
        AddBreakdown(db, site, pump, Now.AddDays(-20), 2, seal);         // 50 days on: a fresh start, not a repeat
        AddBreakdown(db, site, pump, Now.AddDays(-15), 2, noise);        // a different fault
        AddBreakdown(db, site, other, Now.AddDays(-60), 2, seal);
        AddBreakdown(db, site, other, Now.AddDays(-5), 2, seal);         // 55 days apart: not a repeat
        await db.SaveChangesAsync();

        var analysis = await Service(db, site).RepeatFailuresAsync(site.TenantId, null, Now.AddDays(-365), Now, 30);

        Assert.Equal((7, 2, 28.6m, 1, 30), (analysis.Summary.Failures, analysis.Summary.Repeats, analysis.Summary.RepeatRatePercent, analysis.Summary.AssetsWithRepeats, analysis.Summary.RepeatWithinDays));
        var chain = Assert.Single(analysis.Chains);
        Assert.Equal(("CHL-P1", "Seal leak", 3), (chain.AssetCode, chain.Problem, chain.Failures.Count));
        Assert.Equal([null, 10m, 10m], chain.Failures.Select(x => x.DaysSincePrevious));
        var mode = analysis.ByMode[0];
        Assert.Equal(("Seal leak", 6, 2, 2, 10m, "Worn seal"), (mode.Problem, mode.Failures, mode.Repeats, mode.Assets, mode.MeanDaysBetweenRepeats, mode.TopCause));
        Assert.Equal(("CHL-P1", 5, 2), (analysis.ByAsset[0].AssetCode, analysis.ByAsset[0].Failures, analysis.ByAsset[0].Repeats));
    }

    [Theory]
    [InlineData(10, "Days", 10)]
    [InlineData(21, "Weeks", 3)]
    [InlineData(59, "Weeks", 8)]
    [InlineData(95, "Months", 3)]
    public void SuggestedIntervals_UseAPlannersUnit(double days, string unit, int interval) =>
        Assert.Equal((unit, interval), ReliabilityService.Calendar((decimal)days));

    [Theory]
    [InlineData(354, 350)]
    [InlineData(1234, 1200)]
    [InlineData(5678, 5600)]
    [InlineData(4, 10)]
    public void MeterIntervals_RoundDownToASensibleStep(double value, double expected) =>
        Assert.Equal((decimal)expected, ReliabilityService.RoundMeter((decimal)value));

    [Fact]
    public async Task Controller_ScopesByLocation_AndUsesTheReworkWindowByDefault()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var pump = await AddAssetAsync(db, site, "Feed pump", "CHL-P1");
        var seal = await AddCodeAsync(db, site, "Seal leak");
        AddBreakdown(db, site, pump, Now.AddDays(-40), 1, seal);
        AddBreakdown(db, site, pump, Now.AddDays(-10), 1, seal);
        await db.SaveChangesAsync();
        var controller = FactoryAssetRegisterTests.WithHttp(new ReliabilityController(site.Context, FactoryTestSupport.Access(site.Context),
            FactoryTestSupport.Scope(db, site.Context), Service(db, site), FactoryTestSupport.Settings(db), FactoryTestSupport.Audit(db)));

        Assert.Single(Ok(await controller.Assets(site.FactoryId, null, default)));
        var repeats = Ok(await controller.RepeatFailures(null, null, null, default));
        var window = (await FactoryTestSupport.Settings(db).GetOrCreateAsync(site.TenantId)).ReworkWindowDays;
        Assert.Equal(window, repeats.Summary.RepeatWithinDays);
        Assert.Equal(window >= 30 ? 1 : 0, repeats.Summary.Repeats);
        await Assert.ThrowsAsync<NotFoundException>(() => controller.Assets(Guid.NewGuid(), null, default));
    }

    private static T Ok<T>(ActionResult<T> result) => Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private sealed record Site(Guid TenantId, Guid FactoryId, FactoryTenantContext Context);

    private static async Task<Site> SeedAsync(AppDbContext db)
    {
        var (tenant, admin) = await FactoryTestSupport.SeedTenantAsync(db);
        var factory = new OrgNode { TenantId = tenant.Id, Level = OrgNodeLevels.Factory, Name = "Chelal", Code = "CHL" };
        db.OrgNodes.Add(factory);
        await db.SaveChangesAsync();
        return new Site(tenant.Id, factory.Id, new FactoryTenantContext(tenant.Id, admin.Id));
    }

    private static async Task<Asset> AddAssetAsync(AppDbContext db, Site site, string name, string code)
    {
        var asset = new Asset { TenantId = site.TenantId, AssetName = name, AssetCode = code, OrgNodeId = site.FactoryId, Status = "Active", Criticality = AssetCriticality.B };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();
        return asset;
    }

    private static async Task<FailureCode> AddCodeAsync(AppDbContext db, Site site, string name, string kind = FailureCodeKinds.Problem)
    {
        var code = new FailureCode { TenantId = site.TenantId, Kind = kind, Code = name.ToUpperInvariant().Replace(' ', '-'), Name = name };
        db.FailureCodes.Add(code);
        await db.SaveChangesAsync();
        return code;
    }

    private static int number;

    private static void AddBreakdown(AppDbContext db, Site site, Asset asset, DateTime at, decimal hours, FailureCode? problem = null, FailureCode? cause = null, string status = WorkOrderStatuses.Closed) =>
        db.WorkOrders.Add(new WorkOrder
        {
            TenantId = site.TenantId, AssetId = asset.Id, OrgNodeId = asset.OrgNodeId, WorkOrderNumber = $"WO-{Interlocked.Increment(ref number):00000}",
            Title = "Breakdown", IsBreakdown = true, Status = status, CreatedAt = at, DowntimeStartedAt = at, DowntimeEndedAt = at.AddHours((double)hours),
            FailureProblemCodeId = problem?.Id, FailureCauseCodeId = cause?.Id,
        });

    private static ReliabilityService Service(AppDbContext db, Site site) =>
        new(db, new FactoryPmService(db, FactoryTestSupport.WorkOrders(db, site.Context), FactoryTestSupport.Settings(db)));
}
