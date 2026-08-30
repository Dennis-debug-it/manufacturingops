using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Options;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

/// <summary>
/// Every outbound email — credentials, outbox, finance documents, and the tenant's own SMTP test —
/// now routes through one resolver, so the "which mailbox sends this?" rule is pinned here.
/// </summary>
public sealed class EmailSettingsResolverTests
{
    [Fact]
    public async Task TenantOnPlatformDefaults_SendsThroughPlatformMail()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        SeedSettings(dbContext, PlatformConstants.RootTenantId, "smtp.platform.test", usePlatformDefaults: false, overrideSmtp: true);
        SeedSettings(dbContext, tenantId, "smtp.tenant-typed-this.test", usePlatformDefaults: true, overrideSmtp: false);
        await dbContext.SaveChangesAsync();

        var result = await new EmailSettingsResolver(dbContext).ResolveAsync(tenantId, CancellationToken.None);

        Assert.True(result.UsesPlatformMail);
        Assert.Equal("smtp.platform.test", result.Settings!.Host);
    }

    [Fact]
    public async Task TenantWithGrantedOverride_SendsThroughItsOwnSmtp()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        SeedSettings(dbContext, PlatformConstants.RootTenantId, "smtp.platform.test", usePlatformDefaults: false, overrideSmtp: true);
        SeedSettings(dbContext, tenantId, "smtp.tenant.test", usePlatformDefaults: false, overrideSmtp: true);
        await dbContext.SaveChangesAsync();

        var result = await new EmailSettingsResolver(dbContext).ResolveAsync(tenantId, CancellationToken.None);

        Assert.False(result.UsesPlatformMail);
        Assert.Equal("smtp.tenant.test", result.Settings!.Host);
    }

    [Fact]
    public async Task TenantWithNoRowAtAll_FallsBackToPlatformMail()
    {
        // Previously the credential path returned "not configured" here, which stranded password reset
        // and invitation emails for any tenant missing a row.
        await using var dbContext = CreateDbContext();
        SeedSettings(dbContext, PlatformConstants.RootTenantId, "smtp.platform.test", usePlatformDefaults: false, overrideSmtp: true);
        await dbContext.SaveChangesAsync();

        var result = await new EmailSettingsResolver(dbContext).ResolveAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.UsesPlatformMail);
        Assert.Equal("smtp.platform.test", result.Settings!.Host);
    }

    [Fact]
    public async Task RootTenant_AlwaysUsesItsOwnRow()
    {
        await using var dbContext = CreateDbContext();
        // Flags that would mean "inherit" for a tenant must not send the platform looking for a parent.
        SeedSettings(dbContext, PlatformConstants.RootTenantId, "smtp.platform.test", usePlatformDefaults: true, overrideSmtp: false);
        await dbContext.SaveChangesAsync();

        var result = await new EmailSettingsResolver(dbContext).ResolveAsync(PlatformConstants.RootTenantId, CancellationToken.None);

        Assert.True(result.UsesPlatformMail);
        Assert.Equal("smtp.platform.test", result.Settings!.Host);
    }

    [Fact]
    public async Task NullTenant_IsTreatedAsThePlatform()
    {
        await using var dbContext = CreateDbContext();
        SeedSettings(dbContext, PlatformConstants.RootTenantId, "smtp.platform.test", usePlatformDefaults: false, overrideSmtp: true);
        await dbContext.SaveChangesAsync();

        var result = await new EmailSettingsResolver(dbContext).ResolveAsync(null, CancellationToken.None);

        Assert.True(result.UsesPlatformMail);
        Assert.Equal("smtp.platform.test", result.Settings!.Host);
    }

    [Fact]
    public async Task NothingConfiguredAnywhere_ResolvesToNothingRatherThanThrowing()
    {
        await using var dbContext = CreateDbContext();

        var result = await new EmailSettingsResolver(dbContext).ResolveAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result.Settings);
        Assert.True(result.UsesPlatformMail);
    }

    private static void SeedSettings(AppDbContext dbContext, Guid tenantId, string host, bool usePlatformDefaults, bool overrideSmtp) =>
        dbContext.EmailSettings.Add(new EmailSetting
        {
            TenantId = tenantId,
            Host = host,
            Port = 587,
            UseSsl = true,
            IsEnabled = true,
            Provider = "Smtp",
            SenderName = "Ecosys",
            SenderAddress = $"noreply@{host}",
            UsePlatformDefaults = usePlatformDefaults,
            OverrideSmtpSettings = overrideSmtp
        });

    [Fact]
    public void NoSettingsRowAtAll_FallsBackToTheSmtpConfigurationSection()
    {
        // EC-P1-06d: on a fresh database there is no row, and every send path used to conclude
        // "not configured" right there — so a deployment that passed ECOSYS_SMTP_HOST and nothing
        // else never delivered one message.
        var delivery = EmailDeliveryResolution.Resolve(
            null,
            null,
            new SmtpOptions { Host = "smtp.env.test", Port = 587, Username = "mailer", Password = "secret" });

        Assert.NotNull(delivery);
        Assert.Equal("smtp.env.test", delivery!.SmtpHost);
        Assert.Equal(587, delivery.SmtpPort);
        Assert.Equal("mailer", delivery.SmtpUsername);
        Assert.Equal("secret", delivery.SmtpPasswordSecret);
    }

    [Fact]
    public void BlankHostOnTheSettingsRow_FallsBackToTheSmtpConfigurationSection()
    {
        // The settings screen writes empty strings long before anyone fills the row in, and the old
        // `??` fallback kept those empty strings in preference to the environment.
        var delivery = EmailDeliveryResolution.Resolve(
            new EmailSetting { TenantId = Guid.NewGuid(), Host = "  ", Provider = "Smtp", IsEnabled = true },
            null,
            new SmtpOptions { Host = "smtp.env.test", Port = 25 });

        Assert.Equal("smtp.env.test", delivery!.SmtpHost);
    }

    [Fact]
    public void DatabaseHost_WinsOverTheSmtpConfigurationSection()
    {
        var delivery = EmailDeliveryResolution.Resolve(
            new EmailSetting { TenantId = Guid.NewGuid(), Host = "smtp.tenant.test", Port = 465, Provider = "Smtp", IsEnabled = true },
            "tenant-secret",
            new SmtpOptions { Host = "smtp.env.test", Port = 25 });

        Assert.Equal("smtp.tenant.test", delivery!.SmtpHost);
        Assert.Equal(465, delivery.SmtpPort);
        Assert.Equal("tenant-secret", delivery.SmtpPasswordSecret);
    }

    [Fact]
    public void NeitherSource_ReportsEmailAsUnconfigured()
    {
        Assert.Null(EmailDeliveryResolution.Resolve(null, null, new SmtpOptions { Host = "" }));
    }

    [Fact]
    public void ExplicitlyDisabledRow_IsNotRescuedByConfiguration()
    {
        // Configuration fills gaps; it never overrides an operator who switched delivery off.
        var settings = new EmailSetting { TenantId = Guid.NewGuid(), Host = "smtp.tenant.test", Provider = "Smtp", IsEnabled = false };

        Assert.True(EmailDeliveryResolution.IsExplicitlyDisabled(settings));
        Assert.False(EmailDeliveryResolution.IsExplicitlyDisabled(null));
    }

    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-email-resolver-{Guid.NewGuid():N}")
            .Options);
}
