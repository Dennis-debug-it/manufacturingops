using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ecosys.Tests;

/// <summary>
/// A technician's own trips keep their clock in step (heading out clocks them in on the job), and a
/// session nobody clocks out of is closed at the tenant's limit and flagged instead of running on.
/// </summary>
public sealed class AttendanceClockTests
{
    private static readonly DateTime Day1 = new(2026, 10, 5, 7, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AutoClockOut_ClosesOverdueSessions_AtTheTenantLimit_AndFlagsThem()
    {
        await using var db = CreateDbContext();
        var now = new DateTime(2026, 10, 8, 9, 0, 0, DateTimeKind.Utc);
        var defaultTenant = Guid.NewGuid();
        var strictTenant = Guid.NewGuid();
        var offTenant = Guid.NewGuid();
        db.TimeAttendanceSettings.AddRange(
            new TimeAttendanceSetting { TenantId = strictTenant, AutoClockOutAfterHours = 8 },
            new TimeAttendanceSetting { TenantId = offTenant, AutoClockOutAfterHours = 0 });
        var forgotten = Open(defaultTenant, now.AddHours(-26), onBreakSince: now.AddHours(-16));
        var recent = Open(defaultTenant, now.AddHours(-2));
        var strict = Open(strictTenant, now.AddHours(-9));
        var off = Open(offTenant, now.AddHours(-40));
        db.AttendanceEntries.AddRange(forgotten, recent, strict, off);
        await db.SaveChangesAsync();

        var closed = await AttendanceAutoClockOutWorker.CloseOverdueAsync(db, new AuditLogService(db, new HttpContextAccessor()), now, CancellationToken.None);

        Assert.Equal(2, closed);
        Assert.Equal(forgotten.ClockInAt.AddHours(12), forgotten.ClockOutAt);
        Assert.Equal(AttendanceRules.ClockedOut, forgotten.Status);
        Assert.Equal(120, forgotten.BreakMinutes); // break ran from -16h to the -14h clock-out
        Assert.True(AttendanceRules.IsAutoClockedOut(forgotten));
        Assert.Equal(strict.ClockInAt.AddHours(8), strict.ClockOutAt);
        Assert.Null(recent.ClockOutAt);
        Assert.Null(off.ClockOutAt);
    }

    [Fact]
    public void ClearingTheFlag_KeepsOtherNotes()
    {
        var entry = new AttendanceEntry { ClockInAt = Day1, Note = "Clocked in automatically: arrived at WO-1" };
        AttendanceRules.AutoClockOut(entry, 12, Day1.AddDays(1));

        Assert.True(AttendanceRules.IsAutoClockedOut(entry));
        Assert.Equal("Clocked in automatically: arrived at WO-1", AttendanceRules.ClearAutoClockOutFlag(entry.Note));
        Assert.Null(AttendanceRules.ClearAutoClockOutFlag($"{AttendanceRules.AutoClockOutMarker} closed"));
    }

    private static AttendanceEntry Open(Guid tenantId, DateTime clockInAt, DateTime? onBreakSince = null) => new()
    {
        TenantId = tenantId, TechnicianId = Guid.NewGuid(), WorkDate = DateOnly.FromDateTime(clockInAt), ClockInAt = clockInAt,
        Status = onBreakSince is null ? AttendanceRules.ClockedIn : AttendanceRules.OnBreak, BreakStartedAt = onBreakSince,
    };

    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"ecosys-attendance-{Guid.NewGuid():N}").Options);
}
