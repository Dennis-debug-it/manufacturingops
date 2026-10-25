using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

/// <summary>Settings → Inventory changes how stock behaves, not just what the settings page shows.</summary>
public sealed class InventorySettingsTests
{
    private readonly Guid tenantId = Guid.NewGuid();
    private readonly Guid userId = Guid.NewGuid();

    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private async Task<MaterialItem> SeedPart(AppDbContext db, string? settingsJson = null, decimal onHand = 2, decimal? unitCost = null)
    {
        var part = new MaterialItem { TenantId = tenantId, ItemCode = "P1", ItemName = "Pump seal", UnitOfMeasure = "each", QuantityOnHand = onHand, UnitCost = unitCost };
        db.MaterialItems.Add(part);
        db.Users.Add(new User { Id = userId, TenantId = tenantId, FullName = "Storekeeper", Email = "store@tenant.test", Role = AppRoles.Admin, PasswordHash = string.Empty, IsActive = true });
        if (settingsJson is not null)
            db.TenantModuleSettings.Add(new TenantModuleSetting { TenantId = tenantId, ModuleKey = "inventory", ValueJson = settingsJson });
        await db.SaveChangesAsync();
        return part;
    }

    [Fact]
    public void MissingOrBrokenSettingsFallBackToThePageDefaults()
    {
        Assert.Equal(InventoryModuleSettings.Default, InventoryModuleSettings.Parse(null));
        Assert.Equal(InventoryModuleSettings.Default, InventoryModuleSettings.Parse("not json"));
        var parsed = InventoryModuleSettings.Parse("""{"lowStockThresholdPercent":"35","allowNegativeStock":true,"requireApprovalAboveValue":1000,"autoDeductOnWorkOrderComplete":"false","defaultReorderLeadDays":14}""");
        Assert.Equal(new InventoryModuleSettings(35, false, 1000, true, 14), parsed);
        Assert.True(parsed.IsCritical(3.5m, 10));
        Assert.False(parsed.IsCritical(4, 10));
        Assert.False(parsed.IsCritical(0, 0));
    }

    [Fact]
    public async Task IssuingBeyondStockIsRefusedByDefault()
    {
        await using var db = Db();
        var part = await SeedPart(db);
        await Assert.ThrowsAsync<BusinessRuleException>(() => new StockLedgerService(db).RecordIssueAsync(part, null, 5, userId, "job"));
        Assert.Equal(2, part.QuantityOnHand);
    }

    [Fact]
    public async Task AllowNegativeStockLetsIssuesAndAdjustmentsGoBelowZeroButNotTransfers()
    {
        await using var db = Db();
        var part = await SeedPart(db, """{"allowNegativeStock":true}""");
        var branch = Guid.NewGuid();
        var ledger = new StockLedgerService(db);

        await ledger.RecordIssueAsync(part, null, 5, userId, "job");
        Assert.Equal(-3, part.QuantityOnHand);
        await ledger.RecordAdjustmentAsync(part, null, -1, userId, "count");
        Assert.Equal(-4, part.QuantityOnHand);

        await Assert.ThrowsAsync<BusinessRuleException>(() => ledger.RecordTransferOutAsync(part, branch, 1, userId, "move"));
    }

    [Theory]
    [InlineData(null, 600, "pending_approval")]
    [InlineData(null, 400, "issued")]
    [InlineData("""{"requireApprovalAboveValue":0}""", 600, "issued")]
    public async Task IssuesAboveTheApprovalValueWaitForApproval(string? settingsJson, double unitCost, string expectedStatus)
    {
        await using var db = Db();
        // Default limit is 50 000; 100 × unit cost sits either side of it.
        var part = await SeedPart(db, settingsJson, onHand: 200, unitCost: (decimal)unitCost);
        var controller = new MaterialIssuesController(db, new FakeTenantContext(tenantId, userId), new NoOpUserAccessService(),
            new NoOpBranchAccessService(), new NoOpDocumentNumberingService(), new StockLedgerService(db), new NoOpAuditLogService());

        var result = await controller.Create(new(null, null, null, null, null, null, null, null, [new(part.Id, 100, null, null)]), default);

        var issue = Assert.IsType<MaterialIssueResponse>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.Equal(expectedStatus, issue.IssueStatus);
        Assert.Equal(expectedStatus == "issued" ? 100 : 200, part.QuantityOnHand);
    }

    [Fact]
    public async Task DispatchingAPartsRequestTakesStockAndBooksItOnTheJob()
    {
        await using var db = Db();
        var part = await SeedPart(db, onHand: 10, unitCost: 25);
        var workOrder = new WorkOrder { TenantId = tenantId, WorkOrderNumber = "WO-7" };
        var request = new PartsRequest
        {
            TenantId = tenantId, WorkOrderId = workOrder.Id, TechnicianId = userId, Status = "Approved",
            Items =
            [
                new PartsRequestItem { InventoryItemId = part.Id, QuantityRequested = 4, QuantityApproved = 3 },
                new PartsRequestItem { Description = "Cable ties (buy locally)", QuantityRequested = 1, QuantityApproved = 1 },
            ],
        };
        db.WorkOrders.Add(workOrder);
        db.PartsRequests.Add(request);
        await db.SaveChangesAsync();
        var controller = new PartsRequestsController(db, new FakeTenantContext(tenantId, userId), new NoOpAuditLogService(),
            new NoOpUserAccessService(), new NoOpBranchAccessService(), new StockLedgerService(db));

        await controller.Dispatch(request.Id, default);

        Assert.Equal("Dispatched", request.Status);
        Assert.Equal(7, part.QuantityOnHand);
        var usage = await db.WorkOrderMaterialUsages.SingleAsync();
        Assert.Equal(3, usage.QuantityUsed);
        Assert.Equal(25, usage.UnitCost);
        Assert.Equal("WO-7", (await db.StockMovements.SingleAsync()).ReferenceNumber);
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Dispatch(request.Id, default));
        Assert.Equal(7, part.QuantityOnHand);
    }

    [Fact]
    public async Task PurchaseOrderWithoutAnExpectedDateUsesTheReorderLeadTime()
    {
        await using var db = Db();
        await SeedPart(db, """{"defaultReorderLeadDays":10}""");
        var supplier = new Supplier { TenantId = tenantId, SupplierName = "Acme" };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();
        var controller = new PurchaseOrdersController(db, new FakeTenantContext(tenantId, userId), new NoOpDocumentNumberingService(),
            new NoOpAuditLogService(), new NoApprovals(), new NoOpUserAccessService(), new StockLedgerService(db), new NoOpBranchAccessService());

        var result = await controller.CreatePurchaseOrder(new(supplier.Id, null, [new("Seal", 1, 5)]), default);

        var order = Assert.IsType<PurchaseOrderDetailResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(DateTime.UtcNow.Date.AddDays(10), order.ExpectedAt);
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
            Task.FromResult("TEST-0001");
        public Task<NumberingSetting> UpsertAsync(Guid tenantId, Guid? branchId, string documentType, string prefix, long nextNumber, int paddingLength, string resetFrequency, bool includeYear, bool includeMonth, bool isActive, CancellationToken cancellationToken = default, string? suffix = null, string yearFormat = "YYYY", string separator = "-", bool isLocked = false) =>
            Task.FromException<NumberingSetting>(new NotSupportedException());
    }
    private sealed class NoOpAuditLogService : IAuditLogService
    {
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, string severity = "Info", string? actorName = null, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
