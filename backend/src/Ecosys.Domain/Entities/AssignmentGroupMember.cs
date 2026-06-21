using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class AssignmentGroupMember : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid AssignmentGroupId { get; set; }
    public Guid TechnicianId { get; set; }
    public bool IsLead { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public Tenant? Tenant { get; set; }
    public AssignmentGroup? AssignmentGroup { get; set; }
    public Technician? Technician { get; set; }
}
