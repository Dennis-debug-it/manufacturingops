using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dispatch")]
public sealed class DispatchController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IBranchAccessService branchAccessService,
    IWorkOrderAssignmentWorkflowService assignmentWorkflowService,
    IServiceOpsNotifier serviceOpsNotifier) : TenantAwareControllerBase(tenantContext)
{
    private const double NairobiLatitude = -1.286389;
    private const double NairobiLongitude = 36.817223;
    private const double AverageTravelSpeedKmh = 30.0;

    [HttpGet("technician-locations")]
    public async Task<ActionResult<IReadOnlyCollection<TechnicianLocationResponse>>> GetTechnicianLocations(
        [FromQuery] Guid? branchId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, branchId, cancellationToken);

        var technicians = await dbContext.Technicians
            .Include(x => x.ActiveWorkOrder)
            .Where(x => x.TenantId == TenantId && x.IsActive)
            .WhereAccessible(scope, x => x.BranchId)
            .OrderBy(x => x.FullName)
            .ToListAsync(cancellationToken);

        var response = technicians.Select(Map).ToList();
        return Ok(response);
    }

    [HttpGet("technician-locations/{id:guid}/history")]
    public async Task<ActionResult<IReadOnlyCollection<TechnicianLocationHistoryPoint>>> GetTechnicianLocationHistory(
        Guid id, [FromQuery] string date, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();

        if (!DateOnly.TryParse(date, out var workDate))
        {
            throw new Ecosys.Shared.Errors.BusinessRuleException("A valid date (YYYY-MM-DD) is required.");
        }

        var technician = await dbContext.Technicians
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new Ecosys.Shared.Errors.NotFoundException("Technician was not found.");

        if (technician.UserId is null)
        {
            return Ok(Array.Empty<TechnicianLocationHistoryPoint>());
        }

        var dayStart = workDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        var history = await dbContext.WorkOrderEvents
            .Where(x => x.TenantId == TenantId
                && x.ActorUserId == technician.UserId
                && x.Latitude != null && x.Longitude != null
                && x.OccurredAt >= dayStart && x.OccurredAt < dayEnd)
            .OrderBy(x => x.OccurredAt)
            .Select(x => new TechnicianLocationHistoryPoint(
                x.Latitude!.Value,
                x.Longitude!.Value,
                x.OccurredAt,
                x.EventType,
                x.WorkOrderId))
            .ToListAsync(cancellationToken);

        return Ok(history);
    }

    [HttpGet("route-optimise")]
    public async Task<ActionResult<IReadOnlyCollection<RouteStopResponse>>> GetOptimisedRoute(
        [FromQuery] Guid technicianId, [FromQuery] string date, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();

        if (!DateOnly.TryParse(date, out var workDate))
        {
            throw new Ecosys.Shared.Errors.BusinessRuleException("A valid date (YYYY-MM-DD) is required.");
        }

        var technician = await dbContext.Technicians
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == technicianId, cancellationToken)
            ?? throw new Ecosys.Shared.Errors.NotFoundException("Technician was not found.");

        var dayStart = workDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        var workOrders = await dbContext.WorkOrders
            .Include(x => x.Client)
            .Include(x => x.Site)
            .Where(x => x.TenantId == TenantId
                && x.AssignedTechnicianId == technicianId
                && x.ScheduledStartAt != null
                && x.ScheduledStartAt >= dayStart && x.ScheduledStartAt < dayEnd)
            .OrderBy(x => x.ScheduledStartAt)
            .ToListAsync(cancellationToken);

        var stops = workOrders.Select(wo =>
        {
            var (lat, lng) = ResolveWorkOrderCoordinates(wo);
            return new PendingStop(wo, lat, lng);
        }).ToList();

        var startLat = technician.LastKnownLatitude.HasValue ? (double)technician.LastKnownLatitude.Value : NairobiLatitude;
        var startLng = technician.LastKnownLongitude.HasValue ? (double)technician.LastKnownLongitude.Value : NairobiLongitude;

        var ordered = NearestNeighbourOrder(startLat, startLng, stops);

        var response = new List<RouteStopResponse>(ordered.Count);
        var fromLat = startLat;
        var fromLng = startLng;

        foreach (var stop in ordered)
        {
            var distanceKm = HaversineDistanceKm(fromLat, fromLng, stop.Latitude, stop.Longitude);
            var travelMins = Math.Max(5, (int)Math.Round(distanceKm / AverageTravelSpeedKmh * 60));

            response.Add(new RouteStopResponse(
                stop.WorkOrder.Id,
                stop.WorkOrder.WorkOrderNumber,
                stop.WorkOrder.Client?.ClientName ?? "Unknown Client",
                BuildSiteAddress(stop.WorkOrder.Site),
                (decimal)stop.Latitude,
                (decimal)stop.Longitude,
                stop.WorkOrder.ScheduledStartAt,
                travelMins));

            fromLat = stop.Latitude;
            fromLng = stop.Longitude;
        }

        return Ok(response);
    }

    [HttpGet("board")]
    public async Task<ActionResult<BoardResponse>> GetBoard(
        [FromQuery] string date, [FromQuery] Guid? branchId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanViewWorkOrders);

        if (!DateOnly.TryParse(date, out var workDate))
        {
            throw new BusinessRuleException("A valid date (YYYY-MM-DD) is required.");
        }

        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, branchId, cancellationToken);
        var dayStart = workDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        var technicians = await dbContext.Technicians
            .Where(x => x.TenantId == TenantId && x.IsActive)
            .WhereAccessible(scope, x => x.BranchId)
            .OrderBy(x => x.FullName)
            .ToListAsync(cancellationToken);

        var scheduledWorkOrders = await dbContext.WorkOrders
            .Include(x => x.Client)
            .Include(x => x.Site)
            .Include(x => x.ChecklistItems)
            .Where(x => x.TenantId == TenantId
                && !WorkOrderStatuses.ClosedStatuses.Contains(x.Status)
                && x.AssignedTechnicianId != null
                && x.ScheduledStartAt != null
                && x.ScheduledStartAt >= dayStart && x.ScheduledStartAt < dayEnd)
            .WhereAccessible(scope, x => x.BranchId)
            .ToListAsync(cancellationToken);

        var unassignedWorkOrders = await dbContext.WorkOrders
            .Include(x => x.Client)
            .Include(x => x.Site)
            .Include(x => x.ChecklistItems)
            .Where(x => x.TenantId == TenantId
                && !WorkOrderStatuses.ClosedStatuses.Contains(x.Status)
                && x.Status != WorkOrderStatuses.Completed
                && x.AssignedTechnicianId == null)
            .WhereAccessible(scope, x => x.BranchId)
            .ToListAsync(cancellationToken);

        var technicianBoard = technicians
            .Select(t => new BoardTechnicianResponse(
                t.Id,
                t.FullName,
                ResolveStatus(t),
                scheduledWorkOrders
                    .Where(wo => wo.AssignedTechnicianId == t.Id)
                    .OrderBy(wo => wo.ScheduledStartAt)
                    .Select(MapBoardWorkOrder)
                    .ToList()))
            .ToList();

        var unassigned = unassignedWorkOrders
            .OrderByDescending(wo => PriorityWeight(wo.Priority))
            .ThenBy(wo => wo.DueDate)
            .Select(MapBoardWorkOrder)
            .ToList();

        return Ok(new BoardResponse(technicianBoard, unassigned));
    }

    [HttpPatch("board/assign")]
    public async Task<ActionResult<BoardWorkOrderResponse>> AssignBoardWorkOrder(
        [FromBody] AssignBoardWorkOrderRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanAssignWorkOrders);

        if (request.ScheduledEnd <= request.ScheduledStart)
        {
            throw new BusinessRuleException("Scheduled end time must be after the start time.");
        }

        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);

        var workOrder = await dbContext.WorkOrders
            .Include(x => x.Client)
            .Include(x => x.Site)
            .Include(x => x.ChecklistItems)
            .Where(x => x.TenantId == TenantId && x.Id == request.WorkOrderId)
            .WhereAccessible(scope, x => x.BranchId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");

        if (WorkOrderStatuses.IsClosed(workOrder.Status) || string.Equals(workOrder.Status, WorkOrderStatuses.Completed, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("This work order is closed and cannot be scheduled.");
        }

        var technician = await dbContext.Technicians
            .Where(x => x.TenantId == TenantId && x.Id == request.TechnicianId && x.IsActive)
            .WhereAccessible(scope, x => x.BranchId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Technician was not found.");

        await EnsureNoOverlapAsync(technician.Id, request.ScheduledStart, request.ScheduledEnd, workOrder.Id, cancellationToken);

        var previousStatus = workOrder.Status;

        // EC-019: assignment goes through the one workflow that owns it. Writing
        // work_orders."AssignedTechnicianId" directly here left work_order_technician_assignments
        // with no row, so the board could assign a technician that the assignment model — and
        // therefore every list, report and dispatch load calculation reading that table — still
        // considered unassigned. ApplyAssignmentsAsync creates the row, records history and an
        // event, refreshes SLA, and syncs the legacy column, so the two can no longer disagree.
        var wasAssigned = workOrder.AssignedTechnicianId.HasValue;
        await assignmentWorkflowService.ApplyAssignmentsAsync(
            TenantId,
            workOrder.Id,
            UserId,
            new WorkOrderAssignmentUpdateRequest(
                AssignmentGroupId: null,
                TechnicianIds: [technician.Id],
                LeadTechnicianId: technician.Id,
                Notes: null),
            isReassignment: wasAssigned,
            cancellationToken);

        workOrder.ScheduledStartAt = request.ScheduledStart;
        workOrder.ScheduledEndAt = request.ScheduledEnd;

        // Scheduling runs after the assignment, which moves an unassigned work order to Assigned —
        // so Assigned has to count as schedulable here or the board would silently stop advancing
        // the status it is responsible for.
        if (workOrder.Status is WorkOrderStatuses.New or WorkOrderStatuses.Open
            or WorkOrderStatuses.Reopened or WorkOrderStatuses.Assigned)
        {
            workOrder.Status = WorkOrderStatuses.Scheduled;
        }

        dbContext.WorkOrderEvents.Add(new WorkOrderEvent
        {
            TenantId = TenantId,
            WorkOrderId = workOrder.Id,
            ActorUserId = UserId,
            EventType = "Scheduled",
            OldStatus = previousStatus,
            NewStatus = workOrder.Status,
            Status = workOrder.Status,
            Message = $"Assigned to {technician.FullName} and scheduled for {request.ScheduledStart:g}-{request.ScheduledEnd:t}.",
            OccurredAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await serviceOpsNotifier.NotifyWorkOrderUpdatedAsync(TenantId, workOrder.Id, cancellationToken);

        return Ok(MapBoardWorkOrder(workOrder));
    }

    [HttpPatch("board/unassign")]
    public async Task<ActionResult<BoardWorkOrderResponse>> UnassignBoardWorkOrder(
        [FromBody] UnassignBoardWorkOrderRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanAssignWorkOrders);

        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var workOrder = await dbContext.WorkOrders
            .Include(x => x.Client)
            .Include(x => x.Site)
            .Include(x => x.ChecklistItems)
            .Where(x => x.TenantId == TenantId && x.Id == request.WorkOrderId)
            .WhereAccessible(scope, x => x.BranchId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");

        if (string.Equals(workOrder.Status, WorkOrderStatuses.InProgress, StringComparison.OrdinalIgnoreCase) || WorkOrderStatuses.IsClosed(workOrder.Status))
        {
            throw new BusinessRuleException("Work already in progress or closed cannot be unassigned from the board.");
        }

        var previousStatus = workOrder.Status;

        // EC-019, the other half: clearing only the legacy column left the technician's assignment
        // row behind, so an "unassigned" work order still counted against that technician's load.
        // An empty assignment set removes the rows and re-syncs the legacy column to null.
        await assignmentWorkflowService.ApplyAssignmentsAsync(
            TenantId,
            workOrder.Id,
            UserId,
            new WorkOrderAssignmentUpdateRequest(
                AssignmentGroupId: null,
                TechnicianIds: [],
                LeadTechnicianId: null,
                Notes: null),
            isReassignment: false,
            cancellationToken);

        workOrder.ScheduledStartAt = null;
        workOrder.ScheduledEndAt = null;

        if (workOrder.Status is WorkOrderStatuses.Assigned or WorkOrderStatuses.Scheduled)
        {
            workOrder.Status = WorkOrderStatuses.Open;
        }

        dbContext.WorkOrderEvents.Add(new WorkOrderEvent
        {
            TenantId = TenantId,
            WorkOrderId = workOrder.Id,
            ActorUserId = UserId,
            EventType = "Unassigned",
            OldStatus = previousStatus,
            NewStatus = workOrder.Status,
            Status = workOrder.Status,
            Message = "Removed from technician schedule via dispatch board.",
            OccurredAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await serviceOpsNotifier.NotifyWorkOrderUpdatedAsync(TenantId, workOrder.Id, cancellationToken);

        return Ok(MapBoardWorkOrder(workOrder));
    }

    [HttpPatch("board/reschedule")]
    public async Task<ActionResult<BoardWorkOrderResponse>> RescheduleBoardWorkOrder(
        [FromBody] RescheduleBoardWorkOrderRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanAssignWorkOrders);

        if (request.ScheduledEnd <= request.ScheduledStart)
        {
            throw new BusinessRuleException("Scheduled end time must be after the start time.");
        }

        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var workOrder = await dbContext.WorkOrders
            .Include(x => x.Client)
            .Include(x => x.Site)
            .Include(x => x.ChecklistItems)
            .Where(x => x.TenantId == TenantId && x.Id == request.WorkOrderId)
            .WhereAccessible(scope, x => x.BranchId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");

        if (!workOrder.AssignedTechnicianId.HasValue)
        {
            throw new BusinessRuleException("Assign a technician before rescheduling this work order.");
        }

        await EnsureNoOverlapAsync(workOrder.AssignedTechnicianId.Value, request.ScheduledStart, request.ScheduledEnd, workOrder.Id, cancellationToken);

        workOrder.ScheduledStartAt = request.ScheduledStart;
        workOrder.ScheduledEndAt = request.ScheduledEnd;

        dbContext.WorkOrderEvents.Add(new WorkOrderEvent
        {
            TenantId = TenantId,
            WorkOrderId = workOrder.Id,
            ActorUserId = UserId,
            EventType = "Rescheduled",
            Status = workOrder.Status,
            Message = $"Rescheduled to {request.ScheduledStart:g}-{request.ScheduledEnd:t}.",
            OccurredAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await serviceOpsNotifier.NotifyWorkOrderUpdatedAsync(TenantId, workOrder.Id, cancellationToken);

        return Ok(MapBoardWorkOrder(workOrder));
    }

    private async Task EnsureNoOverlapAsync(Guid technicianId, DateTime start, DateTime end, Guid excludeWorkOrderId, CancellationToken cancellationToken)
    {
        var overlaps = await dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId
                && x.AssignedTechnicianId == technicianId
                && x.Id != excludeWorkOrderId
                && !WorkOrderStatuses.ClosedStatuses.Contains(x.Status)
                && x.ScheduledStartAt != null && x.ScheduledEndAt != null
                && x.ScheduledStartAt < end && x.ScheduledEndAt > start)
            .AnyAsync(cancellationToken);

        if (overlaps)
        {
            throw new BusinessRuleException("This technician already has a work order scheduled during that time slot.");
        }
    }

    private static int PriorityWeight(string priority) => priority switch
    {
        WorkOrderPriorities.Critical => 4,
        WorkOrderPriorities.High => 3,
        WorkOrderPriorities.Medium => 2,
        WorkOrderPriorities.Low => 1,
        _ => 0
    };

    private static BoardWorkOrderResponse MapBoardWorkOrder(WorkOrder workOrder) => new(
        workOrder.Id,
        workOrder.WorkOrderNumber,
        workOrder.Client?.ClientName ?? "Unknown Client",
        BuildSiteAddress(workOrder.Site),
        workOrder.Priority,
        (int)Math.Round((workOrder.EstimatedDuration ?? 1m) * 60m),
        workOrder.ScheduledStartAt,
        workOrder.ScheduledEndAt,
        workOrder.Status,
        workOrder.Description,
        workOrder.ChecklistItems.Count);

    private static TechnicianLocationResponse Map(Technician technician)
    {
        var status = ResolveStatus(technician);
        return new TechnicianLocationResponse(
            technician.Id,
            technician.FullName,
            status,
            technician.LastKnownLatitude,
            technician.LastKnownLongitude,
            technician.LastLocationAt,
            technician.ActiveWorkOrderId,
            technician.ActiveWorkOrder?.WorkOrderNumber);
    }

    private static string ResolveStatus(Technician technician)
    {
        if (!technician.IsTrackingActive)
        {
            return "offline";
        }

        return technician.ActiveWorkOrderId.HasValue ? "on-job" : "available";
    }

    private static string BuildSiteAddress(Site? site)
    {
        if (site is null)
        {
            return "Address not on file";
        }

        var parts = new[] { site.StreetAddress, site.AreaEstate, site.TownCity, site.County }
            .Where(part => !string.IsNullOrWhiteSpace(part));
        var joined = string.Join(", ", parts);
        return string.IsNullOrWhiteSpace(joined) ? site.SiteName : joined;
    }

    /// <summary>
    /// Sites don't yet carry real coordinates, so stops without one get a stable jittered
    /// point around Nairobi derived from the site/work order id — deterministic per record,
    /// distinct across records, and replaceable once Site.Latitude/Longitude exist.
    /// </summary>
    private static (double Latitude, double Longitude) ResolveWorkOrderCoordinates(WorkOrder workOrder)
    {
        var seed = workOrder.SiteId ?? workOrder.Id;
        var bytes = seed.ToByteArray();

        // Seed IDs are often sequential (e.g. demo fixtures differing only in the trailing
        // digits), so hash1 folds every byte via FNV-1a and hash2 is an avalanche mix of
        // hash1 (MurmurHash3 finalizer) rather than a second hash over half the bytes —
        // splitting by byte position left latitude depending only on even-indexed bytes,
        // which collided whenever two GUIDs only differed in their last (odd) byte.
        uint hash1 = 2166136261;
        foreach (var b in bytes)
        {
            hash1 = (hash1 ^ b) * 16777619;
        }

        var hash2 = hash1;
        hash2 ^= hash2 >> 16;
        hash2 *= 0x85ebca6b;
        hash2 ^= hash2 >> 13;
        hash2 *= 0xc2b2ae35;
        hash2 ^= hash2 >> 16;

        var latOffset = (hash1 % 2000) / 10000.0 - 0.1;
        var lngOffset = (hash2 % 2000) / 10000.0 - 0.1;
        return (NairobiLatitude + latOffset, NairobiLongitude + lngOffset);
    }

    private static List<PendingStop> NearestNeighbourOrder(double startLat, double startLng, List<PendingStop> stops)
    {
        var remaining = new List<PendingStop>(stops);
        var ordered = new List<PendingStop>(stops.Count);
        var currentLat = startLat;
        var currentLng = startLng;

        while (remaining.Count > 0)
        {
            var nearestIndex = 0;
            var nearestDistance = double.MaxValue;

            for (var i = 0; i < remaining.Count; i++)
            {
                var distance = HaversineDistanceKm(currentLat, currentLng, remaining[i].Latitude, remaining[i].Longitude);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = i;
                }
            }

            var nearest = remaining[nearestIndex];
            ordered.Add(nearest);
            remaining.RemoveAt(nearestIndex);
            currentLat = nearest.Latitude;
            currentLng = nearest.Longitude;
        }

        return ordered;
    }

    private static double HaversineDistanceKm(double lat1, double lng1, double lat2, double lng2)
    {
        const double earthRadiusKm = 6371.0;
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLng = DegreesToRadians(lng2 - lng1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2))
            * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusKm * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

    private sealed record PendingStop(WorkOrder WorkOrder, double Latitude, double Longitude);
}

public sealed record TechnicianLocationResponse(
    Guid Id,
    string FullName,
    string Status,
    decimal? Latitude,
    decimal? Longitude,
    DateTime? LastLocationAt,
    Guid? CurrentWorkOrderId,
    string? CurrentWorkOrderNumber);

public sealed record TechnicianLocationHistoryPoint(
    decimal Latitude,
    decimal Longitude,
    DateTime OccurredAt,
    string EventType,
    Guid WorkOrderId);

public sealed record RouteStopResponse(
    Guid Id,
    string WoNumber,
    string ClientName,
    string SiteAddress,
    decimal Lat,
    decimal Lng,
    DateTime? ScheduledTime,
    int EstimatedTravelMins);

public sealed record BoardResponse(
    IReadOnlyCollection<BoardTechnicianResponse> Technicians,
    IReadOnlyCollection<BoardWorkOrderResponse> Unassigned);

public sealed record BoardTechnicianResponse(
    Guid Id,
    string FullName,
    string Status,
    IReadOnlyCollection<BoardWorkOrderResponse> WorkOrders);

public sealed record BoardWorkOrderResponse(
    Guid Id,
    string WoNumber,
    string ClientName,
    string SiteAddress,
    string Priority,
    int EstimatedDurationMins,
    DateTime? ScheduledStart,
    DateTime? ScheduledEnd,
    string Status,
    string? Description,
    int ChecklistCount);

public sealed record AssignBoardWorkOrderRequest(Guid WorkOrderId, Guid TechnicianId, DateTime ScheduledStart, DateTime ScheduledEnd);
public sealed record UnassignBoardWorkOrderRequest(Guid WorkOrderId);
public sealed record RescheduleBoardWorkOrderRequest(Guid WorkOrderId, DateTime ScheduledStart, DateTime ScheduledEnd);
