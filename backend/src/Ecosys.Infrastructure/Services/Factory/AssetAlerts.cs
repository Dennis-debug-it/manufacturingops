using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services.Factory;

/// <summary>
/// Asset alerts: warranties about to end, and meter readings outside their limits. They go to
/// asset managers (admins, plus roles given "manage assets") who can see the asset's factory.
/// </summary>
public static class AssetAlerts
{
    public const string WarrantyExpiring = "asset.warranty-expiring";
    public const string MeterOutOfRange = "asset.meter-out-of-range";

    /// <summary>How far ahead the first warranty warning goes out.</summary>
    public const int WarrantyLeadDays = 30;

    /// <summary>Active asset managers, each with the org nodes they may see (null = all).</summary>
    public static async Task<IReadOnlyList<(Guid UserId, IReadOnlySet<Guid>? Scope)>> AssetManagersAsync(AppDbContext dbContext, Guid tenantId, CancellationToken cancellationToken)
    {
        var grantedRoles = await dbContext.RolePermissions.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CanManageAssets)
            .Select(x => x.Role)
            .ToListAsync(cancellationToken);
        var roles = grantedRoles.Append(AppRoles.Admin).Distinct().ToList();
        var users = await dbContext.Users.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive && roles.Contains(x.Role))
            .Select(x => new { x.Id, x.Role })
            .ToListAsync(cancellationToken);
        if (users.Count == 0)
        {
            return [];
        }

        var ids = users.Select(x => x.Id).ToList();
        var assignments = (await dbContext.UserOrgNodeAssignments.AsNoTracking()
                .Where(x => x.TenantId == tenantId && ids.Contains(x.UserId))
                .Select(x => new { x.UserId, x.OrgNodeId })
                .ToListAsync(cancellationToken))
            .ToLookup(x => x.UserId, x => x.OrgNodeId);
        var edges = assignments.Count == 0
            ? []
            : (await dbContext.OrgNodes.AsNoTracking().Where(x => x.TenantId == tenantId).Select(x => new { x.Id, x.ParentId }).ToListAsync(cancellationToken))
                .Select(x => (x.Id, x.ParentId)).ToList();

        return users.Select(user =>
        {
            var assigned = assignments[user.Id].ToList();
            // Admins see everything, as in OrgScopeService.
            IReadOnlySet<Guid>? scope = assigned.Count == 0 || user.Role == AppRoles.Admin ? null : OrgScopeService.Descendants(edges, assigned);
            return (user.Id, scope);
        }).ToList();
    }

    /// <summary>
    /// Tells the asset's managers that a meter's excursion was confirmed or got worse: a warning
    /// (drifting) or an alarm (past its limit), with the job raised for it, if any.
    /// </summary>
    public static async Task NotifyConditionAsync(
        INotificationDispatcher? dispatcher, AppDbContext dbContext, Meter meter, Asset asset, MeterReading reading,
        ConditionEvent conditionEvent, string? raisedWorkOrderNumber, CancellationToken cancellationToken)
    {
        if (dispatcher is null)
        {
            return;
        }

        var recipients = (await AssetManagersAsync(dbContext, asset.TenantId, cancellationToken))
            .Where(x => x.Scope is null || (asset.OrgNodeId.HasValue && x.Scope.Contains(asset.OrgNodeId.Value)))
            .Select(x => x.UserId)
            .ToList();
        if (recipients.Count == 0)
        {
            return;
        }

        var alarm = conditionEvent.Level == ConditionLevels.Alarm;
        var location = asset.OrgNodeId.HasValue
            ? await dbContext.OrgNodes.Where(x => x.Id == asset.OrgNodeId).Select(x => x.Name).SingleOrDefaultAsync(cancellationToken)
            : null;
        var value = $"{reading.Value:0.##} {meter.Unit}".Trim();
        var limits = alarm
            ? ConditionMonitor.Limits(meter.MinThreshold, meter.MaxThreshold, meter.Unit)
            : ConditionMonitor.Limits(meter.WarningMin, meter.WarningMax, meter.Unit);
        var what = alarm ? "past its alarm limit" : "outside its normal band";
        var job = raisedWorkOrderNumber is null ? string.Empty : $" Job {raisedWorkOrderNumber} raised.";

        await dispatcher.NotifyAsync(new NotificationRequest(
            asset.TenantId,
            MeterOutOfRange,
            recipients,
            $"{(alarm ? "Alarm" : "Warning")}: {asset.AssetName} {meter.Name} {value}",
            $"{asset.AssetCode} read {value}, {what} ({limits}).{job}",
            $"/maintenance/condition?meter={meter.Id}",
            nameof(Asset),
            asset.Id,
            new Dictionary<string, string?>
            {
                ["AssetName"] = asset.AssetName,
                ["AssetCode"] = asset.AssetCode,
                ["Location"] = location ?? "—",
                ["MeterName"] = meter.Name,
                ["Value"] = value,
                ["Limits"] = limits,
                ["Level"] = conditionEvent.Level,
                ["WorkOrderNumber"] = raisedWorkOrderNumber ?? "—",
                ["RecordedBy"] = reading.RecordedByName ?? reading.Source,
            },
            alarm ? NotificationSeverities.Critical : NotificationSeverities.Warning), cancellationToken);
    }
}

