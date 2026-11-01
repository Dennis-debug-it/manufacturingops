using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Domain.Enums;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

/// <summary>An asset shows which contract covers it, and can be added to one from the asset's side.</summary>
public sealed class AssetContractsTests
{
    private readonly Guid tenantId = Guid.NewGuid();
    private readonly Guid userId = Guid.NewGuid();

    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed record Fixture(Client Client, Site Site, Asset Asset, Contract ByAssets, Contract BySite, Contract ByCategory, Contract Terminated, WorkOrder OpenJob);

    private async Task<Fixture> Seed(AppDbContext db)
    {
        var client = new Client { TenantId = tenantId, ClientName = "Nairobi Hospital", IsActive = true };
        var site = new Site { TenantId = tenantId, ClientId = client.Id, SiteName = "Main Campus" };
        var asset = new Asset { TenantId = tenantId, ClientId = client.Id, SiteId = site.Id, AssetName = "Chiller 2", AssetCode = "CH-2", AssetType = "HVAC", Status = "Active" };
        var other = new Asset { TenantId = tenantId, ClientId = client.Id, AssetName = "Generator", AssetCode = "GEN-1", AssetType = "Power", Status = "Active" };
        Contract Make(string number, ContractStatus status) => new()
        {
            TenantId = tenantId, ClientId = client.Id, ContractNumber = number, Name = number, Status = status,
            StartDate = DateTime.UtcNow.Date.AddMonths(-1), EndDate = DateTime.UtcNow.Date.AddMonths(11),
        };
        var byAssets = Make("CT-ASSETS", ContractStatus.Active);
        byAssets.Assets.Add(new ContractAsset { TenantId = tenantId, AssetId = other.Id });
        var bySite = Make("CT-SITE", ContractStatus.Active);
        bySite.Sites.Add(new ContractSite { TenantId = tenantId, SiteId = site.Id });
        var byCategory = Make("CT-POWER", ContractStatus.Active);
        byCategory.AssetCategories.Add(new ContractAssetCategory { TenantId = tenantId, AssetCategoryName = "Power" });
        var terminated = Make("CT-OLD", ContractStatus.Terminated);
        terminated.Assets.Add(new ContractAsset { TenantId = tenantId, AssetId = other.Id });
        var job = new WorkOrder { TenantId = tenantId, ClientId = client.Id, AssetId = asset.Id, WorkOrderNumber = "WO-1", Status = "Open" };
        db.AddRange(client, site, asset, other, byAssets, bySite, byCategory, terminated, job);
        await db.SaveChangesAsync();
        return new Fixture(client, site, asset, byAssets, bySite, byCategory, terminated, job);
    }

    private AssetContractsController Controller(AppDbContext db, RecordingOperational? operational = null) =>
        new(db, new TenantContext(tenantId, userId), new AllowContracts(), new AllBranches(), operational ?? new RecordingOperational(), new NoAudit());

    [Fact]
    public async Task TheCoveringContractIsTheMostSpecificOne()
    {
        await using var db = Db();
        var f = await Seed(db);
        var contracts = await db.Contracts.Include(x => x.Assets).Include(x => x.Sites).Include(x => x.AssetCategories).ToListAsync();

        var bySite = AssetsController.FindCoveringContract(f.Asset, contracts);
        Assert.Equal(("CT-SITE", ContractCoverScopes.Site), (bySite!.Value.Contract.ContractNumber, bySite.Value.Scope));

        f.ByAssets.Assets.Add(new ContractAsset { TenantId = tenantId, AssetId = f.Asset.Id });
        var byAsset = AssetsController.FindCoveringContract(f.Asset, contracts);
        Assert.Equal(("CT-ASSETS", ContractCoverScopes.Asset), (byAsset!.Value.Contract.ContractNumber, byAsset.Value.Scope));

        var unrelated = new Asset { TenantId = tenantId, ClientId = Guid.NewGuid(), AssetName = "Elsewhere", AssetCode = "X" };
        Assert.Null(AssetsController.FindCoveringContract(unrelated, contracts));
    }

