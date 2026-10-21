using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services;

public interface IActivityReminderRunner
{
    /// <summary>Sends every activity and action-item reminder that is due now. Returns how many were sent.</summary>
    Task<int> RunAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}

/// <summary>
/// Activity reminders (<c>activity.reminder</c>, <see cref="Activity.ReminderMinutesBefore"/> ahead of the
/// start, to the staff attendees and the organiser) and action items due tomorrow (<c>action-item.due</c>,
/// to the assignee). Each is recorded in <see cref="ActivityReminderLog"/> under what it was for (the start
/// time or the due date), so it goes once however often the runner is called, a missed run catches up
/// while the meeting is still ahead, and a moved meeting or due date earns a fresh reminder. All-day
/// activities get no timed reminder.
/// </summary>
internal sealed class ActivityReminderRunner(
    AppDbContext dbContext,
    ActivityNotifier notifier,
    ITenantTimeZoneService tenantTimeZoneService,
    ILogger<ActivityReminderRunner> logger) : IActivityReminderRunner
{
    /// <summary>The longest reminder lead an activity can ask for.</summary>
    private static readonly TimeSpan MaxLead = TimeSpan.FromDays(7);

    public async Task<int> RunAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var sent = 0;
        var horizon = utcNow + MaxLead;
        var activities = await dbContext.Activities.Include(x => x.Attendees)
            .Where(x => x.Status == ActivityStatuses.Scheduled && !x.AllDay && x.ReminderMinutesBefore != null
                && x.StartsAt > utcNow && x.StartsAt <= horizon && x.Tenant!.IsActive)
            .ToListAsync(cancellationToken);
        var due = activities.Where(x => x.StartsAt.AddMinutes(-x.ReminderMinutesBefore!.Value) <= utcNow).ToList();
        if (due.Count > 0)
        {
            var ids = due.Select(x => x.Id).ToList();
            var already = (await dbContext.ActivityReminderLogs
                    .Where(x => ids.Contains(x.EntityId) && x.NotificationKey == ActivityNotificationKeys.Reminder)
                    .Select(x => new { x.EntityId, x.ForTime }).ToListAsync(cancellationToken))
                .Select(x => (x.EntityId, x.ForTime)).ToHashSet();
            foreach (var activity in due.Where(x => !already.Contains((x.Id, x.StartsAt))))
            {
                if (await SendOnceAsync(activity.TenantId, activity.Id, ActivityNotificationKeys.Reminder, activity.StartsAt,
                        () => notifier.ActivityAsync(ActivityNotificationKeys.Reminder, activity, ActivityService.StaffOf(activity), null, cancellationToken),
                        cancellationToken))
                    sent++;
            }
        }

        // Action items: the day before (or the day itself, if that run was missed), in the tenant's own calendar.
        var utcToday = DateOnly.FromDateTime(utcNow);
        var candidates = await dbContext.ActionItems
            .Where(x => x.Status == ActionItemStatuses.Open && x.AssigneeUserId != null && x.DueDate != null
                && x.DueDate >= utcToday.AddDays(-1) && x.DueDate <= utcToday.AddDays(2) && x.Tenant!.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var tenant in candidates.GroupBy(x => x.TenantId))
        {
            var zone = await tenantTimeZoneService.GetTimeZoneAsync(tenant.Key, cancellationToken);
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), zone));
            var ids = tenant.Select(x => x.Id).ToList();
            var already = (await dbContext.ActivityReminderLogs
                    .Where(x => ids.Contains(x.EntityId) && x.NotificationKey == ActivityNotificationKeys.ActionItemDue)
                    .Select(x => new { x.EntityId, x.ForTime }).ToListAsync(cancellationToken))
                .Select(x => (x.EntityId, x.ForTime)).ToHashSet();
            foreach (var item in tenant.Where(x => x.DueDate >= today && x.DueDate <= today.AddDays(1)))
            {
                var forTime = item.DueDate!.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                if (already.Contains((item.Id, forTime)))
                    continue;
                if (await SendOnceAsync(item.TenantId, item.Id, ActivityNotificationKeys.ActionItemDue, forTime,
                        () => notifier.ActionItemDueAsync(item, cancellationToken), cancellationToken))
                    sent++;
            }
        }
        return sent;
    }

    /// <summary>The marker is saved first: a crash after it loses one reminder, never repeats one.</summary>
    private async Task<bool> SendOnceAsync(Guid tenantId, Guid entityId, string key, DateTime forTime, Func<Task> send, CancellationToken cancellationToken)
    {
        var marker = new ActivityReminderLog { TenantId = tenantId, EntityId = entityId, NotificationKey = key, ForTime = forTime };
        dbContext.ActivityReminderLogs.Add(marker);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Another instance got there first (the unique index), so this one is not ours to send.
            dbContext.Entry(marker).State = EntityState.Detached;
            logger.LogDebug(ex, "Reminder {Key} for {EntityId} already recorded.", key, entityId);
            return false;
        }

        try
        {
            await send();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Reminder {Key} for {EntityId} failed.", key, entityId);
        }
        return true;
    }
}

/// <summary>Runs <see cref="IActivityReminderRunner"/> every 5 minutes, under the background-work lease.</summary>
public sealed class ActivityReminderWorker(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ActivityReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Activity reminder worker started.");
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var sent = await scope.ServiceProvider.GetRequiredService<IActivityReminderRunner>().RunAsync(DateTime.UtcNow, stoppingToken);
                if (sent > 0)
                    logger.LogInformation("Activity reminder worker sent {Count} reminder(s).", sent);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Activity reminder cycle failed.");
            }
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
