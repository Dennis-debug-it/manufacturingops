using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services;

public interface INotificationPushProcessor
{
    /// <summary>
    /// Sends one batch of the pushes recorded on <see cref="Notification.PushPendingSince"/>, oldest
    /// first, and clears the flag. Returns how many rows were handled (sent or dropped as stale).
    /// </summary>
    Task<int> ProcessPendingAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}

/// <summary>
/// Push that survives a restart (design 2.3). The flag is cleared only after the send, so a crash
/// means a delayed (at worst repeated) push, never a lost one. A push still pending after 24 hours
/// is dropped: by then the bell has it, and a day-old alert on a phone is noise.
/// </summary>
internal sealed class NotificationPushProcessor(
    AppDbContext dbContext,
    IWebPushSender sender,
    ILogger<NotificationPushProcessor> logger) : INotificationPushProcessor
{
    public const int BatchSize = 200;
    public static readonly TimeSpan StaleAfter = TimeSpan.FromHours(24);

    public async Task<int> ProcessPendingAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.Notifications
            .IgnoreQueryFilters()
            .Where(x => x.PushPendingSince != null)
            .OrderBy(x => x.PushPendingSince)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        var staleBefore = utcNow - StaleAfter;
        var handled = 0;
        foreach (var row in rows)
        {
            if (row.PushPendingSince >= staleBefore)
            {
                try
                {
                    await sender.SendAsync(dbContext, row.TenantId, [row.UserId],
                        new PushMessage(row.Title, row.Body, row.Url ?? "/", InboxNotifications.PushTag(row)), cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Left pending: the next cycle retries it until it goes stale.
                    logger.LogWarning(ex, "Failed to send push for notification {NotificationId}.", row.Id);
                    continue;
                }
            }

            row.PushPendingSince = null;
            handled++;
        }

        if (rows.Count > 0)
            await dbContext.SaveChangesAsync(cancellationToken);
        return handled;
    }
}

/// <summary>Drains <see cref="INotificationPushProcessor"/> every few seconds. Leased: one instance sends.</summary>
public sealed class NotificationPushWorker(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<NotificationPushWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Notification push worker started.");
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int handled;
                do
                {
                    using var scope = serviceScopeFactory.CreateScope();
                    handled = await scope.ServiceProvider.GetRequiredService<INotificationPushProcessor>().ProcessPendingAsync(DateTime.UtcNow, stoppingToken);
                }
                while (handled == NotificationPushProcessor.BatchSize && !stoppingToken.IsCancellationRequested);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Notification push cycle failed.");
            }
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}

public interface INotificationHousekeeping
{
    /// <summary>Deletes inbox rows older than the retention period. Returns how many were deleted.</summary>
    Task<int> DeleteExpiredAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}

/// <summary>
/// Notifications are kept 90 days (R2), read or not, then deleted in batches so one run never holds
/// a long lock on the table.
/// </summary>
internal sealed class NotificationHousekeeping(AppDbContext dbContext) : INotificationHousekeeping
{
    public static readonly TimeSpan Retention = TimeSpan.FromDays(90);
    public const int BatchSize = 1000;

    public async Task<int> DeleteExpiredAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var cutoff = utcNow - Retention;
        var deleted = 0;
        while (true)
        {
            var ids = await dbContext.Notifications
                .IgnoreQueryFilters()
                .Where(x => x.CreatedAt < cutoff)
                .OrderBy(x => x.CreatedAt)
                .Select(x => x.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);
            if (ids.Count == 0)
                return deleted;

            // Key-only stubs: EF deletes by primary key without loading the rows.
            dbContext.Notifications.RemoveRange(ids.Select(id => new Notification { Id = id }));
            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            deleted += ids.Count;
        }
    }
}

/// <summary>Runs <see cref="INotificationHousekeeping"/> once a day. Leased: one instance deletes.</summary>
public sealed class NotificationHousekeepingWorker(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<NotificationHousekeepingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Notification housekeeping worker started.");
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var deleted = await scope.ServiceProvider.GetRequiredService<INotificationHousekeeping>().DeleteExpiredAsync(DateTime.UtcNow, stoppingToken);
                if (deleted > 0)
                    logger.LogInformation("Notification housekeeping deleted {Count} notification(s) older than 90 days.", deleted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Notification housekeeping cycle failed.");
            }
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
