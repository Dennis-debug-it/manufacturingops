using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Ecosys.Api.Common;
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
[Route("api/workorders")]
public sealed class WorkOrdersController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IDocumentNumberingService documentNumberingService,
    IAuditLogService auditLogService,
    IUserAccessService userAccessService,
    IBranchAccessService branchAccessService,
    ILicenseGuardService licenseGuardService,
    IWorkOrderLifecycleService workOrderLifecycleService,
    IWorkOrderAssignmentWorkflowService assignmentWorkflowService,
    IPmWorkOrderChecklistService pmWorkOrderChecklistService,
    ISlaService slaService,
    IWorkflowService workflowService,
    IContractOperationalService? contractOperationalService = null) : TenantAwareControllerBase(tenantContext)
{
    /// <summary>Minutes-to-deadline below which the list treats a work order as "at risk".
    /// Must stay in step with the client-side <c>resolveSlaStatusKey</c> threshold.</summary>
    private const int SlaAtRiskWindowMinutes = 120;

    /// <summary>
    /// <c>view=current</c> on the list: everything still in play, plus jobs closed, cancelled or
    /// rejected within this many days. Older finished jobs stay reachable by search, a status or
    /// date filter, or from the client, site, asset or technician they belong to.
    /// </summary>
    public const int RecentlyFinishedDays = 30;

    private static readonly HashSet<string> EditableStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        WorkOrderStatuses.New,
        WorkOrderStatuses.Open,
        WorkOrderStatuses.Assigned,
        WorkOrderStatuses.Scheduled,
        WorkOrderStatuses.InProgress,
        WorkOrderStatuses.OnHold,
        WorkOrderStatuses.Reopened,
        WorkOrderStatuses.Cancelled
    };

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? branchId,
        [FromQuery] Guid? clientId,
        [FromQuery] Guid? siteId,
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] string? workType,
        [FromQuery] Guid? assignmentGroupId,
        [FromQuery] Guid? technicianId,
        [FromQuery] bool? unassigned,
        [FromQuery] string? assignmentStatus,
        [FromQuery] string? slaStatus,
        [FromQuery] string? search,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken,
        [FromQuery] bool? followUp = null,
        [FromQuery] string? state = null,
        [FromQuery] string? view = null)
    {
        EnsureGeneralWorkspaceAccess(PermissionNames.CanViewWorkOrders);
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, branchId, cancellationToken);

        // Translatable filters run in SQL. Status is expanded to the set of stored values that
        // normalize to the requested status (RawStatusesMatching), so legacy/synonym rows still
        // match while the filter stays a DB-level IN-list.
        var query = QueryWorkOrders(scope);

        if (clientId.HasValue)
        {
            query = query.Where(x => x.ClientId == clientId.Value);
        }

        if (siteId.HasValue)
        {
            query = query.Where(x => x.SiteId == siteId.Value);
        }

        if (assignmentGroupId.HasValue)
        {
            query = query.Where(x => x.AssignmentGroupId == assignmentGroupId);
        }

        if (technicianId.HasValue)
        {
            query = query.Where(x => x.TechnicianAssignments.Any(assignment => assignment.TechnicianId == technicianId));
        }

        if (unassigned.HasValue)
        {
            query = unassigned.Value
                ? query.Where(x => x.AssignmentGroupId == null && !x.TechnicianAssignments.Any())
                : query.Where(x => x.AssignmentGroupId != null || x.TechnicianAssignments.Any());
        }

        if (followUp.HasValue)
        {
            query = followUp.Value
                ? query.Where(x => x.FollowUpOfWorkOrderId != null)
                : query.Where(x => x.FollowUpOfWorkOrderId == null);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(x => x.CreatedAt <= toDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var matchingStatuses = WorkOrderStatuses.RawStatusesMatching(status);
            query = query.Where(x => matchingStatuses.Contains(x.Status));
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            // Priority, unlike status, is a closed enum with no legacy synonyms: every writer stores
            // WorkOrderPriorities.Normalize(...) output, so a single equality comparison is enough
            // and no RawStatusesMatching-style expansion is needed. Normalizing the filter value
            // also rejects garbage input with the same message the create/update endpoints use.
            var normalizedPriority = WorkOrderPriorities.Normalize(priority);
            query = query.Where(x => x.Priority == normalizedPriority);
        }

        if (!string.IsNullOrWhiteSpace(slaStatus))
        {
            query = ApplySlaStatusFilter(query, slaStatus, DateTime.UtcNow);
        }

        if (!string.IsNullOrWhiteSpace(state))
        {
            // open | overdue: what the dashboards count, so their figures link to exactly these rows.
            query = WorkOrderListStates.Apply(query, state, DateTime.UtcNow);
        }

        if (string.Equals(view, "current", StringComparison.OrdinalIgnoreCase))
        {
            query = ApplyCurrentView(query, DateTime.UtcNow);
        }

        var ordered = query.OrderByDescending(x => x.CreatedAt);

        // branchId/clientId/siteId/status/priority/slaStatus/technicianId/assignmentGroupId/
        // unassigned/fromDate/toDate are all filtered in SQL above, so the list the UI actually
        // requests pages at the database.
        //
        // workType/assignmentStatus/search remain derived in C# (type normalization, the
        // assignment-status precedence, and the assignment-summary component of search aren't
        // SQL-translatable), so any request using one of those three still materializes the
        // tenant's work orders before filtering and paging. Paging is still correct in that case —
        // it just isn't cheap.
        var needsInMemoryFilter = !string.IsNullOrWhiteSpace(workType)
            || !string.IsNullOrWhiteSpace(assignmentStatus)
            || !string.IsNullOrWhiteSpace(search);

        if (!needsInMemoryFilter)
        {
            if (QueryablePagingExtensions.IsPagingRequested(page, pageSize))
            {
                var pagedWorkOrders = await ordered.ToPagedResultAsync(page, pageSize, cancellationToken);
                return Ok(pagedWorkOrders.Map(Map));
            }

            var all = await ordered.ToListAsync(cancellationToken);
            return Ok(all.Select(Map).ToList());
        }

        var workOrders = await ordered.ToListAsync(cancellationToken);

        var filtered = workOrders
            .Where(x => string.IsNullOrWhiteSpace(workType) || string.Equals(WorkOrderTypes.Normalize(x.WorkType, WorkOrderTypes.CorrectiveMaintenance), WorkOrderTypes.Normalize(workType, WorkOrderTypes.CorrectiveMaintenance), StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(assignmentStatus) || string.Equals(WorkOrderAssignmentView.Resolve(x).AssignmentStatus, assignmentStatus, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(search) || $"{x.WorkOrderNumber} {x.Title} {x.Client?.ClientName} {x.Asset?.AssetName} {WorkOrderAssignmentView.Resolve(x).AssignmentSummary}".Contains(search, StringComparison.OrdinalIgnoreCase))
            .Select(Map)
            .ToList();

        if (QueryablePagingExtensions.IsPagingRequested(page, pageSize))
        {
            return Ok(filtered.ToPagedResult(page, pageSize));
        }

        return Ok(filtered);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkOrderResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        EnsureGeneralWorkspaceAccess(PermissionNames.CanViewWorkOrders);
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);

        var workOrder = await QueryWorkOrders(scope)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");

        return Ok(Map(workOrder));
    }

    [HttpGet("{id:guid}/checklist")]
    public async Task<ActionResult<IReadOnlyCollection<WorkOrderChecklistItemResponse>>> GetChecklist(Guid id, CancellationToken cancellationToken)
    {
        EnsureGeneralWorkspaceAccess(PermissionNames.CanViewWorkOrders);
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);

        var workOrder = await QueryWorkOrders(scope)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");

        return Ok(Map(workOrder).ChecklistItems ?? []);
    }

    [HttpGet("{id:guid}/sla")]
    public async Task<ActionResult<WorkOrderSlaStatusResponse>> GetSlaStatus(Guid id, CancellationToken cancellationToken)
    {
        EnsureGeneralWorkspaceAccess(PermissionNames.CanViewWorkOrders);
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);

        var workOrder = await QueryWorkOrders(scope)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");

        var record = await dbContext.SlaRecords
            .Include(x => x.SlaRule)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.WorkOrderId == id, cancellationToken);

        var now = DateTime.UtcNow;

        return Ok(new WorkOrderSlaStatusResponse(
            workOrder.Id,
            workOrder.Priority,
            record?.SlaRule?.Name ?? record?.SlaRule?.Priority,
            workOrder.SlaResponseDeadline,
            workOrder.SlaResolutionDeadline,
            workOrder.SlaResponseBreached,
            workOrder.SlaResolutionBreached,
            workOrder.SlaResponseBreachedAt,
            workOrder.SlaResolutionBreachedAt,
            MinutesRemaining(workOrder.SlaResponseDeadline, now),
            MinutesRemaining(workOrder.SlaResolutionDeadline, now),
            ResolveSlaTimelineStatus(workOrder, now),
            record?.IsPaused ?? false,
            record?.PauseReason));
    }

    [HttpPost("{id:guid}/attach-pm-template")]
    public async Task<ActionResult<WorkOrderResponse>> AttachPmTemplate(Guid id, [FromBody] AttachPmTemplateRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanCreateWorkOrders);

        if (!request.PmTemplateId.HasValue)
        {
            throw new BusinessRuleException("Checklist template is required.");
        }

        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var workOrder = await dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId && x.Id == id)
            .WhereAccessible(scope, x => x.BranchId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");

        await EnsurePmTemplateIsValidAsync(
            WorkOrderTypes.Normalize(workOrder.WorkType, WorkOrderTypes.CorrectiveMaintenance),
            request.PmTemplateId,
            cancellationToken);

        await pmWorkOrderChecklistService.AttachPmTemplateToWorkOrderAsync(id, request.PmTemplateId.Value, TenantId, UserId, cancellationToken: cancellationToken);

        var persisted = await QueryWorkOrders(await branchAccessService.GetQueryScopeAsync(TenantId, workOrder.BranchId, cancellationToken))
            .SingleAsync(x => x.Id == id, cancellationToken);
        return Ok(Map(persisted));
    }

    [HttpPut("{workOrderId:guid}/checklist/{itemId:guid}")]
    public async Task<ActionResult<WorkOrderChecklistItemResponse>> UpdateChecklistItem(
        Guid workOrderId,
        Guid itemId,
        [FromBody] UpdateWorkOrderChecklistItemRequest request,
        CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        await EnsureAssignedExecutionAccessAsync(workOrderId, cancellationToken);
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var workOrderExists = await dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId && x.Id == workOrderId)
            .WhereAccessible(scope, x => x.BranchId)
            .AnyAsync(cancellationToken);

        if (!workOrderExists)
        {
            throw new NotFoundException("Work order was not found.");
        }

        var item = await pmWorkOrderChecklistService.UpdateChecklistItemAsync(
            workOrderId,
            itemId,
            TenantId,
            UserId,
            request.ResponseValue,
            request.Remarks,
            request.IsCompleted,
            cancellationToken);

        return Ok(new WorkOrderChecklistItemResponse(
            item.Id,
            item.PmTemplateQuestionId,
            item.SectionName,
            item.QuestionText,
            item.InputType,
            item.IsRequired,
            item.SortOrder,
            item.ResponseValue,
            item.Remarks,
            item.IsCompleted,
            item.CompletedByUserId,
            item.CompletedAt,
            ParseOptions(item.OptionsJson),
            item.SectionOrder,
            item.HelperText,
            item.Unit,
            item.MinValue,
            item.MaxValue,
            item.DefaultValue,
            item.EvidenceRequired,
            item.AttachmentId,
            item.TechnicianRemarksAllowed,
            item.RequiresNoteOnFail));
    }

    [HttpPost]
    public async Task<ActionResult<WorkOrderResponse>> Create([FromBody] CreateWorkOrderRequest rawRequest, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanCreateWorkOrders);
        await licenseGuardService.EnsureTenantCanMutateAsync(TenantId, cancellationToken);

        var template = await ResolveWorkOrderTemplateAsync(rawRequest.TemplateId, cancellationToken);
        var request = ApplyTemplateDefaults(rawRequest, template);
        var parentWorkOrder = await EnsureParentWorkOrderExistsAsync(request.ParentWorkOrderId, cancellationToken);
        var followUpOf = await EnsureFollowUpSourceAsync(request.FollowUpOfWorkOrderId, request.ClientId, cancellationToken);

        ValidateCreate(request);
        var normalizedWorkType = WorkOrderTypes.Normalize(request.WorkType);
        await EnsureClientExistsAsync(request.ClientId, cancellationToken);
        var asset = await EnsureAssetExistsAsync(request.AssetId, request.ClientId, cancellationToken);
        await EnsureSystemExistsAsync(request.SystemId, request.ClientId, request.SiteId, cancellationToken);
        await EnsurePmTemplateIsValidAsync(normalizedWorkType, request.PmTemplateId, cancellationToken);

        var branchId = await ResolveWorkOrderBranchIdAsync(request.BranchId, asset?.BranchId, cancellationToken);

        var normalizedAssignmentType = NormalizeAssignmentType(request.AssignmentType, request.AssignmentGroupId, request.AssignedTechnicianId, request.AssignedTechnicianIds);
        var repeatVisitCount = request.AssetId.HasValue
            ? await dbContext.WorkOrders
                .Where(x => x.TenantId == TenantId
                    && x.AssetId == request.AssetId.Value
                    && x.CompletedAt != null
                    && x.CompletedAt >= DateTime.UtcNow.AddDays(-30))
                .CountAsync(cancellationToken)
            : 0;

        var workOrder = new WorkOrder
        {
            TenantId = TenantId,
            BranchId = branchId,
            ClientId = request.ClientId,
            SiteId = request.SiteId,
            SystemId = request.SystemId,
            AssetId = request.AssetId,
            IsRepeatVisit = repeatVisitCount >= 2,
            RepeatVisitCount = repeatVisitCount,
            AssignmentGroupId = normalizedAssignmentType == WorkOrderAssignmentTypes.AssignmentGroup ? request.AssignmentGroupId : null,
            AssignmentType = normalizedAssignmentType,
            WorkOrderNumber = await documentNumberingService.GenerateAsync(TenantId, branchId, DocumentTypes.WorkOrder, cancellationToken),
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Priority = WorkOrderPriorities.Normalize(request.Priority),
            Status = WorkOrderStatuses.New,
            WorkType = normalizedWorkType,
            DueDate = DateTimeNormalizationHelper.NormalizeNullableToUtc(request.DueDate),
            AssignedTechnicianId = ResolvePrimaryTechnicianId(request),
            LeadTechnicianId = request.LeadTechnicianId,
            AssignedTechnicianIdsJson = null,
            IsPreventiveMaintenance = WorkOrderTypes.IsPreventiveMaintenanceType(normalizedWorkType),
            PmTemplateId = request.PmTemplateId,
            NotifyClient = request.NotifyClient,
            ClientContactEmail = request.ClientContactEmail?.Trim(),
            TrackingToken = TrackingTokenGenerator.Generate(),
            WorkOrderTemplateId = template?.Id,
            ParentWorkOrderId = parentWorkOrder?.Id,
            FollowUpOfWorkOrderId = followUpOf?.Id,
            EstimatedDuration = template?.EstimatedDurationMins.HasValue == true ? Math.Round(template.EstimatedDurationMins.Value / 60m, 2) : null,
            PlannedMaterialsJson = template is not null ? await BuildPlannedMaterialsSnapshotAsync(template, cancellationToken) : null
        };

        if (contractOperationalService is not null)
        {
            await contractOperationalService.ApplyOperationalSnapshotAsync(workOrder, cancellationToken);
        }
        else
        {
            await slaService.ApplyDefinitionAsync(workOrder, cancellationToken);
        }

        await workflowService.EnsureDefaultWorkflowAsync(TenantId, cancellationToken);
        var activeWorkflow = await workflowService.GetActiveWorkflowAsync(TenantId, cancellationToken);
        var initialWorkflowStatus = activeWorkflow?.Statuses.FirstOrDefault(x => x.IsInitial);
        if (initialWorkflowStatus is not null)
        {
            workOrder.WorkflowStatusId = initialWorkflowStatus.Id;
        }

        dbContext.WorkOrders.Add(workOrder);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (request.PmTemplateId.HasValue)
        {
            await pmWorkOrderChecklistService.AttachPmTemplateToWorkOrderAsync(workOrder.Id, request.PmTemplateId.Value, TenantId, UserId, cancellationToken: cancellationToken);
        }

        await workOrderLifecycleService.RecordCreatedAsync(TenantId, workOrder.Id, UserId, workOrder.WorkOrderNumber, cancellationToken);

        var initialTechnicianIds = ResolveTechnicianIds(request.AssignedTechnicianId, request.AssignedTechnicianIds, normalizedAssignmentType);
        if (workOrder.AssignedTechnicianId.HasValue || workOrder.AssignmentGroupId.HasValue || initialTechnicianIds.Count > 0)
        {
            await assignmentWorkflowService.ApplyAssignmentsAsync(
                TenantId,
                workOrder.Id,
                UserId,
                new WorkOrderAssignmentUpdateRequest(workOrder.AssignmentGroupId, initialTechnicianIds, request.LeadTechnicianId, request.AssignmentNotes),
                false,
                cancellationToken);
        }

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Work order created",
            nameof(WorkOrder),
            workOrder.Id.ToString(),
            followUpOf is null
                ? $"Created work order '{workOrder.WorkOrderNumber}'."
                : $"Created work order '{workOrder.WorkOrderNumber}' as a follow-up of '{followUpOf.WorkOrderNumber}'.",
            cancellationToken);

        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, workOrder.BranchId, cancellationToken);
        var persisted = await QueryWorkOrders(scope).SingleAsync(x => x.Id == workOrder.Id, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = workOrder.Id }, Map(persisted));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WorkOrderResponse>> Update(Guid id, [FromBody] UpdateWorkOrderRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanCreateWorkOrders);

        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var workOrder = await dbContext.WorkOrders
            .Include(x => x.ChecklistItems)
            .Where(x => x.TenantId == TenantId && x.Id == id)
            .WhereAccessible(scope, x => x.BranchId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");

        var normalizedWorkType = WorkOrderTypes.Normalize(request.WorkType, workOrder.WorkType);
        ValidateCreate(new CreateWorkOrderRequest(request.ClientId, request.BranchId, request.SiteId, request.SystemId, request.AssetId, request.AssignmentGroupId, request.AssignmentType, request.AssignedTechnicianId, request.AssignedTechnicianIds, request.LeadTechnicianId, request.AssignmentNotes, request.Title, request.Description, request.Priority, request.DueDate, normalizedWorkType, WorkOrderTypes.IsPreventiveMaintenanceType(normalizedWorkType), request.PmTemplateId));
        await EnsureClientExistsAsync(request.ClientId, cancellationToken);
        var asset = await EnsureAssetExistsAsync(request.AssetId, request.ClientId, cancellationToken);
        await EnsureSystemExistsAsync(request.SystemId, request.ClientId, request.SiteId, cancellationToken);
        await EnsurePmTemplateIsValidAsync(normalizedWorkType, request.PmTemplateId, cancellationToken);
        var branchId = await ResolveWorkOrderBranchIdAsync(request.BranchId ?? workOrder.BranchId, asset?.BranchId, cancellationToken);
        var previousTemplateId = workOrder.PmTemplateId;

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var normalizedStatus = NormalizeStatus(request.Status);
            if (!EditableStatuses.Contains(normalizedStatus))
            {
                throw new BusinessRuleException("Use the dedicated complete or acknowledge endpoints for those status changes.");
            }
            await workOrderLifecycleService.ChangeStatusAsync(
                TenantId,
                workOrder.Id,
                UserId,
                new Infrastructure.Services.WorkOrderStatusChangeRequest(normalizedStatus),
                cancellationToken);
        }

        workOrder.BranchId = branchId;
        workOrder.ClientId = request.ClientId;
        workOrder.SiteId = request.SiteId;
        workOrder.SystemId = request.SystemId;
        workOrder.AssetId = request.AssetId;
        var normalizedUpdateAssignmentType = NormalizeAssignmentType(request.AssignmentType, request.AssignmentGroupId, request.AssignedTechnicianId, request.AssignedTechnicianIds);
        workOrder.AssignmentType = normalizedUpdateAssignmentType;
        workOrder.AssignmentGroupId = workOrder.AssignmentType == WorkOrderAssignmentTypes.AssignmentGroup ? request.AssignmentGroupId : null;
        workOrder.AssignedTechnicianId = ResolvePrimaryTechnicianId(request);
        workOrder.LeadTechnicianId = request.LeadTechnicianId;
        workOrder.AssignedTechnicianIdsJson = null;
        workOrder.Title = request.Title.Trim();
        workOrder.Description = request.Description?.Trim();
        workOrder.Priority = WorkOrderPriorities.Normalize(request.Priority);
        workOrder.WorkType = normalizedWorkType;
        workOrder.IsPreventiveMaintenance = WorkOrderTypes.IsPreventiveMaintenanceType(normalizedWorkType);
        workOrder.DueDate = DateTimeNormalizationHelper.NormalizeNullableToUtc(request.DueDate);
        workOrder.PmTemplateId = request.PmTemplateId;
        workOrder.NotifyClient = request.NotifyClient;
        workOrder.ClientContactEmail = request.ClientContactEmail?.Trim();
        if (contractOperationalService is not null
            && string.IsNullOrWhiteSpace(workOrder.ContractSnapshotJson)
            && string.IsNullOrWhiteSpace(workOrder.SlaSnapshotJson)
            && string.IsNullOrWhiteSpace(workOrder.PricingSnapshotJson))
        {
            await contractOperationalService.ApplyOperationalSnapshotAsync(workOrder, cancellationToken);
        }
        else if (contractOperationalService is null)
        {
            await slaService.ApplyDefinitionAsync(workOrder, cancellationToken);
        }

        if (request.PmTemplateId != previousTemplateId && workOrder.ChecklistItems.Any(item => item.IsCompleted || !string.IsNullOrWhiteSpace(item.ResponseValue) || item.AttachmentId.HasValue))
        {
            throw new BusinessRuleException("Checklist template cannot be changed after responses or evidence have been captured.");
        }

        await assignmentWorkflowService.ApplyAssignmentsAsync(
            TenantId,
            workOrder.Id,
            UserId,
            new WorkOrderAssignmentUpdateRequest(
                workOrder.AssignmentType == WorkOrderAssignmentTypes.AssignmentGroup ? request.AssignmentGroupId : request.AssignmentGroupId,
                ResolveTechnicianIds(request.AssignedTechnicianId, request.AssignedTechnicianIds, normalizedUpdateAssignmentType),
                request.LeadTechnicianId,
                request.AssignmentNotes),
            true,
            cancellationToken);

        if (request.PmTemplateId != previousTemplateId)
        {
            if (request.PmTemplateId.HasValue)
            {
                await pmWorkOrderChecklistService.AttachPmTemplateToWorkOrderAsync(workOrder.Id, request.PmTemplateId.Value, TenantId, UserId, replaceExisting: true, cancellationToken: cancellationToken);
            }
            else if (workOrder.ChecklistItems.Count > 0)
            {
                dbContext.WorkOrderChecklistItems.RemoveRange(workOrder.ChecklistItems);
                workOrder.PmTemplateNameSnapshot = null;
                workOrder.PmTemplateVersionSnapshot = null;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var persisted = await QueryWorkOrders(await branchAccessService.GetQueryScopeAsync(TenantId, workOrder.BranchId, cancellationToken))
            .SingleAsync(x => x.Id == workOrder.Id, cancellationToken);
        return Ok(Map(persisted));
    }

    [HttpGet("workflow-statuses")]
    public async Task<ActionResult<IReadOnlyCollection<WorkflowStatusResponse>>> GetWorkflowStatuses(CancellationToken cancellationToken)
    {
        EnsureGeneralWorkspaceAccess(PermissionNames.CanViewWorkOrders);
        await workflowService.EnsureDefaultWorkflowAsync(TenantId, cancellationToken);

        var statuses = await workflowService.GetActiveWorkflowStatusesAsync(TenantId, cancellationToken);
        return Ok(statuses.Select(status => new WorkflowStatusResponse(
            status.Id,
            status.WorkflowDefinitionId,
            status.Name,
            status.Color,
            status.SortOrder,
            status.IsInitial,
            status.IsFinal,
            WorkflowService.ParseAllowedNextStatusIds(status.AllowedNextStatusIdsJson).ToList())).ToList());
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<WorkOrderResponse>> PatchStatus(Guid id, [FromBody] PatchWorkOrderStatusRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanCreateWorkOrders);

        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var workOrder = await dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId && x.Id == id)
            .WhereAccessible(scope, x => x.BranchId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");

        if (request.WorkflowStatusId.HasValue)
        {
            await workflowService.ApplyStatusTransitionAsync(workOrder, request.WorkflowStatusId.Value, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            await auditLogService.LogAsync(
                TenantId,
                UserId,
                "Work order status changed",
                nameof(WorkOrder),
                workOrder.Id.ToString(),
                $"Status changed to '{workOrder.Status}' via workflow.",
                cancellationToken);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Status))
            {
                throw new BusinessRuleException("Status is required.");
            }

            var normalizedStatus = NormalizeStatus(request.Status);
            if (!EditableStatuses.Contains(normalizedStatus))
            {
                throw new BusinessRuleException("Use the dedicated complete, close, or cancel actions for that status change.");
            }

            await workOrderLifecycleService.ChangeStatusAsync(
                TenantId,
                workOrder.Id,
                UserId,
                new Infrastructure.Services.WorkOrderStatusChangeRequest(normalizedStatus),
                cancellationToken);
        }

        var persisted = await QueryWorkOrders(await branchAccessService.GetQueryScopeAsync(TenantId, workOrder.BranchId, cancellationToken))
            .SingleAsync(x => x.Id == workOrder.Id, cancellationToken);
        return Ok(Map(persisted));
    }

    [HttpPost("bulk-assign")]
    public async Task<ActionResult<BulkOperationResultResponse>> BulkAssign([FromBody] BulkAssignWorkOrdersRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanAssignWorkOrders);

        if (request.WorkOrderIds is null || request.WorkOrderIds.Count == 0)
        {
            throw new BusinessRuleException("Select at least one work order.");
        }

        if (!request.TechnicianId.HasValue)
        {
            throw new BusinessRuleException("Select a technician.");
        }

        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var workOrders = await dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId && request.WorkOrderIds.Contains(x.Id))
            .WhereAccessible(scope, x => x.BranchId)
            .ToListAsync(cancellationToken);

        var succeeded = new List<Guid>();
        var failed = new List<BulkOperationFailure>();

        foreach (var workOrder in workOrders)
        {
            try
            {
                await assignmentWorkflowService.ApplyAssignmentsAsync(
                    TenantId,
                    workOrder.Id,
                    UserId,
                    new WorkOrderAssignmentUpdateRequest(null, [request.TechnicianId.Value], request.TechnicianId, request.Notes),
                    true,
                    cancellationToken);
                succeeded.Add(workOrder.Id);
            }
            catch (BusinessRuleException ex)
            {
                failed.Add(new BulkOperationFailure(workOrder.Id, ex.Message));
            }
        }

        var foundIds = workOrders.Select(x => x.Id).ToHashSet();
        foreach (var missingId in request.WorkOrderIds.Where(x => !foundIds.Contains(x)))
        {
            failed.Add(new BulkOperationFailure(missingId, "Work order was not found."));
        }

        if (succeeded.Count > 0)
        {
            await auditLogService.LogAsync(
                TenantId,
                UserId,
                "Bulk work order assignment",
                nameof(WorkOrder),
                string.Join(",", succeeded),
                $"Bulk-assigned {succeeded.Count} work order(s) to technician.",
                cancellationToken);
        }

        return Ok(new BulkOperationResultResponse(succeeded, failed));
    }

    [HttpPost("bulk-close")]
    public async Task<ActionResult<BulkOperationResultResponse>> BulkClose([FromBody] BulkCloseWorkOrdersRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanCompleteWorkOrders);

        if (request.WorkOrderIds is null || request.WorkOrderIds.Count == 0)
        {
            throw new BusinessRuleException("Select at least one work order.");
        }

        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var workOrders = await dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId && request.WorkOrderIds.Contains(x.Id))
            .WhereAccessible(scope, x => x.BranchId)
            .ToListAsync(cancellationToken);

        var succeeded = new List<Guid>();
        var failed = new List<BulkOperationFailure>();

        foreach (var workOrder in workOrders)
        {
            try
            {
                await workOrderLifecycleService.ChangeStatusAsync(
                    TenantId,
                    workOrder.Id,
                    UserId,
                    new Infrastructure.Services.WorkOrderStatusChangeRequest(WorkOrderStatuses.Closed),
                    cancellationToken);
                succeeded.Add(workOrder.Id);
            }
            catch (BusinessRuleException ex)
            {
                failed.Add(new BulkOperationFailure(workOrder.Id, ex.Message));
            }
        }

        var foundIds = workOrders.Select(x => x.Id).ToHashSet();
        foreach (var missingId in request.WorkOrderIds.Where(x => !foundIds.Contains(x)))
        {
            failed.Add(new BulkOperationFailure(missingId, "Work order was not found."));
        }

        if (succeeded.Count > 0)
        {
            await auditLogService.LogAsync(
                TenantId,
                UserId,
                "Bulk work order close",
                nameof(WorkOrder),
                string.Join(",", succeeded),
                $"Bulk-closed {succeeded.Count} work order(s).",
                cancellationToken);
        }

        return Ok(new BulkOperationResultResponse(succeeded, failed));
    }

    [HttpPost("{id:guid}/assign")]
    public async Task<ActionResult<WorkOrderResponse>> Assign(Guid id, [FromBody] AssignWorkOrderRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanAssignWorkOrders);
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);

        var workOrder = await dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId && x.Id == id)
            .WhereAccessible(scope, x => x.BranchId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");

        // A payload naming neither a technician nor a group used to reach ApplyAssignmentsAsync as a
        // no-op, return 200, and still write an audit entry saying the work order had been assigned.
        // Refuse it here so nothing is assigned and nothing is recorded.
        if (!request.TechnicianId.HasValue && !request.AssignmentGroupId.HasValue)
        {
            throw new BusinessRuleException("Assign a technician or an assignment group.");
        }

        await assignmentWorkflowService.ApplyAssignmentsAsync(
            TenantId,
            workOrder.Id,
            UserId,
            new WorkOrderAssignmentUpdateRequest(
                request.AssignmentGroupId,
                request.TechnicianId.HasValue ? [request.TechnicianId.Value] : [],
                request.TechnicianId,
                request.Notes),
            true,
            cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Work order assigned",
            nameof(WorkOrder),
            workOrder.Id.ToString(),
            $"Assigned work order '{workOrder.WorkOrderNumber}'.",
            cancellationToken);

        var persisted = await QueryWorkOrders(await branchAccessService.GetQueryScopeAsync(TenantId, workOrder.BranchId, cancellationToken))
            .SingleAsync(x => x.Id == workOrder.Id, cancellationToken);
        return Ok(Map(persisted));
    }

    [HttpPost("{id:guid}/start")]
    public async Task<ActionResult<WorkOrderResponse>> Start(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanCompleteWorkOrders);
        await EnsureAssignedExecutionAccessAsync(id, cancellationToken);
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var workOrder = await dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId && x.Id == id)
            .WhereAccessible(scope, x => x.BranchId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");

        if (!await assignmentWorkflowService.HasAcceptedTechnicianAsync(TenantId, workOrder.Id, cancellationToken))
        {
            throw new BusinessRuleException("Work order cannot be moved to In Progress before at least one technician accepts.");
        }

        await workOrderLifecycleService.StartAsync(TenantId, workOrder.Id, UserId, cancellationToken);
        var persisted = await QueryWorkOrders(await branchAccessService.GetQueryScopeAsync(TenantId, workOrder.BranchId, cancellationToken))
            .SingleAsync(x => x.Id == workOrder.Id, cancellationToken);
        return Ok(Map(persisted));
    }

    [HttpPost("{id:guid}/arrive")]
    public async Task<ActionResult<WorkOrderResponse>> Arrive(Guid id, [FromBody] WorkOrderLocationRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        await EnsureAssignedExecutionAccessAsync(id, cancellationToken);
        var workOrder = await dbContext.WorkOrders.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");

        // Delegates to the assignment-level recorder rather than WorkOrderLifecycleService.ArriveAsync.
        // That wrote work_orders.ArrivalAt only, which never satisfied the completion guard - the guard
        // reads work_order_technician_assignments.ArrivalAt - so this endpoint returned 200 while
        // leaving Complete blocked. RecordArrivalAsync writes the assignment row and rolls the header
        // up from it, keeping one source of truth.
        var arrivalTechnicianId = await ResolveDispatchTechnicianIdAsync(workOrder.Id, "Arrival", cancellationToken);
        await assignmentWorkflowService.RecordArrivalAsync(
            TenantId,
            workOrder.Id,
            UserId,
            new Infrastructure.Services.TechnicianArrivalRequest(arrivalTechnicianId, request.Latitude, request.Longitude, null, null),
            cancellationToken);
        var persisted = await QueryWorkOrders(await branchAccessService.GetQueryScopeAsync(TenantId, workOrder.BranchId, cancellationToken))
            .SingleAsync(x => x.Id == workOrder.Id, cancellationToken);
        return Ok(Map(persisted));
    }

    [HttpPost("{id:guid}/depart")]
    public async Task<ActionResult<WorkOrderResponse>> Depart(Guid id, [FromBody] WorkOrderLocationRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        await EnsureAssignedExecutionAccessAsync(id, cancellationToken);
        var workOrder = await dbContext.WorkOrders.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");

        // Same reconciliation as /arrive above: the assignment row is the source of truth and
        // RecordDepartureAsync rolls the header up from it.
        var departureTechnicianId = await ResolveDispatchTechnicianIdAsync(workOrder.Id, "Departure", cancellationToken);
        await assignmentWorkflowService.RecordDepartureAsync(
            TenantId,
            workOrder.Id,
            UserId,
            new Infrastructure.Services.TechnicianDepartureRequest(departureTechnicianId, request.Latitude, request.Longitude, null, null),
            cancellationToken);
        var persisted = await QueryWorkOrders(await branchAccessService.GetQueryScopeAsync(TenantId, workOrder.BranchId, cancellationToken))
            .SingleAsync(x => x.Id == workOrder.Id, cancellationToken);
        return Ok(Map(persisted));
    }

    [HttpGet("{id:guid}/events")]
    public async Task<ActionResult<IReadOnlyCollection<WorkOrderEventResponse>>> GetEvents(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        await EnsureAssignedExecutionAccessAsync(id, cancellationToken);
        var workOrderExists = await dbContext.WorkOrders.AnyAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken);
        if (!workOrderExists)
        {
            throw new NotFoundException("Work order was not found.");
        }

        var events = await workOrderLifecycleService.GetEventsAsync(TenantId, id, cancellationToken);
        return Ok(events.Select(x => new WorkOrderEventResponse(
            x.Id,
            x.EventType,
            x.Status,
            x.Message,
            x.ActorUserId,
            x.ActorUser?.FullName,
            x.OldStatus,
            x.NewStatus,
            x.Reason,
            x.Note,
            x.Latitude,
            x.Longitude,
            x.OccurredAt,
            x.MetadataJson)).ToList());
    }

    [HttpGet("{id:guid}/notes")]
    public async Task<ActionResult<IReadOnlyCollection<WorkOrderNoteResponse>>> GetNotes(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        await EnsureAssignedExecutionAccessAsync(id, cancellationToken);
        var workOrderExists = await dbContext.WorkOrders.AnyAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken);
        if (!workOrderExists)
        {
            throw new NotFoundException("Work order was not found.");
        }

        var notes = await dbContext.WorkOrderNotes
            .Include(x => x.CreatedByUser)
            .Where(x => x.TenantId == TenantId && x.WorkOrderId == id)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(notes.Select(x => new WorkOrderNoteResponse(
            x.Id,
            x.WorkOrderId,
            x.Content,
            x.IsClientVisible,
            x.CreatedByUserId,
            x.CreatedByUser?.FullName,
            x.CreatedAt)).ToList());
    }

    [HttpPost("{id:guid}/notes")]
    public async Task<ActionResult<WorkOrderNoteResponse>> AddNote(Guid id, [FromBody] CreateWorkOrderNoteRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        await EnsureAssignedExecutionAccessAsync(id, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new BusinessRuleException("Note content is required.");
        }

        var workOrder = await dbContext.WorkOrders
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");

        var note = new WorkOrderNote
        {
            TenantId = TenantId,
            WorkOrderId = workOrder.Id,
            Content = request.Content.Trim(),
            IsClientVisible = request.IsClientVisible,
            CreatedByUserId = UserId
        };

        dbContext.WorkOrderNotes.Add(note);

        dbContext.WorkOrderEvents.Add(new WorkOrderEvent
        {
            TenantId = TenantId,
            WorkOrderId = workOrder.Id,
            ActorUserId = UserId,
            EventType = "NoteAdded",
            Status = NormalizeStatus(workOrder.Status),
            Message = request.IsClientVisible ? "Added a client-visible note." : "Added an internal note.",
            OccurredAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        var createdByName = await dbContext.Users
            .Where(x => x.Id == UserId)
            .Select(x => x.FullName)
            .SingleOrDefaultAsync(cancellationToken);

        return Ok(new WorkOrderNoteResponse(
            note.Id,
            note.WorkOrderId,
            note.Content,
            note.IsClientVisible,
            note.CreatedByUserId,
            createdByName,
            note.CreatedAt));
    }

    [HttpGet("{id:guid}/related")]
    public async Task<ActionResult<RelatedWorkOrdersResponse>> GetRelated(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var workOrder = await dbContext.WorkOrders
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");

        var sameSite = workOrder.SiteId.HasValue
            ? await dbContext.WorkOrders
                .Include(x => x.AssignedTechnician)
                .Where(x => x.TenantId == TenantId && x.Id != id && x.SiteId == workOrder.SiteId.Value)
                .OrderByDescending(x => x.CreatedAt)
                .Take(5)
                .Select(x => new RelatedWorkOrderResponse(
                    x.Id, x.WorkOrderNumber, x.Status, x.CompletedAt, x.Title, x.AssignedTechnician != null ? x.AssignedTechnician.FullName : null))
                .ToListAsync(cancellationToken)
            : new List<RelatedWorkOrderResponse>();

        var sameAsset = workOrder.AssetId.HasValue
            ? await dbContext.WorkOrders
                .Include(x => x.AssignedTechnician)
                .Where(x => x.TenantId == TenantId && x.Id != id && x.AssetId == workOrder.AssetId.Value)
                .OrderByDescending(x => x.CreatedAt)
                .Take(5)
                .Select(x => new RelatedWorkOrderResponse(
                    x.Id, x.WorkOrderNumber, x.Status, x.CompletedAt, x.Title, x.AssignedTechnician != null ? x.AssignedTechnician.FullName : null))
                .ToListAsync(cancellationToken)
            : new List<RelatedWorkOrderResponse>();

        return Ok(new RelatedWorkOrdersResponse(sameSite, sameAsset));
    }

    [HttpGet("{id:guid}/repeat-visit-check")]
    public async Task<ActionResult<RepeatVisitCheckResponse>> GetRepeatVisitCheck(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var workOrder = await dbContext.WorkOrders
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");

        return Ok(new RepeatVisitCheckResponse(workOrder.IsRepeatVisit, workOrder.RepeatVisitCount));
    }

    [HttpGet("repeat-visit-check")]
    public async Task<ActionResult<RepeatVisitCheckResponse>> GetRepeatVisitCheckForAsset([FromQuery] Guid assetId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();

        var repeatVisitCount = await dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId
                && x.AssetId == assetId
                && x.CompletedAt != null
                && x.CompletedAt >= DateTime.UtcNow.AddDays(-30))
            .CountAsync(cancellationToken);

        return Ok(new RepeatVisitCheckResponse(repeatVisitCount >= 2, repeatVisitCount));
    }

    [HttpGet("{id:guid}/documents")]
    public async Task<ActionResult<IReadOnlyCollection<WorkOrderDocumentResponse>>> GetDocuments(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var workOrder = await dbContext.WorkOrders
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");

        var documents = new List<WorkOrderDocumentResponse>();

        if (!string.IsNullOrWhiteSpace(workOrder.ReportNumber))
        {
            documents.Add(new WorkOrderDocumentResponse("ServiceReport", workOrder.Id, workOrder.ReportNumber, workOrder.ReportStatus ?? "Draft", $"/api/workorders/{workOrder.Id}/service-report/pdf"));
        }

        var materialRequests = await dbContext.MaterialRequests
            .Where(x => x.TenantId == TenantId && x.WorkOrderId == id)
            .Select(x => new { x.Id, x.RequestNumber, x.Status })
            .ToListAsync(cancellationToken);
        documents.AddRange(materialRequests.Select(x => new WorkOrderDocumentResponse("MaterialRequest", x.Id, x.RequestNumber, x.Status, null)));

        return Ok(documents);
    }

    [HttpPost("{id:guid}/comments")]
    public async Task<ActionResult<WorkOrderEventResponse>> AddComment(Guid id, [FromBody] AddWorkOrderCommentRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        await EnsureAssignedExecutionAccessAsync(id, cancellationToken);
        var workOrderEvent = await workOrderLifecycleService.AddCommentAsync(TenantId, id, UserId, new Infrastructure.Services.WorkOrderCommentRequest(request.Message), cancellationToken);
        return Ok(new WorkOrderEventResponse(
            workOrderEvent.Id,
            workOrderEvent.EventType,
            workOrderEvent.Status,
            workOrderEvent.Message,
            workOrderEvent.ActorUserId,
            workOrderEvent.ActorUser?.FullName,
            workOrderEvent.OldStatus,
            workOrderEvent.NewStatus,
            workOrderEvent.Reason,
            workOrderEvent.Note,
            workOrderEvent.Latitude,
            workOrderEvent.Longitude,
            workOrderEvent.OccurredAt));
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<WorkOrderResponse>> Complete(Guid id, [FromBody] CompleteWorkOrderRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanCompleteWorkOrders);
        await EnsureAssignedExecutionAccessAsync(id, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.WorkDoneNotes))
        {
            throw new BusinessRuleException("Work done notes are required.");
        }

        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var workOrder = await dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId && x.Id == id)
            .WhereAccessible(scope, x => x.BranchId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");

        if (request.TechnicianId.HasValue || request.AssignmentGroupId.HasValue)
        {
            await assignmentWorkflowService.ApplyAssignmentsAsync(
                TenantId,
                workOrder.Id,
                UserId,
                new WorkOrderAssignmentUpdateRequest(
                    request.AssignmentGroupId,
                    request.TechnicianId.HasValue ? [request.TechnicianId.Value] : [],
                    request.TechnicianId,
                    request.OverrideReason),
                true,
                cancellationToken);
        }

        if (!await assignmentWorkflowService.HasArrivalAsync(TenantId, workOrder.Id, cancellationToken))
        {
            if (!request.AdminOverride)
            {
                throw new BusinessRuleException("Completion cannot happen before arrival unless admin override is used.");
            }

            if (!IsAdmin)
            {
                throw new BusinessRuleException("Only admins can use completion override.");
            }

            if (string.IsNullOrWhiteSpace(request.OverrideReason))
            {
                throw new BusinessRuleException("Provide an override reason before completing without arrival.");
            }
        }

        if (workOrder.PmTemplateId.HasValue || await dbContext.WorkOrderChecklistItems.AnyAsync(x => x.TenantId == TenantId && x.WorkOrderId == workOrder.Id, cancellationToken))
        {
            await pmWorkOrderChecklistService.EnsureRequiredChecklistCompletedAsync(workOrder.Id, TenantId, cancellationToken);
        }

        var operationsPolicy = await dbContext.TenantOperationsPolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken);

        var requireTechSig = operationsPolicy?.RequireTechnicianSignature ?? true;
        var requireClientSig = (operationsPolicy?.RequireClientSignature ?? true) && !(operationsPolicy?.AllowRemoteCompletion ?? false);

        if (requireTechSig || requireClientSig)
        {
            var signatureTypes = await dbContext.WorkOrderSignatures
                .Where(x => x.TenantId == TenantId && x.WorkOrderId == workOrder.Id)
                .Select(x => x.SignatureType)
                .ToListAsync(cancellationToken);

            if (requireTechSig && !signatureTypes.Contains("Technician", StringComparer.OrdinalIgnoreCase))
            {
                throw new BusinessRuleException("Capture the technician signature before completing the work order.");
            }

            if (requireClientSig && !signatureTypes.Contains("Client", StringComparer.OrdinalIgnoreCase))
            {
                throw new BusinessRuleException("Capture the client signature before completing the work order.");
            }
        }

        await workOrderLifecycleService.CompleteAsync(
            TenantId,
            workOrder.Id,
            UserId,
            new Infrastructure.Services.WorkOrderCompletionRequest(request.WorkDoneNotes, request.ReportSummary, request.AnswersJson),
            cancellationToken);
        if (contractOperationalService is not null)
        {
            await contractOperationalService.IncrementPmEntitlementUsageIfNeededAsync(TenantId, workOrder.Id, cancellationToken);
        }

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Work order completed",
            nameof(WorkOrder),
            workOrder.Id.ToString(),
            $"Completed work order '{workOrder.WorkOrderNumber}'.",
            cancellationToken);

        if (workOrder.ParentWorkOrderId.HasValue)
        {
            await TryAutoCompleteParentAsync(workOrder.ParentWorkOrderId.Value, cancellationToken);
        }

        var persisted = await QueryWorkOrders(await branchAccessService.GetQueryScopeAsync(TenantId, workOrder.BranchId, cancellationToken))
            .SingleAsync(x => x.Id == workOrder.Id, cancellationToken);
        return Ok(Map(persisted));
    }

    [HttpPost("{id:guid}/acknowledge")]
    public async Task<ActionResult<WorkOrderResponse>> Acknowledge(Guid id, [FromBody] AcknowledgeWorkOrderRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.AcknowledgedByName))
        {
            throw new BusinessRuleException("Acknowledged by name is required.");
        }

        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var workOrder = await dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId && x.Id == id)
            .WhereAccessible(scope, x => x.BranchId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");

        if (!string.Equals(workOrder.Status, WorkOrderStatuses.Completed, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("Only completed work orders can be acknowledged.");
        }

        workOrder.AcknowledgedByName = request.AcknowledgedByName.Trim();
        workOrder.AcknowledgementComments = request.Comments?.Trim();
        workOrder.AcknowledgementDate = DateTimeNormalizationHelper.NormalizeNullableToUtc(request.AcknowledgementDate) ?? DateTime.UtcNow;
        await workOrderLifecycleService.ChangeStatusAsync(
            TenantId,
            workOrder.Id,
            UserId,
            new Infrastructure.Services.WorkOrderStatusChangeRequest(
                WorkOrderStatuses.ClientAccepted,
                "Client sign-off captured",
                request.Comments?.Trim()),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Work order acknowledged",
            nameof(WorkOrder),
            workOrder.Id.ToString(),
            $"Acknowledged work order '{workOrder.WorkOrderNumber}' by '{workOrder.AcknowledgedByName}'.",
            cancellationToken);

        var persisted = await QueryWorkOrders(await branchAccessService.GetQueryScopeAsync(TenantId, workOrder.BranchId, cancellationToken))
            .SingleAsync(x => x.Id == workOrder.Id, cancellationToken);
        return Ok(Map(persisted));
    }

    [HttpGet("{id:guid}/job-card")]
    public async Task<IActionResult> GetJobCard(Guid id, CancellationToken cancellationToken)
    {
        EnsureGeneralWorkspaceAccess(PermissionNames.CanViewWorkOrders);
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);

        var wo = await QueryWorkOrders(scope)
            .Include(x => x.Site)
            .Include(x => x.ChecklistItems)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");

        var tenantSetting = await dbContext.EmailSettings
            .Where(x => x.TenantId == TenantId)
            .Select(x => new { x.SenderName })
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new
        {
            wo.Id,
            wo.WorkOrderNumber,
            wo.Title,
            wo.Description,
            wo.Priority,
            wo.Status,
            wo.WorkType,
            wo.ServiceType,
            wo.DueDate,
            wo.JobCardNotes,
            wo.IsPreventiveMaintenance,
            wo.ArrivalAt,
            wo.DepartureAt,
            Client = new { wo.Client?.ClientName, wo.Client?.ContactPerson, wo.Client?.Phone },
            Site = wo.Site == null ? null : new
            {
                wo.Site.SiteName,
                wo.Site.SiteCode,
                wo.Site.StreetAddress,
                wo.Site.TownCity,
                wo.Site.County,
                wo.Site.ContactPerson,
                wo.Site.ContactPhone,
                wo.Site.AccessNotes,
                wo.Site.OperatingHours
            },
            Asset = wo.Asset == null ? null : new
            {
                wo.Asset.AssetName,
                wo.Asset.AssetCode,
                wo.Asset.AssetType,
                wo.Asset.SerialNumber,
                wo.Asset.Manufacturer,
                wo.Asset.Model,
                wo.Asset.Location
            },
            AssignedTechnician = wo.AssignedTechnician == null ? null : new
            {
                wo.AssignedTechnician.FullName,
                wo.AssignedTechnician.Phone,
                wo.AssignedTechnician.Email
            },
            Checklist = wo.ChecklistItems
                .OrderBy(x => x.SortOrder)
                .Select(c => new
                {
                    c.SectionName,
                    c.QuestionText,
                    c.InputType,
                    c.IsRequired,
                    c.ResponseValue,
                    c.IsCompleted
                }),
            CompanyName = tenantSetting?.SenderName
        });
    }

    [HttpPost("{id:guid}/duplicate")]
    public async Task<ActionResult<WorkOrderResponse>> Duplicate(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanCreateWorkOrders);
        await licenseGuardService.EnsureTenantCanMutateAsync(TenantId, cancellationToken);

        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var source = await QueryWorkOrders(scope).SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");

        var duplicate = new WorkOrder
        {
            TenantId = TenantId,
            BranchId = source.BranchId,
            ClientId = source.ClientId,
            SiteId = source.SiteId,
            SystemId = source.SystemId,
            AssetId = source.AssetId,
            AssignmentGroupId = null,
            AssignmentType = WorkOrderAssignmentTypes.Unassigned,
            WorkOrderNumber = await documentNumberingService.GenerateAsync(TenantId, source.BranchId, DocumentTypes.WorkOrder, cancellationToken),
            Title = source.Title,
            Description = source.Description,
            Priority = source.Priority,
            Status = WorkOrderStatuses.New,
            WorkType = source.WorkType,
            ServiceType = source.ServiceType,
            EstimatedDuration = source.EstimatedDuration,
            AssignedTechnicianId = null,
            LeadTechnicianId = null,
            ScheduledStartAt = null,
            ScheduledEndAt = null,
            DueDate = null,
            IsPreventiveMaintenance = source.IsPreventiveMaintenance,
            PmTemplateId = source.PmTemplateId,
            WorkOrderTemplateId = source.WorkOrderTemplateId,
            ContractId = source.ContractId,
            RateCardId = source.RateCardId,
            JobCardNotes = source.JobCardNotes,
            PlannedMaterialsJson = source.PlannedMaterialsJson,
            NotifyClient = source.NotifyClient,
            ClientContactEmail = source.ClientContactEmail,
            TrackingToken = TrackingTokenGenerator.Generate()
        };

        if (contractOperationalService is not null)
        {
            await contractOperationalService.ApplyOperationalSnapshotAsync(duplicate, cancellationToken);
        }
        else
        {
            await slaService.ApplyDefinitionAsync(duplicate, cancellationToken);
        }

        dbContext.WorkOrders.Add(duplicate);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (duplicate.PmTemplateId.HasValue)
        {
            await pmWorkOrderChecklistService.AttachPmTemplateToWorkOrderAsync(duplicate.Id, duplicate.PmTemplateId.Value, TenantId, UserId, cancellationToken: cancellationToken);
        }

        await workOrderLifecycleService.RecordCreatedAsync(TenantId, duplicate.Id, UserId, duplicate.WorkOrderNumber, cancellationToken);
        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Work order duplicated",
            nameof(WorkOrder),
            duplicate.Id.ToString(),
            $"Duplicated work order '{source.WorkOrderNumber}' as '{duplicate.WorkOrderNumber}'.",
            cancellationToken);

        var persisted = await QueryWorkOrders(await branchAccessService.GetQueryScopeAsync(TenantId, duplicate.BranchId, cancellationToken))
            .SingleAsync(x => x.Id == duplicate.Id, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = duplicate.Id }, Map(persisted));
    }

    [HttpGet("{id:guid}/children")]
    public async Task<ActionResult<IReadOnlyCollection<WorkOrderResponse>>> GetChildren(Guid id, CancellationToken cancellationToken)
    {
        EnsureGeneralWorkspaceAccess(PermissionNames.CanViewWorkOrders);
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var parentExists = await dbContext.WorkOrders.AnyAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken);
        if (!parentExists)
        {
            throw new NotFoundException("Work order was not found.");
        }

        var children = await QueryWorkOrders(scope)
            .Where(x => x.ParentWorkOrderId == id)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(children.Select(Map).ToList());
    }

    [HttpPatch("{id:guid}/hold")]
    public async Task<ActionResult<WorkOrderResponse>> Hold(Guid id, [FromBody] HoldWorkOrderRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanCompleteWorkOrders);
        await EnsureAssignedExecutionAccessAsync(id, cancellationToken);

        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var exists = await dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId && x.Id == id)
            .WhereAccessible(scope, x => x.BranchId)
            .AnyAsync(cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Work order was not found.");
        }

        var workOrder = await workOrderLifecycleService.HoldAsync(TenantId, id, UserId, request.HoldType, request.Reason, cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Work order put on hold",
            nameof(WorkOrder),
            workOrder.Id.ToString(),
            $"Work order '{workOrder.WorkOrderNumber}' put on hold ({request.HoldType ?? WorkOrderHoldTypes.OnHold}).",
            cancellationToken);

        var persisted = await QueryWorkOrders(await branchAccessService.GetQueryScopeAsync(TenantId, workOrder.BranchId, cancellationToken))
            .SingleAsync(x => x.Id == workOrder.Id, cancellationToken);
        return Ok(Map(persisted));
    }

    [HttpPatch("{id:guid}/resume")]
    public async Task<ActionResult<WorkOrderResponse>> ResumeFromHold(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanCompleteWorkOrders);
        await EnsureAssignedExecutionAccessAsync(id, cancellationToken);

        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var exists = await dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId && x.Id == id)
            .WhereAccessible(scope, x => x.BranchId)
            .AnyAsync(cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Work order was not found.");
        }

        var workOrder = await workOrderLifecycleService.ResumeAsync(TenantId, id, UserId, cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Work order resumed",
            nameof(WorkOrder),
            workOrder.Id.ToString(),
            $"Work order '{workOrder.WorkOrderNumber}' resumed from hold.",
            cancellationToken);

        var persisted = await QueryWorkOrders(await branchAccessService.GetQueryScopeAsync(TenantId, workOrder.BranchId, cancellationToken))
            .SingleAsync(x => x.Id == workOrder.Id, cancellationToken);
        return Ok(Map(persisted));
    }

    private async Task<WorkOrderTemplate?> ResolveWorkOrderTemplateAsync(Guid? templateId, CancellationToken cancellationToken)
    {
        if (!templateId.HasValue)
        {
            return null;
        }

        return await dbContext.WorkOrderTemplates
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == templateId.Value && x.IsActive, cancellationToken)
            ?? throw new BusinessRuleException("Work order template was not found.");
    }

    private static CreateWorkOrderRequest ApplyTemplateDefaults(CreateWorkOrderRequest request, WorkOrderTemplate? template)
    {
        if (template is null)
        {
            return request;
        }

        return request with
        {
            WorkType = string.IsNullOrWhiteSpace(request.WorkType) ? template.JobType : request.WorkType,
            Priority = string.IsNullOrWhiteSpace(request.Priority) ? template.DefaultPriority : request.Priority,
            Description = string.IsNullOrWhiteSpace(request.Description) ? template.Description : request.Description,
            PmTemplateId = request.PmTemplateId ?? template.ChecklistTemplateId
        };
    }

    /// <summary>
    /// A follow-up is a return visit to a finished job, so the original must be completed (or
    /// accepted/closed) and belong to the same client; anything still open should be worked on directly.
    /// </summary>
    private async Task<WorkOrder?> EnsureFollowUpSourceAsync(Guid? followUpOfWorkOrderId, Guid clientId, CancellationToken cancellationToken)
    {
        if (!followUpOfWorkOrderId.HasValue)
        {
            return null;
        }

        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var original = await dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId && x.Id == followUpOfWorkOrderId.Value)
            .WhereAccessible(scope, x => x.BranchId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleException("The work order being followed up was not found.");

        if (!FollowUpChecklist.CanBeFollowedUp(original.Status))
        {
            throw new BusinessRuleException("Only completed or closed work orders can be followed up. Continue the open job instead.");
        }

        if (original.ClientId != clientId)
        {
            throw new BusinessRuleException("A follow-up must be for the same client as the original work order.");
        }

        return original;
    }

    private async Task<WorkOrder?> EnsureParentWorkOrderExistsAsync(Guid? parentWorkOrderId, CancellationToken cancellationToken)
    {
        if (!parentWorkOrderId.HasValue)
        {
            return null;
        }

        return await dbContext.WorkOrders.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == parentWorkOrderId.Value, cancellationToken)
            ?? throw new BusinessRuleException("Parent work order was not found.");
    }

    private async Task<string?> BuildPlannedMaterialsSnapshotAsync(WorkOrderTemplate template, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(template.DefaultMaterialsJson))
        {
            return null;
        }

        List<TemplateMaterialRefDto> refs;
        try
        {
            refs = JsonSerializer.Deserialize<List<TemplateMaterialRefDto>>(template.DefaultMaterialsJson) ?? [];
        }
        catch
        {
            return null;
        }

        if (refs.Count == 0)
        {
            return null;
        }

        var ids = refs.Select(x => x.MaterialId).ToList();
        var names = await dbContext.MaterialItems
            .Where(x => x.TenantId == TenantId && ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.ItemName, cancellationToken);

        var snapshot = refs
            .Select(r => new PlannedMaterialSnapshot(r.MaterialId, names.GetValueOrDefault(r.MaterialId) ?? "Unknown material", r.Quantity))
            .ToList();
        return JsonSerializer.Serialize(snapshot);
    }

    internal static IReadOnlyCollection<PlannedMaterialResponse> ParsePlannedMaterials(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var snapshot = JsonSerializer.Deserialize<List<PlannedMaterialSnapshot>>(json) ?? [];
            return snapshot.Select(x => new PlannedMaterialResponse(x.MaterialId, x.MaterialName, x.Quantity)).ToList();
        }
        catch
        {
            return [];
        }
    }

    private async Task TryAutoCompleteParentAsync(Guid parentWorkOrderId, CancellationToken cancellationToken)
    {
        var parent = await dbContext.WorkOrders
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == parentWorkOrderId, cancellationToken);

        if (parent is null
            || string.Equals(WorkOrderStatuses.Normalize(parent.Status), WorkOrderStatuses.Completed, StringComparison.OrdinalIgnoreCase)
            || WorkOrderStatuses.IsClosed(parent.Status))
        {
            return;
        }

        var childStatuses = await dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId && x.ParentWorkOrderId == parentWorkOrderId)
            .Select(x => x.Status)
            .ToListAsync(cancellationToken);

        var allChildrenDone = childStatuses.Count > 0 && childStatuses.All(status =>
            string.Equals(WorkOrderStatuses.Normalize(status), WorkOrderStatuses.Completed, StringComparison.OrdinalIgnoreCase)
            || WorkOrderStatuses.IsClosed(status));

        if (!allChildrenDone)
        {
            return;
        }

        var oldStatus = WorkOrderStatuses.Normalize(parent.Status);
        parent.Status = WorkOrderStatuses.Completed;
        parent.CompletedAt = DateTime.UtcNow;

        dbContext.WorkOrderEvents.Add(new WorkOrderEvent
        {
            TenantId = TenantId,
            WorkOrderId = parent.Id,
            ActorUserId = UserId,
            EventType = "StatusChanged",
            Status = WorkOrderStatuses.Completed,
            OldStatus = oldStatus,
            NewStatus = WorkOrderStatuses.Completed,
            Message = "All sub-tasks completed; parent work order auto-completed.",
            OccurredAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed record TemplateMaterialRefDto(Guid MaterialId, decimal Quantity);
    private sealed record PlannedMaterialSnapshot(Guid MaterialId, string MaterialName, decimal Quantity);

    private IQueryable<WorkOrder> QueryWorkOrders(BranchQueryScope scope) =>
        dbContext.WorkOrders
            .Include(x => x.Client)
            .Include(x => x.Asset)
            .ThenInclude(x => x.AssetCategory)
            .Include(x => x.Site)
            .Include(x => x.System)
            .Include(x => x.Contract)
            .Include(x => x.RateCard)
            .Include(x => x.AssignedTechnician)
            .Include(x => x.LeadTechnician)
            .Include(x => x.AssignmentGroup)
            .Include(x => x.Branch)
            .Include(x => x.Assignments)
            .ThenInclude(x => x.AssignmentGroup)
            .Include(x => x.Events)
            .Include(x => x.TechnicianAssignments)
            .ThenInclude(x => x.Technician)
            .Include(x => x.PmTemplate)
            .Include(x => x.ChecklistItems)
            .Include(x => x.ChildWorkOrders)
            .Include(x => x.FollowUpOfWorkOrder)
            .Include(x => x.FollowUpWorkOrders)
            .Where(x => x.TenantId == TenantId)
            .WhereAccessible(scope, x => x.BranchId);

    private static void ValidateCreate(CreateWorkOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new BusinessRuleException("Title is required.");
        }

        _ = WorkOrderTypes.Normalize(request.WorkType);

        var assignmentType = NormalizeAssignmentType(request.AssignmentType, request.AssignmentGroupId, request.AssignedTechnicianId, request.AssignedTechnicianIds);
        var assignedTechnicianIds = NormalizeAssignedTechnicianIds(request.AssignedTechnicianIds);

        if (assignmentType == WorkOrderAssignmentTypes.IndividualTechnician && !request.AssignedTechnicianId.HasValue && assignedTechnicianIds.Count == 0)
        {
            throw new BusinessRuleException("Select a technician for an individual assignment.");
        }

        if (assignmentType == WorkOrderAssignmentTypes.MultipleTechnicians && assignedTechnicianIds.Count < 2)
        {
            throw new BusinessRuleException("Select at least two technicians for a multiple-technician assignment.");
        }

        if (assignmentType == WorkOrderAssignmentTypes.AssignmentGroup && !request.AssignmentGroupId.HasValue)
        {
            throw new BusinessRuleException("Select an assignment group before saving.");
        }

        if (request.LeadTechnicianId.HasValue && assignmentType == WorkOrderAssignmentTypes.MultipleTechnicians && !assignedTechnicianIds.Contains(request.LeadTechnicianId.Value))
        {
            throw new BusinessRuleException("Lead technician must belong to the selected technicians.");
        }

        if (WorkOrderTypes.IsPreventiveMaintenanceType(request.WorkType) && !request.PmTemplateId.HasValue)
        {
            throw new BusinessRuleException("Select a checklist before saving a preventive maintenance work order.");
        }
    }

    private async Task EnsureClientExistsAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Clients.AnyAsync(x => x.TenantId == TenantId && x.IsActive && x.Id == clientId, cancellationToken);
        if (!exists)
        {
            throw new BusinessRuleException("Client was not found for this tenant.");
        }
    }

    private async Task<Asset?> EnsureAssetExistsAsync(Guid? assetId, Guid clientId, CancellationToken cancellationToken)
    {
        if (!assetId.HasValue)
        {
            return null;
        }

        var asset = await dbContext.Assets.SingleOrDefaultAsync(
            x => x.TenantId == TenantId
                && x.Status != "Inactive"
                && x.Id == assetId.Value
                && x.ClientId == clientId,
            cancellationToken);

        if (asset is null)
        {
            throw new BusinessRuleException("Asset was not found for this tenant and client.");
        }

        return asset;
    }

    private async Task<Guid?> ResolveWorkOrderBranchIdAsync(Guid? requestedBranchId, Guid? assetBranchId, CancellationToken cancellationToken)
    {
        var resolvedBranchId = await branchAccessService.ResolveBranchIdForWriteAsync(TenantId, requestedBranchId ?? assetBranchId, cancellationToken);
        if (requestedBranchId.HasValue && assetBranchId.HasValue && requestedBranchId != assetBranchId)
        {
            throw new BusinessRuleException("Work order branch must match the selected asset branch.");
        }

        return resolvedBranchId;
    }

    private static string NormalizeStatus(string status)
        => WorkOrderStatuses.Normalize(status);

    private static string NormalizeAssignmentType(string? assignmentType, Guid? assignmentGroupId, Guid? assignedTechnicianId, IReadOnlyCollection<Guid>? assignedTechnicianIds)
    {
        var normalizedTechnicianIds = ResolveTechnicianIds(assignedTechnicianId, assignedTechnicianIds, assignmentType);
        var value = string.IsNullOrWhiteSpace(assignmentType)
            ? assignmentGroupId.HasValue
                ? WorkOrderAssignmentTypes.AssignmentGroup
                : normalizedTechnicianIds.Count > 0
                    ? WorkOrderAssignmentTypes.IndividualTechnician
                    : WorkOrderAssignmentTypes.Unassigned
            : assignmentType.Trim();
        return value switch
        {
            WorkOrderAssignmentTypes.IndividualTechnician or WorkOrderAssignmentTypes.MultipleTechnicians or WorkOrderAssignmentTypes.AssignmentGroup or WorkOrderAssignmentTypes.Unassigned => value,
            _ => throw new BusinessRuleException("Invalid assignment type.")
        };
    }

    private static List<Guid> NormalizeAssignedTechnicianIds(IReadOnlyCollection<Guid>? assignedTechnicianIds) =>
        assignedTechnicianIds?.Where(id => id != Guid.Empty).Distinct().ToList() ?? [];

    private static Guid? ResolvePrimaryTechnicianId(CreateWorkOrderRequest request)
    {
        var assignmentType = NormalizeAssignmentType(request.AssignmentType, request.AssignmentGroupId, request.AssignedTechnicianId, request.AssignedTechnicianIds);
        var assignedTechnicianIds = NormalizeAssignedTechnicianIds(request.AssignedTechnicianIds);

        return assignmentType switch
        {
            WorkOrderAssignmentTypes.IndividualTechnician => request.AssignedTechnicianId,
            WorkOrderAssignmentTypes.MultipleTechnicians => request.LeadTechnicianId ?? assignedTechnicianIds.FirstOrDefault(),
            WorkOrderAssignmentTypes.AssignmentGroup => request.LeadTechnicianId,
            _ => request.AssignedTechnicianId
        };
    }

    private static Guid? ResolvePrimaryTechnicianId(UpdateWorkOrderRequest request)
    {
        var assignmentType = NormalizeAssignmentType(request.AssignmentType, request.AssignmentGroupId, request.AssignedTechnicianId, request.AssignedTechnicianIds);
        var assignedTechnicianIds = NormalizeAssignedTechnicianIds(request.AssignedTechnicianIds);

        return assignmentType switch
        {
            WorkOrderAssignmentTypes.IndividualTechnician => request.AssignedTechnicianId,
            WorkOrderAssignmentTypes.MultipleTechnicians => request.LeadTechnicianId ?? assignedTechnicianIds.FirstOrDefault(),
            WorkOrderAssignmentTypes.AssignmentGroup => request.LeadTechnicianId,
            _ => request.AssignedTechnicianId
        };
    }

    private static List<Guid> ResolveTechnicianIds(Guid? assignedTechnicianId, IReadOnlyCollection<Guid>? assignedTechnicianIds, string? assignmentType)
    {
        var normalized = NormalizeAssignedTechnicianIds(assignedTechnicianIds);
        if (assignedTechnicianId.HasValue && !normalized.Contains(assignedTechnicianId.Value))
        {
            normalized.Insert(0, assignedTechnicianId.Value);
        }

        if (string.Equals(assignmentType, WorkOrderAssignmentTypes.AssignmentGroup, StringComparison.OrdinalIgnoreCase) && assignedTechnicianId.HasValue && normalized.Count == 0)
        {
            normalized.Add(assignedTechnicianId.Value);
        }

        return normalized;
    }

    /// <summary>
    /// Applies the SLA bucket the Work Orders list filters on. Mirrors the client-side
    /// <c>resolveSlaStatusKey</c> exactly (breached wins, then "no deadline" is No SLA, then a
    /// deadline inside the at-risk window is At risk) so moving the filter server-side does not
    /// change which rows a user sees.
    /// </summary>
    private static IQueryable<WorkOrder> ApplySlaStatusFilter(IQueryable<WorkOrder> query, string slaStatus, DateTime utcNow)
    {
        var atRiskThreshold = utcNow.AddMinutes(SlaAtRiskWindowMinutes);
        var normalized = new string(slaStatus.Where(char.IsLetter).ToArray()).ToLowerInvariant();

        return normalized switch
        {
            "breached" => query.Where(x => x.SlaResponseBreached || x.SlaResolutionBreached),
            "nosla" => query.Where(x => !x.SlaResponseBreached && !x.SlaResolutionBreached && x.SlaResolutionDeadline == null),
            "atrisk" => query.Where(x => !x.SlaResponseBreached
                && !x.SlaResolutionBreached
                && x.SlaResolutionDeadline != null
                && x.SlaResolutionDeadline <= atRiskThreshold),
            "ontrack" => query.Where(x => !x.SlaResponseBreached
                && !x.SlaResolutionBreached
                && x.SlaResolutionDeadline != null
                && x.SlaResolutionDeadline > atRiskThreshold),
            _ => throw new BusinessRuleException("SLA status filter must be on-track, at-risk, breached, or no-sla.")
        };
    }

    private WorkOrderResponse Map(WorkOrder workOrder)
    {
        var holdDetails = ResolveActiveHoldDetails(workOrder);
        var assignment = WorkOrderAssignmentView.Resolve(workOrder);
        var technicianAssignments = assignment.TechnicianAssignments;
        var availableTransitions = WorkOrderStatusTransitions
            .GetAllowedTargets(workOrder.Status, CanUseAdminOnlyLifecycleTransitions())
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
            NormalizeStatus(workOrder.Status),
            WorkOrderTypes.Normalize(workOrder.WorkType, WorkOrderTypes.CorrectiveMaintenance),
            ResolveSlaStatus(workOrder),
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
            technicianAssignments,
            assignment.AssignmentSummary,
            assignment.IsUnassigned,
            workOrder.PmTemplateId,
            workOrder.PmTemplateNameSnapshot ?? workOrder.PmTemplate?.Name,
            workOrder.PreventiveMaintenancePlanId,
            workOrder.SystemId,
            workOrder.System?.SystemName,
            workOrder.ChecklistItems
                .OrderBy(x => x.SectionOrder)
                .ThenBy(x => x.SectionName ?? string.Empty)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.CreatedAt)
                .Select(x => new WorkOrderChecklistItemResponse(
                    x.Id,
                    x.PmTemplateQuestionId,
                    x.SectionName,
                    x.QuestionText,
                    x.InputType,
                    x.IsRequired,
                    x.SortOrder,
                    x.ResponseValue,
                    x.Remarks,
                    x.IsCompleted,
                    x.CompletedByUserId,
                    x.CompletedAt,
                    ParseOptions(x.OptionsJson),
                    x.SectionOrder,
                    x.HelperText,
                    x.Unit,
                    x.MinValue,
                    x.MaxValue,
                    x.DefaultValue,
                    x.EvidenceRequired,
                    x.AttachmentId,
                    x.TechnicianRemarksAllowed,
                    x.RequiresNoteOnFail))
                .ToList(),
            workOrder.PmTemplateVersionSnapshot,
            workOrder.ScheduledStartAt,
            workOrder.ScheduledEndAt,
            workOrder.NotifyClient,
            workOrder.ClientContactEmail,
            "NotBillable",
            false,
            workOrder.Client?.ContactPerson,
            workOrder.Client?.ContactPhone,
            workOrder.Client?.SlaPlan,
            workOrder.Site?.SiteCode,
            workOrder.Site?.ContactPerson,
            workOrder.Site?.ContactPhone,
            workOrder.Site?.OperatingHours,
            workOrder.Site?.AccessNotes,
            BuildSiteLocation(workOrder.Site),
            workOrder.Site?.Latitude,
            workOrder.Site?.Longitude,
            workOrder.EstimatedLabourHours,
            workOrder.Asset?.AssetCode,
            workOrder.Asset?.AssetType,
            workOrder.Asset?.AssetCategory?.Name,
            workOrder.Asset?.SerialNumber,
            workOrder.Asset?.Manufacturer,
            workOrder.Asset?.Model,
            workOrder.Asset?.Location,
            workOrder.Asset?.WarrantyExpiryDate,
            workOrder.Asset?.LastPmDate,
            workOrder.Asset?.NextPmDate,
            ResolveAssetWarrantyStatus(workOrder.Asset),
            null,
            availableTransitions,
            workOrder.ContractId,
            workOrder.Contract?.ContractNumber,
            workOrder.Contract?.Status.ToString(),
            workOrder.CoverageStatus,
            workOrder.BillingModel,
            workOrder.RateCard?.Name,
            workOrder.Contract?.EndDate,
            workOrder.Contract?.RenewalRisk.ToString(),
            workOrder.PricingNotes,
            workOrder.EstimatedCharge,
            workOrder.FinalCharge,
            workOrder.AppliedSlaSource,
            workOrder.AppliedRateSource,
            workOrder.TrackingToken,
            workOrder.WorkOrderTemplateId,
            null,
            null,
            workOrder.ParentWorkOrderId,
            workOrder.ChildWorkOrders?.Count ?? 0,
            WorkOrderStatuses.IsHold(workOrder.Status) ? WorkOrderHoldTypes.FromStatus(workOrder.Status) : null,
            workOrder.HoldStartedAt,
            ParsePlannedMaterials(workOrder.PlannedMaterialsJson),
            workOrder.WorkflowStatusId,
            workOrder.IsRepeatVisit,
            workOrder.RepeatVisitCount,
            workOrder.FollowUpOfWorkOrderId,
            workOrder.FollowUpOfWorkOrder?.WorkOrderNumber,
            (workOrder.FollowUpWorkOrders ?? [])
                .OrderBy(x => x.CreatedAt)
                .Select(x => new WorkOrderLinkResponse(x.Id, x.WorkOrderNumber, WorkOrderStatuses.Normalize(x.Status), x.CreatedAt))
                .ToList(),
            FollowUpChecklist.IsRequested(workOrder.ChecklistItems));
    }

    /// <summary>Open work plus anything finished in the last <see cref="RecentlyFinishedDays"/> days.</summary>
    internal static IQueryable<WorkOrder> ApplyCurrentView(IQueryable<WorkOrder> query, DateTime utcNow)
    {
        var finished = WorkOrderStatuses.TerminalStatuses.SelectMany(WorkOrderStatuses.RawStatusesMatching).Distinct().ToArray();
        var cutoff = utcNow.AddDays(-RecentlyFinishedDays);
        // ClosedAt is set when a job is closed; a cancellation or rejection is dated by its last update.
        return query.Where(x => !finished.Contains(x.Status) || (x.ClosedAt ?? x.UpdatedAt ?? x.CreatedAt) >= cutoff);
    }

    private static string ResolveSlaStatus(WorkOrder workOrder)
    {
        if (workOrder.SlaResolutionBreached)
        {
            return "Resolution Breached";
        }

        if (workOrder.SlaResponseBreached)
        {
            return "Response Breached";
        }

        if (workOrder.SlaResolutionDeadline.HasValue)
        {
            return "On Track";
        }

        return "Not Configured";
    }

    private static int? MinutesRemaining(DateTime? deadline, DateTime now) =>
        deadline.HasValue ? (int)Math.Round((deadline.Value - now).TotalMinutes) : null;

    private static string ResolveSlaTimelineStatus(WorkOrder workOrder, DateTime now)
    {
        if (workOrder.SlaResponseBreached || workOrder.SlaResolutionBreached)
        {
            return "Breached";
        }

        if (!workOrder.SlaResponseDeadline.HasValue && !workOrder.SlaResolutionDeadline.HasValue)
        {
            return "NoSla";
        }

        var nearestDeadline = new[] { workOrder.SlaResponseDeadline, workOrder.SlaResolutionDeadline }
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .OrderBy(x => x)
            .FirstOrDefault();

        if (nearestDeadline != default && (nearestDeadline - now).TotalHours <= 2)
        {
            return "AtRisk";
        }

        return "OnTrack";
    }

    private static (string? Reason, string? Note) ResolveActiveHoldDetails(WorkOrder workOrder)
        => WorkOrderHoldReasons.ResolveCurrentDetails(workOrder);

    private bool CanUseAdminOnlyLifecycleTransitions() =>
        IsAdmin
        || tenantContext.IsSuperAdmin
        || tenantContext.HasRole(AppRoles.PlatformAdmin)
        || tenantContext.HasRole(AppRoles.PlatformAdminRole)
        || tenantContext.HasRole(AppRoles.PlatformOwner)
        || tenantContext.HasRole(AppRoles.PlatformSuperAdmin)
        || tenantContext.HasRole(AppRoles.SuperAdmin);

    internal static string? BuildSiteLocation(Site? site)
    {
        if (site is null)
        {
            return null;
        }

        var parts = new[] { site.StreetAddress, site.TownCity, site.County, site.Country }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part!.Trim())
            .ToList();

        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    internal static string? ResolveAssetWarrantyStatus(Asset? asset)
    {
        if (asset?.WarrantyExpiryDate is null)
        {
            return null;
        }

        return asset.WarrantyExpiryDate.Value.Date >= DateTime.UtcNow.Date
            ? "Under Warranty"
            : "Out of Warranty";
    }

    private async Task EnsurePmTemplateIsValidAsync(string workType, Guid? pmTemplateId, CancellationToken cancellationToken)
    {
        if (!pmTemplateId.HasValue)
        {
            return;
        }

        var template = await pmWorkOrderChecklistService.GetActiveTemplateAsync(TenantId, pmTemplateId.Value, cancellationToken);
        if (WorkOrderTypes.IsPreventiveMaintenanceType(workType) && !string.Equals(template.WorkType, "PreventiveMaintenance", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("Preventive maintenance work orders require a preventive maintenance checklist.");
        }

        if (!WorkOrderTypes.IsPreventiveMaintenanceType(workType) && string.Equals(template.WorkType, "PreventiveMaintenance", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("Select a breakdown or corrective checklist for this work order.");
        }

        // TODO: When checklist templates support installation and inspection categories,
        // tighten template filtering for New Installation and Inspection / Assessment.
    }

    private async Task EnsureSystemExistsAsync(Guid? systemId, Guid clientId, Guid? siteId, CancellationToken cancellationToken)
    {
        if (!systemId.HasValue)
        {
            return;
        }

        var system = await dbContext.Systems.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == systemId.Value, cancellationToken)
            ?? throw new BusinessRuleException("System was not found for this tenant.");

        if (system.ClientId != clientId)
        {
            throw new BusinessRuleException("System does not belong to the selected client.");
        }

        if (siteId.HasValue && system.SiteId != siteId.Value)
        {
            throw new BusinessRuleException("System does not belong to the selected site.");
        }
    }

    private static IReadOnlyCollection<string> ParseOptions(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(optionsJson) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void EnsureGeneralWorkspaceAccess(string permissionName)
    {
        userAccessService.EnsureAdminOrPermission(permissionName);

        if (IsTechnicianOnlyWorkspaceUser())
        {
            throw new ForbiddenException("Technicians must use the assigned technician jobs workspace.");
        }
    }

    /// <summary>
    /// Resolves the technician whose assignment row the location-only <c>/arrive</c> and <c>/depart</c>
    /// endpoints act on. They predate the technician dispatch lifecycle and carry no technician id, so
    /// the caller's own assignment is preferred and a sole assignment is used otherwise. Failing loudly
    /// beats writing a header timestamp that satisfies no guard.
    /// </summary>
    private async Task<Guid> ResolveDispatchTechnicianIdAsync(Guid workOrderId, string action, CancellationToken cancellationToken)
    {
        var currentTechnicianId = await ResolveCurrentTechnicianIdAsync(cancellationToken);
        if (currentTechnicianId.HasValue
            && await dbContext.WorkOrderTechnicianAssignments.AnyAsync(
                x => x.TenantId == TenantId && x.WorkOrderId == workOrderId && x.TechnicianId == currentTechnicianId.Value,
                cancellationToken))
        {
            return currentTechnicianId.Value;
        }

        var technicianIds = await dbContext.WorkOrderTechnicianAssignments
            .Where(x => x.TenantId == TenantId && x.WorkOrderId == workOrderId)
            .Select(x => x.TechnicianId)
            .Take(2)
            .ToListAsync(cancellationToken);

        return technicianIds.Count switch
        {
            1 => technicianIds[0],
            0 => throw new BusinessRuleException($"{action} cannot be recorded before a technician is assigned to this work order."),
            _ => throw new BusinessRuleException($"This work order has more than one assigned technician, so {action.ToLowerInvariant()} must be recorded against a specific technician.")
        };
    }

    private async Task EnsureAssignedExecutionAccessAsync(Guid workOrderId, CancellationToken cancellationToken)
    {
        if (!IsTechnicianOnlyWorkspaceUser())
        {
            return;
        }

        var technicianId = await ResolveCurrentTechnicianIdAsync(cancellationToken);
        if (!technicianId.HasValue)
        {
            throw new ForbiddenException("Technicians can only act on jobs assigned to them.");
        }

        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var hasAssignment = await dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId && x.Id == workOrderId)
            .WhereAccessible(scope, x => x.BranchId)
            .AnyAsync(x =>
                x.AssignedTechnicianId == technicianId.Value
                || x.LeadTechnicianId == technicianId.Value
                || x.TechnicianAssignments.Any(assignment => assignment.TechnicianId == technicianId.Value),
                cancellationToken);

        if (!hasAssignment)
        {
            throw new ForbiddenException("Technicians can only act on jobs assigned to them.");
        }
    }

    private async Task<Guid?> ResolveCurrentTechnicianIdAsync(CancellationToken cancellationToken)
    {
        var query = dbContext.Technicians
            .Where(x => x.TenantId == TenantId && x.IsActive);

        if (UserId != Guid.Empty)
        {
            var byUserId = await query
                .Where(x => x.UserId == UserId)
                .Select(x => (Guid?)x.Id)
                .SingleOrDefaultAsync(cancellationToken);

            if (byUserId.HasValue)
            {
                return byUserId.Value;
            }
        }

        var userEmail = await dbContext.Users
            .Where(x => x.TenantId == TenantId && x.Id == UserId)
            .Select(x => x.Email)
            .SingleOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(userEmail))
        {
            return null;
        }

        return await query
            .Where(x => x.Email == userEmail)
            .Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }
}

