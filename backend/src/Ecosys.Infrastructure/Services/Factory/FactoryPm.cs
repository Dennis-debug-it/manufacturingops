using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services.Factory;

public static class PmFrequencyUnits
{
    public const string Days = "Days";
    public const string Weeks = "Weeks";
    public const string Months = "Months";
    public const string Years = "Years";
    public static readonly IReadOnlyList<string> All = [Days, Weeks, Months, Years];

    public static string? Find(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "day" or "days" or "daily" => Days,
        "week" or "weeks" or "weekly" => Weeks,
        "month" or "months" or "monthly" => Months,
        "year" or "years" or "yearly" or "annual" or "annually" => Years,
        _ => null
    };
}

/// <summary>Due-date arithmetic for factory PM plans.</summary>
public static class PmSchedule
{
    public static DateTime Add(DateTime from, string unit, int interval) => unit switch
    {
        PmFrequencyUnits.Days => from.AddDays(interval),
        PmFrequencyUnits.Weeks => from.AddDays(7 * interval),
        PmFrequencyUnits.Years => from.AddYears(interval),
        _ => from.AddMonths(interval),
    };

    public static double IntervalDays(string unit, int interval) => unit switch
    {
        PmFrequencyUnits.Days => interval,
        PmFrequencyUnits.Weeks => 7.0 * interval,
        PmFrequencyUnits.Years => 365.0 * interval,
        _ => 30.4 * interval,
    };

    /// <summary>Days either side of the due date that still count as on time (at least one).</summary>
    public static int ToleranceDays(PreventiveMaintenancePlan plan) =>
        Math.Max(1, (int)Math.Round(IntervalDays(plan.FrequencyUnit, plan.FrequencyInterval) * plan.TolerancePercent / 100.0));

    /// <summary>Meter units either side of the due reading that still count as on time.</summary>
    public static decimal ToleranceMeter(PreventiveMaintenancePlan plan) =>
        (plan.MeterInterval ?? 0) * plan.TolerancePercent / 100m;

    /// <summary>
    /// The next calendar due date after this one is done (or skipped). Fixed plans keep their
    /// cadence from the due date; floating plans restart from when the work was done. Either way the
    /// result is after <paramref name="doneAt"/>, so a late job doesn't raise the next one at once.
    /// </summary>
    public static DateTime NextDue(PreventiveMaintenancePlan plan, DateTime due, DateTime doneAt, bool skipped = false)
    {
        var fixedCadence = skipped || plan.ScheduleMode == PmScheduleModes.Fixed;
        var next = Add(fixedCadence ? due : doneAt.Date, plan.FrequencyUnit, plan.FrequencyInterval);
        var guard = 0;
        while (next <= doneAt.Date && guard++ < 1000)
        {
            next = Add(next, plan.FrequencyUnit, plan.FrequencyInterval);
        }

        return next;
    }

    /// <summary>The next meter reading at which the plan falls due, after the reading it was done at.</summary>
    public static decimal NextMeterDue(PreventiveMaintenancePlan plan, decimal due, decimal doneAt, bool skipped = false)
    {
        var interval = plan.MeterInterval ?? throw new BusinessRuleException("The plan has no meter interval.");
        var fixedCadence = skipped || plan.ScheduleMode == PmScheduleModes.Fixed;
        var next = (fixedCadence ? due : doneAt) + interval;
        while (next <= doneAt)
        {
            next += interval;
        }

        return next;
    }
}

