using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ecosys.Tests;

public sealed class WorkOrderWorkTypeTests
{
    [Fact]
    public async Task Create_ManualWorkOrder_PersistsSelectedWorkType()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedManualCreateFixtureAsync(dbContext);
        var controller = CreateWorkOrdersController(dbContext, fixture);

        var result = await controller.Create(
            new CreateWorkOrderRequest(
                fixture.Client.Id,
                fixture.Branch.Id,
                null,
                null,
                fixture.Asset.Id,
                null,
                "Unassigned",
                null,
                null,
                null,
                null,
                "Install generator controller",
                "Commission the new controller and capture baseline readings.",
                "High",
                DateTime.UtcNow.Date.AddDays(2),
                WorkOrderTypes.NewInstallation,
                false,
                null),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var response = Assert.IsType<WorkOrderResponse>(created.Value);

        Assert.Equal(WorkOrderTypes.NewInstallation, response.WorkType);
        Assert.False(response.IsPreventiveMaintenance);

        var persisted = await dbContext.WorkOrders.SingleAsync(x => x.Id == response.Id);
        Assert.Equal(WorkOrderTypes.NewInstallation, persisted.WorkType);
        Assert.False(persisted.IsPreventiveMaintenance);
    }

    [Fact]
    public async Task Create_ManualWorkOrder_RejectsMissingWorkType()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedManualCreateFixtureAsync(dbContext);
        var controller = CreateWorkOrdersController(dbContext, fixture);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Create(
            new CreateWorkOrderRequest(
                fixture.Client.Id,
                fixture.Branch.Id,
                null,
                null,
                null,
                null,
                "Unassigned",
                null,
                null,
                null,
                null,
                "Missing work type",
                "This request should be rejected.",
                "Medium",
                null,
                null,
                false,
                null),
            CancellationToken.None));

        Assert.Contains("Work type", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateWorkOrder_FromPmPlan_SetsPreventiveMaintenanceWorkType()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedPmFixtureAsync(dbContext);
        var controller = CreatePreventiveMaintenanceController(dbContext, fixture);

        var result = await controller.GenerateWorkOrder(fixture.Plan.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<WorkOrderResponse>(ok.Value);

        Assert.Equal(WorkOrderTypes.PreventiveMaintenance, response.WorkType);
        Assert.True(response.IsPreventiveMaintenance);

        var persisted = await dbContext.WorkOrders.SingleAsync(x => x.Id == response.Id);
        Assert.Equal(WorkOrderTypes.PreventiveMaintenance, persisted.WorkType);
        Assert.True(persisted.IsPreventiveMaintenance);
    }

    [Fact]
    public async Task Get_WorkOrderDetail_IncludesWorkType()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedManualCreateFixtureAsync(dbContext);
        var workOrder = new WorkOrder
        {
            TenantId = fixture.Tenant.Id,
            BranchId = fixture.Branch.Id,
            ClientId = fixture.Client.Id,
            AssetId = fixture.Asset.Id,
            AssignmentType = "Unassigned",
            WorkOrderNumber = "WO-TYPE-001",
            Title = "Inspect rooftop array",
            Priority = "Medium",
            Status = WorkOrderStatuses.Open,
            WorkType = WorkOrderTypes.InspectionAssessment
        };

        dbContext.WorkOrders.Add(workOrder);
        await dbContext.SaveChangesAsync();

        var controller = CreateWorkOrdersController(dbContext, fixture);
        var result = await controller.Get(workOrder.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<WorkOrderResponse>(ok.Value);
        Assert.Equal(WorkOrderTypes.InspectionAssessment, response.WorkType);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-work-order-type-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<ManualFixture> SeedManualCreateFixtureAsync(AppDbContext dbContext)
    {
        var tenant = CreateTenant();
        var branch = new Branch
        {
            TenantId = tenant.Id,
            Name = "Nairobi",
            Code = $"NRB-{Guid.NewGuid():N}".Substring(0, 8),
            IsActive = true
        };
        var user = CreateUser(tenant.Id);
        var client = new Client
        {
            TenantId = tenant.Id,
            ClientName = "Acme Facilities",
            Email = "client@acme.test",
            IsActive = true
        };
        var asset = new Asset
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            ClientId = client.Id,
            AssetName = "Generator 01",
            AssetCode = $"GEN-{Guid.NewGuid():N}".Substring(0, 10),
            Status = "Active"
        };

        dbContext.AddRange(tenant, branch, user, client, asset);
        await dbContext.SaveChangesAsync();
        return new ManualFixture(tenant, branch, user, client, asset);
    }

    private static async Task<PmFixture> SeedPmFixtureAsync(AppDbContext dbContext)
    {
        var manualFixture = await SeedManualCreateFixtureAsync(dbContext);
        var template = new PmTemplate
        {
            TenantId = manualFixture.Tenant.Id,
            Category = "Generators",
            Name = "Generator PM",
            WorkType = WorkOrderTypes.PreventiveMaintenance,
            IsActive = true
        };
        var plan = new PreventiveMaintenancePlan
        {
            TenantId = manualFixture.Tenant.Id,
            BranchId = manualFixture.Branch.Id,
            AssetId = manualFixture.Asset.Id,
            PmTemplateId = template.Id,
            Frequency = "Monthly",
            NextPmDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Status = "Active"
        };

        dbContext.AddRange(template, plan);
        await dbContext.SaveChangesAsync();
        return new PmFixture(manualFixture.Tenant, manualFixture.Branch, manualFixture.User, manualFixture.Client, manualFixture.Asset, template, plan);
    }

    private static WorkOrdersController CreateWorkOrdersController(AppDbContext dbContext, ManualFixture fixture) =>
        new(
            dbContext,
            new FakeTenantContext(fixture.Tenant.Id, fixture.User.Id, fixture.User.Email),
            new NoOpDocumentNumberingService(),
            new AuditLogService(dbContext, new HttpContextAccessor()),
            new NoOpUserAccessService(),
            new NoOpBranchAccessService(),
            new NoOpLicenseGuardService(),
            new NoOpWorkOrderLifecycleService(),
            new NoOpAssignmentWorkflowService(),
            new NoOpPmChecklistService(),
            new NoOpSlaService());

    private static PreventiveMaintenanceController CreatePreventiveMaintenanceController(AppDbContext dbContext, PmFixture fixture) =>
        new(
            dbContext,
            new FakeTenantContext(fixture.Tenant.Id, fixture.User.Id, fixture.User.Email),
            new NoOpUserAccessService(),
            new NoOpDocumentNumberingService(),
            new AuditLogService(dbContext, new HttpContextAccessor()),
            new NoOpBranchAccessService(),
            new TemplateLookupPmChecklistService(fixture.Template),
            new NoOpLogger<PreventiveMaintenanceController>());

    private static Tenant CreateTenant() =>
        new()
        {
            Name = "Acme",
            Slug = $"acme-{Guid.NewGuid():N}".Substring(0, 12),
            CompanyName = "Acme Facilities",
            Email = "ops@acme.test",
            Country = "Kenya",
            Status = "Active",
            LicenseStatus = "Active",
            PrimaryColor = "#0F4C81",
            SecondaryColor = "#F4B942",
            IsActive = true
        };

    private static User CreateUser(Guid tenantId) =>
        new()
        {
            TenantId = tenantId,
            FullName = "Ops Admin",
            Email = $"{Guid.NewGuid():N}@acme.test",
            PasswordHash = "hash",
            Role = AppRoles.Admin,
            JobTitle = "Admin",
            IsActive = true,
            HasAllBranchAccess = true
        };

    private sealed record ManualFixture(Tenant Tenant, Branch Branch, User User, Client Client, Asset Asset);
    private sealed record PmFixture(Tenant Tenant, Branch Branch, User User, Client Client, Asset Asset, PmTemplate Template, PreventiveMaintenancePlan Plan);

    private sealed class FakeTenantContext(Guid tenantId, Guid userId, string email) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
        public Guid? SessionId => Guid.NewGuid();
        public string? Email => email;
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
        public Task EnsureCanAccessBranchAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyCollection<Guid>> GetAccessibleBranchIdsAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<Guid>>([]);
        public Task<BranchQueryScope> GetQueryScopeAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new BranchQueryScope(false, requestedBranchId, [], true));
        public Task<Guid?> ResolveBranchIdForWriteAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(requestedBranchId);
    }

    private sealed class NoOpDocumentNumberingService : IDocumentNumberingService
    {
        public Task<string> GenerateAsync(Guid tenantId, Guid? branchId, string documentType, CancellationToken cancellationToken = default) =>
            Task.FromResult($"WO-{Guid.NewGuid():N}".Substring(0, 11).ToUpperInvariant());

        public Task<NumberingSetting> UpsertAsync(Guid tenantId, Guid? branchId, string documentType, string prefix, long nextNumber, int paddingLength, string resetFrequency, bool includeYear, bool includeMonth, bool isActive, CancellationToken cancellationToken = default, string? suffix = null, string yearFormat = "YYYY", string separator = "-", bool isLocked = false) =>
            throw new NotSupportedException();
    }

    private sealed class NoOpLicenseGuardService : ILicenseGuardService
    {
        public Task<TenantLicenseSnapshot> GetSnapshotAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LicenseUsageSnapshot> GetUsageAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PlatformLicenseUsageSnapshot>> GetPlatformUsageAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<PlatformLicenseUsageSnapshot>>([]);
        public Task EnsureTenantCanMutateAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureCanCreateAssetAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureCanCreateBranchAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureCanCreateUserAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureCanCreateWorkOrderAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureFeatureEnabledAsync(Guid tenantId, string featureName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<TenantLicense> GetOrCreateTenantLicenseAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NoOpWorkOrderLifecycleService : IWorkOrderLifecycleService
    {
        public Task<WorkOrderEvent> AddCommentAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, WorkOrderCommentRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkOrder> ArriveAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, Ecosys.Infrastructure.Services.WorkOrderLocationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkOrder> AssignAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, Guid? technicianId, Guid? assignmentGroupId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkOrder> ChangeStatusAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, WorkOrderStatusChangeRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkOrder> CompleteAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, WorkOrderCompletionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkOrder> DepartAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, Ecosys.Infrastructure.Services.WorkOrderLocationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<WorkOrderEvent>> GetEventsAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<WorkOrderEvent>>([]);
        public string NormalizeStatus(string status) => status;
        public Task RecordCreatedAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, string workOrderNumber, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<WorkOrder> StartAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NoOpAssignmentWorkflowService : IWorkOrderAssignmentWorkflowService
    {
        public Task ApplyAssignmentsAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, WorkOrderAssignmentUpdateRequest request, bool isReassignment, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyCollection<WorkOrderAssignmentHistory>> GetHistoryAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<WorkOrderAssignmentHistory>>([]);
        public Task<bool> HasAcceptedTechnicianAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> HasArrivalAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task RecordArrivalAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, TechnicianArrivalRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RecordDepartureAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, TechnicianDepartureRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RecordInTransitAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, TechnicianInTransitRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RecordTechnicianResponseAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, TechnicianDispatchResponseRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoOpPmChecklistService : IPmWorkOrderChecklistService
    {
        public Task<int> AttachPmTemplateToWorkOrderAsync(Guid workOrderId, Guid pmTemplateId, Guid tenantId, Guid? actorUserId = null, bool replaceExisting = false, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task EnsureRequiredChecklistCompletedAsync(Guid workOrderId, Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<PmTemplate> GetActiveTemplateAsync(Guid tenantId, Guid pmTemplateId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkOrderChecklistItem> UpdateChecklistItemAsync(Guid workOrderId, Guid itemId, Guid tenantId, Guid actorUserId, string? responseValue, string? remarks, bool isCompleted, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class TemplateLookupPmChecklistService(PmTemplate template) : IPmWorkOrderChecklistService
    {
        public Task<int> AttachPmTemplateToWorkOrderAsync(Guid workOrderId, Guid pmTemplateId, Guid tenantId, Guid? actorUserId = null, bool replaceExisting = false, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task EnsureRequiredChecklistCompletedAsync(Guid workOrderId, Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<WorkOrderChecklistItem> UpdateChecklistItemAsync(Guid workOrderId, Guid itemId, Guid tenantId, Guid actorUserId, string? responseValue, string? remarks, bool isCompleted, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<PmTemplate> GetActiveTemplateAsync(Guid tenantId, Guid pmTemplateId, CancellationToken cancellationToken = default)
        {
            if (template.TenantId == tenantId && template.Id == pmTemplateId && template.IsActive)
            {
                return Task.FromResult(template);
            }

            throw new NotSupportedException();
        }
    }

    private sealed class NoOpSlaService : ISlaService
    {
        public Task ApplyDefinitionAsync(WorkOrder workOrder, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> RefreshWorkOrderAsync(Guid workOrderId, DateTime nowUtc, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class NoOpLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
