using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class UserBranchAssignment : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid BranchId { get; set; }
    public bool IsDefault { get; set; }

    public Tenant? Tenant { get; set; }
    public User? User { get; set; }
    public Branch? Branch { get; set; }
}
