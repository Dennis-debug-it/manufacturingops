using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services;

/// <summary>
/// The dispatcher's WhatsApp channel for staff (design 2.1/4.2). The dispatcher calls it only when the key
/// lists WhatsApp and the user turned WhatsApp on for it; here the user must also be opted in on a
/// confirmed number. The outbox then applies the tenant flag, allowed keys, STOP list and cap.
/// </summary>
internal sealed class WhatsAppNotificationChannel(AppDbContext dbContext, IWhatsAppOutbox outbox, ILogger<WhatsAppNotificationChannel> logger) : INotificationChannel
{
    public NotificationChannels Channel => NotificationChannels.WhatsApp;

    public async Task SendAsync(NotificationDelivery delivery, CancellationToken cancellationToken = default)
    {
        var tenantId = delivery.Request.TenantId;
        var user = await dbContext.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Id == delivery.UserId && x.IsActive)
            .Select(x => new { x.WhatsAppNumber, x.WhatsAppVerifiedAt, x.WhatsAppOptedInAt })
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null || string.IsNullOrWhiteSpace(user.WhatsAppNumber) || user.WhatsAppVerifiedAt is null || user.WhatsAppOptedInAt is null)
            return;

        var result = await outbox.QueueAsync(new WhatsAppQueueRequest(
            tenantId, delivery.Request.Key, user.WhatsAppNumber, delivery.FullName, delivery.TemplateValues,
            RecipientUserId: delivery.UserId,
            EntityType: delivery.Request.EntityType,
            EntityId: delivery.Request.EntityId,
            IdempotencyKey: delivery.NotificationId is { } id ? $"notification:{id:N}" : null), cancellationToken);
        if (result.Outcome is not (WhatsAppQueueOutcome.Queued or WhatsAppQueueOutcome.Duplicate))
            logger.LogDebug("WhatsApp {Key} for user {UserId} not queued: {Outcome}.", delivery.Request.Key, delivery.UserId, result.Outcome);
    }
}
