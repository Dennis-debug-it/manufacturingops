using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class FinanceApprovalRule : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string ApprovalArea { get; set; } = string.Empty;
    public decimal ThresholdAmount { get; set; }
    public string Currency { get; set; } = "KES";
    public string RequiredRole { get; set; } = string.Empty;
    public Guid? RequiredApproverUserId { get; set; }
    public Guid? RequiredAssignmentGroupId { get; set; }
    public int Sequence { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
    public User? RequiredApproverUser { get; set; }
    public AssignmentGroup? RequiredAssignmentGroup { get; set; }
}
