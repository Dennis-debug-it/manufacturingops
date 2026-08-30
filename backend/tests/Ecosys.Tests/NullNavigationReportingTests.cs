using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

/// <summary>
/// Regression cover for EC-030. Four CS8602 "possibly null dereference" warnings sat on
/// <c>ThenInclude</c> lambdas over optional navigations (<c>WorkOrder.Asset</c>,
/// <c>PreventiveMaintenancePlan.Asset</c>). Those lambdas are expression trees that EF parses into
/// an include path and never invokes, so they were not the crash the warning implied — but the
/// null navigations they walk are genuinely reachable, and nothing proved the surrounding
/// projections coped. These tests drive both endpoints with the navigations actually null.
///
/// <c>Asset.AssetCategoryId</c>, <c>WorkOrder.SiteId</c> and <c>WorkOrder.AssetId</c> are all
/// nullable columns, so an uncategorised asset and a work order with neither site nor asset are
/// ordinary rows, not corrupt ones.
/// </summary>
public sealed class NullNavigationReportingTests
{
    [Fact]
    public async Task PmCompliance_WhenPlanAssetHasNoCategoryOrClient_ReturnsOkInsteadOfThrowing()
    {
        await using var dbContext = CreateDbContext();
        var (tenant, admin) = await SeedTenantAsync(dbContext);

        // Deliberately uncategorised and unsited. Those two FKs are nullable columns, so this is an
        // ordinary row and `Asset.AssetCategory` / `Asset.Site` really do resolve to null.
        //
        // The client, by contrast, is seeded for real: `Asset.ClientId` is a non-nullable Guid, so
        // the relationship is required and `.Include(x => x.Client)` compiles to an INNER JOIN — an
        // asset pointing at a client row that does not exist is not "a null navigation", it is a
        // corrupt row, and EF silently filters it out of the result entirely.
        var client = new Client
        {
            TenantId = tenant.Id,
            ClientName = "Acme Hospital",
            IsActive = true
        };
        dbContext.Clients.Add(client);
        await dbContext.SaveChangesAsync();

        var asset = new Asset
        {
            TenantId = tenant.Id,
            ClientId = client.Id,
            AssetCategoryId = null,
            SiteId = null,
            AssetName = "Unregistered chiller",
            AssetCode = "AST-ORPHAN-1"
        };
        dbContext.Assets.Add(asset);
        await dbContext.SaveChangesAsync();

        dbContext.PreventiveMaintenancePlans.Add(new PreventiveMaintenancePlan
        {
            TenantId = tenant.Id,
            AssetId = asset.Id,
            SiteId = null,
            Frequency = "Monthly",
            FrequencyUnit = "Monthly",
            Status = "Active",
            NextPmDate = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-10), DateTimeKind.Utc)
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateReportsController(dbContext, tenant.Id, admin.Id);

        var result = await controller.PmCompliance(
            DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-30), DateTimeKind.Utc),
            DateTime.SpecifyKind(DateTime.UtcNow.AddDays(1), DateTimeKind.Utc),
            null,
            null,
            null,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        Assert.Equal(1, ReadProperty<int>(ok.Value!, "ActivePlans"));
        Assert.Equal(1, ReadProperty<int>(ok.Value!, "Overdue"));