public sealed record CreateWorkOrderRequest(
    Guid ClientId,
    Guid? BranchId,
    Guid? SiteId,
    Guid? SystemId,
    Guid? AssetId,
    Guid? AssignmentGroupId,
    [StringLength(40)] string? AssignmentType,
    Guid? AssignedTechnicianId,
    IReadOnlyCollection<Guid>? AssignedTechnicianIds,
    Guid? LeadTechnicianId,
    [StringLength(2000)] string? AssignmentNotes,
    [StringLength(200)] string Title,
    [StringLength(4000)] string? Description,
    [StringLength(40)] string? Priority,
    DateTime? DueDate,
    [StringLength(50)] string? WorkType,
    bool IsPreventiveMaintenance,
    Guid? PmTemplateId,
    bool NotifyClient = true,
    [StringLength(256)] string? ClientContactEmail = null,
    Guid? TemplateId = null,
    Guid? ParentWorkOrderId = null,
    Guid? FollowUpOfWorkOrderId = null,
    Guid? ProjectId = null);

public sealed record UpdateWorkOrderRequest(
    Guid ClientId,
    Guid? BranchId,
    Guid? SiteId,
    Guid? SystemId,
    Guid? AssetId,
    Guid? AssignmentGroupId,
    string? AssignmentType,
    Guid? AssignedTechnicianId,
    IReadOnlyCollection<Guid>? AssignedTechnicianIds,
    Guid? LeadTechnicianId,
    string? AssignmentNotes,
    string Title,
    string? Description,
    string? Priority,
    string? Status,
    DateTime? DueDate,
    string? WorkType,
    Guid? PmTemplateId,
    bool NotifyClient = true,
    string? ClientContactEmail = null);

