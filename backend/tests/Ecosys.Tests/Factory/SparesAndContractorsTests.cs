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

public sealed class SparesAndContractorsTests
{
    private static readonly DateTime Today = DateTime.UtcNow.Date;

    [Fact]
    public async Task Parts_used_on_a_job_are_issued_from_the_factory_store_at_moving_average_cost()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var stores = Stores(db, site.Admin);
        var jobs = FactoryWorkOrderTests.Controller(db, site.Admin);
        var bearing = await PartAsync(stores, "BRG-6205", "Bearing 6205");
        var job = await NewJobAsync(jobs, site, "Replace fan bearing");

        // No store yet: nothing can be used on the job.
        var noStore = await Assert.ThrowsAsync<BusinessRuleException>(() => jobs.Parts(job, new PartUsedRequest(bearing.Id, 1), CancellationToken.None));
        Assert.Contains("no store", noStore.Message);

        var main = Ok(await stores.CreateStore(new SaveStoreRequest(site.Chelal, "CHL-MS", "Chelal main stores"), CancellationToken.None));
        Assert.True(main.IsMain);
        Ok(await stores.Receive(main.Id, new ReceiveStockRequest(bearing.Id, 10, 100), CancellationToken.None));
        var after = Ok(await stores.Receive(main.Id, new ReceiveStockRequest(bearing.Id, 10, 130), CancellationToken.None));
        Assert.Equal(20, after.OnHand);
        Assert.Equal(115, after.AverageCost);

        var detail = Ok(await jobs.Parts(job, new PartUsedRequest(bearing.Id, 4), CancellationToken.None));
        Assert.Equal(460, detail.PartsCost);
        Assert.Equal(115, Assert.Single(detail.Parts).UnitCost);
        Assert.Equal(16, (await db.StoreStocks.SingleAsync()).OnHand);
        Assert.Equal(16, (await db.MaterialItems.SingleAsync(x => x.Id == bearing.Id)).QuantityOnHand);
        Assert.Contains(await db.StockMovements.ToListAsync(), x => x.MovementType == StoreMovementTypes.Issue && x.QuantityChange == -4 && x.WorkOrderId == job && x.StoreId == main.Id);

        var tooMany = await Assert.ThrowsAsync<BusinessRuleException>(() => jobs.Parts(job, new PartUsedRequest(bearing.Id, 17), CancellationToken.None));
        Assert.Contains("Only 16", tooMany.Message);

