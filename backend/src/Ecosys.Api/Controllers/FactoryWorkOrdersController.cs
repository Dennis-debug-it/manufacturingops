using System.ComponentModel.DataAnnotations;
using Ecosys.Api.Common;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// In-house factory jobs: requested → approved → planned → assigned → in progress → completed →
/// verified → closed, with holds, failure codes, rework detection, child jobs, downtime, handover
/// notes, labour and parts. Client (ServiceOps) jobs stay on /api/workorders.
/// </summary>
[ApiController]
[Authorize]
[Route("api/factory/work-orders")]
public sealed class FactoryWorkOrdersController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IOrgScopeService orgScopeService,
    IFactoryWorkOrderService workOrderService,
    IFailureCodeService failureCodeService,
    IFactorySlaService slaService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<FactoryWorkOrderSummary>>> List(
        [FromQuery] string? view,
        [FromQuery] Guid? orgNodeId,
        [FromQuery] Guid? assetId,
        [FromQuery] string? workType,
        [FromQuery] string? priority,
        [FromQuery] string? status,
        [FromQuery] bool? breakdown,
        [FromQuery] bool? mine,
        [FromQuery] string? search,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        EnsureCanView();
        var query = await ScopedAsync(dbContext.WorkOrders.Where(x => x.TenantId == TenantId && x.ClientId == null && x.OrgNodeId != null), cancellationToken);

        query = (view ?? "open").Trim().ToLowerInvariant() switch
        {
            "open" => query.Where(x => !ClosedOrDone.Contains(x.Status)),
            "approval" => query.Where(x => x.Status == WorkOrderStatuses.Requested),
            "verify" => query.Where(x => x.Status == WorkOrderStatuses.Completed),
            "overdue" => query.Where(x => !ClosedOrDone.Contains(x.Status) && x.DueDate != null && x.DueDate < DateTime.UtcNow),
            "closed" => query.Where(x => ClosedOrDone.Contains(x.Status)),
            "all" => query,
            _ => throw new BusinessRuleException("View must be open, approval, verify, overdue, closed or all.")
        };

        if (orgNodeId.HasValue)
        {
            var subtree = (await orgScopeService.GetSubtreeAsync(TenantId, orgNodeId.Value, cancellationToken)).ToList();
            query = query.Where(x => subtree.Contains(x.OrgNodeId!.Value));
        }

        if (assetId.HasValue) query = query.Where(x => x.AssetId == assetId);
        if (FactoryWorkTypes.Find(workType) is { } type) query = query.Where(x => x.WorkType == type);
        if (FactoryPriorities.Find(priority) is { } level) query = query.Where(x => x.Priority == level);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == WorkOrderStatuses.Normalize(status));
        if (breakdown == true) query = query.Where(x => x.IsBreakdown);
        if (mine == true)
        {
            var technicianId = await CurrentTechnicianIdAsync(cancellationToken);
            query = technicianId is { } id
                ? query.Where(x => x.TechnicianAssignments.Any(a => a.TechnicianId == id))
                : query.Where(_ => false);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.WorkOrderNumber.ToLower().Contains(term) || x.Title.ToLower().Contains(term)
                || (x.Asset != null && (x.Asset.AssetCode.ToLower().Contains(term) || x.Asset.AssetName.ToLower().Contains(term))));
        }

        var size = Math.Clamp(pageSize ?? 50, 1, 200);
        var number = Math.Max(page ?? 1, 1);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(x => x.IsBreakdown && x.DowntimeEndedAt == null)
            .ThenBy(x => x.Priority == "Critical" ? 0 : x.Priority == "High" ? 1 : x.Priority == "Medium" ? 2 : 3)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((number - 1) * size)
            .Take(size)
            .Select(x => new FactoryWorkOrderSummary(
                x.Id, x.WorkOrderNumber, x.Title, x.WorkType, x.Priority, x.Status, x.IsBreakdown, x.NeedsLineStop,
                x.AssetId, x.Asset != null ? x.Asset.AssetCode : null, x.Asset != null ? x.Asset.AssetName : null,
                x.Asset != null ? x.Asset.Criticality : null,
                x.OrgNodeId, x.OrgNode != null ? x.OrgNode.Name : null,
                x.LeadTechnician != null ? x.LeadTechnician.FullName : null,
                x.DueDate, x.SlaResolutionDeadline, x.CreatedAt, x.CompletedAt, x.DowntimeStartedAt, x.DowntimeEndedAt,
                x.ReworkOfWorkOrderId != null, x.HoldReason))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<FactoryWorkOrderSummary>(rows, number, size, total));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FactoryWorkOrderDetail>> Get(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var workOrder = await LoadAsync(id, cancellationToken);
        return Ok(await MapDetailAsync(workOrder, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<FactoryWorkOrderDetail>> Create([FromBody] CreateFactoryWorkOrderRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanCreateWorkOrders, PermissionNames.CanAssignWorkOrders);
        await failureCodeService.EnsureSeededAsync(TenantId, cancellationToken);
        var workOrder = await workOrderService.CreateAsync(TenantId, Actor, new CreateFactoryWorkOrder(
            request.Title,
            request.WorkType ?? FactoryWorkTypes.Corrective,
            request.Priority ?? "Medium",
            request.AssetId,
            request.OrgNodeId,
            request.Description,
            request.IsBreakdown,
            request.NeedsLineStop,
            request.DueDate,
            request.DowntimeStartedAt,
            request.EstimatedHours,
            FactoryRequestSources.Office,
            request.RequestedByName,
            null,
            request.ProblemCodeId,
            request.JobPlanId), cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "factory-work-order.created", nameof(WorkOrder), workOrder.Id.ToString(),
            $"{(workOrder.IsBreakdown ? "Breakdown" : "Job")} {workOrder.WorkOrderNumber} raised: {workOrder.Title}", cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = workOrder.Id }, await MapDetailAsync(await LoadAsync(workOrder.Id, cancellationToken), cancellationToken));
    }

    /// <summary>Edits the request itself; allowed until the work is completed.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<FactoryWorkOrderDetail>> Update(Guid id, [FromBody] UpdateFactoryWorkOrderRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanAssignWorkOrders);
        var workOrder = await LoadAsync(id, cancellationToken);
        if (!FactoryWorkOrderActions.From[FactoryWorkOrderActions.Assign].Contains(workOrder.Status) && workOrder.Status != WorkOrderStatuses.Requested)
        {
            throw new BusinessRuleException("A finished job can't be edited.");
        }

        if (string.IsNullOrWhiteSpace(request.Title)) throw new BusinessRuleException("Title is required.");
        workOrder.Title = request.Title.Trim();
        workOrder.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        workOrder.WorkType = FactoryWorkTypes.Find(request.WorkType) ?? throw new BusinessRuleException("Unknown job type.");
        var priority = FactoryPriorities.Find(request.Priority) ?? throw new BusinessRuleException("Unknown priority.");
        workOrder.DueDate = request.DueDate;
        if (priority != workOrder.Priority)
        {
            // Targets follow the new priority, still counted from when the job was raised.
            workOrder.Priority = priority;
            await slaService.ApplyAsync(workOrder, workOrder.Asset?.Criticality, cancellationToken);
        }

        workOrder.NeedsLineStop = request.NeedsLineStop;
        workOrder.EstimatedLabourHours = request.EstimatedHours;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await MapDetailAsync(workOrder, cancellationToken));
    }

    /// <summary>
    /// Moves the job on: approve, reject, plan, assign, start, hold, resume, complete, verify,
    /// return (send back after checking), close, cancel, reopen.
    /// </summary>
    [HttpPost("{id:guid}/actions/{action}")]
    public async Task<ActionResult<FactoryWorkOrderDetail>> Act(Guid id, string action, [FromBody] FactoryActionRequest request, CancellationToken cancellationToken)
    {
        var workOrder = await LoadAsync(id, cancellationToken);
        await EnsureCanActAsync(workOrder, action, cancellationToken);
        await workOrderService.ApplyAsync(workOrder, Actor, new FactoryTransition(
            action,
            request.Reason,
            request.HoldReason,
            request.TechnicianIds,
            request.LeadTechnicianId,
            request.ScheduledStartAt,
            request.ScheduledEndAt,
            request.EstimatedHours,
            request.WorkDone,
            request.ProblemCodeId,
            request.CauseCodeId,
            request.RemedyCodeId,
            request.AssetBackInService ?? true,
            request.DowntimeEndedAt,
            request.SignatureDataUrl,
            request.SignerName), cancellationToken);
        return Ok(await MapDetailAsync(await LoadAsync(id, cancellationToken), cancellationToken));
    }

    /// <summary>Splits off a child job (for example a second trade, or parts of an overhaul).</summary>
    [HttpPost("{id:guid}/children")]
    public async Task<ActionResult<FactoryWorkOrderDetail>> Split(Guid id, [FromBody] CreateChildWorkOrderRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanAssignWorkOrders);
        var parent = await LoadAsync(id, cancellationToken);
        var child = await workOrderService.CreateAsync(TenantId, Actor, new CreateFactoryWorkOrder(
            request.Title,
            request.WorkType ?? parent.WorkType,
            request.Priority ?? parent.Priority,
            request.AssetId ?? parent.AssetId,
            parent.OrgNodeId,
            request.Description,
            NeedsLineStop: parent.NeedsLineStop,
            DueDate: request.DueDate ?? parent.DueDate,
            EstimatedHours: request.EstimatedHours,
            ParentWorkOrderId: parent.Id), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = child.Id }, await MapDetailAsync(await LoadAsync(child.Id, cancellationToken), cancellationToken));
    }

    /// <summary>Ticks a job plan step done (or not), with an optional remark.</summary>
    [HttpPost("{id:guid}/steps/{stepId:guid}")]
    public async Task<ActionResult<FactoryWorkOrderDetail>> Step(Guid id, Guid stepId, [FromBody] StepUpdateRequest request, CancellationToken cancellationToken)
    {
        var workOrder = await LoadAsync(id, cancellationToken);
        await EnsureCanActAsync(workOrder, FactoryWorkOrderActions.Start, cancellationToken, anyOpenStatus: true);
        var step = await dbContext.WorkOrderChecklistItems.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.WorkOrderId == id && x.Id == stepId, cancellationToken)
            ?? throw new NotFoundException("Step was not found.");
        step.ResponseValue = request.Done ? "Done" : null;
        step.Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await MapDetailAsync(workOrder, cancellationToken));
    }

    /// <summary>A note for the next shift: what was found, what is left, anything unsafe.</summary>
    [HttpPost("{id:guid}/handover")]
    public async Task<ActionResult<FactoryWorkOrderDetail>> Handover(Guid id, [FromBody] HandoverNoteRequest request, CancellationToken cancellationToken)
    {
        var workOrder = await LoadAsync(id, cancellationToken);
        await EnsureCanActAsync(workOrder, FactoryWorkOrderActions.Start, cancellationToken, anyOpenStatus: true);
        if (string.IsNullOrWhiteSpace(request.Note)) throw new BusinessRuleException("Write the handover note.");
        dbContext.WorkOrderEvents.Add(new WorkOrderEvent
        {
            TenantId = TenantId, WorkOrderId = id, ActorUserId = UserId, EventType = "Handover",
            Status = workOrder.Status, Message = request.Note.Trim(), Note = UserDisplayName, OccurredAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await MapDetailAsync(workOrder, cancellationToken));
    }

    [HttpPost("{id:guid}/labour")]
    public async Task<ActionResult<FactoryWorkOrderDetail>> Labour(Guid id, [FromBody] LabourEntryRequest request, CancellationToken cancellationToken)
    {
        var workOrder = await LoadAsync(id, cancellationToken);
        await EnsureCanActAsync(workOrder, FactoryWorkOrderActions.Start, cancellationToken, anyOpenStatus: true);
        var technicianId = request.TechnicianId ?? await CurrentTechnicianIdAsync(cancellationToken)
            ?? throw new BusinessRuleException("Choose whose time this is.");
        var technician = await dbContext.Technicians.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == technicianId, cancellationToken)
            ?? throw new BusinessRuleException("Technician was not found.");
        if (request.Minutes is <= 0 or > 24 * 60) throw new BusinessRuleException("Time must be between 1 minute and 24 hours.");
        var started = FactoryTime.EffectiveAt(request.StartedAt);
        dbContext.WorkOrderTimeLogs.Add(new WorkOrderTimeLog
        {
            TenantId = TenantId, WorkOrderId = id, TechnicianId = technician.Id, TechnicianName = technician.FullName,
            StartedAt = started, EndedAt = started.AddMinutes(request.Minutes), DurationMinutes = request.Minutes,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await MapDetailAsync(workOrder, cancellationToken));
    }

    /// <summary>Records a part used. Stock is not moved yet: factory stores arrive with Phase 5.</summary>
    [HttpPost("{id:guid}/parts")]
    public async Task<ActionResult<FactoryWorkOrderDetail>> Parts(Guid id, [FromBody] PartUsedRequest request, CancellationToken cancellationToken)
    {
        var workOrder = await LoadAsync(id, cancellationToken);
        await EnsureCanActAsync(workOrder, FactoryWorkOrderActions.Start, cancellationToken, anyOpenStatus: true);
        if (request.Quantity <= 0) throw new BusinessRuleException("Quantity must be more than zero.");
        if (!await dbContext.MaterialItems.AnyAsync(x => x.TenantId == TenantId && x.Id == request.MaterialItemId, cancellationToken))
            throw new BusinessRuleException("Part was not found.");
        dbContext.WorkOrderMaterialUsages.Add(new WorkOrderMaterialUsage
        {
            TenantId = TenantId, WorkOrderId = id, MaterialItemId = request.MaterialItemId, AssetId = workOrder.AssetId,
            QuantityUsed = request.Quantity, Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            UsedByUserId = UserId, UsedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await MapDetailAsync(workOrder, cancellationToken));
    }

    [HttpGet("/api/failure-codes")]
    public async Task<ActionResult<IReadOnlyCollection<FailureCodeResponse>>> FailureCodes([FromQuery] string? kind, [FromQuery] Guid? assetCategoryId, [FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        EnsureCanView();
        await failureCodeService.EnsureSeededAsync(TenantId, cancellationToken);
        var query = dbContext.FailureCodes.Where(x => x.TenantId == TenantId && (includeInactive || x.IsActive));
        if (FailureCodeKinds.Find(kind) is { } k) query = query.Where(x => x.Kind == k);
        // Codes for the asset's class come first, then the general ones; other classes' codes are left out.
        if (assetCategoryId.HasValue) query = query.Where(x => x.AssetCategoryId == null || x.AssetCategoryId == assetCategoryId);
        var codes = await query.OrderBy(x => x.Kind).ThenByDescending(x => x.AssetCategoryId != null).ThenBy(x => x.DisplayOrder).ToListAsync(cancellationToken);
        return Ok(codes.Select(MapCode).ToList());
    }

    [HttpPost("/api/failure-codes")]
    public async Task<ActionResult<FailureCodeResponse>> CreateFailureCode([FromBody] UpsertFailureCodeRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        await failureCodeService.EnsureSeededAsync(TenantId, cancellationToken);
        var code = new FailureCode { TenantId = TenantId };
        await ApplyCodeAsync(code, request, cancellationToken);
        code.DisplayOrder = await dbContext.FailureCodes.CountAsync(x => x.TenantId == TenantId, cancellationToken);
        dbContext.FailureCodes.Add(code);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapCode(code));
    }

    [HttpPut("/api/failure-codes/{codeId:guid}")]
    public async Task<ActionResult<FailureCodeResponse>> UpdateFailureCode(Guid codeId, [FromBody] UpsertFailureCodeRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        var code = await dbContext.FailureCodes.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == codeId, cancellationToken)
            ?? throw new NotFoundException("Failure code was not found.");
        await ApplyCodeAsync(code, request, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapCode(code));
    }

    private static readonly string[] ClosedOrDone =
        [WorkOrderStatuses.Completed, WorkOrderStatuses.Verified, WorkOrderStatuses.Closed, WorkOrderStatuses.Cancelled, WorkOrderStatuses.Rejected];

    private FactoryActor Actor => new(UserId, UserDisplayName);

    private void EnsureCanView() =>
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanViewWorkOrders, PermissionNames.CanCompleteWorkOrders,
            PermissionNames.CanCreateWorkOrders, PermissionNames.CanAssignWorkOrders);

    /// <summary>Supervisory actions need the assign permission; the technician's actions need the job to be theirs (or that permission).</summary>
    private async Task EnsureCanActAsync(WorkOrder workOrder, string action, CancellationToken cancellationToken, bool anyOpenStatus = false)
    {
        if (IsAdmin || HasPermission(PermissionNames.CanAssignWorkOrders))
        {
            return;
        }

        if (FactoryWorkOrderActions.Supervisory.Contains(action))
        {
            throw new ForbiddenException("Only a supervisor or planner can do this.");
        }

        if (!HasPermission(PermissionNames.CanCompleteWorkOrders))
        {
            throw new ForbiddenException("You can't work on jobs.");
        }

        var technicianId = await CurrentTechnicianIdAsync(cancellationToken);
        var assigned = technicianId is { } tid && await dbContext.WorkOrderTechnicianAssignments
            .AnyAsync(x => x.TenantId == TenantId && x.WorkOrderId == workOrder.Id && x.TechnicianId == tid, cancellationToken);
        if (!assigned)
        {
            throw new ForbiddenException("This job is not assigned to you.");
        }

        if (anyOpenStatus && ClosedOrDone.Contains(workOrder.Status))
        {
            throw new BusinessRuleException("The job is finished.");
        }
    }

    private async Task<Guid?> CurrentTechnicianIdAsync(CancellationToken cancellationToken)
    {
        var userId = UserId;
        return await dbContext.Technicians
            .Where(x => x.TenantId == TenantId && x.IsActive && x.UserId == userId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<IQueryable<WorkOrder>> ScopedAsync(IQueryable<WorkOrder> query, CancellationToken cancellationToken)
    {
        var scope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        if (scope.Nodes is null) return query;
        var nodes = scope.Nodes.ToList();
        return query.Where(x => x.OrgNodeId.HasValue && nodes.Contains(x.OrgNodeId.Value));
    }

    private async Task<WorkOrder> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        EnsureTenantOperationalAccess();
        var query = await ScopedAsync(dbContext.WorkOrders.Where(x => x.TenantId == TenantId && x.Id == id && x.ClientId == null), cancellationToken);
        return await query
            .Include(x => x.Asset)
            .Include(x => x.OrgNode)
            .Include(x => x.FailureProblemCode)
            .Include(x => x.FailureCauseCode)
            .Include(x => x.FailureRemedyCode)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Job was not found.");
    }

    private async Task<FactoryWorkOrderDetail> MapDetailAsync(WorkOrder x, CancellationToken cancellationToken)
    {
        var events = await dbContext.WorkOrderEvents.Where(e => e.WorkOrderId == x.Id).OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.CreatedAt).ToListAsync(cancellationToken);
        var actors = await dbContext.Users.Where(u => events.Select(e => e.ActorUserId).Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
        var team = await dbContext.WorkOrderTechnicianAssignments.Where(a => a.WorkOrderId == x.Id)
            .Select(a => new FactoryJobTechnician(a.TechnicianId, a.Technician != null ? a.Technician.FullName : string.Empty, a.IsLead)).ToListAsync(cancellationToken);
        var labour = await dbContext.WorkOrderTimeLogs.Where(t => t.WorkOrderId == x.Id).OrderBy(t => t.StartedAt)
            .Select(t => new FactoryLabourEntry(t.Id, t.TechnicianName, t.StartedAt, t.DurationMinutes, t.Notes)).ToListAsync(cancellationToken);
        var parts = await dbContext.WorkOrderMaterialUsages.Where(p => p.WorkOrderId == x.Id).OrderBy(p => p.UsedAt)
            .Select(p => new FactoryPartUsed(p.Id, p.MaterialItemId, p.MaterialItem != null ? p.MaterialItem.ItemName : string.Empty, p.QuantityUsed, p.Notes, p.UsedAt)).ToListAsync(cancellationToken);
        var children = await dbContext.WorkOrders.Where(c => c.ParentWorkOrderId == x.Id)
            .Select(c => new FactoryJobLink(c.Id, c.WorkOrderNumber, c.Title, c.Status)).ToListAsync(cancellationToken);
        var links = await dbContext.WorkOrders.Where(w => w.Id == x.ParentWorkOrderId || w.Id == x.ReworkOfWorkOrderId)
            .Select(w => new FactoryJobLink(w.Id, w.WorkOrderNumber, w.Title, w.Status)).ToListAsync(cancellationToken);
        var signature = await dbContext.WorkOrderSignatures.Where(s => s.WorkOrderId == x.Id).OrderByDescending(s => s.CapturedAt)
            .Select(s => new FactorySignature(s.SignerName, s.CapturedAt, s.SignatureDataUrl)).FirstOrDefaultAsync(cancellationToken);
        var steps = await dbContext.WorkOrderChecklistItems.Where(s => s.WorkOrderId == x.Id && s.QuestionType == "Task").OrderBy(s => s.SortOrder)
            .Select(s => new FactoryJobStep(s.Id, s.QuestionText, s.HelperText, s.ResponseValue == "Done", s.Remarks)).ToListAsync(cancellationToken);
        var plan = x.JobPlanId is { } planId
            ? await dbContext.JobPlans.Include(p => p.Steps).Include(p => p.Resources).Include(p => p.AssetCategory).SingleOrDefaultAsync(p => p.Id == planId, cancellationToken)
            : null;
        var downtimeHours = x.DowntimeStartedAt is { } start ? Math.Round((decimal)((x.DowntimeEndedAt ?? DateTime.UtcNow) - start).TotalHours, 2) : (decimal?)null;

        return new FactoryWorkOrderDetail(
            x.Id, x.WorkOrderNumber, x.Title, x.Description, x.WorkType, x.Priority, x.Status,
            x.IsBreakdown, x.NeedsLineStop, x.RequestSource, x.RequestedByName,
            x.AssetId, x.Asset?.AssetCode, x.Asset?.AssetName, x.Asset?.Criticality, x.Asset?.AssetCategoryId,
            x.OrgNodeId, x.OrgNode?.Name,
            x.DueDate, x.ScheduledStartAt, x.ScheduledEndAt, x.EstimatedLabourHours,
            x.CreatedAt, x.ApprovedAt, x.PlannedAt, x.WorkStartedAt, x.CompletedAt, x.VerifiedAt, x.ClosedAt,
            x.HoldReason, x.HoldNote,
            x.DowntimeStartedAt, x.DowntimeEndedAt, downtimeHours,
            x.WorkDoneNotes, x.VerificationNotes,
            x.FailureProblemCode is null ? null : MapCode(x.FailureProblemCode),
            x.FailureCauseCode is null ? null : MapCode(x.FailureCauseCode),
            x.FailureRemedyCode is null ? null : MapCode(x.FailureRemedyCode),
            x.SlaResponseDeadline, x.SlaResolutionDeadline,
            links.FirstOrDefault(l => l.Id == x.ParentWorkOrderId),
            links.FirstOrDefault(l => l.Id == x.ReworkOfWorkOrderId),
            children, team, labour, parts, signature,
            events.Where(e => e.EventType == "Handover").Select(e => new FactoryJobEvent(e.EventType, e.OccurredAt, e.Message, e.Note ?? (e.ActorUserId is { } a ? actors.GetValueOrDefault(a) : null), e.OldStatus, e.NewStatus)).ToList(),
            events.Where(e => e.EventType != "Handover").Select(e => new FactoryJobEvent(e.EventType, e.OccurredAt, e.Message, e.ActorUserId is { } a ? actors.GetValueOrDefault(a) : null, e.OldStatus, e.NewStatus)).ToList(),
            FactoryWorkOrderActions.AllowedFrom(WorkOrderStatuses.Normalize(x.Status)))
        {
            JobPlanId = plan?.Id,
            JobPlanName = plan?.Name,
            Steps = steps,
            Resources = plan?.Resources.OrderBy(r => r.Kind).ThenBy(r => r.Description).Select(r => new JobPlanResourceDto(r.Kind, r.Description, r.MaterialItemId, r.Quantity)).ToList() ?? [],
            ResponseBreached = x.SlaResponseBreached,
            RepairBreached = x.SlaResolutionBreached,
        };
    }

    private async Task ApplyCodeAsync(FailureCode code, UpsertFailureCodeRequest request, CancellationToken cancellationToken)
    {
        code.Kind = FailureCodeKinds.Find(request.Kind) ?? throw new BusinessRuleException("Kind must be Problem, Cause or Remedy.");
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)) throw new BusinessRuleException("Code and name are required.");
        var value = request.Code.Trim().ToUpperInvariant();
        if (await dbContext.FailureCodes.AnyAsync(x => x.TenantId == TenantId && x.Kind == code.Kind && x.Code == value && x.Id != code.Id, cancellationToken))
            throw new ConflictException($"{code.Kind} code {value} already exists.");
        code.Code = value;
        code.Name = request.Name.Trim();
        code.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        code.AssetCategoryId = request.AssetCategoryId;
        code.IsActive = request.IsActive ?? true;
    }

    private static FailureCodeResponse MapCode(FailureCode code) => new(code.Id, code.Kind, code.Code, code.Name, code.Description, code.AssetCategoryId, code.IsActive);
}

