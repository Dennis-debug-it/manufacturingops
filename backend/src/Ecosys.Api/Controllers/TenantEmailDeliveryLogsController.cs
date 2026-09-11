using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/settings/email/delivery-logs")]
public sealed class TenantEmailDeliveryLogsController(
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IEmailDeliveryLogService emailDeliveryLogService,
    IEmailOutboxService emailOutboxService,
    Ecosys.Infrastructure.Data.AppDbContext dbContext,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<TenantEmailDeliveryLogResponse>>> List(
        [FromQuery] string? status,
        [FromQuery] string? templateKey,
        [FromQuery] string? eventKey,
        [FromQuery] string? recipientEmail,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] string? documentType,
        CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.FinanceView, PermissionNames.FinanceManage, PermissionNames.CanManageSettings);
        var logs = await emailDeliveryLogService.ListTenantAsync(
            TenantId, new EmailDeliveryLogQuery(status, templateKey, eventKey, recipientEmail, dateFrom, dateTo, documentType), cancellationToken);
        return Ok(logs.Select(Map).ToList());
    }

    [HttpPost("{id:guid}/resend")]
    public async Task<ActionResult<TenantEmailDeliveryLogResponse>> Resend(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.FinanceManage, PermissionNames.CanManageSettings);
        var log = await dbContext.EmailDeliveryLogs
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Delivery log was not found.");
        if (!log.OutboxMessageId.HasValue)
            throw new BusinessRuleException("This message cannot be resent from the delivery log.");

        var outbox = await dbContext.EmailOutboxMessages
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == log.OutboxMessageId.Value, cancellationToken)
            ?? throw new NotFoundException("The queued message was not found.");
        if (outbox.Status is not (EmailOutboxStatuses.Failed or EmailOutboxStatuses.Cancelled))
            throw new BusinessRuleException("Only failed or cancelled messages can be resent.");

        await emailOutboxService.RetryAsync(outbox.Id, DateTime.UtcNow, "Manually queued for retry.", cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "email.delivery.resent", nameof(EmailDeliveryLog), log.Id.ToString(),
            $"Delivery retry queued for {log.TemplateKey} to {log.RecipientEmail}.", cancellationToken);
        log.Status = EmailOutboxStatuses.Pending;
        log.ErrorMessage = null;
        return Ok(Map(log));
    }

    private static TenantEmailDeliveryLogResponse Map(EmailDeliveryLog log) => new(
        log.Id, log.TenantId, log.EventKey, log.TemplateKey, log.RecipientEmail, log.Subject, log.Status,
        log.AttemptCount, log.LastAttemptAt, log.NextAttemptAt, log.ErrorCategory, log.ErrorMessage,
        log.TriggeredByUserId, log.CreatedAt, log.SentAt, log.ProviderMessageId,
        log.DocumentType, log.DocumentId, log.TriggeredAutomatically);
}

public sealed record TenantEmailDeliveryLogResponse(
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
    string? ProviderMessageId,
    string? DocumentType,
    Guid? DocumentId,
    bool TriggeredAutomatically);
