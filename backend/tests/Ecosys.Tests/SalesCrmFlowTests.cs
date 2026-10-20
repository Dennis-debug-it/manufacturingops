using System.Security.Claims;
using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

/// <summary>The CRM hand-off: a prospect deal is won, becomes a client, and turns into work orders.</summary>
public sealed class SalesCrmFlowTests
{
    [Fact]
    public async Task WonProspect_BecomesClient_AndConvertsToLinkedWorkOrder()
    {
        await using var dbContext = CreateDbContext();
        var (tenant, actor) = await SeedAsync(dbContext);
        var crm = CreateCrmController(dbContext, tenant.Id, actor);

        var created = await crm.CreateOpportunity(
            new UpsertSalesOpportunityRequest(
                "Rooftop solar for warehouse", null, null,
                "Baraka Logistics", "Wanjiru Otieno", "wanjiru@baraka.test", "+254700000200", "Mombasa Road",
                "Referral", WorkOrderTypes.NewInstallation, "20 kWp grid-tie system.",
                1_250_000m, "KES", null, null, null),
            CancellationToken.None);
        var opportunity = Assert.IsType<SalesOpportunityDetailResponse>(Assert.IsType<CreatedAtActionResult>(created.Result).Value);
        Assert.Equal(SalesStages.Lead, opportunity.Stage);
        Assert.Equal(10, opportunity.Probability);
        Assert.Equal(actor.Id, opportunity.OwnerUserId);

        var wo = CreateWorkOrdersController(dbContext, tenant.Id, actor);

        // Not won yet: no work order.
        await Assert.ThrowsAsync<BusinessRuleException>(() => wo.CreateFromOpportunity(
            new CreateWorkOrderFromOpportunityRequest(opportunity.Id, null, null, null, null, null, null), CancellationToken.None));

        await crm.ChangeStage(opportunity.Id, new ChangeSalesStageRequest(SalesStages.Won, null), CancellationToken.None);

        // Won but still a prospect: the client has to exist first.
        await Assert.ThrowsAsync<BusinessRuleException>(() => wo.CreateFromOpportunity(
            new CreateWorkOrderFromOpportunityRequest(opportunity.Id, null, null, null, null, null, null), CancellationToken.None));

        var converted = Assert.IsType<SalesOpportunityDetailResponse>(Assert.IsType<OkObjectResult>(
            (await crm.ConvertToClient(opportunity.Id, CancellationToken.None)).Result).Value);
        Assert.NotNull(converted.ClientId);
        var client = await dbContext.Clients.SingleAsync(x => x.Id == converted.ClientId);
        Assert.Equal("Baraka Logistics", client.ClientName);
        Assert.Equal("Wanjiru Otieno", client.ContactPerson);

        var result = await wo.CreateFromOpportunity(
            new CreateWorkOrderFromOpportunityRequest(opportunity.Id, null, null, WorkOrderPriorities.High, null, null, null),
            CancellationToken.None);
        var workOrder = Assert.IsType<WorkOrderResponse>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.Equal(client.Id, workOrder.ClientId);
        Assert.Equal("Rooftop solar for warehouse", workOrder.Title);
        Assert.Equal(WorkOrderTypes.NewInstallation, workOrder.WorkType);
        Assert.Equal(opportunity.Id, workOrder.SalesOpportunityId);

        var detail = Assert.IsType<SalesOpportunityDetailResponse>(Assert.IsType<OkObjectResult>(
            (await crm.GetOpportunity(opportunity.Id, CancellationToken.None)).Result).Value);
        Assert.Equal(SalesStages.Won, detail.Stage);
        Assert.Single(detail.WorkOrders);
        Assert.Contains(detail.Activities, x => x.Summary.Contains("Work order"));
    }

    [Fact]
    public async Task MarkingLost_RequiresReason()
    {
        await using var dbContext = CreateDbContext();
        var (tenant, actor) = await SeedAsync(dbContext);
        var crm = CreateCrmController(dbContext, tenant.Id, actor);
        var created = await crm.CreateOpportunity(
            new UpsertSalesOpportunityRequest("Generator AMC", null, null, "Kilele Hotel", null, null, null, null,
                null, null, null, 300_000m, "KES", null, null, null),
            CancellationToken.None);
        var id = Assert.IsType<SalesOpportunityDetailResponse>(Assert.IsType<CreatedAtActionResult>(created.Result).Value).Id;

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            crm.ChangeStage(id, new ChangeSalesStageRequest(SalesStages.Lost, " "), CancellationToken.None));