public sealed record AssignWorkOrderRequest(Guid? TechnicianId, Guid? AssignmentGroupId, string? Notes);
public sealed record AttachPmTemplateRequest(Guid? PmTemplateId);
public sealed record UpdateWorkOrderChecklistItemRequest(string? ResponseValue, string? Remarks, bool IsCompleted);

public sealed record CompleteWorkOrderRequest(string WorkDoneNotes, DateTime? CompletedAt, Guid? TechnicianId, Guid? AssignmentGroupId, string? ReportSummary, string? AnswersJson, bool AdminOverride = false, string? OverrideReason = null);

public sealed record AcknowledgeWorkOrderRequest(string AcknowledgedByName, string? Comments, DateTime? AcknowledgementDate);
public sealed record AddWorkOrderCommentRequest(string Message);
public sealed record WorkOrderLocationRequest(decimal? Latitude, decimal? Longitude);
public sealed record PatchWorkOrderStatusRequest(string? Status, Guid? WorkflowStatusId);
public sealed record BulkAssignWorkOrdersRequest(IReadOnlyCollection<Guid> WorkOrderIds, Guid? TechnicianId, string? Notes);
public sealed record BulkCloseWorkOrdersRequest(IReadOnlyCollection<Guid> WorkOrderIds);
public sealed record BulkOperationFailure(Guid WorkOrderId, string Reason);
public sealed record BulkOperationResultResponse(IReadOnlyCollection<Guid> Succeeded, IReadOnlyCollection<BulkOperationFailure> Failed);