/// <summary>
/// Moves a factory PM plan on when its job is finished: completed (on time or not) or cancelled
/// (counts as skipped). Called from the work order lifecycle, so it needs only the database.
/// </summary>
public sealed class FactoryPmProgress(AppDbContext dbContext)
{
    public async Task OnJobFinishedAsync(WorkOrder workOrder, bool completed, DateTime at, string? actorName, string? reason, CancellationToken cancellationToken)
    {
        if (workOrder.PreventiveMaintenancePlanId is not { } planId)
        {
            return;
        }

        var plan = await dbContext.PreventiveMaintenancePlans.Include(x => x.Meter)
            .SingleOrDefaultAsync(x => x.Id == planId && x.TenantId == workOrder.TenantId && x.OrgNodeId != null, cancellationToken);
        if (plan is null)
        {
            return;
        }

        var generated = await dbContext.PmPlanEvents
            .Where(x => x.PlanId == plan.Id && x.WorkOrderId == workOrder.Id && x.Kind == PmPlanEventKinds.Generated)
            .OrderByDescending(x => x.OccurredAt)
            .FirstOrDefaultAsync(cancellationToken);
        var item = new PmPlanEvent
        {
            TenantId = plan.TenantId,
            PlanId = plan.Id,
            WorkOrderId = workOrder.Id,
            Kind = completed ? PmPlanEventKinds.Completed : PmPlanEventKinds.Skipped,
            OccurredAt = at,
            ActorName = actorName,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
        };

        if (plan.TriggerType == PmTriggerTypes.Meter)
        {
            var due = generated?.DueMeterValue ?? plan.NextMeterDue ?? 0;
            var reading = plan.Meter?.CurrentValue ?? due;
            item.DueMeterValue = due;
            if (completed)
            {
                item.OnTime = reading <= due + PmSchedule.ToleranceMeter(plan);
                plan.LastMeterValue = reading;
            }

            plan.NextMeterDue = PmSchedule.NextMeterDue(plan, due, reading, skipped: !completed);
        }
        else
        {
            var due = (generated?.DueDate ?? plan.NextPmDate ?? at).Date;
            item.DueDate = due;
            if (completed)
            {
                var tolerance = PmSchedule.ToleranceDays(plan);
                item.OnTime = at.Date >= due.AddDays(-tolerance) && at.Date <= due.AddDays(tolerance);
            }

            plan.NextPmDate = PmSchedule.NextDue(plan, due, at, skipped: !completed);
        }

        if (completed)
        {
            plan.LastPmDate = at;
            plan.DeferReason = null;
            plan.DeferredAt = null;
            plan.DeferRisk = false;
        }

        plan.LastPmWorkOrderId = workOrder.Id;
        dbContext.PmPlanEvents.Add(item);
    }
}

public sealed record SaveFactoryPmPlan(
    string Name,
    Guid AssetId,
    Guid? PmTemplateId,
    string TriggerType,
    string FrequencyUnit,
    int FrequencyInterval,
    DateTime? NextDueDate,
    Guid? MeterId,
    decimal? MeterInterval,
    decimal? NextMeterDue,
    string ScheduleMode,
    int TolerancePercent,
    int DaysBeforeDue,
    string Priority,
    decimal? EstimatedHours,
    bool NeedsLineStop,
    string? Basis,
    IReadOnlyCollection<Guid>? RouteAssetIds,
    bool AutoSchedule = true);

public sealed record PmForecastWeek(DateTime WeekStart, int Jobs, decimal Hours, decimal LineStopHours, decimal CapacityHours, IReadOnlyCollection<PmForecastItem> Items);

public sealed record PmForecastItem(Guid PlanId, string Name, string? AssetCode, DateTime Due, decimal Hours, bool NeedsLineStop, bool Estimated);

public sealed record PmComplianceSummary(int Due, int OnTime, int Late, int Skipped, int Overdue, decimal? Percent, IReadOnlyCollection<PmComplianceRow> Plans);

public sealed record PmComplianceRow(Guid PlanId, string Name, string? AssetCode, int Due, int OnTime, int Late, int Skipped, int Overdue);

public interface IFactoryPmService
{
    Task<PreventiveMaintenancePlan> SaveAsync(Guid tenantId, Guid? id, SaveFactoryPmPlan request, CancellationToken cancellationToken = default);

    /// <summary>Raises a job for every plan that has fallen due (calendar or meter) and has none open. Returns how many.</summary>
    Task<int> GenerateDueAsync(Guid tenantId, DateTime utcNow, CancellationToken cancellationToken = default);

    /// <summary>Raises the plan's job now, whether or not it is due.</summary>
    Task<WorkOrder> GenerateNowAsync(PreventiveMaintenancePlan plan, FactoryActor actor, CancellationToken cancellationToken = default);

    Task SkipAsync(PreventiveMaintenancePlan plan, FactoryActor actor, string reason, CancellationToken cancellationToken = default);

