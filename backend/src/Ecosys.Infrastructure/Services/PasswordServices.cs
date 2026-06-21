using System.Security.Cryptography;
using System.Text;
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
    IAuditLogService auditLogService) : IPasswordResetService
{
    private const int ExpiryMinutes = 45;
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

        var now = DateTime.UtcNow;
        var existingTokens = await dbContext.PasswordResetTokens
            .Where(x => x.UserId == user.Id && !x.UsedAt.HasValue && x.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var existingToken in existingTokens)
        {
            existingToken.UsedAt = now;
        }

        var rawToken = CreateRawToken();
        var expiresAt = now.AddMinutes(ExpiryMinutes);
        dbContext.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = HashToken(rawToken),
            ExpiresAt = expiresAt,
            RequestedIp = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString()
        });

        await dbContext.SaveChangesAsync(cancellationToken);

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
            .Include(x => x.User)
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        if (resetToken is null || resetToken.User is null || resetToken.UsedAt.HasValue || resetToken.ExpiresAt <= now)
        {
            throw new Shared.Errors.ForbiddenException(InvalidLinkMessage);
        }

        await tenantSecurityPolicyService.ValidatePasswordAsync(resetToken.User.TenantId, newPassword, cancellationToken);

        resetToken.User.PasswordHash = passwordHasher.HashPassword(resetToken.User, newPassword);
        resetToken.User.MustChangePassword = false;
        resetToken.UsedAt = now;

        await userSessionService.RevokeAllForUserAsync(
            resetToken.User.Id,
            "Password reset completed.",
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

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

    private static string CreateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}
