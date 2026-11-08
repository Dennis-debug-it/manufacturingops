using System.ComponentModel.DataAnnotations;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>Meters on assets and their readings (run hours, kg made tea, odometer, temperature, vibration, pressure…).</summary>
[ApiController]
[Authorize]
public sealed class MetersController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IOrgScopeService orgScopeService,
    IFactorySettingsService factorySettingsService,
    IMeterReadingService meterReadingService,
    IConditionMonitor conditionMonitor,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    private const int MaxReadingsPage = 1000;

    [HttpGet("api/meter-types")]
    public async Task<ActionResult<IReadOnlyCollection<MeterTypeResponse>>> Types(CancellationToken cancellationToken)
    {
        var settings = await factorySettingsService.GetOrCreateAsync(TenantId, cancellationToken);
        return Ok(MeterTypes.All
            .Select(x => new MeterTypeResponse(x, MeterTypes.DefaultUnit(x, settings.ProductionUnit), MeterTypes.IsCumulativeByDefault(x)))
            .ToList());
    }

    [HttpGet("api/assets/{assetId:guid}/meters")]
    public async Task<ActionResult<IReadOnlyCollection<MeterResponse>>> List(Guid assetId, [FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        await LoadAssetAsync(assetId, cancellationToken);
        var meters = await dbContext.Meters
            .Where(x => x.TenantId == TenantId && x.AssetId == assetId && (includeInactive || x.IsActive))
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return Ok(meters.Select(Map).ToList());
    }

    [HttpPost("api/assets/{assetId:guid}/meters")]
    public async Task<ActionResult<MeterResponse>> Create(Guid assetId, [FromBody] UpsertMeterRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageAssets);
        var asset = await LoadAssetAsync(assetId, cancellationToken);
        var type = MeterTypes.Find(request.MeterType)
            ?? throw new BusinessRuleException($"Meter type must be one of: {string.Join(", ", MeterTypes.All)}.");
        var settings = await factorySettingsService.GetOrCreateAsync(TenantId, cancellationToken);

        var meter = new Meter
        {
            TenantId = TenantId,
            AssetId = asset.Id,
            Asset = asset,
            MeterType = type,
            IsCumulative = request.IsCumulative ?? MeterTypes.IsCumulativeByDefault(type),
            Unit = string.IsNullOrWhiteSpace(request.Unit) ? MeterTypes.DefaultUnit(type, settings.ProductionUnit) : request.Unit.Trim()
        };
        await ApplyAsync(meter, request, cancellationToken);
        dbContext.Meters.Add(meter);

        MeterReading? initial = null;
        if (request.InitialValue.HasValue)
        {
            initial = await meterReadingService.RecordAsync(meter, new NewMeterReading(request.InitialValue.Value, null, Notes: "Initial reading", UserId: UserId, UserName: UserDisplayName), cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (initial is not null)
        {
            await conditionMonitor.EvaluateAsync(meter, initial, new FactoryActor(UserId, UserDisplayName), cancellationToken);
        }

        await auditLogService.LogAsync(TenantId, UserId, "meter.created", nameof(Meter), meter.Id.ToString(),
            $"Added {meter.MeterType} meter '{meter.Name}' to asset '{asset.AssetName}'.", cancellationToken);

        return CreatedAtAction(nameof(Readings), new { id = meter.Id }, Map(meter));
    }

    [HttpPut("api/meters/{id:guid}")]
    public async Task<ActionResult<MeterResponse>> Update(Guid id, [FromBody] UpsertMeterRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageAssets);
        var meter = await LoadMeterAsync(id, cancellationToken);
        if (MeterTypes.Find(request.MeterType) is { } type && type != meter.MeterType)
        {
            if (await dbContext.MeterReadings.AnyAsync(x => x.MeterId == id, cancellationToken))
            {
                throw new BusinessRuleException("A meter's type can't change once it has readings. Add a new meter instead.");
            }

            meter.MeterType = type;
            meter.IsCumulative = request.IsCumulative ?? MeterTypes.IsCumulativeByDefault(type);
        }

        if (!string.IsNullOrWhiteSpace(request.Unit))
        {
            meter.Unit = request.Unit.Trim();
        }

        await ApplyAsync(meter, request, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Map(meter));
    }

    [HttpPost("api/meters/{id:guid}/deactivate")]
    public async Task<ActionResult<MeterResponse>> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageAssets);
        var meter = await LoadMeterAsync(id, cancellationToken);
        meter.IsActive = false;
        meter.IsPrimary = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "meter.deactivated", nameof(Meter), meter.Id.ToString(),
            $"Deactivated meter '{meter.Name}'.", cancellationToken);
        return Ok(Map(meter));
    }

    [HttpGet("api/meters/{id:guid}/readings")]
    public async Task<ActionResult<IReadOnlyCollection<MeterReadingResponse>>> Readings(
        Guid id,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        await LoadMeterAsync(id, cancellationToken);
        var take = Math.Clamp(limit ?? 200, 1, MaxReadingsPage);
        var query = dbContext.MeterReadings.Where(x => x.TenantId == TenantId && x.MeterId == id);
        if (from.HasValue)
        {
            query = query.Where(x => x.ReadAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.ReadAt <= to.Value);
        }

        var readings = await query.OrderByDescending(x => x.ReadAt).Take(take).ToListAsync(cancellationToken);
        return Ok(readings.Select(Map).ToList());
    }

    /// <summary>Technicians and operators record readings, so completing work is enough permission.</summary>
    [HttpPost("api/meters/{id:guid}/readings")]
    public async Task<ActionResult<MeterReadingResponse>> Record(Guid id, [FromBody] RecordMeterReadingRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageAssets, PermissionNames.CanCompleteWorkOrders);
        var meter = await LoadMeterAsync(id, cancellationToken);
        var reading = await meterReadingService.RecordAsync(meter, new NewMeterReading(
            request.Value,
            request.ReadAt,
            request.Source ?? MeterReadingSources.Manual,
            request.IsReset,
            request.Notes,
            UserId,
            UserDisplayName), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await conditionMonitor.EvaluateAsync(meter, reading, new FactoryActor(UserId, UserDisplayName), cancellationToken);
        return Ok(Map(reading));
    }

    private async Task ApplyAsync(Meter meter, UpsertMeterRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessRuleException("Meter name is required.");
        }

        if (request.MinThreshold.HasValue && request.MaxThreshold.HasValue && request.MinThreshold > request.MaxThreshold)
        {
            throw new BusinessRuleException("The low limit must be below the high limit.");
        }

        if (request.WarningMin.HasValue && request.WarningMax.HasValue && request.WarningMin > request.WarningMax)
        {
            throw new BusinessRuleException("The low warning limit must be below the high warning limit.");
        }

        if ((request.WarningMin.HasValue && request.MinThreshold.HasValue && request.WarningMin < request.MinThreshold)
            || (request.WarningMax.HasValue && request.MaxThreshold.HasValue && request.WarningMax > request.MaxThreshold))
        {
            throw new BusinessRuleException("The warning band must sit inside the alarm limits.");
        }

        var action = request.ConditionAction is null ? meter.ConditionAction : ConditionActions.Find(request.ConditionAction)
            ?? throw new BusinessRuleException($"Alarm action must be one of: {string.Join(", ", ConditionActions.All)}.");
        var priority = request.AlarmPriority is null ? meter.AlarmPriority : FactoryPriorities.Find(request.AlarmPriority)
            ?? throw new BusinessRuleException($"Alarm job priority must be one of: {string.Join(", ", FactoryPriorities.All)}.");
        if (request.TriggerAfterReadings is < 1 or > 50)
        {
            throw new BusinessRuleException("Readings in a row before an alert must be between 1 and 50.");
        }

        if (request.AlarmJobPlanId is { } planId && !await dbContext.JobPlans.AnyAsync(x => x.TenantId == TenantId && x.Id == planId, cancellationToken))
        {
            throw new BusinessRuleException("Job plan was not found.");
        }

        var name = request.Name.Trim();
        if (await dbContext.Meters.AnyAsync(x => x.TenantId == TenantId && x.AssetId == meter.AssetId && x.Id != meter.Id && x.Name.ToLower() == name.ToLower(), cancellationToken))
        {
            throw new ConflictException($"This asset already has a meter called '{name}'.");
        }

        meter.Name = name;
        meter.MinThreshold = request.MinThreshold;
        meter.MaxThreshold = request.MaxThreshold;
        meter.WarningMin = request.WarningMin;
        meter.WarningMax = request.WarningMax;
        meter.ConditionAction = action;
        meter.AlarmPriority = priority;
        meter.TriggerAfterReadings = request.TriggerAfterReadings ?? meter.TriggerAfterReadings;
        meter.AlarmJobPlanId = request.AlarmJobPlanId;
        meter.ExternalId = string.IsNullOrWhiteSpace(request.ExternalId) ? null : request.ExternalId.Trim();
        meter.ExternalSystem = string.IsNullOrWhiteSpace(request.ExternalSystem) ? null : request.ExternalSystem.Trim();

        if (request.IsPrimary == true && !meter.IsPrimary)
        {
            if (!meter.IsCumulative)
            {
                throw new BusinessRuleException("Only a counter (run hours, production, odometer, energy) can be the asset's main meter.");
            }

            var others = await dbContext.Meters.Where(x => x.TenantId == TenantId && x.AssetId == meter.AssetId && x.IsPrimary && x.Id != meter.Id).ToListAsync(cancellationToken);
            others.ForEach(x => x.IsPrimary = false);
            meter.IsPrimary = true;
        }
        else if (request.IsPrimary == false)
        {
            meter.IsPrimary = false;
        }
    }

    private async Task<Asset> LoadAssetAsync(Guid assetId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var asset = await dbContext.Assets.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == assetId, cancellationToken)
            ?? throw new NotFoundException("Asset was not found.");
        var scope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        return scope.Allows(asset.OrgNodeId) ? asset : throw new NotFoundException("Asset was not found.");
    }

    private async Task<Meter> LoadMeterAsync(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var meter = await dbContext.Meters.Include(x => x.Asset).SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Meter was not found.");
        var scope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        return scope.Allows(meter.Asset?.OrgNodeId) ? meter : throw new NotFoundException("Meter was not found.");
    }

    private static MeterResponse Map(Meter meter) => new(
        meter.Id,
        meter.AssetId,
        meter.Name,
        meter.MeterType,
        meter.Unit,
        meter.IsCumulative,
        meter.IsPrimary,
        meter.IsActive,
        meter.CurrentValue,
        meter.LastReadingAt,
        meter.MinThreshold,
        meter.MaxThreshold,
        meter.ExternalId,
        meter.ExternalSystem,
        meter.WarningMin,
        meter.WarningMax,
        meter.ConditionAction,
        meter.TriggerAfterReadings,
        meter.AlarmPriority,
        meter.AlarmJobPlanId,
        meter.ConditionState,
        meter.ConditionSince);

    private static MeterReadingResponse Map(MeterReading reading) => new(
        reading.Id,
        reading.MeterId,
        reading.Value,
        reading.Delta,
        reading.ReadAt,
        reading.Source,
        reading.IsReset,
        reading.IsOutOfRange,
        reading.Notes,
        reading.RecordedByName,
        reading.Condition);
}

