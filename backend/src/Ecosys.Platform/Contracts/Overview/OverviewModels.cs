namespace Ecosys.Platform.Contracts.Overview;

public sealed class PlatformOverviewResponse
{
    public int TotalTenants { get; init; }
    public int ActiveTenants { get; init; }
    public int TotalUsers { get; init; }
    public int TotalWorkOrdersCount { get; init; }
    public required string SystemHealth { get; init; }
}