    /// <summary>Puts the next due date back (or forward on the meter). "Until low crop" moves it to the next low-crop season.</summary>
    Task DeferAsync(PreventiveMaintenancePlan plan, FactoryActor actor, string reason, DateTime? newDueDate, decimal? newMeterDue, bool untilLowCrop, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PmForecastWeek>> ForecastAsync(Guid tenantId, IReadOnlyCollection<Guid>? orgNodes, DateTime utcNow, int weeks, CancellationToken cancellationToken = default);

    Task<PmComplianceSummary> ComplianceAsync(Guid tenantId, IReadOnlyCollection<Guid>? orgNodes, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
}

internal sealed class FactoryPmService(
    AppDbContext dbContext,
    IFactoryWorkOrderService workOrderService,
    IFactorySettingsService factorySettingsService) : IFactoryPmService
{
    private static readonly string[] Finished =
        [WorkOrderStatuses.Completed, WorkOrderStatuses.Verified, WorkOrderStatuses.Closed, WorkOrderStatuses.Cancelled, WorkOrderStatuses.Rejected];

    public async Task<PreventiveMaintenancePlan> SaveAsync(Guid tenantId, Guid? id, SaveFactoryPmPlan request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessRuleException("Give the plan a name, e.g. \"Dryer fan bearings – grease\".");
        }

        var asset = await dbContext.Assets.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == request.AssetId, cancellationToken)
            ?? throw new BusinessRuleException("The asset was not found.");
        if (asset.OrgNodeId is null || asset.ClientId is not null)
        {
            throw new BusinessRuleException("Factory PM plans are for factory assets.");
        }

        var plan = id is { } planId
            ? await dbContext.PreventiveMaintenancePlans.Include(x => x.RouteAssets)
                .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == planId && x.OrgNodeId != null, cancellationToken)
                ?? throw new NotFoundException("The PM plan was not found.")
            : new PreventiveMaintenancePlan { TenantId = tenantId, Status = "Active" };

        var unit = PmFrequencyUnits.Find(request.FrequencyUnit)
            ?? throw new BusinessRuleException($"How often must be one of: {string.Join(", ", PmFrequencyUnits.All)}.");
        if (request.FrequencyInterval is < 1 or > 1000)
        {
            throw new BusinessRuleException("The interval must be at least 1.");
        }