public sealed record MeterTypeResponse(string MeterType, string DefaultUnit, bool IsCumulative);

public sealed record UpsertMeterRequest(
    [StringLength(150)] string Name,
    [StringLength(30)] string MeterType,
    [StringLength(30)] string? Unit = null,
    bool? IsCumulative = null,
    bool? IsPrimary = null,
    decimal? MinThreshold = null,
    decimal? MaxThreshold = null,
    decimal? InitialValue = null,
    [StringLength(100)] string? ExternalId = null,
    [StringLength(50)] string? ExternalSystem = null,
    decimal? WarningMin = null,
    decimal? WarningMax = null,
    [StringLength(20)] string? ConditionAction = null,
    int? TriggerAfterReadings = null,
    [StringLength(20)] string? AlarmPriority = null,
    Guid? AlarmJobPlanId = null);

public sealed record MeterResponse(
    Guid Id,
    Guid AssetId,
    string Name,
    string MeterType,
    string Unit,
    bool IsCumulative,
    bool IsPrimary,
    bool IsActive,
    decimal? CurrentValue,
    DateTime? LastReadingAt,
    decimal? MinThreshold,
    decimal? MaxThreshold,
    string? ExternalId,
    string? ExternalSystem,
    decimal? WarningMin,
    decimal? WarningMax,
    string ConditionAction,
    int TriggerAfterReadings,
    string AlarmPriority,
    Guid? AlarmJobPlanId,
    string ConditionState,
    DateTime? ConditionSince);

public sealed record RecordMeterReadingRequest(
    decimal Value,
    DateTime? ReadAt,
    [StringLength(20)] string? Source = null,
    bool IsReset = false,
    [StringLength(1000)] string? Notes = null);

public sealed record MeterReadingResponse(
    Guid Id,
    Guid MeterId,
    decimal Value,
    decimal? Delta,
    DateTime ReadAt,
    string Source,
    bool IsReset,
    bool IsOutOfRange,
    string? Notes,
    string? RecordedByName,
    string Condition = ConditionLevels.Normal);
