using Ecosys.Api.Common;
using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

/// <summary>The tenant-wide movement feed and per-branch stock totals.</summary>
public sealed class StockLedgerFeedTests
{
    private readonly Guid tenantId = Guid.NewGuid();
    private readonly Guid userId = Guid.NewGuid();
    private readonly Guid mainBranch = Guid.NewGuid();
    private readonly Guid otherBranch = Guid.NewGuid();

    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private async Task<MaterialItem> Seed(AppDbContext db)
    {
        var part = new MaterialItem { TenantId = tenantId, ItemCode = "FLT-1", ItemName = "Air filter", UnitOfMeasure = "each", UnitCost = 10 };
        db.Branches.AddRange(
            new Branch { Id = mainBranch, TenantId = tenantId, Name = "Main", Code = "M" },
            new Branch { Id = otherBranch, TenantId = tenantId, Name = "Other", Code = "O" });
        db.Users.Add(new User { Id = userId, TenantId = tenantId, FullName = "Storekeeper", Email = "store@tenant.test", Role = AppRoles.Admin, PasswordHash = string.Empty, IsActive = true });
        db.MaterialItems.Add(part);
        await db.SaveChangesAsync();

        var ledger = new StockLedgerService(db);
        // Each action saves on its own, as each endpoint does.
        await ledger.RecordReceiptAsync(part, mainBranch, 10, userId, "Opening", "GRN-1");
        await db.SaveChangesAsync();
        await ledger.RecordAdjustmentAsync(part, mainBranch, -2, userId, "Damaged");
        await db.SaveChangesAsync();
        await ledger.RecordReceiptAsync(part, otherBranch, 5, userId, "Opening", "GRN-2");
        await db.SaveChangesAsync();
        return part;
    }

    private StockMovementsController Controller(AppDbContext db, IBranchAccessService branches, TimeZoneInfo? zone = null) =>
        new(db, new FakeTenantContext(tenantId, userId), new NoOpUserAccessService(), branches, new FixedZone(zone ?? TimeZoneInfo.Utc));

    private InventoryReportsController Reports(AppDbContext db) =>
        new(db, new FakeTenantContext(tenantId, userId), new NoOpUserAccessService(), new Branches(mainBranch, otherBranch), new FixedZone(TimeZoneInfo.Utc));

