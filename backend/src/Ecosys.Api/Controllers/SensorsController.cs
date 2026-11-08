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
/// The HTTP sensor feed. A gateway posts readings by tag with its key in the X-Sensor-Key header
/// (or "Authorization: Bearer"); there is no user session.
/// </summary>
[ApiController]
[AllowAnonymous]
[EnableRateLimiting(SensorFeedRateLimitPolicy)]
public sealed class SensorsController(ISensorFeed sensorFeed) : ControllerBase
{
    public const string SensorFeedRateLimitPolicy = "SensorFeed";
    public const string KeyHeader = "X-Sensor-Key";

    [HttpPost("api/sensors/readings")]
    public async Task<ActionResult<SensorIngestResult>> Post([FromBody] SensorReadingsRequest request, CancellationToken cancellationToken)
    {
        var gateway = await sensorFeed.AuthenticateAsync(KeyFrom(Request), cancellationToken);
        if (gateway is null)
        {
            return Unauthorized(new { message = "Missing, wrong or disabled sensor key.", errorCode = "SENSOR_KEY_INVALID" });
        }

        var samples = (request.Readings ?? []).Select(x => new SensorSample(x.Tag ?? string.Empty, x.Value, x.At)).ToList();
        return Ok(await sensorFeed.IngestAsync(gateway, samples, cancellationToken));
    }

    internal static string? KeyFrom(HttpRequest request)
    {
        if (request.Headers.TryGetValue(KeyHeader, out var key) && !string.IsNullOrWhiteSpace(key))
            return key.ToString();
        var authorization = request.Headers.Authorization.ToString();
        return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? authorization[7..] : null;
    }
}

