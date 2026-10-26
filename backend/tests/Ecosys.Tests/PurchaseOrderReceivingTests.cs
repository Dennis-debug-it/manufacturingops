using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

/// <summary>Receiving a purchase order line linked to a part books the stock, at a weighted cost.</summary>
public sealed class PurchaseOrderReceivingTests
{
    private readonly Guid tenantId = Guid.NewGuid();
    private readonly Guid userId = Guid.NewGuid();

    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static IStockLedgerService Ledger(AppDbContext db) => (IStockLedgerService)Activator.CreateInstance(
        typeof(AppDbContext).Assembly.GetType("Ecosys.Infrastructure.Services.StockLedgerService")!, db)!;
    private PurchaseOrdersController Controller(AppDbContext db, bool pendingApproval = false) => new(db,
        new FakeTenantContext(tenantId, userId), new NoOpDocumentNumberingService(), new NoOpAuditLogService(),
        new FakeApprovals(pendingApproval), new NoOpUserAccessService(), Ledger(db), new NoOpBranchAccessService());

    private async Task<(PurchaseOrder Order, MaterialItem Part, Branch Branch)> Seed(
        AppDbContext db, string currency = "KES", bool linked = true, decimal onHand = 10, decimal? unitCost = 100)
    {
        var branch = new Branch { TenantId = tenantId, Name = "Main Store", Code = "MAIN" };
        var part = new MaterialItem { TenantId = tenantId, ItemCode = "FLT-1", ItemName = "Filter", UnitOfMeasure = "each", QuantityOnHand = onHand, UnitCost = unitCost };
        var supplier = new Supplier { TenantId = tenantId, SupplierName = "Acme" };
        var order = new PurchaseOrder
        {
            TenantId = tenantId, PoNumber = "PO-0001", SupplierId = supplier.Id, Status = "Sent", Currency = currency,
            Lines = [new PurchaseOrderLine { TenantId = tenantId, ItemName = "Filter", MaterialItemId = linked ? part.Id : null, QuantityOrdered = 10, UnitCost = 160, LineTotal = 1600 }],
        };
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "T", CompanyName = "T", Currency = "KES" });
        db.Users.Add(new User { Id = userId, TenantId = tenantId, FullName = "Storekeeper", Email = "store@tenant.test", Role = AppRoles.Admin, PasswordHash = string.Empty, IsActive = true });
        db.Branches.Add(branch);
        db.MaterialItems.Add(part);
        db.BranchMaterialStocks.Add(new BranchMaterialStock { TenantId = tenantId, BranchId = branch.Id, MaterialId = part.Id, QuantityOnHand = onHand, UnitCost = unitCost });
        db.Suppliers.Add(supplier);
        db.PurchaseOrders.Add(order);
        await db.SaveChangesAsync();
        return (order, part, branch);
    }

    [Fact]
    public async Task ReceivingALinkedLineAddsBranchStockAtAWeightedCost()
    {
        await using var db = Db();
        var (order, part, branch) = await Seed(db);
        var line = order.Lines.Single();

        await Controller(db).ReceiveLine(order.Id, new(line.Id, 10, branch.Id), default);

        var stock = await db.BranchMaterialStocks.SingleAsync(x => x.BranchId == branch.Id && x.MaterialId == part.Id);
        Assert.Equal(20, stock.QuantityOnHand);
        Assert.Equal(20, part.QuantityOnHand);
        // (10 × 100 + 10 × 160) / 20
        Assert.Equal(130, part.UnitCost);
        Assert.Equal(130, stock.UnitCost);
        var movement = await db.StockMovements.SingleAsync();
        Assert.Equal("Receipt", movement.MovementType);
        Assert.Equal("PO-0001", movement.ReferenceNumber);
        Assert.Equal(branch.Id, movement.BranchId);
        Assert.Equal(branch.Id, (await db.PurchaseOrderReceiptEvents.SingleAsync()).BranchId);
        Assert.Equal("Received", order.Status);
    }

    [Fact]
    public async Task PartialReceiptLeavesTheOrderPartiallyReceivedAndRefusesOverReceipt()
    {
        await using var db = Db();
        var (order, part, branch) = await Seed(db);
        var line = order.Lines.Single();
        var controller = Controller(db);

        await controller.ReceiveLine(order.Id, new(line.Id, 4, branch.Id), default);
        Assert.Equal("PartiallyReceived", order.Status);

        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.ReceiveLine(order.Id, new(line.Id, 7, branch.Id), default));
        Assert.Equal(14, part.QuantityOnHand);
        Assert.Equal(4, line.QuantityReceived);
        Assert.Single(db.StockMovements);
    }

    [Fact]
    public async Task UnlinkedLineRecordsTheReceiptWithoutTouchingStock()
    {
        await using var db = Db();
        var (order, part, branch) = await Seed(db, linked: false);

        await Controller(db).ReceiveLine(order.Id, new(order.Lines.Single().Id, 10, branch.Id), default);

        Assert.Equal(10, part.QuantityOnHand);
        Assert.Equal(100, part.UnitCost);
        Assert.Empty(db.StockMovements);
        Assert.Null((await db.PurchaseOrderReceiptEvents.SingleAsync()).BranchId);
        Assert.Equal("Received", order.Status);
    }

    [Fact]
    public async Task ForeignCurrencyCostIsConvertedIntoTheStockCurrency()
    {
        await using var db = Db();
        var (order, part, branch) = await Seed(db, currency: "USD", onHand: 0, unitCost: null);
        db.ExchangeRates.Add(new ExchangeRate { TenantId = tenantId, BaseCurrency = "USD", QuoteCurrency = "KES", Rate = 130, EffectiveFrom = DateTime.UtcNow.AddDays(-1) });
        await db.SaveChangesAsync();

        await Controller(db).ReceiveLine(order.Id, new(order.Lines.Single().Id, 2, branch.Id), default);

        Assert.Equal(160 * 130, part.UnitCost);
        Assert.Equal(2, part.QuantityOnHand);
    }

    [Fact]
    public async Task WithoutAnExchangeRateTheStockIsBookedButTheCostIsLeftAlone()
    {
        await using var db = Db();
        var (order, part, branch) = await Seed(db, currency: "USD");

        await Controller(db).ReceiveLine(order.Id, new(order.Lines.Single().Id, 2, branch.Id), default);

        Assert.Equal(12, part.QuantityOnHand);
        Assert.Equal(100, part.UnitCost);
    }

    [Fact]
    public async Task OrderAwaitingApprovalCannotBeReceived()
    {
        await using var db = Db();
        var (order, part, branch) = await Seed(db);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Controller(db, pendingApproval: true).ReceiveLine(order.Id, new(order.Lines.Single().Id, 1, branch.Id), default));
        Assert.Equal(10, part.QuantityOnHand);
        Assert.Empty(db.PurchaseOrderReceiptEvents);
    }

    [Fact]
    public async Task CreatingALinkedLineTakesThePartsNameAndRejectsUnknownParts()
    {
        await using var db = Db();
        var (order, part, _) = await Seed(db);
        var controller = Controller(db);

        var result = await controller.CreatePurchaseOrder(new(order.SupplierId, null, [new(null, 3, 50, part.Id)]), default);
        var created = Assert.IsType<PurchaseOrderDetailResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        var line = Assert.Single(created.Lines);
        Assert.Equal("Filter", line.ItemName);
        Assert.Equal(part.Id, line.MaterialItemId);
        Assert.Equal("FLT-1", line.ItemCode);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            controller.CreatePurchaseOrder(new(order.SupplierId, null, [new("Ghost", 1, 1, Guid.NewGuid())]), default));
    }

    [Theory]
    [InlineData(0, null, 5, 80, 80)]
    [InlineData(0, 100.0, 5, 80, 80)]
    [InlineData(10, 100.0, 10, 160, 130)]
    [InlineData(3, 10.0, 1, 20, 12.5)]
    public void WeightedCostBlendsOnHandAndReceived(double onHand, double? current, double received, double cost, double expected) =>
        Assert.Equal((decimal)expected, PurchaseOrdersController.WeightedCost((decimal)onHand, (decimal?)current, (decimal)received, (decimal)cost));

    private sealed class FakeApprovals(bool pending) : IApprovalWorkflowService
    {
        public Task<ApprovalStartResult?> StartAsync(Guid tenantId, string documentType, Guid documentId, string documentNumber, decimal amount, string currency, Guid initiatedByUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ApprovalStartResult?>(null);
        public Task DecideAsync(Guid tenantId, Guid requestId, bool approve, string? note, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ApprovalRequest?> GetActiveForDocumentAsync(Guid tenantId, string documentType, Guid documentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(pending ? new ApprovalRequest() : null);
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
            Task.FromResult("PO-0002");
        public Task<NumberingSetting> UpsertAsync(Guid tenantId, Guid? branchId, string documentType, string prefix, long nextNumber, int paddingLength, string resetFrequency, bool includeYear, bool includeMonth, bool isActive, CancellationToken cancellationToken = default, string? suffix = null, string yearFormat = "YYYY", string separator = "-", bool isLocked = false) =>
            Task.FromException<NumberingSetting>(new NotSupportedException());
    }
    private sealed class NoOpAuditLogService : IAuditLogService
    {
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, string severity = "Info", string? actorName = null, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
