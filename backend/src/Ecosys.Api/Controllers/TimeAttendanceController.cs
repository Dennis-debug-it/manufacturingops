using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/attendance")]
public sealed class TimeAttendanceController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IAuditLogService auditLogService,
    IUserAccessService userAccessService) : TenantAwareControllerBase(tenantContext)
{
    // ── Live board ───────────────────────────────────────────────────────────

    [HttpGet("live")]
    public async Task<ActionResult<AttendanceLiveResponse>> GetLive(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.AttendanceView, PermissionNames.AttendanceManage);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var technicians = await dbContext.Technicians
            .Where(x => x.TenantId == TenantId && x.IsActive)
            .OrderBy(x => x.FullName)
            .ToListAsync(cancellationToken);

        var todayEntries = await dbContext.AttendanceEntries
            .Include(x => x.CurrentWorkOrder)
            .Where(x => x.TenantId == TenantId && x.WorkDate == today)
            .ToListAsync(cancellationToken);

        var entries = technicians.Select(tech =>
        {
            var entry = todayEntries
                .Where(x => x.TechnicianId == tech.Id)
                .OrderByDescending(x => x.ClockInAt)
                .FirstOrDefault();
            var status = entry is null || entry.Status == "ClockedOut" ? "NotClockedIn" : entry.Status;
            return new AttendanceLiveEntryResponse(
                entry?.Id ?? tech.Id,
                tech.Id,
                tech.FullName,
                status,
                status == "NotClockedIn" ? null : entry?.ClockInAt,
                entry?.LastLocation,
                entry?.CurrentWorkOrder?.WorkOrderNumber);
        }).ToList();

        var summary = new AttendanceLiveSummaryResponse(
            entries.Count(x => x.Status == "ClockedIn"),
            entries.Count(x => x.Status == "OnBreak"),
            entries.Count(x => x.Status == "Travelling"),
            entries.Count(x => x.Status == "NotClockedIn"));

        return Ok(new AttendanceLiveResponse(summary, entries));
    }

    // ── Clock actions ────────────────────────────────────────────────────────

    [HttpPost("clock-in")]
    public async Task<ActionResult<AttendanceLiveEntryResponse>> ClockIn(
        [FromBody] ClockActionRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.AttendanceManage);
        var technician = await LoadTechnicianAsync(request.TechnicianId, cancellationToken);

        var open = await FindOpenEntryAsync(technician.Id, cancellationToken);
        if (open is not null)
            throw new BusinessRuleException($"{technician.FullName} is already clocked in.");

        var now = DateTime.UtcNow;
        var entry = new AttendanceEntry
        {
            TenantId = TenantId,
            TechnicianId = technician.Id,
            WorkDate = DateOnly.FromDateTime(now),
            ClockInAt = now,
            Status = "ClockedIn",
            LastLocation = request.Location?.Trim(),
        };
        dbContext.AttendanceEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "attendance.clock_in", nameof(AttendanceEntry), entry.Id.ToString(), $"Clocked in {technician.FullName}", cancellationToken);

        return Ok(new AttendanceLiveEntryResponse(entry.Id, technician.Id, technician.FullName, entry.Status, entry.ClockInAt, entry.LastLocation, null));
    }

    [HttpPost("clock-out")]
    public async Task<IActionResult> ClockOut(
        [FromBody] ClockActionRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.AttendanceManage);
        var technician = await LoadTechnicianAsync(request.TechnicianId, cancellationToken);

        var entry = await FindOpenEntryAsync(technician.Id, cancellationToken)
            ?? throw new BusinessRuleException($"{technician.FullName} is not clocked in.");

        var now = DateTime.UtcNow;
        if (entry.Status == "OnBreak" && entry.BreakStartedAt is not null)
        {
            entry.BreakMinutes += (int)Math.Round((now - entry.BreakStartedAt.Value).TotalMinutes);
            entry.BreakStartedAt = null;
        }
        entry.ClockOutAt = now;
        entry.Status = "ClockedOut";
        entry.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "attendance.clock_out", nameof(AttendanceEntry), entry.Id.ToString(), $"Clocked out {technician.FullName}", cancellationToken);

        return NoContent();
    }

    [HttpPost("status")]
    public async Task<IActionResult> SetStatus(
        [FromBody] AttendanceStatusRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.AttendanceManage);
        var technician = await LoadTechnicianAsync(request.TechnicianId, cancellationToken);

        var allowed = new[] { "ClockedIn", "OnBreak", "Travelling" };
        if (!allowed.Contains(request.Status))
            throw new BusinessRuleException("Status must be ClockedIn, OnBreak, or Travelling.");

        var entry = await FindOpenEntryAsync(technician.Id, cancellationToken)
            ?? throw new BusinessRuleException($"{technician.FullName} is not clocked in.");

        var now = DateTime.UtcNow;
        if (entry.Status == "OnBreak" && request.Status != "OnBreak" && entry.BreakStartedAt is not null)
        {
            entry.BreakMinutes += (int)Math.Round((now - entry.BreakStartedAt.Value).TotalMinutes);
            entry.BreakStartedAt = null;
        }
        if (request.Status == "OnBreak" && entry.Status != "OnBreak")
            entry.BreakStartedAt = now;

        entry.Status = request.Status;
        if (!string.IsNullOrWhiteSpace(request.Location))
            entry.LastLocation = request.Location.Trim();
        entry.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    // ── Timesheets ───────────────────────────────────────────────────────────

    [HttpGet("timesheets")]
    public async Task<ActionResult<IReadOnlyCollection<TimesheetListItemResponse>>> ListTimesheets(
        [FromQuery] string view,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.AttendanceView, PermissionNames.AttendanceManage);

        var (start, end, label) = ResolvePeriod(view, from, to);
        var settings = await GetOrDefaultSettingsAsync(cancellationToken);

        var entries = await dbContext.AttendanceEntries
            .Where(x => x.TenantId == TenantId && x.WorkDate >= start && x.WorkDate <= end)
            .ToListAsync(cancellationToken);

        var approvedLeave = await dbContext.LeaveRequests
            .Where(x => x.TenantId == TenantId && x.Status == "Approved" && x.StartDate <= end && x.EndDate >= start)
            .ToListAsync(cancellationToken);

        var technicians = await dbContext.Technicians
            .Where(x => x.TenantId == TenantId && x.IsActive)
            .OrderBy(x => x.FullName)
            .ToListAsync(cancellationToken);

        var items = technicians.Select(tech =>
        {
            var techEntries = entries.Where(x => x.TechnicianId == tech.Id).ToList();
            var (total, overtime) = ComputeHours(techEntries, settings.OvertimeThresholdHoursPerDay);
            var leaveDays = approvedLeave
                .Where(x => x.TechnicianId == tech.Id)
                .Sum(x => OverlapDays(x.StartDate, x.EndDate, start, end));
            var leaveHours = (double)(leaveDays * settings.OvertimeThresholdHoursPerDay);
            var status = overtime > 0 ? "Flagged" : "Pending";
            return new
            {
                HasActivity = techEntries.Count > 0 || leaveDays > 0,
                Item = new TimesheetListItemResponse(tech.Id, tech.Id, tech.FullName, label,
                    Math.Round(total, 2), Math.Round(overtime, 2), Math.Round(leaveHours, 2), status),
            };
        })
        .Where(x => x.HasActivity)
        .Select(x => x.Item)
        .ToList();

        return Ok(items);
    }

    [HttpGet("timesheets/{technicianId:guid}")]
    public async Task<ActionResult<TimesheetDetailResponse>> GetTimesheetDetail(
        Guid technicianId,
        [FromQuery] string? view,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.AttendanceView, PermissionNames.AttendanceManage);

        var technician = await LoadTechnicianAsync(technicianId, cancellationToken);
        return Ok(await BuildTimesheetDetailAsync(technician, view, from, to, cancellationToken));
    }

    // ── Leave ────────────────────────────────────────────────────────────────

    [HttpGet("leave")]
    public async Task<ActionResult<IReadOnlyCollection<LeaveRequestResponse>>> ListLeave(
        [FromQuery] string? status, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.AttendanceView, PermissionNames.AttendanceManage);

        var query = dbContext.LeaveRequests
            .Include(x => x.Technician)
            .Where(x => x.TenantId == TenantId);
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.Status == status.Trim());

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        return Ok(items.Select(MapLeave).ToList());
    }

    [HttpPost("leave")]
    public async Task<ActionResult<LeaveRequestResponse>> CreateLeave(
        [FromBody] CreateLeaveRequestRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.AttendanceManage);
        var technician = await LoadTechnicianAsync(request.TechnicianId, cancellationToken);

        if (request.EndDate < request.StartDate)
            throw new BusinessRuleException("End date cannot be before start date.");
        if (string.IsNullOrWhiteSpace(request.LeaveType))
            throw new BusinessRuleException("Leave type is required.");

        var leave = new LeaveRequest
        {
            TenantId = TenantId,
            TechnicianId = technician.Id,
            LeaveTypeCode = request.LeaveType.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            TotalDays = request.EndDate.DayNumber - request.StartDate.DayNumber + 1,
            Reason = request.Reason?.Trim(),
        };
        dbContext.LeaveRequests.Add(leave);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "attendance.leave.requested", nameof(LeaveRequest), leave.Id.ToString(), $"Leave requested for {technician.FullName} ({leave.LeaveTypeCode})", cancellationToken);

        leave.Technician = technician;
        return Ok(MapLeave(leave));
    }

    [HttpPost("leave/{id:guid}/decision")]
    public async Task<IActionResult> DecideLeave(
        Guid id, [FromBody] LeaveDecisionRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.AttendanceManage);

        var leave = await dbContext.LeaveRequests
            .Include(x => x.Technician)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Leave request not found.");

        if (leave.Status != "Pending")
            throw new BusinessRuleException("Only pending leave requests can be decided.");

        leave.Status = request.Approve ? "Approved" : "Rejected";
        leave.DecisionNote = request.Note?.Trim();
        leave.DecidedByUserId = UserId;
        leave.DecidedAt = DateTime.UtcNow;
        leave.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "attendance.leave.decided", nameof(LeaveRequest), leave.Id.ToString(), $"Leave {leave.Status.ToLowerInvariant()} for {leave.Technician?.FullName}", cancellationToken);

        return NoContent();
    }

    // ── Self-service (technician) ─────────────────────────────────────────────
    // These endpoints are available to any authenticated user and always act on the
    // caller's own linked technician profile. Approvals stay with managers (DecideLeave).

    [HttpGet("leave-types")]
    public async Task<ActionResult<IReadOnlyList<LeaveTypeModel>>> ListLeaveTypes(CancellationToken cancellationToken)
        => Ok(await GetOrDefaultLeaveTypesAsync(cancellationToken));

    [HttpGet("leave/mine")]
    public async Task<ActionResult<IReadOnlyCollection<LeaveRequestResponse>>> ListMyLeave(CancellationToken cancellationToken)
    {
        var technician = await ResolveCallerTechnicianAsync(cancellationToken);
        var items = await dbContext.LeaveRequests
            .Include(x => x.Technician)
            .Where(x => x.TenantId == TenantId && x.TechnicianId == technician.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(MapLeave).ToList());
    }

    [HttpPost("leave/mine")]
    public async Task<ActionResult<LeaveRequestResponse>> CreateMyLeave(
        [FromBody] CreateMyLeaveRequest request, CancellationToken cancellationToken)
    {
        var technician = await ResolveCallerTechnicianAsync(cancellationToken);

        if (request.EndDate < request.StartDate)
            throw new BusinessRuleException("End date cannot be before start date.");
        if (string.IsNullOrWhiteSpace(request.LeaveType))
            throw new BusinessRuleException("Leave type is required.");

        var leave = new LeaveRequest
        {
            TenantId = TenantId,
            TechnicianId = technician.Id,
            LeaveTypeCode = request.LeaveType.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            TotalDays = request.EndDate.DayNumber - request.StartDate.DayNumber + 1,
            Reason = request.Reason?.Trim(),
        };
        dbContext.LeaveRequests.Add(leave);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "attendance.leave.requested", nameof(LeaveRequest), leave.Id.ToString(), $"Leave self-requested by {technician.FullName} ({leave.LeaveTypeCode})", cancellationToken);

        leave.Technician = technician;
        return Ok(MapLeave(leave));
    }

    [HttpGet("me/status")]
    public async Task<ActionResult<MyAttendanceStatusResponse>> GetMyStatus(CancellationToken cancellationToken)
    {
        var technician = await ResolveCallerTechnicianAsync(cancellationToken);
        var open = await FindOpenEntryAsync(technician.Id, cancellationToken);
        var status = open is null || open.Status == "ClockedOut" ? "NotClockedIn" : open.Status;
        return Ok(new MyAttendanceStatusResponse(
            technician.Id, technician.FullName, status,
            status == "NotClockedIn" ? null : open?.ClockInAt, open?.LastLocation));
    }

    [HttpPost("me/clock-in")]
    public async Task<ActionResult<MyAttendanceStatusResponse>> ClockInMine(
        [FromBody] SelfClockRequest request, CancellationToken cancellationToken)
    {
        var technician = await ResolveCallerTechnicianAsync(cancellationToken);
        if (await FindOpenEntryAsync(technician.Id, cancellationToken) is not null)
            throw new BusinessRuleException("You are already clocked in.");

        var now = DateTime.UtcNow;
        var entry = new AttendanceEntry
        {
            TenantId = TenantId,
            TechnicianId = technician.Id,
            WorkDate = DateOnly.FromDateTime(now),
            ClockInAt = now,
            Status = "ClockedIn",
            LastLocation = request.Location?.Trim(),
        };
        dbContext.AttendanceEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "attendance.clock_in", nameof(AttendanceEntry), entry.Id.ToString(), $"{technician.FullName} clocked in", cancellationToken);

        return Ok(new MyAttendanceStatusResponse(technician.Id, technician.FullName, entry.Status, entry.ClockInAt, entry.LastLocation));
    }

    [HttpPost("me/clock-out")]
    public async Task<ActionResult<MyAttendanceStatusResponse>> ClockOutMine(CancellationToken cancellationToken)
    {
        var technician = await ResolveCallerTechnicianAsync(cancellationToken);
        var entry = await FindOpenEntryAsync(technician.Id, cancellationToken)
            ?? throw new BusinessRuleException("You are not clocked in.");

        var now = DateTime.UtcNow;
        if (entry.Status == "OnBreak" && entry.BreakStartedAt is not null)
        {
            entry.BreakMinutes += (int)Math.Round((now - entry.BreakStartedAt.Value).TotalMinutes);
            entry.BreakStartedAt = null;
        }
        entry.ClockOutAt = now;
        entry.Status = "ClockedOut";
        entry.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "attendance.clock_out", nameof(AttendanceEntry), entry.Id.ToString(), $"{technician.FullName} clocked out", cancellationToken);

        return Ok(new MyAttendanceStatusResponse(technician.Id, technician.FullName, "NotClockedIn", null, entry.LastLocation));
    }

    [HttpPost("me/status")]
    public async Task<ActionResult<MyAttendanceStatusResponse>> SetMyStatus(
        [FromBody] SelfStatusRequest request, CancellationToken cancellationToken)
    {
        var technician = await ResolveCallerTechnicianAsync(cancellationToken);
        var allowed = new[] { "ClockedIn", "OnBreak", "Travelling" };
        if (!allowed.Contains(request.Status))
            throw new BusinessRuleException("Status must be ClockedIn, OnBreak, or Travelling.");

        var entry = await FindOpenEntryAsync(technician.Id, cancellationToken)
            ?? throw new BusinessRuleException("You are not clocked in.");

        var now = DateTime.UtcNow;
        if (entry.Status == "OnBreak" && request.Status != "OnBreak" && entry.BreakStartedAt is not null)
        {
            entry.BreakMinutes += (int)Math.Round((now - entry.BreakStartedAt.Value).TotalMinutes);
            entry.BreakStartedAt = null;
        }
        if (request.Status == "OnBreak" && entry.Status != "OnBreak")
            entry.BreakStartedAt = now;

        entry.Status = request.Status;
        if (!string.IsNullOrWhiteSpace(request.Location))
            entry.LastLocation = request.Location.Trim();
        entry.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new MyAttendanceStatusResponse(technician.Id, technician.FullName, entry.Status, entry.ClockInAt, entry.LastLocation));
    }

    [HttpGet("timesheets/mine")]
    public async Task<ActionResult<TimesheetDetailResponse>> GetMyTimesheet(
        [FromQuery] string? view, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        var technician = await ResolveCallerTechnicianAsync(cancellationToken);
        return Ok(await BuildTimesheetDetailAsync(technician, view, from, to, cancellationToken));
    }

    // ── Reports ──────────────────────────────────────────────────────────────

    [HttpGet("reports/summary")]
    public async Task<ActionResult<AttendanceReportSummaryResponse>> GetReportSummary(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.AttendanceView, PermissionNames.AttendanceManage);

        var now = DateTime.UtcNow;
        var start = new DateOnly(now.Year, now.Month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        var settings = await GetOrDefaultSettingsAsync(cancellationToken);

        var entries = await dbContext.AttendanceEntries
            .Where(x => x.TenantId == TenantId && x.WorkDate >= start && x.WorkDate <= end)
            .ToListAsync(cancellationToken);

        var technicianCount = await dbContext.Technicians
            .CountAsync(x => x.TenantId == TenantId && x.IsActive, cancellationToken);

        var (worked, overtime) = ComputeHours(entries, settings.OvertimeThresholdHoursPerDay);

        var leaveDays = await dbContext.LeaveRequests
            .Where(x => x.TenantId == TenantId && x.Status == "Approved" && x.StartDate <= end && x.EndDate >= start)
            .ToListAsync(cancellationToken);
        var leaveDaysTaken = leaveDays.Sum(x => OverlapDays(x.StartDate, x.EndDate, start, end));

        // Late = clocked in after 08:00 local plus grace, judged on the UTC clock-in hour.
        var lateArrivals = entries.Count(x =>
            x.ClockInAt.TimeOfDay > new TimeSpan(8, 0, 0).Add(TimeSpan.FromMinutes(settings.GraceMinutes)));

        var scheduled = (double)(BusinessDays(start, end) * settings.OvertimeThresholdHoursPerDay * technicianCount);

        return Ok(new AttendanceReportSummaryResponse(
            now.ToString("MMMM yyyy"),
            Math.Round(scheduled, 1),
            Math.Round(worked, 1),
            Math.Round(overtime, 1),
            Math.Round((double)leaveDaysTaken, 1),
            lateArrivals));
    }

    // ── Settings ─────────────────────────────────────────────────────────────

    [HttpGet("/api/settings/time-attendance")]
    public async Task<ActionResult<TimeAttendanceSettingsResponse>> GetSettings(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.AttendanceView, PermissionNames.AttendanceManage, PermissionNames.CanManageSettings);

        var setting = await GetOrDefaultSettingsAsync(cancellationToken);
        var leaveTypes = await GetOrDefaultLeaveTypesAsync(cancellationToken);
        return Ok(new TimeAttendanceSettingsResponse(
            setting.RoundingMinutes,
            setting.OvertimeThresholdHoursPerDay,
            setting.GraceMinutes,
            setting.AutoClockOutAfterHours,
            leaveTypes));
    }

    [HttpPut("/api/settings/time-attendance")]
    public async Task<IActionResult> SaveSettings(
        [FromBody] TimeAttendanceSettingsResponse request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.AttendanceManage, PermissionNames.CanManageSettings);

        if (request.RoundingMinutes is < 0 or > 60)
            throw new BusinessRuleException("Rounding minutes must be between 0 and 60.");
        if (request.OvertimeThresholdHoursPerDay is <= 0 or > 24)
            throw new BusinessRuleException("Overtime threshold must be between 1 and 24 hours.");

        var setting = await dbContext.TimeAttendanceSettings
            .SingleOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken);
        if (setting is null)
        {
            setting = new TimeAttendanceSetting { TenantId = TenantId };
            dbContext.TimeAttendanceSettings.Add(setting);
        }
        setting.RoundingMinutes = request.RoundingMinutes;
        setting.OvertimeThresholdHoursPerDay = request.OvertimeThresholdHoursPerDay;
        setting.GraceMinutes = request.GraceMinutes;
        setting.AutoClockOutAfterHours = request.AutoClockOutAfterHours;
        setting.UpdatedAt = DateTime.UtcNow;

        var existing = await dbContext.LeaveTypes
            .Where(x => x.TenantId == TenantId)
            .ToListAsync(cancellationToken);
        var requestedCodes = request.LeaveTypes.Select(x => x.Code.Trim().ToUpperInvariant()).ToHashSet();

        foreach (var leaveType in existing)
            leaveType.IsActive = requestedCodes.Contains(leaveType.Code.ToUpperInvariant());

        foreach (var requested in request.LeaveTypes)
        {
            var code = requested.Code.Trim();
            if (code.Length == 0)
                throw new BusinessRuleException("Leave type code is required.");
            var match = existing.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                dbContext.LeaveTypes.Add(new LeaveType
                {
                    TenantId = TenantId,
                    Code = code.ToUpperInvariant(),
                    Name = requested.Name.Trim(),
                    IsPaid = requested.IsPaid,
                    AnnualAllowanceDays = requested.AnnualAllowanceDays,
                });
            }
            else
            {
                match.Name = requested.Name.Trim();
                match.IsPaid = requested.IsPaid;
                match.AnnualAllowanceDays = requested.AnnualAllowanceDays;
                match.IsActive = true;
                match.UpdatedAt = DateTime.UtcNow;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "attendance.settings.updated", nameof(TimeAttendanceSetting), setting.Id.ToString(), "Time & attendance settings updated", cancellationToken);

        return NoContent();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<Technician> LoadTechnicianAsync(Guid technicianId, CancellationToken cancellationToken) =>
        await dbContext.Technicians
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == technicianId, cancellationToken)
        ?? throw new NotFoundException("Technician not found.");

    private async Task<Technician> ResolveCallerTechnicianAsync(CancellationToken cancellationToken) =>
        await dbContext.Technicians
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.UserId == UserId, cancellationToken)
        ?? throw new BusinessRuleException("Your account is not linked to a technician profile. Ask an administrator to link it before using time & attendance.");

    private async Task<TimesheetDetailResponse> BuildTimesheetDetailAsync(
        Technician technician, string? view, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var (start, end, label) = ResolvePeriod(view ?? "week", from, to);
        var settings = await GetOrDefaultSettingsAsync(cancellationToken);

        var entries = await dbContext.AttendanceEntries
            .Where(x => x.TenantId == TenantId && x.TechnicianId == technician.Id && x.WorkDate >= start && x.WorkDate <= end)
            .OrderBy(x => x.WorkDate).ThenBy(x => x.ClockInAt)
            .ToListAsync(cancellationToken);

        var (total, overtime) = ComputeHours(entries, settings.OvertimeThresholdHoursPerDay);

        var approvedLeaveDays = await dbContext.LeaveRequests
            .Where(x => x.TenantId == TenantId && x.TechnicianId == technician.Id && x.Status == "Approved"
                && x.StartDate <= end && x.EndDate >= start)
            .ToListAsync(cancellationToken);
        var leaveHours = (double)(approvedLeaveDays.Sum(x => OverlapDays(x.StartDate, x.EndDate, start, end)) * settings.OvertimeThresholdHoursPerDay);

        var daily = entries
            .GroupBy(x => x.WorkDate)
            .Select(g =>
            {
                var dayHours = g.Sum(EntryHours);
                var first = g.OrderBy(x => x.ClockInAt).First();
                var last = g.OrderByDescending(x => x.ClockInAt).First();
                return new TimesheetDailyEntryResponse(
                    g.Key.ToString("yyyy-MM-dd"),
                    first.ClockInAt,
                    last.ClockOutAt,
                    g.Sum(x => x.BreakMinutes),
                    Math.Round(dayHours, 2),
                    first.Note);
            })
            .OrderBy(x => x.Date)
            .ToList();

        var audit = entries.SelectMany(entry =>
        {
            var events = new List<TimesheetAuditEntryResponse>
            {
                new(entry.ClockInAt, technician.FullName, $"Clocked in ({entry.WorkDate:dd MMM})"),
            };
            if (entry.ClockOutAt is not null)
                events.Add(new(entry.ClockOutAt.Value, technician.FullName, $"Clocked out ({entry.WorkDate:dd MMM})"));
            return events;
        })
        .OrderBy(x => x.At)
        .ToList();

        var status = overtime > 0 ? "Flagged" : "Pending";
        return new TimesheetDetailResponse(
            technician.Id, technician.Id, technician.FullName, label,
            Math.Round(total, 2), Math.Round(overtime, 2), Math.Round(leaveHours, 2), status,
            daily, audit);
    }

    private async Task<AttendanceEntry?> FindOpenEntryAsync(Guid technicianId, CancellationToken cancellationToken) =>
        await dbContext.AttendanceEntries
            .Where(x => x.TenantId == TenantId && x.TechnicianId == technicianId && x.ClockOutAt == null)
            .OrderByDescending(x => x.ClockInAt)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<TimeAttendanceSetting> GetOrDefaultSettingsAsync(CancellationToken cancellationToken) =>
        await dbContext.TimeAttendanceSettings
            .SingleOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken)
        ?? new TimeAttendanceSetting { TenantId = TenantId };

    private async Task<IReadOnlyList<LeaveTypeModel>> GetOrDefaultLeaveTypesAsync(CancellationToken cancellationToken)
    {
        var stored = await dbContext.LeaveTypes
            .Where(x => x.TenantId == TenantId && x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        if (stored.Count > 0)
            return stored.Select(x => new LeaveTypeModel(x.Code, x.Name, x.IsPaid, x.AnnualAllowanceDays)).ToList();

        return
        [
            new LeaveTypeModel("ANNUAL", "Annual Leave", true, 21),
            new LeaveTypeModel("SICK", "Sick Leave", true, 14),
            new LeaveTypeModel("UNPAID", "Unpaid Leave", false, 0),
        ];
    }

    private static double EntryHours(AttendanceEntry entry)
    {
        var endAt = entry.ClockOutAt ?? DateTime.UtcNow;
        var hours = (endAt - entry.ClockInAt).TotalHours - entry.BreakMinutes / 60.0;
        return Math.Max(0, hours);
    }

    private static (double Total, double Overtime) ComputeHours(IReadOnlyCollection<AttendanceEntry> entries, decimal thresholdPerDay)
    {
        var total = 0.0;
        var overtime = 0.0;
        foreach (var day in entries.GroupBy(x => new { x.TechnicianId, x.WorkDate }))
        {
            var dayHours = day.Sum(EntryHours);
            total += dayHours;
            overtime += Math.Max(0, dayHours - (double)thresholdPerDay);
        }
        return (total, overtime);
    }

    private static decimal OverlapDays(DateOnly start, DateOnly end, DateOnly periodStart, DateOnly periodEnd)
    {
        var overlapStart = start > periodStart ? start : periodStart;
        var overlapEnd = end < periodEnd ? end : periodEnd;
        return overlapEnd < overlapStart ? 0 : overlapEnd.DayNumber - overlapStart.DayNumber + 1;
    }

    private static int BusinessDays(DateOnly start, DateOnly end)
    {
        var days = 0;
        for (var date = start; date <= end; date = date.AddDays(1))
            if (date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                days++;
        return days;
    }

    private static (DateOnly Start, DateOnly End, string Label) ResolvePeriod(string view, DateOnly? from, DateOnly? to)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        switch (view.ToLowerInvariant())
        {
            case "month":
            {
                var start = new DateOnly(today.Year, today.Month, 1);
                var end = start.AddMonths(1).AddDays(-1);
                return (start, end, start.ToString("MMMM yyyy"));
            }
            case "custom":
            {
                if (from is null || to is null)
                    throw new BusinessRuleException("Custom view requires from and to dates.");
                if (to < from)
                    throw new BusinessRuleException("End date cannot be before start date.");
                return (from.Value, to.Value, $"{from:dd MMM} – {to:dd MMM yyyy}");
            }
            default:
            {
                var offset = ((int)today.DayOfWeek + 6) % 7; // Monday = start of week
                var start = today.AddDays(-offset);
                var end = start.AddDays(6);
                return (start, end, $"Week of {start:dd MMM yyyy}");
            }
        }
    }

    private static LeaveRequestResponse MapLeave(LeaveRequest leave) => new(
        leave.Id,
        leave.TechnicianId,
        leave.Technician?.FullName ?? "Unknown",
        leave.LeaveTypeCode,
        leave.StartDate.ToString("yyyy-MM-dd"),
        leave.EndDate.ToString("yyyy-MM-dd"),
        leave.TotalDays,
        leave.Status,
        leave.Reason,
        leave.CreatedAt);
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public sealed record AttendanceLiveEntryResponse(
    Guid Id, Guid TechnicianId, string TechnicianName, string Status,
    DateTime? ClockInAt, string? LastLocation, string? CurrentWorkOrderNumber);

public sealed record AttendanceLiveSummaryResponse(int ClockedIn, int OnBreak, int Travelling, int NotClockedIn);

public sealed record AttendanceLiveResponse(
    AttendanceLiveSummaryResponse Summary,
    IReadOnlyList<AttendanceLiveEntryResponse> Entries);

public sealed record ClockActionRequest(Guid TechnicianId, string? Location = null);

public sealed record AttendanceStatusRequest(Guid TechnicianId, string Status, string? Location = null);

public sealed record TimesheetListItemResponse(
    Guid Id, Guid TechnicianId, string TechnicianName, string PeriodLabel,
    double TotalHours, double OvertimeHours, double LeaveHours, string Status);

public sealed record TimesheetDailyEntryResponse(
    string Date, DateTime? ClockInAt, DateTime? ClockOutAt, int BreakMinutes, double TotalHours, string? Note);

public sealed record TimesheetAuditEntryResponse(DateTime At, string Actor, string Action);

public sealed record TimesheetDetailResponse(
    Guid Id, Guid TechnicianId, string TechnicianName, string PeriodLabel,
    double TotalHours, double OvertimeHours, double LeaveHours, string Status,
    IReadOnlyList<TimesheetDailyEntryResponse> DailyBreakdown,
    IReadOnlyList<TimesheetAuditEntryResponse> AuditTrail);

public sealed record LeaveRequestResponse(
    Guid Id, Guid TechnicianId, string TechnicianName, string LeaveType,
    string StartDate, string EndDate, decimal TotalDays, string Status,
    string? Reason, DateTime RequestedAt);

public sealed record CreateLeaveRequestRequest(
    Guid TechnicianId, string LeaveType, DateOnly StartDate, DateOnly EndDate, string? Reason = null);

public sealed record CreateMyLeaveRequest(
    string LeaveType, DateOnly StartDate, DateOnly EndDate, string? Reason = null);

public sealed record SelfClockRequest(string? Location = null);

public sealed record SelfStatusRequest(string Status, string? Location = null);

public sealed record MyAttendanceStatusResponse(
    Guid TechnicianId, string TechnicianName, string Status, DateTime? ClockInAt, string? LastLocation);

public sealed record LeaveDecisionRequest(bool Approve, string? Note = null);

public sealed record AttendanceReportSummaryResponse(
    string PeriodLabel, double TotalScheduledHours, double TotalWorkedHours,
    double TotalOvertimeHours, double LeaveDaysTaken, int LateArrivals);

public sealed record LeaveTypeModel(string Code, string Name, bool IsPaid, decimal AnnualAllowanceDays);

public sealed record TimeAttendanceSettingsResponse(
    int RoundingMinutes, decimal OvertimeThresholdHoursPerDay, int GraceMinutes,
    decimal AutoClockOutAfterHours, IReadOnlyList<LeaveTypeModel> LeaveTypes);
