using System.Security.Cryptography;
using System.Text;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Contracts.Integration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecosys.Infrastructure.Services;

public interface ITemporaryPasswordService
{
    string Generate(int minimumLength = 14);
}

/// <summary>
/// Raised when self-service password reset cannot run because no mailbox is configured anywhere —
/// neither the platform settings row nor <c>Smtp:*</c> / <c>ECOSYS_SMTP_*</c>. Deliberately raised
/// before the account lookup and from the platform scope, so the answer depends only on the server's
/// own configuration and never on whether the submitted address exists (EC-P1-06d).
/// </summary>
public sealed class PasswordResetUnavailableException(string message)
    : Shared.Errors.AppException(message), Shared.Errors.ILocalizedErrorCode
{
    public override int StatusCode => 503;
    public string ErrorCode => "EMAIL_NOT_CONFIGURED";
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
    IAuditLogService auditLogService,
    IEmailSettingsResolver emailSettingsResolver,
    IOptions<Shared.Options.AuthSecurityOptions> authSecurityOptions,
    IOptions<Shared.Options.SmtpOptions> smtpOptions,
    ILogger<PasswordResetService> logger) : IPasswordResetService
{
    private const int MinimumExpiryMinutes = 15;
    private const int DefaultExpiryMinutes = 120;
    private const string GenericMessage = "If an account exists for this email, password reset instructions will be sent shortly.";
    private const string SuccessMessage = "Your password has been reset. You can now sign in.";
    private const string InvalidLinkMessage = "This password reset link is invalid or has expired. Please request a new one.";
    private const string UnavailableMessage = "Password reset is temporarily unavailable because email delivery is not configured. Please contact your administrator.";

    public async Task<string> RequestAsync(string email, CancellationToken cancellationToken = default)
    {
        // Deliverability is checked first, at platform scope, and its answer is the same for every
        // caller: it depends on this server's configuration, not on the address that was submitted.
        // Reporting success while no mailbox exists anywhere is what stranded reset requests
        // silently, but the check has to stay address-independent or it becomes an account
        // enumeration oracle — so it runs before the lookup below and cannot see the address at all.
        // A per-recipient failure after this point (that mailbox rejected, that tenant's own SMTP
        // broken) is address-dependent by nature and stays behind GenericMessage; it is logged and
        // audited instead (EC-P1-06d).
        await EnsureEmailDeliveryIsConfiguredAsync(cancellationToken);

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
        var expiresAt = now.AddMinutes(ResolveExpiryMinutes());
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

        if (resetToken is null || resetToken.User is null)
        {
            logger.LogWarning("Password reset rejected: the token was not found.");
            throw new Shared.Errors.ForbiddenException(InvalidLinkMessage);
        }

        if (resetToken.UsedAt.HasValue)
        {
            logger.LogWarning(
                "Password reset rejected for user {UserId}: the token was already used at {UsedAt:o}.",
                resetToken.UserId,
                resetToken.UsedAt.Value);
            throw new Shared.Errors.ForbiddenException(InvalidLinkMessage);
        }

        // Rows written before the column carried an explicit UTC kind come back as Unspecified; compare
        // like-for-like so a valid link is never rejected as expired.
        var expiresAtUtc = DateTimeNormalizationHelper.NormalizeToUtc(resetToken.ExpiresAt);
        if (expiresAtUtc <= now)
        {
            logger.LogWarning(
                "Password reset rejected for user {UserId}: the token expired at {ExpiresAt:o} (now {Now:o}).",
                resetToken.UserId,
                expiresAtUtc,
                now);
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

    /// <summary>
    /// Resolves the platform mailbox exactly as a send would — database row first, then the
    /// <c>Smtp:*</c> configuration section — and refuses the request when neither supplies a host.
    /// Platform mail is what every tenant without its own override sends through, so "nothing here"
    /// means nothing can be delivered for anyone.
    /// </summary>
    private async Task EnsureEmailDeliveryIsConfiguredAsync(CancellationToken cancellationToken)
    {
        var platformSettings = (await emailSettingsResolver.ResolveAsync(null, cancellationToken)).Settings;

        if (EmailDeliveryResolution.IsExplicitlyDisabled(platformSettings)
            || EmailDeliveryResolution.Resolve(platformSettings, null, smtpOptions.Value) is null)
        {
            logger.LogError("Password reset was requested but no SMTP host is configured in the platform email settings or the Smtp configuration section.");
            throw new PasswordResetUnavailableException(UnavailableMessage);
        }
    }

    /// <summary>
    /// Reset links have to survive SMTP queueing and retry delays before the recipient ever opens the
    /// mail, so an operator-supplied window below <see cref="MinimumExpiryMinutes"/> is treated as a
    /// misconfiguration rather than honoured.
    /// </summary>
    private int ResolveExpiryMinutes()
    {
        var configured = authSecurityOptions.Value.PasswordResetExpiryMinutes;
        return configured <= 0 ? DefaultExpiryMinutes : Math.Max(MinimumExpiryMinutes, configured);
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
