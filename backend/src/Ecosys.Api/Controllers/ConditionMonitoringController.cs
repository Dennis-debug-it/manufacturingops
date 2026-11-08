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

/// <summary>
/// Condition monitoring: every meter with limits, its condition now, its excursions (warnings and
/// alarms) and where its trend is heading.
/// </summary>
[ApiController]
[Authorize]
public sealed class ConditionMonitoringController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IOrgScopeService orgScopeService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    /// <summary>A meter without a reading for this long is shown as gone quiet.</summary>
    private static readonly TimeSpan QuietAfter = TimeSpan.FromDays(7);

    [HttpGet("api/condition-monitoring/overview")]
    public async Task<ActionResult<ConditionOverviewResponse>> Overview([FromQuery] Guid? orgNodeId, CancellationToken cancellationToken)
    {
        var nodes = await VisibleNodesAsync(orgNodeId, cancellationToken);
        var meters = await dbContext.Meters.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.IsActive
                && (x.MinThreshold != null || x.MaxThreshold != null || x.WarningMin != null || x.WarningMax != null)
                && (nodes == null || (x.Asset!.OrgNodeId != null && nodes.Contains(x.Asset.OrgNodeId.Value))))
            .Select(x => new
            {
                Meter = x,
                x.Asset!.AssetName,
                x.Asset.AssetCode,
                x.Asset.OrgNodeId,
                Location = x.Asset.OrgNode != null ? x.Asset.OrgNode.Name : null,
            })
            .ToListAsync(cancellationToken);

        var meterIds = meters.Select(x => x.Meter.Id).ToList();
        var openEvents = (await dbContext.ConditionEvents.AsNoTracking()
                .Where(x => x.TenantId == TenantId && x.Status == ConditionEventStatuses.Open && meterIds.Contains(x.MeterId))
                .Select(x => new { x.MeterId, x.Id, x.StartedAt, x.AcknowledgedAt, x.WorkOrderId, WorkOrderNumber = x.WorkOrder != null ? x.WorkOrder.WorkOrderNumber : null })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.MeterId);

        var now = DateTime.UtcNow;
        var rows = meters
            .Select(x =>
            {
                var m = x.Meter;
                openEvents.TryGetValue(m.Id, out var open);
                return new ConditionMeterRow(
                    m.Id, m.Name, m.MeterType, m.Unit, m.AssetId, x.AssetName, x.AssetCode, x.Location,
                    m.CurrentValue, m.LastReadingAt, m.ConditionState, m.ConditionSince,
                    m.WarningMin, m.WarningMax, m.MinThreshold, m.MaxThreshold, m.ConditionAction,
                    m.LastReadingAt is null || now - m.LastReadingAt > QuietAfter,
                    open?.Id, open?.StartedAt, open?.AcknowledgedAt, open?.WorkOrderId, open?.WorkOrderNumber);
            })
            .OrderByDescending(x => ConditionLevels.Rank(x.ConditionState))
            .ThenBy(x => x.AcknowledgedAt.HasValue)
            .ThenBy(x => x.AssetName)
            .ThenBy(x => x.MeterName)
            .ToList();

        return Ok(new ConditionOverviewResponse(
            rows.Count,
            rows.Count(x => x.ConditionState == ConditionLevels.Alarm),
            rows.Count(x => x.ConditionState == ConditionLevels.Warning),
            rows.Count(x => x.IsQuiet),
            rows));
    }

    [HttpGet("api/condition-monitoring/events")]
    public async Task<ActionResult<IReadOnlyCollection<ConditionEventResponse>>> Events(
        [FromQuery] string? status,
        [FromQuery] Guid? meterId,
        [FromQuery] Guid? orgNodeId,
        [FromQuery] DateTime? from,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var nodes = await VisibleNodesAsync(orgNodeId, cancellationToken);
        var query = dbContext.ConditionEvents.AsNoTracking()
            .Where(x => x.TenantId == TenantId && (nodes == null || (x.Asset!.OrgNodeId != null && nodes.Contains(x.Asset.OrgNodeId.Value))));
        if (string.Equals(status, ConditionEventStatuses.Open, StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.Status == ConditionEventStatuses.Open);
        else if (string.Equals(status, ConditionEventStatuses.Cleared, StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.Status == ConditionEventStatuses.Cleared);
        if (meterId.HasValue)
            query = query.Where(x => x.MeterId == meterId);
        if (from.HasValue)
            query = query.Where(x => x.StartedAt >= from || x.ClearedAt == null || x.ClearedAt >= from);

        var events = await query
            .OrderByDescending(x => x.StartedAt)
            .Take(Math.Clamp(limit ?? 100, 1, 500))
            .Select(x => new ConditionEventResponse(
                x.Id, x.MeterId, x.Meter!.Name, x.Meter.Unit, x.AssetId, x.Asset!.AssetName, x.Asset.AssetCode,
                x.Level, x.Direction, x.Status, x.StartedAt, x.LastReadingAt, x.ClearedAt, x.PeakValue, x.ReadingCount,
                x.WorkOrderId, x.WorkOrder != null ? x.WorkOrder.WorkOrderNumber : null, x.WorkOrder != null ? x.WorkOrder.Status : null,
                x.AcknowledgedAt, x.AcknowledgedByName, x.AcknowledgementNote))
            .ToListAsync(cancellationToken);
        return Ok(events);
    }

    /// <summary>"Seen, being dealt with": an open excursion stays open until a reading is back in range.</summary>
    [HttpPost("api/condition-monitoring/events/{id:guid}/acknowledge")]
    public async Task<ActionResult<ConditionEventResponse>> Acknowledge(Guid id, [FromBody] AcknowledgeConditionEventRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageAssets, PermissionNames.CanCompleteWorkOrders);
        var conditionEvent = await dbContext.ConditionEvents.Include(x => x.Asset)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Condition event was not found.");
        var scope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        if (!scope.Allows(conditionEvent.Asset?.OrgNodeId))
            throw new NotFoundException("Condition event was not found.");
        if (conditionEvent.AcknowledgedAt.HasValue)
            throw new BusinessRuleException($"Already acknowledged by {conditionEvent.AcknowledgedByName}.");

        conditionEvent.AcknowledgedAt = DateTime.UtcNow;
        conditionEvent.AcknowledgedByUserId = UserId;
        conditionEvent.AcknowledgedByName = UserDisplayName;
        conditionEvent.AcknowledgementNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "condition.event.acknowledged", nameof(ConditionEvent), id.ToString(),
            $"Acknowledged a {conditionEvent.Level.ToLowerInvariant()} on {conditionEvent.Asset?.AssetCode}.", cancellationToken);

        var events = await Events(null, conditionEvent.MeterId, null, null, 500, cancellationToken);
        return Ok(((IReadOnlyCollection<ConditionEventResponse>)((OkObjectResult)events.Result!).Value!).Single(x => x.Id == id));
    }

    /// <summary>A meter's readings over the last days, its excursions in that time and its trend.</summary>
    [HttpGet("api/meters/{id:guid}/condition")]
    public async Task<ActionResult<MeterConditionResponse>> MeterCondition(Guid id, [FromQuery] int? days, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var meter = await dbContext.Meters.AsNoTracking().Include(x => x.Asset)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Meter was not found.");
        var scope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        if (!scope.Allows(meter.Asset?.OrgNodeId))
            throw new NotFoundException("Meter was not found.");

        var span = Math.Clamp(days ?? 30, 1, 366);
        var since = DateTime.UtcNow.AddDays(-span);
        var readings = await dbContext.MeterReadings.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.MeterId == id && x.ReadAt >= since)
            .OrderByDescending(x => x.ReadAt)
            .Take(2000)
            .Select(x => new ConditionReadingPoint(x.ReadAt, x.Value, x.Condition, x.Source))
            .ToListAsync(cancellationToken);
        readings.Reverse();

        var events = await dbContext.ConditionEvents.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.MeterId == id && (x.ClearedAt == null || x.ClearedAt >= since))
            .OrderByDescending(x => x.StartedAt)
            .Take(50)
            .Select(x => new ConditionEventResponse(
                x.Id, x.MeterId, meter.Name, meter.Unit, x.AssetId, meter.Asset!.AssetName, meter.Asset.AssetCode,
                x.Level, x.Direction, x.Status, x.StartedAt, x.LastReadingAt, x.ClearedAt, x.PeakValue, x.ReadingCount,
                x.WorkOrderId, x.WorkOrder != null ? x.WorkOrder.WorkOrderNumber : null, x.WorkOrder != null ? x.WorkOrder.Status : null,
                x.AcknowledgedAt, x.AcknowledgedByName, x.AcknowledgementNote))
            .ToListAsync(cancellationToken);

        // The trend looks at the last two weeks at most: older drift says little about where it goes next.
        var trendSince = DateTime.UtcNow.AddDays(-Math.Min(span, 14));
        var trend = meter.IsCumulative
            ? new ConditionTrend(0, null, null, null, null, null)
            : ConditionTrends.Fit(meter, readings.Where(x => x.ReadAt >= trendSince).Select(x => (x.ReadAt, x.Value)).ToList());

        return Ok(new MeterConditionResponse(
            meter.Id, meter.Name, meter.MeterType, meter.Unit, meter.IsCumulative, meter.AssetId, meter.Asset!.AssetName, meter.Asset.AssetCode,
            meter.CurrentValue, meter.LastReadingAt, meter.ConditionState, meter.ConditionSince,
            meter.WarningMin, meter.WarningMax, meter.MinThreshold, meter.MaxThreshold,
            readings, events, trend));
    }

    /// <summary>The org nodes the caller may see under the chosen one; null when nothing narrows it.</summary>
    private async Task<IReadOnlySet<Guid>?> VisibleNodesAsync(Guid? orgNodeId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        return await orgScopeService.VisibleNodesAsync(TenantId, UserId, orgNodeId, cancellationToken);
    }
}

