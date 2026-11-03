namespace Ecosys.Platform.Contracts.Audit;

public interface IAuditService
{
    Task WriteAsync(Guid tenantId, Guid? userId, string category, string action, string entityType, Guid? entityId, string? details, CancellationToken cancellationToken = default);
}
