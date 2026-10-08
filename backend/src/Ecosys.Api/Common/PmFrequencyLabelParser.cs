using System.Text.RegularExpressions;

namespace Ecosys.Api.Common;

/// <summary>
/// Converts free-text maintenance frequencies ("Monthly", "Semi-Annual", "Every 2 weeks", "3 months")
/// into the structured value + unit the PM scheduler works from. Units match the asset API:
/// days, weeks, months, quarters, years.
/// </summary>
public static partial class PmFrequencyLabelParser
{
    private static readonly Dictionary<string, (int Value, string Unit)> NamedFrequencies = new(StringComparer.OrdinalIgnoreCase)
    {
        ["daily"] = (1, "days"),
        ["weekly"] = (1, "weeks"),
        ["fortnightly"] = (2, "weeks"),
        ["biweekly"] = (2, "weeks"),
        ["monthly"] = (1, "months"),
        ["bimonthly"] = (2, "months"),
        ["quarterly"] = (1, "quarters"),
        ["semiannual"] = (6, "months"),
        ["semiannually"] = (6, "months"),
        ["biannual"] = (6, "months"),
        ["biannually"] = (6, "months"),
        ["halfyearly"] = (6, "months"),
        ["sixmonthly"] = (6, "months"),
        ["annual"] = (1, "years"),
        ["annually"] = (1, "years"),
        ["yearly"] = (1, "years"),
    };

    public static bool TryParse(string? label, out int value, out string unit)
    {
        value = 0;
        unit = string.Empty;
        if (string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        var compact = NonLetters().Replace(label, string.Empty);
        if (NamedFrequencies.TryGetValue(compact, out var named))
        {
            (value, unit) = named;
            return true;
        }

        var match = CountAndUnit().Match(label.Trim());
        if (!match.Success || !int.TryParse(match.Groups["value"].Value, out var parsed) || parsed <= 0)
        {
            return false;
        }

        value = parsed;
        unit = match.Groups["unit"].Value.ToLowerInvariant() switch
        {
            "day" => "days",
            "week" => "weeks",
            "month" => "months",
            "quarter" => "quarters",
            _ => "years"
        };
        return true;
    }

    [GeneratedRegex("[^a-zA-Z]")]
    private static partial Regex NonLetters();

    [GeneratedRegex(@"^(?:every\s+)?(?<value>\d{1,4})\s*-?\s*(?<unit>day|week|month|quarter|year)s?$", RegexOptions.IgnoreCase)]
    private static partial Regex CountAndUnit();
}
