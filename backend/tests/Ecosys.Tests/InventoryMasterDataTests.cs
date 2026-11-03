using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

/// <summary>Supplier management and the part fields reordering and traceability rely on.</summary>
public sealed class InventoryMasterDataTests
{
    private readonly Guid tenantId = Guid.NewGuid();
    private readonly Guid userId = Guid.NewGuid();

    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private async Task SeedUser(AppDbContext db)
    {
        db.Users.Add(new User { Id = userId, TenantId = tenantId, FullName = "Storekeeper", Email = "store@tenant.test", Role = AppRoles.Admin, PasswordHash = string.Empty, IsActive = true });
        await db.SaveChangesAsync();
    }

    private PurchaseOrdersController Purchasing(AppDbContext db) => new(db, new FakeTenantContext(tenantId, userId),
        new NoOpDocumentNumberingService(), new NoOpAuditLogService(), new NoApprovals(), new NoOpUserAccessService(),
        new StockLedgerService(db), new NoOpBranchAccessService());

    private MaterialsController Materials(AppDbContext db) => new(db, new FakeTenantContext(tenantId, userId),
        new StockLedgerService(db), new NoOpUserAccessService(), new NoOpAuditLogService(), new NoOpBranchAccessService());

    private static T Ok<T>(ActionResult<T> result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    [Fact]
    public async Task SupplierCanBeEditedAndNamesStayUnique()
    {
        await using var db = Db();
        await SeedUser(db);
        var controller = Purchasing(db);
        var acme = Ok(await controller.CreateSupplier(new("Acme Parts", LeadTimeDays: 5), default));
        Ok(await controller.CreateSupplier(new("Bolt Co"), default));

        var updated = Ok(await controller.UpdateSupplier(acme.Id, new("Acme Parts Ltd", Phone: " 0700 000 000 ", LeadTimeDays: 12), default));

        Assert.Equal("Acme Parts Ltd", updated.SupplierName);
        Assert.Equal("0700 000 000", updated.Phone);
        Assert.Equal(12, updated.LeadTimeDays);
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.UpdateSupplier(acme.Id, new("bolt co"), default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.UpdateSupplier(acme.Id, new("Acme", LeadTimeDays: -1), default));
    }

    [Fact]
    public async Task InactiveSupplierCannotTakeNewOrdersAndSupplierLeadTimeSetsTheExpectedDate()
    {
        await using var db = Db();
        await SeedUser(db);
        var controller = Purchasing(db);
        var supplier = Ok(await controller.CreateSupplier(new("Acme", LeadTimeDays: 3), default));

        var order = Ok(await controller.CreatePurchaseOrder(new(supplier.Id, null, [new("Seal", 1, 5)]), default));
        Assert.Equal(DateTime.UtcNow.Date.AddDays(3), order.ExpectedAt);

        await controller.SetSupplierStatus(supplier.Id, new(false), default);
        Assert.False((await db.Suppliers.SingleAsync()).IsActive);
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.CreatePurchaseOrder(new(supplier.Id, null, [new("Seal", 1, 5)]), default));

        await controller.SetSupplierStatus(supplier.Id, new(true), default);
        Ok(await controller.CreatePurchaseOrder(new(supplier.Id, null, [new("Seal", 1, 5)]), default));
    }

