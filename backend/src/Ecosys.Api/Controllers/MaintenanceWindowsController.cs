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

/// <summary>The weekly maintenance window: plan, pack, run and hand back the line-stop work of each factory.</summary>
[ApiController]
[Authorize]
[Route("api/factory/windows")]
public sealed class MaintenanceWindowsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IOrgScopeService orgScopeService,
    IMaintenanceWindowService windowService,
    IFactoryPmService pmService,
    IFactorySettingsService factorySettingsService) : TenantAwareControllerBase(tenantContext)
{
    private FactoryActor Actor => new(UserId, UserDisplayName ?? "Unknown");

    /// <summary>Upcoming windows (creating the next ones as needed) and the last few, per factory.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<WindowSummary>>> List([FromQuery] Guid? factoryId, [FromQuery] int? past, CancellationToken cancellationToken)
    {
        EnsureCanView();
        await windowService.EnsureUpcomingAsync(TenantId, DateTime.UtcNow, cancellationToken);
        var factories = await FactoriesInScopeAsync(cancellationToken);
        var since = DateTime.UtcNow.Date.AddDays(-7 * Math.Clamp(past ?? 4, 0, 52));
        var query = dbContext.MaintenanceWindows.Where(x => x.TenantId == TenantId && factories.Contains(x.FactoryId) && x.WindowDate >= since);
        if (factoryId.HasValue) query = query.Where(x => x.FactoryId == factoryId);
        var windows = await query.OrderBy(x => x.WindowDate).ThenBy(x => x.Factory!.Name).ToListAsync(cancellationToken);
        var result = new List<WindowSummary>();
        foreach (var window in windows)
        {
            result.Add(await SummaryAsync(window, cancellationToken));
        }

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WindowDetail>> Get(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanView();
        return Ok(await DetailAsync(await LoadAsync(id, cancellationToken), cancellationToken));
    }

    /// <summary>Open jobs in the factory that could go into this window.</summary>
    [HttpGet("{id:guid}/candidates")]
    public async Task<ActionResult<IReadOnlyCollection<WindowCandidate>>> Candidates(Guid id, [FromQuery] string? search, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var window = await LoadAsync(id, cancellationToken);
        var subtree = (await orgScopeService.GetSubtreeAsync(TenantId, window.FactoryId, cancellationToken)).ToList();
        var inWindow = await dbContext.MaintenanceWindowJobs.Where(x => x.WindowId == id).Select(x => x.WorkOrderId).ToListAsync(cancellationToken);
        string[] finished = [WorkOrderStatuses.Completed, WorkOrderStatuses.Verified, WorkOrderStatuses.Closed, WorkOrderStatuses.Cancelled, WorkOrderStatuses.Rejected, WorkOrderStatuses.Requested];
        var query = dbContext.WorkOrders.Where(x => x.TenantId == TenantId && x.ClientId == null && x.OrgNodeId != null && subtree.Contains(x.OrgNodeId.Value)
            && !finished.Contains(x.Status) && !inWindow.Contains(x.Id));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.WorkOrderNumber.ToLower().Contains(term) || x.Title.ToLower().Contains(term) || (x.Asset != null && x.Asset.AssetCode.ToLower().Contains(term)));
        }

        return Ok(await query.OrderByDescending(x => x.NeedsLineStop).ThenBy(x => x.DueDate ?? DateTime.MaxValue).Take(50)
            .Select(x => new WindowCandidate(x.Id, x.WorkOrderNumber, x.Title, x.Asset != null ? x.Asset.AssetCode : null, x.Priority, x.Status, x.EstimatedLabourHours, x.NeedsLineStop, x.DueDate))
            .ToListAsync(cancellationToken));
    }

    [HttpPost("{id:guid}/jobs")]
    public async Task<ActionResult<WindowDetail>> AddJob(Guid id, [FromBody] WindowJobRequest request, CancellationToken cancellationToken)
    {
        EnsureCanPlan();
        var window = await LoadAsync(id, cancellationToken);
        await windowService.AddJobAsync(window, request.WorkOrderId, cancellationToken);
        return Ok(await DetailAsync(window, cancellationToken));
    }

    [HttpDelete("{id:guid}/jobs/{workOrderId:guid}")]
    public async Task<ActionResult<WindowDetail>> RemoveJob(Guid id, Guid workOrderId, CancellationToken cancellationToken)
    {
        EnsureCanPlan();
        var window = await LoadAsync(id, cancellationToken);
        await windowService.RemoveJobAsync(window, workOrderId, cancellationToken);
        return Ok(await DetailAsync(window, cancellationToken));
    }

    /// <summary>Moves the window to another day or changes its hours or capacity.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WindowDetail>> Update(Guid id, [FromBody] UpdateWindowRequest request, CancellationToken cancellationToken)
    {
        EnsureCanPlan();
        var window = await LoadAsync(id, cancellationToken);
        if (request.Date is { } date)
        {
            await windowService.MoveAsync(window, date, ParseTime(request.Start), ParseTime(request.End), cancellationToken);
        }

        window.CapacityHours = request.CapacityHours is > 0 ? request.CapacityHours : null;
        window.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await DetailAsync(window, cancellationToken));
    }

    [HttpPost("{id:guid}/skip")]
    public async Task<ActionResult<WindowDetail>> Skip(Guid id, [FromBody] WindowReasonRequest request, CancellationToken cancellationToken)
    {
        EnsureCanPlan();
        var window = await LoadAsync(id, cancellationToken);
        await windowService.SkipAsync(window, Actor, request.Reason ?? string.Empty, cancellationToken);
        return Ok(await DetailAsync(window, cancellationToken));
    }

    [HttpPost("{id:guid}/pack")]
    public async Task<ActionResult<WindowDetail>> Pack(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanPlan();
        var window = await LoadAsync(id, cancellationToken);
        await windowService.PackAsync(window, Actor, cancellationToken);
        return Ok(await DetailAsync(window, cancellationToken));
    }

    [HttpPost("{id:guid}/start")]
    public async Task<ActionResult<WindowDetail>> Start(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanRun();
        var window = await LoadAsync(id, cancellationToken);
        await windowService.StartAsync(window, Actor, cancellationToken);
        return Ok(await DetailAsync(window, cancellationToken));
    }

    [HttpPost("{id:guid}/handback-items/{itemId:guid}")]
    public async Task<ActionResult<WindowDetail>> HandbackItem(Guid id, Guid itemId, [FromBody] HandbackItemRequest request, CancellationToken cancellationToken)
    {
        EnsureCanRun();
        var window = await LoadAsync(id, cancellationToken);
        await windowService.SetHandbackItemAsync(window, itemId, request.Done, request.Note, Actor, cancellationToken);
        return Ok(await DetailAsync(window, cancellationToken));
    }

    [HttpPost("{id:guid}/handback")]
    public async Task<ActionResult<WindowHandbackResponse>> HandBack(Guid id, [FromBody] HandbackRequest request, CancellationToken cancellationToken)
    {
        EnsureCanRun();
        var window = await LoadAsync(id, cancellationToken);
        var result = await windowService.HandBackAsync(window, Actor, request.Notes, request.SignatureDataUrl, cancellationToken);
        return Ok(new WindowHandbackResponse(result, await DetailAsync(window, cancellationToken)));
    }

    /// <summary>
    /// Everything the team needs on the day, in one download kept on the phone: each job with its
    /// steps, team, spares and tools, isolations, and the start-up checks.
    /// </summary>
    [HttpGet("{id:guid}/pack")]
    public async Task<ActionResult<WindowPack>> GetPack(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var window = await LoadAsync(id, cancellationToken);
        var detail = await DetailAsync(window, cancellationToken);
        var ids = detail.Jobs.Select(x => x.WorkOrderId).ToList();
        var steps = (await dbContext.WorkOrderChecklistItems.Where(x => ids.Contains(x.WorkOrderId)).OrderBy(x => x.SectionOrder).ThenBy(x => x.SortOrder)
            .Select(x => new { x.WorkOrderId, Step = new WindowPackStep(x.Id, x.SectionName, x.QuestionText, x.QuestionType, x.Unit, x.IsCompleted) }).ToListAsync(cancellationToken))
            .GroupBy(x => x.WorkOrderId).ToDictionary(g => g.Key, g => g.Select(x => x.Step).ToList());
        var resources = (await (from w in dbContext.WorkOrders
                                where ids.Contains(w.Id) && w.JobPlanId != null
                                from r in dbContext.JobPlanResources.Where(r => r.JobPlanId == w.JobPlanId)
                                select new { w.Id, Resource = new WindowPackResource(r.Kind, r.Description, r.Quantity) }).ToListAsync(cancellationToken))
            .GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.Select(x => x.Resource).ToList());
        var descriptions = await dbContext.WorkOrders.Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.Description, Location = x.OrgNode != null ? x.OrgNode.Name : null }).ToDictionaryAsync(x => x.Id, cancellationToken);
        // Spares held for each job: reserved, kitted (picked and set aside) or already issued.
        var held = await dbContext.StockReservations.Where(x => ids.Contains(x.WorkOrderId) && x.Status != ReservationStatuses.Released)
            .Select(x => new { x.Id, x.WorkOrderId, x.StoreId, x.MaterialItemId, x.MaterialItem!.ItemCode, x.MaterialItem.ItemName, x.MaterialItem.UnitOfMeasure, x.Quantity, x.IssuedQuantity, x.Status, Store = x.Store!.Name })
            .ToListAsync(cancellationToken);
        var heldStores = held.Select(x => x.StoreId).Distinct().ToList();
        var bins = await dbContext.StoreStocks.Where(x => heldStores.Contains(x.StoreId) && x.Bin != null)
            .Select(x => new { x.StoreId, x.MaterialItemId, Bin = x.Bin!.Code }).ToListAsync(cancellationToken);
        var spares = held.GroupBy(x => x.WorkOrderId).ToDictionary(g => g.Key, g => g.Select(x => new WindowPackSpare(x.Id, x.MaterialItemId, x.ItemCode, x.ItemName, x.UnitOfMeasure,
            x.Quantity, x.IssuedQuantity, x.Status, x.Store, bins.FirstOrDefault(b => b.StoreId == x.StoreId && b.MaterialItemId == x.MaterialItemId)?.Bin ?? x.Store)).ToList());
        var planned = (await (from w in dbContext.WorkOrders
                              where ids.Contains(w.Id) && w.JobPlanId != null
                              from r in dbContext.JobPlanResources.Where(r => r.JobPlanId == w.JobPlanId && r.MaterialItemId != null)
                              select new { w.Id, MaterialItemId = r.MaterialItemId!.Value, r.Description, r.Quantity }).ToListAsync(cancellationToken))
            .GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.ToList());
        var jobs = detail.Jobs.Select(job =>
        {
            var held = spares.GetValueOrDefault(job.WorkOrderId) ?? [];
            var shortages = (planned.GetValueOrDefault(job.WorkOrderId) ?? [])
                .Select(p => new WindowPackShortage(p.MaterialItemId, p.Description, p.Quantity - held.Where(h => h.MaterialItemId == p.MaterialItemId).Sum(h => h.Quantity)))
                .Where(x => x.Short > 0).ToList();
            return new WindowPackJob(job, descriptions.GetValueOrDefault(job.WorkOrderId)?.Description, descriptions.GetValueOrDefault(job.WorkOrderId)?.Location,
                steps.GetValueOrDefault(job.WorkOrderId) ?? [], resources.GetValueOrDefault(job.WorkOrderId) ?? [],
                // Every line-stop job is done under isolation: the pack says so on each job.
                "Isolate and lock out before work; confirm zero energy.") { Spares = held, Shortages = shortages };
        }).ToList();
        return Ok(new WindowPack(detail.Window, jobs, detail.HandbackItems, DateTime.UtcNow));
    }

    /// <summary>Each factory's window day and on/off.</summary>
    [HttpGet("factories")]
    public async Task<ActionResult<IReadOnlyCollection<FactoryWindowSetting>>> Factories(CancellationToken cancellationToken)
    {
        EnsureCanView();
        var settings = await factorySettingsService.GetOrCreateAsync(TenantId, cancellationToken);
        var factories = await FactoriesInScopeAsync(cancellationToken);
        return Ok(await dbContext.OrgNodes.Where(x => factories.Contains(x.Id)).OrderBy(x => x.Name)
            .Select(x => new FactoryWindowSetting(x.Id, x.Name, x.WindowEnabled ?? settings.WeeklyWindowEnabled, x.WindowDay ?? settings.WeeklyWindowDay, x.WindowDay != null || x.WindowEnabled != null))
            .ToListAsync(cancellationToken));
    }

    [HttpPut("factories/{factoryId:guid}")]
    public async Task<ActionResult<FactoryWindowSetting>> SetFactory(Guid factoryId, [FromBody] FactoryWindowRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageSettings);
        var factory = await LoadFactoryAsync(factoryId, cancellationToken);
        factory.WindowEnabled = request.Enabled;
        factory.WindowDay = request.Day;
        await dbContext.SaveChangesAsync(cancellationToken);
        var settings = await factorySettingsService.GetOrCreateAsync(TenantId, cancellationToken);
        return Ok(new FactoryWindowSetting(factory.Id, factory.Name, factory.WindowEnabled ?? settings.WeeklyWindowEnabled, factory.WindowDay ?? settings.WeeklyWindowDay, factory.WindowDay != null || factory.WindowEnabled != null));
    }

    /// <summary>Sets up the standard weekly routines (lubrication, belts, CTC segments, dryer cleaning, fans, panels, boiler) on the factory's assets.</summary>
    [HttpPost("factories/{factoryId:guid}/routines")]
    public async Task<ActionResult<RoutinesInstalled>> InstallRoutines(Guid factoryId, CancellationToken cancellationToken)
    {
        EnsureCanPlan();
        var factory = await LoadFactoryAsync(factoryId, cancellationToken);
        await windowService.EnsureUpcomingAsync(TenantId, DateTime.UtcNow, cancellationToken);
        var next = await dbContext.MaintenanceWindows
            .Where(x => x.FactoryId == factory.Id && (x.Status == MaintenanceWindowStatuses.Planned || x.Status == MaintenanceWindowStatuses.Packed))
            .OrderBy(x => x.WindowDate).Select(x => (DateTime?)x.WindowDate).FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleException("This factory has no weekly window. Turn it on first.");
        var added = await WindowRoutines.InstallAsync(dbContext, pmService, TenantId, factory.Id, next, cancellationToken);
        return Ok(new RoutinesInstalled(added));
    }

    private async Task<OrgNode> LoadFactoryAsync(Guid factoryId, CancellationToken cancellationToken)
    {
        var factory = await dbContext.OrgNodes.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == factoryId && x.Level == OrgNodeLevels.Factory, cancellationToken)
            ?? throw new NotFoundException("The factory was not found.");
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, factory.Id, cancellationToken);
        return factory;
    }

    private async Task<MaintenanceWindow> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        var window = await dbContext.MaintenanceWindows.Include(x => x.Factory)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("The window was not found.");
        if (!(await FactoriesInScopeAsync(cancellationToken)).Contains(window.FactoryId))
        {
            throw new NotFoundException("The window was not found.");
        }

        return window;
    }

    /// <summary>Factories the user works in: those in their scope, or above part of it.</summary>
    private async Task<HashSet<Guid>> FactoriesInScopeAsync(CancellationToken cancellationToken)
    {
        var factories = await dbContext.OrgNodes.Where(x => x.TenantId == TenantId && x.Level == OrgNodeLevels.Factory && x.IsActive).Select(x => x.Id).ToListAsync(cancellationToken);
        var scope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        if (scope.Nodes is null)
        {
            return factories.ToHashSet();
        }

        var result = new HashSet<Guid>();
        foreach (var node in scope.Nodes)
        {
            if (await windowService.FactoryOfAsync(TenantId, node, cancellationToken) is { } factory)
            {
                result.Add(factory);
            }
        }

        foreach (var factory in factories.Where(scope.Nodes.Contains))
        {
            result.Add(factory);
        }

        return result;
    }

    private async Task<WindowSummary> SummaryAsync(MaintenanceWindow window, CancellationToken cancellationToken)
    {
        var jobs = await dbContext.MaintenanceWindowJobs.Where(x => x.WindowId == window.Id)
            .Select(x => new { x.WorkOrder!.EstimatedLabourHours, x.WorkOrder.Status }).ToListAsync(cancellationToken);
        string[] done = [WorkOrderStatuses.Completed, WorkOrderStatuses.Verified, WorkOrderStatuses.Closed];
        return new WindowSummary(window.Id, window.FactoryId, window.Factory?.Name ?? string.Empty, window.WindowDate, window.StartsAt, window.EndsAt, window.Status,
            jobs.Count, jobs.Count(x => done.Contains(x.Status)), jobs.Sum(x => x.EstimatedLabourHours ?? 0), await windowService.CapacityAsync(window, cancellationToken),
            window.CapacityHours.HasValue, window.MovedFromDate, window.SkipReason, window.Notes, window.PackedAt, window.PackedByName, window.StartedAt, window.HandedBackAt, window.HandedBackByName, window.HandbackNotes);
    }

    private async Task<WindowDetail> DetailAsync(MaintenanceWindow window, CancellationToken cancellationToken)
    {
        var jobs = await dbContext.MaintenanceWindowJobs.Where(x => x.WindowId == window.Id).OrderBy(x => x.Sequence)
            .Select(x => new WindowJob(
                x.WorkOrderId, x.WorkOrder!.WorkOrderNumber, x.WorkOrder.Title, x.WorkOrder.AssetId, x.WorkOrder.Asset != null ? x.WorkOrder.Asset.AssetCode : null,
                x.WorkOrder.Asset != null ? x.WorkOrder.Asset.AssetName : null, x.WorkOrder.Priority, x.WorkOrder.Status, x.WorkOrder.WorkType, x.WorkOrder.EstimatedLabourHours,
                x.WorkOrder.WindowCarryoverCount, x.AddedAutomatically, x.Outcome, x.WorkOrder.PreventiveMaintenancePlanId != null,
                dbContext.WorkOrderTechnicianAssignments.Where(a => a.WorkOrderId == x.WorkOrderId).OrderByDescending(a => a.IsLead).Select(a => a.Technician!.FullName).ToList()))
            .ToListAsync(cancellationToken);
        var items = await dbContext.MaintenanceWindowHandbackItems.Where(x => x.WindowId == window.Id).OrderBy(x => x.Sequence)
            .Select(x => new WindowHandbackItemDto(x.Id, x.Prompt, x.Done, x.Note, x.DoneByName, x.DoneAt)).ToListAsync(cancellationToken);
        return new WindowDetail(await SummaryAsync(window, cancellationToken), jobs, items);
    }

    private static TimeOnly? ParseTime(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : TimeOnly.TryParse(value, out var t) ? t : throw new BusinessRuleException("Times are like 06:00.");

    private void EnsureCanView() => userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanViewWorkOrders);

    private void EnsureCanPlan() => userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageSettings, PermissionNames.CanAssignWorkOrders);

    /// <summary>Running the window on the day: supervisors and the technicians doing it.</summary>
    private void EnsureCanRun() => userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanAssignWorkOrders, PermissionNames.CanCompleteWorkOrders);
}

