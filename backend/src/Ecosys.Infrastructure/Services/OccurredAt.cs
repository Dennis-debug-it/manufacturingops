namespace Ecosys.Infrastructure.Services;

/// <summary>
/// When the action in the current request actually happened. Normally that is now; for an action the
/// technician app recorded offline and synced later, it is the time on the phone when the technician
/// tapped it (sent as <c>X-Occurred-At</c>, accepted only within <see cref="MaxAge"/>), so arrival,
/// start, completion and clock times stay true to the field. Only the "it happened" timestamps read
/// this; record-keeping times (created/updated/audit) stay server time.
/// </summary>
public static class OccurredAt
{
    /// <summary>How far back an offline action may be dated; older queued actions get server time.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(7);

    /// <summary>Allowance for a phone clock running slightly ahead.</summary>
    public static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(2);

    private static readonly AsyncLocal<DateTime?> Current = new();

    /// <summary>The time the current action happened (UTC).</summary>
    public static DateTime UtcNow => Current.Value ?? DateTime.UtcNow;

    /// <summary>True when the current action carries a client time earlier than now (it was synced late).</summary>
    public static bool IsDeferred => Current.Value.HasValue;

    /// <summary>
    /// Dates the rest of the current async flow at <paramref name="clientTime"/> when it is plausible:
    /// not in the future beyond clock skew and not older than <see cref="MaxAge"/>. Returns false, and
    /// leaves server time in place, otherwise.
    /// </summary>
    public static bool TrySet(DateTime clientTime, DateTime? serverNow = null)
    {
        var now = serverNow ?? DateTime.UtcNow;
        var utc = clientTime.Kind == DateTimeKind.Utc ? clientTime : clientTime.ToUniversalTime();
        if (utc > now + MaxClockSkew || utc < now - MaxAge)
        {
            return false;
        }

        // A phone a little ahead of the server still means "just now".
        Current.Value = utc > now ? now : utc;
        return true;
    }

    public static void Clear() => Current.Value = null;
}
