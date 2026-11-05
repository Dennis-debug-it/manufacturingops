using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public static class MaintenanceWindowStatuses
{
    /// <summary>Jobs are being lined up.</summary>
    public const string Planned = "Planned";
    /// <summary>The job pack is issued: jobs, technicians, spares and permits are set.</summary>
    public const string Packed = "Packed";
    /// <summary>The line is stopped and the work is under way.</summary>
    public const string InProgress = "InProgress";
    /// <summary>Started up and handed back to production; unfinished jobs moved on.</summary>
    public const string HandedBack = "HandedBack";
    /// <summary>No window this week (e.g. peak crop); its jobs moved to the next one.</summary>
    public const string Skipped = "Skipped";

    public static bool IsOpen(string status) => status is Planned or Packed or InProgress;
}

public static class WindowJobOutcomes
{
    public const string Done = "Done";
    public const string CarriedOver = "CarriedOver";
    public const string Cancelled = "Cancelled";
}

/// <summary>
/// One factory's weekly maintenance window (Monday by default, no leaf intake): the line-stop
/// jobs planned for it, the job pack, and the start-up hand-back to production.
/// </summary>
public sealed class MaintenanceWindow : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    /// <summary>The factory (org node at factory level).</summary>
    public Guid FactoryId { get; set; }
    /// <summary>The factory's local date of the window.</summary>
    public DateTime WindowDate { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string Status { get; set; } = MaintenanceWindowStatuses.Planned;
    /// <summary>Technician hours available; blank uses window length × active technicians.</summary>
    public decimal? CapacityHours { get; set; }
    public string? Notes { get; set; }
    /// <summary>The date it was originally planned for, when moved.</summary>
    public DateTime? MovedFromDate { get; set; }
    public string? SkipReason { get; set; }

    public DateTime? PackedAt { get; set; }
    public string? PackedByName { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? HandedBackAt { get; set; }
    public string? HandedBackByName { get; set; }
    public string? HandbackNotes { get; set; }
    public string? HandbackSignatureDataUrl { get; set; }

    public Tenant? Tenant { get; set; }
    public OrgNode? Factory { get; set; }
    public ICollection<MaintenanceWindowJob> Jobs { get; set; } = new List<MaintenanceWindowJob>();
    public ICollection<MaintenanceWindowHandbackItem> HandbackItems { get; set; } = new List<MaintenanceWindowHandbackItem>();
}

/// <summary>A job planned into a window.</summary>
public sealed class MaintenanceWindowJob : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid WindowId { get; set; }
    public Guid WorkOrderId { get; set; }
    /// <summary>Queued automatically (a line-stop job) rather than added by a planner.</summary>
    public bool AddedAutomatically { get; set; }
    /// <summary>Set at hand-back: Done, CarriedOver or Cancelled.</summary>
    public string? Outcome { get; set; }
    public int Sequence { get; set; }

    public MaintenanceWindow? Window { get; set; }
    public WorkOrder? WorkOrder { get; set; }
}

/// <summary>A start-up check before the line goes back to production (guards on, isolations removed, trial run).</summary>
public sealed class MaintenanceWindowHandbackItem : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid WindowId { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public bool Done { get; set; }
    public string? Note { get; set; }
    public string? DoneByName { get; set; }
    public DateTime? DoneAt { get; set; }

    public MaintenanceWindow? Window { get; set; }
}
