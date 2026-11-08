using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services;

public static class EscalationConditions
{
    public const string UnassignedAfterMins = "UnassignedAfterMins";
    public const string InProgressOverEstimatePercent = "InProgressOverEstimatePercent";
    public const string StatusUnchangedMins = "StatusUnchangedMins";
    public const string OverdueSinceMins = "OverdueSinceMins";

    public static readonly IReadOnlyCollection<string> All =
        [UnassignedAfterMins, InProgressOverEstimatePercent, StatusUnchangedMins, OverdueSinceMins];
}

public static class EscalationActions
{
    public const string NotifyDispatcher = "NotifyDispatcher";
    public const string NotifyManager = "NotifyManager";
    public const string NotifyTechnician = "NotifyTechnician";
    public const string ReassignToUserId = "ReassignToUserId";
    public const string ChangePriority = "ChangePriority";

    public static readonly IReadOnlyCollection<string> All =
        [NotifyDispatcher, NotifyManager, NotifyTechnician, ReassignToUserId, ChangePriority];
}

public interface IEscalationRuleService
{
    Task<int> EvaluateAllTenantsAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}

internal sealed class EscalationRuleService(
    AppDbContext dbContext,
    IEmailOutboxService emailOutboxService,
    ILogger<EscalationRuleService> logger,
    INotificationDispatcher? notificationDispatcher = null) : IEscalationRuleService
{
    private static readonly string[] ClosedStatuses = WorkOrderStatuses.ClosedStatuses;

    public async Task<int> EvaluateAllTenantsAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var rules = await dbContext.EscalationRules
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);

        if (rules.Count == 0)
        {
            return 0;
        }

        var tenantIds = rules.Select(x => x.TenantId).Distinct().ToList();
        var openWorkOrders = await dbContext.WorkOrders
            .Include(x => x.TechnicianAssignments)
            .Include(x => x.Events)
            .Where(x => tenantIds.Contains(x.TenantId) && !ClosedStatuses.Contains(x.Status))
            .ToListAsync(cancellationToken);

        var fired = 0;
        foreach (var rule in rules)
        {
            var candidateWorkOrders = openWorkOrders.Where(x => x.TenantId == rule.TenantId).ToList();
            foreach (var workOrder in candidateWorkOrders)
            {
                try
                {
                    if (!MatchesCondition(rule, workOrder, utcNow))
                    {
                        continue;
                    }

                    if (await IsInCooldownAsync(rule, workOrder, utcNow, cancellationToken))
                    {
                        continue;
                    }

                    await FireAsync(rule, workOrder, utcNow, cancellationToken);
                    fired++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to evaluate escalation rule {RuleId} for work order {WorkOrderId}.", rule.Id, workOrder.Id);
                }
            }
        }

        return fired;
    }

    private static bool MatchesCondition(EscalationRule rule, WorkOrder workOrder, DateTime utcNow)
    {
        var elapsed = utcNow - workOrder.CreatedAt;

        return rule.TriggerCondition switch
        {
            EscalationConditions.UnassignedAfterMins =>
                !workOrder.AssignmentGroupId.HasValue
                    && !workOrder.TechnicianAssignments.Any()
                    && !workOrder.AssignedTechnicianId.HasValue
                    && elapsed.TotalMinutes >= rule.TriggerValue,

            EscalationConditions.InProgressOverEstimatePercent =>
                string.Equals(workOrder.Status, WorkOrderStatuses.InProgress, StringComparison.OrdinalIgnoreCase)
                    && workOrder.WorkStartedAt.HasValue
                    && workOrder.EstimatedDuration.HasValue
                    && workOrder.EstimatedDuration.Value > 0
                    && (decimal)(utcNow - workOrder.WorkStartedAt.Value).TotalHours
                        >= workOrder.EstimatedDuration.Value * (rule.TriggerValue / 100m),

            EscalationConditions.StatusUnchangedMins =>
                (utcNow - LastStatusChangeAt(workOrder)).TotalMinutes >= rule.TriggerValue,

            EscalationConditions.OverdueSinceMins =>
                workOrder.DueDate.HasValue
                    && (utcNow - workOrder.DueDate.Value).TotalMinutes >= rule.TriggerValue,

            _ => false,
        };
    }

    private static DateTime LastStatusChangeAt(WorkOrder workOrder)
    {
        var lastEvent = workOrder.Events
            .Where(x => string.Equals(x.EventType, "StatusChanged", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.OccurredAt)
            .FirstOrDefault();

        return lastEvent?.OccurredAt ?? workOrder.CreatedAt;
    }

    private async Task<bool> IsInCooldownAsync(EscalationRule rule, WorkOrder workOrder, DateTime utcNow, CancellationToken cancellationToken)
    {
        if (rule.CooldownMins <= 0)
        {
            return false;
        }

        var lastFired = await dbContext.EscalationFired
            .Where(x => x.RuleId == rule.Id && x.WorkOrderId == workOrder.Id)
            .OrderByDescending(x => x.FiredAt)
            .Select(x => (DateTime?)x.FiredAt)
            .FirstOrDefaultAsync(cancellationToken);

        return lastFired.HasValue && (utcNow - lastFired.Value).TotalMinutes < rule.CooldownMins;
    }

    private async Task FireAsync(EscalationRule rule, WorkOrder workOrder, DateTime utcNow, CancellationToken cancellationToken)
    {
        await ExecuteActionAsync(rule, workOrder, cancellationToken);

        dbContext.EscalationFired.Add(new EscalationFired
        {
            TenantId = rule.TenantId,
            RuleId = rule.Id,
            WorkOrderId = workOrder.Id,
            FiredAt = utcNow,
        });

        dbContext.WorkOrderEvents.Add(new WorkOrderEvent
        {
            TenantId = rule.TenantId,
            WorkOrderId = workOrder.Id,
            EventType = "Escalation",
            Message = $"Escalation rule '{rule.Name}' fired: {rule.Action}.",
            OccurredAt = utcNow,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ExecuteActionAsync(EscalationRule rule, WorkOrder workOrder, CancellationToken cancellationToken)
    {
        switch (rule.Action)
        {
            case EscalationActions.NotifyDispatcher:
                await NotifyRoleAsync(workOrder, rule, AppRoles.Admin, cancellationToken);
                break;
            case EscalationActions.NotifyManager:
                await NotifyRoleAsync(workOrder, rule, AppRoles.Admin, cancellationToken);
                break;
            case EscalationActions.NotifyTechnician:
                await NotifyTechnicianAsync(workOrder, rule, cancellationToken);
                break;
            case EscalationActions.ReassignToUserId:
                await ReassignAsync(workOrder, rule, cancellationToken);
                break;
            case EscalationActions.ChangePriority:
                if (!string.IsNullOrWhiteSpace(rule.ActionValue))
                {
                    workOrder.Priority = WorkOrderPriorities.Normalize(rule.ActionValue);
                }
                break;
        }
    }

    private async Task NotifyRoleAsync(WorkOrder workOrder, EscalationRule rule, string role, CancellationToken cancellationToken)
    {
        var recipients = await dbContext.Users
            .Where(x => x.TenantId == workOrder.TenantId && x.IsActive && x.Role == role && x.Email != null)
            .Select(x => new { x.Email, x.FullName })
            .ToListAsync(cancellationToken);

        foreach (var recipient in recipients)
        {
            await QueueEscalationEmailAsync(workOrder, rule, recipient.Email!, recipient.FullName, cancellationToken);
        }
    }

    private async Task NotifyTechnicianAsync(WorkOrder workOrder, EscalationRule rule, CancellationToken cancellationToken)
    {
        var technicianUserId = await dbContext.Technicians
            .Where(x => x.Id == workOrder.AssignedTechnicianId)
            .Select(x => x.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!technicianUserId.HasValue)
        {
            return;
        }

        var recipient = await dbContext.Users
            .Where(x => x.Id == technicianUserId.Value && x.Email != null)
            .Select(x => new { x.Email, x.FullName })
            .FirstOrDefaultAsync(cancellationToken);

        if (recipient is not null)
        {
            await QueueEscalationEmailAsync(workOrder, rule, recipient.Email!, recipient.FullName, cancellationToken);
        }
    }

    private async Task ReassignAsync(WorkOrder workOrder, EscalationRule rule, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(rule.ActionValue, out var targetUserId))
        {
            return;
        }

        var technician = await dbContext.Technicians
            .FirstOrDefaultAsync(x => x.TenantId == workOrder.TenantId && x.UserId == targetUserId, cancellationToken);

        if (technician is null)
        {
            return;
        }

        workOrder.AssignedTechnicianId = technician.Id;
        if (workOrder.AssignmentType == WorkOrderAssignmentTypes.Unassigned)
        {
            workOrder.AssignmentType = WorkOrderAssignmentTypes.IndividualTechnician;
        }

        var alreadyAssigned = await dbContext.WorkOrderTechnicianAssignments
            .AnyAsync(x => x.WorkOrderId == workOrder.Id && x.TechnicianId == technician.Id, cancellationToken);

        if (!alreadyAssigned)
        {
            dbContext.WorkOrderTechnicianAssignments.Add(new WorkOrderTechnicianAssignment
            {
                TenantId = workOrder.TenantId,
                WorkOrderId = workOrder.Id,
                TechnicianId = technician.Id,
                IsLead = true,
                AssignedAt = DateTime.UtcNow,
            });
            await AssignmentNotifications.NotifyAsync(notificationDispatcher, workOrder, [technician], cancellationToken);
        }

        var recipient = await dbContext.Users
            .Where(x => x.Id == targetUserId && x.Email != null)
            .Select(x => new { x.Email, x.FullName })
            .FirstOrDefaultAsync(cancellationToken);

        if (recipient is not null)
        {
            await QueueEscalationEmailAsync(workOrder, rule, recipient.Email!, recipient.FullName, cancellationToken);
        }
    }

    private async Task QueueEscalationEmailAsync(WorkOrder workOrder, EscalationRule rule, string toEmail, string? toName, CancellationToken cancellationToken)
    {
        try
        {
            await emailOutboxService.QueueEmailAsync(
                new QueueEmailRequest(
                    workOrder.TenantId,
                    "escalation-rule-fired",
                    "escalation-rule-fired",
                    toEmail,
                    toName,
                    "Ecosys Factory Maintenance",
                    null,
                    null,
                    $"Escalation: {rule.Name} for {workOrder.WorkOrderNumber}",
                    $"<p>Escalation rule <strong>{rule.Name}</strong> fired for work order <strong>{workOrder.WorkOrderNumber}</strong>.</p>",
                    $"Escalation rule {rule.Name} fired for work order {workOrder.WorkOrderNumber}.",
                    null),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to queue escalation email for work order {WorkOrderId}.", workOrder.Id);
        }
    }
}

public sealed class EscalationRuleWorker(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<EscalationRuleWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Escalation rule worker started.");

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10));
        await RunCycleAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCycleAsync(stoppingToken);
        }
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var escalationRuleService = scope.ServiceProvider.GetRequiredService<IEscalationRuleService>();
            await escalationRuleService.EvaluateAllTenantsAsync(DateTime.UtcNow, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Escalation rule evaluation cycle failed.");
        }
    }
}
