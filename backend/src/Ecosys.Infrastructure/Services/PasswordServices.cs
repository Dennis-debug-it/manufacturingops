using System.Security.Cryptography;
using System.Text;
using Ecosys.Shared.Options;
using Microsoft.Extensions.Options;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Contracts.Integration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

public interface ITemporaryPasswordService
{
    string Generate(int minimumLength = 14);
}

public interface IPasswordResetService
{
    Task<string> RequestAsync(string email, CancellationToken cancellationToken = default);
    Task<string> ValidateInvitationAsync(string token, CancellationToken cancellationToken = default);
    Task<string> ResendInvitationAsync(string email, CancellationToken cancellationToken = default);
    Task<string> ResetAsync(string token, string newPassword, string confirmPassword, CancellationToken cancellationToken = default);
}

public sealed class TemporaryPasswordService : ITemporaryPasswordService
{
    private const string Uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lowercase = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%^&*()-_=+[]{}?";
    private const string AllCharacters = Uppercase + Lowercase + Digits + Symbols;

    public string Generate(int minimumLength = 14)
    {
        var length = Math.Max(12, minimumLength);
        var characters = new char[length];
        characters[0] = GetRandomCharacter(Uppercase);
        characters[1] = GetRandomCharacter(Lowercase);
        characters[2] = GetRandomCharacter(Digits);
        characters[3] = GetRandomCharacter(Symbols);

        for (var index = 4; index < characters.Length; index += 1)
        {
            characters[index] = GetRandomCharacter(AllCharacters);
        }

        Shuffle(characters);
        return new string(characters);
    }

    private static char GetRandomCharacter(string source) => source[RandomNumberGenerator.GetInt32(source.Length)];

    private static void Shuffle(Span<char> values)
    {
        for (var index = values.Length - 1; index > 0; index -= 1)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }
    }
}

