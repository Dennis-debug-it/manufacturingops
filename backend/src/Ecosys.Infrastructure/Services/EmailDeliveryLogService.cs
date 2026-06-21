using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

public interface IEmailDeliveryLogService
{
    Task<EmailDeliveryLog> LogAsync(EmailDeliveryLogWriteRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<EmailDeliveryLog>> ListPlatformAsync(EmailDeliveryLogQuery query, CancellationToken cancellationToken = default);
    Task UpdateOutboxStatusAsync(UpdateOutboxStatusRequest request, CancellationToken cancellationToken = default);
}

public sealed record EmailDeliveryLogWriteRequest(
    Guid? TenantId,
    string EventKey,
    string TemplateKey,
    string RecipientEmail,
    string Subject,
    string Status,
    string? ErrorCategory,
    string? ErrorMessage,
    Guid? TriggeredByUserId,
    DateTime? SentAt = null,
    string? ProviderMessageId = null,
    Guid? OutboxMessageId = null,
    int? AttemptCount = null,
    DateTime? LastAttemptAt = null,
    DateTime? NextAttemptAt = null);

public sealed record EmailDeliveryLogQuery(
    string? Status,
    string? TemplateKey,
    string? EventKey,
    string? RecipientEmail,
    DateTime? DateFrom,
    DateTime? DateTo);

public sealed class EmailDeliveryLogService(AppDbContext dbContext) : IEmailDeliveryLogService
{
    public async Task<EmailDeliveryLog> LogAsync(EmailDeliveryLogWriteRequest request, CancellationToken cancellationToken = default)
    {
        var log = new EmailDeliveryLog
        {
            TenantId = request.TenantId,
            OutboxMessageId = request.OutboxMessageId,
            EventKey = request.EventKey.Trim(),
            TemplateKey = request.TemplateKey.Trim(),
            RecipientEmail = request.RecipientEmail.Trim().ToLowerInvariant(),
            Subject = request.Subject.Trim(),
            Status = request.Status.Trim(),
            AttemptCount = request.AttemptCount,
            LastAttemptAt = request.LastAttemptAt,
            NextAttemptAt = request.NextAttemptAt,
            ErrorCategory = Normalize(request.ErrorCategory),
            ErrorMessage = Normalize(request.ErrorMessage),
            TriggeredByUserId = request.TriggeredByUserId,
            SentAt = request.SentAt,
            ProviderMessageId = Normalize(request.ProviderMessageId),
        };

        dbContext.EmailDeliveryLogs.Add(log);
        await dbContext.SaveChangesAsync(cancellationToken);
        return log;
    }

    public async Task<IReadOnlyCollection<EmailDeliveryLog>> ListPlatformAsync(EmailDeliveryLogQuery query, CancellationToken cancellationToken = default)
    {
        var logs = dbContext.EmailDeliveryLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            logs = logs.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.TemplateKey))
        {
            var templateKey = query.TemplateKey.Trim();
            logs = logs.Where(x => x.TemplateKey == templateKey);
        }

        if (!string.IsNullOrWhiteSpace(query.EventKey))
        {
            var eventKey = query.EventKey.Trim();
            logs = logs.Where(x => x.EventKey == eventKey);
        }

        if (!string.IsNullOrWhiteSpace(query.RecipientEmail))
        {
            var recipient = query.RecipientEmail.Trim().ToLowerInvariant();
            logs = logs.Where(x => x.RecipientEmail.Contains(recipient) || x.Subject.ToLower().Contains(recipient));
        }

        if (query.DateFrom.HasValue)
        {
            logs = logs.Where(x => x.CreatedAt >= query.DateFrom.Value);
        }

        if (query.DateTo.HasValue)
        {
            var end = query.DateTo.Value;
            logs = logs.Where(x => x.CreatedAt <= end);
        }

        return await logs
            .OrderByDescending(x => x.CreatedAt)
            .Take(250)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateOutboxStatusAsync(UpdateOutboxStatusRequest request, CancellationToken cancellationToken = default)
    {
        var log = await dbContext.EmailDeliveryLogs
            .SingleOrDefaultAsync(x => x.OutboxMessageId == request.OutboxMessageId, cancellationToken);

        if (log is null)
        {
            return;
        }

        log.Status = request.Status.Trim();
        log.ErrorCategory = Normalize(request.ErrorCategory);
        log.ErrorMessage = Normalize(request.ErrorMessage);
        log.AttemptCount = request.AttemptCount;
        log.LastAttemptAt = request.LastAttemptAt;
        log.NextAttemptAt = request.NextAttemptAt;
        log.SentAt = request.SentAt;
        log.ProviderMessageId = Normalize(request.ProviderMessageId);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
