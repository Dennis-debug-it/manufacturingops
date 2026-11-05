using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>The basic factory dashboard (scope Phase 2): jobs, breakdowns, failures and running-check completion.</summary>
[ApiController]
[Authorize]
[Route("api/factory/dashboard")]
public sealed class FactoryDashboardController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IOrgScopeService orgScopeService,
    IFactorySettingsService factorySettingsService) : TenantAwareControllerBase(tenantContext)
{
    private static readonly string[] Done =
        [WorkOrderStatuses.Completed, WorkOrderStatuses.Verified, WorkOrderStatuses.Closed, WorkOrderStatuses.Cancelled, WorkOrderStatuses.Rejected];

    [HttpGet]
    public async Task<ActionResult<FactoryDashboardResponse>> Get([FromQuery] Guid? orgNodeId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanViewWorkOrders, PermissionNames.CanAssignWorkOrders, PermissionNames.CanViewReports);
        var now = DateTime.UtcNow;
        var nodes = await NodesAsync(orgNodeId, cancellationToken);
        var settings = await factorySettingsService.GetOrCreateAsync(TenantId, cancellationToken);

        var jobs = dbContext.WorkOrders.Where(x => x.TenantId == TenantId && x.ClientId == null && x.OrgNodeId != null);
        var assets = dbContext.Assets.Where(x => x.TenantId == TenantId && x.Status != "Inactive" && x.OrgNodeId != null);
        var runs = dbContext.CheckRuns.Where(x => x.TenantId == TenantId);
        if (nodes is not null)
        {
            jobs = jobs.Where(x => nodes.Contains(x.OrgNodeId!.Value));
            assets = assets.Where(x => nodes.Contains(x.OrgNodeId!.Value));
            runs = runs.Where(x => nodes.Contains(x.OrgNodeId));
        }

        var open = jobs.Where(x => !Done.Contains(x.Status));
        var counts = new FactoryJobCounts(
            await open.CountAsync(cancellationToken),
            await open.CountAsync(x => x.DueDate != null && x.DueDate < now, cancellationToken),
            await open.CountAsync(x => x.Status == WorkOrderStatuses.Requested, cancellationToken),
            await jobs.CountAsync(x => x.Status == WorkOrderStatuses.Completed, cancellationToken),
            await open.CountAsync(x => x.Status == WorkOrderStatuses.OnHold || x.Status == WorkOrderStatuses.WaitingOnParts, cancellationToken),
            await open.CountAsync(x => x.NeedsLineStop, cancellationToken));

        // Downtime: every breakdown overlapping the last 7 days, clipped to the window.
        var weekAgo = now.AddDays(-7);
        var breakdowns = await jobs
            .Where(x => x.IsBreakdown && x.DowntimeStartedAt != null && (x.DowntimeEndedAt == null || x.DowntimeEndedAt > weekAgo))
            .Select(x => new { x.Id, x.WorkOrderNumber, x.Title, x.AssetId, AssetCode = x.Asset!.AssetCode, AssetName = x.Asset.AssetName, x.Asset.LostProductionPerHour, x.DowntimeStartedAt, x.DowntimeEndedAt })
            .ToListAsync(cancellationToken);
        decimal Hours(DateTime start, DateTime? end) => (decimal)((end ?? now) - (start < weekAgo ? weekAgo : start)).TotalHours;
        var downtimeHours = breakdowns.Sum(x => Hours(x.DowntimeStartedAt!.Value, x.DowntimeEndedAt));
        var lostProduction = breakdowns.Sum(x => Hours(x.DowntimeStartedAt!.Value, x.DowntimeEndedAt) * (x.LostProductionPerHour ?? 0));
        var openBreakdowns = breakdowns.Where(x => x.DowntimeEndedAt == null)
            .OrderBy(x => x.DowntimeStartedAt)
            .Select(x => new FactoryBreakdown(x.Id, x.WorkOrderNumber, x.Title, x.AssetCode, x.AssetName, x.DowntimeStartedAt!.Value, Math.Round((decimal)(now - x.DowntimeStartedAt!.Value).TotalHours, 1)))
            .ToList();

        // Reliability over the last 90 days: corrective work by asset and by failure mode.
        var quarterAgo = now.AddDays(-90);
        var corrective = jobs.Where(x => x.CreatedAt >= quarterAgo && x.AssetId != null && (x.WorkType == FactoryWorkTypes.Corrective || x.IsBreakdown) && x.Status != WorkOrderStatuses.Cancelled && x.Status != WorkOrderStatuses.Rejected);
        // Grouped in memory: three months of corrective jobs is a small set, and it keeps the query simple for every provider.
        var failures = await corrective
            .Select(x => new { AssetId = x.AssetId!.Value, x.Asset!.AssetCode, x.Asset.AssetName, x.IsBreakdown, Rework = x.ReworkOfWorkOrderId != null,
                ProblemCode = x.FailureProblemCode != null ? x.FailureProblemCode.Code : null, ProblemName = x.FailureProblemCode != null ? x.FailureProblemCode.Name : null })
            .ToListAsync(cancellationToken);
        var topAssets = failures
            .GroupBy(x => new { x.AssetId, x.AssetCode, x.AssetName })
            .Select(g => new FactoryFailingAsset(g.Key.AssetId, g.Key.AssetCode, g.Key.AssetName, g.Count(), g.Count(x => x.IsBreakdown), g.Count(x => x.Rework)))
            .OrderByDescending(x => x.Jobs).ThenByDescending(x => x.Breakdowns)
            .Take(5)
            .ToList();
        var topProblems = failures.Where(x => x.ProblemCode is not null)
            .GroupBy(x => new { x.ProblemCode, x.ProblemName })
            .Select(g => new FactoryFailureMode(g.Key.ProblemCode!, g.Key.ProblemName!, g.Count()))
            .OrderByDescending(x => x.Jobs)
            .Take(5)
            .ToList();

        var monthAgo = now.AddDays(-30);
        var recent = jobs.Where(x => x.CreatedAt >= monthAgo && x.Status != WorkOrderStatuses.Cancelled && x.Status != WorkOrderStatuses.Rejected);
        var reactive = await recent.CountAsync(x => x.WorkType == FactoryWorkTypes.Corrective || x.IsBreakdown, cancellationToken);
        var planned = await recent.CountAsync(x => x.WorkType != FactoryWorkTypes.Corrective && !x.IsBreakdown, cancellationToken);

        // Running checks over the last 24 hours.
        var dayAgo = now.AddHours(-24);
        var recentRuns = runs.Where(x => x.DueFrom >= dayAgo && x.DueFrom <= now);
        var checks = new FactoryCheckCompletion(
            await recentRuns.CountAsync(cancellationToken),
            await recentRuns.CountAsync(x => x.Status == CheckRunStatuses.Completed, cancellationToken),
            await recentRuns.CountAsync(x => x.Status == CheckRunStatuses.Missed, cancellationToken),
            await recentRuns.CountAsync(x => x.Status == CheckRunStatuses.Due, cancellationToken),
            await recentRuns.SumAsync(x => x.FailedCount, cancellationToken));

        return Ok(new FactoryDashboardResponse(
            counts,
            await assets.CountAsync(cancellationToken),
            await assets.CountAsync(x => x.OperationalStatus == AssetOperationalStatuses.Down, cancellationToken),
            openBreakdowns,
            Math.Round(downtimeHours, 1),
            Math.Round(lostProduction, 0),
            settings.ProductionUnit,
            topAssets,
            topProblems,
            reactive,
            planned,
            checks));
    }

    /// <summary>The org nodes to count: the chosen subtree within the user's scope, or null for everything.</summary>
    private async Task<List<Guid>?> NodesAsync(Guid? orgNodeId, CancellationToken cancellationToken)
    {
        var scope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        if (orgNodeId is { } id)
        {
            var subtree = await orgScopeService.GetSubtreeAsync(TenantId, id, cancellationToken);
            return subtree.Where(x => scope.Allows(x)).ToList();
        }

        return scope.Nodes?.ToList();
    }
}

