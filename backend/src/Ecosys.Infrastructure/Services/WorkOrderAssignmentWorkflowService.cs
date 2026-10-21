using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services;

public sealed record WorkOrderAssignmentUpdateRequest(
    Guid? AssignmentGroupId,
    IReadOnlyCollection<Guid>? TechnicianIds,
    Guid? LeadTechnicianId,
    string? Notes);

public sealed record TechnicianDispatchResponseRequest(Guid TechnicianId, string Response, string? Notes);
public sealed record TechnicianInTransitRequest(Guid TechnicianId, decimal? Latitude, decimal? Longitude, DateTime? DepartedForSiteAt, string? Notes);
public sealed record TechnicianArrivalRequest(Guid TechnicianId, decimal? Latitude, decimal? Longitude, DateTime? ArrivedAt, string? Notes);
public sealed record TechnicianDepartureRequest(Guid TechnicianId, decimal? Latitude, decimal? Longitude, DateTime? DepartedAt, string? Notes);

public interface IWorkOrderAssignmentWorkflowService
{
    Task ApplyAssignmentsAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, WorkOrderAssignmentUpdateRequest request, bool isReassignment, CancellationToken cancellationToken = default);
    Task RecordTechnicianResponseAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, TechnicianDispatchResponseRequest request, CancellationToken cancellationToken = default);
    Task RecordInTransitAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, TechnicianInTransitRequest request, CancellationToken cancellationToken = default);
    Task RecordArrivalAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, TechnicianArrivalRequest request, CancellationToken cancellationToken = default);
    Task RecordDepartureAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, TechnicianDepartureRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<WorkOrderAssignmentHistory>> GetHistoryAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default);
    Task<bool> HasAcceptedTechnicianAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default);
    Task<bool> HasArrivalAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default);
}

