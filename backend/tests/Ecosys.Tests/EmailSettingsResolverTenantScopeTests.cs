using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Options;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

/// <summary>
/// A workspace admin adding a user sends the invitation during their own request, where email
/// settings are filtered to their workspace. The platform's SMTP server must still be used.
/// </summary>
public sealed class EmailSettingsResolverTenantScopeTests
{
    private readonly Guid tenantId = Guid.NewGuid();

    [Fact]
    public async Task WorkspaceUserRequest_StillSendsThroughThePlatformServer()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-email-scope-{Guid.NewGuid():N}")
            .Options;

        await using (var setup = new AppDbContext(options))
        {
            setup.EmailSettings.AddRange(
                new EmailSetting
                {
                    TenantId = PlatformConstants.RootTenantId,
                    Host = "smtp.platform.test",
                    Port = 587,
                    UseSsl = true,
                    Username = "mailer@platform.test",
                    SenderAddress = "noreply@platform.test",
                },
                new EmailSetting
                {
                    TenantId = tenantId,
                    SenderName = "Acme",
                    SenderAddress = "acme@serviceops.ecosysdigital.co.ke",
                    ManagedEmailAddress = "acme@serviceops.ecosysdigital.co.ke",
                });
            await setup.SaveChangesAsync();
        }

        // The request of a signed-in workspace admin: only that workspace's rows are visible.
        await using var db = new AppDbContext(options, new WorkspaceAdminContext(tenantId));
        Assert.Equal(1, await db.EmailSettings.CountAsync());

        var settings = (await new EmailSettingsResolver(db).ResolveAsync(tenantId)).Settings;
        var delivery = EmailDeliveryResolution.Resolve(settings, null, new SmtpOptions { Host = string.Empty });

        Assert.NotNull(delivery);
        Assert.Equal("smtp.platform.test", delivery!.SmtpHost);
        Assert.Equal(587, delivery.SmtpPort);
        Assert.Equal("mailer@platform.test", delivery.SmtpUsername);
        // Sent as the workspace, never as the platform address.
        Assert.Equal("acme@serviceops.ecosysdigital.co.ke", delivery.SenderEmail);
    }

    private sealed class WorkspaceAdminContext(Guid tenantId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId { get; } = Guid.NewGuid();
        public Guid? SessionId { get; } = Guid.NewGuid();
        public string? Email => "admin@acme.test";
        public string? Role => AppRoles.Admin;
        public string? JobTitle => null;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public bool IsAdmin => true;
        public bool HasRole(string role) => string.Equals(role, Role, StringComparison.OrdinalIgnoreCase);
        public bool HasPermission(string permissionName) => true;
        public Guid GetRequiredTenantId() => tenantId;
        public Guid GetRequiredUserId() => UserId!.Value;
        public Guid GetRequiredSessionId() => SessionId!.Value;
    }
}
