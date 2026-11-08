using System.ComponentModel.DataAnnotations;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// The leaf collection fleet: each vehicle with its odometer, next service, papers, fuel use and
/// last known position, and the link to a vehicle tracking platform.
/// </summary>
[ApiController]
[Authorize]
public sealed class FleetController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IOrgScopeService orgScopeService,
    IFleetService fleetService,
    ISecretEncryptionService secretEncryptionService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet("api/fleet/vehicles")]
    public async Task<ActionResult<IReadOnlyList<FleetVehicleRow>>> List([FromQuery] Guid? orgNodeId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var nodes = await orgScopeService.VisibleNodesAsync(TenantId, UserId, orgNodeId, cancellationToken);
        return Ok(await fleetService.ListAsync(TenantId, nodes, cancellationToken));
    }

    [HttpGet("api/fleet/vehicles/{assetId:guid}")]
    public async Task<ActionResult<FleetVehicleDetailResponse>> Get(Guid assetId, CancellationToken cancellationToken)
    {
        var row = await LoadRowAsync(assetId, cancellationToken);
        var fuel = await dbContext.FuelLogs.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.AssetId == assetId)
            .OrderByDescending(x => x.FilledAt)
            .Take(50)
            .Select(x => new FuelLogResponse(x.Id, x.FilledAt, x.Litres, x.Cost, x.OdometerKm, x.FullTank, x.Station, x.Notes, x.RecordedByName))
            .ToListAsync(cancellationToken);
        var profile = await dbContext.VehicleProfiles.AsNoTracking().SingleAsync(x => x.TenantId == TenantId && x.AssetId == assetId, cancellationToken);
        return Ok(new FleetVehicleDetailResponse(row, profile.InsuranceExpiresOn, profile.InspectionExpiresOn, profile.SpeedGovernorExpiresOn, fuel));
    }

    [HttpPost("api/fleet/vehicles")]
    public async Task<ActionResult<FleetVehicleRow>> Create([FromBody] SaveVehicleRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageAssets);
        if (request.OrgNodeId is { } node)
            await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, node, cancellationToken);
        var profile = await fleetService.CreateAsync(TenantId, request.ToSave(), new FactoryActor(UserId, UserDisplayName), cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "fleet.vehicle.created", nameof(VehicleProfile), profile.Id.ToString(), $"Added vehicle {profile.RegistrationNumber}.", cancellationToken);
        return Ok(await LoadRowAsync(profile.AssetId, cancellationToken));
    }

    [HttpPut("api/fleet/vehicles/{assetId:guid}")]
    public async Task<ActionResult<FleetVehicleRow>> Update(Guid assetId, [FromBody] SaveVehicleRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageAssets);
        await LoadRowAsync(assetId, cancellationToken);
        await fleetService.UpdateAsync(TenantId, assetId, request.ToSave(), cancellationToken);
        return Ok(await LoadRowAsync(assetId, cancellationToken));
    }

    /// <summary>Drivers and fleet clerks log fill-ups, so completing work is enough permission.</summary>
    [HttpPost("api/fleet/vehicles/{assetId:guid}/fuel")]
    public async Task<ActionResult<FuelLogResponse>> AddFuel(Guid assetId, [FromBody] AddFuelRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageAssets, PermissionNames.CanCompleteWorkOrders);
        await LoadRowAsync(assetId, cancellationToken);
        var log = await fleetService.AddFuelAsync(TenantId, assetId,
            new NewFuelLog(request.FilledAt, request.Litres, request.Cost, request.OdometerKm, request.FullTank, request.Station, request.Notes),
            new FactoryActor(UserId, UserDisplayName), cancellationToken);
        return Ok(new FuelLogResponse(log.Id, log.FilledAt, log.Litres, log.Cost, log.OdometerKm, log.FullTank, log.Station, log.Notes, log.RecordedByName));
    }

    [HttpGet("api/fleet/telematics")]
    public async Task<ActionResult<TelematicsConnectionResponse?>> GetTelematics(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        var connection = await dbContext.TelematicsConnections.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken);
        return Ok(connection is null ? null : Map(connection));
    }

    /// <summary>Saves the tracking platform link. A blank password keeps the stored one.</summary>
    [HttpPut("api/fleet/telematics")]
    public async Task<ActionResult<TelematicsConnectionResponse>> SaveTelematics([FromBody] SaveTelematicsRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        var provider = TelematicsProviders.Find(request.Provider)
            ?? throw new BusinessRuleException($"Tracking platform must be one of: {string.Join(", ", TelematicsProviders.All)}. Other platforms can push positions with a sensor gateway key.");
        if (!Uri.TryCreate(request.BaseUrl?.Trim(), UriKind.Absolute, out var url) || url.Scheme is not ("https" or "http"))
            throw new BusinessRuleException("Enter the platform's address, e.g. https://tracking.example.co.ke.");

        var connection = await dbContext.TelematicsConnections.SingleOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken);
        if (connection is null)
        {
            connection = new TelematicsConnection { TenantId = TenantId };
            dbContext.TelematicsConnections.Add(connection);
        }

        connection.Provider = provider;
        connection.BaseUrl = url.GetLeftPart(UriPartial.Path).TrimEnd('/');
        connection.Username = string.IsNullOrWhiteSpace(request.Username) ? null : request.Username.Trim();
        if (!string.IsNullOrEmpty(request.Password))
            connection.EncryptedPassword = secretEncryptionService.Encrypt(request.Password);
        connection.IsEnabled = request.IsEnabled;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "fleet.telematics.saved", nameof(TelematicsConnection), connection.Id.ToString(),
            $"{provider} link {(connection.IsEnabled ? "on" : "off")} ({connection.BaseUrl}).", cancellationToken);
        return Ok(Map(connection));
    }

    [HttpPost("api/fleet/telematics/sync")]
    public async Task<ActionResult<TelematicsSyncResult>> SyncNow([FromServices] ITelematicsSync telematicsSync, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        var connection = await dbContext.TelematicsConnections.SingleOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken)
            ?? throw new BusinessRuleException("Link a tracking platform first.");
        return Ok(await telematicsSync.SyncAsync(connection, cancellationToken));
    }

    private async Task<FleetVehicleRow> LoadRowAsync(Guid assetId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var scope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        var rows = await fleetService.ListAsync(TenantId, scope.Nodes, cancellationToken);
        return rows.SingleOrDefault(x => x.AssetId == assetId) ?? throw new NotFoundException("Vehicle was not found.");
    }

    private static TelematicsConnectionResponse Map(TelematicsConnection x) =>
        new(x.Provider, x.BaseUrl, x.Username, x.EncryptedPassword is not null, x.IsEnabled, x.LastSyncAt, x.LastSyncVehicles, x.LastSyncError);
}

