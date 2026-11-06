using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Ecosys.Tests.Factory;

/// <summary>
/// The costs, KPI, compliance framework and data protection queries against real PostgreSQL, on a
/// database built by the migrations. Needs a local server (ECOSYS_TEST_DB_CONNECTION, default
/// localhost:5432 postgres/postgres).
/// </summary>
public sealed class CostsPostgresTests
{
    [Fact]
    public async Task Postgres_CostsKpisEvidenceAndDataProtection_AllTranslate()
    {
        await WithPostgresAsync(async connection =>
        {
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var context = new FactoryTenantContext(tenantId, userId);
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options, context);
            static DateTime Plain(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Unspecified);
            var now = DateTime.UtcNow;
            db.Tenants.Add(new Tenant { Id = tenantId, Name = "Kericho", Slug = $"k-{Guid.NewGuid():N}"[..12], CompanyName = "Kericho Tea", Email = "ops@k.test", Country = "Kenya", IsActive = true });
            db.Users.Add(new User { Id = userId, TenantId = tenantId, FullName = "Admin", Email = $"a-{Guid.NewGuid():N}@k.test", Role = AppRoles.Admin, PasswordHash = string.Empty, IsActive = true });
            var factory = new OrgNode { TenantId = tenantId, Level = OrgNodeLevels.Factory, Name = "Chelal", Code = "CHL" };
            var section = new OrgNode { TenantId = tenantId, ParentId = factory.Id, Level = OrgNodeLevels.Section, Name = "Drying", Code = "CHL-DRY" };
            var asset = new Asset { TenantId = tenantId, AssetName = "Dryer", AssetCode = "FBD-01", OrgNodeId = section.Id, Criticality = "A", LostProductionPerHour = 300 };
            var tech = new Technician { TenantId = tenantId, FullName = "Peter", Email = "p@k.test", IsActive = false, HomeOrgNodeId = section.Id };
            db.AddRange(factory, section, asset, tech);
            await db.SaveChangesAsync();

            var job = new WorkOrder { TenantId = tenantId, WorkOrderNumber = "WO-1", Title = "Fan", WorkType = FactoryWorkTypes.Corrective, Priority = "High", Status = WorkOrderStatuses.Completed,
                AssetId = asset.Id, OrgNodeId = section.Id, IsBreakdown = true, DowntimeStartedAt = now.AddHours(-5), DowntimeEndedAt = now.AddHours(-2), WindowCarryoverCount = 1, EstimatedLabourHours = 2 };
            db.WorkOrders.Add(job);
            var production = new Meter { TenantId = tenantId, AssetId = asset.Id, Name = "Made tea", MeterType = MeterTypes.Production, Unit = "kg MT" };
            db.Meters.Add(production);
            db.MeterReadings.Add(new MeterReading { TenantId = tenantId, MeterId = production.Id, AssetId = asset.Id, Value = 5000, Delta = 5000, ReadAt = now.AddHours(-1) });
            db.WorkOrderTimeLogs.Add(new WorkOrderTimeLog { TenantId = tenantId, WorkOrderId = job.Id, TechnicianId = tech.Id, StartedAt = now.AddHours(-4), DurationMinutes = 60, LabourCost = 500 });
            var contractor = new Contractor { TenantId = tenantId, Name = "Rift" };
            var invoice = new ContractorInvoice { TenantId = tenantId, ContractorId = contractor.Id, InvoiceNumber = "I1", InvoiceDate = now.Date, Amount = 1160, VatAmount = 160, Status = ContractorInvoiceStatuses.Matched };
            invoice.Lines.Add(new ContractorInvoiceLine { TenantId = tenantId, WorkOrderId = job.Id, Description = "x", Amount = 1160 });
            db.AddRange(contractor, invoice);
            var window = new MaintenanceWindow { TenantId = tenantId, FactoryId = factory.Id, WindowDate = now.Date.AddDays(-1), StartsAt = now.AddDays(-1), EndsAt = now.AddDays(-1).AddHours(8), Status = MaintenanceWindowStatuses.HandedBack };
            window.Jobs.Add(new MaintenanceWindowJob { TenantId = tenantId, WorkOrderId = job.Id, Outcome = WindowJobOutcomes.CarriedOver });
            db.MaintenanceWindows.Add(window);
            var plan = new OverhaulPlan { TenantId = tenantId, FactoryId = factory.Id, Name = "2027", PlannedStart = now.AddDays(-10), PlannedEnd = now.AddDays(10), Budget = 1000 };
            plan.Items.Add(new OverhaulItem { TenantId = tenantId, FactoryId = factory.Id, Title = "Reline", Status = OverhaulItemStatuses.Done, ActualCost = 300 });
            db.OverhaulPlans.Add(plan);
            db.TrainingRecords.Add(new TrainingRecord { TenantId = tenantId, TechnicianId = tech.Id, PersonName = "Peter", Course = "Heights", CompletedOn = now.Date.AddDays(-2) });
            db.TrainingRecords.Add(new TrainingRecord { TenantId = tenantId, PersonName = "Old contractor", IsContractor = true, Course = "Induction", CompletedOn = now.Date.AddYears(-10) });
            db.PpeIssues.Add(new PpeIssue { TenantId = tenantId, TechnicianId = tech.Id, PersonName = "Peter", Item = "Gloves", IssuedOn = now.Date.AddDays(-2) });
            db.Incidents.Add(new Incident { TenantId = tenantId, IncidentNumber = "INC-1", OccurredAt = now.AddDays(-1), OrgNodeId = section.Id, Title = "Cut", Description = "Cut", PersonsInvolved = "Peter", ReportedByName = "A" });
            db.ComplianceItems.Add(new ComplianceItem { TenantId = tenantId, OrgNodeId = section.Id, AssetId = asset.Id, Name = "Boiler exam", DueDate = now.AddDays(-3) });
            db.ComplianceItems.Add(new ComplianceItem { TenantId = tenantId, OrgNodeId = section.Id, Name = "Scale", Kind = ComplianceKinds.Calibration, DueDate = now.AddDays(20) });
            await db.SaveChangesAsync();

            var scope = FactoryTestSupport.Scope(db, context);
            var settings = FactoryTestSupport.Settings(db);
            var kpiService = new FactoryKpiService(db, new SparesService(db), MaintenanceWindowTests.Pm(db, context), new PeopleService(db), settings);
            var costs = FactoryAssetRegisterTests.WithHttp(new FactoryCostsController(db, context, FactoryTestSupport.Access(context), scope, kpiService, settings));
            var reports = new ScheduledReportService(db, null!, NullLogger<ScheduledReportService>.Instance, kpiService);
            var kpis = FactoryAssetRegisterTests.WithHttp(new FactoryKpisController(db, context, FactoryTestSupport.Access(context), scope, kpiService, reports));
            var frameworkService = new ComplianceFrameworkService(db, kpiService, MaintenanceWindowTests.Pm(db, context));
            var workOrders = new FactoryWorkOrderService(db, new DocumentNumberingService(db), settings, new AssetLifecycleService(db, scope), scope, new FactorySlaService(db), new FactoryPmProgress(db), new FactoryJobGuards(db));
            var framework = FactoryAssetRegisterTests.WithHttp(new ComplianceFrameworkController(db, context, FactoryTestSupport.Access(context), scope, frameworkService, workOrders));
            var dataProtection = FactoryAssetRegisterTests.WithHttp(new DataProtectionController(db, context, FactoryTestSupport.Access(context), new DataProtectionService(db, settings), FactoryTestSupport.Audit(db)));

            // Costs and budgets.
            Ok(await costs.SaveSettings(new SaveCostSettingsRequest(250, 16, "3,4,5,10,11,12", 7, "Kenya"), default));
            var centres = Ok(await costs.CreateCostCentre(new SaveCostCentreRequest(factory.Id, "CHL-DRY", "Drying", section.Id), default));
            Ok(await costs.SaveBudgets(new SaveBudgetsRequest(factory.Id, now.Year, [new SaveBudgetLine(null, "Labour", 100_000), new SaveBudgetLine(centres.Single().Id, "Parts", 50_000)]), default));
            Assert.Equal(2, Ok(await costs.Budgets(now.Year, default)).Count);
            foreach (var groupBy in new[] { "factory", "section", "costcentre", "asset", "job", "month" })
            {
                Assert.NotEmpty(Ok(await costs.Summary(null, Plain(now.AddDays(-30)), Plain(now), groupBy, default)).Rows);
            }

            Assert.Equal(1500, Ok(await costs.Job(job.Id, default)).Total);

            // KPIs, comparison, exports, pilot and the scheduled report.
            var k = Ok(await kpis.Get(factory.Id, Plain(now.AddDays(-30)), Plain(now), default));
            Assert.Equal((1, 1, 1500m), (k.Reliability.Breakdowns, k.Window.CarriedOver, k.Costs.Total));
            Assert.Single(Ok(await kpis.Compare(Plain(now.AddDays(-30)), Plain(now), default)));
            Assert.IsType<FileContentResult>(await kpis.Export(null, Plain(now.AddDays(-30)), Plain(now), "xlsx", default));
            Assert.IsType<FileContentResult>(await kpis.Export(null, Plain(now.AddDays(-30)), Plain(now), "pdf", default));
            Assert.IsType<OkObjectResult>(await kpis.Pilot(null, Plain(now.AddDays(-90)), Plain(now.AddDays(-61)), Plain(now.AddDays(-30)), Plain(now), null, default));
            Ok(await kpis.Schedule(new SaveKpiScheduleRequest(null, "gm@k.test", factory.Id), default));
            Assert.NotEmpty(await reports.GenerateReportAsync(await db.ScheduledReports.SingleAsync()));

            // Compliance framework: every evidence source, the pack, findings, audits and registers.
            foreach (var template in frameworkService.Templates) Ok(await framework.Install(new InstallStandardRequest(template.Key), default));
            Ok(await framework.RecordWaste(new SaveWasteRequest(factory.Id, Plain(now), "Scrap metal", 500, HandlerName: "Scrap Co"), default));
            Ok(await framework.RecordFoodSafety(new SaveFoodSafetyRequest(factory.Id, "Cleaning", asset.Id, Method: "Brush"), default));
            Ok(await framework.RecordFoodSafety(new SaveFoodSafetyRequest(factory.Id, "MetalDetector", asset.Id, FerrousRejected: true, NonFerrousRejected: false, StainlessRejected: true), default));
            Ok(await framework.AddGlassItem(new SaveGlassItemRequest(factory.Id, "Sorting", "Light cover", "Hard plastic", 4), default));
            Ok(await framework.RecordFoodSafety(new SaveFoodSafetyRequest(factory.Id, "GlassCheck", Area: "Sorting", ItemsChecked: 4, ItemsDamaged: 0), default));
            var audit = Ok(await framework.PlanAudit(new SaveAuditRequest(factory.Id, "Surveillance audit", Plain(now.AddDays(10))), default));
            var nc = Ok(await framework.RaiseNonConformity(new SaveNonConformityRequest(factory.Id, "Guard missing", "Major", AuditEventId: audit.Id, DueDate: Plain(now.AddDays(-1))), default));
            Ok(await framework.NonConformityAction(nc.Id, "job", null, default));
            foreach (var source in EvidenceSources.Labels.Keys)
            {
                await frameworkService.CollectAsync(tenantId, factory.Id, source, now.Date.AddYears(-1), now.Date.AddDays(1));
            }

            var standards = Ok(await framework.Standards(default));
            foreach (var standard in standards)
            {
                Ok(await framework.Evidence(standard.Id, factory.Id, null, null, default));
            }

            Assert.IsType<FileContentResult>(await framework.EvidencePackPdf(standards.First().Id, factory.Id, Plain(now.AddDays(-30)), Plain(now), default));
            Assert.NotEmpty(Ok(await framework.NonConformities("open", null, default)));
            Assert.NotEmpty(Ok(await framework.Audits(null, null, default)));
            Assert.NotEmpty(Ok(await framework.Waste(null, null, null, default)));
            Assert.NotEmpty(Ok(await framework.FoodSafety(null, null, null, null, default)));
            await frameworkService.RunAlertsAsync(tenantId, now);
            Assert.NotNull((await db.NonConformities.AsNoTracking().SingleAsync()).LastOverdueAlertAt);
            Assert.Equal(30, (await db.AuditEvents.AsNoTracking().SingleAsync()).LastReminderDays);

            // Data protection.
            Ok(await dataProtection.RecordConsent(new SaveConsentRequest("SMS job alerts", TechnicianId: tech.Id, GivenAt: Plain(now)), default));
            Assert.NotEmpty(Ok(await dataProtection.People(default)));
            Ok(await dataProtection.Export(null, tech.Id, default));
            Ok(await dataProtection.Erase(new EraseRequest(null, tech.Id, "ERASE"), default));
            Assert.Equal(1, Ok(await dataProtection.ApplyRetention(default)).TrainingAnonymised);
        });
    }

    private static T Ok<T>(ActionResult<T> result) => Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private static async Task WithPostgresAsync(Func<string, Task> body)
    {
        var server = Environment.GetEnvironmentVariable("ECOSYS_TEST_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=ecosys_test;Username=postgres;Password=postgres";
        var databaseName = $"ecosys_costs_{Guid.NewGuid():N}";
        var database = new NpgsqlConnectionStringBuilder(server) { Database = databaseName, Pooling = false }.ConnectionString;
        var admin = new NpgsqlConnectionStringBuilder(server) { Database = "postgres", Pooling = false }.ConnectionString;
        await ExecuteAdminAsync(admin, $"CREATE DATABASE \"{databaseName}\"");
        try
        {
            await using (var setup = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(database).Options))
            {
                await setup.Database.MigrateAsync();
            }

            await body(database);
        }
        finally
        {
            await ExecuteAdminAsync(admin, $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)");
        }
    }

    private static async Task ExecuteAdminAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
