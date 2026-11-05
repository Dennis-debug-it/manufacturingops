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

/// <summary>
/// The annual overhaul: the list of work built up through the season (deferred jobs, window
/// carryovers, findings), and each low-crop shutdown's scope, budget, progress and recommissioning.
/// </summary>
[ApiController]
[Authorize]
[Route("api/factory/overhaul")]
public sealed class OverhaulController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IOrgScopeService orgScopeService,
    IMaintenanceWindowService windowService,
    IFactoryWorkOrderService workOrderService) : TenantAwareControllerBase(tenantContext)
{
    private static readonly string[] JobDone = [WorkOrderStatuses.Completed, WorkOrderStatuses.Verified, WorkOrderStatuses.Closed];
    private static readonly string[] JobGone = [WorkOrderStatuses.Cancelled, WorkOrderStatuses.Rejected];

    private FactoryActor Actor => new(UserId, UserDisplayName ?? "Unknown");

    [HttpGet("items")]
    public async Task<ActionResult<IReadOnlyCollection<OverhaulItemDto>>> Items([FromQuery] Guid? factoryId, [FromQuery] Guid? planId, [FromQuery] bool includeClosed, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var factories = await FactoriesAsync(cancellationToken);
        var query = dbContext.OverhaulItems.Where(x => x.TenantId == TenantId && factories.Contains(x.FactoryId));
        if (factoryId.HasValue) query = query.Where(x => x.FactoryId == factoryId);
        if (planId.HasValue) query = query.Where(x => x.OverhaulPlanId == planId);
        if (!includeClosed) query = query.Where(x => x.Status != OverhaulItemStatuses.Done && x.Status != OverhaulItemStatuses.Dropped);
        await SyncFromJobsAsync(query, cancellationToken);
        return Ok(await query.OrderBy(x => x.Status == OverhaulItemStatuses.Listed ? 0 : 1).ThenBy(x => x.Priority == "Critical" ? 0 : x.Priority == "High" ? 1 : x.Priority == "Medium" ? 2 : 3)
            .ThenBy(x => x.CreatedAt).Select(ItemProjection).ToListAsync(cancellationToken));
    }

    /// <summary>Adds work found during the season (wear seen on a job, an inspection finding) to the overhaul list.</summary>
    [HttpPost("items")]
    public async Task<ActionResult<OverhaulItemDto>> AddItem([FromBody] AddOverhaulItemRequest request, CancellationToken cancellationToken)
    {
        EnsureCanPlan();
        if (string.IsNullOrWhiteSpace(request.Title)) throw new BusinessRuleException("Say what needs doing in the overhaul.");
        Guid factoryId;
        if (request.AssetId is { } assetId)
        {
            var node = await dbContext.Assets.Where(x => x.TenantId == TenantId && x.Id == assetId && x.ClientId == null).Select(x => x.OrgNodeId).SingleOrDefaultAsync(cancellationToken)
                ?? throw new BusinessRuleException("The asset was not found.");
            factoryId = await windowService.FactoryOfAsync(TenantId, node, cancellationToken) ?? throw new BusinessRuleException("The asset is not in a factory.");
        }
        else
        {
            factoryId = request.FactoryId ?? throw new BusinessRuleException("Choose the asset or the factory.");
        }

        await EnsureFactoryAsync(factoryId, cancellationToken);
        var item = new OverhaulItem
        {
            TenantId = TenantId, FactoryId = factoryId, AssetId = request.AssetId, Title = request.Title.Trim(),
            Description = Clean(request.Description),
            Source = request.Source is OverhaulItemSources.Finding ? OverhaulItemSources.Finding : OverhaulItemSources.Manual,
            Priority = FactoryPriorities.Find(request.Priority) ?? "Medium",
            EstimatedHours = request.EstimatedHours, EstimatedCost = request.EstimatedCost, AddedByName = Actor.Name,
        };
        dbContext.OverhaulItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await dbContext.OverhaulItems.Where(x => x.Id == item.Id).Select(ItemProjection).SingleAsync(cancellationToken));
    }

    /// <summary>Defers an open job to the overhaul: it leaves the weekly window queue and waits for the shutdown.</summary>
    [HttpPost("items/from-job")]
    public async Task<ActionResult<OverhaulItemDto>> DeferJob([FromBody] DeferJobToOverhaulRequest request, CancellationToken cancellationToken)
    {
        EnsureCanPlan();
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new BusinessRuleException("Say why the job waits for the overhaul.");
        var job = await dbContext.WorkOrders.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == request.WorkOrderId && x.ClientId == null, cancellationToken)
            ?? throw new NotFoundException("The job was not found.");
        if (JobDone.Contains(job.Status) || JobGone.Contains(job.Status)) throw new BusinessRuleException("The job is already finished.");
        var factoryId = job.OrgNodeId is { } node ? await windowService.FactoryOfAsync(TenantId, node, cancellationToken) : null;
        await EnsureFactoryAsync(factoryId ?? throw new BusinessRuleException("The job is not in a factory."), cancellationToken);
        if (await dbContext.OverhaulItems.AnyAsync(x => x.WorkOrderId == job.Id && x.Status != OverhaulItemStatuses.Done && x.Status != OverhaulItemStatuses.Dropped, cancellationToken))
            throw new BusinessRuleException("The job is already on the overhaul list.");

        // Out of any window that hasn't started: it now waits for the shutdown.
        dbContext.MaintenanceWindowJobs.RemoveRange(await dbContext.MaintenanceWindowJobs
            .Where(x => x.WorkOrderId == job.Id && (x.Window!.Status == MaintenanceWindowStatuses.Planned || x.Window.Status == MaintenanceWindowStatuses.Packed))
            .ToListAsync(cancellationToken));
        var item = new OverhaulItem
        {
            TenantId = TenantId, FactoryId = factoryId.Value, AssetId = job.AssetId, WorkOrderId = job.Id, Title = job.Title,
            Description = request.Reason.Trim(), Source = OverhaulItemSources.Deferred, Priority = job.Priority,
            EstimatedHours = job.EstimatedLabourHours, AddedByName = Actor.Name,
        };
        dbContext.OverhaulItems.Add(item);
        dbContext.WorkOrderEvents.Add(new WorkOrderEvent
        {
            TenantId = TenantId, WorkOrderId = job.Id, ActorUserId = UserId, EventType = "DeferredToOverhaul", Status = job.Status,
            Message = $"Deferred to the overhaul: {request.Reason.Trim()}", OccurredAt = OccurredAt.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await dbContext.OverhaulItems.Where(x => x.Id == item.Id).Select(ItemProjection).SingleAsync(cancellationToken));
    }

    [HttpPut("items/{id:guid}")]
    public async Task<ActionResult<OverhaulItemDto>> UpdateItem(Guid id, [FromBody] UpdateOverhaulItemRequest request, CancellationToken cancellationToken)
    {
        EnsureCanPlan();
        var item = await LoadItemAsync(id, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Title)) throw new BusinessRuleException("Say what needs doing.");
        if (request.OverhaulPlanId is { } planId)
        {
            var plan = await dbContext.OverhaulPlans.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == planId, cancellationToken)
                ?? throw new BusinessRuleException("The overhaul was not found.");
            if (plan.FactoryId != item.FactoryId) throw new BusinessRuleException("That overhaul is for another factory.");
            if (plan.Status == OverhaulPlanStatuses.Completed) throw new BusinessRuleException("That overhaul is finished.");
        }

        item.Title = request.Title.Trim();
        item.Description = Clean(request.Description);
        item.Priority = FactoryPriorities.Find(request.Priority) ?? item.Priority;
        item.EstimatedHours = request.EstimatedHours;
        item.EstimatedCost = request.EstimatedCost;
        item.ActualCost = request.ActualCost;
        item.SparesNote = Clean(request.SparesNote);
        item.ContractorName = Clean(request.ContractorName);
        item.ProgressPercent = Math.Clamp(request.ProgressPercent ?? item.ProgressPercent, 0, 100);
        item.NeedsRecommissioning = request.NeedsRecommissioning ?? item.NeedsRecommissioning;
        item.OverhaulPlanId = request.OverhaulPlanId;
        if (item.Status is OverhaulItemStatuses.Listed or OverhaulItemStatuses.Scoped)
        {
            item.Status = item.OverhaulPlanId is null ? OverhaulItemStatuses.Listed : OverhaulItemStatuses.Scoped;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await dbContext.OverhaulItems.Where(x => x.Id == id).Select(ItemProjection).SingleAsync(cancellationToken));
    }

    /// <summary>Marks an item done, or drops it from the list (with the reason).</summary>
    [HttpPost("items/{id:guid}/status")]
    public async Task<ActionResult<OverhaulItemDto>> SetItemStatus(Guid id, [FromBody] OverhaulItemStatusRequest request, CancellationToken cancellationToken)
    {
        EnsureCanPlan();
        var item = await LoadItemAsync(id, cancellationToken);
        switch (request.Status)
        {
            case OverhaulItemStatuses.Done:
                item.Status = OverhaulItemStatuses.Done;
                item.ProgressPercent = 100;
                break;
            case OverhaulItemStatuses.Dropped:
                if (string.IsNullOrWhiteSpace(request.Reason)) throw new BusinessRuleException("Say why it is taken off the list.");
                item.Status = OverhaulItemStatuses.Dropped;
                item.Description = $"{item.Description}{(item.Description is null ? string.Empty : "\n")}Dropped: {request.Reason.Trim()}";
                break;
            case OverhaulItemStatuses.Listed:
                item.Status = item.OverhaulPlanId is null ? OverhaulItemStatuses.Listed : OverhaulItemStatuses.Scoped;
                break;
            default:
                throw new BusinessRuleException("Status must be Done, Dropped or Listed.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await dbContext.OverhaulItems.Where(x => x.Id == id).Select(ItemProjection).SingleAsync(cancellationToken));
    }

    /// <summary>The recommissioning check after the work: trial run, settings and guards confirmed.</summary>
    [HttpPost("items/{id:guid}/recommission")]
    public async Task<ActionResult<OverhaulItemDto>> Recommission(Guid id, [FromBody] RecommissionRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanAssignWorkOrders, PermissionNames.CanCompleteWorkOrders);
        var item = await LoadItemAsync(id, cancellationToken);
        if (item.Status != OverhaulItemStatuses.Done) throw new BusinessRuleException("Finish the work before recommissioning.");
        item.RecommissionedAt = OccurredAt.UtcNow;
        item.RecommissionedByName = Actor.Name;
        item.RecommissioningNotes = Clean(request.Notes);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await dbContext.OverhaulItems.Where(x => x.Id == id).Select(ItemProjection).SingleAsync(cancellationToken));
    }

    [HttpGet("plans")]
    public async Task<ActionResult<IReadOnlyCollection<OverhaulPlanSummary>>> Plans([FromQuery] Guid? factoryId, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var factories = await FactoriesAsync(cancellationToken);
        var query = dbContext.OverhaulPlans.Where(x => x.TenantId == TenantId && factories.Contains(x.FactoryId));
        if (factoryId.HasValue) query = query.Where(x => x.FactoryId == factoryId);
        var plans = await query.OrderByDescending(x => x.PlannedStart).ToListAsync(cancellationToken);
        var result = new List<OverhaulPlanSummary>();
        foreach (var plan in plans) result.Add(await SummaryAsync(plan, cancellationToken));
        return Ok(result);
    }

    [HttpGet("plans/{id:guid}")]
    public async Task<ActionResult<OverhaulPlanDetail>> Plan(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var plan = await LoadPlanAsync(id, cancellationToken);
        var items = dbContext.OverhaulItems.Where(x => x.OverhaulPlanId == id);
        await SyncFromJobsAsync(items, cancellationToken);
        return Ok(new OverhaulPlanDetail(await SummaryAsync(plan, cancellationToken), await items.OrderBy(x => x.CreatedAt).Select(ItemProjection).ToListAsync(cancellationToken)));
    }

    [HttpPost("plans")]
    public async Task<ActionResult<OverhaulPlanSummary>> CreatePlan([FromBody] SaveOverhaulPlanRequest request, CancellationToken cancellationToken)
    {
        EnsureCanPlan();
        var factoryId = request.FactoryId ?? throw new BusinessRuleException("Choose the factory.");
        await EnsureFactoryAsync(factoryId, cancellationToken);
        var plan = new OverhaulPlan { TenantId = TenantId, FactoryId = factoryId };
        Apply(plan, request);
        dbContext.OverhaulPlans.Add(plan);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await SummaryAsync(plan, cancellationToken));
    }

    [HttpPut("plans/{id:guid}")]
    public async Task<ActionResult<OverhaulPlanSummary>> UpdatePlan(Guid id, [FromBody] SaveOverhaulPlanRequest request, CancellationToken cancellationToken)
    {
        EnsureCanPlan();
        var plan = await LoadPlanAsync(id, cancellationToken);
        Apply(plan, request);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await SummaryAsync(plan, cancellationToken));
    }

    /// <summary>The shutdown has begun: every scoped item gets a job (approved, ready to assign) unless it already has an open one.</summary>
    [HttpPost("plans/{id:guid}/start")]
    public async Task<ActionResult<OverhaulPlanDetail>> StartPlan(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanPlan();
        var plan = await LoadPlanAsync(id, cancellationToken);
        if (plan.Status != OverhaulPlanStatuses.Planning) throw new BusinessRuleException("This overhaul has already started.");
        var items = await dbContext.OverhaulItems.Include(x => x.WorkOrder)
            .Where(x => x.OverhaulPlanId == id && x.Status == OverhaulItemStatuses.Scoped).ToListAsync(cancellationToken);
        if (items.Count == 0) throw new BusinessRuleException("Add work to the overhaul's scope first.");

        plan.Status = OverhaulPlanStatuses.InProgress;
        plan.ActualStart = OccurredAt.UtcNow;
        foreach (var item in items)
        {
            if (item.WorkOrder is null || JobDone.Contains(item.WorkOrder.Status) || JobGone.Contains(item.WorkOrder.Status))
            {
                var job = await workOrderService.CreateAsync(TenantId, Actor, new CreateFactoryWorkOrder(
                    $"Overhaul: {item.Title}", FactoryWorkTypes.Preventive, item.Priority,
                    AssetId: item.AssetId, OrgNodeId: item.AssetId is null ? item.FactoryId : null,
                    Description: string.Join("\n", new[] { item.Description, item.SparesNote is null ? null : $"Spares: {item.SparesNote}", item.ContractorName is null ? null : $"Contractor: {item.ContractorName}" }.Where(x => x is not null)),
                    DueDate: plan.PlannedEnd, EstimatedHours: item.EstimatedHours, RequestedByName: Actor.Name), cancellationToken);
                if (job.Status == WorkOrderStatuses.Requested)
                {
                    await workOrderService.ApplyAsync(job, Actor, new FactoryTransition(FactoryWorkOrderActions.Approve, $"Part of {plan.Name}."), cancellationToken);
                }

                item.WorkOrderId = job.Id;
            }

            item.Status = OverhaulItemStatuses.InProgress;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await Plan(id, cancellationToken);
    }

    /// <summary>Closes the overhaul: everything in scope done (and recommissioned) or dropped.</summary>
    [HttpPost("plans/{id:guid}/complete")]
    public async Task<ActionResult<OverhaulPlanDetail>> CompletePlan(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanPlan();
        var plan = await LoadPlanAsync(id, cancellationToken);
        if (plan.Status != OverhaulPlanStatuses.InProgress) throw new BusinessRuleException("Start the overhaul first.");
        var items = dbContext.OverhaulItems.Where(x => x.OverhaulPlanId == id);
        await SyncFromJobsAsync(items, cancellationToken);
        var open = await items.CountAsync(x => x.Status != OverhaulItemStatuses.Done && x.Status != OverhaulItemStatuses.Dropped, cancellationToken);
        if (open > 0) throw new BusinessRuleException($"{open} item(s) are not finished. Finish them, or drop them back to the list.");
        var notRecommissioned = await items.CountAsync(x => x.Status == OverhaulItemStatuses.Done && x.NeedsRecommissioning && x.RecommissionedAt == null, cancellationToken);
        if (notRecommissioned > 0) throw new BusinessRuleException($"{notRecommissioned} item(s) still need recommissioning.");
        plan.Status = OverhaulPlanStatuses.Completed;
        plan.ActualEnd = OccurredAt.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await Plan(id, cancellationToken);
    }

    /// <summary>Items whose job is finished are done; items whose job was cancelled go back to the list.</summary>
    private async Task SyncFromJobsAsync(IQueryable<OverhaulItem> query, CancellationToken cancellationToken)
    {
        var items = await query.Where(x => x.WorkOrderId != null && x.Status == OverhaulItemStatuses.InProgress)
            .Include(x => x.WorkOrder).ToListAsync(cancellationToken);
        foreach (var item in items)
        {
            if (JobDone.Contains(item.WorkOrder!.Status))
            {
                item.Status = OverhaulItemStatuses.Done;
                item.ProgressPercent = 100;
            }
            else if (JobGone.Contains(item.WorkOrder.Status))
            {
                item.Status = OverhaulItemStatuses.Scoped;
            }
        }

        if (items.Count > 0) await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<OverhaulPlanSummary> SummaryAsync(OverhaulPlan plan, CancellationToken cancellationToken)
    {
        var items = await dbContext.OverhaulItems.Where(x => x.OverhaulPlanId == plan.Id && x.Status != OverhaulItemStatuses.Dropped)
            .Select(x => new { x.Status, x.EstimatedCost, x.ActualCost, x.EstimatedHours, x.ProgressPercent, x.NeedsRecommissioning, x.RecommissionedAt })
            .ToListAsync(cancellationToken);
        var factory = await dbContext.OrgNodes.Where(x => x.Id == plan.FactoryId).Select(x => x.Name).SingleAsync(cancellationToken);
        var hours = items.Sum(x => x.EstimatedHours ?? 1);
        var progress = items.Count == 0 ? 0 : hours == 0 ? 0 : (int)Math.Round(items.Sum(x => (x.EstimatedHours ?? 1) * x.ProgressPercent) / hours);
        return new OverhaulPlanSummary(plan.Id, plan.FactoryId, factory, plan.Name, plan.PlannedStart, plan.PlannedEnd, plan.Budget, plan.Status, plan.ActualStart, plan.ActualEnd, plan.Notes,
            items.Count, items.Count(x => x.Status == OverhaulItemStatuses.Done), items.Sum(x => x.EstimatedCost ?? 0), items.Sum(x => x.ActualCost ?? 0), hours, progress,
            items.Count(x => x.Status == OverhaulItemStatuses.Done && x.NeedsRecommissioning && x.RecommissionedAt == null));
    }

    private static void Apply(OverhaulPlan plan, SaveOverhaulPlanRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new BusinessRuleException("Name the overhaul, e.g. \"2027 low-crop overhaul\".");
        if (request.PlannedEnd.Date < request.PlannedStart.Date) throw new BusinessRuleException("The end must be after the start.");
        plan.Name = request.Name.Trim();
        plan.PlannedStart = DateTime.SpecifyKind(request.PlannedStart.Date, DateTimeKind.Utc);
        plan.PlannedEnd = DateTime.SpecifyKind(request.PlannedEnd.Date, DateTimeKind.Utc);
        plan.Budget = request.Budget is > 0 ? request.Budget : null;
        plan.Notes = Clean(request.Notes);
    }

    private static readonly System.Linq.Expressions.Expression<Func<OverhaulItem, OverhaulItemDto>> ItemProjection = x => new OverhaulItemDto(
        x.Id, x.FactoryId, x.Factory!.Name, x.OverhaulPlanId, x.OverhaulPlan != null ? x.OverhaulPlan.Name : null,
        x.AssetId, x.Asset != null ? x.Asset.AssetCode : null, x.Asset != null ? x.Asset.AssetName : null, x.Asset != null ? x.Asset.Criticality : null,
        x.WorkOrderId, x.WorkOrder != null ? x.WorkOrder.WorkOrderNumber : null, x.WorkOrder != null ? x.WorkOrder.Status : null, x.WorkOrder != null ? x.WorkOrder.WindowCarryoverCount : 0,
        x.Title, x.Description, x.Source, x.Priority, x.Status, x.EstimatedHours, x.EstimatedCost, x.ActualCost, x.SparesNote, x.ContractorName, x.ProgressPercent,
        x.NeedsRecommissioning, x.RecommissionedAt, x.RecommissionedByName, x.RecommissioningNotes, x.AddedByName, x.CreatedAt);

    private async Task<OverhaulItem> LoadItemAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await dbContext.OverhaulItems.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("The overhaul item was not found.");
        if (!(await FactoriesAsync(cancellationToken)).Contains(item.FactoryId)) throw new NotFoundException("The overhaul item was not found.");
        return item;
    }

    private async Task<OverhaulPlan> LoadPlanAsync(Guid id, CancellationToken cancellationToken)
    {
        var plan = await dbContext.OverhaulPlans.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("The overhaul was not found.");
        if (!(await FactoriesAsync(cancellationToken)).Contains(plan.FactoryId)) throw new NotFoundException("The overhaul was not found.");
        return plan;
    }

    private async Task EnsureFactoryAsync(Guid factoryId, CancellationToken cancellationToken)
    {
        if (!await dbContext.OrgNodes.AnyAsync(x => x.TenantId == TenantId && x.Id == factoryId && x.Level == OrgNodeLevels.Factory, cancellationToken))
            throw new BusinessRuleException("Choose a factory.");
        if (!(await FactoriesAsync(cancellationToken)).Contains(factoryId)) throw new ForbiddenException("That factory is outside your area.");
    }

    /// <summary>Factories the user works in (their scope's factories).</summary>
    private async Task<HashSet<Guid>> FactoriesAsync(CancellationToken cancellationToken)
    {
        var all = await dbContext.OrgNodes.Where(x => x.TenantId == TenantId && x.Level == OrgNodeLevels.Factory).Select(x => x.Id).ToListAsync(cancellationToken);
        var scope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        if (scope.Nodes is null) return all.ToHashSet();
        var result = all.Where(scope.Nodes.Contains).ToHashSet();
        foreach (var node in scope.Nodes)
        {
            if (await windowService.FactoryOfAsync(TenantId, node, cancellationToken) is { } factory) result.Add(factory);
        }

        return result;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void EnsureCanView() => userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanViewWorkOrders);

    private void EnsureCanPlan() => userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageSettings, PermissionNames.CanAssignWorkOrders);
}

