using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Contracts.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services;

/// <summary>
/// One notification to one or more users of a tenant (Release 3 design 2.1).
/// </summary>
/// <param name="Key">A <see cref="NotificationKeyDescriptor"/> key; it picks the preferences and the email template.</param>
/// <param name="Title">The bell and push title (max 200).</param>
/// <param name="Body">Plain text for the bell and push (max 1000).</param>
/// <param name="Url">In-app link, e.g. <c>/projects/{id}?tab=valuations</c>.</param>
/// <param name="TemplateValues">Email template placeholders. RecipientName is added for each recipient.</param>
/// <param name="Severity">Info, Warning or Critical.</param>
/// <param name="Channels">
/// Limits the channels for this one send, for a caller that delivers a channel itself (the work-order
/// assignment emails the technician record, not the user). Preferences still apply within it.
/// </param>
/// <param name="Attachments">Files for the email only, e.g. published meeting minutes as a PDF.</param>
public sealed record NotificationRequest(
    Guid TenantId,
    string Key,
    IReadOnlyCollection<Guid> UserIds,
    string Title,
    string Body,
    string? Url,
    string? EntityType,
    Guid? EntityId,
    IReadOnlyDictionary<string, string?> TemplateValues,
    string Severity = NotificationSeverities.Info,
    NotificationChannels Channels = NotificationKeyDescriptor.AnyChannel,
    IReadOnlyCollection<EmailAttachment>? Attachments = null);

/// <summary>
/// The single way to notify tenant users. For each recipient it applies their preference for the key
/// (a missing row means the key's <see cref="NotificationDescriptor.DefaultChannels"/>), then writes
/// the bell row and tells their open tabs, marks the row for push, queues the email through the outbox,
/// and hands any further channel (WhatsApp, step 3d) to its <see cref="INotificationChannel"/>.
/// Failures are logged and swallowed: a notification must never undo the change that triggered it.
/// </summary>
public interface INotificationDispatcher
{
    Task NotifyAsync(NotificationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>What an <see cref="INotificationChannel"/> gets for one recipient.</summary>
/// <param name="NotificationId">The bell row, when one was written (null if the user turned the bell and push off).</param>
/// <param name="TemplateValues">The request's values plus RecipientName.</param>
public sealed record NotificationDelivery(
    NotificationRequest Request,
    Guid UserId,
    string FullName,
    string? Email,
    string? Phone,
    Guid? NotificationId,
    IReadOnlyDictionary<string, string?> TemplateValues);

/// <summary>
/// A delivery channel beyond the bell, push and email, which the dispatcher handles itself. Step 3d
/// adds WhatsApp by registering one (<c>services.AddScoped&lt;INotificationChannel, WhatsAppNotificationChannel&gt;()</c>)
/// and adding <see cref="NotificationChannels.WhatsApp"/> to the keys' <see cref="NotificationDescriptor.Channels"/>.
/// <see cref="SendAsync"/> is called once per recipient whose resolved channels include <see cref="Channel"/>;
/// opt-in, consent and number checks belong to the channel. An exception is logged and skipped.
/// </summary>
public interface INotificationChannel
{
    NotificationChannels Channel { get; }

    Task SendAsync(NotificationDelivery delivery, CancellationToken cancellationToken = default);
}

public static class NotificationPreferences
{
    /// <summary>The channels a user has on for a key: their saved row, or the key's defaults.</summary>
    public static NotificationChannels Resolve(NotificationDescriptor? descriptor, UserNotificationPreference? preference)
    {
        if (preference is null)
            return descriptor?.DefaultChannels ?? NotificationKeyDescriptor.Staff;

        var channels = NotificationChannels.None;
        if (preference.InAppEnabled) channels |= NotificationChannels.InApp;
        if (preference.PushEnabled) channels |= NotificationChannels.Push;
        if (preference.EmailEnabled) channels |= NotificationChannels.Email;
        if (preference.WhatsAppEnabled) channels |= NotificationChannels.WhatsApp;
        return channels;
    }
}

public static class InboxNotifications
{
    public const int TitleMaxLength = 200;
    public const int BodyMaxLength = 1000;
    public const int UrlMaxLength = 500;

    public static InboxNotification ToInbox(this Notification row) =>
        new(row.Id, row.Key, row.Title, row.Body, row.Url, row.EntityType, row.EntityId, row.Severity, row.CreatedAt, row.ReadAt);

    /// <summary>What the bell counts: not read and not archived.</summary>
    public static IQueryable<Notification> Unread(this IQueryable<Notification> query) =>
        query.Where(x => x.ReadAt == null && x.ArchivedAt == null);

    /// <summary>The service worker replaces an earlier push with the same tag, so one per record.</summary>
    public static string PushTag(Notification row) =>
        row.EntityId.HasValue ? $"{row.Key}-{row.EntityId}" : $"notification-{row.Id}";

    public static string Clip(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..(maxLength - 1)] + "…";
    }
}

internal sealed class NotificationDispatcher(
    AppDbContext dbContext,
    IEmailTemplateService emailTemplateService,
    IEmailSubjectRuleService emailSubjectRuleService,
    IEmailOutboxService emailOutboxService,
    IInboxNotifier inboxNotifier,
    IEnumerable<INotificationChannel> extraChannels,
    ILogger<NotificationDispatcher> logger) : INotificationDispatcher
{
    public async Task NotifyAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        var userIds = request.UserIds.Distinct().ToList();
        if (userIds.Count == 0)
            return;

        List<(Recipient Recipient, NotificationChannels Channels)> plans;
        try
        {
            var descriptor = NotificationKeyDescriptor.Find(request.Key);
            var allowed = (descriptor?.Channels ?? NotificationKeyDescriptor.Staff) & request.Channels;
            var recipients = await dbContext.Users.IgnoreQueryFilters()
                .Where(x => x.TenantId == request.TenantId && x.IsActive && userIds.Contains(x.Id))
                .Select(x => new Recipient(x.Id, x.FullName, x.Email, x.Phone))
                .ToListAsync(cancellationToken);
            var preferences = await dbContext.UserNotificationPreferences
                .Where(x => x.TenantId == request.TenantId && userIds.Contains(x.UserId) && x.NotificationKey == request.Key)
                .ToDictionaryAsync(x => x.UserId, cancellationToken);
            plans = recipients
                .Select(r => (r, NotificationPreferences.Resolve(descriptor, preferences.GetValueOrDefault(r.Id)) & allowed))
                .Where(x => x.Item2 != NotificationChannels.None)
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to resolve recipients for {Key} notification.", request.Key);
            return;
        }

        var rows = await WriteInboxAsync(request, plans, cancellationToken);
        foreach (var (recipient, channels) in plans)
        {
            var values = new Dictionary<string, string?>(request.TemplateValues) { ["RecipientName"] = recipient.FullName };
            if (channels.HasFlag(NotificationChannels.Email))
                await QueueEmailAsync(request, recipient, values, cancellationToken);

            foreach (var channel in extraChannels.Where(c => channels.HasFlag(c.Channel)))
            {
                try
                {
                    await channel.SendAsync(new NotificationDelivery(request, recipient.Id, recipient.FullName, recipient.Email, recipient.Phone,
                        rows.GetValueOrDefault(recipient.Id)?.Id, values), cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "{Channel} delivery of {Key} failed for user {UserId}.", channel.Channel, request.Key, recipient.Id);
                }
            }
        }
    }