public sealed record WorkOrderResponse(
    Guid Id,
    Guid? BranchId,
    string? BranchName,
    Guid? ClientId,
    string? ClientName,
    Guid? SiteId,
    string? SiteName,
    Guid? AssetId,
    string? AssetName,
    Guid? AssignmentGroupId,
    string? AssignmentGroupName,
    string WorkOrderNumber,
    string Title,
    string? Description,
    string Priority,
    string Status,
    string WorkType,
    string SlaStatus,
    string AssignmentType,
    Guid? AssignedTechnicianId,
    string? AssignedTechnicianName,
    IReadOnlyCollection<Guid> AssignedTechnicianIds,
    Guid? LeadTechnicianId,
    string? LeadTechnicianName,
    DateTime? DueDate,
    DateTime CreatedAt,
    DateTime? WorkStartedAt,
    DateTime? ArrivalAt,
    DateTime? DepartureAt,
    DateTime? CompletedAt,
    string? WorkDoneNotes,
    string? JobCardNotes,
    DateTime? SlaResponseDeadline,
    DateTime? SlaResolutionDeadline,
    bool SlaResponseBreached,
    bool SlaResolutionBreached,
    DateTime? SlaResponseBreachedAt,
    DateTime? SlaResolutionBreachedAt,
    string? AcknowledgedByName,
    string? AcknowledgementComments,
    DateTime? AcknowledgementDate,
    bool IsPreventiveMaintenance,
    string? HoldReason = null,
    string? HoldNote = null,
    string AssignmentStatus = "Unassigned",
    string? AssignmentNotes = null,
    IReadOnlyCollection<WorkOrderTechnicianAssignmentResponse>? TechnicianAssignments = null,
    string? AssignmentSummary = null,
    bool IsUnassigned = false,
    Guid? PmTemplateId = null,
    string? PmTemplateName = null,
    Guid? PreventiveMaintenancePlanId = null,
    Guid? SystemId = null,
    string? SystemName = null,
    IReadOnlyCollection<WorkOrderChecklistItemResponse>? ChecklistItems = null,
    int? PmTemplateVersion = null,
    DateTime? ScheduledStartAt = null,
    DateTime? ScheduledEndAt = null,
    bool NotifyClient = true,
    string? ClientContactEmail = null,
    string? BillingStatus = null,
    bool? IsBillable = null,
    string? ClientContactPerson = null,
    string? ClientContactPhone = null,
    string? ClientSlaPlan = null,
    string? SiteCode = null,
    string? SiteContactPerson = null,
    string? SiteContactPhone = null,
    string? SiteOperatingHours = null,
    string? SiteAccessNotes = null,
    string? SiteLocation = null,
    decimal? SiteLatitude = null,
    decimal? SiteLongitude = null,
    decimal? EstimatedLabourHours = null,
    string? AssetCode = null,
    string? AssetType = null,
    string? AssetCategoryName = null,
    string? AssetSerialNumber = null,
    string? AssetManufacturer = null,
    string? AssetModel = null,
    string? AssetLocation = null,
    DateTime? AssetWarrantyExpiryDate = null,
    DateTime? AssetLastServiceDate = null,
    DateTime? AssetNextServiceDate = null,
    string? AssetWarrantyStatus = null,
    string? AssetCriticality = null,
    IReadOnlyCollection<string>? AvailableTransitions = null,
    Guid? ContractId = null,
    string? ContractNumber = null,
    string? ContractStatus = null,
    string? CoverageStatus = null,
    string? BillingModel = null,
    string? RateCardName = null,
    DateTime? ContractEndDate = null,
    string? ContractRenewalRisk = null,
    string? PricingNotes = null,
    decimal? EstimatedCharge = null,
    decimal? FinalCharge = null,
    string? AppliedSlaSource = null,
    string? AppliedRateSource = null,
    string? TrackingToken = null,
    Guid? WorkOrderTemplateId = null,
    Guid? QuoteId = null,
    string? QuoteNumber = null,
    Guid? ParentWorkOrderId = null,
    int ChildWorkOrderCount = 0,
    string? HoldType = null,
    DateTime? HoldSince = null,
    IReadOnlyCollection<PlannedMaterialResponse>? PlannedMaterials = null,
    Guid? WorkflowStatusId = null,
    bool IsRepeatVisit = false,
    int RepeatVisitCount = 0,
    Guid? FollowUpOfWorkOrderId = null,
    string? FollowUpOfWorkOrderNumber = null,
    IReadOnlyCollection<WorkOrderLinkResponse>? FollowUps = null,
    bool FollowUpRequested = false);