public sealed record ConditionMeterRow(
    Guid MeterId,
    string MeterName,
    string MeterType,
    string Unit,
    Guid AssetId,
    string AssetName,
    string AssetCode,
    string? Location,
    decimal? CurrentValue,
    DateTime? LastReadingAt,
    string ConditionState,
    DateTime? ConditionSince,
    decimal? WarningMin,
    decimal? WarningMax,
    decimal? AlarmMin,
    decimal? AlarmMax,
    string ConditionAction,
    bool IsQuiet,
    Guid? OpenEventId,
    DateTime? OpenSince,
    DateTime? AcknowledgedAt,
    Guid? WorkOrderId,
    string? WorkOrderNumber);

public sealed record ConditionOverviewResponse(int Monitored, int Alarms, int Warnings, int Quiet, IReadOnlyList<ConditionMeterRow> Meters);

public sealed record ConditionEventResponse(
    Guid Id,
    Guid MeterId,
    string MeterName,
    string Unit,
    Guid AssetId,
    string AssetName,
    string AssetCode,
    string Level,
    string Direction,
    string Status,
    DateTime StartedAt,
    DateTime LastReadingAt,
    DateTime? ClearedAt,
    decimal PeakValue,
    int ReadingCount,
    Guid? WorkOrderId,
    string? WorkOrderNumber,
    string? WorkOrderStatus,
    DateTime? AcknowledgedAt,
    string? AcknowledgedByName,
    string? AcknowledgementNote);

public sealed record AcknowledgeConditionEventRequest([StringLength(1000)] string? Note);

public sealed record ConditionReadingPoint(DateTime ReadAt, decimal Value, string Condition, string Source);

public sealed record MeterConditionResponse(
    Guid MeterId,
    string MeterName,
    string MeterType,
    string Unit,
    bool IsCumulative,
    Guid AssetId,
    string AssetName,
    string AssetCode,
    decimal? CurrentValue,
    DateTime? LastReadingAt,
    string ConditionState,
    DateTime? ConditionSince,
    decimal? WarningMin,
    decimal? WarningMax,
    decimal? AlarmMin,
    decimal? AlarmMax,
    IReadOnlyList<ConditionReadingPoint> Readings,
    IReadOnlyList<ConditionEventResponse> Events,
    ConditionTrend Trend);
