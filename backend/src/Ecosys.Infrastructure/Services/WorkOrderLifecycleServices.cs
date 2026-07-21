using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Errors;
using Ecosys.Shared.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ecosys.Infrastructure.Services;

public sealed record WorkOrderCommentRequest(string Message);
public sealed record WorkOrderLocationRequest(decimal? Latitude, decimal? Longitude);
public sealed record WorkOrderCompletionRequest(string WorkDoneNotes, string? ReportSummary = null, string? AnswersJson = null);
public sealed record WorkOrderStatusChangeRequest(string Status, string? Reason = null, string? Note = null);

public interface IWorkOrderLifecycleService
{
    string NormalizeStatus(string status);
    Task RecordCreatedAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, string workOrderNumber, CancellationToken cancellationToken = default);
    Task<WorkOrder> AssignAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, Guid? technicianId, Guid? assignmentGroupId, CancellationToken cancellationToken = default);
    Task<WorkOrderEvent> AddCommentAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, WorkOrderCommentRequest request, CancellationToken cancellationToken = default);
    Task<WorkOrder> StartAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<WorkOrder> ArriveAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, WorkOrderLocationRequest request, CancellationToken cancellationToken = default);
    Task<WorkOrder> DepartAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, WorkOrderLocationRequest request, CancellationToken cancellationToken = default);
    Task<WorkOrder> ChangeStatusAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, WorkOrderStatusChangeRequest request, CancellationToken cancellationToken = default);
    Task<WorkOrder> CompleteAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, WorkOrderCompletionRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<WorkOrderEvent>> GetEventsAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default);
    Task<WorkOrder> HoldAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, string? holdType, string? reason, CancellationToken cancellationToken = default);
    Task<WorkOrder> ResumeAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, CancellationToken cancellationToken = default);
}

