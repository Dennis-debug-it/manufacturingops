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
    ILogger<PreventiveMaintenanceController> logger) : TenantAwareControllerBase(tenantContext)
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

        await dbContext.SaveChangesAsync(cancellationToken);

        var persisted = await Query().SingleAsync(x => x.Id == plan.Id, cancellationToken);
        return Ok(Map(persisted));
    }

    /// <summary>
    /// Auto-schedule on: the scheduler raises each visit ahead of its due date. Off: manual trigger
    /// only. For the asset's own schedule (the one its maintenance settings create), the asset's
    /// auto-schedule setting follows, so editing the asset later doesn't undo this.
    /// </summary>
    [HttpPost("{id:guid}/auto-schedule")]
    public async Task<ActionResult<PreventiveMaintenancePlanResponse>> SetAutoSchedule(Guid id, [FromBody] SetAutoScheduleRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        var plan = await Query().SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Preventive maintenance plan was not found.");
        if (request.Enabled && !plan.NextPmDate.HasValue)
            throw new BusinessRuleException("This schedule has no next due date, so it can't run automatically.");
        if (request.Enabled && plan.Status == "Inactive")
            throw new BusinessRuleException("This schedule is inactive.");

        if (plan.AutoSchedule != request.Enabled)
        {
            plan.AutoSchedule = request.Enabled;
            if (plan.PmTemplateId is null && plan.Asset is { } asset)
                asset.AutoSchedulePm = request.Enabled;
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditLogService.LogAsync(TenantId, UserId, request.Enabled ? "pm.auto-schedule.on" : "pm.auto-schedule.off",
                nameof(PreventiveMaintenancePlan), plan.Id.ToString(),
                $"PM schedule set to {(request.Enabled ? "auto-generate work orders" : "manual trigger only")}.", cancellationToken);
        }

        return Ok(Map(plan));
    }

    private IQueryable<PreventiveMaintenancePlan> Query() =>
        dbContext.PreventiveMaintenancePlans
            .Include(x => x.Asset)
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
            plan.PmTemplateId,
            plan.PmTemplate?.Name,
            plan.Frequency,
            ToServiceIntervalMonths(plan.Frequency),
            plan.AutoSchedule,
            plan.LastPmDate,
            plan.NextPmDate,
            plan.Status,
            plan.CreatedAt);

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
    Guid? PmTemplateId,
    string? PmTemplateName,
    string Frequency,
    int? ServiceIntervalMonths,
    bool AutoSchedule,
    DateTime? LastPmDate,
    DateTime? NextPmDate,
    string Status,
    DateTime CreatedAt);
