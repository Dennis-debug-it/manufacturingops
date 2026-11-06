using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ecosys.Tests.Factory;

public sealed class CostsAndEvidenceTests
{
    private static readonly DateTime Today = DateTime.UtcNow.Date;
    private static readonly DateTime MonthStart = new(Today.Year, Today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task A_jobs_labour_parts_contractor_and_downtime_roll_up_by_section_cost_centre_and_against_budget()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var settings = await FactoryTestSupport.Settings(db).GetOrCreateAsync(site.TenantId);
        settings.ProductionUnitValue = 250;
        db.Assets.Single(x => x.Id == site.Dryer.Id).LostProductionPerHour = 400;
        await db.SaveChangesAsync();

        var job = await JobAsync(db, site, "Dryer fan bearing failed", breakdown: true, downtimeHours: 3);
        db.WorkOrderTimeLogs.Add(new WorkOrderTimeLog { TenantId = site.TenantId, WorkOrderId = job, StartedAt = At(1), DurationMinutes = 120, LabourCost = 800 });
        db.WorkOrderMaterialUsages.Add(new WorkOrderMaterialUsage { TenantId = site.TenantId, WorkOrderId = job, MaterialItemId = await PartAsync(db, site), QuantityUsed = 2, UnitCost = 450, UsedAt = At(1) });
        var contractor = new Contractor { TenantId = site.TenantId, Name = "Kericho Electricals" };
        var invoice = new ContractorInvoice { TenantId = site.TenantId, ContractorId = contractor.Id, InvoiceNumber = "INV-1", InvoiceDate = At(2), Amount = 11600, VatAmount = 1600, Status = ContractorInvoiceStatuses.Approved };
        invoice.Lines.Add(new ContractorInvoiceLine { TenantId = site.TenantId, WorkOrderId = job, Description = "Rewind", Amount = 11600 });
        db.AddRange(contractor, invoice);
        db.CostCentres.Add(new CostCentre { TenantId = site.TenantId, FactoryId = site.Chelal, Code = "CHL-DRY", Name = "Drying", OrgNodeId = site.Drying });
        db.MaintenanceBudgets.Add(new MaintenanceBudget { TenantId = site.TenantId, FactoryId = site.Chelal, Year = Today.Year, Category = CostCategories.Parts, Amount = 365_000 });
        await db.SaveChangesAsync();

        var costs = Costs(db, site.Admin);
        var bySection = Ok(await costs.Summary(null, MonthStart, Today, "section", CancellationToken.None));
        var drying = Assert.Single(bySection.Rows);
        Assert.Equal(("Drying", 800m, 900m, 10_000m, 11_700m), (drying.Name, drying.Labour, drying.Parts, drying.Contractor, drying.Total));
        Assert.Equal(3m, drying.DowntimeHours);
        Assert.Equal((1200m, 300_000m), (drying.ProductionLost, drying.DowntimeValue!.Value));
        Assert.Equal("Drying", Assert.Single(Ok(await costs.Summary(null, MonthStart, Today, "costcentre", CancellationToken.None)).Rows).Name);

        var jobCosts = Ok(await costs.Job(job, CancellationToken.None));
        Assert.Equal((11_700m, 3m, "Drying"), (jobCosts.Total, jobCosts.DowntimeHours, jobCosts.CostCentre!));

        // Budget: 365,000 a year for parts; the period's share is pro rata by day.
        var kpis = await Kpis(db, site.Admin).KpisAsync(site.TenantId, null, "All", MonthStart, Today.AddDays(1));
        var line = Assert.Single(kpis.Costs.Budget.Lines);
        Assert.Equal(Math.Round(365_000m * ((decimal)(Today.AddDays(1) - MonthStart).TotalDays) / (DateTime.IsLeapYear(Today.Year) ? 366 : 365), 2), line.BudgetForPeriod);
        Assert.Equal(900m, line.Actual);
    }

