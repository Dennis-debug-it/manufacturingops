namespace Ecosys.Platform.Contracts.Overview;

public interface IPlatformOverviewService
{
    Task<PlatformOverviewResponse> GetAsync(CancellationToken cancellationToken = default);
}
