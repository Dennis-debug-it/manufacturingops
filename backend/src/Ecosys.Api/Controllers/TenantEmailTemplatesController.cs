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
[Route("api/settings/email-templates")]
public sealed class TenantEmailTemplatesController(
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IEmailTemplateService emailTemplateService,
    IEmailSubjectRuleService emailSubjectRuleService,
    IEmailOutboxService emailOutboxService,
    ITenantTimeZoneService tenantTimeZoneService,
    IEmailSettingsResolver emailSettingsResolver,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<EmailTemplateResponse>>> List(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var templates = await emailTemplateService.ListTenantTemplatesAsync(TenantId, cancellationToken);
        return Ok(templates.Select(Map));
    }

    [HttpGet("{eventKey}")]
    public async Task<ActionResult<EmailTemplateResponse>> Get(string eventKey, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        return Ok(Map(await emailTemplateService.GetTenantTemplateAsync(TenantId, eventKey, cancellationToken)));
    }

    [HttpPut("{eventKey}")]
    public async Task<ActionResult<EmailTemplateResponse>> Save(string eventKey, [FromBody] EmailTemplateUpsertRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var template = await emailTemplateService.SaveTenantTemplateAsync(
            TenantId,
            eventKey,
            new EmailTemplateUpdateRequest(
                request.TemplateName,
                request.Subject,
                request.HtmlBody,
                request.TextBody,
                request.Enabled,
                request.SenderNameOverride,
                request.ReplyToOverride),
            UserId,
            cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "tenant.email-template.updated",
            nameof(PlatformSetting),
            eventKey,
            $"Tenant email template '{eventKey}' was updated.",
            cancellationToken);

        return Ok(Map(template));
    }

    [HttpPost("{eventKey}/preview")]
    public async Task<ActionResult<EmailTemplatePreviewResponse>> Preview(string eventKey, [FromBody] EmailTemplatePreviewRequest? request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var rendered = await emailTemplateService.RenderTenantTemplateAsync(TenantId, eventKey, BuildSampleValues(request?.SampleData, await SampleExpiryAsync(cancellationToken)), cancellationToken);
        var finalSubject = await emailSubjectRuleService.BuildFinalSubjectAsync(
            TenantId,
            eventKey,
            rendered.Subject,
            cancellationToken: cancellationToken);
        return Ok(new EmailTemplatePreviewResponse(rendered.EventKey, rendered.TemplateName, rendered.Subject, finalSubject, rendered.HtmlBody, rendered.TextBody));
    }

    [HttpPost("{eventKey}/test")]
    public async Task<ActionResult<EmailTemplateTestResponse>> SendTest(string eventKey, [FromBody] EmailTemplateTestRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var settings = (await emailSettingsResolver.ResolveAsync(TenantId, cancellationToken)).Settings
            ?? throw new BusinessRuleException("Email settings are not configured.");

        var rendered = await emailTemplateService.RenderTenantTemplateAsync(TenantId, eventKey, BuildSampleValues(request.SampleData, await SampleExpiryAsync(cancellationToken)), cancellationToken);
        if (!rendered.Enabled)
        {
            return Ok(new EmailTemplateTestResponse(false, "This email template is disabled."));
        }

        var recipient = string.IsNullOrWhiteSpace(request.TestRecipientEmail) ? settings.SenderAddress : request.TestRecipientEmail.Trim();
        if (string.IsNullOrWhiteSpace(recipient))
        {
            throw new BusinessRuleException("A test recipient email is required.");
        }

        var finalSubject = await emailSubjectRuleService.BuildFinalSubjectAsync(
            TenantId,
            eventKey,
            rendered.Subject,
            cancellationToken: cancellationToken);

        await emailOutboxService.QueueEmailAsync(
            new QueueEmailRequest(
                TenantId,
                eventKey,
                eventKey,
                recipient,
                null,
                rendered.SenderNameOverride ?? settings.SenderName,
                settings.SenderAddress,
                rendered.ReplyToOverride ?? settings.ReplyToEmail,
                finalSubject,
                rendered.HtmlBody,
                rendered.TextBody,
                UserId),
            cancellationToken);

        return Ok(new EmailTemplateTestResponse(true, "Template test email queued. Check Delivery Logs for status."));
    }

    [HttpPost("{eventKey}/reset")]
    public async Task<ActionResult<EmailTemplateResponse>> Reset(string eventKey, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        return Ok(Map(await emailTemplateService.ResetTenantTemplateAsync(TenantId, eventKey, cancellationToken)));
    }

    private Task<string> SampleExpiryAsync(CancellationToken cancellationToken) =>
        tenantTimeZoneService.FormatForTenantAsync(DateTime.UtcNow.AddHours(1), TenantId, cancellationToken: cancellationToken);

    private static Dictionary<string, string?> BuildSampleValues(IReadOnlyDictionary<string, string?>? sampleData, string expiresAtSample)
    {
        var values = EmailTemplateVariables.WithRecipientAndActorAliases(
            "Jane Doe",
            "Alex Kimani",
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["email"] = "jane.doe@example.com",
                ["temporaryPassword"] = "Eco!TempPass9",
                ["inviteLink"] = "https://app.example.com/reset-password?token=sample&mode=invite",
                ["setPasswordUrl"] = "https://app.example.com/reset-password?token=sample&mode=invite",
                ["activateAccountUrl"] = "https://app.example.com/reset-password?token=sample&mode=invite",
                ["resetPasswordLink"] = "https://app.example.com/reset-password?token=sample",
                ["resetLink"] = "https://app.example.com/reset-password?token=sample",
                ["loginUrl"] = "https://app.example.com/login",
                ["companyName"] = "Acme Facilities Ltd",
                ["workspaceName"] = "Acme Facilities Ltd",
                ["tenantName"] = "Acme Facilities Ltd",
                ["platformName"] = "Ecosys ServiceOps",
                ["workOrderNumber"] = "WO-000123",
                ["assetName"] = "Generator 250kVA",
                ["assignedTo"] = "Alex Kimani",
                ["priority"] = "High",
                ["dueDate"] = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd"),
                ["supportEmail"] = "support@ecosysdigital.co.ke",
                ["senderName"] = "Ecosys ServiceOps",
                ["sentAt"] = DateTime.UtcNow.ToString("u"),
                ["contactPhone"] = "+254700000001",
                ["country"] = "Kenya",
                ["industry"] = "Facilities",
                ["message"] = "We need help onboarding our team.",
                ["leadCompanyName"] = "Acme Facilities Ltd",
                ["contactName"] = "Jane Doe",
                ["contactEmail"] = "jane.doe@example.com",
                ["submittedAt"] = DateTime.UtcNow.ToString("u"),
                ["expiresAt"] = expiresAtSample,
                ["expiresAtUtc"] = DateTime.UtcNow.AddHours(1).ToString("u")
            });

        if (sampleData is not null)
        {
            foreach (var item in sampleData)
            {
                values[item.Key] = item.Value;
            }
        }

        return values;
    }

    private static EmailTemplateResponse Map(EmailTemplateDescriptor template) =>
        new(
            template.EventKey,
            template.TemplateName,
            template.Subject,
            template.HtmlBody,
            template.TextBody,
            template.Enabled,
            template.SenderNameOverride,
            template.ReplyToOverride,
            template.AvailablePlaceholders.ToList(),
            template.RequiredPlaceholders.ToList(),
            template.SupportsTenantOverride,
            template.IsOverride,
            template.Source,
            template.LastUpdatedBy,
            template.LastUpdatedAt);
}
