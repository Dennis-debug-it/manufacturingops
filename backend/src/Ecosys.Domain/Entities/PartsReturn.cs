using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// Parts a technician sends back from a work order. Raised from the job (Pending), then received or
/// rejected by stores. Receiving is what moves stock and corrects the job's usage — see
/// <see cref="PartsReturnConditions"/> for what each line condition does.
/// </summary>
public sealed class PartsReturn : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid WorkOrderId { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public string Status { get; set; } = PartsReturnStatuses.Pending;
    public Guid RequestedByUserId { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public Guid? ProcessedByUserId { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? Notes { get; set; }
    public string? RejectionReason { get; set; }

    public Tenant? Tenant { get; set; }
    public Branch? Branch { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public User? RequestedByUser { get; set; }
    public User? ProcessedByUser { get; set; }
    public ICollection<PartsReturnLine> Lines { get; set; } = new List<PartsReturnLine>();
}

public sealed class PartsReturnLine : AuditableEntity
{
    public Guid PartsReturnId { get; set; }
    public Guid MaterialItemId { get; set; }
    /// <summary>The usage row the part was booked on. Required for Unused/Faulty; null for a Removed part.</summary>
    public Guid? WorkOrderMaterialUsageId { get; set; }
    public decimal Quantity { get; set; }
    public string Condition { get; set; } = PartsReturnConditions.Unused;
    public string? Notes { get; set; }

    public PartsReturn? PartsReturn { get; set; }
    public MaterialItem? MaterialItem { get; set; }
}

public static class PartsReturnStatuses
{
    public const string Pending = "Pending";
    public const string Received = "Received";
    public const string Rejected = "Rejected";
}

public static class PartsReturnConditions
{
    /// <summary>Taken to site but not fitted: goes back on the shelf and comes off the job's usage (not billed).</summary>
    public const string Unused = "Unused";
    /// <summary>Supplied part was defective: comes off the job's usage but is not restocked (held for supplier return/write-off).</summary>
    public const string Faulty = "Faulty";
    /// <summary>Old part taken out of the client's equipment: recorded for traceability; no stock or usage change.</summary>
    public const string Removed = "Removed";

    public static readonly string[] All = [Unused, Faulty, Removed];
}