public sealed record FactoryWorkOrderSummary(
    Guid Id, string WorkOrderNumber, string Title, string WorkType, string Priority, string Status, bool IsBreakdown, bool NeedsLineStop,
    Guid? AssetId, string? AssetCode, string? AssetName, string? AssetCriticality,
    Guid? OrgNodeId, string? OrgNodeName, string? LeadTechnicianName,
    DateTime? DueDate, DateTime? RepairDueAt, DateTime CreatedAt, DateTime? CompletedAt, DateTime? DowntimeStartedAt, DateTime? DowntimeEndedAt,
    bool IsRework, string? HoldReason);

public sealed record FactoryJobTechnician(Guid TechnicianId, string Name, bool IsLead);
public sealed record FactoryLabourEntry(Guid Id, string? TechnicianName, DateTime StartedAt, int Minutes, string? Notes);
public sealed record FactoryPartUsed(Guid Id, Guid MaterialItemId, string PartName, decimal Quantity, string? Notes, DateTime UsedAt);
public sealed record FactoryJobLink(Guid Id, string WorkOrderNumber, string Title, string Status);
public sealed record FactorySignature(string SignerName, DateTime CapturedAt, string DataUrl);
public sealed record FactoryJobEvent(string Type, DateTime OccurredAt, string Message, string? By, string? FromStatus, string? ToStatus);
public sealed record FailureCodeResponse(Guid Id, string Kind, string Code, string Name, string? Description, Guid? AssetCategoryId, bool IsActive);

