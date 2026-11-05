using System.ComponentModel.DataAnnotations;
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

/// <summary>Factory planned maintenance: PM plans (calendar or meter), skip and defer, the workload forecast and PM compliance.</summary>
[ApiController]
[Authorize]
[Route("api/factory/pm")]
public sealed class FactoryPmController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IOrgScopeService orgScopeService,
    IFactoryPmService pmService) : TenantAwareControllerBase(tenantContext)
{
    private static readonly string[] Finished =
        [WorkOrderStatuses.Completed, WorkOrderStatuses.Verified, WorkOrderStatuses.Closed, WorkOrderStatuses.Cancelled, WorkOrderStatuses.Rejected];

    private FactoryActor Actor => new(UserId, UserDisplayName ?? "Unknown");

    [HttpGet("plans")]
    public async Task<ActionResult<IReadOnlyCollection<PmPlanSummary>>> Plans([FromQuery] Guid? orgNodeId, [FromQuery] Guid? assetId, [FromQuery] bool includeInactive, [FromQuery] string? search, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var query = await ScopedAsync(dbContext.PreventiveMaintenancePlans.Where(x => x.TenantId == TenantId && x.OrgNodeId != null), orgNodeId, cancellationToken);
        if (!includeInactive) query = query.Where(x => x.Status == "Active");
        if (assetId.HasValue) query = query.Where(x => x.AssetId == assetId || x.RouteAssets.Any(r => r.AssetId == assetId));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => (x.Name ?? string.Empty).ToLower().Contains(term) || x.Asset!.AssetCode.ToLower().Contains(term) || x.Asset.AssetName.ToLower().Contains(term));
        }

        var rows = await query.Select(SummaryProjection).ToListAsync(cancellationToken);
        var open = await OpenJobsAsync(rows.Select(x => x.Id).ToList(), cancellationToken);
        return Ok(rows.Select(x => x with { OpenJobId = open.GetValueOrDefault(x.Id).Id, OpenJobNumber = open.GetValueOrDefault(x.Id).Number })
            .OrderBy(x => x.NextDueDate ?? DateTime.MaxValue).ThenBy(x => x.Name).ToList());
    }

    [HttpGet("plans/{id:guid}")]
    public async Task<ActionResult<PmPlanDetail>> Get(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanView();
        return Ok(await DetailAsync(id, cancellationToken));
    }

    [HttpPost("plans")]
    public async Task<ActionResult<PmPlanDetail>> Create([FromBody] SavePmPlanRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        await EnsureAssetInScopeAsync(request.AssetId, cancellationToken);
        var plan = await pmService.SaveAsync(TenantId, null, ToCommand(request), cancellationToken);
        return Ok(await DetailAsync(plan.Id, cancellationToken));
    }

    [HttpPut("plans/{id:guid}")]
    public async Task<ActionResult<PmPlanDetail>> Update(Guid id, [FromBody] SavePmPlanRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        await LoadAsync(id, cancellationToken);
        await EnsureAssetInScopeAsync(request.AssetId, cancellationToken);
        await pmService.SaveAsync(TenantId, id, ToCommand(request), cancellationToken);
        return Ok(await DetailAsync(id, cancellationToken));
    }

    [HttpPost("plans/{id:guid}/active")]
    public async Task<ActionResult<PmPlanDetail>> SetActive(Guid id, [FromBody] SetPmActiveRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var plan = await LoadAsync(id, cancellationToken);
        plan.Status = request.Active ? "Active" : "Inactive";
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await DetailAsync(id, cancellationToken));
    }

    /// <summary>Raises the plan's job now, ahead of its due date.</summary>
    [HttpPost("plans/{id:guid}/generate")]
    public async Task<ActionResult<PmGenerated>> Generate(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var plan = await LoadAsync(id, cancellationToken);
        var job = await pmService.GenerateNowAsync(plan, Actor, cancellationToken);
        return Ok(new PmGenerated(job.Id, job.WorkOrderNumber));
    }

    [HttpPost("plans/{id:guid}/skip")]
    public async Task<ActionResult<PmPlanDetail>> Skip(Guid id, [FromBody] SkipPmRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var plan = await LoadAsync(id, cancellationToken);
        await pmService.SkipAsync(plan, Actor, request.Reason ?? string.Empty, cancellationToken);
        return Ok(await DetailAsync(id, cancellationToken));
    }

    [HttpPost("plans/{id:guid}/defer")]
    public async Task<ActionResult<PmPlanDetail>> Defer(Guid id, [FromBody] DeferPmRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var plan = await LoadAsync(id, cancellationToken);
        await pmService.DeferAsync(plan, Actor, request.Reason ?? string.Empty, request.NewDueDate, request.NewMeterDue, request.UntilLowCrop, cancellationToken);
        return Ok(await DetailAsync(id, cancellationToken));
    }

    /// <summary>PM hours due per week against technician capacity, for levelling the load.</summary>
    [HttpGet("forecast")]
    public async Task<ActionResult<IReadOnlyList<PmForecastWeek>>> Forecast([FromQuery] Guid? orgNodeId, [FromQuery] int? weeks, CancellationToken cancellationToken)
    {
        EnsureCanView();
        return Ok(await pmService.ForecastAsync(TenantId, await NodesAsync(orgNodeId, cancellationToken), DateTime.UtcNow, weeks ?? 12, cancellationToken));
    }

    /// <summary>PM done on time, late, skipped or overdue over the last N days.</summary>
    [HttpGet("compliance")]
    public async Task<ActionResult<PmComplianceSummary>> Compliance([FromQuery] Guid? orgNodeId, [FromQuery] int? days, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var now = DateTime.UtcNow;
        return Ok(await pmService.ComplianceAsync(TenantId, await NodesAsync(orgNodeId, cancellationToken), now.AddDays(-Math.Clamp(days ?? 90, 7, 730)), now.AddDays(1), cancellationToken));
    }

    private static SaveFactoryPmPlan ToCommand(SavePmPlanRequest r) => new(
        r.Name ?? string.Empty, r.AssetId, r.PmTemplateId, r.TriggerType ?? PmTriggerTypes.Calendar, r.FrequencyUnit ?? PmFrequencyUnits.Months, r.FrequencyInterval ?? 1,
        r.NextDueDate, r.MeterId, r.MeterInterval, r.NextMeterDue, r.ScheduleMode ?? PmScheduleModes.Floating, r.TolerancePercent ?? 10, r.DaysBeforeDue ?? 3,
        r.Priority ?? "Medium", r.EstimatedHours, r.NeedsLineStop, r.Basis, r.RouteAssetIds, r.AutoSchedule ?? true);

    private async Task<PreventiveMaintenancePlan> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        var plan = await dbContext.PreventiveMaintenancePlans.Include(x => x.Meter)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id && x.OrgNodeId != null, cancellationToken)
            ?? throw new NotFoundException("The PM plan was not found.");
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, plan.OrgNodeId!.Value, cancellationToken);
        return plan;
    }

    private async Task EnsureAssetInScopeAsync(Guid assetId, CancellationToken cancellationToken)
    {
        var node = await dbContext.Assets.Where(x => x.TenantId == TenantId && x.Id == assetId).Select(x => x.OrgNodeId).SingleOrDefaultAsync(cancellationToken);
        if (node is { } nodeId)
        {
            await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, nodeId, cancellationToken);
        }
    }

    private async Task<PmPlanDetail> DetailAsync(Guid id, CancellationToken cancellationToken)
    {
        await LoadAsync(id, cancellationToken);
        var summary = await dbContext.PreventiveMaintenancePlans.Where(x => x.Id == id).Select(SummaryProjection).SingleAsync(cancellationToken);
        var open = await OpenJobsAsync([id], cancellationToken);
        summary = summary with { OpenJobId = open.GetValueOrDefault(id).Id, OpenJobNumber = open.GetValueOrDefault(id).Number };
        var plan = await dbContext.PreventiveMaintenancePlans.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.PmTemplateId, Template = x.PmTemplate != null ? x.PmTemplate.Name : null, x.MeterInterval, x.TolerancePercent, x.DaysBeforeDue, x.FrequencyUnit, x.FrequencyInterval, x.AutoSchedule })
            .SingleAsync(cancellationToken);
        var route = await dbContext.PmPlanRouteAssets.Where(x => x.PlanId == id).OrderBy(x => x.Sequence)
            .Select(x => new PmRouteAssetDto(x.AssetId, x.Asset!.AssetCode, x.Asset.AssetName)).ToListAsync(cancellationToken);
        var events = await (from e in dbContext.PmPlanEvents
                            where e.PlanId == id
                            orderby e.OccurredAt descending
                            select new PmEventDto(e.Kind, e.OccurredAt, e.DueDate, e.DueMeterValue, e.OnTime, e.Reason, e.ActorName, e.WorkOrderId,
                                dbContext.WorkOrders.Where(w => w.Id == e.WorkOrderId).Select(w => w.WorkOrderNumber).FirstOrDefault()))
            .Take(50).ToListAsync(cancellationToken);
        return new PmPlanDetail(summary, plan.PmTemplateId, plan.Template, plan.FrequencyUnit, plan.FrequencyInterval, plan.MeterInterval, plan.TolerancePercent, plan.DaysBeforeDue, plan.AutoSchedule, route, events);
    }

    private async Task<Dictionary<Guid, (Guid? Id, string? Number)>> OpenJobsAsync(IReadOnlyCollection<Guid> planIds, CancellationToken cancellationToken) =>
        (await dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId && x.PreventiveMaintenancePlanId != null && planIds.Contains(x.PreventiveMaintenancePlanId.Value) && !Finished.Contains(x.Status))
            .Select(x => new { PlanId = x.PreventiveMaintenancePlanId!.Value, x.Id, x.WorkOrderNumber })
            .ToListAsync(cancellationToken))
        .GroupBy(x => x.PlanId).ToDictionary(g => g.Key, g => ((Guid?)g.First().Id, (string?)g.First().WorkOrderNumber));

    private static readonly System.Linq.Expressions.Expression<Func<PreventiveMaintenancePlan, PmPlanSummary>> SummaryProjection = x => new PmPlanSummary(
        x.Id, x.Name ?? x.Frequency, x.AssetId, x.Asset!.AssetCode, x.Asset.AssetName, x.Asset.Criticality, x.OrgNodeId!.Value, x.OrgNode!.Name,
        x.TriggerType, x.Frequency, x.ScheduleMode, x.NextPmDate, x.NextMeterDue, x.Meter != null ? x.Meter.CurrentValue : null, x.Meter != null ? x.Meter.Unit : null,
        x.LastPmDate, x.Priority, x.EstimatedHours, x.NeedsLineStop, x.IsRoute, x.RouteAssets.Count, x.Basis, x.Status,
        x.DeferReason, x.DeferredAt, x.DeferRisk, null, null);

    private async Task<IQueryable<PreventiveMaintenancePlan>> ScopedAsync(IQueryable<PreventiveMaintenancePlan> query, Guid? orgNodeId, CancellationToken cancellationToken)
    {
        var nodes = await NodesAsync(orgNodeId, cancellationToken);
        return nodes is null ? query : query.Where(x => nodes.Contains(x.OrgNodeId!.Value));
    }

    /// <summary>The org nodes the user may see, narrowed to a chosen section; null means everything.</summary>
    private async Task<IReadOnlyCollection<Guid>?> NodesAsync(Guid? orgNodeId, CancellationToken cancellationToken)
    {
        var scope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        IReadOnlyCollection<Guid>? nodes = scope.Nodes?.ToList();
        if (orgNodeId.HasValue)
        {
            var subtree = await orgScopeService.GetSubtreeAsync(TenantId, orgNodeId.Value, cancellationToken);
            nodes = nodes is null ? subtree.ToList() : subtree.Where(nodes.Contains).ToList();
        }

        return nodes;
    }

    private void EnsureCanView() => userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanViewWorkOrders);

    private void EnsureCanManage() => userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageSettings, PermissionNames.CanAssignWorkOrders);
}

