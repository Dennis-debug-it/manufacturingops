using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class WorkOrderAssignmentHistory : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid WorkOrderId { get; set; }
    public string Action { get; set; } = string.Empty;
    public Guid? FromGroupId { get; set; }
    public Guid? ToGroupId { get; set; }
    public Guid? FromTechnicianId { get; set; }
    public Guid? ToTechnicianId { get; set; }
    public Guid? PerformedByUserId { get; set; }
    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }

    public Tenant? Tenant { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public AssignmentGroup? FromGroup { get; set; }
    public AssignmentGroup? ToGroup { get; set; }
    public Technician? FromTechnician { get; set; }
    public Technician? ToTechnician { get; set; }
    public User? PerformedByUser { get; set; }
}
