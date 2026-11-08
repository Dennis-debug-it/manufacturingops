using System.Text.Json;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services.Factory;

public static class FactoryWorkTypes
{
    public const string Corrective = "Corrective";
    public const string Preventive = "Preventive";
    public const string Inspection = "Inspection";
    public const string Statutory = "Statutory";
    public const string Modification = "Modification";
    public const string Safety = "Safety";

    public static readonly IReadOnlyList<string> All = [Corrective, Preventive, Inspection, Statutory, Modification, Safety];

    public static string? Find(string? value) =>
        All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

public static class FactoryRequestSources
{
    public const string Office = "Office";
    public const string Operator = "Operator";
    public const string RunningCheck = "RunningCheck";
    public const string Preventive = "Preventive";
    /// <summary>Raised by a meter crossing its alarm limit (condition monitoring).</summary>
    public const string Condition = "Condition";
}

public static class FactoryPriorities
{
    public static readonly IReadOnlyList<string> All = ["Low", "Medium", "High", "Critical"];

    public static string? Find(string? value) =>
        All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>The actions that move a factory job through requested → approved → planned → assigned → in progress → completed → verified → closed.</summary>
public static class FactoryWorkOrderActions
{
    public const string Approve = "approve";
    public const string Reject = "reject";
    public const string Plan = "plan";
    public const string Assign = "assign";
    public const string Start = "start";
    public const string Hold = "hold";
    public const string Resume = "resume";
    public const string Complete = "complete";
    public const string Verify = "verify";
    public const string ReturnForRework = "return";
    public const string Close = "close";
    public const string Cancel = "cancel";
    public const string Reopen = "reopen";

    /// <summary>Actions a supervisor or planner takes; the rest are the technician's.</summary>
    public static readonly IReadOnlySet<string> Supervisory = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Approve, Reject, Plan, Assign, Verify, ReturnForRework, Close, Cancel, Reopen
    };

    private static readonly string[] Open =
    [
        WorkOrderStatuses.Requested, WorkOrderStatuses.Approved, WorkOrderStatuses.Planned, WorkOrderStatuses.Assigned,
        WorkOrderStatuses.InProgress, WorkOrderStatuses.OnHold, WorkOrderStatuses.WaitingOnParts, WorkOrderStatuses.Reopened
    ];

    /// <summary>Which statuses each action may be taken from.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> From = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        [Approve] = [WorkOrderStatuses.Requested],
        [Reject] = [WorkOrderStatuses.Requested],
        [Plan] = [WorkOrderStatuses.Approved, WorkOrderStatuses.Planned, WorkOrderStatuses.Reopened],
        [Assign] = [WorkOrderStatuses.Approved, WorkOrderStatuses.Planned, WorkOrderStatuses.Assigned, WorkOrderStatuses.InProgress, WorkOrderStatuses.OnHold, WorkOrderStatuses.WaitingOnParts, WorkOrderStatuses.Reopened],
        [Start] = [WorkOrderStatuses.Assigned],
        [Hold] = [WorkOrderStatuses.Assigned, WorkOrderStatuses.InProgress],
        [Resume] = [WorkOrderStatuses.OnHold, WorkOrderStatuses.WaitingOnParts],
        [Complete] = [WorkOrderStatuses.InProgress],
        [Verify] = [WorkOrderStatuses.Completed],
        [ReturnForRework] = [WorkOrderStatuses.Completed],
        [Close] = [WorkOrderStatuses.Verified],
        [Cancel] = Open.Where(x => x != WorkOrderStatuses.InProgress).ToArray(),
        [Reopen] = [WorkOrderStatuses.Verified, WorkOrderStatuses.Closed],
    };

    public static IReadOnlyList<string> AllowedFrom(string status) =>
        From.Where(x => x.Value.Contains(status, StringComparer.OrdinalIgnoreCase)).Select(x => x.Key).ToList();
}

public static class FactoryHoldReasons
{
    public static readonly IReadOnlyList<string> All =
    [
        WorkOrderHoldReasons.AwaitingSpares,
        WorkOrderHoldReasons.AwaitingWindow,
        WorkOrderHoldReasons.AwaitingShutdown,
        WorkOrderHoldReasons.AwaitingContractor,
        WorkOrderHoldReasons.Other,
    ];

