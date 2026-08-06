namespace Ecosys.Shared.Options;

/// <summary>
/// The application's default wall-clock time zone, used where a calendar-date decision must reflect
/// the operator's local day rather than UTC (e.g. "is this PM due today?"). Single-region default;
/// override via config for other deployments. (A future per-tenant time zone would supersede this.)
/// </summary>
public sealed class TimeZoneOptions
{
    public const string SectionName = "Time";

    /// <summary>IANA time-zone id (e.g. "Africa/Nairobi"). Falls back to UTC if unrecognized.</summary>
    public string DefaultTimeZone { get; set; } = "Africa/Nairobi";
}
