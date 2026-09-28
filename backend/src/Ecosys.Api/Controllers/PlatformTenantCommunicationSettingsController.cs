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
    private static readonly string[] AllowedNotificationKeys =
    [
        "work-order.new-created",
        "work-order.assigned",
        "work-order.updated",
        "work-order.completed",
        "work-order.overdue",
        "sla.warning",
        "sla.breach",
        "pm.due",
        "pm.overdue",
        "pm.completed",
        "asset.created",
        "asset.updated",
        "asset.deactivated",
        "asset.maintenance-due",
        "materials.request-submitted",
        "materials.request-approved",
        "materials.request-rejected",
        "materials.request-issued",
        "materials.low-stock-alert",
        "users.invited",
        "users.activated",
        "users.deactivated",
        "users.role-changed",
        "tenant.profile-updated",
        "tenant.branding-updated",
        "tenant.module-access-changed",
        "tenant.subscription-status-changed",
        "system.alerts",
        "system.failed-login-attempts",
        "system.integration-errors",
        "system.background-job-failures"
    ];

    private static readonly HashSet<string> AllowedNotificationKeySet = AllowedNotificationKeys
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

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

    [HttpGet("notification-settings")]
    public async Task<ActionResult<TenantNotificationSettingsResponse>> GetNotificationSettings(Guid tenantId, CancellationToken cancellationToken)
    {
        _ = await LoadManagedTenantAsync(tenantId, cancellationToken);
        var notificationSettings = await EnsureAndLoadNotificationSettingsAsync(tenantId, cancellationToken);
        var recipients = await LoadRecipientsAsync(tenantId, cancellationToken);
        return Ok(new TenantNotificationSettingsResponse(
            notificationSettings.Select(MapNotificationSetting).ToList(),
            recipients.Select(MapRecipient).ToList()));
    }

    [HttpPut("notification-settings")]
    public async Task<ActionResult<TenantNotificationSettingsResponse>> UpdateNotificationSettings(Guid tenantId, [FromBody] TenantNotificationSettingsRequest request, CancellationToken cancellationToken)
    {
        _ = await LoadManagedTenantAsync(tenantId, cancellationToken);
        await UpsertNotificationSettingsAsync(tenantId, request.NotificationSettings, cancellationToken);
        await ReplaceRecipientsAsync(tenantId, request.Recipients, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync(tenantId, "platform.tenant.communication.notifications.updated", "Tenant notification settings were updated.", cancellationToken);

        var notificationSettings = await EnsureAndLoadNotificationSettingsAsync(tenantId, cancellationToken);
        var recipients = await LoadRecipientsAsync(tenantId, cancellationToken);
        return Ok(new TenantNotificationSettingsResponse(
            notificationSettings.Select(MapNotificationSetting).ToList(),
            recipients.Select(MapRecipient).ToList()));
    }

    [HttpGet("communication-settings")]
    public async Task<ActionResult<TenantCommunicationSettingsResponse>> GetCommunicationSettings(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await LoadManagedTenantAsync(tenantId, cancellationToken);
        var email = await GetOrCreateTenantEmailSettingAsync(tenant, cancellationToken);
        var notificationSettings = await EnsureAndLoadNotificationSettingsAsync(tenantId, cancellationToken);
        var recipients = await LoadRecipientsAsync(tenantId, cancellationToken);

        return Ok(new TenantCommunicationSettingsResponse(
            MapEmail(email),
            notificationSettings.Select(MapNotificationSetting).ToList(),
            recipients.Select(MapRecipient).ToList()));
    }

    private async Task<Tenant> LoadManagedTenantAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await dbContext.Tenants.SingleOrDefaultAsync(x => x.Id == tenantId && x.Id != PlatformConstants.RootTenantId, cancellationToken)
        ?? throw new NotFoundException("Tenant was not found.");

    private async Task<EmailSetting> GetOrCreateTenantEmailSettingAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        var setting = await dbContext.EmailSettings.SingleOrDefaultAsync(x => x.TenantId == tenant.Id, cancellationToken);
        if (setting is not null)
        {
            return setting;
        }

        // Same identity tenant provisioning creates — never a row whose sender is the tenant's contact email.
        setting = TenantEmailIdentity.Create(tenant);
        setting.CreatedByUserId = tenantContext.GetRequiredUserId();
        setting.UpdatedByUserId = tenantContext.GetRequiredUserId();

        dbContext.EmailSettings.Add(setting);
        await dbContext.SaveChangesAsync(cancellationToken);
        return setting;
    }

    private async Task<List<TenantNotificationSetting>> EnsureAndLoadNotificationSettingsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var current = await dbContext.TenantNotificationSettings
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var hasChanges = false;
        foreach (var key in AllowedNotificationKeys)
        {
            if (current.Any(x => string.Equals(x.NotificationKey, key, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var row = new TenantNotificationSetting
            {
                TenantId = tenantId,
                NotificationKey = key,
                EmailEnabled = true,
                InAppEnabled = key is "system.failed-login-attempts" or "system.integration-errors" or "system.background-job-failures",
                SmsEnabled = false,
                IsActive = true
            };
            dbContext.TenantNotificationSettings.Add(row);
            current.Add(row);
            hasChanges = true;
        }

        if (hasChanges)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return current
            .OrderBy(x => x.NotificationKey)
            .ToList();
    }

    private async Task<List<TenantNotificationRecipient>> LoadRecipientsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.TenantNotificationRecipients
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.RecipientGroup)
            .ThenBy(x => x.Email)
            .ToListAsync(cancellationToken);
    }

    private async Task UpsertNotificationSettingsAsync(Guid tenantId, IReadOnlyCollection<TenantNotificationSettingRequest> requests, CancellationToken cancellationToken)
    {
        if (requests.Count == 0)
        {
            throw new BusinessRuleException("At least one notification setting is required.");
        }

        var normalized = requests
            .Where(x => !string.IsNullOrWhiteSpace(x.NotificationKey))
            .Select(x => new TenantNotificationSettingRequest(
                x.NotificationKey.Trim().ToLowerInvariant(),
                x.EmailEnabled,
                x.InAppEnabled,
                x.SmsEnabled,
                x.IsActive))
            .DistinctBy(x => x.NotificationKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
        {
            throw new BusinessRuleException("At least one notification setting is required.");
        }

        foreach (var row in normalized)
        {
            if (!AllowedNotificationKeySet.Contains(row.NotificationKey))
            {
                throw new BusinessRuleException($"Unsupported notification key: {row.NotificationKey}");
            }
        }

        var current = await EnsureAndLoadNotificationSettingsAsync(tenantId, cancellationToken);
        var map = current.ToDictionary(x => x.NotificationKey, x => x, StringComparer.OrdinalIgnoreCase);

        foreach (var input in normalized)
        {
            if (!map.TryGetValue(input.NotificationKey, out var target))
            {
                target = new TenantNotificationSetting
                {
                    TenantId = tenantId,
                    NotificationKey = input.NotificationKey
                };
                dbContext.TenantNotificationSettings.Add(target);
                map[input.NotificationKey] = target;
            }

            target.EmailEnabled = input.EmailEnabled;
            target.InAppEnabled = input.InAppEnabled;
            target.SmsEnabled = input.SmsEnabled;
            target.IsActive = input.IsActive;
        }
    }

    private async Task ReplaceRecipientsAsync(Guid tenantId, IReadOnlyCollection<TenantNotificationRecipientRequest> requests, CancellationToken cancellationToken)
    {
        var normalized = requests
            .Where(x => !string.IsNullOrWhiteSpace(x.RecipientGroup) && !string.IsNullOrWhiteSpace(x.Email))
            .Select(x => new TenantNotificationRecipientRequest(
                NormalizeRecipientGroup(x.RecipientGroup),
                NormalizeEmail(x.Email, "Recipient email must be a valid email address."),
                x.IsActive))
            .DistinctBy(x => $"{x.RecipientGroup}|{x.Email}", StringComparer.OrdinalIgnoreCase)
            .ToList();

        var current = await dbContext.TenantNotificationRecipients
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        if (current.Count > 0)
        {
            dbContext.TenantNotificationRecipients.RemoveRange(current);
        }

        foreach (var row in normalized)
        {
            dbContext.TenantNotificationRecipients.Add(new TenantNotificationRecipient
            {
                TenantId = tenantId,
                RecipientGroup = row.RecipientGroup,
                Email = row.Email,
                IsActive = row.IsActive
            });
        }
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
            setting.ManagedEmailStatusDetail);

    private static TenantNotificationSettingResponse MapNotificationSetting(TenantNotificationSetting setting) =>
        new(
            setting.NotificationKey,
            setting.EmailEnabled,
            setting.InAppEnabled,
            setting.SmsEnabled,
            setting.IsActive);

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
