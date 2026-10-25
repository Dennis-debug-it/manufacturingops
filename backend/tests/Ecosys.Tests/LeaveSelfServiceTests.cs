using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

/// <summary>
/// Anyone signed in can request leave. A technician's request stays on their technician profile (so it
/// counts on their timesheet); office staff without a profile file it against their user account.
/// </summary>
public sealed class LeaveSelfServiceTests
{
    private static readonly DateOnly Start = new(2026, 11, 2);

    [Fact]
    public async Task StaffWithoutTechnicianProfile_CanRequestLeave_AndSeeIt()
    {
        var seed = await SeedAsync();
        await using var db = CreateDbContext(seed.Database, seed.Tenant, seed.OfficeUserId);
        var controller = CreateController(db, seed.Tenant, seed.OfficeUserId);

        var created = Value<LeaveRequestResponse>(await controller.CreateMyLeave(new CreateMyLeaveRequest("Annual", Start, Start.AddDays(2), "Family visit"), CancellationToken.None));

        Assert.Null(created.TechnicianId);
        Assert.Equal("Grace Njeri", created.TechnicianName);
        Assert.Equal(3m, created.TotalDays);
        var stored = await db.LeaveRequests.SingleAsync();
        Assert.Equal((seed.OfficeUserId, (Guid?)null), (stored.UserId!.Value, stored.TechnicianId));

        var mine = Value<IReadOnlyCollection<LeaveRequestResponse>>(await controller.ListMyLeave(CancellationToken.None));
        Assert.Equal(created.Id, Assert.Single(mine).Id);
    }

    [Fact]
    public async Task TechniciansLeave_StaysOnTheirProfile_AndTheirListIncludesOlderRequests()
    {
        var seed = await SeedAsync();
        await using (var setup = CreateDbContext(seed.Database))
        {
            // Filed before leave recorded the user account: only the technician link is set.
            setup.LeaveRequests.Add(new LeaveRequest { TenantId = seed.Tenant, TechnicianId = seed.TechnicianId, LeaveTypeCode = "Sick", StartDate = Start.AddDays(-30), EndDate = Start.AddDays(-30), TotalDays = 1 });
            await setup.SaveChangesAsync();
        }
        await using var db = CreateDbContext(seed.Database, seed.Tenant, seed.TechnicianUserId);
        var controller = CreateController(db, seed.Tenant, seed.TechnicianUserId);

        var created = Value<LeaveRequestResponse>(await controller.CreateMyLeave(new CreateMyLeaveRequest("Annual", Start, Start), CancellationToken.None));

        Assert.Equal(seed.TechnicianId, created.TechnicianId);
        Assert.Equal(seed.TechnicianUserId, (await db.LeaveRequests.SingleAsync(x => x.Id == created.Id)).UserId);
        Assert.Equal(2, Value<IReadOnlyCollection<LeaveRequestResponse>>(await controller.ListMyLeave(CancellationToken.None)).Count);
    }

    [Fact]
    public async Task OfficeLeaveList_NamesStaffAndTechnicians_AndManagersCanDecideEither()
    {
        var seed = await SeedAsync();
        await using (var staff = CreateDbContext(seed.Database, seed.Tenant, seed.OfficeUserId))
            await CreateController(staff, seed.Tenant, seed.OfficeUserId).CreateMyLeave(new CreateMyLeaveRequest("Annual", Start, Start), CancellationToken.None);
        await using (var tech = CreateDbContext(seed.Database, seed.Tenant, seed.TechnicianUserId))
            await CreateController(tech, seed.Tenant, seed.TechnicianUserId).CreateMyLeave(new CreateMyLeaveRequest("Sick", Start, Start), CancellationToken.None);

        await using var db = CreateDbContext(seed.Database, seed.Tenant, Guid.NewGuid());
        var manager = CreateController(db, seed.Tenant, Guid.NewGuid());
        var all = Value<IReadOnlyCollection<LeaveRequestResponse>>(await manager.ListLeave(null, CancellationToken.None));
        Assert.Equal(["Grace Njeri", "Kevin Ochieng"], all.Select(x => x.TechnicianName).Order().ToArray());

        var staffLeave = all.Single(x => x.TechnicianId is null);
        Assert.IsType<NoContentResult>(await manager.DecideLeave(staffLeave.Id, new LeaveDecisionRequest(true, null), CancellationToken.None));
        Assert.Equal("Approved", (await db.LeaveRequests.SingleAsync(x => x.Id == staffLeave.Id)).Status);
    }

    private static T Value<T>(ActionResult<T> result) => (T)Assert.IsType<OkObjectResult>(result.Result).Value!;

    private sealed record Seed(string Database, Guid Tenant, Guid OfficeUserId, Guid TechnicianUserId, Guid TechnicianId);

    private static async Task<Seed> SeedAsync()
    {
        var database = $"ecosys-leave-{Guid.NewGuid():N}";
        var tenantId = Guid.NewGuid();
        await using var db = CreateDbContext(database);
        var office = new User { TenantId = tenantId, FullName = "Grace Njeri", Email = "grace@demo.test", PasswordHash = "x", Role = AppRoles.Dispatcher };
        var techUser = new User { TenantId = tenantId, FullName = "Kevin Ochieng", Email = "kevin@demo.test", PasswordHash = "x", Role = AppRoles.Technician };
        db.Users.AddRange(office, techUser);
        var technician = new Technician { TenantId = tenantId, FullName = "Kevin Ochieng", Email = "kevin@demo.test", UserId = techUser.Id, IsActive = true };
        db.Technicians.Add(technician);
        await db.SaveChangesAsync();
        return new Seed(database, tenantId, office.Id, techUser.Id, technician.Id);
    }

    private static TimeAttendanceController CreateController(AppDbContext db, Guid tenantId, Guid userId) =>
        new(db, new FakeTenantContext(tenantId, userId), new NoOpAuditLogService(), new AllowAllUserAccessService());

    private static AppDbContext CreateDbContext(string databaseName, Guid? tenantId = null, Guid? userId = null) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(databaseName).Options,
            tenantId is null ? null : new FakeTenantContext(tenantId.Value, userId!.Value));

    private sealed class FakeTenantContext(Guid tenantId, Guid userId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
        public Guid? SessionId => Guid.NewGuid();
        public string? Email => "test@demo.test";
        public string? Role => AppRoles.Admin;
        public string? JobTitle => "Admin";
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public bool IsAdmin => true;
        public bool HasRole(string role) => string.Equals(role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase);
        public bool HasPermission(string permissionName) => true;
        public Guid GetRequiredTenantId() => tenantId;
        public Guid GetRequiredUserId() => userId;
        public Guid GetRequiredSessionId() => SessionId!.Value;
    }

    private sealed class AllowAllUserAccessService : IUserAccessService
    {
        public void EnsureAdmin() { }
        public void EnsureAdminOrPermission(string permissionName) { }
        public void EnsureAdminOrAnyPermission(params string[] permissionNames) { }
        public void EnsureTenantOperationalAccess() { }
    }

    private sealed class NoOpAuditLogService : IAuditLogService
    {
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, string severity = "Info", string? actorName = null, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
