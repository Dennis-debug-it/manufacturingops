using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// A physical count of one branch's stock (a stocktake). Opening it snapshots what the books expect;
/// posting it adjusts each counted part by counted − expected, so movements made while the count was
/// open are kept rather than overwritten.
/// </summary>
public sealed class StockCount : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string CountNumber { get; set; } = string.Empty;
    /// <summary>The branch counted; null in a workspace without branches, and for a van count.</summary>
    public Guid? BranchId { get; set; }
    /// <summary>The van counted, when the count is of a van's stock rather than a branch's.</summary>
    /// <summary>
    /// A blind count: counters do not see the book quantity while counting, so they count what is
    /// there rather than confirm what the system says. The expected figures show once it is posted.
    /// </summary>
    public bool IsBlind { get; set; }
    /// <summary>Limits the count to one category of parts; null counts every active part.</summary>
    public string? Category { get; set; }
    public string Status { get; set; } = StockCountStatuses.Open;
    public string? Notes { get; set; }
    public Guid StartedByUserId { get; set; }
    public Guid? PostedByUserId { get; set; }
    public DateTime? PostedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    public Tenant? Tenant { get; set; }
    public Branch? Branch { get; set; }
    public User? StartedByUser { get; set; }
    public User? PostedByUser { get; set; }
    public ICollection<StockCountLine> Lines { get; set; } = new List<StockCountLine>();
}

public sealed class StockCountLine : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid StockCountId { get; set; }
    public Guid MaterialItemId { get; set; }
    /// <summary>The book balance when the count was opened.</summary>
    public decimal ExpectedQuantity { get; set; }
    /// <summary>What was found on the shelf; null until counted. Uncounted lines are left alone on posting.</summary>
    public decimal? CountedQuantity { get; set; }
    public decimal? UnitCost { get; set; }
    public string? Notes { get; set; }
    /// <summary>Marked for a second count (a large or surprising variance); cleared when recounted.</summary>
    public bool NeedsRecount { get; set; }
    public Guid? CountedByUserId { get; set; }
    public DateTime? CountedAt { get; set; }

    public Tenant? Tenant { get; set; }
    public StockCount? StockCount { get; set; }
    public MaterialItem? MaterialItem { get; set; }
}

public static class StockCountStatuses
{
    public const string Open = "Open";
    public const string Posted = "Posted";
    public const string Cancelled = "Cancelled";
}
