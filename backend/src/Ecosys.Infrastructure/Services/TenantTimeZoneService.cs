using System.Text.Json;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Ecosys.Infrastructure.Services;

/// <summary>
/// Resolves the wall-clock time zone that a given tenant's users think in, and renders UTC instants
/// in it. Everything is stored in UTC; this is the single place that decides what "local" means when
/// a timestamp is shown to a human (emails, documents) or a calendar-date decision is made.
/// Resolution order: tenant override → platform default setting → <see cref="TimeZoneOptions"/> → UTC.
/// </summary>
public interface ITenantTimeZoneService
{
    /// <summary>The platform-wide default IANA id (platform setting, else configuration, else UTC).</summary>
    Task<string> GetPlatformTimeZoneIdAsync(CancellationToken cancellationToken = default);

    /// <summary>The effective IANA id for a tenant, falling back to the platform default.</summary>
    Task<string> GetTimeZoneIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<TimeZoneInfo> GetTimeZoneAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Converts a UTC instant to the tenant's wall-clock time.</summary>
    Task<DateTime> ToTenantTimeAsync(DateTime utcValue, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The tenant's current calendar date, with <see cref="DateTimeKind.Unspecified"/> — it is a
    /// wall-clock date, not an instant. Use it for display and for date arithmetic, never as a
    /// parameter in a database query against a <c>timestamptz</c> column (Npgsql rejects
    /// non-UTC kinds); for "is this due by end of today?" use <see cref="TenantDayEndUtcAsync"/>.
    /// </summary>
    Task<DateTime> TenantTodayAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The UTC instant at which the tenant's current local day ends — i.e. midnight at the start of
    /// their next local day. This is the exclusive upper bound for a "due today or earlier"
    /// comparison against a UTC-stored timestamp: anything strictly before it falls on or before the
    /// tenant's current calendar day. Keeps the calendar decision in the tenant's local day while
    /// still handing EF/Npgsql a UTC parameter.
    /// </summary>
    Task<DateTime> TenantDayEndUtcAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders a UTC instant as tenant-local text with an explicit zone label, e.g.
    /// "23 Aug 2026 at 17:45 (EAT)". Timestamps shown to humans must never be bare UTC — a reader in
    /// UTC+3 sees a bare UTC expiry as a time that has already passed.
    /// </summary>
    Task<string> FormatForTenantAsync(
        DateTime utcValue,
        Guid tenantId,
        string format = "dd MMM yyyy 'at' HH:mm",
        CancellationToken cancellationToken = default);

    /// <summary>Validates a user-supplied IANA id, returning null when it is blank.</summary>
    string? NormalizeTimeZoneId(string? timeZoneId);

    /// <summary>
    /// Drops cached resolutions after a time zone is changed. Pass a tenant id for a tenant override,
    /// or null when the platform default moved (which every tenant without an override inherits).
    /// </summary>
    void Invalidate(Guid? tenantId = null);
}

public sealed class TenantTimeZoneService(
    AppDbContext dbContext,
    IMemoryCache cache,
    IOptions<TimeZoneOptions> timeZoneOptions) : ITenantTimeZoneService
{
    private const string PlatformGeneralCategory = "platform-general";
    private static int cacheGeneration;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<string> GetPlatformTimeZoneIdAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue("timezone:platform", out string? cached) && !string.IsNullOrWhiteSpace(cached))
        {
            return cached;
        }

