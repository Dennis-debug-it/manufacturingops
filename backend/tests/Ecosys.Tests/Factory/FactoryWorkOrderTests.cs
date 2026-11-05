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

public sealed class FactoryWorkOrderTests
{
    [Fact]
    public async Task Job_GoesThroughTheFullLifecycle()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var jobs = Controller(db, site.Admin);

        var job = await CreateAsync(jobs, new CreateFactoryWorkOrderRequest("Dryer fan bearing noisy", AssetId: site.Dryer.Id, Priority: "Medium"));
        Assert.Equal(WorkOrderStatuses.Requested, job.Status);
        Assert.Null(job.Problem);
        Assert.Equal(site.Drying, job.OrgNodeId);
        Assert.Equal([FactoryWorkOrderActions.Approve, FactoryWorkOrderActions.Cancel, FactoryWorkOrderActions.Reject], job.AllowedActions.Order().ToArray());

        await Assert.ThrowsAsync<BusinessRuleException>(() => Act(jobs, job.Id, FactoryWorkOrderActions.Start));

        job = await Act(jobs, job.Id, FactoryWorkOrderActions.Approve);
        job = await Act(jobs, job.Id, FactoryWorkOrderActions.Plan, new FactoryActionRequest(ScheduledStartAt: DateTime.UtcNow.AddDays(1), EstimatedHours: 3));
        Assert.Equal(WorkOrderStatuses.Planned, job.Status);
        job = await Act(jobs, job.Id, FactoryWorkOrderActions.Assign, new FactoryActionRequest(TechnicianIds: [site.Fitter.Id]));
        Assert.Equal("Peter Fitter", Assert.Single(job.Team).Name);
        job = await Act(jobs, job.Id, FactoryWorkOrderActions.Start);

        job = await Act(jobs, job.Id, FactoryWorkOrderActions.Hold, new FactoryActionRequest(HoldReason: WorkOrderHoldReasons.AwaitingWindow, Reason: "Needs the line stopped"));
        Assert.Equal(WorkOrderStatuses.OnHold, job.Status);
        Assert.True(job.NeedsLineStop);
        job = await Act(jobs, job.Id, FactoryWorkOrderActions.Resume);
        Assert.Equal(WorkOrderStatuses.InProgress, job.Status);

        // A corrective job needs the problem found.
        await Assert.ThrowsAsync<BusinessRuleException>(() => Act(jobs, job.Id, FactoryWorkOrderActions.Complete, new FactoryActionRequest(WorkDone: "Replaced bearing")));
        job = await Act(jobs, job.Id, FactoryWorkOrderActions.Complete, new FactoryActionRequest(
            WorkDone: "Replaced drive-end bearing", ProblemCodeId: site.Code("NOI"), CauseCodeId: site.Code("2.4"), RemedyCodeId: site.Code("REP"),
            SignatureDataUrl: "data:image/png;base64,iVBORw0KGgo=", SignerName: "Peter Fitter"));
        Assert.Equal(WorkOrderStatuses.Completed, job.Status);
        Assert.Equal("NOI", job.Problem?.Code);
        Assert.Equal("Peter Fitter", job.Signature?.SignerName);

