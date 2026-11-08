using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services.Factory;

/// <summary>A breakdown, as reliability analysis sees it.</summary>
public sealed record FailureRecord(
    Guid WorkOrderId,
    string WorkOrderNumber,
    Guid AssetId,
    DateTime At,
    decimal? DowntimeHours,
    Guid? ProblemId,
    string? Problem,
    string? Cause,
    string? Remedy,
    bool FlaggedRework);

/// <param name="Pattern">Regular (failures come at a steady interval: wear-out, PM can catch it), Irregular (random: watch condition instead) or Unknown (too few).</param>
/// <param name="MeanRunBetween">Mean run on the main meter between failures (e.g. run hours), when its readings cover them.</param>
public sealed record AssetReliability(
    Guid AssetId,
    string AssetCode,
    string AssetName,
    string? Location,
    string? Criticality,
    int Failures,
    decimal? MtbfHours,
    decimal? MttrHours,
    decimal DowntimeHours,
    decimal? MeanDaysBetween,
    decimal? Variation,
    string Pattern,
    decimal? MeanRunBetween,
    string? MeterUnit,
    string? TopProblem,
    int TopProblemCount,
    DateTime LastFailureAt,
    int ActivePlans);

public static class PmSuggestionKinds
{
    /// <summary>Breaks down regularly and has no PM: start one.</summary>
    public const string NoPlan = "NoPlan";
    /// <summary>Has PM, but less often than it breaks down: shorten it.</summary>
    public const string PlanTooLong = "PlanTooLong";
    /// <summary>PM already runs more often than it breaks down, yet it still does: review what the PM checks.</summary>
    public const string PlanNotPreventing = "PlanNotPreventing";
    /// <summary>Breakdowns come at random intervals: time-based PM will not catch them; watch condition.</summary>
    public const string Random = "Random";
}

/// <summary>A suggested change to an asset's PM, worked out from its breakdown history.</summary>
/// <param name="TriggerType">Calendar or Meter, for a plan to create or shorten; null when there is nothing to schedule.</param>
public sealed record PmSuggestion(
    Guid AssetId,
    string AssetCode,
    string AssetName,
    string Kind,
    string Reason,
    int Failures,
    decimal? MeanDaysBetween,
    string? TopProblem,
    string? TriggerType,
    string? FrequencyUnit,
    int? FrequencyInterval,
    Guid? MeterId,
    decimal? MeterInterval,
    string? MeterUnit,
    Guid? PlanId,
    string? PlanName,
    string? CurrentInterval);

public sealed record RepeatFailureSummary(int Failures, int Repeats, decimal? RepeatRatePercent, int AssetsWithRepeats, int RepeatWithinDays);

public sealed record FailureModeRow(Guid? ProblemId, string Problem, int Failures, int Repeats, int Assets, decimal? MeanDaysBetweenRepeats, string? TopCause, string? TopRemedy);

public sealed record RepeatAssetRow(Guid AssetId, string AssetCode, string AssetName, int Failures, int Repeats, string? TopProblem, DateTime LastFailureAt);

public sealed record RepeatChainLink(Guid WorkOrderId, string WorkOrderNumber, DateTime At, decimal? DaysSincePrevious, string? Cause, string? Remedy);

/// <summary>The same fault on the same asset again and again, each within the repeat window of the last.</summary>
public sealed record RepeatChain(Guid AssetId, string AssetCode, string AssetName, string Problem, IReadOnlyList<RepeatChainLink> Failures);

public sealed record RepeatFailureAnalysis(
    RepeatFailureSummary Summary,
    IReadOnlyList<FailureModeRow> ByMode,
    IReadOnlyList<RepeatAssetRow> ByAsset,
    IReadOnlyList<RepeatChain> Chains);

