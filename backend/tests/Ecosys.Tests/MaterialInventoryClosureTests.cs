using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

public sealed class MaterialInventoryClosureTests
{
    private readonly Guid tenantId = Guid.NewGuid();
    private readonly Guid userId = Guid.NewGuid();

    private AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private IStockLedgerService Ledger(AppDbContext db) => (IStockLedgerService)Activator.CreateInstance(
        typeof(AppDbContext).Assembly.GetType("Ecosys.Infrastructure.Services.StockLedgerService")!, db)!;
    private MaterialIssuesController Issues(AppDbContext db, IBranchAccessService? branches = null) => new(db,
        new FakeTenantContext(tenantId, userId), new NoOpUserAccessService(), branches ?? new NoOpBranchAccessService(),
        new NoOpDocumentNumberingService(), Ledger(db), new NoOpAuditLogService());
    private MaterialRequestsController Requests(AppDbContext db) => new(db,
        new FakeTenantContext(tenantId, userId), new NoOpAuditLogService(), Ledger(db), new NoOpUserAccessService(),
        new NoOpBranchAccessService(), new NoOpDocumentNumberingService());

    private async Task<(MaterialIssue Issue, MaterialItem Item)> SeedIssue(AppDbContext db, string status = "approved", bool workOrder = false)
    {
        var item = new MaterialItem { TenantId = tenantId, ItemName = "Filter", ItemCode = "F1", QuantityOnHand = 20, UnitOfMeasure = "each" };
        var issue = new MaterialIssue { TenantId = tenantId, RequestedByUserId = userId, IssueNumber = "MI-1", IssueStatus = status,
            Lines = [new MaterialIssueLine { TenantId = tenantId, MaterialItemId = item.Id, QuantityRequested = 5, Unit = "each" }] };
        if (workOrder)
        {
            var wo = new WorkOrder { TenantId = tenantId, WorkOrderNumber = "WO-1" };
            db.WorkOrders.Add(wo);
            issue.WorkOrderId = wo.Id;
        }
        // RequestedByUserId is a required FK, so Query()'s Include(x => x.RequestedByUser) compiles to
        // an inner join — omitting the row it points at (as this seed previously did) silently drops
        // every issue from the result, which a real FK constraint would never allow in the first place.
        db.Users.Add(new User { Id = userId, TenantId = tenantId, FullName = "Requester", Email = "requester@tenant.test", Role = AppRoles.Admin, PasswordHash = string.Empty, IsActive = true });
        db.MaterialItems.Add(item);
        db.MaterialIssues.Add(issue);
        await db.SaveChangesAsync();
        return (issue, item);
    }

    [Theory]
    [InlineData("issued")]
    [InlineData("returned")]
    [InlineData("rejected")]
    [InlineData("cancelled")]
    public async Task TerminalIssueCannotBeReapprovedOrRejected(string status)
    {
        await using var db = Db();
        var (issue, _) = await SeedIssue(db, status);
        var controller = Issues(db);
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Approve(issue.Id, default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Reject(issue.Id, new("reason"), default));
        Assert.Equal(status, issue.IssueStatus);
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("pending_approval")]
    [InlineData("rejected")]
    public async Task NonApprovedIssueCannotDeductStock(string status)
    {
        await using var db = Db();
        var (issue, item) = await SeedIssue(db, status);
        await Assert.ThrowsAsync<BusinessRuleException>(() => Issues(db).Issue(issue.Id, null, default));
        Assert.Equal(20, item.QuantityOnHand);
        Assert.Empty(db.StockMovements);
    }

