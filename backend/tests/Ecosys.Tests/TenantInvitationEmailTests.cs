using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ecosys.Tests;

public sealed class TenantInvitationEmailTests
{
    [Theory]
    [InlineData("Africa/Nairobi", "8:30 AM EAT")]
    [InlineData("Europe/London", "6:30 AM Europe/London")]
    public async Task InvitationEmail_RendersTenantLocalExpiry_AndUsesPlatformTransport(string zone, string expectedTime)
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var tenant = new Tenant { Slug = "acme", Name = "Acme", CompanyName = "Acme", TimeZoneId = zone, Country = "Kenya" };
        var user = new User { Tenant = tenant, TenantId = tenant.Id, FullName = "John", Email = "john@acme.test", Role = "Admin" };
        var identity = TenantEmailIdentity.Create(tenant);
        db.AddRange(tenant, user, identity, new EmailSetting
        {
            TenantId = PlatformConstants.RootTenantId, Host = "smtp.platform.test", Port = 587, UseSsl = true,
            IsEnabled = true, SenderAddress = "platform@platform.test", SenderName = "Platform", Provider = "Smtp"
        });
        await db.SaveChangesAsync();
        var capture = new CaptureSender();
        var http = new DefaultHttpContext();
        http.Request.Headers.Origin = "https://untrusted.test";
        var delivery = new UserCredentialDeliveryService(db, new HttpContextAccessor { HttpContext = http }, capture,
            new EmailTemplateService(db), new EmailDeliveryLogService(db), new EmailSubjectRuleService(db), new NoSecrets(),
            new EmailSettingsResolver(db), Options.Create(new SmtpOptions()),
            Options.Create(new PublicAppOptions { PublicUrl = "https://serviceops.example.test" }),
            NullLogger<UserCredentialDeliveryService>.Instance);
        var link = delivery.BuildInviteUrl("opaque+/=token");
        Assert.StartsWith("https://serviceops.example.test/reset-password?", link);
        Assert.Contains("opaque%2B%2F%3Dtoken", link);
        Assert.DoesNotContain("untrusted", link);
        var result = await delivery.SendPasswordResetLinkAsync(tenant.Id, user, new PasswordResetLinkDeliveryRequest(
            link, new DateTime(2026, 9, 8, 5, 30, 0, DateTimeKind.Utc), PasswordLinkDeliveryPurpose.AccountSetup));
        Assert.True(result.Success, result.Message);
        Assert.NotNull(capture.Message);
        Assert.Contains(expectedTime, System.Net.WebUtility.HtmlDecode(capture.Message.Body));
        Assert.Contains("valid for 72 hours", capture.Message.Body);
        Assert.DoesNotContain(" UTC", capture.Message.Body);
        Assert.Equal("acme@serviceops.ecosysdigital.co.ke", capture.Message.FromAddress);
        Assert.Equal("smtp.platform.test", capture.Settings!.SmtpHost);
    }

    private sealed class NoSecrets : ISecretEncryptionService
    {
        public string? Encrypt(string? value) => value;
        public string? Decrypt(string? value) => value;
    }

    private sealed class CaptureSender : IEmailSender
    {
        public EmailMessage? Message { get; private set; }
        public EmailDeliverySettings? Settings { get; private set; }
        public Task SendAsync(EmailMessage message, EmailDeliverySettings settings, CancellationToken cancellationToken = default)
        { Message = message; Settings = settings; return Task.CompletedTask; }
        public Task<EmailVerificationResult> VerifyAsync(EmailDeliverySettings settings, CancellationToken cancellationToken = default) =>
            Task.FromResult(new EmailVerificationResult(true));
    }
}