public interface IReliabilityService
{
    /// <summary>Every asset that broke down in the period, worst first, with MTBF, MTTR and its failure pattern.</summary>
    Task<IReadOnlyList<AssetReliability>> AssetsAsync(Guid tenantId, IReadOnlySet<Guid>? orgNodes, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>PM changes the breakdown history argues for, for assets with enough breakdowns to go on.</summary>
    Task<IReadOnlyList<PmSuggestion>> SuggestionsAsync(Guid tenantId, IReadOnlySet<Guid>? orgNodes, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>Creates the suggested plan, or shortens the existing one, as worked out now (not as the caller last saw it).</summary>
    Task<PreventiveMaintenancePlan> ApplyAsync(Guid tenantId, Guid assetId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>Breakdowns of the same kind on the same asset coming back within <paramref name="repeatWithinDays"/>.</summary>
    Task<RepeatFailureAnalysis> RepeatFailuresAsync(Guid tenantId, IReadOnlySet<Guid>? orgNodes, DateTime fromUtc, DateTime toUtc, int repeatWithinDays, CancellationToken cancellationToken = default);
}

internal sealed class ReliabilityService(AppDbContext dbContext, IFactoryPmService pmService) : IReliabilityService
{
    /// <summary>Fewer breakdowns than this and an interval says nothing.</summary>
    public const int MinimumFailures = 3;

    /// <summary>PM at this share of the mean time between breakdowns catches most wear before it fails.</summary>
    public const decimal PmShareOfMtbf = 0.7m;

    /// <summary>Breakdown intervals varying more than this (coefficient of variation) are treated as random.</summary>
    public const decimal RandomAbove = 1.0m;

    private static readonly string[] Dropped = [WorkOrderStatuses.Cancelled, WorkOrderStatuses.Rejected];

    public async Task<IReadOnlyList<AssetReliability>> AssetsAsync(Guid tenantId, IReadOnlySet<Guid>? orgNodes, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var failures = await FailuresAsync(tenantId, orgNodes, fromUtc, toUtc, cancellationToken);
        var assets = await AssetInfoAsync(tenantId, failures.Select(x => x.AssetId).Distinct().ToList(), cancellationToken);
        var runs = await RunBetweenAsync(tenantId, failures, cancellationToken);
        var periodHours = (decimal)(toUtc - fromUtc).TotalHours;
        var assetIds = assets.Keys.ToList();
        var plans = (await dbContext.PreventiveMaintenancePlans.AsNoTracking()
                .Where(x => x.TenantId == tenantId && assetIds.Contains(x.AssetId) && x.Status == "Active")
                .GroupBy(x => x.AssetId)
                .Select(g => new { AssetId = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.AssetId, x => x.Count);

        return failures.GroupBy(x => x.AssetId)
            .Where(g => assets.ContainsKey(g.Key))
            .Select(g => Analyse(assets[g.Key], g.OrderBy(x => x.At).ToList(), periodHours, runs.GetValueOrDefault(g.Key)) with { ActivePlans = plans.GetValueOrDefault(g.Key) })
            .OrderByDescending(x => x.Failures)
            .ThenByDescending(x => x.DowntimeHours)
            .ToList();
    }

    public async Task<IReadOnlyList<PmSuggestion>> SuggestionsAsync(Guid tenantId, IReadOnlySet<Guid>? orgNodes, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var reliability = (await AssetsAsync(tenantId, orgNodes, fromUtc, toUtc, cancellationToken)).Where(x => x.Failures >= MinimumFailures).ToList();
        var assetIds = reliability.Select(x => x.AssetId).ToList();
        var plans = (await dbContext.PreventiveMaintenancePlans.AsNoTracking()
                .Where(x => x.TenantId == tenantId && assetIds.Contains(x.AssetId) && x.Status == "Active" && !x.IsRoute)
                .Select(x => new PlanInfo(x.Id, x.AssetId, x.Name ?? x.Frequency, x.TriggerType, x.FrequencyUnit, x.FrequencyInterval, x.MeterId, x.MeterInterval))
                .ToListAsync(cancellationToken))
            .ToLookup(x => x.AssetId);
        var meters = await MainMetersAsync(tenantId, assetIds, cancellationToken);

        return reliability
            .Select(x => Suggest(x, plans[x.AssetId].ToList(), meters.GetValueOrDefault(x.AssetId)))
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();
    }

    public async Task<PreventiveMaintenancePlan> ApplyAsync(Guid tenantId, Guid assetId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var asset = await dbContext.Assets.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == assetId, cancellationToken)
            ?? throw new NotFoundException("Asset was not found.");
        var orgNodes = asset.OrgNodeId is { } node ? new HashSet<Guid> { node } : null;
        var suggestion = (await SuggestionsAsync(tenantId, orgNodes, fromUtc, toUtc, cancellationToken)).SingleOrDefault(x => x.AssetId == assetId)
            ?? throw new BusinessRuleException("There is no PM change to make for this asset now.");
        if (suggestion.Kind is not (PmSuggestionKinds.NoPlan or PmSuggestionKinds.PlanTooLong) || suggestion.TriggerType is null)
            throw new BusinessRuleException("This suggestion is a review, not a schedule change; there is no plan to create or shorten.");

        var meter = suggestion.TriggerType == PmTriggerTypes.Meter;
        var basis = $"Reliability: {suggestion.Failures} breakdowns, about every {suggestion.MeanDaysBetween:0} days";
        if (suggestion.Kind == PmSuggestionKinds.NoPlan)
        {
            var current = meter ? await dbContext.Meters.Where(x => x.Id == suggestion.MeterId).Select(x => x.CurrentValue).SingleOrDefaultAsync(cancellationToken) : null;
            return await pmService.SaveAsync(tenantId, null, new SaveFactoryPmPlan(
                $"{asset.AssetName}: {(suggestion.TopProblem is null ? "preventive" : suggestion.TopProblem.ToLowerInvariant())} check",
                asset.Id,
                null,
                suggestion.TriggerType,
                suggestion.FrequencyUnit ?? PmFrequencyUnits.Weeks,
                suggestion.FrequencyInterval ?? 1,
                meter ? null : DateTime.UtcNow.Date.AddDays(7),
                suggestion.MeterId,
                suggestion.MeterInterval,
                meter ? (current ?? 0) + (suggestion.MeterInterval ?? 0) : null,
                PmScheduleModes.Floating,
                10,
                3,
                asset.Criticality == AssetCriticality.A ? "High" : "Medium",
                null,
                false,
                basis,
                null), cancellationToken);
        }

        var plan = await dbContext.PreventiveMaintenancePlans.AsNoTracking().SingleAsync(x => x.TenantId == tenantId && x.Id == suggestion.PlanId, cancellationToken);
        return await pmService.SaveAsync(tenantId, plan.Id, new SaveFactoryPmPlan(
            plan.Name ?? plan.Frequency,
            plan.AssetId,
            plan.PmTemplateId,
            plan.TriggerType,
            meter ? plan.FrequencyUnit : suggestion.FrequencyUnit!,
            meter ? plan.FrequencyInterval : suggestion.FrequencyInterval!.Value,
            plan.NextPmDate,
            plan.MeterId,
            meter ? suggestion.MeterInterval : plan.MeterInterval,
            plan.NextMeterDue,
            plan.ScheduleMode,
            plan.TolerancePercent,
            plan.DaysBeforeDue,
            plan.Priority,
            plan.EstimatedHours,
            plan.NeedsLineStop,
            plan.Basis is null ? basis : $"{plan.Basis}; {basis}",
            null,
            plan.AutoSchedule), cancellationToken);
    }

    public async Task<RepeatFailureAnalysis> RepeatFailuresAsync(Guid tenantId, IReadOnlySet<Guid>? orgNodes, DateTime fromUtc, DateTime toUtc, int repeatWithinDays, CancellationToken cancellationToken = default)
    {
        var window = Math.Clamp(repeatWithinDays, 1, 365);
        var failures = await FailuresAsync(tenantId, orgNodes, fromUtc, toUtc, cancellationToken);
        var assets = await AssetInfoAsync(tenantId, failures.Select(x => x.AssetId).Distinct().ToList(), cancellationToken);

        // A failure repeats the one before it when it is the same fault on the same asset within the window.
        var repeats = new HashSet<Guid>();
        var gaps = new List<(Guid? ProblemId, double Days)>();
        var chains = new List<RepeatChain>();
        foreach (var group in failures.Where(x => x.ProblemId != null).GroupBy(x => (x.AssetId, x.ProblemId)))
        {
            var ordered = group.OrderBy(x => x.At).ToList();
            var chain = new List<RepeatChainLink>();
            for (var i = 0; i < ordered.Count; i++)
            {
                var gap = i == 0 ? (double?)null : (ordered[i].At - ordered[i - 1].At).TotalDays;
                if (gap is { } days && days <= window)
                {
                    repeats.Add(ordered[i].WorkOrderId);
                    gaps.Add((group.Key.ProblemId, days));
                    if (chain.Count == 0)
                        chain.Add(Link(ordered[i - 1], null));
                    chain.Add(Link(ordered[i], (decimal)Math.Round(days, 1)));
                }
                else
                {
                    Flush();
                }
            }

            Flush();

            void Flush()
            {
                if (chain.Count >= 2 && assets.TryGetValue(group.Key.AssetId, out var asset))
                    chains.Add(new RepeatChain(asset.Id, asset.Code, asset.Name, ordered[0].Problem ?? "—", chain.ToList()));
                chain.Clear();
            }
        }

        var byMode = failures.GroupBy(x => (x.ProblemId, Problem: x.Problem ?? "No problem code"))
            .Select(g => new FailureModeRow(
                g.Key.ProblemId,
                g.Key.Problem,
                g.Count(),
                g.Count(x => repeats.Contains(x.WorkOrderId)),
                g.Select(x => x.AssetId).Distinct().Count(),
                gaps.Where(x => x.ProblemId == g.Key.ProblemId && g.Key.ProblemId != null).Select(x => x.Days).DefaultIfEmpty().Average() is var mean && mean > 0 ? Math.Round((decimal)mean, 1) : null,
                Top(g.Select(x => x.Cause)),
                Top(g.Select(x => x.Remedy))))
            .OrderByDescending(x => x.Repeats)
            .ThenByDescending(x => x.Failures)
            .ToList();

        var byAsset = failures.GroupBy(x => x.AssetId)
            .Where(g => assets.ContainsKey(g.Key))
            .Select(g => new RepeatAssetRow(g.Key, assets[g.Key].Code, assets[g.Key].Name, g.Count(), g.Count(x => repeats.Contains(x.WorkOrderId)),
                Top(g.Select(x => x.Problem)), g.Max(x => x.At)))
            .Where(x => x.Repeats > 0)
            .OrderByDescending(x => x.Repeats)
            .ThenByDescending(x => x.Failures)
            .Take(20)
            .ToList();

        return new RepeatFailureAnalysis(
            new RepeatFailureSummary(failures.Count, repeats.Count, failures.Count == 0 ? null : Math.Round(100m * repeats.Count / failures.Count, 1),
                failures.Where(x => repeats.Contains(x.WorkOrderId)).Select(x => x.AssetId).Distinct().Count(), window),
            byMode,
            byAsset,
            chains.OrderByDescending(x => x.Failures.Count).ThenByDescending(x => x.Failures[^1].At).Take(20).ToList());

        static RepeatChainLink Link(FailureRecord failure, decimal? days) =>
            new(failure.WorkOrderId, failure.WorkOrderNumber, failure.At, days, failure.Cause, failure.Remedy);
    }

    private sealed record AssetInfo(Guid Id, string Code, string Name, string? Location, string? Criticality);

    private sealed record PlanInfo(Guid Id, Guid AssetId, string Name, string TriggerType, string FrequencyUnit, int FrequencyInterval, Guid? MeterId, decimal? MeterInterval);

    private sealed record MainMeter(Guid Id, string Unit);

    private sealed record RunInfo(decimal Mean, string Unit);

    private async Task<List<FailureRecord>> FailuresAsync(Guid tenantId, IReadOnlySet<Guid>? orgNodes, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var rows = await dbContext.WorkOrders.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsBreakdown && x.AssetId != null && !Dropped.Contains(x.Status)
                && (x.DowntimeStartedAt ?? x.CreatedAt) >= fromUtc && (x.DowntimeStartedAt ?? x.CreatedAt) < toUtc
                && (orgNodes == null || (x.Asset!.OrgNodeId != null && orgNodes.Contains(x.Asset.OrgNodeId.Value))))
            .Select(x => new
            {
                x.Id,
                x.WorkOrderNumber,
                AssetId = x.AssetId!.Value,
                At = x.DowntimeStartedAt ?? x.CreatedAt,
                x.DowntimeStartedAt,
                x.DowntimeEndedAt,
                x.FailureProblemCodeId,
                Problem = x.FailureProblemCode != null ? x.FailureProblemCode.Name : null,
                Cause = x.FailureCauseCode != null ? x.FailureCauseCode.Name : null,
                Remedy = x.FailureRemedyCode != null ? x.FailureRemedyCode.Name : null,
                Rework = x.ReworkOfWorkOrderId != null,
            })
            .ToListAsync(cancellationToken);
        return rows.Select(x => new FailureRecord(x.Id, x.WorkOrderNumber, x.AssetId, x.At,
                x.DowntimeStartedAt is { } start && x.DowntimeEndedAt is { } end ? (decimal)(end - start).TotalHours : null,
                x.FailureProblemCodeId, x.Problem, x.Cause, x.Remedy, x.Rework))
            .OrderBy(x => x.At)
            .ToList();
    }

    private async Task<Dictionary<Guid, AssetInfo>> AssetInfoAsync(Guid tenantId, List<Guid> assetIds, CancellationToken cancellationToken) =>
        await dbContext.Assets.AsNoTracking()
            .Where(x => x.TenantId == tenantId && assetIds.Contains(x.Id))
            .Select(x => new AssetInfo(x.Id, x.AssetCode, x.AssetName, x.OrgNode != null ? x.OrgNode.Name : null, x.Criticality))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

    /// <summary>Each asset's main counter (preferably run hours), for meter-based intervals.</summary>
    private async Task<Dictionary<Guid, MainMeter>> MainMetersAsync(Guid tenantId, List<Guid> assetIds, CancellationToken cancellationToken) =>
        (await dbContext.Meters.AsNoTracking()
            .Where(x => x.TenantId == tenantId && assetIds.Contains(x.AssetId) && x.IsActive && x.IsCumulative && (x.IsPrimary || x.MeterType == MeterTypes.RunHours))
            .Select(x => new { x.AssetId, x.Id, x.Unit, x.IsPrimary, RunHours = x.MeterType == MeterTypes.RunHours })
            .ToListAsync(cancellationToken))
        .GroupBy(x => x.AssetId)
        .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.RunHours).ThenByDescending(x => x.IsPrimary).Select(x => new MainMeter(x.Id, x.Unit)).First());

    /// <summary>Mean run on the main meter between consecutive breakdowns, from the readings at each.</summary>
    private async Task<Dictionary<Guid, RunInfo>> RunBetweenAsync(Guid tenantId, List<FailureRecord> failures, CancellationToken cancellationToken)
    {
        var assetIds = failures.GroupBy(x => x.AssetId).Where(g => g.Count() >= 2).Select(g => g.Key).ToList();
        if (assetIds.Count == 0)
            return [];

        var meters = await MainMetersAsync(tenantId, assetIds, cancellationToken);
        var meterIds = meters.Values.Select(x => x.Id).ToList();
        var from = failures.Min(x => x.At).AddDays(-60);
        var readings = (await dbContext.MeterReadings.AsNoTracking()
                .Where(x => x.TenantId == tenantId && meterIds.Contains(x.MeterId) && x.ReadAt >= from)
                .Select(x => new { x.MeterId, x.ReadAt, x.Value, x.IsReset })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.MeterId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.ReadAt).ToList());

        var result = new Dictionary<Guid, RunInfo>();
        foreach (var (assetId, meter) in meters)
        {
            if (!readings.TryGetValue(meter.Id, out var series))
                continue;
            var atFailures = failures.Where(x => x.AssetId == assetId).OrderBy(x => x.At)
                .Select(f => series.LastOrDefault(r => r.ReadAt <= f.At))
                .ToList();
            var runs = new List<decimal>();
            for (var i = 1; i < atFailures.Count; i++)
            {
                if (atFailures[i - 1] is { } before && atFailures[i] is { } after && after.Value > before.Value
                    && !series.Any(r => r.IsReset && r.ReadAt > before.ReadAt && r.ReadAt <= after.ReadAt))
                    runs.Add(after.Value - before.Value);
            }

            if (runs.Count >= 2)
                result[assetId] = new RunInfo(Math.Round(runs.Average(), 1), meter.Unit);
        }

        return result;
    }

