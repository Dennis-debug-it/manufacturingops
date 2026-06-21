using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class WorkOrderMaterialUsage : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid MaterialItemId { get; set; }
    public Guid? AssetId { get; set; }
    public decimal QuantityUsed { get; set; }
    public decimal? UnitCost { get; set; }
    public bool Chargeable { get; set; }
    public string? Notes { get; set; }
    public Guid UsedByUserId { get; set; }
    public DateTime UsedAt { get; set; } = DateTime.UtcNow;

    public Tenant? Tenant { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public MaterialItem? MaterialItem { get; set; }
    public Asset? Asset { get; set; }
    public User? UsedByUser { get; set; }
}
