using System.Text;
using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

public sealed class CoreReportsFlowTests
{
    [Fact]
    public async Task WorkOrderPerformance_ReturnsExpectedSummaryAndCsv()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var controller = CreateController(dbContext, fixture.Tenant.Id, fixture.Admin.Id, fixture.Branch.Id);

        var result = await controller.WorkOrderPerformance(
            fixture.RangeStart,
            fixture.RangeEnd,
            fixture.Client.Id,
            fixture.Branch.Id,
            null,
            null,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = ReadAnonymousObject(ok.Value);
        Assert.Equal(6, ReadInt(payload["Total"]));
        Assert.Equal(3, ReadInt(payload["Completed"]));
        Assert.Equal(1, ReadInt(payload["Overdue"]));
        Assert.NotEmpty(ReadArray(payload["ByStatus"]));
        Assert.NotEmpty(ReadArray(payload["ByPriority"]));
        Assert.NotEmpty(ReadArray(payload["ByDay"]));

        var export = await controller.WorkOrderPerformanceExport(
            fixture.RangeStart,
            fixture.RangeEnd,
            fixture.Client.Id,
            fixture.Branch.Id,
            null,
            null,
            CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(export);
        var csv = Encoding.UTF8.GetString(file.FileContents);
        Assert.Contains("WO Number,Title,Client,Priority,Status", csv, StringComparison.Ordinal);
        Assert.Contains("WO-REP-001", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TechnicianProductivity_ReturnsExpectedRowsAndCsv()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var controller = CreateController(dbContext, fixture.Tenant.Id, fixture.Admin.Id, fixture.Branch.Id);

        var result = await controller.TechnicianProductivity(
            fixture.RangeStart,
            fixture.RangeEnd,
            fixture.TechnicianA.Id,
            fixture.Branch.Id,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = ReadAnonymousObject(ok.Value);
        var technicians = ReadArray(payload["Technicians"]);
        Assert.Single(technicians);
        var first = ReadAnonymousObject(technicians[0]);
        Assert.Equal("Moses Otieno", ReadString(first["Name"]));
        Assert.Equal(2, ReadInt(first["TotalJobs"]));
        Assert.Equal(2, ReadInt(first["Completed"]));

        var export = await controller.TechnicianProductivityExport(
            fixture.RangeStart,
            fixture.RangeEnd,
            fixture.TechnicianA.Id,
            fixture.Branch.Id,
            CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(export);
        var csv = Encoding.UTF8.GetString(file.FileContents);
        Assert.Contains("Technician,Total Jobs,Completed", csv, StringComparison.Ordinal);
        Assert.Contains("Moses Otieno", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AssetReliability_ReturnsExpectedRowsAndCsv()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var controller = CreateController(dbContext, fixture.Tenant.Id, fixture.Admin.Id, fixture.Branch.Id);

        var result = await controller.AssetReliability(
            fixture.RangeStart,
            fixture.RangeEnd,
            fixture.Client.Id,
            fixture.Site.Id,
            fixture.AssetCategory.Id,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = ReadAnonymousObject(ok.Value);
        var assets = ReadArray(payload["Assets"]);
        Assert.Single(assets);
        var first = ReadAnonymousObject(assets[0]);
        Assert.Equal("Generator 01", ReadString(first["AssetName"]));
        Assert.Equal("Nairobi HQ", ReadString(first["SiteName"]));
        Assert.Equal(3, ReadInt(first["CorrectiveWos"]));
        Assert.True(ReadBool(first["IsRecurringFault"]));

        var export = await controller.AssetReliabilityExport(
            fixture.RangeStart,
            fixture.RangeEnd,
            fixture.Client.Id,
            fixture.Site.Id,
            fixture.AssetCategory.Id,
            CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(export);
        var csv = Encoding.UTF8.GetString(file.FileContents);
        Assert.Contains("Asset Code,Asset Name,Category,Client,Status", csv, StringComparison.Ordinal);
        Assert.Contains("GEN-01", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PmCompliance_ReturnsExpectedSummaryAndCsv()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var controller = CreateController(dbContext, fixture.Tenant.Id, fixture.Admin.Id, fixture.Branch.Id);

        var result = await controller.PmCompliance(
            fixture.RangeStart,
            fixture.RangeEnd,
            fixture.Client.Id,
            fixture.Site.Id,
            fixture.AssetCategory.Id,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = ReadAnonymousObject(ok.Value);
        Assert.Equal(1, ReadInt(payload["ActivePlans"]));
        Assert.Equal(1, ReadInt(payload["DueInPeriod"]));
        Assert.Equal(1, ReadInt(payload["CompletedOnTime"]));
        Assert.Equal(1, ReadInt(payload["Overdue"]));
        Assert.NotEmpty(ReadArray(payload["OverduePlans"]));

        var export = await controller.PmComplianceExport(
            fixture.RangeStart,
            fixture.RangeEnd,
            fixture.Client.Id,
            fixture.Site.Id,
            fixture.AssetCategory.Id,
            CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(export);
        var csv = Encoding.UTF8.GetString(file.FileContents);
        Assert.Contains("Asset,Asset Code,Client,Next PM Date,Days Overdue", csv, StringComparison.Ordinal);
        Assert.Contains("Generator 01", csv, StringComparison.Ordinal);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-reports-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static ReportsController CreateController(AppDbContext dbContext, Guid tenantId, Guid userId, Guid branchId) =>
        new(
            dbContext,
            new FakeTenantContext(tenantId, userId),
            new NoOpUserAccessService(),
            new FakeBranchAccessService(branchId));

    private static async Task<FixtureData> SeedFixtureAsync(AppDbContext dbContext)
    {
        var now = DateTime.UtcNow;
        var rangeStart = now.AddDays(-30);
        var rangeEnd = now.AddDays(1);

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

        var branch = new Branch
        {
            TenantId = tenant.Id,
            Name = "Nairobi Service Hub",
            Code = "NRB-OPS",
            IsActive = true
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
            SiteCode = "SITE-001",
            SiteName = "Nairobi HQ",
            SiteType = "Office",
            Status = "Active"
        };

        var siteTwo = new Site
        {
            TenantId = tenant.Id,
            ClientId = client.Id,
            SiteCode = "SITE-002",
            SiteName = "Mombasa Depot",
            SiteType = "Depot",
            Status = "Active"
        };

        var assetCategory = new AssetCategory
        {
            TenantId = tenant.Id,
            Name = "Generator",
            Icon = "tool",
            IsDefault = true,
            IsActive = true,
            DisplayOrder = 1
        };

        var assetCategoryTwo = new AssetCategory
        {
            TenantId = tenant.Id,
            Name = "UPS",
            Icon = "bolt",
            IsDefault = false,
            IsActive = true,
            DisplayOrder = 2
        };

        var asset = new Asset
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            ClientId = client.Id,
            SiteId = site.Id,
            AssetCategoryId = assetCategory.Id,
            AssetName = "Generator 01",
            AssetCode = "GEN-01",
            AssetType = "Generator",
            Status = "Active",
            LastPmDate = now.AddDays(-20),
            NextPmDate = now.AddDays(-2),
            WarrantyExpiryDate = now.AddDays(45)
        };

        var assetTwo = new Asset
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            ClientId = client.Id,
            SiteId = siteTwo.Id,
            AssetCategoryId = assetCategoryTwo.Id,
            AssetName = "UPS 01",
            AssetCode = "UPS-01",
            AssetType = "UPS",
            Status = "Active",
            NextPmDate = now.AddDays(3),
            WarrantyExpiryDate = now.AddDays(-10)
        };

        var technicianA = new Technician
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            FullName = "Moses Otieno",
            Email = "moses@acme.test",
            IsActive = true
        };

        var technicianB = new Technician
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            FullName = "James Kariuki",
            Email = "james@acme.test",
            IsActive = true
        };

        var wo1 = new WorkOrder
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            ClientId = client.Id,
            SiteId = site.Id,
            AssetId = asset.Id,
            AssignedTechnicianId = technicianA.Id,
            WorkOrderNumber = "WO-REP-001",
            Title = "Generator fault A",
            Priority = "High",
            Status = "Completed",
            CreatedAt = now.AddDays(-10),
            DueDate = now.AddDays(-8),
            CompletedAt = now.AddDays(-9),
            ArrivalAt = now.AddDays(-10).AddHours(2),
            DepartureAt = now.AddDays(-10).AddHours(5)
        };

        var wo2 = new WorkOrder
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            ClientId = client.Id,
            SiteId = site.Id,
            AssetId = asset.Id,
            AssignedTechnicianId = technicianA.Id,
            WorkOrderNumber = "WO-REP-002",
            Title = "Generator fault B",
            Priority = "Medium",
            Status = "Completed",
            CreatedAt = now.AddDays(-7),
            DueDate = now.AddDays(-5),
            CompletedAt = now.AddDays(-4),
            ArrivalAt = now.AddDays(-7).AddHours(1),
            DepartureAt = now.AddDays(-7).AddHours(4)
        };

        var wo3 = new WorkOrder
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            ClientId = client.Id,
            SiteId = site.Id,
            AssetId = asset.Id,
            AssignedTechnicianId = technicianB.Id,
            WorkOrderNumber = "WO-REP-003",
            Title = "Generator fault C",
            Priority = "High",
            Status = "Open",
            CreatedAt = now.AddDays(-3),
            DueDate = now.AddDays(-1)
        };

        var wo4 = new WorkOrder
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            ClientId = client.Id,
            SiteId = site.Id,
            AssetId = assetTwo.Id,
            WorkOrderNumber = "WO-REP-004",
            Title = "Generator inspection",
            Priority = "Low",
            Status = "Cancelled",
            CreatedAt = now.AddDays(-2)
        };

        var pmCompleted = new WorkOrder
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            ClientId = client.Id,
            SiteId = site.Id,
            AssetId = asset.Id,
            WorkOrderNumber = "WO-PM-001",
            Title = "PM - Generator 01",
            Priority = "Medium",
            Status = "Completed",
            IsPreventiveMaintenance = true,
            CreatedAt = now.AddDays(-12),
            DueDate = now.AddDays(-2),
            CompletedAt = now.AddDays(-2)
        };

        var pmOpen = new WorkOrder
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            ClientId = client.Id,
            SiteId = site.Id,
            AssetId = assetTwo.Id,
            WorkOrderNumber = "WO-PM-002",
            Title = "PM - Generator 02",
            Priority = "Medium",
            Status = "Open",
            IsPreventiveMaintenance = true,
            CreatedAt = now.AddDays(-1),
            DueDate = now.AddDays(3)
        };

        var planOverdue = new PreventiveMaintenancePlan
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            AssetId = asset.Id,
            SiteId = site.Id,
            Frequency = "Monthly",
            FrequencyUnit = "Monthly",
            FrequencyInterval = 1,
            TriggerType = "Calendar",
            Status = "Active",
            AutoSchedule = true,
            NextPmDate = now.AddDays(-2)
        };

        var planUpcoming = new PreventiveMaintenancePlan
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            AssetId = assetTwo.Id,
            SiteId = site.Id,
            Frequency = "Monthly",
            FrequencyUnit = "Monthly",
            FrequencyInterval = 1,
            TriggerType = "Calendar",
            Status = "Active",
            AutoSchedule = true,
            NextPmDate = now.AddDays(3)
        };

