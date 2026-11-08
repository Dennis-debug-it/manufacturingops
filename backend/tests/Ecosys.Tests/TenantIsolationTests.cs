using System.Text;
using System.Security.Claims;
using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ecosys.Tests;

public sealed class TenantIsolationTests
{
    [Fact]
    public async Task AppDbContext_RequestScopedQueries_UseTenantFiltersOnTheLiveContext()
    {
        var databaseName = $"ecosys-tenant-isolation-{Guid.NewGuid():N}";
        var fixture = await SeedFixtureAsync(databaseName);
        var tenantContext = new FakeTenantContext(fixture.TenantAId, fixture.TenantAAdminId);

        await using var dbContext = CreateDbContext(databaseName, tenantContext);

        Assert.Equal(new[] { fixture.TenantAClientId }, await dbContext.Clients.Select(x => x.Id).ToArrayAsync());
        Assert.Equal(new[] { fixture.TenantAAssetId }, await dbContext.Assets.Select(x => x.Id).ToArrayAsync());
        Assert.Equal(new[] { fixture.TenantAWorkOrderId }, await dbContext.WorkOrders.Select(x => x.Id).ToArrayAsync());
        Assert.Equal(new[] { fixture.TenantATemplateId }, await dbContext.PmTemplates.Select(x => x.Id).ToArrayAsync());
        Assert.Equal(new[] { fixture.TenantAPlanId }, await dbContext.PreventiveMaintenancePlans.Select(x => x.Id).ToArrayAsync());
        Assert.Equal(new[] { fixture.TenantAMaterialRequestId }, await dbContext.MaterialRequests.Select(x => x.Id).ToArrayAsync());
        Assert.Equal(new[] { fixture.TenantAMaterialItemId }, await dbContext.MaterialItems.Select(x => x.Id).ToArrayAsync());
        Assert.Equal(
            new[] { fixture.TenantAAdminId, fixture.TenantAManagedUserId }.OrderBy(x => x).ToArray(),
            (await dbContext.Users.Select(x => x.Id).ToArrayAsync()).OrderBy(x => x).ToArray());
        Assert.Equal(new[] { fixture.TenantAAssetCategoryId }, await dbContext.AssetCategories.Select(x => x.Id).ToArrayAsync());
    }