    public static string? Find(string? value) =>
        All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

public sealed record FactoryActor(Guid? UserId, string Name);

public sealed record CreateFactoryWorkOrder(
    string Title,
    string WorkType,
    string Priority,
    Guid? AssetId = null,
    Guid? OrgNodeId = null,
    string? Description = null,
    bool IsBreakdown = false,
    bool NeedsLineStop = false,
    DateTime? DueDate = null,
    DateTime? DowntimeStartedAt = null,
    decimal? EstimatedHours = null,
    string Source = FactoryRequestSources.Office,
    string? RequestedByName = null,
    Guid? ParentWorkOrderId = null,
    Guid? ProblemCodeId = null,
    Guid? JobPlanId = null,
    Guid? RequiredCertificationId = null,
    IReadOnlyCollection<string>? RequiredPermitTypes = null);

public sealed record FactoryTransition(
    string Action,
    string? Reason = null,
    string? HoldReason = null,
    IReadOnlyCollection<Guid>? TechnicianIds = null,
    Guid? LeadTechnicianId = null,
    DateTime? ScheduledStartAt = null,
    DateTime? ScheduledEndAt = null,
    decimal? EstimatedHours = null,
    string? WorkDone = null,
    Guid? ProblemCodeId = null,
    Guid? CauseCodeId = null,
    Guid? RemedyCodeId = null,
    bool AssetBackInService = true,
    DateTime? DowntimeEndedAt = null,
    string? SignatureDataUrl = null,
    string? SignerName = null);

public interface IFactoryWorkOrderService
{
    /// <summary>Raises a job. It starts as Requested, or Approved when approval is off or it is a breakdown that skips approval.</summary>
    Task<WorkOrder> CreateAsync(Guid tenantId, FactoryActor actor, CreateFactoryWorkOrder request, CancellationToken cancellationToken = default);

    /// <summary>Applies one lifecycle action and saves.</summary>
    Task ApplyAsync(WorkOrder workOrder, FactoryActor actor, FactoryTransition transition, CancellationToken cancellationToken = default);
}

internal sealed class FactoryWorkOrderService(
    AppDbContext dbContext,
    IDocumentNumberingService documentNumberingService,
    IFactorySettingsService factorySettingsService,
    IAssetLifecycleService assetLifecycleService,
    IOrgScopeService orgScopeService,
    IFactorySlaService slaService,
    FactoryPmProgress pmProgress,
    FactoryJobGuards guards) : IFactoryWorkOrderService
{
    public async Task<WorkOrder> CreateAsync(Guid tenantId, FactoryActor actor, CreateFactoryWorkOrder request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new BusinessRuleException("Say briefly what is wrong or what needs doing.");
        }

        var workType = FactoryWorkTypes.Find(request.WorkType)
            ?? throw new BusinessRuleException($"Job type must be one of: {string.Join(", ", FactoryWorkTypes.All)}.");
        var priority = FactoryPriorities.Find(request.Priority)
            ?? throw new BusinessRuleException($"Priority must be one of: {string.Join(", ", FactoryPriorities.All)}.");

        WorkOrder? parent = null;
        if (request.ParentWorkOrderId is { } parentId)
        {
            parent = await dbContext.WorkOrders.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == parentId, cancellationToken)
                ?? throw new BusinessRuleException("The parent job was not found.");
            if (WorkOrderStatuses.IsClosed(parent.Status) || parent.Status is WorkOrderStatuses.Completed or WorkOrderStatuses.Verified)
            {
                throw new BusinessRuleException("A finished job can't be split. Raise a new job instead.");
            }
        }

        Asset? asset = null;
        var assetId = request.AssetId ?? parent?.AssetId;
        if (assetId.HasValue)
        {
            asset = await dbContext.Assets.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == assetId, cancellationToken)
                ?? throw new BusinessRuleException("The asset was not found.");
            if (string.Equals(asset.Status, "Inactive", StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessRuleException("The asset is retired.");
            }
        }

        var orgNodeId = request.OrgNodeId ?? asset?.OrgNodeId ?? parent?.OrgNodeId
            ?? throw new BusinessRuleException("Choose the asset, or where the work is.");
        if (actor.UserId is { } userId)
        {
            await orgScopeService.EnsureCanAccessAsync(tenantId, userId, orgNodeId, cancellationToken);
        }

        if (request.IsBreakdown && asset is null)
        {
            throw new BusinessRuleException("A breakdown is recorded against the asset that stopped.");
        }

        if (request.ProblemCodeId is { } problemId)
        {
            await EnsureCodeAsync(tenantId, problemId, FailureCodeKinds.Problem, cancellationToken);
        }

