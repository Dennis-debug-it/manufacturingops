using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ecosys.Tests;

/// <summary>Tools checked out to technicians and back, with calibration control.</summary>
public sealed class ToolRegisterTests
{
    private static readonly DateTime Today = new(2026, 10, 27);
    private readonly Guid tenantId = Guid.NewGuid();
    private readonly Guid userId = Guid.NewGuid();
    private readonly Guid techUserId = Guid.NewGuid();

    private static AppDbContext InMemory() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private async Task<(Technician Tech, Technician Leaver)> Seed(AppDbContext db)
    {
        db.Users.AddRange(
            new User { Id = userId, TenantId = tenantId, FullName = "Storekeeper", Email = $"store-{Guid.NewGuid():N}@t.test", Role = AppRoles.Admin, PasswordHash = string.Empty, IsActive = true },
            new User { Id = techUserId, TenantId = tenantId, FullName = "Alex Njoroge", Email = $"alex-{Guid.NewGuid():N}@t.test", Role = AppRoles.Technician, PasswordHash = string.Empty, IsActive = true });
        var tech = new Technician { TenantId = tenantId, UserId = techUserId, FullName = "Alex Njoroge", Email = "alex@t.test" };
        var leaver = new Technician { TenantId = tenantId, FullName = "Mary Achieng", Email = "mary@t.test" };
        db.Technicians.AddRange(tech, leaver);
        await db.SaveChangesAsync();
        return (tech, leaver);
    }

    private ToolsController Controller(AppDbContext db, Guid? asUser = null) => new(db, new FakeTenant(tenantId, asUser ?? userId),
        new AllowAll(), new AllBranches(), new FixedToday(), new NoAudit());

    private static T Ok<T>(ActionResult<T> result) => Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private static async Task<ToolDetailResponse> Create(ToolsController controller, string code, int? intervalDays = null, DateTime? nextDue = null, decimal cost = 1000) =>
        Assert.IsType<ToolDetailResponse>(Assert.IsType<CreatedAtActionResult>((await controller.Create(
            new(code, $"Tool {code}", "Test equipment", $"SN-{code}", PurchaseCost: cost, CalibrationIntervalDays: intervalDays, NextCalibrationDue: nextDue), default)).Result).Value);

    [Fact]
    public async Task CheckOutAcknowledgeAndCheckInKeepAFullHoldingHistory()
    {
        await using var db = InMemory();
        var (tech, _) = await Seed(db);
        var store = Controller(db);
        var meter = await Create(store, "MM-01");

        var out1 = Ok(await store.CheckOut(meter.Tool.Id, new(tech.Id, ExpectedReturnAt: Today.AddDays(7), Notes: "For the Westlands job"), default));
        Assert.Equal(("CheckedOut", "Alex Njoroge", true), (out1.Tool.Status, out1.Tool.HolderName, out1.Tool.AwaitingAcknowledgement));
        await Assert.ThrowsAsync<BusinessRuleException>(() => store.CheckOut(meter.Tool.Id, new(tech.Id), default));

        // The technician sees it and confirms receipt.
        var technician = Controller(db, techUserId);
        Assert.Single(Ok(await technician.Mine(default)));
        await technician.Acknowledge(meter.Tool.Id, default);
        Assert.False(Ok(await store.Get(meter.Tool.Id, default)).Tool.AwaitingAcknowledgement);
        await Assert.ThrowsAsync<NotFoundException>(() => store.Acknowledge(meter.Tool.Id, default));

        await Assert.ThrowsAsync<BusinessRuleException>(() => store.CheckIn(meter.Tool.Id, new(""), default));
        var back = Ok(await store.CheckIn(meter.Tool.Id, new("Damaged", "Cracked screen", SendToRepair: true), default));

        Assert.Equal(("UnderRepair", "Damaged", (string?)null), (back.Tool.Status, back.Tool.Condition, back.Tool.HolderName));
        var spell = Assert.Single(back.History);
        Assert.Equal(("Good", "Damaged", "Cracked screen"), (spell.ConditionOut, spell.ConditionIn, spell.ReturnNotes));
        Assert.NotNull(spell.AcknowledgedAt);
        Assert.Empty(Ok(await technician.Mine(default)));
    }

