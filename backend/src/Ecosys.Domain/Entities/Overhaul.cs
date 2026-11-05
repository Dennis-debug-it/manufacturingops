using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public static class OverhaulPlanStatuses
{
    public const string Planning = "Planning";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
}

public static class OverhaulItemSources
{
    /// <summary>A job carried over from window to window too many times.</summary>
    public const string Carryover = "Carryover";
    /// <summary>A job or PM put off until the overhaul.</summary>
    public const string Deferred = "Deferred";
    /// <summary>Found on an inspection, check or job: wear to fix at the next shutdown.</summary>
    public const string Finding = "Finding";
    public const string Manual = "Manual";

    public static readonly IReadOnlyList<string> All = [Carryover, Deferred, Finding, Manual];
}

public static class OverhaulItemStatuses
{
    /// <summary>On the list, not yet in an overhaul's scope.</summary>
    public const string Listed = "Listed";
    /// <summary>In an overhaul's scope: costed and planned.</summary>
    public const string Scoped = "Scoped";
    public const string InProgress = "InProgress";
    public const string Done = "Done";
    /// <summary>Taken off the list (no longer needed).</summary>
    public const string Dropped = "Dropped";

    public static bool IsActive(string status) => status is Listed or Scoped or InProgress;
}

/// <summary>
/// The annual overhaul: the low-crop shutdown of one factory, with its scope, budget and progress.
/// </summary>
public sealed class OverhaulPlan : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid FactoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime PlannedStart { get; set; }
    public DateTime PlannedEnd { get; set; }
    public decimal? Budget { get; set; }
    public string Status { get; set; } = OverhaulPlanStatuses.Planning;
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualEnd { get; set; }
    public string? Notes { get; set; }

    public Tenant? Tenant { get; set; }
    public OrgNode? Factory { get; set; }
    public ICollection<OverhaulItem> Items { get; set; } = new List<OverhaulItem>();
}

/// <summary>
/// Work for the next overhaul, collected through the season. Once scoped into an overhaul it is
/// costed, given spares and a contractor, done as a job, and recommissioned.
/// </summary>
public sealed class OverhaulItem : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid FactoryId { get; set; }
    public Guid? OverhaulPlanId { get; set; }
    public Guid? AssetId { get; set; }
    /// <summary>The job it came from, or the job raised to do it.</summary>
    public Guid? WorkOrderId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Source { get; set; } = OverhaulItemSources.Manual;
    public string Priority { get; set; } = "Medium";
    public string Status { get; set; } = OverhaulItemStatuses.Listed;
    public decimal? EstimatedHours { get; set; }
    public decimal? EstimatedCost { get; set; }
    public decimal? ActualCost { get; set; }
    public string? SparesNote { get; set; }
    public string? ContractorName { get; set; }
    public int ProgressPercent { get; set; }
    /// <summary>Needs a recommissioning check (trial run, settings, guards) before production.</summary>
    public bool NeedsRecommissioning { get; set; } = true;
    public DateTime? RecommissionedAt { get; set; }
    public string? RecommissionedByName { get; set; }
    public string? RecommissioningNotes { get; set; }
    public string? AddedByName { get; set; }

    public Tenant? Tenant { get; set; }
    public OrgNode? Factory { get; set; }
    public OverhaulPlan? OverhaulPlan { get; set; }
    public Asset? Asset { get; set; }
    public WorkOrder? WorkOrder { get; set; }
}