public sealed record FactoryWorkOrderDetail(
    Guid Id, string WorkOrderNumber, string Title, string? Description, string WorkType, string Priority, string Status,
    bool IsBreakdown, bool NeedsLineStop, string? RequestSource, string? RequestedByName,
    Guid? AssetId, string? AssetCode, string? AssetName, string? AssetCriticality, Guid? AssetCategoryId,
    Guid? OrgNodeId, string? OrgNodeName,
    DateTime? DueDate, DateTime? ScheduledStartAt, DateTime? ScheduledEndAt, decimal? EstimatedHours,
    DateTime CreatedAt, DateTime? ApprovedAt, DateTime? PlannedAt, DateTime? StartedAt, DateTime? CompletedAt, DateTime? VerifiedAt, DateTime? ClosedAt,
    string? HoldReason, string? HoldNote,
    DateTime? DowntimeStartedAt, DateTime? DowntimeEndedAt, decimal? DowntimeHours,
    string? WorkDone, string? VerificationNotes,
    FailureCodeResponse? Problem, FailureCodeResponse? Cause, FailureCodeResponse? Remedy,
    DateTime? ResponseDueAt, DateTime? RepairDueAt,
    FactoryJobLink? Parent, FactoryJobLink? ReworkOf,
    IReadOnlyCollection<FactoryJobLink> Children,
    IReadOnlyCollection<FactoryJobTechnician> Team,
    IReadOnlyCollection<FactoryLabourEntry> Labour,
    IReadOnlyCollection<FactoryPartUsed> Parts,
    FactorySignature? Signature,
    IReadOnlyCollection<FactoryJobEvent> Handover,
    IReadOnlyCollection<FactoryJobEvent> History,
    IReadOnlyCollection<string> AllowedActions)
{
    public Guid? JobPlanId { get; init; }
    public string? JobPlanName { get; init; }
    public IReadOnlyCollection<FactoryJobStep> Steps { get; init; } = [];
    public IReadOnlyCollection<JobPlanResourceDto> Resources { get; init; } = [];
    public bool ResponseBreached { get; init; }
    public bool RepairBreached { get; init; }
}