/// <summary>Trackers on any platform push positions here with a sensor gateway's key (X-Sensor-Key).</summary>
[ApiController]
[AllowAnonymous]
[EnableRateLimiting(SensorsController.SensorFeedRateLimitPolicy)]
public sealed class FleetPositionsController(ISensorFeed sensorFeed, IFleetService fleetService, AppDbContext dbContext) : ControllerBase
{
    [HttpPost("api/fleet/positions")]
    public async Task<ActionResult<PositionResult>> Post([FromBody] VehiclePositionsRequest request, CancellationToken cancellationToken)
    {
        var gateway = await sensorFeed.AuthenticateAsync(SensorsController.KeyFrom(Request), cancellationToken);
        if (gateway is null)
            return Unauthorized(new { message = "Missing, wrong or disabled sensor key.", errorCode = "SENSOR_KEY_INVALID" });

        var positions = (request.Positions ?? [])
            .Select(x => new VehiclePosition(x.DeviceId ?? string.Empty, x.At ?? DateTime.UtcNow, x.Latitude, x.Longitude, x.SpeedKph, x.OdometerKm, x.EngineHours, x.Registration))
            .ToList();
        var result = await fleetService.ApplyPositionsAsync(gateway.TenantId, positions, gateway.Name, cancellationToken);
        gateway.LastSeenAt = DateTime.UtcNow;
        gateway.AcceptedCount += result.Readings;
        if (result.UnknownDevices.Count > 0)
        {
            gateway.LastError = $"Not matched to a vehicle: {string.Join(", ", result.UnknownDevices.Take(5))}";
            gateway.LastErrorAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(result);
    }
}

public sealed record SaveVehicleRequest(
    [StringLength(20)] string RegistrationNumber,
    [StringLength(20)] string VehicleType,
    Guid? OrgNodeId = null,
    [StringLength(100)] string? Make = null,
    [StringLength(100)] string? Model = null,
    int? Year = null,
    decimal? PayloadKg = null,
    [StringLength(20)] string? FuelType = null,
    [StringLength(500)] string? Route = null,
    [StringLength(150)] string? DriverName = null,
    [StringLength(30)] string? DriverPhone = null,
    DateTime? InsuranceExpiresOn = null,
    DateTime? InspectionExpiresOn = null,
    DateTime? SpeedGovernorExpiresOn = null,
    [StringLength(100)] string? TrackerDeviceId = null,
    decimal? OdometerKm = null,
    bool TrackEngineHours = false,
    decimal? EngineHours = null)
{
    public SaveVehicle ToSave() => new(RegistrationNumber, VehicleType, OrgNodeId, Make, Model, Year, PayloadKg, FuelType, Route, DriverName, DriverPhone,
        InsuranceExpiresOn, InspectionExpiresOn, SpeedGovernorExpiresOn, TrackerDeviceId, OdometerKm, TrackEngineHours, EngineHours);
}

public sealed record AddFuelRequest(
    DateTime? FilledAt,
    decimal Litres,
    decimal? Cost = null,
    decimal? OdometerKm = null,
    bool FullTank = true,
    [StringLength(150)] string? Station = null,
    [StringLength(1000)] string? Notes = null);

public sealed record FuelLogResponse(Guid Id, DateTime FilledAt, decimal Litres, decimal? Cost, decimal? OdometerKm, bool FullTank, string? Station, string? Notes, string? RecordedByName);

public sealed record FleetVehicleDetailResponse(
    FleetVehicleRow Vehicle,
    DateTime? InsuranceExpiresOn,
    DateTime? InspectionExpiresOn,
    DateTime? SpeedGovernorExpiresOn,
    IReadOnlyList<FuelLogResponse> FuelLogs);

public sealed record SaveTelematicsRequest(
    [StringLength(30)] string? Provider,
    [StringLength(500)] string? BaseUrl,
    [StringLength(200)] string? Username,
    [StringLength(500)] string? Password,
    bool IsEnabled = true);

public sealed record TelematicsConnectionResponse(
    string Provider,
    string BaseUrl,
    string? Username,
    bool HasPassword,
    bool IsEnabled,
    DateTime? LastSyncAt,
    int LastSyncVehicles,
    string? LastSyncError);

/// <param name="DeviceId">The tracker's id as fitted on the vehicle; or leave it out and give the registration.</param>
public sealed record VehiclePositionInput(
    [StringLength(100)] string? DeviceId,
    [StringLength(20)] string? Registration,
    DateTime? At,
    double? Latitude,
    double? Longitude,
    decimal? SpeedKph,
    decimal? OdometerKm,
    decimal? EngineHours);

public sealed record VehiclePositionsRequest(IReadOnlyList<VehiclePositionInput>? Positions);