    [Fact]
    public async Task OverdueCalibrationBlocksCheckOutUntilRecordedOrOverridden()
    {
        await using var db = InMemory();
        var (tech, _) = await Seed(db);
        var store = Controller(db);
        var detector = await Create(store, "GAS-01", intervalDays: 180, nextDue: Today.AddDays(-1));
        Assert.Equal("Overdue", detector.Tool.CalibrationState);

        await Assert.ThrowsAsync<BusinessRuleException>(() => store.CheckOut(detector.Tool.Id, new(tech.Id), default));

        var calibrated = Ok(await store.RecordCalibration(detector.Tool.Id, new(Today, CertificateNumber: "CAL-881", PerformedBy: "Metrology Ltd"), default));
        Assert.Equal((Today.AddDays(180), "Ok"), (calibrated.Tool.NextCalibrationDue, calibrated.Tool.CalibrationState));
        Assert.Equal("CAL-881", Assert.Single(calibrated.Calibrations).CertificateNumber);
        Ok(await store.CheckOut(detector.Tool.Id, new(tech.Id), default));

        // A second overdue tool goes out only with an explicit override.
        var gauge = await Create(store, "GAUGE-01", intervalDays: 365, nextDue: Today.AddDays(-30));
        Assert.Equal("CheckedOut", Ok(await store.CheckOut(gauge.Tool.Id, new(tech.Id, OverrideCalibration: true), default)).Tool.Status);
    }

    [Fact]
    public async Task FailedCalibrationSendsTheToolToRepairAndBackfilledRecordsDoNotMoveTheDueDate()
    {
        await using var db = InMemory();
        await Seed(db);
        var store = Controller(db);
        var tool = await Create(store, "TQ-01", intervalDays: 90);

        Ok(await store.RecordCalibration(tool.Tool.Id, new(Today.AddDays(-10)), default));
        var backfilled = Ok(await store.RecordCalibration(tool.Tool.Id, new(Today.AddDays(-100), CertificateNumber: "OLD"), default));
        Assert.Equal(Today.AddDays(80), backfilled.Tool.NextCalibrationDue);
        Assert.Equal(2, backfilled.Calibrations.Count);

        var failed = Ok(await store.RecordCalibration(tool.Tool.Id, new(Today, Result: "Fail", Notes: "Out of tolerance"), default));
        Assert.Equal(("UnderRepair", "Overdue"), (failed.Tool.Status, failed.Tool.CalibrationState));
        await Assert.ThrowsAsync<BusinessRuleException>(() => store.RecordCalibration(tool.Tool.Id, new(Today.AddDays(1)), default));
    }

