using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

public sealed class KnowledgeCentreFlowTests
{
    [Fact]
    public async Task CreateAndPublishArticle_CanBeSearched()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var controller = CreateController(dbContext, fixture.Tenant.Id, fixture.Admin.Id, isAdmin: true);

        var create = await controller.CreateArticle(
            new UpsertKnowledgeArticleRequest(
                "Generator ATS reset guide",
                "Reset steps for a failed ATS transfer.",
                "Use the local reset and inspect the relay.",
                fixture.Category.Id,
                "Draft",
                "Internal",
                ["Generator", "ATS"]),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(create.Result);
        var createdPayload = Assert.IsType<KnowledgeArticleDetailResponse>(created.Value);

        var publish = await controller.PublishArticle(createdPayload.Id, CancellationToken.None);
        var publishOk = Assert.IsType<OkObjectResult>(publish.Result);
        var published = Assert.IsType<KnowledgeArticleDetailResponse>(publishOk.Value);
        Assert.Equal("Published", published.Status);

        var search = await controller.Search("relay", null, CancellationToken.None);
        var searchOk = Assert.IsType<OkObjectResult>(search.Result);
        var results = Assert.IsAssignableFrom<IReadOnlyCollection<KnowledgeArticleListItemResponse>>(searchOk.Value);
        Assert.Contains(results, x => x.Id == createdPayload.Id);
    }

    [Fact]
    public async Task SuggestForWorkOrder_ReturnsRelevantPublishedArticle()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var controller = CreateController(dbContext, fixture.Tenant.Id, fixture.Admin.Id, isAdmin: true);

        var article = new KnowledgeArticle
        {
            TenantId = fixture.Tenant.Id,
            Title = "Generator troubleshooting guide",
            Slug = "generator-troubleshooting-guide",
            Summary = "Guide for generator start failures.",
            Body = "Check the starter relay and battery voltage before replacing the ATS module.",
            CategoryId = fixture.Category.Id,
            Status = "Published",
            Visibility = "Internal",
            CreatedByUserId = fixture.Admin.Id,
            UpdatedByUserId = fixture.Admin.Id,
            PublishedAt = DateTime.UtcNow
        };
        dbContext.KnowledgeArticles.Add(article);
        await dbContext.SaveChangesAsync();
        dbContext.KnowledgeTags.Add(new KnowledgeTag { TenantId = fixture.Tenant.Id, Name = "Generator", Slug = "generator" });
        await dbContext.SaveChangesAsync();
        var tag = await dbContext.KnowledgeTags.SingleAsync();
        dbContext.KnowledgeArticleTags.Add(new KnowledgeArticleTag { TenantId = fixture.Tenant.Id, ArticleId = article.Id, TagId = tag.Id });
        await dbContext.SaveChangesAsync();

        var result = await controller.SuggestForWorkOrder(fixture.WorkOrder.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var suggestions = Assert.IsAssignableFrom<IReadOnlyCollection<KnowledgeArticleListItemResponse>>(ok.Value);
        Assert.Contains(suggestions, x => x.Id == article.Id);
    }

    [Fact]
    public async Task DraftFromWorkOrder_CreatesDraftArticle()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var controller = CreateController(dbContext, fixture.Tenant.Id, fixture.Admin.Id, isAdmin: true);

        var result = await controller.DraftFromWorkOrder(fixture.WorkOrder.Id, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var article = Assert.IsType<KnowledgeArticleDetailResponse>(created.Value);
        Assert.Equal("Draft", article.Status);
        Assert.Equal(fixture.WorkOrder.Id, article.SourceWorkOrderId);
        Assert.Contains(fixture.WorkOrder.WorkOrderNumber, article.Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetArticle_CrossTenantAccessIsBlocked()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var foreignTenant = new Tenant
        {
            Name = "Other",
            Slug = $"other-{Guid.NewGuid():N}".Substring(0, 12),
            CompanyName = "Other Tenant",
            Email = "other@test.local",
            Country = "Kenya",
            Status = "Active",
            LicenseStatus = "Active"
        };
        var foreignUser = new User
        {
            TenantId = foreignTenant.Id,
            FullName = "Other Admin",
            Email = $"other-admin-{Guid.NewGuid():N}@test.local",
            PasswordHash = "hash",
            Role = AppRoles.Admin,
            IsActive = true,
            HasAllBranchAccess = true
        };
        var foreignArticle = new KnowledgeArticle
        {
            TenantId = foreignTenant.Id,
            Title = "Foreign article",
            Slug = "foreign-article",
            Body = "Should not be visible.",
            Status = "Published",
            Visibility = "Internal",
            CreatedByUserId = foreignUser.Id
        };
        dbContext.AddRange(foreignTenant, foreignUser, foreignArticle);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, fixture.Tenant.Id, fixture.Admin.Id, isAdmin: true);

        await Assert.ThrowsAsync<NotFoundException>(() => controller.GetArticle(foreignArticle.Id, CancellationToken.None));
    }