    [Fact]
    public async Task Clients_Sites_And_Assets_CannotLoadOtherTenantRecords()
    {
        var databaseName = $"ecosys-tenant-isolation-{Guid.NewGuid():N}";
        var fixture = await SeedFixtureAsync(databaseName);
        var tenantContext = new FakeTenantContext(fixture.TenantAId, fixture.TenantAAdminId);

        await using var dbContext = CreateDbContext(databaseName, tenantContext);

        var clientsController = new ClientsController(dbContext, tenantContext, new NoOpUserAccessService(), new NoOpAuditLogService());
        var sitesController = new SitesController(dbContext, tenantContext, new NoOpUserAccessService(), new NoOpDocumentNumberingService());
        var assetsController = new AssetsController(
            dbContext,
            tenantContext,
            new NoOpPreventiveMaintenancePlanService(),
            new NoOpAuditLogService(),
            new NoOpUserAccessService(),
            new NoOpBranchAccessService(),
            new NoOpDocumentNumberingService(),
            new NoOpLicenseGuardService(),
            new Ecosys.Infrastructure.Services.Factory.OrgScopeService(dbContext, tenantContext),
            new Ecosys.Infrastructure.Services.Factory.AssetLifecycleService(dbContext, new Ecosys.Infrastructure.Services.Factory.OrgScopeService(dbContext, tenantContext)),
            new Ecosys.Infrastructure.Services.Factory.AssetClassService(dbContext));

        await Assert.ThrowsAsync<NotFoundException>(() => clientsController.Get(fixture.TenantBClientId, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => sitesController.Get(fixture.TenantBClientId, fixture.TenantBSiteId, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => assetsController.Get(fixture.TenantBAssetId, CancellationToken.None));
    }

    [Fact]
    public async Task WorkOrders_And_MaterialRequests_CannotLoadOtherTenantRecords()
    {
        var databaseName = $"ecosys-tenant-isolation-{Guid.NewGuid():N}";
        var fixture = await SeedFixtureAsync(databaseName);
        var tenantContext = new FakeTenantContext(fixture.TenantAId, fixture.TenantAAdminId);

        await using var dbContext = CreateDbContext(databaseName, tenantContext);

        var workOrdersController = new WorkOrdersController(
            dbContext,
            tenantContext,
            new NoOpDocumentNumberingService(),
            new NoOpAuditLogService(),
            new NoOpUserAccessService(),
            new NoOpBranchAccessService(),
            new NoOpLicenseGuardService(),
            new NoOpWorkOrderLifecycleService(),
            new NoOpAssignmentWorkflowService(),
            new NoOpPmChecklistService(),
            new NoOpSlaService(),
            new NoOpWorkflowService());

        var materialRequestsController = new MaterialRequestsController(
            dbContext,
            tenantContext,
            new NoOpAuditLogService(),
            new NoOpStockLedgerService(),
            new NoOpUserAccessService(),
            new NoOpBranchAccessService(),
            new NoOpDocumentNumberingService());

        await Assert.ThrowsAsync<NotFoundException>(() => workOrdersController.Get(fixture.TenantBWorkOrderId, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => materialRequestsController.Get(fixture.TenantBMaterialRequestId, CancellationToken.None));
    }

    [Fact]
    public async Task PmTemplates_And_Plans_AreTenantScoped()
    {
        var databaseName = $"ecosys-tenant-isolation-{Guid.NewGuid():N}";
        var fixture = await SeedFixtureAsync(databaseName);
        var tenantContext = new FakeTenantContext(fixture.TenantAId, fixture.TenantAAdminId);

        await using var dbContext = CreateDbContext(databaseName, tenantContext);

        var templatesController = new PmTemplatesController(tenantContext, new NoOpUserAccessService(), new PmTemplateService(dbContext));
        var plansController = new PreventiveMaintenanceController(
            dbContext,
            tenantContext,
            new NoOpUserAccessService(),
            new NoOpDocumentNumberingService(),
            new NoOpAuditLogService(),
            new NoOpBranchAccessService(),
            new NoOpPmChecklistService(),
            TestTimeZones.Create(dbContext),
            new NoOpLogger<PreventiveMaintenanceController>());

        await Assert.ThrowsAsync<NotFoundException>(() => templatesController.Get(fixture.TenantBTemplateId, CancellationToken.None));

        var dueResult = await plansController.GetDue(null, CancellationToken.None);
        var dueOk = Assert.IsType<OkObjectResult>(dueResult.Result);
        var duePlans = Assert.IsAssignableFrom<IReadOnlyCollection<PreventiveMaintenancePlanResponse>>(dueOk.Value);
        var duePlan = Assert.Single(duePlans);
        Assert.Equal(fixture.TenantAPlanId, duePlan.Id);
    }

    [Fact]
    public async Task Users_CannotLoadOtherTenantAccounts()
    {
        var databaseName = $"ecosys-tenant-isolation-{Guid.NewGuid():N}";
        var fixture = await SeedFixtureAsync(databaseName);
        var tenantContext = new FakeTenantContext(fixture.TenantAId, fixture.TenantAAdminId);

        await using var dbContext = CreateDbContext(databaseName, tenantContext);

        var controller = new UsersController(
            dbContext,
            tenantContext,
            new NoOpUserAccessService(),
            new NoOpUserPermissionTemplateService(),
            new PasswordHasher<User>(),
            new NoOpLicenseGuardService(),
            new NoOpTenantSecurityPolicyService(),
            new NoOpAuditLogService(),
            new NoOpUserCredentialDeliveryService(),
            new NoOpTemporaryPasswordService(),
            Microsoft.Extensions.Options.Options.Create(new Ecosys.Shared.Options.AuthSecurityOptions()),
            new NoOpUserSessionService());

        await Assert.ThrowsAsync<NotFoundException>(() => controller.Get(fixture.TenantBManagedUserId, CancellationToken.None));
    }

    [Fact]
    public async Task Reports_And_Dashboard_IgnoreOtherTenantData()
    {
        var databaseName = $"ecosys-tenant-isolation-{Guid.NewGuid():N}";
        var fixture = await SeedFixtureAsync(databaseName);
        var tenantContext = new FakeTenantContext(fixture.TenantAId, fixture.TenantAAdminId);

        await using var dbContext = CreateDbContext(databaseName, tenantContext);

        var reportsController = new ReportsController(
            dbContext,
            tenantContext,
            new NoOpUserAccessService(),
            new NoOpBranchAccessService());

        var dashboardController = new DashboardController(
            dbContext,
            new NoOpUserAccessService(),
            new NoOpBranchAccessService(),
            new NoOpLogger<DashboardController>(),
            tenantContext);
        dashboardController.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(TenantClaimTypes.UserId, fixture.TenantAAdminId.ToString())
                ], "TestAuth"))
            }
        };

        var reportResult = await reportsController.WorkOrderPerformance(
            null,
            null,
            null,
            null,
            null,
            null,
            CancellationToken.None);

        var reportOk = Assert.IsType<OkObjectResult>(reportResult);
        var reportPayload = ReadAnonymousObject(reportOk.Value);
        Assert.Equal(1, ReadInt(reportPayload["Total"]));

        var dashboardResult = await dashboardController.GetSummary(null, CancellationToken.None);
        var dashboardOk = Assert.IsType<OkObjectResult>(dashboardResult.Result);
        var summary = Assert.IsType<DashboardSummaryDto>(dashboardOk.Value);
        Assert.Equal(1, summary.OpenWorkOrders);
        Assert.Equal(0, summary.ClosedWorkOrders);
        Assert.Equal(1, summary.Assets);
        Assert.Equal(1, summary.Clients);
        Assert.Equal(0, summary.MaterialsLowStock);
    }

    [Fact]
    public async Task Intake_RejectsForeignClientAssetAndBranchBeforeCreatingWork()
    {
        var databaseName = $"ecosys-intake-isolation-{Guid.NewGuid():N}";
        var fixture = await SeedFixtureAsync(databaseName);
        var tenantContext = new FakeTenantContext(fixture.TenantAId, fixture.TenantAAdminId);
        await using var dbContext = CreateDbContext(databaseName, tenantContext);
        dbContext.MonitoringSettings.Add(new MonitoringSetting
        {
            TenantId = fixture.TenantAId, WebhookSecret = "local-test-secret", IsEnabled = true
        });
        await dbContext.SaveChangesAsync();
        var count = await dbContext.WorkOrders.CountAsync();
        var controller = new IntakeController(dbContext, new NoOpLicenseGuardService(), new NoOpDocumentNumberingService());
        foreach (var request in new[]
        {
            new EmailIntakeRequest(fixture.TenantAId, null, fixture.TenantBClientId, null, "Test", null, null, "local-test-secret"),
            new EmailIntakeRequest(fixture.TenantAId, null, fixture.TenantAClientId, fixture.TenantBAssetId, "Test", null, null, "local-test-secret"),
            new EmailIntakeRequest(fixture.TenantAId, Guid.NewGuid(), fixture.TenantAClientId, null, "Test", null, null, "local-test-secret")
        })
            await Assert.ThrowsAsync<BusinessRuleException>(() => controller.EmailWebhook(request, CancellationToken.None));
        Assert.Equal(count, await dbContext.WorkOrders.CountAsync());
    }

    [Fact]
    public async Task Dashboard_WorkloadExcludesTerminalOrders()
    {
        var databaseName = $"ecosys-dashboard-workload-{Guid.NewGuid():N}";
        var fixture = await SeedFixtureAsync(databaseName);
        var tenantContext = new FakeTenantContext(fixture.TenantAId, fixture.TenantAAdminId);
        await using var dbContext = CreateDbContext(databaseName, tenantContext);
        foreach (var status in new[] { "Completed", "Acknowledged", "Closed", "Cancelled" })
        {
            dbContext.WorkOrders.Add(new WorkOrder
            {
                TenantId = fixture.TenantAId,
                ClientId = fixture.TenantAClientId,
                WorkOrderNumber = $"TERMINAL-{status}",
                Title = status,
                Status = status,
            });
        }
        await dbContext.SaveChangesAsync();
        var controller = new DashboardController(dbContext, new NoOpUserAccessService(),
            new NoOpBranchAccessService(), new NoOpLogger<DashboardController>(), tenantContext)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.GetSummary(null, CancellationToken.None);
        var summary = Assert.IsType<DashboardSummaryDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(1, summary.OpenWorkOrders);
        Assert.Equal(3, summary.ClosedWorkOrders);
        Assert.Equal(1, summary.UnassignedWorkOrders);
        Assert.Empty(summary.WorkOrdersByGroup);
        Assert.Empty(summary.TechnicianWorkload);
    }

    [Fact]
    public async Task AssetCategoryFieldEndpoints_BlockCrossTenantFieldMutation()
    {
        var databaseName = $"ecosys-tenant-isolation-{Guid.NewGuid():N}";
        var fixture = await SeedFixtureAsync(databaseName);
        var tenantContext = new FakeTenantContext(fixture.TenantAId, fixture.TenantAAdminId);

        await using var dbContext = CreateDbContext(databaseName, tenantContext);

        var controller = new AssetCategoriesController(dbContext, tenantContext, new NoOpUserAccessService(), new Ecosys.Infrastructure.Services.Factory.FactorySettingsService(dbContext), new Ecosys.Infrastructure.Services.Factory.AssetClassService(dbContext));

        await Assert.ThrowsAsync<NotFoundException>(() => controller.UpdateField(
            fixture.TenantBAssetCategoryId,
            fixture.TenantBAssetCategoryFieldId,
            new UpsertAssetCategoryFieldRequest("Phase", "Text", null, null, false, 1),
            CancellationToken.None));

        await Assert.ThrowsAsync<NotFoundException>(() => controller.DeleteField(
            fixture.TenantBAssetCategoryId,
            fixture.TenantBAssetCategoryFieldId,
            CancellationToken.None));
    }

    private static AppDbContext CreateDbContext(string databaseName, ITenantContext? tenantContext = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new AppDbContext(options, tenantContext);
    }

    private static async Task<CrossTenantFixture> SeedFixtureAsync(string databaseName)
    {
        await using var dbContext = CreateDbContext(databaseName);

        var tenantA = CreateTenant("Tenant A");
        var tenantB = CreateTenant("Tenant B");

        var tenantAAdmin = CreateUser(tenantA.Id, "Alice Admin", $"alice-{Guid.NewGuid():N}@tenant-a.test", AppRoles.Admin, hasAllBranchAccess: true);
        var tenantAManagedUser = CreateUser(tenantA.Id, "Analyst A", $"analyst-{Guid.NewGuid():N}@tenant-a.test", AppRoles.User, hasAllBranchAccess: true);
        var tenantBAdmin = CreateUser(tenantB.Id, "Bob Admin", $"bob-{Guid.NewGuid():N}@tenant-b.test", AppRoles.Admin, hasAllBranchAccess: true);
        var tenantBManagedUser = CreateUser(tenantB.Id, "Analyst B", $"analyst-{Guid.NewGuid():N}@tenant-b.test", AppRoles.User, hasAllBranchAccess: true);

        var clientA = new Client { TenantId = tenantA.Id, ClientName = "Client A", Email = "client-a@test.local", IsActive = true };
        var clientB = new Client { TenantId = tenantB.Id, ClientName = "Client B", Email = "client-b@test.local", IsActive = true };

        var siteA = new Site
        {
            TenantId = tenantA.Id,
            ClientId = clientA.Id,
            SiteCode = "SITE-A",
            SiteName = "Site A",
            SiteType = "Factory",
            Status = "Active"
        };

        var siteB = new Site
        {
            TenantId = tenantB.Id,
            ClientId = clientB.Id,
            SiteCode = "SITE-B",
            SiteName = "Site B",
            SiteType = "Factory",
            Status = "Active"
        };

        var categoryA = new AssetCategory
        {
            TenantId = tenantA.Id,
            Name = "Generator",
            Icon = "tool",
            IsDefault = true,
            IsActive = true,
            DisplayOrder = 1,
            Fields =
            [
                new AssetCategoryField
                {
                    FieldName = "capacity_kva",
                    FieldLabel = "Capacity KVA",
                    FieldType = "Number",
                    IsRequired = false,
                    IsActive = true,
                    DisplayOrder = 1
                }
            ]
        };

        var categoryB = new AssetCategory
        {
            TenantId = tenantB.Id,
            Name = "Generator",
            Icon = "tool",
            IsDefault = true,
            IsActive = true,
            DisplayOrder = 1,
            Fields =
            [
                new AssetCategoryField
                {
                    FieldName = "phase",
                    FieldLabel = "Phase",
                    FieldType = "Text",
                    IsRequired = false,
                    IsActive = true,
                    DisplayOrder = 1
                }
            ]
        };

        var assetA = new Asset
        {
            TenantId = tenantA.Id,
            ClientId = clientA.Id,
            SiteId = siteA.Id,
            AssetCategoryId = categoryA.Id,
            AssetName = "Generator A",
            AssetCode = "GEN-A",
            AssetType = "Generator",
            OwnershipType = "ClientOwned",
            Status = "Active",
            NextPmDate = DateTime.UtcNow.Date.AddDays(-1)
        };

        var assetB = new Asset
        {
            TenantId = tenantB.Id,
            ClientId = clientB.Id,
            SiteId = siteB.Id,
            AssetCategoryId = categoryB.Id,
            AssetName = "Generator B",
            AssetCode = "GEN-B",
            AssetType = "Generator",
            OwnershipType = "ClientOwned",
            Status = "Active",
            NextPmDate = DateTime.UtcNow.Date.AddDays(-1)
        };

        var templateA = CreateTemplate(tenantA.Id, categoryA.Id, "PM Template A");
        var templateB = CreateTemplate(tenantB.Id, categoryB.Id, "PM Template B");

        var planA = new PreventiveMaintenancePlan
        {
            TenantId = tenantA.Id,
            AssetId = assetA.Id,
            PmTemplateId = templateA.Id,
            Frequency = "Monthly",
            Status = "Active",
            AutoSchedule = true,
            NextPmDate = DateTime.UtcNow.Date.AddDays(-1)
        };

        var planB = new PreventiveMaintenancePlan
        {
            TenantId = tenantB.Id,
            AssetId = assetB.Id,
            PmTemplateId = templateB.Id,
            Frequency = "Monthly",
            Status = "Active",
            AutoSchedule = true,
            NextPmDate = DateTime.UtcNow.Date.AddDays(-1)
        };

        var workOrderA = new WorkOrder
        {
            TenantId = tenantA.Id,
            ClientId = clientA.Id,
            SiteId = siteA.Id,
            AssetId = assetA.Id,
            WorkOrderNumber = "WO-A-001",
            Title = "Repair generator A",
            Priority = "High",
            Status = "Open",
            WorkType = WorkOrderTypes.CorrectiveMaintenance,
            AssignmentType = "Unassigned"
        };

        var workOrderB = new WorkOrder
        {
            TenantId = tenantB.Id,
            ClientId = clientB.Id,
            SiteId = siteB.Id,
            AssetId = assetB.Id,
            WorkOrderNumber = "WO-B-001",
            Title = "Repair generator B",
            Priority = "High",
            Status = "Open",
            WorkType = WorkOrderTypes.CorrectiveMaintenance,
            AssignmentType = "Unassigned"
        };

        var materialItemA = new MaterialItem
        {
            TenantId = tenantA.Id,
            ItemCode = "MAT-A",
            ItemName = "Fuse A",
            UnitOfMeasure = "pcs",
            QuantityOnHand = 10,
            ReorderLevel = 2,
            UnitCost = 50m,
            IsActive = true
        };

        var materialItemB = new MaterialItem
        {
            TenantId = tenantB.Id,
            ItemCode = "MAT-B",
            ItemName = "Fuse B",
            UnitOfMeasure = "pcs",
            QuantityOnHand = 1,
            ReorderLevel = 2,
            UnitCost = 50m,
            IsActive = true
        };

        var materialRequestA = new MaterialRequest
        {
            TenantId = tenantA.Id,
            WorkOrderId = workOrderA.Id,
            RequestedByUserId = tenantAAdmin.Id,
            RequestNumber = "MR-A-001",
            Status = "Pending",
            Lines =
            [
                new MaterialRequestLine
                {
                    MaterialItemId = materialItemA.Id,
                    QuantityRequested = 2
                }
            ]
        };

        var materialRequestB = new MaterialRequest
        {
            TenantId = tenantB.Id,
            WorkOrderId = workOrderB.Id,
            RequestedByUserId = tenantBAdmin.Id,
            RequestNumber = "MR-B-001",
            Status = "Pending",
            Lines =
            [
                new MaterialRequestLine
                {
                    MaterialItemId = materialItemB.Id,
                    QuantityRequested = 3
                }
            ]
        };

        dbContext.AddRange(
            tenantA,
            tenantB,
            tenantAAdmin,
            tenantAManagedUser,
            tenantBAdmin,
            tenantBManagedUser,
            clientA,
            clientB,
            siteA,
            siteB,
            categoryA,
            categoryB,
            assetA,
            assetB,
            templateA,
            templateB,
            planA,
            planB,
            workOrderA,
            workOrderB,
            materialItemA,
            materialItemB,
            materialRequestA,
            materialRequestB);

        await dbContext.SaveChangesAsync();

        return new CrossTenantFixture(
            tenantA.Id,
            tenantB.Id,
            tenantAAdmin.Id,
            tenantAManagedUser.Id,
            tenantBManagedUser.Id,
            clientA.Id,
            clientB.Id,
            siteA.Id,
            siteB.Id,
            assetA.Id,
            assetB.Id,
            workOrderA.Id,
            workOrderB.Id,
            templateA.Id,
            templateB.Id,
            planA.Id,
            planB.Id,
            materialItemA.Id,
            materialItemB.Id,
            materialRequestA.Id,
            materialRequestB.Id,
            categoryA.Id,
            categoryB.Id,
            categoryB.Fields.Single().Id);
    }

    private static Tenant CreateTenant(string name) =>
        new()
        {
            Name = name,
            Slug = $"{name.ToLowerInvariant().Replace(' ', '-')}-{Guid.NewGuid():N}"[..12],
            CompanyName = $"{name} Company",
            Email = $"{name.ToLowerInvariant().Replace(' ', '.')}@test.local",
            Country = "Kenya",
            Status = "Active",
            LicenseStatus = "Active",
            PrimaryColor = "#0F4C81",
            SecondaryColor = "#F4B942",
            IsActive = true
        };

    private static User CreateUser(Guid tenantId, string fullName, string email, string role, bool hasAllBranchAccess) =>
        new()
        {
            TenantId = tenantId,
            FullName = fullName,
            Email = email,
            PasswordHash = "hash",
            Role = role,
            IsActive = true,
            HasAllBranchAccess = hasAllBranchAccess
        };

    private static PmTemplate CreateTemplate(Guid tenantId, Guid assetCategoryId, string name) =>
        new()
        {
            TenantId = tenantId,
            AssetCategoryId = assetCategoryId,
            Category = "Generators",
            Name = name,
            WorkType = WorkOrderTypes.PreventiveMaintenance,
            IsActive = true,
            Version = 1,
            Questions =
            [
                new PmTemplateQuestion
                {
                    Prompt = "Inspect panel",
                    ResponseType = "checkbox",
                    IsRequired = true,
                    DisplayOrder = 1
                }
            ]
        };

    private static IDictionary<string, object?> ReadAnonymousObject(object? value) =>
        value?.GetType()
            .GetProperties()
            .ToDictionary(property => property.Name, property => property.GetValue(value))
        ?? new Dictionary<string, object?>();

    private static int ReadInt(object? value) => Convert.ToInt32(value);

    private sealed record CrossTenantFixture(
        Guid TenantAId,
        Guid TenantBId,
        Guid TenantAAdminId,
        Guid TenantAManagedUserId,
        Guid TenantBManagedUserId,
        Guid TenantAClientId,
        Guid TenantBClientId,
        Guid TenantASiteId,
        Guid TenantBSiteId,
        Guid TenantAAssetId,
        Guid TenantBAssetId,
        Guid TenantAWorkOrderId,
        Guid TenantBWorkOrderId,
        Guid TenantATemplateId,
        Guid TenantBTemplateId,
        Guid TenantAPlanId,
        Guid TenantBPlanId,
        Guid TenantAMaterialItemId,
        Guid TenantBMaterialItemId,
        Guid TenantAMaterialRequestId,
        Guid TenantBMaterialRequestId,
        Guid TenantAAssetCategoryId,
        Guid TenantBAssetCategoryId,
        Guid TenantBAssetCategoryFieldId);

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

    private sealed class NoOpUserSessionService : IUserSessionService
    {
        public UserSession StartSession(User user, string? jwtId) => new() { TenantId = user.TenantId, UserId = user.Id };
        public Task<bool> TouchAsync(Guid sessionId, TimeSpan minimumInterval, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task LogoutAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<UserSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.FromResult<UserSession?>(null);
        public Task<int> RevokeAllForUserAsync(Guid userId, string? reason = null, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> RevokeOthersForUserAsync(Guid userId, Guid currentSessionId, string? reason = null, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> RevokeSessionsForRolesAsync(Guid tenantId, IReadOnlyCollection<string> roles, string? reason = null, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> RevokeSessionsForUsersAsync(IReadOnlyCollection<Guid> userIds, string? reason = null, CancellationToken cancellationToken = default) => Task.FromResult(0);
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

    private sealed class NoOpPreventiveMaintenancePlanService : IPreventiveMaintenancePlanService
    {
        public Task SyncForAssetAsync(Asset asset, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoOpAuditLogService : IAuditLogService
    {
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, string severity = "Info", string? actorName = null, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoOpLicenseGuardService : ILicenseGuardService
    {
        public Task<TenantLicenseSnapshot> GetSnapshotAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromException<TenantLicenseSnapshot>(new NotSupportedException());
        public Task<LicenseUsageSnapshot> GetUsageAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromException<LicenseUsageSnapshot>(new NotSupportedException());
        public Task<IReadOnlyCollection<PlatformLicenseUsageSnapshot>> GetPlatformUsageAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<PlatformLicenseUsageSnapshot>>([]);
        public Task EnsureTenantCanMutateAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureCanCreateUserAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureCanCreateBranchAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureFeatureEnabledAsync(Guid tenantId, string featureName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<TenantLicense> GetOrCreateTenantLicenseAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromException<TenantLicense>(new NotSupportedException());
    }

    private sealed class NoOpWorkOrderLifecycleService : IWorkOrderLifecycleService
    {
        public string NormalizeStatus(string status) => status;
        public Task RecordCreatedAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, string workOrderNumber, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<WorkOrder> AssignAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, Guid? technicianId, Guid? assignmentGroupId, CancellationToken cancellationToken = default) => Task.FromException<WorkOrder>(new NotSupportedException());
        public Task<WorkOrderEvent> AddCommentAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, WorkOrderCommentRequest request, CancellationToken cancellationToken = default) => Task.FromException<WorkOrderEvent>(new NotSupportedException());
        public Task<WorkOrder> StartAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, CancellationToken cancellationToken = default) => Task.FromException<WorkOrder>(new NotSupportedException());
        public Task<WorkOrder> HoldAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, string? holdType, string? reason, CancellationToken cancellationToken = default) => Task.FromException<WorkOrder>(new NotSupportedException());
        public Task<WorkOrder> ResumeAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, CancellationToken cancellationToken = default) => Task.FromException<WorkOrder>(new NotSupportedException());
        public Task<WorkOrder> ArriveAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, Ecosys.Infrastructure.Services.WorkOrderLocationRequest request, CancellationToken cancellationToken = default) => Task.FromException<WorkOrder>(new NotSupportedException());
        public Task<WorkOrder> DepartAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, Ecosys.Infrastructure.Services.WorkOrderLocationRequest request, CancellationToken cancellationToken = default) => Task.FromException<WorkOrder>(new NotSupportedException());
        public Task<WorkOrder> ChangeStatusAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, WorkOrderStatusChangeRequest request, CancellationToken cancellationToken = default) => Task.FromException<WorkOrder>(new NotSupportedException());
        public Task<WorkOrder> CompleteAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, WorkOrderCompletionRequest request, CancellationToken cancellationToken = default) => Task.FromException<WorkOrder>(new NotSupportedException());
        public Task<IReadOnlyCollection<WorkOrderEvent>> GetEventsAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<WorkOrderEvent>>([]);
    }

    private sealed class NoOpAssignmentWorkflowService : IWorkOrderAssignmentWorkflowService
    {
        public Task ApplyAssignmentsAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, WorkOrderAssignmentUpdateRequest request, bool isReassignment, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RecordTechnicianResponseAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, TechnicianDispatchResponseRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RecordInTransitAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, TechnicianInTransitRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RecordArrivalAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, TechnicianArrivalRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RecordDepartureAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, TechnicianDepartureRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyCollection<WorkOrderAssignmentHistory>> GetHistoryAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<WorkOrderAssignmentHistory>>([]);
        public Task<bool> HasAcceptedTechnicianAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> HasArrivalAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class NoOpPmChecklistService : IPmWorkOrderChecklistService
    {
        public Task<PmTemplate> GetActiveTemplateAsync(Guid tenantId, Guid pmTemplateId, CancellationToken cancellationToken = default) => Task.FromException<PmTemplate>(new NotSupportedException());
        public Task<int> AttachPmTemplateToWorkOrderAsync(Guid workOrderId, Guid pmTemplateId, Guid tenantId, Guid? actorUserId = null, bool replaceExisting = false, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<WorkOrderChecklistItem> UpdateChecklistItemAsync(Guid workOrderId, Guid itemId, Guid tenantId, Guid actorUserId, string? responseValue, string? remarks, bool isCompleted, CancellationToken cancellationToken = default) => Task.FromException<WorkOrderChecklistItem>(new NotSupportedException());
        public Task EnsureRequiredChecklistCompletedAsync(Guid workOrderId, Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoOpWorkflowService : IWorkflowService
    {
        public Task<WorkflowDefinition?> GetActiveWorkflowAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<WorkflowDefinition?>(null);
        public Task<IReadOnlyCollection<WorkflowStatus>> GetActiveWorkflowStatusesAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<WorkflowStatus>>([]);
        public Task EnsureDefaultWorkflowAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<WorkflowStatus> ApplyStatusTransitionAsync(WorkOrder workOrder, Guid targetWorkflowStatusId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NoOpSlaService : ISlaService
    {
        public Task ApplyDefinitionAsync(WorkOrder workOrder, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> RefreshWorkOrderAsync(Guid workOrderId, DateTime utcNow, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class NoOpStockLedgerService : IStockLedgerService
    {
        public Task<StockMovement> RecordReceiptAsync(MaterialItem material, Guid? branchId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber, Guid? workOrderId = null, Guid? materialRequestId = null, CancellationToken cancellationToken = default) => Task.FromException<StockMovement>(new NotSupportedException());
        public Task<StockMovement> RecordAdjustmentAsync(MaterialItem material, Guid? branchId, decimal quantityChange, Guid? createdByUserId, string? reason, string? referenceNumber = null, Guid? workOrderId = null, Guid? materialRequestId = null, CancellationToken cancellationToken = default) => Task.FromException<StockMovement>(new NotSupportedException());
        public Task<StockMovement> RecordIssueAsync(MaterialItem material, Guid? branchId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber = null, Guid? workOrderId = null, Guid? materialRequestId = null, bool allowNegativeStock = false, CancellationToken cancellationToken = default) => Task.FromException<StockMovement>(new NotSupportedException());
        public Task<StockMovement> RecordReturnAsync(MaterialItem material, Guid? branchId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber = null, Guid? workOrderId = null, Guid? materialRequestId = null, CancellationToken cancellationToken = default) => Task.FromException<StockMovement>(new NotSupportedException());
        public Task<StockMovement> RecordTransferOutAsync(MaterialItem material, Guid fromBranchId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber = null, CancellationToken cancellationToken = default) => Task.FromException<StockMovement>(new NotSupportedException());
        public Task<StockMovement> RecordTransferInAsync(MaterialItem material, Guid toBranchId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber = null, CancellationToken cancellationToken = default) => Task.FromException<StockMovement>(new NotSupportedException());
        public Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default) => operation(cancellationToken);
    }

    private sealed class NoOpUserPermissionTemplateService : IUserPermissionTemplateService
    {
        private static readonly UserPermissionsModel EmptyPermissions = new UserPermissionsModel();

        public UserPermissionsModel GetDefaultPermissions(string role, string? jobTitle) => EmptyPermissions;
        public UserPermissionsModel GetFullTenantPermissions() => EmptyPermissions;
        public UserPermissionsModel GetSuperAdminPermissions() => EmptyPermissions;
        public UserPermissionsModel Merge(string role, string? jobTitle, UserPermissionsModel? customPermissions) => customPermissions ?? EmptyPermissions;
    }

    private sealed class NoOpTenantSecurityPolicyService : ITenantSecurityPolicyService
    {
        public Task<TenantSecurityPolicy> GetOrCreateAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(new TenantSecurityPolicy { TenantId = tenantId });
        public Task ValidatePasswordAsync(Guid tenantId, string password, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void ValidatePassword(string password, TenantSecurityPolicy policy) { }
    }

    private sealed class NoOpUserCredentialDeliveryService : IUserCredentialDeliveryService
    {
        public string BuildLoginUrl() => "https://app.test/login";
        public string BuildInviteUrl(string token) => $"https://app.test/invite/{token}";
        public string BuildResetPasswordUrl(string token) => $"https://app.test/reset/{token}";
        public Task<UserCredentialDeliveryResult> SendAsync(Guid tenantId, User user, UserCredentialDeliveryRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new UserCredentialDeliveryResult(true, "Sent", "Sent"));
        public Task<UserCredentialDeliveryResult> SendPasswordResetLinkAsync(Guid tenantId, User user, PasswordResetLinkDeliveryRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new UserCredentialDeliveryResult(true, "Sent", "Sent"));
    }

    private sealed class NoOpTemporaryPasswordService : ITemporaryPasswordService
    {
        public string Generate(int minimumLength = 14) => "TempPassword123!";
    }

    private sealed class NoOpLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
