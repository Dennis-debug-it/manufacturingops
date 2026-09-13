using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ecosys.Tests;

/// <summary>
/// A platform SuperAdmin can move a tenant's workspace between platform mail and its own SMTP relay
/// (email infrastructure is platform-managed; tenant admins only customize display name/reply-to via
/// the separate tenant email identity settings — see <c>SettingsController.EnsurePlatformEmailAccess</c>).
/// The two stored flags are a single mode, and the screen must always report the mailbox really in force.
/// </summary>
public sealed class TenantEmailDeliveryModeTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public async Task NewWorkspace_StartsOnPlatformMail()
    {
        // The previous default seeded an override pointed at localhost:25, quietly breaking delivery.
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, tenantSettings: null);
        var controller = CreateController(dbContext);

        var response = Unwrap(await controller.GetEmailNotifications(CancellationToken.None));

        Assert.True(response.UsePlatformDefaults);
        Assert.True(response.UsesPlatformMail);
        Assert.Equal("noreply@platform.test", response.EffectiveFromEmail);

        var stored = await dbContext.EmailSettings.SingleAsync(x => x.TenantId == TenantId);
        Assert.True(stored.UsePlatformDefaults);
        Assert.False(stored.OverrideSmtpSettings);
        Assert.NotEqual("localhost", stored.Host);
    }

    [Fact]
    public async Task SwitchingToOwnSmtp_FlipsBothFlagsTogether()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, tenantSettings: PlatformMailRow());
        var controller = CreateController(dbContext);

        var response = Unwrap(await controller.UpdateEmailNotifications(
            Request(usePlatformDefaults: false, smtpHost: "smtp.acme.test"),
            CancellationToken.None));

        Assert.False(response.UsePlatformDefaults);
        Assert.False(response.UsesPlatformMail);
        Assert.Equal("noreply@acme.test", response.EffectiveFromEmail);

        var stored = await dbContext.EmailSettings.SingleAsync(x => x.TenantId == TenantId);
        Assert.False(stored.UsePlatformDefaults);
        Assert.True(stored.OverrideSmtpSettings);
        Assert.Equal("smtp.acme.test", stored.Host);
    }

    [Fact]
    public async Task SwitchingBackToPlatformMail_LeavesTheWorkspaceOnPlatformMail()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, tenantSettings: OwnSmtpRow());
        var controller = CreateController(dbContext);

        var response = Unwrap(await controller.UpdateEmailNotifications(
            Request(usePlatformDefaults: true, smtpHost: "smtp.acme.test"),
            CancellationToken.None));

        Assert.True(response.UsesPlatformMail);
        Assert.Equal("noreply@platform.test", response.EffectiveFromEmail);

        var stored = await dbContext.EmailSettings.SingleAsync(x => x.TenantId == TenantId);
        Assert.True(stored.UsePlatformDefaults);
        Assert.False(stored.OverrideSmtpSettings);
    }

    [Fact]
    public async Task OwnSmtpWithoutAHost_IsRejected()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, tenantSettings: PlatformMailRow());
        var controller = CreateController(dbContext);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            controller.UpdateEmailNotifications(Request(usePlatformDefaults: false, smtpHost: ""), CancellationToken.None));
    }

    [Fact]
    public async Task PlatformMailWithoutAHost_IsAccepted()
    {
        // The SMTP fields are inert on platform mail, so requiring a host would block a workspace from
        // saving a sender name it is entitled to set.
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, tenantSettings: PlatformMailRow());
        var controller = CreateController(dbContext);

        var response = Unwrap(await controller.UpdateEmailNotifications(
            Request(usePlatformDefaults: true, smtpHost: ""),
            CancellationToken.None));

        Assert.True(response.UsesPlatformMail);
    }

    [Fact]
    public async Task TestEmail_ReportsThatItExercisedPlatformMail()
    {
        // A pass against the workspace's own inert row would say nothing about whether its mail works.
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, tenantSettings: PlatformMailRow());
        var sender = new RecordingEmailSender();
        var controller = CreateController(dbContext, sender);

        var actionResult = await controller.TestEmailNotifications(new EmailNotificationTestRequest("ops@acme.test"), CancellationToken.None);
        var result = Assert.IsType<EmailNotificationTestResponse>(Assert.IsType<OkObjectResult>(actionResult.Result).Value);

        Assert.True(result.Success);
        Assert.True(result.UsedPlatformMail);
        Assert.Contains("platform mail", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("smtp.platform.test", sender.LastHost);
    }

    [Fact]
    public async Task TestEmail_UsesTheWorkspacesOwnRelayOnceOverridden()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, tenantSettings: OwnSmtpRow());
        var sender = new RecordingEmailSender();
        var controller = CreateController(dbContext, sender);

        var actionResult = await controller.TestEmailNotifications(new EmailNotificationTestRequest("ops@acme.test"), CancellationToken.None);
        var result = Assert.IsType<EmailNotificationTestResponse>(Assert.IsType<OkObjectResult>(actionResult.Result).Value);

        Assert.False(result.UsedPlatformMail);
        Assert.Equal("smtp.acme.test", sender.LastHost);
    }

    private static EmailNotificationSettingsRequest Request(bool usePlatformDefaults, string smtpHost) =>
        new(
            IsEnabled: true,
            DeliveryMode: EmailDeliveryMode.Smtp,
            FromName: "Acme Facilities",
            FromEmail: "noreply@acme.test",
            ReplyToEmail: "support@acme.test",
            SmtpHost: smtpHost,
            SmtpPort: 587,
            EnableSslTls: true,
            SecureMode: EmailSecureMode.StartTls,
            SmtpUsername: "mailer@acme.test",
            SmtpPasswordSecret: "s3cret",
            ApiEndpoint: null,
            ApiKeySecret: null,
            ApiProviderName: null,
            TimeoutSeconds: 30,
            MaxRetries: 0,
            UsePlatformDefaults: usePlatformDefaults);

    private static EmailNotificationSettingsResponse Unwrap(ActionResult<EmailNotificationSettingsResponse> actionResult) =>
        Assert.IsType<EmailNotificationSettingsResponse>(Assert.IsType<OkObjectResult>(actionResult.Result).Value);

    private static EmailSetting PlatformMailRow() => new()
    {
        TenantId = TenantId,
        UsePlatformDefaults = true,
        OverrideSmtpSettings = false,
        IsEnabled = true,
        Provider = "Smtp",
        Port = 587,
        UseSsl = true,
        SenderName = "Acme Facilities",
        SenderAddress = "noreply@acme.test"
    };

    private static EmailSetting OwnSmtpRow() => new()
    {
        TenantId = TenantId,
        UsePlatformDefaults = false,
        OverrideSmtpSettings = true,
        IsEnabled = true,
        Provider = "Smtp",
        Host = "smtp.acme.test",
        Port = 587,
        UseSsl = true,
        SenderName = "Acme Facilities",
        SenderAddress = "noreply@acme.test"
    };

    private static async Task SeedAsync(AppDbContext dbContext, EmailSetting? tenantSettings)
    {
        dbContext.Tenants.Add(new Tenant
        {
            Id = TenantId,
            Name = "Acme Facilities",
            Slug = "acme-facilities",
            CompanyName = "Acme Facilities",
            Email = "info@acme.test",
            Country = "Kenya",
            Status = "Active",
            IsActive = true
        });

        dbContext.EmailSettings.Add(new EmailSetting
        {
            TenantId = PlatformConstants.RootTenantId,
            UsePlatformDefaults = false,
            OverrideSmtpSettings = true,
            IsEnabled = true,
            Provider = "Smtp",
            Host = "smtp.platform.test",
            Port = 587,
            UseSsl = true,
            SenderName = "Ecosys",
            SenderAddress = "noreply@platform.test"
        });

        if (tenantSettings is not null)
        {
            dbContext.EmailSettings.Add(tenantSettings);
        }

        await dbContext.SaveChangesAsync();
    }

    private static SettingsController CreateController(AppDbContext dbContext, IEmailSender? emailSender = null) =>
        new(
            dbContext,
            new FakeTenantContext(TenantId),
            new NoOpUserAccessService(),
            new NoOpBranchAccessService(),
            new NoOpDocumentNumberingService(),
            new NoOpAuditLogService(),
            new PassthroughSecretEncryptionService(),
            TestTimeZones.Create(dbContext),
            new EmailSettingsResolver(dbContext),
            emailSender ?? new RecordingEmailSender(),
            NullLogger<SettingsController>.Instance);

    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-email-mode-{Guid.NewGuid():N}")
            .Options);

    private sealed class RecordingEmailSender : IEmailSender
    {
        public string? LastHost { get; private set; }

        public Task SendAsync(EmailMessage message, EmailDeliverySettings settings, CancellationToken cancellationToken = default)
        {
            LastHost = settings.SmtpHost;
            return Task.CompletedTask;
        }

        public Task<EmailVerificationResult> VerifyAsync(EmailDeliverySettings settings, CancellationToken cancellationToken = default)
        {
            LastHost = settings.SmtpHost;
            return Task.FromResult(new EmailVerificationResult(true));
        }
    }

    private sealed class PassthroughSecretEncryptionService : ISecretEncryptionService
    {
        public string? Encrypt(string? plaintext) => plaintext;
        public string? Decrypt(string? ciphertext) => ciphertext;
    }

    private sealed class NoOpUserAccessService : IUserAccessService
    {
        public void EnsureAdmin() { }
        public void EnsureAdminOrPermission(string permissionName) { }
        public void EnsureAdminOrAnyPermission(params string[] permissionNames) { }
        public void EnsureTenantOperationalAccess() { }
    }

    private sealed class NoOpBranchAccessService : IBranchAccessService
    {
        public Task<BranchQueryScope> GetQueryScopeAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new BranchQueryScope(false, requestedBranchId, [], true));

        public Task<Guid?> ResolveBranchIdForWriteAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(requestedBranchId);

        public Task EnsureCanAccessBranchAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyCollection<Guid>> GetAccessibleBranchIdsAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Guid>>([]);
    }

    private sealed class NoOpDocumentNumberingService : IDocumentNumberingService
    {
        public Task<string> GenerateAsync(Guid tenantId, Guid? branchId, string documentType, CancellationToken cancellationToken = default) =>
            Task.FromResult("DOC-0001");

        public Task<NumberingSetting> UpsertAsync(
            Guid tenantId,
            Guid? branchId,
            string documentType,
            string prefix,
            long nextNumber,
            int paddingLength,
            string resetFrequency,
            bool includeYear,
            bool includeMonth,
            bool isActive,
            CancellationToken cancellationToken = default,
            string? suffix = null,
            string yearFormat = "YYYY",
            string separator = "-",
            bool isLocked = false) => Task.FromResult(new NumberingSetting { TenantId = tenantId, DocumentType = documentType });
    }

    private sealed class NoOpAuditLogService : IAuditLogService
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

    private sealed class FakeTenantContext(Guid tenantId) : ITenantContext
    {
        private readonly Guid _userId = Guid.NewGuid();

        public Guid? TenantId => tenantId;
        public Guid? UserId => _userId;
        public Guid? SessionId => Guid.Empty;
        public string? Email => "admin@acme.test";
        public string? Role => AppRoles.Admin;
        public string? JobTitle => "Administrator";
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public bool IsAdmin => true;

        // Email infrastructure (SMTP host/mode) is platform-managed, not tenant self-service — see
        // SettingsController.EnsurePlatformEmailAccess. A platform admin reaches it by impersonating
        // this tenant (IsSuperAdmin is false during impersonation — TenantAwareControllerBase
        // otherwise blocks SuperAdmin from ever resolving TenantId, EC-020), so these tests simulate
        // that impersonated session rather than a tenant admin's own access.
        public Guid? ImpersonatedByUserId => Guid.NewGuid();
        public string? ImpersonatedByName => "Platform Admin";
        public bool HasRole(string role) => string.Equals(role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase);
        public bool HasPermission(string permissionName) => true;
        public Guid GetRequiredTenantId() => tenantId;
        public Guid GetRequiredUserId() => _userId;
        public Guid GetRequiredSessionId() => Guid.Empty;
    }
}
