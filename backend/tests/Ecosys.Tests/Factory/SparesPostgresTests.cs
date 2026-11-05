using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ecosys.Tests.Factory;

/// <summary>
/// The stores and contractor queries against real PostgreSQL, on a database built by the
/// migrations. The in-memory provider accepts LINQ that PostgreSQL cannot translate; this catches
/// it. Needs a local server (ECOSYS_TEST_DB_CONNECTION, default localhost:5432 postgres/postgres).
/// </summary>
public sealed class SparesPostgresTests
{
    [Fact]
    public async Task Postgres_StoresContractorsAndWindowPack_AllTranslate()
    {
        await WithPostgresAsync(async connection =>
        {
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var context = new FactoryTenantContext(tenantId, userId);
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options, context);
            db.Tenants.Add(new Tenant { Id = tenantId, Name = "Kericho", Slug = $"k-{Guid.NewGuid():N}"[..12], CompanyName = "Kericho Tea", Email = "ops@k.test", Country = "Kenya", IsActive = true });
            db.Users.Add(new User { Id = userId, TenantId = tenantId, FullName = "Admin", Email = $"a-{Guid.NewGuid():N}@k.test", Role = AppRoles.Admin, PasswordHash = string.Empty, IsActive = true });
            var factory = new OrgNode { TenantId = tenantId, Level = OrgNodeLevels.Factory, Name = "Chelal", Code = "CHL" };
            var other = new OrgNode { TenantId = tenantId, Level = OrgNodeLevels.Factory, Name = "Kapset", Code = "KPS" };
            var asset = new Asset { TenantId = tenantId, AssetName = "Dryer", AssetCode = "FBD-01", OrgNodeId = factory.Id, Criticality = "A" };
            var supplier = new Supplier { TenantId = tenantId, SupplierName = "Atlas", LeadTimeDays = 10 };
            var technician = new Technician { TenantId = tenantId, FullName = "Peter", Email = "p@k.test", IsActive = true };
            db.AddRange(factory, other, asset, supplier, technician);
            await db.SaveChangesAsync();

            var scope = FactoryTestSupport.Scope(db, context);
            var spares = new SparesService(db);
            var stores = FactoryAssetRegisterTests.WithHttp(new FactoryStoresController(db, context, FactoryTestSupport.Access(context), scope, spares));
            var workOrders = new FactoryWorkOrderService(db, new DocumentNumberingService(db), FactoryTestSupport.Settings(db), new AssetLifecycleService(db, scope), scope, new FactorySlaService(db), new FactoryPmProgress(db), new FactoryJobGuards(db));
            var jobs = FactoryAssetRegisterTests.WithHttp(new FactoryWorkOrdersController(db, context, FactoryTestSupport.Access(context), scope, workOrders,
                new FailureCodeService(db), new FactorySlaService(db), FactoryTestSupport.Audit(db), spares));
            var contractorService = new ContractorService(db, workOrders);
            var contractors = FactoryAssetRegisterTests.WithHttp(new ContractorsController(db, context, FactoryTestSupport.Access(context), scope, contractorService));

            var main = Ok(await stores.CreateStore(new SaveStoreRequest(factory.Id, "CHL-MS", "Main"), default));
            var far = Ok(await stores.CreateStore(new SaveStoreRequest(other.Id, "KPS-MS", "Kapset"), default));
            var bin = Ok(await stores.AddBin(main.Id, new SaveBinRequest("A-01"), default));
            var belt = Ok(await stores.CreatePart(new SaveSparePartRequest("BLT", "Belt", "each", "Belts", IsCriticalSpare: true), default));
            var segment = Ok(await stores.CreatePart(new SaveSparePartRequest("SEG", "Segment", "each", IsSerialised: true, IsRepairable: true), default));
            var oil = Ok(await stores.CreatePart(new SaveSparePartRequest("OIL", "Gear oil H1", "litre", IsChemical: true, ChemicalKind: "Lubricant", FoodGradeClass: "H1"), default));
            Ok(await stores.Receive(main.Id, new ReceiveStockRequest(belt.Id, 6, 800), default));
            Ok(await stores.Receive(main.Id, new ReceiveStockRequest(segment.Id, 1, 20000, SerialNumbers: ["S1"]), default));
            Ok(await stores.Receive(main.Id, new ReceiveStockRequest(oil.Id, 20, 450), default));
            Ok(await stores.SetLevels(main.Id, belt.Id, new StockLevelsRequest(2, 4, 8, bin.Id), default));
            Ok(await stores.AddAssetPart(asset.Id, new SaveAssetPartRequest(belt.Id, 2, "Fan"), default));
            Ok(await stores.AddCatalogue(new SaveCatalogueRequest(supplier.Id, belt.Id, 7, "USD"), default));

            var plan = new JobPlan { TenantId = tenantId, Name = "Belts" };
            plan.Resources.Add(new JobPlanResource { Kind = JobPlanResourceKinds.Part, Description = "Belt", MaterialItemId = belt.Id, Quantity = 3 });
            db.JobPlans.Add(plan);
            await db.SaveChangesAsync();
            var job = Assert.IsType<FactoryWorkOrderDetail>(Assert.IsType<CreatedAtActionResult>((await jobs.Create(
                new CreateFactoryWorkOrderRequest("Change belts", AssetId: asset.Id, WorkType: "Corrective", JobPlanId: plan.Id), default)).Result).Value).Id;
            await jobs.Act(job, FactoryWorkOrderActions.Approve, new FactoryActionRequest(), default);
            var window = new MaintenanceWindow { TenantId = tenantId, FactoryId = factory.Id, WindowDate = DateTime.UtcNow.Date.AddDays(2), StartsAt = DateTime.UtcNow.Date.AddDays(2), EndsAt = DateTime.UtcNow.Date.AddDays(2).AddHours(12) };
            window.Jobs.Add(new MaintenanceWindowJob { TenantId = tenantId, WorkOrderId = job, Sequence = 1 });
            db.MaintenanceWindows.Add(window);
            await db.SaveChangesAsync();

            Ok(await stores.ReserveWindow(window.Id, default));
            var held = Ok(await stores.JobSparesFor(job, default));
            Ok(await stores.Kit(held.Reservations.Single().Id, default));
            var pack = Ok(await FactoryAssetRegisterTests.WithHttp(new MaintenanceWindowsController(db, context, FactoryTestSupport.Access(context), scope,
                new MaintenanceWindowService(db, FactoryTestSupport.Settings(db)), MaintenanceWindowTests.Pm(db, context), FactoryTestSupport.Settings(db))).GetPack(window.Id, default));
            Assert.Equal("A-01", pack.Jobs.Single().Spares.Single().Location);
            Ok(await jobs.Parts(job, new PartUsedRequest(belt.Id, 3), default));
            Ok(await jobs.Parts(job, new PartUsedRequest(segment.Id, 1, SerialNumber: "S1"), default));
            Ok(await jobs.Parts(job, new PartUsedRequest(oil.Id, 2), default));
            Assert.True(Ok(await jobs.Get(job, default)).PartsCost > 0);

            // Every read path.
            Assert.NotEmpty(Ok(await stores.Stores(false, default)));
            Assert.NotEmpty(Ok(await stores.Stock(main.Id, "b", "low", default)));
            Ok(await stores.Bins(main.Id, default));
            Assert.NotEmpty(Ok(await stores.Movements(main.Id, null, job, null, default)));
            Ok(await stores.Valuation(default));
            Assert.NotEmpty(Ok(await stores.Parts("e", "critical", default)));
            Assert.NotEmpty(Ok(await stores.Part(belt.Id, default)).WhereUsed);
            Ok(await stores.AssetParts(asset.Id, default));
            Ok(await stores.Units(segment.Id, null, default));
            var unit = Ok(await stores.Units(null, SpareUnitStatuses.Installed, default)).Single();
            Ok(await stores.UnitHistory(unit.Id, default));
            Assert.NotEmpty(Ok(await stores.Reorder(factory.Id, default)));
            Ok(await stores.Catalogue(null, null, "a", default));
            Assert.NotEmpty(Ok(await stores.Chemicals(default)));
            Assert.NotEmpty(Ok(await stores.LubricantLog(DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-5), DateTimeKind.Unspecified), null, default)));

