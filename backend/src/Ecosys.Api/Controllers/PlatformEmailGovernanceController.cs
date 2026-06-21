using Ecosys.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize(Policy = "PlatformSettingsAccess")]
[Route("api/platform/settings/email")]
public sealed class PlatformEmailGovernanceController(
    IEmailNotificationRegistry emailNotificationRegistry,
    IEmailDeliveryLogService emailDeliveryLogService) : ControllerBase
{
    [HttpGet("notification-rules")]
    public ActionResult<IReadOnlyCollection<PlatformEmailNotificationRuleResponse>> GetNotificationRules()
    {
        var rules = emailNotificationRegistry.List()
            .Select(item => new PlatformEmailNotificationRuleResponse(
                item.EventKey,
                item.DisplayName,
                item.TemplateKey,
                item.RecipientStrategy,
                item.SenderScope,
                item.DispatchStatus,
                item.Description,
                item.SupportedChannels.ToList(),
                item.Notes))
            .ToList();

        return Ok(rules);
    }

    [HttpGet("delivery-logs")]
    public async Task<ActionResult<IReadOnlyCollection<PlatformEmailDeliveryLogResponse>>> GetDeliveryLogs(
        [FromQuery] string? status,
        [FromQuery] string? templateKey,
        [FromQuery] string? eventKey,
        [FromQuery] string? recipientEmail,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken)
    {
        var logs = await emailDeliveryLogService.ListPlatformAsync(
            new EmailDeliveryLogQuery(status, templateKey, eventKey, recipientEmail, dateFrom, dateTo),
            cancellationToken);

        return Ok(logs.Select(item => new PlatformEmailDeliveryLogResponse(
            item.Id,
            item.TenantId,
            item.EventKey,
            item.TemplateKey,
            item.RecipientEmail,
            item.Subject,
            item.Status,
            item.AttemptCount,
            item.LastAttemptAt,
            item.NextAttemptAt,
            item.ErrorCategory,
            item.ErrorMessage,
            item.TriggeredByUserId,
            item.CreatedAt,
            item.SentAt,
            item.ProviderMessageId)).ToList());
    }
}

public sealed record PlatformEmailNotificationRuleResponse(
    string EventKey,
    string DisplayName,
    string TemplateKey,
    string RecipientStrategy,
    string SenderScope,
    string DispatchStatus,
    string Description,
    IReadOnlyCollection<string> SupportedChannels,
    string Notes);

public sealed record PlatformEmailDeliveryLogResponse(
    Guid Id,
    Guid? TenantId,
    string EventKey,
    string TemplateKey,
    string RecipientEmail,
    string Subject,
    string Status,
    int? AttemptCount,
    DateTime? LastAttemptAt,
    DateTime? NextAttemptAt,
    string? ErrorCategory,
    string? ErrorMessage,
    Guid? TriggeredByUserId,
    DateTime CreatedAt,
    DateTime? SentAt,
    string? ProviderMessageId);
