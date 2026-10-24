using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// Stage categories drive behaviour (dashboards, lifecycle rules). Stage display names are
/// tenant-defined; code never matches on a name, only on one of these values.
/// </summary>
public static class ProjectStageCategories
{
    public const string Planning = "Planning";
    public const string Active = "Active";
    public const string OnHold = "OnHold";
    public const string Handover = "Handover";
    public const string Completed = "Completed";
    public const string Closed = "Closed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = [Planning, Active, OnHold, Handover, Completed, Closed, Cancelled];

    /// <summary>Categories in which the project is still being delivered.</summary>
    public static readonly string[] Open = [Planning, Active, OnHold, Handover];

    public static bool IsSupported(string? value) =>
        value is not null && All.Contains(value, StringComparer.OrdinalIgnoreCase);
}

public static class ProjectMemberRoles
{
    public const string ProjectManager = "ProjectManager";
    public const string Supervisor = "Supervisor";
    public const string Member = "Member";
    public const string Viewer = "Viewer";

    public static readonly string[] All = [ProjectManager, Supervisor, Member, Viewer];
    /// <summary>Roles that run the site day to day: mark the muster roll and write the diary.</summary>
    public static readonly string[] SiteRoles = [ProjectManager, Supervisor];

    public static bool IsSupported(string? value) =>
        value is not null && All.Contains(value, StringComparer.OrdinalIgnoreCase);
}

public static class ProjectMilestoneStatuses
{
    public const string NotStarted = "NotStarted";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Verified = "Verified";

    public static readonly string[] All = [NotStarted, InProgress, Completed, Verified];
}

public static class ProjectTaskStatuses
{
    public const string Open = "Open";
    public const string InProgress = "InProgress";
    public const string Done = "Done";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = [Open, InProgress, Done, Cancelled];
}

public static class ProjectVariationKinds
{
    public const string Addition = "Addition";
    public const string Omission = "Omission";

    public static readonly string[] All = [Addition, Omission];
}

public static class ProjectMusterStatuses
{
    public const string Present = "Present";
    public const string HalfDay = "HalfDay";
    public const string Absent = "Absent";

    public static readonly string[] All = [Present, HalfDay, Absent];

    public static decimal DayFraction(string status) => status switch
    {
        Present => 1m,
        HalfDay => 0.5m,
        _ => 0m,
    };
}

public static class ProjectWorkerEngagements
{
    public const string Casual = "Casual";
    public const string Permanent = "Permanent";
    public const string Subcontractor = "Subcontractor";

    public static readonly string[] All = [Casual, Permanent, Subcontractor];
}

public static class ProjectCostCategories
{
    public const string Materials = "Materials";
    public const string Labour = "Labour";
    public const string Subcontractor = "Subcontractor";
    public const string Equipment = "Equipment";
    public const string Other = "Other";

    public static readonly string[] All = [Materials, Labour, Subcontractor, Equipment, Other];
}

public static class ProjectValuationStatuses
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string Certified = "Certified";
    public const string Invoiced = "Invoiced";
    public const string Void = "Void";
}

public static class ProjectRetentionStages
{
    public const string PracticalCompletion = "PracticalCompletion";
    public const string EndOfDefectsLiability = "EndOfDefectsLiability";
}

public static class ProjectVariationStatuses
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Void = "Void";
}

/// <summary>Tenant-defined classification such as "New Installation" or "Service Project".</summary>
public sealed class ProjectType : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? DefaultTemplateId { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public Tenant? Tenant { get; set; }
    public ProjectTemplate? DefaultTemplate { get; set; }
}

/// <summary>Tenant-defined workflow stage. Rules use <see cref="Category"/>, never <see cref="Name"/>.</summary>
public sealed class ProjectStage : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = ProjectStageCategories.Planning;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
}

/// <summary>
/// Reusable starting point. <see cref="ItemsJson"/> holds milestones (with weights and day offsets
/// from the planned start) and their tasks; they are copied into a project when it is created.
/// </summary>
public sealed class ProjectTemplate : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ItemsJson { get; set; } = "[]";
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
}

