using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ecosys.Tests;

/// <summary>
/// Covers the two halves of the "reset links expire immediately" report: the lifetime a link is
/// actually issued with, and the time zone the expiry is read in.
/// </summary>
public sealed class TimeZoneAndResetExpiryTests
{
    [Fact]
    public async Task ForgotPassword_IssuesTokenForTheConfiguredWindow()
    {
        await using var dbContext = CreateDbContext();
        var user = await SeedUserAsync(dbContext, Guid.NewGuid(), "window@tenant.test");
        var service = CreateResetService(dbContext, new AuthSecurityOptions { PasswordResetExpiryMinutes = 180 });

        var before = DateTime.UtcNow;
        await service.RequestAsync(user.Email, CancellationToken.None);

        var token = await dbContext.PasswordResetTokens.SingleAsync(x => x.UserId == user.Id);
        Assert.InRange(token.ExpiresAt, before.AddMinutes(179), DateTime.UtcNow.AddMinutes(181));
    }

    [Fact]
    public async Task ForgotPassword_FloorsAnUnusablyShortConfiguredWindow()
    {
        await using var dbContext = CreateDbContext();
        var user = await SeedUserAsync(dbContext, Guid.NewGuid(), "tooshort@tenant.test");
        var service = CreateResetService(dbContext, new AuthSecurityOptions { PasswordResetExpiryMinutes = 1 });

        var before = DateTime.UtcNow;
        await service.RequestAsync(user.Email, CancellationToken.None);

        var token = await dbContext.PasswordResetTokens.SingleAsync(x => x.UserId == user.Id);
        Assert.True(token.ExpiresAt >= before.AddMinutes(14), "A sub-minimum window must be raised to the floor, not honoured.");
    }

