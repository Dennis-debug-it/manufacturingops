using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Ecosys.Shared.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/preventive-maintenance")]
public sealed class PreventiveMaintenanceController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IDocumentNumberingService documentNumberingService,
    IAuditLogService auditLogService,
    IBranchAccessService branchAccessService,
    IPmWorkOrderChecklistService pmWorkOrderChecklistService,
    ITenantTimeZoneService tenantTimeZoneService,
    ILogger<PreventiveMaintenanceController> logger,
    IContractOperationalService? contractOperationalService = null) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<PreventiveMaintenancePlanResponse>>> GetAll([FromQuery] Guid? branchId, CancellationToken cancellationToken)
    {
        // Reading the schedule is for everyone who sees work orders (the Maintenance calendar);
        // changing it stays with settings managers.
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageSettings, PermissionNames.CanViewWorkOrders);
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, branchId, cancellationToken);
        var plans = await Query()
            .WhereAccessible(scope, x => x.BranchId)
            .OrderBy(x => x.NextPmDate)
            .ToListAsync(cancellationToken);

        return Ok(plans.Select(Map).ToList());
    }

    [HttpGet("due")]
    public async Task<ActionResult<IReadOnlyCollection<PreventiveMaintenancePlanResponse>>> GetDue([FromQuery] Guid? branchId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageSettings, PermissionNames.CanViewWorkOrders);
        // "Due today" is a calendar-date decision — use the tenant's local day, not UTC, so a PM
        // due today isn't shown a day late near the UTC/local midnight boundary (F6-02).
        // NextPmDate is a timestamptz, and Npgsql only accepts UTC parameters, so the bound is the
        // tenant's local end-of-day expressed as a UTC instant rather than a bare local date
        // (EC-010): everything strictly before it falls on or before the tenant's current local day.
        var dueBeforeUtc = await tenantTimeZoneService.TenantDayEndUtcAsync(TenantId, cancellationToken);
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, branchId, cancellationToken);
        var plans = await Query()
            .WhereAccessible(scope, x => x.BranchId)
            .Where(x => x.NextPmDate.HasValue && x.NextPmDate.Value < dueBeforeUtc && x.Status == "Active")
            .OrderBy(x => x.NextPmDate)
            .ToListAsync(cancellationToken);

        return Ok(plans.Select(Map).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<PreventiveMaintenancePlanResponse>> Create([FromBody] UpsertPreventiveMaintenancePlanRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        Validate(request);
        var serviceIntervalMonths = request.ResolveServiceIntervalMonths()!.Value;
        var asset = await GetAssetAsync(request.AssetId, cancellationToken);
        var template = await RequireTemplateAsync(request.PmTemplateId, cancellationToken);
        var lastPmDate = DateTimeNormalizationHelper.NormalizeNullableToUtc(request.LastPmDate);
        var nextPmDate = DateTimeNormalizationHelper.NormalizeNullableToUtc(request.NextPmDate);

        var plan = new PreventiveMaintenancePlan
        {
            TenantId = TenantId,
            BranchId = asset.BranchId,
            AssetId = asset.Id,
            PmTemplateId = template.Id,
            Frequency = ToFrequencyLabel(serviceIntervalMonths),
            AutoSchedule = request.AutoSchedule,
            LastPmDate = lastPmDate,
            NextPmDate = nextPmDate ?? CalculateNextPmDate(lastPmDate, serviceIntervalMonths),
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Active" : request.Status.Trim()
        };

        dbContext.PreventiveMaintenancePlans.Add(plan);
        await dbContext.SaveChangesAsync(cancellationToken);

        var persisted = await Query().SingleAsync(x => x.Id == plan.Id, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { id = plan.Id }, Map(persisted));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PreventiveMaintenancePlanResponse>> Update(Guid id, [FromBody] UpsertPreventiveMaintenancePlanRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        Validate(request);
        var serviceIntervalMonths = request.ResolveServiceIntervalMonths()!.Value;
        var asset = await GetAssetAsync(request.AssetId, cancellationToken);
        var template = await RequireTemplateAsync(request.PmTemplateId, cancellationToken);
        var lastPmDate = DateTimeNormalizationHelper.NormalizeNullableToUtc(request.LastPmDate);
        var nextPmDate = DateTimeNormalizationHelper.NormalizeNullableToUtc(request.NextPmDate);

        var plan = await dbContext.PreventiveMaintenancePlans.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Preventive maintenance plan was not found.");

        plan.AssetId = request.AssetId;
        plan.BranchId = asset.BranchId;
        plan.PmTemplateId = template.Id;
        plan.Frequency = ToFrequencyLabel(serviceIntervalMonths);
        plan.AutoSchedule = request.AutoSchedule;
        plan.LastPmDate = lastPmDate;
        plan.NextPmDate = nextPmDate ?? CalculateNextPmDate(lastPmDate, serviceIntervalMonths);
        plan.Status = string.IsNullOrWhiteSpace(request.Status) ? plan.Status : request.Status.Trim();
        if (plan.Status != PmContractGuard.HeldStatus)
        {
            // Taken off hold by hand: the scheduler checks the contract again before the next visit.
            plan.HoldReason = null;
            plan.HeldAt = null;
            plan.LapsedContractId = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var persisted = await Query().SingleAsync(x => x.Id == plan.Id, cancellationToken);
        return Ok(Map(persisted));
    }

    /// <summary>
    /// Raises the plan's next PM work order now. If the asset's contract has lapsed the visit is not
    /// free: it is refused unless <paramref name="chargeable"/> is set, and then raised as a chargeable
    /// visit priced at the client's standard rates.
    /// </summary>
    [HttpPost("{id:guid}/generate-workorder")]
    public async Task<ActionResult<WorkOrderResponse>> GenerateWorkOrder(Guid id, CancellationToken cancellationToken, [FromQuery] bool chargeable = false)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        var plan = await Query().SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Preventive maintenance plan was not found.");

        if (plan.Asset is null)
        {
            throw new BusinessRuleException("Plan asset was not found.");
        }

        // Idempotency guard (F6-01): don't generate a second work order while one is still open for
        // this plan — matches the background PmSchedulerWorker so a double-click or retry can't
        // create duplicate PM work orders.
        var hasOpenWorkOrder = await dbContext.WorkOrders.AnyAsync(
            x => x.PreventiveMaintenancePlanId == plan.Id
                && x.Status != "Completed"
                && x.Status != "Cancelled",
            cancellationToken);
        if (hasOpenWorkOrder)
        {
            throw new BusinessRuleException("An open preventive-maintenance work order already exists for this plan. Complete or cancel it before generating another.");
        }

        var today = DateTime.UtcNow;
        var coverDate = plan.NextPmDate is { } due && due.Date > today.Date ? due : today;
        var cover = await PmContractGuard.CheckAsync(dbContext, plan, coverDate, cancellationToken);
        var lapsed = cover.Outcome == PmContractOutcome.Lapsed;
        if (lapsed && !chargeable)
        {
            throw new BusinessRuleException(
                $"{PmContractGuard.LapseText(cover.Contract!)}, so this asset is not under contract. Renew the contract, raise this as a chargeable visit, or set the schedule to continue without a contract.");
        }
        var notUnderContract = lapsed || cover.Outcome == PmContractOutcome.ContinuingWithoutContract;

        var workOrder = new WorkOrder
        {
            TenantId = TenantId,
            BranchId = plan.BranchId,
            ClientId = plan.Asset.ClientId,
            AssetId = plan.AssetId,
            PreventiveMaintenancePlanId = plan.Id,
            PmTemplateId = plan.PmTemplateId,
            WorkOrderNumber = await documentNumberingService.GenerateAsync(TenantId, plan.BranchId, DocumentTypes.WorkOrder, cancellationToken),
            Title = notUnderContract
                ? $"PM (chargeable) - {plan.Asset.AssetName} - {plan.Frequency}"
                : $"PM - {plan.Asset.AssetName} - {plan.Frequency}",
            Description = notUnderContract
                ? $"Preventive maintenance for asset '{plan.Asset.AssetName}' on {plan.Frequency} cycle. Not under contract (contract {cover.Contract!.ContractNumber} has lapsed): chargeable at the client's standard rates."
                : $"Preventive maintenance for asset '{plan.Asset.AssetName}' on {plan.Frequency} cycle.",
            Priority = "Medium",
            Status = "Open",
            WorkType = WorkOrderTypes.PreventiveMaintenance,
            DueDate = plan.NextPmDate,
            IsPreventiveMaintenance = true
        };

        if (notUnderContract && contractOperationalService is not null)
        {
            // Stamps the cover and pricing on the job, so a visit off contract is priced as chargeable.
            await contractOperationalService.ApplyOperationalSnapshotAsync(workOrder, cancellationToken);
        }

        dbContext.WorkOrders.Add(workOrder);
        await dbContext.SaveChangesAsync(cancellationToken);

        var copiedChecklistCount = 0;
        if (plan.PmTemplateId.HasValue)
        {
            copiedChecklistCount = await pmWorkOrderChecklistService.AttachPmTemplateToWorkOrderAsync(
                workOrder.Id,
                plan.PmTemplateId.Value,
                TenantId,
                UserId,
                cancellationToken: cancellationToken);
        }
        else
        {
            logger.LogWarning(
                "PM schedule {ScheduleId} has no template assigned. Work order {WorkOrderId} generated without checklist for tenant {TenantId}.",
                plan.Id,
                workOrder.Id,
                TenantId);
        }

        plan.LastPmDate = DateTime.UtcNow;
        var nextIntervalMonths = ToServiceIntervalMonths(plan.Frequency);
        if (nextIntervalMonths.HasValue)
        {
            // TenantTodayAsync returns a wall-clock date (Kind=Unspecified); NextPmDate is a
            // timestamptz, and Npgsql rejects a non-UTC parameter. PM dates are stored date-only at
            // midnight, so tag the tenant's local calendar date as UTC rather than shifting it,
            // which would move the scheduled day across the local/UTC boundary (EC-010).
            var seedDate = plan.NextPmDate?.Date
                ?? DateTimeNormalizationHelper.NormalizeToUtc(await tenantTimeZoneService.TenantTodayAsync(TenantId, cancellationToken));
            plan.NextPmDate = seedDate.AddMonths(nextIntervalMonths.Value);
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Work order created",
            nameof(WorkOrder),
            workOrder.Id.ToString(),
            notUnderContract
                ? $"Generated chargeable PM work order '{workOrder.WorkOrderNumber}' from plan '{plan.Id}' (contract {cover.Contract!.ContractNumber} lapsed)."
                : $"Generated PM work order '{workOrder.WorkOrderNumber}' from plan '{plan.Id}'.",
            cancellationToken);

        logger.LogInformation(
            "Generated PM WorkOrder {WorkOrderNumber} ({WorkOrderId}) from schedule {ScheduleId} using template {TemplateId}; copied {Count} checklist items for tenant {TenantId}.",
            workOrder.WorkOrderNumber,
            workOrder.Id,
            plan.Id,
            plan.PmTemplateId,
            copiedChecklistCount,
            TenantId);

        var persisted = await dbContext.WorkOrders
            .Include(x => x.Client)
            .Include(x => x.Asset)
            .Include(x => x.AssignedTechnician)
            .Include(x => x.LeadTechnician)
            .Include(x => x.AssignmentGroup)
            .Include(x => x.Branch)
            .Include(x => x.Site)
            .Include(x => x.PmTemplate)
            .Include(x => x.ChecklistItems)
            .SingleAsync(x => x.Id == workOrder.Id, cancellationToken);

        return Ok(new WorkOrderResponse(
            persisted.Id,
            persisted.BranchId,
            persisted.Branch?.Name,
            persisted.ClientId,
            persisted.Client?.ClientName,
            persisted.SiteId,
            persisted.Site?.SiteName,
            persisted.AssetId,
            persisted.Asset?.AssetName,
            persisted.AssignmentGroupId,
            persisted.AssignmentGroup?.Name,
            persisted.WorkOrderNumber,
            persisted.Title,
            persisted.Description,
            persisted.Priority,
            persisted.Status,
            persisted.WorkType,
            persisted.SlaResolutionBreached ? "Resolution Breached" : persisted.SlaResponseBreached ? "Response Breached" : persisted.SlaResolutionDeadline.HasValue ? "On Track" : "Not Configured",
            persisted.AssignmentType,
            persisted.AssignedTechnicianId,
            persisted.AssignedTechnician?.FullName,
            [],
            persisted.LeadTechnicianId,
            persisted.LeadTechnician?.FullName,
            persisted.DueDate,
            persisted.CreatedAt,
            persisted.WorkStartedAt,
            persisted.ArrivalAt,
            persisted.DepartureAt,
            persisted.CompletedAt,
            persisted.WorkDoneNotes,
            persisted.JobCardNotes,
            persisted.SlaResponseDeadline,
            persisted.SlaResolutionDeadline,
            persisted.SlaResponseBreached,
            persisted.SlaResolutionBreached,
            persisted.SlaResponseBreachedAt,
            persisted.SlaResolutionBreachedAt,
            persisted.AcknowledgedByName,
            persisted.AcknowledgementComments,
            persisted.AcknowledgementDate,
            persisted.IsPreventiveMaintenance,
            PmTemplateId: persisted.PmTemplateId,
            PmTemplateName: persisted.PmTemplateNameSnapshot ?? persisted.PmTemplate?.Name,
            PreventiveMaintenancePlanId: persisted.PreventiveMaintenancePlanId,
            ChecklistItems: persisted.ChecklistItems
                .OrderBy(x => x.SectionOrder)
                .ThenBy(x => x.SectionName ?? string.Empty)
                .ThenBy(x => x.SortOrder)
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
            PmTemplateVersion: persisted.PmTemplateVersionSnapshot));
    }

    /// <summary>
    /// The client will pay per visit after their contract lapsed: the schedule keeps raising visits
    /// (marked chargeable) instead of holding. Takes a held schedule off hold.
    /// </summary>
    [HttpPost("{id:guid}/continue-without-contract")]
    public Task<ActionResult<PreventiveMaintenancePlanResponse>> ContinueWithoutContract(Guid id, CancellationToken cancellationToken) =>
        SetContinueWithoutContractAsync(id, true, cancellationToken);

    /// <summary>Back to the default: the schedule holds whenever the asset's contract lapses.</summary>
    [HttpDelete("{id:guid}/continue-without-contract")]
    public Task<ActionResult<PreventiveMaintenancePlanResponse>> StopContinuingWithoutContract(Guid id, CancellationToken cancellationToken) =>
        SetContinueWithoutContractAsync(id, false, cancellationToken);

    private async Task<ActionResult<PreventiveMaintenancePlanResponse>> SetContinueWithoutContractAsync(Guid id, bool enabled, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        var plan = await dbContext.PreventiveMaintenancePlans.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Preventive maintenance plan was not found.");
        plan.ContinueWithoutContract = enabled;
        if (enabled && plan.Status == PmContractGuard.HeldStatus)
            PmContractGuard.Resume(plan, DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, enabled ? "pm.continue-without-contract" : "pm.hold-on-contract-lapse",
            nameof(PreventiveMaintenancePlan), plan.Id.ToString(),
            enabled ? "PM schedule set to continue without a contract (visits chargeable)." : "PM schedule set to hold when the asset's contract lapses.",
            cancellationToken);
        var persisted = await Query().SingleAsync(x => x.Id == plan.Id, cancellationToken);
        return Ok(Map(persisted));
    }

    private IQueryable<PreventiveMaintenancePlan> Query() =>
        dbContext.PreventiveMaintenancePlans
            .Include(x => x.Asset)
            .ThenInclude(x => x!.Client)
            .Include(x => x.Branch)
            .Include(x => x.PmTemplate)
            .Where(x => x.TenantId == TenantId);

    private async Task<Asset> GetAssetAsync(Guid assetId, CancellationToken cancellationToken) =>
        await dbContext.Assets.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Status != "Inactive" && x.Id == assetId, cancellationToken)
        ?? throw new BusinessRuleException("Asset was not found for this tenant.");

    private static void Validate(UpsertPreventiveMaintenancePlanRequest request)
    {
        if (!request.ResolveServiceIntervalMonths().HasValue)
        {
            throw new BusinessRuleException("Service interval is required.");
        }
    }

    private static PreventiveMaintenancePlanResponse Map(PreventiveMaintenancePlan plan) =>
        new(
            plan.Id,
            plan.BranchId,
            plan.Branch?.Name,
            plan.AssetId,
            plan.Asset?.AssetName,
            plan.Asset?.ClientId,
            plan.Asset?.Client?.ClientName,
            plan.PmTemplateId,
            plan.PmTemplate?.Name,
            plan.Frequency,
            ToServiceIntervalMonths(plan.Frequency),
            plan.AutoSchedule,
            plan.LastPmDate,
            plan.NextPmDate,
            plan.Status,
            plan.CreatedAt)
        {
            ContinueWithoutContract = plan.ContinueWithoutContract,
            HoldReason = plan.HoldReason,
            HeldAt = plan.HeldAt,
            LapsedContractId = plan.LapsedContractId,
        };

    private async Task<PmTemplate> RequireTemplateAsync(Guid? pmTemplateId, CancellationToken cancellationToken)
    {
        if (!pmTemplateId.HasValue)
        {
            throw new BusinessRuleException("PM template is required.");
        }

        var template = await pmWorkOrderChecklistService.GetActiveTemplateAsync(TenantId, pmTemplateId.Value, cancellationToken);
        if (!string.Equals(template.WorkType, "PreventiveMaintenance", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("Select a preventive maintenance checklist for the PM schedule.");
        }

        return template;
    }

    private static IReadOnlyCollection<string> ParseOptions(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson))
        {
            return [];
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(optionsJson) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string ToFrequencyLabel(int serviceIntervalMonths) =>
        serviceIntervalMonths switch
        {
            1 => "Monthly",
            3 => "Quarterly",
            6 => "Semi-Annual",
            12 => "Annual",
            _ => throw new BusinessRuleException("Service interval must be Monthly, Quarterly, Semi-Annual, or Annual.")
        };

    private static int? ToServiceIntervalMonths(string? frequency) =>
        string.IsNullOrWhiteSpace(frequency)
            ? null
            : frequency.Trim().ToLowerInvariant() switch
            {
                "monthly" => 1,
                "quarterly" => 3,
                "semi-annual" => 6,
                "semi annual" => 6,
                "semiannual" => 6,
                "annual" => 12,
                _ => null
            };

    private static DateTime? CalculateNextPmDate(DateTime? lastPmDate, int serviceIntervalMonths) =>
        lastPmDate?.AddMonths(serviceIntervalMonths);
}

public sealed record UpsertPreventiveMaintenancePlanRequest(
    Guid AssetId,
    Guid? PmTemplateId,
    int? ServiceIntervalMonths,
    string? Frequency,
    bool AutoSchedule,
    DateTime? LastPmDate,
    DateTime? NextPmDate,
    string? Status)
{
    public int? ResolveServiceIntervalMonths()
    {
        if (ServiceIntervalMonths.HasValue)
        {
            return ServiceIntervalMonths.Value is 1 or 3 or 6 or 12 ? ServiceIntervalMonths.Value : null;
        }

        if (string.IsNullOrWhiteSpace(Frequency))
        {
            return null;
        }

        return Frequency.Trim().ToLowerInvariant() switch
        {
            "monthly" => 1,
            "quarterly" => 3,
            "semi-annual" => 6,
            "semi annual" => 6,
            "semiannual" => 6,
            "annual" => 12,
            _ => null
        };
    }
}

public sealed record PreventiveMaintenancePlanResponse(
    Guid Id,
    Guid? BranchId,
    string? BranchName,
    Guid AssetId,
    string? AssetName,
    Guid? ClientId,
    string? ClientName,
    Guid? PmTemplateId,
    string? PmTemplateName,
    string Frequency,
    int? ServiceIntervalMonths,
    bool AutoSchedule,
    DateTime? LastPmDate,
    DateTime? NextPmDate,
    string Status,
    DateTime CreatedAt)
{
    /// <summary>Visits continue (chargeable) after the asset's contract lapses, instead of holding.</summary>
    public bool ContinueWithoutContract { get; init; }
    /// <summary>Set while Status is ContractLapsed: why, since when, and which contract.</summary>
    public string? HoldReason { get; init; }
    public DateTime? HeldAt { get; init; }
    public Guid? LapsedContractId { get; init; }
}
