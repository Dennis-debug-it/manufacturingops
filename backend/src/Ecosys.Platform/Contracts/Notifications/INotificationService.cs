namespace Ecosys.Platform.Contracts.Notifications;

public interface INotificationService
{
    Task QueueAsync(Guid tenantId, Guid? userId, string subject, string message, CancellationToken cancellationToken = default);
}