    private static AssetReliability Analyse(AssetInfo asset, List<FailureRecord> failures, decimal periodHours, RunInfo? run)
    {
        var downtime = failures.Sum(x => x.DowntimeHours ?? 0);
        var repaired = failures.Where(x => x.DowntimeHours is not null).ToList();
        var gaps = failures.Zip(failures.Skip(1), (a, b) => (decimal)(b.At - a.At).TotalDays).ToList();
        decimal? mean = gaps.Count == 0 ? null : gaps.Average();
        decimal? variation = gaps.Count < 2 || mean is not > 0
            ? null
            : (decimal)Math.Sqrt((double)gaps.Sum(g => (g - mean.Value) * (g - mean.Value)) / (gaps.Count - 1)) / mean.Value;
        var pattern = failures.Count < MinimumFailures || variation is null ? "Unknown" : variation > RandomAbove ? "Irregular" : "Regular";
        var top = failures.Where(x => x.Problem != null).GroupBy(x => x.Problem).OrderByDescending(g => g.Count()).FirstOrDefault();

        return new AssetReliability(
            asset.Id, asset.Code, asset.Name, asset.Location, asset.Criticality,
            failures.Count,
            Math.Round(Math.Max(0, periodHours - downtime) / failures.Count, 1),
            repaired.Count == 0 ? null : Math.Round(repaired.Average(x => x.DowntimeHours!.Value), 1),
            Math.Round(downtime, 1),
            mean is null ? null : Math.Round(mean.Value, 1),
            variation is null ? null : Math.Round(variation.Value, 2),
            pattern,
            run?.Mean,
            run?.Unit,
            top?.Key,
            top?.Count() ?? 0,
            failures[^1].At,
            0);
    }