        string? configured = null;
        var setting = await dbContext.PlatformSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Category == PlatformGeneralCategory, cancellationToken);

        if (setting is not null)
        {
            try
            {
                var general = JsonSerializer.Deserialize<PlatformGeneralTimeZone>(setting.JsonValue, JsonOptions);
                configured = general?.Timezone;
            }
            catch (JsonException)
            {
                configured = null;
            }
        }

        var resolved = FirstValid(configured, timeZoneOptions.Value.DefaultTimeZone) ?? TimeZoneInfo.Utc.Id;
        cache.Set("timezone:platform", resolved, CacheDuration);
        return resolved;
    }

    public async Task<string> GetTimeZoneIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            return await GetPlatformTimeZoneIdAsync(cancellationToken);
        }

        var cacheKey = TenantCacheKey(tenantId);
        if (cache.TryGetValue(cacheKey, out string? cached) && !string.IsNullOrWhiteSpace(cached))
        {
            return cached;
        }

        var tenantTimeZoneId = await dbContext.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(x => x.Id == tenantId)
            .Select(x => x.TimeZoneId)
            .SingleOrDefaultAsync(cancellationToken);

        var resolved = FirstValid(tenantTimeZoneId) ?? await GetPlatformTimeZoneIdAsync(cancellationToken);
        cache.Set(cacheKey, resolved, CacheDuration);
        return resolved;
    }

    private static string TenantCacheKey(Guid tenantId) => $"timezone:tenant:{Volatile.Read(ref cacheGeneration)}:{tenantId}";

    public async Task<TimeZoneInfo> GetTimeZoneAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        ResolveTimeZone(await GetTimeZoneIdAsync(tenantId, cancellationToken));

    public async Task<DateTime> ToTenantTimeAsync(DateTime utcValue, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var timeZone = await GetTimeZoneAsync(tenantId, cancellationToken);
        return TimeZoneInfo.ConvertTimeFromUtc(DateTimeNormalizationHelper.NormalizeToUtc(utcValue), timeZone);
    }

    public async Task<DateTime> TenantTodayAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        (await ToTenantTimeAsync(DateTime.UtcNow, tenantId, cancellationToken)).Date;

    public async Task<DateTime> TenantDayEndUtcAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var timeZone = await GetTimeZoneAsync(tenantId, cancellationToken);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        return ToUtcFromTenantLocal(localNow.Date.AddDays(1), timeZone);
    }

    /// <summary>
    /// Converts a tenant wall-clock time to the UTC instant it names. Local midnight does not exist
    /// in a few zones on the day DST starts, so an invalid time is nudged forward past the gap
    /// rather than throwing; an ambiguous time resolves to the standard-time (later) instant, which
    /// keeps an exclusive end-of-day bound inclusive of the whole local day.
    /// </summary>
    private static DateTime ToUtcFromTenantLocal(DateTime localValue, TimeZoneInfo timeZone)
    {
        var unspecified = DateTime.SpecifyKind(localValue, DateTimeKind.Unspecified);
        while (timeZone.IsInvalidTime(unspecified))
        {
            unspecified = unspecified.AddMinutes(15);
        }

        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeToUtc(unspecified, timeZone), DateTimeKind.Utc);
    }

    public async Task<string> FormatForTenantAsync(
        DateTime utcValue,
        Guid tenantId,
        string format = "dd MMM yyyy 'at' HH:mm",
        CancellationToken cancellationToken = default)
    {
        var timeZone = await GetTimeZoneAsync(tenantId, cancellationToken);
        var normalized = DateTimeNormalizationHelper.NormalizeToUtc(utcValue);
        var local = TimeZoneInfo.ConvertTimeFromUtc(normalized, timeZone);
        return $"{local.ToString(format, System.Globalization.CultureInfo.InvariantCulture)} ({DescribeZone(timeZone, normalized)})";
    }

    public string? NormalizeTimeZoneId(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return null;
        }

        var trimmed = timeZoneId.Trim();
        if (!TryFindTimeZone(trimmed, out _))
        {
            throw new Shared.Errors.BusinessRuleException($"'{trimmed}' is not a recognized time zone. Use an IANA id such as 'Africa/Nairobi'.");
        }

        return trimmed;
    }

    public void Invalidate(Guid? tenantId = null)
    {
        if (tenantId.HasValue)
        {
            cache.Remove(TenantCacheKey(tenantId.Value));
            return;
        }

        // A platform-default change affects every tenant that has no override, and IMemoryCache has no
        // prefix eviction — bumping the generation makes every previously cached key unreachable.
        cache.Remove("timezone:platform");
        Interlocked.Increment(ref cacheGeneration);
    }

    /// <summary>Resolves an IANA (or Windows) id to a <see cref="TimeZoneInfo"/>, falling back to UTC.</summary>
    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId) =>
        TryFindTimeZone(timeZoneId, out var timeZone) ? timeZone : TimeZoneInfo.Utc;

    private static bool TryFindTimeZone(string? timeZoneId, out TimeZoneInfo timeZone)
    {
        timeZone = TimeZoneInfo.Utc;
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return false;
        }

        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
            return true;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static string? FirstValid(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (TryFindTimeZone(candidate, out _))
            {
                return candidate!.Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// A short label a reader recognizes. .NET exposes no zone abbreviations, so well-known ones are
    /// named directly and everything else falls back to its unambiguous UTC offset.
    /// </summary>
    private static string DescribeZone(TimeZoneInfo timeZone, DateTime utcValue)
    {
        if (timeZone.Id is "UTC" or "Etc/UTC")
        {
            return "UTC";
        }

        var offset = timeZone.GetUtcOffset(utcValue);
        var abbreviation = timeZone.Id switch
        {
            "Africa/Nairobi" or "Africa/Kampala" or "Africa/Dar_es_Salaam" or "Africa/Addis_Ababa" or "Africa/Mogadishu" or "E. Africa Standard Time" => "EAT",
            "Africa/Lagos" or "Africa/Kinshasa" or "W. Central Africa Standard Time" => "WAT",
            "Africa/Johannesburg" or "South Africa Standard Time" => "SAST",
            "Africa/Cairo" or "Egypt Standard Time" => "EET",
            _ => null
        };

        var offsetLabel = $"UTC{(offset < TimeSpan.Zero ? "-" : "+")}{offset.Duration():hh\\:mm}";
        return abbreviation is null ? offsetLabel : $"{abbreviation}, {offsetLabel}";
    }

    private sealed record PlatformGeneralTimeZone(string? Timezone);
}