        // The overdue projection walks Asset?.AssetCategory and Asset?.Site, both null here. The
        // report must still render the row rather than throwing on the way through.
        var overdue = Assert.IsAssignableFrom<System.Collections.IEnumerable>(ReadProperty<object>(ok.Value!, "OverduePlans"));
        var rows = overdue.Cast<object>().ToList();
        var row = Assert.Single(rows);
        Assert.Equal("Acme Hospital", ReadProperty<string?>(row, "ClientName"));
        Assert.Equal("Unregistered chiller", ReadProperty<string?>(row, "AssetName"));
    }

    [Fact]
    public async Task DraftFromWorkOrder_WhenWorkOrderHasNoAssetSiteOrClient_SkipsNullTags()
    {
        await using var dbContext = CreateDbContext();
        var (tenant, admin) = await SeedTenantAsync(dbContext);

        // Real client for the same reason as above — WorkOrder.ClientId is non-nullable, so
        // `.Include(x => x.Client)` is an inner join and a dangling FK would drop the row.
        // SiteId and AssetId are nullable, so those navigations are legitimately null.
        var client = new Client
        {
            TenantId = tenant.Id,
            ClientName = "Acme Hospital",
            IsActive = true
        };
        dbContext.Clients.Add(client);
        await dbContext.SaveChangesAsync();

        var workOrder = new WorkOrder
        {
            TenantId = tenant.Id,
            ClientId = client.Id,
            SiteId = null,
            AssetId = null,
            WorkOrderNumber = "WO-NULLNAV-1",
            Title = "Reset tripped breaker",
            Status = "Completed",
            Priority = "Medium",
            WorkType = "CorrectiveMaintenance"
        };
        dbContext.WorkOrders.Add(workOrder);
        await dbContext.SaveChangesAsync();

        var controller = CreateKnowledgeController(dbContext, tenant.Id, admin.Id);

        // Tag sources are Asset?.AssetCategory?.Name, Client?.ClientName and Site?.SiteName. With no
        // site and no asset, two of the three are null — the CS8620 signature mismatch was real in
        // the sense that nulls genuinely arrive, and ReplaceTagsAsync must filter them out.
        var result = await controller.DraftFromWorkOrder(workOrder.Id, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var article = Assert.IsType<KnowledgeArticleDetailResponse>(created.Value);
        Assert.Equal("Draft", article.Status);
        Assert.Equal(workOrder.Id, article.SourceWorkOrderId);

        // Exactly the non-null sources survive: the client name and the fixed marker. No empty or
        // null-named tag rows were created from the missing site and asset.
        var tags = await dbContext.KnowledgeTags.Where(x => x.TenantId == tenant.Id).ToListAsync();
        Assert.Equal(
            new[] { "Acme Hospital", "Draft from work order" },
            tags.Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain(tags, x => string.IsNullOrWhiteSpace(x.Name));

        var links = await dbContext.KnowledgeArticleTags.Where(x => x.ArticleId == article.Id).ToListAsync();
        Assert.Equal(tags.Count, links.Count);
    }

    private static T ReadProperty<T>(object source, string name)
    {
        var property = source.GetType().GetProperty(name);
        Assert.NotNull(property);
        return (T)property!.GetValue(source)!;
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-null-navigation-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static ReportsController CreateReportsController(AppDbContext dbContext, Guid tenantId, Guid userId) =>
        new(
            dbContext,
            new FakeTenantContext(tenantId, userId),
            new NoOpUserAccessService(),
            new NoOpBranchAccessService());

    private static KnowledgeController CreateKnowledgeController(AppDbContext dbContext, Guid tenantId, Guid userId) =>
        new(
            dbContext,
            new FakeTenantContext(tenantId, userId),
            new NoOpUserAccessService());

    private static async Task<(Tenant Tenant, User Admin)> SeedTenantAsync(AppDbContext dbContext)
    {
        var tenant = new Tenant
        {
            Name = "Acme",
            Slug = $"acme-{Guid.NewGuid():N}"[..12],
            CompanyName = "Acme Facilities",
            Email = "ops@acme.test",
            Country = "Kenya",
            Status = "Active",
            LicenseStatus = "Active"
        };

        var admin = new User
        {
            TenantId = tenant.Id,
            FullName = "Alice Admin",
            Email = $"alice-{Guid.NewGuid():N}@acme.test",
            PasswordHash = "hash",
            Role = AppRoles.Admin,
            IsActive = true,
            HasAllBranchAccess = true
        };

        dbContext.AddRange(tenant, admin);
        await dbContext.SaveChangesAsync();
        return (tenant, admin);
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
}