/// <summary>A linked job, as shown on the job it is linked to.</summary>
public sealed record WorkOrderLinkResponse(Guid Id, string WorkOrderNumber, string Status, DateTime CreatedAt);

public sealed record PlannedMaterialResponse(Guid MaterialId, string MaterialName, decimal Quantity);
public sealed record HoldWorkOrderRequest(string? Reason, string? HoldType);

public sealed record WorkOrderSlaStatusResponse(
    Guid WorkOrderId,
    string Priority,
    string? PolicyName,
    DateTime? ResponseDeadline,
    DateTime? ResolutionDeadline,
    bool ResponseBreached,
    bool ResolutionBreached,
    DateTime? ResponseBreachedAt,
    DateTime? ResolutionBreachedAt,
    int? ResponseMinutesRemaining,
    int? ResolutionMinutesRemaining,
    string Status,
    bool IsPaused,
    string? PauseReason);

public sealed record WorkOrderChecklistItemResponse(
    Guid Id,
    Guid? PmTemplateQuestionId,
    string? SectionName,
    string QuestionText,
    string InputType,
    bool IsRequired,
    int SortOrder,
    string? ResponseValue,
    string? Remarks,
    bool IsCompleted,
    Guid? CompletedByUserId,
    DateTime? CompletedAt,
    IReadOnlyCollection<string> Options,
    int SectionOrder = 0,
    string? HelperText = null,
    string? Unit = null,
    decimal? MinValue = null,
    decimal? MaxValue = null,
    string? DefaultValue = null,
    bool EvidenceRequired = false,
    Guid? EvidenceAttachmentId = null,
    bool TechnicianRemarksAllowed = true,
    bool RequiresNoteOnFail = false);