    [Fact]
    public async Task Production_and_energy_meters_give_cost_and_energy_per_kg_made_tea_and_downtime_splits_by_season()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var settings = await FactoryTestSupport.Settings(db).GetOrCreateAsync(site.TenantId);
        settings.PeakSeasonMonths = Today.Month.ToString();
        db.Assets.Single(x => x.Id == site.Dryer.Id).LostProductionPerHour = 100;
        var production = new Meter { TenantId = site.TenantId, AssetId = site.Dryer.Id, Name = "Made tea", MeterType = MeterTypes.Production, Unit = "kg MT", IsCumulative = true };
        var power = new Meter { TenantId = site.TenantId, AssetId = site.Dryer.Id, Name = "Power", MeterType = MeterTypes.Energy, Unit = "kWh", IsCumulative = true };
        var wood = new Meter { TenantId = site.TenantId, AssetId = site.Dryer.Id, Name = "Firewood", MeterType = MeterTypes.Fuel, Unit = "m³", IsCumulative = false };
        db.Meters.AddRange(production, power, wood);
        db.MeterReadings.AddRange(
            new MeterReading { TenantId = site.TenantId, MeterId = production.Id, AssetId = site.Dryer.Id, Value = 10_000, Delta = 10_000, ReadAt = At(0) },
            new MeterReading { TenantId = site.TenantId, MeterId = production.Id, AssetId = site.Dryer.Id, Value = 30_000, Delta = 20_000, ReadAt = At(1) },
            new MeterReading { TenantId = site.TenantId, MeterId = power.Id, AssetId = site.Dryer.Id, Value = 15_000, Delta = 15_000, ReadAt = At(1) },
            new MeterReading { TenantId = site.TenantId, MeterId = wood.Id, AssetId = site.Dryer.Id, Value = 12, ReadAt = At(0) },
            new MeterReading { TenantId = site.TenantId, MeterId = wood.Id, AssetId = site.Dryer.Id, Value = 18, ReadAt = At(1) });
        await db.SaveChangesAsync();
        var job = await JobAsync(db, site, "Fan down", breakdown: true, downtimeHours: 2);
        db.WorkOrderTimeLogs.Add(new WorkOrderTimeLog { TenantId = site.TenantId, WorkOrderId = job, StartedAt = At(1), DurationMinutes = 60, LabourCost = 1_500 });
        await db.SaveChangesAsync();

