using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

/// <summary>
/// The inventory reorganisation: stock read by location (branches and vans), one low-stock rule,
/// manual receipts that explain themselves, partial issues, and the single requests queue.
/// </summary>
public sealed class InventoryReorganisationTests
{
    private readonly Guid tenantId = Guid.NewGuid();
    private readonly Guid userId = Guid.NewGuid();
    private readonly Guid branchId = Guid.NewGuid();
    private readonly Guid otherBranchId = Guid.NewGuid();

    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private MaterialsController Materials(AppDbContext db) => new(db, new FakeTenantContext(tenantId, userId),
        new StockLedgerService(db), new NoOpUserAccessService(), new NoOpAuditLogService(), new BranchScope([branchId, otherBranchId]));

    private MaterialIssuesController Issues(AppDbContext db) => new(db, new FakeTenantContext(tenantId, userId), new NoOpUserAccessService(),
        new BranchScope([branchId, otherBranchId]), new NoOpDocumentNumberingService(), new StockLedgerService(db), new NoOpAuditLogService());

    private InventoryRequestsController Queue(AppDbContext db) => new(db, new FakeTenantContext(tenantId, userId), new NoOpUserAccessService(),
        new BranchScope([branchId, otherBranchId]));

    private static T Ok<T>(ActionResult<T> result) => Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private async Task<(MaterialItem Filter, MaterialItem Belt, MaterialItem Untouched, Van Van)> Seed(AppDbContext db)
    {
        db.Users.Add(new User { Id = userId, TenantId = tenantId, FullName = "Storekeeper", Email = "store@tenant.test", Role = AppRoles.Admin, PasswordHash = string.Empty, IsActive = true });
        db.Branches.AddRange(
            new Branch { Id = branchId, TenantId = tenantId, Name = "Nairobi", Code = "NBO", IsActive = true },
            new Branch { Id = otherBranchId, TenantId = tenantId, Name = "Mombasa", Code = "MBA", IsActive = true });
        var filter = new MaterialItem { TenantId = tenantId, ItemCode = "F", ItemName = "Filter", UnitOfMeasure = "each", UnitCost = 10, ReorderLevel = 0 };
        var belt = new MaterialItem { TenantId = tenantId, ItemCode = "B", ItemName = "Belt", UnitOfMeasure = "each", UnitCost = 50 };
        var untouched = new MaterialItem { TenantId = tenantId, ItemCode = "U", ItemName = "Never stocked", UnitOfMeasure = "each", UnitCost = 1 };
        var van = new Van { TenantId = tenantId, Name = "Van 1", HomeBranchId = branchId };
        db.MaterialItems.AddRange(filter, belt, untouched);
        db.Vans.Add(van);
        await db.SaveChangesAsync();

        var ledger = new StockLedgerService(db);
        await ledger.RecordReceiptAsync(filter, branchId, 10, userId, "Opening", null);
        await db.SaveChangesAsync();
        await ledger.RecordReceiptAsync(filter, otherBranchId, 4, userId, "Opening", null);
        await db.SaveChangesAsync();
        await ledger.RecordReceiptAsync(belt, branchId, 1, userId, "Opening", null);
        await db.SaveChangesAsync();
        await ledger.RecordVanLoadAsync(filter, branchId, van.Id, 3, userId, "Load", null);
        await db.SaveChangesAsync();
        return (filter, belt, untouched, van);
    }

    [Fact]
    public async Task StockByLocation_ListsEachBranchAndVanBalance_AndSkipsPartsNeverStockedThere()
    {
        await using var db = Db();
        var (filter, belt, untouched, van) = await Seed(db);
        // Belt is low at Nairobi (1 on hand, reorder level 2); the branch row carries the level.
        (await db.BranchMaterialStocks.SingleAsync(x => x.MaterialId == belt.Id)).ReorderLevel = 2;
        await db.SaveChangesAsync();

        var rows = Ok(await Materials(db).GetStockByLocation(default));

        Assert.DoesNotContain(rows, x => x.MaterialId == untouched.Id);
        var filterRows = rows.Where(x => x.MaterialId == filter.Id).ToList();
        Assert.Equal(7m, filterRows.Single(x => x.LocationId == branchId).QuantityOnHand);
        Assert.Equal(4m, filterRows.Single(x => x.LocationId == otherBranchId).QuantityOnHand);
        var onVan = filterRows.Single(x => x.LocationType == StockLocationTypes.Van);
        Assert.Equal(van.Id, onVan.LocationId);
        Assert.Equal(3m, onVan.QuantityOnHand);
        Assert.Equal("Nairobi", filterRows.Single(x => x.LocationId == branchId).LocationName);
        Assert.True(rows.Single(x => x.MaterialId == belt.Id).IsLowStock);
        Assert.DoesNotContain(filterRows, x => x.IsLowStock);
    }