public sealed record WorkOrderTechnicianAssignmentResponse(
    Guid Id,
    Guid TechnicianId,
    string? TechnicianName,
    bool IsLead,
    string Status,
    DateTime AssignedAt,
    DateTime? AcceptedAt,
    DateTime? ArrivalAt,
    DateTime? DepartureAt,
    string? Notes,
    /// <summary>Most recent arrival; on site when it is later than DepartureAt.</summary>
    DateTime? LastArrivalAt = null);

public sealed record WorkOrderEventResponse(
    Guid Id,
    string EventType,
    string? Status,
    string Message,
    Guid? ActorUserId,
    string? ActorUserName,
    string? OldStatus,
    string? NewStatus,
    string? Reason,
    string? Note,
    decimal? Latitude,
    decimal? Longitude,
    DateTime OccurredAt,
    string? MetadataJson = null);

public sealed record WorkOrderNoteResponse(
    Guid Id,
    Guid WorkOrderId,
    string Content,
    bool IsClientVisible,
    Guid CreatedByUserId,
    string? CreatedByUserName,
    DateTime CreatedAt);

public sealed record CreateWorkOrderNoteRequest(string Content, bool IsClientVisible);

public sealed record RelatedWorkOrderResponse(
    Guid Id,
    string WorkOrderNumber,
    string Status,
    DateTime? CompletedAt,
    string Description,
    string? TechnicianName);

