using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services;

public interface ISlaService
{
    Task ApplyDefinitionAsync(WorkOrder workOrder, CancellationToken cancellationToken = default);
    Task<bool> RefreshWorkOrderAsync(Guid workOrderId, DateTime utcNow, CancellationToken cancellationToken = default);
}

internal sealed class SlaService(
    AppDbContext dbContext,
    IEmailOutboxService emailOutboxService,
    ILogger<SlaService> logger) : ISlaService
{
    private static readonly TimeSpan BusinessDayStart = TimeSpan.FromHours(8);
    private static readonly TimeSpan BusinessDayEnd = TimeSpan.FromHours(17);
    private static readonly string[] ClosedStatuses = WorkOrderStatuses.ClosedStatuses;

    public async Task ApplyDefinitionAsync(WorkOrder workOrder, CancellationToken cancellationToken = default)
    {
        var rule = await ResolveRuleAsync(workOrder.TenantId, workOrder.ClientId ?? Guid.Empty, workOrder.Priority, cancellationToken);

        if (rule is null)
        {
            ResetSlaFields(workOrder);
            return;
        }

        workOrder.SlaResponseDeadline = CalculateDeadline(workOrder.CreatedAt, rule.ResponseTargetHours, rule.BusinessHoursOnly, rule.BusinessHoursStart, rule.BusinessHoursEnd);
        workOrder.SlaResolutionDeadline = CalculateDeadline(workOrder.CreatedAt, rule.ResolutionTargetHours, rule.BusinessHoursOnly, rule.BusinessHoursStart, rule.BusinessHoursEnd);
        workOrder.SlaResponseBreached = false;
        workOrder.SlaResolutionBreached = false;
        workOrder.SlaResponseBreachedAt = null;
        workOrder.SlaResolutionBreachedAt = null;
    }

    private async Task<SlaRule?> ResolveRuleAsync(Guid tenantId, Guid clientId, string priority, CancellationToken cancellationToken)
    {
        var client = await dbContext.Clients
            .Include(x => x.SlaDefinition)
            .ThenInclude(x => x!.Rules)
            .ThenInclude(x => x.Escalations)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == clientId, cancellationToken);

        var definition = client?.SlaDefinitionId is not null && client.SlaDefinition is { IsActive: true }
            ? client.SlaDefinition
            : null;

        if (definition is null)
        {
            definition = await dbContext.SlaDefinitions
                .Include(x => x.Rules)
                .ThenInclude(x => x.Escalations)
                .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.IsDefault && x.IsActive, cancellationToken);
        }

        return definition?.Rules.FirstOrDefault(x => MatchesPriority(x.Priority, priority));
    }

    public async Task<bool> RefreshWorkOrderAsync(Guid workOrderId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var workOrder = await dbContext.WorkOrders
            .Include(x => x.Client)
            .Include(x => x.Assignments)
            .Include(x => x.TechnicianAssignments)
            .SingleOrDefaultAsync(x => x.Id == workOrderId, cancellationToken);

        if (workOrder is null)
        {
            return false;
        }

        var responseBreachedNow = false;
        var resolutionBreachedNow = false;
        var changed = false;

        var firstAssignedAt = ResolveFirstAssignmentAt(workOrder);
        var effectiveResponseDeadline = AdjustDeadlineForActiveHold(workOrder.SlaResponseDeadline, workOrder, utcNow);
        if (effectiveResponseDeadline.HasValue && !workOrder.SlaResponseBreached)
        {
            if (firstAssignedAt.HasValue && firstAssignedAt.Value > effectiveResponseDeadline.Value)
            {
                workOrder.SlaResponseBreached = true;
                workOrder.SlaResponseBreachedAt = firstAssignedAt.Value;
                responseBreachedNow = true;
                changed = true;
            }
            else if (!firstAssignedAt.HasValue && utcNow > effectiveResponseDeadline.Value)
            {
                workOrder.SlaResponseBreached = true;
                workOrder.SlaResponseBreachedAt = utcNow;
                responseBreachedNow = true;
                changed = true;
            }
        }

        var resolvedAt = ResolveResolvedAt(workOrder);
        var effectiveResolutionDeadline = AdjustDeadlineForActiveHold(workOrder.SlaResolutionDeadline, workOrder, utcNow);
        if (effectiveResolutionDeadline.HasValue && !workOrder.SlaResolutionBreached)
        {
            if (resolvedAt.HasValue && resolvedAt.Value > effectiveResolutionDeadline.Value)
            {
                workOrder.SlaResolutionBreached = true;
                workOrder.SlaResolutionBreachedAt = resolvedAt.Value;
                resolutionBreachedNow = true;
                changed = true;
            }
            else if (!resolvedAt.HasValue && IsStillOpen(workOrder.Status) && utcNow > effectiveResolutionDeadline.Value)
            {
                workOrder.SlaResolutionBreached = true;
                workOrder.SlaResolutionBreachedAt = utcNow;
                resolutionBreachedNow = true;
                changed = true;
            }
        }

        var record = await UpsertRecordAsync(workOrder, utcNow, cancellationToken);
        var escalationsFired = await ProcessEscalationsAsync(workOrder, record, effectiveResponseDeadline, effectiveResolutionDeadline, utcNow, cancellationToken);

        if (!changed && !record.RecordChanged && !escalationsFired)
        {
            return false;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (responseBreachedNow)
        {
            await TryQueueBreachNotificationsAsync(workOrder, "Response", workOrder.SlaResponseDeadline, cancellationToken);
        }

        if (resolutionBreachedNow)
        {
            await TryQueueBreachNotificationsAsync(workOrder, "Resolution", workOrder.SlaResolutionDeadline, cancellationToken);
        }

        return changed;
    }

    private async Task<(SlaRecord Record, bool RecordChanged)> UpsertRecordAsync(WorkOrder workOrder, DateTime utcNow, CancellationToken cancellationToken)
    {
        var record = await dbContext.SlaRecords
            .Include(x => x.SlaRule)
            .ThenInclude(x => x!.Escalations)
            .SingleOrDefaultAsync(x => x.WorkOrderId == workOrder.Id, cancellationToken);

        var recordChanged = false;
        if (record is null)
        {
            var rule = await ResolveRuleAsync(workOrder.TenantId, workOrder.ClientId ?? Guid.Empty, workOrder.Priority, cancellationToken);
            record = new SlaRecord { TenantId = workOrder.TenantId, WorkOrderId = workOrder.Id, SlaRuleId = rule?.Id };
            dbContext.SlaRecords.Add(record);
            record.SlaRule = rule;
            recordChanged = true;
        }

        var isPaused = WorkOrderStatuses.IsHold(workOrder.Status) && workOrder.HoldStartedAt.HasValue;
        if (record.IsPaused != isPaused)
        {
            record.IsPaused = isPaused;
            recordChanged = true;
        }

        var pauseReason = isPaused ? workOrder.HoldReason : null;
        if (record.PauseReason != pauseReason)
        {
            record.PauseReason = pauseReason;
            recordChanged = true;
        }

        if (record.ResponseBreachedAt != workOrder.SlaResponseBreachedAt)
        {
            record.ResponseBreachedAt = workOrder.SlaResponseBreachedAt;
            recordChanged = true;
        }

        if (record.ResolutionBreachedAt != workOrder.SlaResolutionBreachedAt)
        {
            record.ResolutionBreachedAt = workOrder.SlaResolutionBreachedAt;
            recordChanged = true;
        }

        return (record, recordChanged);
    }

    private async Task<bool> ProcessEscalationsAsync(
        WorkOrder workOrder,
        (SlaRecord Record, bool RecordChanged) recordState,
        DateTime? effectiveResponseDeadline,
        DateTime? effectiveResolutionDeadline,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var record = recordState.Record;
        var rule = record.SlaRule;
        if (rule is null || rule.Escalations.Count == 0)
        {
            return false;
        }

        var fired = false;

        if (!workOrder.SlaResponseBreached && effectiveResponseDeadline.HasValue)
        {
            var percentElapsed = PercentElapsed(workOrder.CreatedAt, effectiveResponseDeadline.Value, utcNow);
            var due = rule.Escalations
                .Where(x => x.TriggerAtPercent <= percentElapsed && x.TriggerAtPercent > (record.LastResponseEscalationPercent ?? 0))
                .OrderByDescending(x => x.TriggerAtPercent)
                .ToList();

            if (due.Count > 0)
            {
                await FireEscalationsAsync(workOrder, due, "Response", cancellationToken);
                record.LastResponseEscalationPercent = due.Max(x => x.TriggerAtPercent);
                fired = true;
            }
        }

        if (!workOrder.SlaResolutionBreached && effectiveResolutionDeadline.HasValue)
        {
            var percentElapsed = PercentElapsed(workOrder.CreatedAt, effectiveResolutionDeadline.Value, utcNow);
            var due = rule.Escalations
                .Where(x => x.TriggerAtPercent <= percentElapsed && x.TriggerAtPercent > (record.LastResolutionEscalationPercent ?? 0))
                .OrderByDescending(x => x.TriggerAtPercent)
                .ToList();

            if (due.Count > 0)
            {
                await FireEscalationsAsync(workOrder, due, "Resolution", cancellationToken);
                record.LastResolutionEscalationPercent = due.Max(x => x.TriggerAtPercent);
                fired = true;
            }
        }

        return fired;
    }

    private static int PercentElapsed(DateTime startedAt, DateTime deadline, DateTime utcNow)
    {
        var totalMinutes = (deadline - startedAt).TotalMinutes;
        if (totalMinutes <= 0)
        {
            return 100;
        }

        var elapsedMinutes = (utcNow - startedAt).TotalMinutes;
        return (int)Math.Clamp(elapsedMinutes / totalMinutes * 100, 0, 100);
    }

    private async Task FireEscalationsAsync(WorkOrder workOrder, IReadOnlyCollection<SlaEscalation> escalations, string breachType, CancellationToken cancellationToken)
    {
        try
        {
            var roleNames = escalations
                .SelectMany(x => (x.NotifyRoles ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var escalateToUserIds = escalations
                .Where(x => x.EscalateToUserId.HasValue)
                .Select(x => x.EscalateToUserId!.Value)
                .Distinct()
                .ToList();

            var recipients = await dbContext.Users
                .Where(x => x.TenantId == workOrder.TenantId && x.IsActive && x.Email != null
                    && (roleNames.Contains(x.Role) || escalateToUserIds.Contains(x.Id)))
                .Select(x => new { x.Email, x.FullName })
                .Distinct()
                .ToListAsync(cancellationToken);

            var maxPercent = escalations.Max(x => x.TriggerAtPercent);

            foreach (var recipient in recipients)
            {
                await emailOutboxService.QueueEmailAsync(
                    new QueueEmailRequest(
                        workOrder.TenantId,
                        "sla-escalation",
                        "sla-escalation",
                        recipient.Email!,
                        recipient.FullName,
                        "Ecosys Factory Maintenance",
                        null,
                        null,
                        $"{breachType} SLA at {maxPercent}% for {workOrder.WorkOrderNumber}",
                        $"<p>Work order <strong>{workOrder.WorkOrderNumber}</strong> has reached {maxPercent}% of its {breachType.ToLowerInvariant()} SLA target.</p>",
                        $"Work order {workOrder.WorkOrderNumber} has reached {maxPercent}% of its {breachType.ToLowerInvariant()} SLA target.",
                        null),
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to queue SLA escalation notifications for work order {WorkOrderId}.", workOrder.Id);
        }
    }

    private async Task TryQueueBreachNotificationsAsync(WorkOrder workOrder, string breachType, DateTime? deadline, CancellationToken cancellationToken)
    {
        try
        {
            var recipients = await dbContext.Users
                .Where(x => x.TenantId == workOrder.TenantId && x.IsActive && x.Role == AppRoles.Admin && x.Email != null)
                .Select(x => new { x.Email, x.FullName })
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var recipient in recipients)
            {
                await emailOutboxService.QueueEmailAsync(
                    new QueueEmailRequest(
                        workOrder.TenantId,
                        "sla-breach",
                        "sla-breach",
                        recipient.Email!,
                        recipient.FullName,
                        "Ecosys Factory Maintenance",
                        null,
                        null,
                        $"{breachType} SLA breached for {workOrder.WorkOrderNumber}",
                        $"<p>Work order <strong>{workOrder.WorkOrderNumber}</strong> breached its {breachType.ToLowerInvariant()} SLA.</p><p>Deadline: {deadline:yyyy-MM-dd HH:mm} UTC</p>",
                        $"Work order {workOrder.WorkOrderNumber} breached its {breachType.ToLowerInvariant()} SLA. Deadline: {deadline:yyyy-MM-dd HH:mm} UTC",
                        null),
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to queue SLA breach notifications for work order {WorkOrderId}.", workOrder.Id);
        }
    }

    private static DateTime? ResolveFirstAssignmentAt(WorkOrder workOrder)
    {
        var timestamps = workOrder.Assignments
            .Select(x => (DateTime?)x.AssignedAt)
            .Concat(workOrder.TechnicianAssignments.Select(x => (DateTime?)x.AssignedAt))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .OrderBy(x => x)
            .ToList();

        return timestamps.Count == 0 ? null : timestamps[0];
    }

    private static DateTime? ResolveResolvedAt(WorkOrder workOrder)
    {
        var values = new[] { workOrder.CompletedAt, workOrder.ClosedAt }
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .OrderBy(x => x)
            .ToList();

        return values.Count == 0 ? null : values[0];
    }

    private static bool IsStillOpen(string status) =>
        WorkOrderStatuses.IsOpen(status);

    private static DateTime? AdjustDeadlineForActiveHold(DateTime? deadline, WorkOrder workOrder, DateTime utcNow)
    {
        if (!deadline.HasValue || !WorkOrderStatuses.IsHold(workOrder.Status) || !workOrder.HoldStartedAt.HasValue)
        {
            return deadline;
        }

        var holdElapsed = utcNow - workOrder.HoldStartedAt.Value;
        return holdElapsed > TimeSpan.Zero ? deadline.Value.Add(holdElapsed) : deadline;
    }

    private static void ResetSlaFields(WorkOrder workOrder)
    {
        workOrder.SlaResponseDeadline = null;
        workOrder.SlaResolutionDeadline = null;
        workOrder.SlaResponseBreached = false;
        workOrder.SlaResolutionBreached = false;
        workOrder.SlaResponseBreachedAt = null;
        workOrder.SlaResolutionBreachedAt = null;
    }

    private static bool MatchesPriority(string rulePriority, string workOrderPriority) =>
        string.Equals((rulePriority ?? string.Empty).Trim(), (workOrderPriority ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);

    private static DateTime CalculateDeadline(DateTime createdAtUtc, decimal targetHours, bool businessHoursOnly, TimeSpan? businessHoursStart = null, TimeSpan? businessHoursEnd = null)
    {
        if (!businessHoursOnly)
        {
            return createdAtUtc.AddHours((double)targetHours);
        }

        var dayStart = businessHoursStart ?? BusinessDayStart;
        var dayEnd = businessHoursEnd ?? BusinessDayEnd;

        var remainingMinutes = (double)targetHours * 60d;
        var cursor = EnsureBusinessTime(createdAtUtc, dayStart, dayEnd);

        while (remainingMinutes > 0.0001d)
        {
            var boundary = cursor.Date + dayEnd;
            var availableMinutes = Math.Max(0d, (boundary - cursor).TotalMinutes);

            if (availableMinutes <= 0.0001d)
            {
                cursor = NextBusinessDay(cursor.Date.AddDays(1), dayStart);
                continue;
            }

            var consumed = Math.Min(remainingMinutes, availableMinutes);
            cursor = cursor.AddMinutes(consumed);
            remainingMinutes -= consumed;

            if (remainingMinutes > 0.0001d)
            {
                cursor = NextBusinessDay(cursor.Date.AddDays(1), dayStart);
            }
        }

        return cursor;
    }

    private static DateTime EnsureBusinessTime(DateTime value, TimeSpan dayStart, TimeSpan dayEnd)
    {
        var cursor = value;
        if (IsWeekend(cursor))
        {
            return NextBusinessDay(cursor.Date, dayStart);
        }

        var start = cursor.Date + dayStart;
        var end = cursor.Date + dayEnd;

        if (cursor < start)
        {
            return start;
        }

        if (cursor >= end)
        {
            return NextBusinessDay(cursor.Date.AddDays(1), dayStart);
        }

        return cursor;
    }

    private static DateTime NextBusinessDay(DateTime candidateDate, TimeSpan dayStart)
    {
        var cursor = candidateDate.Date + dayStart;
        while (IsWeekend(cursor))
        {
            cursor = cursor.AddDays(1).Date + dayStart;
        }

        return cursor;
    }

    private static bool IsWeekend(DateTime value) =>
        value.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
}

public sealed class SlaEnforcementWorker(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<SlaEnforcementWorker> logger) : BackgroundService
{
    private static readonly string[] ClosedStatuses = ["Closed", "Cancelled"];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SLA enforcement worker started.");

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
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
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var slaService = scope.ServiceProvider.GetRequiredService<ISlaService>();

            var workOrderIds = await dbContext.WorkOrders
                .Where(x =>
                    (x.SlaResponseDeadline.HasValue || x.SlaResolutionDeadline.HasValue) &&
                    !ClosedStatuses.Contains(x.Status))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            foreach (var workOrderId in workOrderIds)
            {
                await slaService.RefreshWorkOrderAsync(workOrderId, DateTime.UtcNow, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SLA enforcement cycle failed.");
        }
    }
}