    [Fact]
    public async Task IssueReplayCannotDeductStockAgain_AndPartialReturnsReconcileUsage()
    {
        await using var db = Db();
        var (issue, item) = await SeedIssue(db, workOrder: true);
        var controller = Issues(db);
        await controller.Issue(issue.Id, null, default);
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Issue(issue.Id, null, default));
        Assert.Equal(15, item.QuantityOnHand);
        var line = issue.Lines.Single();
        var firstReturn = new ReturnMaterialIssueRequest([new(line.Id, 2, 0)]);
        await controller.Return(issue.Id, firstReturn, default);
        Assert.Equal("issued", issue.IssueStatus);
        Assert.Equal(17, item.QuantityOnHand);
        Assert.Equal(3, (await db.WorkOrderMaterialUsages.SingleAsync()).QuantityUsed);
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Return(issue.Id, firstReturn, default));
        Assert.Equal(17, item.QuantityOnHand);
        await controller.Return(issue.Id, new([new(line.Id, 3, 2)]), default);
        Assert.Equal("returned", issue.IssueStatus);
        Assert.Equal(20, item.QuantityOnHand);
        Assert.Equal(0, (await db.WorkOrderMaterialUsages.SingleAsync()).QuantityUsed);
        Assert.Equal(3, await db.StockMovements.CountAsync());
    }

    [Fact]
    public async Task MissingReturnVersionIsRejected()
    {
        await using var db = Db();
        var (issue, item) = await SeedIssue(db);
        var controller = Issues(db);
        await controller.Issue(issue.Id, null, default);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Return(issue.Id,
            new([new(issue.Lines.Single().Id, 1)]), default));
        Assert.Contains("version", error.Message);
        Assert.Equal(15, item.QuantityOnHand);
    }

    [Fact]
    public async Task InaccessibleBranchCannotBeMutated()
    {
        await using var db = Db();
        var (issue, _) = await SeedIssue(db, "pending_approval");
        issue.BranchId = Guid.NewGuid();
        await db.SaveChangesAsync();
        var controller = Issues(db, new DeniedBranches());
        await Assert.ThrowsAsync<NotFoundException>(() => controller.Approve(issue.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => controller.Reject(issue.Id, new("reason"), default));
        await Assert.ThrowsAsync<NotFoundException>(() => controller.Issue(issue.Id, null, default));
        await Assert.ThrowsAsync<NotFoundException>(() => controller.Return(issue.Id, new([]), default));
        await Assert.ThrowsAsync<NotFoundException>(() => controller.Cancel(issue.Id, default));
        Assert.Equal("pending_approval", issue.IssueStatus);
    }

    [Fact]
    public async Task RequestRequiresApprovalCapsQuantityAndCannotReplayClosure()
    {
        await using var db = Db();
        var item = new MaterialItem { TenantId = tenantId, ItemName = "Filter", QuantityOnHand = 20 };
        var wo = new WorkOrder { TenantId = tenantId, WorkOrderNumber = "WO-2" };
        var request = new MaterialRequest { TenantId = tenantId, WorkOrderId = wo.Id, RequestedByUserId = userId,
            RequestNumber = "MR-1", Status = "Pending", Lines = [new MaterialRequestLine { MaterialItemId = item.Id, QuantityRequested = 5 }] };
        // RequestedByUserId is a required FK — Query()'s Include(x => x.RequestedByUser) is an inner
        // join, so a real matching User row is needed, same as SeedIssue above.
        db.Users.Add(new User { Id = userId, TenantId = tenantId, FullName = "Requester", Email = "requester@tenant.test", Role = AppRoles.Admin, PasswordHash = string.Empty, IsActive = true });
        db.MaterialItems.Add(item); db.WorkOrders.Add(wo); db.MaterialRequests.Add(request); await db.SaveChangesAsync();
        var controller = Requests(db);
        var line = request.Lines.Single();
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Issue(request.Id, new([new(line.Id, 5)]), default));
        await controller.Approve(request.Id, default);
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Issue(request.Id, new([new(line.Id, 6)]), default));
        await controller.Issue(request.Id, new([new(line.Id, 5)]), default);
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Approve(request.Id, default));
        await controller.Close(request.Id, new([new(line.Id, 3, 2)]), default);
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Close(request.Id, new([new(line.Id, 3, 2)]), default));
        Assert.Equal(17, item.QuantityOnHand);
        Assert.Equal(3, (await db.WorkOrderMaterialUsages.SingleAsync()).QuantityUsed);
        Assert.Equal(2, await db.StockMovements.CountAsync());
    }

    private sealed class DeniedBranches : IBranchAccessService
    {
        public Task<BranchQueryScope> GetQueryScopeAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default) => Task.FromResult(new BranchQueryScope(true, null, [], false));
        public Task<Guid?> ResolveBranchIdForWriteAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task EnsureCanAccessBranchAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<Guid>> GetAccessibleBranchIdsAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<Guid>>([]);
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

        public Task EnsureCanAccessBranchAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

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
