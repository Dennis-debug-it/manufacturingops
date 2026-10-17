using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services;

/// <summary>
/// Applies each tenant's "auto clock-out after N hours" setting: a session still open N hours after
/// clock-in is closed at clock-in + N and flagged for the office. Without this, a forgotten clock-out
/// counts every hour until the next morning and blocks that day's clock-in.
/// </summary>
public sealed class AttendanceAutoClockOutWorker(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<AttendanceAutoClockOutWorker> logger) : BackgroundService
{
    private const decimal DefaultLimitHours = 12;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Attendance auto clock-out worker started.");

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
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
            var closed = await CloseOverdueAsync(db, auditLog, DateTime.UtcNow, ct);
            if (closed > 0)
            {
                logger.LogInformation("Attendance auto clock-out closed {Count} session(s).", closed);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Attendance auto clock-out cycle failed.");
        }
    }

    /// <summary>Closes every open session past its tenant's limit; returns how many it closed.</summary>
    public static async Task<int> CloseOverdueAsync(AppDbContext db, IAuditLogService auditLog, DateTime now, CancellationToken ct)
    {
        // No tenant allows less than an hour, so younger sessions are never candidates.
        var open = await db.AttendanceEntries
            .IgnoreQueryFilters()
            .Where(x => x.ClockOutAt == null && x.ClockInAt <= now.AddHours(-1))
            .ToListAsync(ct);
        if (open.Count == 0)
        {
            return 0;
        }

        var tenantIds = open.Select(x => x.TenantId).Distinct().ToList();
        var limits = await db.TimeAttendanceSettings
            .IgnoreQueryFilters()
            .Where(x => tenantIds.Contains(x.TenantId))
            .ToDictionaryAsync(x => x.TenantId, x => x.AutoClockOutAfterHours, ct);

        var closed = new List<AttendanceEntry>();
        foreach (var entry in open)
        {
            var limit = limits.TryGetValue(entry.TenantId, out var configured) ? configured : DefaultLimitHours;
            // Zero (or less) switches auto clock-out off for the tenant.
            if (limit <= 0 || entry.ClockInAt.AddHours((double)limit) > now)
            {
                continue;
            }

            AttendanceRules.AutoClockOut(entry, limit, now);
            closed.Add(entry);
        }

        if (closed.Count == 0)
        {
            return 0;
        }

        await db.SaveChangesAsync(ct);
        var technicianIds = closed.Select(x => x.TechnicianId).Distinct().ToList();
        var names = await db.Technicians
            .IgnoreQueryFilters()
            .Where(x => technicianIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        foreach (var entry in closed)
        {
            await auditLog.LogAsync(
                entry.TenantId,
                null,
                "attendance.auto_clock_out",
                nameof(AttendanceEntry),
                entry.Id.ToString(),
                $"{names.GetValueOrDefault(entry.TechnicianId, "Technician")} was clocked out automatically at {entry.ClockOutAt:yyyy-MM-dd HH:mm} UTC; hours need confirming.",
                severity: "Warning",
                cancellationToken: ct);
        }

        return closed.Count;
    }
}