        // One wasn't needed: it goes back on the shelf.
        var usage = await db.WorkOrderMaterialUsages.SingleAsync();
        var spares = Ok(await stores.ReturnPart(job, usage.Id, new ReturnPartRequest(1), CancellationToken.None));
        Assert.Equal(3, Assert.Single(spares.Used).Quantity);
        Assert.Equal(17, (await db.StoreStocks.SingleAsync()).OnHand);
    }

    [Fact]
    public async Task Planned_parts_are_reserved_and_kitted_for_the_window_and_a_critical_spare_running_short_raises_one_alert()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var dispatcher = new RecordingDispatcher();
        var stores = Stores(db, site.Admin, dispatcher);
        var jobs = FactoryWorkOrderTests.Controller(db, site.Admin, dispatcher);
        var main = Ok(await stores.CreateStore(new SaveStoreRequest(site.Chelal, "CHL-MS", "Main stores"), CancellationToken.None));
        var belt = await PartAsync(stores, "BLT-B52", "V-belt B52", critical: true);
        Ok(await stores.Receive(main.Id, new ReceiveStockRequest(belt.Id, 4, 800), CancellationToken.None));
        Ok(await stores.SetLevels(main.Id, belt.Id, new StockLevelsRequest(1, 2), CancellationToken.None));

        var plan = new JobPlan { TenantId = site.TenantId, Name = "Change dryer fan belts", WorkType = "Preventive", NeedsLineStop = true };
        plan.Resources.Add(new JobPlanResource { Kind = JobPlanResourceKinds.Part, Description = "V-belt B52", MaterialItemId = belt.Id, Quantity = 3 });
        db.JobPlans.Add(plan);
        await db.SaveChangesAsync();
        var job = await NewJobAsync(jobs, site, "Dryer fan belts", plan.Id);
        var other = await NewJobAsync(jobs, site, "Withering fan belt");

        var window = new MaintenanceWindow { TenantId = site.TenantId, FactoryId = site.Chelal, WindowDate = Today.AddDays(3), StartsAt = Today.AddDays(3).AddHours(3), EndsAt = Today.AddDays(3).AddHours(15) };
        window.Jobs.Add(new MaintenanceWindowJob { TenantId = site.TenantId, WorkOrderId = job, Sequence = 1 });
        db.MaintenanceWindows.Add(window);
        await db.SaveChangesAsync();

        var reserved = Ok(await stores.ReserveWindow(window.Id, CancellationToken.None));
        Assert.Equal(1, reserved.LinesReserved);
        Assert.Empty(reserved.Shortages);
        // 4 on the shelf, 3 held: 1 free is below the reorder point of 2, so stores hear once.
        Assert.Single(dispatcher.Sent, x => x.Key == SparesService.CriticalLowKey);

        // Another job can't take what is held for the window.
        var held = await Assert.ThrowsAsync<BusinessRuleException>(() => jobs.Parts(other, new PartUsedRequest(belt.Id, 2), CancellationToken.None));
        Assert.Contains("reserved for other jobs", held.Message);

        var spares = Ok(await stores.JobSparesFor(job, CancellationToken.None));
        var line = Assert.Single(spares.Reservations);
        Ok(await stores.Kit(line.Id, CancellationToken.None));

        var pack = Ok(await MaintenanceWindowTests.Controller(db, site.Admin).GetPack(window.Id, CancellationToken.None));
        var packed = Assert.Single(Assert.Single(pack.Jobs).Spares);
        Assert.Equal(ReservationStatuses.Kitted, packed.Status);
        Assert.Equal(3, packed.Quantity);
        Assert.Empty(pack.Jobs.Single().Shortages);

        // On the day the kit is issued against the reservation; only 1 remains free.
        Ok(await jobs.Parts(job, new PartUsedRequest(belt.Id, 3), CancellationToken.None));
        var stock = await db.StoreStocks.SingleAsync();
        Assert.Equal((1m, 0m), (stock.OnHand, stock.Reserved));
        Assert.Equal(ReservationStatuses.Issued, (await db.StockReservations.SingleAsync()).Status);

        // Out of stock after the other job: still only one alert for this dip.
        Ok(await jobs.Parts(other, new PartUsedRequest(belt.Id, 1), CancellationToken.None));
        Assert.Single(dispatcher.Sent, x => x.Key == SparesService.CriticalLowKey);

        // Restocked above the reorder point, then short again: a new alert.
        Ok(await stores.Receive(main.Id, new ReceiveStockRequest(belt.Id, 5, 800), CancellationToken.None));
        var third = await NewJobAsync(jobs, site, "Fan belt again");
        Ok(await jobs.Parts(third, new PartUsedRequest(belt.Id, 4), CancellationToken.None));
        Assert.Equal(2, dispatcher.Sent.Count(x => x.Key == SparesService.CriticalLowKey));
    }

    [Fact]
    public async Task A_cancelled_job_gives_back_what_was_reserved_and_plan_shortages_are_reported()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var stores = Stores(db, site.Admin);
        var jobs = FactoryWorkOrderTests.Controller(db, site.Admin);
        var main = Ok(await stores.CreateStore(new SaveStoreRequest(site.Chelal, "CHL-MS", "Main stores"), CancellationToken.None));
        var seal = await PartAsync(stores, "SEAL-40", "Oil seal 40 mm");
        Ok(await stores.Receive(main.Id, new ReceiveStockRequest(seal.Id, 2, 50), CancellationToken.None));
        var plan = new JobPlan { TenantId = site.TenantId, Name = "Reseal gearbox" };
        plan.Resources.Add(new JobPlanResource { Kind = JobPlanResourceKinds.Part, Description = "Oil seal", MaterialItemId = seal.Id, Quantity = 3 });
        db.JobPlans.Add(plan);
        await db.SaveChangesAsync();
        var job = await NewJobAsync(jobs, site, "Reseal CTC gearbox", plan.Id);

        var spares = Ok(await stores.ReserveFromPlan(job, CancellationToken.None));
        Assert.Equal(2, Assert.Single(spares.Reservations).Quantity);
        // Asking again doesn't double up.
        Ok(await stores.ReserveFromPlan(job, CancellationToken.None));
        Assert.Equal(2, (await db.StoreStocks.SingleAsync()).Reserved);

        await jobs.Act(job, FactoryWorkOrderActions.Cancel, new FactoryActionRequest(Reason: "Gearbox replaced instead"), CancellationToken.None);
        Assert.Equal(0, (await db.StoreStocks.SingleAsync()).Reserved);
        Assert.Equal(ReservationStatuses.Released, (await db.StockReservations.SingleAsync()).Status);
    }

    [Fact]
    public async Task A_serialised_repairable_spare_is_tracked_onto_the_machine_out_for_recutting_and_back_into_stock()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var stores = Stores(db, site.Admin);
        var jobs = FactoryWorkOrderTests.Controller(db, site.Admin);
        var main = Ok(await stores.CreateStore(new SaveStoreRequest(site.Chelal, "CHL-MS", "Main stores"), CancellationToken.None));
        var segment = await PartAsync(stores, "CTC-SEG", "CTC roller segment", serialised: true, repairable: true);
        var recutter = new Supplier { TenantId = site.TenantId, SupplierName = "Limuru Engineering" };
        db.Suppliers.Add(recutter);
        db.ExchangeRates.Add(new ExchangeRate { TenantId = site.TenantId, BaseCurrency = "USD", QuoteCurrency = "KES", Rate = 130, EffectiveFrom = Today.AddDays(-1) });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => stores.Receive(main.Id, new ReceiveStockRequest(segment.Id, 2, 20000), CancellationToken.None));
        Ok(await stores.Receive(main.Id, new ReceiveStockRequest(segment.Id, 2, 20000, SerialNumbers: ["S-001", "S-002"]), CancellationToken.None));

        var job = await NewJobAsync(jobs, site, "Change CTC segments");
        Assert.Contains("serial number", (await Assert.ThrowsAsync<BusinessRuleException>(() => jobs.Parts(job, new PartUsedRequest(segment.Id, 1), CancellationToken.None))).Message);
        Ok(await jobs.Parts(job, new PartUsedRequest(segment.Id, 1, SerialNumber: "S-001"), CancellationToken.None));
        var fitted = await db.SpareUnits.SingleAsync(x => x.SerialNumber == "S-001");
        Assert.Equal((SpareUnitStatuses.Installed, site.Dryer.Id), (fitted.Status, fitted.AssetId!.Value));

        // The worn segment comes off for re-cutting: not usable stock until it is back.
        var usage = await db.WorkOrderMaterialUsages.SingleAsync();
        Ok(await stores.ReturnPart(job, usage.Id, new ReturnPartRequest(1, SerialNumber: "S-001", NeedsRepair: true), CancellationToken.None));
        Assert.Equal(SpareUnitStatuses.AwaitingRepair, (await db.SpareUnits.SingleAsync(x => x.Id == fitted.Id)).Status);
        Assert.Equal(1, (await db.StoreStocks.SingleAsync()).OnHand);

        Ok(await stores.SendForRepair(fitted.Id, new SendForRepairRequest(recutter.Id, "LE-778", Today.AddDays(14)), CancellationToken.None));
        var back = Ok(await stores.BackFromRepair(fitted.Id, new RepairReturnRequest(main.Id, 50, "USD", "Re-cut 0.4 mm"), CancellationToken.None));
        Assert.Equal(SpareUnitStatuses.InStore, back.Status);
        Assert.Equal(1, back.RepairCount);
        Assert.Equal(6500, back.TotalRepairCost);
        Assert.Equal(2, (await db.StoreStocks.SingleAsync()).OnHand);

        var history = Ok(await stores.UnitHistory(fitted.Id, CancellationToken.None));
        Assert.Equal(["Back from repair", "Sent for repair", "Removed for repair", "Installed", "Received"], history.Select(x => x.Type).ToArray());
    }

    [Fact]
    public async Task A_factory_sees_the_whole_groups_spares_and_borrows_through_a_transfer()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var kapset = new OrgNode { TenantId = site.TenantId, Level = OrgNodeLevels.Factory, Name = "Kapset", Code = "KPS" };
        db.OrgNodes.Add(kapset);
        var kapsetUser = FactoryTestSupport.NewUser(site.TenantId, "stores@kapset.test", AppRoles.Manager);
        db.Users.Add(kapsetUser);
        db.UserOrgNodeAssignments.Add(new UserOrgNodeAssignment { TenantId = site.TenantId, UserId = kapsetUser.Id, OrgNodeId = kapset.Id });
        await db.SaveChangesAsync();

        var admin = Stores(db, site.Admin);
        var chelalStore = Ok(await admin.CreateStore(new SaveStoreRequest(site.Chelal, "CHL-MS", "Chelal stores"), CancellationToken.None));
        var kapsetStore = Ok(await admin.CreateStore(new SaveStoreRequest(kapset.Id, "KPS-MS", "Kapset stores"), CancellationToken.None));
        var motor = await PartAsync(admin, "MTR-15", "15 kW motor", critical: true);
        Ok(await admin.Receive(chelalStore.Id, new ReceiveStockRequest(motor.Id, 2, 90000), CancellationToken.None));

        var kapsetContext = new FactoryTenantContext(site.TenantId, kapsetUser.Id, false, PermissionNames.CanViewMaterials, PermissionNames.CanIssueMaterials);
        var kapsetStores = Stores(db, kapsetContext);
        // Kapset's own stores only…
        Assert.Equal(["KPS-MS"], Ok(await kapsetStores.Stores(false, CancellationToken.None)).Select(x => x.Code).ToArray());
        await Assert.ThrowsAsync<NotFoundException>(() => kapsetStores.Stock(chelalStore.Id, null, null, CancellationToken.None));
        // …but the part shows where it is held across the group.
        var part = Ok(await kapsetStores.Part(motor.Id, CancellationToken.None));
        Assert.Equal(2, Assert.Single(part.Stores, x => x.StoreName == "Chelal stores").Available);

        var transfer = Ok(await kapsetStores.RequestTransfer(new TransferRequest(chelalStore.Id, kapsetStore.Id, motor.Id, 1, "Withering fan motor burnt"), CancellationToken.None));
        Assert.True(transfer.IsInterFactory);
        // Only the sending side dispatches.
        await Assert.ThrowsAsync<NotFoundException>(() => kapsetStores.Dispatch(transfer.Id, null, CancellationToken.None));
        Ok(await admin.Dispatch(transfer.Id, null, CancellationToken.None));
        Assert.Equal(1, (await db.StoreStocks.SingleAsync(x => x.StoreId == chelalStore.Id)).OnHand);
        var received = Ok(await kapsetStores.ReceiveTransfer(transfer.Id, CancellationToken.None));
        Assert.Equal(StoreTransferStatuses.Received, received.Status);
        var arrived = await db.StoreStocks.SingleAsync(x => x.StoreId == kapsetStore.Id);
        Assert.Equal((1m, 90000m), (arrived.OnHand, arrived.AverageCost));
    }

    [Fact]
    public async Task A_blind_cycle_count_hides_the_book_figure_and_posts_the_variance()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var stores = Stores(db, site.Admin);
        var main = Ok(await stores.CreateStore(new SaveStoreRequest(site.Chelal, "CHL-MS", "Main stores"), CancellationToken.None));
        var fuse = await PartAsync(stores, "FUSE-63", "Fuse 63 A");
        var grease = await PartAsync(stores, "GRS-EP2", "EP2 grease");
        Ok(await stores.Receive(main.Id, new ReceiveStockRequest(fuse.Id, 10, 40), CancellationToken.None));
        Ok(await stores.Receive(main.Id, new ReceiveStockRequest(grease.Id, 5, 600), CancellationToken.None));

        var count = Ok(await stores.OpenCount(new OpenCountRequest(main.Id), CancellationToken.None));
        Assert.All(count.Lines, x => Assert.Null(x.Expected));
        await Assert.ThrowsAsync<ConflictException>(() => stores.OpenCount(new OpenCountRequest(main.Id), CancellationToken.None));
        var fuseLine = count.Lines.Single(x => x.MaterialItemId == fuse.Id);
        Ok(await stores.CountLine(count.Count.Id, fuseLine.Id, new CountLineRequest(8, "Two found blown in the bin"), CancellationToken.None));

        var posted = Ok(await stores.PostCount(count.Count.Id, CancellationToken.None));
        Assert.Equal(StockCountStatuses.Posted, posted.Count.Status);
        Assert.Equal(-80, posted.Count.VarianceValue);
        Assert.Equal(-2, posted.Lines.Single(x => x.MaterialItemId == fuse.Id).Variance);
        Assert.Equal(8, (await db.StoreStocks.SingleAsync(x => x.MaterialItemId == fuse.Id)).OnHand);
        // Uncounted lines are left as they were.
        Assert.Equal(5, (await db.StoreStocks.SingleAsync(x => x.MaterialItemId == grease.Id)).OnHand);

        var value = Ok(await stores.Valuation(CancellationToken.None));
        Assert.Equal(8 * 40 + 5 * 600, value.Total);
    }

    [Fact]
    public async Task Reorder_suggestions_become_an_approved_requisition_a_purchase_order_and_stock_at_the_converted_price()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var stores = Stores(db, site.Admin);
        var main = Ok(await stores.CreateStore(new SaveStoreRequest(site.Chelal, "CHL-MS", "Main stores"), CancellationToken.None));
        var filter = await PartAsync(stores, "FLT-AIR", "Compressor air filter");
        var supplier = new Supplier { TenantId = site.TenantId, SupplierName = "Atlas Parts EA", LeadTimeDays = 21 };
        db.Suppliers.Add(supplier);
        db.ExchangeRates.Add(new ExchangeRate { TenantId = site.TenantId, BaseCurrency = "USD", QuoteCurrency = "KES", Rate = 130, EffectiveFrom = Today.AddDays(-1) });
        await db.SaveChangesAsync();
        Ok(await stores.Receive(main.Id, new ReceiveStockRequest(filter.Id, 1, 2600), CancellationToken.None));
        Ok(await stores.SetLevels(main.Id, filter.Id, new StockLevelsRequest(1, 2, 6), CancellationToken.None));
        Ok(await stores.AddCatalogue(new SaveCatalogueRequest(supplier.Id, filter.Id, 22, "USD", "AT-1613", IsPreferred: true), CancellationToken.None));

        var row = Assert.Single(Ok(await stores.Reorder(null, CancellationToken.None)));
        Assert.Equal((5m, "Atlas Parts EA", 22m, "USD", 21), (row.SuggestedQuantity, row.SupplierName!, row.UnitPrice!.Value, row.Currency!, row.LeadTimeDays!.Value));

        var requisition = Ok(await stores.CreateRequisition(new SaveRequisitionRequest(site.Chelal, [new SaveRequisitionLine(filter.Id, row.SuggestedQuantity, SupplierId: supplier.Id)], main.Id, "Below reorder point"), CancellationToken.None));
        Assert.Equal(22, Assert.Single(requisition.Lines).UnitPrice);
        Assert.Equal(5, Assert.Single(Ok(await stores.Reorder(null, CancellationToken.None))).OnRequisition);

        // A storekeeper can't approve their own; a manager can.
        var storekeeperUser = FactoryTestSupport.NewUser(site.TenantId, "stores@chelal.test", AppRoles.User);
        db.Users.Add(storekeeperUser);
        await db.SaveChangesAsync();
        var keeper = Stores(db, new FactoryTenantContext(site.TenantId, storekeeperUser.Id, false, PermissionNames.CanViewMaterials, PermissionNames.CanRequestMaterials, PermissionNames.CanApproveMaterials));
        var own = Ok(await keeper.CreateRequisition(new SaveRequisitionRequest(site.Chelal, [new SaveRequisitionLine(filter.Id, 1, SupplierId: supplier.Id)]), CancellationToken.None));
        Ok(await keeper.RequisitionAction(own.Requisition.Id, "submit", null, CancellationToken.None));
        Assert.Contains("other than the requester", (await Assert.ThrowsAsync<BusinessRuleException>(() => keeper.RequisitionAction(own.Requisition.Id, "approve", null, CancellationToken.None))).Message);

        Ok(await stores.RequisitionAction(requisition.Requisition.Id, "submit", null, CancellationToken.None));
        Ok(await stores.RequisitionAction(requisition.Requisition.Id, "approve", null, CancellationToken.None));
        var ordered = Ok(await stores.RequisitionAction(requisition.Requisition.Id, "order", null, CancellationToken.None));
        Assert.Equal(RequisitionStatuses.Ordered, ordered.Requisition.Status);
        var po = Assert.Single(ordered.PurchaseOrders);
        Assert.Equal(("USD", 110m), (po.Currency, po.Total));

        var received = Ok(await stores.ReceivePurchaseOrder(po.Id, new ReceivePurchaseOrderRequest(main.Id, [new ReceivePurchaseOrderLine(po.Lines.Single().Id, 5)]), CancellationToken.None));
        Assert.Equal("Received", received.Status);
        var stock = await db.StoreStocks.SingleAsync();
        // 1 at 2,600 and 5 at 22 USD × 130 = 2,860: average 2,816.67.
        Assert.Equal(6, stock.OnHand);
        Assert.Equal(2816.6667m, stock.AverageCost);
        Assert.Empty(Ok(await stores.Reorder(null, CancellationToken.None)));
    }

    [Fact]
    public async Task Where_used_lists_mark_parts_on_A_class_assets_as_critical_and_show_what_the_factory_holds()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var stores = Stores(db, site.Admin);
        var main = Ok(await stores.CreateStore(new SaveStoreRequest(site.Chelal, "CHL-MS", "Main stores"), CancellationToken.None));
        var chain = await PartAsync(stores, "CHN-12B", "Chain 12B");
        Ok(await stores.Receive(main.Id, new ReceiveStockRequest(chain.Id, 3, 1500), CancellationToken.None));

        var list = Ok(await stores.AddAssetPart(site.Dryer.Id, new SaveAssetPartRequest(chain.Id, 2, "Feed conveyor"), CancellationToken.None));
        var line = Assert.Single(list);
        Assert.True(line.IsCritical); // the dryer is A-class
        Assert.Equal(3, line.AvailableInFactory);
        await Assert.ThrowsAsync<ConflictException>(() => stores.AddAssetPart(site.Dryer.Id, new SaveAssetPartRequest(chain.Id, 1, "Feed conveyor"), CancellationToken.None));

        var part = Ok(await stores.Part(chain.Id, CancellationToken.None));
        Assert.True(part.OnCriticalAsset);
        Assert.Equal("FBD-01", Assert.Single(part.WhereUsed).AssetCode);
        Assert.True(Assert.Single(Ok(await stores.Parts(null, null, CancellationToken.None))).OnCriticalAsset);
    }

    [Fact]
    public async Task Lubricants_need_a_safety_data_sheet_and_what_goes_on_machines_is_logged_with_its_food_grade()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var stores = Stores(db, site.Admin);
        var jobs = FactoryWorkOrderTests.Controller(db, site.Admin);
        var main = Ok(await stores.CreateStore(new SaveStoreRequest(site.Chelal, "CHL-MS", "Main stores"), CancellationToken.None));
        var oil = Ok(await stores.CreatePart(new SaveSparePartRequest("LUB-H1-68", "Food-grade gear oil ISO 68", "litre", "Lubricants", "drum", 20,
            IsChemical: true, ChemicalKind: "Lubricant", HazardClass: "Not classified", FoodGradeClass: "h1"), CancellationToken.None));
        Assert.Equal("H1", oil.FoodGradeClass);

        var register = Assert.Single(Ok(await stores.Chemicals(CancellationToken.None)));
        Assert.Equal("Missing", register.SdsStatus);

        // Bought in 20-litre drums: 2 drums is 40 litres.
        Ok(await stores.Receive(main.Id, new ReceiveStockRequest(oil.Id, 2, 9000, InPurchaseUnits: true), CancellationToken.None));
        var stock = await db.StoreStocks.SingleAsync();
        Assert.Equal((40m, 450m), (stock.OnHand, stock.AverageCost));

        var job = await NewJobAsync(jobs, site, "Top up CTC gearbox");
        Ok(await jobs.Parts(job, new PartUsedRequest(oil.Id, 3), CancellationToken.None));
        var log = Assert.Single(Ok(await stores.LubricantLog(null, null, CancellationToken.None)));
        Assert.Equal(("H1", 3m, "FBD-01"), (log.FoodGradeClass!, log.Quantity, log.AssetCode!));
    }

    [Fact]
    public async Task A_contractor_takes_work_only_with_current_insurance_and_tax_compliance_and_their_agreement_sets_the_SLA()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var contractors = Contractors(db, site.Admin);
        var jobs = FactoryWorkOrderTests.Controller(db, site.Admin);
        var firm = Ok(await contractors.Create(new SaveContractorRequest("Rift Valley Boilers", "Boiler servicing, refractory"), CancellationToken.None));
        Assert.Equal(2, firm.Blockers.Count);

        var job = await NewJobAsync(jobs, site, "Boiler refractory repair");
        var blocked = await Assert.ThrowsAsync<BusinessRuleException>(() => contractors.Assign(job, new AssignContractorRequest(firm.Id), CancellationToken.None));
        Assert.Contains("no insurance on file", blocked.Message);

        Ok(await contractors.AddDocument(firm.Id, new SaveContractorDocumentRequest("Insurance", "POL-1", Today.AddYears(-1), Today.AddDays(-1)), CancellationToken.None));
        Ok(await contractors.AddDocument(firm.Id, new SaveContractorDocumentRequest("Tax compliance", "TCC-9", Today.AddMonths(-2), Today.AddMonths(10)), CancellationToken.None));
        Assert.Contains("insurance has expired", (await Assert.ThrowsAsync<BusinessRuleException>(() => contractors.Assign(job, new AssignContractorRequest(firm.Id), CancellationToken.None))).Message);
        var insurance = (await db.ContractorDocuments.SingleAsync(x => x.DocumentType == "Insurance")).Id;
        Ok(await contractors.UpdateDocument(insurance, new SaveContractorDocumentRequest("Insurance", "POL-2", Today, Today.AddYears(1)), CancellationToken.None));

        var agreement = Ok(await contractors.CreateAgreement(new SaveAgreementRequest(firm.Id, "Boiler AMC", Today.AddMonths(-1), Today.AddMonths(11), "Boiler and economiser",
            site.Chelal, 1_200_000, null, 4, 24, AssetIds: [site.Dryer.Id]), CancellationToken.None));
        var given = Ok(await contractors.Assign(job, new AssignContractorRequest(firm.Id), CancellationToken.None));
        Assert.Equal((firm.Name, agreement.Agreement.AgreementNumber), (given.ContractorName!, given.AgreementNumber!));
        var stored = await db.WorkOrders.SingleAsync(x => x.Id == job);
        Assert.Equal((stored.ApprovedAt ?? stored.CreatedAt).AddHours(24), stored.SlaResolutionDeadline);
        Assert.Equal("Rift Valley Boilers", Ok(await jobs.Get(job, CancellationToken.None)).ContractorName);
    }

    [Fact]
    public async Task A_contractor_invoice_is_matched_to_their_completed_jobs_before_it_can_be_approved()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var contractors = Contractors(db, site.Admin);
        var jobs = FactoryWorkOrderTests.Controller(db, site.Admin);
        var firm = await ApprovedContractorAsync(contractors, "Kericho Electricals");
        var done = await NewJobAsync(jobs, site, "Rewind dryer fan motor");
        var open = await NewJobAsync(jobs, site, "Replace panel MCCB");
        Ok(await contractors.Assign(done, new AssignContractorRequest(firm), CancellationToken.None));
        Ok(await contractors.Assign(open, new AssignContractorRequest(firm), CancellationToken.None));
        await CompleteAsync(jobs, site, done);

        Assert.Equal([done], Ok(await contractors.Unbilled(firm, CancellationToken.None)).Select(x => x.Id).ToArray());
        var invoice = Ok(await contractors.RecordInvoice(new SaveContractorInvoiceRequest(firm, "INV-204", Today,
            [new SaveContractorInvoiceLine(done, 45000), new SaveContractorInvoiceLine(open, 12000)]), CancellationToken.None));
        Assert.Equal(ContractorInvoiceStatuses.Query, invoice.Invoice.Status);
        Assert.Contains(invoice.Issues, x => x.Contains("is not complete"));
        await Assert.ThrowsAsync<BusinessRuleException>(() => contractors.Approve(invoice.Invoice.Id, null, CancellationToken.None));

        await CompleteAsync(jobs, site, open);
        var matched = Ok(await contractors.Rematch(invoice.Invoice.Id, CancellationToken.None));
        Assert.Equal(ContractorInvoiceStatuses.Matched, matched.Invoice.Status);
        Assert.Empty(matched.Issues);
        Assert.Equal(ContractorInvoiceStatuses.Approved, Ok(await contractors.Approve(invoice.Invoice.Id, null, CancellationToken.None)).Invoice.Status);

        // The same job billed twice is caught.
        var again = Ok(await contractors.RecordInvoice(new SaveContractorInvoiceRequest(firm, "INV-219", Today, [new SaveContractorInvoiceLine(done, 45000)]), CancellationToken.None));
        Assert.Contains(again.Issues, x => x.Contains("already billed on invoice INV-204"));
        Assert.Empty(Ok(await contractors.Unbilled(firm, CancellationToken.None)));
    }

    [Fact]
    public async Task Expiring_documents_and_agreements_alert_once_per_step_and_scheduled_visits_raise_contractor_jobs()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var dispatcher = new RecordingDispatcher();
        var contractors = Contractors(db, site.Admin, dispatcher);
        var firm = await ApprovedContractorAsync(contractors, "Lift & Hoist Inspections");
        var insurance = (await db.ContractorDocuments.SingleAsync(x => x.ContractorId == firm && x.DocumentType == "Insurance")).Id;
        Ok(await contractors.UpdateDocument(insurance, new SaveContractorDocumentRequest("Insurance", "POL-7", Today.AddYears(-1), Today.AddDays(20)), CancellationToken.None));
        var agreement = Ok(await contractors.CreateAgreement(new SaveAgreementRequest(firm, "Hoist inspections", Today.AddMonths(-11), Today.AddDays(30), null,
            null, 300_000, null, VisitEveryMonths: 3, FirstVisitDue: Today.AddDays(5), AssetIds: [site.Dryer.Id]), CancellationToken.None));

        var service = (ContractorService)ContractorServiceFor(db, site.Admin, dispatcher);
        var run = await service.RunAsync(site.TenantId, DateTime.UtcNow);
        Assert.Equal((1, 1, 1, 0), (run.DocumentAlerts, run.RenewalAlerts, run.VisitsRaised, run.AgreementsExpired));
        Assert.Single(dispatcher.Sent, x => x.Key == ContractorService.DocumentKey);
        Assert.Single(dispatcher.Sent, x => x.Key == ContractorService.RenewalKey);
        var visit = await db.WorkOrders.SingleAsync(x => x.MaintenanceAgreementId == agreement.Agreement.Id);
        Assert.Equal((firm, Today.AddDays(5)), (visit.ContractorId!.Value, visit.DueDate!.Value.Date));
        Assert.Equal(Today.AddDays(5).AddMonths(3), (await db.MaintenanceAgreements.SingleAsync()).NextVisitDue!.Value.Date);

        // Nothing new the next day.
        var again = await service.RunAsync(site.TenantId, DateTime.UtcNow.AddDays(1));
        Assert.Equal((0, 0, 0, 0), (again.DocumentAlerts, again.RenewalAlerts, again.VisitsRaised, again.AgreementsExpired));
        // At 7 days the document alerts again; after the end date the agreement expires.
        var later = await service.RunAsync(site.TenantId, DateTime.UtcNow.AddDays(31));
        Assert.Equal((1, 0, 1), (later.DocumentAlerts, later.RenewalAlerts, later.AgreementsExpired));
        Assert.Equal(AgreementStatuses.Expired, (await db.MaintenanceAgreements.SingleAsync()).Status);
    }

    /* ── Helpers ─────────────────────────────────────────────────────────── */

    private static async Task<Guid> ApprovedContractorAsync(ContractorsController contractors, string name)
    {
        var firm = Ok(await contractors.Create(new SaveContractorRequest(name), CancellationToken.None));
        Ok(await contractors.AddDocument(firm.Id, new SaveContractorDocumentRequest("Insurance", "POL", Today.AddMonths(-1), Today.AddYears(1)), CancellationToken.None));
        Ok(await contractors.AddDocument(firm.Id, new SaveContractorDocumentRequest("Tax compliance", "TCC", Today.AddMonths(-1), Today.AddYears(1)), CancellationToken.None));
        return firm.Id;
    }

    private static async Task CompleteAsync(FactoryWorkOrdersController jobs, FactoryWorkOrderTests.Seeded site, Guid id)
    {
        await jobs.Act(id, FactoryWorkOrderActions.Assign, new FactoryActionRequest(TechnicianIds: [site.Fitter.Id]), CancellationToken.None);
        await jobs.Act(id, FactoryWorkOrderActions.Start, new FactoryActionRequest(), CancellationToken.None);
        await jobs.Act(id, FactoryWorkOrderActions.Complete, new FactoryActionRequest(WorkDone: "Done by contractor", ProblemCodeId: site.Code("BRD")), CancellationToken.None);
    }

    private static async Task<SparePartDetail> PartAsync(FactoryStoresController stores, string code, string name, bool critical = false, bool serialised = false, bool repairable = false) =>
        Ok(await stores.CreatePart(new SaveSparePartRequest(code, name, "each", IsCriticalSpare: critical, IsSerialised: serialised, IsRepairable: repairable), CancellationToken.None));

    private static async Task<Guid> NewJobAsync(FactoryWorkOrdersController jobs, FactoryWorkOrderTests.Seeded site, string title, Guid? jobPlanId = null)
    {
        var created = Assert.IsType<FactoryWorkOrderDetail>(Assert.IsType<CreatedAtActionResult>((await jobs.Create(
            new CreateFactoryWorkOrderRequest(title, WorkType: "Corrective", AssetId: site.Dryer.Id, JobPlanId: jobPlanId), CancellationToken.None)).Result).Value);
        if (created.Status == WorkOrderStatuses.Requested) await jobs.Act(created.Id, FactoryWorkOrderActions.Approve, new FactoryActionRequest(), CancellationToken.None);
        return created.Id;
    }

    private static FactoryStoresController Stores(AppDbContext db, FactoryTenantContext context, INotificationDispatcher? dispatcher = null) =>
        FactoryAssetRegisterTests.WithHttp(new FactoryStoresController(db, context, FactoryTestSupport.Access(context), FactoryTestSupport.Scope(db, context), new SparesService(db, dispatcher)));

    private static IContractorService ContractorServiceFor(AppDbContext db, FactoryTenantContext context, INotificationDispatcher? dispatcher)
    {
        var scope = FactoryTestSupport.Scope(db, context);
        var workOrders = new FactoryWorkOrderService(db, new DocumentNumberingService(db), FactoryTestSupport.Settings(db), new AssetLifecycleService(db, scope), scope, new FactorySlaService(db), new FactoryPmProgress(db), new FactoryJobGuards(db));
        return new ContractorService(db, workOrders, dispatcher);
    }

    private static ContractorsController Contractors(AppDbContext db, FactoryTenantContext context, INotificationDispatcher? dispatcher = null) =>
        FactoryAssetRegisterTests.WithHttp(new ContractorsController(db, context, FactoryTestSupport.Access(context), FactoryTestSupport.Scope(db, context), ContractorServiceFor(db, context, dispatcher)));

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
