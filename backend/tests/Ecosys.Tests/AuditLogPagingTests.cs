using Ecosys.Api.Common;
using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ecosys.Tests;

/// <summary>
/// EC-015 and EC-028 regression cover.
///
/// EC-015: <c>GET /api/audit-logs</c> took no parameters at all — no paging, no filters — and
/// serialized every audit row the tenant had ever written into one response. Audit logs are the
/// fastest-growing table in the product (a row per login and per mutation), so these tests assert
/// the properties that keep the endpoint bounded and honest: a page is a page, the total describes
/// the whole filtered set rather than the page, every filter runs in the query rather than in the
/// browser, and none of it loosens the tenant boundary.
///
/// EC-028: impersonation audit entries carried an empty description, so the one question an auditor
/// asks of them — who impersonated which tenant, and why — could not be answered from the log.
/// </summary>
public sealed class AuditLogPagingTests
{
    [Fact]
    public async Task Paging_ReturnsRequestedPage_AndTotalCountOfTheWholeSet()
    {
        var databaseName = $"ecosys-audit-paging-{Guid.NewGuid():N}";
        var fixture = await SeedAsync(databaseName);

        await using var dbContext = CreateDbContext(databaseName, new FakeTenantContext(fixture.TenantAId, fixture.TenantAUserId));
        var controller = CreateController(dbContext, fixture.TenantAId, fixture.TenantAUserId);

        var firstPage = await GetPageAsync(controller, page: 1, pageSize: 2);

        // The total describes the tenant's whole audit history, not the slice that was returned.
        Assert.Equal(TenantALogCount, firstPage.TotalCount);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Equal(1, firstPage.Page);
        Assert.Equal(2, firstPage.PageSize);

        var secondPage = await GetPageAsync(controller, page: 2, pageSize: 2);

        Assert.Equal(TenantALogCount, secondPage.TotalCount);
        Assert.Equal(2, secondPage.Items.Count);
        // Newest-first ordering has to be stable across pages, or paging silently repeats and skips rows.
        Assert.Empty(firstPage.Items.Select(x => x.Id).Intersect(secondPage.Items.Select(x => x.Id)));
        Assert.True(firstPage.Items.Last().CreatedAt >= secondPage.Items.First().CreatedAt);
    }

    [Fact]
    public async Task PageSize_IsCappedSoOneRequestCannotAskForEverything()
    {
        var databaseName = $"ecosys-audit-cap-{Guid.NewGuid():N}";
        var fixture = await SeedAsync(databaseName);

        await using var dbContext = CreateDbContext(databaseName, new FakeTenantContext(fixture.TenantAId, fixture.TenantAUserId));
        var controller = CreateController(dbContext, fixture.TenantAId, fixture.TenantAUserId);

        var page = await GetPageAsync(controller, page: 1, pageSize: int.MaxValue);

        Assert.Equal(QueryablePagingExtensions.MaxPageSize, page.PageSize);
    }

