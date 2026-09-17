using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services;

public interface IUserCredentialDeliveryService
{
    string BuildLoginUrl();
    string BuildInviteUrl(string token);
    string BuildResetPasswordUrl(string token);
    Task<UserCredentialDeliveryResult> SendAsync(
        Guid tenantId,
        User user,
        UserCredentialDeliveryRequest request,
        CancellationToken cancellationToken = default);
    Task<UserCredentialDeliveryResult> SendPasswordResetLinkAsync(
        Guid tenantId,
        User user,
        PasswordResetLinkDeliveryRequest request,
        CancellationToken cancellationToken = default);
}

public enum PasswordLinkDeliveryPurpose
{
    PasswordReset = 0,
    AccountSetup = 1,
}

public sealed record UserCredentialDeliveryRequest(
    string TemplateEventKey,
    string LoginUrl,
    string? TemporaryPassword,
    string? InviteLink,
    bool RequirePasswordChange);

public sealed record UserCredentialDeliveryResult(
    bool Success,
    string Status,
    string Message);

public sealed record PasswordResetLinkDeliveryRequest(
    string ResetLink,
    DateTime ExpiresAt,
    PasswordLinkDeliveryPurpose Purpose = PasswordLinkDeliveryPurpose.PasswordReset);

internal sealed class UserCredentialDeliveryService(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    IEmailSender emailSender,
    IEmailTemplateService emailTemplateService,
    IEmailDeliveryLogService emailDeliveryLogService,
    IEmailSubjectRuleService emailSubjectRuleService,
    ISecretEncryptionService secretEncryptionService,
    IEmailSettingsResolver emailSettingsResolver,
    IOptions<SmtpOptions> smtpOptions,
    IOptions<PublicAppOptions> publicAppOptions,
    ILogger<UserCredentialDeliveryService> logger,
    IOptions<AuthSecurityOptions>? authSecurityOptions = null) : IUserCredentialDeliveryService
{
    public string BuildLoginUrl() => $"{ResolvePublicBaseUrl()}/login";

    public string BuildInviteUrl(string token) => $"{ResolvePublicBaseUrl()}/reset-password?token={Uri.EscapeDataString(token)}&mode=invite";

    public string BuildResetPasswordUrl(string token) => $"{ResolvePublicBaseUrl()}/reset-password?token={Uri.EscapeDataString(token)}";

    public async Task<UserCredentialDeliveryResult> SendAsync(
        Guid tenantId,
        User user,
        UserCredentialDeliveryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!user.IsActive)
        {
            await WriteDeliveryLogAsync(
                tenantId,
                user.Email,
                request.TemplateEventKey,
                "Skipped",
                null,
                "Credential email was skipped because the user is inactive.",
                cancellationToken);
            return new UserCredentialDeliveryResult(false, "SkippedInactive", "Credential email was skipped because the user is inactive.");
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return new UserCredentialDeliveryResult(false, "MissingEmail", "Credential email could not be sent because the user email address is missing.");
        }

        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        var tenantSettings = await dbContext.EmailSettings.SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        var effectiveSettings = (await emailSettingsResolver.ResolveAsync(tenantId, cancellationToken)).Settings;
        var supportEmail = effectiveSettings?.ReplyToEmail
            ?? effectiveSettings?.SenderAddress
            ?? tenant?.ContactEmail
            ?? tenant?.Email
            ?? "support@ecosys.local";

        if (tenantSettings?.IsEnabled == false || EmailDeliveryResolution.IsExplicitlyDisabled(effectiveSettings))
        {
            await WriteDeliveryLogAsync(
                tenantId,
                user.Email,
                request.TemplateEventKey,
                "Skipped",
                "Disabled",
                "Credential email could not be sent because email delivery is disabled.",
                cancellationToken);
            return new UserCredentialDeliveryResult(false, "NotConfigured", "Credential email could not be sent because email delivery is disabled.");
        }

        var delivery = CreateDeliveryOptions(effectiveSettings);
        if (delivery is null)
        {
            await WriteDeliveryLogAsync(
                tenantId,
                user.Email,
                request.TemplateEventKey,
                "Failed",
                "Not configured",
                "Credential email could not be sent because SMTP settings are incomplete.",
                cancellationToken);
            return new UserCredentialDeliveryResult(false, "NotConfigured", "Credential email could not be sent because SMTP settings are incomplete.");
        }

        try
        {
            var template = await emailTemplateService.RenderTenantTemplateAsync(
                tenantId,
                request.TemplateEventKey,
                EmailTemplateVariables.WithRecipientAndActorAliases(
                    user.FullName,
                    null,
                    new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["email"] = user.Email,
                        ["temporaryPassword"] = request.TemporaryPassword,
                        ["inviteLink"] = request.InviteLink,
                        ["resetPasswordLink"] = request.InviteLink,
                        ["resetLink"] = request.InviteLink,
                        ["loginUrl"] = request.LoginUrl,
                        ["companyName"] = tenant?.CompanyName ?? tenant?.Name ?? "Ecosys",
                        ["workspaceName"] = tenant?.Name ?? tenant?.CompanyName ?? "Ecosys Workspace",
                        ["tenantName"] = tenant?.Name ?? tenant?.CompanyName ?? "Ecosys Workspace",
                        ["platformName"] = "Ecosys",
                        ["supportEmail"] = supportEmail,
                    }),
                cancellationToken);
            var finalSubject = await emailSubjectRuleService.BuildFinalSubjectAsync(
                tenantId,
                ResolveEventKey(tenantId, request.TemplateEventKey),
                template.Subject,
                tenant?.Name ?? tenant?.CompanyName,
                cancellationToken);

            if (!template.Enabled)
            {
                await WriteDeliveryLogAsync(
                    tenantId,
                    user.Email,
                    request.TemplateEventKey,
                    "Skipped",
                    "Disabled",
                    "Credential email is disabled for this template.",
                    cancellationToken,
                    finalSubject);
                return new UserCredentialDeliveryResult(false, "Disabled", "Credential email is disabled for this template.");
            }

            await emailSender.SendAsync(
                new EmailMessage(
                    user.Email,
                    finalSubject,
                    template.HtmlBody,
                    template.SenderNameOverride ?? tenantSettings?.SenderName ?? delivery.SenderName,
                    tenantSettings?.ManagedEmailAddress ?? delivery.SenderEmail,
                    template.ReplyToOverride ?? tenantSettings?.ReplyToEmail ?? delivery.ReplyToEmail,
                    IsHtml: true),
                delivery,
                cancellationToken);

            await WriteDeliveryLogAsync(
                tenantId,
                user.Email,
                request.TemplateEventKey,
                "Sent",
                null,
                null,
                cancellationToken,
                finalSubject,
                DateTime.UtcNow);
            return new UserCredentialDeliveryResult(true, "Sent", "User credentials sent.");
        }
        catch (Exception ex)
        {
            var errorCategory = ex is EmailDeliveryException emailDeliveryException
                ? emailDeliveryException.ErrorCategory
                : EmailErrorCategories.UnknownSmtpError;

            logger.LogWarning(
                ex,
                "Credential email failed for tenant {TenantId} and user {UserId}. Status {Status}.",
                tenantId,
                user.Id,
                errorCategory);

            await WriteDeliveryLogAsync(
                tenantId,
                user.Email,
                request.TemplateEventKey,
                "Failed",
                errorCategory,
                ex.Message,
                cancellationToken);

            var workspaceName = tenant?.CompanyName ?? tenant?.Name ?? "this workspace";
            return new UserCredentialDeliveryResult(false, "Failed", $"Credential email could not be sent for {workspaceName}. Please verify email settings and try again.");
        }
    }

    public async Task<UserCredentialDeliveryResult> SendPasswordResetLinkAsync(
        Guid tenantId,
        User user,
        PasswordResetLinkDeliveryRequest request,
        CancellationToken cancellationToken = default)
    {
        var isAccountSetup = request.Purpose == PasswordLinkDeliveryPurpose.AccountSetup;
        var templateKey = isAccountSetup ? "account-setup-link" : "password-reset-link";
        var deliveryLabel = isAccountSetup ? "Account setup" : "Password reset";

        if (!user.IsActive)
        {
            await WriteDeliveryLogAsync(
                tenantId,
                user.Email,
                templateKey,
                "Skipped",
                null,
                $"{deliveryLabel} email was skipped because the user is inactive.",
                cancellationToken);
            return new UserCredentialDeliveryResult(false, "SkippedInactive", $"{deliveryLabel} email was skipped because the user is inactive.");
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return new UserCredentialDeliveryResult(false, "MissingEmail", $"{deliveryLabel} email could not be sent because the user email address is missing.");
        }

        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        var tenantSettings = await dbContext.EmailSettings.SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        var effectiveSettings = (await emailSettingsResolver.ResolveAsync(tenantId, cancellationToken)).Settings;
        var supportEmail = effectiveSettings?.ReplyToEmail
            ?? effectiveSettings?.SenderAddress
            ?? tenant?.ContactEmail
            ?? tenant?.Email
            ?? "support@ecosys.local";

        if (tenantSettings?.IsEnabled == false || EmailDeliveryResolution.IsExplicitlyDisabled(effectiveSettings))
        {
            await WriteDeliveryLogAsync(
                tenantId,
                user.Email,
                templateKey,
                "Skipped",
                "Disabled",
                $"{deliveryLabel} email could not be sent because email delivery is disabled.",
                cancellationToken);
            return new UserCredentialDeliveryResult(false, "NotConfigured", $"{deliveryLabel} email could not be sent because email delivery is disabled.");
        }

        var delivery = CreateDeliveryOptions(effectiveSettings);
        if (delivery is null)
        {
            await WriteDeliveryLogAsync(
                tenantId,
                user.Email,
                templateKey,
                "Failed",
                "Not configured",
                $"{deliveryLabel} email could not be sent because SMTP settings are incomplete.",
                cancellationToken);
            return new UserCredentialDeliveryResult(false, "NotConfigured", $"{deliveryLabel} email could not be sent because SMTP settings are incomplete.");
        }

        try
        {
            var template = await emailTemplateService.RenderTenantTemplateAsync(
                tenantId,
                templateKey,
                EmailTemplateVariables.WithRecipientAndActorAliases(
                    user.FullName,
                    null,
                    new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["platformName"] = "Ecosys",
                        ["resetLink"] = request.ResetLink,
                        ["setPasswordUrl"] = request.ResetLink,
                        ["activateAccountUrl"] = request.ResetLink,
                        ["supportEmail"] = supportEmail,
                        ["tenantName"] = tenant?.Name ?? tenant?.CompanyName ?? "Ecosys Workspace",
                        ["expiresAt"] = TenantTimeService.Format(request.ExpiresAt, tenant?.TimeZoneId, tenant?.Country),
                        ["invitationLifetimeHours"] = Math.Clamp(authSecurityOptions?.Value.InvitationLifetimeHours ?? 72, 1, 720).ToString(),
                        ["role"] = user.Role,
                    }),
                cancellationToken);
            var finalSubject = await emailSubjectRuleService.BuildFinalSubjectAsync(
                tenantId,
                isAccountSetup ? "users.invited" : "auth.password-reset.requested",
                template.Subject,
                tenant?.Name ?? tenant?.CompanyName,
                cancellationToken);

            if (!template.Enabled)
            {
                await WriteDeliveryLogAsync(
                    tenantId,
                    user.Email,
                    templateKey,
                    "Skipped",
                    "Disabled",
                    $"{deliveryLabel} email is disabled for this template.",
                    cancellationToken,
                    finalSubject);
                return new UserCredentialDeliveryResult(false, "Disabled", $"{deliveryLabel} email is disabled for this template.");
            }

            await emailSender.SendAsync(
                new EmailMessage(
                    user.Email,
                    finalSubject,
                    template.HtmlBody,
                    template.SenderNameOverride ?? tenantSettings?.SenderName ?? delivery.SenderName,
                    tenantSettings?.ManagedEmailAddress ?? delivery.SenderEmail,
                    template.ReplyToOverride ?? tenantSettings?.ReplyToEmail ?? delivery.ReplyToEmail,
                    IsHtml: true),
                delivery,
                cancellationToken);

            await WriteDeliveryLogAsync(
                tenantId,
                user.Email,
                templateKey,
                "Sent",
                null,
                null,
                cancellationToken,
                finalSubject,
                DateTime.UtcNow);
            return new UserCredentialDeliveryResult(true, "Sent", isAccountSetup ? "Account setup email sent." : "Password reset email sent.");
        }
        catch (Exception ex)
        {
            var errorCategory = ex is EmailDeliveryException emailDeliveryException
                ? emailDeliveryException.ErrorCategory
                : EmailErrorCategories.UnknownSmtpError;

            logger.LogWarning(
                ex,
                "{DeliveryLabel} email failed for tenant {TenantId} and user {UserId}. Status {Status}.",
                deliveryLabel,
                tenantId,
                user.Id,
                errorCategory);

            await WriteDeliveryLogAsync(
                tenantId,
                user.Email,
                templateKey,
                "Failed",
                errorCategory,
                ex.Message,
                cancellationToken);

            return new UserCredentialDeliveryResult(false, "Failed", $"{deliveryLabel} email could not be sent. Please verify email settings and try again.");
        }
    }

    private Task WriteDeliveryLogAsync(
        Guid tenantId,
        string? recipientEmail,
        string templateKey,
        string status,
        string? errorCategory,
        string? errorMessage,
        CancellationToken cancellationToken,
        string? subject = null,
        DateTime? sentAt = null)
    {
        return emailDeliveryLogService.LogAsync(
            new EmailDeliveryLogWriteRequest(
                tenantId,
                ResolveEventKey(tenantId, templateKey),
                templateKey,
                string.IsNullOrWhiteSpace(recipientEmail) ? "unknown@local" : recipientEmail,
                string.IsNullOrWhiteSpace(subject) ? $"Credential delivery - {templateKey}" : subject,
                status,
                errorCategory,
                errorMessage,
                null,
                sentAt),
            cancellationToken);
    }

    private static string ResolveEventKey(Guid tenantId, string templateKey)
    {
        var isPlatform = tenantId == PlatformConstants.RootTenantId;
        return templateKey switch
        {
            "user-credentials" when isPlatform => "platform.user.created",
            "user-credentials" => "tenant.user.created",
            "account-setup-link" => "users.invited",
            "resend-credentials" when isPlatform => "platform.user.credentials.resent",
            "resend-credentials" => "tenant.user.credentials.resent",
            "password-reset" => "user.password-reset.admin",
            "tenant-onboarding" => "tenant.onboarding",
            _ => templateKey,
        };
    }

    /// <summary>
    /// Delegates to the one shared resolver/merge pipeline (see EmailSettingsResolver.cs) instead of
    /// the bespoke lookup this used to do — which returned null (and gave up on sending) whenever no
    /// DB row existed, never falling back to the Smtp:*/ECOSYS_SMTP_* configuration the platform's
    /// own sender already honors.
    /// </summary>
    private EmailDeliverySettings? CreateDeliveryOptions(EmailSetting? settings) =>
        EmailDeliveryResolution.Resolve(
            settings,
            secretEncryptionService.Decrypt(settings?.EncryptedSecret) ?? settings?.Password,
            smtpOptions.Value);
    private string ResolvePublicBaseUrl()
    {
        var configuredUrl = publicAppOptions.Value.PublicUrl?.Trim();
        if (Uri.TryCreate(configuredUrl, UriKind.Absolute, out var uri)
            && uri.Scheme is "https" or "http" && string.IsNullOrEmpty(uri.UserInfo)
            && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment))
        {
            return uri.AbsoluteUri.TrimEnd('/');
        }

        throw new InvalidOperationException("App:PublicUrl must be configured for authentication links.");
    }
}
