namespace Ecosys.Platform.Contracts.Tenants;

public interface ITenantManagementService
{
    Task<IReadOnlyCollection<TenantResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<TenantResponse> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken = default);
    Task<TenantResponse> ActivateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TenantResponse> SuspendAsync(Guid id, CancellationToken cancellationToken = default);
}