            var transfer = Ok(await stores.RequestTransfer(new TransferRequest(main.Id, far.Id, oil.Id, 5), default));
            Ok(await stores.Dispatch(transfer.Id, null, default));
            Ok(await stores.ReceiveTransfer(transfer.Id, default));
            Assert.NotEmpty(Ok(await stores.Transfers("Received", default)));

            var count = Ok(await stores.OpenCount(new OpenCountRequest(main.Id), default));
            Ok(await stores.CountLine(count.Count.Id, count.Lines.First().Id, new CountLineRequest(1), default));
            Ok(await stores.PostCount(count.Count.Id, default));
            Ok(await stores.Counts(null, default));

            var requisition = Ok(await stores.CreateRequisition(new SaveRequisitionRequest(factory.Id, [new SaveRequisitionLine(belt.Id, 4, SupplierId: supplier.Id)], main.Id, NeededBy: DateTime.SpecifyKind(DateTime.UtcNow.AddDays(7), DateTimeKind.Unspecified)), default));
            Ok(await stores.RequisitionAction(requisition.Requisition.Id, "submit", null, default));
            Ok(await stores.RequisitionAction(requisition.Requisition.Id, "approve", null, default));
            Ok(await stores.Reorder(null, default));
            db.ExchangeRates.Add(new ExchangeRate { TenantId = tenantId, BaseCurrency = "USD", QuoteCurrency = "KES", Rate = 130, EffectiveFrom = DateTime.UtcNow.AddDays(-1) });
            await db.SaveChangesAsync();
            var ordered = Ok(await stores.RequisitionAction(requisition.Requisition.Id, "order", null, default));
            var po = ordered.PurchaseOrders.Single();
            Ok(await stores.ReceivePurchaseOrder(po.Id, new ReceivePurchaseOrderRequest(main.Id, [new ReceivePurchaseOrderLine(po.Lines.Single().Id, 4)]), default));
            Assert.NotEmpty(Ok(await stores.Requisitions("Ordered", default)));