public sealed record FactoryJobCounts(int Open, int Overdue, int AwaitingApproval, int AwaitingVerification, int OnHold, int NeedLineStop);
public sealed record FactoryBreakdown(Guid WorkOrderId, string WorkOrderNumber, string Title, string AssetCode, string AssetName, DateTime DownSince, decimal HoursDown);
public sealed record FactoryFailingAsset(Guid AssetId, string AssetCode, string AssetName, int Jobs, int Breakdowns, int Rework);
public sealed record FactoryFailureMode(string Code, string Name, int Jobs);
public sealed record FactoryCheckCompletion(int Due, int Completed, int Missed, int Outstanding, int FailedItems);

public sealed record FactoryDashboardResponse(
    FactoryJobCounts Jobs,
    int Assets,
    int AssetsDown,
    IReadOnlyCollection<FactoryBreakdown> OpenBreakdowns,
    decimal DowntimeHoursLast7Days,
    decimal ProductionLostLast7Days,
    string ProductionUnit,
    IReadOnlyCollection<FactoryFailingAsset> TopFailingAssets,
    IReadOnlyCollection<FactoryFailureMode> TopFailureModes,
    int ReactiveJobsLast30Days,
    int PlannedJobsLast30Days,
    FactoryCheckCompletion RunningChecksLast24Hours);
