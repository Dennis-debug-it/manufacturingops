using Ecosys.Application.DTOs.Tenants;

namespace Ecosys.Application.Abstractions.Services;

public interface ITenantService
{
    Task<IReadOnlyCollection<TenantResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<TenantResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TenantResponse> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken = default);
    Task<TenantResponse?> UpdateAsync(Guid id, UpdateTenantRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
