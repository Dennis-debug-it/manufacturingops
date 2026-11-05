using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services.Factory;

public static class CertificateStatuses
{
    public const string Valid = "Valid";
    public const string Expiring = "Expiring";
    public const string Expired = "Expired";

    public static string Of(DateTime? expiresOn, DateTime today) => expiresOn is not { } e
        ? Valid
        : e.Date < today.Date ? Expired : (e.Date - today.Date).TotalDays <= 30 ? Expiring : Valid;
}

public sealed record WorkloadRow(
    Guid TechnicianId, string Name, string? Trade, decimal RosteredHours, decimal LoggedHours, decimal OvertimeHours, decimal LabourCost,
    int JobsCompleted, int OpenJobs, decimal OpenEstimatedHours, decimal? UtilisationPercent, int OnCallDays, decimal LeaveDays);

public interface IPeopleService
{
    /// <summary>Alerts technicians and supervisors about certificates expiring (30, 7 days) and expired. Returns alerts sent.</summary>
    Task<int> RunCertificateAlertsAsync(Guid tenantId, DateTime utcNow, CancellationToken cancellationToken = default);

    /// <summary>Per technician over a period: rostered vs logged hours, overtime, cost, jobs done and the open load.</summary>
    Task<IReadOnlyList<WorkloadRow>> WorkloadAsync(Guid tenantId, IReadOnlyCollection<Guid>? orgNodes, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}

internal sealed class PeopleService(AppDbContext dbContext, INotificationDispatcher? notificationDispatcher = null) : IPeopleService
{
    public const string ExpiringKey = "people.certificate-expiring";

    private static readonly string[] Done = [WorkOrderStatuses.Completed, WorkOrderStatuses.Verified, WorkOrderStatuses.Closed];
    private static readonly string[] Finished = [WorkOrderStatuses.Completed, WorkOrderStatuses.Verified, WorkOrderStatuses.Closed, WorkOrderStatuses.Cancelled, WorkOrderStatuses.Rejected];

    public async Task<int> RunCertificateAlertsAsync(Guid tenantId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var today = utcNow.Date;
        var horizon = today.AddDays(30);
        var certificates = await dbContext.TechnicianCertificates
            .Include(x => x.Technician).Include(x => x.CertificationDefinition)
            .Where(x => x.TenantId == tenantId && x.ExpiresOn != null && x.ExpiresOn <= horizon && x.Technician!.IsActive)
            .ToListAsync(cancellationToken);

        var due = new List<TechnicianCertificate>();
        foreach (var certificate in certificates)
        {
            var days = (int)(certificate.ExpiresOn!.Value.Date - today).TotalDays;
            // Steps: 30 and 7 days ahead, then 0 once expired (sent once).
            int step = days < 0 ? 0 : days <= 7 ? 7 : 30;
            if (certificate.LastAlertDays is null || step < certificate.LastAlertDays)
            {
                certificate.LastAlertDays = step;
                due.Add(certificate);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (notificationDispatcher is null || due.Count == 0)
        {
            return 0;
        }

        var sent = 0;
        foreach (var group in due.GroupBy(x => x.Technician!.HomeOrgNodeId))
        {
            var recipients = new HashSet<Guid>();
            if (group.Key is { } node)
            {
                foreach (var id in await FactoryRecipients.SupervisorsAsync(dbContext, tenantId, node, cancellationToken)) recipients.Add(id);
            }
            else
            {
                foreach (var id in await dbContext.Users.Where(x => x.TenantId == tenantId && x.IsActive && x.Role == Ecosys.Shared.Auth.AppRoles.Admin).Select(x => x.Id).ToListAsync(cancellationToken)) recipients.Add(id);
            }

            foreach (var userId in group.Where(x => x.Technician!.UserId != null).Select(x => x.Technician!.UserId!.Value)) recipients.Add(userId);
            if (recipients.Count == 0) continue;

            var lines = group.OrderBy(x => x.ExpiresOn).Select(x => $"{x.Technician!.FullName}: {x.CertificationDefinition?.Name} {(x.ExpiresOn!.Value.Date < today ? "expired" : "expires")} {x.ExpiresOn:dd MMM yyyy}").ToList();
            await notificationDispatcher.NotifyAsync(new NotificationRequest(
                tenantId, ExpiringKey, recipients.ToList(),
                $"{lines.Count} technician certificate{(lines.Count == 1 ? "" : "s")} expiring",
                string.Join("; ", lines.Take(5)) + (lines.Count > 5 ? $" and {lines.Count - 5} more" : string.Empty),
                "/maintenance/people?tab=certificates", nameof(TechnicianCertificate), null,
                new Dictionary<string, string?> { ["Count"] = lines.Count.ToString(), ["Certificates"] = string.Join("\n", lines) },
                NotificationSeverities.Warning), cancellationToken);
            sent++;
        }

        return sent;
    }

    public async Task<IReadOnlyList<WorkloadRow>> WorkloadAsync(Guid tenantId, IReadOnlyCollection<Guid>? orgNodes, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var technicians = dbContext.Technicians.AsNoTracking().Where(x => x.TenantId == tenantId && x.IsActive);
        if (orgNodes is not null)
        {
            technicians = technicians.Where(x => x.HomeOrgNodeId == null || orgNodes.Contains(x.HomeOrgNodeId.Value));
        }

        var people = await technicians.OrderBy(x => x.FullName).Select(x => new { x.Id, x.FullName, x.Trade, x.UserId }).ToListAsync(cancellationToken);
        var ids = people.Select(x => x.Id).ToList();
        var fromUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var settings = await dbContext.FactorySettings.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        var shifts = ShiftCalendar.Parse(settings?.ShiftsJson);
        decimal ShiftHours(string? name)
        {
            var shift = shifts.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)) ?? shifts[0];
            var hours = (shift.End.ToTimeSpan() - shift.Start.ToTimeSpan()).TotalHours;
            return (decimal)(hours <= 0 ? hours + 24 : hours);
        }

        var roster = await dbContext.RosterEntries.AsNoTracking()
            .Where(x => ids.Contains(x.TechnicianId) && x.WorkDate >= from && x.WorkDate <= to)
            .Select(x => new { x.TechnicianId, x.Kind, x.ShiftName }).ToListAsync(cancellationToken);
        var logs = await dbContext.WorkOrderTimeLogs.AsNoTracking()
            .Where(x => x.TechnicianId != null && ids.Contains(x.TechnicianId.Value) && x.StartedAt >= fromUtc && x.StartedAt < toUtc)
            .Select(x => new { TechnicianId = x.TechnicianId!.Value, x.DurationMinutes, x.IsOvertime, x.LabourCost }).ToListAsync(cancellationToken);
        var completed = await dbContext.WorkOrders.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.ClientId == null && x.LeadTechnicianId != null && ids.Contains(x.LeadTechnicianId.Value)
                && x.CompletedAt >= fromUtc && x.CompletedAt < toUtc && Done.Contains(x.Status))
            .GroupBy(x => x.LeadTechnicianId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        var open = await dbContext.WorkOrderTechnicianAssignments.AsNoTracking()
            .Where(x => ids.Contains(x.TechnicianId) && x.WorkOrder!.ClientId == null && !Finished.Contains(x.WorkOrder.Status))
            .Select(x => new { x.TechnicianId, Hours = x.WorkOrder!.EstimatedLabourHours }).ToListAsync(cancellationToken);
        var leave = await dbContext.LeaveRequests.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Status == "Approved" && x.StartDate <= to && x.EndDate >= from)
            .Select(x => new { x.TechnicianId, x.UserId, x.StartDate, x.EndDate }).ToListAsync(cancellationToken);

