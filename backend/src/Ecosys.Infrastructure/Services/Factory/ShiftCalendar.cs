using System.Text.Json;
using Ecosys.Domain.Entities;
using Ecosys.Shared.Errors;

namespace Ecosys.Infrastructure.Services.Factory;

public sealed record ShiftDefinition(string Name, TimeOnly Start, TimeOnly End)
{
    /// <summary>A shift whose end is not after its start runs past midnight (e.g. 18:00–06:00).</summary>
    public bool Overnight => End <= Start;
}

/// <summary>The shift or day a check belongs to, in UTC, with a stable key such as "2026-11-05/Night".</summary>
public sealed record CheckPeriod(string Key, string? ShiftName, DateTime StartUtc, DateTime EndUtc);

/// <summary>
/// Works out shifts in the factory's own time zone. A shift is named by the local date it starts
/// on, so the night shift that starts at 18:00 on the 5th and ends at 06:00 on the 6th is
/// "2026-11-05/Night" all the way through.
/// </summary>
public static class ShiftCalendar
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private sealed record ShiftJson(string? Name, string? Start, string? End);

    /// <summary>Day 06:00–18:00 and night 18:00–06:00, used until the factory sets its own.</summary>
    public const string DefaultJson = "[{\"name\":\"Day\",\"start\":\"06:00\",\"end\":\"18:00\"},{\"name\":\"Night\",\"start\":\"18:00\",\"end\":\"06:00\"}]";

    /// <summary>Reads the shift list. Blank (settings saved before shifts existed) means the default day and night shifts.</summary>
    public static IReadOnlyList<ShiftDefinition> Parse(string? json)
    {
        List<ShiftJson>? items;
        try
        {
            items = JsonSerializer.Deserialize<List<ShiftJson>>(string.IsNullOrWhiteSpace(json) ? DefaultJson : json, Json);
        }
        catch (JsonException)
        {
            throw new BusinessRuleException("Shifts could not be read.");
        }

        var shifts = (items ?? [])
            .Select(x => new ShiftDefinition(
                string.IsNullOrWhiteSpace(x.Name) ? throw new BusinessRuleException("Every shift needs a name.") : x.Name.Trim(),
                TimeOnly.TryParse(x.Start, out var start) ? start : throw new BusinessRuleException($"Shift '{x.Name}' needs a start time such as 06:00."),
                TimeOnly.TryParse(x.End, out var end) ? end : throw new BusinessRuleException($"Shift '{x.Name}' needs an end time such as 18:00.")))
            .ToList();
        if (shifts.Count == 0)
        {
            throw new BusinessRuleException("Define at least one shift.");
        }

        if (shifts.Select(x => x.Name.ToLowerInvariant()).Distinct().Count() != shifts.Count)
        {
            throw new BusinessRuleException("Shift names must be different.");
        }

        return shifts;
    }

    public static string Serialize(IReadOnlyList<ShiftDefinition> shifts) =>
        JsonSerializer.Serialize(shifts.Select(x => new { name = x.Name, start = x.Start.ToString("HH:mm"), end = x.End.ToString("HH:mm") }));

    /// <summary>The period a check of this frequency is due in at <paramref name="utcNow"/>; null between shifts.</summary>
    public static CheckPeriod? Current(string frequency, IReadOnlyList<ShiftDefinition> shifts, TimeZoneInfo zone, DateTime utcNow)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), zone);
        var today = DateOnly.FromDateTime(local);
        if (frequency == CheckFrequencies.Daily)
        {
            return new CheckPeriod(today.ToString("yyyy-MM-dd"), null, ToUtc(today.ToDateTime(TimeOnly.MinValue), zone), ToUtc(today.AddDays(1).ToDateTime(TimeOnly.MinValue), zone));
        }

        // Yesterday's overnight shift may still be running this morning.
        foreach (var day in new[] { today, today.AddDays(-1) })
        {
            foreach (var shift in shifts)
            {
                var startLocal = day.ToDateTime(shift.Start);
                var endLocal = (shift.Overnight ? day.AddDays(1) : day).ToDateTime(shift.End);
                if (local >= startLocal && local < endLocal)
                {
                    return new CheckPeriod($"{day:yyyy-MM-dd}/{shift.Name}", shift.Name, ToUtc(startLocal, zone), ToUtc(endLocal, zone));
                }
            }
        }

        return null;
    }

    private static DateTime ToUtc(DateTime local, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), zone);
}