public sealed class PasswordResetService(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    IPasswordHasher<User> passwordHasher,
    ITenantSecurityPolicyService tenantSecurityPolicyService,
    IUserSessionService userSessionService,
    IUserCredentialDeliveryService credentialDeliveryService,
    IAuditLogService auditLogService,
    IOptions<AuthSecurityOptions>? authSecurityOptions = null) : IPasswordResetService
{
    private const string GenericMessage = "If an account exists for this email, password reset instructions will be sent shortly.";
    private const string SuccessMessage = "Your password has been reset. You can now sign in.";
    private const string InvalidLinkMessage = "This password reset link is invalid or has expired. Please request a new one.";

    public async Task<string> RequestAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return GenericMessage;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await dbContext.Users
            .Include(x => x.Tenant)
            .SingleOrDefaultAsync(x => x.Email.ToLower() == normalizedEmail && x.IsActive, cancellationToken);

        if (user is null || user.Tenant is null || !user.Tenant.IsActive)
        {
            return GenericMessage;
        }

        var issued = await PasswordLinkTokens.IssueAsync(dbContext, user, PasswordLinkPurposes.PasswordReset,
            authSecurityOptions?.Value, cancellationToken);
        var rawToken = issued.Token;
        var expiresAt = issued.ExpiresAt;

        var resetLink = credentialDeliveryService.BuildResetPasswordUrl(rawToken);
        var delivery = await credentialDeliveryService.SendPasswordResetLinkAsync(
            user.TenantId,
            user,
            new PasswordResetLinkDeliveryRequest(resetLink, expiresAt, PasswordLinkDeliveryPurpose.PasswordReset),
            cancellationToken);

        await auditLogService.LogAsync(
            user.TenantId,
            user.Id,
            "auth.password-reset.requested",
            nameof(User),
            user.Id.ToString(),
            delivery.Success
                ? $"Password reset requested for '{user.Email}'."
                : $"Password reset requested for '{user.Email}', but email delivery was not completed.",
            severity: "Warning",
            cancellationToken: cancellationToken);

        return GenericMessage;
    }

    public async Task<string> ResetAsync(string token, string newPassword, string confirmPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new Shared.Errors.BusinessRuleException(InvalidLinkMessage);
        }

        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
        {
            throw new Shared.Errors.BusinessRuleException("New password and confirmation password must match.");
        }

        var tokenHash = HashToken(token.Trim());
        var now = DateTime.UtcNow;
        var resetToken = await dbContext.PasswordResetTokens
            .Include(x => x.User).ThenInclude(x => x!.Tenant)
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        if (resetToken is null
            || resetToken.User is null
            || resetToken.Purpose is not (PasswordLinkPurposes.PasswordReset or PasswordLinkPurposes.Invitation)
            || PasswordLinkTokens.Status(resetToken, now) != "VALID"
            || !resetToken.User.IsActive
            || resetToken.User.Tenant?.IsActive != true)
        {
            throw new Shared.Errors.ForbiddenException(InvalidLinkMessage);
        }

        await tenantSecurityPolicyService.ValidatePasswordAsync(resetToken.User.TenantId, newPassword, cancellationToken);

        resetToken.User.PasswordHash = passwordHasher.HashPassword(resetToken.User, newPassword);
        resetToken.User.MustChangePassword = false;
        resetToken.UsedAt = now;

        try
        {
            await userSessionService.RevokeAllForUserAsync(resetToken.User.Id, "Password reset completed.", cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException) { throw new Shared.Errors.ForbiddenException(InvalidLinkMessage); }


        await auditLogService.LogAsync(
            resetToken.User.TenantId,
            resetToken.User.Id,
            "auth.password-reset.completed",
            nameof(User),
            resetToken.User.Id.ToString(),
            $"Password reset completed for '{resetToken.User.Email}'.",
            severity: "Warning",
            cancellationToken: cancellationToken);

        return SuccessMessage;
    }

    public async Task<string> ValidateInvitationAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 512) return "INVALID";
        var hash = PasswordLinkTokens.Hash(token.Trim());
        var invitation = await dbContext.PasswordResetTokens.Include(x => x.User).ThenInclude(x => x!.Tenant)
            .SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (invitation?.User is null
            || invitation.Purpose != PasswordLinkPurposes.Invitation
            || !invitation.User.IsActive
            || invitation.User.Tenant?.IsActive != true) return "INVALID";
        return PasswordLinkTokens.Status(invitation, DateTime.UtcNow);
    }

    public async Task<string> ResendInvitationAsync(string email, CancellationToken cancellationToken = default)
    {
        const string message = "If a pending ServiceOps invitation exists for this email, a new invitation has been sent.";
        if (string.IsNullOrWhiteSpace(email)) return message;
        var normalized = email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.Include(x => x.Tenant)
            .SingleOrDefaultAsync(x => x.Email.ToLower() == normalized && x.IsActive && x.MustChangePassword, cancellationToken);
        if (user?.Tenant?.IsActive != true) return message;
        var recent = await dbContext.PasswordResetTokens.AnyAsync(x => x.UserId == user.Id
            && x.Purpose == PasswordLinkPurposes.Invitation && x.CreatedAt > DateTime.UtcNow.AddMinutes(-1), cancellationToken);
        if (recent) return message;
        var issued = await PasswordLinkTokens.IssueAsync(dbContext, user, PasswordLinkPurposes.Invitation,
            authSecurityOptions?.Value, cancellationToken);
        var delivery = await credentialDeliveryService.SendPasswordResetLinkAsync(user.TenantId, user,
            new PasswordResetLinkDeliveryRequest(credentialDeliveryService.BuildInviteUrl(issued.Token), issued.ExpiresAt,
                PasswordLinkDeliveryPurpose.AccountSetup), cancellationToken);
        if (delivery.Success) { user.LastCredentialSentAt = DateTime.UtcNow; await dbContext.SaveChangesAsync(cancellationToken); }
        await auditLogService.LogAsync(user.TenantId, user.Id, "users.invitation.reissued", nameof(User), user.Id.ToString(),
            "Self-service invitation recovery requested.", cancellationToken);
        return message;
    }

    private static string HashToken(string token) => PasswordLinkTokens.Hash(token);
}