    private static PagedResult<StockLedgerEntryResponse> Page(ActionResult<PagedResult<StockLedgerEntryResponse>> result) =>
        Assert.IsType<PagedResult<StockLedgerEntryResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value);

    [Fact]
    public async Task FeedListsEveryMovementWithItsSignedChange()
    {
        await using var db = Db();
        await Seed(db);

        var page = Page(await Controller(db, new AllBranches()).List(null, null, null, null, null, null, 1, 50, default));

        Assert.Equal(3, page.TotalCount);
        var adjustment = page.Items.Single(x => x.MovementType == "Adjustment");
        Assert.Equal(-2, adjustment.QuantityChange);
        Assert.Equal(8, adjustment.BalanceAfter);
        Assert.Equal("Air filter", adjustment.ItemName);
        Assert.Equal("Storekeeper", adjustment.CreatedByName);
        Assert.DoesNotContain(page.Items, x => x.MovementType == "Issue");
    }

    [Fact]
    public async Task FiltersByTypeSearchAndDate()
    {
        await using var db = Db();
        await Seed(db);
        var controller = Controller(db, new AllBranches());

        Assert.Equal(2, Page(await controller.List(null, null, "Receipt", null, null, null, 1, 50, default)).TotalCount);
        Assert.Equal("GRN-2", Assert.Single(Page(await controller.List(null, null, null, null, null, "grn-2", 1, 50, default)).Items).ReferenceNumber);
        Assert.Equal(3, Page(await controller.List(null, null, null, DateTime.UtcNow.Date, DateTime.UtcNow.Date, null, 1, 50, default)).TotalCount);
        Assert.Equal(0, Page(await controller.List(null, null, null, DateTime.UtcNow.Date.AddDays(1), null, null, 1, 50, default)).TotalCount);
    }

    [Fact]
    public async Task BranchScopeKeepsOtherBranchesOut()
    {
        await using var db = Db();
        await Seed(db);

        var page = Page(await Controller(db, new OnlyBranch(mainBranch)).List(null, null, null, null, null, null, 1, 50, default));

        Assert.Equal(2, page.TotalCount);
        Assert.DoesNotContain(page.Items, x => x.BranchId == otherBranch);
    }

    [Fact]
    public async Task BranchSummaryCountsPartsLowStockAndValuePerBranch()
    {
        await using var db = Db();
        var part = await Seed(db);
        (await db.BranchMaterialStocks.SingleAsync(x => x.BranchId == otherBranch)).ReorderLevel = 6;
        await db.SaveChangesAsync();
        var controller = new MaterialsController(db, new FakeTenantContext(tenantId, userId), new StockLedgerService(db),
            new NoOpUserAccessService(), new NoOpAuditLogService(), new AllBranches());

        var rows = Assert.IsAssignableFrom<IReadOnlyCollection<BranchStockSummaryResponse>>(Assert.IsType<OkObjectResult>((await controller.GetBranchSummary(default)).Result).Value);

        var main = rows.Single(x => x.BranchId == mainBranch);
        Assert.Equal((1, 0, 80m), (main.PartsStocked, main.LowStockParts, main.StockValue));
        var other = rows.Single(x => x.BranchId == otherBranch);
        Assert.Equal((1, 1, 50m), (other.PartsStocked, other.LowStockParts, other.StockValue));
        Assert.Equal(10, part.UnitCost);
    }

    [Fact]
    public async Task DateFiltersAreWholeDaysInTheWorkspaceTimeZone()
    {
        await using var db = Db();
        await Seed(db);
        var nairobi = TimeZoneInfo.FindSystemTimeZoneById("Africa/Nairobi");
        // 22:30 UTC is 01:30 the next morning in Nairobi.
        var late = DateTime.UtcNow.Date.AddHours(22.5);
        foreach (var movement in db.StockMovements)
            movement.CreatedAt = late;
        await db.SaveChangesAsync();
        var localDay = TimeZoneInfo.ConvertTimeFromUtc(late, nairobi).Date;
        var controller = Controller(db, new AllBranches(), nairobi);

        Assert.Equal(3, Page(await controller.List(null, null, null, localDay, localDay, null, 1, 50, default)).TotalCount);
        Assert.Equal(0, Page(await controller.List(null, null, null, late.Date, late.Date, null, 1, 50, default)).TotalCount);
    }

    [Fact]
    public async Task ValuationCoversEveryBranchAtItsCost()
    {
        await using var db = Db();
        await Seed(db);

        var report = Assert.IsType<StockValuationResponse>(Assert.IsType<OkObjectResult>(await Reports(db).Valuation(null, null, default)).Value);

        Assert.Equal([("Main", "Branch", 8m), ("Other", "Branch", 5m)],
            report.Rows.Select(x => (x.Location, x.LocationType, x.Quantity)));
        Assert.Equal(130m, report.TotalValue);
    }

    [Fact]
    public async Task UsageCountsIssuesLessReturns()
    {
        await using var db = Db();
        var part = await Seed(db);
        var ledger = new StockLedgerService(db);
        var job = Guid.NewGuid();
        await ledger.RecordIssueAsync(part, mainBranch, 4, userId, "Job", workOrderId: job);
        await db.SaveChangesAsync();
        await ledger.RecordReturnAsync(part, mainBranch, 1, userId, "Unused", workOrderId: job);
        await db.SaveChangesAsync();

        var report = Assert.IsType<StockUsageResponse>(Assert.IsType<OkObjectResult>(await Reports(db).Usage(null, null, null, null, default)).Value);

        var row = Assert.Single(report.Rows);
        Assert.Equal((4m, 1m, 3m, 1), (row.QuantityUsed, row.QuantityReturned, row.NetQuantity, row.JobCount));
        Assert.Equal(30m, row.NetValue);
    }

    [Fact]
    public async Task MovementExportIsCsvWithSignedChanges()
    {
        await using var db = Db();
        await Seed(db);

        var file = Assert.IsType<FileContentResult>(await Reports(db).Movements(null, null, "Adjustment", null, null, null, default));

        Assert.Equal("text/csv", file.ContentType);
        var lines = System.Text.Encoding.UTF8.GetString(file.FileContents).Trim().Split('\n');
        Assert.StartsWith("When,Type,Code,Part,Change", lines[0]);
        var row = Assert.Single(lines.Skip(1));
        // A leading minus is neutralised against spreadsheet formula injection.
        Assert.Contains("\"'-2", row);
        Assert.Contains("\"Damaged\"", row);
    }

    private sealed class AllBranches : IBranchAccessService
    {
        public Task<BranchQueryScope> GetQueryScopeAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new BranchQueryScope(false, requestedBranchId, [], true));
        public Task<Guid?> ResolveBranchIdForWriteAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default) => Task.FromResult(requestedBranchId);
        public Task EnsureCanAccessBranchAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyCollection<Guid>> GetAccessibleBranchIdsAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<Guid>>([]);
    }
    /// <summary>A workspace with branches, all of them visible.</summary>
    private sealed class Branches(params Guid[] ids) : IBranchAccessService
    {
        public Task<BranchQueryScope> GetQueryScopeAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new BranchQueryScope(true, requestedBranchId, ids, false));
        public Task<Guid?> ResolveBranchIdForWriteAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default) => Task.FromResult(requestedBranchId);
        public Task EnsureCanAccessBranchAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyCollection<Guid>> GetAccessibleBranchIdsAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<Guid>>(ids);
    }
    private sealed class OnlyBranch(Guid branchId) : IBranchAccessService
    {
        public Task<BranchQueryScope> GetQueryScopeAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new BranchQueryScope(true, requestedBranchId, [branchId], false));
        public Task<Guid?> ResolveBranchIdForWriteAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default) => Task.FromResult<Guid?>(branchId);
        public Task EnsureCanAccessBranchAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyCollection<Guid>> GetAccessibleBranchIdsAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<Guid>>([branchId]);
    }
    private sealed class FixedZone(TimeZoneInfo zone) : ITenantTimeZoneService
    {
        public Task<string> GetPlatformTimeZoneIdAsync(CancellationToken cancellationToken = default) => Task.FromResult(zone.Id);
        public Task<string> GetTimeZoneIdAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(zone.Id);
        public Task<TimeZoneInfo> GetTimeZoneAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(zone);
        public Task<DateTime> ToTenantTimeAsync(DateTime utcValue, Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(TimeZoneInfo.ConvertTimeFromUtc(utcValue, zone));
        public Task<DateTime> TenantTodayAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone).Date);
        public Task<DateTime> TenantDayEndUtcAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> FormatForTenantAsync(DateTime utcValue, Guid tenantId, string format = "dd MMM yyyy 'at' HH:mm", CancellationToken cancellationToken = default) => Task.FromResult(utcValue.ToString(format));
        public string? NormalizeTimeZoneId(string? timeZoneId) => timeZoneId;
        public void Invalidate(Guid? tenantId = null) { }
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
    private sealed class NoOpAuditLogService : IAuditLogService
    {
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, string severity = "Info", string? actorName = null, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
