namespace Ecosys.Infrastructure.Services;

public static class DateTimeNormalizationHelper
{
    public static DateTime NormalizeToUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return value;
        }

        if (value.Kind == DateTimeKind.Unspecified)
        {
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        return value.ToUniversalTime();
    }

    public static DateTime? NormalizeNullableToUtc(DateTime? value) =>
        value.HasValue ? NormalizeToUtc(value.Value) : null;

    /// <summary>
    /// The current calendar date in the given IANA time zone (e.g. "Africa/Nairobi"). Used for
    /// calendar-date decisions that must reflect the operator's local day, not UTC — avoids the
    /// off-by-one where a date due "today" locally isn't yet "today" in UTC. Falls back to UTC when
    /// the id is missing or the platform lacks time-zone data.
    /// </summary>
    public static DateTime LocalToday(string? timeZoneId)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ResolveTimeZone(timeZoneId)).Date;
    }

    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
