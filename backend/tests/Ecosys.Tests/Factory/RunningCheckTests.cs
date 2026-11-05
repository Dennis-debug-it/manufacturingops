using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Errors;
using Ecosys.Shared.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ecosys.Tests.Factory;

public sealed class RunningCheckTests
{
    private static readonly TimeZoneInfo Nairobi = TimeZoneInfo.FindSystemTimeZoneById("Africa/Nairobi");
    private static readonly IReadOnlyList<ShiftDefinition> TwoShifts = ShiftCalendar.Parse("""[{"name":"Day","start":"06:00","end":"18:00"},{"name":"Night","start":"18:00","end":"06:00"}]""");

    [Fact]
    public void Shifts_AreWorkedOutInTheFactorysTimeZone_AndNightShiftsKeepTheirStartDate()
    {
        // 10:00 in Nairobi (UTC+3) is the day shift of the 5th, 06:00–18:00 local.
        var day = ShiftCalendar.Current(CheckFrequencies.PerShift, TwoShifts, Nairobi, new DateTime(2026, 11, 5, 7, 0, 0, DateTimeKind.Utc))!;
        Assert.Equal("2026-11-05/Day", day.Key);
        Assert.Equal(new DateTime(2026, 11, 5, 3, 0, 0), day.StartUtc);
        Assert.Equal(new DateTime(2026, 11, 5, 15, 0, 0), day.EndUtc);

        // 02:00 on the 6th local is still the night shift that started on the 5th.
        var night = ShiftCalendar.Current(CheckFrequencies.PerShift, TwoShifts, Nairobi, new DateTime(2026, 11, 5, 23, 0, 0, DateTimeKind.Utc))!;
        Assert.Equal("2026-11-05/Night", night.Key);
        Assert.Equal(new DateTime(2026, 11, 6, 3, 0, 0), night.EndUtc);

        var daily = ShiftCalendar.Current(CheckFrequencies.Daily, TwoShifts, Nairobi, new DateTime(2026, 11, 5, 23, 0, 0, DateTimeKind.Utc))!;
        Assert.Equal("2026-11-06", daily.Key);

        // Between shifts nothing is due.
        var gaps = ShiftCalendar.Parse("""[{"name":"Day","start":"07:00","end":"17:00"}]""");
        Assert.Null(ShiftCalendar.Current(CheckFrequencies.PerShift, gaps, Nairobi, new DateTime(2026, 11, 5, 16, 0, 0, DateTimeKind.Utc)));

        Assert.Throws<BusinessRuleException>(() => ShiftCalendar.Parse("""[{"name":"Day","start":"seven","end":"17:00"}]"""));
        Assert.Throws<BusinessRuleException>(() => ShiftCalendar.Parse("[]"));
    }

    [Fact]
    public async Task TeaTemplates_AreInstalledOnce_AndLinkedToTheirAssetClass()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var checks = Controller(db, site.Context);

