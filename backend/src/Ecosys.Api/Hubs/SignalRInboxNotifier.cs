using Ecosys.Shared.Contracts.Integration;
using Microsoft.AspNetCore.SignalR;

namespace Ecosys.Api.Hubs;

/// <summary>
/// Implements IInboxNotifier (declared in Ecosys.Shared) using SignalR. Each connection joins its
/// user's group in <see cref="ServiceOpsHub.OnConnectedAsync"/>, so every open tab gets the event.
/// </summary>
internal sealed class SignalRInboxNotifier(IHubContext<ServiceOpsHub> hubContext) : IInboxNotifier
{
    public Task NotificationCreatedAsync(Guid userId, InboxNotification notification, CancellationToken cancellationToken = default) =>
        hubContext.Clients.Group(ServiceOpsHub.UserGroup(userId)).SendAsync("NotificationCreated", notification, cancellationToken);

    public Task UnreadCountChangedAsync(Guid userId, int unreadCount, CancellationToken cancellationToken = default) =>
        hubContext.Clients.Group(ServiceOpsHub.UserGroup(userId)).SendAsync("UnreadCountChanged", unreadCount, cancellationToken);
}