public interface IAssetAlertRunner
{
    /// <summary>Sends warranty warnings that are due. Returns how many notifications were sent.</summary>
    Task<int> RunAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}

/// <summary>
/// Warranty warnings: once when a warranty comes within <see cref="AssetAlerts.WarrantyLeadDays"/>
/// days of its end, and again on the day it ends. Each manager gets one message listing their
/// assets. Sends are marked in <see cref="ActivityReminderLog"/> (a generic sent-once marker keyed
/// by entity, key and date) before they go, so a crash loses a warning rather than repeating one,
/// and a changed expiry date earns a fresh warning.
/// </summary>
internal sealed class AssetAlertRunner(
    AppDbContext dbContext,
    INotificationDispatcher dispatcher,
    ILogger<AssetAlertRunner> logger) : IAssetAlertRunner
{
    private const string DayOfKey = AssetAlerts.WarrantyExpiring + ":today";

    public async Task<int> RunAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var today = utcNow.Date;
        var horizon = today.AddDays(AssetAlerts.WarrantyLeadDays);
        var expiring = await dbContext.Assets.AsNoTracking()
            .Where(x => x.Status != "Inactive" && x.WarrantyExpiryDate != null
                && x.WarrantyExpiryDate >= today && x.WarrantyExpiryDate <= horizon && x.Tenant!.IsActive)
            .Select(x => new { x.Id, x.TenantId, x.AssetName, x.AssetCode, x.OrgNodeId, Expiry = x.WarrantyExpiryDate!.Value })
            .ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var tenant in expiring.GroupBy(x => x.TenantId))
        {
            var ids = tenant.Select(x => x.Id).ToList();
            var already = (await dbContext.ActivityReminderLogs.AsNoTracking()
                    .Where(x => x.TenantId == tenant.Key && ids.Contains(x.EntityId) && (x.NotificationKey == AssetAlerts.WarrantyExpiring || x.NotificationKey == DayOfKey))
                    .Select(x => new { x.EntityId, x.NotificationKey, x.ForTime })
                    .ToListAsync(cancellationToken))
                .Select(x => (x.EntityId, x.NotificationKey, x.ForTime.Date))
                .ToHashSet();

            var due = tenant
                .Select(x => (Asset: x, Key: x.Expiry.Date == today ? DayOfKey : AssetAlerts.WarrantyExpiring))
                .Where(x => !already.Contains((x.Asset.Id, x.Key, x.Asset.Expiry.Date)))
                .ToList();
            if (due.Count == 0)
            {
                continue;
            }

            foreach (var item in due)
            {
                dbContext.ActivityReminderLogs.Add(new ActivityReminderLog { TenantId = tenant.Key, EntityId = item.Asset.Id, NotificationKey = item.Key, ForTime = item.Asset.Expiry.Date });
            }

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                // Another instance is sending this tenant's warnings.
                logger.LogDebug(ex, "Warranty warnings for tenant {TenantId} already recorded.", tenant.Key);
                dbContext.ChangeTracker.Clear();
                continue;
            }

            foreach (var manager in await AssetAlerts.AssetManagersAsync(dbContext, tenant.Key, cancellationToken))
            {
                var theirs = due
                    .Where(x => manager.Scope is null || (x.Asset.OrgNodeId.HasValue && manager.Scope.Contains(x.Asset.OrgNodeId.Value)))
                    .OrderBy(x => x.Asset.Expiry)
                    .ToList();
                if (theirs.Count == 0)
                {
                    continue;
                }

                var lines = theirs.Select(x => $"{x.Asset.AssetName} ({x.Asset.AssetCode}): {(x.Asset.Expiry.Date == today ? "ends today" : $"ends {x.Asset.Expiry:dd MMM yyyy}")}").ToList();
                try
                {
                    await dispatcher.NotifyAsync(new NotificationRequest(
                        tenant.Key,
                        AssetAlerts.WarrantyExpiring,
                        [manager.UserId],
                        theirs.Count == 1 ? $"Warranty ending: {theirs[0].Asset.AssetName}" : $"{theirs.Count} warranties ending soon",
                        string.Join("; ", lines),
                        theirs.Count == 1 ? $"/assets/{theirs[0].Asset.Id}" : "/assets?warranty=expiring",
                        nameof(Asset),
                        theirs.Count == 1 ? theirs[0].Asset.Id : null,
                        new Dictionary<string, string?>
                        {
                            ["Assets"] = string.Join("\n", lines),
                            ["Count"] = theirs.Count.ToString(),
                        },
                        NotificationSeverities.Warning), cancellationToken);
                    sent++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Warranty warning to {UserId} failed.", manager.UserId);
                }
            }
        }

        return sent;
    }
}

/// <summary>Runs <see cref="IAssetAlertRunner"/> every hour; each warning goes once, so the cadence only sets how soon after midnight it arrives.</summary>
public sealed class AssetAlertWorker(IServiceScopeFactory serviceScopeFactory, ILogger<AssetAlertWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var sent = await scope.ServiceProvider.GetRequiredService<IAssetAlertRunner>().RunAsync(DateTime.UtcNow, stoppingToken);
                if (sent > 0)
                {
                    logger.LogInformation("Asset alert worker sent {Count} warranty warning(s).", sent);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Asset alert cycle failed.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
