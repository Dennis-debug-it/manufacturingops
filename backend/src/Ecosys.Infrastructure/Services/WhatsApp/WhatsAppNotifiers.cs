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

/// <summary>
/// Client-facing WhatsApp messages (design 4.2, R6): technician on the way, job completed, invoice issued,
/// valuation certified. Goes only to the client's contacts marked to receive WhatsApp, with consent
/// recorded. Never throws: the action that triggered it must not fail because of it.
/// </summary>
public interface IClientWhatsAppNotifier
{
    /// <param name="onceScope">
    /// When set, each contact gets this message once for the scope (e.g. an invoice id), however often
    /// the hook runs. Leave null for events that can repeat (a technician on the way to a second visit).
    /// </param>
    Task NotifyClientAsync(Guid tenantId, Guid clientId, string key, IReadOnlyDictionary<string, string?> values,
        string? entityType, Guid? entityId, string? onceScope = null, CancellationToken cancellationToken = default);
}

internal sealed class ClientWhatsAppNotifier(AppDbContext dbContext, IWhatsAppOutbox outbox, ILogger<ClientWhatsAppNotifier> logger) : IClientWhatsAppNotifier
{
    public async Task NotifyClientAsync(Guid tenantId, Guid clientId, string key, IReadOnlyDictionary<string, string?> values,
        string? entityType, Guid? entityId, string? onceScope = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (clientId == Guid.Empty || WhatsAppTemplateCatalogue.Find(key)?.Audience != WhatsAppAudiences.Client)
                return;
            // Cheap checks first, so a tenant without WhatsApp costs one query.
            var policy = await WhatsAppTenantPolicy.LoadAsync(dbContext, tenantId, cancellationToken);
            if (!policy.Allows(key))
                return;

            var contacts = await dbContext.ClientPortalUsers.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.ClientId == clientId && x.IsActive && x.ReceivesWhatsApp
                    && x.WhatsAppNumber != null && x.WhatsAppConsentAt != null)
                .Select(x => new { x.Id, x.FullName, x.WhatsAppNumber })
                .ToListAsync(cancellationToken);
            foreach (var contact in contacts)
            {
                await outbox.QueueAsync(new WhatsAppQueueRequest(
                    tenantId, key, contact.WhatsAppNumber!, contact.FullName, values,
                    RecipientPortalUserId: contact.Id,
                    EntityType: entityType,
                    EntityId: entityId,
                    IdempotencyKey: onceScope is null ? null : $"{key}:{onceScope}:{contact.Id:N}"), cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to queue client WhatsApp {Key} for client {ClientId}.", key, clientId);
        }
    }
}

/// <summary>Where nothing is registered (tests, tools).</summary>
internal sealed class NullClientWhatsAppNotifier : IClientWhatsAppNotifier
{
    public Task NotifyClientAsync(Guid tenantId, Guid clientId, string key, IReadOnlyDictionary<string, string?> values,
        string? entityType, Guid? entityId, string? onceScope = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
