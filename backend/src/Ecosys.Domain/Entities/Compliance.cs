using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public static class ComplianceKinds
{
    /// <summary>A certified examiner's inspection required by law (boilers, pressure vessels, lifting equipment).</summary>
    public const string StatutoryInspection = "StatutoryInspection";
    /// <summary>Instruments, weighbridges, scales, metal detectors.</summary>
    public const string Calibration = "Calibration";
    /// <summary>Extinguishers, hydrants, alarms, fire audit.</summary>
    public const string FireSafety = "FireSafety";
    /// <summary>A licence, permit or audit certificate that expires.</summary>
    public const string Certificate = "Certificate";
    public const string Other = "Other";

    public static readonly IReadOnlyList<string> All = [StatutoryInspection, Calibration, FireSafety, Certificate, Other];
}

public static class ComplianceResults
{
    public const string Pass = "Pass";
    /// <summary>Passed with conditions or repairs to make.</summary>
    public const string Conditional = "Conditional";
    public const string Fail = "Fail";

    public static readonly IReadOnlyList<string> All = [Pass, Conditional, Fail];
}

/// <summary>
/// Something the factory must have done and certified by a date: a statutory inspection, a
/// calibration, fire equipment servicing, a licence. The first part of the compliance framework:
/// an owner, a due date, evidence (certificates) and escalation when it lapses.
/// </summary>
public sealed class ComplianceItem : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    /// <summary>Where it applies: the factory or section.</summary>
    public Guid OrgNodeId { get; set; }
    public Guid? AssetId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = ComplianceKinds.StatutoryInspection;
    /// <summary>The rule it satisfies, e.g. "OSHA 2007 s.67 (DOSH)".</summary>
    public string? Regulation { get; set; }
    public int IntervalMonths { get; set; } = 12;
    public DateTime? LastDoneAt { get; set; }
    public DateTime? DueDate { get; set; }
    public Guid? OwnerUserId { get; set; }
    /// <summary>The usual examiner, calibration lab or service company.</summary>
    public string? Provider { get; set; }
    /// <summary>Raise a statutory job this many days before the due date (blank: no job).</summary>
    public int? RaiseJobDaysBefore { get; set; } = 30;
    public Guid? OpenWorkOrderId { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>The template it came from, so a pack isn't installed twice.</summary>
    public string? TemplateKey { get; set; }

    /// <summary>Alerts sent: the last "due in N days" step (30, 14, 7) for the current due date, and the last overdue escalation.</summary>
    public int? LastDueAlertDays { get; set; }
    public DateTime? LastOverdueAlertAt { get; set; }

    public Tenant? Tenant { get; set; }
    public OrgNode? OrgNode { get; set; }
    public Asset? Asset { get; set; }
    public User? Owner { get; set; }
    public WorkOrder? OpenWorkOrder { get; set; }
    public ICollection<ComplianceRecord> Records { get; set; } = new List<ComplianceRecord>();
}

/// <summary>One inspection, calibration or renewal done, with its certificate.</summary>
public sealed class ComplianceRecord : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ItemId { get; set; }
    public DateTime DoneAt { get; set; }
    public string Result { get; set; } = ComplianceResults.Pass;
    /// <summary>When the certificate runs out: the item's next due date.</summary>
    public DateTime? ValidUntil { get; set; }
    public string? CertificateNumber { get; set; }
    public string? Examiner { get; set; }
    public string? Notes { get; set; }
    public Guid? CertificateAttachmentId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public string? RecordedByName { get; set; }

    public ComplianceItem? Item { get; set; }
    public Attachment? CertificateAttachment { get; set; }
}
