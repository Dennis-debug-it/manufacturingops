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
        var client = await dbContext.Clients
            .Include(x => x.SlaDefinition)
            .ThenInclude(x => x!.Rules)
            .SingleOrDefaultAsync(
                x => x.TenantId == workOrder.TenantId && x.Id == workOrder.ClientId,
                cancellationToken);

        var rule = client?.SlaDefinitionId is null || client.SlaDefinition is null || !client.SlaDefinition.IsActive
            ? null
            : client.SlaDefinition.Rules.FirstOrDefault(x => MatchesPriority(x.Priority, workOrder.Priority));

        if (rule is null)
        {
            ResetSlaFields(workOrder);
            return;
        }

        workOrder.SlaResponseDeadline = CalculateDeadline(workOrder.CreatedAt, rule.ResponseTargetHours, rule.BusinessHoursOnly);
        workOrder.SlaResolutionDeadline = CalculateDeadline(workOrder.CreatedAt, rule.ResolutionTargetHours, rule.BusinessHoursOnly);
        workOrder.SlaResponseBreached = false;
        workOrder.SlaResolutionBreached = false;
        workOrder.SlaResponseBreachedAt = null;
        workOrder.SlaResolutionBreachedAt = null;
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

        if (!changed)
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

        return true;
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
                        "Ecosys ServiceOps",
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

    private static DateTime CalculateDeadline(DateTime createdAtUtc, decimal targetHours, bool businessHoursOnly)
    {
        if (!businessHoursOnly)
        {
            return createdAtUtc.AddHours((double)targetHours);
        }

        var remainingMinutes = (double)targetHours * 60d;
        var cursor = EnsureBusinessTime(createdAtUtc);

        while (remainingMinutes > 0.0001d)
        {
            var dayEnd = cursor.Date + BusinessDayEnd;
            var availableMinutes = Math.Max(0d, (dayEnd - cursor).TotalMinutes);

            if (availableMinutes <= 0.0001d)
            {
                cursor = NextBusinessDay(cursor.Date.AddDays(1));
                continue;
            }

            var consumed = Math.Min(remainingMinutes, availableMinutes);
            cursor = cursor.AddMinutes(consumed);
            remainingMinutes -= consumed;

            if (remainingMinutes > 0.0001d)
            {
                cursor = NextBusinessDay(cursor.Date.AddDays(1));
            }
        }

        return cursor;
    }

    private static DateTime EnsureBusinessTime(DateTime value)
    {
        var cursor = value;
        if (IsWeekend(cursor))
        {
            return NextBusinessDay(cursor.Date);
        }

        var start = cursor.Date + BusinessDayStart;
        var end = cursor.Date + BusinessDayEnd;

        if (cursor < start)
        {
            return start;
        }

        if (cursor >= end)
        {
            return NextBusinessDay(cursor.Date.AddDays(1));
        }

        return cursor;
    }

    private static DateTime NextBusinessDay(DateTime candidateDate)
    {
        var cursor = candidateDate.Date + BusinessDayStart;
        while (IsWeekend(cursor))
        {
            cursor = cursor.AddDays(1).Date + BusinessDayStart;
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