        var k = await Kpis(db, site.Admin).KpisAsync(site.TenantId, null, "All", MonthStart, Today.AddDays(1));
        Assert.Equal((30_000m, 15_000m, 30m, "m³"), (k.Energy.Production, k.Energy.EnergyKwh, k.Energy.Fuel, k.Energy.FuelUnit!));
        Assert.Equal((0.5m, 0.001m), (k.Energy.KwhPerUnit!.Value, k.Energy.FuelPerUnit!.Value));
        Assert.Equal(0.05m, k.Costs.CostPerUnit);
        Assert.Equal((2m, 0m, 200m), (k.Downtime.PeakHours, k.Downtime.LowHours, k.Downtime.PeakProductionLost));
        Assert.Equal((1, 2m), (k.Reliability.Breakdowns, k.Reliability.MttrHours!.Value));
    }

    [Fact]
    public async Task The_board_report_exports_to_excel_and_pdf_and_can_be_emailed_on_a_schedule()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        await JobAsync(db, site, "Belt snapped", breakdown: true, downtimeHours: 1);
        var controller = KpisController(db, site.Admin);

        var xlsx = Assert.IsType<FileContentResult>(await controller.Export(site.Chelal, MonthStart, Today, "xlsx", CancellationToken.None));
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", xlsx.ContentType);
        Assert.Equal((byte)'P', xlsx.FileContents[0]);
        var pdf = Assert.IsType<FileContentResult>(await controller.Export(site.Chelal, MonthStart, Today, "pdf", CancellationToken.None));
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf.FileContents, 0, 4));

        var schedules = Ok(await controller.Schedule(new SaveKpiScheduleRequest("Chelal board report", "gm@kericho.test", site.Chelal), CancellationToken.None));
        var schedule = Assert.Single(schedules);
        Assert.Equal(("Chelal", "Monthly"), (schedule.OrgNodeName!, schedule.Frequency));
        var report = await db.ScheduledReports.SingleAsync();
        var bytes = await Reports(db, site.Admin).GenerateReportAsync(report);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));

        var compare = Ok(await controller.Compare(MonthStart, Today, CancellationToken.None));
        Assert.Equal(1, Assert.Single(compare).Breakdowns);
    }

    [Fact]
    public async Task The_pilot_report_compares_baseline_and_pilot_per_30_days()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        // Baseline: 60 days with 4 breakdowns of 3 h; pilot: 30 days with 1 of 2 h.
        for (var i = 0; i < 4; i++) await JobAsync(db, site, $"Old breakdown {i}", breakdown: true, downtimeHours: 3, startDaysAgo: 100 - i * 10);
        await JobAsync(db, site, "New breakdown", breakdown: true, downtimeHours: 2, startDaysAgo: 5);
        var result = await KpisController(db, site.Admin).Pilot(null, Today.AddDays(-120), Today.AddDays(-61), Today.AddDays(-29), Today, null, CancellationToken.None);
        var report = Assert.IsType<PilotReport>(Assert.IsType<OkObjectResult>(result).Value);
        var downtime = report.Measures.Single(x => x.Measure == "Downtime per 30 days");
        Assert.Equal((6m, 2m, -66.7m), (downtime.Baseline!.Value, downtime.Current!.Value, downtime.ChangePercent!.Value));
        Assert.True(downtime.LowerIsBetter);
        Assert.IsType<FileContentResult>(await KpisController(db, site.Admin).Pilot(null, Today.AddDays(-120), Today.AddDays(-61), Today.AddDays(-29), Today, "pdf", CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(() => KpisController(db, site.Admin).Pilot(null, Today.AddDays(-40), Today.AddDays(-10), Today.AddDays(-29), Today, null, CancellationToken.None));
    }

    [Fact]
    public async Task An_evidence_pack_shows_each_requirements_records_and_its_gaps()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var framework = Framework(db, site.Admin);
        var standards = Ok(await framework.Install(new InstallStandardRequest("rainforest-alliance"), CancellationToken.None));
        var ra = Assert.Single(standards);
        Assert.Equal(9, ra.Requirements.Count);
        // Installing again doesn't duplicate it.
        Assert.Single(Ok(await framework.Install(new InstallStandardRequest("rainforest-alliance"), CancellationToken.None)));

        Ok(await framework.RecordWaste(new SaveWasteRequest(site.Chelal, Today, "Used oil", 200, "litres", HandlerName: "Green Oil Recyclers", LicenceNumber: "NEMA/WM/123"), CancellationToken.None));
        db.Incidents.Add(new Incident { TenantId = site.TenantId, IncidentNumber = "INC-1", OccurredAt = At(1), OrgNodeId = site.Drying, Title = "Hand caught", Description = "Glove caught in belt guard", ReportedByName = "Sup" });
        await db.SaveChangesAsync();

        var pack = Ok(await framework.Evidence(ra.Id, site.Chelal, null, null, CancellationToken.None));
        var waste = pack.Requirements.Single(x => x.Clause == "RA-ENV-2");
        Assert.True(waste.HasEvidence);
        Assert.Contains("Green Oil Recyclers", waste.Sections.Single().Rows.Single());
        Assert.True(pack.Requirements.Single(x => x.Clause == "RA-SAFE-4").HasEvidence);
        Assert.False(pack.Requirements.Single(x => x.Clause == "RA-ENV-3").HasEvidence);
        Assert.Equal(2, pack.Covered);

        var file = Assert.IsType<FileContentResult>(await framework.EvidencePackPdf(ra.Id, site.Chelal, null, null, CancellationToken.None));
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(file.FileContents, 0, 4));
    }

    [Fact]
    public async Task A_failed_metal_detector_test_raises_a_corrective_job_and_findings_close_only_with_evidence()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var framework = Framework(db, site.Admin);
        await Assert.ThrowsAsync<BusinessRuleException>(() => framework.RecordFoodSafety(new SaveFoodSafetyRequest(site.Chelal, "MetalDetector", site.Dryer.Id, FerrousRejected: true), CancellationToken.None));
        var failed = Ok(await framework.RecordFoodSafety(new SaveFoodSafetyRequest(site.Chelal, "MetalDetector", site.Dryer.Id, FerrousRejected: true, NonFerrousRejected: true, StainlessRejected: false), CancellationToken.None));
        Assert.False(failed.Passed);
        var job = await db.WorkOrders.SingleAsync(x => x.Id == failed.CorrectiveWorkOrderId);
        Assert.Equal((FactoryWorkTypes.Safety, "High"), (job.WorkType, job.Priority));
        var cleaned = Ok(await framework.RecordFoodSafety(new SaveFoodSafetyRequest(site.Chelal, "Cleaning", Area: "Sorting room", Method: "Dry brush and food-grade sanitiser"), CancellationToken.None));
        Assert.True(cleaned.Passed);
        Assert.Null(cleaned.CorrectiveWorkOrderId);

        var nc = Ok(await framework.RaiseNonConformity(new SaveNonConformityRequest(site.Chelal, "No SDS for boiler water treatment", "Minor", DueDate: Today.AddDays(-1)), CancellationToken.None));
        Assert.Equal("NC-00001", nc.Number);
        await Assert.ThrowsAsync<BusinessRuleException>(() => framework.NonConformityAction(nc.Id, "close", null, CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(() => framework.NonConformityAction(nc.Id, "resolve", new NonConformityActionRequest("Obtained SDS from supplier"), CancellationToken.None));

        // Overdue: escalated once a week.
        var dispatcher = new RecordingDispatcher();
        var service = FrameworkService(db, site.Admin, dispatcher);
        Assert.Equal(1, await service.RunAlertsAsync(site.TenantId, DateTime.UtcNow));
        Assert.Equal(0, await service.RunAlertsAsync(site.TenantId, DateTime.UtcNow.AddDays(1)));
        Assert.Single(dispatcher.Sent, x => x.Key == ComplianceFrameworkService.NonConformityOverdueKey);

        Ok(await framework.NonConformityAction(nc.Id, "resolve", new NonConformityActionRequest("Obtained SDS from supplier", "SDS uploaded on the part, rev. 2025"), CancellationToken.None));
        var closed = Ok(await framework.NonConformityAction(nc.Id, "close", null, CancellationToken.None));
        Assert.Equal(NonConformityStatuses.Closed, closed.Status);

        // Audit reminders at 30 and 7 days.
        Ok(await framework.PlanAudit(new SaveAuditRequest(site.Chelal, "Surveillance audit", Today.AddDays(20), Auditor: "SCS Global"), CancellationToken.None));
        Assert.Equal(1, await service.RunAlertsAsync(site.TenantId, DateTime.UtcNow));
        Assert.Equal(0, await service.RunAlertsAsync(site.TenantId, DateTime.UtcNow));
        Assert.Equal(1, await service.RunAlertsAsync(site.TenantId, DateTime.UtcNow.AddDays(14)));
    }

    [Fact]
    public async Task A_former_technician_can_be_exported_and_erased_and_old_contractor_records_are_anonymised()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        db.TrainingRecords.AddRange(
            new TrainingRecord { TenantId = site.TenantId, TechnicianId = site.Electrician.Id, PersonName = "Grace Sparks", Course = "Working at heights", CompletedOn = Today.AddMonths(-3) },
            new TrainingRecord { TenantId = site.TenantId, PersonName = "Visiting welder", IsContractor = true, Course = "Induction", CompletedOn = Today.AddYears(-9) });
        db.Incidents.Add(new Incident { TenantId = site.TenantId, IncidentNumber = "INC-9", OccurredAt = Today.AddMonths(-2), OrgNodeId = site.Drying, Title = "Shock", Description = "Minor shock", PersonsInvolved = "Grace Sparks", ReportedByName = "Sup" });
        await db.SaveChangesAsync();
        var controller = DataProtection(db, site.Admin);

        var export = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(Assert.IsType<OkObjectResult>((await controller.Export(null, site.Electrician.Id, CancellationToken.None)).Result).Value);
        Assert.NotNull(export["technician"]);
        Assert.Single(Assert.IsAssignableFrom<System.Collections.IEnumerable>(export["training"]!).Cast<object>());

        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Erase(new EraseRequest(null, site.Electrician.Id, "ERASE"), CancellationToken.None));
        db.Technicians.Single(x => x.Id == site.Electrician.Id).IsActive = false;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Erase(new EraseRequest(null, site.Electrician.Id, "yes"), CancellationToken.None));
        var erased = Assert.IsType<EraseResult>(Assert.IsType<OkObjectResult>((await controller.Erase(new EraseRequest(null, site.Electrician.Id, "ERASE"), CancellationToken.None)).Result).Value);
        Assert.StartsWith("Erased person", erased.Placeholder);
        Assert.Equal(erased.Placeholder, (await db.Technicians.SingleAsync(x => x.Id == site.Electrician.Id)).FullName);
        Assert.Equal(erased.Placeholder, (await db.TrainingRecords.SingleAsync(x => x.TechnicianId == site.Electrician.Id)).PersonName);
        Assert.Equal(erased.Placeholder, (await db.Incidents.SingleAsync()).PersonsInvolved);

        var retention = Assert.IsType<RetentionResult>(Assert.IsType<OkObjectResult>((await controller.ApplyRetention(CancellationToken.None)).Result).Value);
        Assert.Equal(1, retention.TrainingAnonymised);
        Assert.Equal("Former contractor", (await db.TrainingRecords.SingleAsync(x => x.TechnicianId == null)).PersonName);
    }

    /* ── Helpers ─────────────────────────────────────────────────────────── */

    private static DateTime At(int day) => MonthStart.AddDays(Math.Min(day, (Today - MonthStart).Days)).AddHours(8);

    private static async Task<Guid> JobAsync(AppDbContext db, FactoryWorkOrderTests.Seeded site, string title, bool breakdown = false, decimal downtimeHours = 0, int? startDaysAgo = null)
    {
        var start = startDaysAgo is { } d ? Today.AddDays(-d).AddHours(8) : At(0);
        var job = new WorkOrder
        {
            TenantId = site.TenantId, WorkOrderNumber = $"WO-{Guid.NewGuid():N}"[..10], Title = title, WorkType = FactoryWorkTypes.Corrective, Priority = "High",
            Status = WorkOrderStatuses.Completed, AssetId = site.Dryer.Id, OrgNodeId = site.Drying, IsBreakdown = breakdown, CreatedAt = start,
            DowntimeStartedAt = downtimeHours > 0 ? start : null, DowntimeEndedAt = downtimeHours > 0 ? start.AddHours((double)downtimeHours) : null,
        };
        db.WorkOrders.Add(job);
        await db.SaveChangesAsync();
        return job.Id;
    }

    private static async Task<Guid> PartAsync(AppDbContext db, FactoryWorkOrderTests.Seeded site)
    {
        var part = new MaterialItem { TenantId = site.TenantId, ItemCode = "BRG", ItemName = "Bearing", UnitOfMeasure = "each" };
        db.MaterialItems.Add(part);
        await db.SaveChangesAsync();
        return part.Id;
    }

    private static FactoryKpiService Kpis(AppDbContext db, FactoryTenantContext context) =>
        new(db, new SparesService(db), MaintenanceWindowTests.Pm(db, context), new PeopleService(db), FactoryTestSupport.Settings(db));

    private static FactoryCostsController Costs(AppDbContext db, FactoryTenantContext context) =>
        FactoryAssetRegisterTests.WithHttp(new FactoryCostsController(db, context, FactoryTestSupport.Access(context), FactoryTestSupport.Scope(db, context), Kpis(db, context), FactoryTestSupport.Settings(db)));

    private static ScheduledReportService Reports(AppDbContext db, FactoryTenantContext context) =>
        new(db, null!, NullLogger<ScheduledReportService>.Instance, Kpis(db, context));

    private static FactoryKpisController KpisController(AppDbContext db, FactoryTenantContext context) =>
        FactoryAssetRegisterTests.WithHttp(new FactoryKpisController(db, context, FactoryTestSupport.Access(context), FactoryTestSupport.Scope(db, context), Kpis(db, context), Reports(db, context)));

    private static ComplianceFrameworkService FrameworkService(AppDbContext db, FactoryTenantContext context, INotificationDispatcher? dispatcher = null) =>
        new(db, Kpis(db, context), MaintenanceWindowTests.Pm(db, context), dispatcher);

    private static ComplianceFrameworkController Framework(AppDbContext db, FactoryTenantContext context)
    {
        var scope = FactoryTestSupport.Scope(db, context);
        var workOrders = new FactoryWorkOrderService(db, new DocumentNumberingService(db), FactoryTestSupport.Settings(db), new AssetLifecycleService(db, scope), scope, new FactorySlaService(db), new FactoryPmProgress(db), new FactoryJobGuards(db));
        return FactoryAssetRegisterTests.WithHttp(new ComplianceFrameworkController(db, context, FactoryTestSupport.Access(context), scope, FrameworkService(db, context), workOrders));
    }

    private static DataProtectionController DataProtection(AppDbContext db, FactoryTenantContext context) =>
        FactoryAssetRegisterTests.WithHttp(new DataProtectionController(db, context, FactoryTestSupport.Access(context), new DataProtectionService(db, FactoryTestSupport.Settings(db)), FactoryTestSupport.Audit(db)));

    private static T Ok<T>(ActionResult<T> result) => Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

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