public sealed record RelatedWorkOrdersResponse(
    IReadOnlyCollection<RelatedWorkOrderResponse> SameSite,
    IReadOnlyCollection<RelatedWorkOrderResponse> SameAsset);

public sealed record RepeatVisitCheckResponse(bool IsRepeatVisit, int RepeatVisitCount);

public sealed record WorkOrderDocumentResponse(
    string DocumentType,
    Guid Id,
    string Number,
    string Status,
    string? PdfUrl);

/// <summary>
/// One work order's assignment picture, derived once and used for every assignment-shaped field on
/// <see cref="WorkOrderResponse"/> so that they can never contradict each other.
/// </summary>
public sealed record WorkOrderAssignmentSnapshot(
    IReadOnlyCollection<WorkOrderTechnicianAssignmentResponse> TechnicianAssignments,
    Guid? PrimaryTechnicianId,
    string? PrimaryTechnicianName,
    IReadOnlyCollection<Guid> TechnicianIds,
    string AssignmentStatus,
    string AssignmentSummary,
    bool IsUnassigned);

/// <summary>
/// EC-019 — the single source of truth for "who is assigned to this work order".
///
/// <para><c>work_order_technician_assignments</c> is authoritative. The legacy
/// <c>WorkOrder.AssignedTechnicianId</c>/<c>AssignedTechnicianIdsJson</c> columns are still written
/// and still exposed on the DTO (dispatch, reports, escalation, the SLA monitor and the technician
/// workspace all query the column), but they are never read alongside the table: they only stand in
/// when the table holds no row for the work order at all, and then they are projected into the same
/// shape, so <c>assignedTechnicianId</c>, <c>technicianAssignments</c>, <c>assignmentStatus</c>,
/// <c>assignmentSummary</c> and <c>isUnassigned</c> all describe the same reality.
/// </para>
///
/// <para>Rows in that stand-in state are legacy or other-writer data (the demo seeder, the dispatch
/// board's schedule endpoint). Migration
/// <c>20260824113000_BackfillLegacyWorkOrderTechnicianAssignments</c> converts the existing ones
/// into real assignment rows, and <c>WorkOrderAssignmentWorkflowService.SyncLegacyFieldsAsync</c>
/// keeps the legacy column derived from the table for everything written through the assignment
/// workflow.
/// </para>
/// </summary>
internal static class WorkOrderAssignmentView
{
    private const string UnassignedStatus = "Unassigned";