    [Fact]
    public async Task PartKeepsBarcodePreferredSupplierAndReorderQuantity()
    {
        await using var db = Db();
        await SeedUser(db);
        var supplier = Ok(await Purchasing(db).CreateSupplier(new("Acme"), default));
        var materials = Materials(db);

        var created = Assert.IsType<MaterialItemResponse>(Assert.IsType<CreatedAtActionResult>((await materials.Create(
            new("FLT-1", "Air filter", "Filters", "each", 0, 4, 12, null, "5012345678900", supplier.Id, 10), default)).Result).Value);

        Assert.Equal("5012345678900", created.Barcode);
        Assert.Equal("Acme", created.PreferredSupplierName);
        Assert.Equal(10, created.ReorderQuantity);
        // Barcode works as an exact-match search.
        var found = Assert.IsAssignableFrom<IReadOnlyCollection<MaterialItemResponse>>(
            Assert.IsType<OkObjectResult>((await materials.GetAll(false, null, "5012345678900", null, default)).Result).Value);
        Assert.Single(found);
        await Assert.ThrowsAsync<BusinessRuleException>(() => materials.Create(new("FLT-2", "Other filter", null, "each", 0, 0, null, null, "5012345678900"), default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => materials.Create(new("FLT-3", "Third", null, "each", 0, 0, null, null, ReorderQuantity: 0), default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => materials.Create(new("FLT-4", "Fourth", null, "each", 0, 0, null, null, PreferredSupplierId: Guid.NewGuid()), default));
    }

    [Fact]
    public async Task AdjustmentNeedsAReasonAndKeepsItsBatchNumber()
    {
        await using var db = Db();
        await SeedUser(db);
        var part = new MaterialItem { TenantId = tenantId, ItemCode = "P", ItemName = "Pump", UnitOfMeasure = "each", QuantityOnHand = 5 };
        db.MaterialItems.Add(part);
        await db.SaveChangesAsync();
        var materials = Materials(db);

        await Assert.ThrowsAsync<BusinessRuleException>(() => materials.Adjust(part.Id, new(null, -1, "  "), default));
        Assert.Empty(db.StockMovements);

        await materials.Adjust(part.Id, new(null, -1, "Damaged in store", "LOT-7"), default);
        await materials.Replenish(part.Id, new(null, 2, null, "Top-up", null, "SN-1, SN-2"), default);

        var movements = await db.StockMovements.OrderBy(x => x.CreatedAt).ToListAsync();
        Assert.Equal(["LOT-7", "SN-1, SN-2"], movements.Select(x => x.BatchOrSerial));
        Assert.Equal(6, part.QuantityOnHand);
    }

    [Fact]
    public async Task StockLocationsListsBranchesAndVansHoldingThePart()
    {
        await using var db = Db();
        await SeedUser(db);
        var branch = new Branch { TenantId = tenantId, Name = "Main", Code = "M" };
        var van = new Van { TenantId = tenantId, Name = "Van 1", HomeBranchId = branch.Id };
        var part = new MaterialItem { TenantId = tenantId, ItemCode = "P", ItemName = "Pump", UnitOfMeasure = "each" };
        db.AddRange(branch, van, part);
        await db.SaveChangesAsync();
        var ledger = new StockLedgerService(db);
        await ledger.RecordReceiptAsync(part, branch.Id, 6, userId, "Opening", null);
        await db.SaveChangesAsync();
        await ledger.RecordVanLoadAsync(part, branch.Id, van.Id, 2, userId, "Load");
        await db.SaveChangesAsync();

        var locations = Ok(await Materials(db).GetStockLocations(part.Id, default));

        Assert.Equal(("Main", 4m), (locations.Branches.Single().BranchName, locations.Branches.Single().QuantityOnHand));
        Assert.Equal(("Van 1", 2m), (locations.Vans.Single().VanName, locations.Vans.Single().QuantityOnHand));
    }

    [Theory]
    [InlineData(2, 10, null, 18)]      // top up to twice the reorder level
    [InlineData(2, 10, 5.0, 10)]       // two lots of 5 clear the level of 10 from 2
    [InlineData(10, 10, 5.0, 5)]       // at the level exactly: one lot lifts it above
    [InlineData(19.5, 10, null, 1)]
    public void SuggestedQuantityUsesLotsOrTopsUp(double available, double reorderLevel, double? lot, double expected) =>
        Assert.Equal((decimal)expected, PurchaseOrdersController.SuggestQuantity((decimal)available, (decimal)reorderLevel, (decimal?)lot));

    [Fact]
    public async Task ReorderSuggestionsCountWhatIsOnOrderAndDraftOnePoPerSupplier()
    {
        await using var db = Db();
        await SeedUser(db);
        var purchasing = Purchasing(db);
        var acme = Ok(await purchasing.CreateSupplier(new("Acme", LeadTimeDays: 4), default));
        var bolt = Ok(await purchasing.CreateSupplier(new("Bolt Co"), default));
        var filter = new MaterialItem { TenantId = tenantId, ItemCode = "F", ItemName = "Filter", UnitOfMeasure = "each", QuantityOnHand = 2, ReorderLevel = 10, UnitCost = 8, PreferredSupplierId = acme.Id };
        var belt = new MaterialItem { TenantId = tenantId, ItemCode = "B", ItemName = "Belt", UnitOfMeasure = "each", QuantityOnHand = 1, ReorderLevel = 4, UnitCost = 30, PreferredSupplierId = acme.Id, ReorderQuantity = 6 };
        var seal = new MaterialItem { TenantId = tenantId, ItemCode = "S", ItemName = "Seal", UnitOfMeasure = "each", QuantityOnHand = 0, ReorderLevel = 3, UnitCost = 5 };
        var plenty = new MaterialItem { TenantId = tenantId, ItemCode = "P", ItemName = "Plenty", UnitOfMeasure = "each", QuantityOnHand = 50, ReorderLevel = 3 };
        db.MaterialItems.AddRange(filter, belt, seal, plenty);
        await db.SaveChangesAsync();
        // 3 belts already on order: 1 + 3 = 4 is still not above the level of 4.
        Ok(await purchasing.CreatePurchaseOrder(new(bolt.Id, null, [new(null, 3, 30, belt.Id)]), default));

        var suggestions = Assert.IsAssignableFrom<IReadOnlyCollection<ReorderSuggestionResponse>>(
            Assert.IsType<OkObjectResult>((await purchasing.GetReorderSuggestions(null, default)).Result).Value).ToDictionary(x => x.ItemName);

        Assert.DoesNotContain("Plenty", suggestions.Keys);
        Assert.Equal((18m, 4), (suggestions["Filter"].SuggestedQuantity, suggestions["Filter"].LeadTimeDays));
        Assert.Equal((3m, 6m), (suggestions["Belt"].OnOrder, suggestions["Belt"].SuggestedQuantity));
        Assert.Null(suggestions["Seal"].PreferredSupplierId);

        await Assert.ThrowsAsync<BusinessRuleException>(() => purchasing.CreateFromSuggestions(new([new(seal.Id, 3)]), default));
        var drafted = Assert.IsAssignableFrom<IReadOnlyCollection<PurchaseOrderListItemResponse>>(Assert.IsType<OkObjectResult>((await purchasing.CreateFromSuggestions(new(
            [new(filter.Id, 18), new(belt.Id, 6), new(seal.Id, 3, bolt.Id)]), default)).Result).Value);

        Assert.Equal(2, drafted.Count);
        var acmeOrder = await db.PurchaseOrders.Include(x => x.Lines).SingleAsync(x => x.SupplierId == acme.Id);
        Assert.Equal("Draft", acmeOrder.Status);
        Assert.Equal(18 * 8 + 6 * 30, acmeOrder.TotalAmount);
        Assert.All(acmeOrder.Lines, line => Assert.NotNull(line.MaterialItemId));
        Assert.Equal(2, await db.PurchaseOrders.CountAsync(x => x.SupplierId == bolt.Id));
    }

    private sealed class NoApprovals : IApprovalWorkflowService
    {
        public Task<ApprovalStartResult?> StartAsync(Guid tenantId, string documentType, Guid documentId, string documentNumber, decimal amount, string currency, Guid initiatedByUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ApprovalStartResult?>(null);
        public Task DecideAsync(Guid tenantId, Guid requestId, bool approve, string? note, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ApprovalRequest?> GetActiveForDocumentAsync(Guid tenantId, string documentType, Guid documentId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ApprovalRequest?>(null);
    }
    private sealed class FakeTenantContext(Guid tenantId, Guid userId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
        public Guid? SessionId => Guid.NewGuid();
        public string? Email => "admin@test.local";
        public string? Role => AppRoles.Admin;
        public string? JobTitle => "Admin";
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public bool IsAdmin => true;
        public bool HasRole(string role) => string.Equals(role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase);
        public bool HasPermission(string permissionName) => true;
        public Guid GetRequiredTenantId() => tenantId;
        public Guid GetRequiredUserId() => userId;
        public Guid GetRequiredSessionId() => SessionId!.Value;
    }
    private sealed class NoOpUserAccessService : IUserAccessService
    {
        public void EnsureAdmin() { }
        public void EnsureAdminOrPermission(string permissionName) { }
        public void EnsureAdminOrAnyPermission(params string[] permissionNames) { }
        public void EnsureTenantOperationalAccess() { }
    }
    private sealed class NoOpBranchAccessService : IBranchAccessService
    {
        public Task<BranchQueryScope> GetQueryScopeAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new BranchQueryScope(false, requestedBranchId, [], true));
        public Task<Guid?> ResolveBranchIdForWriteAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(requestedBranchId);
        public Task EnsureCanAccessBranchAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyCollection<Guid>> GetAccessibleBranchIdsAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Guid>>([]);
    }
    private sealed class NoOpDocumentNumberingService : IDocumentNumberingService
    {
        public Task<string> GenerateAsync(Guid tenantId, Guid? branchId, string documentType, CancellationToken cancellationToken = default) =>
            Task.FromResult("PO-0001");
        public Task<NumberingSetting> UpsertAsync(Guid tenantId, Guid? branchId, string documentType, string prefix, long nextNumber, int paddingLength, string resetFrequency, bool includeYear, bool includeMonth, bool isActive, CancellationToken cancellationToken = default, string? suffix = null, string yearFormat = "YYYY", string separator = "-", bool isLocked = false) =>
            Task.FromException<NumberingSetting>(new NotSupportedException());
    }
    private sealed class NoOpAuditLogService : IAuditLogService
    {
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, string severity = "Info", string? actorName = null, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