        dbContext.AddRange(
            tenant,
            admin,
            branch,
            client,
            site,
            siteTwo,
            assetCategory,
            assetCategoryTwo,
            asset,
            assetTwo,
            technicianA,
            technicianB,
            wo1,
            wo2,
            wo3,
            wo4,
            pmCompleted,
            pmOpen,
            planOverdue,
            planUpcoming);

        await dbContext.SaveChangesAsync();
        return new FixtureData(tenant, admin, branch, client, site, assetCategory, technicianA, rangeStart, rangeEnd);
    }

    private static IDictionary<string, object?> ReadAnonymousObject(object? value) =>
        value?.GetType()
            .GetProperties()
            .ToDictionary(property => property.Name, property => property.GetValue(value))
        ?? new Dictionary<string, object?>();

    private static IReadOnlyList<object?> ReadArray(object? value) =>
        value is System.Collections.IEnumerable sequence
            ? sequence.Cast<object?>().ToList()
            : [];

    private static int ReadInt(object? value) => Convert.ToInt32(value);
    private static bool ReadBool(object? value) => Convert.ToBoolean(value);
    private static string ReadString(object? value) => Convert.ToString(value) ?? string.Empty;

    private sealed record FixtureData(
        Tenant Tenant,
        User Admin,
        Branch Branch,
        Client Client,
        Site Site,
        AssetCategory AssetCategory,
        Technician TechnicianA,
        DateTime RangeStart,
        DateTime RangeEnd);

    private sealed class FakeTenantContext(Guid tenantId, Guid userId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
        public Guid? SessionId => Guid.NewGuid();
        public string? Email => "admin@acme.test";
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

    private sealed class FakeBranchAccessService(Guid branchId) : IBranchAccessService
    {
        public Task<BranchQueryScope> GetQueryScopeAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new BranchQueryScope(true, requestedBranchId ?? branchId, [branchId], false));

        public Task<Guid?> ResolveBranchIdForWriteAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(requestedBranchId ?? branchId);

        public Task EnsureCanAccessBranchAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyCollection<Guid>> GetAccessibleBranchIdsAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Guid>>([branchId]);
    }
}