internal sealed class WorkOrderLifecycleService(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IAuditLogService auditLogService,
    IEmailOutboxService emailOutboxService,
    IEmailTemplateService emailTemplateService,
    IEmailSubjectRuleService emailSubjectRuleService,
    ISlaService slaService,
    IServiceOpsNotifier serviceOpsNotifier,
    IEtaNotificationService etaNotificationService,
    IOptions<PublicAppOptions> publicAppOptions,
    ILogger<WorkOrderLifecycleService> logger) : IWorkOrderLifecycleService
{
    public string NormalizeStatus(string status) => WorkOrderStatuses.Normalize(status);

    public async Task RecordCreatedAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, string workOrderNumber, CancellationToken cancellationToken = default)
    {
        dbContext.WorkOrderEvents.Add(new WorkOrderEvent
        {
            TenantId = tenantId,
            WorkOrderId = workOrderId,
            ActorUserId = actorUserId,
            EventType = "Created",
            Status = WorkOrderStatuses.New,
            NewStatus = WorkOrderStatuses.New,
            Message = $"Work order {workOrderNumber} was created.",
            OccurredAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<WorkOrder> AssignAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, Guid? technicianId, Guid? assignmentGroupId, CancellationToken cancellationToken = default)
    {
        var workOrder = await LoadWorkOrderAsync(tenantId, workOrderId, cancellationToken);
        var currentStatus = NormalizeStatus(workOrder.Status);

        if (assignmentGroupId.HasValue)
        {
            var groupExists = await dbContext.AssignmentGroups.AnyAsync(x => x.TenantId == tenantId && x.IsActive && x.Id == assignmentGroupId.Value, cancellationToken);
            if (!groupExists)
            {
                throw new BusinessRuleException("Assignment group was not found for this tenant.");
            }
        }

        Technician? technician = null;
        if (technicianId.HasValue)
        {
            technician = await dbContext.Technicians.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.IsActive && x.Id == technicianId.Value, cancellationToken)
                ?? throw new BusinessRuleException("Technician was not found for this tenant.");

            if (assignmentGroupId.HasValue)
            {
                var membershipExists = await dbContext.AssignmentGroupMembers.AnyAsync(
                    x => x.TenantId == tenantId && x.AssignmentGroupId == assignmentGroupId.Value && x.TechnicianId == technicianId.Value,
                    cancellationToken);

                if (!membershipExists)
                {
                    throw new BusinessRuleException("Technician is not a member of the selected assignment group.");
                }
            }
        }

        if (string.Equals(currentStatus, WorkOrderStatuses.Open, StringComparison.OrdinalIgnoreCase)
            || string.Equals(currentStatus, WorkOrderStatuses.Reopened, StringComparison.OrdinalIgnoreCase))
        {
            workOrder.Status = WorkOrderStatuses.Assigned;
        }

        workOrder.AssignedTechnicianId = technicianId;
        workOrder.AssignmentGroupId = assignmentGroupId;
        await SaveEventAsync(tenantId, workOrder, actorUserId, "Assigned", WorkOrderStatuses.Assigned, technician is null
            ? "Work order assigned to group queue."
            : $"Work order assigned to {technician.FullName}.", cancellationToken: cancellationToken);

        if (technician is not null)
        {
            await TryQueueAssignedEmailAsync(tenantId, workOrder, technician, actorUserId, cancellationToken);
            await etaNotificationService.NotifyAsync(tenantId, workOrder, EtaNotificationEventTypes.Assigned, technician.FullName, cancellationToken);
        }

        await NotifySafeAsync(tenantId, workOrder.Id, cancellationToken);
        return workOrder;
    }

    public async Task<WorkOrderEvent> AddCommentAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, WorkOrderCommentRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new BusinessRuleException("Comment message is required.");
        }

        var workOrder = await LoadWorkOrderAsync(tenantId, workOrderId, cancellationToken);
        var workOrderEvent = new WorkOrderEvent
        {
            TenantId = tenantId,
            WorkOrderId = workOrder.Id,
            ActorUserId = actorUserId,
            EventType = "Comment",
            Status = NormalizeStatus(workOrder.Status),
            Message = request.Message.Trim(),
            OccurredAt = DateTime.UtcNow
        };

        dbContext.WorkOrderEvents.Add(workOrderEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
        return workOrderEvent;
    }

    public async Task<WorkOrder> StartAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var workOrder = await LoadWorkOrderAsync(tenantId, workOrderId, cancellationToken);
        await TransitionAsync(workOrder, actorUserId, WorkOrderStatuses.InProgress, "Field execution started", null, cancellationToken);

        if (!workOrder.WorkStartedAt.HasValue)
        {
            workOrder.WorkStartedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await TryQueueStartedEmailAsync(tenantId, workOrder, actorUserId, cancellationToken);
        await TryQueueClientStartedEmailAsync(tenantId, workOrder, actorUserId, cancellationToken);
        return workOrder;
    }

    public async Task<WorkOrder> ArriveAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, WorkOrderLocationRequest request, CancellationToken cancellationToken = default)
    {
        var workOrder = await LoadWorkOrderAsync(tenantId, workOrderId, cancellationToken);
        workOrder.ArrivalAt = DateTime.UtcNow;

        if (workOrder.AssignedTechnicianId.HasValue)
        {
            await UpdateTechnicianTrackingAsync(tenantId, workOrder.AssignedTechnicianId.Value, workOrder.Id, request, true, cancellationToken);
        }

        await SaveEventAsync(
            tenantId,
            workOrder,
            actorUserId,
            "Arrival",
            NormalizeStatus(workOrder.Status),
            "Technician marked arrival on site.",
            request.Latitude,
            request.Longitude,
            cancellationToken: cancellationToken);
        return workOrder;
    }

    public async Task<WorkOrder> DepartAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, WorkOrderLocationRequest request, CancellationToken cancellationToken = default)
    {
        var workOrder = await LoadWorkOrderAsync(tenantId, workOrderId, cancellationToken);
        workOrder.DepartureAt = DateTime.UtcNow;

        if (workOrder.AssignedTechnicianId.HasValue)
        {
            await UpdateTechnicianTrackingAsync(tenantId, workOrder.AssignedTechnicianId.Value, null, request, false, cancellationToken);
        }

        await SaveEventAsync(
            tenantId,
            workOrder,
            actorUserId,
            "Departure",
            NormalizeStatus(workOrder.Status),
            "Technician departed from site.",
            request.Latitude,
            request.Longitude,
            cancellationToken: cancellationToken);
        return workOrder;
    }

    public async Task<WorkOrder> ChangeStatusAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, WorkOrderStatusChangeRequest request, CancellationToken cancellationToken = default)
    {
        var workOrder = await LoadWorkOrderAsync(tenantId, workOrderId, cancellationToken);
        var normalizedStatus = NormalizeStatus(request.Status);
        await TransitionAsync(workOrder, actorUserId, normalizedStatus, request.Reason, request.Note, cancellationToken);

        // Notify admins when a work order is placed on hold
        if (WorkOrderStatuses.IsHold(normalizedStatus))
        {
            await TryQueueHoldEmailAsync(tenantId, workOrder, cancellationToken);
            await TryQueueClientOnHoldEmailAsync(tenantId, workOrder, request.Reason ?? request.Note, cancellationToken);
        }

        if (string.Equals(normalizedStatus, WorkOrderStatuses.Closed, StringComparison.OrdinalIgnoreCase))
        {
            await TryQueueClosedEmailAsync(tenantId, workOrder, cancellationToken);
        }
        else if (string.Equals(normalizedStatus, WorkOrderStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            await TryQueueCancelledEmailAsync(tenantId, workOrder, request.Reason ?? request.Note, cancellationToken);
        }

        await NotifySafeAsync(tenantId, workOrder.Id, cancellationToken);
        return workOrder;
    }

    public async Task<WorkOrder> HoldAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, string? holdType, string? reason, CancellationToken cancellationToken = default)
    {
        var workOrder = await LoadWorkOrderAsync(tenantId, workOrderId, cancellationToken);
        var targetStatus = WorkOrderHoldTypes.ToStatus(holdType);
        var derivedHoldReason = WorkOrderHoldTypes.ToHoldReason(holdType);

        await TransitionAsync(workOrder, actorUserId, targetStatus, derivedHoldReason, reason, cancellationToken);
        await TryQueueHoldEmailAsync(tenantId, workOrder, cancellationToken);
        await TryQueueClientOnHoldEmailAsync(tenantId, workOrder, reason ?? derivedHoldReason, cancellationToken);
        await NotifySafeAsync(tenantId, workOrder.Id, cancellationToken);
        return workOrder;
    }

    public async Task<WorkOrder> ResumeAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var workOrder = await LoadWorkOrderAsync(tenantId, workOrderId, cancellationToken);
        if (!WorkOrderStatuses.IsHold(workOrder.Status))
        {
            throw new BusinessRuleException("Work order is not currently on hold.");
        }

        var targetStatus = string.IsNullOrWhiteSpace(workOrder.PreviousStatus)
            ? WorkOrderStatuses.InProgress
            : WorkOrderStatuses.Normalize(workOrder.PreviousStatus);

        await TransitionAsync(workOrder, actorUserId, targetStatus, "Resumed from hold", null, cancellationToken);
        await NotifySafeAsync(tenantId, workOrder.Id, cancellationToken);
        return workOrder;
    }

    /// <summary>
    /// Returns false if the user has explicitly disabled the notification.
    /// Defaults to true when no preference row exists or userId is null.
    /// </summary>
    private async Task<bool> UserWantsEmailAsync(Guid? userId, string notificationKey, CancellationToken cancellationToken)
    {
        if (userId is null)
            return true;

        var pref = await dbContext.UserNotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId.Value && p.NotificationKey == notificationKey, cancellationToken);

        return pref?.EmailEnabled ?? true;
    }

    private async Task TryQueueHoldEmailAsync(Guid tenantId, WorkOrder workOrder, CancellationToken cancellationToken)
    {
        try
        {
            var adminRecipients = await dbContext.Users
                .Where(x => x.TenantId == tenantId && x.IsActive
                    && x.Role == AppRoles.Admin
                    && x.Email != null)
                .Select(x => new { x.Id, x.Email, x.FullName })
                .Distinct()
                .ToListAsync(cancellationToken);

            if (adminRecipients.Count == 0)
                return;

            var assetName = workOrder.AssetId.HasValue
                ? await dbContext.Assets.Where(x => x.Id == workOrder.AssetId.Value)
                    .Select(x => x.AssetName).SingleOrDefaultAsync(cancellationToken)
                : null;

            var clientName = workOrder.ClientId != Guid.Empty
                ? await dbContext.Clients.Where(x => x.Id == workOrder.ClientId)
                    .Select(x => x.ClientName).SingleOrDefaultAsync(cancellationToken)
                : null;

            var template = await emailTemplateService.RenderTenantTemplateAsync(
                tenantId,
                "work-order.on-hold",
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["WorkOrderNumber"] = workOrder.WorkOrderNumber,
                    ["HoldReason"] = workOrder.HoldReason ?? "Not specified",
                    ["HoldNote"] = string.IsNullOrWhiteSpace(workOrder.HoldNote) ? "None" : workOrder.HoldNote,
                    ["ClientName"] = clientName ?? "N/A",
                    ["AssetName"] = assetName ?? "N/A"
                },
                cancellationToken);

            if (!template.Enabled)
                return;

            var subject = await emailSubjectRuleService.BuildFinalSubjectAsync(
                tenantId, "work-order.on-hold", template.Subject, cancellationToken: cancellationToken);

            foreach (var recipient in adminRecipients)
            {
                if (!await UserWantsEmailAsync(recipient.Id, "work-order.on-hold", cancellationToken))
                    continue;

                await emailOutboxService.QueueEmailAsync(
                    new QueueEmailRequest(
                        tenantId, "work-order.on-hold", "work-order-on-hold",
                        recipient.Email!, recipient.FullName,
                        template.SenderNameOverride, null, null,
                        subject, template.HtmlBody, template.TextBody, null),
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to queue work-order.on-hold emails for work order {WorkOrderId}.", workOrder.Id);
        }
    }

    private async Task TryQueueAssignedEmailAsync(Guid tenantId, WorkOrder workOrder, Technician technician, Guid? actorUserId, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(technician.Email))
                return;

            var clientName = workOrder.ClientId != Guid.Empty
                ? await dbContext.Clients.Where(x => x.Id == workOrder.ClientId)
                    .Select(x => x.ClientName).SingleOrDefaultAsync(cancellationToken)
                : null;

            var assignedByName = actorUserId.HasValue
                ? await dbContext.Users.Where(x => x.Id == actorUserId.Value)
                    .Select(x => x.FullName).SingleOrDefaultAsync(cancellationToken)
                : null;

            var template = await emailTemplateService.RenderTenantTemplateAsync(
                tenantId,
                "work-order.assigned",
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["WorkOrderNumber"] = workOrder.WorkOrderNumber,
                    ["AssignedTo"] = technician.FullName,
                    ["ClientName"] = clientName ?? "N/A",
                    ["DueDate"] = workOrder.DueDate?.ToString("yyyy-MM-dd") ?? "Not set",
                    ["AssignedBy"] = assignedByName ?? "N/A"
                },
                cancellationToken);

            if (!template.Enabled)
                return;

            if (!await UserWantsEmailAsync(technician.UserId, "work-order.assigned", cancellationToken))
                return;

            var subject = await emailSubjectRuleService.BuildFinalSubjectAsync(
                tenantId, "work-order.assigned", template.Subject, cancellationToken: cancellationToken);

            await emailOutboxService.QueueEmailAsync(
                new QueueEmailRequest(
                    tenantId, "work-order.assigned", "work-order-assigned",
                    technician.Email, technician.FullName,
                    template.SenderNameOverride, null, null,
                    subject, template.HtmlBody, template.TextBody, null),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to queue work-order.assigned email for work order {WorkOrderId}.", workOrder.Id);
        }
    }

    private async Task TryQueueStartedEmailAsync(Guid tenantId, WorkOrder workOrder, Guid actorUserId, CancellationToken cancellationToken)
    {
        try
        {
            var adminRecipients = await dbContext.Users
                .Where(x => x.TenantId == tenantId && x.IsActive
                    && x.Role == AppRoles.Admin
                    && x.Email != null)
                .Select(x => new { x.Id, x.Email, x.FullName })
                .Distinct()
                .ToListAsync(cancellationToken);

            if (adminRecipients.Count == 0)
                return;

            var technicianName = await dbContext.Users
                .Where(x => x.Id == actorUserId)
                .Select(x => x.FullName)
                .SingleOrDefaultAsync(cancellationToken);

            var template = await emailTemplateService.RenderTenantTemplateAsync(
                tenantId,
                "work-order.started",
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["WorkOrderNumber"] = workOrder.WorkOrderNumber,
                    ["TechnicianName"] = technicianName ?? "N/A",
                    ["StartedAt"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC"
                },
                cancellationToken);

            if (!template.Enabled)
                return;

            var subject = await emailSubjectRuleService.BuildFinalSubjectAsync(
                tenantId, "work-order.started", template.Subject, cancellationToken: cancellationToken);

            foreach (var recipient in adminRecipients)
            {
                if (!await UserWantsEmailAsync(recipient.Id, "work-order.started", cancellationToken))
                    continue;

                await emailOutboxService.QueueEmailAsync(
                    new QueueEmailRequest(
                        tenantId, "work-order.started", "work-order-started",
                        recipient.Email!, recipient.FullName,
                        template.SenderNameOverride, null, null,
                        subject, template.HtmlBody, template.TextBody, null),
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to queue work-order.started email for work order {WorkOrderId}.", workOrder.Id);
        }
    }

    private async Task TryQueueClosedEmailAsync(Guid tenantId, WorkOrder workOrder, CancellationToken cancellationToken)
    {
        try
        {
            if (workOrder.ClientId == Guid.Empty)
                return;

            var client = await dbContext.Clients
                .Where(x => x.Id == workOrder.ClientId)
                .Select(x => new { x.Email, x.ClientName })
                .SingleOrDefaultAsync(cancellationToken);

            if (client is null || string.IsNullOrWhiteSpace(client.Email))
                return;

            var template = await emailTemplateService.RenderTenantTemplateAsync(
                tenantId,
                "work-order.closed",
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["WorkOrderNumber"] = workOrder.WorkOrderNumber,
                    ["ClientName"] = client.ClientName,
                    ["CompletionSummary"] = string.IsNullOrWhiteSpace(workOrder.WorkDoneNotes)
                        ? "Work completed successfully."
                        : workOrder.WorkDoneNotes
                },
                cancellationToken);

            if (!template.Enabled)
                return;

            var subject = await emailSubjectRuleService.BuildFinalSubjectAsync(
                tenantId, "work-order.closed", template.Subject, cancellationToken: cancellationToken);

            await emailOutboxService.QueueEmailAsync(
                new QueueEmailRequest(
                    tenantId, "work-order.closed", "work-order-closed",
                    client.Email, client.ClientName,
                    template.SenderNameOverride, null, null,
                    subject, template.HtmlBody, template.TextBody, null),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to queue work-order.closed email for work order {WorkOrderId}.", workOrder.Id);
        }
    }

    private async Task TryQueueCancelledEmailAsync(Guid tenantId, WorkOrder workOrder, string? reason, CancellationToken cancellationToken)
    {
        try
        {
            var techIds = await dbContext.WorkOrderTechnicianAssignments
                .Where(x => x.TenantId == tenantId && x.WorkOrderId == workOrder.Id)
                .Select(x => x.TechnicianId)
                .ToListAsync(cancellationToken);

            if (techIds.Count == 0 && workOrder.AssignedTechnicianId.HasValue)
            {
                techIds = [workOrder.AssignedTechnicianId.Value];
            }

            if (techIds.Count == 0)
                return;

            var recipients = await dbContext.Technicians
                .Where(x => techIds.Contains(x.Id) && !string.IsNullOrEmpty(x.Email))
                .Select(x => new { x.UserId, x.Email, x.FullName })
                .ToListAsync(cancellationToken);

            if (recipients.Count == 0)
                return;

            var template = await emailTemplateService.RenderTenantTemplateAsync(
                tenantId,
                "work-order.cancelled",
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["WorkOrderNumber"] = workOrder.WorkOrderNumber,
                    ["Reason"] = string.IsNullOrWhiteSpace(reason) ? "No reason provided." : reason
                },
                cancellationToken);

            if (!template.Enabled)
                return;

            var subject = await emailSubjectRuleService.BuildFinalSubjectAsync(
                tenantId, "work-order.cancelled", template.Subject, cancellationToken: cancellationToken);

            foreach (var recipient in recipients)
            {
                if (!await UserWantsEmailAsync(recipient.UserId, "work-order.cancelled", cancellationToken))
                    continue;

                await emailOutboxService.QueueEmailAsync(
                    new QueueEmailRequest(
                        tenantId, "work-order.cancelled", "work-order-cancelled",
                        recipient.Email, recipient.FullName,
                        template.SenderNameOverride, null, null,
                        subject, template.HtmlBody, template.TextBody, null),
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to queue work-order.cancelled email for work order {WorkOrderId}.", workOrder.Id);
        }
    }

    public async Task<WorkOrder> CompleteAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, WorkOrderCompletionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.WorkDoneNotes))
        {
            throw new BusinessRuleException("Work done notes are required.");
        }

        var workOrder = await LoadWorkOrderAsync(tenantId, workOrderId, cancellationToken);

        if (!workOrder.ArrivalAt.HasValue)
        {
            throw new BusinessRuleException("Work order arrival must be recorded before completion.");
        }

        await TransitionAsync(workOrder, actorUserId, WorkOrderStatuses.Completed, "Field work completed", null, cancellationToken);

        workOrder.WorkDoneNotes = request.WorkDoneNotes.Trim();
        workOrder.CompletedAt = DateTime.UtcNow;

        if (workOrder.PmTemplateId.HasValue
            || await dbContext.WorkOrderChecklistItems.AnyAsync(x => x.TenantId == tenantId && x.WorkOrderId == workOrder.Id, cancellationToken))
        {
            var checklistSnapshot = await dbContext.WorkOrderChecklistItems
                .Where(x => x.TenantId == tenantId && x.WorkOrderId == workOrder.Id)
                .OrderBy(x => x.SectionOrder)
                .ThenBy(x => x.SectionName ?? string.Empty)
                .ThenBy(x => x.SortOrder)
                .Select(x => new
                {
                    x.Id,
                    x.SectionName,
                    x.SectionOrder,
                    x.QuestionText,
                    x.HelperText,
                    x.InputType,
                    x.Unit,
                    x.MinValue,
                    x.MaxValue,
                    x.IsRequired,
                    x.EvidenceRequired,
                    x.SortOrder,
                    x.ResponseValue,
                    x.Remarks,
                    x.AttachmentId,
                    x.IsCompleted,
                    x.CompletedAt
                })
                .ToListAsync(cancellationToken);

            var report = new PmReport
            {
                TenantId = tenantId,
                WorkOrderId = workOrder.Id,
                PmTemplateId = workOrder.PmTemplateId,
                Summary = string.IsNullOrWhiteSpace(request.ReportSummary) ? request.WorkDoneNotes.Trim() : request.ReportSummary.Trim(),
                AnswersJson = string.IsNullOrWhiteSpace(request.AnswersJson)
                    ? JsonSerializer.Serialize(checklistSnapshot)
                    : request.AnswersJson.Trim()
            };
            dbContext.PmReports.Add(report);

            if (workOrder.IsPreventiveMaintenance)
            {
                await UpdatePmAssetAndPlanAsync(tenantId, workOrder, checklistSnapshot, cancellationToken);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await slaService.RefreshWorkOrderAsync(workOrder.Id, DateTime.UtcNow, cancellationToken);
        await TryQueueCompletionEmailAsync(tenantId, workOrder, cancellationToken);
        await TryQueueClientCompletedEmailAsync(tenantId, workOrder, cancellationToken);
        await TryQueueEtaCompletedNotificationAsync(tenantId, workOrder, cancellationToken);
        await TryCreateCsatSurveyAsync(tenantId, workOrder, cancellationToken);
        await NotifySafeAsync(tenantId, workOrder.Id, cancellationToken);
        return workOrder;
    }

    private async Task UpdatePmAssetAndPlanAsync(
        Guid tenantId,
        WorkOrder workOrder,
        IReadOnlyCollection<dynamic> checklistSnapshot,
        CancellationToken cancellationToken)
    {
        if (!workOrder.CompletedAt.HasValue || !workOrder.AssetId.HasValue)
        {
            return;
        }

        var asset = await dbContext.Assets
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == workOrder.AssetId.Value, cancellationToken);

        if (asset is null)
        {
            return;
        }

        asset.LastPmDate = workOrder.CompletedAt.Value;

        var meterReading = TryExtractMeterReading(checklistSnapshot);
        if (meterReading.HasValue)
        {
            asset.CurrentMeterReading = meterReading.Value;
        }

        if (!workOrder.PreventiveMaintenancePlanId.HasValue)
        {
            return;
        }

        var plan = await dbContext.PreventiveMaintenancePlans
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == workOrder.PreventiveMaintenancePlanId.Value, cancellationToken);

        if (plan is null)
        {
            return;
        }

        plan.LastPmDate = workOrder.CompletedAt.Value;
        plan.LastGeneratedAt ??= workOrder.CreatedAt;
        plan.LastPmWorkOrderId = workOrder.Id;

        var intervalMonths = ResolvePmIntervalMonths(plan);
        if (!intervalMonths.HasValue)
        {
            return;
        }

        var nextSeed = plan.NextPmDate?.Date ?? workOrder.CompletedAt.Value.Date;
        plan.NextPmDate = nextSeed.AddMonths(intervalMonths.Value);
        asset.NextPmDate = plan.NextPmDate;
    }

    private async Task TryQueueCompletionEmailAsync(Guid tenantId, WorkOrder workOrder, CancellationToken cancellationToken)
    {
        try
        {
            var tenant = await dbContext.Tenants.SingleOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
            var contactEmail = tenant?.ContactEmail ?? tenant?.Email;
            if (string.IsNullOrWhiteSpace(contactEmail))
                return;

            var technicianName = workOrder.AssignedTechnicianId.HasValue
                ? (await dbContext.Technicians.SingleOrDefaultAsync(x => x.Id == workOrder.AssignedTechnicianId.Value, cancellationToken))?.FullName
                : null;

            var assetName = workOrder.AssetId.HasValue
                ? (await dbContext.Assets.SingleOrDefaultAsync(x => x.Id == workOrder.AssetId.Value, cancellationToken))?.AssetName
                : null;

            var template = await emailTemplateService.RenderTenantTemplateAsync(
                tenantId,
                "work-order.completed",
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["WorkOrderNumber"] = workOrder.WorkOrderNumber,
                    ["AssignedTo"] = technicianName ?? "Unassigned",
                    ["AssetName"] = assetName ?? "N/A"
                },
                cancellationToken);

            if (!template.Enabled)
                return;

            var subject = await emailSubjectRuleService.BuildFinalSubjectAsync(
                tenantId, "work-order.completed", template.Subject, tenant?.Name, cancellationToken);

            await emailOutboxService.QueueEmailAsync(
                new QueueEmailRequest(
                    tenantId,
                    "work-order.completed",
                    "work-order-completed",
                    contactEmail,
                    tenant?.Name,
                    template.SenderNameOverride,
                    null,
                    null,
                    subject,
                    template.HtmlBody,
                    template.TextBody,
                    null),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to queue work-order.completed email for work order {WorkOrderId}.", workOrder.Id);
        }
    }

    private static int? ResolvePmIntervalMonths(PreventiveMaintenancePlan plan)
    {
        if (plan.FrequencyInterval > 0)
        {
            if (string.Equals(plan.FrequencyUnit, "Monthly", StringComparison.OrdinalIgnoreCase))
            {
                return plan.FrequencyInterval;
            }

            if (string.Equals(plan.FrequencyUnit, "Annual", StringComparison.OrdinalIgnoreCase))
            {
                return plan.FrequencyInterval * 12;
            }

            if (string.Equals(plan.FrequencyUnit, "Weekly", StringComparison.OrdinalIgnoreCase))
            {
                // Weekly PM is scheduled differently — no month approximation
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Fire-and-forget-safe wrapper around IServiceOpsNotifier.
    /// Swallows all exceptions so a SignalR failure never rolls back a business operation.
    /// </summary>
    private async Task NotifySafeAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken)
    {
        try
        {
            await serviceOpsNotifier.NotifyWorkOrderUpdatedAsync(tenantId, workOrderId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SignalR broadcast failed for work order {WorkOrderId}.", workOrderId);
        }
    }

    // ── Client notification emails ─────────────────────────────────────────────

    private async Task TryQueueClientStartedEmailAsync(Guid tenantId, WorkOrder workOrder, Guid actorUserId, CancellationToken cancellationToken)
    {
        try
        {
            if (!workOrder.NotifyClient) return;

            var client = await dbContext.Clients
                .Where(x => x.Id == workOrder.ClientId)
                .Select(x => new { x.Email, x.ClientName })
                .SingleOrDefaultAsync(cancellationToken);

            var recipientEmail = workOrder.ClientContactEmail ?? client?.Email;
            if (string.IsNullOrWhiteSpace(recipientEmail)) return;

            var technicianName = await dbContext.Users
                .Where(x => x.Id == actorUserId)
                .Select(x => x.FullName)
                .SingleOrDefaultAsync(cancellationToken);

            var siteName = workOrder.SiteId.HasValue
                ? await dbContext.Sites.Where(x => x.Id == workOrder.SiteId.Value)
                    .Select(x => x.SiteName).SingleOrDefaultAsync(cancellationToken)
                : null;

            var template = await emailTemplateService.RenderTenantTemplateAsync(
                tenantId,
                "work-order.client.started",
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["WoNumber"]           = workOrder.WorkOrderNumber,
                    ["Title"]              = workOrder.Title,
                    ["TechnicianName"]     = technicianName ?? "Your assigned technician",
                    ["EstimatedDuration"]  = workOrder.EstimatedDuration.HasValue
                                               ? $"{workOrder.EstimatedDuration.Value} hours"
                                               : "To be confirmed",
                    ["SiteName"]           = siteName ?? "N/A",
                    ["ClientName"]         = client?.ClientName ?? "Valued Client"
                },
                cancellationToken);

            if (!template.Enabled) return;

            var subject = await emailSubjectRuleService.BuildFinalSubjectAsync(
                tenantId, "work-order.client.started", template.Subject, cancellationToken: cancellationToken);

            await emailOutboxService.QueueEmailAsync(
                new QueueEmailRequest(
                    tenantId, "work-order.client.started", "work-order-client-started",
                    recipientEmail, client?.ClientName,
                    template.SenderNameOverride, null, null,
                    subject, template.HtmlBody, template.TextBody, null),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to queue work-order.client.started email for work order {WorkOrderId}.", workOrder.Id);
        }
    }

    private async Task TryQueueClientOnHoldEmailAsync(Guid tenantId, WorkOrder workOrder, string? reason, CancellationToken cancellationToken)
    {
        try
        {
            if (!workOrder.NotifyClient) return;

            var client = await dbContext.Clients
                .Where(x => x.Id == workOrder.ClientId)
                .Select(x => new { x.Email, x.ClientName, x.Phone })
                .SingleOrDefaultAsync(cancellationToken);

            var recipientEmail = workOrder.ClientContactEmail ?? client?.Email;
            if (string.IsNullOrWhiteSpace(recipientEmail)) return;

            var template = await emailTemplateService.RenderTenantTemplateAsync(
                tenantId,
                "work-order.client.on-hold",
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["WoNumber"]    = workOrder.WorkOrderNumber,
                    ["Title"]       = workOrder.Title,
                    ["HoldReason"]  = !string.IsNullOrWhiteSpace(reason) ? reason
                                      : (!string.IsNullOrWhiteSpace(workOrder.HoldReason) ? workOrder.HoldReason : "Under review"),
                    ["HoldNote"]    = string.IsNullOrWhiteSpace(workOrder.HoldNote)
                                      ? "Our team will be in contact shortly."
                                      : workOrder.HoldNote,
                    ["ClientName"]  = client?.ClientName ?? "Valued Client",
                    ["ContactInfo"] = client?.Phone ?? "your service provider"
                },
                cancellationToken);

            if (!template.Enabled) return;

            var subject = await emailSubjectRuleService.BuildFinalSubjectAsync(
                tenantId, "work-order.client.on-hold", template.Subject, cancellationToken: cancellationToken);

            await emailOutboxService.QueueEmailAsync(
                new QueueEmailRequest(
                    tenantId, "work-order.client.on-hold", "work-order-client-on-hold",
                    recipientEmail, client?.ClientName,
                    template.SenderNameOverride, null, null,
                    subject, template.HtmlBody, template.TextBody, null),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to queue work-order.client.on-hold email for work order {WorkOrderId}.", workOrder.Id);
        }
    }

    private async Task TryQueueClientCompletedEmailAsync(Guid tenantId, WorkOrder workOrder, CancellationToken cancellationToken)
    {
        try
        {
            if (!workOrder.NotifyClient) return;

            var client = await dbContext.Clients
                .Where(x => x.Id == workOrder.ClientId)
                .Select(x => new { x.Email, x.ClientName })
                .SingleOrDefaultAsync(cancellationToken);

            var recipientEmail = workOrder.ClientContactEmail ?? client?.Email;
            if (string.IsNullOrWhiteSpace(recipientEmail)) return;

            var technicianNames = await dbContext.WorkOrderTechnicianAssignments
                .Where(x => x.TenantId == tenantId && x.WorkOrderId == workOrder.Id)
                .Join(dbContext.Technicians, a => a.TechnicianId, t => t.Id, (_, t) => t.FullName)
                .ToListAsync(cancellationToken);

            if (technicianNames.Count == 0 && workOrder.AssignedTechnicianId.HasValue)
            {
                var name = await dbContext.Technicians
                    .Where(x => x.Id == workOrder.AssignedTechnicianId.Value)
                    .Select(x => x.FullName)
                    .SingleOrDefaultAsync(cancellationToken);
                if (name is not null) technicianNames.Add(name);
            }

            var template = await emailTemplateService.RenderTenantTemplateAsync(
                tenantId,
                "work-order.client.completed",
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["WoNumber"]         = workOrder.WorkOrderNumber,
                    ["Title"]            = workOrder.Title,
                    ["CompletionDate"]   = (workOrder.CompletedAt ?? DateTime.UtcNow).ToString("dd MMM yyyy"),
                    ["TechnicianNames"]  = technicianNames.Count > 0
                                           ? string.Join(", ", technicianNames)
                                           : "Your assigned technician",
                    ["Summary"]          = string.IsNullOrWhiteSpace(workOrder.WorkDoneNotes)
                                           ? "Work completed successfully."
                                           : workOrder.WorkDoneNotes,
                    ["ClientName"]       = client?.ClientName ?? "Valued Client"
                },
                cancellationToken);

            if (!template.Enabled) return;

            var subject = await emailSubjectRuleService.BuildFinalSubjectAsync(
                tenantId, "work-order.client.completed", template.Subject, cancellationToken: cancellationToken);

            await emailOutboxService.QueueEmailAsync(
                new QueueEmailRequest(
                    tenantId, "work-order.client.completed", "work-order-client-completed",
                    recipientEmail, client?.ClientName,
                    template.SenderNameOverride, null, null,
                    subject, template.HtmlBody, template.TextBody, null),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to queue work-order.client.completed email for work order {WorkOrderId}.", workOrder.Id);
        }
    }

    private async Task TryQueueEtaCompletedNotificationAsync(Guid tenantId, WorkOrder workOrder, CancellationToken cancellationToken)
    {
        var technicianName = workOrder.AssignedTechnicianId.HasValue
            ? await dbContext.Technicians
                .Where(x => x.Id == workOrder.AssignedTechnicianId.Value)
                .Select(x => x.FullName)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        await etaNotificationService.NotifyAsync(tenantId, workOrder, EtaNotificationEventTypes.Completed, technicianName, cancellationToken);
    }

    private async Task TryCreateCsatSurveyAsync(Guid tenantId, WorkOrder workOrder, CancellationToken cancellationToken)
    {
        try
        {
            if (!workOrder.NotifyClient) return;

            var exists = await dbContext.CsatSurveys
                .AnyAsync(x => x.TenantId == tenantId && x.WorkOrderId == workOrder.Id, cancellationToken);
            if (exists) return;

            var client = await dbContext.Clients
                .Where(x => x.Id == workOrder.ClientId)
                .Select(x => new { x.Email, x.ClientName })
                .SingleOrDefaultAsync(cancellationToken);

            var recipientEmail = workOrder.ClientContactEmail ?? client?.Email;
            if (string.IsNullOrWhiteSpace(recipientEmail)) return;

            var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');

            dbContext.CsatSurveys.Add(new CsatSurvey
            {
                TenantId = tenantId,
                WorkOrderId = workOrder.Id,
                ClientId = workOrder.ClientId,
                TechnicianId = workOrder.AssignedTechnicianId,
                TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))),
                ExpiresAt = DateTime.UtcNow.AddDays(14),
                SentAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync(cancellationToken);

            var surveyLink = $"{publicAppOptions.Value.PublicUrl.TrimEnd('/')}/csat/{rawToken}";
            var template = await emailTemplateService.RenderTenantTemplateAsync(
                tenantId,
                "csat.survey",
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["WorkOrderNumber"] = workOrder.WorkOrderNumber,
                    ["ClientName"] = client?.ClientName ?? "Valued Client",
                    ["SurveyLink"] = surveyLink
                },
                cancellationToken);

            if (!template.Enabled) return;

            var subject = await emailSubjectRuleService.BuildFinalSubjectAsync(
                tenantId, "csat.survey", template.Subject, cancellationToken: cancellationToken);

            await emailOutboxService.QueueEmailAsync(
                new QueueEmailRequest(
                    tenantId, "csat.survey", "csat-survey",
                    recipientEmail, client?.ClientName,
                    template.SenderNameOverride, null, null,
                    subject, template.HtmlBody, template.TextBody, null),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to create CSAT survey for work order {WorkOrderId}.", workOrder.Id);
        }
    }

    // ── Private helpers (state machine) ───────────────────────────────────────

    private async Task<WorkOrder> LoadWorkOrderAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken)
    {
        return await dbContext.WorkOrders
            .Include(x => x.TechnicianAssignments)
            .Include(x => x.Events)
            .Where(x => x.TenantId == tenantId && x.Id == workOrderId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new Ecosys.Shared.Errors.NotFoundException("Work order was not found.");
    }

    private async Task TransitionAsync(WorkOrder workOrder, Guid actorUserId, string targetStatus, string? reason, string? note, CancellationToken cancellationToken)
    {
        var currentStatus = NormalizeStatus(workOrder.Status);
        var utcNow = DateTime.UtcNow;
        var canUseAdminOnlyTransitions = CanUseAdminOnlyLifecycleTransitions();

        if (!WorkOrderStatusTransitions.CanTransition(currentStatus, targetStatus, canUseAdminOnlyTransitions))
        {
            throw new Ecosys.Shared.Errors.BusinessRuleException(
                $"Cannot transition work order from '{currentStatus}' to '{targetStatus}'.");
        }

        if (WorkOrderStatusTransitions.IsAdminOnlyTransition(currentStatus, targetStatus) && !canUseAdminOnlyTransitions)
        {
            throw new BusinessRuleException($"Only administrators can transition work orders from '{currentStatus}' to '{targetStatus}'.");
        }

        if (WorkOrderStatuses.IsHold(currentStatus) && !WorkOrderStatuses.IsHold(targetStatus))
        {
            if (workOrder.HoldStartedAt.HasValue)
            {
                var holdElapsed = utcNow - workOrder.HoldStartedAt.Value;
                if (holdElapsed > TimeSpan.Zero)
                {
                    if (workOrder.SlaResponseDeadline.HasValue)
                    {
                        workOrder.SlaResponseDeadline = workOrder.SlaResponseDeadline.Value.Add(holdElapsed);
                    }

                    if (workOrder.SlaResolutionDeadline.HasValue)
                    {
                        workOrder.SlaResolutionDeadline = workOrder.SlaResolutionDeadline.Value.Add(holdElapsed);
                    }
                }
            }

            workOrder.HoldEndedAt = utcNow;
            workOrder.PreviousStatus = null;
        }

        workOrder.Status = targetStatus;

        if (WorkOrderStatuses.IsHold(targetStatus))
        {
            if (!WorkOrderStatuses.IsHold(currentStatus))
            {
                workOrder.PreviousStatus = currentStatus;
            }

            workOrder.HoldReason = WorkOrderHoldReasons.Normalize(reason);
            workOrder.HoldNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            workOrder.HoldStartedAt = utcNow;
            workOrder.HoldEndedAt = null;
        }
        else if (!WorkOrderStatuses.IsHold(currentStatus))
        {
            workOrder.HoldStartedAt = null;
            workOrder.HoldEndedAt = null;
        }

        if (string.Equals(targetStatus, WorkOrderStatuses.InProgress, StringComparison.OrdinalIgnoreCase))
        {
            workOrder.WorkStartedAt ??= utcNow;
        }

        if (string.Equals(targetStatus, WorkOrderStatuses.Closed, StringComparison.OrdinalIgnoreCase))
        {
            workOrder.ClosedAt = utcNow;
            workOrder.ClosedByUserId = actorUserId;
        }
        else if (string.Equals(targetStatus, WorkOrderStatuses.Reopened, StringComparison.OrdinalIgnoreCase))
        {
            workOrder.ClosedAt = null;
            workOrder.ClosedByUserId = null;
        }

        await SaveEventAsync(
            workOrder.TenantId,
            workOrder,
            actorUserId,
            "StatusChanged",
            targetStatus,
            BuildStatusChangeMessage(currentStatus, targetStatus, reason, note),
            latitude: null,
            longitude: null,
            oldStatus: currentStatus,
            newStatus: targetStatus,
            reason: reason,
            note: note,
            cancellationToken: cancellationToken);
    }

    private async Task SaveEventAsync(
        Guid tenantId,
        WorkOrder workOrder,
        Guid? actorUserId,
        string eventType,
        string status,
        string message,
        decimal? latitude = null,
        decimal? longitude = null,
        string? oldStatus = null,
        string? newStatus = null,
        string? reason = null,
        string? note = null,
        CancellationToken cancellationToken = default)
    {
        dbContext.WorkOrderEvents.Add(new WorkOrderEvent
        {
            TenantId      = tenantId,
            WorkOrderId   = workOrder.Id,
            ActorUserId   = actorUserId,
            EventType     = eventType,
            Status        = status,
            OldStatus     = oldStatus,
            NewStatus     = newStatus,
            Reason        = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            Note          = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            Message       = message,
            Latitude      = latitude,
            Longitude     = longitude,
            OccurredAt    = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private bool CanUseAdminOnlyLifecycleTransitions() =>
        tenantContext.IsAdmin
        || tenantContext.IsSuperAdmin
        || tenantContext.HasRole(AppRoles.PlatformAdmin)
        || tenantContext.HasRole(AppRoles.PlatformAdminRole)
        || tenantContext.HasRole(AppRoles.PlatformOwner)
        || tenantContext.HasRole(AppRoles.PlatformSuperAdmin)
        || tenantContext.HasRole(AppRoles.SuperAdmin);

    private static string BuildStatusChangeMessage(string currentStatus, string targetStatus, string? reason, string? note)
    {
        var message = $"Status changed from {currentStatus} to {targetStatus}.";

        if (!string.IsNullOrWhiteSpace(reason))
        {
            message += $" Reason: {reason.Trim()}.";
        }

        if (!string.IsNullOrWhiteSpace(note))
        {
            message += $" Note: {note.Trim()}.";
        }

        return message;
    }

    private async Task UpdateTechnicianTrackingAsync(Guid tenantId, Guid technicianId, Guid? workOrderId, WorkOrderLocationRequest request, bool isArrival, CancellationToken cancellationToken)
    {
        var technician = await dbContext.Technicians
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == technicianId, cancellationToken);
        if (technician is null) return;

        if (request.Latitude.HasValue)  technician.LastKnownLatitude  = request.Latitude;
        if (request.Longitude.HasValue) technician.LastKnownLongitude = request.Longitude;
        technician.ActiveWorkOrderId = isArrival ? workOrderId : null;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static decimal? TryExtractMeterReading(IReadOnlyCollection<dynamic> checklistSnapshot)
    {
        foreach (var item in checklistSnapshot)
        {
            string? inputType     = item.InputType;
            string? responseValue = item.ResponseValue;
            if (string.Equals(inputType, "Number", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(responseValue)
                && decimal.TryParse(responseValue, out var reading))
            {
                return reading;
            }
        }
        return null;
    }

    public async Task<IReadOnlyCollection<WorkOrderEvent>> GetEventsAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default)
    {
        return await dbContext.WorkOrderEvents
            .Include(x => x.ActorUser)
            .Where(x => x.TenantId == tenantId && x.WorkOrderId == workOrderId)
            .OrderBy(x => x.OccurredAt)
            .ToListAsync(cancellationToken);
    }
}
