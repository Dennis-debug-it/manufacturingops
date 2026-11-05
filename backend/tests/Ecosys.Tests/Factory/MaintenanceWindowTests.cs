using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests.Factory;

public sealed class MaintenanceWindowTests
{
    private static readonly TimeZoneInfo Nairobi = TimeZoneInfo.FindSystemTimeZoneById("Africa/Nairobi");

    private static DateTime NextLocal(DayOfWeek day)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Nairobi);
        var date = local.Date.AddDays(((int)day - (int)local.DayOfWeek + 7) % 7);
        // The default window ends at 18:00: once that has passed, today's window is next week's.
        return date == local.Date && local.Hour >= 18 ? date.AddDays(7) : date;
    }

    [Fact]
    public async Task Each_factory_gets_its_next_two_windows_on_its_own_day_with_the_start_up_checks()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var momul = new OrgNode { TenantId = site.TenantId, Level = OrgNodeLevels.Factory, Name = "Momul", Code = "MML", WindowDay = DayOfWeek.Tuesday };
        var closed = new OrgNode { TenantId = site.TenantId, Level = OrgNodeLevels.Factory, Name = "Closed", Code = "CLS", WindowEnabled = false };
        db.OrgNodes.AddRange(momul, closed);
        await db.SaveChangesAsync();

        var windows = Ok(await Controller(db, site.Admin).List(null, 0, CancellationToken.None));
        Assert.Equal([NextLocal(DayOfWeek.Monday), NextLocal(DayOfWeek.Monday).AddDays(7)], windows.Where(x => x.FactoryId == site.Chelal).Select(x => x.WindowDate));
        Assert.Equal(NextLocal(DayOfWeek.Tuesday), windows.Where(x => x.FactoryId == momul.Id).Min(x => x.WindowDate));
        Assert.DoesNotContain(windows, x => x.FactoryId == closed.Id);

        // 06:00–18:00 Nairobi is 03:00–15:00 UTC.
        var first = windows.First(x => x.FactoryId == site.Chelal);
        Assert.Equal(3, first.StartsAt.Hour);
        Assert.Equal(12, (first.EndsAt - first.StartsAt).TotalHours);
        Assert.Equal(24m, first.CapacityHours); // 12 h × two technicians
        Assert.Equal(MaintenanceWindowService.StandardHandback.Count, Ok(await Controller(db, site.Admin).Get(first.Id, CancellationToken.None)).HandbackItems.Count);

        // Calling again adds nothing.
        Assert.Equal(windows.Count, Ok(await Controller(db, site.Admin).List(null, 0, CancellationToken.None)).Count);
    }

    [Fact]
    public async Task Approved_line_stop_jobs_queue_into_the_next_window_and_unfinished_ones_carry_over_then_go_to_the_overhaul_list()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var jobs = FactoryWorkOrderTests.Controller(db, site.Admin);
        var belt = await NewJobAsync(db, site, "Replace dryer belts", lineStop: true);
        var bearing = await NewJobAsync(db, site, "Change fan bearing", lineStop: true);
        var running = await NewJobAsync(db, site, "Paint guard rails", lineStop: false);
        var windows = Controller(db, site.Admin);

        var first = Ok(await windows.List(site.Chelal, 0, CancellationToken.None)).First();
        var detail = Ok(await windows.Get(first.Id, CancellationToken.None));
        Assert.Equal(new[] { belt, bearing }.Order(), detail.Jobs.Select(x => x.WorkOrderId).Order());
        Assert.All(detail.Jobs, x => Assert.True(x.AddedAutomatically));
        Assert.DoesNotContain(detail.Jobs, x => x.WorkOrderId == running);

        // The planner adds one by hand; it then needs the line stopped.
        Ok(await windows.AddJob(first.Id, new WindowJobRequest(running), CancellationToken.None));
        Assert.True((await db.WorkOrders.FindAsync(running))!.NeedsLineStop);

        await Assert.ThrowsAsync<BusinessRuleException>(() => windows.HandBack(first.Id, new HandbackRequest(null, null), CancellationToken.None));
        Ok(await windows.Pack(first.Id, CancellationToken.None));
        var pack = Ok(await windows.GetPack(first.Id, CancellationToken.None));
        Assert.Equal(3, pack.Jobs.Count);
        Assert.All(pack.Jobs, x => Assert.Contains("lock out", x.Isolation));
        Ok(await windows.Start(first.Id, CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(() => windows.HandBack(first.Id, new HandbackRequest(null, null), CancellationToken.None));

        await FactoryWorkOrderTests.RunToCompletionAsync(jobs, belt, site);
        await jobs.Act(running, FactoryWorkOrderActions.Cancel, new FactoryActionRequest(Reason: "Not needed"), CancellationToken.None);
        await TickAllAsync(windows, first.Id);
        var handed = Ok(await windows.HandBack(first.Id, new HandbackRequest("Line running 17:40", null), CancellationToken.None));
        Assert.Equal(1, handed.Result.Done);
        Assert.Equal(1, handed.Result.CarriedOver);
        Assert.Equal(MaintenanceWindowStatuses.HandedBack, handed.Window.Window.Status);
        Assert.Equal(WindowJobOutcomes.Cancelled, handed.Window.Jobs.Single(x => x.WorkOrderId == running).Outcome);

        // The bearing job is in the next window, carried over once.
        var next = Ok(await windows.Get(handed.Result.NextWindowId!.Value, CancellationToken.None));
        Assert.Equal(1, Assert.Single(next.Jobs).CarryoverCount);

        // Two more carryovers: escalated, then moved to the overhaul list and out of the queue.
        for (var round = 0; round < 2; round++)
        {
            var window = Ok(await windows.List(site.Chelal, 0, CancellationToken.None)).First(x => x.Status == MaintenanceWindowStatuses.Planned);
            Ok(await windows.Start(window.Id, CancellationToken.None));
            await TickAllAsync(windows, window.Id);
            var result = Ok(await windows.HandBack(window.Id, new HandbackRequest(null, null), CancellationToken.None)).Result;
            Assert.Equal(round == 1 ? 1 : 0, result.ToOverhaul);
        }

        var item = await db.OverhaulItems.SingleAsync();
        Assert.Equal(bearing, item.WorkOrderId);
        Assert.Equal(OverhaulItemSources.Carryover, item.Source);
        Assert.Equal(site.Chelal, item.FactoryId);
        var upcoming = Ok(await windows.List(site.Chelal, 0, CancellationToken.None)).Where(x => x.Status == MaintenanceWindowStatuses.Planned);
        Assert.All(upcoming, x => Assert.Equal(0, x.Jobs));
    }

    [Fact]
    public async Task A_skipped_window_passes_its_jobs_on_without_counting_a_carryover_and_a_window_can_be_moved()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var job = await NewJobAsync(db, site, "Re-cut CTC segments", lineStop: true);
        var windows = Controller(db, site.Admin);
        var list = Ok(await windows.List(site.Chelal, 0, CancellationToken.None)).ToList();

        await Assert.ThrowsAsync<BusinessRuleException>(() => windows.Skip(list[0].Id, new WindowReasonRequest(""), CancellationToken.None));
        Ok(await windows.Skip(list[0].Id, new WindowReasonRequest("Peak crop: factory running 7 days"), CancellationToken.None));
        var second = Ok(await windows.Get(list[1].Id, CancellationToken.None));
        Assert.Equal(0, Assert.Single(second.Jobs, x => x.WorkOrderId == job).CarryoverCount);

        var moved = Ok(await windows.Update(list[1].Id, new UpdateWindowRequest(list[1].WindowDate.AddDays(1), "07:00", "15:00", 30, "Moved for a public holiday"), CancellationToken.None));
        Assert.Equal(list[1].WindowDate.AddDays(1), moved.Window.WindowDate);
        Assert.Equal(list[1].WindowDate, moved.Window.MovedFromDate);
        Assert.Equal(8, (moved.Window.EndsAt - moved.Window.StartsAt).TotalHours);
        Assert.Equal(30m, moved.Window.CapacityHours);
        await Assert.ThrowsAsync<BusinessRuleException>(() => windows.Update(list[1].Id, new UpdateWindowRequest(DateTime.UtcNow.Date.AddDays(-3), null, null, null, null), CancellationToken.None));

        // The moved window isn't recreated on its old date.
        var again = Ok(await windows.List(site.Chelal, 0, CancellationToken.None));
        Assert.DoesNotContain(again, x => x.WindowDate == list[1].WindowDate);
    }

    [Fact]
    public async Task The_weekly_routines_become_line_stop_pm_plans_that_land_in_the_window()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var fbd = new AssetCategory { TenantId = site.TenantId, Name = "Fluid bed dryer", Code = "TEA-FBD" };
        var fan = new AssetCategory { TenantId = site.TenantId, Name = "Withering fan", Code = "TEA-WFN" };
        db.AssetCategories.AddRange(fbd, fan);
        site.Dryer.AssetCategoryId = fbd.Id;
        db.Assets.AddRange(
            new Asset { TenantId = site.TenantId, AssetName = "Withering fan 1", AssetCode = "WF-01", OrgNodeId = site.Drying, AssetCategoryId = fan.Id },
            new Asset { TenantId = site.TenantId, AssetName = "Withering fan 2", AssetCode = "WF-02", OrgNodeId = site.Drying, AssetCategoryId = fan.Id });
        await db.SaveChangesAsync();

        var windows = Controller(db, site.Admin);
        Assert.Equal(4, Ok(await windows.InstallRoutines(site.Chelal, CancellationToken.None)).PlansAdded);
        Assert.Equal(0, Ok(await windows.InstallRoutines(site.Chelal, CancellationToken.None)).PlansAdded);
        var plans = await db.PreventiveMaintenancePlans.Where(x => x.OrgNodeId != null).ToListAsync();
        Assert.Contains(plans, x => x.Name == "Dryer and fluid bed cleaning – FBD-01" && !x.IsRoute);
        Assert.Contains(plans, x => x.Name == "Withering fan check" && x.IsRoute);
        Assert.All(plans, x => Assert.True(x.NeedsLineStop));

        var monday = NextLocal(DayOfWeek.Monday);
        Assert.All(plans, x => Assert.Equal(monday, x.NextPmDate));
        await Runner(db).RunAsync(monday.AddDays(-3).AddHours(8));
        var window = Ok(await windows.List(site.Chelal, 0, CancellationToken.None)).First();
        Assert.Equal(4, window.Jobs);
        Assert.True(window.PlannedHours > 0);
    }

    private static async Task TickAllAsync(MaintenanceWindowsController windows, Guid windowId)
    {
        foreach (var item in Ok(await windows.Get(windowId, CancellationToken.None)).HandbackItems)
        {
            await windows.HandbackItem(windowId, item.Id, new HandbackItemRequest(true, null), CancellationToken.None);
        }
    }

    private static async Task<Guid> NewJobAsync(AppDbContext db, FactoryWorkOrderTests.Seeded site, string title, bool lineStop)
    {
        var jobs = FactoryWorkOrderTests.Controller(db, site.Admin);
        var created = Assert.IsType<FactoryWorkOrderDetail>(Assert.IsType<CreatedAtActionResult>((await jobs.Create(
            new CreateFactoryWorkOrderRequest(title, WorkType: "Preventive", AssetId: site.Dryer.Id, NeedsLineStop: lineStop), CancellationToken.None)).Result).Value);
        await jobs.Act(created.Id, FactoryWorkOrderActions.Approve, new FactoryActionRequest(), CancellationToken.None);
        return created.Id;
    }

    internal static FactoryPmRunner Runner(AppDbContext db)
    {
        var context = new FactoryTenantContext(Guid.Empty, Guid.Empty, true);
        return new FactoryPmRunner(db, Pm(db, context), Windows(db), Microsoft.Extensions.Logging.Abstractions.NullLogger<FactoryPmRunner>.Instance);
    }

    internal static FactoryPmService Pm(AppDbContext db, FactoryTenantContext context)
    {
        var scope = FactoryTestSupport.Scope(db, context);
        var workOrders = new FactoryWorkOrderService(db, new DocumentNumberingService(db), FactoryTestSupport.Settings(db), new AssetLifecycleService(db, scope), scope, new FactorySlaService(db), new FactoryPmProgress(db), new FactoryJobGuards(db));
        return new FactoryPmService(db, workOrders, FactoryTestSupport.Settings(db));
    }

    internal static MaintenanceWindowService Windows(AppDbContext db) => new(db, FactoryTestSupport.Settings(db));

    internal static MaintenanceWindowsController Controller(AppDbContext db, FactoryTenantContext context) =>
        FactoryAssetRegisterTests.WithHttp(new MaintenanceWindowsController(db, context, FactoryTestSupport.Access(context), FactoryTestSupport.Scope(db, context),
            Windows(db), Pm(db, context), FactoryTestSupport.Settings(db)));

    private static T Ok<T>(ActionResult<T> result) => Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);
}
