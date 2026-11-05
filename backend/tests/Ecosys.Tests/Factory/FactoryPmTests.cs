using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests.Factory;

public sealed class FactoryPmTests
{
    private static readonly DateTime Today = DateTime.UtcNow.Date;

    [Fact]
    public void Fixed_plans_keep_their_cadence_and_floating_plans_restart_from_the_work()
    {
        var plan = new PreventiveMaintenancePlan { FrequencyUnit = PmFrequencyUnits.Months, FrequencyInterval = 1, ScheduleMode = PmScheduleModes.Fixed };
        var due = new DateTime(2026, 3, 1);
        Assert.Equal(new DateTime(2026, 4, 1), PmSchedule.NextDue(plan, due, new DateTime(2026, 3, 10)));
        // Done very late: the missed cycles are passed over rather than raised at once.
        Assert.Equal(new DateTime(2026, 6, 1), PmSchedule.NextDue(plan, due, new DateTime(2026, 5, 15)));

        plan.ScheduleMode = PmScheduleModes.Floating;
        Assert.Equal(new DateTime(2026, 4, 10), PmSchedule.NextDue(plan, due, new DateTime(2026, 3, 10)));
        // A skip keeps the cadence even for a floating plan.
        Assert.Equal(new DateTime(2026, 4, 1), PmSchedule.NextDue(plan, due, new DateTime(2026, 3, 1), skipped: true));

        var meter = new PreventiveMaintenancePlan { MeterInterval = 500, ScheduleMode = PmScheduleModes.Floating };
        Assert.Equal(1_560m, PmSchedule.NextMeterDue(meter, 1_000, 1_060));
        meter.ScheduleMode = PmScheduleModes.Fixed;
        Assert.Equal(1_500m, PmSchedule.NextMeterDue(meter, 1_000, 1_060));
    }

    [Fact]
    public async Task A_due_calendar_plan_raises_one_approved_job_with_its_checklist_and_completing_it_moves_the_plan_on()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var template = await TemplateAsync(db, site.TenantId, ("Grease fan bearings", "checkbox"), ("Bearing temperature", "number"));
        var pm = Pm(db, site.Admin);
        var plan = await CreateAsync(pm, Request(site.Dryer.Id, template.Id) with { NextDueDate = Today.AddDays(2), NeedsLineStop = true });

        var service = Service(db, site.Admin);
        Assert.Equal(0, await service.GenerateDueAsync(site.TenantId, Today.AddDays(-5)));
        Assert.Equal(1, await service.GenerateDueAsync(site.TenantId, Today));
        Assert.Equal(0, await service.GenerateDueAsync(site.TenantId, Today));

        var job = await db.WorkOrders.SingleAsync(x => x.PreventiveMaintenancePlanId == plan.Plan.Id);
        Assert.Equal(WorkOrderStatuses.Approved, job.Status);
        Assert.Equal(FactoryWorkTypes.Preventive, job.WorkType);
        Assert.True(job.NeedsLineStop);
        Assert.Equal(Today.AddDays(2), job.DueDate);

        var jobs = FactoryWorkOrderTests.Controller(db, site.Admin);
        await jobs.Act(job.Id, FactoryWorkOrderActions.Assign, new FactoryActionRequest(TechnicianIds: [site.Fitter.Id]), CancellationToken.None);
        await jobs.Act(job.Id, FactoryWorkOrderActions.Start, new FactoryActionRequest(), CancellationToken.None);
        var detail = Ok(await jobs.Get(job.Id, CancellationToken.None));
        Assert.Equal(2, detail.Steps.Count);

        // Required steps come first.
        await Assert.ThrowsAsync<BusinessRuleException>(() => jobs.Act(job.Id, FactoryWorkOrderActions.Complete, new FactoryActionRequest(WorkDone: "Greased"), CancellationToken.None));
        var reading = detail.Steps.Single(x => x.Type == "number");
        await Assert.ThrowsAsync<BusinessRuleException>(() => jobs.Step(job.Id, reading.Id, new StepUpdateRequest(true), CancellationToken.None));
        foreach (var step in detail.Steps)
        {
            await jobs.Step(job.Id, step.Id, new StepUpdateRequest(true, Value: step.Type == "number" ? "61" : null), CancellationToken.None);
        }

        Assert.Equal("61", Ok(await jobs.Get(job.Id, CancellationToken.None)).Steps.Single(x => x.Type == "number").Value);
        await jobs.Act(job.Id, FactoryWorkOrderActions.Complete, new FactoryActionRequest(WorkDone: "Greased"), CancellationToken.None);

