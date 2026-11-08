using System.Net.Mail;
using System.Security.Cryptography;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Ecosys.Shared.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecosys.Infrastructure.Services;

public sealed record SelfSignupStartRequest(
    string? FullName,
    string? Email,
    string? Password,
    string? CompanyName,
    string? Phone,
    string? Country,
    string? Industry);

public interface ISelfSignupService
{
    /// <summary>Checks the form and emails a confirmation link. Nothing is created until it is opened.</summary>
    Task<string> StartAsync(SelfSignupStartRequest request, string? requestedIp, CancellationToken cancellationToken = default);

    /// <summary>Opens a confirmation link: creates the trial workspace and signs its administrator in.</summary>
    Task<LoginResult> CompleteAsync(string token, CancellationToken cancellationToken = default);
}

/// <summary>
/// Self-service sign-up for new companies. The email is confirmed before anything is created, so a
/// workspace always belongs to a mailbox its owner controls; each completed sign-up is written to the
/// audit log and the new tenant appears under Platform → Tenants.
/// </summary>
internal sealed class SelfSignupService(
    AppDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    ITenantSecurityPolicyService tenantSecurityPolicyService,
    IMvpAuthService authService,
    IEmailTemplateService emailTemplateService,
    IEmailSubjectRuleService emailSubjectRuleService,
    IEmailOutboxService emailOutboxService,
    IEmailSettingsResolver emailSettingsResolver,
    IAuditLogService auditLogService,
    IOptions<AuthSecurityOptions> authSecurityOptions,
    IOptions<SmtpOptions> smtpOptions,
    IOptions<PublicAppOptions> publicAppOptions,
    ILogger<SelfSignupService> logger) : ISelfSignupService
{
    public const string SentMessage =
        "Check your email. We've sent a link to confirm your address and open your workspace. If you already have an account, sign in or reset your password instead.";
    private const string InvalidLinkMessage = "This sign-up link is invalid or has expired. Please sign up again.";
    private const string UnavailableMessage = "Sign-up is temporarily unavailable. Please contact us and we'll set up your workspace.";
    /// <summary>Unconfirmed sign-ups allowed per email address per hour, so the form can't be used to flood a mailbox.</summary>
    private const int MaxLinksPerEmailPerHour = 3;

    public async Task<string> StartAsync(SelfSignupStartRequest request, string? requestedIp, CancellationToken cancellationToken = default)
    {
        EnsureEnabled();

        var fullName = Required(request.FullName, "Your name is required.", 200);
        var companyName = Required(request.CompanyName, "Company name is required.", 200);
        var phone = Required(request.Phone, "Phone number is required.", 50);
        var country = Required(request.Country, "Country is required.", 100);
        var industry = string.IsNullOrWhiteSpace(request.Industry) ? null : Truncate(request.Industry.Trim(), 100);
        var email = NormalizeEmail(request.Email);
        var password = request.Password ?? string.Empty;
        tenantSecurityPolicyService.ValidatePassword(password, new TenantSecurityPolicy());

        // Same answer for every address: it depends on this server's mail setup, not on who signs up.
        await EnsureEmailDeliveryIsConfiguredAsync(cancellationToken);

        // An existing account gets the same reply and no email, so the form doesn't reveal who has one.
        if (await dbContext.Users.AnyAsync(x => x.Email.ToLower() == email, cancellationToken))
        {
            return SentMessage;
        }

        var now = DateTime.UtcNow;
        var recentLinks = await dbContext.PendingSignups.CountAsync(x => x.Email == email && x.CreatedAt > now.AddHours(-1), cancellationToken);
        if (recentLinks >= MaxLinksPerEmailPerHour)
        {
            return SentMessage;
        }

        // Only the newest link works: earlier unconfirmed sign-ups for this address stop working.
        var earlier = await dbContext.PendingSignups
            .Where(x => x.Email == email && x.CompletedAt == null && x.ExpiresAt > now)
            .ToListAsync(cancellationToken);
        foreach (var previous in earlier)
        {
            previous.ExpiresAt = now;
        }

        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var signup = new PendingSignup
        {
            Email = email,
            FullName = fullName,
            CompanyName = companyName,
            Phone = phone,
            Country = country,
            Industry = industry,
            PasswordHash = passwordHasher.HashPassword(new User { Email = email, FullName = fullName }, password),
            TokenHash = PasswordLinkTokens.Hash(rawToken),
            ExpiresAt = now.AddHours(Math.Max(1, authSecurityOptions.Value.SignupLinkLifetimeHours)),
            RequestedIp = requestedIp is null ? null : Truncate(requestedIp, 100),
        };
        dbContext.PendingSignups.Add(signup);
        await dbContext.SaveChangesAsync(cancellationToken);

        await QueueConfirmationEmailAsync(signup, BuildVerifyUrl(rawToken), cancellationToken);
        return SentMessage;
    }

    public async Task<LoginResult> CompleteAsync(string token, CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new BusinessRuleException(InvalidLinkMessage);
        }

        var tokenHash = PasswordLinkTokens.Hash(token.Trim());
        var signup = await dbContext.PendingSignups.SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
        if (signup is null || signup.CompletedAt.HasValue || signup.ExpiresAt <= DateTime.UtcNow)
        {
            throw new BusinessRuleException(InvalidLinkMessage);
        }

        // Claimed first (CompletedAt is a concurrency token), so opening the link twice at once
        // can't create two workspaces.
        signup.CompletedAt = DateTime.UtcNow;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessRuleException(InvalidLinkMessage);
        }

        LoginResult result;
        try
        {
            result = await authService.CreateVerifiedSignupWorkspaceAsync(signup, cancellationToken);
        }
        catch
        {
            // Not created (its transaction rolled back): let the same link be tried again. The
            // half-built workspace is still tracked, so it is dropped before the link is reopened.
            dbContext.ChangeTracker.Clear();
            await dbContext.PendingSignups.Where(x => x.Id == signup.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(p => p.CompletedAt, (DateTime?)null), CancellationToken.None);
            throw;
        }

        signup.TenantId = result.TenantId;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            result.TenantId,
            result.UserId,
            "auth.signup.completed",
            nameof(Tenant),
            result.TenantId.ToString(),
            $"'{signup.CompanyName}' confirmed '{signup.Email}' and started a trial through self sign-up.",
            cancellationToken);
        return result;
    }

    private void EnsureEnabled()
    {
        if (!authSecurityOptions.Value.EmailVerifiedSignupEnabled)
        {
            throw new NotFoundException("Self-service sign-up is not available.");
        }
    }

    private async Task EnsureEmailDeliveryIsConfiguredAsync(CancellationToken cancellationToken)
    {
        var platformSettings = (await emailSettingsResolver.ResolveAsync(null, cancellationToken)).Settings;
        if (EmailDeliveryResolution.IsExplicitlyDisabled(platformSettings)
            || EmailDeliveryResolution.Resolve(platformSettings, null, smtpOptions.Value) is null)
        {
            logger.LogError("Self sign-up was attempted but no SMTP host is configured in the platform email settings or the Smtp configuration section.");
            throw new BusinessRuleException(UnavailableMessage);
        }
    }

    private async Task QueueConfirmationEmailAsync(PendingSignup signup, string verifyUrl, CancellationToken cancellationToken)
    {
        var platformEmail = await dbContext.EmailSettings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == PlatformConstants.RootTenantId, cancellationToken);
        var supportEmail = platformEmail?.ReplyToEmail ?? platformEmail?.SenderAddress ?? string.Empty;

        var template = await emailTemplateService.RenderPlatformTemplateAsync(
            "signup-verify-email",
            EmailTemplateVariables.WithRecipientAndActorAliases(
                signup.FullName,
                null,
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["email"] = signup.Email,
                    ["companyName"] = signup.CompanyName,
                    ["verifyLink"] = verifyUrl,
                    ["expiresAt"] = signup.ExpiresAt.ToString("u"),
                    ["platformName"] = "Ecosys",
                    ["supportEmail"] = supportEmail,
                }),
            cancellationToken);
        var subject = await emailSubjectRuleService.BuildFinalSubjectAsync(
            PlatformConstants.RootTenantId,
            "auth.signup.verify-email",
            template.Subject,
            cancellationToken: cancellationToken);

        await emailOutboxService.QueueEmailAsync(
            new QueueEmailRequest(
                PlatformConstants.RootTenantId,
                "auth.signup.verify-email",
                "signup-verify-email",
                signup.Email,
                signup.FullName,
                template.SenderNameOverride ?? platformEmail?.SenderName,
                platformEmail?.SenderAddress,
                template.ReplyToOverride ?? platformEmail?.ReplyToEmail,
                subject,
                template.HtmlBody,
                template.TextBody,
                null),
            cancellationToken);
    }

    /// <summary>The token goes after <c>#</c>, so it never reaches server logs or a Referer header.</summary>
    private string BuildVerifyUrl(string token)
    {
        var configuredUrl = publicAppOptions.Value.PublicUrl?.Trim();
        if (Uri.TryCreate(configuredUrl, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http")
        {
            return $"{uri.GetLeftPart(UriPartial.Authority)}/signup/verify#{token}";
        }

        throw new InvalidOperationException("App:PublicUrl must be configured for authentication links.");
    }

    private static string Required(string? value, string message, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessRuleException(message);
        }

        return Truncate(value.Trim(), maxLength);
    }

    private static string NormalizeEmail(string? value)
    {
        var email = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || email.Length > 256 || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
        {
            throw new BusinessRuleException("Enter a valid email address.");
        }

        return email;
    }

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
