using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

/// <summary>Stocktakes snapshot the books, record counts, and post variances as adjustments.</summary>
public sealed class StockCountTests
{
    private readonly Guid tenantId = Guid.NewGuid();
    private readonly Guid userId = Guid.NewGuid();
    private readonly Guid branchId = Guid.NewGuid();

    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private async Task<(MaterialItem Filter, MaterialItem Belt)> Seed(AppDbContext db)
    {
        db.Users.Add(new User { Id = userId, TenantId = tenantId, FullName = "Storekeeper", Email = "store@tenant.test", Role = AppRoles.Admin, PasswordHash = string.Empty, IsActive = true });
        db.Branches.Add(new Branch { Id = branchId, TenantId = tenantId, Name = "Main", Code = "M" });
        var filter = new MaterialItem { TenantId = tenantId, ItemCode = "F", ItemName = "Filter", Category = "Filters", UnitOfMeasure = "each", UnitCost = 10 };
        var belt = new MaterialItem { TenantId = tenantId, ItemCode = "B", ItemName = "Belt", Category = "Belts", UnitOfMeasure = "each", UnitCost = 50 };
        db.MaterialItems.AddRange(filter, belt);
        await db.SaveChangesAsync();
        var ledger = new StockLedgerService(db);
        await ledger.RecordReceiptAsync(filter, branchId, 10, userId, "Opening", null);
        await db.SaveChangesAsync();
        await ledger.RecordReceiptAsync(belt, branchId, 4, userId, "Opening", null);
        await db.SaveChangesAsync();
        return (filter, belt);
    }

    private StockCountsController Controller(AppDbContext db) => new(db, new FakeTenantContext(tenantId, userId), new NoOpUserAccessService(),
        new NoOpBranchAccessService(), new StockLedgerService(db), new NoOpDocumentNumberingService(), new NoOpAuditLogService());

    private static StockCountDetailResponse Created(ActionResult<StockCountDetailResponse> result) =>
        Assert.IsType<StockCountDetailResponse>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
    private static StockCountDetailResponse Ok(ActionResult<StockCountDetailResponse> result) =>
        Assert.IsType<StockCountDetailResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);

    [Fact]
    public async Task PostingAdjustsCountedVariancesAgainstTheSnapshotAndKeepsLaterMovements()
    {
        await using var db = Db();
        var (filter, belt) = await Seed(db);
        var controller = Controller(db);
        var count = Created(await controller.Create(new(branchId), default));
        Assert.Equal([4m, 10m], count.Lines.Select(x => x.ExpectedQuantity));

        // Two filters go out while the count is open; the count is of the shelf before that.
        await new StockLedgerService(db).RecordIssueAsync(filter, branchId, 2, userId, "Job");
        await db.SaveChangesAsync();

        var filterLine = count.Lines.Single(x => x.MaterialItemId == filter.Id);
        var saved = Ok(await controller.SaveLines(count.Id, new([new(filterLine.Id, 9, "1 missing")]), default));
        Assert.Equal(-10m, saved.NetVarianceValue);

        var posted = Ok(await controller.Post(count.Id, default));

        Assert.Equal(StockCountStatuses.Posted, posted.Status);
        var filterStock = await db.BranchMaterialStocks.SingleAsync(x => x.MaterialId == filter.Id);
        Assert.Equal(7, filterStock.QuantityOnHand);
        Assert.Equal(4, (await db.BranchMaterialStocks.SingleAsync(x => x.MaterialId == belt.Id)).QuantityOnHand);
        var adjustment = await db.StockMovements.SingleAsync(x => x.MovementType == "Adjustment");
        Assert.Equal(-1, adjustment.QuantityChange);
        Assert.Equal("TEST-0001", adjustment.ReferenceNumber);
        Assert.Contains("1 missing", adjustment.Reason);

        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Post(count.Id, default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.SaveLines(count.Id, new([new(filterLine.Id, 5)]), default));
        Assert.Single(db.StockMovements.Where(x => x.MovementType == "Adjustment"));
    }

    [Fact]
    public async Task CategoryScopesTheCountAndOneOpenCountPerBranch()
    {
        await using var db = Db();
        await Seed(db);
        var controller = Controller(db);

        var count = Created(await controller.Create(new(branchId, "Belts"), default));

        Assert.Equal("Belt", Assert.Single(count.Lines).ItemName);
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Create(new(branchId), default));
        await controller.Cancel(count.Id, default);
        Created(await controller.Create(new(branchId), default));
    }

    [Fact]
    public async Task NothingCountedCannotBePostedAndNegativeCountsAreRefused()
    {
        await using var db = Db();
        await Seed(db);
        var controller = Controller(db);
        var count = Created(await controller.Create(new(branchId), default));

        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Post(count.Id, default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.SaveLines(count.Id, new([new(count.Lines[0].Id, -1)]), default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.SaveLines(count.Id, new([new(Guid.NewGuid(), 1)]), default));
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
