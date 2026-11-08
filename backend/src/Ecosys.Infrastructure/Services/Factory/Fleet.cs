using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services.Factory;

/// <summary>Where a vehicle is and what its odometer and engine-hour meters read, from a tracker.</summary>
/// <param name="Name">The platform's own name for the vehicle; often the registration.</param>
public sealed record VehiclePosition(
    string DeviceId,
    DateTime At,
    double? Latitude = null,
    double? Longitude = null,
    decimal? SpeedKph = null,
    decimal? OdometerKm = null,
    decimal? EngineHours = null,
    string? Name = null);

public sealed record PositionResult(int Vehicles, int Readings, IReadOnlyList<string> UnknownDevices);

public sealed record VehicleDocument(string Type, string Label, DateTime ExpiresOn, int DaysLeft);

public sealed record FuelEfficiency(decimal? LatestKmPerLitre, decimal? AverageKmPerLitre, bool UsingMore);

public sealed record FleetVehicleRow(
    Guid AssetId,
    string AssetCode,
    string AssetName,
    string? Location,
    string RegistrationNumber,
    string VehicleType,
    string? Make,
    string? Model,
    int? Year,
    decimal? PayloadKg,
    string FuelType,
    string? Route,
    string? DriverName,
    string? DriverPhone,
    Guid? OdometerMeterId,
    decimal? OdometerKm,
    DateTime? OdometerAt,
    Guid? EngineHoursMeterId,
    decimal? EngineHours,
    decimal? KmToService,
    DateTime? ServiceDueOn,
    string? ServicePlanName,
    int OpenJobs,
    IReadOnlyList<VehicleDocument> Documents,
    string? TrackerDeviceId,
    double? LastLatitude,
    double? LastLongitude,
    decimal? LastSpeedKph,
    DateTime? LastPositionAt,
    string? LastPositionSource,
    FuelEfficiency Fuel,
    string Condition,
    string OperationalStatus);

public sealed record SaveVehicle(
    string RegistrationNumber,
    string VehicleType,
    Guid? OrgNodeId,
    string? Make,
    string? Model,
    int? Year,
    decimal? PayloadKg,
    string? FuelType,
    string? Route,
    string? DriverName,
    string? DriverPhone,
    DateTime? InsuranceExpiresOn,
    DateTime? InspectionExpiresOn,
    DateTime? SpeedGovernorExpiresOn,
    string? TrackerDeviceId,
    decimal? OdometerKm,
    bool TrackEngineHours = false,
    decimal? EngineHours = null);

public sealed record NewFuelLog(DateTime? FilledAt, decimal Litres, decimal? Cost, decimal? OdometerKm, bool FullTank, string? Station, string? Notes);

public interface IFleetService
{
    Task<IReadOnlyList<FleetVehicleRow>> ListAsync(Guid tenantId, IReadOnlySet<Guid>? orgNodes, CancellationToken cancellationToken = default);

    /// <summary>Registers a vehicle: an asset (coded by its registration), its profile, and an odometer meter tagged for trackers.</summary>
    Task<VehicleProfile> CreateAsync(Guid tenantId, SaveVehicle request, FactoryActor actor, CancellationToken cancellationToken = default);

    Task<VehicleProfile> UpdateAsync(Guid tenantId, Guid assetId, SaveVehicle request, CancellationToken cancellationToken = default);

    /// <summary>Records a fill-up; its odometer reading also goes on the odometer meter.</summary>
    Task<FuelLog> AddFuelAsync(Guid tenantId, Guid assetId, NewFuelLog request, FactoryActor actor, CancellationToken cancellationToken = default);

    /// <summary>Applies tracker positions to the vehicles they belong to (by device id, or by registration as the platform's name).</summary>
    Task<PositionResult> ApplyPositionsAsync(Guid tenantId, IReadOnlyList<VehiclePosition> positions, string source, CancellationToken cancellationToken = default);
}