    /// <summary>
    /// One row per recipient who wants the bell or push. A push-only row is written already read and
    /// archived: it exists so the push worker can deliver it, and never shows in the bell.
    /// </summary>
    private async Task<Dictionary<Guid, Notification>> WriteInboxAsync(
        NotificationRequest request, IReadOnlyList<(Recipient Recipient, NotificationChannels Channels)> plans, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var rows = new Dictionary<Guid, Notification>();
        foreach (var (recipient, channels) in plans)
        {
            var inApp = channels.HasFlag(NotificationChannels.InApp);
            var push = channels.HasFlag(NotificationChannels.Push);
            if (!inApp && !push)
                continue;

            rows[recipient.Id] = new Notification
            {
                TenantId = request.TenantId,
                UserId = recipient.Id,
                Key = request.Key,
                Title = InboxNotifications.Clip(request.Title, InboxNotifications.TitleMaxLength),
                Body = InboxNotifications.Clip(request.Body, InboxNotifications.BodyMaxLength),
                Url = request.Url is { Length: > 0 and <= InboxNotifications.UrlMaxLength } ? request.Url : null,
                EntityType = request.EntityType,
                EntityId = request.EntityId,
                Severity = NotificationSeverities.All.Contains(request.Severity) ? request.Severity : NotificationSeverities.Info,
                CreatedAt = now,
                ReadAt = inApp ? null : now,
                ArchivedAt = inApp ? null : now,
                PushPendingSince = push ? now : null,
            };
        }

        if (rows.Count == 0)
            return rows;

        try
        {
            dbContext.Notifications.AddRange(rows.Values);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to write {Key} notifications.", request.Key);
            return [];
        }

        try
        {
            var visible = rows.Values.Where(x => x.ArchivedAt == null).ToList();
            var visibleUserIds = visible.Select(x => x.UserId).ToList();
            var unread = await dbContext.Notifications.IgnoreQueryFilters().Unread()
                .Where(x => x.TenantId == request.TenantId && visibleUserIds.Contains(x.UserId))
                .GroupBy(x => x.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count, cancellationToken);
            foreach (var row in visible)
            {
                await inboxNotifier.NotificationCreatedAsync(row.UserId, row.ToInbox(), cancellationToken);
                await inboxNotifier.UnreadCountChangedAsync(row.UserId, unread.GetValueOrDefault(row.UserId), cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The row is saved; an open tab that missed the event catches up on its next fetch.
            logger.LogWarning(ex, "Failed to publish live {Key} notifications.", request.Key);
        }

        return rows;
    }

    private async Task QueueEmailAsync(NotificationRequest request, Recipient recipient, IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(recipient.Email))
            return;

        try
        {
            var template = await emailTemplateService.RenderTenantTemplateAsync(request.TenantId, request.Key, values, cancellationToken);
            if (!template.Enabled)
                return;
            var subject = await emailSubjectRuleService.BuildFinalSubjectAsync(request.TenantId, request.Key, template.Subject, cancellationToken: cancellationToken);
            await emailOutboxService.QueueEmailAsync(new QueueEmailRequest(
                request.TenantId, request.Key, request.Key, recipient.Email, recipient.FullName, template.SenderNameOverride,
                null, null, subject, template.HtmlBody, template.TextBody, null, Attachments: request.Attachments), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to queue {Key} email for user {UserId}.", request.Key, recipient.Id);
        }
    }

    private sealed record Recipient(Guid Id, string FullName, string? Email, string? Phone);
}

/// <summary>Used where no live channel is registered (tests, tools); the Api replaces it with SignalR.</summary>
internal sealed class NullInboxNotifier : IInboxNotifier
{
    public Task NotificationCreatedAsync(Guid userId, InboxNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task UnreadCountChangedAsync(Guid userId, int unreadCount, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
