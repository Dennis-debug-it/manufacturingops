using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// A faulty part stores is holding: received back from a job as Faulty, so it came off the job but
/// was not restocked. It stays Held until stores sends it back to the supplier, writes it off, or —
/// once repaired or found sound — puts it back into stock. Before this record a faulty part simply
/// disappeared on receipt, with nothing to say it was sitting in stores awaiting a supplier claim.
/// </summary>
public sealed class HeldPart : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid MaterialItemId { get; set; }
    /// <summary>The branch whose stores is holding it.</summary>
    public Guid? BranchId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    public string Status { get; set; } = HeldPartStatuses.Held;
    /// <summary>Where it came from, e.g. the parts return number.</summary>
    public string? SourceReference { get; set; }
    public Guid? PartsReturnId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public string? Notes { get; set; }
    /// <summary>The supplier it went back to, when returned.</summary>
    public Guid? SupplierId { get; set; }
    /// <summary>The supplier's RMA number, a write-off reference, or similar.</summary>
    public string? ResolutionReference { get; set; }
    public string? ResolutionNote { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public Tenant? Tenant { get; set; }
    public MaterialItem? MaterialItem { get; set; }
    public Branch? Branch { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public Supplier? Supplier { get; set; }
    public User? ResolvedByUser { get; set; }
}

public static class HeldPartStatuses
{
    public const string Held = "Held";
    public const string ReturnedToSupplier = "ReturnedToSupplier";
    public const string WrittenOff = "WrittenOff";
    public const string Restocked = "Restocked";
}
