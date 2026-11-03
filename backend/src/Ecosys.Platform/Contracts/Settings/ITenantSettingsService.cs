namespace Ecosys.Platform.Contracts.Settings;

public interface ITenantSettingsService
{
    Task<TenantSettingsResponse> GetAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<TenantSettingsResponse> UpsertAsync(Guid tenantId, TenantSettingsRequest request, CancellationToken cancellationToken = default);
}