internal sealed class WorkOrderAssignmentWorkflowService(
    AppDbContext dbContext,
    IAuditLogService auditLogService,
    IEmailOutboxService emailOutboxService,
    IEmailTemplateService emailTemplateService,
    IEmailSubjectRuleService emailSubjectRuleService,
    ISlaService slaService,
    IEtaNotificationService etaNotificationService,
    INotificationDispatcher notificationDispatcher,
    ILogger<WorkOrderAssignmentWorkflowService> logger) : IWorkOrderAssignmentWorkflowService
{
    public async Task ApplyAssignmentsAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, WorkOrderAssignmentUpdateRequest request, bool isReassignment, CancellationToken cancellationToken = default)
    {
        var workOrder = await LoadWorkOrderAsync(tenantId, workOrderId, cancellationToken);
        var currentGroupAssignment = await dbContext.WorkOrderAssignments.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.WorkOrderId == workOrderId, cancellationToken);
        var currentTechnicianAssignments = await dbContext.WorkOrderTechnicianAssignments
            .Include(x => x.Technician)
            .Where(x => x.TenantId == tenantId && x.WorkOrderId == workOrderId)
            .ToListAsync(cancellationToken);

        var technicianIds = NormalizeTechnicianIds(request.TechnicianIds);
        if (request.LeadTechnicianId.HasValue && !technicianIds.Contains(request.LeadTechnicianId.Value))
        {
            throw new BusinessRuleException("Lead technician must be among selected technicians.");
        }

        AssignmentGroup? group = null;
        if (request.AssignmentGroupId.HasValue)
        {
            group = await dbContext.AssignmentGroups
                .Include(x => x.Members)
                .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == request.AssignmentGroupId.Value && x.IsActive, cancellationToken)
                ?? throw new BusinessRuleException("Cannot assign to inactive group.");
        }

        var technicians = technicianIds.Count == 0
            ? []
            : await dbContext.Technicians
                .Where(x => x.TenantId == tenantId && x.IsActive && technicianIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

        if (technicians.Count != technicianIds.Count)
        {
            throw new BusinessRuleException("Cannot assign inactive technician.");
        }

        if (group is not null && technicians.Count > 0)
        {
            var groupMemberIds = group.Members.Where(x => x.IsActive).Select(x => x.TechnicianId).ToHashSet();
            if (technicians.Any(x => !groupMemberIds.Contains(x.Id)))
            {
                throw new BusinessRuleException("Selected technicians must belong to the selected assignment group.");
            }
        }

        var previousGroupId = currentGroupAssignment?.AssignmentGroupId;
        if (previousGroupId != request.AssignmentGroupId)
        {
            await AddHistoryAsync(tenantId, workOrderId, isReassignment ? "GroupReassigned" : "GroupAssigned", previousGroupId, request.AssignmentGroupId, null, null, actorUserId, request.Notes, cancellationToken);
            await AddEventAsync(tenantId, workOrderId, actorUserId, "GroupAssignment", ResolveAssignmentStatus(workOrder.Status, request.AssignmentGroupId, currentTechnicianAssignments), request.AssignmentGroupId.HasValue ? "Work order assigned to group queue." : "Work order removed from group queue.", cancellationToken: cancellationToken);
        }

        var currentByTechnician = currentTechnicianAssignments.ToDictionary(x => x.TechnicianId);
        var newlyAssignedTechnicians = new List<Technician>();
        foreach (var removed in currentTechnicianAssignments.Where(x => !technicianIds.Contains(x.TechnicianId)).ToList())
        {
            dbContext.WorkOrderTechnicianAssignments.Remove(removed);
            await AddHistoryAsync(tenantId, workOrderId, isReassignment ? "TechnicianReassigned" : "TechnicianUnassigned", previousGroupId, request.AssignmentGroupId, removed.TechnicianId, null, actorUserId, request.Notes, cancellationToken);
            await AddEventAsync(tenantId, workOrderId, actorUserId, "TechnicianUnassigned", ResolveAssignmentStatus(workOrder.Status, request.AssignmentGroupId, currentTechnicianAssignments), $"{removed.Technician?.FullName ?? "Technician"} was removed from the work order.", cancellationToken: cancellationToken);
        }

        foreach (var technician in technicians)
        {
            if (!currentByTechnician.TryGetValue(technician.Id, out var assignment))
            {
                assignment = new WorkOrderTechnicianAssignment
                {
                    TenantId = tenantId,
                    WorkOrderId = workOrderId,
                    TechnicianId = technician.Id,
                    AssignedByUserId = actorUserId,
                    AssignedAt = DateTime.UtcNow,
                    Status = "Pending",
                    Notes = request.Notes?.Trim()
                };
                dbContext.WorkOrderTechnicianAssignments.Add(assignment);
                currentTechnicianAssignments.Add(assignment);
                newlyAssignedTechnicians.Add(technician);
                await AddHistoryAsync(tenantId, workOrderId, isReassignment ? "TechnicianReassigned" : "TechnicianAssigned", previousGroupId, request.AssignmentGroupId, null, technician.Id, actorUserId, request.Notes, cancellationToken);
                await AddEventAsync(tenantId, workOrderId, actorUserId, "TechnicianAssigned", ResolveAssignmentStatus(workOrder.Status, request.AssignmentGroupId, currentTechnicianAssignments), $"{technician.FullName} was assigned to the work order.", cancellationToken: cancellationToken);
            }

            assignment.IsLead = request.LeadTechnicianId.HasValue && technician.Id == request.LeadTechnicianId.Value;
            assignment.Notes = string.IsNullOrWhiteSpace(request.Notes) ? assignment.Notes : request.Notes.Trim();
        }

        if (!request.LeadTechnicianId.HasValue && technicianIds.Count == 1)
        {
            var singleAssignment = currentTechnicianAssignments.SingleOrDefault(x => x.TechnicianId == technicianIds[0]);
            if (singleAssignment is not null)
            {
                singleAssignment.IsLead = true;
            }
        }

        if (request.AssignmentGroupId.HasValue || technicianIds.Count > 0)
        {
            currentGroupAssignment ??= new WorkOrderAssignment { TenantId = tenantId, WorkOrderId = workOrderId };
            currentGroupAssignment.AssignmentGroupId = request.AssignmentGroupId;
            currentGroupAssignment.AssignedByUserId = actorUserId;
            currentGroupAssignment.AssignedAt = DateTime.UtcNow;
            currentGroupAssignment.Notes = request.Notes?.Trim();
            currentGroupAssignment.AssignmentStatus = ResolveAssignmentStatus(workOrder.Status, request.AssignmentGroupId, currentTechnicianAssignments);
            if (dbContext.Entry(currentGroupAssignment).State == EntityState.Detached)
            {
                dbContext.WorkOrderAssignments.Add(currentGroupAssignment);
            }
        }
        else if (currentGroupAssignment is not null)
        {
            dbContext.WorkOrderAssignments.Remove(currentGroupAssignment);
        }

        if (request.LeadTechnicianId.HasValue)
        {
            await AddHistoryAsync(tenantId, workOrderId, "LeadTechnicianAssigned", previousGroupId, request.AssignmentGroupId, null, request.LeadTechnicianId, actorUserId, request.Notes, cancellationToken);
            var lead = technicians.FirstOrDefault(x => x.Id == request.LeadTechnicianId.Value);
            await AddEventAsync(tenantId, workOrderId, actorUserId, "LeadTechnicianAssigned", ResolveAssignmentStatus(workOrder.Status, request.AssignmentGroupId, currentTechnicianAssignments), $"{lead?.FullName ?? "Lead technician"} assigned as lead technician.", cancellationToken: cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await SyncLegacyFieldsAsync(workOrder, request.AssignmentGroupId, cancellationToken);
        await slaService.RefreshWorkOrderAsync(workOrder.Id, DateTime.UtcNow, cancellationToken);

        if (newlyAssignedTechnicians.Count > 0)
        {
            await TryQueueAssignmentEmailsAsync(tenantId, workOrder, newlyAssignedTechnicians, cancellationToken);
            await AssignmentNotifications.NotifyAsync(notificationDispatcher, workOrder, newlyAssignedTechnicians, cancellationToken);
        }
    }

    private async Task TryQueueAssignmentEmailsAsync(Guid tenantId, WorkOrder workOrder, IReadOnlyList<Technician> technicians, CancellationToken cancellationToken)
    {
        try
        {
            var assetName = workOrder.AssetId.HasValue
                ? (await dbContext.Assets.SingleOrDefaultAsync(x => x.Id == workOrder.AssetId.Value, cancellationToken))?.AssetName
                : null;

            var dueDate = workOrder.DueDate?.ToString("yyyy-MM-dd") ?? "Not set";

            // A technician with a login can turn this email off; one without has no preferences.
            var userIds = technicians.Where(x => x.UserId.HasValue).Select(x => x.UserId!.Value).ToList();
            var emailOff = await dbContext.UserNotificationPreferences
                .Where(x => x.TenantId == tenantId && userIds.Contains(x.UserId) && x.NotificationKey == AssignmentNotifications.Key && !x.EmailEnabled)
                .Select(x => x.UserId)
                .ToListAsync(cancellationToken);

            foreach (var technician in technicians)
            {
                if (string.IsNullOrWhiteSpace(technician.Email) || (technician.UserId is { } userId && emailOff.Contains(userId)))
                    continue;

                var template = await emailTemplateService.RenderTenantTemplateAsync(
                    tenantId,
                    "work-order.assigned",
                    new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["AssignedTo"] = technician.FullName,
                        ["WorkOrderNumber"] = workOrder.WorkOrderNumber,
                        ["Priority"] = workOrder.Priority,
                        ["DueDate"] = dueDate,
                        ["AssetName"] = assetName ?? "N/A"
                    },
                    cancellationToken);

                if (!template.Enabled)
                    continue;

                var subject = await emailSubjectRuleService.BuildFinalSubjectAsync(
                    tenantId, "work-order.assigned", template.Subject, cancellationToken: cancellationToken);

                await emailOutboxService.QueueEmailAsync(
                    new QueueEmailRequest(
                        tenantId,
                        "work-order.assigned",
                        "work-order-assigned",
                        technician.Email,
                        technician.FullName,
                        template.SenderNameOverride,
                        null,
                        null,
                        subject,
                        template.HtmlBody,
                        template.TextBody,
                        null),
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to queue work-order.assigned emails for work order {WorkOrderId}.", workOrder.Id);
        }
    }

    public async Task RecordTechnicianResponseAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, TechnicianDispatchResponseRequest request, CancellationToken cancellationToken = default)
    {
        var assignment = await dbContext.WorkOrderTechnicianAssignments
            .Include(x => x.Technician)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.WorkOrderId == workOrderId && x.TechnicianId == request.TechnicianId, cancellationToken)
            ?? throw new BusinessRuleException("Technician is not currently assigned to this work order.");

        assignment.Status = request.Response switch
        {
            "Accepted" => "Accepted",
            "Declined" => "Declined",
            _ => throw new BusinessRuleException("Technician response must be Accepted or Declined.")
        };
        assignment.AcceptedAt = assignment.Status == "Accepted" ? OccurredAt.UtcNow : assignment.AcceptedAt;
        assignment.DeclinedAt = assignment.Status == "Declined" ? OccurredAt.UtcNow : assignment.DeclinedAt;

        await AddHistoryAsync(tenantId, workOrderId, assignment.Status == "Accepted" ? "TechnicianAccepted" : "TechnicianDeclined", null, null, request.TechnicianId, request.TechnicianId, actorUserId, request.Notes, cancellationToken);
        await AddEventAsync(tenantId, workOrderId, actorUserId, assignment.Status == "Accepted" ? "TechnicianAccepted" : "TechnicianDeclined", assignment.Status == "Accepted" ? "Accepted" : "AssignedToTechnician", $"{assignment.Technician?.FullName ?? "Technician"} {assignment.Status.ToLowerInvariant()} the job.", cancellationToken: cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RefreshAssignmentStatusAsync(tenantId, workOrderId, cancellationToken);
    }

    public async Task RecordInTransitAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, TechnicianInTransitRequest request, CancellationToken cancellationToken = default)
    {
        var assignment = await dbContext.WorkOrderTechnicianAssignments
            .Include(x => x.Technician)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.WorkOrderId == workOrderId && x.TechnicianId == request.TechnicianId, cancellationToken)
            ?? throw new BusinessRuleException("Technician is not currently assigned to this work order.");

        var travelStartedAt = request.DepartedForSiteAt ?? OccurredAt.UtcNow;
        var openVisit = await FindOpenVisitAsync(tenantId, workOrderId, request.TechnicianId, cancellationToken);
        if (openVisit?.ArrivedAt is not null)
        {
            throw new BusinessRuleException("Record the departure from site before starting the next trip.");
        }

        // Each trip is its own visit, so a job that runs over several days gets one visit per day.
        if (openVisit is null)
        {
            dbContext.WorkOrderVisits.Add(new WorkOrderVisit
            {
                TenantId = tenantId,
                WorkOrderId = workOrderId,
                TechnicianId = request.TechnicianId,
                TravelStartedAt = travelStartedAt,
                Notes = NormalizeNotes(request.Notes)
            });
        }

        assignment.Status = "InTransit";
        assignment.AcceptedAt ??= travelStartedAt;

        await UpdateTechnicianTrackingAsync(tenantId, request.TechnicianId, workOrderId, request.Latitude, request.Longitude, true, cancellationToken);
        await AddHistoryAsync(tenantId, workOrderId, "TechnicianInTransit", null, null, request.TechnicianId, request.TechnicianId, actorUserId, request.Notes, cancellationToken);
        await AddEventAsync(tenantId, workOrderId, actorUserId, "InTransit", "Accepted", $"{assignment.Technician?.FullName ?? "Technician"} is in transit to site.", request.Latitude, request.Longitude, cancellationToken);
        await SyncOwnAttendanceAsync(tenantId, assignment.Technician, workOrderId, actorUserId, TripEvent.Travelling, travelStartedAt, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RefreshAssignmentStatusAsync(tenantId, workOrderId, cancellationToken);
        await NotifyEtaAsync(tenantId, workOrderId, EtaNotificationEventTypes.EnRoute, assignment.Technician?.FullName, cancellationToken);
    }

    public async Task RecordArrivalAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, TechnicianArrivalRequest request, CancellationToken cancellationToken = default)
    {
        var assignment = await dbContext.WorkOrderTechnicianAssignments
            .Include(x => x.Technician)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.WorkOrderId == workOrderId && x.TechnicianId == request.TechnicianId, cancellationToken)
            ?? throw new BusinessRuleException("Arrival cannot be recorded before technician assignment.");

        var arrivedAt = request.ArrivedAt ?? OccurredAt.UtcNow;
        var visit = await FindOpenVisitAsync(tenantId, workOrderId, request.TechnicianId, cancellationToken);
        if (visit?.ArrivedAt is not null)
        {
            throw new BusinessRuleException("Arrival is already recorded for this visit. Record the departure first.");
        }

        if (visit is null)
        {
            // Arrived without "en route" being tapped: still a visit, just with no travel time.
            visit = new WorkOrderVisit { TenantId = tenantId, WorkOrderId = workOrderId, TechnicianId = request.TechnicianId };
            dbContext.WorkOrderVisits.Add(visit);
        }

        visit.ArrivedAt = arrivedAt;
        visit.TravelMinutes = MinutesBetween(visit.TravelStartedAt, arrivedAt);

        assignment.Status = "Arrived";
        // The first arrival is the response milestone (SLA, completion guard); later visits don't move it.
        assignment.ArrivalAt ??= arrivedAt;
        assignment.LastArrivalAt = arrivedAt;
        await UpdateTechnicianTrackingAsync(tenantId, request.TechnicianId, workOrderId, request.Latitude, request.Longitude, true, cancellationToken);
        await AddHistoryAsync(tenantId, workOrderId, "TechnicianArrived", null, null, request.TechnicianId, request.TechnicianId, actorUserId, request.Notes, cancellationToken);
        await AddEventAsync(tenantId, workOrderId, actorUserId, "Arrival", "Accepted", $"{assignment.Technician?.FullName ?? "Technician"} arrived on site.", request.Latitude, request.Longitude, cancellationToken);
        await SyncOwnAttendanceAsync(tenantId, assignment.Technician, workOrderId, actorUserId, TripEvent.Arrived, arrivedAt, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RefreshAssignmentStatusAsync(tenantId, workOrderId, cancellationToken);
        await NotifyEtaAsync(tenantId, workOrderId, EtaNotificationEventTypes.Arrived, assignment.Technician?.FullName, cancellationToken);
    }

    public async Task RecordDepartureAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, TechnicianDepartureRequest request, CancellationToken cancellationToken = default)
    {
        var assignment = await dbContext.WorkOrderTechnicianAssignments
            .Include(x => x.Technician)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.WorkOrderId == workOrderId && x.TechnicianId == request.TechnicianId, cancellationToken)
            ?? throw new BusinessRuleException("Departure cannot be recorded before technician assignment.");

        var departedAt = request.DepartedAt ?? OccurredAt.UtcNow;
        var visit = await FindOpenVisitAsync(tenantId, workOrderId, request.TechnicianId, cancellationToken);
        if (visit is null && assignment.ArrivalAt is { } legacyArrival && (assignment.DepartureAt is null || assignment.DepartureAt < legacyArrival))
        {
            // On site since before visits were recorded: rebuild the visit from the assignment's arrival.
            visit = new WorkOrderVisit { TenantId = tenantId, WorkOrderId = workOrderId, TechnicianId = request.TechnicianId, ArrivedAt = legacyArrival };
            dbContext.WorkOrderVisits.Add(visit);
        }

        if (visit?.ArrivedAt is null)
        {
            throw new BusinessRuleException("Record the arrival on site before the departure.");
        }

        visit.DepartedAt = departedAt;
        visit.OnSiteMinutes = MinutesBetween(visit.ArrivedAt, departedAt);
        assignment.DepartureAt = departedAt;
        await UpdateTechnicianTrackingAsync(tenantId, request.TechnicianId, null, request.Latitude, request.Longitude, false, cancellationToken);
        await AddHistoryAsync(tenantId, workOrderId, "TechnicianDeparted", null, null, request.TechnicianId, request.TechnicianId, actorUserId, request.Notes, cancellationToken);
        await AddEventAsync(tenantId, workOrderId, actorUserId, "Departure", "Accepted", $"{assignment.Technician?.FullName ?? "Technician"} departed from site.", request.Latitude, request.Longitude, cancellationToken);
        await SyncOwnAttendanceAsync(tenantId, assignment.Technician, workOrderId, actorUserId, TripEvent.Departed, departedAt, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<IReadOnlyCollection<WorkOrderAssignmentHistory>> GetHistoryAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default) =>
        dbContext.WorkOrderAssignmentHistories
            .Include(x => x.FromGroup)
            .Include(x => x.ToGroup)
            .Include(x => x.FromTechnician)
            .Include(x => x.ToTechnician)
            .Include(x => x.PerformedByUser)
            .Where(x => x.TenantId == tenantId && x.WorkOrderId == workOrderId)
            .OrderBy(x => x.PerformedAt)
            .ToListAsync(cancellationToken)
            .ContinueWith<IReadOnlyCollection<WorkOrderAssignmentHistory>>(task => task.Result, cancellationToken);

    public Task<bool> HasAcceptedTechnicianAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default) =>
        dbContext.WorkOrderTechnicianAssignments.AnyAsync(
            x => x.TenantId == tenantId && x.WorkOrderId == workOrderId && (x.Status == "Accepted" || x.Status == "InTransit" || x.Status == "Arrived" || x.Status == "InProgress" || x.Status == "Completed"),
            cancellationToken);

    public Task<bool> HasArrivalAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default) =>
        dbContext.WorkOrderTechnicianAssignments.AnyAsync(x => x.TenantId == tenantId && x.WorkOrderId == workOrderId && x.ArrivalAt.HasValue, cancellationToken);

    private async Task NotifyEtaAsync(Guid tenantId, Guid workOrderId, string eventType, string? technicianName, CancellationToken cancellationToken)
    {
        var workOrder = await dbContext.WorkOrders.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == workOrderId, cancellationToken);
        if (workOrder is null) return;

        await etaNotificationService.NotifyAsync(tenantId, workOrder, eventType, technicianName, cancellationToken);
    }

    private enum TripEvent { Travelling, Arrived, Departed }

    /// <summary>
    /// Keeps a technician's clock in step with their own trips: heading out or arriving clocks them in
    /// (if they weren't) against this job; leaving site drops the job but leaves them clocked in, since
    /// they may drive to the next one. Office staff recording a trip for someone leave that clock alone.
    /// </summary>
    private async Task SyncOwnAttendanceAsync(Guid tenantId, Technician? technician, Guid workOrderId, Guid? actorUserId, TripEvent tripEvent, DateTime at, CancellationToken cancellationToken)
    {
        if (technician?.UserId is null || technician.UserId != actorUserId)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (at > now)
        {
            at = now;
        }

        var entry = await dbContext.AttendanceEntries
            .Where(x => x.TenantId == tenantId && x.TechnicianId == technician.Id && x.ClockOutAt == null)
            .OrderByDescending(x => x.ClockInAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (tripEvent == TripEvent.Departed)
        {
            if (entry is not null && entry.CurrentWorkOrderId == workOrderId)
            {
                entry.CurrentWorkOrderId = null;
                if (entry.Status == AttendanceRules.Travelling)
                {
                    entry.Status = AttendanceRules.ClockedIn;
                }

                entry.UpdatedAt = now;
            }

            return;
        }

        if (entry is null)
        {
            var workOrderNumber = await dbContext.WorkOrders
                .Where(x => x.TenantId == tenantId && x.Id == workOrderId)
                .Select(x => x.WorkOrderNumber)
                .SingleOrDefaultAsync(cancellationToken);
            entry = new AttendanceEntry
            {
                TenantId = tenantId,
                TechnicianId = technician.Id,
                WorkDate = DateOnly.FromDateTime(at),
                ClockInAt = at,
                Note = $"Clocked in automatically: {(tripEvent == TripEvent.Travelling ? "on the way to" : "arrived at")} {workOrderNumber}",
            };
            dbContext.AttendanceEntries.Add(entry);
        }
        else if (entry.Status == AttendanceRules.OnBreak)
        {
            AttendanceRules.EndBreak(entry, at);
        }

        entry.Status = tripEvent == TripEvent.Travelling ? AttendanceRules.Travelling : AttendanceRules.ClockedIn;
        entry.CurrentWorkOrderId = workOrderId;
        entry.UpdatedAt = now;
    }

    /// <summary>The technician's visit that has not been closed by a departure yet, if any.</summary>
    private async Task<WorkOrderVisit?> FindOpenVisitAsync(Guid tenantId, Guid workOrderId, Guid technicianId, CancellationToken cancellationToken) =>
        dbContext.WorkOrderVisits.Local.FirstOrDefault(x => x.TenantId == tenantId && x.WorkOrderId == workOrderId && x.TechnicianId == technicianId && x.DepartedAt == null)
        ?? await dbContext.WorkOrderVisits
            .Where(x => x.TenantId == tenantId && x.WorkOrderId == workOrderId && x.TechnicianId == technicianId && x.DepartedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    private static int MinutesBetween(DateTime? start, DateTime end) =>
        start is null || end <= start.Value ? 0 : (int)Math.Round((end - start.Value).TotalMinutes);

    private static string? NormalizeNotes(string? notes) =>
        string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()[..Math.Min(notes.Trim().Length, 1000)];

    private async Task<WorkOrder> LoadWorkOrderAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken) =>
        await dbContext.WorkOrders.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == workOrderId, cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");

    private static List<Guid> NormalizeTechnicianIds(IReadOnlyCollection<Guid>? technicianIds) =>
        technicianIds?.Where(x => x != Guid.Empty).Distinct().ToList() ?? [];

    private static string ResolveAssignmentStatus(string workOrderStatus, Guid? assignmentGroupId, IReadOnlyCollection<WorkOrderTechnicianAssignment> technicianAssignments) =>
        WorkOrderStatuses.Normalize(workOrderStatus) switch
        {
            WorkOrderStatuses.Completed => "Completed",
            WorkOrderStatuses.Closed => "Closed",
            WorkOrderStatuses.Cancelled => "Cancelled",
            WorkOrderStatuses.OnHold => "OnHold",
            WorkOrderStatuses.InProgress => "InProgress",
            _ when technicianAssignments.Any(x => x.Status is "Accepted" or "Arrived" or "InProgress" or "Completed") => "Accepted",
            _ when technicianAssignments.Any(x => x.Status == "InTransit") => "Accepted",
            _ when technicianAssignments.Count > 0 => "AssignedToTechnician",
            _ when assignmentGroupId.HasValue => "AssignedToGroup",
            _ => "Unassigned"
        };

    private async Task RefreshAssignmentStatusAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken)
    {
        var workOrder = await dbContext.WorkOrders.SingleAsync(x => x.TenantId == tenantId && x.Id == workOrderId, cancellationToken);
        var groupAssignment = await dbContext.WorkOrderAssignments.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.WorkOrderId == workOrderId, cancellationToken);
        var technicianAssignments = await dbContext.WorkOrderTechnicianAssignments.Where(x => x.TenantId == tenantId && x.WorkOrderId == workOrderId).ToListAsync(cancellationToken);
        if (groupAssignment is not null)
        {
            groupAssignment.AssignmentStatus = ResolveAssignmentStatus(workOrder.Status, groupAssignment.AssignmentGroupId, technicianAssignments);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await SyncLegacyFieldsAsync(workOrder, groupAssignment?.AssignmentGroupId, cancellationToken);
    }

    private async Task SyncLegacyFieldsAsync(WorkOrder workOrder, Guid? assignmentGroupId, CancellationToken cancellationToken)
    {
        var technicianAssignments = await dbContext.WorkOrderTechnicianAssignments
            .Where(x => x.TenantId == workOrder.TenantId && x.WorkOrderId == workOrder.Id)
            .OrderByDescending(x => x.IsLead)
            .ThenBy(x => x.AssignedAt)
            .ToListAsync(cancellationToken);

        workOrder.AssignmentGroupId = assignmentGroupId;
        workOrder.LeadTechnicianId = technicianAssignments.FirstOrDefault(x => x.IsLead)?.TechnicianId;
        workOrder.AssignedTechnicianId = workOrder.LeadTechnicianId ?? technicianAssignments.FirstOrDefault()?.TechnicianId;
        workOrder.AssignedTechnicianIdsJson = null;
        workOrder.AssignmentType = assignmentGroupId.HasValue && technicianAssignments.Count == 0
            ? WorkOrderAssignmentTypes.AssignmentGroup
            : technicianAssignments.Count > 1
                ? WorkOrderAssignmentTypes.MultipleTechnicians
                : technicianAssignments.Count == 1
                    ? WorkOrderAssignmentTypes.IndividualTechnician
                    : WorkOrderAssignmentTypes.Unassigned;
        workOrder.ArrivalAt = technicianAssignments.Where(x => x.ArrivalAt.HasValue).Select(x => x.ArrivalAt).OrderBy(x => x).FirstOrDefault();
        workOrder.DepartureAt = technicianAssignments.Where(x => x.DepartureAt.HasValue).Select(x => x.DepartureAt).OrderByDescending(x => x).FirstOrDefault();

        var hasAssignments = assignmentGroupId.HasValue || technicianAssignments.Count > 0;
        var normalizedStatus = WorkOrderStatuses.Normalize(workOrder.Status);
        if ((string.Equals(normalizedStatus, WorkOrderStatuses.New, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedStatus, WorkOrderStatuses.Open, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedStatus, WorkOrderStatuses.Reopened, StringComparison.OrdinalIgnoreCase))
            && hasAssignments)
        {
            workOrder.Status = WorkOrderStatuses.Assigned;
        }
        else if (string.Equals(normalizedStatus, WorkOrderStatuses.Assigned, StringComparison.OrdinalIgnoreCase) && !hasAssignments)
        {
            workOrder.Status = WorkOrderStatuses.Open;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private Task AddHistoryAsync(Guid tenantId, Guid workOrderId, string action, Guid? fromGroupId, Guid? toGroupId, Guid? fromTechnicianId, Guid? toTechnicianId, Guid? actorUserId, string? notes, CancellationToken cancellationToken)
    {
        dbContext.WorkOrderAssignmentHistories.Add(new WorkOrderAssignmentHistory
        {
            TenantId = tenantId,
            WorkOrderId = workOrderId,
            Action = action,
            FromGroupId = fromGroupId,
            ToGroupId = toGroupId,
            FromTechnicianId = fromTechnicianId,
            ToTechnicianId = toTechnicianId,
            PerformedByUserId = actorUserId,
            PerformedAt = OccurredAt.UtcNow,
            Notes = notes?.Trim()
        });

        return Task.CompletedTask;
    }

    private async Task AddEventAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, string eventType, string status, string message, decimal? latitude = null, decimal? longitude = null, CancellationToken cancellationToken = default)
    {
        dbContext.WorkOrderEvents.Add(new WorkOrderEvent
        {
            TenantId = tenantId,
            WorkOrderId = workOrderId,
            ActorUserId = actorUserId,
            EventType = eventType,
            Status = status,
            Message = message,
            Latitude = latitude,
            Longitude = longitude,
            OccurredAt = OccurredAt.UtcNow
        });

        await auditLogService.LogAsync(tenantId, actorUserId, $"Work order {eventType}", nameof(WorkOrder), workOrderId.ToString(), message, cancellationToken);
    }

    private async Task UpdateTechnicianTrackingAsync(Guid tenantId, Guid technicianId, Guid? activeWorkOrderId, decimal? latitude, decimal? longitude, bool isTrackingActive, CancellationToken cancellationToken)
    {
        var technician = await dbContext.Technicians.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == technicianId, cancellationToken);
        if (technician is null)
        {
            return;
        }

        technician.IsTrackingActive = isTrackingActive;
        technician.ActiveWorkOrderId = activeWorkOrderId;
        technician.LastLocationAt = OccurredAt.UtcNow;
        technician.LastKnownLatitude = latitude;
        technician.LastKnownLongitude = longitude;
    }
}

