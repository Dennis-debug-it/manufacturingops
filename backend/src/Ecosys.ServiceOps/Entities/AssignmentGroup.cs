using Ecosys.Shared.Common;

namespace Ecosys.ServiceOps.Entities;

public sealed class AssignmentGroup : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public ICollection<AssignmentGroupMember> Members { get; set; } = new List<AssignmentGroupMember>();
}
