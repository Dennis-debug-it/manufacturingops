namespace Ecosys.Platform.Contracts.Numbering;

public interface INumberSequenceService
{
    Task<string> GenerateAsync(Guid tenantId, string entityType, CancellationToken cancellationToken = default);
}
