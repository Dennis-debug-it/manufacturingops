using System.Text.Json;
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
        return Ok(await LoadResponseAsync(workOrderId, cancellationToken));
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
        var technicianAssignments = workOrder.TechnicianAssignments
            .OrderByDescending(x => x.IsLead)
            .ThenBy(x => x.AssignedAt)
            .Select(x => new WorkOrderTechnicianAssignmentResponse(x.Id, x.TechnicianId, x.Technician?.FullName, x.IsLead, x.Status, x.AssignedAt, x.AcceptedAt, x.ArrivalAt, x.DepartureAt, x.Notes))
            .ToList();

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
            workOrder.AssignedTechnicianId,
            workOrder.AssignedTechnician?.FullName,
            ResolveAssignedTechnicianIds(workOrder),
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
            workOrder.Assignments.OrderByDescending(x => x.AssignedAt).Select(x => x.AssignmentStatus).FirstOrDefault() ?? "Unassigned",
            workOrder.Assignments.OrderByDescending(x => x.AssignedAt).Select(x => x.Notes).FirstOrDefault(),
            technicianAssignments,
            BuildAssignmentSummary(workOrder.AssignmentGroup?.Name, technicianAssignments),
            !workOrder.AssignmentGroupId.HasValue && technicianAssignments.Count == 0);
    }

    private static IReadOnlyCollection<Guid> ParseAssignedTechnicianIds(string? assignedTechnicianIdsJson)
    {
        if (string.IsNullOrWhiteSpace(assignedTechnicianIdsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(assignedTechnicianIdsJson) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyCollection<Guid> ResolveAssignedTechnicianIds(WorkOrder workOrder)
    {
        var relationalIds = workOrder.TechnicianAssignments
            .OrderByDescending(x => x.IsLead)
            .ThenBy(x => x.AssignedAt)
            .Select(x => x.TechnicianId)
            .Distinct()
            .ToList();

        if (relationalIds.Count > 0)
        {
            return relationalIds;
        }

        var legacyIds = ParseAssignedTechnicianIds(workOrder.AssignedTechnicianIdsJson);
        if (legacyIds.Count > 0)
        {
            return legacyIds;
        }

        return workOrder.AssignedTechnicianId.HasValue ? [workOrder.AssignedTechnicianId.Value] : [];
    }

    private static string BuildAssignmentSummary(string? groupName, IReadOnlyCollection<WorkOrderTechnicianAssignmentResponse> technicianAssignments)
    {
        if (technicianAssignments.Count == 0)
        {
            return groupName ?? "Unassigned";
        }

        var firstTechnician = technicianAssignments.First().TechnicianName ?? "Technician";
        if (!string.IsNullOrWhiteSpace(groupName) && technicianAssignments.Count == 1)
        {
            return $"{groupName} -> {firstTechnician}";
        }

        return technicianAssignments.Count == 1 ? firstTechnician : $"{firstTechnician} + {technicianAssignments.Count - 1} others";
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