    [Fact]
    public async Task ActionFilter_NarrowsToMatchingRows()
    {
        var databaseName = $"ecosys-audit-action-{Guid.NewGuid():N}";
        var fixture = await SeedAsync(databaseName);

        await using var dbContext = CreateDbContext(databaseName, new FakeTenantContext(fixture.TenantAId, fixture.TenantAUserId));
        var controller = CreateController(dbContext, fixture.TenantAId, fixture.TenantAUserId);

        // Deliberately the wrong case: an auditor typing "login" must not have to match the stored casing.
        var page = await GetPageAsync(controller, action: "login");

        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, item => Assert.Equal("Login", item.Action));
    }

    [Fact]
    public async Task EntityFilter_NarrowsToMatchingRows()
    {
        var databaseName = $"ecosys-audit-entity-{Guid.NewGuid():N}";
        var fixture = await SeedAsync(databaseName);

        await using var dbContext = CreateDbContext(databaseName, new FakeTenantContext(fixture.TenantAId, fixture.TenantAUserId));
        var controller = CreateController(dbContext, fixture.TenantAId, fixture.TenantAUserId);

        var page = await GetPageAsync(controller, entityName: "workorder");

        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, item => Assert.Equal("WorkOrder", item.EntityName));
    }

    [Fact]
    public async Task DateRangeFilter_NarrowsToMatchingRows_AndIncludesTheWholeOfTheEndDay()
    {
        var databaseName = $"ecosys-audit-dates-{Guid.NewGuid():N}";
        var fixture = await SeedAsync(databaseName);

        await using var dbContext = CreateDbContext(databaseName, new FakeTenantContext(fixture.TenantAId, fixture.TenantAUserId));
        var controller = CreateController(dbContext, fixture.TenantAId, fixture.TenantAUserId);

        var page = await GetPageAsync(controller, from: BaseDate.AddDays(1), to: BaseDate.AddDays(2));

        // Day 1 and day 2 only: day 0 and day 3 are outside the range. `to` is a calendar day, so an
        // entry written late on day 2 still counts — the trap is an exclusive `<= to.Date` bound that
        // silently drops the last day the user asked for.
        Assert.Equal(3, page.TotalCount);
        Assert.All(page.Items, item => Assert.InRange(item.CreatedAt, BaseDate.AddDays(1), BaseDate.AddDays(3)));
    }

    [Fact]
    public async Task SearchFilter_RunsServerSide_OverActionEntityAndDetails()
    {
        var databaseName = $"ecosys-audit-search-{Guid.NewGuid():N}";
        var fixture = await SeedAsync(databaseName);

        await using var dbContext = CreateDbContext(databaseName, new FakeTenantContext(fixture.TenantAId, fixture.TenantAUserId));
        var controller = CreateController(dbContext, fixture.TenantAId, fixture.TenantAUserId);

        // The match lives in Details only, and on a row that is not on the first page. A client-side
        // search over the loaded page would miss it entirely.
        var page = await GetPageAsync(controller, search: "generator", pageSize: 2);

        Assert.Equal(1, page.TotalCount);
        Assert.Equal("Created work order 'WO-0002' for the standby generator.", Assert.Single(page.Items).Details);
    }

    [Fact]
    public async Task Paging_StaysTenantScoped()
    {
        var databaseName = $"ecosys-audit-isolation-{Guid.NewGuid():N}";
        var fixture = await SeedAsync(databaseName);

        await using var dbContext = CreateDbContext(databaseName, new FakeTenantContext(fixture.TenantAId, fixture.TenantAUserId));
        var controller = CreateController(dbContext, fixture.TenantAId, fixture.TenantAUserId);

        // Every page of the whole set, plus an unfiltered total: neither may leak tenant B.
        var collected = new List<AuditLogResponse>();
        for (var pageNumber = 1; pageNumber <= 5; pageNumber++)
        {
            var page = await GetPageAsync(controller, page: pageNumber, pageSize: 2);
            Assert.Equal(TenantALogCount, page.TotalCount);
            collected.AddRange(page.Items);
        }

        Assert.Equal(TenantALogCount, collected.Count);
        var tenantBIds = await CreateDbContext(databaseName).AuditLogs
            .Where(x => x.TenantId == fixture.TenantBId)
            .Select(x => x.Id)
            .ToListAsync();
        Assert.NotEmpty(tenantBIds);
        Assert.Empty(collected.Select(x => x.Id).Intersect(tenantBIds));
    }

    [Fact]
    public async Task Export_CoversTheWholeFilteredSet_NotJustThePageOnScreen()
    {
        var databaseName = $"ecosys-audit-export-{Guid.NewGuid():N}";
        var fixture = await SeedAsync(databaseName);

        await using var dbContext = CreateDbContext(databaseName, new FakeTenantContext(fixture.TenantAId, fixture.TenantAUserId));
        var controller = CreateController(dbContext, fixture.TenantAId, fixture.TenantAUserId);

        var export = Assert.IsType<FileContentResult>(
            await controller.Export(null, null, null, null, null, CancellationToken.None));

        Assert.Equal("text/csv", export.ContentType);
        var lines = System.Text.Encoding.UTF8.GetString(export.FileContents)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Header plus every row of the tenant's history — the page size the table happens to be
        // using must not shrink the export.
        Assert.Equal(TenantALogCount + 1, lines.Length);
        Assert.StartsWith("When,Action,Entity", lines[0]);
        Assert.DoesNotContain(lines, line => line.Contains("Tenant B only", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Impersonation_WritesDescriptionsNamingActorTenantAndReason()
    {
        var databaseName = $"ecosys-audit-impersonation-{Guid.NewGuid():N}";
        var auditLog = new RecordingAuditLogService();

        Guid tenantId;
        await using (var seed = CreateDbContext(databaseName))
        {
            var tenant = CreateTenant("Kilimani Facilities");
            tenantId = tenant.Id;
            seed.Tenants.Add(tenant);
            seed.Users.Add(CreateUser(tenantId, "Asha Admin", "asha@kilimani.test", AppRoles.Admin));
            await seed.SaveChangesAsync();
        }

        await using var dbContext = CreateDbContext(databaseName);
        var authService = CreateAuthService(dbContext, auditLog);

        await authService.ImpersonateTenantAdminAsync(
            tenantId,
            Guid.NewGuid(),
            "Pauline Platform",
            "Investigating support ticket SUP-4192",
            CancellationToken.None);

        var tenantEntry = Assert.Single(auditLog.Entries, x => x.Action == "impersonation.started");
        var platformEntry = Assert.Single(auditLog.Entries, x => x.Action == "platform.tenant.impersonation.started");

        foreach (var entry in new[] { tenantEntry, platformEntry })
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Details));
            Assert.Contains("Pauline Platform", entry.Details!, StringComparison.Ordinal);
            Assert.Contains("Kilimani Facilities", entry.Details!, StringComparison.Ordinal);
            Assert.Contains("asha@kilimani.test", entry.Details!, StringComparison.Ordinal);
            Assert.Contains("Investigating support ticket SUP-4192", entry.Details!, StringComparison.Ordinal);
            Assert.Equal("Warning", entry.Severity);
        }
    }

    [Fact]
    public async Task Impersonation_WithoutAReason_StillWritesADescriptionThatSaysSo()
    {
        var databaseName = $"ecosys-audit-impersonation-noreason-{Guid.NewGuid():N}";
        var auditLog = new RecordingAuditLogService();

        Guid tenantId;
        await using (var seed = CreateDbContext(databaseName))
        {
            var tenant = CreateTenant("Kilimani Facilities");
            tenantId = tenant.Id;
            seed.Tenants.Add(tenant);
            seed.Users.Add(CreateUser(tenantId, "Asha Admin", "asha@kilimani.test", AppRoles.Admin));
            await seed.SaveChangesAsync();
        }

        await using var dbContext = CreateDbContext(databaseName);
        var authService = CreateAuthService(dbContext, auditLog);

        await authService.ImpersonateTenantAdminAsync(tenantId, Guid.NewGuid(), "Pauline Platform", CancellationToken.None);

        Assert.All(auditLog.Entries, entry =>
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Details));
            // "No reason given" is itself audit-relevant: silence must be recorded, not implied.
            Assert.Contains("No reason", entry.Details!, StringComparison.OrdinalIgnoreCase);
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Audit rows seeded for tenant A. Kept as a constant so the paging maths is explicit.</summary>
    private const int TenantALogCount = 6;

    private static readonly DateTime BaseDate = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    private static async Task<PagedResult<AuditLogResponse>> GetPageAsync(
        AuditLogsController controller,
        DateTime? from = null,
        DateTime? to = null,
        string? action = null,
        string? entityName = null,
        string? search = null,
        int? page = null,
        int? pageSize = null)
    {
        var result = await controller.GetAll(from, to, action, entityName, search, page, pageSize, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<PagedResult<AuditLogResponse>>(ok.Value);
    }

    private static AuditLogsController CreateController(AppDbContext dbContext, Guid tenantId, Guid userId) =>
        new(dbContext, new FakeTenantContext(tenantId, userId), new NoOpUserAccessService());

    private static AppDbContext CreateDbContext(string databaseName, ITenantContext? tenantContext = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new AppDbContext(options, tenantContext);
    }

    private static MvpAuthService CreateAuthService(AppDbContext dbContext, IAuditLogService auditLogService) =>
        new(
            dbContext,
            new PasswordHasher<User>(),
            Options.Create(new JwtOptions
            {
                Issuer = "ecosys-tests",
                Audience = "ecosys-tests",
                SigningKey = "12345678901234567890123456789012",
                ExpiryMinutes = 60
            }),
            Options.Create(new AuthSecurityOptions()),
            auditLogService,
            new FakeUserPermissionTemplateService(),
            new FakeRolePermissionService(),
            new FakeDynamicAccessService(),
            new FakeUserSessionService(),
            new NoOpLicenseGuardService(),
            new FakeTenantSecurityPolicyService());

    private static async Task<Fixture> SeedAsync(string databaseName)
    {
        var tenantA = CreateTenant("Tenant A");
        var tenantB = CreateTenant("Tenant B");
        var tenantAUser = CreateUser(tenantA.Id, "Alice Admin", $"alice-{Guid.NewGuid():N}@tenant-a.test", AppRoles.Admin);

        await using (var dbContext = CreateDbContext(databaseName))
        {
            dbContext.Tenants.AddRange(tenantA, tenantB);
            dbContext.Users.Add(tenantAUser);

            dbContext.AuditLogs.AddRange(
                Log(tenantA.Id, "Login", "User", "u-1", "User 'alice@tenant-a.test' signed in.", BaseDate),
                Log(tenantA.Id, "Login", "User", "u-2", "User 'ben@tenant-a.test' signed in.", BaseDate.AddDays(1)),
                Log(tenantA.Id, "Work order created", "WorkOrder", "wo-1", "Created work order 'WO-0001'.", BaseDate.AddDays(1).AddHours(6)),
                Log(tenantA.Id, "Work order created", "WorkOrder", "wo-2", "Created work order 'WO-0002' for the standby generator.", BaseDate.AddDays(2).AddHours(23)),
                Log(tenantA.Id, "Client created", "Client", "c-1", "Created client 'Westlands Mall'.", BaseDate.AddDays(3)),
                Log(tenantA.Id, "Client updated", "Client", "c-1", "Updated client 'Westlands Mall'.", BaseDate.AddDays(4)),
                Log(tenantB.Id, "Login", "User", "u-9", "Tenant B only: user 'zara@tenant-b.test' signed in.", BaseDate.AddDays(1)),
                Log(tenantB.Id, "Work order created", "WorkOrder", "wo-9", "Tenant B only: created work order 'WO-9001' for a generator.", BaseDate.AddDays(2)));

            await dbContext.SaveChangesAsync();
        }

        // AppDbContext stamps CreatedAt on insert, so the intended timestamps are applied as an
        // update — otherwise every seeded row would share the same "now" and the ordering and
        // date-range assertions would be vacuous.
        await using (var dbContext = CreateDbContext(databaseName))
        {
            var logs = await dbContext.AuditLogs.ToListAsync();
            foreach (var log in logs)
            {
                log.CreatedAt = SeededTimestamps[log.EntityId + "|" + log.Action];
            }

            await dbContext.SaveChangesAsync();
        }

        return new Fixture(tenantA.Id, tenantB.Id, tenantAUser.Id);
    }

    private static readonly Dictionary<string, DateTime> SeededTimestamps = new();

    private static AuditLog Log(Guid tenantId, string action, string entityName, string entityId, string details, DateTime createdAt)
    {
        SeededTimestamps[entityId + "|" + action] = createdAt;
        return new AuditLog
        {
            TenantId = tenantId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Details = details,
            Severity = "Info",
            CreatedAt = createdAt
        };
    }

    private static Tenant CreateTenant(string name) =>
        new()
        {
            Name = name,
            Slug = $"{name.ToLowerInvariant().Replace(' ', '-')}-{Guid.NewGuid():N}"[..12],
            CompanyName = $"{name} Company",
            Email = $"{Guid.NewGuid():N}@test.local",
            Country = "Kenya",
            Status = "Active",
            LicenseStatus = "Active",
            PrimaryColor = "#0F4C81",
            SecondaryColor = "#F4B942",
            IsActive = true
        };

    private static User CreateUser(Guid tenantId, string fullName, string email, string role) =>
        new()
        {
            TenantId = tenantId,
            FullName = fullName,
            Email = email,
            PasswordHash = "hash",
            Role = role,
            IsActive = true,
            HasAllBranchAccess = true
        };

    private sealed record Fixture(Guid TenantAId, Guid TenantBId, Guid TenantAUserId);

    private sealed record RecordedAuditEntry(
        Guid? TenantId,
        Guid? UserId,
        string Action,
        string EntityName,
        string EntityId,
        string? Details,
        string Severity);

    private sealed class RecordingAuditLogService : IAuditLogService
    {
        public List<RecordedAuditEntry> Entries { get; } = [];

        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, CancellationToken cancellationToken = default) =>
            LogAsync(tenantId, userId, action, entityName, entityId, details, "Info", null, null, null, cancellationToken);

        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, string severity = "Info", string? actorName = null, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default)
        {
            Entries.Add(new RecordedAuditEntry(tenantId, userId, action, entityName, entityId, details, severity));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTenantContext(Guid tenantId, Guid userId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
        public Guid? SessionId { get; } = Guid.NewGuid();
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

    private sealed class NoOpUserAccessService : IUserAccessService
    {
        public void EnsureAdmin() { }
        public void EnsureAdminOrPermission(string permissionName) { }
        public void EnsureAdminOrAnyPermission(params string[] permissionNames) { }
        public void EnsureTenantOperationalAccess() { }
    }

    private sealed class FakeUserSessionService : IUserSessionService
    {
        public UserSession StartSession(User user, string? jwtId) => new() { TenantId = user.TenantId, UserId = user.Id, JwtId = jwtId };
        public Task<bool> TouchAsync(Guid sessionId, TimeSpan minimumInterval, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task LogoutAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<UserSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.FromResult<UserSession?>(null);
        public Task<int> RevokeAllForUserAsync(Guid userId, string? reason = null, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> RevokeOthersForUserAsync(Guid userId, Guid currentSessionId, string? reason = null, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> RevokeSessionsForRolesAsync(Guid tenantId, IReadOnlyCollection<string> roles, string? reason = null, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> RevokeSessionsForUsersAsync(IReadOnlyCollection<Guid> userIds, string? reason = null, CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class FakeDynamicAccessService : IDynamicAccessService
    {
        public Task<IReadOnlyList<EffectiveCapability>> GetEffectiveAccessAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EffectiveCapability>>([]);
        public Task<UserPermissionsModel> ResolveLegacyClaimsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new UserPermissionsModel());
    }

    private sealed class FakeRolePermissionService : IRolePermissionService
    {
        public Task<IReadOnlyList<RolePermissionEntry>> GetMatrixAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RolePermissionEntry>>([]);
        public Task SaveMatrixAsync(Guid tenantId, IReadOnlyDictionary<string, UserPermissionsModel> updates, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task<UserPermissionsModel> ResolveAsync(Guid tenantId, string role, CancellationToken cancellationToken = default) =>
            Task.FromResult(new UserPermissionsModel());
    }

    private sealed class FakeUserPermissionTemplateService : IUserPermissionTemplateService
    {
        public UserPermissionsModel GetDefaultPermissions(string role, string? jobTitle) => new();
        public UserPermissionsModel GetFullTenantPermissions() => new() { CanViewReports = true };
        public UserPermissionsModel GetSuperAdminPermissions() => new();
        public UserPermissionsModel Merge(string role, string? jobTitle, UserPermissionsModel? customPermissions) => customPermissions ?? new UserPermissionsModel();
    }

    private sealed class FakeTenantSecurityPolicyService : ITenantSecurityPolicyService
    {
        public Task<TenantSecurityPolicy> GetOrCreateAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TenantSecurityPolicy { TenantId = tenantId });
        public Task ValidatePasswordAsync(Guid tenantId, string password, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void ValidatePassword(string password, TenantSecurityPolicy policy) { }
    }

    private sealed class NoOpLicenseGuardService : ILicenseGuardService
    {
        public Task<TenantLicenseSnapshot> GetSnapshotAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LicenseUsageSnapshot> GetUsageAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PlatformLicenseUsageSnapshot>> GetPlatformUsageAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task EnsureTenantCanMutateAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureCanCreateUserAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureCanCreateBranchAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureFeatureEnabledAsync(Guid tenantId, string featureName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<TenantLicense> GetOrCreateTenantLicenseAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