public sealed record WindowSummary(
    Guid Id, Guid FactoryId, string FactoryName, DateTime WindowDate, DateTime StartsAt, DateTime EndsAt, string Status,
    int Jobs, int JobsDone, decimal PlannedHours, decimal CapacityHours, bool CapacitySet, DateTime? MovedFromDate, string? SkipReason, string? Notes,
    DateTime? PackedAt, string? PackedByName, DateTime? StartedAt, DateTime? HandedBackAt, string? HandedBackByName, string? HandbackNotes);

public sealed record WindowJob(
    Guid WorkOrderId, string WorkOrderNumber, string Title, Guid? AssetId, string? AssetCode, string? AssetName, string Priority, string Status, string WorkType,
    decimal? EstimatedHours, int CarryoverCount, bool AddedAutomatically, string? Outcome, bool IsPm, IReadOnlyCollection<string> Team);

public sealed record WindowHandbackItemDto(Guid Id, string Prompt, bool Done, string? Note, string? DoneByName, DateTime? DoneAt);

public sealed record WindowDetail(WindowSummary Window, IReadOnlyCollection<WindowJob> Jobs, IReadOnlyCollection<WindowHandbackItemDto> HandbackItems);

public sealed record WindowCandidate(Guid WorkOrderId, string WorkOrderNumber, string Title, string? AssetCode, string Priority, string Status, decimal? EstimatedHours, bool NeedsLineStop, DateTime? DueDate);

