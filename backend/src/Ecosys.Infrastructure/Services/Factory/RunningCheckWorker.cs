using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services.Factory;

public interface IRunningCheckRunner
{
    /// <summary>Generates this shift's checks and flags the ones missed last shift. Returns how many were flagged.</summary>
    Task<int> RunAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}

/// <summary>
/// Keeps running checks moving without anyone opening the app: each shift's checks are created
/// as it starts, and checks not done by the end of their shift are marked missed and reported to
/// that section's supervisors, one message per section and shift.
/// </summary>
internal sealed class RunningCheckRunner(
    AppDbContext dbContext,
    IRunningCheckService runningCheckService,
    ILogger<RunningCheckRunner> logger,
    INotificationDispatcher? notificationDispatcher = null) : IRunningCheckRunner
{
    public const string MissedKey = "factory-check.missed";

    public async Task<int> RunAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var tenants = await dbContext.CheckSchedules.AsNoTracking()
            .Where(x => x.IsActive && x.Tenant!.IsActive)
            .Select(x => x.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var flagged = 0;
        foreach (var tenantId in tenants)
        {
            try
            {
                var missed = await runningCheckService.MarkMissedAsync(tenantId, utcNow, cancellationToken);
                await runningCheckService.EnsureCurrentRunsAsync(tenantId, utcNow, cancellationToken);
                flagged += missed.Count;
                await NotifyAsync(tenantId, missed, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Running checks for tenant {TenantId} failed.", tenantId);
            }
        }

        return flagged;
    }

    private async Task NotifyAsync(Guid tenantId, IReadOnlyList<CheckRun> missed, CancellationToken cancellationToken)
    {
        if (notificationDispatcher is null || missed.Count == 0)
        {
            return;
        }

        var names = await dbContext.OrgNodes.AsNoTracking().Where(x => x.TenantId == tenantId).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        foreach (var group in missed.GroupBy(x => new { x.OrgNodeId, x.PeriodKey }))
        {
            var recipients = await FactoryRecipients.SupervisorsAsync(dbContext, tenantId, group.Key.OrgNodeId, cancellationToken);
            if (recipients.Count == 0)
            {
                continue;
            }

            var place = names.GetValueOrDefault(group.Key.OrgNodeId) ?? "the factory";
            var lines = group.OrderBy(x => x.CheckSchedule?.Name).ThenBy(x => x.Sequence)
                .Select(x => $"{x.CheckSchedule?.Name}: {x.Asset?.AssetCode} {x.Asset?.AssetName}").ToList();
            await notificationDispatcher.NotifyAsync(new NotificationRequest(
                tenantId,
                MissedKey,
                recipients,
                $"{group.Count()} running check{(group.Count() == 1 ? string.Empty : "s")} missed in {place}",
                $"Not done in shift {group.Key.PeriodKey}: {string.Join("; ", lines.Take(5))}{(lines.Count > 5 ? $" and {lines.Count - 5} more" : string.Empty)}",
                "/maintenance/checks?view=missed",
                nameof(CheckRun),
                null,
                new Dictionary<string, string?>
                {
                    ["Count"] = group.Count().ToString(),
                    ["Place"] = place,
                    ["Shift"] = group.Key.PeriodKey,
                    ["Checks"] = string.Join("\n", lines),
                },
                NotificationSeverities.Warning), cancellationToken);
        }
    }
}

/// <summary>Runs <see cref="IRunningCheckRunner"/> every 10 minutes.</summary>
public sealed class RunningCheckWorker(IServiceScopeFactory serviceScopeFactory, ILogger<RunningCheckWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var flagged = await scope.ServiceProvider.GetRequiredService<IRunningCheckRunner>().RunAsync(DateTime.UtcNow, stoppingToken);
                if (flagged > 0)
                {
                    logger.LogInformation("Running check worker flagged {Count} missed check(s).", flagged);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Running check cycle failed.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