public sealed record PmPlanSummary(
    Guid Id, string Name, Guid AssetId, string AssetCode, string AssetName, string? AssetCriticality, Guid OrgNodeId, string? OrgNodeName,
    string TriggerType, string Frequency, string ScheduleMode, DateTime? NextDueDate, decimal? NextMeterDue, decimal? MeterValue, string? MeterUnit,
    DateTime? LastDoneAt, string Priority, decimal? EstimatedHours, bool NeedsLineStop, bool IsRoute, int RouteAssetCount, string? Basis, string Status,
    string? DeferReason, DateTime? DeferredAt, bool DeferRisk, Guid? OpenJobId, string? OpenJobNumber);

public sealed record PmRouteAssetDto(Guid AssetId, string AssetCode, string AssetName);

public sealed record PmEventDto(string Kind, DateTime OccurredAt, DateTime? DueDate, decimal? DueMeterValue, bool? OnTime, string? Reason, string? ActorName, Guid? WorkOrderId, string? WorkOrderNumber);

public sealed record PmPlanDetail(
    PmPlanSummary Plan, Guid? PmTemplateId, string? PmTemplateName, string FrequencyUnit, int FrequencyInterval, decimal? MeterInterval,
    int TolerancePercent, int DaysBeforeDue, bool AutoSchedule, IReadOnlyCollection<PmRouteAssetDto> RouteAssets, IReadOnlyCollection<PmEventDto> Events);

public sealed record SavePmPlanRequest(
    [StringLength(200)] string? Name,
    Guid AssetId,
    Guid? PmTemplateId,
    string? TriggerType,
    string? FrequencyUnit,
    int? FrequencyInterval,
    DateTime? NextDueDate,
    Guid? MeterId,
    decimal? MeterInterval,
    decimal? NextMeterDue,
    string? ScheduleMode,
    int? TolerancePercent,
    int? DaysBeforeDue,
    string? Priority,
    decimal? EstimatedHours,
    bool NeedsLineStop,
    [StringLength(100)] string? Basis,
    IReadOnlyCollection<Guid>? RouteAssetIds,
    bool? AutoSchedule);

public sealed record SetPmActiveRequest(bool Active);

public sealed record SkipPmRequest([StringLength(1000)] string? Reason);

public sealed record DeferPmRequest([StringLength(1000)] string? Reason, DateTime? NewDueDate, decimal? NewMeterDue, bool UntilLowCrop = false);

public sealed record PmGenerated(Guid WorkOrderId, string WorkOrderNumber);