public sealed record FactoryJobStep(Guid Id, string Instruction, string? Hint, bool Done, string? Remarks);
public sealed record StepUpdateRequest(bool Done, [StringLength(1000)] string? Remarks = null);

public sealed record CreateFactoryWorkOrderRequest(
    [StringLength(250)] string Title,
    Guid? AssetId = null,
    Guid? OrgNodeId = null,
    [StringLength(4000)] string? Description = null,
    string? WorkType = null,
    string? Priority = null,
    bool IsBreakdown = false,
    bool NeedsLineStop = false,
    DateTime? DueDate = null,
    DateTime? DowntimeStartedAt = null,
    decimal? EstimatedHours = null,
    [StringLength(200)] string? RequestedByName = null,
    Guid? ProblemCodeId = null,
    Guid? JobPlanId = null);

public sealed record UpdateFactoryWorkOrderRequest(
    [StringLength(250)] string Title, [StringLength(4000)] string? Description, string WorkType, string Priority,
    DateTime? DueDate, bool NeedsLineStop, decimal? EstimatedHours);

public sealed record FactoryActionRequest(
    [StringLength(2000)] string? Reason = null,
    string? HoldReason = null,
    IReadOnlyCollection<Guid>? TechnicianIds = null,
    Guid? LeadTechnicianId = null,
    DateTime? ScheduledStartAt = null,
    DateTime? ScheduledEndAt = null,
    decimal? EstimatedHours = null,
    [StringLength(4000)] string? WorkDone = null,
    Guid? ProblemCodeId = null,
    Guid? CauseCodeId = null,
    Guid? RemedyCodeId = null,
    bool? AssetBackInService = null,
    DateTime? DowntimeEndedAt = null,
    string? SignatureDataUrl = null,
    [StringLength(200)] string? SignerName = null);

public sealed record CreateChildWorkOrderRequest(
    [StringLength(250)] string Title, [StringLength(4000)] string? Description = null, string? WorkType = null, string? Priority = null,
    Guid? AssetId = null, DateTime? DueDate = null, decimal? EstimatedHours = null);

public sealed record HandoverNoteRequest([StringLength(2000)] string Note);
public sealed record LabourEntryRequest(int Minutes, DateTime? StartedAt = null, Guid? TechnicianId = null, [StringLength(1000)] string? Notes = null);
public sealed record PartUsedRequest(Guid MaterialItemId, decimal Quantity, [StringLength(1000)] string? Notes = null);
public sealed record UpsertFailureCodeRequest(string Kind, [StringLength(30)] string Code, [StringLength(150)] string Name, [StringLength(500)] string? Description = null, Guid? AssetCategoryId = null, bool? IsActive = null);