    [Fact]
    public void LowStock_IgnoresPartsWithNothingOnHandAndNoReorderLevel()
    {
        Assert.False(StockRules.IsLow(0, 0));
        Assert.True(StockRules.IsLow(0, 1));
        Assert.True(StockRules.IsLow(2, 2));
        Assert.False(StockRules.IsLow(3, 2));
        Assert.True(StockRules.IsLow(-1, 0));
    }

    [Fact]
    public async Task ManualReceipt_NeedsAReason()
    {
        await using var db = Db();
        var (filter, _, _, _) = await Seed(db);
        var controller = Materials(db);

        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Replenish(filter.Id, new(branchId, 5, null, " ", null), default));
        Ok(await controller.Replenish(filter.Id, new(branchId, 5, 12, "Found in old store room", "DN-1"), default));

        var receipt = await db.StockMovements.OrderBy(x => x.CreatedAt).LastAsync(x => x.MaterialId == filter.Id && x.MovementType == "Receipt");
        Assert.Equal("Found in old store room", receipt.Reason);
        Assert.Equal(12m, (await db.BranchMaterialStocks.SingleAsync(x => x.BranchId == branchId && x.MaterialId == filter.Id)).QuantityOnHand);
    }

    [Fact]
    public async Task IssueSlip_CanHandOverLessThanAsked_AndLeaveALineOut()
    {
        await using var db = Db();
        var (filter, belt, _, _) = await Seed(db);
        var issue = new MaterialIssue
        {
            TenantId = tenantId, BranchId = branchId, IssueNumber = "MI-1", IssueStatus = "approved", RequestedByUserId = userId,
            Lines =
            {
                new MaterialIssueLine { TenantId = tenantId, MaterialItemId = filter.Id, QuantityRequested = 5, Unit = "each", UnitCost = 10 },
                new MaterialIssueLine { TenantId = tenantId, MaterialItemId = belt.Id, QuantityRequested = 1, Unit = "each", UnitCost = 50 },
            },
        };
        db.MaterialIssues.Add(issue);
        await db.SaveChangesAsync();
        var filterLine = issue.Lines.Single(x => x.MaterialItemId == filter.Id);
        var beltLine = issue.Lines.Single(x => x.MaterialItemId == belt.Id);
        var controller = Issues(db);

        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Issue(issue.Id, new([new(filterLine.Id, 6)]), default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Issue(issue.Id, new([new(filterLine.Id, 0), new(beltLine.Id, 0)]), default));
        var issued = Ok(await controller.Issue(issue.Id, new([new(filterLine.Id, 2), new(beltLine.Id, 0)]), default));

        Assert.Equal("issued", issued.IssueStatus);
        Assert.Equal(5m, (await db.BranchMaterialStocks.SingleAsync(x => x.BranchId == branchId && x.MaterialId == filter.Id)).QuantityOnHand);
        Assert.Equal(1m, (await db.BranchMaterialStocks.SingleAsync(x => x.BranchId == branchId && x.MaterialId == belt.Id)).QuantityOnHand);
        Assert.Equal(20m, issued.TotalIssuedCost);
    }

    [Fact]
    public async Task Queue_ListsJobPartsRequestsIssueSlipsAndRetiredRestockRequestsWithOneSetOfStages()
    {
        await using var db = Db();
        var (filter, belt, _, _) = await Seed(db);
        var workOrder = new WorkOrder { TenantId = tenantId, WorkOrderNumber = "WO-9" };
        db.WorkOrders.Add(workOrder);
        var workOrderId = workOrder.Id;
        db.PartsRequests.Add(new PartsRequest
        {
            TenantId = tenantId, BranchId = branchId, WorkOrderId = workOrderId, TechnicianId = userId, Status = "Approved",
            Items = { new PartsRequestItem { InventoryItemId = filter.Id, QuantityRequested = 3, QuantityApproved = 2 } },
        });
        db.MaterialIssues.Add(new MaterialIssue
        {
            TenantId = tenantId, BranchId = branchId, IssueNumber = "MI-7", IssueStatus = "pending_approval", RequestedByUserId = userId,
            Lines = { new MaterialIssueLine { TenantId = tenantId, MaterialItemId = belt.Id, QuantityRequested = 1, Unit = "each", UnitCost = 50 } },
        });
        db.MaterialRequests.Add(new MaterialRequest
        {
            TenantId = tenantId, BranchId = branchId, WorkOrderId = workOrderId, RequestedByUserId = userId, RequestNumber = "MR-3", Status = "Closed",
            Lines = { new MaterialRequestLine { MaterialItemId = filter.Id, QuantityRequested = 1, QuantityIssued = 1, QuantityUsed = 1 } },
        });
        await db.SaveChangesAsync();

        var rows = Ok(await Queue(db).List(null, default));

        Assert.Equal(3, rows.Count);
        var parts = rows.Single(x => x.Source == InventoryRequestSources.PartsRequest);
        Assert.Equal(InventoryRequestStages.Approved, parts.Stage);
        Assert.Null(parts.Number);
        Assert.Equal("WO-9", parts.WorkOrderNumber);
        Assert.Equal(2m, parts.Lines.Single().QuantityApproved);
        Assert.Equal(20m, parts.Value);
        var slip = rows.Single(x => x.Source == InventoryRequestSources.IssueSlip);
        Assert.Equal(InventoryRequestStages.Requested, slip.Stage);
        Assert.Equal("MI-7", slip.Number);
        Assert.Equal(InventoryRequestStages.Completed, rows.Single(x => x.Source == InventoryRequestSources.Restock).Stage);
    }

    [Fact]
    public async Task APartWithStockOrOpenWorkCannotBeDeactivated()
    {
        await using var db = Db();
        var (filter, _, untouched, _) = await Seed(db);
        var controller = Materials(db);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Delete(filter.Id, default));
        Assert.Contains("on vans", error.Message);

        db.MaterialIssues.Add(new MaterialIssue
        {
            TenantId = tenantId, BranchId = branchId, IssueNumber = "MI-9", IssueStatus = "pending_approval", RequestedByUserId = userId,
            Lines = { new MaterialIssueLine { TenantId = tenantId, MaterialItemId = untouched.Id, QuantityRequested = 1, Unit = "each" } },
        });
        await db.SaveChangesAsync();
        Assert.Contains("open request", (await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Delete(untouched.Id, default))).Message);

        (await db.MaterialIssues.SingleAsync(x => x.IssueNumber == "MI-9")).IssueStatus = "cancelled";
        await db.SaveChangesAsync();
        Assert.IsType<NoContentResult>(await controller.Delete(untouched.Id, default));
        Assert.False((await db.MaterialItems.SingleAsync(x => x.Id == untouched.Id)).IsActive);
    }

    [Fact]
    public void RestockRequests_AreRetired()
    {
        var controller = new MaterialRequestsController(Db(), new FakeTenantContext(tenantId, userId), new NoOpAuditLogService(),
            new StockLedgerService(Db()), new NoOpUserAccessService(), new BranchScope([branchId]), new NoOpDocumentNumberingService());
        Assert.Throws<BusinessRuleException>(() => controller.Create(new(Guid.NewGuid(), [new(Guid.NewGuid(), 1)])));
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

    /// <summary>A tenant with branches, all of which the user can reach.</summary>
    private sealed class BranchScope(IReadOnlyCollection<Guid> branches) : IBranchAccessService
    {
        public Task<BranchQueryScope> GetQueryScopeAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new BranchQueryScope(true, requestedBranchId, branches, true));
        public Task<Guid?> ResolveBranchIdForWriteAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(requestedBranchId ?? branches.First());
        public Task EnsureCanAccessBranchAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyCollection<Guid>> GetAccessibleBranchIdsAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(branches);
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
