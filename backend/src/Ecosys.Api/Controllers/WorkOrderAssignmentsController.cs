using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/workorders/{workOrderId:guid}")]
public sealed class WorkOrderAssignmentsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IBranchAccessService branchAccessService,
    IWorkOrderAssignmentWorkflowService assignmentWorkflowService) : TenantAwareControllerBase(tenantContext)
{
    [HttpPost("assign-group")]
    public async Task<ActionResult<WorkOrderResponse>> AssignGroup(Guid workOrderId, [FromBody] AssignGroupActionRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanAssignWorkOrders);
        await EnsureAccessibleAsync(workOrderId, cancellationToken);
        var currentTechnicianIds = await dbContext.WorkOrderTechnicianAssignments
            .Where(x => x.TenantId == TenantId && x.WorkOrderId == workOrderId)
            .Select(x => x.TechnicianId)
            .ToListAsync(cancellationToken);
        var leadTechnicianId = await dbContext.WorkOrderTechnicianAssignments
            .Where(x => x.TenantId == TenantId && x.WorkOrderId == workOrderId && x.IsLead)
            .Select(x => (Guid?)x.TechnicianId)
            .FirstOrDefaultAsync(cancellationToken);

        await assignmentWorkflowService.ApplyAssignmentsAsync(TenantId, workOrderId, UserId, new WorkOrderAssignmentUpdateRequest(request.AssignmentGroupId, currentTechnicianIds, leadTechnicianId, request.Notes), true, cancellationToken);
        return Ok(await LoadResponseAsync(workOrderId, cancellationToken));
    }

    [HttpPost("assign-technicians")]
    public async Task<ActionResult<WorkOrderResponse>> AssignTechnicians(Guid workOrderId, [FromBody] AssignTechniciansActionRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanAssignWorkOrders);
        await EnsureAccessibleAsync(workOrderId, cancellationToken);
        var assignmentGroupId = await dbContext.WorkOrderAssignments
            .Where(x => x.TenantId == TenantId && x.WorkOrderId == workOrderId)
            .Select(x => x.AssignmentGroupId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!assignmentGroupId.HasValue)
        {
            throw new BusinessRuleException("Select an assignment group first before choosing technicians.");
        }

        if (request.TechnicianIds is null || request.TechnicianIds.Count == 0)
        {
            throw new BusinessRuleException("Select at least one technician.");
        }

        await assignmentWorkflowService.ApplyAssignmentsAsync(TenantId, workOrderId, UserId, new WorkOrderAssignmentUpdateRequest(assignmentGroupId, request.TechnicianIds, request.LeadTechnicianId, request.Notes), true, cancellationToken);
        return Ok(await LoadResponseAsync(workOrderId, cancellationToken));
    }

    [HttpPost("reassign")]
    public async Task<ActionResult<WorkOrderResponse>> Reassign(Guid workOrderId, [FromBody] ReassignActionRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanAssignWorkOrders);
        await EnsureAccessibleAsync(workOrderId, cancellationToken);

        if (request.TechnicianIds is not null && request.TechnicianIds.Count > 0 && !request.AssignmentGroupId.HasValue)
        {
            throw new BusinessRuleException("Select an assignment group first before choosing technicians.");
        }

        await assignmentWorkflowService.ApplyAssignmentsAsync(TenantId, workOrderId, UserId, new WorkOrderAssignmentUpdateRequest(request.AssignmentGroupId, request.TechnicianIds, request.LeadTechnicianId, request.Notes), true, cancellationToken);
        return Ok(await LoadResponseAsync(workOrderId, cancellationToken));
    }

    [HttpGet("assignment-history")]
    public async Task<ActionResult<IReadOnlyCollection<WorkOrderAssignmentHistoryItem>>> GetHistory(Guid workOrderId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanViewWorkOrders);
        await EnsureAccessibleAsync(workOrderId, cancellationToken);
        var history = await assignmentWorkflowService.GetHistoryAsync(TenantId, workOrderId, cancellationToken);
        return Ok(history.Select(x => new WorkOrderAssignmentHistoryItem(x.Id, x.Action, x.FromGroupId, x.FromGroup?.Name, x.ToGroupId, x.ToGroup?.Name, x.FromTechnicianId, x.FromTechnician?.FullName, x.ToTechnicianId, x.ToTechnician?.FullName, x.PerformedByUserId, x.PerformedByUser?.FullName, x.PerformedAt, x.Notes)).ToList());
    }

    [HttpPost("technician-response")]
    public async Task<ActionResult<WorkOrderResponse>> TechnicianResponse(Guid workOrderId, [FromBody] TechnicianResponseActionRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        await EnsureAccessibleAsync(workOrderId, cancellationToken);
        await assignmentWorkflowService.RecordTechnicianResponseAsync(TenantId, workOrderId, UserId, new TechnicianDispatchResponseRequest(request.TechnicianId, request.Response, request.Notes), cancellationToken);
        return Ok(await LoadResponseAsync(workOrderId, cancellationToken));
    }

    [HttpPost("arrival")]
    public async Task<ActionResult<WorkOrderResponse>> Arrival(Guid workOrderId, [FromBody] ArrivalActionRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        await EnsureAccessibleAsync(workOrderId, cancellationToken);
        await assignmentWorkflowService.RecordArrivalAsync(TenantId, workOrderId, UserId, new TechnicianArrivalRequest(request.TechnicianId, request.Latitude, request.Longitude, request.ArrivedAt, null), cancellationToken);
        return Ok(await LoadResponseAsync(workOrderId, cancellationToken));
    }

    [HttpPost("departure")]
    public async Task<ActionResult<WorkOrderResponse>> Departure(Guid workOrderId, [FromBody] DepartureActionRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        await EnsureAccessibleAsync(workOrderId, cancellationToken);
        await assignmentWorkflowService.RecordDepartureAsync(TenantId, workOrderId, UserId, new TechnicianDepartureRequest(request.TechnicianId, request.Latitude, request.Longitude, request.DepartedAt, null), cancellationToken);
        await SyncSiteVisitTimestampsAsync(workOrderId, cancellationToken);
        return Ok(await LoadResponseAsync(workOrderId, cancellationToken));
    }

    /// <summary>
    /// Every trip each technician has made to this job (travel start, arrival, departure) with travel
    /// and on-site totals, so multi-day jobs show the whole history. <c>State</c> per technician drives
    /// which action the UI offers next: Idle → Travelling → OnSite → Idle.
    /// </summary>
    [HttpGet("visits")]
    public async Task<ActionResult<WorkOrderVisitsResponse>> GetVisits(Guid workOrderId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        await EnsureAccessibleAsync(workOrderId, cancellationToken);

        var visits = await dbContext.WorkOrderVisits
            .AsNoTracking()
            .Include(x => x.Technician)
            .Where(x => x.TenantId == TenantId && x.WorkOrderId == workOrderId)
            .OrderBy(x => x.TravelStartedAt ?? x.ArrivedAt ?? x.CreatedAt)
            .ToListAsync(cancellationToken);

        var rows = visits.Select(x => new WorkOrderVisitResponse(
            x.Id, x.TechnicianId, x.Technician?.FullName ?? "Technician",
            x.TravelStartedAt, x.ArrivedAt, x.DepartedAt, x.TravelMinutes, x.OnSiteMinutes, x.Notes)).ToList();

        var technicians = visits
            .GroupBy(x => x.TechnicianId)
            .Select(group =>
            {
                var open = group.Where(x => x.DepartedAt == null).OrderByDescending(x => x.CreatedAt).FirstOrDefault();
                var state = open is null ? "Idle" : open.ArrivedAt is null ? "Travelling" : "OnSite";
                return new WorkOrderVisitTechnicianSummary(
                    group.Key,
                    group.First().Technician?.FullName ?? "Technician",
                    state,
                    group.Count(),
                    group.Select(x => (x.ArrivedAt ?? x.TravelStartedAt ?? x.CreatedAt).Date).Distinct().Count(),
                    group.Sum(x => x.TravelMinutes),
                    group.Sum(x => x.OnSiteMinutes));
            })
            .ToList();

        return Ok(new WorkOrderVisitsResponse(rows, technicians, technicians.Sum(x => x.TravelMinutes), technicians.Sum(x => x.OnSiteMinutes)));
    }

    /// <summary>
    /// Rolls the per-technician arrival/departure stamps up onto the work-order header.
    /// <para>
    /// EC-013: recording a departure only wrote <c>work_order_technician_assignments.DepartureAt</c>.
    /// The arrival path happens to refresh the header as a side effect of its assignment-status
    /// refresh; the departure path has no such refresh, so <c>work_orders.DepartureAt</c> — which the
    /// detail DTO exposes as <c>departureAt</c> — stayed null forever.
    /// </para>
    /// <para>
    /// Multi-technician convention (identical to the workflow service's own legacy-field sync):
    /// the header arrival is the <b>earliest</b> technician arrival ("the crew got on site") and the
    /// header departure is the <b>latest</b> technician departure ("the crew left site"). Both are
    /// recomputed from the assignment rows, so the roll-up is idempotent and self-correcting.
    /// </para>
    /// </summary>
    private async Task SyncSiteVisitTimestampsAsync(Guid workOrderId, CancellationToken cancellationToken)
    {
        var workOrder = await dbContext.WorkOrders
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == workOrderId, cancellationToken);
        if (workOrder is null)
        {
            return;
        }

        var stamps = await dbContext.WorkOrderTechnicianAssignments
            .Where(x => x.TenantId == TenantId && x.WorkOrderId == workOrderId)
            .Select(x => new { x.ArrivalAt, x.DepartureAt })
            .ToListAsync(cancellationToken);

        // Min/Max over Nullable<DateTime> ignore nulls and yield null for an empty set. Only write
        // when there is a stamp to write, so this never clears a header value recorded by another
        // path (e.g. the lifecycle service, which stamps arrival without a technician row).
        var earliestArrival = stamps.Min(x => x.ArrivalAt);
        var latestDeparture = stamps.Max(x => x.DepartureAt);

        if (earliestArrival.HasValue)
        {
            workOrder.ArrivalAt = earliestArrival;
        }

        if (latestDeparture.HasValue)
        {
            workOrder.DepartureAt = latestDeparture;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureAccessibleAsync(Guid workOrderId, CancellationToken cancellationToken)
    {
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var exists = await dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId && x.Id == workOrderId)
            .WhereAccessible(scope, x => x.BranchId)
            .AnyAsync(cancellationToken);

        if (!exists)
        {
            throw new NotFoundException("Work order was not found.");
        }
    }

    private async Task<WorkOrderResponse> LoadResponseAsync(Guid workOrderId, CancellationToken cancellationToken)
    {
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var workOrder = await dbContext.WorkOrders
            .Include(x => x.Client)
            .Include(x => x.Asset)
            .Include(x => x.AssignedTechnician)
            .Include(x => x.LeadTechnician)
            .Include(x => x.AssignmentGroup)
            .Include(x => x.Branch)
            .Include(x => x.Assignments)
            .ThenInclude(x => x.AssignmentGroup)
            .Include(x => x.Events)
            .Include(x => x.TechnicianAssignments)
            .ThenInclude(x => x.Technician)
            .Include(x => x.Site)
            .Where(x => x.TenantId == TenantId && x.Id == workOrderId)
            .WhereAccessible(scope, x => x.BranchId)
            .SingleAsync(cancellationToken);

        var holdDetails = ResolveActiveHoldDetails(workOrder);
        // EC-019: same derivation the work orders list/detail endpoints use, so the assignment
        // endpoints cannot report a different assignment picture for the same work order.
        var assignment = WorkOrderAssignmentView.Resolve(workOrder);

        return new WorkOrderResponse(
            workOrder.Id,
            workOrder.BranchId,
            workOrder.Branch?.Name,
            workOrder.ClientId,
            workOrder.Client?.ClientName,
            workOrder.SiteId,
            workOrder.Site?.SiteName,
            workOrder.AssetId,
            workOrder.Asset?.AssetName,
            workOrder.AssignmentGroupId,
            workOrder.AssignmentGroup?.Name,
            workOrder.WorkOrderNumber,
            workOrder.Title,
            workOrder.Description,
            workOrder.Priority,
            workOrder.Status,
            WorkOrderTypes.Normalize(workOrder.WorkType, WorkOrderTypes.CorrectiveMaintenance),
            workOrder.SlaResolutionBreached ? "Resolution Breached" : workOrder.SlaResponseBreached ? "Response Breached" : workOrder.SlaResolutionDeadline.HasValue ? "On Track" : "Not Configured",
            workOrder.AssignmentType,
            assignment.PrimaryTechnicianId,
            assignment.PrimaryTechnicianName,
            assignment.TechnicianIds,
            workOrder.LeadTechnicianId,
            workOrder.LeadTechnician?.FullName,
            workOrder.DueDate,
            workOrder.CreatedAt,
            workOrder.WorkStartedAt,
            workOrder.ArrivalAt,
            workOrder.DepartureAt,
            workOrder.CompletedAt,
            workOrder.WorkDoneNotes,
            workOrder.JobCardNotes,
            workOrder.SlaResponseDeadline,
            workOrder.SlaResolutionDeadline,
            workOrder.SlaResponseBreached,
            workOrder.SlaResolutionBreached,
            workOrder.SlaResponseBreachedAt,
            workOrder.SlaResolutionBreachedAt,
            workOrder.AcknowledgedByName,
            workOrder.AcknowledgementComments,
            workOrder.AcknowledgementDate,
            workOrder.IsPreventiveMaintenance,
            holdDetails.Reason,
            holdDetails.Note,
            assignment.AssignmentStatus,
            workOrder.Assignments.OrderByDescending(x => x.AssignedAt).Select(x => x.Notes).FirstOrDefault(),
            assignment.TechnicianAssignments,
            assignment.AssignmentSummary,
            assignment.IsUnassigned);
    }

    private static (string? Reason, string? Note) ResolveActiveHoldDetails(WorkOrder workOrder)
        => WorkOrderHoldReasons.ResolveCurrentDetails(workOrder);
}

