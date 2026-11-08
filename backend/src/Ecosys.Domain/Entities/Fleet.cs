using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public static class VehicleTypes
{
    public const string Lorry = "Lorry";
    public const string Pickup = "Pickup";
    public const string Tractor = "Tractor";
    public const string Trailer = "Trailer";
    public const string Motorcycle = "Motorcycle";
    public const string Other = "Other";

    public static readonly IReadOnlyList<string> All = [Lorry, Pickup, Tractor, Trailer, Motorcycle, Other];

    public static string? Find(string? value) => All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>The papers a vehicle must keep current to be on the road.</summary>
public static class VehicleDocumentTypes
{
    public const string Insurance = "Insurance";
    /// <summary>The annual motor vehicle inspection.</summary>
    public const string Inspection = "Inspection";
    /// <summary>Speed governor certificate (commercial vehicles).</summary>
    public const string SpeedGovernor = "SpeedGovernor";

    public static readonly IReadOnlyList<string> All = [Insurance, Inspection, SpeedGovernor];

    public static string Label(string type) => type switch
    {
        Insurance => "Insurance",
        Inspection => "Inspection",
        SpeedGovernor => "Speed governor",
        _ => type,
    };
}

/// <summary>
/// A leaf collection vehicle. The vehicle itself is an <see cref="Asset"/> (so its jobs, PM by
/// kilometre, running checks and fault reports work as for any machine); this holds what only a
/// vehicle has: registration, payload, route, driver, its papers' expiry dates and its tracker.
/// </summary>
public sealed class VehicleProfile : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid AssetId { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public string VehicleType { get; set; } = VehicleTypes.Lorry;
    public string? Make { get; set; }
    public string? VehicleModel { get; set; }
    public int? Year { get; set; }
    /// <summary>Green leaf it can carry, kg.</summary>
    public decimal? PayloadKg { get; set; }
    public string FuelType { get; set; } = "Diesel";
    /// <summary>Collection route or buying centres it serves.</summary>
    public string? Route { get; set; }
    public string? DriverName { get; set; }
    public string? DriverPhone { get; set; }

    public DateTime? InsuranceExpiresOn { get; set; }
    public DateTime? InspectionExpiresOn { get; set; }
    public DateTime? SpeedGovernorExpiresOn { get; set; }

    public Guid? OdometerMeterId { get; set; }
    public Guid? EngineHoursMeterId { get; set; }

    /// <summary>The tracker's device id (IMEI or the tracking platform's unique id).</summary>
    public string? TrackerDeviceId { get; set; }
    public double? LastLatitude { get; set; }
    public double? LastLongitude { get; set; }
    public decimal? LastSpeedKph { get; set; }
    public DateTime? LastPositionAt { get; set; }
    public string? LastPositionSource { get; set; }

    public Asset? Asset { get; set; }
    public Meter? OdometerMeter { get; set; }
    public Meter? EngineHoursMeter { get; set; }
}

/// <summary>A fill-up. Full-tank fills, with the odometer, give the vehicle's kilometres per litre.</summary>
public sealed class FuelLog : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid AssetId { get; set; }
    public DateTime FilledAt { get; set; }
    public decimal Litres { get; set; }
    public decimal? Cost { get; set; }
    public decimal? OdometerKm { get; set; }
    public bool FullTank { get; set; } = true;
    public string? Station { get; set; }
    public string? Notes { get; set; }
    public Guid? RecordedByUserId { get; set; }
    public string? RecordedByName { get; set; }

    public Asset? Asset { get; set; }
}

public static class TelematicsProviders
{
    /// <summary>Traccar (open source), pulled from its REST API.</summary>
    public const string Traccar = "Traccar";

    public static readonly IReadOnlyList<string> All = [Traccar];

    public static string? Find(string? value) => All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// A tenant's vehicle tracking platform, polled for positions, odometer and engine hours. Trackers
/// on any other platform can push the same data with a sensor gateway key instead.
/// </summary>
public sealed class TelematicsConnection : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Provider { get; set; } = TelematicsProviders.Traccar;
    public string BaseUrl { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? EncryptedPassword { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime? LastSyncAt { get; set; }
    public int LastSyncVehicles { get; set; }
    public string? LastSyncError { get; set; }

    public Tenant? Tenant { get; set; }
}