public sealed class Project : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public string ProjectNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid ClientId { get; set; }
    public Guid? SiteId { get; set; }
    public Guid ProjectTypeId { get; set; }
    public Guid StageId { get; set; }
    public Guid? ProjectManagerUserId { get; set; }

    public string Currency { get; set; } = "KES";
    public decimal OriginalValue { get; set; }
    /// <summary>Sum of approved additions; recomputed from the variation rows on every decision.</summary>
    public decimal ApprovedAdditions { get; set; }
    /// <summary>Sum of approved omissions (a positive number); recomputed like <see cref="ApprovedAdditions"/>.</summary>
    public decimal ApprovedOmissions { get; set; }
    /// <summary>
    /// Concurrency token for the commercial totals. Bumped on every recompute, so two variation
    /// decisions saved at the same moment cannot both win: the second gets a concurrency conflict.
    /// </summary>
    public int ValueVersion { get; set; }

    public DateOnly? PlannedStartDate { get; set; }
    public DateOnly? PlannedEndDate { get; set; }
    public DateOnly? ActualStartDate { get; set; }
    public DateOnly? ActualEndDate { get; set; }

    /// <summary>Cached result of the weighted-milestone calculation.</summary>
    public decimal ProgressPercent { get; set; }
    public decimal? ProgressOverridePercent { get; set; }
    public Guid? ProgressOverrideByUserId { get; set; }
    public string? ProgressOverrideReason { get; set; }

    public bool VisibleInClientPortal { get; set; } = true;

    // Billing terms (D9). Null retention means none is held.
    public decimal? RetentionPercent { get; set; }
    public decimal? RetentionCapAmount { get; set; }
    /// <summary>Share of retention held released at practical completion; the rest at the end of defects liability.</summary>
    public decimal RetentionReleaseAtCompletionPercent { get; set; } = 50;
    public int? DefectsLiabilityMonths { get; set; }
    public decimal? AdvancePaymentAmount { get; set; }
    /// <summary>Percentage of each valuation's work done in the period recovered against the advance.</summary>
    public decimal? AdvanceRecoveryPercent { get; set; }
    public Guid? AdvanceInvoiceId { get; set; }

    public Guid? SourceQuotationId { get; set; }
    public Guid? MaintenanceContractId { get; set; }

    public DateTime? HandedOverAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public string? ClosureNotes { get; set; }

    public decimal RevisedValue => OriginalValue + ApprovedAdditions - ApprovedOmissions;
    public decimal EffectiveProgressPercent => ProgressOverridePercent ?? ProgressPercent;

    public Tenant? Tenant { get; set; }
    public Branch? Branch { get; set; }
    public Client? Client { get; set; }
    public Site? Site { get; set; }
    public ProjectType? ProjectType { get; set; }
    public ProjectStage? Stage { get; set; }
    public User? ProjectManagerUser { get; set; }
    public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
    public ICollection<ProjectConsultant> Consultants { get; set; } = new List<ProjectConsultant>();
    public ICollection<ProjectMilestone> Milestones { get; set; } = new List<ProjectMilestone>();
    public ICollection<ProjectTask> Tasks { get; set; } = new List<ProjectTask>();
    public ICollection<ProjectVariation> Variations { get; set; } = new List<ProjectVariation>();
}

/// <summary>Internal team member: an office user, a field technician, or both when linked.</summary>
public sealed class ProjectMember : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? TechnicianId { get; set; }
    public string Role { get; set; } = ProjectMemberRoles.Member;
    public string? Trade { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }

    public Tenant? Tenant { get; set; }
    public Project? Project { get; set; }
    public User? User { get; set; }
    public Technician? Technician { get; set; }
}

/// <summary>Consultant directory entry: a firm and its contact details, shared by all projects.</summary>
public sealed class Consultant : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string FirmName { get; set; } = string.Empty;
    public string? Discipline { get; set; }
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? PhysicalAddress { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
}

public sealed class ProjectConsultant : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid ConsultantId { get; set; }
    public string? RoleOnProject { get; set; }

    public Tenant? Tenant { get; set; }
    public Project? Project { get; set; }
    public Consultant? Consultant { get; set; }
}

public sealed class ProjectMilestone : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    /// <summary>Relative weight; normalised against the project's total when computing progress.</summary>
    public decimal Weight { get; set; } = 1;
    public DateOnly? PlannedDate { get; set; }
    public DateOnly? ForecastDate { get; set; }
    public string Status { get; set; } = ProjectMilestoneStatuses.NotStarted;
    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public Guid? VerifiedByUserId { get; set; }

    public Tenant? Tenant { get; set; }
    public Project? Project { get; set; }
}

public sealed class ProjectTask : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? MilestoneId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? AssigneeUserId { get; set; }
    public DateOnly? DueDate { get; set; }
    public string Priority { get; set; } = "Medium";
    public string Status { get; set; } = ProjectTaskStatuses.Open;
    public DateTime? CompletedAt { get; set; }
    public int SortOrder { get; set; }

    public Tenant? Tenant { get; set; }
    public Project? Project { get; set; }
    public ProjectMilestone? Milestone { get; set; }
    public User? AssigneeUser { get; set; }
}

public sealed class ProjectVariation : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public string VariationNumber { get; set; } = string.Empty;
    public string Kind { get; set; } = ProjectVariationKinds.Addition;
    /// <summary>Always positive; <see cref="Kind"/> says whether it adds to or omits from the value.</summary>
    public decimal Amount { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ClientReference { get; set; }
    public string Status { get; set; } = ProjectVariationStatuses.Draft;
    public Guid? SubmittedByUserId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public string? DecisionNote { get; set; }

    public Tenant? Tenant { get; set; }
    public Project? Project { get; set; }
}