    [Fact]
    public async Task ListArticles_HidesAdminOnlyArticlesFromTechnicians()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        dbContext.KnowledgeArticles.Add(new KnowledgeArticle
        {
            TenantId = fixture.Tenant.Id,
            Title = "Admin escalation guide",
            Slug = "admin-escalation-guide",
            Body = "Only admins should see this article.",
            Status = "Published",
            Visibility = "AdminOnly",
            CreatedByUserId = fixture.Admin.Id
        });
        dbContext.KnowledgeArticles.Add(new KnowledgeArticle
        {
            TenantId = fixture.Tenant.Id,
            Title = "Technician field guide",
            Slug = "technician-field-guide",
            Body = "Technicians can see this article.",
            Status = "Published",
            Visibility = "TechnicianOnly",
            CreatedByUserId = fixture.Admin.Id
        });
        await dbContext.SaveChangesAsync();

        var technician = CreateController(dbContext, fixture.Tenant.Id, fixture.Admin.Id, isAdmin: false);
        var result = await technician.ListArticles(null, null, "Published", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var articles = Assert.IsAssignableFrom<IReadOnlyCollection<KnowledgeArticleListItemResponse>>(ok.Value);
        Assert.DoesNotContain(articles, x => x.Visibility == "AdminOnly");
        Assert.Contains(articles, x => x.Visibility == "TechnicianOnly");
    }

    [Fact]
    public async Task GetArticle_AllowsAdminToViewAdminOnlyArticles()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var article = new KnowledgeArticle
        {
            TenantId = fixture.Tenant.Id,
            Title = "Admin escalation guide",
            Slug = "admin-escalation-guide",
            Body = "Only admins should see this article.",
            Status = "Published",
            Visibility = "AdminOnly",
            CreatedByUserId = fixture.Admin.Id
        };
        dbContext.KnowledgeArticles.Add(article);
        await dbContext.SaveChangesAsync();

        var admin = CreateController(dbContext, fixture.Tenant.Id, fixture.Admin.Id, isAdmin: true);
        var result = await admin.GetArticle(article.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<KnowledgeArticleDetailResponse>(ok.Value);
        Assert.Equal("AdminOnly", payload.Visibility);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-knowledge-tests-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static KnowledgeController CreateController(AppDbContext dbContext, Guid tenantId, Guid userId, bool isAdmin) =>
        new(
            dbContext,
            new FakeTenantContext(tenantId, userId, isAdmin),
            new NoOpUserAccessService());

    private static async Task<FixtureData> SeedFixtureAsync(AppDbContext dbContext)
    {
        var tenant = new Tenant
        {
            Name = "Acme",
            Slug = $"acme-{Guid.NewGuid():N}".Substring(0, 12),
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

        var client = new Client
        {
            TenantId = tenant.Id,
            ClientName = "Acme Facilities",
            Email = "client@acme.test",
            IsActive = true
        };

        var site = new Site
        {
            TenantId = tenant.Id,
            ClientId = client.Id,
            SiteCode = $"SITE-{Guid.NewGuid():N}".Substring(0, 10),
            SiteName = "Nairobi HQ",
            SiteType = "Office",
            Status = "Active"
        };

        var category = new KnowledgeCategory
        {
            TenantId = tenant.Id,
            Name = "Electrical",
            Description = "Electrical guides",
            DisplayOrder = 1
        };

        var assetCategory = new AssetCategory
        {
            TenantId = tenant.Id,
            Name = "Generator",
            Icon = "tool",
            IsActive = true,
            IsDefault = true,
            DisplayOrder = 1
        };

        var asset = new Asset
        {
            TenantId = tenant.Id,
            ClientId = client.Id,
            SiteId = site.Id,
            AssetCategoryId = assetCategory.Id,
            AssetName = "Generator 01",
            AssetCode = "GEN-01",
            AssetType = "Generator",
            Status = "Active"
        };

        var workOrder = new WorkOrder
        {
            TenantId = tenant.Id,
            ClientId = client.Id,
            SiteId = site.Id,
            AssetId = asset.Id,
            WorkOrderNumber = "WO-100",
            Title = "Generator start failure",
            Description = "Generator did not start after mains failure.",
            Priority = "High",
            Status = "Completed",
            JobCardNotes = "Starter relay failed and ATS showed an alarm.",
            WorkDoneNotes = "Replaced starter relay and reset the ATS."
        };

        dbContext.AddRange(tenant, admin, client, site, category, assetCategory, asset, workOrder);
        await dbContext.SaveChangesAsync();
        return new FixtureData(tenant, admin, category, workOrder);
    }

    private sealed record FixtureData(Tenant Tenant, User Admin, KnowledgeCategory Category, WorkOrder WorkOrder);

    private sealed class FakeTenantContext(Guid tenantId, Guid userId, bool isAdmin) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
        public Guid? SessionId => Guid.NewGuid();
        public string? Email => "admin@acme.test";
        public string? Role => isAdmin ? AppRoles.Admin : AppRoles.User;
        public string? JobTitle => isAdmin ? "Admin" : "Technician";
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public bool IsAdmin => isAdmin;
        public bool HasRole(string role) => string.Equals(role, Role, StringComparison.OrdinalIgnoreCase);
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
}
