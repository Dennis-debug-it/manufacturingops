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
[Authorize(Policy = "PlatformSettingsAccess")]
[Route("api/platform/settings/email-templates")]
public sealed class PlatformEmailTemplatesController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IEmailTemplateService emailTemplateService,
    IEmailSubjectRuleService emailSubjectRuleService,
    IEmailOutboxService emailOutboxService,
    IAuditLogService auditLogService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<EmailTemplateResponse>>> List(CancellationToken cancellationToken)
    {
        var templates = await emailTemplateService.ListPlatformTemplatesAsync(cancellationToken);
        return Ok(templates.Select(Map));
    }

    [HttpGet("{eventKey}")]
    public async Task<ActionResult<EmailTemplateResponse>> Get(string eventKey, CancellationToken cancellationToken)
    {
        var template = await emailTemplateService.GetPlatformTemplateAsync(eventKey, cancellationToken);
        return Ok(Map(template));
    }

    [HttpPut("{eventKey}")]
    public async Task<ActionResult<EmailTemplateResponse>> Save(string eventKey, [FromBody] EmailTemplateUpsertRequest request, CancellationToken cancellationToken)
    {
        var template = await emailTemplateService.SavePlatformTemplateAsync(
            eventKey,
            new EmailTemplateUpdateRequest(
                request.TemplateName,
                request.Subject,
                request.HtmlBody,
                request.TextBody,
                request.Enabled,
                request.SenderNameOverride,
                request.ReplyToOverride),
            tenantContext.GetRequiredUserId(),
            cancellationToken);

        await auditLogService.LogAsync(
            PlatformConstants.RootTenantId,
            tenantContext.GetRequiredUserId(),
            "platform.email-template.updated",
            nameof(PlatformSetting),
            eventKey,
            $"Platform email template '{eventKey}' was updated.",
            cancellationToken: cancellationToken);

        return Ok(Map(template));
    }

    [HttpPost("{eventKey}/preview")]
    public async Task<ActionResult<EmailTemplatePreviewResponse>> Preview(string eventKey, [FromBody] EmailTemplatePreviewRequest? request, CancellationToken cancellationToken)
    {
        var rendered = await emailTemplateService.RenderPlatformTemplateAsync(eventKey, BuildSampleValues(request?.SampleData), cancellationToken);
        var finalSubject = await emailSubjectRuleService.BuildFinalSubjectAsync(
            PlatformConstants.RootTenantId,
            ResolveNotificationEventKey(eventKey),
            rendered.Subject,
            cancellationToken: cancellationToken);
        return Ok(new EmailTemplatePreviewResponse(rendered.EventKey, rendered.TemplateName, rendered.Subject, finalSubject, rendered.HtmlBody, rendered.TextBody));
    }

    [HttpPost("{eventKey}/test")]
    public async Task<ActionResult<EmailTemplateTestResponse>> SendTest(string eventKey, [FromBody] EmailTemplateTestRequest request, CancellationToken cancellationToken)
    {
        var settings = await dbContext.EmailSettings.SingleOrDefaultAsync(x => x.TenantId == PlatformConstants.RootTenantId, cancellationToken)
            ?? throw new BusinessRuleException("Platform email settings are not configured.");

        var rendered = await emailTemplateService.RenderPlatformTemplateAsync(eventKey, BuildSampleValues(request.SampleData), cancellationToken);
        if (!rendered.Enabled)
        {
            return Ok(new EmailTemplateTestResponse(false, "This email template is disabled."));
        }

        var recipient = string.IsNullOrWhiteSpace(request.TestRecipientEmail)
            ? settings.SenderAddress
            : request.TestRecipientEmail.Trim();

        if (string.IsNullOrWhiteSpace(recipient))
        {
            throw new BusinessRuleException("A test recipient email is required.");
        }

        var finalSubject = await emailSubjectRuleService.BuildFinalSubjectAsync(
            PlatformConstants.RootTenantId,
            ResolveNotificationEventKey(eventKey),
            rendered.Subject,
            cancellationToken: cancellationToken);

        await emailOutboxService.QueueEmailAsync(
            new QueueEmailRequest(
                PlatformConstants.RootTenantId,
                ResolveNotificationEventKey(eventKey),
                eventKey,
                recipient,
                null,
                rendered.SenderNameOverride ?? settings.SenderName,
                settings.SenderAddress,
                rendered.ReplyToOverride ?? settings.ReplyToEmail,
                finalSubject,
                rendered.HtmlBody,
                rendered.TextBody,
                tenantContext.GetRequiredUserId()),
            cancellationToken);

        return Ok(new EmailTemplateTestResponse(true, "Template test email queued. Check Delivery Logs for status."));
    }

    [HttpPost("{eventKey}/reset")]
    public async Task<ActionResult<EmailTemplateResponse>> Reset(string eventKey, CancellationToken cancellationToken)
    {
        var template = await emailTemplateService.ResetPlatformTemplateAsync(eventKey, cancellationToken);
        return Ok(Map(template));
    }

    private static Dictionary<string, string?> BuildSampleValues(IReadOnlyDictionary<string, string?>? sampleData)
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
                ["expiresAt"] = DateTime.UtcNow.AddHours(1).ToString("u")
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

    private static string ResolveNotificationEventKey(string eventKey) => eventKey switch
    {
        "test-email" => "email.test",
        _ => eventKey,
    };

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

public sealed record EmailTemplateResponse(
    string EventKey,
    string TemplateName,
    string Subject,
    string HtmlBody,
    string TextBody,
    bool Enabled,
    string? SenderNameOverride,
    string? ReplyToOverride,
    IReadOnlyCollection<string> AvailablePlaceholders,
    IReadOnlyCollection<string> RequiredPlaceholders,
    bool SupportsTenantOverride,
    bool IsOverride,
    string Source,
    Guid? LastUpdatedBy,
    DateTime? LastUpdatedAt);

public sealed record EmailTemplateUpsertRequest(
    string TemplateName,
    string Subject,
    string HtmlBody,
    string? TextBody,
    bool Enabled,
    string? SenderNameOverride,
    string? ReplyToOverride);

public sealed record EmailTemplatePreviewRequest(IReadOnlyDictionary<string, string?>? SampleData);
public sealed record EmailTemplatePreviewResponse(string EventKey, string TemplateName, string TemplateSubject, string FinalSubject, string HtmlBody, string TextBody);
public sealed record EmailTemplateTestRequest(string? TestRecipientEmail, IReadOnlyDictionary<string, string?>? SampleData);
public sealed record EmailTemplateTestResponse(bool Success, string? Message);
