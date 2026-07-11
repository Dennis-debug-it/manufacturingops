using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class StockMovement : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? VanId { get; set; }
    public Guid MaterialId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public Guid? MaterialRequestId { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? Reason { get; set; }
    public string? ReferenceNumber { get; set; }
    public Guid? CreatedByUserId { get; set; }

    public Tenant? Tenant { get; set; }
    public Branch? Branch { get; set; }
    public Van? Van { get; set; }
    public MaterialItem? Material { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public MaterialRequest? MaterialRequest { get; set; }
    public User? CreatedByUser { get; set; }
}