public sealed record WindowPackStep(Guid Id, string? Section, string Instruction, string Type, string? Unit, bool Done);

public sealed record WindowPackResource(string Kind, string Description, decimal Quantity);

public sealed record WindowPackJob(WindowJob Job, string? Description, string? Location, IReadOnlyCollection<WindowPackStep> Steps, IReadOnlyCollection<WindowPackResource> Resources, string Isolation)
{
    public IReadOnlyCollection<WindowPackSpare> Spares { get; init; } = [];
    public IReadOnlyCollection<WindowPackShortage> Shortages { get; init; } = [];
}

/// <summary>A spare held for a window job, and where to collect it (store / bin).</summary>
public sealed record WindowPackSpare(Guid ReservationId, Guid MaterialItemId, string ItemCode, string ItemName, string UnitOfMeasure, decimal Quantity, decimal IssuedQuantity, string Status, string StoreName, string Location);

/// <summary>A planned part not (fully) reserved for the job: short in stores.</summary>
public sealed record WindowPackShortage(Guid MaterialItemId, string Description, decimal Short);

public sealed record WindowPack(WindowSummary Window, IReadOnlyCollection<WindowPackJob> Jobs, IReadOnlyCollection<WindowHandbackItemDto> HandbackItems, DateTime GeneratedAt);

public sealed record FactoryWindowSetting(Guid FactoryId, string FactoryName, bool Enabled, DayOfWeek Day, bool Overridden);

public sealed record FactoryWindowRequest(bool? Enabled, DayOfWeek? Day);

public sealed record WindowJobRequest(Guid WorkOrderId);

public sealed record UpdateWindowRequest(DateTime? Date, [StringLength(5)] string? Start, [StringLength(5)] string? End, decimal? CapacityHours, [StringLength(2000)] string? Notes);

public sealed record WindowReasonRequest([StringLength(1000)] string? Reason);

public sealed record HandbackItemRequest(bool Done, [StringLength(1000)] string? Note);

public sealed record HandbackRequest([StringLength(2000)] string? Notes, string? SignatureDataUrl);

public sealed record WindowHandbackResponse(WindowHandbackResult Result, WindowDetail Window);

public sealed record RoutinesInstalled(int PlansAdded);
