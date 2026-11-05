using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class Technician : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? BranchId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? SkillCategory { get; set; }
    public string? AssignmentGroup { get; set; }
    public string AvailabilityStatus { get; set; } = "Available";
    public string? AvailabilityNote { get; set; }
    public DateTime? AvailabilityUntil { get; set; }
    public DateTime? AvailabilityUpdatedAt { get; set; }
    public decimal? LastKnownLatitude { get; set; }
    public decimal? LastKnownLongitude { get; set; }
    public DateTime? LastLocationAt { get; set; }
    public bool IsTrackingActive { get; set; }
    public Guid? ActiveWorkOrderId { get; set; }
    public bool IsActive { get; set; } = true;

    // ── Factory (scope Phase 4) ──
    /// <summary>Fitter, electrician, boiler operator, instrument technician…</summary>
    public string? Trade { get; set; }
    /// <summary>The factory or section they normally work in.</summary>
    public Guid? HomeOrgNodeId { get; set; }
    /// <summary>Normal hourly cost of their time.</summary>
    public decimal? HourlyRate { get; set; }
    /// <summary>Overtime is paid at the hourly rate × this (1.5 by default; 2 on rest days by policy).</summary>
    public decimal OvertimeMultiplier { get; set; } = 1.5m;
    public ICollection<TechnicianSkill> Skills { get; set; } = new List<TechnicianSkill>();
    public ICollection<TechnicianCertificate> Certificates { get; set; } = new List<TechnicianCertificate>();

    public Tenant? Tenant { get; set; }
    public User? User { get; set; }
    public Branch? Branch { get; set; }
    public WorkOrder? ActiveWorkOrder { get; set; }
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
    public ICollection<AssignmentGroupMember> AssignmentGroups { get; set; } = new List<AssignmentGroupMember>();
    public ICollection<WorkOrderTechnicianAssignment> WorkOrderAssignments { get; set; } = new List<WorkOrderTechnicianAssignment>();
    public ICollection<WorkOrderAssignmentHistory> AssignmentHistoryFromTechnicians { get; set; } = new List<WorkOrderAssignmentHistory>();
    public ICollection<WorkOrderAssignmentHistory> AssignmentHistoryToTechnicians { get; set; } = new List<WorkOrderAssignmentHistory>();
}