/// <summary>
/// The "new job assigned" bell entry and push for a technician's login (only for newly added
/// technicians), through the dispatcher so the technician's preferences apply. The email is sent
/// separately, to the technician record's own address.
/// </summary>
internal static class AssignmentNotifications
{
    public const string Key = "work-order.assigned";

    public static Task NotifyAsync(INotificationDispatcher? dispatcher, WorkOrder workOrder, IEnumerable<Technician> technicians, CancellationToken cancellationToken)
    {
        if (dispatcher is null) return Task.CompletedTask;

        var userIds = technicians.Where(x => x.UserId.HasValue).Select(x => x.UserId!.Value).Distinct().ToList();
        if (userIds.Count == 0) return Task.CompletedTask;

        var title = string.IsNullOrWhiteSpace(workOrder.Title) ? "New job" : workOrder.Title.Trim();
        return dispatcher.NotifyAsync(new NotificationRequest(
            workOrder.TenantId,
            Key,
            userIds,
            Title: $"New job assigned: {workOrder.WorkOrderNumber}",
            Body: $"{title} · {workOrder.Priority} priority",
            Url: $"/tech/jobs/{workOrder.Id}",
            EntityType: "WorkOrder",
            EntityId: workOrder.Id,
            TemplateValues: new Dictionary<string, string?>(),
            Channels: NotificationChannels.InApp | NotificationChannels.Push), cancellationToken);
    }
}
