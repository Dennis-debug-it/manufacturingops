using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services.Factory;

/// <summary>What a reading did to its meter's condition.</summary>
public sealed record ConditionOutcome(string Level, ConditionEvent? Event, bool Notified, string? RaisedWorkOrderNumber);

public interface IConditionMonitor
{
    /// <summary>
    /// Judges a saved reading against its meter's limits: keeps the meter's condition and its open
    /// excursion up to date, alerts asset managers when an excursion is confirmed or gets worse, and
    /// raises a job when an alarm limit is crossed on a meter set to do so. A back-dated reading
    /// (not the meter's latest) changes nothing. Saves its own changes; never throws for a
    /// notification failure.
    /// </summary>
    Task<ConditionOutcome> EvaluateAsync(Meter meter, MeterReading reading, FactoryActor actor, CancellationToken cancellationToken = default);
}

internal sealed class ConditionMonitor(
    AppDbContext dbContext,
    IFactoryWorkOrderService workOrderService,
    ILogger<ConditionMonitor> logger,
    INotificationDispatcher? notificationDispatcher = null) : IConditionMonitor
{
    private static readonly string[] Finished =
        [WorkOrderStatuses.Completed, WorkOrderStatuses.Verified, WorkOrderStatuses.Closed, WorkOrderStatuses.Cancelled, WorkOrderStatuses.Rejected];

    public async Task<ConditionOutcome> EvaluateAsync(Meter meter, MeterReading reading, FactoryActor actor, CancellationToken cancellationToken = default)
    {
        // Only the latest reading speaks for the meter's condition now.
        if (meter.LastReadingAt != reading.ReadAt || reading.MeterId != meter.Id)
        {
            return new ConditionOutcome(meter.ConditionState, null, false, null);
        }

        var (level, direction) = ConditionRules.Classify(meter, reading.Value);
        var open = await dbContext.ConditionEvents
            .SingleOrDefaultAsync(x => x.TenantId == meter.TenantId && x.MeterId == meter.Id && x.Status == ConditionEventStatuses.Open, cancellationToken);

        if (level == ConditionLevels.Normal)
        {
            meter.AbnormalStreak = 0;
            SetState(meter, ConditionLevels.Normal, reading.ReadAt);
            if (open is not null)
            {
                open.Status = ConditionEventStatuses.Cleared;
                open.ClearedAt = reading.ReadAt;
                open.LastReadingAt = reading.ReadAt;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return new ConditionOutcome(level, open, false, null);
        }

        meter.AbnormalStreak++;
        var notify = false;
        if (open is null || open.Direction != direction)
        {
            if (open is not null)
            {
                // Swung straight from one side to the other: close the old excursion, start a new one.
                open.Status = ConditionEventStatuses.Cleared;
                open.ClearedAt = reading.ReadAt;
                meter.AbnormalStreak = 1;
            }

            if (meter.AbnormalStreak < Math.Max(1, meter.TriggerAfterReadings))
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return new ConditionOutcome(meter.ConditionState, null, false, null);
            }

            open = new ConditionEvent
            {
                TenantId = meter.TenantId,
                MeterId = meter.Id,
                AssetId = meter.AssetId,
                Level = level,
                Direction = direction!,
                StartedAt = reading.ReadAt,
                LastReadingAt = reading.ReadAt,
                PeakValue = reading.Value,
                ReadingCount = meter.AbnormalStreak,
            };
            dbContext.ConditionEvents.Add(open);
            notify = true;
        }
        else
        {
            open.ReadingCount++;
            open.LastReadingAt = reading.ReadAt;
            if (direction == ConditionDirections.High ? reading.Value > open.PeakValue : reading.Value < open.PeakValue)
            {
                open.PeakValue = reading.Value;
            }

            if (ConditionLevels.Rank(level) > ConditionLevels.Rank(open.Level))
            {
                open.Level = level;
                notify = true;
            }
        }

        SetState(meter, level, reading.ReadAt);
        await dbContext.SaveChangesAsync(cancellationToken);

        string? raised = null;
        if (level == ConditionLevels.Alarm && meter.ConditionAction == ConditionActions.WorkOrder && open.WorkOrderId is null)
        {
            raised = await LinkOrRaiseJobAsync(meter, reading, open, actor, cancellationToken);
        }

        if (notify)
        {
            await NotifyAsync(meter, reading, open, raised, cancellationToken);
        }

        return new ConditionOutcome(level, open, notify, raised);
    }

    private static void SetState(Meter meter, string level, DateTime at)
    {
        if (meter.ConditionState != level)
        {
            meter.ConditionState = level;
            meter.ConditionSince = at;
        }
    }

    /// <summary>
    /// The job for an alarm: one still open from an earlier excursion of this meter is reused, so a
    /// gauge that flickers in and out of range does not pile up jobs. Returns the new job's number,
    /// or null when an existing one was linked or the job could not be raised.
    /// </summary>
    private async Task<string?> LinkOrRaiseJobAsync(Meter meter, MeterReading reading, ConditionEvent open, FactoryActor actor, CancellationToken cancellationToken)
    {
        var existing = await dbContext.ConditionEvents.AsNoTracking()
            .Where(x => x.TenantId == meter.TenantId && x.MeterId == meter.Id && x.Id != open.Id && x.WorkOrderId != null
                && !Finished.Contains(x.WorkOrder!.Status))
            .OrderByDescending(x => x.StartedAt)
            .Select(x => x.WorkOrderId)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            open.WorkOrderId = existing;
            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        var asset = meter.Asset ?? await dbContext.Assets.SingleAsync(x => x.Id == meter.AssetId, cancellationToken);
        var value = $"{reading.Value:0.##} {meter.Unit}".Trim();
        try
        {
            var job = await workOrderService.CreateAsync(meter.TenantId, actor, new CreateFactoryWorkOrder(
                Truncate($"Condition alarm: {asset.AssetName} {meter.Name} {value}", 200),
                FactoryWorkTypes.Corrective,
                FactoryPriorities.Find(meter.AlarmPriority) ?? "High",
                AssetId: asset.Id,
                Description: $"{meter.Name} read {value} at {reading.ReadAt:yyyy-MM-dd HH:mm} UTC, past its alarm limit ({Limits(meter.MinThreshold, meter.MaxThreshold, meter.Unit)}). Raised automatically by condition monitoring.",
                Source: FactoryRequestSources.Condition,
                RequestedByName: actor.Name,
                JobPlanId: meter.AlarmJobPlanId), cancellationToken);
            open.WorkOrderId = job.Id;
            await dbContext.SaveChangesAsync(cancellationToken);
            return job.WorkOrderNumber;
        }
        catch (BusinessRuleException ex)
        {
            // The alarm still stands and managers are still told; the job can be raised by hand.
            logger.LogWarning(ex, "Condition alarm on meter {MeterId} could not raise a job.", meter.Id);
            return null;
        }
    }

    private async Task NotifyAsync(Meter meter, MeterReading reading, ConditionEvent open, string? raised, CancellationToken cancellationToken)
    {
        try
        {
            var asset = meter.Asset ?? await dbContext.Assets.SingleAsync(x => x.Id == meter.AssetId, cancellationToken);
            await AssetAlerts.NotifyConditionAsync(notificationDispatcher, dbContext, meter, asset, reading, open, raised, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not send the condition alert for meter {MeterId}.", meter.Id);
        }
    }

    internal static string Limits(decimal? min, decimal? max, string unit) =>
        $"{string.Join(" to ", new[] { min, max }.Select(x => x.HasValue ? $"{x:0.##}" : "—"))} {unit}".Trim();

    private static string Truncate(string value, int max) => value.Length <= max ? value : $"{value[..(max - 3)]}...";
}

/// <summary>Where a gauge is heading: a straight-line fit through its recent readings.</summary>
/// <param name="ApproachingLevel">The limit the trend is heading for (warning before alarm); <paramref name="DaysToLimit"/> is when it gets there.</param>
public sealed record ConditionTrend(
    int Readings,
    decimal? SlopePerDay,
    string? Heading,
    string? ApproachingLevel,
    decimal? ApproachingLimit,
    decimal? DaysToLimit);

public static class ConditionTrends
{
    /// <summary>Fewer points than this and a fitted line says nothing.</summary>
    public const int MinimumReadings = 5;

    /// <summary>
    /// Least-squares slope of value over time, and when the line reaches the next limit in its
    /// direction. A flat line, a meter already past that limit, or one heading away from every limit
    /// reaches nothing (null days).
    /// </summary>
    public static ConditionTrend Fit(Meter meter, IReadOnlyList<(DateTime At, decimal Value)> readings)
    {
        if (readings.Count < MinimumReadings)
        {
            return new ConditionTrend(readings.Count, null, null, null, null, null);
        }

        var origin = readings.Min(x => x.At);
        var xs = readings.Select(x => (x.At - origin).TotalDays).ToArray();
        var ys = readings.Select(x => (double)x.Value).ToArray();
        var meanX = xs.Average();
        var meanY = ys.Average();
        var sxx = xs.Sum(x => (x - meanX) * (x - meanX));
        if (sxx <= 0)
        {
            return new ConditionTrend(readings.Count, null, null, null, null, null);
        }

        var slope = xs.Zip(ys).Sum(p => (p.First - meanX) * (p.Second - meanY)) / sxx;
        var intercept = meanY - slope * meanX;
        var latestX = xs.Max();
        var fittedNow = intercept + slope * latestX;
        // Below 0.1% of the current level per day the line is effectively flat.
        if (Math.Abs(slope) < Math.Max(1e-9, Math.Abs(fittedNow) * 0.001))
        {
            return new ConditionTrend(readings.Count, 0, "Steady", null, null, null);
        }

        var rising = slope > 0;
        var candidates = rising
            ? new[] { (ConditionLevels.Warning, meter.WarningMax), (ConditionLevels.Alarm, meter.MaxThreshold) }
            : new[] { (ConditionLevels.Warning, meter.WarningMin), (ConditionLevels.Alarm, meter.MinThreshold) };
        foreach (var (level, limit) in candidates)
        {
            if (limit is not { } value)
            {
                continue;
            }

            var gap = (double)value - fittedNow;
            if (rising ? gap <= 0 : gap >= 0)
            {
                continue; // already past this one
            }

            var days = gap / slope;
            return new ConditionTrend(readings.Count, Round(slope), rising ? "Rising" : "Falling", level, value, Math.Round((decimal)days, 1));
        }

        return new ConditionTrend(readings.Count, Round(slope), rising ? "Rising" : "Falling", null, null, null);
    }

    private static decimal Round(double value) => Math.Round((decimal)value, 4);
}