        await crm.ChangeStage(id, new ChangeSalesStageRequest(SalesStages.Lost, "Went with a cheaper vendor"), CancellationToken.None);
        var stored = await dbContext.SalesOpportunities.SingleAsync(x => x.Id == id);
        Assert.Equal(SalesStages.Lost, stored.Stage);
        Assert.Equal(0, stored.Probability);
        Assert.NotNull(stored.LostAt);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-sales-crm-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<(Tenant Tenant, User Actor)> SeedAsync(AppDbContext dbContext)
    {
        var tenant = new Tenant
        {
            Name = "Acme",
            Slug = $"acme-{Guid.NewGuid():N}"[..12],
            CompanyName = "Acme Facilities",
            Email = "ops@acme.test",
            Country = "Kenya",
            Status = "Active",
            LicenseStatus = "Active",
            IsActive = true
        };
        var actor = new User
        {
            TenantId = tenant.Id,
            FullName = "Sales Admin",
            Email = $"sales-{Guid.NewGuid():N}@acme.test",
            PasswordHash = "hash",
            Role = AppRoles.Admin,
            JobTitle = "Admin",
            IsActive = true,
            HasAllBranchAccess = true
        };
        dbContext.AddRange(tenant, actor);
        await dbContext.SaveChangesAsync();
        return (tenant, actor);
    }

    private static ControllerContext CreateControllerContext(User actor) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, actor.Id.ToString()),
                new Claim(ClaimTypes.Name, actor.FullName),
                new Claim(ClaimTypes.Email, actor.Email)
            ], "Test"))
        }
    };

    private static SalesCrmController CreateCrmController(AppDbContext dbContext, Guid tenantId, User actor) =>
        new(
            dbContext,
            new FakeTenantContext(tenantId, actor.Id, actor.Email),
            new NoOpDocumentNumberingService(),
            new AuditLogService(dbContext, new HttpContextAccessor()),
            new NoOpUserAccessService(),
            new NoOpLicenseGuardService())
        {
            ControllerContext = CreateControllerContext(actor)
        };

    private static WorkOrdersController CreateWorkOrdersController(AppDbContext dbContext, Guid tenantId, User actor) =>
        new(
            dbContext,
            new FakeTenantContext(tenantId, actor.Id, actor.Email),
            new NoOpDocumentNumberingService(),
            new AuditLogService(dbContext, new HttpContextAccessor()),
            new NoOpUserAccessService(),
            new NoOpBranchAccessService(),
            new NoOpLicenseGuardService(),
            new NoOpWorkOrderLifecycleService(),
            new NoOpAssignmentWorkflowService(),
            new NoOpPmChecklistService(),
            new NoOpSlaService(),
            new NoOpWorkflowService())
        {
            ControllerContext = CreateControllerContext(actor)
        };

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
        public Task<bool> RefreshWorkOrderAsync(Guid workOrderId, DateTime nowUtc, CancellationToken cancellationToken = default) => Task.FromResult(false);
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
            Task.FromResult("WO-TEST-001");

        public Task<NumberingSetting> UpsertAsync(Guid tenantId, Guid? branchId, string documentType, string prefix, long nextNumber, int paddingLength, string resetFrequency, bool includeYear, bool includeMonth, bool isActive, CancellationToken cancellationToken = default, string? suffix = null, string yearFormat = "YYYY", string separator = "-", bool isLocked = false) =>
            throw new NotSupportedException();
    }

    private sealed class NoOpLicenseGuardService : ILicenseGuardService
    {
        public Task<TenantLicenseSnapshot> GetSnapshotAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LicenseUsageSnapshot> GetUsageAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PlatformLicenseUsageSnapshot>> GetPlatformUsageAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<PlatformLicenseUsageSnapshot>>([]);
        public Task EnsureTenantCanMutateAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureCanCreateBranchAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureCanCreateUserAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
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
        public string NormalizeStatus(string status) => WorkOrderStatuses.Normalize(status);
        public Task RecordCreatedAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, string workOrderNumber, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<WorkOrder> StartAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkOrder> HoldAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, string? holdType, string? reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkOrder> ResumeAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
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
}