        await Assert.ThrowsAsync<BusinessRuleException>(() => Act(jobs, job.Id, FactoryWorkOrderActions.ReturnForRework));
        job = await Act(jobs, job.Id, FactoryWorkOrderActions.Verify, new FactoryActionRequest(Reason: "Running quietly"));
        job = await Act(jobs, job.Id, FactoryWorkOrderActions.Close);
        Assert.Equal(WorkOrderStatuses.Closed, job.Status);
        Assert.Equal(
            ["Requested", "Approved", "Planned", "Assigned", "In Progress", "On Hold", "In Progress", "Completed", "Verified", "Closed"],
            job.History.Where(x => x.Type is "Created" or "StatusChanged").Reverse().Select(x => x.ToStatus ?? string.Empty).ToArray());
    }

    [Fact]
    public async Task Breakdown_SkipsApproval_PutsTheAssetDown_AndBackInServiceWhenFixed()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var jobs = Controller(db, site.Admin);

        var first = await CreateAsync(jobs, new CreateFactoryWorkOrderRequest("Dryer tripped", AssetId: site.Dryer.Id, IsBreakdown: true, Priority: "Low"));
        var second = await CreateAsync(jobs, new CreateFactoryWorkOrderRequest("Dryer burner fault", AssetId: site.Dryer.Id, IsBreakdown: true, Priority: "Critical"));

        Assert.Equal(WorkOrderStatuses.Approved, first.Status);
        Assert.Equal("High", first.Priority); // a breakdown is never low priority
        Assert.NotNull(first.DowntimeStartedAt);
        Assert.Equal(AssetOperationalStatuses.Down, (await db.Assets.SingleAsync(x => x.Id == site.Dryer.Id)).OperationalStatus);

        await RunToCompletionAsync(jobs, first.Id, site);
        var done = await Get(jobs, first.Id);
        Assert.NotNull(done.DowntimeEndedAt);
        // The other breakdown still has it down.
        Assert.Equal(AssetOperationalStatuses.Down, (await db.Assets.SingleAsync(x => x.Id == site.Dryer.Id)).OperationalStatus);

        await Act(jobs, second.Id, FactoryWorkOrderActions.Cancel, new FactoryActionRequest(Reason: "Duplicate of the first report"));
        Assert.Equal(AssetOperationalStatuses.Operational, (await db.Assets.SingleAsync(x => x.Id == site.Dryer.Id)).OperationalStatus);
        Assert.Contains(await db.AssetLifecycleEvents.Where(x => x.AssetId == site.Dryer.Id).ToListAsync(),
            x => x.EventType == AssetLifecycleEventTypes.StatusChanged && x.ToValue == AssetOperationalStatuses.Down);

        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateAsync(jobs, new CreateFactoryWorkOrderRequest("Something stopped", OrgNodeId: site.Drying, IsBreakdown: true)));
    }

    [Fact]
    public async Task WithApprovalSwitchedOff_JobsStartApproved()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        (await new FactorySettingsService(db).GetOrCreateAsync(site.TenantId)).RequireWorkOrderApproval = false;
        await db.SaveChangesAsync();

        var job = await CreateAsync(Controller(db, site.Admin), new CreateFactoryWorkOrderRequest("Paint guard rails", OrgNodeId: site.Drying, WorkType: "Safety"));
        Assert.Equal(WorkOrderStatuses.Approved, job.Status);
        Assert.Null(job.AssetId);
    }

    [Fact]
    public async Task SameFaultOnSameAssetWithinTheWindow_IsFlaggedAsRework()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var jobs = Controller(db, site.Admin);

        var first = await CreateAsync(jobs, new CreateFactoryWorkOrderRequest("Fan vibrating", AssetId: site.Dryer.Id));
        await RunToCompletionAsync(jobs, first.Id, site, "VIB");
        var other = await CreateAsync(jobs, new CreateFactoryWorkOrderRequest("Fan overheating", AssetId: site.Dryer.Id));
        await RunToCompletionAsync(jobs, other.Id, site, "OHE");
        var again = await CreateAsync(jobs, new CreateFactoryWorkOrderRequest("Fan vibrating again", AssetId: site.Dryer.Id));
        await RunToCompletionAsync(jobs, again.Id, site, "VIB");

        var result = await Get(jobs, again.Id);
        Assert.Equal(first.Id, result.ReworkOf?.Id);
        Assert.Contains(result.History, x => x.Type == "Rework");
        Assert.Null((await Get(jobs, other.Id)).ReworkOf);

        // Outside the window it is a new fault.
        (await db.WorkOrders.SingleAsync(x => x.Id == first.Id)).CompletedAt = DateTime.UtcNow.AddDays(-40);
        (await db.WorkOrders.SingleAsync(x => x.Id == again.Id)).CompletedAt = DateTime.UtcNow.AddDays(-35);
        await db.SaveChangesAsync();
        var later = await CreateAsync(jobs, new CreateFactoryWorkOrderRequest("Vibration", AssetId: site.Dryer.Id));
        await RunToCompletionAsync(jobs, later.Id, site, "VIB");
        Assert.Null((await Get(jobs, later.Id)).ReworkOf);
    }

    [Fact]
    public async Task Technician_WorksOnlyTheirOwnJobs_AndCannotApprove()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var adminJobs = Controller(db, site.Admin);
        var mine = await CreateAsync(adminJobs, new CreateFactoryWorkOrderRequest("Mine", AssetId: site.Dryer.Id));
        var theirs = await CreateAsync(adminJobs, new CreateFactoryWorkOrderRequest("Theirs", AssetId: site.Dryer.Id));
        foreach (var id in new[] { mine.Id, theirs.Id })
        {
            await Act(adminJobs, id, FactoryWorkOrderActions.Approve);
        }

        await Act(adminJobs, mine.Id, FactoryWorkOrderActions.Assign, new FactoryActionRequest(TechnicianIds: [site.Fitter.Id]));
        await Act(adminJobs, theirs.Id, FactoryWorkOrderActions.Assign, new FactoryActionRequest(TechnicianIds: [site.Electrician.Id]));

        var techJobs = Controller(db, new FactoryTenantContext(site.TenantId, site.FitterUser.Id, false, PermissionNames.CanCompleteWorkOrders));
        await Act(techJobs, mine.Id, FactoryWorkOrderActions.Start);
        await Assert.ThrowsAsync<ForbiddenException>(() => Act(techJobs, theirs.Id, FactoryWorkOrderActions.Start));
        await Assert.ThrowsAsync<ForbiddenException>(() => Act(techJobs, mine.Id, FactoryWorkOrderActions.Cancel, new FactoryActionRequest(Reason: "x")));

        var listed = Assert.IsType<OkObjectResult>((await techJobs.List(null, null, null, null, null, null, null, true, null, null, null, CancellationToken.None)).Result);
        Assert.Equal(mine.Id, Assert.Single(Assert.IsType<Ecosys.Api.Common.PagedResult<FactoryWorkOrderSummary>>(listed.Value).Items).Id);

        Ok(await techJobs.Labour(mine.Id, new LabourEntryRequest(90, Notes: "Stripped and refitted"), CancellationToken.None));
        Ok(await techJobs.Handover(mine.Id, new HandoverNoteRequest("Bearing housing worn; watch it next shift."), CancellationToken.None));
        var detail = await Get(techJobs, mine.Id);
        Assert.Equal(90, Assert.Single(detail.Labour).Minutes);
        Assert.Equal("Peter Fitter", detail.Labour.Single().TechnicianName);
        Assert.Contains("Bearing housing", Assert.Single(detail.Handover).Message);
    }

    [Fact]
    public async Task ChildJobs_InheritTheAssetAndLocation()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var jobs = Controller(db, site.Admin);
        var parent = await CreateAsync(jobs, new CreateFactoryWorkOrderRequest("Dryer burner rebuild", AssetId: site.Dryer.Id, WorkType: "Modification"));

        var child = Assert.IsType<FactoryWorkOrderDetail>(Assert.IsType<CreatedAtActionResult>(
            (await jobs.Split(parent.Id, new CreateChildWorkOrderRequest("Electrical rewiring", Priority: "High"), CancellationToken.None)).Result).Value);
        Assert.Equal(parent.Id, child.Parent?.Id);
        Assert.Equal(site.Dryer.Id, child.AssetId);
        Assert.Equal("Modification", child.WorkType);
        Assert.Single((await Get(jobs, parent.Id)).Children);

        await RunToCompletionAsync(jobs, parent.Id, site);
        await Assert.ThrowsAsync<BusinessRuleException>(() => jobs.Split(parent.Id, new CreateChildWorkOrderRequest("Too late"), CancellationToken.None));
    }

    [Fact]
    public async Task ScopedUser_CannotSeeOrRaiseJobsInAnotherFactory()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var other = new OrgNode { TenantId = site.TenantId, Level = OrgNodeLevels.Factory, Name = "Momul", Code = "MML" };
        db.OrgNodes.Add(other);
        await db.SaveChangesAsync();
        var theirs = await CreateAsync(Controller(db, site.Admin), new CreateFactoryWorkOrderRequest("Their job", OrgNodeId: other.Id));

        var planner = FactoryTestSupport.NewUser(site.TenantId, "planner@chelal.test");
        db.Users.Add(planner);
        db.UserOrgNodeAssignments.Add(new UserOrgNodeAssignment { TenantId = site.TenantId, UserId = planner.Id, OrgNodeId = site.Chelal });
        await db.SaveChangesAsync();
        var jobs = Controller(db, new FactoryTenantContext(site.TenantId, planner.Id, false, PermissionNames.CanAssignWorkOrders, PermissionNames.CanViewWorkOrders));

        await Assert.ThrowsAsync<NotFoundException>(() => jobs.Get(theirs.Id, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => CreateAsync(jobs, new CreateFactoryWorkOrderRequest("Sneaky", OrgNodeId: other.Id)));
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    internal static async Task RunToCompletionAsync(FactoryWorkOrdersController jobs, Guid id, Seeded site, string problem = "BRD")
    {
        var job = await Get(jobs, id);
        if (job.Status == WorkOrderStatuses.Requested) await Act(jobs, id, FactoryWorkOrderActions.Approve);
        await Act(jobs, id, FactoryWorkOrderActions.Assign, new FactoryActionRequest(TechnicianIds: [site.Fitter.Id]));
        await Act(jobs, id, FactoryWorkOrderActions.Start);
        await Act(jobs, id, FactoryWorkOrderActions.Complete, new FactoryActionRequest(WorkDone: "Fixed", ProblemCodeId: site.Code(problem)));
    }

    internal sealed record Seeded(Guid TenantId, FactoryTenantContext Admin, Guid Chelal, Guid Drying, Asset Dryer, Technician Fitter, User FitterUser, Technician Electrician, IReadOnlyDictionary<string, Guid> Codes)
    {
        public Guid Code(string code) => Codes[code];
    }

    internal static async Task<Seeded> SeedAsync(AppDbContext db)
    {
        var (tenant, admin) = await FactoryTestSupport.SeedTenantAsync(db);
        var chelal = new OrgNode { TenantId = tenant.Id, Level = OrgNodeLevels.Factory, Name = "Chelal", Code = "CHL" };
        var drying = new OrgNode { TenantId = tenant.Id, ParentId = chelal.Id, Level = OrgNodeLevels.Section, Name = "Drying", Code = "CHL-DRY" };
        var dryer = new Asset { TenantId = tenant.Id, AssetName = "Fluid bed dryer", AssetCode = "FBD-01", OrgNodeId = drying.Id, Criticality = "A" };
        var fitterUser = FactoryTestSupport.NewUser(tenant.Id, "fitter@chelal.test", AppRoles.Technician);
        var fitter = new Technician { TenantId = tenant.Id, UserId = fitterUser.Id, FullName = "Peter Fitter", Email = "fitter@chelal.test", IsActive = true };
        var electrician = new Technician { TenantId = tenant.Id, FullName = "Grace Sparks", Email = "sparks@chelal.test", IsActive = true };
        db.OrgNodes.AddRange(chelal, drying);
        db.Assets.Add(dryer);
        db.Users.Add(fitterUser);
        db.Technicians.AddRange(fitter, electrician);
        await db.SaveChangesAsync();
        await new FailureCodeService(db).EnsureSeededAsync(tenant.Id);
        // OTH is both a problem and a remedy code; the tests don't use it.
        var codes = (await db.FailureCodes.Where(x => x.TenantId == tenant.Id).ToListAsync()).GroupBy(x => x.Code).ToDictionary(x => x.Key, x => x.First().Id);
        return new Seeded(tenant.Id, new FactoryTenantContext(tenant.Id, admin.Id), chelal.Id, drying.Id, dryer, fitter, fitterUser, electrician, codes);
    }

    private static async Task<FactoryWorkOrderDetail> CreateAsync(FactoryWorkOrdersController controller, CreateFactoryWorkOrderRequest request) =>
        Assert.IsType<FactoryWorkOrderDetail>(Assert.IsType<CreatedAtActionResult>((await controller.Create(request, CancellationToken.None)).Result).Value);

    private static async Task<FactoryWorkOrderDetail> Act(FactoryWorkOrdersController controller, Guid id, string action, FactoryActionRequest? request = null) =>
        Ok(await controller.Act(id, action, request ?? new FactoryActionRequest(), CancellationToken.None));

    private static async Task<FactoryWorkOrderDetail> Get(FactoryWorkOrdersController controller, Guid id) =>
        Ok(await controller.Get(id, CancellationToken.None));

    private static T Ok<T>(ActionResult<T> result) => Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    internal static FactoryWorkOrdersController Controller(AppDbContext db, FactoryTenantContext context, INotificationDispatcher? dispatcher = null)
    {
        var scope = FactoryTestSupport.Scope(db, context);
        var service = new FactoryWorkOrderService(db, new DocumentNumberingService(db), FactoryTestSupport.Settings(db), new AssetLifecycleService(db, scope), scope, new FactorySlaService(db), new FactoryPmProgress(db), new FactoryJobGuards(db));
        return FactoryAssetRegisterTests.WithHttp(new FactoryWorkOrdersController(db, context, FactoryTestSupport.Access(context), scope, service,
            new FailureCodeService(db), new FactorySlaService(db), FactoryTestSupport.Audit(db), new SparesService(db, dispatcher)));
    }
}
