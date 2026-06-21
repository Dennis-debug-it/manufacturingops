using System.Text.Json;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize(Policy = "PlatformSettingsAccess")]
[Route("api/platform/settings")]
public sealed class PlatformSettingsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IAuditLogService auditLogService,
    ISecretEncryptionService secretEncryptionService,
    IEmailTemplateService emailTemplateService,
    IEmailSubjectRuleService emailSubjectRuleService,
    IEmailOutboxService emailOutboxService,
    IEmailSender emailSender,
    ILogger<PlatformSettingsController> logger) : ControllerBase
{
    private const string PlatformFinanceCategory = "platform-finance";
    private const string PlatformNumberingCategory = "platform-numbering";
    private const string PlatformGeneralCategory = "platform-general";
    private const string PlatformTaxFinanceCategory = "platform-tax-finance";
    private const string PlatformSecurityCategory = "platform-security";
    private const string PlatformIntegrationsCategory = "platform-integrations";
    private const string PlatformSystemPreferencesCategory = "platform-system-preferences";
    private const string PlatformEmailFlagsCategory = "platform-email-flags";
    private const string PlatformNotificationPreferencesCategory = "platform-notification-preferences";
    private const string PlatformEmailSubjectRulesCategory = "platform-email-subject-rules";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly IReadOnlyDictionary<string, string> NumberingDefaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Quotation"] = "QTN",
        ["Invoice"] = "INV",
        ["Receipt"] = "REC",
        ["WorkOrder"] = "WO",
        ["PurchaseOrder"] = "PO",
        ["Expense"] = "EXP",
        ["CreditNote"] = "CN",
        ["TenantCode"] = "TEN",
        ["AssetCode"] = "AST"
    };

    private static readonly IReadOnlyList<string> NotificationPreferenceKeys =
    [
        "new-tenant-created",
        "tenant-deactivated",
        "subscription-expiring",
        "invoice-overdue",
        "payment-received",
        "work-order-overdue",
        "sla-breach",
        "failed-login-attempts",
        "system-errors"
    ];

    [HttpGet]
    public async Task<ActionResult<PlatformSettingsResponse>> Get(CancellationToken cancellationToken)
    {
        var response = await BuildResponseAsync(cancellationToken);
        return Ok(response);
    }

    [HttpPut]
    public async Task<ActionResult<PlatformSettingsResponse>> Update([FromBody] PlatformSettingsUpdateRequest request, CancellationToken cancellationToken)
    {
        var tenant = await LoadPlatformTenantAsync(cancellationToken);
        tenant.Name = string.IsNullOrWhiteSpace(request.PlatformName) ? tenant.Name : request.PlatformName.Trim();
        tenant.CompanyName = tenant.Name;
        tenant.ContactEmail = NormalizeOptional(request.SupportEmail) ?? tenant.ContactEmail;
        tenant.Email = NormalizeOptional(request.SupportEmail) ?? tenant.Email;
        tenant.LogoUrl = NormalizeOptional(request.PlatformLogoUrl);
        tenant.PrimaryColor = NormalizeColorOrDefault(request.PrimaryColor, tenant.PrimaryColor);
        tenant.SecondaryColor = NormalizeColorOrDefault(request.SecondaryColor, tenant.SecondaryColor);
        tenant.ShowPoweredByEcosys = request.ShowPoweredByEcosys;

        await UpsertCategoryAsync(PlatformFinanceCategory, new PlatformFinanceSettingsRecord(
            NormalizeCurrencyOrDefault(request.DefaultCurrency),
            new PlatformTaxSettingRecord(
                string.IsNullOrWhiteSpace(request.TaxName) ? "VAT" : request.TaxName.Trim(),
                request.DefaultTaxRate,
                request.TaxMode ?? "Exclusive")), cancellationToken);

        await UpsertCategoryAsync(PlatformNumberingCategory, new PlatformNumberingRecord(
            request.InvoiceNumberingPrefix,
            request.QuotationNumberingPrefix,
            request.ReceiptNumberingPrefix), cancellationToken);

        await UpsertRootNumberingAsync("Invoice", request.InvoiceNumberingPrefix, cancellationToken);
        await UpsertRootNumberingAsync("Quotation", request.QuotationNumberingPrefix, cancellationToken);
        await UpsertRootNumberingAsync("Receipt", request.ReceiptNumberingPrefix, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("platform.settings.updated", nameof(PlatformSetting), "platform", "Platform settings were updated.", cancellationToken);
        return Ok(await BuildResponseAsync(cancellationToken));
    }

    [HttpGet("general")]
    public async Task<ActionResult<PlatformGeneralSettingsResponse>> GetGeneral(CancellationToken cancellationToken)
    {
        var tenant = await LoadPlatformTenantAsync(cancellationToken);
        var finance = await LoadFinanceAsync(cancellationToken);
        var general = await LoadGeneralAsync(cancellationToken);

        return Ok(new PlatformGeneralSettingsResponse(
            tenant.Name,
            tenant.ContactEmail ?? tenant.Email,
            general.DefaultCountry,
            finance.DefaultCurrency,
            general.Timezone,
            general.CompanyLegalName,
            general.CompanyRegistrationNumber,
            general.CompanyPinTaxNumber,
            general.DefaultLanguage));
    }

    [HttpPut("general")]
    public async Task<ActionResult<PlatformGeneralSettingsResponse>> UpdateGeneral([FromBody] PlatformGeneralSettingsRequest request, CancellationToken cancellationToken)
    {
        var tenant = await LoadPlatformTenantAsync(cancellationToken);
        tenant.Name = string.IsNullOrWhiteSpace(request.PlatformName) ? tenant.Name : request.PlatformName.Trim();
        tenant.CompanyName = tenant.Name;
        tenant.ContactEmail = NormalizeOptional(request.SupportEmail) ?? tenant.ContactEmail;
        tenant.Email = NormalizeOptional(request.SupportEmail) ?? tenant.Email;

        var currentFinance = await LoadFinanceAsync(cancellationToken);
        await UpsertCategoryAsync(PlatformFinanceCategory, currentFinance with { DefaultCurrency = NormalizeCurrencyOrDefault(request.DefaultCurrency) }, cancellationToken);
        await UpsertCategoryAsync(PlatformGeneralCategory, new PlatformGeneralRecord(
            NormalizeOptional(request.DefaultCountry) ?? "Kenya",
            NormalizeOptional(request.Timezone) ?? "Africa/Nairobi",
            NormalizeOptional(request.CompanyLegalName) ?? tenant.Name,
            NormalizeOptional(request.CompanyRegistrationNumber),
            NormalizeOptional(request.CompanyPinTaxNumber),
            NormalizeOptional(request.DefaultLanguage) ?? "en"), cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("platform.settings.general.updated", nameof(Tenant), tenant.Id.ToString(), "Platform general settings were updated.", cancellationToken);
        return Ok((await GetGeneral(cancellationToken)).Value!);
    }

    [HttpGet("branding")]
    public async Task<ActionResult<PlatformBrandingResponse>> GetBranding(CancellationToken cancellationToken)
    {
        var tenant = await LoadPlatformTenantAsync(cancellationToken);
        var branding = await LoadBrandingExtendedAsync(cancellationToken);
        return Ok(new PlatformBrandingResponse(
            tenant.LogoUrl,
            branding.FaviconUrl,
            tenant.PrimaryColor,
            tenant.SecondaryColor,
            branding.AccentColor,
            tenant.ShowPoweredByEcosys,
            branding.LoginPageBrandingPreview,
            branding.DocumentBrandingPreview));
    }

    [HttpPut("branding")]
    public async Task<ActionResult<PlatformBrandingResponse>> UpdateBranding([FromBody] PlatformBrandingRequest request, CancellationToken cancellationToken)
    {
        var tenant = await LoadPlatformTenantAsync(cancellationToken);
        tenant.LogoUrl = NormalizeOptional(request.PlatformLogoUrl);
        tenant.PrimaryColor = NormalizeColorOrDefault(request.PrimaryColor, tenant.PrimaryColor);
        tenant.SecondaryColor = NormalizeColorOrDefault(request.SecondaryColor, tenant.SecondaryColor);
        tenant.ShowPoweredByEcosys = request.ShowPoweredByEcosys;

        await UpsertCategoryAsync("platform-branding-ext", new PlatformBrandingExtendedRecord(
            NormalizeOptional(request.FaviconUrl),
            NormalizeColorOrDefault(request.AccentColor, "#f59e0b"),
            request.LoginPageBrandingPreview,
            request.DocumentBrandingPreview), cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("platform.settings.branding.updated", nameof(Tenant), tenant.Id.ToString(), "Platform branding settings were updated.", cancellationToken);
        return Ok((await GetBranding(cancellationToken)).Value!);
    }

    [HttpGet("email")]
    public async Task<ActionResult<PlatformEmailSettingsResponse>> GetEmail(CancellationToken cancellationToken)
    {
        var setting = await GetOrCreatePlatformEmailSettingAsync(cancellationToken);
        var flags = await LoadEmailFlagsAsync(cancellationToken);
        var preferences = await LoadNotificationPreferencesAsync(cancellationToken);
        var subjectRules = await LoadEmailSubjectRulesAsync(cancellationToken);
        return Ok(MapEmail(setting, flags, preferences, subjectRules));
    }

    [HttpPut("email")]
    public async Task<ActionResult<PlatformEmailSettingsResponse>> UpdateEmail([FromBody] PlatformEmailSettingsRequest request, CancellationToken cancellationToken)
    {
        if (request.DeliveryMode == EmailDeliveryMode.Smtp && request.EnableEmailNotifications && string.IsNullOrWhiteSpace(request.SmtpHost))
        {
            throw new BusinessRuleException("SMTP host is required.");
        }

        if (request.DeliveryMode == EmailDeliveryMode.Smtp && request.EnableEmailNotifications && request.SmtpPort <= 0)
        {
            throw new BusinessRuleException("SMTP port must be greater than zero.");
        }

        var setting = await GetOrCreatePlatformEmailSettingAsync(cancellationToken);
        setting.Provider = request.DeliveryMode.ToString();
        setting.Host = string.IsNullOrWhiteSpace(request.SmtpHost) ? string.Empty : request.SmtpHost.Trim();
        setting.Port = request.SmtpPort > 0 ? request.SmtpPort : setting.Port;
        setting.Username = NormalizeOptional(request.SmtpUsername);
        setting.SenderName = string.IsNullOrWhiteSpace(request.SenderName) ? "Ecosys Platform" : request.SenderName.Trim();
        setting.SenderAddress = string.IsNullOrWhiteSpace(request.SenderEmail) ? "noreply@ecosys.local" : request.SenderEmail.Trim().ToLowerInvariant();
        setting.ReplyToEmail = NormalizeOptional(request.ReplyToEmail);
        setting.UseSsl = request.SecureMode is EmailSecureMode.StartTls or EmailSecureMode.SslOnConnect || request.EnableSslTls;
        setting.IsEnabled = request.EnableEmailNotifications && request.DeliveryMode != EmailDeliveryMode.Disabled;
        setting.UpdatedAt = DateTime.UtcNow;

        var encryptedSecret = secretEncryptionService.Encrypt(request.SmtpPasswordSecret ?? request.ApiKeySecret);
        if (!string.IsNullOrWhiteSpace(encryptedSecret))
        {
            setting.EncryptedSecret = encryptedSecret;
            setting.Password = null;
        }

        setting.IsConfigured = !string.IsNullOrWhiteSpace(setting.EncryptedSecret) || !string.IsNullOrWhiteSpace(setting.Password);

        await UpsertCategoryAsync(PlatformEmailFlagsCategory, new PlatformEmailFlagsRecord(
            request.EnableSystemAlerts,
            request.EnableInvoiceEmails,
            request.EnableQuotationEmails,
            request.EnablePaymentReceiptEmails,
            request.EnableWorkOrderNotificationEmails,
            request.EnableSlaEscalationEmails,
            request.EnableTenantOnboardingEmails), cancellationToken);

        var normalizedPrefs = (request.NotificationPreferences ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.NotificationKey))
            .Select(x => new PlatformNotificationPreferenceRecord(
                x.NotificationKey.Trim().ToLowerInvariant(),
                x.EmailEnabled,
                x.InAppEnabled,
                x.SmsEnabled,
                x.IsActive))
            .ToList();

        if (normalizedPrefs.Count == 0)
        {
            normalizedPrefs = BuildDefaultNotificationPreferences();
        }

        await UpsertCategoryAsync(PlatformNotificationPreferencesCategory, normalizedPrefs, cancellationToken);
        await UpsertCategoryAsync(PlatformEmailSubjectRulesCategory, new PlatformEmailSubjectRulesRecord(
            NormalizeSubjectPrefix(request.SubjectPrefix),
            NormalizeOptional(request.SubjectSuffix),
            request.IncludeEnvironmentInSubject,
            NormalizeOptional(request.EnvironmentLabel) ?? "Production",
            request.IncludeTenantNameInSubject,
            request.EnableEventSubjectTags), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("platform.settings.email.updated", nameof(EmailSetting), setting.Id.ToString(), "Platform email settings were updated.", cancellationToken);
        return Ok((await GetEmail(cancellationToken)).Value!);
    }

    [HttpPost("email/test")]
    public async Task<ActionResult<PlatformEmailActionResponse>> TestEmail([FromBody] PlatformEmailTestRequest request, CancellationToken cancellationToken)
    {
        var setting = await GetOrCreatePlatformEmailSettingAsync(cancellationToken);
        var recipient = string.IsNullOrWhiteSpace(request.TestRecipientEmail) ? setting.SenderAddress : request.TestRecipientEmail.Trim();
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
                    ["senderName"] = setting.SenderName,
                    ["sentAt"] = DateTime.UtcNow.ToString("u"),
                }),
            cancellationToken);

        var finalSubject = await emailSubjectRuleService.BuildFinalSubjectAsync(
            PlatformConstants.RootTenantId,
            "email.test",
            template.Subject,
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "Platform email test queued with mode {DeliveryMode}, host {Host}, port {Port}, secure mode {SecureMode}, sender {SenderEmail}.",
            ParseDeliveryMode(setting.Provider),
            setting.Host,
            setting.Port,
            EmailDeliveryModeResolver.ResolveSecureMode(setting.Port, setting.UseSsl),
            setting.SenderAddress);

        await emailOutboxService.QueueEmailAsync(
            new QueueEmailRequest(
                PlatformConstants.RootTenantId,
                "email.test",
                "test-email",
                recipient,
                recipient,
                template.SenderNameOverride ?? setting.SenderName,
                setting.SenderAddress,
                template.ReplyToOverride ?? setting.ReplyToEmail,
                finalSubject,
                template.HtmlBody,
                template.TextBody,
                tenantContext.GetRequiredUserId()),
            cancellationToken);

        setting.LastError = null;
        setting.LastTestedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new PlatformEmailActionResponse(true, setting.LastTestedAt, null, "Test email queued. Check Delivery Logs for status."));
    }

    [HttpPost("email/verify")]
    public async Task<ActionResult<PlatformEmailActionResponse>> VerifyEmailConnection(CancellationToken cancellationToken)
    {
        var setting = await GetOrCreatePlatformEmailSettingAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(setting.Host) && !string.Equals(setting.Provider, EmailDeliveryMode.Disabled.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("SMTP host is not configured.");
        }

        try
        {
            var delivery = CreateDeliveryOptions(setting);
            logger.LogInformation(
                "Platform email verification requested with mode {DeliveryMode}, host {Host}, port {Port}, secure mode {SecureMode}, sender {SenderEmail}.",
                delivery.DeliveryMode,
                delivery.SmtpHost,
                delivery.SmtpPort,
                delivery.SecureMode,
                delivery.SenderEmail);
            var verification = await emailSender.VerifyAsync(delivery, cancellationToken);
            setting.LastError = null;
            setting.LastTestedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            if (verification.Success)
            {
                return Ok(new PlatformEmailActionResponse(true, setting.LastTestedAt, null, null));
            }

            setting.LastError = verification.ErrorCategory ?? EmailErrorCategories.UnknownSmtpError;
            await dbContext.SaveChangesAsync(cancellationToken);
            return Ok(new PlatformEmailActionResponse(false, setting.LastTestedAt, setting.LastError, null));
        }
        catch (Exception ex)
        {
            setting.LastError = ex is EmailDeliveryException deliveryException
                ? deliveryException.ErrorCategory
                : EmailErrorCategories.UnknownSmtpError;
            setting.LastTestedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogWarning(ex, "Platform email verification failed.");
            return Ok(new PlatformEmailActionResponse(false, setting.LastTestedAt, setting.LastError, null));
        }
    }

    [HttpGet("numbering")]
    public async Task<ActionResult<PlatformLegacyNumberingResponse>> GetNumbering(CancellationToken cancellationToken)
    {
        var numbering = await LoadNumberingAsync(cancellationToken);
        return Ok(new PlatformLegacyNumberingResponse(numbering.InvoiceNumberingPrefix ?? "INV", numbering.QuotationNumberingPrefix ?? "QTN", numbering.ReceiptNumberingPrefix ?? "REC"));
    }

    [HttpPut("numbering")]
    public async Task<ActionResult<PlatformLegacyNumberingResponse>> UpdateNumbering([FromBody] PlatformLegacyNumberingRequest request, CancellationToken cancellationToken)
    {
        await UpsertCategoryAsync(PlatformNumberingCategory, new PlatformNumberingRecord(
            request.InvoiceNumberingPrefix,
            request.QuotationNumberingPrefix,
            request.ReceiptNumberingPrefix), cancellationToken);

        await UpsertRootNumberingAsync("Invoice", request.InvoiceNumberingPrefix, cancellationToken);
        await UpsertRootNumberingAsync("Quotation", request.QuotationNumberingPrefix, cancellationToken);
        await UpsertRootNumberingAsync("Receipt", request.ReceiptNumberingPrefix, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("platform.settings.numbering.updated", nameof(NumberingSetting), "platform-numbering", "Platform numbering settings were updated.", cancellationToken);

        return Ok(new PlatformLegacyNumberingResponse(
            request.InvoiceNumberingPrefix ?? "INV",
            request.QuotationNumberingPrefix ?? "QTN",
            request.ReceiptNumberingPrefix ?? "REC"));
    }

    [HttpGet("numbering/rules")]
    public async Task<ActionResult<IReadOnlyCollection<PlatformNumberingRuleResponse>>> GetNumberingRules(CancellationToken cancellationToken)
    {
        var rules = await EnsureAndLoadPlatformNumberingRulesAsync(cancellationToken);
        return Ok(rules.Select(MapNumberingRule).ToList());
    }

    [HttpPut("numbering/rules")]
    public async Task<ActionResult<IReadOnlyCollection<PlatformNumberingRuleResponse>>> UpdateNumberingRules([FromBody] IReadOnlyCollection<PlatformNumberingRuleRequest> request, CancellationToken cancellationToken)
    {
        if (request.Count == 0)
        {
            throw new BusinessRuleException("At least one numbering rule is required.");
        }

        foreach (var item in request)
        {
            if (string.IsNullOrWhiteSpace(item.DocumentType))
            {
                throw new BusinessRuleException("Document type is required for numbering rules.");
            }

            var normalizedDocumentType = NormalizeDocumentType(item.DocumentType);
            var prefix = NormalizePrefixOrDefault(item.Prefix, NumberingDefaults[normalizedDocumentType]);
            var existing = await dbContext.NumberingSettings.SingleOrDefaultAsync(
                x => x.TenantId == PlatformConstants.RootTenantId && x.BranchId == null && x.DocumentType == normalizedDocumentType,
                cancellationToken);

            if (existing is null)
            {
                existing = new NumberingSetting
                {
                    TenantId = PlatformConstants.RootTenantId,
                    BranchId = null,
                    DocumentType = normalizedDocumentType,
                    Prefix = prefix,
                    NextNumber = Math.Max(1, item.NextNumber),
                    PaddingLength = Math.Max(3, item.PaddingLength),
                    ResetFrequency = NormalizeResetFrequency(item.ResetFrequency),
                    IncludeYear = false,
                    IncludeMonth = false,
                    IsActive = true
                };
                dbContext.NumberingSettings.Add(existing);
            }
            else
            {
                existing.Prefix = prefix;
                existing.NextNumber = Math.Max(1, item.NextNumber);
                existing.PaddingLength = Math.Max(3, item.PaddingLength);
                existing.ResetFrequency = NormalizeResetFrequency(item.ResetFrequency);
                existing.IsActive = true;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("platform.settings.numbering.rules.updated", nameof(NumberingSetting), "platform-numbering-rules", "Platform numbering rules were updated.", cancellationToken);
        var rules = await EnsureAndLoadPlatformNumberingRulesAsync(cancellationToken);
        return Ok(rules.Select(MapNumberingRule).ToList());
    }

    [HttpGet("tax-finance")]
    public async Task<ActionResult<PlatformTaxFinanceSettingsResponse>> GetTaxFinance(CancellationToken cancellationToken)
    {
        var finance = await LoadFinanceAsync(cancellationToken);
        var extended = await LoadTaxFinanceExtendedAsync(cancellationToken);
        return Ok(new PlatformTaxFinanceSettingsResponse(
            finance.Tax.DefaultRate,
            extended.EnableVat,
            finance.Tax.Name,
            extended.DefaultPaymentTerms,
            extended.DefaultInvoiceDueDays,
            extended.DefaultQuotationValidityDays,
            extended.DefaultExpenseApprovalRequired,
            finance.DefaultCurrency,
            extended.InvoiceNotes,
            extended.QuotationTermsAndConditions));
    }

    [HttpPut("tax-finance")]
    public async Task<ActionResult<PlatformTaxFinanceSettingsResponse>> UpdateTaxFinance([FromBody] PlatformTaxFinanceSettingsRequest request, CancellationToken cancellationToken)
    {
        var finance = await LoadFinanceAsync(cancellationToken);
        await UpsertCategoryAsync(PlatformFinanceCategory, finance with
        {
            DefaultCurrency = NormalizeCurrencyOrDefault(request.DefaultCurrency),
            Tax = new PlatformTaxSettingRecord(
                string.IsNullOrWhiteSpace(request.TaxName) ? "VAT" : request.TaxName.Trim(),
                request.DefaultVatRate,
                finance.Tax.Mode)
        }, cancellationToken);

        await UpsertCategoryAsync(PlatformTaxFinanceCategory, new PlatformTaxFinanceExtendedRecord(
            request.EnableVat,
            NormalizeOptional(request.DefaultPaymentTerms) ?? "30 days",
            Math.Max(1, request.DefaultInvoiceDueDays),
            Math.Max(1, request.DefaultQuotationValidityDays),
            request.DefaultExpenseApprovalRequired,
            NormalizeOptional(request.InvoiceNotes),
            NormalizeOptional(request.QuotationTermsAndConditions)), cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("platform.settings.tax_finance.updated", nameof(PlatformSetting), "platform-tax-finance", "Platform tax and finance settings were updated.", cancellationToken);
        return Ok((await GetTaxFinance(cancellationToken)).Value!);
    }

    [HttpGet("security")]
    public async Task<ActionResult<PlatformSecuritySettingsResponse>> GetSecurity(CancellationToken cancellationToken)
    {
        var security = await LoadSecurityAsync(cancellationToken);
        return Ok(new PlatformSecuritySettingsResponse(
            security.RequireStrongPasswords,
            security.MinimumPasswordLength,
            security.RequireEmailVerification,
            security.SessionTimeoutMinutes,
            security.FailedLoginLockoutThreshold,
            security.TwoFactorAuthImplemented,
            security.PasswordResetExpiryMinutes));
    }

    [HttpPut("security")]
    public async Task<ActionResult<PlatformSecuritySettingsResponse>> UpdateSecurity([FromBody] PlatformSecuritySettingsRequest request, CancellationToken cancellationToken)
    {
        var next = new PlatformSecurityRecord(
            request.RequireStrongPasswords,
            Math.Max(8, request.MinimumPasswordLength),
            request.RequireEmailVerification,
            Math.Max(5, request.SessionTimeoutMinutes),
            Math.Max(3, request.FailedLoginLockoutThreshold),
            false,
            Math.Max(5, request.PasswordResetExpiryMinutes));
        await UpsertCategoryAsync(PlatformSecurityCategory, next, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("platform.settings.security.updated", nameof(PlatformSetting), "platform-security", "Platform security settings were updated.", cancellationToken);
        return Ok((await GetSecurity(cancellationToken)).Value!);
    }

    [HttpGet("integrations")]
    public async Task<ActionResult<PlatformIntegrationsSettingsResponse>> GetIntegrations(CancellationToken cancellationToken)
    {
        var settings = await LoadIntegrationsAsync(cancellationToken);
        return Ok(new PlatformIntegrationsSettingsResponse(
            settings.MpesaDarajaEnabled,
            settings.MpesaConsumerKeyMasked,
            settings.MpesaConsumerSecretMasked,
            settings.WebhooksEnabled,
            settings.ApiKeysEnabled,
            settings.FutureMonitoringIntegrationsNotes,
            settings.EmailSmtpEnabled));
    }

    [HttpPut("integrations")]
    public async Task<ActionResult<PlatformIntegrationsSettingsResponse>> UpdateIntegrations([FromBody] PlatformIntegrationsSettingsRequest request, CancellationToken cancellationToken)
    {
        var current = await LoadIntegrationsAsync(cancellationToken);
        var next = new PlatformIntegrationsRecord(
            request.MpesaDarajaEnabled,
            MaskSecret(request.MpesaConsumerKey, current.MpesaConsumerKeyMasked),
            MaskSecret(request.MpesaConsumerSecret, current.MpesaConsumerSecretMasked),
            request.WebhooksEnabled,
            request.ApiKeysEnabled,
            NormalizeOptional(request.FutureMonitoringIntegrationsNotes),
            request.EmailSmtpEnabled);
        await UpsertCategoryAsync(PlatformIntegrationsCategory, next, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("platform.settings.integrations.updated", nameof(PlatformSetting), "platform-integrations", "Platform integrations settings were updated.", cancellationToken);
        return Ok((await GetIntegrations(cancellationToken)).Value!);
    }

    [HttpGet("system-preferences")]
    public async Task<ActionResult<PlatformSystemPreferencesResponse>> GetSystemPreferences(CancellationToken cancellationToken)
    {
        var settings = await LoadSystemPreferencesAsync(cancellationToken);
        return Ok(new PlatformSystemPreferencesResponse(
            settings.DateFormat,
            settings.TimeFormat,
            settings.DefaultPaginationSize,
            settings.EnableDarkModeDefault,
            settings.MaintenanceMode,
            settings.ShowBetaModules,
            settings.AllowTenantSelfRegistration));
    }

    [HttpPut("system-preferences")]
    public async Task<ActionResult<PlatformSystemPreferencesResponse>> UpdateSystemPreferences([FromBody] PlatformSystemPreferencesRequest request, CancellationToken cancellationToken)
    {
        var next = new PlatformSystemPreferencesRecord(
            NormalizeOptional(request.DateFormat) ?? "yyyy-MM-dd",
            NormalizeOptional(request.TimeFormat) ?? "HH:mm",
            Math.Clamp(request.DefaultPaginationSize, 10, 200),
            request.EnableDarkModeDefault,
            request.MaintenanceMode,
            request.ShowBetaModules,
            request.AllowTenantSelfRegistration);
        await UpsertCategoryAsync(PlatformSystemPreferencesCategory, next, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("platform.settings.system_preferences.updated", nameof(PlatformSetting), "platform-system-preferences", "Platform system preferences were updated.", cancellationToken);
        return Ok((await GetSystemPreferences(cancellationToken)).Value!);
    }

    private async Task<PlatformSettingsResponse> BuildResponseAsync(CancellationToken cancellationToken)
    {
        var tenant = await LoadPlatformTenantAsync(cancellationToken);
        var finance = await LoadFinanceAsync(cancellationToken);
        var numbering = await LoadNumberingAsync(cancellationToken);

        return new PlatformSettingsResponse(
            tenant.Name,
            tenant.ContactEmail ?? tenant.Email,
            finance.DefaultCurrency,
            finance.Tax.Name,
            finance.Tax.DefaultRate,
            finance.Tax.Mode,
            numbering.InvoiceNumberingPrefix,
            numbering.QuotationNumberingPrefix,
            numbering.ReceiptNumberingPrefix,
            tenant.LogoUrl,
            tenant.PrimaryColor,
            tenant.SecondaryColor,
            tenant.ShowPoweredByEcosys);
    }

    private async Task<Tenant> LoadPlatformTenantAsync(CancellationToken cancellationToken) =>
        await dbContext.Tenants.SingleAsync(x => x.Id == PlatformConstants.RootTenantId, cancellationToken);

    private async Task<PlatformFinanceSettingsRecord> LoadFinanceAsync(CancellationToken cancellationToken)
    {
        var setting = await dbContext.PlatformSettings.SingleOrDefaultAsync(x => x.Category == PlatformFinanceCategory, cancellationToken);
        if (setting is null)
        {
            return new PlatformFinanceSettingsRecord("KES", new PlatformTaxSettingRecord("VAT", 16m, "Exclusive"));
        }

        var value = JsonSerializer.Deserialize<PlatformFinanceSettingsRecord>(setting.JsonValue, JsonOptions);
        return value ?? new PlatformFinanceSettingsRecord("KES", new PlatformTaxSettingRecord("VAT", 16m, "Exclusive"));
    }

    private async Task<PlatformNumberingRecord> LoadNumberingAsync(CancellationToken cancellationToken)
    {
        var setting = await dbContext.PlatformSettings.SingleOrDefaultAsync(x => x.Category == PlatformNumberingCategory, cancellationToken);
        if (setting is null)
        {
            return new PlatformNumberingRecord("INV", "QTN", "REC");
        }

        var value = JsonSerializer.Deserialize<PlatformNumberingRecord>(setting.JsonValue, JsonOptions);
        return value ?? new PlatformNumberingRecord("INV", "QTN", "REC");
    }

    private async Task<PlatformGeneralRecord> LoadGeneralAsync(CancellationToken cancellationToken)
    {
        var setting = await dbContext.PlatformSettings.SingleOrDefaultAsync(x => x.Category == PlatformGeneralCategory, cancellationToken);
        if (setting is null)
        {
            return new PlatformGeneralRecord("Kenya", "Africa/Nairobi", "Ecosys ServiceOps Platform", null, null, "en");
        }

        return JsonSerializer.Deserialize<PlatformGeneralRecord>(setting.JsonValue, JsonOptions)
            ?? new PlatformGeneralRecord("Kenya", "Africa/Nairobi", "Ecosys ServiceOps Platform", null, null, "en");
    }

    private async Task<PlatformBrandingExtendedRecord> LoadBrandingExtendedAsync(CancellationToken cancellationToken)
    {
        var setting = await dbContext.PlatformSettings.SingleOrDefaultAsync(x => x.Category == "platform-branding-ext", cancellationToken);
        if (setting is null)
        {
            return new PlatformBrandingExtendedRecord(null, "#f59e0b", true, true);
        }

        return JsonSerializer.Deserialize<PlatformBrandingExtendedRecord>(setting.JsonValue, JsonOptions)
            ?? new PlatformBrandingExtendedRecord(null, "#f59e0b", true, true);
    }

    private async Task<PlatformTaxFinanceExtendedRecord> LoadTaxFinanceExtendedAsync(CancellationToken cancellationToken)
    {
        var setting = await dbContext.PlatformSettings.SingleOrDefaultAsync(x => x.Category == PlatformTaxFinanceCategory, cancellationToken);
        if (setting is null)
        {
            return new PlatformTaxFinanceExtendedRecord(true, "30 days", 30, 14, true, null, null);
        }

        return JsonSerializer.Deserialize<PlatformTaxFinanceExtendedRecord>(setting.JsonValue, JsonOptions)
            ?? new PlatformTaxFinanceExtendedRecord(true, "30 days", 30, 14, true, null, null);
    }

    private async Task<PlatformSecurityRecord> LoadSecurityAsync(CancellationToken cancellationToken)
    {
        var setting = await dbContext.PlatformSettings.SingleOrDefaultAsync(x => x.Category == PlatformSecurityCategory, cancellationToken);
        if (setting is null)
        {
            return new PlatformSecurityRecord(true, 10, true, 60, 5, false, 30);
        }

        return JsonSerializer.Deserialize<PlatformSecurityRecord>(setting.JsonValue, JsonOptions)
            ?? new PlatformSecurityRecord(true, 10, true, 60, 5, false, 30);
    }

    private async Task<PlatformIntegrationsRecord> LoadIntegrationsAsync(CancellationToken cancellationToken)
    {
        var setting = await dbContext.PlatformSettings.SingleOrDefaultAsync(x => x.Category == PlatformIntegrationsCategory, cancellationToken);
        if (setting is null)
        {
            return new PlatformIntegrationsRecord(false, null, null, true, true, "Future monitoring integrations can be configured here.", true);
        }

        return JsonSerializer.Deserialize<PlatformIntegrationsRecord>(setting.JsonValue, JsonOptions)
            ?? new PlatformIntegrationsRecord(false, null, null, true, true, "Future monitoring integrations can be configured here.", true);
    }

    private async Task<PlatformSystemPreferencesRecord> LoadSystemPreferencesAsync(CancellationToken cancellationToken)
    {
        var setting = await dbContext.PlatformSettings.SingleOrDefaultAsync(x => x.Category == PlatformSystemPreferencesCategory, cancellationToken);
        if (setting is null)
        {
            return new PlatformSystemPreferencesRecord("yyyy-MM-dd", "HH:mm", 25, false, false, false, false);
        }

        return JsonSerializer.Deserialize<PlatformSystemPreferencesRecord>(setting.JsonValue, JsonOptions)
            ?? new PlatformSystemPreferencesRecord("yyyy-MM-dd", "HH:mm", 25, false, false, false, false);
    }

    private async Task<PlatformEmailFlagsRecord> LoadEmailFlagsAsync(CancellationToken cancellationToken)
    {
        var setting = await dbContext.PlatformSettings.SingleOrDefaultAsync(x => x.Category == PlatformEmailFlagsCategory, cancellationToken);
        if (setting is null)
        {
            return new PlatformEmailFlagsRecord(true, true, true, true, true, true, true);
        }

        return JsonSerializer.Deserialize<PlatformEmailFlagsRecord>(setting.JsonValue, JsonOptions)
            ?? new PlatformEmailFlagsRecord(true, true, true, true, true, true, true);
    }

    private async Task<List<PlatformNotificationPreferenceRecord>> LoadNotificationPreferencesAsync(CancellationToken cancellationToken)
    {
        var setting = await dbContext.PlatformSettings.SingleOrDefaultAsync(x => x.Category == PlatformNotificationPreferencesCategory, cancellationToken);
        if (setting is null)
        {
            return BuildDefaultNotificationPreferences();
        }

        return JsonSerializer.Deserialize<List<PlatformNotificationPreferenceRecord>>(setting.JsonValue, JsonOptions)
            ?? BuildDefaultNotificationPreferences();
    }

    private static List<PlatformNotificationPreferenceRecord> BuildDefaultNotificationPreferences() =>
        NotificationPreferenceKeys
            .Select(key => new PlatformNotificationPreferenceRecord(key, true, key is "failed-login-attempts" or "system-errors", false, true))
            .ToList();

    private async Task<PlatformEmailSubjectRulesRecord> LoadEmailSubjectRulesAsync(CancellationToken cancellationToken)
    {
        var setting = await dbContext.PlatformSettings.SingleOrDefaultAsync(x => x.Category == PlatformEmailSubjectRulesCategory, cancellationToken);
        if (setting is null)
        {
            return new PlatformEmailSubjectRulesRecord("[Ecosys]", null, false, "Production", false, true);
        }

        return JsonSerializer.Deserialize<PlatformEmailSubjectRulesRecord>(setting.JsonValue, JsonOptions)
            ?? new PlatformEmailSubjectRulesRecord("[Ecosys]", null, false, "Production", false, true);
    }

    private async Task<EmailSetting> GetOrCreatePlatformEmailSettingAsync(CancellationToken cancellationToken)
    {
        var setting = await dbContext.EmailSettings.SingleOrDefaultAsync(x => x.TenantId == PlatformConstants.RootTenantId, cancellationToken);
        if (setting is not null)
        {
            return setting;
        }

        var tenant = await LoadPlatformTenantAsync(cancellationToken);
        setting = new EmailSetting
        {
            TenantId = PlatformConstants.RootTenantId,
            UsePlatformDefaults = false,
            OverrideSmtpSettings = true,
            IsEnabled = true,
            Provider = EmailDeliveryMode.Smtp.ToString(),
            Host = "localhost",
            Port = 587,
            UseSsl = true,
            SenderName = tenant.Name,
            SenderAddress = tenant.ContactEmail ?? tenant.Email,
            IsConfigured = false
        };
        dbContext.EmailSettings.Add(setting);
        await dbContext.SaveChangesAsync(cancellationToken);
        return setting;
    }

    private PlatformEmailSettingsResponse MapEmail(
        EmailSetting setting,
        PlatformEmailFlagsRecord flags,
        IReadOnlyCollection<PlatformNotificationPreferenceRecord> preferences,
        PlatformEmailSubjectRulesRecord subjectRules) =>
        new(
            ParseDeliveryMode(setting.Provider),
            setting.Host,
            setting.Port,
            setting.Username,
            BuildSavedSecretHint(setting.EncryptedSecret ?? setting.Password, "Password saved. Leave blank to keep existing password."),
            setting.SenderName,
            setting.SenderAddress,
            setting.ReplyToEmail,
            setting.UseSsl,
            EmailDeliveryModeResolver.ResolveSecureMode(setting.Port, setting.UseSsl),
            null,
            BuildSavedSecretHint(null, "Secret saved. Leave blank to keep existing secret."),
            null,
            30,
            0,
            setting.IsEnabled,
            flags.EnableSystemAlerts,
            flags.EnableInvoiceEmails,
            flags.EnableQuotationEmails,
            flags.EnablePaymentReceiptEmails,
            flags.EnableWorkOrderNotificationEmails,
            flags.EnableSlaEscalationEmails,
            flags.EnableTenantOnboardingEmails,
            subjectRules.SubjectPrefix,
            subjectRules.SubjectSuffix,
            subjectRules.IncludeEnvironmentInSubject,
            subjectRules.EnvironmentLabel,
            subjectRules.IncludeTenantNameInSubject,
            subjectRules.EnableEventSubjectTags,
            preferences.Select(x => new PlatformNotificationPreferenceResponse(
                x.NotificationKey,
                x.EmailEnabled,
                x.InAppEnabled,
                x.SmsEnabled,
                x.IsActive)).ToList(),
            setting.LastTestedAt,
            setting.LastError);

    private EmailDeliverySettings CreateDeliveryOptions(EmailSetting settings)
    {
        var secret = secretEncryptionService.Decrypt(settings.EncryptedSecret) ?? settings.Password;
        return new EmailDeliverySettings(
            ParseDeliveryMode(settings.Provider),
            settings.Host,
            settings.Port,
            settings.Username,
            secret,
            settings.SenderName,
            settings.SenderAddress,
            settings.ReplyToEmail,
            settings.UseSsl,
            EmailDeliveryModeResolver.ResolveSecureMode(settings.Port, settings.UseSsl),
            null,
            null,
            null,
            30,
            0);
    }

    private async Task<List<NumberingSetting>> EnsureAndLoadPlatformNumberingRulesAsync(CancellationToken cancellationToken)
    {
        var settings = await dbContext.NumberingSettings
            .Where(x => x.TenantId == PlatformConstants.RootTenantId && x.BranchId == null)
            .ToListAsync(cancellationToken);

        foreach (var pair in NumberingDefaults)
        {
            if (settings.Any(x => string.Equals(x.DocumentType, pair.Key, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var row = new NumberingSetting
            {
                TenantId = PlatformConstants.RootTenantId,
                BranchId = null,
                DocumentType = pair.Key,
                Prefix = pair.Value,
                NextNumber = 1,
                PaddingLength = 6,
                ResetFrequency = "Never",
                IncludeYear = false,
                IncludeMonth = false,
                IsActive = true
            };
            dbContext.NumberingSettings.Add(row);
            settings.Add(row);
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return settings.OrderBy(x => x.DocumentType).ToList();
    }

    private static PlatformNumberingRuleResponse MapNumberingRule(NumberingSetting setting) =>
        new(
            setting.Id,
            setting.DocumentType,
            setting.Prefix,
            setting.NextNumber,
            setting.PaddingLength,
            setting.ResetFrequency,
            BuildPreview(setting));

    private static string BuildPreview(NumberingSetting setting)
    {
        var prefix = string.IsNullOrWhiteSpace(setting.Prefix) ? "DOC" : setting.Prefix.Trim().ToUpperInvariant();
        return $"{prefix}-{Math.Max(1, setting.NextNumber).ToString().PadLeft(Math.Max(3, setting.PaddingLength), '0')}";
    }

    private async Task UpsertCategoryAsync<T>(string category, T payload, CancellationToken cancellationToken)
    {
        var setting = await dbContext.PlatformSettings.SingleOrDefaultAsync(x => x.Category == category, cancellationToken);
        if (setting is null)
        {
            dbContext.PlatformSettings.Add(new PlatformSetting
            {
                Category = category,
                JsonValue = JsonSerializer.Serialize(payload, JsonOptions)
            });
            return;
        }

        setting.JsonValue = JsonSerializer.Serialize(payload, JsonOptions);
    }

    private async Task UpsertRootNumberingAsync(string documentType, string? prefix, CancellationToken cancellationToken)
    {
        var fallbackPrefix = NumberingDefaults.TryGetValue(documentType, out var defaultPrefix) ? defaultPrefix : documentType.ToUpperInvariant()[0..Math.Min(3, documentType.Length)];
        var normalizedPrefix = NormalizePrefixOrDefault(prefix, fallbackPrefix);

        var setting = await dbContext.NumberingSettings
            .SingleOrDefaultAsync(x => x.TenantId == PlatformConstants.RootTenantId && x.BranchId == null && x.DocumentType == documentType, cancellationToken);

        if (setting is null)
        {
            dbContext.NumberingSettings.Add(new NumberingSetting
            {
                TenantId = PlatformConstants.RootTenantId,
                BranchId = null,
                DocumentType = documentType,
                Prefix = normalizedPrefix,
                NextNumber = 1,
                PaddingLength = 6,
                ResetFrequency = "Never",
                IncludeYear = false,
                IncludeMonth = false,
                IsActive = true
            });
            return;
        }

        setting.Prefix = normalizedPrefix;
    }

    private async Task AuditAsync(string action, string entityType, string entityId, string description, CancellationToken cancellationToken)
    {
        await auditLogService.LogAsync(
            PlatformConstants.RootTenantId,
            tenantContext.GetRequiredUserId(),
            action,
            entityType,
            entityId,
            description,
            cancellationToken: cancellationToken);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static EmailDeliveryMode ParseDeliveryMode(string? value)
    {
        if (Enum.TryParse<EmailDeliveryMode>(value, true, out var parsed))
        {
            return parsed;
        }

        return EmailDeliveryMode.Smtp;
    }

    private static string NormalizeCurrencyOrDefault(string? value)
    {
        var normalized = NormalizeOptional(value);
        return string.IsNullOrWhiteSpace(normalized) ? "KES" : normalized.ToUpperInvariant();
    }

    private static string NormalizeColorOrDefault(string? value, string fallback)
    {
        var normalized = NormalizeOptional(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return fallback;
        }

        if (!normalized.StartsWith('#'))
        {
            throw new BusinessRuleException("Color values must be in hex format.");
        }

        return normalized;
    }

    private static string NormalizePrefixOrDefault(string? value, string fallback)
    {
        var normalized = NormalizeOptional(value);
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized.ToUpperInvariant();
    }

    private static string NormalizeSubjectPrefix(string? value)
    {
        var normalized = NormalizeOptional(value);
        return string.IsNullOrWhiteSpace(normalized) ? "[Ecosys]" : normalized;
    }

    private static string NormalizeDocumentType(string value)
    {
        var normalized = value.Trim();
        var match = NumberingDefaults.Keys.FirstOrDefault(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            throw new BusinessRuleException($"Unsupported numbering document type: {value}.");
        }

        return match;
    }

    private static string NormalizeResetFrequency(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "Never" : value.Trim();
        return normalized switch
        {
            "Never" => "Never",
            "Monthly" => "Monthly",
            "Yearly" => "Yearly",
            _ => "Never"
        };
    }

    private static string? MaskSecret(string? rawSecret, string? existingMasked)
    {
        if (!string.IsNullOrWhiteSpace(rawSecret))
        {
            var trimmed = rawSecret.Trim();
            var visible = trimmed.Length <= 3 ? trimmed : trimmed[^3..];
            return $"{new string('*', Math.Max(5, trimmed.Length - 3))}{visible}";
        }

        return existingMasked;
    }

    private static string? BuildSavedSecretHint(string? rawSecret, string hint) =>
        string.IsNullOrWhiteSpace(rawSecret) ? null : hint;

    private sealed record PlatformFinanceSettingsRecord(string DefaultCurrency, PlatformTaxSettingRecord Tax);
    private sealed record PlatformTaxSettingRecord(string Name, decimal DefaultRate, string Mode);
    private sealed record PlatformNumberingRecord(string? InvoiceNumberingPrefix, string? QuotationNumberingPrefix, string? ReceiptNumberingPrefix);
    private sealed record PlatformGeneralRecord(
        string DefaultCountry,
        string Timezone,
        string CompanyLegalName,
        string? CompanyRegistrationNumber,
        string? CompanyPinTaxNumber,
        string DefaultLanguage);
    private sealed record PlatformBrandingExtendedRecord(
        string? FaviconUrl,
        string AccentColor,
        bool LoginPageBrandingPreview,
        bool DocumentBrandingPreview);
    private sealed record PlatformTaxFinanceExtendedRecord(
        bool EnableVat,
        string DefaultPaymentTerms,
        int DefaultInvoiceDueDays,
        int DefaultQuotationValidityDays,
        bool DefaultExpenseApprovalRequired,
        string? InvoiceNotes,
        string? QuotationTermsAndConditions);
    private sealed record PlatformSecurityRecord(
        bool RequireStrongPasswords,
        int MinimumPasswordLength,
        bool RequireEmailVerification,
        int SessionTimeoutMinutes,
        int FailedLoginLockoutThreshold,
        bool TwoFactorAuthImplemented,
        int PasswordResetExpiryMinutes);
    private sealed record PlatformIntegrationsRecord(
        bool MpesaDarajaEnabled,
        string? MpesaConsumerKeyMasked,
        string? MpesaConsumerSecretMasked,
        bool WebhooksEnabled,
        bool ApiKeysEnabled,
        string? FutureMonitoringIntegrationsNotes,
        bool EmailSmtpEnabled);
    private sealed record PlatformSystemPreferencesRecord(
        string DateFormat,
        string TimeFormat,
        int DefaultPaginationSize,
        bool EnableDarkModeDefault,
        bool MaintenanceMode,
        bool ShowBetaModules,
        bool AllowTenantSelfRegistration);
    private sealed record PlatformEmailFlagsRecord(
        bool EnableSystemAlerts,
        bool EnableInvoiceEmails,
        bool EnableQuotationEmails,
        bool EnablePaymentReceiptEmails,
        bool EnableWorkOrderNotificationEmails,
        bool EnableSlaEscalationEmails,
        bool EnableTenantOnboardingEmails);
    private sealed record PlatformEmailSubjectRulesRecord(
        string SubjectPrefix,
        string? SubjectSuffix,
        bool IncludeEnvironmentInSubject,
        string EnvironmentLabel,
        bool IncludeTenantNameInSubject,
        bool EnableEventSubjectTags);
    private sealed record PlatformNotificationPreferenceRecord(
        string NotificationKey,
        bool EmailEnabled,
        bool InAppEnabled,
        bool SmsEnabled,
        bool IsActive);
}

public sealed record PlatformSettingsResponse(
    string PlatformName,
    string? SupportEmail,
    string DefaultCurrency,
    string TaxName,
    decimal DefaultTaxRate,
    string TaxMode,
    string? InvoiceNumberingPrefix,
    string? QuotationNumberingPrefix,
    string? ReceiptNumberingPrefix,
    string? PlatformLogoUrl,
    string PrimaryColor,
    string SecondaryColor,
    bool ShowPoweredByEcosys);

public sealed record PlatformSettingsUpdateRequest(
    string PlatformName,
    string? SupportEmail,
    string? DefaultCurrency,
    string TaxName,
    decimal DefaultTaxRate,
    string? TaxMode,
    string? InvoiceNumberingPrefix,
    string? QuotationNumberingPrefix,
    string? ReceiptNumberingPrefix,
    string? PlatformLogoUrl,
    string? PrimaryColor,
    string? SecondaryColor,
    bool ShowPoweredByEcosys);

public sealed record PlatformGeneralSettingsResponse(
    string PlatformName,
    string? SupportEmail,
    string DefaultCountry,
    string DefaultCurrency,
    string Timezone,
    string CompanyLegalName,
    string? CompanyRegistrationNumber,
    string? CompanyPinTaxNumber,
    string DefaultLanguage);

public sealed record PlatformGeneralSettingsRequest(
    string PlatformName,
    string? SupportEmail,
    string? DefaultCountry,
    string? DefaultCurrency,
    string? Timezone,
    string? CompanyLegalName,
    string? CompanyRegistrationNumber,
    string? CompanyPinTaxNumber,
    string? DefaultLanguage);

public sealed record PlatformBrandingResponse(
    string? PlatformLogoUrl,
    string? FaviconUrl,
    string PrimaryColor,
    string SecondaryColor,
    string AccentColor,
    bool ShowPoweredByEcosys,
    bool LoginPageBrandingPreview,
    bool DocumentBrandingPreview);

public sealed record PlatformBrandingRequest(
    string? PlatformLogoUrl,
    string? FaviconUrl,
    string? PrimaryColor,
    string? SecondaryColor,
    string? AccentColor,
    bool ShowPoweredByEcosys,
    bool LoginPageBrandingPreview,
    bool DocumentBrandingPreview);

public sealed record PlatformLegacyNumberingResponse(
    string InvoiceNumberingPrefix,
    string QuotationNumberingPrefix,
    string ReceiptNumberingPrefix);

public sealed record PlatformLegacyNumberingRequest(
    string? InvoiceNumberingPrefix,
    string? QuotationNumberingPrefix,
    string? ReceiptNumberingPrefix);

public sealed record PlatformNumberingRuleResponse(
    Guid Id,
    string DocumentType,
    string Prefix,
    long NextNumber,
    int PaddingLength,
    string ResetFrequency,
    string Preview);

public sealed record PlatformNumberingRuleRequest(
    string DocumentType,
    string? Prefix,
    long NextNumber,
    int PaddingLength,
    string? ResetFrequency);

public sealed record PlatformEmailSettingsResponse(
    EmailDeliveryMode DeliveryMode,
    string SmtpHost,
    int SmtpPort,
    string? SmtpUsername,
    string? SmtpPasswordMasked,
    string SenderName,
    string SenderEmail,
    string? ReplyToEmail,
    bool EnableSslTls,
    EmailSecureMode SecureMode,
    string? ApiEndpoint,
    string? ApiKeyMasked,
    string? ApiProviderName,
    int TimeoutSeconds,
    int MaxRetries,
    bool EnableEmailNotifications,
    bool EnableSystemAlerts,
    bool EnableInvoiceEmails,
    bool EnableQuotationEmails,
    bool EnablePaymentReceiptEmails,
    bool EnableWorkOrderNotificationEmails,
    bool EnableSlaEscalationEmails,
    bool EnableTenantOnboardingEmails,
    string SubjectPrefix,
    string? SubjectSuffix,
    bool IncludeEnvironmentInSubject,
    string EnvironmentLabel,
    bool IncludeTenantNameInSubject,
    bool EnableEventSubjectTags,
    IReadOnlyCollection<PlatformNotificationPreferenceResponse> NotificationPreferences,
    DateTime? LastTestedAt,
    string? LastError);

public sealed record PlatformEmailSettingsRequest(
    EmailDeliveryMode DeliveryMode,
    string SmtpHost,
    int SmtpPort,
    string? SmtpUsername,
    string? SmtpPasswordSecret,
    string SenderName,
    string SenderEmail,
    string? ReplyToEmail,
    bool EnableSslTls,
    EmailSecureMode SecureMode,
    string? ApiEndpoint,
    string? ApiKeySecret,
    string? ApiProviderName,
    int TimeoutSeconds,
    int MaxRetries,
    bool EnableEmailNotifications,
    bool EnableSystemAlerts,
    bool EnableInvoiceEmails,
    bool EnableQuotationEmails,
    bool EnablePaymentReceiptEmails,
    bool EnableWorkOrderNotificationEmails,
    bool EnableSlaEscalationEmails,
    bool EnableTenantOnboardingEmails,
    string SubjectPrefix,
    string? SubjectSuffix,
    bool IncludeEnvironmentInSubject,
    string EnvironmentLabel,
    bool IncludeTenantNameInSubject,
    bool EnableEventSubjectTags,
    IReadOnlyCollection<PlatformNotificationPreferenceRequest>? NotificationPreferences);

public sealed record PlatformNotificationPreferenceResponse(
    string NotificationKey,
    bool EmailEnabled,
    bool InAppEnabled,
    bool SmsEnabled,
    bool IsActive);

public sealed record PlatformNotificationPreferenceRequest(
    string NotificationKey,
    bool EmailEnabled,
    bool InAppEnabled,
    bool SmsEnabled,
    bool IsActive);

public sealed record PlatformEmailTestRequest(string? TestRecipientEmail);

public sealed record PlatformEmailActionResponse(
    bool Success,
    DateTime? LastTestedAt,
    string? LastError,
    string? Message);

public sealed record PlatformTaxFinanceSettingsResponse(
    decimal DefaultVatRate,
    bool EnableVat,
    string TaxName,
    string DefaultPaymentTerms,
    int DefaultInvoiceDueDays,
    int DefaultQuotationValidityDays,
    bool DefaultExpenseApprovalRequired,
    string DefaultCurrency,
    string? InvoiceNotes,
    string? QuotationTermsAndConditions);

public sealed record PlatformTaxFinanceSettingsRequest(
    decimal DefaultVatRate,
    bool EnableVat,
    string TaxName,
    string DefaultPaymentTerms,
    int DefaultInvoiceDueDays,
    int DefaultQuotationValidityDays,
    bool DefaultExpenseApprovalRequired,
    string DefaultCurrency,
    string? InvoiceNotes,
    string? QuotationTermsAndConditions);

public sealed record PlatformSecuritySettingsResponse(
    bool RequireStrongPasswords,
    int MinimumPasswordLength,
    bool RequireEmailVerification,
    int SessionTimeoutMinutes,
    int FailedLoginLockoutThreshold,
    bool TwoFactorAuthImplemented,
    int PasswordResetExpiryMinutes);

public sealed record PlatformSecuritySettingsRequest(
    bool RequireStrongPasswords,
    int MinimumPasswordLength,
    bool RequireEmailVerification,
    int SessionTimeoutMinutes,
    int FailedLoginLockoutThreshold,
    int PasswordResetExpiryMinutes);

public sealed record PlatformIntegrationsSettingsResponse(
    bool MpesaDarajaEnabled,
    string? MpesaConsumerKeyMasked,
    string? MpesaConsumerSecretMasked,
    bool WebhooksEnabled,
    bool ApiKeysEnabled,
    string? FutureMonitoringIntegrationsNotes,
    bool EmailSmtpEnabled);

public sealed record PlatformIntegrationsSettingsRequest(
    bool MpesaDarajaEnabled,
    string? MpesaConsumerKey,
    string? MpesaConsumerSecret,
    bool WebhooksEnabled,
    bool ApiKeysEnabled,
    string? FutureMonitoringIntegrationsNotes,
    bool EmailSmtpEnabled);

public sealed record PlatformSystemPreferencesResponse(
    string DateFormat,
    string TimeFormat,
    int DefaultPaginationSize,
    bool EnableDarkModeDefault,
    bool MaintenanceMode,
    bool ShowBetaModules,
    bool AllowTenantSelfRegistration);

public sealed record PlatformSystemPreferencesRequest(
    string DateFormat,
    string TimeFormat,
    int DefaultPaginationSize,
    bool EnableDarkModeDefault,
    bool MaintenanceMode,
    bool ShowBetaModules,
    bool AllowTenantSelfRegistration);
