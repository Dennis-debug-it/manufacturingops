using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class MaterialIssue : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public Guid? AssetId { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? SiteId { get; set; }
    public Guid? SystemId { get; set; }
    public Guid? IssuedToUserId { get; set; }
    public string IssueNumber { get; set; } = string.Empty;
    public string IssueStatus { get; set; } = "Draft";
    public string? Notes { get; set; }
    public Guid RequestedByUserId { get; set; }
    public Guid? IssuedByUserId { get; set; }
    public DateTime? IssuedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? RejectedByUserId { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
    public decimal TotalEstimatedCost { get; set; }
    public decimal TotalIssuedCost { get; set; }

    public Tenant? Tenant { get; set; }
    public Branch? Branch { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public Asset? Asset { get; set; }
    public Client? Client { get; set; }
    public Site? Site { get; set; }
    public OperationalSystem? System { get; set; }
    public User? RequestedByUser { get; set; }
    public User? IssuedByUser { get; set; }
    public User? ApprovedByUser { get; set; }
    public User? RejectedByUser { get; set; }
    public User? IssuedToUser { get; set; }
    public ICollection<MaterialIssueLine> Lines { get; set; } = new List<MaterialIssueLine>();
}
