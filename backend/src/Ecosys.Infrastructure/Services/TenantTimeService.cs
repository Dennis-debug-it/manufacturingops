using System.Globalization;

namespace Ecosys.Infrastructure.Services;

/// <summary>Presentation only. Token/security comparisons always use the original UTC instant.</summary>
public static class TenantTimeService
{
    public static string NormalizeTimeZone(string? zone, string? country = null)
    {
        var candidate = string.IsNullOrWhiteSpace(zone) ? DefaultTimeZone(country) : zone.Trim();
        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(candidate, out var iana)) candidate = iana;
        try { return TimeZoneInfo.FindSystemTimeZoneById(candidate).HasIanaId ? candidate : throw new TimeZoneNotFoundException(); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new Ecosys.Shared.Errors.BusinessRuleException("Choose a valid IANA timezone, such as Africa/Nairobi."); }
    }

    public static string DefaultTimeZone(string? country) => country?.Trim().ToUpperInvariant() switch
    {
        "KE" or "KENYA" => "Africa/Nairobi", "UG" or "UGANDA" => "Africa/Kampala",
        "TZ" or "TANZANIA" => "Africa/Dar_es_Salaam", "RW" or "RWANDA" => "Africa/Kigali",
        "NG" or "NIGERIA" => "Africa/Lagos", "ZA" or "SOUTH AFRICA" => "Africa/Johannesburg",
        "GB" or "UK" or "UNITED KINGDOM" => "Europe/London", _ => "Etc/UTC"
    };

    public static string Format(DateTime utc, string? zone, string? country = null)
    {
        var zoneId = NormalizeTimeZone(zone, country);
        var instant = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
        var local = TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(zoneId));
        var label = zoneId is "Africa/Nairobi" or "Africa/Kampala" or "Africa/Dar_es_Salaam" ? "EAT" : zoneId;
        return local.ToString("d MMMM yyyy 'at' h:mm tt", CultureInfo.InvariantCulture) + " " + label;
    }
}
