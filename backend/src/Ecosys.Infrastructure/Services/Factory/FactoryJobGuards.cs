using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services.Factory;

/// <summary>
/// The people and safety rules on a job (scope Phase 4): who may be assigned (certificate, leave,
/// roster), when work may start (approved permits) and finish (permits closed, locks off), and what
/// an hour of someone's time costs (overtime outside their rostered shift).
/// </summary>
public sealed class FactoryJobGuards(AppDbContext dbContext)
{
    private static readonly string[] OpenPermit = [PermitStatuses.Requested, PermitStatuses.Approved];

    public static IReadOnlyList<string> PermitTypesOf(string? value) =>
        (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(PermitTypes.Find).Where(x => x is not null).Select(x => x!).Distinct().ToList();

    public static string? NormalisePermitTypes(IEnumerable<string>? types)
    {
        var list = new List<string>();
        foreach (var type in types ?? [])
        {
            list.Add(PermitTypes.Find(type) ?? throw new BusinessRuleException($"Permit type must be one of: {string.Join(", ", PermitTypes.All)}."));
        }

        return list.Count == 0 ? null : string.Join(",", list.Distinct());
    }

    public static string Describe(string permitType) => permitType switch
    {
        PermitTypes.HotWork => "hot work",
        PermitTypes.ConfinedSpace => "confined space",
        PermitTypes.Electrical => "electrical",
        PermitTypes.WorkingAtHeights => "working at heights",
        _ => "general",
    };

    /// <summary>Blocks technicians without the job's certificate, on approved leave, or rostered off on the day.</summary>
    public async Task EnsureCanAssignAsync(WorkOrder workOrder, IReadOnlyList<Technician> technicians, DateTime utcNow, CancellationToken cancellationToken)
    {
        var day = DateOnly.FromDateTime((workOrder.ScheduledStartAt ?? utcNow).Date);
        var ids = technicians.Select(x => x.Id).ToList();

        if (workOrder.RequiredCertificationId is { } certificationId)
        {
            var name = await dbContext.CertificationDefinitions.Where(x => x.Id == certificationId).Select(x => x.Name).SingleOrDefaultAsync(cancellationToken) ?? "the required certificate";
            var at = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var holders = await dbContext.TechnicianCertificates
                .Where(x => ids.Contains(x.TechnicianId) && x.CertificationDefinitionId == certificationId && (x.ExpiresOn == null || x.ExpiresOn >= at))
                .Select(x => x.TechnicianId).ToListAsync(cancellationToken);
            var missing = technicians.Where(x => !holders.Contains(x.Id)).Select(x => x.FullName).ToList();
            if (missing.Count > 0)
            {
                throw new BusinessRuleException($"{string.Join(", ", missing)} {(missing.Count == 1 ? "has" : "have")} no valid {name}, which this job needs.");
            }
        }

        var userIds = technicians.Where(x => x.UserId != null).Select(x => x.UserId!.Value).ToList();
        var onLeave = await dbContext.LeaveRequests
            .Where(x => x.TenantId == workOrder.TenantId && x.Status == "Approved" && x.StartDate <= day && x.EndDate >= day
                && ((x.TechnicianId != null && ids.Contains(x.TechnicianId.Value)) || (x.UserId != null && userIds.Contains(x.UserId.Value))))
            .Select(x => new { x.TechnicianId, x.UserId }).ToListAsync(cancellationToken);
        var leaveNames = technicians.Where(t => onLeave.Any(l => l.TechnicianId == t.Id || (t.UserId != null && l.UserId == t.UserId))).Select(x => x.FullName).ToList();
        if (leaveNames.Count > 0)
        {
            throw new BusinessRuleException($"{string.Join(", ", leaveNames)} {(leaveNames.Count == 1 ? "is" : "are")} on leave on {day:dd MMM}.");
        }

        // Breakdowns can call out someone on call; nobody is assigned on a day they are rostered off.
        var off = await dbContext.RosterEntries
            .Where(x => ids.Contains(x.TechnicianId) && x.WorkDate == day && x.Kind == RosterKinds.Off)
            .Select(x => x.TechnicianId).ToListAsync(cancellationToken);
        var offNames = technicians.Where(x => off.Contains(x.Id)).Select(x => x.FullName).ToList();
        if (offNames.Count > 0)
        {
            throw new BusinessRuleException($"{string.Join(", ", offNames)} {(offNames.Count == 1 ? "is" : "are")} rostered off on {day:dd MMM}.");
        }
    }

    /// <summary>Work needing permits starts only under an approved permit of each type, valid now.</summary>
    public async Task EnsureCanStartAsync(WorkOrder workOrder, DateTime utcNow, CancellationToken cancellationToken)
    {
        var needed = PermitTypesOf(workOrder.RequiredPermitTypes);
        if (needed.Count == 0)
        {
            return;
        }

        var valid = await dbContext.WorkPermits
            .Where(x => x.WorkOrderId == workOrder.Id && x.Status == PermitStatuses.Approved && x.ValidFrom <= utcNow && x.ValidTo >= utcNow)
            .Select(x => x.PermitType).ToListAsync(cancellationToken);
        var missing = needed.Where(x => !valid.Contains(x)).Select(Describe).ToList();
        if (missing.Count > 0)
        {
            throw new BusinessRuleException($"This job needs an approved {string.Join(" and ", missing)} permit, valid now, before work starts.");
        }
    }

    /// <summary>A job is finished only when its permits are closed out and its locks removed.</summary>
    public async Task EnsureCanCompleteAsync(WorkOrder workOrder, CancellationToken cancellationToken)
    {
        var locks = await dbContext.LotoLocks.CountAsync(x => x.WorkOrderId == workOrder.Id && x.RemovedAt == null, cancellationToken);
        if (locks > 0)
        {
            throw new BusinessRuleException($"Remove the {locks} isolation lock{(locks == 1 ? "" : "s")} first.");
        }

        var permits = await dbContext.WorkPermits.CountAsync(x => x.WorkOrderId == workOrder.Id && x.Status == PermitStatuses.Approved, cancellationToken);
        if (permits > 0)
        {
            throw new BusinessRuleException("Close out the permit to work first.");
        }
    }

    /// <summary>
    /// Whether time starting then is overtime for the technician (outside their rostered shift, or
    /// on a day off or on call), and what it costs at their rate.
    /// </summary>
    public async Task<(bool Overtime, decimal? Cost)> LabourAsync(Technician technician, DateTime startedAtUtc, int minutes, CancellationToken cancellationToken)
    {
        var settings = await dbContext.FactorySettings.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == technician.TenantId, cancellationToken);
        var zoneId = await dbContext.Tenants.Where(x => x.Id == technician.TenantId).Select(x => x.TimeZoneId).SingleOrDefaultAsync(cancellationToken);
        TimeZoneInfo zone;
        try { zone = string.IsNullOrWhiteSpace(zoneId) ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(zoneId); }
        catch (TimeZoneNotFoundException) { zone = TimeZoneInfo.Utc; }

        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(startedAtUtc, DateTimeKind.Utc), zone);
        var day = DateOnly.FromDateTime(local);
        // A night shift that started yesterday still covers the small hours.
        var entries = await dbContext.RosterEntries.AsNoTracking()
            .Where(x => x.TechnicianId == technician.Id && (x.WorkDate == day || x.WorkDate == day.AddDays(-1)))
            .ToListAsync(cancellationToken);
        var overtime = false;
        var today = entries.FirstOrDefault(x => x.WorkDate == day);
        if (entries.Count > 0)
        {
            var shifts = ShiftCalendar.Parse(settings?.ShiftsJson);
            bool Covers(RosterEntry entry, DateOnly date)
            {
                if (entry.Kind != RosterKinds.Shift) return false;
                var shift = shifts.FirstOrDefault(s => string.Equals(s.Name, entry.ShiftName, StringComparison.OrdinalIgnoreCase)) ?? shifts[0];
                var start = date.ToDateTime(shift.Start);
                var end = shift.Overnight ? date.AddDays(1).ToDateTime(shift.End) : date.ToDateTime(shift.End);
                return local >= start && local < end;
            }

            var yesterday = entries.FirstOrDefault(x => x.WorkDate == day.AddDays(-1));
            overtime = !((today is not null && Covers(today, day)) || (yesterday is not null && Covers(yesterday, day.AddDays(-1))));
        }

        decimal? cost = technician.HourlyRate is { } rate
            ? Math.Round(minutes / 60m * rate * (overtime ? technician.OvertimeMultiplier : 1m), 2)
            : null;
        return (overtime, cost);
    }
}
