using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class WorkOrder : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid ClientId { get; set; }
    public Guid? SiteId { get; set; }
    public Guid? SystemId { get; set; }
    public Guid? AssetId { get; set; }
    public Guid? AssignmentGroupId { get; set; }
    public Guid? ContractId { get; set; }
    public Guid? RateCardId { get; set; }
    public string AssignmentType { get; set; } = "IndividualTechnician";
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Priority { get; set; } = "Medium";
    public string Status { get; set; } = "New";
    public string WorkType { get; set; } = "CorrectiveMaintenance";
    public string? ServiceType { get; set; }
    public decimal? EstimatedDuration { get; set; }
    public decimal? ActualDuration { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
    public Guid? LeadTechnicianId { get; set; }
    public string? AssignedTechnicianIdsJson { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? ScheduledStartAt { get; set; }
    public DateTime? ScheduledEndAt { get; set; }
    public DateTime? WorkStartedAt { get; set; }
    public DateTime? ArrivalAt { get; set; }
    public DateTime? DepartureAt { get; set; }
    public string? HoldReason { get; set; }
    public string? HoldNote { get; set; }
    public DateTime? HoldStartedAt { get; set; }
    public DateTime? HoldEndedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public string? WorkDoneNotes { get; set; }
    public string? JobCardNotes { get; set; }
    public string? AcknowledgedByName { get; set; }
    public string? AcknowledgementComments { get; set; }
    public DateTime? AcknowledgementDate { get; set; }
    public bool IsPreventiveMaintenance { get; set; }
    public Guid? PreventiveMaintenancePlanId { get; set; }
    public Guid? PmTemplateId { get; set; }
    public string? PmTemplateNameSnapshot { get; set; }
    public int? PmTemplateVersionSnapshot { get; set; }
    public DateTime? SlaResponseDeadline { get; set; }
    public DateTime? SlaResolutionDeadline { get; set; }
    public string? AppliedSlaSource { get; set; }
    public decimal? AppliedResponseHours { get; set; }
    public decimal? AppliedResolutionHours { get; set; }
    public bool SlaResponseBreached { get; set; }
    public bool SlaResolutionBreached { get; set; }
    public DateTime? SlaResponseBreachedAt { get; set; }
    public DateTime? SlaResolutionBreachedAt { get; set; }
    public string? BillingModel { get; set; }
    public string? CoverageStatus { get; set; }
    public string? PricingStatus { get; set; }
    public string? AppliedRateSource { get; set; }
    public decimal? EstimatedLabourHours { get; set; }
    public decimal? EstimatedMaterialCost { get; set; }
    public decimal? EstimatedCharge { get; set; }
    public decimal? FinalCharge { get; set; }
    public string? PricingNotes { get; set; }
    public string? ContractSnapshotJson { get; set; }
    public string? SlaSnapshotJson { get; set; }
    public string? PricingSnapshotJson { get; set; }
    // Client notifications
    public bool NotifyClient { get; set; } = true;
    public string? ClientContactEmail { get; set; }

    public Tenant? Tenant { get; set; }
    public Branch? Branch { get; set; }
    public Client? Client { get; set; }
    public Site? Site { get; set; }
    public OperationalSystem? System { get; set; }
    public Asset? Asset { get; set; }
    public AssignmentGroup? AssignmentGroup { get; set; }
    public Contract? Contract { get; set; }
    public ContractRateCard? RateCard { get; set; }
    public Technician? AssignedTechnician { get; set; }
    public Technician? LeadTechnician { get; set; }
    public User? ClosedByUser { get; set; }
    public PreventiveMaintenancePlan? PreventiveMaintenancePlan { get; set; }
    public PmTemplate? PmTemplate { get; set; }
    public ICollection<MaterialRequest> MaterialRequests { get; set; } = new List<MaterialRequest>();
    public ICollection<WorkOrderEvent> Events { get; set; } = new List<WorkOrderEvent>();
    public ICollection<PmReport> PmReports { get; set; } = new List<PmReport>();
    public ICollection<WorkOrderAssignment> Assignments { get; set; } = new List<WorkOrderAssignment>();
    public ICollection<WorkOrderTechnicianAssignment> TechnicianAssignments { get; set; } = new List<WorkOrderTechnicianAssignment>();
    public ICollection<WorkOrderAssignmentHistory> AssignmentHistory { get; set; } = new List<WorkOrderAssignmentHistory>();
    public ICollection<WorkOrderChecklistItem> ChecklistItems { get; set; } = new List<WorkOrderChecklistItem>();
    public ICollection<WorkOrderPhotoEvidence> PhotoEvidence { get; set; } = new List<WorkOrderPhotoEvidence>();
    public ICollection<WorkOrderSignature> Signatures { get; set; } = new List<WorkOrderSignature>();
    public ICollection<WorkOrderMaterialUsage> MaterialUsages { get; set; } = new List<WorkOrderMaterialUsage>();
    public ICollection<MaterialIssue> MaterialIssues { get; set; } = new List<MaterialIssue>();

    // Billing
    public string BillingStatus { get; set; } = "Unbilled"; // Unbilled|PartiallyBilled|Billed|NotBillable
    public bool IsBillable { get; set; } = true;
    public ICollection<WorkOrderBillableItem> BillableItems { get; set; } = new List<WorkOrderBillableItem>();
    public ICollection<WorkOrderTimeLog> TimeLogs { get; set; } = new List<WorkOrderTimeLog>();
}
