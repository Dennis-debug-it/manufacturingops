using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services;

/// <summary>
/// Sends reminder notifications for approval steps that have been pending past their
/// due time. Per the approved design, this NEVER advances or reassigns a request — it
/// only nudges the current approvers, and re-nudges once per day until decided.
/// </summary>
public sealed class ApprovalReminderWorker(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ApprovalReminderWorker> logger) : BackgroundService
{
    private static readonly TimeSpan ReminderInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Approval reminder worker started.");

        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCycleAsync(stoppingToken);
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var auditLog = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
            var now = DateTime.UtcNow;

            // Pending steps that are the current level of a still-pending request, are past
            // their due time, and haven't been reminded within the interval.
            var dueSteps = await db.ApprovalRequestSteps
                .IgnoreQueryFilters()
                .Where(step =>
                    step.Status == ApprovalStatuses.Pending &&
                    step.DueAt != null && step.DueAt <= now &&
                    (step.ReminderSentAt == null || step.ReminderSentAt <= now - ReminderInterval))
                .Join(
                    db.ApprovalRequests.IgnoreQueryFilters().Where(r => r.Status == ApprovalStatuses.Pending),
                    step => step.ApprovalRequestId,
                    request => request.Id,
                    (step, request) => new { step, request })
                .Where(x => x.step.LevelOrder == x.request.CurrentLevelOrder)
                .ToListAsync(ct);

            foreach (var item in dueSteps)
            {
                await auditLog.LogAsync(
                    item.request.TenantId,
                    null,
                    "approval.request.reminder",
                    nameof(ApprovalRequest),
                    item.request.Id.ToString(),
                    $"Reminder: {item.request.DocumentType} {item.request.DocumentNumber} ({item.request.Currency} {item.request.Amount:N2}) is awaiting approval at level {item.step.LevelOrder} ({item.step.LevelName}).",
                    severity: "Warning",
                    cancellationToken: ct);

                item.step.ReminderSentAt = now;
            }

            if (dueSteps.Count > 0)
            {
                await db.SaveChangesAsync(ct);
                logger.LogInformation("Approval reminder worker sent {Count} reminder(s).", dueSteps.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Approval reminder cycle failed.");
        }
    }
}
