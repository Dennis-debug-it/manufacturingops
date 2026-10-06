using Ecosys.Infrastructure.Services;

namespace Ecosys.Tests;

/// <summary>Records queued service reports instead of rendering templates and writing to the outbox.</summary>
public sealed class FakeServiceReportDeliveryService : IServiceReportDeliveryService
{
    public List<Guid> QueuedWorkOrderIds { get; } = [];

    public Task<string> QueueAsync(Guid tenantId, Guid workOrderId, byte[] pdf, Guid? triggeredByUserId, CancellationToken cancellationToken = default)
    {
        QueuedWorkOrderIds.Add(workOrderId);
        return Task.FromResult("client@example.com");
    }
}