        var after = Ok(await pm.Get(plan.Plan.Id, CancellationToken.None));
        // Floating: a month from when it was done (today), not from the due date.
        Assert.Equal(Today.AddMonths(1), after.Plan.NextDueDate);
        var done = Assert.Single(after.Events, x => x.Kind == PmPlanEventKinds.Completed);
        Assert.True(done.OnTime);
        Assert.Null(after.Plan.OpenJobNumber);
    }

    [Fact]
    public async Task Cancelling_a_pm_job_skips_that_pm_and_counts_against_compliance()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var pm = Pm(db, site.Admin);
        var plan = await CreateAsync(pm, Request(site.Dryer.Id, null) with { NextDueDate = Today, ScheduleMode = PmScheduleModes.Floating });
        await Service(db, site.Admin).GenerateDueAsync(site.TenantId, Today);
        var job = await db.WorkOrders.SingleAsync(x => x.PreventiveMaintenancePlanId == plan.Plan.Id);

        await FactoryWorkOrderTests.Controller(db, site.Admin).Act(job.Id, FactoryWorkOrderActions.Cancel, new FactoryActionRequest(Reason: "Line running for a rush order"), CancellationToken.None);

        var after = Ok(await pm.Get(plan.Plan.Id, CancellationToken.None));
        Assert.Equal(Today.AddMonths(1), after.Plan.NextDueDate);
        Assert.Equal("Line running for a rush order", Assert.Single(after.Events, x => x.Kind == PmPlanEventKinds.Skipped).Reason);

        var compliance = Ok(await pm.Compliance(null, 30, CancellationToken.None));
        Assert.Equal(1, compliance.Due);
        Assert.Equal(1, compliance.Skipped);
        Assert.Equal(0m, compliance.Percent);
    }

    [Fact]
    public async Task A_meter_plan_falls_due_on_the_reading_and_restarts_from_the_reading_it_was_done_at()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var meter = new Meter { TenantId = site.TenantId, AssetId = site.Dryer.Id, Name = "Run hours", MeterType = MeterTypes.RunHours, Unit = "h", IsCumulative = true, CurrentValue = 900 };
        db.Meters.Add(meter);
        await db.SaveChangesAsync();
        var pm = Pm(db, site.Admin);
        var plan = await CreateAsync(pm, Request(site.Dryer.Id, null) with { TriggerType = PmTriggerTypes.Meter, MeterId = meter.Id, MeterInterval = 500, NextMeterDue = 1_000, NextDueDate = null });

        var service = Service(db, site.Admin);
        Assert.Equal(0, await service.GenerateDueAsync(site.TenantId, Today));
        meter.CurrentValue = 980; // inside the 5% buffer (25 h)
        await db.SaveChangesAsync();
        Assert.Equal(1, await service.GenerateDueAsync(site.TenantId, Today));

        var job = await db.WorkOrders.SingleAsync(x => x.PreventiveMaintenancePlanId == plan.Plan.Id);
        Assert.Contains("Due at 1000 h", job.Description);
        meter.CurrentValue = 1_020;
        await db.SaveChangesAsync();
        await FactoryWorkOrderTests.RunToCompletionAsync(FactoryWorkOrderTests.Controller(db, site.Admin), job.Id, site);

        var after = Ok(await pm.Get(plan.Plan.Id, CancellationToken.None));
        Assert.Equal(1_520m, after.Plan.NextMeterDue);
        Assert.True(Assert.Single(after.Events, x => x.Kind == PmPlanEventKinds.Completed).OnTime);
    }

    [Fact]
    public async Task Skipping_and_deferring_need_a_reason_and_putting_an_A_class_asset_off_to_low_crop_is_flagged()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var settings = await FactoryTestSupport.Settings(db).GetOrCreateAsync(site.TenantId);
        settings.LowCropStartMonth = 7;
        await db.SaveChangesAsync();
        var pm = Pm(db, site.Admin);
        var plan = await CreateAsync(pm, Request(site.Dryer.Id, null) with { NextDueDate = Today.AddDays(10), FrequencyUnit = "Weeks", FrequencyInterval = 2, ScheduleMode = PmScheduleModes.Fixed });

        await Assert.ThrowsAsync<BusinessRuleException>(() => pm.Skip(plan.Plan.Id, new SkipPmRequest(" "), CancellationToken.None));
        var skipped = Ok(await pm.Skip(plan.Plan.Id, new SkipPmRequest("Asset replaced last week"), CancellationToken.None));
        Assert.Equal(Today.AddDays(24), skipped.Plan.NextDueDate);

        var deferred = Ok(await pm.Defer(plan.Plan.Id, new DeferPmRequest("Peak crop, no line stops", null, null, UntilLowCrop: true), CancellationToken.None));
        Assert.Equal(7, deferred.Plan.NextDueDate!.Value.Month);
        Assert.True(deferred.Plan.DeferRisk);
        Assert.Equal("Peak crop, no line stops", deferred.Plan.DeferReason);

        // With a job open, skipping is done by cancelling the job.
        await pm.Generate(plan.Plan.Id, CancellationToken.None);
        await Assert.ThrowsAsync<BusinessRuleException>(() => pm.Skip(plan.Plan.Id, new SkipPmRequest("x"), CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(() => pm.Generate(plan.Plan.Id, CancellationToken.None));
    }

    [Fact]
    public async Task A_route_plan_raises_one_job_with_the_checklist_repeated_for_each_asset_in_order()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var fan = new Asset { TenantId = site.TenantId, AssetName = "Withering fan 1", AssetCode = "WF-01", OrgNodeId = site.Drying };
        db.Assets.Add(fan);
        await db.SaveChangesAsync();
        var template = await TemplateAsync(db, site.TenantId, ("Grease nipple", "checkbox"));
        var pm = Pm(db, site.Admin);
        var plan = await CreateAsync(pm, Request(site.Dryer.Id, template.Id) with { Name = "Drying lubrication round", NextDueDate = Today, RouteAssetIds = [fan.Id, site.Dryer.Id] });

        var job = await db.WorkOrders.FindAsync(Ok(await pm.Generate(plan.Plan.Id, CancellationToken.None)).WorkOrderId);
        Assert.Null(job!.AssetId);
        Assert.Contains("WF-01 → FBD-01", job.Description);
        var sections = await db.WorkOrderChecklistItems.Where(x => x.WorkOrderId == job.Id).OrderBy(x => x.SortOrder).Select(x => x.SectionName).ToListAsync();
        Assert.Equal(["WF-01 · Withering fan 1", "FBD-01 · Fluid bed dryer"], sections);

        var listed = Ok(await pm.Plans(null, fan.Id, false, null, CancellationToken.None));
        Assert.Equal(job.WorkOrderNumber, Assert.Single(listed).OpenJobNumber);
    }

    [Fact]
    public async Task The_forecast_adds_up_pm_hours_per_week_against_technician_capacity()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var pm = Pm(db, site.Admin);
        await CreateAsync(pm, Request(site.Dryer.Id, null) with { NextDueDate = Today, FrequencyUnit = "Weeks", FrequencyInterval = 1, EstimatedHours = 3, NeedsLineStop = true });
        await CreateAsync(pm, Request(site.Dryer.Id, null) with { Name = "Monthly", NextDueDate = Today, EstimatedHours = 8 });

        var weeks = Ok(await pm.Forecast(null, 4, CancellationToken.None));
        Assert.Equal(4, weeks.Count);
        Assert.Equal(11m, weeks[0].Hours);
        Assert.Equal(3m, weeks[0].LineStopHours);
        Assert.All(weeks.Skip(1).Take(2), w => Assert.Equal(3m, w.Hours));
        // Two active technicians at 30 planned hours a week.
        Assert.Equal(60m, weeks[0].CapacityHours);
    }

    [Fact]
    public async Task Service_plans_are_left_alone()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        db.PreventiveMaintenancePlans.Add(new PreventiveMaintenancePlan { TenantId = site.TenantId, AssetId = site.Dryer.Id, Frequency = "Monthly", AutoSchedule = true, NextPmDate = Today.AddDays(-3) });
        await db.SaveChangesAsync();
        Assert.Equal(0, await Service(db, site.Admin).GenerateDueAsync(site.TenantId, Today));
        Assert.Empty(Ok(await Pm(db, site.Admin).Plans(null, null, true, null, CancellationToken.None)));
    }

    private static SavePmPlanRequest Request(Guid assetId, Guid? templateId) => new(
        "Dryer bearings", assetId, templateId, PmTriggerTypes.Calendar, PmFrequencyUnits.Months, 1, Today, null, null, null,
        PmScheduleModes.Floating, 10, 3, "Medium", 2, false, "Manufacturer", null, true);

    private static async Task<PmPlanDetail> CreateAsync(FactoryPmController pm, SavePmPlanRequest request) => Ok(await pm.Create(request, CancellationToken.None));

    private static async Task<PmTemplate> TemplateAsync(AppDbContext db, Guid tenantId, params (string Prompt, string Type)[] questions)
    {
        var template = new PmTemplate { TenantId = tenantId, Name = "Bearing PM", Category = "Factory", WorkType = "Preventive" };
        foreach (var (q, i) in questions.Select((q, i) => (q, i)))
        {
            template.Questions.Add(new PmTemplateQuestion { PmTemplateId = template.Id, Prompt = q.Prompt, ResponseType = q.Type, IsRequired = true, DisplayOrder = i, Unit = q.Type == "number" ? "°C" : null });
        }

        db.PmTemplates.Add(template);
        await db.SaveChangesAsync();
        return template;
    }

    private static FactoryPmService Service(AppDbContext db, FactoryTenantContext context)
    {
        var scope = FactoryTestSupport.Scope(db, context);
        var workOrders = new FactoryWorkOrderService(db, new DocumentNumberingService(db), FactoryTestSupport.Settings(db), new AssetLifecycleService(db, scope), scope, new FactorySlaService(db), new FactoryPmProgress(db), new FactoryJobGuards(db));
        return new FactoryPmService(db, workOrders, FactoryTestSupport.Settings(db));
    }

    private static FactoryPmController Pm(AppDbContext db, FactoryTenantContext context) =>
        FactoryAssetRegisterTests.WithHttp(new FactoryPmController(db, context, FactoryTestSupport.Access(context), FactoryTestSupport.Scope(db, context), Service(db, context)));

    private static T Ok<T>(ActionResult<T> result) => Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);
}