    [Fact]
    public async Task OptionsExplainEachContract_AndOnlyAnAssetListContractAcceptsTheAsset()
    {
        await using var db = Db();
        var f = await Seed(db);

        var options = Assert.IsAssignableFrom<IReadOnlyCollection<AssetContractOptionResponse>>(Assert.IsType<OkObjectResult>((await Controller(db).List(f.Asset.Id, default)).Result).Value);

        Assert.DoesNotContain(options, x => x.ContractId == f.Terminated.Id);
        var assets = options.Single(x => x.ContractId == f.ByAssets.Id);
        Assert.True(assets.CanAdd);
        Assert.False(assets.ScopeCoversAsset);
        var site = options.Single(x => x.ContractId == f.BySite.Id);
        Assert.True(site.ScopeCoversAsset);
        Assert.False(site.CanAdd);
        Assert.Contains("site", site.Reason);
        var category = options.Single(x => x.ContractId == f.ByCategory.Id);
        Assert.False(category.ScopeCoversAsset);
        Assert.False(category.CanAdd);
        Assert.Contains("category", category.Reason);
    }

    [Fact]
    public async Task AddingListsTheAssetOnce_RechecksItsOpenJobs_AndRefusesScopeNarrowingContracts()
    {
        await using var db = Db();
        var f = await Seed(db);
        var operational = new RecordingOperational();
        var controller = Controller(db, operational);

        var added = Assert.IsType<AssetContractOptionResponse>(Assert.IsType<OkObjectResult>((await controller.Add(f.Asset.Id, f.ByAssets.Id, default)).Result).Value);
        Assert.True(added.ScopeCoversAsset);
        Assert.False(added.CanAdd);
        Assert.Single(db.ContractAssets.Where(x => x.ContractId == f.ByAssets.Id && x.AssetId == f.Asset.Id));
        Assert.Equal([f.OpenJob.Id], operational.Recalculated);

        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Add(f.Asset.Id, f.ByAssets.Id, default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Add(f.Asset.Id, f.BySite.Id, default));
        Assert.Empty(db.ContractAssets.Where(x => x.ContractId == f.BySite.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => controller.Add(f.Asset.Id, f.Terminated.Id, default));
    }

    private sealed class RecordingOperational : IContractOperationalService
    {
        public List<Guid> Recalculated { get; } = [];
        public Task<ContractOperationalSnapshot> ApplyOperationalSnapshotAsync(WorkOrder workOrder, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ContractOperationalSnapshot> RecalculateWorkOrderAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default)
        {
            Recalculated.Add(workOrderId);
            return Task.FromResult<ContractOperationalSnapshot>(null!);
        }
        public Task IncrementPmEntitlementUsageIfNeededAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class AllowContracts : IContractAccessService
    {
        public bool CanViewContracts() => true;
        public bool CanManageContracts() => true;
        public bool CanManageLifecycle() => true;
        public bool CanManageRates() => true;
        public bool CanViewPricing() => true;
        public bool CanManageRenewals() => true;
        public void EnsureCanViewContracts() { }
        public void EnsureCanManageContracts() { }
        public void EnsureCanManageLifecycle() { }
        public void EnsureCanManageRates() { }
        public void EnsureCanViewPricing() { }
        public void EnsureCanManageRenewals() { }
    }

    private sealed class AllBranches : IBranchAccessService
    {
        public Task<BranchQueryScope> GetQueryScopeAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new BranchQueryScope(false, requestedBranchId, [], true));
        public Task<Guid?> ResolveBranchIdForWriteAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default) => Task.FromResult(requestedBranchId);
        public Task EnsureCanAccessBranchAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyCollection<Guid>> GetAccessibleBranchIdsAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<Guid>>([]);
    }

    private sealed class NoAudit : IAuditLogService
    {
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, string severity = "Info", string? actorName = null, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TenantContext(Guid tenantId, Guid userId) : ITenantContext
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
}
