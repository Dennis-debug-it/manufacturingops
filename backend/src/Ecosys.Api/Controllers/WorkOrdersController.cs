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
    IContractOperationalService? contractOperationalService = null) : TenantAwareControllerBase(tenantContext)
{
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
    public async Task<ActionResult<IReadOnlyCollection<WorkOrderResponse>>> GetAll(
        [FromQuery] Guid? branchId,
        [FromQuery] string? status,
        [FromQuery] string? workType,
        [FromQuery] Guid? assignmentGroupId,
        [FromQuery] Guid? technicianId,
        [FromQuery] bool? unassigned,
        [FromQuery] string? assignmentStatus,
        [FromQuery] string? search,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken)
    {
        EnsureGeneralWorkspaceAccess(PermissionNames.CanViewWorkOrders);
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, branchId, cancellationToken);

        var workOrders = await QueryWorkOrders(scope)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var filtered = workOrders
            .Where(x => string.IsNullOrWhiteSpace(status) || string.Equals(workOrderLifecycleService.NormalizeStatus(x.Status), workOrderLifecycleService.NormalizeStatus(status), StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(workType) || string.Equals(WorkOrderTypes.Normalize(x.WorkType, WorkOrderTypes.CorrectiveMaintenance), WorkOrderTypes.Normalize(workType, WorkOrderTypes.CorrectiveMaintenance), StringComparison.OrdinalIgnoreCase))
            .Where(x => !assignmentGroupId.HasValue || x.AssignmentGroupId == assignmentGroupId)
            .Where(x => !technicianId.HasValue || x.TechnicianAssignments.Any(assignment => assignment.TechnicianId == technicianId))
            .Where(x => !unassigned.HasValue || (unassigned.Value
                ? x.AssignmentGroupId is null && !x.TechnicianAssignments.Any()
                : x.AssignmentGroupId is not null || x.TechnicianAssignments.Any()))
            .Where(x => string.IsNullOrWhiteSpace(assignmentStatus) || string.Equals(GetAssignmentStatus(x), assignmentStatus, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(search) || $"{x.WorkOrderNumber} {x.Title} {x.Client?.ClientName} {x.Asset?.AssetName} {BuildAssignmentSummary(x)}".Contains(search, StringComparison.OrdinalIgnoreCase))
            .Where(x => !fromDate.HasValue || x.CreatedAt >= fromDate.Value)
            .Where(x => !toDate.HasValue || x.CreatedAt <= toDate.Value)
            .Select(Map)
            .ToList();

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
    public async Task<ActionResult<WorkOrderResponse>> Create([FromBody] CreateWorkOrderRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanCreateWorkOrders);
        await licenseGuardService.EnsureCanCreateWorkOrderAsync(TenantId, cancellationToken);
        ValidateCreate(request);
        var normalizedWorkType = WorkOrderTypes.Normalize(request.WorkType);
        await EnsureClientExistsAsync(request.ClientId, cancellationToken);
        var asset = await EnsureAssetExistsAsync(request.AssetId, request.ClientId, cancellationToken);
        await EnsureSystemExistsAsync(request.SystemId, request.ClientId, request.SiteId, cancellationToken);
        await EnsurePmTemplateIsValidAsync(normalizedWorkType, request.PmTemplateId, cancellationToken);

        var branchId = await ResolveWorkOrderBranchIdAsync(request.BranchId, asset?.BranchId, cancellationToken);

        var normalizedAssignmentType = NormalizeAssignmentType(request.AssignmentType, request.AssignmentGroupId, request.AssignedTechnicianId, request.AssignedTechnicianIds);
        string? siteAccessNotes = null;
        if (request.SiteId.HasValue)
        {
            siteAccessNotes = await dbContext.Sites
                .Where(x => x.TenantId == TenantId && x.Id == request.SiteId.Value)
                .Select(x => x.AccessNotes)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var workOrder = new WorkOrder
        {
            TenantId = TenantId,
            BranchId = branchId,
            ClientId = request.ClientId,
            SiteId = request.SiteId,
            SystemId = request.SystemId,
            AssetId = request.AssetId,
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
            JobCardNotes = !string.IsNullOrWhiteSpace(siteAccessNotes) ? siteAccessNotes : null,
            NotifyClient = request.NotifyClient,
            ClientContactEmail = request.ClientContactEmail?.Trim()
        };

        if (contractOperationalService is not null)
        {
            await contractOperationalService.ApplyOperationalSnapshotAsync(workOrder, cancellationToken);
        }
        else
        {
            await slaService.ApplyDefinitionAsync(workOrder, cancellationToken);
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
            $"Created work order '{workOrder.WorkOrderNumber}'.",
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

        await workOrderLifecycleService.ArriveAsync(TenantId, workOrder.Id, UserId, new Infrastructure.Services.WorkOrderLocationRequest(request.Latitude, request.Longitude), cancellationToken);
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

        await workOrderLifecycleService.DepartAsync(TenantId, workOrder.Id, UserId, new Infrastructure.Services.WorkOrderLocationRequest(request.Latitude, request.Longitude), cancellationToken);
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
            x.OccurredAt)).ToList());
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

    private static string GetAssignmentStatus(WorkOrder workOrder) =>
        workOrder.Assignments
            .OrderByDescending(x => x.AssignedAt)
            .Select(x => x.AssignmentStatus)
            .FirstOrDefault()
        ?? (workOrder.AssignmentGroupId.HasValue
            ? "AssignedToGroup"
            : workOrder.TechnicianAssignments.Any()
                ? "AssignedToTechnician"
                : "Unassigned");

    private static string BuildAssignmentSummary(WorkOrder workOrder)
    {
        var technicians = workOrder.TechnicianAssignments
            .OrderByDescending(x => x.IsLead)
            .ThenBy(x => x.AssignedAt)
            .ToList();

        if (!technicians.Any())
        {
            return workOrder.AssignmentGroup?.Name ?? "Unassigned";
        }

        var firstTechnicianName = technicians[0].Technician?.FullName ?? workOrder.AssignedTechnician?.FullName ?? "Technician";
        if (!string.IsNullOrWhiteSpace(workOrder.AssignmentGroup?.Name) && technicians.Count == 1)
        {
            return $"{workOrder.AssignmentGroup.Name} -> {firstTechnicianName}";
        }

        if (technicians.Count == 1)
        {
            return firstTechnicianName;
        }

        return $"{firstTechnicianName} + {technicians.Count - 1} others";
    }

    private WorkOrderResponse Map(WorkOrder workOrder)
    {
        var holdDetails = ResolveActiveHoldDetails(workOrder);
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
                x.Notes))
            .ToList();
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
            GetAssignmentStatus(workOrder),
            workOrder.Assignments.OrderByDescending(x => x.AssignedAt).Select(x => x.Notes).FirstOrDefault(),
            technicianAssignments,
            BuildAssignmentSummary(workOrder),
            !workOrder.AssignmentGroupId.HasValue && technicianAssignments.Count == 0,
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
            workOrder.BillingStatus,
            workOrder.IsBillable,
            workOrder.Client?.ContactPerson,
            workOrder.Client?.ContactPhone,
            workOrder.Client?.SlaPlan,
            workOrder.Site?.SiteCode,
            workOrder.Site?.ContactPerson,
            workOrder.Site?.ContactPhone,
            workOrder.Site?.OperatingHours,
            workOrder.Site?.AccessNotes,
            BuildSiteLocation(workOrder.Site),
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
            workOrder.AppliedRateSource);
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

    private static string? BuildSiteLocation(Site? site)
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

    private static string? ResolveAssetWarrantyStatus(Asset? asset)
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
    string? AssignmentType,
    Guid? AssignedTechnicianId,
    IReadOnlyCollection<Guid>? AssignedTechnicianIds,
    Guid? LeadTechnicianId,
    string? AssignmentNotes,
    string Title,
    string? Description,
    string? Priority,
    DateTime? DueDate,
    string? WorkType,
    bool IsPreventiveMaintenance,
    Guid? PmTemplateId,
    bool NotifyClient = true,
    string? ClientContactEmail = null);

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

public sealed record WorkOrderResponse(
    Guid Id,
    Guid? BranchId,
    string? BranchName,
    Guid ClientId,
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
    string? AppliedRateSource = null);

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
    string? Notes);

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
    DateTime OccurredAt);
