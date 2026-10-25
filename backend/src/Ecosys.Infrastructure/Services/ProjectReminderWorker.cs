using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services;

public interface IProjectReminderRunner
{
    /// <summary>Sends every project reminder that is due now. Returns how many were sent.</summary>
    Task<int> RunAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}

/// <summary>
/// Date-based project reminders (design section 11): task and milestone due dates, overdue
/// milestones, the planned end date, a missing muster roll and retention falling due. Each
/// reminder is recorded in <see cref="ProjectReminderLog"/> under (entity, key, date), so a
/// reminder goes out once however often the runner is called, and a missed run catches up.
/// Reminders only nudge people; they never change a project.
/// </summary>
internal sealed class ProjectReminderRunner(
    AppDbContext dbContext,
    ILicenseGuardService licenseGuardService,
    IProjectSettingsService projectSettingsService,
    IProjectFinancialsService financialsService,
    IProjectNotificationService notifications,
    ITenantTimeZoneService tenantTimeZoneService,
    ILogger<ProjectReminderRunner> logger) : IProjectReminderRunner
{
    public async Task<int> RunAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        // Every project that still has something to remind about: open ones, plus completed ones
        // that may still hold retention.
        var tenantIds = await dbContext.Projects
            .Where(x => x.Stage!.Category != ProjectStageCategories.Cancelled && x.Stage.Category != ProjectStageCategories.Closed
                || x.RetentionPercent > 0)
            .Select(x => x.TenantId).Distinct().ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var tenantId in tenantIds)
        {
            try
            {
                var modules = (await licenseGuardService.GetSnapshotAsync(tenantId, cancellationToken)).Modules;
                if (!modules.Contains(ServiceOpsPlans.Projects, StringComparer.OrdinalIgnoreCase))
                    continue;
                sent += await RunTenantAsync(tenantId, utcNow, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Project reminders failed for tenant {TenantId}.", tenantId);
            }
        }
        return sent;
    }

    private async Task<int> RunTenantAsync(Guid tenantId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var settings = await projectSettingsService.GetAsync(tenantId, cancellationToken);
        var zone = await tenantTimeZoneService.GetTimeZoneAsync(tenantId, cancellationToken);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), zone);
        var today = DateOnly.FromDateTime(localNow);
        var lead = today.AddDays(settings.ReminderLeadDays);

        var projects = await dbContext.Projects.Include(x => x.Stage)
            .Where(x => x.TenantId == tenantId && x.Stage!.Category != ProjectStageCategories.Cancelled)
            .ToListAsync(cancellationToken);
        var open = projects.Where(x => ProjectStageCategories.Open.Contains(x.Stage!.Category)).ToList();
        var openIds = open.Select(x => x.Id).ToList();
        var byId = projects.ToDictionary(x => x.Id);

        var log = await dbContext.ProjectReminderLogs.Where(x => x.TenantId == tenantId && x.ForDate >= today.AddDays(-400))
            .Select(x => new { x.EntityId, x.NotificationKey, x.ForDate }).ToListAsync(cancellationToken);
        var already = log.Select(x => (x.EntityId, x.NotificationKey, x.ForDate)).ToHashSet();
        var sent = 0;

        async Task SendOnceAsync(Guid entityId, string key, DateOnly forDate, Project project, IReadOnlyCollection<Guid> userIds,
            IReadOnlyDictionary<string, string?> values, string title, string body)
        {
            if (!already.Add((entityId, key, forDate)))
                return;
            // The marker is saved first: a crash after it loses one reminder, never repeats one.
            dbContext.ProjectReminderLogs.Add(new ProjectReminderLog { TenantId = tenantId, EntityId = entityId, NotificationKey = key, ForDate = forDate });
            await dbContext.SaveChangesAsync(cancellationToken);
            await notifications.NotifyAsync(tenantId, key, project, userIds, values, title, body, cancellationToken);
            sent++;
        }

        // Tasks due tomorrow (or today, if the run was missed).
        if (settings.Tasks)
        {
            var tasks = await dbContext.ProjectTasks
                .Where(x => x.TenantId == tenantId && openIds.Contains(x.ProjectId) && x.AssigneeUserId != null && x.DueDate != null
                    && x.DueDate >= today && x.DueDate <= today.AddDays(1)
                    && x.Status != ProjectTaskStatuses.Done && x.Status != ProjectTaskStatuses.Cancelled)
                .ToListAsync(cancellationToken);
            foreach (var task in tasks)
            {
                var project = byId[task.ProjectId];
                await SendOnceAsync(task.Id, ProjectNotificationKeys.TaskDue, task.DueDate!.Value, project, [task.AssigneeUserId!.Value],
                    new Dictionary<string, string?> { ["TaskTitle"] = task.Title, ["DueDate"] = task.DueDate.Value.ToString("yyyy-MM-dd") },
                    $"Task due on {project.ProjectNumber}", $"{task.Title} is due {task.DueDate:dd MMM}.");
            }
        }

        if (settings.Milestones)
        {
            var milestones = await dbContext.ProjectMilestones
                .Where(x => x.TenantId == tenantId && openIds.Contains(x.ProjectId) && x.PlannedDate != null && x.PlannedDate <= lead
                    && x.Status != ProjectMilestoneStatuses.Completed && x.Status != ProjectMilestoneStatuses.Verified)
                .ToListAsync(cancellationToken);
            foreach (var milestone in milestones)
            {
                var project = byId[milestone.ProjectId];
                var values = new Dictionary<string, string?> { ["MilestoneName"] = milestone.Name, ["PlannedDate"] = milestone.PlannedDate!.Value.ToString("yyyy-MM-dd") };
                if (milestone.PlannedDate >= today)
                {
                    if (project.ProjectManagerUserId is { } pm)
                        await SendOnceAsync(milestone.Id, ProjectNotificationKeys.MilestoneDue, milestone.PlannedDate.Value, project, [pm], values,
                            $"Milestone due on {project.ProjectNumber}", $"{milestone.Name} is planned for {milestone.PlannedDate:dd MMM}.");
                }
                else
                {
                    var recipients = await notifications.ManagerAndRolesAsync(tenantId, project, [AppRoles.Admin, AppRoles.Manager], cancellationToken);
                    await SendOnceAsync(milestone.Id, ProjectNotificationKeys.MilestoneOverdue, milestone.PlannedDate.Value, project, recipients, values,
                        $"Milestone overdue on {project.ProjectNumber}", $"{milestone.Name} was planned for {milestone.PlannedDate:dd MMM} and isn't complete.");
                }
            }
        }

        foreach (var project in open.Where(x => x.PlannedEndDate.HasValue && x.PlannedEndDate >= today && x.PlannedEndDate <= lead && x.ProjectManagerUserId.HasValue
                     && x.Stage!.Category != ProjectStageCategories.Handover))
        {
            await SendOnceAsync(project.Id, ProjectNotificationKeys.EndDateApproaching, project.PlannedEndDate!.Value, project, [project.ProjectManagerUserId!.Value],
                new Dictionary<string, string?> { ["PlannedEndDate"] = project.PlannedEndDate.Value.ToString("yyyy-MM-dd") },
                $"{project.ProjectNumber} ends soon", $"{project.Name} is planned to finish on {project.PlannedEndDate:dd MMM}.");
        }

        // Muster roll not marked by the reminder hour on a working day (Monday to Saturday) of an
        // active project that has workers on its roster.
        if (settings.MusterRoll && localNow.DayOfWeek != DayOfWeek.Sunday && localNow.Hour >= settings.MusterReminderHour)
        {
            var activeIds = open.Where(x => x.Stage!.Category == ProjectStageCategories.Active).Select(x => x.Id).ToList();
            var withWorkers = await dbContext.ProjectWorkers.Where(x => x.TenantId == tenantId && activeIds.Contains(x.ProjectId) && x.IsActive)
                .Select(x => x.ProjectId).Distinct().ToListAsync(cancellationToken);
            var marked = await dbContext.ProjectMusterEntries.Where(x => x.TenantId == tenantId && withWorkers.Contains(x.ProjectId) && x.WorkDate == today)
                .Select(x => x.ProjectId).Distinct().ToListAsync(cancellationToken);
            foreach (var projectId in withWorkers.Except(marked))
            {
                var project = byId[projectId];
                var recipients = await dbContext.ProjectMembers
                    .Where(x => x.TenantId == tenantId && x.ProjectId == projectId && ProjectMemberRoles.SiteRoles.Contains(x.Role))
                    .Select(x => x.UserId ?? (x.Technician != null ? x.Technician.UserId : null))
                    .Where(x => x != null).Select(x => x!.Value)
                    .ToListAsync(cancellationToken);
                if (project.ProjectManagerUserId.HasValue)
                    recipients.Add(project.ProjectManagerUserId.Value);
                await SendOnceAsync(project.Id, ProjectNotificationKeys.MusterMissing, today, project, recipients,
                    new Dictionary<string, string?> { ["WorkDate"] = today.ToString("yyyy-MM-dd") },
                    $"Muster roll for {project.ProjectNumber}", $"Today's muster roll for {project.Name} hasn't been marked yet.");
            }
        }

        // Retention falling due: at handover, and again once the defects liability period ends.
        foreach (var project in projects.Where(x => x.HandedOverAt.HasValue && x.RetentionPercent > 0))
        {
            var (held, completionDue, finalDue) = await financialsService.RetentionPositionAsync(tenantId, project, cancellationToken);
            if (held <= 0)
                continue;
            var recipients = await notifications.ManagerAndRolesAsync(tenantId, project, [AppRoles.Admin, AppRoles.FinanceUser], cancellationToken);
            if (completionDue > 0)
                await SendOnceAsync(project.Id, ProjectNotificationKeys.RetentionDue, DateOnly.FromDateTime(project.HandedOverAt!.Value), project, recipients,
                    new Dictionary<string, string?> { ["Stage"] = "practical completion", ["Amount"] = $"{project.Currency} {completionDue:N2}" },
                    $"Retention due on {project.ProjectNumber}", $"{project.Currency} {completionDue:N2} of retention can be released at practical completion.");
            if (finalDue > 0)
                await SendOnceAsync(project.Id, ProjectNotificationKeys.RetentionDue, DateOnly.FromDateTime(project.HandedOverAt!.Value.AddMonths(project.DefectsLiabilityMonths ?? 0)),
                    project, recipients,
                    new Dictionary<string, string?> { ["Stage"] = "the end of the defects liability period", ["Amount"] = $"{project.Currency} {finalDue:N2}" },
                    $"Retention balance due on {project.ProjectNumber}", $"The defects liability period has ended: {project.Currency} {finalDue:N2} of retention can be released.");
        }

        return sent;
    }
}

/// <summary>Runs <see cref="IProjectReminderRunner"/> every 15 minutes, so the muster reminder lands close to its hour.</summary>
public sealed class ProjectReminderWorker(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ProjectReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Project reminder worker started.");
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var sent = await scope.ServiceProvider.GetRequiredService<IProjectReminderRunner>().RunAsync(DateTime.UtcNow, stoppingToken);
                if (sent > 0)
                    logger.LogInformation("Project reminder worker sent {Count} reminder(s).", sent);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Project reminder cycle failed.");
            }
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
