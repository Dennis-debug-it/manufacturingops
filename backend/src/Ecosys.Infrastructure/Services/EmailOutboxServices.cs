using System.Text.RegularExpressions;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Errors;
using Ecosys.Shared.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecosys.Infrastructure.Services;

public static class EmailOutboxStatuses
{
    public const string Pending = "Pending";
    public const string Sending = "Sending";
    public const string Sent = "Sent";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
}

public sealed record QueueEmailRequest(
    Guid? TenantId,
    string EventKey,
    string TemplateKey,
    string RecipientEmail,
    string? RecipientName,
    string? SenderName,
    string? SenderEmail,
    string? ReplyToEmail,
    string Subject,
    string? HtmlBody,
    string? TextBody,
    Guid? TriggeredByUserId,
    int MaxAttempts = 3);

public sealed record UpdateOutboxStatusRequest(
    Guid OutboxMessageId,
    string Status,
    string? ErrorCategory,
    string? ErrorMessage,
    int AttemptCount,
    DateTime? LastAttemptAt,
    DateTime? NextAttemptAt,
    DateTime? SentAt,
    string? ProviderMessageId = null);

public interface IEmailOutboxService
{
    Task<EmailOutboxMessage> QueueEmailAsync(QueueEmailRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<EmailOutboxMessage>> GetPendingBatchAsync(int batchSize, DateTime utcNow, CancellationToken cancellationToken = default);
    Task<bool> MarkSendingAsync(Guid outboxMessageId, DateTime utcNow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns messages whose <c>Sending</c> claim is older than <paramref name="claimTimeout"/> to
    /// the queue. <see cref="MarkSendingAsync"/> moves a row to <c>Sending</c> and the batch query
    /// only ever selects <c>Pending</c>, so an instance that stopped mid-send left its claimed rows
    /// unreachable for ever — verified live with three messages stuck in <c>Sending</c>. Each rescue
    /// counts an attempt, so a message that kills whichever worker picks it up still reaches the
    /// terminal <c>Failed</c> state instead of looping (EC-P1-06d).
    /// </summary>
    Task<int> ReleaseAbandonedClaimsAsync(TimeSpan claimTimeout, DateTime utcNow, CancellationToken cancellationToken = default);
    Task MarkSentAsync(Guid outboxMessageId, DateTime utcNow, string? providerMessageId, CancellationToken cancellationToken = default);
    Task MarkFailedAsync(Guid outboxMessageId, string? errorCategory, string? errorMessage, DateTime utcNow, CancellationToken cancellationToken = default);
    Task RetryAsync(Guid outboxMessageId, DateTime nextAttemptAt, string? errorMessage, CancellationToken cancellationToken = default);
    Task CancelAsync(Guid outboxMessageId, string? errorMessage, CancellationToken cancellationToken = default);
}

public interface IEmailOutboxProcessor
{
    Task<int> ProcessPendingBatchAsync(int batchSize, CancellationToken cancellationToken = default);
}

public sealed class EmailOutboxService(
    AppDbContext dbContext,
    IEmailDeliveryLogService emailDeliveryLogService) : IEmailOutboxService
{
    public async Task<EmailOutboxMessage> QueueEmailAsync(QueueEmailRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var message = new EmailOutboxMessage
        {
            TenantId = request.TenantId,
            EventKey = request.EventKey.Trim(),
            TemplateKey = request.TemplateKey.Trim(),
            RecipientEmail = request.RecipientEmail.Trim().ToLowerInvariant(),
            RecipientName = Normalize(request.RecipientName),
            SenderName = Normalize(request.SenderName),
            SenderEmail = Normalize(request.SenderEmail),
            ReplyToEmail = Normalize(request.ReplyToEmail),
            Subject = request.Subject.Trim(),
            HtmlBody = Normalize(request.HtmlBody),
            TextBody = Normalize(request.TextBody),
            Status = EmailOutboxStatuses.Pending,
            AttemptCount = 0,
            MaxAttempts = request.MaxAttempts <= 0 ? 3 : request.MaxAttempts,
            TriggeredByUserId = request.TriggeredByUserId,
            NextAttemptAt = now
        };

        dbContext.EmailOutboxMessages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);

        await emailDeliveryLogService.LogAsync(
            new EmailDeliveryLogWriteRequest(
                request.TenantId,
                message.EventKey,
                message.TemplateKey,
                message.RecipientEmail,
                message.Subject,
                EmailOutboxStatuses.Pending,
                null,
                "Email queued for background delivery.",
                request.TriggeredByUserId,
                OutboxMessageId: message.Id,
                AttemptCount: 0,
                NextAttemptAt: message.NextAttemptAt),
            cancellationToken);

        return message;
    }

    public async Task<IReadOnlyCollection<EmailOutboxMessage>> GetPendingBatchAsync(int batchSize, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        return await dbContext.EmailOutboxMessages
            .Where(x => x.Status == EmailOutboxStatuses.Pending && (!x.NextAttemptAt.HasValue || x.NextAttemptAt <= utcNow))
            .OrderBy(x => x.CreatedAt)
            .Take(Math.Max(1, batchSize))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> MarkSendingAsync(Guid outboxMessageId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var updated = await dbContext.EmailOutboxMessages
            .Where(x => x.Id == outboxMessageId && x.Status == EmailOutboxStatuses.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, EmailOutboxStatuses.Sending)
                .SetProperty(x => x.LastAttemptAt, utcNow)
                .SetProperty(x => x.NextAttemptAt, (DateTime?)null)
                .SetProperty(x => x.UpdatedAt, utcNow),
                cancellationToken);

        if (updated > 0)
        {
            await emailDeliveryLogService.UpdateOutboxStatusAsync(
                new UpdateOutboxStatusRequest(
                    outboxMessageId,
                    EmailOutboxStatuses.Sending,
                    null,
                    null,
                    await dbContext.EmailOutboxMessages.Where(x => x.Id == outboxMessageId).Select(x => x.AttemptCount).SingleAsync(cancellationToken),
                    utcNow,
                    null,
                    null),
                cancellationToken);
        }

        return updated > 0;
    }

    public async Task<int> ReleaseAbandonedClaimsAsync(TimeSpan claimTimeout, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var cutoff = utcNow - claimTimeout;
        var abandoned = await dbContext.EmailOutboxMessages
            .Where(x => x.Status == EmailOutboxStatuses.Sending
                && (!x.LastAttemptAt.HasValue || x.LastAttemptAt < cutoff))
            .ToListAsync(cancellationToken);

        if (abandoned.Count == 0)
        {
            return 0;
        }

        foreach (var message in abandoned)
        {
            // Same ladder MarkFailedAsync uses: count the attempt, retire the message once it has
            // used up MaxAttempts, otherwise put it back with the normal backoff.
            message.AttemptCount += 1;
            message.ErrorCategory = "Abandoned";
            message.ErrorMessage = "Delivery was claimed but never completed. The sending instance stopped before it finished.";

            if (message.AttemptCount >= message.MaxAttempts)
            {
                message.Status = EmailOutboxStatuses.Failed;
                message.NextAttemptAt = null;
            }
            else
            {
                message.Status = EmailOutboxStatuses.Pending;
                message.NextAttemptAt = GetNextAttemptAt(message.AttemptCount, utcNow);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var message in abandoned)
        {
            await emailDeliveryLogService.UpdateOutboxStatusAsync(
                new UpdateOutboxStatusRequest(
                    message.Id,
                    message.Status == EmailOutboxStatuses.Failed ? EmailOutboxStatuses.Failed : "Retrying",
                    message.ErrorCategory,
                    message.ErrorMessage,
                    message.AttemptCount,
                    message.LastAttemptAt,
                    message.NextAttemptAt,
                    null),
                cancellationToken);
        }

        return abandoned.Count;
    }

    public async Task MarkSentAsync(Guid outboxMessageId, DateTime utcNow, string? providerMessageId, CancellationToken cancellationToken = default)
    {
        var message = await dbContext.EmailOutboxMessages.SingleAsync(x => x.Id == outboxMessageId, cancellationToken);
        message.Status = EmailOutboxStatuses.Sent;
        message.SentAt = utcNow;
        message.LastAttemptAt = utcNow;
        message.NextAttemptAt = null;
        message.AttemptCount += 1;
        message.ErrorCategory = null;
        message.ErrorMessage = null;
        await dbContext.SaveChangesAsync(cancellationToken);

        await emailDeliveryLogService.UpdateOutboxStatusAsync(
            new UpdateOutboxStatusRequest(
                outboxMessageId,
                EmailOutboxStatuses.Sent,
                null,
                null,
                message.AttemptCount,
                message.LastAttemptAt,
                null,
                message.SentAt,
                providerMessageId),
            cancellationToken);
    }

    public async Task MarkFailedAsync(Guid outboxMessageId, string? errorCategory, string? errorMessage, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var message = await dbContext.EmailOutboxMessages.SingleAsync(x => x.Id == outboxMessageId, cancellationToken);
        message.AttemptCount += 1;
        message.LastAttemptAt = utcNow;
        message.ErrorCategory = Normalize(errorCategory);
        message.ErrorMessage = Normalize(errorMessage);

        if (message.AttemptCount >= message.MaxAttempts)
        {
            message.Status = EmailOutboxStatuses.Failed;
            message.NextAttemptAt = null;
        }
        else
        {
            message.Status = EmailOutboxStatuses.Pending;
            message.NextAttemptAt = GetNextAttemptAt(message.AttemptCount, utcNow);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await emailDeliveryLogService.UpdateOutboxStatusAsync(
            new UpdateOutboxStatusRequest(
                outboxMessageId,
                message.Status == EmailOutboxStatuses.Failed ? EmailOutboxStatuses.Failed : "Retrying",
                errorCategory,
                message.ErrorMessage,
                message.AttemptCount,
                message.LastAttemptAt,
                message.NextAttemptAt,
                null),
            cancellationToken);
    }

    public async Task RetryAsync(Guid outboxMessageId, DateTime nextAttemptAt, string? errorMessage, CancellationToken cancellationToken = default)
    {
        var message = await dbContext.EmailOutboxMessages.SingleAsync(x => x.Id == outboxMessageId, cancellationToken);
        message.Status = EmailOutboxStatuses.Pending;
        message.NextAttemptAt = nextAttemptAt;
        message.ErrorCategory = null;
        message.ErrorMessage = Normalize(errorMessage);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelAsync(Guid outboxMessageId, string? errorMessage, CancellationToken cancellationToken = default)
    {
        var message = await dbContext.EmailOutboxMessages.SingleAsync(x => x.Id == outboxMessageId, cancellationToken);
        message.Status = EmailOutboxStatuses.Cancelled;
        message.NextAttemptAt = null;
        message.ErrorCategory = null;
        message.ErrorMessage = Normalize(errorMessage);
        await dbContext.SaveChangesAsync(cancellationToken);

        await emailDeliveryLogService.UpdateOutboxStatusAsync(
            new UpdateOutboxStatusRequest(
                outboxMessageId,
                EmailOutboxStatuses.Cancelled,
                null,
                message.ErrorMessage,
                message.AttemptCount,
                message.LastAttemptAt,
                null,
                null),
            cancellationToken);
    }

    private static DateTime GetNextAttemptAt(int attemptCount, DateTime utcNow) => attemptCount switch
    {
        1 => utcNow.AddMinutes(2),
        2 => utcNow.AddMinutes(10),
        _ => utcNow.AddMinutes(10),
    };

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class EmailOutboxProcessor(
    IEmailOutboxService emailOutboxService,
    IEmailSender emailSender,
    ISecretEncryptionService secretEncryptionService,
    IEmailSettingsResolver emailSettingsResolver,
    IOptions<SmtpOptions> smtpOptions,
    ILogger<EmailOutboxProcessor> logger) : IEmailOutboxProcessor
{
    /// <summary>
    /// How long a <c>Sending</c> claim may stand before it is treated as abandoned. Comfortably
    /// beyond the 30s SMTP timeout a single send can take, short enough that a replica restart costs
    /// one cycle rather than a stranded message.
    /// </summary>
    private static readonly TimeSpan ClaimTimeout = TimeSpan.FromMinutes(15);

    public async Task<int> ProcessPendingBatchAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var released = await emailOutboxService.ReleaseAbandonedClaimsAsync(ClaimTimeout, now, cancellationToken);
        if (released > 0)
        {
            logger.LogWarning("Returned {ReleasedCount} abandoned email outbox message(s) from Sending to the queue.", released);
        }

        var pending = await emailOutboxService.GetPendingBatchAsync(batchSize, now, cancellationToken);
        var processed = 0;

        foreach (var message in pending)
        {
            try
            {
                var claimed = await emailOutboxService.MarkSendingAsync(message.Id, now, cancellationToken);
                if (!claimed)
                {
                    continue;
                }

                // A row that exists and says "off" is an operator decision and is honoured. A missing
                // row is not a decision at all, so it falls through to the configuration fallback
                // inside CreateDeliveryOptions rather than failing the message outright.
                var emailSettings = await ResolveEffectiveEmailSettingsAsync(message.TenantId, cancellationToken);
                if (EmailDeliveryResolution.IsExplicitlyDisabled(emailSettings))
                {
                    await emailOutboxService.MarkFailedAsync(
                        message.Id,
                        "Disabled",
                        "Email delivery is disabled for this sender scope.",
                        DateTime.UtcNow,
                        cancellationToken);
                    continue;
                }

                var delivery = CreateDeliveryOptions(emailSettings);
                if (delivery is null)
                {
                    await emailOutboxService.MarkFailedAsync(
                        message.Id,
                        "Not configured",
                        "Email delivery could not start because neither the email settings nor the Smtp configuration section supply an SMTP host.",
                        DateTime.UtcNow,
                        cancellationToken);
                    continue;
                }

                var (cleanBody, attachments) = ExtractAttachments(
                    string.IsNullOrWhiteSpace(message.HtmlBody) ? message.TextBody ?? string.Empty : message.HtmlBody);

                await emailSender.SendAsync(
                    new EmailMessage(
                        message.RecipientEmail,
                        message.Subject,
                        cleanBody,
                        message.SenderName ?? delivery.SenderName,
                        message.SenderEmail ?? delivery.SenderEmail,
                        message.ReplyToEmail ?? delivery.ReplyToEmail,
                        IsHtml: !string.IsNullOrWhiteSpace(message.HtmlBody),
                        Attachments: attachments.Count > 0 ? attachments : null),
                    delivery,
                    cancellationToken);

                await emailOutboxService.MarkSentAsync(message.Id, DateTime.UtcNow, null, cancellationToken);
                processed += 1;
            }
            catch (Exception ex)
            {
                var errorCategory = ex is EmailDeliveryException deliveryException
                    ? deliveryException.ErrorCategory
                    : EmailErrorCategories.UnknownSmtpError;

                logger.LogWarning(ex, "Email outbox delivery failed for message {OutboxMessageId}.", message.Id);
                await emailOutboxService.MarkFailedAsync(message.Id, errorCategory, ex.Message, DateTime.UtcNow, cancellationToken);
            }
        }

        return processed;
    }

    private async Task<EmailSetting?> ResolveEffectiveEmailSettingsAsync(Guid? tenantId, CancellationToken cancellationToken) =>
        (await emailSettingsResolver.ResolveAsync(tenantId, cancellationToken)).Settings;

    // Parses <!-- attachment:filename:base64 --> comments injected by ScheduledReportService,
    // returns the cleaned body and any reconstructed attachments.
    private static (string CleanBody, List<EmailAttachment> Attachments) ExtractAttachments(string body)
    {
        var attachments = new List<EmailAttachment>();
        if (string.IsNullOrEmpty(body))
            return (body, attachments);

        var pattern = new Regex(@"<!--\s*attachment:([^:]+):([A-Za-z0-9+/=]+)\s*-->", RegexOptions.Compiled);
        var cleanBody = pattern.Replace(body, match =>
        {
            var filename = match.Groups[1].Value.Trim();
            var base64 = match.Groups[2].Value.Trim();
            try
            {
                var bytes = Convert.FromBase64String(base64);
                var contentType = filename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                    ? "application/pdf"
                    : "application/octet-stream";
                attachments.Add(new EmailAttachment(filename, bytes, contentType));
            }
            catch
            {
                // Malformed base64 — skip silently
            }
            return string.Empty;
        });

        return (cleanBody.Trim(), attachments);
    }

    private EmailDeliverySettings? CreateDeliveryOptions(EmailSetting? settings) =>
        EmailDeliveryResolution.Resolve(
            settings,
            settings is null ? null : secretEncryptionService.Decrypt(settings.EncryptedSecret) ?? settings.Password,
            smtpOptions.Value);
}

public sealed class EmailOutboxWorker(IServiceScopeFactory serviceScopeFactory, ILogger<EmailOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Email outbox worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IEmailOutboxProcessor>();
                await processor.ProcessPendingBatchAsync(10, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Email outbox worker cycle failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