    [Fact]
    public async Task LostNeedsAReasonAndClosesTheCheckOutAgainstTheHolder()
    {
        await using var db = InMemory();
        var (tech, _) = await Seed(db);
        var store = Controller(db);
        var drill = await Create(store, "DR-01");
        Ok(await store.CheckOut(drill.Tool.Id, new(tech.Id), default));

        await Assert.ThrowsAsync<BusinessRuleException>(() => store.SetStatus(drill.Tool.Id, new("UnderRepair"), default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => store.SetStatus(drill.Tool.Id, new("Lost"), default));
        var lost = Ok(await store.SetStatus(drill.Tool.Id, new("Lost", "Left at site, not found"), default));

        Assert.Equal("Lost", lost.Tool.Status);
        Assert.Contains("Left at site", Assert.Single(lost.History).ReturnNotes);
        await Assert.ThrowsAsync<BusinessRuleException>(() => store.CheckOut(drill.Tool.Id, new(tech.Id), default));
    }

    [Fact]
    public async Task CodesAreUniqueAndAttentionFiltersFindWhatNeedsAction()
    {
        await using var db = InMemory();
        var (tech, leaver) = await Seed(db);
        var store = Controller(db);
        await Assert.ThrowsAsync<BusinessRuleException>(() => store.Create(new("", "Nameless"), default));
        var a = await Create(store, "A-1", intervalDays: 30, nextDue: Today.AddDays(5), cost: 500);
        await Assert.ThrowsAsync<BusinessRuleException>(() => Create(store, "a-1"));
        var b = await Create(store, "B-1", cost: 2000);
        Ok(await store.CheckOut(b.Tool.Id, new(leaver.Id, ExpectedReturnAt: Today), default));
        leaver.IsActive = false;
        await db.SaveChangesAsync();
        // An expected return of today is not yet overdue; push it back as if days had passed.
        (await db.ToolAssignments.SingleAsync()).ExpectedReturnAt = Today.AddDays(-2);
        await db.SaveChangesAsync();

        Assert.Equal("A-1", Assert.Single(Ok(await store.List(null, null, null, null, null, "calibration", default))).ToolCode);
        Assert.Equal("B-1", Assert.Single(Ok(await store.List(null, null, null, null, null, "overdue-return", default))).ToolCode);
        Assert.Equal("B-1", Assert.Single(Ok(await store.List(null, null, leaver.Id, null, null, null, default))).ToolCode);
        Assert.Equal("B-1", Assert.Single(Ok(await store.List(null, null, null, null, "mary", null, default))).ToolCode);

        var summary = Ok(await store.Summary(null, default));
        Assert.Equal((2, 1, 1, 1, 1, 1, 2000m), (summary.Total, summary.InStore, summary.CheckedOut, summary.CalibrationDueSoon, summary.ReturnOverdue, summary.HeldByInactiveTechnicians, summary.ValueCheckedOut));
        await Assert.ThrowsAsync<BusinessRuleException>(() => store.CheckOut(a.Tool.Id, new(leaver.Id), default));
        Assert.NotNull(tech);
    }

    [Fact]
    public async Task Postgres_ToolFlowsAndQueries_Translate()
    {
        var server = Environment.GetEnvironmentVariable("ECOSYS_TEST_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=ecosys_test;Username=postgres;Password=postgres";
        var name = $"ecosys_tools_{Guid.NewGuid():N}";
        var database = new NpgsqlConnectionStringBuilder(server) { Database = name, Pooling = false }.ConnectionString;
        var admin = new NpgsqlConnectionStringBuilder(server) { Database = "postgres", Pooling = false }.ConnectionString;
        await Execute(admin, $"CREATE DATABASE \"{name}\"");
        try
        {
            await using (var setup = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(database).Options))
                await setup.Database.MigrateAsync();
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(database).Options, new FakeTenant(tenantId, userId));
            db.Tenants.Add(new Tenant { Id = tenantId, Name = "Acme", Slug = $"acme-{Guid.NewGuid():N}"[..12], CompanyName = "Acme", Email = "ops@acme.test", Country = "Kenya" });
            await db.SaveChangesAsync();
            var (tech, _) = await Seed(db);
            var store = Controller(db);
            var tool = await Create(store, "PG-1", intervalDays: 30, nextDue: Today.AddDays(3));

            Ok(await store.CheckOut(tool.Tool.Id, new(tech.Id, ExpectedReturnAt: Today.AddDays(1)), default));
            await Controller(db, techUserId).Acknowledge(tool.Tool.Id, default);
            Assert.Single(Ok(await Controller(db, techUserId).Mine(default)));
            foreach (var attention in new[] { "calibration", "overdue-return", "unacknowledged" })
                Ok(await store.List(null, null, null, null, "pg", attention, default));
            Ok(await store.Summary(null, default));
            Ok(await store.RecordCalibration(tool.Tool.Id, new(Today), default));
            var back = Ok(await store.CheckIn(tool.Tool.Id, new("Good"), default));
            Assert.Equal("InStore", back.Tool.Status);
            Assert.Single(back.History);
        }
        finally
        {
            await Execute(admin, $"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");
        }
    }

    private static async Task Execute(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private sealed class FixedToday : ITenantTimeZoneService
    {
        public Task<string> GetPlatformTimeZoneIdAsync(CancellationToken cancellationToken = default) => Task.FromResult("UTC");
        public Task<string> GetTimeZoneIdAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult("UTC");
        public Task<TimeZoneInfo> GetTimeZoneAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(TimeZoneInfo.Utc);
        public Task<DateTime> ToTenantTimeAsync(DateTime utcValue, Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(utcValue);
        public Task<DateTime> TenantTodayAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(Today);
        public Task<DateTime> TenantDayEndUtcAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> FormatForTenantAsync(DateTime utcValue, Guid tenantId, string format = "dd MMM yyyy 'at' HH:mm", CancellationToken cancellationToken = default) => Task.FromResult(utcValue.ToString(format));
        public string? NormalizeTimeZoneId(string? timeZoneId) => timeZoneId;
        public void Invalidate(Guid? tenantId = null) { }
    }
    private sealed class FakeTenant(Guid tenantId, Guid userId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
        public Guid? SessionId => Guid.NewGuid();
        public string? Email => "admin@test.local";
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
    private sealed class AllowAll : IUserAccessService
    {
        public void EnsureAdmin() { }
        public void EnsureAdminOrPermission(string permissionName) { }
        public void EnsureAdminOrAnyPermission(params string[] permissionNames) { }
        public void EnsureTenantOperationalAccess() { }
    }
    private sealed class AllBranches : IBranchAccessService
    {
        public Task<BranchQueryScope> GetQueryScopeAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new BranchQueryScope(false, requestedBranchId, [], true));
        public Task<Guid?> ResolveBranchIdForWriteAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default) => Task.FromResult(requestedBranchId);
        public Task EnsureCanAccessBranchAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyCollection<Guid>> GetAccessibleBranchIdsAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<Guid>>([]);
    }
    private sealed class NoAudit : IAuditLogService
    {
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, string severity = "Info", string? actorName = null, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
