using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/settings")]
public sealed class SettingsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IBranchAccessService branchAccessService,
    IDocumentNumberingService documentNumberingService,
    IAuditLogService auditLogService,
    ISecretEncryptionService secretEncryptionService,
    ITenantTimeZoneService tenantTimeZoneService) : TenantAwareControllerBase(tenantContext)
{
    private static readonly string[] RequiredNumberingDocumentTypes =
    [
        DocumentTypes.WorkOrder,
        DocumentTypes.PreventiveMaintenance,
        DocumentTypes.MaterialRequest,
        DocumentTypes.Asset,
        DocumentTypes.Site,
        DocumentTypes.Contract,
        DocumentTypes.Quotation,
        DocumentTypes.ProformaInvoice,
        DocumentTypes.Invoice,
        DocumentTypes.CreditNote,
        DocumentTypes.DebitNote,
        DocumentTypes.Receipt,
        DocumentTypes.Payment,
        DocumentTypes.Expense
    ];

    [HttpGet("company")]
    public async Task<ActionResult<CompanySettingsResponse>> GetCompany(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var tenant = await GetTenantAsync(cancellationToken);
        return Ok(await MapCompanyAsync(tenant, cancellationToken));
    }

    [HttpPut("company")]
    public async Task<ActionResult<CompanySettingsResponse>> UpdateCompany([FromBody] CompanySettingsRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var tenant = await GetTenantAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(request.CompanyName) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Country))
        {
            throw new BusinessRuleException("Company name, email, and country are required.");
        }

        tenant.CompanyName = request.CompanyName.Trim();
        tenant.LegalName = request.LegalName is null ? tenant.LegalName : NormalizeOptional(request.LegalName);
        tenant.TradingName = request.TradingName is null ? tenant.TradingName : NormalizeOptional(request.TradingName);
        tenant.PostalAddress = request.PostalAddress is null ? tenant.PostalAddress : NormalizeOptional(request.PostalAddress);
        tenant.PhysicalAddress = request.PhysicalAddress is null ? tenant.PhysicalAddress : NormalizeOptional(request.PhysicalAddress);
        tenant.Website = request.Website is null ? tenant.Website : NormalizeOptional(request.Website);
        tenant.VatNumber = request.VatNumber is null ? tenant.VatNumber : NormalizeOptional(request.VatNumber);
        tenant.RegistrationNumber = request.RegistrationNumber is null ? tenant.RegistrationNumber : NormalizeOptional(request.RegistrationNumber);
        tenant.Email = request.Email.Trim().ToLowerInvariant();
        tenant.Phone = NormalizeOptional(request.Phone);
        tenant.Country = request.Country.Trim();
        tenant.Industry = NormalizeOptional(request.Industry);
        tenant.Address = request.Address is null ? tenant.Address : NormalizeOptional(request.Address);
        // Null leaves the current override untouched; an explicit blank clears it back to the platform
        // default. Tenant.TimeZoneId is a required column (it always carries a concrete zone), so
        // "cleared" means baked in as whatever the platform default resolves to right now, not null.
        if (request.TimeZoneId is not null)
        {
            tenant.TimeZoneId = tenantTimeZoneService.NormalizeTimeZoneId(request.TimeZoneId)
                ?? await tenantTimeZoneService.GetPlatformTimeZoneIdAsync(cancellationToken);
        }
        tenant.PrimaryColor = string.IsNullOrWhiteSpace(request.PrimaryColor) ? tenant.PrimaryColor : request.PrimaryColor.Trim();
        tenant.SecondaryColor = string.IsNullOrWhiteSpace(request.SecondaryColor) ? tenant.SecondaryColor : request.SecondaryColor.Trim();
        tenant.ShowPoweredByEcosys = request.ShowPoweredByEcosys;
        await dbContext.SaveChangesAsync(cancellationToken);
        tenantTimeZoneService.Invalidate(tenant.Id);
        return Ok(await MapCompanyAsync(tenant, cancellationToken));
    }

    [HttpGet("email-intake")]
    public async Task<ActionResult<EmailIntakeSettingsResponse>> GetEmailIntake(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var settings = await GetOrCreateEmailIntakeSettingAsync(cancellationToken);
        return Ok(MapEmailIntake(settings));
    }

    [HttpPut("email-intake")]
    public async Task<ActionResult<EmailIntakeSettingsResponse>> UpdateEmailIntake([FromBody] EmailIntakeSettingsRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        ValidateEmailIntakeRequest(request);

        var settings = await GetOrCreateEmailIntakeSettingAsync(cancellationToken);
        await ApplyEmailIntakeSettingsAsync(settings, request, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await AuditSettingsChangeAsync("Email intake updated", nameof(EmailIntakeSetting), settings.Id, cancellationToken);
        return Ok(MapEmailIntake(settings));
    }

    /// <summary>Signs in to the mailbox and opens the inbox, so "it works" means intake can really read it.</summary>
    [HttpPost("email-intake/test-connection")]
    public async Task<ActionResult<EmailIntakeTestResponse>> TestEmailIntakeConnection(
        [FromBody] EmailIntakeTestRequest request,
        [FromServices] IEmailIntakeMailbox mailbox,
        CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var settings = await GetOrCreateEmailIntakeSettingAsync(cancellationToken);
        var host = string.IsNullOrWhiteSpace(request.Host) ? settings.Host : request.Host.Trim();
        var port = request.Port > 0 ? request.Port : settings.Port;
        var username = string.IsNullOrWhiteSpace(request.Username) ? settings.Username : request.Username.Trim();
        var password = string.IsNullOrEmpty(request.Password) ? secretEncryptionService.Decrypt(settings.EncryptedPassword) : request.Password;

        if (string.IsNullOrWhiteSpace(host) || port <= 0 || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            throw new BusinessRuleException("Mail server, port, username and password are required to test the mailbox.");
        }

        try
        {
            await mailbox.VerifyAsync(new EmailIntakeMailboxSettings(host, port, request.UseSsl ?? settings.UseSsl, username, password), cancellationToken);
            settings.LastCheckedAt = DateTime.UtcNow;
            settings.IsConnectionHealthy = true;
            settings.LastError = null;
            await dbContext.SaveChangesAsync(cancellationToken);
            return Ok(new EmailIntakeTestResponse(true, settings.LastCheckedAt, null));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            settings.LastCheckedAt = DateTime.UtcNow;
            settings.IsConnectionHealthy = false;
            settings.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            await dbContext.SaveChangesAsync(cancellationToken);
            return Ok(new EmailIntakeTestResponse(false, settings.LastCheckedAt, settings.LastError));
        }
    }

    /// <summary>Reads the mailbox now instead of waiting for the next minute.</summary>
    [HttpPost("email-intake/check-now")]
    public async Task<ActionResult<EmailIntakeCheckResponse>> CheckEmailIntakeNow([FromServices] IEmailIntakeService intake, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var handled = await intake.PollTenantAsync(TenantId, cancellationToken);
        dbContext.ChangeTracker.Clear();
        var settings = await GetOrCreateEmailIntakeSettingAsync(cancellationToken);
        return Ok(new EmailIntakeCheckResponse(handled, settings.LastCheckedAt, settings.IsConnectionHealthy, settings.LastError));
    }

    /// <summary>The latest emails intake read and what happened to each.</summary>
    [HttpGet("email-intake/messages")]
    public async Task<ActionResult<IReadOnlyCollection<EmailIntakeMessageResponse>>> GetEmailIntakeMessages(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var items = await dbContext.EmailIntakeMessages.AsNoTracking()
            .Where(x => x.TenantId == TenantId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .Select(x => new EmailIntakeMessageResponse(
                x.Id, x.ReceivedAt, x.FromAddress, x.FromName, x.Subject, x.Outcome, x.Detail,
                x.WorkOrderId, x.WorkOrder != null ? x.WorkOrder.WorkOrderNumber : null, x.AutoReplySent))
            .ToListAsync(cancellationToken);
        return Ok(items);
    }

    [HttpGet("monitoring-webhooks")]
    public async Task<ActionResult<IReadOnlyCollection<MonitoringWebhookIntegrationResponse>>> GetMonitoringWebhooks(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();

        var items = await dbContext.MonitoringWebhookIntegrations
            .Where(x => x.TenantId == TenantId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return Ok(items.Select(item => MapMonitoringWebhook(item) with { EndpointUrl = BuildWebhookEndpointUrl(item.EndpointSlug) }).ToList());
    }

    [HttpGet("monitoring-webhooks/{id:guid}")]
    public async Task<ActionResult<MonitoringWebhookIntegrationResponse>> GetMonitoringWebhook(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var item = await dbContext.MonitoringWebhookIntegrations
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Monitoring webhook integration was not found.");

        return Ok(MapMonitoringWebhook(item) with { EndpointUrl = BuildWebhookEndpointUrl(item.EndpointSlug) });
    }

    [HttpPost("monitoring-webhooks")]
    public async Task<ActionResult<MonitoringWebhookIntegrationResponse>> CreateMonitoringWebhook([FromBody] UpsertMonitoringWebhookIntegrationRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        ValidateMonitoringWebhookRequest(request);

        var item = new MonitoringWebhookIntegration
        {
            TenantId = TenantId,
            EndpointSlug = await GenerateUniqueEndpointSlugAsync(request.Name, cancellationToken)
        };

        await ApplyMonitoringWebhookAsync(item, request, cancellationToken);
        var generatedSecret = GenerateWebhookSecret();
        item.SecretHash = HashSecret(generatedSecret);

        dbContext.MonitoringWebhookIntegrations.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        await AuditSettingsChangeAsync("Monitoring webhook created", nameof(MonitoringWebhookIntegration), item.Id, cancellationToken);
        return CreatedAtAction(nameof(GetMonitoringWebhook), new { id = item.Id }, MapMonitoringWebhook(item, generatedSecret) with { EndpointUrl = BuildWebhookEndpointUrl(item.EndpointSlug) });
    }

    [HttpPut("monitoring-webhooks/{id:guid}")]
    public async Task<ActionResult<MonitoringWebhookIntegrationResponse>> UpdateMonitoringWebhook(Guid id, [FromBody] UpsertMonitoringWebhookIntegrationRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        ValidateMonitoringWebhookRequest(request);

        var item = await dbContext.MonitoringWebhookIntegrations
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Monitoring webhook integration was not found.");

        await ApplyMonitoringWebhookAsync(item, request, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await AuditSettingsChangeAsync("Monitoring webhook updated", nameof(MonitoringWebhookIntegration), item.Id, cancellationToken);
        return Ok(MapMonitoringWebhook(item) with { EndpointUrl = BuildWebhookEndpointUrl(item.EndpointSlug) });
    }

    [HttpDelete("monitoring-webhooks/{id:guid}")]
    public async Task<IActionResult> DeleteMonitoringWebhook(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var item = await dbContext.MonitoringWebhookIntegrations
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Monitoring webhook integration was not found.");

        dbContext.MonitoringWebhookIntegrations.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        await AuditSettingsChangeAsync("Monitoring webhook deleted", nameof(MonitoringWebhookIntegration), item.Id, cancellationToken);
        return NoContent();
    }

    [HttpPost("monitoring-webhooks/{id:guid}/rotate-secret")]
    public async Task<ActionResult<MonitoringWebhookIntegrationResponse>> RotateMonitoringWebhookSecret(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var item = await dbContext.MonitoringWebhookIntegrations
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Monitoring webhook integration was not found.");

        var generatedSecret = GenerateWebhookSecret();
        item.SecretHash = HashSecret(generatedSecret);
        item.LastStatus = "Secret rotated";
        item.LastError = null;
        await dbContext.SaveChangesAsync(cancellationToken);

        await AuditSettingsChangeAsync("Monitoring webhook secret rotated", nameof(MonitoringWebhookIntegration), item.Id, cancellationToken);
        return Ok(MapMonitoringWebhook(item, generatedSecret) with { EndpointUrl = BuildWebhookEndpointUrl(item.EndpointSlug) });
    }

    [HttpPost("monitoring-webhooks/{id:guid}/test")]
    public async Task<ActionResult<MonitoringWebhookTestResponse>> TestMonitoringWebhook(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var item = await dbContext.MonitoringWebhookIntegrations
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Monitoring webhook integration was not found.");

        item.LastStatus = "Test successful";
        item.LastError = null;
        item.LastReceivedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new MonitoringWebhookTestResponse(true, item.LastReceivedAt, item.LastStatus, null));
    }

    [HttpGet("numbering")]
    public async Task<ActionResult<IReadOnlyCollection<NumberingSettingsResponse>>> GetNumbering([FromQuery] Guid? branchId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        await EnsureDefaultNumberingRulesAsync(cancellationToken);

        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, branchId, cancellationToken);
        var query = dbContext.NumberingSettings.Where(x => x.TenantId == TenantId);

        if (scope.RequestedBranchId.HasValue)
        {
            query = query.Where(x => x.BranchId == scope.RequestedBranchId || x.BranchId == null);
        }
        else if (!IsAdmin)
        {
            query = query.Where(x => x.BranchId == null || (x.BranchId.HasValue && scope.AccessibleBranchIds.Contains(x.BranchId.Value)));
        }

        var settings = await query
            .Include(x => x.Branch)
            .OrderBy(x => x.DocumentType)
            .ThenBy(x => x.BranchId.HasValue ? 1 : 0)
            .ToListAsync(cancellationToken);

        return Ok(settings.Select(MapLegacyNumbering).ToList());
    }

    [HttpPut("numbering")]
    public async Task<ActionResult<NumberingSettingsResponse>> UpdateNumbering([FromBody] NumberingSettingsRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        await ValidateBranchIdAsync(request.BranchId, cancellationToken);

        var setting = await documentNumberingService.UpsertAsync(
            TenantId,
            request.BranchId,
            request.DocumentType,
            request.Prefix,
            request.NextNumber,
            request.PaddingLength,
            request.ResetFrequency,
            request.IncludeYear,
            request.IncludeMonth,
            request.IsActive,
            cancellationToken);

        if (setting.BranchId.HasValue)
        {
            await dbContext.Entry(setting).Reference(x => x.Branch).LoadAsync(cancellationToken);
        }

        return Ok(MapLegacyNumbering(setting));
    }

    [HttpGet("numbering-rules")]
    public async Task<ActionResult<IReadOnlyCollection<NumberingRuleResponse>>> GetNumberingRules(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        await EnsureDefaultNumberingRulesAsync(cancellationToken);

        var settings = await dbContext.NumberingSettings
            .Include(x => x.Branch)
            .Where(x => x.TenantId == TenantId && x.BranchId == null)
            .OrderBy(x => x.DocumentType)
            .ToListAsync(cancellationToken);

        return Ok(settings.Select(MapNumberingRule).ToList());
    }

    [HttpPut("numbering-rules/{id:guid}")]
    public async Task<ActionResult<NumberingRuleResponse>> UpdateNumberingRule(Guid id, [FromBody] UpdateNumberingRuleRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var setting = await dbContext.NumberingSettings
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Numbering rule was not found.");

        var updated = await documentNumberingService.UpsertAsync(
            TenantId,
            setting.BranchId,
            setting.DocumentType,
            request.Prefix,
            request.NextNumber,
            request.PaddingLength,
            request.ResetPeriod,
            request.IncludeYear,
            request.IncludeMonth,
            request.IsActive,
            cancellationToken,
            request.Suffix,
            request.YearFormat ?? "YYYY",
            request.Separator ?? "-");

        await AuditSettingsChangeAsync("Numbering rule updated", nameof(NumberingSetting), updated.Id, cancellationToken);
        return Ok(MapNumberingRule(updated));
    }

    [HttpPost("numbering-rules/{id:guid}/reset")]
    public async Task<ActionResult<NumberingRuleResponse>> ResetNumberingRuleSequence(Guid id, [FromBody] ResetNumberingRuleRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var setting = await dbContext.NumberingSettings
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Numbering rule was not found.");

        if (request.StartFrom < 1)
            throw new BusinessRuleException("Start value must be at least 1.");

        var updated = await documentNumberingService.UpsertAsync(
            TenantId,
            setting.BranchId,
            setting.DocumentType,
            setting.Prefix,
            request.StartFrom,
            setting.PaddingLength,
            setting.ResetFrequency,
            setting.IncludeYear,
            setting.IncludeMonth,
            setting.IsActive,
            cancellationToken,
            setting.Suffix,
            setting.YearFormat,
            setting.Separator);

        await AuditSettingsChangeAsync($"Numbering sequence reset to {request.StartFrom}", nameof(NumberingSetting), updated.Id, cancellationToken);
        return Ok(MapNumberingRule(updated));
    }

    [HttpPost("numbering-rules/seed-defaults")]
    public async Task<ActionResult<IReadOnlyCollection<NumberingRuleResponse>>> SeedDefaultNumberingRules(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        await EnsureDefaultNumberingRulesAsync(cancellationToken);

        var settings = await dbContext.NumberingSettings
            .Where(x => x.TenantId == TenantId && x.BranchId == null)
            .OrderBy(x => x.DocumentType)
            .ToListAsync(cancellationToken);

        return Ok(settings.Select(MapNumberingRule).ToList());
    }

    [HttpPost("numbering-rules/preview")]
    public async Task<ActionResult<NumberingPreviewResponse>> PreviewNumberingRule([FromBody] NumberingPreviewRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        await EnsureDefaultNumberingRulesAsync(cancellationToken);

        NumberingSetting setting;
        if (request.RuleId.HasValue)
        {
            setting = await dbContext.NumberingSettings
                .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == request.RuleId.Value, cancellationToken)
                ?? throw new NotFoundException("Numbering rule was not found.");
        }
        else
        {
            var documentType = NormalizeNumberingDocumentType(request.DocumentType);
            setting = await dbContext.NumberingSettings
                .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.BranchId == null && x.DocumentType == documentType, cancellationToken)
                ?? throw new NotFoundException("Numbering rule was not found.");
        }

        return Ok(new NumberingPreviewResponse(setting.Id, setting.DocumentType, BuildPreview(setting)));
    }

    private async Task<Tenant> GetTenantAsync(CancellationToken cancellationToken) =>
        await dbContext.Tenants.SingleOrDefaultAsync(x => x.Id == TenantId, cancellationToken)
        ?? throw new NotFoundException("Tenant settings were not found.");

    private Task<EmailIntakeSetting> GetOrCreateEmailIntakeSettingAsync(CancellationToken cancellationToken) =>
        dbContext.GetOrCreateAsync(
            dbContext.EmailIntakeSettings.Where(x => x.TenantId == TenantId),
            () => new EmailIntakeSetting
            {
                TenantId = TenantId,
                MailboxProvider = "IMAP",
                Host = string.Empty,
                Port = 993,
                UseSsl = true,
                DefaultPriority = "Medium",
                CreateWorkOrderFromUnknownSender = false
            },
            cancellationToken);

    private static void ValidateEmailIntakeRequest(EmailIntakeSettingsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.MailboxProvider))
        {
            throw new BusinessRuleException("Mailbox provider is required.");
        }

        if (request.IsEnabled && (string.IsNullOrWhiteSpace(request.Host) || request.Port <= 0))
        {
            throw new BusinessRuleException("Host and port are required when email intake is enabled.");
        }
    }

    private async Task ApplyEmailIntakeSettingsAsync(EmailIntakeSetting settings, EmailIntakeSettingsRequest request, CancellationToken cancellationToken)
    {
        // A different mailbox starts reading afresh from its newest email.
        if (!string.Equals(settings.Host, NormalizeOptional(request.Host) ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(settings.Username, NormalizeOptional(request.Username), StringComparison.OrdinalIgnoreCase))
        {
            settings.ImapUidValidity = null;
            settings.ImapLastUid = null;
        }

        settings.IsEnabled = request.IsEnabled;
        settings.IntakeEmailAddress = NormalizeOptional(request.IntakeEmailAddress);
        settings.MailboxProvider = request.MailboxProvider.Trim();
        settings.Host = NormalizeOptional(request.Host) ?? string.Empty;
        settings.Port = request.Port;
        settings.UseSsl = request.UseSsl;
        settings.Username = NormalizeOptional(request.Username);
        settings.DefaultClientId = await ValidateClientIdAsync(request.DefaultClientId, cancellationToken);
        settings.DefaultBranchId = await ValidateBranchIdAsync(request.DefaultBranchId, cancellationToken);
        settings.DefaultAssignmentGroupId = await ValidateAssignmentGroupIdAsync(request.DefaultAssignmentGroupId, cancellationToken);
        settings.DefaultPriority = string.IsNullOrWhiteSpace(request.DefaultPriority) ? "Medium" : request.DefaultPriority.Trim();
        settings.CreateWorkOrderFromUnknownSender = request.CreateWorkOrderFromUnknownSender;
        settings.SubjectParsingRules = NormalizeOptional(request.SubjectParsingRules);
        settings.AllowedSenderDomains = NormalizeOptional(request.AllowedSenderDomains);
        settings.AutoReplyEnabled = request.AutoReplyEnabled ?? settings.AutoReplyEnabled;

        var encryptedPassword = secretEncryptionService.Encrypt(request.Password);
        if (!string.IsNullOrWhiteSpace(encryptedPassword))
        {
            settings.EncryptedPassword = encryptedPassword;
        }
    }

    private async Task ApplyMonitoringWebhookAsync(MonitoringWebhookIntegration item, UpsertMonitoringWebhookIntegrationRequest request, CancellationToken cancellationToken)
    {
        item.Name = request.Name.Trim();
        item.ToolType = request.ToolType.Trim();
        item.IsActive = request.IsActive;
        item.DefaultClientId = await ValidateClientIdAsync(request.DefaultClientId, cancellationToken);
        item.DefaultAssetId = await ValidateAssetIdAsync(request.DefaultAssetId, cancellationToken);
        item.DefaultBranchId = await ValidateBranchIdAsync(request.DefaultBranchId, cancellationToken);
        item.DefaultAssignmentGroupId = await ValidateAssignmentGroupIdAsync(request.DefaultAssignmentGroupId, cancellationToken);
        item.DefaultPriority = string.IsNullOrWhiteSpace(request.DefaultPriority) ? "Medium" : request.DefaultPriority.Trim();
        item.CreateWorkOrderOnAlert = request.CreateWorkOrderOnAlert;
        item.PayloadMappingJson = NormalizeOptional(request.PayloadMappingJson);
    }

    private static void ValidateMonitoringWebhookRequest(UpsertMonitoringWebhookIntegrationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.ToolType))
        {
            throw new BusinessRuleException("Name and tool type are required.");
        }
    }

    private async Task<Guid?> ValidateClientIdAsync(Guid? clientId, CancellationToken cancellationToken)
    {
        if (!clientId.HasValue)
        {
            return null;
        }

        var exists = await dbContext.Clients.AnyAsync(x => x.TenantId == TenantId && x.IsActive && x.Id == clientId.Value, cancellationToken);
        if (!exists)
        {
            throw new BusinessRuleException("Default client was not found for this tenant.");
        }

        return clientId.Value;
    }

    private async Task<Guid?> ValidateAssetIdAsync(Guid? assetId, CancellationToken cancellationToken)
    {
        if (!assetId.HasValue)
        {
            return null;
        }

        var exists = await dbContext.Assets.AnyAsync(x => x.TenantId == TenantId && x.Id == assetId.Value, cancellationToken);
        if (!exists)
        {
            throw new BusinessRuleException("Default asset was not found for this tenant.");
        }

        return assetId.Value;
    }

    private async Task<Guid?> ValidateBranchIdAsync(Guid? branchId, CancellationToken cancellationToken)
    {
        if (!branchId.HasValue)
        {
            return null;
        }

        var exists = await dbContext.Branches.AnyAsync(x => x.TenantId == TenantId && x.IsActive && x.Id == branchId.Value, cancellationToken);
        if (!exists)
        {
            throw new BusinessRuleException("Branch was not found for this tenant.");
        }

        return branchId.Value;
    }

    private async Task<Guid?> ValidateAssignmentGroupIdAsync(Guid? assignmentGroupId, CancellationToken cancellationToken)
    {
        if (!assignmentGroupId.HasValue)
        {
            return null;
        }

        var exists = await dbContext.AssignmentGroups.AnyAsync(x => x.TenantId == TenantId && x.IsActive && x.Id == assignmentGroupId.Value, cancellationToken);
        if (!exists)
        {
            throw new BusinessRuleException("Assignment group was not found for this tenant.");
        }

        return assignmentGroupId.Value;
    }

    private async Task EnsureDefaultNumberingRulesAsync(CancellationToken cancellationToken)
    {
        foreach (var documentType in RequiredNumberingDocumentTypes)
        {
            var exists = await dbContext.NumberingSettings.AnyAsync(
                x => x.TenantId == TenantId && x.BranchId == null && x.DocumentType == documentType,
                cancellationToken);

            if (exists)
            {
                continue;
            }

            await documentNumberingService.UpsertAsync(
                TenantId,
                null,
                documentType,
                DefaultPrefix(documentType),
                1,
                6,
                NumberResetFrequencies.Never,
                false,
                false,
                true,
                cancellationToken);
        }
    }

    private static string DefaultPrefix(string documentType) =>
        documentType switch
        {
            DocumentTypes.WorkOrder => "WO",
            DocumentTypes.PreventiveMaintenance => "PM",
            DocumentTypes.MaterialRequest => "MR",
            DocumentTypes.Asset => "ASSET",
            DocumentTypes.Site => "SITE",
            DocumentTypes.Contract => "CON",
            DocumentTypes.Quotation => "QT",
            DocumentTypes.ProformaInvoice => "PI",
            DocumentTypes.Invoice => "INV",
            DocumentTypes.CreditNote => "CN",
            DocumentTypes.DebitNote => "DN",
            DocumentTypes.Receipt => "REC",
            DocumentTypes.Payment => "PAY",
            DocumentTypes.Expense => "EXP",
            _ => "DOC"
        };

    private static string BuildPreview(NumberingSetting setting)
    {
        var sep = string.IsNullOrEmpty(setting.Separator) ? "-" : setting.Separator;
        var now = DateTime.UtcNow;
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(setting.Prefix))
            parts.Add(setting.Prefix.Trim().ToUpperInvariant());
        if (setting.IncludeYear)
            parts.Add(string.Equals(setting.YearFormat, "YY", StringComparison.OrdinalIgnoreCase)
                ? now.Year.ToString()[2..] : now.Year.ToString("0000"));
        if (setting.IncludeMonth)
            parts.Add(now.Month.ToString("00"));
        parts.Add(setting.NextNumber.ToString().PadLeft(setting.PaddingLength, '0'));
        if (!string.IsNullOrWhiteSpace(setting.Suffix))
            parts.Add(setting.Suffix.Trim().ToUpperInvariant());
        return string.Join(sep, parts);
    }

    private static string NormalizeNumberingDocumentType(string? documentType)
    {
        if (string.IsNullOrWhiteSpace(documentType))
            throw new BusinessRuleException("Document type is required.");

        return DocumentTypes.Normalize(documentType.Trim());
    }

    private async Task<string> GenerateUniqueEndpointSlugAsync(string name, CancellationToken cancellationToken)
    {
        var baseSlug = string.Concat(name.Trim().ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '-')).Trim('-');
        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            baseSlug = "webhook";
        }

        baseSlug = string.Join("-", baseSlug.Split('-', StringSplitOptions.RemoveEmptyEntries));
        var slug = baseSlug;
        var suffix = 1;

        while (await dbContext.MonitoringWebhookIntegrations.AnyAsync(x => x.EndpointSlug == slug, cancellationToken))
        {
            suffix += 1;
            slug = $"{baseSlug}-{suffix}";
        }

        return slug;
    }

    private static string GenerateWebhookSecret() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

    private static string HashSecret(string secret) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret.Trim())));

    private string BuildWebhookEndpointUrl(string endpointSlug)
    {
        var origin = Request.Headers.Origin.FirstOrDefault();
        var baseUrl = !string.IsNullOrWhiteSpace(origin)
            ? origin.TrimEnd('/')
            : $"{Request.Scheme}://{Request.Host.Value}".TrimEnd('/');

        return $"{baseUrl}/api/integrations/webhooks/{endpointSlug}";
    }

    private async Task AuditSettingsChangeAsync(string action, string entityName, Guid entityId, CancellationToken cancellationToken)
    {
        await auditLogService.LogAsync(TenantId, UserId, action, entityName, entityId.ToString(), action, cancellationToken);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<CompanySettingsResponse> MapCompanyAsync(Tenant tenant, CancellationToken cancellationToken) =>
        new(
            tenant.CompanyName,
            tenant.LegalName,
            tenant.TradingName,
            tenant.PostalAddress,
            tenant.PhysicalAddress,
            tenant.Website,
            tenant.VatNumber,
            tenant.RegistrationNumber,
            tenant.Email,
            tenant.Phone,
            tenant.Country,
            tenant.Industry,
            tenant.Address,
            tenant.TimeZoneId,
            await tenantTimeZoneService.GetTimeZoneIdAsync(tenant.Id, cancellationToken),
            tenant.PrimaryColor,
            tenant.SecondaryColor,
            tenant.ShowPoweredByEcosys,
            tenant.Slug);

    private static EmailIntakeSettingsResponse MapEmailIntake(EmailIntakeSetting settings) =>
        new(
            settings.Id,
            settings.TenantId,
            settings.IsEnabled,
            settings.IntakeEmailAddress,
            settings.MailboxProvider,
            settings.Host,
            settings.Port,
            settings.UseSsl,
            settings.Username,
            !string.IsNullOrWhiteSpace(settings.EncryptedPassword),
            settings.DefaultClientId,
            settings.DefaultBranchId,
            settings.DefaultAssignmentGroupId,
            settings.DefaultPriority,
            settings.CreateWorkOrderFromUnknownSender,
            settings.SubjectParsingRules,
            settings.AllowedSenderDomains,
            settings.LastCheckedAt,
            settings.IsConnectionHealthy,
            settings.LastError,
            settings.AutoReplyEnabled,
            settings.ImapLastUid.HasValue);

    private static MonitoringWebhookIntegrationResponse MapMonitoringWebhook(MonitoringWebhookIntegration item, string? generatedSecret = null) =>
        new(
            item.Id,
            item.TenantId,
            item.Name,
            item.ToolType,
            item.EndpointSlug,
            item.IsActive,
            item.DefaultClientId,
            item.DefaultAssetId,
            item.DefaultBranchId,
            item.DefaultAssignmentGroupId,
            item.DefaultPriority,
            item.CreateWorkOrderOnAlert,
            item.PayloadMappingJson,
            item.LastReceivedAt,
            item.LastStatus,
            item.LastError,
            item.CreatedAt,
            item.UpdatedAt,
            true,
            generatedSecret);

    private static NumberingSettingsResponse MapLegacyNumbering(NumberingSetting settings) =>
        new(
            settings.Id,
            settings.BranchId,
            settings.Branch?.Name,
            settings.DocumentType,
            settings.Prefix,
            settings.Suffix,
            settings.NextNumber,
            settings.PaddingLength,
            settings.ResetFrequency,
            settings.IncludeYear,
            settings.YearFormat,
            settings.IncludeMonth,
            settings.Separator,
            settings.IsLocked,
            settings.IsActive);

    private static NumberingRuleResponse MapNumberingRule(NumberingSetting settings) =>
        new(
            settings.Id,
            settings.DocumentType,
            settings.Prefix,
            settings.Suffix,
            settings.NextNumber,
            settings.PaddingLength,
            settings.ResetFrequency,
            settings.YearFormat,
            settings.IncludeYear,
            settings.IncludeMonth,
            settings.Separator,
            settings.IsLocked,
            BuildPreview(settings),
            settings.IsActive,
            settings.CreatedAt,
            settings.UpdatedAt);
}
