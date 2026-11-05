using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests.Factory;

public sealed class SlaAndJobPlanTests
{
    [Fact]
    public async Task Targets_ComeFromPriorityAndTheAssetsCriticality()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var jobs = FactoryWorkOrderTests.Controller(db, site.Admin);

        // Asset criticality A, priority High: respond in 1 h, repair in 8 h.
        var onDryer = await CreateAsync(jobs, new CreateFactoryWorkOrderRequest("Dryer fault", AssetId: site.Dryer.Id, Priority: "High"));
        AssertHours(1, onDryer.ResponseDueAt, onDryer.CreatedAt);
        AssertHours(8, onDryer.RepairDueAt, onDryer.CreatedAt);
        Assert.Equal(onDryer.RepairDueAt, onDryer.DueDate);

        // No asset: treated as C. Medium / C: 24 h and 72 h.
        var general = await CreateAsync(jobs, new CreateFactoryWorkOrderRequest("Fix the door", OrgNodeId: site.Drying));
        AssertHours(72, general.RepairDueAt, general.CreatedAt);

        // Raising the priority tightens the targets, still from when the job was raised.
        var updated = Assert.IsType<FactoryWorkOrderDetail>(Assert.IsType<OkObjectResult>((await jobs.Update(general.Id,
            new UpdateFactoryWorkOrderRequest("Fix the door", null, "Corrective", "Critical", null, false, null), CancellationToken.None)).Result).Value);
        AssertHours(24, updated.RepairDueAt, updated.CreatedAt);
    }

    [Fact]
    public async Task Matrix_CanBeTuned_ButRepairCannotBeSoonerThanResponse()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var plans = Controller(db, site.Admin);

        var matrix = Ok(await plans.Matrix(CancellationToken.None));
        Assert.Equal(12, matrix.Count);
        Assert.Equal("Critical", matrix.First().Priority);

        var tuned = Ok(await plans.UpdateMatrix([new SlaMatrixCellResponse("critical", "a", 0.25m, 2)], CancellationToken.None));
        Assert.Equal(2m, tuned.Single(x => x.Priority == "Critical" && x.Criticality == "A").RepairHours);
        await Assert.ThrowsAsync<BusinessRuleException>(() => plans.UpdateMatrix([new SlaMatrixCellResponse("High", "B", 5, 2)], CancellationToken.None));
    }

    [Fact]
    public async Task JobFromAPlan_GetsItsStepsHoursAndLineStop_AndStepsCanBeTicked()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var dryerClass = new AssetCategory { TenantId = site.TenantId, Name = "Fluid bed dryer", Code = "TEA-FBD", Icon = "tool" };
        var otherClass = new AssetCategory { TenantId = site.TenantId, Name = "Boiler", Code = "TEA-BLR", Icon = "tool" };
        var bearing = new MaterialItem { TenantId = site.TenantId, ItemCode = "BRG-6312", ItemName = "Bearing 6312 2RS", UnitOfMeasure = "pcs" };
        db.AssetCategories.AddRange(dryerClass, otherClass);
        db.MaterialItems.Add(bearing);
        site.Dryer.AssetCategoryId = dryerClass.Id;
        await db.SaveChangesAsync();
        var plans = Controller(db, site.Admin);

        var plan = Ok(await plans.Create(new UpsertJobPlanRequest(
            "Replace dryer fan bearing", "Corrective", AssetCategoryId: dryerClass.Id, EstimatedHours: 4, NeedsLineStop: true,
            Steps: [new("Isolate and lock out the fan motor", 15), new("Remove coupling and old bearing", 60), new("Fit new bearing and align", 90)],
            Resources: [new("Tool", "Bearing puller"), new("Part", null, bearing.Id, 2)]), CancellationToken.None));
        Assert.Equal(3, plan.Steps.Count);
        Assert.Equal("Bearing 6312 2RS", plan.Resources.Single(x => x.Kind == "Part").Description);
        Ok(await plans.Create(new UpsertJobPlanRequest("Boiler tube cleaning", "Preventive", AssetCategoryId: otherClass.Id), CancellationToken.None));
        Ok(await plans.Create(new UpsertJobPlanRequest("Weld repair", "Corrective"), CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(() => plans.Create(new UpsertJobPlanRequest("weld REPAIR", "Corrective"), CancellationToken.None));

        var forDryer = Ok(await plans.List(dryerClass.Id, false, CancellationToken.None));
        Assert.Equal(["Replace dryer fan bearing", "Weld repair"], forDryer.Select(x => x.Name).ToArray());

        var jobs = FactoryWorkOrderTests.Controller(db, site.Admin);
        var job = await CreateAsync(jobs, new CreateFactoryWorkOrderRequest("Fan bearing failing", AssetId: site.Dryer.Id, JobPlanId: plan.Id));
        Assert.Equal(4m, job.EstimatedHours);
        Assert.True(job.NeedsLineStop);
        Assert.Equal("Replace dryer fan bearing", job.JobPlanName);
        Assert.Equal(["Isolate and lock out the fan motor", "Remove coupling and old bearing", "Fit new bearing and align"], job.Steps.Select(x => x.Instruction).ToArray());
        Assert.Equal(2, job.Resources.Count);

        var ticked = Assert.IsType<FactoryWorkOrderDetail>(Assert.IsType<OkObjectResult>((await jobs.Step(job.Id, job.Steps.First().Id, new StepUpdateRequest(true, "Locked out, tag 14"), CancellationToken.None)).Result).Value);
        Assert.True(ticked.Steps.First().Done);
        Assert.Equal("Locked out, tag 14", ticked.Steps.First().Remarks);

        // Editing the plan later doesn't change jobs already raised.
        Ok(await plans.Update(plan.Id, new UpsertJobPlanRequest("Replace dryer fan bearing", "Corrective", Steps: [new("Just one step")]), CancellationToken.None));
        Assert.Equal(3, Ok(await jobs.Get(job.Id, CancellationToken.None)).Steps.Count);
    }

    /// <summary>Deadlines are worked out a moment before the save stamps the creation time.</summary>
    private static void AssertHours(double hours, DateTime? due, DateTime created) =>
        Assert.InRange(((due ?? DateTime.MinValue) - created).TotalHours, hours - 0.01, hours + 0.01);

    private static async Task<FactoryWorkOrderDetail> CreateAsync(FactoryWorkOrdersController controller, CreateFactoryWorkOrderRequest request) =>
        Assert.IsType<FactoryWorkOrderDetail>(Assert.IsType<CreatedAtActionResult>((await controller.Create(request, CancellationToken.None)).Result).Value);

    private static T Ok<T>(ActionResult<T> result) => Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private static JobPlansController Controller(AppDbContext db, FactoryTenantContext context) =>
        FactoryAssetRegisterTests.WithHttp(new JobPlansController(db, context, FactoryTestSupport.Access(context), new FactorySlaService(db)));
}
