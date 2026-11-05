using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class WorkOrderTimeLog : AuditableEntity, ITenantEntity
{
    public Guid WorkOrderId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? TechnicianId { get; set; }
    public string? TechnicianName { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int DurationMinutes { get; set; }
    public string? Notes { get; set; }
    public bool IsBilled { get; set; } = false;
    /// <summary>Outside the technician's rostered shift (or on a day off).</summary>
    public bool IsOvertime { get; set; }
    /// <summary>Cost of the time at the technician's rate when it was logged.</summary>
    public decimal? LabourCost { get; set; }

    public Tenant? Tenant { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public Technician? Technician { get; set; }
}
