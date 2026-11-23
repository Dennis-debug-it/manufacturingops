using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services;

public sealed class PmSchedulerWorker(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<PmSchedulerWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Overdue work order notifier started.");

        // Wait until 06:00 UTC for first run, then every 24h
        await WaitUntilNextRunTimeAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCycleAsync(stoppingToken);
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private static async Task WaitUntilNextRunTimeAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var nextRun = now.Date.AddHours(6);
        if (nextRun <= now)
            nextRun = nextRun.AddDays(1);

        var delay = nextRun - now;
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, ct).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTime.UtcNow;

            // Send overdue-by-DueDate notifications for open work orders that have passed their due date
            await RunOverdueNotificationsAsync(db, scope.ServiceProvider, now, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Overdue work order notification cycle failed.");
        }
    }

    /// <summary>
    /// The event key (outbox, subject prefix, people's notification settings) and the template it
    /// renders. They differ: the built-in overdue template has always been registered under the
    /// hyphenated key, which is also the key a tenant's edited copy is saved under, and rendering by
    /// the event key threw "template not found" for every work order, so no overdue email ever went.
    /// </summary>
    internal const string OverdueEventKey = "work-order.overdue";
    internal const string OverdueTemplateKey = "work-order-overdue";

    /// <summary>Emails each tenant's admins about open work orders past their due date, once a day per work order.</summary>
    internal async Task<int> RunOverdueNotificationsAsync(AppDbContext db, IServiceProvider services, DateTime now, CancellationToken ct)
    {
        var notified = 0;
        try
        {
            var emailOutbox = services.GetService<IEmailOutboxService>();
            var emailTemplate = services.GetService<IEmailTemplateService>();
            var emailSubjectRule = services.GetService<IEmailSubjectRuleService>();

            if (emailOutbox is null || emailTemplate is null || emailSubjectRule is null)
                return 0;

            // Find open WOs that are past due date.
            // Deduplication: skip any WO that already has an "OverdueNotified" event today.
            var todayUtc = now.Date;
            var alreadyNotifiedToday = await db.WorkOrderEvents
                .Where(x => x.EventType == "OverdueNotified" && x.OccurredAt.Date >= todayUtc)
                .Select(x => x.WorkOrderId)
                .Distinct()
                .ToListAsync(ct);

            var overdueWOs = await db.WorkOrders
                .Where(x => x.DueDate.HasValue
                    && x.DueDate.Value.Date < now.Date
                    && x.Status != "Completed" && x.Status != "Closed"
                    && x.Status != "Cancelled" && x.Status != "Acknowledged"
                    && !alreadyNotifiedToday.Contains(x.Id))
                .Include(x => x.AssignedTechnician)
                .Include(x => x.Asset)
                .ToListAsync(ct);

            foreach (var wo in overdueWOs)
            {
                try
                {
                    var optedOut = await db.UserNotificationPreferences
                        .Where(x => x.TenantId == wo.TenantId && x.NotificationKey == OverdueEventKey && !x.EmailEnabled)
                        .Select(x => x.UserId)
                        .ToListAsync(ct);
                    var admins = await db.Users
                        .Where(x => x.TenantId == wo.TenantId && x.IsActive
                            && x.Role == AppRoles.Admin && x.Email != null && x.HasEmail
                            && !optedOut.Contains(x.Id))
                        .Select(x => new { x.Email, x.FullName })
                        .ToListAsync(ct);

                    if (admins.Count == 0)
                        continue;

                    var template = await emailTemplate.RenderTenantTemplateAsync(
                        wo.TenantId,
                        OverdueTemplateKey,
                        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["WorkOrderNumber"] = wo.WorkOrderNumber,
                            ["AssignedTo"] = wo.AssignedTechnician?.FullName ?? "Unassigned",
                            ["Priority"] = wo.Priority ?? "Normal",
                            ["DueDate"] = wo.DueDate?.ToString("yyyy-MM-dd") ?? "Not set",
                            ["AssetName"] = wo.Asset?.AssetName ?? "N/A"
                        },
                        ct);

                    if (!template.Enabled)
                        continue;

                    var subject = await emailSubjectRule.BuildFinalSubjectAsync(
                        wo.TenantId, OverdueEventKey, template.Subject, cancellationToken: ct);

                    foreach (var admin in admins)
                    {
                        await emailOutbox.QueueEmailAsync(
                            new QueueEmailRequest(
                                wo.TenantId, OverdueEventKey, OverdueTemplateKey,
                                admin.Email!, admin.FullName,
                                template.SenderNameOverride, null, null,
                                subject, template.HtmlBody, template.TextBody, null),
                            ct);
                    }

                    // Record a WorkOrderEvent so we don't re-send within the same day
                    db.WorkOrderEvents.Add(new WorkOrderEvent
                    {
                        TenantId = wo.TenantId,
                        WorkOrderId = wo.Id,
                        EventType = "OverdueNotified",
                        Status = wo.Status,
                        Message = "Overdue notification email queued.",
                        OccurredAt = now
                    });
                    notified++;
                    logger.LogInformation("Queued overdue notification for work order {WoNumber}.", wo.WorkOrderNumber);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to queue overdue notification for work order {WorkOrderId}.", wo.Id);
                }
            }

            if (notified > 0)
            {
                await db.SaveChangesAsync(ct);
                logger.LogInformation("Overdue notification sweep complete. Notified for {Count} work orders.", notified);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Overdue notification sweep failed.");
        }

        return notified;
    }

}
