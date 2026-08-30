using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

/// <summary>
/// EC-019. <c>work_order_technician_assignments</c> is the source of truth for work order
/// assignment; <c>work_orders."AssignedTechnicianId"</c> is a derived mirror that
/// <c>WorkOrderAssignmentWorkflowService.SyncLegacyFieldsAsync</c> owns.
///
/// <para>
/// The dispatch board used to write that column directly and never create an assignment row, so a
/// work order could name a technician while every consumer of the assignment table — the list DTO's
/// <c>isUnassigned</c>, technician load, dispatch capacity — still counted it as unassigned. These
/// tests pin the contract that fixed it: the board delegates to the assignment workflow and does not
/// hand-write the mirror. A regression back to a direct column write fails here.
/// </para>
/// </summary>
public sealed class DispatchAssignmentInvariantTests
{
    [Fact]
    public async Task BoardAssign_DelegatesToTheAssignmentWorkflow_InsteadOfWritingTheLegacyColumn()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedAsync(dbContext);
        var workflow = new RecordingAssignmentWorkflowService();
        var controller = CreateController(dbContext, fixture, workflow);

        var start = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(1), DateTimeKind.Utc);
        var result = await controller.AssignBoardWorkOrder(
            new AssignBoardWorkOrderRequest(fixture.WorkOrderId, fixture.TechnicianId, start, start.AddHours(2)),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);

        var call = Assert.Single(workflow.Calls);
        Assert.Equal(fixture.WorkOrderId, call.WorkOrderId);
        Assert.Equal([fixture.TechnicianId], call.Request.TechnicianIds);
        Assert.Equal(fixture.TechnicianId, call.Request.LeadTechnicianId);
        Assert.Null(call.Request.AssignmentGroupId);

        // The mirror is the workflow's to maintain. The fake does not run it, so the column must
        // still be untouched here — if the controller sets it again, this fails.
        var workOrder = await dbContext.WorkOrders.SingleAsync(x => x.Id == fixture.WorkOrderId);
        Assert.Null(workOrder.AssignedTechnicianId);

        // What the board *is* responsible for still happens.
        Assert.Equal(start, workOrder.ScheduledStartAt);
        Assert.Equal(start.AddHours(2), workOrder.ScheduledEndAt);
        Assert.Equal(WorkOrderStatuses.Scheduled, workOrder.Status);
    }

    [Fact]
    public async Task BoardAssign_SchedulesAWorkOrderTheWorkflowHasAlreadyMovedToAssigned()
    {
        // Ordering guard: the workflow moves an unassigned work order to Assigned before the board
        // gets to schedule it. If Assigned is not treated as schedulable, the board silently stops
        // advancing the status it owns — which is invisible until someone looks at the board.
        await using var dbContext = CreateDbContext();
        var fixture = await SeedAsync(dbContext, status: WorkOrderStatuses.Assigned);
        var controller = CreateController(dbContext, fixture, new RecordingAssignmentWorkflowService());

        var start = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(1), DateTimeKind.Utc);
        await controller.AssignBoardWorkOrder(
            new AssignBoardWorkOrderRequest(fixture.WorkOrderId, fixture.TechnicianId, start, start.AddHours(2)),
            CancellationToken.None);

        var workOrder = await dbContext.WorkOrders.SingleAsync(x => x.Id == fixture.WorkOrderId);
        Assert.Equal(WorkOrderStatuses.Scheduled, workOrder.Status);
    }

    [Fact]
    public async Task BoardUnassign_ClearsTheAssignmentThroughTheWorkflow()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedAsync(dbContext, status: WorkOrderStatuses.Scheduled);
        var workflow = new RecordingAssignmentWorkflowService();
        var controller = CreateController(dbContext, fixture, workflow);

        var result = await controller.UnassignBoardWorkOrder(
            new UnassignBoardWorkOrderRequest(fixture.WorkOrderId), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);

        // An empty technician set is what removes the rows and re-syncs the mirror to null.
        var call = Assert.Single(workflow.Calls);
        Assert.Empty(call.Request.TechnicianIds ?? []);
        Assert.Null(call.Request.AssignmentGroupId);
        Assert.Null(call.Request.LeadTechnicianId);

        var workOrder = await dbContext.WorkOrders.SingleAsync(x => x.Id == fixture.WorkOrderId);
        Assert.Null(workOrder.ScheduledStartAt);
        Assert.Null(workOrder.ScheduledEndAt);
        Assert.Equal(WorkOrderStatuses.Open, workOrder.Status);
    }

    // ── fixture ───────────────────────────────────────────────────────────

    private sealed record Fixture(Guid TenantId, Guid UserId, Guid WorkOrderId, Guid TechnicianId);

    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-dispatch-{Guid.NewGuid():N}")
            .Options);

    private static async Task<Fixture> SeedAsync(AppDbContext dbContext, string status = WorkOrderStatuses.Open)
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var client = new Client { TenantId = tenantId, ClientName = "Acme Hospital", IsActive = true };
        var technician = new Technician
        {
            TenantId = tenantId,
            FullName = "Demo Technician",
            Email = $"tech-{Guid.NewGuid():N}@acme.test",
            IsActive = true,
        };
        dbContext.AddRange(client, technician);
        await dbContext.SaveChangesAsync();

        var workOrder = new WorkOrder
        {
            TenantId = tenantId,
            ClientId = client.Id,
            WorkOrderNumber = "WO-DISPATCH-1",
            Title = "Board probe",
            Status = status,
            Priority = "Medium",
            WorkType = "CorrectiveMaintenance",
        };
        dbContext.WorkOrders.Add(workOrder);
        await dbContext.SaveChangesAsync();

        return new Fixture(tenantId, userId, workOrder.Id, technician.Id);
    }

    private static DispatchController CreateController(
        AppDbContext dbContext, Fixture fixture, IWorkOrderAssignmentWorkflowService workflow) =>
        new(
            dbContext,
            new FakeTenantContext(fixture.TenantId, fixture.UserId),
            new NoOpUserAccessService(),
            new NoOpBranchAccessService(),
            workflow,
            new NoOpServiceOpsNotifier());

    /// <summary>Records what the controller asked the workflow to do, and does nothing else.</summary>
    private sealed class RecordingAssignmentWorkflowService : IWorkOrderAssignmentWorkflowService
    {
        public List<(Guid WorkOrderId, WorkOrderAssignmentUpdateRequest Request, bool IsReassignment)> Calls { get; } = [];

        public Task ApplyAssignmentsAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, WorkOrderAssignmentUpdateRequest request, bool isReassignment, CancellationToken cancellationToken = default)
        {
            Calls.Add((workOrderId, request, isReassignment));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<WorkOrderAssignmentHistory>> GetHistoryAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<WorkOrderAssignmentHistory>>([]);
        public Task<bool> HasAcceptedTechnicianAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> HasArrivalAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task RecordArrivalAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, TechnicianArrivalRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RecordDepartureAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, TechnicianDepartureRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RecordInTransitAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, TechnicianInTransitRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RecordTechnicianResponseAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, TechnicianDispatchResponseRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoOpServiceOpsNotifier : IServiceOpsNotifier
    {
        public Task NotifyWorkOrderUpdatedAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeTenantContext(Guid tenantId, Guid userId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
        public Guid? SessionId => Guid.NewGuid();
        public string? Email => "test@acme.test";
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

        public Task EnsureCanAccessBranchAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyCollection<Guid>> GetAccessibleBranchIdsAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Guid>>([]);
    }
}
