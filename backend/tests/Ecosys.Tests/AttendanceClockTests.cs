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
    public async Task OwnTrip_ClocksTheTechnicianIn_OnThatJob_AndDepartureLeavesThemClockedIn()
    {
        await using var db = CreateDbContext();
        var seed = await SeedAsync(db);
        var workflow = CreateWorkflow(db);

        await workflow.RecordInTransitAsync(seed.TenantId, seed.WorkOrderId, seed.UserId, new(seed.TechnicianId, null, null, Day1, null));
        var entry = await db.AttendanceEntries.SingleAsync();
        Assert.Equal(AttendanceRules.Travelling, entry.Status);
        Assert.Equal(Day1, entry.ClockInAt);
        Assert.Equal(seed.WorkOrderId, entry.CurrentWorkOrderId);
        Assert.Contains("on the way to WO-CLOCK-1", entry.Note);

        await workflow.RecordArrivalAsync(seed.TenantId, seed.WorkOrderId, seed.UserId, new(seed.TechnicianId, null, null, Day1.AddMinutes(40), null));
        entry = await db.AttendanceEntries.SingleAsync();
        Assert.Equal(AttendanceRules.ClockedIn, entry.Status);

        await workflow.RecordDepartureAsync(seed.TenantId, seed.WorkOrderId, seed.UserId, new(seed.TechnicianId, null, null, Day1.AddHours(8), null));
        entry = await db.AttendanceEntries.SingleAsync();
        Assert.Null(entry.ClockOutAt);
        Assert.Null(entry.CurrentWorkOrderId);
    }

    [Fact]
    public async Task OfficeRecordingATrip_LeavesTheTechniciansClockAlone()
    {
        await using var db = CreateDbContext();
        var seed = await SeedAsync(db);

        await CreateWorkflow(db).RecordArrivalAsync(seed.TenantId, seed.WorkOrderId, Guid.NewGuid(), new(seed.TechnicianId, null, null, Day1, null));

        Assert.Empty(await db.AttendanceEntries.ToListAsync());
    }

    [Fact]
    public async Task ArrivingWhileOnBreak_EndsTheBreak_AndUsesTheOpenSession()
    {
        await using var db = CreateDbContext();
        var seed = await SeedAsync(db);
        db.AttendanceEntries.Add(new AttendanceEntry
        {
            TenantId = seed.TenantId, TechnicianId = seed.TechnicianId, WorkDate = DateOnly.FromDateTime(Day1),
            ClockInAt = Day1, Status = AttendanceRules.OnBreak, BreakStartedAt = Day1.AddHours(3),
        });
        await db.SaveChangesAsync();

        await CreateWorkflow(db).RecordArrivalAsync(seed.TenantId, seed.WorkOrderId, seed.UserId, new(seed.TechnicianId, null, null, Day1.AddHours(3).AddMinutes(30), null));

        var entry = await db.AttendanceEntries.SingleAsync();
        Assert.Equal(AttendanceRules.ClockedIn, entry.Status);
        Assert.Equal(30, entry.BreakMinutes);
        Assert.Null(entry.BreakStartedAt);
        Assert.Equal(seed.WorkOrderId, entry.CurrentWorkOrderId);
    }

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

    private sealed record Seed(Guid TenantId, Guid WorkOrderId, Guid TechnicianId, Guid UserId);

    private static async Task<Seed> SeedAsync(AppDbContext db)
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var client = new Client { TenantId = tenantId, ClientName = "Acme Hospital", IsActive = true };
        var technician = new Technician { TenantId = tenantId, UserId = userId, FullName = "Field Tech", Email = $"tech-{Guid.NewGuid():N}@acme.test", IsActive = true };
        db.AddRange(client, technician);
        await db.SaveChangesAsync();

        var workOrder = new WorkOrder
        {
            TenantId = tenantId, ClientId = client.Id, WorkOrderNumber = "WO-CLOCK-1", Title = "Chiller overhaul",
            Status = WorkOrderStatuses.InProgress, Priority = "Medium", WorkType = "CorrectiveMaintenance",
        };
        db.WorkOrders.Add(workOrder);
        db.WorkOrderTechnicianAssignments.Add(new WorkOrderTechnicianAssignment
        {
            TenantId = tenantId, WorkOrderId = workOrder.Id, TechnicianId = technician.Id, IsLead = true, Status = "Accepted",
        });
        await db.SaveChangesAsync();
        return new Seed(tenantId, workOrder.Id, technician.Id, userId);
    }

    private static WorkOrderAssignmentWorkflowService CreateWorkflow(AppDbContext db) =>
        new(db, new AuditLogService(db, new HttpContextAccessor()), null!, null!, null!, null!, new NoOpEtaNotificationService(),
            null!, NullLogger<WorkOrderAssignmentWorkflowService>.Instance);

    private sealed class NoOpEtaNotificationService : IEtaNotificationService
    {
        public Task NotifyAsync(Guid tenantId, WorkOrder workOrder, string eventType, string? technicianName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
