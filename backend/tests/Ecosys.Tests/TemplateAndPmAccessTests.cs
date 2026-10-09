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

public sealed class TemplateAndPmAccessTests
{
    [Fact]
    public async Task TemplateLibrary_BlocksTechnicianBrowsing()
    {
        await using var dbContext = CreateDbContext();
        var tenant = CreateTenant();
        var user = CreateUser(tenant.Id, "Field Technician", "Technician", AppRoles.User);
        dbContext.AddRange(tenant, user);
        await dbContext.SaveChangesAsync();

        var controller = new PmTemplatesController(
            new FakeTenantContext(tenant.Id, user.Id, user.Email, AppRoles.User, "Technician", isAdmin: false, permissions: []),
            new PermissionAwareUserAccessService(new FakeTenantContext(tenant.Id, user.Id, user.Email, AppRoles.User, "Technician", isAdmin: false, permissions: [])),
            new PmTemplateService(dbContext));

        await Assert.ThrowsAsync<ForbiddenException>(() => controller.GetAll(CancellationToken.None));
    }

    [Fact]
    public async Task PreventiveMaintenance_WorkOrderViewersCanReadButNotChangePlans()
    {
        await using var dbContext = CreateDbContext();
        var tenant = CreateTenant();
        var user = CreateUser(tenant.Id, "Planner Reader", "Supervisor", AppRoles.User);
        dbContext.AddRange(tenant, user);
        await dbContext.SaveChangesAsync();

        var controller = CreatePmController(dbContext, new FakeTenantContext(tenant.Id, user.Id, user.Email, AppRoles.User, "Supervisor", isAdmin: false, permissions: [PermissionNames.CanViewWorkOrders]));

        Assert.IsType<OkObjectResult>((await controller.GetAll(null, CancellationToken.None)).Result);
        Assert.IsType<OkObjectResult>((await controller.GetDue(null, CancellationToken.None)).Result);
        await Assert.ThrowsAsync<ForbiddenException>(() => controller.Create(new UpsertPreventiveMaintenancePlanRequest(Guid.NewGuid(), Guid.NewGuid(), 1, null, true, null, null, "Active"), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => controller.Update(Guid.NewGuid(), new UpsertPreventiveMaintenancePlanRequest(Guid.NewGuid(), Guid.NewGuid(), 1, null, true, null, null, "Active"), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => controller.GenerateWorkOrder(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task PreventiveMaintenance_BlocksReadingWithoutWorkOrderOrSettingsAccess()
    {
        await using var dbContext = CreateDbContext();
        var tenant = CreateTenant();
        var user = CreateUser(tenant.Id, "No Access", "Clerk", AppRoles.User);
        dbContext.AddRange(tenant, user);
        await dbContext.SaveChangesAsync();

        var controller = CreatePmController(dbContext, new FakeTenantContext(tenant.Id, user.Id, user.Email, AppRoles.User, "Clerk", isAdmin: false, permissions: []));

        await Assert.ThrowsAsync<ForbiddenException>(() => controller.GetAll(null, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => controller.GetDue(null, CancellationToken.None));
    }

    private static PreventiveMaintenanceController CreatePmController(AppDbContext dbContext, FakeTenantContext tenantContext) =>
        new(
            dbContext,
            tenantContext,
            new PermissionAwareUserAccessService(tenantContext),
            new NoOpDocumentNumberingService(),
            new AuditLogService(dbContext, new HttpContextAccessor()),
            new NoOpBranchAccessService(),
            new NoOpPmChecklistService(),
            TestTimeZones.Create(dbContext),
            new NoOpLogger<PreventiveMaintenanceController>());

    [Fact]
    public async Task PreventiveMaintenance_Create_NormalizesBrowserDatesToUtc()
    {
        await using var dbContext = CreateDbContext();
        var tenant = CreateTenant();
        var user = CreateUser(tenant.Id, "Planner", "Maintenance Planner", AppRoles.User);
        var client = new Client { TenantId = tenant.Id, ClientName = "Acme Client", IsActive = true };
        var asset = new Asset
        {
            TenantId = tenant.Id,
            ClientId = client.Id,
            AssetName = "Generator 01",
            AssetCode = "GEN-001",
            Status = "Active"
        };
        var template = new PmTemplate
        {
            TenantId = tenant.Id,
            Category = "Generators",
            Name = "Generator PM",
            IsActive = true
        };

        dbContext.AddRange(tenant, user, client, asset, template);
        await dbContext.SaveChangesAsync();

        var tenantContext = new FakeTenantContext(
            tenant.Id,
            user.Id,
            user.Email,
            AppRoles.User,
            "Maintenance Planner",
            isAdmin: false,
            permissions: [PermissionNames.CanManageSettings]);
        var controller = new PreventiveMaintenanceController(
            dbContext,
            tenantContext,
            new PermissionAwareUserAccessService(tenantContext),
            new NoOpDocumentNumberingService(),
            new AuditLogService(dbContext, new HttpContextAccessor()),
            new NoOpBranchAccessService(),
            new TemplateLookupPmChecklistService(template),
            TestTimeZones.Create(dbContext),
            new NoOpLogger<PreventiveMaintenanceController>());

        var createResult = await controller.Create(
            new UpsertPreventiveMaintenancePlanRequest(
                asset.Id,
                template.Id,
                1,
                null,
                true,
                new DateTime(2026, 5, 24),
                new DateTime(2026, 6, 24),
                "Active"),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(createResult.Result);
        var response = Assert.IsType<PreventiveMaintenancePlanResponse>(created.Value);
        var plan = await dbContext.PreventiveMaintenancePlans.SingleAsync(x => x.Id == response.Id);

        Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(plan.LastPmDate).Kind);
        Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(plan.NextPmDate).Kind);
    }

    [Fact]
    public async Task GeneralWorkOrderWorkspace_BlocksTechnicianUser()
    {
        await using var dbContext = CreateDbContext();
        var tenant = CreateTenant();
        var user = CreateUser(tenant.Id, "Field Technician", "Technician", AppRoles.User);
        dbContext.AddRange(tenant, user);
        await dbContext.SaveChangesAsync();

        var tenantContext = new FakeTenantContext(
            tenant.Id,
            user.Id,
            user.Email,
            AppRoles.User,
            "Technician",
            isAdmin: false,
            permissions: [PermissionNames.CanViewWorkOrders, PermissionNames.CanCompleteWorkOrders]);
        var controller = new WorkOrdersController(
            dbContext,
            tenantContext,
            new NoOpDocumentNumberingService(),
            new AuditLogService(dbContext, new HttpContextAccessor()),
            new NoOpUserAccessService(),
            new NoOpBranchAccessService(),
            new NoOpLicenseGuardService(),
            new NoOpWorkOrderLifecycleService(),
            new NoOpAssignmentWorkflowService(),
            new NoOpPmChecklistService(),
            new NoOpSlaService(),
            new NoOpWorkflowService());

        await Assert.ThrowsAsync<ForbiddenException>(() => controller.Get(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task GeneralWorkOrderWorkspace_BlocksTechnicianUserFromListing()
    {
        await using var dbContext = CreateDbContext();
        var tenant = CreateTenant();
        var user = CreateUser(tenant.Id, "Field Technician", "Technician", AppRoles.User);
        dbContext.AddRange(tenant, user);
        await dbContext.SaveChangesAsync();

        var tenantContext = new FakeTenantContext(
            tenant.Id,
            user.Id,
            user.Email,
            AppRoles.User,
            "Technician",
            isAdmin: false,
            permissions: [PermissionNames.CanViewWorkOrders, PermissionNames.CanCompleteWorkOrders]);
        var controller = new WorkOrdersController(
            dbContext,
            tenantContext,
            new NoOpDocumentNumberingService(),
            new AuditLogService(dbContext, new HttpContextAccessor()),
            new NoOpUserAccessService(),
            new NoOpBranchAccessService(),
            new NoOpLicenseGuardService(),
            new NoOpWorkOrderLifecycleService(),
            new NoOpAssignmentWorkflowService(),
            new NoOpPmChecklistService(),
            new NoOpSlaService(),
            new NoOpWorkflowService());

        await Assert.ThrowsAsync<ForbiddenException>(() => controller.GetAll(
                branchId: null, clientId: null, siteId: null, status: null, priority: null,
                workType: null, assignmentGroupId: null, technicianId: null, unassigned: null,
                assignmentStatus: null, slaStatus: null, search: null, fromDate: null, toDate: null,
                page: null, pageSize: null, cancellationToken: CancellationToken.None));
    }

    [Fact]
    public async Task ManagerWithTechnicianProfile_CanListOpenAndStartWorkOrders()
    {
        await using var dbContext = CreateDbContext();
        var tenant = CreateTenant();
        var user = CreateUser(tenant.Id, "Operations Supervisor", "Technician", AppRoles.User);
        var client = new Client { TenantId = tenant.Id, ClientName = "Acme Client", IsActive = true };
        var technician = new Technician
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            IsActive = true
        };
        var workOrder = new WorkOrder
        {
            TenantId = tenant.Id,
            ClientId = client.Id,
            WorkOrderNumber = "WO-MGMT-001",
            Title = "Restore generator",
            Priority = "High",
            Status = WorkOrderStatuses.Assigned,
            AssignedTechnicianId = technician.Id,
            LeadTechnicianId = technician.Id,
            AssignmentType = "IndividualTechnician"
        };

        dbContext.AddRange(tenant, user, client, technician, workOrder);
        dbContext.WorkOrderTechnicianAssignments.Add(new WorkOrderTechnicianAssignment
        {
            TenantId = tenant.Id,
            WorkOrderId = workOrder.Id,
            TechnicianId = technician.Id,
            IsLead = true,
            Status = "Accepted"
        });
        await dbContext.SaveChangesAsync();

        var tenantContext = new FakeTenantContext(
            tenant.Id,
            user.Id,
            user.Email,
            AppRoles.User,
            "Technician",
            isAdmin: false,
            permissions:
            [
                PermissionNames.CanViewWorkOrders,
                PermissionNames.CanCreateWorkOrders,
                PermissionNames.CanAssignWorkOrders,
                PermissionNames.CanCompleteWorkOrders
            ]);
        var controller = new WorkOrdersController(
            dbContext,
            tenantContext,
            new NoOpDocumentNumberingService(),
            new AuditLogService(dbContext, new HttpContextAccessor()),
            new PermissionAwareUserAccessService(tenantContext),
            new NoOpBranchAccessService(),
            new NoOpLicenseGuardService(),
            new TrackingWorkOrderLifecycleService(dbContext),
            new NoOpAssignmentWorkflowService(),
            new NoOpPmChecklistService(),
            new NoOpSlaService(),
            new NoOpWorkflowService());

        var listResult = await controller.GetAll(
                branchId: null, clientId: null, siteId: null, status: null, priority: null,
                workType: null, assignmentGroupId: null, technicianId: null, unassigned: null,
                assignmentStatus: null, slaStatus: null, search: null, fromDate: null, toDate: null,
                page: null, pageSize: null, cancellationToken: CancellationToken.None);
        var listOk = Assert.IsType<OkObjectResult>(listResult);
        var listItems = Assert.IsAssignableFrom<IReadOnlyCollection<WorkOrderResponse>>(listOk.Value);
        Assert.Single(listItems);

        var detailResult = await controller.Get(workOrder.Id, CancellationToken.None);
        var detailOk = Assert.IsType<OkObjectResult>(detailResult.Result);
        var detail = Assert.IsType<WorkOrderResponse>(detailOk.Value);
        Assert.Equal(workOrder.Id, detail.Id);

        var startResult = await controller.Start(workOrder.Id, CancellationToken.None);
        var startOk = Assert.IsType<OkObjectResult>(startResult.Result);
        var started = Assert.IsType<WorkOrderResponse>(startOk.Value);
        Assert.Equal(WorkOrderStatuses.InProgress, started.Status);
    }

    [Fact]
    public async Task ExecutionEndpoints_BlockTechnicianForUnassignedWorkOrder()
    {
        await using var dbContext = CreateDbContext();
        var tenant = CreateTenant();
        var user = CreateUser(tenant.Id, "Field Technician", "Technician", AppRoles.User);
        var client = new Client { TenantId = tenant.Id, ClientName = "Acme Client", IsActive = true };
        var technician = new Technician { TenantId = tenant.Id, UserId = user.Id, FullName = user.FullName, Email = user.Email, IsActive = true };
        var otherTechnician = new Technician { TenantId = tenant.Id, FullName = "Other Tech", Email = $"other-{Guid.NewGuid():N}@test.local", IsActive = true };
        var workOrder = new WorkOrder
        {
            TenantId = tenant.Id,
            ClientId = client.Id,
            WorkOrderNumber = "WO-ACCESS-001",
            Title = "Unassigned execution",
            Priority = "High",
            Status = "Open",
            AssignedTechnicianId = otherTechnician.Id,
            LeadTechnicianId = otherTechnician.Id,
            AssignmentType = "IndividualTechnician"
        };

        dbContext.AddRange(tenant, user, client, technician, otherTechnician, workOrder);
        dbContext.WorkOrderTechnicianAssignments.Add(new WorkOrderTechnicianAssignment
        {
            TenantId = tenant.Id,
            WorkOrderId = workOrder.Id,
            TechnicianId = otherTechnician.Id,
            IsLead = true,
            Status = "Accepted"
        });
        await dbContext.SaveChangesAsync();

        var tenantContext = new FakeTenantContext(
            tenant.Id,
            user.Id,
            user.Email,
            AppRoles.User,
            "Technician",
            isAdmin: false,
            permissions: [PermissionNames.CanViewWorkOrders, PermissionNames.CanCompleteWorkOrders]);
        var controller = new WorkOrderExecutionController(
            dbContext,
            tenantContext,
            new NoOpUserAccessService(),
            new NoOpBranchAccessService(),
            new NoOpFileStorageService(),
            new AuditLogService(dbContext, new HttpContextAccessor()),
            new NoOpStockLedgerService(),
            new NoOpAssignmentWorkflowService(),
            new NoOpWorkOrderLifecycleService(),
            new NoOpPdfRenderer(),
            new NoOpDocumentNumberingService(),
            new StubHttpClientFactory(),
            new FakeServiceReportDeliveryService());

        await Assert.ThrowsAsync<ForbiddenException>(() => controller.GetExecutionBundle(workOrder.Id, CancellationToken.None));
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-template-pm-access-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

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
            IsActive = true
        };

    private static User CreateUser(Guid tenantId, string fullName, string jobTitle, string role) =>
        new()
        {
            TenantId = tenantId,
            FullName = fullName,
            Email = $"{Guid.NewGuid():N}@acme.test",
            PasswordHash = "hash",
            Role = role,
            JobTitle = jobTitle,
            IsActive = true,
            HasAllBranchAccess = true
        };

    private sealed class FakeTenantContext(
        Guid tenantId,
        Guid userId,
        string email,
        string role,
        string jobTitle,
        bool isAdmin,
        IReadOnlyCollection<string> permissions) : ITenantContext
    {
        private readonly HashSet<string> _permissions = permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);

        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
        public Guid? SessionId => Guid.NewGuid();
        public string? Email => email;
        public string? Role => role;
        public string? JobTitle => jobTitle;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public bool IsAdmin => isAdmin;
        public bool HasRole(string requestedRole) => string.Equals(requestedRole, role, StringComparison.OrdinalIgnoreCase);
        public bool HasPermission(string permissionName) => _permissions.Contains(permissionName);
        public Guid GetRequiredTenantId() => tenantId;
        public Guid GetRequiredUserId() => userId;
        public Guid GetRequiredSessionId() => SessionId!.Value;
    }

    private sealed class PermissionAwareUserAccessService(ITenantContext tenantContext) : IUserAccessService
    {
        public void EnsureAdmin()
        {
            EnsureTenantOperationalAccess();
            if (!tenantContext.IsAdmin)
            {
                throw new ForbiddenException("Admin access is required for this action.");
            }
        }

        public void EnsureAdminOrPermission(string permissionName)
        {
            EnsureTenantOperationalAccess();
            if (tenantContext.IsAdmin || tenantContext.HasPermission(permissionName))
            {
                return;
            }

            throw new ForbiddenException("You do not have permission to perform this action.");
        }

        public void EnsureAdminOrAnyPermission(params string[] permissionNames)
        {
            EnsureTenantOperationalAccess();
            if (tenantContext.IsAdmin || permissionNames.Any(tenantContext.HasPermission))
            {
                return;
            }

            throw new ForbiddenException("You do not have permission to perform this action.");
        }

        public void EnsureTenantOperationalAccess()
        {
            if (tenantContext.IsSuperAdmin)
            {
                throw new ForbiddenException("SuperAdmin accounts cannot access tenant operational endpoints.");
            }
        }
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
            Task.FromResult("WO-TEST-0001");

        public Task<NumberingSetting> UpsertAsync(Guid tenantId, Guid? branchId, string documentType, string prefix, long nextNumber, int paddingLength, string resetFrequency, bool includeYear, bool includeMonth, bool isActive, CancellationToken cancellationToken = default, string? suffix = null, string yearFormat = "YYYY", string separator = "-", bool isLocked = false) =>
            Task.FromException<NumberingSetting>(new NotSupportedException());
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
        public Task<WorkOrderEvent> AddCommentAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, WorkOrderCommentRequest request, CancellationToken cancellationToken = default) => Task.FromException<WorkOrderEvent>(new NotSupportedException());
        public Task<WorkOrder> AssignAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, Guid? technicianId, Guid? assignmentGroupId, CancellationToken cancellationToken = default) => Task.FromException<WorkOrder>(new NotSupportedException());
        public Task<WorkOrder> ArriveAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, Ecosys.Infrastructure.Services.WorkOrderLocationRequest request, CancellationToken cancellationToken = default) => Task.FromException<WorkOrder>(new NotSupportedException());
        public Task<WorkOrder> ChangeStatusAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, WorkOrderStatusChangeRequest request, CancellationToken cancellationToken = default) => Task.FromException<WorkOrder>(new NotSupportedException());
        public Task<WorkOrder> CompleteAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, WorkOrderCompletionRequest request, CancellationToken cancellationToken = default) => Task.FromException<WorkOrder>(new NotSupportedException());
        public Task<WorkOrder> DepartAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, Ecosys.Infrastructure.Services.WorkOrderLocationRequest request, CancellationToken cancellationToken = default) => Task.FromException<WorkOrder>(new NotSupportedException());
        public Task<IReadOnlyCollection<WorkOrderEvent>> GetEventsAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<WorkOrderEvent>>([]);
        public string NormalizeStatus(string status) => status;
        public Task RecordCreatedAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, string workOrderNumber, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<WorkOrder> StartAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, CancellationToken cancellationToken = default) => Task.FromException<WorkOrder>(new NotSupportedException());
        public Task<WorkOrder> HoldAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, string? holdType, string? reason, CancellationToken cancellationToken = default) => Task.FromException<WorkOrder>(new NotSupportedException());
        public Task<WorkOrder> ResumeAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, CancellationToken cancellationToken = default) => Task.FromException<WorkOrder>(new NotSupportedException());
    }

    private sealed class TrackingWorkOrderLifecycleService(AppDbContext dbContext) : IWorkOrderLifecycleService
    {
        public Task<WorkOrderEvent> AddCommentAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, WorkOrderCommentRequest request, CancellationToken cancellationToken = default) => Task.FromException<WorkOrderEvent>(new NotSupportedException());
        public Task<WorkOrder> AssignAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, Guid? technicianId, Guid? assignmentGroupId, CancellationToken cancellationToken = default) => Task.FromException<WorkOrder>(new NotSupportedException());
        public Task<WorkOrder> ArriveAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, Ecosys.Infrastructure.Services.WorkOrderLocationRequest request, CancellationToken cancellationToken = default) => Task.FromException<WorkOrder>(new NotSupportedException());
        public Task<WorkOrder> ChangeStatusAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, WorkOrderStatusChangeRequest request, CancellationToken cancellationToken = default) => Task.FromException<WorkOrder>(new NotSupportedException());
        public Task<WorkOrder> CompleteAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, WorkOrderCompletionRequest request, CancellationToken cancellationToken = default) => Task.FromException<WorkOrder>(new NotSupportedException());
        public Task<WorkOrder> DepartAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, Ecosys.Infrastructure.Services.WorkOrderLocationRequest request, CancellationToken cancellationToken = default) => Task.FromException<WorkOrder>(new NotSupportedException());
        public Task<IReadOnlyCollection<WorkOrderEvent>> GetEventsAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<WorkOrderEvent>>([]);
        public string NormalizeStatus(string status) => status;
        public Task RecordCreatedAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, string workOrderNumber, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public async Task<WorkOrder> StartAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, CancellationToken cancellationToken = default)
        {
            var workOrder = await dbContext.WorkOrders.SingleAsync(x => x.TenantId == tenantId && x.Id == workOrderId, cancellationToken);
            workOrder.Status = WorkOrderStatuses.InProgress;
            workOrder.WorkStartedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return workOrder;
        }

        public Task<WorkOrder> HoldAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, string? holdType, string? reason, CancellationToken cancellationToken = default) => Task.FromException<WorkOrder>(new NotSupportedException());
        public Task<WorkOrder> ResumeAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, CancellationToken cancellationToken = default) => Task.FromException<WorkOrder>(new NotSupportedException());
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
        public Task<int> AttachPmTemplateToWorkOrderAsync(Guid workOrderId, Guid pmTemplateId, Guid tenantId, Guid? userId, bool replaceExisting = true, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task EnsureRequiredChecklistCompletedAsync(Guid workOrderId, Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<PmTemplate> GetActiveTemplateAsync(Guid tenantId, Guid pmTemplateId, CancellationToken cancellationToken = default) => Task.FromException<PmTemplate>(new NotSupportedException());
        public Task<WorkOrderChecklistItem> UpdateChecklistItemAsync(Guid workOrderId, Guid itemId, Guid tenantId, Guid userId, string? responseValue, string? remarks, bool isCompleted, CancellationToken cancellationToken = default) => Task.FromException<WorkOrderChecklistItem>(new NotSupportedException());
    }

    private sealed class TemplateLookupPmChecklistService(PmTemplate template) : IPmWorkOrderChecklistService
    {
        public Task<int> AttachPmTemplateToWorkOrderAsync(Guid workOrderId, Guid pmTemplateId, Guid tenantId, Guid? userId, bool replaceExisting = true, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task EnsureRequiredChecklistCompletedAsync(Guid workOrderId, Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<PmTemplate> GetActiveTemplateAsync(Guid tenantId, Guid pmTemplateId, CancellationToken cancellationToken = default)
        {
            if (template.TenantId == tenantId && template.Id == pmTemplateId && template.IsActive)
            {
                return Task.FromResult(template);
            }

            return Task.FromException<PmTemplate>(new NotSupportedException());
        }

        public Task<WorkOrderChecklistItem> UpdateChecklistItemAsync(Guid workOrderId, Guid itemId, Guid tenantId, Guid userId, string? responseValue, string? remarks, bool isCompleted, CancellationToken cancellationToken = default) => Task.FromException<WorkOrderChecklistItem>(new NotSupportedException());
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

    private sealed class NoOpFileStorageService : IFileStorageService
    {
        public Task DeleteAsync(string storagePath, CancellationToken ct) => Task.CompletedTask;
        public Task<Stream> DownloadAsync(string storagePath, CancellationToken ct) => Task.FromResult<Stream>(new MemoryStream());
        public string GetPublicUrl(string storagePath) => storagePath;
        public Task<UploadResult> UploadAsync(Stream stream, string fileName, string mimeType, Guid tenantId, CancellationToken ct) => Task.FromException<UploadResult>(new NotSupportedException());
    }

    private sealed class NoOpStockLedgerService : IStockLedgerService
    {
        public Task<StockMovement> RecordReceiptAsync(MaterialItem material, Guid? branchId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber, Guid? workOrderId = null, Guid? materialRequestId = null, CancellationToken cancellationToken = default) => Task.FromException<StockMovement>(new NotSupportedException());
        public Task<StockMovement> RecordAdjustmentAsync(MaterialItem material, Guid? branchId, decimal quantityChange, Guid? createdByUserId, string? reason, string? referenceNumber = null, Guid? workOrderId = null, Guid? materialRequestId = null, CancellationToken cancellationToken = default) => Task.FromException<StockMovement>(new NotSupportedException());
        public Task<StockMovement> RecordIssueAsync(MaterialItem material, Guid? branchId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber = null, Guid? workOrderId = null, Guid? materialRequestId = null, bool allowNegativeStock = false, CancellationToken cancellationToken = default) => Task.FromException<StockMovement>(new NotSupportedException());
        public Task<StockMovement> RecordReturnAsync(MaterialItem material, Guid? branchId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber = null, Guid? workOrderId = null, Guid? materialRequestId = null, CancellationToken cancellationToken = default) => Task.FromException<StockMovement>(new NotSupportedException());
        public Task<StockMovement> RecordTransferOutAsync(MaterialItem material, Guid fromBranchId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber = null, CancellationToken cancellationToken = default) => Task.FromException<StockMovement>(new NotSupportedException());
        public Task<StockMovement> RecordTransferInAsync(MaterialItem material, Guid toBranchId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber = null, CancellationToken cancellationToken = default) => Task.FromException<StockMovement>(new NotSupportedException());
        public Task RecordVanLoadAsync(MaterialItem material, Guid fromBranchId, Guid vanId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber = null, CancellationToken cancellationToken = default) => Task.FromException(new NotSupportedException());
        public Task RecordVanReturnAsync(MaterialItem material, Guid vanId, Guid toBranchId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber = null, CancellationToken cancellationToken = default) => Task.FromException(new NotSupportedException());
        public Task<StockMovement> RecordVanConsumeAsync(MaterialItem material, Guid vanId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber = null, Guid? workOrderId = null, CancellationToken cancellationToken = default) => Task.FromException<StockMovement>(new NotSupportedException());
        public Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default) => operation(cancellationToken);
    }

    private sealed class NoOpPdfRenderer : IPdfRenderer
    {
        public byte[] RenderWorkOrderReportPdf(string title, IReadOnlyCollection<string> sections) => [];
        public byte[] RenderWorkOrderReportPdf(WorkOrderReportPdfModel report) => [];
        public byte[] RenderInvoicePdf(InvoicePdfModel model) => [];
        public byte[] RenderQuotationPdf(QuotationPdfModel model) => [];
        public byte[] RenderReceiptPdf(ReceiptPdfModel model) => [];
        public byte[] RenderClientStatementPdf(ClientStatementPdfModel model) => [];
        public byte[] RenderPurchaseOrderPdf(PurchaseOrderPdfModel model) => [];
    }

    private sealed class NoOpLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
