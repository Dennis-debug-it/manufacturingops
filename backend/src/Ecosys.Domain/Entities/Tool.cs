using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// A tool or piece of test equipment tracked as one unit (unlike stock, which is counted): it is
/// checked out to a technician, comes back, and may need periodic calibration or inspection.
/// </summary>
public sealed class Tool : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    /// <summary>The asset tag; unique within the workspace.</summary>
    public string ToolCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? SerialNumber { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public decimal? PurchaseCost { get; set; }
    /// <summary>The store the tool belongs to and returns to.</summary>
    public Guid? HomeBranchId { get; set; }
    public string Status { get; set; } = ToolStatuses.InStore;
    public string Condition { get; set; } = ToolConditions.Good;
    /// <summary>Days between calibrations or inspections; null when the tool needs none.</summary>
    public int? CalibrationIntervalDays { get; set; }
    public DateTime? LastCalibratedOn { get; set; }
    public DateTime? NextCalibrationDue { get; set; }
    public string? Notes { get; set; }
    /// <summary>The open check-out while the tool is with a technician.</summary>
    public Guid? CurrentAssignmentId { get; set; }

    public Tenant? Tenant { get; set; }
    public Branch? HomeBranch { get; set; }
    public ToolAssignment? CurrentAssignment { get; set; }
    public ICollection<ToolAssignment> Assignments { get; set; } = new List<ToolAssignment>();
    public ICollection<ToolCalibration> Calibrations { get; set; } = new List<ToolCalibration>();
}

/// <summary>One spell of a tool with a technician, from check-out to return.</summary>
public sealed class ToolAssignment : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ToolId { get; set; }
    public Guid TechnicianId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public DateTime CheckedOutAt { get; set; }
    public Guid CheckedOutByUserId { get; set; }
    public DateTime? ExpectedReturnAt { get; set; }
    public string ConditionOut { get; set; } = ToolConditions.Good;
    public string? Notes { get; set; }
    /// <summary>When the technician confirmed they have the tool.</summary>
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public Guid? ReturnedByUserId { get; set; }
    public string? ConditionIn { get; set; }
    public string? ReturnNotes { get; set; }

    public Tenant? Tenant { get; set; }
    public Tool? Tool { get; set; }
    public Technician? Technician { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public User? CheckedOutByUser { get; set; }
    public User? ReturnedByUser { get; set; }
}

/// <summary>A recorded calibration or inspection: the evidence an auditor or client asks for.</summary>
public sealed class ToolCalibration : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ToolId { get; set; }
    public DateTime CalibratedOn { get; set; }
    public DateTime? NextDueOn { get; set; }
    public string? CertificateNumber { get; set; }
    public string? PerformedBy { get; set; }
    public string Result { get; set; } = "Pass";
    public string? Notes { get; set; }
    public Guid RecordedByUserId { get; set; }

    public Tenant? Tenant { get; set; }
    public Tool? Tool { get; set; }
}

public static class ToolStatuses
{
    public const string InStore = "InStore";
    public const string CheckedOut = "CheckedOut";
    public const string UnderRepair = "UnderRepair";
    public const string Lost = "Lost";
    public const string Retired = "Retired";
}

public static class ToolConditions
{
    public const string Good = "Good";
    public const string Fair = "Fair";
    public const string Damaged = "Damaged";

    public static readonly string[] All = [Good, Fair, Damaged];
}
