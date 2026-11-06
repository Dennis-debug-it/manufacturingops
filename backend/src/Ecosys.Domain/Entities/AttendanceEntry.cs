using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>One clock session for a technician (clock-in through clock-out) on a given work date.</summary>
public sealed class AttendanceEntry : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid TechnicianId { get; set; }
    public DateOnly WorkDate { get; set; }
    public DateTime ClockInAt { get; set; }
    public DateTime? ClockOutAt { get; set; }
    public int BreakMinutes { get; set; }
    public DateTime? BreakStartedAt { get; set; }
    public string Status { get; set; } = "ClockedIn"; // ClockedIn | OnBreak | Travelling | ClockedOut
    public string? LastLocation { get; set; }
    public Guid? CurrentWorkOrderId { get; set; }
    /// <summary>The project the technician clocked in on site for, if any.</summary>
    public string? Note { get; set; }

    public Tenant? Tenant { get; set; }
    public Technician? Technician { get; set; }
    public WorkOrder? CurrentWorkOrder { get; set; }
}