public sealed record OverhaulItemDto(
    Guid Id, Guid FactoryId, string FactoryName, Guid? OverhaulPlanId, string? OverhaulPlanName,
    Guid? AssetId, string? AssetCode, string? AssetName, string? AssetCriticality,
    Guid? WorkOrderId, string? WorkOrderNumber, string? WorkOrderStatus, int CarryoverCount,
    string Title, string? Description, string Source, string Priority, string Status, decimal? EstimatedHours, decimal? EstimatedCost, decimal? ActualCost,
    string? SparesNote, string? ContractorName, int ProgressPercent, bool NeedsRecommissioning, DateTime? RecommissionedAt, string? RecommissionedByName,
    string? RecommissioningNotes, string? AddedByName, DateTime CreatedAt);

public sealed record OverhaulPlanSummary(
    Guid Id, Guid FactoryId, string FactoryName, string Name, DateTime PlannedStart, DateTime PlannedEnd, decimal? Budget, string Status,
    DateTime? ActualStart, DateTime? ActualEnd, string? Notes, int Items, int ItemsDone, decimal EstimatedCost, decimal ActualCost, decimal EstimatedHours,
    int ProgressPercent, int AwaitingRecommissioning);

public sealed record OverhaulPlanDetail(OverhaulPlanSummary Plan, IReadOnlyCollection<OverhaulItemDto> Items);

public sealed record AddOverhaulItemRequest(
    Guid? FactoryId, Guid? AssetId, [StringLength(200)] string? Title, [StringLength(4000)] string? Description, string? Source, string? Priority, decimal? EstimatedHours, decimal? EstimatedCost);

public sealed record DeferJobToOverhaulRequest(Guid WorkOrderId, [StringLength(1000)] string? Reason);

public sealed record UpdateOverhaulItemRequest(
    [StringLength(200)] string? Title, [StringLength(4000)] string? Description, string? Priority, decimal? EstimatedHours, decimal? EstimatedCost, decimal? ActualCost,
    [StringLength(2000)] string? SparesNote, [StringLength(200)] string? ContractorName, int? ProgressPercent, bool? NeedsRecommissioning, Guid? OverhaulPlanId);

public sealed record OverhaulItemStatusRequest(string Status, [StringLength(1000)] string? Reason);

public sealed record RecommissionRequest([StringLength(2000)] string? Notes);

public sealed record SaveOverhaulPlanRequest(Guid? FactoryId, [StringLength(200)] string? Name, DateTime PlannedStart, DateTime PlannedEnd, decimal? Budget, [StringLength(4000)] string? Notes);
