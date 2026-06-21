using Ecosys.Shared.Common;

namespace Ecosys.ServiceOps.Entities;

public sealed class AssignmentGroupMember : TenantEntity
{
    public Guid AssignmentGroupId { get; set; }
    public Guid UserId { get; set; }

    public AssignmentGroup? AssignmentGroup { get; set; }
}
