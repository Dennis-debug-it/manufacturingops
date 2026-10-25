using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class LeaveRequest : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    /// <summary>The requester's technician profile, when they have one; their leave then counts on their timesheet.</summary>
    public Guid? TechnicianId { get; set; }
    /// <summary>The requester's user account. Office staff without a technician profile have only this.</summary>
    public Guid? UserId { get; set; }
    public string LeaveTypeCode { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal TotalDays { get; set; }
    public string Status { get; set; } = "Pending"; // Pending | Approved | Rejected
    public string? Reason { get; set; }
    public string? DecisionNote { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public DateTime? DecidedAt { get; set; }

    public Tenant? Tenant { get; set; }
    public Technician? Technician { get; set; }
    public User? User { get; set; }
    public User? DecidedByUser { get; set; }
}
