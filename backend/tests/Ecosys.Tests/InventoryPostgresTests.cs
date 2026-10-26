using Ecosys.Api.Common;
using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ecosys.Tests;

/// <summary>
/// The inventory queries against real PostgreSQL, on a database built by the migrations (so the
/// inventory migrations and their backfills run too). The in-memory provider accepts LINQ that
/// PostgreSQL cannot translate; this catches it. Needs a local server (ECOSYS_TEST_DB_CONNECTION,
/// default localhost:5432 postgres/postgres).
/// </summary>
public sealed class InventoryPostgresTests
{
    private readonly Guid tenantId = Guid.NewGuid();
    private readonly Guid userId = Guid.NewGuid();

    [Fact]
    public async Task Postgres_InventoryFlowsAndReports_AllTranslate()
    {
        await WithPostgresAsync(async connection =>
        {
            await using var db = Context(connection);
            var branch = new Branch { TenantId = tenantId, Name = "Main", Code = "M" };
            var van = new Van { TenantId = tenantId, Name = "Van 1", HomeBranchId = branch.Id };
            var part = new MaterialItem { TenantId = tenantId, ItemCode = "F", ItemName = "Filter", Category = "Filters", UnitOfMeasure = "each", ReorderLevel = 20, UnitCost = 10 };
            db.Tenants.Add(new Tenant { Id = tenantId, Name = "Acme", Slug = $"acme-{Guid.NewGuid():N}"[..12], CompanyName = "Acme", Email = "ops@acme.test", Country = "Kenya" });
            db.Users.Add(new User { Id = userId, TenantId = tenantId, FullName = "Storekeeper", Email = $"store-{Guid.NewGuid():N}@acme.test", Role = AppRoles.Admin, PasswordHash = string.Empty, IsActive = true });
            db.AddRange(branch, van, part);
            await db.SaveChangesAsync();

            var branches = new Branches(branch.Id);
            var ledger = new StockLedgerService(db);
            var numbers = new Numbers();
            var purchasing = new PurchaseOrdersController(db, new TestTenant(tenantId, userId), numbers, new NoAudit(), new NoApprovals(), new AllowAll(), ledger, branches);
            var materials = new MaterialsController(db, new TestTenant(tenantId, userId), ledger, new AllowAll(), new NoAudit(), branches);
            var movements = new StockMovementsController(db, new TestTenant(tenantId, userId), new AllowAll(), branches, new Zone());
            var counts = new StockCountsController(db, new TestTenant(tenantId, userId), new AllowAll(), branches, ledger, numbers, new NoAudit());
            var reports = new InventoryReportsController(db, new TestTenant(tenantId, userId), new AllowAll(), branches, new Zone());

            // Buy, receive into the branch, load a van, use some, adjust.
            var supplier = Value(await purchasing.CreateSupplier(new("Acme Parts", LeadTimeDays: 5), default));
            await materials.Update(part.Id, new("F", "Filter", "Filters", "each", 0, 20, 10, null, "123456", supplier.Id, 12), default);
            var order = Value(await purchasing.CreatePurchaseOrder(new(supplier.Id, null, [new(null, 30, 12, part.Id)]), default));
            await purchasing.SetStatus(order.Id, new("Sent"), default);
            await purchasing.ReceiveLine(order.Id, new(order.Lines[0].Id, 30, branch.Id, "LOT-1"), default);
            await materials.Replenish(part.Id, new(branch.Id, 2, null, "Top-up", null), default);
            await ledger.ExecuteAsync(async ct =>
            {
                await ledger.RecordVanLoadAsync(part, branch.Id, van.Id, 5, userId, "Load", cancellationToken: ct);
                await db.SaveChangesAsync(ct);
            });
            await ledger.ExecuteAsync(async ct =>
            {
                await ledger.RecordVanConsumeAsync(part, van.Id, 2, userId, "Fitted", cancellationToken: ct);
                await db.SaveChangesAsync(ct);
            });
            await materials.Adjust(part.Id, new(branch.Id, -1, "Damaged"), default);

            // Reads used by the screens.
            Assert.Single(Value(await materials.GetBranchSummary(default)));
            Assert.Single(Value(await materials.GetStockLocations(part.Id, default)).Vans);
            Assert.NotEmpty(Value(await materials.GetMovements(part.Id, null, default)));
            Assert.Single(Value(await materials.GetAll(false, null, "123456", null, default)));
            Assert.NotNull(Value(await purchasing.GetSupplier(supplier.Id, default)).PreferredParts);
            Assert.NotEmpty(Value(await purchasing.ListSuppliers(default)));

            var ledgerPage = Value(await movements.List(null, null, null, null, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, "lot", 1, 50, default));
            Assert.Equal(1, ledgerPage.TotalCount);
            // PO receipt, replenish, van load (branch and van sides), van use, adjustment.
            Assert.Equal(6, Value(await movements.List(null, branch.Id, null, null, null, null, null, 1, 50, default)).TotalCount);

            // Stock count: open, count, post.
            var count = Assert.IsType<StockCountDetailResponse>(Assert.IsType<CreatedAtActionResult>((await counts.Create(new(branch.Id), default)).Result).Value);
            await counts.SaveLines(count.Id, new([new(count.Lines[0].Id, count.Lines[0].ExpectedQuantity - 2, "2 missing")]), default);
            Assert.Single(Value(await counts.List("Open", default)));
            await counts.Post(count.Id, default);

            // Reorder: on hand is now 24 - 2 = 22 at the branch, above 20; push it below and reorder.
            await materials.Adjust(part.Id, new(branch.Id, -10, "Written off"), default);
            var suggestions = Value(await purchasing.GetReorderSuggestions(branch.Id, default));
            Assert.Equal(12, Assert.Single(suggestions).SuggestedQuantity);
            Assert.Single(Value(await purchasing.GetReorderSuggestions(null, default)));
            Assert.Single(Value(await purchasing.CreateFromSuggestions(new([new(part.Id, 12)]), default)));

            // Reports.
            Assert.IsType<StockValuationResponse>(Assert.IsType<OkObjectResult>(await reports.Valuation(null, null, default)).Value);
            var usage = Assert.IsType<StockUsageResponse>(Assert.IsType<OkObjectResult>(await reports.Usage(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, null, null, default)).Value);
            Assert.Equal(2, Assert.Single(usage.Rows).QuantityUsed);
            Assert.IsType<FileContentResult>(await reports.Usage(null, null, branch.Id, "csv", default));
            Assert.IsType<FileContentResult>(await reports.Movements(null, null, null, null, null, null, null, default));
        });
    }

