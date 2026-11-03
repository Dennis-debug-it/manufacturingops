using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class StockTransfer : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid FromBranchId { get; set; }
    public Guid ToBranchId { get; set; }
    public Guid MaterialId { get; set; }
    public decimal Quantity { get; set; }
    public string Status { get; set; } = "Draft";
    public Guid RequestedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public string? Reason { get; set; }
    public string? ReferenceNumber { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Tenant? Tenant { get; set; }
    public Branch? FromBranch { get; set; }
    public Branch? ToBranch { get; set; }
    public MaterialItem? Material { get; set; }
    public User? RequestedByUser { get; set; }
    public User? ApprovedByUser { get; set; }
    public User? CompletedByUser { get; set; }
}
