using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class PreventiveMaintenancePlan : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid AssetId { get; set; }
    public Guid? SiteId { get; set; }
    public Guid? PmTemplateId { get; set; }
    public Guid? DefaultAssignmentGroupId { get; set; }

    public string TriggerType { get; set; } = "Calendar";
    public string Frequency { get; set; } = string.Empty;
    public string FrequencyUnit { get; set; } = "Monthly";
    public int FrequencyInterval { get; set; } = 1;
    public int? PreferredDayOfWeek { get; set; }
    public int? PreferredDayOfMonth { get; set; }
    public decimal? MeterInterval { get; set; }
    public decimal? MeterBuffer { get; set; }
    public int DaysBeforeDue { get; set; } = 3;

    public bool AutoSchedule { get; set; }
    public bool AutoAssign { get; set; }
    public bool NotifyOnGeneration { get; set; }

    public DateTime? LastPmDate { get; set; }
    public DateTime? NextPmDate { get; set; }
    public DateTime? LastGeneratedAt { get; set; }
    public Guid? LastPmWorkOrderId { get; set; }

    public string Status { get; set; } = "Active";

    /// <summary>
    /// The client agreed to keep these visits going without a contract (paid per visit), so a
    /// lapsed contract doesn't put the schedule on hold.
    /// </summary>
    public bool ContinueWithoutContract { get; set; }

    /// <summary>Why the schedule is on hold (status ContractLapsed), and since when.</summary>
    public string? HoldReason { get; set; }
    public DateTime? HeldAt { get; set; }

    /// <summary>The contract whose lapse put the schedule on hold.</summary>
    public Guid? LapsedContractId { get; set; }

    // ── Factory PM (scope Phase 3). A factory plan has an OrgNodeId; ServiceOps plans don't. ──

    /// <summary>Where the work is (the asset's section); set on every factory plan.</summary>
    public Guid? OrgNodeId { get; set; }
    public string? Name { get; set; }
    /// <summary>Fixed: the next due date counts from the last due date. Floating: from when the work was done.</summary>
    public string ScheduleMode { get; set; } = PmScheduleModes.Floating;
    /// <summary>Done within ±this % of the interval around the due date counts as on time.</summary>
    public int TolerancePercent { get; set; } = 10;
    /// <summary>Meter that triggers the plan (run hours, kg made tea, …) when TriggerType is Meter.</summary>
    public Guid? MeterId { get; set; }
    public decimal? LastMeterValue { get; set; }
    public decimal? NextMeterDue { get; set; }
    public string Priority { get; set; } = "Medium";
    public decimal? EstimatedHours { get; set; }
    /// <summary>Jobs need the line stopped, so they go into the weekly window.</summary>
    public bool NeedsLineStop { get; set; }
    /// <summary>Why this interval: manufacturer, statutory, experience.</summary>
    public string? Basis { get; set; }
    /// <summary>A round over several assets (lubrication, inspection) done as one job.</summary>
    public bool IsRoute { get; set; }

    /// <summary>The last deferral: why, when, and whether it carries a risk (an A-class asset put off until low crop).</summary>
    public string? DeferReason { get; set; }
    public DateTime? DeferredAt { get; set; }
    public bool DeferRisk { get; set; }

    public Meter? Meter { get; set; }
    public OrgNode? OrgNode { get; set; }
    public ICollection<PmPlanRouteAsset> RouteAssets { get; set; } = new List<PmPlanRouteAsset>();
    public ICollection<PmPlanEvent> Events { get; set; } = new List<PmPlanEvent>();

    public Tenant? Tenant { get; set; }
    public Branch? Branch { get; set; }
    public Asset? Asset { get; set; }
    public Site? Site { get; set; }
    public PmTemplate? PmTemplate { get; set; }
    public AssignmentGroup? DefaultAssignmentGroup { get; set; }
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
}

public static class PmScheduleModes
{
    public const string Fixed = "Fixed";
    public const string Floating = "Floating";
    public static readonly IReadOnlyList<string> All = [Fixed, Floating];
}

public static class PmTriggerTypes
{
    public const string Calendar = "Calendar";
    public const string Meter = "Meter";
}

/// <summary>An asset on a route plan, in the order it is visited.</summary>
public sealed class PmPlanRouteAsset : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid PlanId { get; set; }
    public Guid AssetId { get; set; }
    public int Sequence { get; set; }

    public PreventiveMaintenancePlan? Plan { get; set; }
    public Asset? Asset { get; set; }
}

public static class PmPlanEventKinds
{
    public const string Generated = "Generated";
    public const string Completed = "Completed";
    public const string Skipped = "Skipped";
    public const string Deferred = "Deferred";
}

/// <summary>
/// What happened to each due PM: generated, done (on time or not), skipped or deferred. PM
/// compliance is counted from these.
/// </summary>
public sealed class PmPlanEvent : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid PlanId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public string Kind { get; set; } = PmPlanEventKinds.Generated;
    public DateTime? DueDate { get; set; }
    public decimal? DueMeterValue { get; set; }
    public DateTime OccurredAt { get; set; }
    /// <summary>For Completed: done inside the tolerance window.</summary>
    public bool? OnTime { get; set; }
    public string? Reason { get; set; }
    public string? ActorName { get; set; }

    public PreventiveMaintenancePlan? Plan { get; set; }
}
