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
[Route("api/technician/jobs")]
public sealed class TechnicianJobsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IBranchAccessService branchAccessService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<WorkOrderResponse>>> List(CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var technician = await ResolveTechnicianAsync(cancellationToken);

        var query = QueryWorkOrders(scope);
        if (!IsAdmin)
        {
            if (technician is null)
            {
                return Ok(Array.Empty<WorkOrderResponse>());
            }

            var technicianId = technician.Id;
            query = query.Where(x =>
                x.AssignedTechnicianId == technicianId
                || x.LeadTechnicianId == technicianId
                || x.TechnicianAssignments.Any(assignment => assignment.TechnicianId == technicianId));
        }

        var workOrders = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(workOrders.Select(Map).ToList());
    }

    /// <summary>
    /// The signed-in user's technician profile. Assignments are keyed by technician, not user, so the
    /// technician app needs this to find "my" assignment (to accept a job or record arrival).
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<TechnicianSelfResponse>> Me(CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var technician = await ResolveTechnicianAsync(cancellationToken)
            ?? throw new NotFoundException("No technician profile is linked to this account.");

        return Ok(new TechnicianSelfResponse(technician.Id, technician.FullName));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkOrderResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var technician = await ResolveTechnicianAsync(cancellationToken);

        var query = QueryWorkOrders(scope).Where(x => x.Id == id);
        if (!IsAdmin)
        {
            if (technician is null)
            {
                throw new ForbiddenException("Technicians can only access jobs assigned to them.");
            }

            var technicianId = technician.Id;
            query = query.Where(x =>
                x.AssignedTechnicianId == technicianId
                || x.LeadTechnicianId == technicianId
                || x.TechnicianAssignments.Any(assignment => assignment.TechnicianId == technicianId));
        }

        var workOrder = await query.SingleOrDefaultAsync(cancellationToken)
            ?? throw new ForbiddenException("Technicians can only access jobs assigned to them.");

        return Ok(Map(workOrder));
    }

    private async Task<Technician?> ResolveTechnicianAsync(CancellationToken cancellationToken)
    {
        var query = dbContext.Technicians
            .Where(x => x.TenantId == TenantId && x.IsActive);

        if (UserId != Guid.Empty)
        {
            var byUserId = await query.SingleOrDefaultAsync(x => x.UserId == UserId, cancellationToken);
            if (byUserId is not null)
            {
                return byUserId;
            }
        }

        var userEmail = await dbContext.Users
            .Where(x => x.TenantId == TenantId && x.Id == UserId)
            .Select(x => x.Email)
            .SingleOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(userEmail))
        {
            return await query.SingleOrDefaultAsync(x => x.Email == userEmail, cancellationToken);
        }

        return null;
    }

    private IQueryable<WorkOrder> QueryWorkOrders(BranchQueryScope scope) =>
        dbContext.WorkOrders
            .AsNoTracking()
            .Include(x => x.Client)
            .Include(x => x.Site)
            .Include(x => x.Asset)
            .ThenInclude(x => x!.AssetCategory)
            .Include(x => x.Contract)
            .Include(x => x.System)
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

    private static string NormalizeStatus(string status)
        => WorkOrderStatuses.Normalize(status);

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

    private static WorkOrderResponse Map(WorkOrder workOrder)
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
            ContractId: workOrder.ContractId,
            ContractNumber: workOrder.Contract?.ContractNumber,
            ContractStatus: workOrder.Contract?.Status.ToString(),
            CoverageStatus: workOrder.CoverageStatus,
            BillingModel: workOrder.BillingModel,
            ContractEndDate: workOrder.Contract?.EndDate,
            ContractRenewalRisk: workOrder.Contract?.RenewalRisk.ToString(),
            PricingNotes: workOrder.PricingNotes,
            AppliedSlaSource: workOrder.AppliedSlaSource,
            // Field details a technician needs on site (pricing fields stay with the office view).
            ClientContactPerson: workOrder.Client?.ContactPerson,
            ClientContactPhone: workOrder.Client?.ContactPhone,
            SiteCode: workOrder.Site?.SiteCode,
            SiteContactPerson: workOrder.Site?.ContactPerson,
            SiteContactPhone: workOrder.Site?.ContactPhone,
            SiteOperatingHours: workOrder.Site?.OperatingHours,
            SiteAccessNotes: workOrder.Site?.AccessNotes,
            SiteLocation: WorkOrdersController.BuildSiteLocation(workOrder.Site),
            SiteLatitude: workOrder.Site?.Latitude,
            SiteLongitude: workOrder.Site?.Longitude,
            AssetCode: workOrder.Asset?.AssetCode,
            AssetType: workOrder.Asset?.AssetType,
            AssetCategoryName: workOrder.Asset?.AssetCategory?.Name,
            AssetSerialNumber: workOrder.Asset?.SerialNumber,
            AssetManufacturer: workOrder.Asset?.Manufacturer,
            AssetModel: workOrder.Asset?.Model,
            AssetLocation: workOrder.Asset?.Location,
            AssetWarrantyExpiryDate: workOrder.Asset?.WarrantyExpiryDate,
            AssetLastServiceDate: workOrder.Asset?.LastPmDate,
            AssetNextServiceDate: workOrder.Asset?.NextPmDate,
            AssetWarrantyStatus: WorkOrdersController.ResolveAssetWarrantyStatus(workOrder.Asset),
            PlannedMaterials: WorkOrdersController.ParsePlannedMaterials(workOrder.PlannedMaterialsJson),
            HoldSince: workOrder.HoldStartedAt,
            IsRepeatVisit: workOrder.IsRepeatVisit,
            RepeatVisitCount: workOrder.RepeatVisitCount);
    }

    private static (string? Reason, string? Note) ResolveActiveHoldDetails(WorkOrder workOrder)
        => WorkOrderHoldReasons.ResolveCurrentDetails(workOrder);
}

public sealed record TechnicianSelfResponse(Guid TechnicianId, string FullName);
