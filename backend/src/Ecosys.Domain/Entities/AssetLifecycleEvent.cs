using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public static class AssetCriticality
{
    public const string A = "A";
    public const string B = "B";
    public const string C = "C";

    public static readonly IReadOnlyList<string> All = [A, B, C];

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = All.FirstOrDefault(x => string.Equals(x, value.Trim(), StringComparison.OrdinalIgnoreCase));
        return match ?? throw new ArgumentException("Criticality must be A, B or C.");
    }
}

public static class AssetOperationalStatuses
{
    public const string Operational = "Operational";
    public const string Standby = "Standby";
    public const string Down = "Down";

    public static readonly IReadOnlyList<string> All = [Operational, Standby, Down];

    public static string? Find(string? value) =>
        All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

public static class AssetLifecycleEventTypes
{
    public const string Registered = "Registered";
    public const string Commissioned = "Commissioned";
    public const string Transferred = "Transferred";
    public const string ComponentAttached = "ComponentAttached";
    public const string ComponentDetached = "ComponentDetached";
    public const string StatusChanged = "StatusChanged";
    public const string Decommissioned = "Decommissioned";
    public const string Reactivated = "Reactivated";
    public const string Disposed = "Disposed";
}

/// <summary>
/// One entry in an asset's lifecycle history: commissioning, transfers between sections or
/// factories, component moves, decommissioning and disposal. Append-only.
/// </summary>
public sealed class AssetLifecycleEvent : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid AssetId { get; set; }
    public string EventType { get; set; } = AssetLifecycleEventTypes.Registered;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public Guid? FromOrgNodeId { get; set; }
    public Guid? ToOrgNodeId { get; set; }
    public Guid? FromParentAssetId { get; set; }
    public Guid? ToParentAssetId { get; set; }
    public string? FromValue { get; set; }
    public string? ToValue { get; set; }

    public string? Notes { get; set; }
    public Guid? PerformedByUserId { get; set; }
    public string? PerformedByName { get; set; }

    public Tenant? Tenant { get; set; }
    public Asset? Asset { get; set; }
}