        var templates = Ok(await checks.Templates(CancellationToken.None));
        Assert.Equal(6, templates.Count);
        Assert.Equal(6, Ok(await checks.Templates(CancellationToken.None)).Count);
        var dryer = templates.Single(x => x.Name == "Fluid bed dryer shift check");
        Assert.Equal(site.DryerClass, dryer.AssetCategoryId);
        Assert.Equal(CheckFrequencies.PerShift, dryer.SuggestedFrequency);
        Assert.Equal(CheckFrequencies.Daily, templates.Single(x => x.Name == "Generator daily check").SuggestedFrequency);
    }

    [Fact]
    public async Task Schedule_TakesEveryAssetOfTheClassInTheSection_AndChecksAreGeneratedOncePerShift()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var checks = Controller(db, site.Context);
        var template = Ok(await checks.Templates(CancellationToken.None)).Single(x => x.Name == "Fluid bed dryer shift check");

        var schedule = Ok(await checks.CreateSchedule(new UpsertCheckScheduleRequest("Dryer rounds", template.Id, "PerShift", site.Factory, IsRoute: true), CancellationToken.None));
        Assert.Equal(["FBD-01", "FBD-02"], schedule.Assets.Select(x => x.AssetCode).ToArray());
        await Assert.ThrowsAsync<BusinessRuleException>(() => checks.CreateSchedule(
            new UpsertCheckScheduleRequest("Wrong", template.Id, "PerShift", site.Drying, AssetIds: [site.Elsewhere.Id]), CancellationToken.None));

        var due = Ok(await checks.Due(null, null, CancellationToken.None));
        Assert.Equal(2, due.Count);
        Assert.All(due, x => Assert.Equal(CheckRunStatuses.Due, x.Status));
        Assert.Equal(2, Ok(await checks.Due(null, null, CancellationToken.None)).Count);
        Assert.Equal(2, await db.CheckRuns.CountAsync());
        Assert.Single(Ok(await checks.Due(null, site.Dryer1.Id, CancellationToken.None)));
    }

    [Fact]
    public async Task Submitting_ScoresAnswers_FeedsMeters_AndRaisesJobsForFailures()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var checks = Controller(db, site.Context);
        var template = Ok(await checks.Templates(CancellationToken.None)).Single(x => x.Name == "Fluid bed dryer shift check");
        Ok(await checks.CreateSchedule(new UpsertCheckScheduleRequest("Dryer rounds", template.Id, "PerShift", site.Factory, AssetIds: [site.Dryer1.Id]), CancellationToken.None));
        var run = Assert.Single(Ok(await checks.Due(null, null, CancellationToken.None)));
        var detail = Ok(await checks.Run(run.Id, CancellationToken.None));
        Guid Q(string prompt) => detail.Questions.Single(x => x.Prompt.StartsWith(prompt, StringComparison.Ordinal)).Id;

        // Required answers can't be skipped, and a failure needs a note.
        await Assert.ThrowsAsync<BusinessRuleException>(() => checks.Submit(run.Id, new SubmitCheckRequest([new(Q("Inlet"), "120")]), CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(() => checks.Submit(run.Id, new SubmitCheckRequest(
        [
            new(Q("Inlet"), "150"), new(Q("Bed"), "Pass"), new(Q("Fire"), "Pass"),
        ]), CancellationToken.None));

        var result = Ok(await checks.Submit(run.Id, new SubmitCheckRequest(
        [
            new(Q("Inlet"), "150", "Burner running hot"),
            new(Q("Bed"), "Fail", "Dead spot at the back"),
            new(Q("Fire"), "Pass"),
            new(Q("Dryer run hours"), "8123"),
        ], Notes: "Night operator: Ali"), CancellationToken.None));

        Assert.Equal(2, result.Failed);
        var job = await db.WorkOrders.SingleAsync(x => x.WorkOrderNumber == Assert.Single(result.RaisedWorkOrders));
        Assert.Equal(FactoryRequestSources.RunningCheck, job.RequestSource);
        Assert.Equal(site.Dryer1.Id, job.AssetId);
        Assert.Contains("(+1 more)", job.Title);
        Assert.Contains("Inlet air temperature: 150 °C (Burner running hot)", job.Description);

        // Readings went onto the asset's meters.
        Assert.Equal(8123m, (await db.Meters.SingleAsync(x => x.AssetId == site.Dryer1.Id && x.MeterType == MeterTypes.RunHours)).CurrentValue);
        Assert.Equal(150m, (await db.Meters.SingleAsync(x => x.AssetId == site.Dryer1.Id && x.MeterType == MeterTypes.Temperature)).CurrentValue);

        var done = Ok(await checks.Run(run.Id, CancellationToken.None));
        Assert.Equal(CheckRunStatuses.Completed, done.Run.Status);
        Assert.False(done.Questions.Single(x => x.Prompt.StartsWith("Bed", StringComparison.Ordinal)).Passed);
        Assert.Equal(job.Id, done.Questions.Single(x => x.Prompt.StartsWith("Inlet", StringComparison.Ordinal)).RaisedWorkOrderId);
        await Assert.ThrowsAsync<BusinessRuleException>(() => checks.Submit(run.Id, new SubmitCheckRequest([]), CancellationToken.None));
    }

    [Fact]
    public async Task WindowDefects_BecomeLineStopJobs_AndMissedChecksAreFlaggedToSupervisors()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var checks = Controller(db, site.Context);
        var templates = Ok(await checks.Templates(CancellationToken.None));
        var compressor = templates.Single(x => x.Name == "Compressor daily check");
        Ok(await checks.CreateSchedule(new UpsertCheckScheduleRequest("Compressor", compressor.Id, "Daily", site.Factory, AssetIds: [site.Dryer1.Id, site.Dryer2.Id]), CancellationToken.None));
        var runs = Ok(await checks.Due(null, null, CancellationToken.None));
        var first = runs.Single(x => x.AssetId == site.Dryer1.Id);
        var detail = Ok(await checks.Run(first.Id, CancellationToken.None));
        Guid Q(string prompt) => detail.Questions.Single(x => x.Prompt.StartsWith(prompt, StringComparison.Ordinal)).Id;

        var result = Ok(await checks.Submit(first.Id, new SubmitCheckRequest(
        [
            new(Q("Discharge"), "7.2"), new(Q("Oil"), "Fail", "Slightly low"), new(Q("Condensate"), "Yes"),
        ]), CancellationToken.None));
        var defect = await db.WorkOrders.SingleAsync(x => x.WorkOrderNumber == Assert.Single(result.RaisedWorkOrders));
        Assert.True(defect.NeedsLineStop);
        Assert.StartsWith("Fix in window", defect.Title);

        // The day ends with the second check not done.
        var dispatcher = new RecordingDispatcher();
        var runner = new RunningCheckRunner(db, Service(db), NullLogger<RunningCheckRunner>.Instance, dispatcher);
        var endOfDay = (await db.CheckRuns.SingleAsync(x => x.Id == first.Id)).DueBy.AddMinutes(1);
        Assert.Equal(1, await runner.RunAsync(endOfDay));
        Assert.Equal(CheckRunStatuses.Missed, (await db.CheckRuns.SingleAsync(x => x.AssetId == site.Dryer2.Id && x.PeriodKey == first.PeriodKey)).Status);
        // The next day's checks were created as the day turned.
        Assert.Equal(2, await db.CheckRuns.CountAsync(x => x.PeriodKey != first.PeriodKey && x.Status == CheckRunStatuses.Due));
        var notice = Assert.Single(dispatcher.Sent);
        Assert.Equal(RunningCheckRunner.MissedKey, notice.Key);
        Assert.Contains(site.AdminId, notice.UserIds);
        Assert.Equal(0, await runner.RunAsync(endOfDay.AddMinutes(10))); // flagged once
    }

    private sealed record Seeded(Guid TenantId, Guid AdminId, FactoryTenantContext Context, Guid Factory, Guid Drying, Guid DryerClass, Asset Dryer1, Asset Dryer2, Asset Elsewhere);

    private static async Task<Seeded> SeedAsync(AppDbContext db)
    {
        var (tenant, admin) = await FactoryTestSupport.SeedTenantAsync(db);
        await new AssetClassService(db).InstallAsync(tenant.Id, IndustryTemplates.TeaKey);
        var dryerClass = await db.AssetCategories.SingleAsync(x => x.Code == "TEA-FBD");
        var factory = new OrgNode { TenantId = tenant.Id, Level = OrgNodeLevels.Factory, Name = "Chelal", Code = "CHL" };
        var drying = new OrgNode { TenantId = tenant.Id, ParentId = factory.Id, Level = OrgNodeLevels.Section, Name = "Drying", Code = "CHL-DRY" };
        var other = new OrgNode { TenantId = tenant.Id, Level = OrgNodeLevels.Factory, Name = "Momul", Code = "MML" };
        var dryer1 = new Asset { TenantId = tenant.Id, AssetName = "Dryer 1", AssetCode = "FBD-01", OrgNodeId = drying.Id, AssetCategoryId = dryerClass.Id, Criticality = "A" };
        var dryer2 = new Asset { TenantId = tenant.Id, AssetName = "Dryer 2", AssetCode = "FBD-02", OrgNodeId = drying.Id, AssetCategoryId = dryerClass.Id };
        var elsewhere = new Asset { TenantId = tenant.Id, AssetName = "Momul dryer", AssetCode = "MML-FBD", OrgNodeId = other.Id, AssetCategoryId = dryerClass.Id };
        db.OrgNodes.AddRange(factory, drying, other);
        db.Assets.AddRange(dryer1, dryer2, elsewhere);
        db.Meters.AddRange(
            new Meter { TenantId = tenant.Id, AssetId = dryer1.Id, Name = "Run hours", MeterType = MeterTypes.RunHours, Unit = "h", IsPrimary = true },
            new Meter { TenantId = tenant.Id, AssetId = dryer1.Id, Name = "Inlet temperature", MeterType = MeterTypes.Temperature, Unit = "°C", IsCumulative = false });
        await db.SaveChangesAsync();
        await new FailureCodeService(db).EnsureSeededAsync(tenant.Id);
        return new Seeded(tenant.Id, admin.Id, new FactoryTenantContext(tenant.Id, admin.Id), factory.Id, drying.Id, dryerClass.Id, dryer1, dryer2, elsewhere);
    }

    private static T Ok<T>(ActionResult<T> result) => Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private static RunningCheckService Service(AppDbContext db)
    {
        var anyone = new FactoryTenantContext(Guid.Empty, Guid.Empty);
        var scope = FactoryTestSupport.Scope(db, anyone);
        var timeZones = new TenantTimeZoneService(db, new MemoryCache(new MemoryCacheOptions()), Microsoft.Extensions.Options.Options.Create(new TimeZoneOptions { DefaultTimeZone = "Africa/Nairobi" }));
        var workOrders = new FactoryWorkOrderService(db, new DocumentNumberingService(db), FactoryTestSupport.Settings(db), new AssetLifecycleService(db, scope), scope, new FactorySlaService(db));
        return new RunningCheckService(db, FactoryTestSupport.Settings(db), timeZones, new MeterReadingService(db), workOrders);
    }

    private static RunningChecksController Controller(AppDbContext db, FactoryTenantContext context) =>
        FactoryAssetRegisterTests.WithHttp(new RunningChecksController(db, context, FactoryTestSupport.Access(context), FactoryTestSupport.Scope(db, context), Service(db)));

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
