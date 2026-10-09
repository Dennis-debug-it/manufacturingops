using System.Net.Mail;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Errors;
using Ecosys.Shared.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize(Policy = "PlatformSettingsAccess")]
[Route("api/platform/tenants/{tenantId:guid}")]
public sealed class PlatformTenantCommunicationSettingsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    ISecretEncryptionService secretEncryptionService,
    IEmailSender emailSender,
    IEmailSettingsResolver emailSettingsResolver,
    IOptions<SmtpOptions> smtpOptions,
    IEmailTemplateService emailTemplateService,
    IEmailSubjectRuleService emailSubjectRuleService,
    IEmailOutboxService emailOutboxService,
    IAuditLogService auditLogService,
    ILogger<PlatformTenantCommunicationSettingsController> logger) : ControllerBase
{
    private static readonly string[] AllowedRecipientGroups =
    [
        "Admin",
        "Operations",
        "SLAEscalation",
        "Dispatch",
        "Maintenance",
        "Assets",
        "Materials",
        "SystemAlerts",
        "Billing",
        "Finance",
        "Invoice",
        "Receipt",
        "Statement",
        "BillingCC",
        "InvoiceCC",
        "ReceiptCC",
        "StatementCC",
        "Additional"
    ];

    [HttpGet("email-settings")]
    public async Task<ActionResult<TenantEmailSettingsResponse>> GetEmailSettings(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await LoadManagedTenantAsync(tenantId, cancellationToken);
        var email = await GetOrCreateTenantEmailSettingAsync(tenant, cancellationToken);
        return Ok(MapEmail(email));
    }

    [HttpPost("email-settings/test")]
    public async Task<ActionResult<TenantCommunicationActionResponse>> SendTestEmail(Guid tenantId, [FromBody] TenantCommunicationActionRequest request, CancellationToken cancellationToken)
    {
        var tenant = await LoadManagedTenantAsync(tenantId, cancellationToken);
        var tenantEmail = await GetOrCreateTenantEmailSettingAsync(tenant, cancellationToken);
        var deliverySettings = (await emailSettingsResolver.ResolveAsync(tenantId, cancellationToken)).Settings
            ?? throw new BusinessRuleException("Email settings are not configured.");
        var recipient = string.IsNullOrWhiteSpace(request.TestRecipientEmail)
            ? tenantEmail.ManagedEmailAddress ?? deliverySettings.SenderAddress
            : NormalizeEmail(request.TestRecipientEmail, "Test recipient email must be a valid email address.");

        if (string.IsNullOrWhiteSpace(recipient))
        {
            throw new BusinessRuleException("A test recipient email is required.");
        }

        var template = await emailTemplateService.RenderPlatformTemplateAsync(
            "test-email",
            EmailTemplateVariables.WithRecipientAndActorAliases(
                recipient,
                tenantContext.Email,
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["platformName"] = "Ecosys",
                    ["senderName"] = deliverySettings.SenderName ?? tenant.Name,
                    ["sentAt"] = DateTime.UtcNow.ToString("u"),
                    ["tenantName"] = tenant.Name,
                }),
            cancellationToken);

        var finalSubject = await emailSubjectRuleService.BuildFinalSubjectAsync(
            tenantId,
            "email.test",
            template.Subject,
            tenant.Name,
            cancellationToken);

        logger.LogInformation(
            "Tenant email test queued for tenant {TenantId}. Mode {DeliveryMode}, host {Host}, port {Port}, secure mode {SecureMode}, sender {SenderEmail}.",
            tenantId,
            ParseDeliveryMode(deliverySettings.Provider),
            deliverySettings.Host,
            deliverySettings.Port,
            EmailDeliveryModeResolver.ResolveSecureMode(deliverySettings.Port, deliverySettings.UseSsl),
            deliverySettings.SenderAddress);

        await emailOutboxService.QueueEmailAsync(
            new QueueEmailRequest(
                tenantId,
                "email.test",
                "test-email",
                recipient,
                recipient,
                template.SenderNameOverride ?? deliverySettings.SenderName,
                deliverySettings.SenderAddress,
                template.ReplyToOverride ?? deliverySettings.ReplyToEmail,
                finalSubject,
                template.HtmlBody,
                template.TextBody,
                tenantContext.GetRequiredUserId()),
            cancellationToken);

        tenantEmail.LastError = null;
        tenantEmail.LastTestedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync(tenantId, "platform.tenant.communication.email.test", "Tenant email test was queued.", cancellationToken);
        return Ok(new TenantCommunicationActionResponse(true, tenantEmail.LastTestedAt, null));
    }

    [HttpPost("email-settings/verify")]
    public async Task<ActionResult<TenantCommunicationActionResponse>> VerifyDeliveryConnection(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await LoadManagedTenantAsync(tenantId, cancellationToken);
        var tenantEmail = await GetOrCreateTenantEmailSettingAsync(tenant, cancellationToken);
        var deliverySettings = (await emailSettingsResolver.ResolveAsync(tenantId, cancellationToken)).Settings
            ?? throw new BusinessRuleException("Email settings are not configured.");

        if (string.IsNullOrWhiteSpace(deliverySettings.Host) && !string.Equals(deliverySettings.Provider, EmailDeliveryMode.Disabled.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("SMTP host is not configured.");
        }

        try
        {
            var delivery = CreateDeliveryOptions(deliverySettings)
                ?? throw new BusinessRuleException("SMTP host is not configured.");
            logger.LogInformation(
                "Tenant email verification requested for tenant {TenantId}. Mode {DeliveryMode}, host {Host}, port {Port}, secure mode {SecureMode}, sender {SenderEmail}.",
                tenantId,
                delivery.DeliveryMode,
                delivery.SmtpHost,
                delivery.SmtpPort,
                delivery.SecureMode,
                delivery.SenderEmail);

            var verification = await emailSender.VerifyAsync(delivery, cancellationToken);
            // Connecting and logging in proves the transport, not the tenant's sender: a server that verifies
            // senders only refuses an unprovisioned {slug}@serviceops address at MAIL FROM / RCPT time. The
            // managed address is only reported usable once the server has accepted it.
            var sender = TenantEmailIdentity.ResolveSender(tenant, tenantEmail);
            if (verification.Success && delivery.DeliveryMode == EmailDeliveryMode.Smtp && sender is not null)
            {
                verification = await emailSender.VerifySenderAsync(delivery, sender.FromAddress, cancellationToken);
                TenantEmailIdentity.RecordSenderOutcome(tenantEmail, verification.Success, verification.ErrorCategory, verification.TechnicalMessage, DateTime.UtcNow);
            }

            tenantEmail.LastError = null;
            tenantEmail.LastTestedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            await WriteAuditAsync(tenantId, "platform.tenant.communication.email.verify", "Tenant email delivery connection was verified.", cancellationToken);
            if (verification.Success)
            {
                return Ok(new TenantCommunicationActionResponse(true, tenantEmail.LastTestedAt, null));
            }

            tenantEmail.LastError = verification.ErrorCategory ?? EmailErrorCategories.UnknownSmtpError;
            await dbContext.SaveChangesAsync(cancellationToken);
            return Ok(new TenantCommunicationActionResponse(false, tenantEmail.LastTestedAt, tenantEmail.LastError));
        }
        catch (Exception ex)
        {
            tenantEmail.LastError = ex is EmailDeliveryException deliveryException
                ? deliveryException.ErrorCategory
                : EmailErrorCategories.UnknownSmtpError;
            tenantEmail.LastTestedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogWarning(ex, "Tenant email verification failed for tenant {TenantId}.", tenantId);
            return Ok(new TenantCommunicationActionResponse(false, tenantEmail.LastTestedAt, tenantEmail.LastError));
        }
    }

    [HttpGet("communication-settings")]
    public async Task<ActionResult<TenantCommunicationSettingsResponse>> GetCommunicationSettings(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await LoadManagedTenantAsync(tenantId, cancellationToken);
        var email = await GetOrCreateTenantEmailSettingAsync(tenant, cancellationToken);
        var recipients = await LoadRecipientsAsync(tenantId, cancellationToken);

        return Ok(new TenantCommunicationSettingsResponse(
            MapEmail(email),
            recipients.Select(MapRecipient).ToList()));
    }

    private async Task<Tenant> LoadManagedTenantAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await dbContext.Tenants.SingleOrDefaultAsync(x => x.Id == tenantId && x.Id != PlatformConstants.RootTenantId, cancellationToken)
        ?? throw new NotFoundException("Tenant was not found.");

    private Task<EmailSetting> GetOrCreateTenantEmailSettingAsync(Tenant tenant, CancellationToken cancellationToken) =>
        dbContext.GetOrCreateAsync(
            dbContext.EmailSettings.Where(x => x.TenantId == tenant.Id),
            () =>
            {
                // Same identity tenant provisioning creates — never a row whose sender is the tenant's contact email.
                var setting = TenantEmailIdentity.Create(tenant);
                setting.CreatedByUserId = tenantContext.GetRequiredUserId();
                setting.UpdatedByUserId = tenantContext.GetRequiredUserId();
                return setting;
            },
            cancellationToken);

    private async Task<List<TenantNotificationRecipient>> LoadRecipientsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.TenantNotificationRecipients
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.RecipientGroup)
            .ThenBy(x => x.Email)
            .ToListAsync(cancellationToken);
    }

    private async Task WriteAuditAsync(Guid tenantId, string action, string details, CancellationToken cancellationToken)
    {
        await auditLogService.LogAsync(
            tenantId,
            tenantContext.GetRequiredUserId(),
            action,
            "TenantCommunicationSettings",
            tenantId.ToString(),
            details,
            cancellationToken);
    }

    private static TenantEmailSettingsResponse MapEmail(EmailSetting setting) =>
        new(
            setting.Id,
            setting.TenantId,
            string.IsNullOrWhiteSpace(setting.SenderName) ? null : setting.SenderName,
            setting.ManagedEmailAddress ?? (string.IsNullOrWhiteSpace(setting.SenderAddress) ? null : setting.SenderAddress),
            setting.ReplyToEmail,
            setting.IsEnabled,
            setting.LastTestedAt,
            setting.LastError,
            setting.ManagedEmailStatus,
            setting.ManagedEmailStatusCheckedAt,
            setting.ManagedEmailStatusDetail,
            UsesOwnSmtp: TenantEmailIdentity.UsesOwnSmtp(setting),
            OwnSmtpHost: TenantEmailIdentity.UsesOwnSmtp(setting) ? setting.Host : null,
            OwnFromAddress: TenantEmailIdentity.UsesOwnSmtp(setting) ? setting.CustomFromAddress : null);

    private static TenantNotificationRecipientResponse MapRecipient(TenantNotificationRecipient recipient) =>
        new(
            recipient.RecipientGroup,
            recipient.Email,
            recipient.IsActive);

    /// <summary>Delegates to the one shared resolver/merge pipeline — see EmailSettingsResolver.cs.</summary>
    private EmailDeliverySettings? CreateDeliveryOptions(EmailSetting? settings) =>
        EmailDeliveryResolution.Resolve(
            settings,
            secretEncryptionService.Decrypt(settings?.EncryptedSecret) ?? settings?.Password,
            smtpOptions.Value);

    private static EmailDeliveryMode ParseDeliveryMode(string? value)
    {
        if (Enum.TryParse<EmailDeliveryMode>(value, true, out var parsed))
        {
            return parsed;
        }

        return EmailDeliveryMode.Smtp;
    }

    private static string NormalizeRecipientGroup(string value)
    {
        var candidate = value.Trim();
        var match = AllowedRecipientGroups.FirstOrDefault(x => string.Equals(x, candidate, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            throw new BusinessRuleException($"Unsupported recipient group: {value}");
        }

        return match;
    }

    private static string NormalizeEmail(string? value, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessRuleException(errorMessage);
        }

        try
        {
            return new MailAddress(value.Trim()).Address.ToLowerInvariant();
        }
        catch (FormatException)
        {
            throw new BusinessRuleException(errorMessage);
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

}