    public static WorkOrderAssignmentSnapshot Resolve(WorkOrder workOrder)
    {
        var technicianAssignments = workOrder.TechnicianAssignments
            .OrderByDescending(x => x.IsLead)
            .ThenBy(x => x.AssignedAt)
            .Select(x => new WorkOrderTechnicianAssignmentResponse(
                x.Id,
                x.TechnicianId,
                x.Technician?.FullName,
                x.IsLead,
                x.Status,
                x.AssignedAt,
                x.AcceptedAt,
                x.ArrivalAt,
                x.DepartureAt,
                x.Notes,
                x.LastArrivalAt ?? x.ArrivalAt))
            .ToList();

        if (technicianAssignments.Count == 0 && workOrder.AssignedTechnicianId.HasValue)
        {
            // Legacy stand-in: an assignment that only ever reached the work_orders row. Carries an
            // empty Id because no persisted assignment record backs it yet.
            technicianAssignments.Add(new WorkOrderTechnicianAssignmentResponse(
                Guid.Empty,
                workOrder.AssignedTechnicianId.Value,
                workOrder.AssignedTechnician?.FullName,
                true,
                ResolveLegacyTechnicianStatus(workOrder),
                workOrder.CreatedAt,
                null,
                workOrder.ArrivalAt,
                workOrder.DepartureAt,
                null));
        }

        var technicianIds = technicianAssignments.Count > 0
            ? technicianAssignments.Select(x => x.TechnicianId).Distinct().ToList()
            : ParseAssignedTechnicianIds(workOrder.AssignedTechnicianIdsJson).Distinct().ToList();

        var primary = technicianAssignments.FirstOrDefault();

        return new WorkOrderAssignmentSnapshot(
            technicianAssignments,
            primary?.TechnicianId,
            primary?.TechnicianName,
            technicianIds,
            ResolveAssignmentStatus(workOrder, technicianAssignments),
            BuildAssignmentSummary(workOrder.AssignmentGroup?.Name, technicianAssignments),
            !workOrder.AssignmentGroupId.HasValue && technicianAssignments.Count == 0);
    }

    private static string ResolveAssignmentStatus(WorkOrder workOrder, IReadOnlyCollection<WorkOrderTechnicianAssignmentResponse> technicianAssignments)
    {
        var derived = workOrder.AssignmentGroupId.HasValue
            ? "AssignedToGroup"
            : technicianAssignments.Count > 0
                ? "AssignedToTechnician"
                : UnassignedStatus;

        var persisted = workOrder.Assignments
            .OrderByDescending(x => x.AssignedAt)
            .Select(x => x.AssignmentStatus)
            .FirstOrDefault();

        // The persisted assignment-workflow status wins, because it carries the richer lifecycle
        // values (Accepted, InProgress, Completed, Closed) — except when it claims "Unassigned" for
        // a work order that demonstrably has someone on it. A stale row never wins over reality.
        if (string.IsNullOrWhiteSpace(persisted)
            || (string.Equals(persisted, UnassignedStatus, StringComparison.OrdinalIgnoreCase) && !string.Equals(derived, UnassignedStatus, StringComparison.Ordinal)))
        {
            return derived;
        }

        return persisted;
    }

    private static string BuildAssignmentSummary(string? groupName, IReadOnlyCollection<WorkOrderTechnicianAssignmentResponse> technicianAssignments)
    {
        if (technicianAssignments.Count == 0)
        {
            return string.IsNullOrWhiteSpace(groupName) ? UnassignedStatus : groupName;
        }

        var firstTechnicianName = technicianAssignments.First().TechnicianName ?? "Technician";
        if (!string.IsNullOrWhiteSpace(groupName) && technicianAssignments.Count == 1)
        {
            return $"{groupName} -> {firstTechnicianName}";
        }

        if (technicianAssignments.Count == 1)
        {
            return firstTechnicianName;
        }

        return $"{firstTechnicianName} + {technicianAssignments.Count - 1} others";
    }

    /// <summary>
    /// Status for the stand-in row, derived from the work order itself. Mirrors the CASE used by the
    /// original backfill in migration <c>20260502132146_WorkOrderAssignmentWorkflowRefactor</c>.
    /// </summary>
    private static string ResolveLegacyTechnicianStatus(WorkOrder workOrder)
    {
        var status = WorkOrderStatuses.Normalize(workOrder.Status);

        if (string.Equals(status, WorkOrderStatuses.Completed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, WorkOrderStatuses.ClientAccepted, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, WorkOrderStatuses.Closed, StringComparison.OrdinalIgnoreCase))
        {
            return "Completed";
        }

        if (string.Equals(status, WorkOrderStatuses.Cancelled, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, WorkOrderStatuses.Rejected, StringComparison.OrdinalIgnoreCase))
        {
            return "Cancelled";
        }

        if (workOrder.ArrivalAt.HasValue)
        {
            return "Arrived";
        }

        return string.Equals(status, WorkOrderStatuses.InProgress, StringComparison.OrdinalIgnoreCase)
            ? "InProgress"
            : "Pending";
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
}
