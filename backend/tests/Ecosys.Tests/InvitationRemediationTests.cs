using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Errors;
using Ecosys.Shared.Options;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

public sealed partial class AuthEmailSafetyTests
{
    [Fact]
    public async Task Invitation_Has72HourUtcLifetime_AndSingleUseAcceptance()
    {
        await using var db = CreateDbContext();
        var user = await SeedTenantUserAsync(db, Guid.NewGuid(), "invite@test.test", true);
        var issued = await PasswordLinkTokens.IssueAsync(db, user, PasswordLinkPurposes.Invitation);
        var row = await db.PasswordResetTokens.SingleAsync();
        Assert.Equal(DateTimeKind.Utc, row.ExpiresAt.Kind);
        Assert.Equal(72, (row.ExpiresAt - row.CreatedAt).TotalHours, 3);
        Assert.DoesNotContain(issued.Token, row.TokenHash);
        Assert.Equal("VALID", PasswordLinkTokens.Status(row, row.CreatedAt.AddHours(71.99)));
        Assert.Equal("EXPIRED", PasswordLinkTokens.Status(row, row.CreatedAt.AddHours(72)));
        var service = CreatePasswordResetService(db, new FakeUserCredentialDeliveryService(db, true));
        Assert.Equal("VALID", await service.ValidateInvitationAsync(issued.Token));
        await service.ResetAsync(issued.Token, "NewStrongPass123!", "NewStrongPass123!");
        Assert.False(user.MustChangePassword);
        Assert.Equal("USED", await service.ValidateInvitationAsync(issued.Token));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.ResetAsync(issued.Token, "OtherStrong123!", "OtherStrong123!"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminResend_BeforeAndAfterExpiry_ReplacesOldToken(bool expired)
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var user = await SeedTenantUserAsync(db, tenantId, "resend@test.test", true);
        var first = await PasswordLinkTokens.IssueAsync(db, user, PasswordLinkPurposes.Invitation);
        if (expired) { (await db.PasswordResetTokens.SingleAsync()).ExpiresAt = DateTime.UtcNow.AddMinutes(-1); await db.SaveChangesAsync(); }
        var controller = CreateUsersController(db, tenantId, new FakeUserCredentialDeliveryService(db, true));
        await controller.ResendCredentials(user.Id, CancellationToken.None);
        var tokens = await db.PasswordResetTokens.OrderBy(x => x.CreatedAt).ToListAsync();
        Assert.Equal(2, tokens.Count);
        Assert.NotEqual(tokens[0].TokenHash, tokens[1].TokenHash);
        Assert.Equal("SUPERSEDED", PasswordLinkTokens.Status(tokens[0], DateTime.UtcNow));
        Assert.Equal("VALID", PasswordLinkTokens.Status(tokens[1], DateTime.UtcNow));
        var service = CreatePasswordResetService(db, new FakeUserCredentialDeliveryService(db, true));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.ResetAsync(first.Token, "NewStrongPass123!", "NewStrongPass123!"));
    }

    [Fact]
    public async Task ReissuedToken_Authenticates_AndRevokedOrExpiredTokenCannot()
    {
        await using var db = CreateDbContext();
        var user = await SeedTenantUserAsync(db, Guid.NewGuid(), "replacement@test.test", true);
        var expired = await PasswordLinkTokens.IssueAsync(db, user, PasswordLinkPurposes.Invitation);
        var row = await db.PasswordResetTokens.SingleAsync();
        row.ExpiresAt = DateTime.UtcNow.AddSeconds(-1); await db.SaveChangesAsync();
        var service = CreatePasswordResetService(db, new FakeUserCredentialDeliveryService(db, true));
        Assert.Equal("EXPIRED", await service.ValidateInvitationAsync(expired.Token));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.ResetAsync(expired.Token, "NewStrongPass123!", "NewStrongPass123!"));
        row.RevokedAt = DateTime.UtcNow; await db.SaveChangesAsync();
        Assert.Equal("REVOKED", await service.ValidateInvitationAsync(expired.Token));
        var current = await PasswordLinkTokens.IssueAsync(db, user, PasswordLinkPurposes.Invitation);
        await service.ResetAsync(current.Token, "NewStrongPass123!", "NewStrongPass123!");
        Assert.Equal("USED", await service.ValidateInvitationAsync(current.Token));
    }

    [Fact]
    public async Task Resend_IsTenantScoped_AndRecoveryResponseDoesNotDiscloseMembership()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var user = await SeedTenantUserAsync(db, tenantId, "private@test.test", true);
        var controller = CreateUsersController(db, Guid.NewGuid(), new FakeUserCredentialDeliveryService(db, true));
        await Assert.ThrowsAsync<NotFoundException>(() => controller.ResendCredentials(user.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => controller.Get(user.Id, CancellationToken.None));
        var service = CreatePasswordResetService(db, new FakeUserCredentialDeliveryService(db, true));
        var known = await service.ResendInvitationAsync(user.Email);
        var unknown = await service.ResendInvitationAsync("unknown@test.test");
        Assert.Equal(known, unknown);
        await service.ResendInvitationAsync(user.Email);
        Assert.Single(await db.PasswordResetTokens.ToListAsync());
    }

    [Theory]
    [InlineData("Africa/Nairobi", "8 September 2026 at 8:30 AM EAT")]
    [InlineData("Africa/Lagos", "8 September 2026 at 6:30 AM Africa/Lagos")]
    [InlineData("Europe/London", "8 September 2026 at 6:30 AM Europe/London")]
    public void TenantTime_UsesConfiguredZone_WithoutChangingSecurityInstant(string zone, string expected)
    {
        var utc = new DateTime(2026, 9, 8, 5, 30, 0, DateTimeKind.Utc);
        var row = new PasswordResetToken { ExpiresAt = utc.AddHours(1) };
        Assert.Equal(expected, TenantTimeService.Format(utc, zone));
        Assert.Equal("VALID", PasswordLinkTokens.Status(row, utc));
        Assert.Equal(5, utc.Hour);
    }

    [Fact]
    public async Task TokenLifetimes_AreCentrallyConfigurable_AndDeliveryFailureKeepsToken()
    {
        await using var db = CreateDbContext();
        var user = await SeedTenantUserAsync(db, Guid.NewGuid(), "configured@test.test", true);
        var issued = await PasswordLinkTokens.IssueAsync(db, user, PasswordLinkPurposes.Invitation,
            new AuthSecurityOptions { InvitationLifetimeHours = 96 });
        var row = await db.PasswordResetTokens.SingleAsync();
        Assert.Equal(96, (issued.ExpiresAt - row.CreatedAt).TotalHours, 3);
        var controller = CreateUsersController(db, user.TenantId, new FakeUserCredentialDeliveryService(db, false));
        await controller.ResendCredentials(user.Id, CancellationToken.None);
        Assert.Equal(2, await db.PasswordResetTokens.CountAsync());
        Assert.Equal("Africa/Nairobi", TenantTimeService.DefaultTimeZone("Kenya"));
    }
}