        return people.Select(p =>
        {
            var mine = roster.Where(r => r.TechnicianId == p.Id).ToList();
            var rostered = mine.Where(r => r.Kind == RosterKinds.Shift).Sum(r => ShiftHours(r.ShiftName));
            var myLogs = logs.Where(l => l.TechnicianId == p.Id).ToList();
            var logged = Math.Round(myLogs.Sum(l => l.DurationMinutes) / 60m, 1);
            var leaveDays = leave.Where(l => l.TechnicianId == p.Id || (p.UserId != null && l.UserId == p.UserId))
                .Sum(l => (decimal)((l.EndDate < to ? l.EndDate : to).DayNumber - (l.StartDate > from ? l.StartDate : from).DayNumber + 1));
            return new WorkloadRow(
                p.Id, p.FullName, p.Trade, rostered, logged,
                Math.Round(myLogs.Where(l => l.IsOvertime).Sum(l => l.DurationMinutes) / 60m, 1),
                myLogs.Sum(l => l.LabourCost ?? 0),
                completed.GetValueOrDefault(p.Id),
                open.Count(o => o.TechnicianId == p.Id),
                open.Where(o => o.TechnicianId == p.Id).Sum(o => o.Hours ?? 0),
                rostered > 0 ? Math.Round(100 * logged / rostered, 0) : null,
                mine.Count(r => r.Kind == RosterKinds.OnCall),
                leaveDays);
        }).ToList();
    }
}

/// <summary>Sends certificate expiry alerts once a day (checked every 6 hours).</summary>
public sealed class PeopleWorker(IServiceScopeFactory scopeFactory, ILogger<PeopleWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var people = scope.ServiceProvider.GetRequiredService<IPeopleService>();
                var tenants = await db.TechnicianCertificates.AsNoTracking().Where(x => x.ExpiresOn != null && x.Technician!.Tenant!.IsActive)
                    .Select(x => x.TenantId).Distinct().ToListAsync(stoppingToken);
                foreach (var tenantId in tenants)
                {
                    await people.RunCertificateAlertsAsync(tenantId, DateTime.UtcNow, stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Certificate alert cycle failed.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