        var mode = PmScheduleModes.All.FirstOrDefault(x => string.Equals(x, request.ScheduleMode, StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessRuleException("Schedule must be fixed or floating.");
        var priority = FactoryPriorities.Find(request.Priority) ?? throw new BusinessRuleException("Choose a priority.");
        if (request.TolerancePercent is < 0 or > 50)
        {
            throw new BusinessRuleException("The on-time window must be between 0 and 50%.");
        }

        if (request.PmTemplateId is { } templateId
            && !await dbContext.PmTemplates.AnyAsync(x => x.TenantId == tenantId && x.Id == templateId && x.IsActive && x.DeletedAt == null, cancellationToken))
        {
            throw new BusinessRuleException("The checklist was not found.");
        }

        plan.Name = request.Name.Trim();
        plan.AssetId = asset.Id;
        plan.OrgNodeId = asset.OrgNodeId;
        plan.BranchId = asset.BranchId;
        plan.PmTemplateId = request.PmTemplateId;
        plan.FrequencyUnit = unit;
        plan.FrequencyInterval = request.FrequencyInterval;
        plan.Frequency = $"Every {request.FrequencyInterval} {unit.ToLowerInvariant()}";
        plan.ScheduleMode = mode;
        plan.TolerancePercent = request.TolerancePercent;
        plan.DaysBeforeDue = Math.Clamp(request.DaysBeforeDue, 0, 60);
        plan.Priority = priority;
        plan.EstimatedHours = request.EstimatedHours is > 0 ? request.EstimatedHours : null;
        plan.NeedsLineStop = request.NeedsLineStop;
        plan.Basis = string.IsNullOrWhiteSpace(request.Basis) ? null : request.Basis.Trim();
        plan.AutoSchedule = request.AutoSchedule;

        if (string.Equals(request.TriggerType, PmTriggerTypes.Meter, StringComparison.OrdinalIgnoreCase))
        {
            var meter = request.MeterId is { } meterId
                ? await dbContext.Meters.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == meterId && x.AssetId == asset.Id && x.IsActive, cancellationToken)
                : null;
            if (meter is null)
            {
                throw new BusinessRuleException("Choose one of the asset's meters.");
            }

            if (!meter.IsCumulative)
            {
                throw new BusinessRuleException("A PM can only follow a meter that counts up, such as run hours or kg made tea.");
            }

            if (request.MeterInterval is not > 0)
            {
                throw new BusinessRuleException("Say every how many units it is due, e.g. every 500 run hours.");
            }

            plan.TriggerType = PmTriggerTypes.Meter;
            plan.MeterId = meter.Id;
            plan.MeterInterval = request.MeterInterval;
            plan.MeterBuffer = Math.Round(request.MeterInterval.Value * 0.05m, 2);
            plan.NextMeterDue = request.NextMeterDue ?? plan.NextMeterDue ?? (meter.CurrentValue ?? 0) + request.MeterInterval.Value;
            plan.NextPmDate = null;
        }
        else
        {
            plan.TriggerType = PmTriggerTypes.Calendar;
            plan.MeterId = null;
            plan.MeterInterval = null;
            plan.NextMeterDue = null;
            plan.NextPmDate = (request.NextDueDate ?? plan.NextPmDate
                ?? throw new BusinessRuleException("Choose when it is next due.")).Date;
        }

        var routeIds = (request.RouteAssetIds ?? []).Distinct().ToList();
        plan.IsRoute = routeIds.Count > 0;
        if (plan.IsRoute)
        {
            var found = await dbContext.Assets.Where(x => x.TenantId == tenantId && routeIds.Contains(x.Id) && x.ClientId == null).Select(x => x.Id).ToListAsync(cancellationToken);
            if (found.Count != routeIds.Count)
            {
                throw new BusinessRuleException("Some route assets were not found.");
            }
        }

        dbContext.PmPlanRouteAssets.RemoveRange(plan.RouteAssets.Where(x => !routeIds.Contains(x.AssetId)));
        for (var i = 0; i < routeIds.Count; i++)
        {
            var existing = plan.RouteAssets.FirstOrDefault(x => x.AssetId == routeIds[i]);
            if (existing is null)
            {
                plan.RouteAssets.Add(new PmPlanRouteAsset { TenantId = tenantId, PlanId = plan.Id, AssetId = routeIds[i], Sequence = i });
            }
            else
            {
                existing.Sequence = i;
            }
        }

        if (id is null)
        {
            dbContext.PreventiveMaintenancePlans.Add(plan);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return plan;
    }

    public async Task<int> GenerateDueAsync(Guid tenantId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var plans = await dbContext.PreventiveMaintenancePlans
            .Include(x => x.Meter)
            .Where(x => x.TenantId == tenantId && x.OrgNodeId != null && x.Status == "Active" && x.AutoSchedule)
            .ToListAsync(cancellationToken);
        var open = await OpenPlanIdsAsync(tenantId, cancellationToken);

        var raised = 0;
        foreach (var plan in plans.Where(x => !open.Contains(x.Id) && IsDue(x, utcNow)))
        {
            await GenerateNowAsync(plan, new FactoryActor(null, "PM scheduler"), cancellationToken);
            raised++;
        }

        return raised;
    }

    private static bool IsDue(PreventiveMaintenancePlan plan, DateTime utcNow) => plan.TriggerType == PmTriggerTypes.Meter
        ? plan.NextMeterDue is { } meterDue && plan.Meter is { IsActive: true, CurrentValue: { } current } && current >= meterDue - (plan.MeterBuffer ?? 0)
        : plan.NextPmDate is { } due && due.Date.AddDays(-plan.DaysBeforeDue) <= utcNow.Date;

    private async Task<HashSet<Guid>> OpenPlanIdsAsync(Guid tenantId, CancellationToken cancellationToken) =>
        (await dbContext.WorkOrders
            .Where(x => x.TenantId == tenantId && x.PreventiveMaintenancePlanId != null && x.ClientId == null && !Finished.Contains(x.Status))
            .Select(x => x.PreventiveMaintenancePlanId!.Value)
            .ToListAsync(cancellationToken)).ToHashSet();

    public async Task<WorkOrder> GenerateNowAsync(PreventiveMaintenancePlan plan, FactoryActor actor, CancellationToken cancellationToken = default)
    {
        if ((await OpenPlanIdsAsync(plan.TenantId, cancellationToken)).Contains(plan.Id))
        {
            throw new BusinessRuleException("This plan already has an open job.");
        }

        var asset = await dbContext.Assets.SingleAsync(x => x.Id == plan.AssetId, cancellationToken);
        var meter = plan.MeterId is { } meterId ? await dbContext.Meters.SingleOrDefaultAsync(x => x.Id == meterId, cancellationToken) : null;
        var route = plan.IsRoute
            ? await dbContext.PmPlanRouteAssets.Where(x => x.PlanId == plan.Id).OrderBy(x => x.Sequence)
                .Select(x => new { x.AssetId, x.Asset!.AssetCode, x.Asset.AssetName }).ToListAsync(cancellationToken)
            : [];

        var dueText = plan.TriggerType == PmTriggerTypes.Meter
            ? $"Due at {plan.NextMeterDue:0.##} {meter?.Unit} (now {meter?.CurrentValue:0.##})."
            : $"Due {plan.NextPmDate:dd MMM yyyy}.";
        var workOrder = await workOrderService.CreateAsync(plan.TenantId, actor, new CreateFactoryWorkOrder(
            plan.Name ?? $"PM – {asset.AssetName}",
            FactoryWorkTypes.Preventive,
            plan.Priority,
            AssetId: plan.IsRoute ? null : asset.Id,
            OrgNodeId: plan.OrgNodeId,
            Description: string.Join(" ", new[] { dueText, plan.IsRoute ? $"Route: {string.Join(" → ", route.Select(x => x.AssetCode))}." : null, plan.Basis is null ? null : $"Basis: {plan.Basis}." }.Where(x => x is not null)),
            NeedsLineStop: plan.NeedsLineStop,
            DueDate: plan.TriggerType == PmTriggerTypes.Calendar ? plan.NextPmDate : null,
            EstimatedHours: plan.EstimatedHours,
            Source: FactoryRequestSources.Preventive,
            RequestedByName: actor.Name), cancellationToken);

        workOrder.PreventiveMaintenancePlanId = plan.Id;
        workOrder.IsPreventiveMaintenance = true;
        if (plan.PmTemplateId is { } templateId)
        {
            await CopyChecklistAsync(workOrder, templateId, route.Count > 0 ? route.Select(x => $"{x.AssetCode} · {x.AssetName}").ToList() : [null], cancellationToken);
        }

        if (workOrder.Status == WorkOrderStatuses.Requested)
        {
            // The plan is the approval: scheduled PM doesn't wait in the request queue.
            await workOrderService.ApplyAsync(workOrder, actor, new FactoryTransition(FactoryWorkOrderActions.Approve, "Scheduled PM."), cancellationToken);
        }

        plan.LastGeneratedAt = OccurredAt.UtcNow;
        dbContext.PmPlanEvents.Add(new PmPlanEvent
        {
            TenantId = plan.TenantId,
            PlanId = plan.Id,
            WorkOrderId = workOrder.Id,
            Kind = PmPlanEventKinds.Generated,
            DueDate = plan.TriggerType == PmTriggerTypes.Calendar ? plan.NextPmDate : null,
            DueMeterValue = plan.TriggerType == PmTriggerTypes.Meter ? plan.NextMeterDue : null,
            OccurredAt = OccurredAt.UtcNow,
            ActorName = actor.Name,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return workOrder;
    }

    private async Task CopyChecklistAsync(WorkOrder workOrder, Guid templateId, IReadOnlyList<string?> sections, CancellationToken cancellationToken)
    {
        var template = await dbContext.PmTemplates.Include(x => x.Questions).ThenInclude(x => x.Section)
            .SingleOrDefaultAsync(x => x.Id == templateId, cancellationToken);
        if (template is null)
        {
            return;
        }

        var questions = template.Questions.Where(x => x.IsActive)
            .OrderBy(x => x.Section?.DisplayOrder ?? int.MaxValue).ThenBy(x => x.DisplayOrder).ToList();
        var order = 0;
        foreach (var (section, sectionIndex) in sections.Select((x, i) => (x, i)))
        {
            foreach (var question in questions)
            {
                var type = question.ResponseType.Trim().ToLowerInvariant();
                dbContext.WorkOrderChecklistItems.Add(new WorkOrderChecklistItem
                {
                    TenantId = workOrder.TenantId,
                    WorkOrderId = workOrder.Id,
                    PmTemplateQuestionId = question.Id,
                    SectionName = section ?? question.Section?.SectionName ?? question.SectionName,
                    SectionOrder = section is null ? question.Section?.DisplayOrder ?? 0 : sectionIndex,
                    QuestionText = question.Prompt.Trim(),
                    HelperText = question.HelperText,
                    InputType = type,
                    QuestionType = type,
                    Unit = question.Unit,
                    MinValue = question.MinValue,
                    MaxValue = question.MaxValue,
                    IsRequired = question.IsRequired,
                    EvidenceRequired = question.EvidenceRequired,
                    TechnicianRemarksAllowed = question.TechnicianRemarksAllowed,
                    RequiresNoteOnFail = question.RequiresNoteOnFail,
                    SortOrder = order++,
                    OptionsJson = question.OptionsJson,
                });
            }
        }

        workOrder.PmTemplateId = template.Id;
        workOrder.PmTemplateNameSnapshot = template.Name;
        workOrder.PmTemplateVersionSnapshot = template.Version;
    }

    public async Task SkipAsync(PreventiveMaintenancePlan plan, FactoryActor actor, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new BusinessRuleException("Say why this PM is skipped.");
        }

        var openJob = await dbContext.WorkOrders
            .Where(x => x.PreventiveMaintenancePlanId == plan.Id && !Finished.Contains(x.Status))
            .Select(x => x.WorkOrderNumber).FirstOrDefaultAsync(cancellationToken);
        if (openJob is not null)
        {
            throw new BusinessRuleException($"Job {openJob} is open for this PM: cancel it with the reason instead, which skips this PM.");
        }

        var now = OccurredAt.UtcNow;
        var item = new PmPlanEvent { TenantId = plan.TenantId, PlanId = plan.Id, Kind = PmPlanEventKinds.Skipped, OccurredAt = now, Reason = reason.Trim(), ActorName = actor.Name };
        if (plan.TriggerType == PmTriggerTypes.Meter)
        {
            var due = plan.NextMeterDue ?? 0;
            var current = plan.MeterId is { } meterId ? await dbContext.Meters.Where(x => x.Id == meterId).Select(x => x.CurrentValue).SingleOrDefaultAsync(cancellationToken) : null;
            item.DueMeterValue = due;
            plan.NextMeterDue = PmSchedule.NextMeterDue(plan, due, Math.Max(current ?? due, due), skipped: true);
        }
        else
        {
            var due = (plan.NextPmDate ?? now).Date;
            item.DueDate = due;
            plan.NextPmDate = PmSchedule.NextDue(plan, due, due > now ? due : now, skipped: true);
        }

        dbContext.PmPlanEvents.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeferAsync(PreventiveMaintenancePlan plan, FactoryActor actor, string reason, DateTime? newDueDate, decimal? newMeterDue, bool untilLowCrop, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new BusinessRuleException("Say why this PM is deferred.");
        }

        var now = OccurredAt.UtcNow;
        var asset = await dbContext.Assets.SingleAsync(x => x.Id == plan.AssetId, cancellationToken);
        var item = new PmPlanEvent { TenantId = plan.TenantId, PlanId = plan.Id, Kind = PmPlanEventKinds.Deferred, OccurredAt = now, Reason = reason.Trim(), ActorName = actor.Name };

        if (plan.TriggerType == PmTriggerTypes.Meter)
        {
            if (untilLowCrop)
            {
                throw new BusinessRuleException("A meter-based PM is deferred to a later reading, not a date.");
            }

            if (newMeterDue is not { } meterDue || meterDue <= (plan.NextMeterDue ?? 0))
            {
                throw new BusinessRuleException("Defer to a later meter reading than it is due at now.");
            }

            item.DueMeterValue = plan.NextMeterDue;
            plan.NextMeterDue = meterDue;
        }
        else
        {
            DateTime target;
            if (untilLowCrop)
            {
                var settings = await factorySettingsService.GetOrCreateAsync(plan.TenantId, cancellationToken);
                var month = settings.LowCropStartMonth ?? throw new BusinessRuleException("Set the low-crop season in factory settings first.");
                var candidate = new DateTime(now.Year, month, 1, 0, 0, 0, DateTimeKind.Utc);
                target = candidate > now.Date ? candidate : candidate.AddYears(1);
            }
            else
            {
                target = (newDueDate ?? throw new BusinessRuleException("Choose the new due date.")).Date;
            }

            var current = plan.NextPmDate ?? now.Date;
            if (target <= current.Date)
            {
                throw new BusinessRuleException("The new date must be after the current due date.");
            }

            item.DueDate = plan.NextPmDate;
            plan.NextPmDate = target;
            var openJob = await dbContext.WorkOrders
                .Where(x => x.PreventiveMaintenancePlanId == plan.Id && !Finished.Contains(x.Status))
                .FirstOrDefaultAsync(cancellationToken);
            if (openJob is not null)
            {
                openJob.DueDate = target;
            }
        }

        plan.DeferReason = reason.Trim();
        plan.DeferredAt = now;
        plan.DeferRisk = untilLowCrop && string.Equals(asset.Criticality, "A", StringComparison.OrdinalIgnoreCase);
        dbContext.PmPlanEvents.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PmForecastWeek>> ForecastAsync(Guid tenantId, IReadOnlyCollection<Guid>? orgNodes, DateTime utcNow, int weeks, CancellationToken cancellationToken = default)
    {
        weeks = Math.Clamp(weeks, 1, 52);
        var start = utcNow.Date.AddDays(-(((int)utcNow.DayOfWeek + 6) % 7));
        var end = start.AddDays(7 * weeks);

        var query = dbContext.PreventiveMaintenancePlans.AsNoTracking()
            .Include(x => x.Meter).Include(x => x.Asset)
            .Where(x => x.TenantId == tenantId && x.OrgNodeId != null && x.Status == "Active" && x.AutoSchedule);
        if (orgNodes is not null)
        {
            query = query.Where(x => orgNodes.Contains(x.OrgNodeId!.Value));
        }

        var plans = await query.ToListAsync(cancellationToken);
        var rates = await MeterRatesAsync(plans.Where(x => x.MeterId != null).Select(x => x.MeterId!.Value).ToList(), utcNow, cancellationToken);

        var items = new List<PmForecastItem>();
        foreach (var plan in plans)
        {
            var hours = plan.EstimatedHours ?? 1m;
            if (plan.TriggerType == PmTriggerTypes.Meter)
            {
                if (plan.NextMeterDue is not { } meterDue || plan.Meter?.CurrentValue is not { } current
                    || !rates.TryGetValue(plan.MeterId!.Value, out var perDay) || perDay <= 0)
                {
                    continue;
                }

                var due = utcNow.Date.AddDays((double)Math.Max(0, (meterDue - current) / perDay));
                var step = (double)((plan.MeterInterval ?? 0) / perDay);
                for (var i = 0; due < end && i < 60; i++)
                {
                    items.Add(new PmForecastItem(plan.Id, plan.Name ?? "PM", plan.Asset?.AssetCode, due < start ? start : due, hours, plan.NeedsLineStop, true));
                    if (step < 1) break;
                    due = due.AddDays(step);
                }
            }
            else if (plan.NextPmDate is { } next)
            {
                var due = next.Date;
                for (var i = 0; due < end && i < 400; i++)
                {
                    items.Add(new PmForecastItem(plan.Id, plan.Name ?? "PM", plan.Asset?.AssetCode, due < start ? start : due, hours, plan.NeedsLineStop, false));
                    due = PmSchedule.Add(due, plan.FrequencyUnit, plan.FrequencyInterval);
                }
            }
        }

        var technicians = await dbContext.Technicians.CountAsync(x => x.TenantId == tenantId && x.IsActive, cancellationToken);
        var settings = await factorySettingsService.GetOrCreateAsync(tenantId, cancellationToken);
        var capacity = technicians * settings.PmHoursPerTechnicianWeek;

        return Enumerable.Range(0, weeks).Select(w =>
        {
            var weekStart = start.AddDays(7 * w);
            var inWeek = items.Where(x => x.Due >= weekStart && x.Due < weekStart.AddDays(7)).OrderBy(x => x.Due).ToList();
            return new PmForecastWeek(weekStart, inWeek.Count, inWeek.Sum(x => x.Hours), inWeek.Where(x => x.NeedsLineStop).Sum(x => x.Hours), capacity, inWeek);
        }).ToList();
    }

    /// <summary>Average use per day over the last 30 days, per meter.</summary>
    private async Task<Dictionary<Guid, decimal>> MeterRatesAsync(IReadOnlyCollection<Guid> meterIds, DateTime utcNow, CancellationToken cancellationToken)
    {
        if (meterIds.Count == 0)
        {
            return [];
        }

        var since = utcNow.AddDays(-30);
        var readings = await dbContext.MeterReadings.AsNoTracking()
            .Where(x => meterIds.Contains(x.MeterId) && x.ReadAt >= since)
            .Select(x => new { x.MeterId, x.ReadAt, x.Value })
            .ToListAsync(cancellationToken);
        return readings.GroupBy(x => x.MeterId).Select(g =>
        {
            var first = g.MinBy(x => x.ReadAt)!;
            var last = g.MaxBy(x => x.ReadAt)!;
            var days = (decimal)(last.ReadAt - first.ReadAt).TotalDays;
            return (g.Key, Rate: days >= 1 ? (last.Value - first.Value) / days : 0);
        }).ToDictionary(x => x.Key, x => x.Rate);
    }

    public async Task<PmComplianceSummary> ComplianceAsync(Guid tenantId, IReadOnlyCollection<Guid>? orgNodes, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var plans = dbContext.PreventiveMaintenancePlans.AsNoTracking().Where(x => x.TenantId == tenantId && x.OrgNodeId != null);
        if (orgNodes is not null)
        {
            plans = plans.Where(x => orgNodes.Contains(x.OrgNodeId!.Value));
        }

        var planInfo = await plans.Select(x => new { x.Id, x.Name, x.Asset!.AssetCode, x.TriggerType, x.NextPmDate, x.FrequencyUnit, x.FrequencyInterval, x.TolerancePercent }).ToListAsync(cancellationToken);
        var ids = planInfo.Select(x => x.Id).ToList();
        var events = await dbContext.PmPlanEvents.AsNoTracking()
            .Where(x => ids.Contains(x.PlanId) && x.OccurredAt >= fromUtc && x.OccurredAt < toUtc
                && (x.Kind == PmPlanEventKinds.Completed || x.Kind == PmPlanEventKinds.Skipped))
            .Select(x => new { x.PlanId, x.Kind, x.OnTime })
            .ToListAsync(cancellationToken);

        // Due and not done: the date (plus the on-time window) has passed with no completion.
        var now = DateTime.UtcNow;
        var overdue = planInfo.Where(x => x.TriggerType == PmTriggerTypes.Calendar && x.NextPmDate is { } due
                && due.AddDays(Math.Max(1, Math.Round(PmSchedule.IntervalDays(x.FrequencyUnit, x.FrequencyInterval) * x.TolerancePercent / 100.0))) < now
                && due >= fromUtc)
            .Select(x => x.Id).ToHashSet();

        var rows = planInfo.Select(p =>
        {
            var mine = events.Where(e => e.PlanId == p.Id).ToList();
            var onTime = mine.Count(e => e.Kind == PmPlanEventKinds.Completed && e.OnTime == true);
            var late = mine.Count(e => e.Kind == PmPlanEventKinds.Completed && e.OnTime != true);
            var skipped = mine.Count(e => e.Kind == PmPlanEventKinds.Skipped);
            var od = overdue.Contains(p.Id) ? 1 : 0;
            return new PmComplianceRow(p.Id, p.Name ?? "PM", p.AssetCode, onTime + late + skipped + od, onTime, late, skipped, od);
        }).Where(x => x.Due > 0).OrderBy(x => x.Due == 0 ? 1 : (decimal)x.OnTime / x.Due).ThenBy(x => x.Name).ToList();

        var dueTotal = rows.Sum(x => x.Due);
        return new PmComplianceSummary(dueTotal, rows.Sum(x => x.OnTime), rows.Sum(x => x.Late), rows.Sum(x => x.Skipped), rows.Sum(x => x.Overdue),
            dueTotal == 0 ? null : Math.Round(100m * rows.Sum(x => x.OnTime) / dueTotal, 1), rows);
    }
}

/// <summary>Raises due factory PM jobs for every tenant with plans.</summary>
public interface IFactoryPmRunner
{
    Task<int> RunAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}

internal sealed class FactoryPmRunner(AppDbContext dbContext, IFactoryPmService pmService, IMaintenanceWindowService windowService, ILogger<FactoryPmRunner> logger) : IFactoryPmRunner
{
    public async Task<int> RunAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        // Every tenant with a factory: PM first, so routine and line-stop jobs it raises are queued into the window.
        var tenants = await dbContext.OrgNodes.AsNoTracking()
            .Where(x => x.Level == OrgNodeLevels.Factory && x.IsActive && x.Tenant!.IsActive)
            .Select(x => x.TenantId).Distinct().ToListAsync(cancellationToken);
        var raised = 0;
        foreach (var tenantId in tenants)
        {
            try
            {
                raised += await pmService.GenerateDueAsync(tenantId, utcNow, cancellationToken);
                await windowService.EnsureUpcomingAsync(tenantId, utcNow, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Factory PM and window planning for tenant {TenantId} failed.", tenantId);
                dbContext.ChangeTracker.Clear();
            }
        }

        return raised;
    }
}

/// <summary>Runs <see cref="IFactoryPmRunner"/> every 30 minutes, so meter-triggered PM follows readings closely.</summary>
public sealed class FactoryPmWorker(IServiceScopeFactory scopeFactory, ILogger<FactoryPmWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var raised = await scope.ServiceProvider.GetRequiredService<IFactoryPmRunner>().RunAsync(DateTime.UtcNow, stoppingToken);
                if (raised > 0)
                {
                    logger.LogInformation("Factory PM worker raised {Count} job(s).", raised);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Factory PM cycle failed.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
