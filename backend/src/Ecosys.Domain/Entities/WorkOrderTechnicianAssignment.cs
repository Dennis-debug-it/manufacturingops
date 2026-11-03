using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class WorkOrderTechnicianAssignment : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid TechnicianId { get; set; }
    public bool IsLead { get; set; }
    public Guid? AssignedByUserId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Pending";
    public string? Notes { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? DeclinedAt { get; set; }
    /// <summary>First arrival on site: the response milestone (SLA, start guard). Later visits don't move it.</summary>
    public DateTime? ArrivalAt { get; set; }
    /// <summary>
    /// Most recent arrival. On a job that runs over several days the technician arrives and leaves each
    /// day; they are on site when this is later than <see cref="DepartureAt"/> (the most recent departure).
    /// </summary>
    public DateTime? LastArrivalAt { get; set; }
    public DateTime? DepartureAt { get; set; }

    public Tenant? Tenant { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public Technician? Technician { get; set; }
    public User? AssignedByUser { get; set; }
}