            // Contractors.
            var firm = Ok(await contractors.Create(new SaveContractorRequest("Rift Boilers"), default));
            // Dates from JSON have no kind; they must still save to PostgreSQL.
            static DateTime Plain(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Unspecified);
            Ok(await contractors.AddDocument(firm.Id, new SaveContractorDocumentRequest("Insurance", null, Plain(DateTime.UtcNow.AddYears(-1)), Plain(DateTime.UtcNow.AddDays(10))), default));
            Ok(await contractors.AddDocument(firm.Id, new SaveContractorDocumentRequest("Tax compliance", null, null, DateTime.UtcNow.AddYears(1)), default));
            var agreement = Ok(await contractors.CreateAgreement(new SaveAgreementRequest(firm.Id, "AMC", DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow.AddDays(20),
                FactoryId: factory.Id, ResponseHours: 4, RepairHours: 24, VisitEveryMonths: 1, FirstVisitDue: DateTime.UtcNow.Date, AssetIds: [asset.Id]), default));
            Ok(await contractors.Assign(job, new AssignContractorRequest(firm.Id), default));
            await jobs.Act(job, FactoryWorkOrderActions.Assign, new FactoryActionRequest(TechnicianIds: [technician.Id]), default);
            await jobs.Act(job, FactoryWorkOrderActions.Start, new FactoryActionRequest(), default);
            await new FailureCodeService(db).EnsureSeededAsync(tenantId);
            var code = await db.FailureCodes.Where(x => x.Kind == FailureCodeKinds.Problem).Select(x => x.Id).FirstAsync();
            await jobs.Act(job, FactoryWorkOrderActions.Complete, new FactoryActionRequest(WorkDone: "Done", ProblemCodeId: code), default);
            Assert.NotEmpty(Ok(await contractors.Unbilled(firm.Id, default)));
            var invoice = Ok(await contractors.RecordInvoice(new SaveContractorInvoiceRequest(firm.Id, "INV-1", DateTime.UtcNow, [new SaveContractorInvoiceLine(job, 1000)]), default));
            Assert.Equal(ContractorInvoiceStatuses.Matched, invoice.Invoice.Status);
            Ok(await contractors.Approve(invoice.Invoice.Id, null, default));
            Assert.NotEmpty(Ok(await contractors.List(false, default)));
            Ok(await contractors.Get(firm.Id, default));
            Ok(await contractors.Agreements(null, null, default));
            Ok(await contractors.Agreement(agreement.Agreement.Id, default));
            Ok(await contractors.Jobs(null, true, default));
            Ok(await contractors.Invoices(null, null, default));
            Ok(await contractors.Invoice(invoice.Invoice.Id, default));
            var run = await contractorService.RunAsync(tenantId, DateTime.UtcNow);
            Assert.Equal(1, run.VisitsRaised);
        });
    }

    private static T Ok<T>(ActionResult<T> result) => Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private static async Task WithPostgresAsync(Func<string, Task> body)
    {
        var server = Environment.GetEnvironmentVariable("ECOSYS_TEST_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=ecosys_test;Username=postgres;Password=postgres";
        var databaseName = $"ecosys_spares_{Guid.NewGuid():N}";
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
