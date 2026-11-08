using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public static class MeterTypes
{
    public const string RunHours = "RunHours";
    /// <summary>Production processed, in the tenant's production unit (e.g. kg made tea).</summary>
    public const string Production = "Production";
    public const string Odometer = "Odometer";
    public const string Energy = "Energy";
    /// <summary>Boiler fuel: firewood (m³ or kg), furnace oil, briquettes.</summary>
    public const string Fuel = "Fuel";
    public const string Temperature = "Temperature";
    public const string Vibration = "Vibration";
    public const string Pressure = "Pressure";
    public const string Other = "Other";

    public static readonly IReadOnlyList<string> All = [RunHours, Production, Odometer, Energy, Fuel, Temperature, Vibration, Pressure, Other];

    /// <summary>Counters only go up (until reset); gauges are spot measurements.</summary>
    public static bool IsCumulativeByDefault(string type) => type is RunHours or Production or Odometer or Energy or Fuel;

    public static string DefaultUnit(string type, string productionUnit) => type switch
    {
        RunHours => "h",
        Production => productionUnit,
        Odometer => "km",
        Energy => "kWh",
        Fuel => "m³",
        Temperature => "°C",
        Vibration => "mm/s",
        Pressure => "bar",
        _ => string.Empty
    };

    public static string? Find(string? value) =>
        All.FirstOrDefault(x => string.Equals(x, value?.Trim().Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase));
}

public static class MeterReadingSources
{
    public const string Manual = "Manual";
    public const string Import = "Import";
    public const string Sensor = "Sensor";

    public static readonly IReadOnlyList<string> All = [Manual, Import, Sensor];
}

/// <summary>
/// A meter on an asset (scope §2 design rule 3: meters are first-class). Counters (run hours,
/// kg made tea, odometer, energy) feed PM triggers and costing; gauges (temperature, vibration,
/// pressure) feed condition monitoring through their thresholds.
/// </summary>
public sealed class Meter : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid AssetId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MeterType { get; set; } = MeterTypes.RunHours;
    public string Unit { get; set; } = string.Empty;
    public bool IsCumulative { get; set; } = true;

    /// <summary>The asset's main meter; its value is mirrored onto the asset for meter-based PM.</summary>
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;

    public decimal? CurrentValue { get; set; }
    public DateTime? LastReadingAt { get; set; }

    /// <summary>Alarm limits: a reading outside them is flagged out of range and raises an alarm.</summary>
    public decimal? MinThreshold { get; set; }
    public decimal? MaxThreshold { get; set; }

    /// <summary>Warning band, inside the alarm limits: early notice that a reading is drifting.</summary>
    public decimal? WarningMin { get; set; }
    public decimal? WarningMax { get; set; }

    /// <summary>What an alarm does: alert asset managers, or also raise a job (<see cref="ConditionActions"/>).</summary>
    public string ConditionAction { get; set; } = ConditionActions.Alert;

    /// <summary>Readings in a row outside the limits before it counts (sensors are noisy; one spike is not a fault).</summary>
    public int TriggerAfterReadings { get; set; } = 1;

    /// <summary>Priority, and optional job plan, of the job an alarm raises.</summary>
    public string AlarmPriority { get; set; } = "High";
    public Guid? AlarmJobPlanId { get; set; }

    /// <summary>The meter's current condition, from its latest reading, and since when.</summary>
    public string ConditionState { get; set; } = ConditionLevels.Normal;
    public DateTime? ConditionSince { get; set; }

    /// <summary>Latest readings in a row outside the limits; reset by a normal one.</summary>
    public int AbnormalStreak { get; set; }

    public string? ExternalId { get; set; }
    public string? ExternalSystem { get; set; }

    public Tenant? Tenant { get; set; }
    public Asset? Asset { get; set; }
    public ICollection<MeterReading> Readings { get; set; } = new List<MeterReading>();
}

public sealed class MeterReading : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid MeterId { get; set; }
    public Guid AssetId { get; set; }
    public decimal Value { get; set; }

    /// <summary>Counters: increase since the previous reading (0 for the first, or after a reset).</summary>
    public decimal? Delta { get; set; }
    public DateTime ReadAt { get; set; } = DateTime.UtcNow;
    public string Source { get; set; } = MeterReadingSources.Manual;

    /// <summary>Counter was reset or replaced; the value may be lower than the previous one.</summary>
    public bool IsReset { get; set; }
    /// <summary>Past an alarm limit (<see cref="Condition"/> is Alarm).</summary>
    public bool IsOutOfRange { get; set; }
    /// <summary>Normal, Warning or Alarm, judged against the meter's limits when the reading was taken.</summary>
    public string Condition { get; set; } = ConditionLevels.Normal;
    public string? Notes { get; set; }
    public Guid? RecordedByUserId { get; set; }
    public string? RecordedByName { get; set; }

    public Tenant? Tenant { get; set; }
    public Meter? Meter { get; set; }
    public Asset? Asset { get; set; }
}