internal sealed class FleetService(
    AppDbContext dbContext,
    IMeterReadingService meterReadingService,
    IConditionMonitor conditionMonitor) : IFleetService
{
    /// <summary>A tracker's odometer is stored at most this often; it changes with every report.</summary>
    internal static readonly TimeSpan TrackerReadingEvery = TimeSpan.FromMinutes(15);

    /// <summary>Papers ending within this many days are flagged.</summary>
    public const int DocumentWarningDays = 30;

    private static readonly string[] Finished =
        [WorkOrderStatuses.Completed, WorkOrderStatuses.Verified, WorkOrderStatuses.Closed, WorkOrderStatuses.Cancelled, WorkOrderStatuses.Rejected];

    public async Task<IReadOnlyList<FleetVehicleRow>> ListAsync(Guid tenantId, IReadOnlySet<Guid>? orgNodes, CancellationToken cancellationToken = default)
    {
        var profiles = await dbContext.VehicleProfiles.AsNoTracking()
            .Include(x => x.Asset).ThenInclude(x => x!.OrgNode)
            .Include(x => x.OdometerMeter)
            .Include(x => x.EngineHoursMeter)
            .Where(x => x.TenantId == tenantId && x.Asset!.Status != "Inactive"
                && (orgNodes == null || (x.Asset.OrgNodeId != null && orgNodes.Contains(x.Asset.OrgNodeId.Value))))
            .OrderBy(x => x.RegistrationNumber)
            .ToListAsync(cancellationToken);
        var assetIds = profiles.Select(x => x.AssetId).ToList();

        var openJobs = (await dbContext.WorkOrders.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.AssetId != null && assetIds.Contains(x.AssetId.Value) && !Finished.Contains(x.Status))
                .GroupBy(x => x.AssetId!.Value)
                .Select(g => new { AssetId = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.AssetId, x => x.Count);
        var plans = (await dbContext.PreventiveMaintenancePlans.AsNoTracking()
                .Where(x => x.TenantId == tenantId && assetIds.Contains(x.AssetId) && x.Status == "Active")
                .Select(x => new { x.AssetId, Name = x.Name ?? x.Frequency, x.TriggerType, x.MeterId, x.NextMeterDue, x.NextPmDate })
                .ToListAsync(cancellationToken))
            .ToLookup(x => x.AssetId);
        var since = DateTime.UtcNow.AddDays(-180);
        var fuel = (await dbContext.FuelLogs.AsNoTracking()
                .Where(x => x.TenantId == tenantId && assetIds.Contains(x.AssetId) && x.FilledAt >= since)
                .OrderBy(x => x.FilledAt)
                .ToListAsync(cancellationToken))
            .ToLookup(x => x.AssetId);
        var conditions = (await dbContext.Meters.AsNoTracking()
                .Where(x => x.TenantId == tenantId && assetIds.Contains(x.AssetId) && x.IsActive && x.ConditionState != ConditionLevels.Normal)
                .Select(x => new { x.AssetId, x.ConditionState })
                .ToListAsync(cancellationToken))
            .ToLookup(x => x.AssetId, x => x.ConditionState);

        var today = DateTime.UtcNow.Date;
        return profiles.Select(p =>
        {
            var odometer = p.OdometerMeter;
            var meterPlans = plans[p.AssetId].Where(x => x.TriggerType == PmTriggerTypes.Meter && x.MeterId == p.OdometerMeterId && x.NextMeterDue != null).ToList();
            var nextByKm = meterPlans.OrderBy(x => x.NextMeterDue).FirstOrDefault();
            var nextByDate = plans[p.AssetId].Where(x => x.TriggerType == PmTriggerTypes.Calendar && x.NextPmDate != null).OrderBy(x => x.NextPmDate).FirstOrDefault();
            var worst = conditions[p.AssetId].OrderByDescending(ConditionLevels.Rank).FirstOrDefault() ?? ConditionLevels.Normal;
            return new FleetVehicleRow(
                p.AssetId, p.Asset!.AssetCode, p.Asset.AssetName, p.Asset.OrgNode?.Name,
                p.RegistrationNumber, p.VehicleType, p.Make, p.VehicleModel, p.Year, p.PayloadKg, p.FuelType, p.Route, p.DriverName, p.DriverPhone,
                p.OdometerMeterId, odometer?.CurrentValue, odometer?.LastReadingAt,
                p.EngineHoursMeterId, p.EngineHoursMeter?.CurrentValue,
                nextByKm is null || odometer?.CurrentValue is not { } km ? null : nextByKm.NextMeterDue - km,
                nextByDate?.NextPmDate,
                nextByKm?.Name ?? nextByDate?.Name,
                openJobs.GetValueOrDefault(p.AssetId),
                Documents(p, today),
                p.TrackerDeviceId, p.LastLatitude, p.LastLongitude, p.LastSpeedKph, p.LastPositionAt, p.LastPositionSource,
                Efficiency(fuel[p.AssetId].ToList()),
                worst,
                p.Asset.OperationalStatus);
        }).ToList();
    }

    public async Task<VehicleProfile> CreateAsync(Guid tenantId, SaveVehicle request, FactoryActor actor, CancellationToken cancellationToken = default)
    {
        var registration = NormaliseRegistration(request.RegistrationNumber);
        if (request.OrgNodeId is not { } node || !await dbContext.OrgNodes.AnyAsync(x => x.TenantId == tenantId && x.Id == node && x.Level != OrgNodeLevels.Group, cancellationToken))
            throw new BusinessRuleException("Choose the factory (or section) the vehicle belongs to.");
        if (await dbContext.Assets.AnyAsync(x => x.TenantId == tenantId && x.AssetCode == registration, cancellationToken))
            throw new ConflictException($"An asset already uses the code {registration}.");

        var type = VehicleTypes.Find(request.VehicleType) ?? throw new BusinessRuleException($"Vehicle type must be one of: {string.Join(", ", VehicleTypes.All)}.");
        var asset = new Asset
        {
            TenantId = tenantId,
            AssetCode = registration,
            AssetName = string.Join(" ", new[] { request.Make?.Trim(), request.Model?.Trim(), registration }.Where(x => !string.IsNullOrEmpty(x))),
            OrgNodeId = node,
            AssetType = $"Vehicle ({type})",
            OwnershipType = "Owned",
            Criticality = AssetCriticality.B,
            OperationalStatus = AssetOperationalStatuses.Operational,
            Manufacturer = request.Make?.Trim(),
            Model = request.Model?.Trim(),
            YearOfManufacture = request.Year,
            Status = "Active",
        };
        dbContext.Assets.Add(asset);

        var odometer = new Meter
        {
            TenantId = tenantId, AssetId = asset.Id, Asset = asset, Name = "Odometer", MeterType = MeterTypes.Odometer, Unit = "km",
            IsCumulative = true, IsPrimary = true, ExternalId = $"{registration}.ODO",
        };
        dbContext.Meters.Add(odometer);
        Meter? engineHours = null;
        if (request.TrackEngineHours)
        {
            engineHours = new Meter
            {
                TenantId = tenantId, AssetId = asset.Id, Asset = asset, Name = "Engine hours", MeterType = MeterTypes.RunHours, Unit = "h",
                IsCumulative = true, ExternalId = $"{registration}.HRS",
            };
            dbContext.Meters.Add(engineHours);
        }

        var profile = new VehicleProfile { TenantId = tenantId, AssetId = asset.Id, Asset = asset, OdometerMeterId = odometer.Id, EngineHoursMeterId = engineHours?.Id };
        Apply(profile, request, registration, type);
        await EnsureDeviceFreeAsync(tenantId, profile, cancellationToken);
        dbContext.VehicleProfiles.Add(profile);
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var (meter, value) in new[] { (odometer, request.OdometerKm), (engineHours, request.EngineHours) })
        {
            if (meter is null || value is null)
                continue;
            var reading = await meterReadingService.RecordAsync(meter, new NewMeterReading(value.Value, null, Notes: "Opening reading", UserId: actor.UserId, UserName: actor.Name), cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await conditionMonitor.EvaluateAsync(meter, reading, actor, cancellationToken);
        }

        return profile;
    }

    public async Task<VehicleProfile> UpdateAsync(Guid tenantId, Guid assetId, SaveVehicle request, CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.VehicleProfiles.Include(x => x.Asset)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.AssetId == assetId, cancellationToken)
            ?? throw new NotFoundException("Vehicle was not found.");
        var type = VehicleTypes.Find(request.VehicleType) ?? throw new BusinessRuleException($"Vehicle type must be one of: {string.Join(", ", VehicleTypes.All)}.");
        var registration = NormaliseRegistration(request.RegistrationNumber);
        if (registration != profile.RegistrationNumber && await dbContext.VehicleProfiles.AnyAsync(x => x.TenantId == tenantId && x.RegistrationNumber == registration && x.Id != profile.Id, cancellationToken))
            throw new ConflictException($"Another vehicle is registered {registration}.");

        Apply(profile, request, registration, type);
        await EnsureDeviceFreeAsync(tenantId, profile, cancellationToken);
        profile.Asset!.Manufacturer = profile.Make;
        profile.Asset.Model = profile.VehicleModel;
        profile.Asset.YearOfManufacture = profile.Year;
        profile.Asset.AssetType = $"Vehicle ({type})";
        await dbContext.SaveChangesAsync(cancellationToken);
        return profile;
    }

    public async Task<FuelLog> AddFuelAsync(Guid tenantId, Guid assetId, NewFuelLog request, FactoryActor actor, CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.VehicleProfiles.Include(x => x.OdometerMeter).ThenInclude(x => x!.Asset)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.AssetId == assetId, cancellationToken)
            ?? throw new NotFoundException("Vehicle was not found.");
        if (request.Litres is <= 0 or > 2000)
            throw new BusinessRuleException("Litres must be more than 0 (and a believable fill-up).");
        if (request.Cost is < 0)
            throw new BusinessRuleException("Cost cannot be negative.");

        var filledAt = AssetLifecycleService.EffectiveAt(SensorFeed.ToUtc(request.FilledAt));
        if (filledAt > DateTime.UtcNow.AddMinutes(10))
            throw new BusinessRuleException("A fill-up cannot be in the future.");

        var log = new FuelLog
        {
            TenantId = tenantId, AssetId = assetId, FilledAt = filledAt, Litres = request.Litres, Cost = request.Cost,
            OdometerKm = request.OdometerKm, FullTank = request.FullTank,
            Station = string.IsNullOrWhiteSpace(request.Station) ? null : request.Station.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            RecordedByUserId = actor.UserId, RecordedByName = actor.Name,
        };
        dbContext.FuelLogs.Add(log);

        // The fill-up's odometer is also a meter reading, unless the meter already has a later one.
        MeterReading? reading = null;
        if (request.OdometerKm is { } km && profile.OdometerMeter is { IsActive: true } odometer && (odometer.LastReadingAt is null || filledAt > odometer.LastReadingAt))
        {
            reading = await meterReadingService.RecordAsync(odometer, new NewMeterReading(km, filledAt, Notes: "Fuel log", UserId: actor.UserId, UserName: actor.Name), cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (reading is not null)
            await conditionMonitor.EvaluateAsync(profile.OdometerMeter!, reading, actor, cancellationToken);
        return log;
    }

    public async Task<PositionResult> ApplyPositionsAsync(Guid tenantId, IReadOnlyList<VehiclePosition> positions, string source, CancellationToken cancellationToken = default)
    {
        if (positions.Count > SensorFeed.MaxBatch)
            throw new BusinessRuleException($"Send at most {SensorFeed.MaxBatch} positions at a time.");

        var ids = positions.Select(x => x.DeviceId?.Trim()).Where(x => !string.IsNullOrEmpty(x)).Select(x => x!).Distinct().ToList();
        var names = positions.Select(x => x.Name is null ? null : NormaliseRegistration(x.Name)).Where(x => !string.IsNullOrEmpty(x)).Select(x => x!).Distinct().ToList();
        var profiles = await dbContext.VehicleProfiles.IgnoreQueryFilters()
            .Include(x => x.OdometerMeter).ThenInclude(x => x!.Asset)
            .Include(x => x.EngineHoursMeter).ThenInclude(x => x!.Asset)
            .Where(x => x.TenantId == tenantId && ((x.TrackerDeviceId != null && ids.Contains(x.TrackerDeviceId)) || names.Contains(x.RegistrationNumber)))
            .ToListAsync(cancellationToken);

        var unknown = new List<string>();
        var vehicles = new HashSet<Guid>();
        var readings = 0;
        var actor = new FactoryActor(null, source);
        foreach (var position in positions.OrderBy(x => x.At))
        {
            var deviceId = position.DeviceId?.Trim();
            var name = position.Name is null ? null : NormaliseRegistration(position.Name);
            var profile = profiles.FirstOrDefault(x => x.TrackerDeviceId != null && x.TrackerDeviceId == deviceId)
                ?? profiles.FirstOrDefault(x => name != null && x.RegistrationNumber == name);
            if (profile is null)
            {
                unknown.Add(deviceId ?? name ?? "?");
                continue;
            }

            var at = SensorFeed.ToUtc(position.At) ?? DateTime.UtcNow;
            vehicles.Add(profile.AssetId);
            if (position.Latitude is { } lat && position.Longitude is { } lng && (profile.LastPositionAt is null || at > profile.LastPositionAt))
            {
                profile.LastLatitude = lat;
                profile.LastLongitude = lng;
                profile.LastSpeedKph = position.SpeedKph;
                profile.LastPositionAt = at;
                profile.LastPositionSource = source;
            }

            foreach (var (meter, value) in new[] { (profile.OdometerMeter, position.OdometerKm), (profile.EngineHoursMeter, position.EngineHours) })
            {
                if (meter is not { IsActive: true } || value is not { } v || v < 0)
                    continue;
                if (meter.LastReadingAt is { } last && (at <= last || (at - last < TrackerReadingEvery && v >= (meter.CurrentValue ?? 0))))
                    continue;
                if (meter.CurrentValue is { } current && v == current)
                    continue;

                var reset = meter.CurrentValue is { } before && v < before;
                var reading = await meterReadingService.RecordAsync(meter, new NewMeterReading(v, at, MeterReadingSources.Sensor, reset,
                    reset ? "The tracker's counter went down, so it was taken as a reset or replacement." : null, UserName: source), cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                await conditionMonitor.EvaluateAsync(meter, reading, actor, cancellationToken);
                readings++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new PositionResult(vehicles.Count, readings, unknown.Distinct().ToList());
    }

    internal static string NormaliseRegistration(string? value)
    {
        var registration = new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return registration.Length is >= 3 and <= 20 ? registration : throw new BusinessRuleException("Enter the vehicle's registration, e.g. KCA 123A.");
    }

    internal static IReadOnlyList<VehicleDocument> Documents(VehicleProfile profile, DateTime today) =>
        new[] { (VehicleDocumentTypes.Insurance, profile.InsuranceExpiresOn), (VehicleDocumentTypes.Inspection, profile.InspectionExpiresOn), (VehicleDocumentTypes.SpeedGovernor, profile.SpeedGovernorExpiresOn) }
            .Where(x => x.Item2 is not null)
            .Select(x => new VehicleDocument(x.Item1, VehicleDocumentTypes.Label(x.Item1), x.Item2!.Value.Date, (int)(x.Item2!.Value.Date - today).TotalDays))
            .OrderBy(x => x.DaysLeft)
            .ToList();

    /// <summary>
    /// Kilometres per litre between full-tank fills: the distance since the last full fill over the
    /// litres put in since (including part fills). "Using more" when the latest is a fifth worse
    /// than the vehicle's average: a fault (injectors, tyres, brakes dragging) or fuel going missing.
    /// </summary>
    internal static FuelEfficiency Efficiency(IReadOnlyList<FuelLog> logs)
    {
        var figures = new List<decimal>();
        FuelLog? lastFull = null;
        decimal litres = 0;
        foreach (var log in logs.OrderBy(x => x.FilledAt))
        {
            litres += log.Litres;
            if (!log.FullTank || log.OdometerKm is null)
                continue;
            if (lastFull is not null && log.OdometerKm > lastFull.OdometerKm && litres > 0)
                figures.Add(Math.Round((log.OdometerKm.Value - lastFull.OdometerKm!.Value) / litres, 2));
            lastFull = log;
            litres = 0;
        }

        if (figures.Count == 0)
            return new FuelEfficiency(null, null, false);
        var latest = figures[^1];
        var average = Math.Round(figures.Average(), 2);
        return new FuelEfficiency(latest, average, figures.Count >= 3 && latest < average * 0.8m);
    }

    private static void Apply(VehicleProfile profile, SaveVehicle request, string registration, string type)
    {
        if (request.Year is < 1950 || request.Year > DateTime.UtcNow.Year + 1)
            throw new BusinessRuleException("Enter the year it was made.");
        if (request.PayloadKg is < 0)
            throw new BusinessRuleException("Payload cannot be negative.");

        profile.RegistrationNumber = registration;
        profile.VehicleType = type;
        profile.Make = Clean(request.Make);
        profile.VehicleModel = Clean(request.Model);
        profile.Year = request.Year;
        profile.PayloadKg = request.PayloadKg;
        profile.FuelType = Clean(request.FuelType) ?? "Diesel";
        profile.Route = Clean(request.Route);
        profile.DriverName = Clean(request.DriverName);
        profile.DriverPhone = Clean(request.DriverPhone);
        profile.InsuranceExpiresOn = request.InsuranceExpiresOn?.Date;
        profile.InspectionExpiresOn = request.InspectionExpiresOn?.Date;
        profile.SpeedGovernorExpiresOn = request.SpeedGovernorExpiresOn?.Date;
        profile.TrackerDeviceId = Clean(request.TrackerDeviceId);
    }

    private async Task EnsureDeviceFreeAsync(Guid tenantId, VehicleProfile profile, CancellationToken cancellationToken)
    {
        if (profile.TrackerDeviceId is { } device
            && await dbContext.VehicleProfiles.AnyAsync(x => x.TenantId == tenantId && x.Id != profile.Id && x.TrackerDeviceId == device, cancellationToken))
            throw new ConflictException($"Tracker {device} is already fitted to another vehicle.");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>One tracking platform: fetches the latest position of every vehicle it tracks.</summary>
public interface ITelematicsProvider
{
    string Provider { get; }
    Task<IReadOnlyList<VehiclePosition>> FetchAsync(TelematicsConnection connection, string? password, CancellationToken cancellationToken = default);
}

/// <summary>
/// Traccar's REST API: /api/devices for each device's unique id and name, /api/positions for the
/// latest fix. Speed is in knots; totalDistance (metres) and hours (milliseconds) are its odometer
/// and engine hours, or the vehicle's own odometer when the tracker reads it.
/// </summary>
internal sealed class TraccarProvider(IHttpClientFactory httpClientFactory) : ITelematicsProvider
{
    public string Provider => TelematicsProviders.Traccar;

    public async Task<IReadOnlyList<VehiclePosition>> FetchAsync(TelematicsConnection connection, string? password, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(nameof(TraccarProvider));
        client.Timeout = TimeSpan.FromSeconds(30);
        var root = connection.BaseUrl.TrimEnd('/');
        var auth = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{connection.Username}:{password}")));

        using var devices = await GetJsonAsync(client, $"{root}/api/devices", auth, cancellationToken);
        using var positions = await GetJsonAsync(client, $"{root}/api/positions", auth, cancellationToken);
        var byId = devices.RootElement.EnumerateArray()
            .Where(x => x.TryGetProperty("id", out _))
            .ToDictionary(x => x.GetProperty("id").GetInt64(), x => (UniqueId: Text(x, "uniqueId"), Name: Text(x, "name")));

        var result = new List<VehiclePosition>();
        foreach (var position in positions.RootElement.EnumerateArray())
        {
            if (!position.TryGetProperty("deviceId", out var deviceIdElement) || !byId.TryGetValue(deviceIdElement.GetInt64(), out var device) || device.UniqueId is null)
                continue;
            var at = position.TryGetProperty("fixTime", out var fix) && fix.TryGetDateTime(out var fixTime) ? fixTime.ToUniversalTime() : DateTime.UtcNow;
            decimal? odometerKm = null, hours = null;
            if (position.TryGetProperty("attributes", out var attributes) && attributes.ValueKind == JsonValueKind.Object)
            {
                var metres = Number(attributes, "odometer") ?? Number(attributes, "totalDistance");
                odometerKm = metres is null ? null : Math.Round(metres.Value / 1000m, 1);
                hours = Number(attributes, "hours") is { } ms ? Math.Round(ms / 3_600_000m, 1) : null;
            }

            result.Add(new VehiclePosition(
                device.UniqueId,
                at,
                position.TryGetProperty("latitude", out var lat) ? lat.GetDouble() : null,
                position.TryGetProperty("longitude", out var lng) ? lng.GetDouble() : null,
                Number(position, "speed") is { } knots ? Math.Round(knots * 1.852m, 1) : null,
                odometerKm,
                hours,
                device.Name));
        }

        return result;
    }

    private static async Task<JsonDocument> GetJsonAsync(HttpClient client, string url, AuthenticationHeaderValue auth, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = auth;
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new BusinessRuleException($"Traccar answered {(int)response.StatusCode} for {new Uri(url).AbsolutePath}{(response.StatusCode == System.Net.HttpStatusCode.Unauthorized ? ": check the user name and password" : string.Empty)}.");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new BusinessRuleException($"Traccar's {new Uri(url).AbsolutePath} did not return a list.");
        return document;
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static decimal? Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDecimal() : null;
}

public sealed record TelematicsSyncResult(int Fetched, PositionResult Applied);

public interface ITelematicsSync
{
    /// <summary>Pulls one tenant's tracking platform now and applies what it returns. Records the outcome on the connection.</summary>
    Task<TelematicsSyncResult> SyncAsync(TelematicsConnection connection, CancellationToken cancellationToken = default);

    /// <summary>Every enabled connection; one failing does not stop the others. Returns how many synced.</summary>
    Task<int> SyncAllAsync(CancellationToken cancellationToken = default);
}

internal sealed class TelematicsSync(
    AppDbContext dbContext,
    IFleetService fleetService,
    ISecretEncryptionService secretEncryptionService,
    IEnumerable<ITelematicsProvider> providers,
    ILogger<TelematicsSync> logger) : ITelematicsSync
{
    public async Task<TelematicsSyncResult> SyncAsync(TelematicsConnection connection, CancellationToken cancellationToken = default)
    {
        var provider = providers.FirstOrDefault(x => x.Provider == connection.Provider)
            ?? throw new BusinessRuleException($"No adapter for {connection.Provider}.");
        try
        {
            var positions = await provider.FetchAsync(connection, secretEncryptionService.Decrypt(connection.EncryptedPassword), cancellationToken);
            var applied = await fleetService.ApplyPositionsAsync(connection.TenantId, positions, connection.Provider, cancellationToken);
            connection.LastSyncAt = DateTime.UtcNow;
            connection.LastSyncVehicles = applied.Vehicles;
            connection.LastSyncError = applied.UnknownDevices.Count == 0 ? null
                : $"{applied.UnknownDevices.Count} device(s) not matched to a vehicle: {string.Join(", ", applied.UnknownDevices.Take(5))}";
            await dbContext.SaveChangesAsync(cancellationToken);
            return new TelematicsSyncResult(positions.Count, applied);
        }
        catch (Exception ex) when (ex is BusinessRuleException or HttpRequestException or TaskCanceledException or JsonException)
        {
            connection.LastSyncAt = DateTime.UtcNow;
            connection.LastSyncError = ex is BusinessRuleException ? ex.Message : $"Could not reach {connection.BaseUrl}: {ex.Message}";
            await dbContext.SaveChangesAsync(CancellationToken.None);
            throw new BusinessRuleException(connection.LastSyncError);
        }
    }

    public async Task<int> SyncAllAsync(CancellationToken cancellationToken = default)
    {
        var connections = await dbContext.TelematicsConnections.IgnoreQueryFilters()
            .Where(x => x.IsEnabled && x.Tenant!.IsActive)
            .ToListAsync(cancellationToken);
        var synced = 0;
        foreach (var connection in connections)
        {
            try
            {
                await SyncAsync(connection, cancellationToken);
                synced++;
            }
            catch (BusinessRuleException ex)
            {
                logger.LogWarning("Telematics sync for tenant {TenantId} failed: {Error}", connection.TenantId, ex.Message);
            }
        }

        return synced;
    }
}

public interface IFleetAlertRunner
{
    /// <summary>Warns asset managers about vehicle papers ending soon (30 days ahead, and on the day). Returns notifications sent.</summary>
    Task<int> RunAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}

internal sealed class FleetAlertRunner(AppDbContext dbContext, INotificationDispatcher dispatcher, ILogger<FleetAlertRunner> logger) : IFleetAlertRunner
{
    public const string DocumentExpiring = "fleet.document-expiring";

    public async Task<int> RunAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var today = utcNow.Date;
        var horizon = today.AddDays(FleetService.DocumentWarningDays);
        var profiles = await dbContext.VehicleProfiles.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Asset!.Status != "Inactive" && x.Asset.Tenant!.IsActive
                && ((x.InsuranceExpiresOn >= today && x.InsuranceExpiresOn <= horizon)
                    || (x.InspectionExpiresOn >= today && x.InspectionExpiresOn <= horizon)
                    || (x.SpeedGovernorExpiresOn >= today && x.SpeedGovernorExpiresOn <= horizon)))
            .Include(x => x.Asset)
            .ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var tenant in profiles.GroupBy(x => x.TenantId))
        {
            var due = tenant
                .SelectMany(p => FleetService.Documents(p, today).Where(d => d.DaysLeft >= 0 && d.DaysLeft <= FleetService.DocumentWarningDays)
                    .Select(d => (Profile: p, Document: d, Key: $"{DocumentExpiring}:{d.Type}{(d.DaysLeft == 0 ? ":today" : string.Empty)}")))
                .ToList();
            var ids = due.Select(x => x.Profile.Id).Distinct().ToList();
            var keys = due.Select(x => x.Key).Distinct().ToList();
            var already = (await dbContext.ActivityReminderLogs.IgnoreQueryFilters().AsNoTracking()
                    .Where(x => x.TenantId == tenant.Key && ids.Contains(x.EntityId) && keys.Contains(x.NotificationKey))
                    .Select(x => new { x.EntityId, x.NotificationKey, x.ForTime })
                    .ToListAsync(cancellationToken))
                .Select(x => (x.EntityId, x.NotificationKey, x.ForTime.Date))
                .ToHashSet();
            due = due.Where(x => !already.Contains((x.Profile.Id, x.Key, x.Document.ExpiresOn))).ToList();
            if (due.Count == 0)
                continue;

            foreach (var item in due)
                dbContext.ActivityReminderLogs.Add(new ActivityReminderLog { TenantId = tenant.Key, EntityId = item.Profile.Id, NotificationKey = item.Key, ForTime = item.Document.ExpiresOn });
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                logger.LogDebug(ex, "Vehicle paper warnings for tenant {TenantId} already recorded.", tenant.Key);
                dbContext.ChangeTracker.Clear();
                continue;
            }

            foreach (var manager in await AssetAlerts.AssetManagersAsync(dbContext, tenant.Key, cancellationToken))
            {
                var theirs = due.Where(x => manager.Scope is null || (x.Profile.Asset!.OrgNodeId is { } node && manager.Scope.Contains(node)))
                    .OrderBy(x => x.Document.ExpiresOn).ToList();
                if (theirs.Count == 0)
                    continue;
                var lines = theirs.Select(x => $"{x.Profile.RegistrationNumber}: {x.Document.Label} {(x.Document.DaysLeft == 0 ? "ends today" : $"ends {x.Document.ExpiresOn:dd MMM yyyy}")}").ToList();
                try
                {
                    await dispatcher.NotifyAsync(new NotificationRequest(
                        tenant.Key,
                        DocumentExpiring,
                        [manager.UserId],
                        theirs.Count == 1 ? $"{theirs[0].Document.Label} ending: {theirs[0].Profile.RegistrationNumber}" : $"{theirs.Count} vehicle papers ending soon",
                        string.Join("; ", lines),
                        "/maintenance/fleet",
                        nameof(Asset),
                        theirs.Count == 1 ? theirs[0].Profile.AssetId : null,
                        new Dictionary<string, string?> { ["Vehicles"] = string.Join("\n", lines), ["Count"] = theirs.Count.ToString() },
                        NotificationSeverities.Warning), cancellationToken);
                    sent++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Vehicle paper warning to {UserId} failed.", manager.UserId);
                }
            }
        }

        return sent;
    }
}

/// <summary>Pulls tracking platforms every ten minutes and sends vehicle paper warnings hourly.</summary>
public sealed class FleetWorker(IServiceScopeFactory serviceScopeFactory, ILogger<FleetWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10));
        var lastAlerts = DateTime.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ITelematicsSync>().SyncAllAsync(stoppingToken);
                if (DateTime.UtcNow - lastAlerts >= TimeSpan.FromHours(1))
                {
                    await scope.ServiceProvider.GetRequiredService<IFleetAlertRunner>().RunAsync(DateTime.UtcNow, stoppingToken);
                    lastAlerts = DateTime.UtcNow;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Fleet cycle failed.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