/// <summary>
/// Someone on site who may not have an Ecosys login (D8). The supervisor can add them; only the
/// office sets their pay (<see cref="ProjectWorkerRate"/>). No national ID is stored.
/// </summary>
public sealed class ProjectWorker : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? TechnicianId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Trade { get; set; }
    public string EngagementType { get; set; } = ProjectWorkerEngagements.Casual;
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
    public Project? Project { get; set; }
    public Technician? Technician { get; set; }
    public ICollection<ProjectWorkerRate> Rates { get; set; } = new List<ProjectWorkerRate>();
}

/// <summary>A dated daily rate set by the office. A later rate never changes days before its effective date.</summary>
public sealed class ProjectWorkerRate : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProjectWorkerId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public decimal DailyRate { get; set; }
    public Guid SetByUserId { get; set; }
    public string? Note { get; set; }

    public Tenant? Tenant { get; set; }
    public ProjectWorker? Worker { get; set; }
}

/// <summary>One mark per worker per project per day, made by the site supervisor. Holds no money.</summary>
public sealed class ProjectMusterEntry : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid ProjectWorkerId { get; set; }
    public DateOnly WorkDate { get; set; }
    public string Status { get; set; } = ProjectMusterStatuses.Present;
    public decimal DayFraction { get; set; } = 1;
    public Guid MarkedByUserId { get; set; }
    public string? Note { get; set; }

    public Tenant? Tenant { get; set; }
    public Project? Project { get; set; }
    public ProjectWorker? Worker { get; set; }
}

public sealed class ProjectDiaryEntry : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public DateOnly EntryDate { get; set; }
    public Guid AuthorUserId { get; set; }
    public string? WorkCompleted { get; set; }
    public string? IssuesAndDelays { get; set; }
    public string? InstructionsReceived { get; set; }
    public string? Weather { get; set; }
    /// <summary>Locked once submitted; corrections are new entries.</summary>
    public DateTime? SubmittedAt { get; set; }

    public Tenant? Tenant { get; set; }
    public Project? Project { get; set; }
    public User? AuthorUser { get; set; }
}

/// <summary>Planned cost by category. Actual costs are derived from the records that hold them.</summary>
public sealed class ProjectBudgetLine : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public string Category { get; set; } = ProjectCostCategories.Materials;
    public string? Description { get; set; }
    public decimal Amount { get; set; }

    public Tenant? Tenant { get; set; }
    public Project? Project { get; set; }
}

/// <summary>A cost with no natural home elsewhere: equipment hire, a one-off site cost, permanent staff labour.</summary>
public sealed class ProjectCostEntry : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public string Category { get; set; } = ProjectCostCategories.Other;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly IncurredOn { get; set; }
    public Guid RecordedByUserId { get; set; }

    public Tenant? Tenant { get; set; }
    public Project? Project { get; set; }
}

/// <summary>
/// An interim valuation / payment certificate, recorded as totals (D9). The QS's BQ workings and the
/// signed certificate are attachments. All "ToDate" figures are cumulative.
/// </summary>
public sealed class ProjectValuation : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public string ValuationNumber { get; set; } = string.Empty;
    public DateOnly PeriodEnd { get; set; }
    public decimal WorkDoneToDate { get; set; }
    public decimal MaterialsOnSite { get; set; }
    public decimal GrossValueToDate { get; set; }
    public decimal RetentionToDate { get; set; }
    public decimal AdvanceRecoveredToDate { get; set; }
    public decimal PreviouslyCertified { get; set; }
    public decimal AmountDue { get; set; }
    public string Status { get; set; } = ProjectValuationStatuses.Draft;
    public Guid? CertifiedByConsultantId { get; set; }
    public string? CertificateReference { get; set; }
    public DateOnly? CertifiedOn { get; set; }
    public decimal? CertifiedAmount { get; set; }
    public Guid? InvoiceId { get; set; }
    public string? Notes { get; set; }

    public Tenant? Tenant { get; set; }
    public Project? Project { get; set; }
    public Consultant? CertifiedByConsultant { get; set; }
    public TenantInvoice? Invoice { get; set; }
}

public sealed class ProjectRetentionRelease : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public string Stage { get; set; } = ProjectRetentionStages.PracticalCompletion;
    public decimal Amount { get; set; }
    public DateOnly ReleasedOn { get; set; }
    public Guid? InvoiceId { get; set; }
    public Guid ReleasedByUserId { get; set; }

    public Tenant? Tenant { get; set; }
    public Project? Project { get; set; }
    public TenantInvoice? Invoice { get; set; }
}

/// <summary>Sent-marker for date-based project reminders, so a reminder goes once per entity, key and date.</summary>
public sealed class ProjectReminderLog : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public string NotificationKey { get; set; } = string.Empty;
    public DateOnly ForDate { get; set; }
}
