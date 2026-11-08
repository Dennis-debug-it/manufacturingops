using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class WorkOrder : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? ClientId { get; set; }
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
    public Guid? WorkflowStatusId { get; set; }
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
    // Service report lifecycle (report queue): null status = awaiting generation once the WO is completed.
    public string? ReportStatus { get; set; } // Draft | UnderReview | Approved | Sent | Acknowledged
    public string? ReportNumber { get; set; } // SR No
    public DateTime? ReportGeneratedAt { get; set; }
    public DateTime? ReportApprovedAt { get; set; }
    public DateTime? ReportSentAt { get; set; }
    // Office edits to the report's wording. Null = use the job's own text (description / technician notes),
    // which is never changed by editing the report.
    public string? ReportProblemText { get; set; }
    public string? ReportFindingsText { get; set; }
    public string? ReportWorkDoneText { get; set; }
    public DateTime? ReportEditedAt { get; set; }
    public Guid? ReportEditedByUserId { get; set; }
    public bool IsPreventiveMaintenance { get; set; }
    public Guid? PreventiveMaintenancePlanId { get; set; }
    public Guid? PmTemplateId { get; set; }
    public string? PmTemplateNameSnapshot { get; set; }
    public int? PmTemplateVersionSnapshot { get; set; }
    public Guid? WorkOrderTemplateId { get; set; }
    public string? PlannedMaterialsJson { get; set; }
    /// <summary>The CRM deal this job was won through, when it came from sales.</summary>
    public Guid? ParentWorkOrderId { get; set; }
    /// <summary>
    /// The finished job this one follows up on (a return visit). Unlike <see cref="ParentWorkOrderId"/>
    /// (sub-tasks), a follow-up never completes or reopens the original; it is a link for history and
    /// repeat-visit reporting, and lets the technician see the original job's report.
    /// </summary>
    public Guid? FollowUpOfWorkOrderId { get; set; }
    public string? PreviousStatus { get; set; }
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
    public string? TrackingToken { get; set; }

    // Repeat visit detection
    public bool IsRepeatVisit { get; set; }
    public int RepeatVisitCount { get; set; }

    // Factory maintenance
    /// <summary>Where the work is (factory, section or sub-section); taken from the asset when there is one.</summary>
    public Guid? OrgNodeId { get; set; }
    /// <summary>How the job came in: Office, Operator, RunningCheck, Preventive.</summary>
    public string? RequestSource { get; set; }
    public string? RequestedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? PlannedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public Guid? VerifiedByUserId { get; set; }
    public string? VerificationNotes { get; set; }
    /// <summary>The asset stopped (a breakdown); downtime runs from <see cref="DowntimeStartedAt"/> to <see cref="DowntimeEndedAt"/>.</summary>
    public bool IsBreakdown { get; set; }
    public DateTime? DowntimeStartedAt { get; set; }
    public DateTime? DowntimeEndedAt { get; set; }
    /// <summary>The job can only be done with the line stopped (queues for the weekly window in Phase 3).</summary>
    public bool NeedsLineStop { get; set; }
    /// <summary>How many weekly windows this job has been carried over from unfinished.</summary>
    public int WindowCarryoverCount { get; set; }
    /// <summary>Only technicians holding this valid certificate (e.g. an electrical licence) may be assigned.</summary>
    public Guid? RequiredCertificationId { get; set; }
    /// <summary>Permit types needed before work starts, comma-separated (HotWork, ConfinedSpace, Electrical, WorkingAtHeights).</summary>
    public string? RequiredPermitTypes { get; set; }
    /// <summary>The outside contractor doing the job, and the maintenance agreement it falls under.</summary>
    public Guid? ContractorId { get; set; }
    public Guid? MaintenanceAgreementId { get; set; }
    public Guid? FailureProblemCodeId { get; set; }
    public Guid? FailureCauseCodeId { get; set; }
    public Guid? FailureRemedyCodeId { get; set; }
    /// <summary>The earlier job on the same asset and fault that this one repeats, within the rework window.</summary>
    public Guid? ReworkOfWorkOrderId { get; set; }
    /// <summary>The job plan the job was raised from; its steps are copied onto the job as checklist items.</summary>
    public Guid? JobPlanId { get; set; }
    public JobPlan? JobPlan { get; set; }
    public FailureCode? FailureProblemCode { get; set; }
    public FailureCode? FailureCauseCode { get; set; }
    public FailureCode? FailureRemedyCode { get; set; }
    public OrgNode? OrgNode { get; set; }

    public Tenant? Tenant { get; set; }
    public WorkflowStatus? WorkflowStatus { get; set; }
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
    public WorkOrderTemplate? WorkOrderTemplate { get; set; }
    public WorkOrder? ParentWorkOrder { get; set; }
    public ICollection<WorkOrder> ChildWorkOrders { get; set; } = new List<WorkOrder>();
    public WorkOrder? FollowUpOfWorkOrder { get; set; }
    public ICollection<WorkOrder> FollowUpWorkOrders { get; set; } = new List<WorkOrder>();
    public ICollection<MaterialRequest> MaterialRequests { get; set; } = new List<MaterialRequest>();
    public ICollection<WorkOrderEvent> Events { get; set; } = new List<WorkOrderEvent>();
    public ICollection<WorkOrderNote> Notes { get; set; } = new List<WorkOrderNote>();
    public ICollection<PmReport> PmReports { get; set; } = new List<PmReport>();
    public ICollection<WorkOrderAssignment> Assignments { get; set; } = new List<WorkOrderAssignment>();
    public ICollection<WorkOrderTechnicianAssignment> TechnicianAssignments { get; set; } = new List<WorkOrderTechnicianAssignment>();
    public ICollection<WorkOrderAssignmentHistory> AssignmentHistory { get; set; } = new List<WorkOrderAssignmentHistory>();
    public ICollection<WorkOrderChecklistItem> ChecklistItems { get; set; } = new List<WorkOrderChecklistItem>();
    public ICollection<WorkOrderPhotoEvidence> PhotoEvidence { get; set; } = new List<WorkOrderPhotoEvidence>();
    public ICollection<WorkOrderSignature> Signatures { get; set; } = new List<WorkOrderSignature>();
    public ICollection<WorkOrderMaterialUsage> MaterialUsages { get; set; } = new List<WorkOrderMaterialUsage>();
    public ICollection<MaterialIssue> MaterialIssues { get; set; } = new List<MaterialIssue>();

    public ICollection<WorkOrderTimeLog> TimeLogs { get; set; } = new List<WorkOrderTimeLog>();
}
