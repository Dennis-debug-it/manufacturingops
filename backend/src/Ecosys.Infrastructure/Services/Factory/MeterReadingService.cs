using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services.Factory;

public sealed record NewMeterReading(
    decimal Value,
    DateTime? ReadAt,
    string Source = MeterReadingSources.Manual,
    bool IsReset = false,
    string? Notes = null,
    Guid? UserId = null,
    string? UserName = null);

public interface IMeterReadingService
{
    /// <summary>
    /// Validates and adds a reading, then updates the meter (and the asset, for its primary meter).
    /// Counters may not go down unless the reading is marked as a reset; a back-dated reading must
    /// fit between its neighbours. The caller saves.
    /// </summary>
    Task<MeterReading> RecordAsync(Meter meter, NewMeterReading reading, CancellationToken cancellationToken = default);
}

internal sealed class MeterReadingService(AppDbContext dbContext) : IMeterReadingService
{
    /// <summary>Phones' clocks drift; allow a little slack before calling a reading "in the future".</summary>
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(10);

    public async Task<MeterReading> RecordAsync(Meter meter, NewMeterReading reading, CancellationToken cancellationToken = default)
    {
        if (!meter.IsActive)
        {
            throw new BusinessRuleException($"Meter '{meter.Name}' is inactive.");
        }

        var now = DateTime.UtcNow;
        var readAt = AssetLifecycleService.EffectiveAt(reading.ReadAt);
        if (readAt > now + ClockSkew)
        {
            throw new BusinessRuleException("A reading cannot be in the future.");
        }

        if (reading.Value < 0 && meter.IsCumulative)
        {
            throw new BusinessRuleException("A counter reading cannot be negative.");
        }

        var source = MeterReadingSources.All.FirstOrDefault(x => string.Equals(x, reading.Source, StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessRuleException($"Reading source must be one of: {string.Join(", ", MeterReadingSources.All)}.");

        var previous = await dbContext.MeterReadings
            .Where(x => x.TenantId == meter.TenantId && x.MeterId == meter.Id && x.ReadAt <= readAt)
            .OrderByDescending(x => x.ReadAt)
            .FirstOrDefaultAsync(cancellationToken);
        var next = await dbContext.MeterReadings
            .Where(x => x.TenantId == meter.TenantId && x.MeterId == meter.Id && x.ReadAt > readAt)
            .OrderBy(x => x.ReadAt)
            .FirstOrDefaultAsync(cancellationToken);

        decimal? delta = null;
        if (meter.IsCumulative)
        {
            if (previous is not null && reading.Value < previous.Value && !reading.IsReset)
            {
                throw new BusinessRuleException(
                    $"{reading.Value:0.##} {meter.Unit} is lower than the previous reading ({previous.Value:0.##} {meter.Unit} on {previous.ReadAt:yyyy-MM-dd}). If the meter was reset or replaced, mark the reading as a reset.");
            }

            if (next is not null && !next.IsReset && reading.Value > next.Value)
            {
                throw new BusinessRuleException(
                    $"{reading.Value:0.##} {meter.Unit} is higher than a later reading ({next.Value:0.##} {meter.Unit} on {next.ReadAt:yyyy-MM-dd}).");
            }

            delta = previous is null || reading.IsReset ? 0 : reading.Value - previous.Value;
            if (next is not null && !next.IsReset)
            {
                next.Delta = next.Value - reading.Value;
            }
        }

        var entry = new MeterReading
        {
            TenantId = meter.TenantId,
            MeterId = meter.Id,
            AssetId = meter.AssetId,
            Value = reading.Value,
            Delta = delta,
            ReadAt = readAt,
            Source = source,
            IsReset = reading.IsReset,
            Condition = ConditionRules.Classify(meter, reading.Value).Level,
            Notes = string.IsNullOrWhiteSpace(reading.Notes) ? null : reading.Notes.Trim(),
            RecordedByUserId = reading.UserId,
            RecordedByName = reading.UserName
        };
        entry.IsOutOfRange = entry.Condition == ConditionLevels.Alarm;
        dbContext.MeterReadings.Add(entry);

        if (next is null)
        {
            meter.CurrentValue = entry.Value;
            meter.LastReadingAt = entry.ReadAt;

            if (meter.IsPrimary)
            {
                var asset = meter.Asset ?? await dbContext.Assets.SingleAsync(x => x.Id == meter.AssetId, cancellationToken);
                asset.CurrentMeterReading = entry.Value;
                asset.MeterLabel ??= meter.Unit;
            }
        }

        return entry;
    }
}
