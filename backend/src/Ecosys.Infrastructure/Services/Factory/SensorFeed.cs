using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services.Factory;

/// <summary>One value from a sensor, named by the meter's tag. No time means "now".</summary>
public sealed record SensorSample(string Tag, decimal Value, DateTime? At = null);

public sealed record SensorRejection(string Tag, string Reason);

/// <param name="SampledOut">Steady readings inside the gateway's sample interval, not stored.</param>
/// <param name="Skipped">Readings no newer than the meter's latest (repeats, or late arrivals).</param>
public sealed record SensorIngestResult(int Accepted, int SampledOut, int Skipped, IReadOnlyList<SensorRejection> Rejected);

/// <summary>Gateway keys: "sg_" and 40 hex characters. Only the SHA-256 hash is kept.</summary>
public static class SensorKeys
{
    public const int PrefixLength = 11;

    public static (string Key, string Prefix, string Hash) Generate()
    {
        var key = $"sg_{Convert.ToHexString(RandomNumberGenerator.GetBytes(20)).ToLowerInvariant()}";
        return (key, key[..PrefixLength], Hash(key));
    }

    public static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    public static bool Matches(string key, string hash) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(key)), Encoding.ASCII.GetBytes(hash));
}

public interface ISensorFeed
{
    /// <summary>The active gateway this key belongs to, or null. Works without a signed-in user.</summary>
    Task<SensorGateway?> AuthenticateAsync(string? key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a gateway's readings on the meters they are tagged for, oldest first, each judged by
    /// condition monitoring. One bad reading does not stop the rest; it is reported back.
    /// </summary>
    Task<SensorIngestResult> IngestAsync(SensorGateway gateway, IReadOnlyList<SensorSample> samples, CancellationToken cancellationToken = default);
}

internal sealed class SensorFeed(
    AppDbContext dbContext,
    IMeterReadingService meterReadingService,
    IConditionMonitor conditionMonitor,
    IOrgScopeService orgScopeService) : ISensorFeed
{
    public const int MaxBatch = 500;

    /// <summary>Clocks drift; a reading this far ahead is still taken as "now-ish".</summary>
    private static readonly TimeSpan FutureSlack = TimeSpan.FromMinutes(10);

    public async Task<SensorGateway?> AuthenticateAsync(string? key, CancellationToken cancellationToken = default)
    {
        key = key?.Trim();
        if (string.IsNullOrEmpty(key) || key.Length < SensorKeys.PrefixLength + 8)
        {
            return null;
        }

        var prefix = key[..SensorKeys.PrefixLength];
        var candidates = await dbContext.SensorGateways.IgnoreQueryFilters()
            .Where(x => x.KeyPrefix == prefix && x.IsActive && x.Tenant!.IsActive)
            .ToListAsync(cancellationToken);
        return candidates.FirstOrDefault(x => SensorKeys.Matches(key, x.KeyHash));
    }

    public async Task<SensorIngestResult> IngestAsync(SensorGateway gateway, IReadOnlyList<SensorSample> samples, CancellationToken cancellationToken = default)
    {
        if (samples.Count > MaxBatch)
        {
            throw new BusinessRuleException($"Send at most {MaxBatch} readings at a time.");
        }

        var rejected = new List<SensorRejection>();
        var tags = samples.Select(x => x.Tag?.Trim() ?? string.Empty).Where(x => x.Length > 0).Distinct().ToList();
        IReadOnlySet<Guid>? allowed = gateway.OrgNodeId is { } node
            ? await orgScopeService.GetSubtreeAsync(gateway.TenantId, node, cancellationToken)
            : null;
        var meters = tags.Count == 0
            ? []
            : await dbContext.Meters.IgnoreQueryFilters().Include(x => x.Asset)
                .Where(x => x.TenantId == gateway.TenantId && x.IsActive && x.ExternalId != null && tags.Contains(x.ExternalId))
                .ToListAsync(cancellationToken);
        var byTag = meters
            .Where(x => allowed is null || (x.Asset?.OrgNodeId is { } assetNode && allowed.Contains(assetNode)))
            .GroupBy(x => x.ExternalId!)
            .ToDictionary(x => x.Key, x => x.ToList());

        var now = DateTime.UtcNow;
        int accepted = 0, sampledOut = 0, skipped = 0;
        foreach (var sample in samples.OrderBy(x => x.At ?? now))
        {
            var tag = sample.Tag?.Trim() ?? string.Empty;
            if (tag.Length == 0)
            {
                rejected.Add(new SensorRejection(string.Empty, "No tag."));
                continue;
            }

            if (!byTag.TryGetValue(tag, out var matches))
            {
                rejected.Add(new SensorRejection(tag, allowed is null ? "No active meter has this tag." : "No active meter with this tag at this gateway's location."));
                continue;
            }

            if (matches.Count > 1)
            {
                rejected.Add(new SensorRejection(tag, $"{matches.Count} meters share this tag; give each its own."));
                continue;
            }

            var meter = matches[0];
            var at = ToUtc(sample.At) ?? now;
            if (at > now + FutureSlack)
            {
                rejected.Add(new SensorRejection(tag, "The reading is dated in the future; check the sender's clock."));
                continue;
            }

            if (meter.LastReadingAt is { } last && at <= last)
            {
                skipped++;
                continue;
            }

            var (level, _) = ConditionRules.Classify(meter, sample.Value);
            var counterWentDown = meter.IsCumulative && meter.CurrentValue is { } current && sample.Value < current;
            if (meter.LastReadingAt is { } previous && (at - previous).TotalSeconds < Math.Max(0, gateway.SampleSeconds)
                && level == meter.ConditionState && !counterWentDown)
            {
                sampledOut++;
                continue;
            }

            try
            {
                var reading = await meterReadingService.RecordAsync(meter, new NewMeterReading(
                    sample.Value, at, MeterReadingSources.Sensor, IsReset: counterWentDown,
                    Notes: counterWentDown ? "The counter went down, so it was taken as a reset or replacement." : null,
                    UserName: gateway.Name), cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                await conditionMonitor.EvaluateAsync(meter, reading, new FactoryActor(null, gateway.Name), cancellationToken);
                accepted++;
            }
            catch (BusinessRuleException ex)
            {
                rejected.Add(new SensorRejection(tag, ex.Message));
            }
        }

        gateway.LastSeenAt = now;
        gateway.AcceptedCount += accepted;
        gateway.RejectedCount += rejected.Count;
        if (rejected.Count > 0)
        {
            gateway.LastError = Truncate($"{rejected[0].Tag}: {rejected[0].Reason}", 500);
            gateway.LastErrorAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new SensorIngestResult(accepted, sampledOut, skipped, rejected);
    }

    internal static DateTime? ToUtc(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Utc } utc => utc,
        { Kind: DateTimeKind.Local } local => local.ToUniversalTime(),
        { } unspecified => DateTime.SpecifyKind(unspecified, DateTimeKind.Utc),
    };

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}

/// <summary>One MQTT message read as a sensor reading.</summary>
public sealed record MqttSensorMessage(Guid GatewayId, string Tag, decimal Value, DateTime? At, string? Key);

/// <summary>
/// MQTT messages: topic "{prefix}/{gatewayId}/{tag}" (the tag may itself contain slashes) and a
/// payload that is either a bare number ("12.5") or JSON: {"value": 12.5, "at": "2026-11-08T06:00:00Z", "key": "sg_…"}.
/// </summary>
public static class MqttSensorMessages
{
    public static bool TryParse(string prefix, string topic, ReadOnlySpan<byte> payload, out MqttSensorMessage? message, out string? error)
    {
        message = null;
        error = null;
        var root = prefix.Trim('/');
        if (!topic.StartsWith(root + "/", StringComparison.Ordinal))
        {
            error = "Topic is outside the sensor prefix.";
            return false;
        }

        var rest = topic[(root.Length + 1)..];
        var slash = rest.IndexOf('/');
        if (slash <= 0 || slash == rest.Length - 1 || !Guid.TryParse(rest[..slash], out var gatewayId))
        {
            error = $"Topic must be {root}/<gateway id>/<tag>.";
            return false;
        }

        var tag = rest[(slash + 1)..];
        var text = Encoding.UTF8.GetString(payload).Trim();
        if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var bare))
        {
            message = new MqttSensorMessage(gatewayId, tag, bare, null, null);
            return true;
        }

        try
        {
            using var json = JsonDocument.Parse(text);
            var body = json.RootElement;
            if (body.ValueKind != JsonValueKind.Object || !body.TryGetProperty("value", out var valueElement))
            {
                error = "Payload must be a number or JSON with a \"value\".";
                return false;
            }

            decimal value;
            if (valueElement.ValueKind == JsonValueKind.Number)
                value = valueElement.GetDecimal();
            else if (valueElement.ValueKind == JsonValueKind.String && decimal.TryParse(valueElement.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                value = parsed;
            else
            {
                error = "\"value\" must be a number.";
                return false;
            }

            DateTime? at = null;
            if (body.TryGetProperty("at", out var atElement) && atElement.ValueKind == JsonValueKind.String)
            {
                if (!DateTimeOffset.TryParse(atElement.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedAt))
                {
                    error = "\"at\" must be an ISO 8601 date and time.";
                    return false;
                }

                at = parsedAt.UtcDateTime;
            }

            var key = body.TryGetProperty("key", out var keyElement) && keyElement.ValueKind == JsonValueKind.String ? keyElement.GetString() : null;
            message = new MqttSensorMessage(gatewayId, tag, value, at, key);
            return true;
        }
        catch (JsonException)
        {
            error = "Payload must be a number or JSON with a \"value\".";
            return false;
        }
    }
}