public sealed record AssignGroupActionRequest(Guid? AssignmentGroupId, string? Notes);
public sealed record AssignTechniciansActionRequest(IReadOnlyCollection<Guid>? TechnicianIds, Guid? LeadTechnicianId, string? Notes);
public sealed record ReassignActionRequest(Guid? AssignmentGroupId, IReadOnlyCollection<Guid>? TechnicianIds, Guid? LeadTechnicianId, string? Notes);
public sealed record TechnicianResponseActionRequest(Guid TechnicianId, string Response, string? Notes);
public sealed record ArrivalActionRequest(Guid TechnicianId, decimal? Latitude, decimal? Longitude, DateTime? ArrivedAt);
public sealed record DepartureActionRequest(Guid TechnicianId, decimal? Latitude, decimal? Longitude, DateTime? DepartedAt);

public sealed record WorkOrderAssignmentHistoryItem(
    Guid Id,
    string Action,
    Guid? FromGroupId,
    string? FromGroupName,
    Guid? ToGroupId,
    string? ToGroupName,
    Guid? FromTechnicianId,
    string? FromTechnicianName,
    Guid? ToTechnicianId,
    string? ToTechnicianName,
    Guid? PerformedByUserId,
    string? PerformedByUserName,
    DateTime PerformedAt,
    string? Notes);

public sealed record WorkOrderVisitResponse(
    Guid Id, Guid TechnicianId, string TechnicianName,
    DateTime? TravelStartedAt, DateTime? ArrivedAt, DateTime? DepartedAt,
    int TravelMinutes, int OnSiteMinutes, string? Notes);

public sealed record WorkOrderVisitTechnicianSummary(
    Guid TechnicianId, string TechnicianName, string State, int VisitCount, int DaysOnJob, int TravelMinutes, int OnSiteMinutes);

public sealed record WorkOrderVisitsResponse(
    IReadOnlyList<WorkOrderVisitResponse> Visits,
    IReadOnlyList<WorkOrderVisitTechnicianSummary> Technicians,
    int TotalTravelMinutes,
    int TotalOnSiteMinutes);
