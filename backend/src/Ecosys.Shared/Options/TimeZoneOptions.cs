namespace Ecosys.Shared.Options;

/// <summary>
/// The deployment's fallback wall-clock time zone. It is the last resort in the resolution chain:
/// tenant time zone → platform general setting → this option → UTC. Used wherever a wall-clock
/// decision or a user-facing timestamp must reflect local time rather than UTC (e.g. "is this PM due
/// today?", "this reset link expires at …").
/// </summary>
public sealed class TimeZoneOptions
{
    public const string SectionName = "Time";

    /// <summary>IANA time-zone id (e.g. "Africa/Nairobi"). Falls back to UTC if unrecognized.</summary>
    public string DefaultTimeZone { get; set; } = "Africa/Nairobi";
}
