using Ecosys.Domain.Entities;

namespace Ecosys.Infrastructure.Services;

/// <summary>Clock-session rules shared by the attendance API, the job trip workflow and the auto clock-out worker.</summary>
public static class AttendanceRules
{
    public const string ClockedIn = "ClockedIn";
    public const string OnBreak = "OnBreak";
    public const string Travelling = "Travelling";
    public const string ClockedOut = "ClockedOut";

    /// <summary>Tags a session the system closed; the office confirms or corrects its hours.</summary>
    public const string AutoClockOutMarker = "[Auto clock-out]";

    public static bool IsAutoClockedOut(AttendanceEntry entry) =>
        entry.Note?.Contains(AutoClockOutMarker, StringComparison.Ordinal) == true;

    /// <summary>Folds a running break into <see cref="AttendanceEntry.BreakMinutes"/> as of <paramref name="at"/>.</summary>
    public static void EndBreak(AttendanceEntry entry, DateTime at)
    {
        if (entry.BreakStartedAt is { } startedAt && at > startedAt)
        {
            entry.BreakMinutes += (int)Math.Round((at - startedAt).TotalMinutes);
        }

        entry.BreakStartedAt = null;
    }

    /// <summary>
    /// Closes a session nobody clocked out of, at clock-in + the tenant's limit rather than "now", so a
    /// forgotten clock-out never turns into a 30-hour day. The note flags it for the office to confirm.
    /// </summary>
    public static void AutoClockOut(AttendanceEntry entry, decimal limitHours, DateTime now)
    {
        var clockOutAt = entry.ClockInAt.AddHours((double)limitHours);
        if (clockOutAt > now)
        {
            clockOutAt = now;
        }

        EndBreak(entry, clockOutAt);
        entry.ClockOutAt = clockOutAt;
        entry.Status = ClockedOut;
        entry.CurrentWorkOrderId = null;
        entry.Note = AppendNote(entry.Note, $"{AutoClockOutMarker} Not clocked out within {limitHours:0.##}h, so closed automatically. Confirm the real hours.");
        entry.UpdatedAt = now;
    }

    public static string AppendNote(string? existing, string addition) =>
        string.IsNullOrWhiteSpace(existing) ? addition : $"{existing.Trim()} · {addition}";

    /// <summary>Drops the auto clock-out flag once someone has confirmed or corrected the hours.</summary>
    public static string? ClearAutoClockOutFlag(string? note)
    {
        if (note is null)
        {
            return null;
        }

        var parts = note.Split(" · ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => !part.StartsWith(AutoClockOutMarker, StringComparison.Ordinal))
            .ToArray();
        return parts.Length == 0 ? null : string.Join(" · ", parts);
    }
}