    private static PmSuggestion? Suggest(AssetReliability asset, List<PlanInfo> plans, MainMeter? meter)
    {
        PmSuggestion Make(string kind, string reason, string? trigger = null, string? unit = null, int? interval = null, Guid? meterId = null,
            decimal? meterInterval = null, PlanInfo? plan = null) =>
            new(asset.AssetId, asset.AssetCode, asset.AssetName, kind, reason, asset.Failures, asset.MeanDaysBetween, asset.TopProblem,
                trigger, unit, interval, meterId, meterInterval, meterId is null ? null : meter?.Unit, plan?.Id, plan?.Name, plan is null ? null : Describe(plan, meter));

        if (asset.MeanDaysBetween is not { } meanDays)
            return null;
        var focus = asset.TopProblem is null ? string.Empty : $" Most were {asset.TopProblem.ToLowerInvariant()}.";
        if (asset.Pattern == "Irregular")
            return Make(PmSuggestionKinds.Random,
                $"{asset.Failures} breakdowns at irregular intervals (from days to months apart): a fixed PM interval will not catch them. Put limits on a meter that shows the fault coming (temperature, vibration, current) and let condition monitoring raise the job.{focus}");

        // Meter-based when the asset's run is known between breakdowns; otherwise by the calendar.
        var useMeter = meter is not null && asset.MeanRunBetween is { } run && run > 0;
        var targetDays = Math.Max(7m, meanDays * ReliabilityService.PmShareOfMtbf);
        var (unit, interval) = Calendar(targetDays);
        var meterInterval = useMeter ? RoundMeter(asset.MeanRunBetween!.Value * ReliabilityService.PmShareOfMtbf) : (decimal?)null;
        var cadence = useMeter ? $"every {meterInterval:0.##} {meter!.Unit}" : $"every {interval} {unit.ToLowerInvariant()}";

        if (plans.Count == 0)
            return Make(PmSuggestionKinds.NoPlan,
                $"{asset.Failures} breakdowns about every {meanDays:0} days{(useMeter ? $" ({asset.MeanRunBetween:0.##} {meter!.Unit} of running)" : string.Empty)}, and no PM plan. A check {cadence} would come before most of them.{focus}",
                useMeter ? PmTriggerTypes.Meter : PmTriggerTypes.Calendar, useMeter ? null : unit, useMeter ? null : interval, useMeter ? meter!.Id : null, meterInterval);

        // The plan that runs most often decides whether PM is keeping up.
        var shortest = plans.OrderBy(p => PlanDays(p, asset, meter) ?? decimal.MaxValue).First();
        var planDays = PlanDays(shortest, asset, meter);
        if (planDays is { } days && days > meanDays)
        {
            var meterPlan = shortest.TriggerType == PmTriggerTypes.Meter && shortest.MeterInterval is not null;
            if (meterPlan && asset.MeanRunBetween is null)
                return null;
            return Make(PmSuggestionKinds.PlanTooLong,
                $"PM runs {Describe(shortest, meter)}, but breakdowns come about every {meanDays:0} days{(asset.MeanRunBetween is { } r && meter is not null ? $" ({r:0.##} {meter.Unit})" : string.Empty)}. Shorten it to {(meterPlan ? $"every {RoundMeter(asset.MeanRunBetween!.Value * ReliabilityService.PmShareOfMtbf):0.##} {meter!.Unit}" : $"every {interval} {unit.ToLowerInvariant()}")}.{focus}",
                shortest.TriggerType, meterPlan ? null : unit, meterPlan ? null : interval, meterPlan ? shortest.MeterId : null,
                meterPlan ? RoundMeter(asset.MeanRunBetween!.Value * ReliabilityService.PmShareOfMtbf) : null, shortest);
        }

        return Make(PmSuggestionKinds.PlanNotPreventing,
            $"PM already runs {Describe(shortest, meter)}, more often than the breakdowns (about every {meanDays:0} days), yet they keep happening. Review what the PM checks and does against the fault itself, or find the root cause.{focus}",
            plan: shortest);
    }

    /// <summary>A plan's interval in days; a meter plan converts at the asset's run per day between breakdowns.</summary>
    private static decimal? PlanDays(PlanInfo plan, AssetReliability asset, MainMeter? meter)
    {
        if (plan.TriggerType == PmTriggerTypes.Meter)
        {
            if (plan.MeterInterval is not { } meterInterval || asset.MeanRunBetween is not { } run || run <= 0 || asset.MeanDaysBetween is not { } days)
                return null;
            return meterInterval / run * days;
        }

        return plan.FrequencyInterval * (plan.FrequencyUnit switch
        {
            PmFrequencyUnits.Days => 1m,
            PmFrequencyUnits.Weeks => 7m,
            PmFrequencyUnits.Years => 365.25m,
            _ => 30.44m,
        });
    }

    private static string Describe(PlanInfo plan, MainMeter? meter) =>
        plan.TriggerType == PmTriggerTypes.Meter
            ? $"every {plan.MeterInterval:0.##} {meter?.Unit}".TrimEnd()
            : $"every {plan.FrequencyInterval} {plan.FrequencyUnit.ToLowerInvariant()}";

    /// <summary>The target in the unit a planner would use: days under a fortnight, weeks under two months, months beyond.</summary>
    internal static (string Unit, int Interval) Calendar(decimal days) =>
        days < 14 ? (PmFrequencyUnits.Days, (int)Math.Max(1, Math.Floor(days)))
        : days < 60 ? (PmFrequencyUnits.Weeks, (int)Math.Max(1, Math.Floor(days / 7)))
        : (PmFrequencyUnits.Months, (int)Math.Max(1, Math.Floor(days / 30.44m)));

    /// <summary>Round down to a sensible step: 10s under 500, 50s under 2,000, 100s above.</summary>
    internal static decimal RoundMeter(decimal value)
    {
        var step = value < 500 ? 10m : value < 2000 ? 50m : 100m;
        return Math.Max(step, Math.Floor(value / step) * step);
    }

    private static string? Top(IEnumerable<string?> values) =>
        values.Where(x => x != null).GroupBy(x => x).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
}
