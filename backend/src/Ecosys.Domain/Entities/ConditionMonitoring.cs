using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>How far a gauge reading is from normal: inside every limit, in the warning band, or past an alarm limit.</summary>
public static class ConditionLevels
{
    public const string Normal = "Normal";
    public const string Warning = "Warning";
    public const string Alarm = "Alarm";

    public static readonly IReadOnlyList<string> All = [Normal, Warning, Alarm];

    public static int Rank(string? level) => level switch
    {
        Alarm => 2,
        Warning => 1,
        _ => 0
    };
}

public static class ConditionDirections
{
    public const string High = "High";
    public const string Low = "Low";
}

/// <summary>What an alarm does beyond telling asset managers: nothing more, or raise a job.</summary>
public static class ConditionActions
{
    /// <summary>Notify asset managers only.</summary>
    public const string Alert = "Alert";
    /// <summary>Notify, and raise a corrective job when the alarm limit is crossed.</summary>
    public const string WorkOrder = "WorkOrder";

    public static readonly IReadOnlyList<string> All = [Alert, WorkOrder];

    public static string? Find(string? value) =>
        All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

public static class ConditionEventStatuses
{
    public const string Open = "Open";
    public const string Cleared = "Cleared";
}

/// <summary>The limits a meter's readings are judged against.</summary>
public static class ConditionRules
{
    /// <summary>True when the meter has any limit to judge a reading by.</summary>
    public static bool IsMonitored(Meter meter) =>
        meter.MinThreshold.HasValue || meter.MaxThreshold.HasValue || meter.WarningMin.HasValue || meter.WarningMax.HasValue;

    /// <summary>The level of a value and which side it is out on (null when normal).</summary>
    public static (string Level, string? Direction) Classify(Meter meter, decimal value)
    {
        if (meter.MaxThreshold.HasValue && value > meter.MaxThreshold.Value)
            return (ConditionLevels.Alarm, ConditionDirections.High);
        if (meter.MinThreshold.HasValue && value < meter.MinThreshold.Value)
            return (ConditionLevels.Alarm, ConditionDirections.Low);
        if (meter.WarningMax.HasValue && value > meter.WarningMax.Value)
            return (ConditionLevels.Warning, ConditionDirections.High);
        if (meter.WarningMin.HasValue && value < meter.WarningMin.Value)
            return (ConditionLevels.Warning, ConditionDirections.Low);
        return (ConditionLevels.Normal, null);
    }
}

/// <summary>
/// One excursion of a meter outside its limits: from the reading that confirmed it (after the
/// meter's debounce count) to the first reading back inside. It records the worst level and value
/// reached and the job raised for it, so a long excursion alerts once, not on every reading.
/// </summary>
public sealed class ConditionEvent : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid MeterId { get; set; }
    public Guid AssetId { get; set; }
    /// <summary>The worst level reached during the excursion.</summary>
    public string Level { get; set; } = ConditionLevels.Warning;
    public string Direction { get; set; } = ConditionDirections.High;
    public string Status { get; set; } = ConditionEventStatuses.Open;
    public DateTime StartedAt { get; set; }
    public DateTime LastReadingAt { get; set; }
    public DateTime? ClearedAt { get; set; }
    /// <summary>The value furthest out of range (highest for a high excursion, lowest for a low one).</summary>
    public decimal PeakValue { get; set; }
    public int ReadingCount { get; set; }
    public Guid? WorkOrderId { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public Guid? AcknowledgedByUserId { get; set; }
    public string? AcknowledgedByName { get; set; }
    public string? AcknowledgementNote { get; set; }

    public Meter? Meter { get; set; }
    public Asset? Asset { get; set; }
    public WorkOrder? WorkOrder { get; set; }
}