        JobPlan? plan = null;
        if (request.JobPlanId is { } planId)
        {
            plan = await dbContext.JobPlans.Include(x => x.Steps)
                .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == planId && x.IsActive, cancellationToken)
                ?? throw new BusinessRuleException("The job plan was not found.");
        }

        var settings = await factorySettingsService.GetOrCreateAsync(tenantId, cancellationToken);
        var now = OccurredAt.UtcNow;
        var workOrder = new WorkOrder
        {
            TenantId = tenantId,
            ClientId = null,
            AssetId = asset?.Id,
            OrgNodeId = orgNodeId,
            ParentWorkOrderId = parent?.Id,
            WorkOrderNumber = await documentNumberingService.GenerateAsync(tenantId, null, DocumentTypes.WorkOrder, cancellationToken),
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            WorkType = workType,
            Priority = request.IsBreakdown && priority is "Low" or "Medium" ? "High" : priority,
            IsPreventiveMaintenance = workType == FactoryWorkTypes.Preventive,
            Status = WorkOrderStatuses.Requested,
            IsBreakdown = request.IsBreakdown,
            NeedsLineStop = request.NeedsLineStop || plan?.NeedsLineStop == true,
            DueDate = request.DueDate,
            EstimatedLabourHours = request.EstimatedHours ?? plan?.EstimatedHours,
            JobPlanId = plan?.Id,
            RequiredCertificationId = request.RequiredCertificationId ?? plan?.RequiredCertificationId,
            RequiredPermitTypes = FactoryJobGuards.NormalisePermitTypes(
                (request.RequiredPermitTypes ?? []).Concat(FactoryJobGuards.PermitTypesOf(plan?.RequiredPermitTypes))),
            CreatedAt = now,
            RequestSource = request.Source,
            RequestedByName = string.IsNullOrWhiteSpace(request.RequestedByName) ? actor.Name : request.RequestedByName.Trim(),
            FailureProblemCodeId = request.ProblemCodeId,
            NotifyClient = false
        };
        dbContext.WorkOrders.Add(workOrder);
        // Response and repair targets are for faults; planned and statutory work runs to its due date.
        if (workType is not (FactoryWorkTypes.Preventive or FactoryWorkTypes.Statutory or FactoryWorkTypes.Inspection) || request.IsBreakdown)
        {
            await slaService.ApplyAsync(workOrder, asset?.Criticality, cancellationToken);
        }
        foreach (var step in plan?.Steps.OrderBy(x => x.Sequence) ?? Enumerable.Empty<JobPlanStep>())
        {
            dbContext.WorkOrderChecklistItems.Add(new WorkOrderChecklistItem
            {
                TenantId = tenantId,
                WorkOrderId = workOrder.Id,
                SectionName = plan!.Name,
                QuestionText = step.Instruction,
                HelperText = step.EstimatedMinutes is { } minutes ? $"About {minutes} min" : null,
                InputType = "checkbox",
                QuestionType = "Task",
                IsRequired = true,
                SortOrder = step.Sequence
            });
        }

        AddEvent(workOrder, actor, "Created", null, WorkOrderStatuses.Requested,
            $"{(request.IsBreakdown ? "Breakdown reported" : "Job requested")} by {workOrder.RequestedByName}.");

        if (request.IsBreakdown)
        {
            StartDowntime(workOrder, asset!, actor, AssetLifecycleService.EffectiveAt(request.DowntimeStartedAt));
        }

        if (!settings.RequireWorkOrderApproval || (request.IsBreakdown && settings.AutoApproveBreakdowns))
        {
            workOrder.ApprovedAt = now;
            workOrder.ApprovedByUserId = actor.UserId;
            SetStatus(workOrder, actor, WorkOrderStatuses.Approved,
                settings.RequireWorkOrderApproval ? "Approved automatically: breakdown." : "Approved automatically: approval is not required.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return workOrder;
    }

    public async Task ApplyAsync(WorkOrder workOrder, FactoryActor actor, FactoryTransition transition, CancellationToken cancellationToken = default)
    {
        var action = transition.Action.Trim().ToLowerInvariant();
        if (!FactoryWorkOrderActions.From.TryGetValue(action, out var allowedFrom))
        {
            throw new BusinessRuleException("Unknown action.");
        }

        var status = WorkOrderStatuses.Normalize(workOrder.Status);
        if (!allowedFrom.Contains(status, StringComparer.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException($"A job that is {status.ToLowerInvariant()} can't be {Describe(action)}.");
        }

        var now = OccurredAt.UtcNow;
        switch (action)
        {
            case FactoryWorkOrderActions.Approve:
                workOrder.ApprovedAt = now;
                workOrder.ApprovedByUserId = actor.UserId;
                SetStatus(workOrder, actor, WorkOrderStatuses.Approved, transition.Reason ?? "Approved.");
                break;

            case FactoryWorkOrderActions.Reject:
                SetStatus(workOrder, actor, WorkOrderStatuses.Rejected, $"Rejected: {RequireReason(transition.Reason, "Say why the request is rejected.")}");
                await EndDowntimeAsync(workOrder, actor, now, true, cancellationToken);
                break;

            case FactoryWorkOrderActions.Plan:
                if (transition.ScheduledStartAt is null)
                {
                    throw new BusinessRuleException("Give the planned start.");
                }

                workOrder.ScheduledStartAt = transition.ScheduledStartAt;
                workOrder.ScheduledEndAt = transition.ScheduledEndAt;
                workOrder.EstimatedLabourHours = transition.EstimatedHours ?? workOrder.EstimatedLabourHours;
                workOrder.PlannedAt = now;
                SetStatus(workOrder, actor, WorkOrderStatuses.Planned, $"Planned for {transition.ScheduledStartAt:yyyy-MM-dd HH:mm}.");
                break;

            case FactoryWorkOrderActions.Assign:
                await AssignAsync(workOrder, actor, transition, cancellationToken);
                break;

            case FactoryWorkOrderActions.Start:
                await guards.EnsureCanStartAsync(workOrder, DateTime.UtcNow, cancellationToken);
                workOrder.WorkStartedAt ??= now;
                SetStatus(workOrder, actor, WorkOrderStatuses.InProgress, "Work started.");
                break;

            case FactoryWorkOrderActions.Hold:
            {
                var reason = FactoryHoldReasons.Find(transition.HoldReason)
                    ?? throw new BusinessRuleException($"Hold reason must be one of: {string.Join(", ", FactoryHoldReasons.All)}.");
                workOrder.PreviousStatus = status;
                workOrder.HoldReason = reason;
                workOrder.HoldNote = string.IsNullOrWhiteSpace(transition.Reason) ? null : transition.Reason.Trim();
                workOrder.HoldStartedAt = now;
                workOrder.HoldEndedAt = null;
                if (reason == WorkOrderHoldReasons.AwaitingWindow)
                {
                    workOrder.NeedsLineStop = true;
                }

                SetStatus(workOrder, actor, reason == WorkOrderHoldReasons.AwaitingSpares ? WorkOrderStatuses.WaitingOnParts : WorkOrderStatuses.OnHold,
                    WorkOrderHoldReasons.BuildMessage(reason, workOrder.HoldNote));
                break;
            }

            case FactoryWorkOrderActions.Resume:
            {
                var back = workOrder.PreviousStatus is WorkOrderStatuses.InProgress ? WorkOrderStatuses.InProgress : WorkOrderStatuses.Assigned;
                if (back == WorkOrderStatuses.InProgress)
                {
                    await guards.EnsureCanStartAsync(workOrder, DateTime.UtcNow, cancellationToken);
                }

                workOrder.HoldEndedAt = now;
                workOrder.HoldReason = null;
                workOrder.HoldNote = null;
                SetStatus(workOrder, actor, back, "Resumed.");
                break;
            }

            case FactoryWorkOrderActions.Complete:
                await CompleteAsync(workOrder, actor, transition, now, cancellationToken);
                await pmProgress.OnJobFinishedAsync(workOrder, true, workOrder.CompletedAt ?? now, actor.Name, null, cancellationToken);
                break;

            case FactoryWorkOrderActions.Verify:
                workOrder.VerifiedAt = now;
                workOrder.VerifiedByUserId = actor.UserId;
                workOrder.VerificationNotes = string.IsNullOrWhiteSpace(transition.Reason) ? null : transition.Reason.Trim();
                SetStatus(workOrder, actor, WorkOrderStatuses.Verified, workOrder.VerificationNotes is null ? "Work checked and accepted." : $"Work checked and accepted: {workOrder.VerificationNotes}");
                break;

            case FactoryWorkOrderActions.ReturnForRework:
                workOrder.CompletedAt = null;
                SetStatus(workOrder, actor, WorkOrderStatuses.InProgress, $"Sent back: {RequireReason(transition.Reason, "Say what still needs doing.")}");
                break;

            case FactoryWorkOrderActions.Close:
                workOrder.ClosedAt = now;
                workOrder.ClosedByUserId = actor.UserId;
                SetStatus(workOrder, actor, WorkOrderStatuses.Closed, "Closed.");
                break;

            case FactoryWorkOrderActions.Cancel:
                SetStatus(workOrder, actor, WorkOrderStatuses.Cancelled, $"Cancelled: {RequireReason(transition.Reason, "Say why the job is cancelled.")}");
                await EndDowntimeAsync(workOrder, actor, now, true, cancellationToken);
                // A cancelled PM job is a skipped PM: the plan moves on to its next due.
                await pmProgress.OnJobFinishedAsync(workOrder, false, now, actor.Name, transition.Reason, cancellationToken);
                break;

            case FactoryWorkOrderActions.Reopen:
                workOrder.ClosedAt = null;
                workOrder.VerifiedAt = null;
                workOrder.CompletedAt = null;
                SetStatus(workOrder, actor, WorkOrderStatuses.Reopened, $"Reopened: {RequireReason(transition.Reason, "Say why the job is reopened.")}");
                break;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task AssignAsync(WorkOrder workOrder, FactoryActor actor, FactoryTransition transition, CancellationToken cancellationToken)
    {
        var ids = (transition.TechnicianIds ?? []).Distinct().ToList();
        if (ids.Count == 0)
        {
            throw new BusinessRuleException("Choose at least one technician.");
        }

        var technicians = await dbContext.Technicians
            .Where(x => x.TenantId == workOrder.TenantId && ids.Contains(x.Id) && x.IsActive)
            .ToListAsync(cancellationToken);
        if (technicians.Count != ids.Count)
        {
            throw new BusinessRuleException("One or more technicians were not found or are inactive.");
        }

        await guards.EnsureCanAssignAsync(workOrder, technicians, DateTime.UtcNow, cancellationToken);
        var lead = transition.LeadTechnicianId is { } leadId && ids.Contains(leadId) ? leadId : ids[0];
        var existing = await dbContext.WorkOrderTechnicianAssignments
            .Where(x => x.TenantId == workOrder.TenantId && x.WorkOrderId == workOrder.Id)
            .ToListAsync(cancellationToken);
        dbContext.WorkOrderTechnicianAssignments.RemoveRange(existing.Where(x => !ids.Contains(x.TechnicianId)));
        foreach (var id in ids)
        {
            var row = existing.FirstOrDefault(x => x.TechnicianId == id);
            if (row is null)
            {
                dbContext.WorkOrderTechnicianAssignments.Add(new WorkOrderTechnicianAssignment
                {
                    TenantId = workOrder.TenantId,
                    WorkOrderId = workOrder.Id,
                    TechnicianId = id,
                    IsLead = id == lead,
                    AssignedByUserId = actor.UserId,
                    Status = "Accepted",
                    AcceptedAt = DateTime.UtcNow
                });
            }
            else
            {
                row.IsLead = id == lead;
            }
        }

        workOrder.AssignedTechnicianId = lead;
        workOrder.LeadTechnicianId = lead;
        workOrder.AssignedTechnicianIdsJson = JsonSerializer.Serialize(ids);
        workOrder.AssignmentType = ids.Count > 1 ? "MultipleTechnicians" : "IndividualTechnician";
        var names = string.Join(", ", technicians.OrderByDescending(x => x.Id == lead).Select(x => x.FullName));
        var message = existing.Count == 0 ? $"Assigned to {names}." : $"Reassigned to {names}.";

        // Reassigning a job that is under way or on hold keeps it where it is.
        var status = WorkOrderStatuses.Normalize(workOrder.Status);
        if (status is WorkOrderStatuses.InProgress or WorkOrderStatuses.OnHold or WorkOrderStatuses.WaitingOnParts or WorkOrderStatuses.Assigned)
        {
            AddEvent(workOrder, actor, "Assigned", status, status, message);
        }
        else
        {
            SetStatus(workOrder, actor, WorkOrderStatuses.Assigned, message);
        }
    }

    private async Task CompleteAsync(WorkOrder workOrder, FactoryActor actor, FactoryTransition transition, DateTime now, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(transition.WorkDone))
        {
            throw new BusinessRuleException("Describe the work done.");
        }

        var problemId = transition.ProblemCodeId ?? workOrder.FailureProblemCodeId;
        if (problemId is null && (workOrder.WorkType == FactoryWorkTypes.Corrective || workOrder.IsBreakdown))
        {
            throw new BusinessRuleException("Choose the problem found, so repeat faults can be tracked.");
        }

        await guards.EnsureCanCompleteAsync(workOrder, cancellationToken);
        var stepsLeft = await dbContext.WorkOrderChecklistItems
            .CountAsync(x => x.WorkOrderId == workOrder.Id && x.IsRequired && !x.IsCompleted, cancellationToken);
        if (stepsLeft > 0)
        {
            throw new BusinessRuleException($"Finish the required steps first ({stepsLeft} left).");
        }

        if (problemId is { } p) await EnsureCodeAsync(workOrder.TenantId, p, FailureCodeKinds.Problem, cancellationToken);
        if (transition.CauseCodeId is { } c) await EnsureCodeAsync(workOrder.TenantId, c, FailureCodeKinds.Cause, cancellationToken);
        if (transition.RemedyCodeId is { } r) await EnsureCodeAsync(workOrder.TenantId, r, FailureCodeKinds.Remedy, cancellationToken);

        workOrder.FailureProblemCodeId = problemId;
        workOrder.FailureCauseCodeId = transition.CauseCodeId ?? workOrder.FailureCauseCodeId;
        workOrder.FailureRemedyCodeId = transition.RemedyCodeId ?? workOrder.FailureRemedyCodeId;
        workOrder.WorkDoneNotes = transition.WorkDone.Trim();
        workOrder.CompletedAt = now;
        if (workOrder.WorkStartedAt is { } started)
        {
            workOrder.ActualDuration = Math.Round((decimal)(now - started).TotalHours, 2);
        }

        if (!string.IsNullOrWhiteSpace(transition.SignatureDataUrl))
        {
            if (!transition.SignatureDataUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) || transition.SignatureDataUrl.Length > 500_000)
            {
                throw new BusinessRuleException("The signature could not be read. Sign again.");
            }

            dbContext.WorkOrderSignatures.Add(new WorkOrderSignature
            {
                TenantId = workOrder.TenantId,
                WorkOrderId = workOrder.Id,
                SignatureType = "Technician",
                SignerName = string.IsNullOrWhiteSpace(transition.SignerName) ? actor.Name : transition.SignerName.Trim(),
                SignatureDataUrl = transition.SignatureDataUrl,
                CapturedByUserId = actor.UserId ?? Guid.Empty,
                CapturedAt = now
            });
        }

        await DetectReworkAsync(workOrder, now, cancellationToken);
        if (transition.AssetBackInService)
        {
            await EndDowntimeAsync(workOrder, actor, AssetLifecycleService.EffectiveAt(transition.DowntimeEndedAt), false, cancellationToken);
        }

        SetStatus(workOrder, actor, WorkOrderStatuses.Completed, "Work completed.");
    }

    /// <summary>The same fault on the same asset within the rework window links to and flags the earlier job.</summary>
    private async Task DetectReworkAsync(WorkOrder workOrder, DateTime now, CancellationToken cancellationToken)
    {
        if (workOrder.AssetId is not { } assetId || workOrder.FailureProblemCodeId is not { } problemId)
        {
            return;
        }

        var settings = await factorySettingsService.GetOrCreateAsync(workOrder.TenantId, cancellationToken);
        var since = now.AddDays(-settings.ReworkWindowDays);
        var previous = await dbContext.WorkOrders
            .Where(x => x.TenantId == workOrder.TenantId && x.Id != workOrder.Id && x.AssetId == assetId
                && x.FailureProblemCodeId == problemId && x.CompletedAt != null && x.CompletedAt >= since)
            .OrderByDescending(x => x.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (previous is null)
        {
            return;
        }

        workOrder.ReworkOfWorkOrderId = previous.Id;
        workOrder.FollowUpOfWorkOrderId ??= previous.Id;
        workOrder.IsRepeatVisit = true;
        workOrder.RepeatVisitCount = previous.RepeatVisitCount + 1;
        AddEvent(workOrder, null, "Rework", null, null,
            $"Same fault as {previous.WorkOrderNumber} on {previous.CompletedAt:yyyy-MM-dd}, within {settings.ReworkWindowDays} days: flagged as rework.");
    }

    private void StartDowntime(WorkOrder workOrder, Asset asset, FactoryActor actor, DateTime at)
    {
        workOrder.DowntimeStartedAt = at;
        if (asset.OperationalStatus != AssetOperationalStatuses.Down)
        {
            var entry = assetLifecycleService.Record(asset, AssetLifecycleEventTypes.StatusChanged, actor.UserId, actor.Name, $"Breakdown {workOrder.WorkOrderNumber}.", at);
            entry.FromValue = asset.OperationalStatus;
            entry.ToValue = AssetOperationalStatuses.Down;
            asset.OperationalStatus = AssetOperationalStatuses.Down;
        }
    }

    /// <summary>Ends a breakdown's downtime and puts the asset back in service unless another breakdown still has it down.</summary>
    private async Task EndDowntimeAsync(WorkOrder workOrder, FactoryActor actor, DateTime at, bool abandoned, CancellationToken cancellationToken)
    {
        if (!workOrder.IsBreakdown || workOrder.DowntimeEndedAt.HasValue || workOrder.AssetId is not { } assetId)
        {
            return;
        }

        workOrder.DowntimeEndedAt = workOrder.DowntimeStartedAt is { } start && at < start ? start : at;
        var stillDown = await dbContext.WorkOrders.AnyAsync(x => x.TenantId == workOrder.TenantId && x.Id != workOrder.Id
            && x.AssetId == assetId && x.IsBreakdown && x.DowntimeStartedAt != null && x.DowntimeEndedAt == null, cancellationToken);
        var asset = await dbContext.Assets.SingleAsync(x => x.Id == assetId, cancellationToken);
        if (!stillDown && asset.OperationalStatus == AssetOperationalStatuses.Down)
        {
            var entry = assetLifecycleService.Record(asset, AssetLifecycleEventTypes.StatusChanged, actor.UserId, actor.Name,
                abandoned ? $"Breakdown {workOrder.WorkOrderNumber} closed without repair." : $"Back in service after {workOrder.WorkOrderNumber}.", workOrder.DowntimeEndedAt);
            entry.FromValue = AssetOperationalStatuses.Down;
            entry.ToValue = AssetOperationalStatuses.Operational;
            asset.OperationalStatus = AssetOperationalStatuses.Operational;
        }
    }

    private async Task EnsureCodeAsync(Guid tenantId, Guid id, string kind, CancellationToken cancellationToken)
    {
        if (!await dbContext.FailureCodes.AnyAsync(x => x.TenantId == tenantId && x.Id == id && x.Kind == kind && x.IsActive, cancellationToken))
        {
            throw new BusinessRuleException($"The {kind.ToLowerInvariant()} code was not found.");
        }
    }

    private void SetStatus(WorkOrder workOrder, FactoryActor actor, string status, string message)
    {
        var old = workOrder.Status;
        workOrder.Status = status;
        AddEvent(workOrder, actor, "StatusChanged", old, status, message);
    }

    private void AddEvent(WorkOrder workOrder, FactoryActor? actor, string type, string? oldStatus, string? newStatus, string message) =>
        dbContext.WorkOrderEvents.Add(new WorkOrderEvent
        {
            TenantId = workOrder.TenantId,
            WorkOrderId = workOrder.Id,
            ActorUserId = actor?.UserId,
            EventType = type,
            Status = newStatus,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            Message = message,
            OccurredAt = Services.OccurredAt.UtcNow
        });

    private static string RequireReason(string? reason, string message) =>
        string.IsNullOrWhiteSpace(reason) ? throw new BusinessRuleException(message) : reason.Trim();

    private static string Describe(string action) => action switch
    {
        FactoryWorkOrderActions.Approve => "approved",
        FactoryWorkOrderActions.Reject => "rejected",
        FactoryWorkOrderActions.Plan => "planned",
        FactoryWorkOrderActions.Assign => "assigned",
        FactoryWorkOrderActions.Start => "started",
        FactoryWorkOrderActions.Hold => "put on hold",
        FactoryWorkOrderActions.Resume => "resumed",
        FactoryWorkOrderActions.Complete => "completed",
        FactoryWorkOrderActions.Verify => "verified",
        FactoryWorkOrderActions.ReturnForRework => "sent back",
        FactoryWorkOrderActions.Close => "closed",
        FactoryWorkOrderActions.Cancel => "cancelled",
        _ => "reopened"
    };
}