    private static T Value<T>(ActionResult<T> result) => Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private AppDbContext Context(string connection) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options, new TestTenant(tenantId, userId));

    private static async Task WithPostgresAsync(Func<string, Task> body)
    {
        var server = Environment.GetEnvironmentVariable("ECOSYS_TEST_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=ecosys_test;Username=postgres;Password=postgres";
        var databaseName = $"ecosys_inventory_{Guid.NewGuid():N}";
        var database = new NpgsqlConnectionStringBuilder(server) { Database = databaseName, Pooling = false }.ConnectionString;
        var admin = new NpgsqlConnectionStringBuilder(server) { Database = "postgres", Pooling = false }.ConnectionString;

        await ExecuteAdminAsync(admin, $"CREATE DATABASE \"{databaseName}\"");
        try
        {
            await using (var setup = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(database).Options))
                await setup.Database.MigrateAsync();
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

    private sealed class Numbers : IDocumentNumberingService
    {
        private int next;
        public Task<string> GenerateAsync(Guid tenantId, Guid? branchId, string documentType, CancellationToken cancellationToken = default) =>
            Task.FromResult($"{documentType}-{Interlocked.Increment(ref next):0000}");
        public Task<NumberingSetting> UpsertAsync(Guid tenantId, Guid? branchId, string documentType, string prefix, long nextNumber, int paddingLength, string resetFrequency, bool includeYear, bool includeMonth, bool isActive, CancellationToken cancellationToken = default, string? suffix = null, string yearFormat = "YYYY", string separator = "-", bool isLocked = false) =>
            throw new NotSupportedException();
    }
    private sealed class Branches(Guid branchId) : IBranchAccessService
    {
        public Task<BranchQueryScope> GetQueryScopeAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new BranchQueryScope(true, requestedBranchId, [branchId], false));
        public Task<Guid?> ResolveBranchIdForWriteAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default) => Task.FromResult<Guid?>(requestedBranchId ?? branchId);
        public Task EnsureCanAccessBranchAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyCollection<Guid>> GetAccessibleBranchIdsAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<Guid>>([branchId]);
    }
    private sealed class Zone : ITenantTimeZoneService
    {
        private static readonly TimeZoneInfo Nairobi = TimeZoneInfo.FindSystemTimeZoneById("Africa/Nairobi");
        public Task<string> GetPlatformTimeZoneIdAsync(CancellationToken cancellationToken = default) => Task.FromResult(Nairobi.Id);
        public Task<string> GetTimeZoneIdAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(Nairobi.Id);
        public Task<TimeZoneInfo> GetTimeZoneAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(Nairobi);
        public Task<DateTime> ToTenantTimeAsync(DateTime utcValue, Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(TimeZoneInfo.ConvertTimeFromUtc(utcValue, Nairobi));
        public Task<DateTime> TenantTodayAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Nairobi).Date);
        public Task<DateTime> TenantDayEndUtcAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> FormatForTenantAsync(DateTime utcValue, Guid tenantId, string format = "dd MMM yyyy 'at' HH:mm", CancellationToken cancellationToken = default) => Task.FromResult(utcValue.ToString(format));
        public string? NormalizeTimeZoneId(string? timeZoneId) => timeZoneId;
        public void Invalidate(Guid? tenantId = null) { }
    }
    private sealed class NoApprovals : IApprovalWorkflowService
    {
        public Task<ApprovalStartResult?> StartAsync(Guid tenantId, string documentType, Guid documentId, string documentNumber, decimal amount, string currency, Guid initiatedByUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ApprovalStartResult?>(null);
        public Task DecideAsync(Guid tenantId, Guid requestId, bool approve, string? note, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ApprovalRequest?> GetActiveForDocumentAsync(Guid tenantId, string documentType, Guid documentId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ApprovalRequest?>(null);
    }
    private sealed class TestTenant(Guid tenantId, Guid userId) : ITenantContext
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
    private sealed class AllowAll : IUserAccessService
    {
        public void EnsureAdmin() { }
        public void EnsureAdminOrPermission(string permissionName) { }
        public void EnsureAdminOrAnyPermission(params string[] permissionNames) { }
        public void EnsureTenantOperationalAccess() { }
    }
    private sealed class NoAudit : IAuditLogService
    {
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, string severity = "Info", string? actorName = null, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
