namespace Ecosys.Shared.Contracts.Integration;

/// <summary>One bell entry, as the API lists it and as the live <c>NotificationCreated</c> event carries it.</summary>
public sealed record InboxNotification(
    Guid Id,
    string Key,
    string Title,
    string Body,
    string? Url,
    string? EntityType,
    Guid? EntityId,
    string Severity,
    DateTime CreatedAt,
    DateTime? ReadAt);

/// <summary>
/// Live updates for a user's bell — implemented in Ecosys.Api via SignalR (group <c>user-{userId}</c>)
/// so that Infrastructure can push without referencing the Api layer, like <see cref="IServiceOpsNotifier"/>.
/// Delivery is best effort: a client that missed an event catches up from the API.
/// </summary>
public interface IInboxNotifier
{
    /// <summary>Raises <c>NotificationCreated</c> with the new entry.</summary>
    Task NotificationCreatedAsync(Guid userId, InboxNotification notification, CancellationToken cancellationToken = default);

    /// <summary>Raises <c>UnreadCountChanged</c> with the user's new unread count.</summary>
    Task UnreadCountChangedAsync(Guid userId, int unreadCount, CancellationToken cancellationToken = default);
}
