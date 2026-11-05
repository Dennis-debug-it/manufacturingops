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

public sealed class OverhaulTests
{
    [Fact]
    public async Task Work_is_listed_through_the_season_scoped_into_an_overhaul_done_as_jobs_and_recommissioned()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var overhaul = Controller(db, site.Admin);
        var jobs = FactoryWorkOrderTests.Controller(db, site.Admin);

        // A finding on the dryer, and an open line-stop job deferred from the weekly window.
        var finding = Ok(await overhaul.AddItem(new AddOverhaulItemRequest(null, site.Dryer.Id, "Re-tube heat exchanger", "Tubes thin at the inlet end", OverhaulItemSources.Finding, "High", 40, 350_000), CancellationToken.None));
        Assert.Equal(site.Chelal, finding.FactoryId);
        var job = Assert.IsType<FactoryWorkOrderDetail>(Assert.IsType<CreatedAtActionResult>((await jobs.Create(
            new CreateFactoryWorkOrderRequest("Replace CTC segment set", WorkType: "Preventive", AssetId: site.Dryer.Id, NeedsLineStop: true), CancellationToken.None)).Result).Value);
        await jobs.Act(job.Id, FactoryWorkOrderActions.Approve, new FactoryActionRequest(), CancellationToken.None);
        await Assert.ThrowsAsync<BusinessRuleException>(() => overhaul.DeferJob(new DeferJobToOverhaulRequest(job.Id, " "), CancellationToken.None));
        var deferred = Ok(await overhaul.DeferJob(new DeferJobToOverhaulRequest(job.Id, "Needs a three-day stop"), CancellationToken.None));
        Assert.Equal(OverhaulItemSources.Deferred, deferred.Source);
        await Assert.ThrowsAsync<BusinessRuleException>(() => overhaul.DeferJob(new DeferJobToOverhaulRequest(job.Id, "again"), CancellationToken.None));

        // A deferred job stays out of the weekly window.
        var windows = new MaintenanceWindowService(db, FactoryTestSupport.Settings(db));
        await windows.EnsureUpcomingAsync(site.TenantId, DateTime.UtcNow);
        Assert.False(await db.MaintenanceWindowJobs.AnyAsync(x => x.WorkOrderId == job.Id));

        var plan = Ok(await overhaul.CreatePlan(new SaveOverhaulPlanRequest(site.Chelal, "2027 low-crop overhaul", new DateTime(2027, 7, 1), new DateTime(2027, 7, 21), 500_000, null), CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(() => overhaul.StartPlan(plan.Id, CancellationToken.None));
        foreach (var item in new[] { finding, deferred })
        {
            Ok(await overhaul.UpdateItem(item.Id, new UpdateOverhaulItemRequest(item.Title, item.Description, item.Priority, item.EstimatedHours, item.EstimatedCost, null,
                "Tubes from Davis & Shirtliff", item == finding ? "Boiler Services Ltd" : null, null, null, plan.Id), CancellationToken.None));
        }

        var scoped = Ok(await overhaul.Plan(plan.Id, CancellationToken.None));
        Assert.Equal(2, scoped.Plan.Items);
        Assert.Equal(350_000m, scoped.Plan.EstimatedCost);
        Assert.All(scoped.Items, x => Assert.Equal(OverhaulItemStatuses.Scoped, x.Status));

        // Starting raises a job for the finding; the deferred job keeps its own.
        var started = Ok(await overhaul.StartPlan(plan.Id, CancellationToken.None));
        Assert.Equal(OverhaulPlanStatuses.InProgress, started.Plan.Status);
        var findingJob = started.Items.Single(x => x.Id == finding.Id);
        Assert.StartsWith("WO-", findingJob.WorkOrderNumber);
        Assert.Equal(WorkOrderStatuses.Approved, findingJob.WorkOrderStatus);
        Assert.Equal(job.Id, started.Items.Single(x => x.Id == deferred.Id).WorkOrderId);

        await Assert.ThrowsAsync<BusinessRuleException>(() => overhaul.CompletePlan(plan.Id, CancellationToken.None));
        await FactoryWorkOrderTests.RunToCompletionAsync(jobs, findingJob.WorkOrderId!.Value, site);
        Ok(await overhaul.SetItemStatus(deferred.Id, new OverhaulItemStatusRequest(OverhaulItemStatuses.Dropped, "Segments re-cut in a window instead"), CancellationToken.None));

        var progress = Ok(await overhaul.Plan(plan.Id, CancellationToken.None));
        Assert.Equal(OverhaulItemStatuses.Done, progress.Items.Single(x => x.Id == finding.Id).Status);
        Assert.Equal(100, progress.Plan.ProgressPercent);
        Assert.Equal(1, progress.Plan.AwaitingRecommissioning);
        await Assert.ThrowsAsync<BusinessRuleException>(() => overhaul.CompletePlan(plan.Id, CancellationToken.None));

        Ok(await overhaul.Recommission(finding.Id, new RecommissionRequest("Hydro-tested to 12 bar, trial run 2 h"), CancellationToken.None));
        var done = Ok(await overhaul.CompletePlan(plan.Id, CancellationToken.None));
        Assert.Equal(OverhaulPlanStatuses.Completed, done.Plan.Status);
        Assert.NotNull(done.Plan.ActualEnd);
    }

    [Fact]
    public async Task Overhaul_items_stay_inside_the_users_factories()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var other = new OrgNode { TenantId = site.TenantId, Level = OrgNodeLevels.Factory, Name = "Momul", Code = "MML" };
        db.OrgNodes.Add(other);
        await db.SaveChangesAsync();
        var item = Ok(await Controller(db, site.Admin).AddItem(new AddOverhaulItemRequest(other.Id, null, "Re-roof withering shed", null, null, null, null, null), CancellationToken.None));

        var supervisor = FactoryTestSupport.NewUser(site.TenantId, "sup@chelal.test", AppRoles.Manager);
        db.Users.Add(supervisor);
        db.UserOrgNodeAssignments.Add(new UserOrgNodeAssignment { TenantId = site.TenantId, UserId = supervisor.Id, OrgNodeId = site.Drying });
        await db.SaveChangesAsync();
        var mine = Controller(db, new FactoryTenantContext(site.TenantId, supervisor.Id, false, PermissionNames.CanViewWorkOrders, PermissionNames.CanAssignWorkOrders));
        Assert.Empty(Ok(await mine.Items(null, null, false, CancellationToken.None)));
        await Assert.ThrowsAsync<NotFoundException>(() => mine.UpdateItem(item.Id, new UpdateOverhaulItemRequest("x", null, null, null, null, null, null, null, null, null, null), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => mine.AddItem(new AddOverhaulItemRequest(other.Id, null, "Sneaky", null, null, null, null, null), CancellationToken.None));
    }

    private static OverhaulController Controller(AppDbContext db, FactoryTenantContext context)
    {
        var scope = FactoryTestSupport.Scope(db, context);
        var workOrders = new FactoryWorkOrderService(db, new DocumentNumberingService(db), FactoryTestSupport.Settings(db), new AssetLifecycleService(db, scope), scope, new FactorySlaService(db), new FactoryPmProgress(db), new FactoryJobGuards(db));
        return FactoryAssetRegisterTests.WithHttp(new OverhaulController(db, context, FactoryTestSupport.Access(context), scope, new MaintenanceWindowService(db, FactoryTestSupport.Settings(db)), workOrders));
    }

    private static T Ok<T>(ActionResult<T> result) => Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);
}