    [Fact]
    public async Task ResetPassword_AcceptsToken_WhoseStoredExpiryHasNoDateTimeKind()
    {
        // Rows written before the column carried an explicit UTC kind read back as Unspecified. Those
        // must still be compared as UTC — otherwise a live link is rejected as expired.
        await using var dbContext = CreateDbContext();
        var user = await SeedUserAsync(dbContext, Guid.NewGuid(), "unspecified@tenant.test");
        dbContext.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = ToTokenHash("kindless-token"),
            ExpiresAt = DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(30), DateTimeKind.Unspecified)
        });
        await dbContext.SaveChangesAsync();

        var service = CreateResetService(dbContext, new AuthSecurityOptions());

        var message = await service.ResetAsync("kindless-token", "NewStrongPass123!", "NewStrongPass123!", CancellationToken.None);

        Assert.Equal("Your password has been reset. You can now sign in.", message);
    }

    [Fact]
    public async Task TenantTimeZone_OverridesThePlatformDefault()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(dbContext, tenantId, timeZoneId: "Africa/Lagos");
        var service = TestTimeZones.Create(dbContext);

        Assert.Equal("Africa/Lagos", await service.GetTimeZoneIdAsync(tenantId, CancellationToken.None));
    }

    [Fact]
    public async Task TenantWithoutOverride_InheritsThePlatformSetting()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(dbContext, tenantId, timeZoneId: null);
        dbContext.PlatformSettings.Add(new PlatformSetting
        {
            Category = "platform-general",
            JsonValue = JsonSerializer.Serialize(new { timezone = "Africa/Johannesburg" })
        });
        await dbContext.SaveChangesAsync();

        var service = TestTimeZones.Create(dbContext);

        Assert.Equal("Africa/Johannesburg", await service.GetTimeZoneIdAsync(tenantId, CancellationToken.None));
    }

    [Fact]
    public async Task TenantWithoutAnySetting_FallsBackToEastAfricaTime()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(dbContext, tenantId, timeZoneId: null);
        var service = TestTimeZones.Create(dbContext);

        Assert.Equal("Africa/Nairobi", await service.GetTimeZoneIdAsync(tenantId, CancellationToken.None));
    }

    [Fact]
    public async Task UnrecognizedStoredTimeZone_FallsBackInsteadOfThrowing()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(dbContext, tenantId, timeZoneId: "Mars/Olympus_Mons");
        var service = TestTimeZones.Create(dbContext);

        Assert.Equal("Africa/Nairobi", await service.GetTimeZoneIdAsync(tenantId, CancellationToken.None));
    }

    [Fact]
    public async Task ExpiryText_IsRenderedInTenantLocalTimeWithAZoneLabel()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(dbContext, tenantId, timeZoneId: "Africa/Nairobi");
        var service = TestTimeZones.Create(dbContext);

        var utc = new DateTime(2026, 8, 23, 9, 15, 0, DateTimeKind.Utc);
        var rendered = await service.FormatForTenantAsync(utc, tenantId, cancellationToken: CancellationToken.None);

        // 09:15Z is 12:15 in Nairobi — the reader must never be shown the bare UTC value.
        Assert.Contains("12:15", rendered, StringComparison.Ordinal);
        Assert.Contains("23 Aug 2026", rendered, StringComparison.Ordinal);
        Assert.Contains("EAT", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void NormalizeTimeZoneId_RejectsGarbageAndClearsBlanks()
    {
        using var dbContext = CreateDbContext();
        var service = TestTimeZones.Create(dbContext);

        Assert.Null(service.NormalizeTimeZoneId("   "));
        Assert.Equal("Africa/Nairobi", service.NormalizeTimeZoneId(" Africa/Nairobi "));
        Assert.Throws<Shared.Errors.BusinessRuleException>(() => service.NormalizeTimeZoneId("Not/AZone"));
    }

    private static PasswordResetService CreateResetService(AppDbContext dbContext, AuthSecurityOptions options) =>
        new(
            dbContext,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            new PasswordHasher<User>(),
            new PermissiveSecurityPolicy(),
            new NoOpSessions(),
            new NoOpDelivery(),
            new NoOpAudit(),
            new EmailSettingsResolver(dbContext),
            Options.Create(options),
            Options.Create(new SmtpOptions { Host = "smtp.test" }),
            NullLogger<PasswordResetService>.Instance);

    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-timezone-{Guid.NewGuid():N}")
            .Options);

    private static async Task SeedTenantAsync(AppDbContext dbContext, Guid tenantId, string? timeZoneId)
    {
        dbContext.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Tenant Workspace",
            Slug = $"tenant-{tenantId:N}",
            CompanyName = "Tenant Workspace",
            Email = "support@tenant.test",
            Country = "Kenya",
            Status = "Active",
            TimeZoneId = timeZoneId,
            IsActive = true
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task<User> SeedUserAsync(AppDbContext dbContext, Guid tenantId, string email)
    {
        await SeedTenantAsync(dbContext, tenantId, timeZoneId: null);
        var user = new User
        {
            TenantId = tenantId,
            FullName = "Tenant User",
            Email = email,
            Role = AppRoles.Admin,
            IsActive = true
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "OriginalPass123!");
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }

    private static string ToTokenHash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private sealed class PermissiveSecurityPolicy : ITenantSecurityPolicyService
    {
        public Task<TenantSecurityPolicy> GetOrCreateAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TenantSecurityPolicy { TenantId = tenantId });

        public Task ValidatePasswordAsync(Guid tenantId, string password, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void ValidatePassword(string password, TenantSecurityPolicy policy)
        {
        }
    }

    private sealed class NoOpSessions : IUserSessionService
    {
        public UserSession StartSession(User user, string? jwtId) => new() { TenantId = user.TenantId, UserId = user.Id };
        public Task<bool> TouchAsync(Guid sessionId, TimeSpan minimumInterval, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task LogoutAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<UserSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.FromResult<UserSession?>(null);
        public Task<int> RevokeAllForUserAsync(Guid userId, string? reason = null, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> RevokeOthersForUserAsync(Guid userId, Guid currentSessionId, string? reason = null, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> RevokeSessionsForRolesAsync(Guid tenantId, IReadOnlyCollection<string> roles, string? reason = null, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> RevokeSessionsForUsersAsync(IReadOnlyCollection<Guid> userIds, string? reason = null, CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class NoOpDelivery : IUserCredentialDeliveryService
    {
        public string BuildLoginUrl() => "https://app.test/login";
        public string BuildInviteUrl(string token) => $"https://app.test/reset-password?token={token}&mode=invite";
        public string BuildResetPasswordUrl(string token) => $"https://app.test/reset-password?token={token}";

        public Task<UserCredentialDeliveryResult> SendAsync(Guid tenantId, User user, UserCredentialDeliveryRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new UserCredentialDeliveryResult(true, "Sent", "Sent."));

        public Task<UserCredentialDeliveryResult> SendPasswordResetLinkAsync(Guid tenantId, User user, PasswordResetLinkDeliveryRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new UserCredentialDeliveryResult(true, "Sent", "Sent."));
    }

    private sealed class NoOpAudit : IAuditLogService
    {
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task LogAsync(
            Guid? tenantId,
            Guid? userId,
            string action,
            string entityName,
            string entityId,
            string? details,
            string severity = "Info",
            string? actorName = null,
            string? ipAddress = null,
            string? userAgent = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