/// <summary>Sensor gateways: the systems allowed to send readings, their keys and how they are doing.</summary>
[ApiController]
[Authorize]
public sealed class SensorGatewaysController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet("api/sensor-gateways")]
    public async Task<ActionResult<IReadOnlyCollection<SensorGatewayResponse>>> List(CancellationToken cancellationToken)
    {
        EnsureAccess();
        var gateways = await dbContext.SensorGateways.AsNoTracking().Include(x => x.OrgNode)
            .Where(x => x.TenantId == TenantId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return Ok(gateways.Select(Map).ToList());
    }

    /// <summary>Creates a gateway and returns its key. The key is shown this once; only its hash is kept.</summary>
    [HttpPost("api/sensor-gateways")]
    public async Task<ActionResult<SensorGatewayCreatedResponse>> Create([FromBody] SaveSensorGatewayRequest request, CancellationToken cancellationToken)
    {
        EnsureAccess();
        var gateway = new SensorGateway { TenantId = TenantId };
        await ApplyAsync(gateway, request, cancellationToken);
        var (key, prefix, hash) = SensorKeys.Generate();
        gateway.KeyPrefix = prefix;
        gateway.KeyHash = hash;
        dbContext.SensorGateways.Add(gateway);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "sensor-gateway.created", nameof(SensorGateway), gateway.Id.ToString(), $"Added sensor gateway '{gateway.Name}'.", cancellationToken);
        return Ok(new SensorGatewayCreatedResponse(Map(gateway), key));
    }

    [HttpPut("api/sensor-gateways/{id:guid}")]
    public async Task<ActionResult<SensorGatewayResponse>> Update(Guid id, [FromBody] SaveSensorGatewayRequest request, CancellationToken cancellationToken)
    {
        EnsureAccess();
        var gateway = await LoadAsync(id, cancellationToken);
        await ApplyAsync(gateway, request, cancellationToken);
        if (request.IsActive is { } active && active != gateway.IsActive)
        {
            gateway.IsActive = active;
            await auditLogService.LogAsync(TenantId, UserId, active ? "sensor-gateway.enabled" : "sensor-gateway.disabled", nameof(SensorGateway), id.ToString(),
                $"{(active ? "Enabled" : "Disabled")} sensor gateway '{gateway.Name}'.", cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Map(gateway));
    }

    /// <summary>A new key; the old one stops working at once.</summary>
    [HttpPost("api/sensor-gateways/{id:guid}/rotate-key")]
    public async Task<ActionResult<SensorGatewayCreatedResponse>> RotateKey(Guid id, CancellationToken cancellationToken)
    {
        EnsureAccess();
        var gateway = await LoadAsync(id, cancellationToken);
        var (key, prefix, hash) = SensorKeys.Generate();
        gateway.KeyPrefix = prefix;
        gateway.KeyHash = hash;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "sensor-gateway.key-rotated", nameof(SensorGateway), id.ToString(), $"New key for sensor gateway '{gateway.Name}'.", cancellationToken);
        return Ok(new SensorGatewayCreatedResponse(Map(gateway), key));
    }

    /// <summary>The meters a gateway can feed: active meters with a tag, at its location.</summary>
    [HttpGet("api/sensor-gateways/{id:guid}/meters")]
    public async Task<ActionResult<IReadOnlyCollection<SensorMeterResponse>>> Meters(Guid id, CancellationToken cancellationToken, [FromServices] IOrgScopeService orgScopeService)
    {
        EnsureAccess();
        var gateway = await LoadAsync(id, cancellationToken);
        IReadOnlySet<Guid>? nodes = gateway.OrgNodeId is { } node ? await orgScopeService.GetSubtreeAsync(TenantId, node, cancellationToken) : null;
        var meters = await dbContext.Meters.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.IsActive && x.ExternalId != null
                && (nodes == null || (x.Asset!.OrgNodeId != null && nodes.Contains(x.Asset.OrgNodeId.Value))))
            .OrderBy(x => x.ExternalId)
            .Select(x => new SensorMeterResponse(x.Id, x.ExternalId!, x.Name, x.Unit, x.AssetId, x.Asset!.AssetName, x.Asset.AssetCode, x.CurrentValue, x.LastReadingAt,
                dbContext.MeterReadings.Where(r => r.MeterId == x.Id && r.Source == MeterReadingSources.Sensor).Max(r => (DateTime?)r.ReadAt)))
            .ToListAsync(cancellationToken);
        return Ok(meters);
    }

    private async Task ApplyAsync(SensorGateway gateway, SaveSensorGatewayRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name))
            throw new BusinessRuleException("Name the gateway, e.g. \"Chelal boiler PLC\".");
        if (await dbContext.SensorGateways.AnyAsync(x => x.TenantId == TenantId && x.Id != gateway.Id && x.Name.ToLower() == name.ToLower(), cancellationToken))
            throw new ConflictException($"There is already a gateway called '{name}'.");
        if (request.SampleSeconds is < 0 or > 86400)
            throw new BusinessRuleException("The sample interval must be between 0 seconds (keep every reading) and a day.");
        if (request.OrgNodeId is { } node && !await dbContext.OrgNodes.AnyAsync(x => x.TenantId == TenantId && x.Id == node, cancellationToken))
            throw new BusinessRuleException("Location was not found.");

        gateway.Name = name;
        gateway.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        gateway.OrgNodeId = request.OrgNodeId;
        gateway.SampleSeconds = request.SampleSeconds ?? gateway.SampleSeconds;
        gateway.MqttTrustBroker = request.MqttTrustBroker ?? gateway.MqttTrustBroker;
    }

    private async Task<SensorGateway> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.SensorGateways.Include(x => x.OrgNode).SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Sensor gateway was not found.");

    private void EnsureAccess() => userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);

    private static SensorGatewayResponse Map(SensorGateway x) => new(
        x.Id, x.Name, x.Description, x.OrgNodeId, x.OrgNode?.Name, x.KeyPrefix, x.IsActive, x.SampleSeconds, x.MqttTrustBroker,
        x.LastSeenAt, x.AcceptedCount, x.RejectedCount, x.LastError, x.LastErrorAt, x.CreatedAt);
}

public sealed record SensorReadingInput([StringLength(100)] string? Tag, decimal Value, DateTime? At);

public sealed record SensorReadingsRequest(IReadOnlyList<SensorReadingInput>? Readings);

public sealed record SaveSensorGatewayRequest(
    [StringLength(150)] string? Name,
    [StringLength(1000)] string? Description = null,
    Guid? OrgNodeId = null,
    int? SampleSeconds = null,
    bool? MqttTrustBroker = null,
    bool? IsActive = null);

public sealed record SensorGatewayResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid? OrgNodeId,
    string? OrgNodeName,
    string KeyPrefix,
    bool IsActive,
    int SampleSeconds,
    bool MqttTrustBroker,
    DateTime? LastSeenAt,
    long AcceptedCount,
    long RejectedCount,
    string? LastError,
    DateTime? LastErrorAt,
    DateTime CreatedAt);

/// <param name="Key">The full key, shown only now.</param>
public sealed record SensorGatewayCreatedResponse(SensorGatewayResponse Gateway, string Key);

public sealed record SensorMeterResponse(
    Guid MeterId,
    string Tag,
    string MeterName,
    string Unit,
    Guid AssetId,
    string AssetName,
    string AssetCode,
    decimal? CurrentValue,
    DateTime? LastReadingAt,
    DateTime? LastSensorReadingAt);
