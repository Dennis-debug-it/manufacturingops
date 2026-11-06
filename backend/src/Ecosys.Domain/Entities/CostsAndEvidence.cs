using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/* ── Costs and budgets ───────────────────────────────────────────────────── */

/// <summary>
/// A cost centre in a factory (e.g. "Withering", "Boiler house"). Jobs are charged to the cost
/// centre of their location: the one linked to the job's org node or its nearest parent, else the
/// factory's default cost centre.
/// </summary>
public sealed class CostCentre : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid FactoryId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>The section (or factory) whose jobs it carries; null for a cost centre used only for budgets.</summary>
    public Guid? OrgNodeId { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Stable external identifiers for ERP mapping.</summary>
    public string? ExternalId { get; set; }
    public string? ExternalSystem { get; set; }

    public OrgNode? Factory { get; set; }
    public OrgNode? OrgNode { get; set; }
}

public static class CostCategories
{
    public const string Labour = "Labour";
    public const string Parts = "Parts";
    public const string Contractor = "Contractor";
    public const string Downtime = "Downtime";

    /// <summary>What a maintenance budget is set against. Downtime is a cost, not a budget line.</summary>
    public static readonly IReadOnlyList<string> Budgeted = [Labour, Parts, Contractor];

    public static string? Find(string? value) => Budgeted.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>An annual maintenance budget for a factory (or one of its cost centres), by cost category, in the tenant currency.</summary>
public sealed class MaintenanceBudget : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid FactoryId { get; set; }
    public Guid? CostCentreId { get; set; }
    public int Year { get; set; }
    public string Category { get; set; } = CostCategories.Parts;
    public decimal Amount { get; set; }
    public string? Notes { get; set; }

    public OrgNode? Factory { get; set; }
    public CostCentre? CostCentre { get; set; }
}

/* ── Compliance framework ────────────────────────────────────────────────── */

/// <summary>
/// The kinds of records the system already keeps that can prove a requirement. A requirement lists
/// the sources that evidence it; the evidence pack pulls those records for a factory and period.
/// </summary>
public static class EvidenceSources
{
    public const string Statutory = "statutory";
    public const string Calibration = "calibration";
    public const string FireSafety = "fire-safety";
    public const string Permits = "permits";
    public const string Loto = "loto";
    public const string Incidents = "incidents";
    public const string Training = "training";
    public const string Ppe = "ppe";
    public const string Chemicals = "chemicals";
    public const string Lubricants = "lubricants";
    public const string Waste = "waste";
    public const string Cleaning = "cleaning";
    public const string MetalDetector = "metal-detector";
    public const string Glass = "glass";
    public const string Energy = "energy";
    public const string PreventiveMaintenance = "pm";
    public const string RunningChecks = "running-checks";
    public const string NonConformities = "non-conformities";
    public const string Contractors = "contractors";
    public const string Documents = "documents";

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [Statutory] = "Statutory inspections", [Calibration] = "Calibrations", [FireSafety] = "Fire equipment checks",
        [Permits] = "Permits to work", [Loto] = "Lockout / tagout", [Incidents] = "Incidents and near misses",
        [Training] = "Training records", [Ppe] = "PPE issue", [Chemicals] = "Chemicals register and SDS",
        [Lubricants] = "Food-grade lubricant log", [Waste] = "Waste disposal", [Cleaning] = "Cleaning logs",
        [MetalDetector] = "Metal detector checks", [Glass] = "Glass and hard plastic register", [Energy] = "Energy use per production unit",
        [PreventiveMaintenance] = "Planned maintenance compliance", [RunningChecks] = "Running checks", [NonConformities] = "Non-conformities and corrective actions",
        [Contractors] = "Contractor compliance documents", [Documents] = "Attached documents",
    };

    public static IReadOnlyList<string> Parse(string? csv) =>
        (csv ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(Labels.ContainsKey).Distinct().ToList();
}

/// <summary>A standard the factory is audited or certified against (Rainforest Alliance, ISO 22000, OSHA statutory…).</summary>
public sealed class ComplianceStandard : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Key of the template it was created from, so the default list can be told apart from edits.</summary>
    public string? TemplateKey { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<StandardRequirement> Requirements { get; set; } = new List<StandardRequirement>();
}

/// <summary>One requirement of a standard, and which records prove it.</summary>
public sealed class StandardRequirement : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid StandardId { get; set; }
    /// <summary>The clause or criterion number, e.g. "4.6.1".</summary>
    public string Clause { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Guidance { get; set; }
    /// <summary>Comma-separated <see cref="EvidenceSources"/>.</summary>
    public string EvidenceSources { get; set; } = string.Empty;
    public int Sequence { get; set; }

    public ComplianceStandard? Standard { get; set; }
}

public static class NonConformityStatuses
{
    public const string Open = "Open";
    public const string InProgress = "InProgress";
    /// <summary>Corrective action done; waiting for verification.</summary>
    public const string Resolved = "Resolved";
    public const string Closed = "Closed";

    public static bool IsOpen(string status) => status is Open or InProgress or Resolved;
}

/// <summary>An audit finding or internal non-conformity, tracked as a corrective action to closure.</summary>
public sealed class NonConformity : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Number { get; set; } = string.Empty;
    public Guid FactoryId { get; set; }
    public Guid? StandardId { get; set; }
    public Guid? RequirementId { get; set; }
    public Guid? AuditEventId { get; set; }
    /// <summary>External audit, self-assessment, internal inspection…</summary>
    public string Source { get; set; } = "Audit";
    /// <summary>Minor, Major or Observation.</summary>
    public string Severity { get; set; } = "Minor";
    public string Finding { get; set; } = string.Empty;
    public string? RootCause { get; set; }
    public string? CorrectiveAction { get; set; }
    public Guid? OwnerUserId { get; set; }
    public DateTime? DueDate { get; set; }
    public string Status { get; set; } = NonConformityStatuses.Open;
    public Guid? WorkOrderId { get; set; }
    public string? ClosureEvidence { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? ClosedByName { get; set; }
    public string? RaisedByName { get; set; }
    public DateTime? LastOverdueAlertAt { get; set; }

    public OrgNode? Factory { get; set; }
    public ComplianceStandard? Standard { get; set; }
    public StandardRequirement? Requirement { get; set; }
    public User? Owner { get; set; }
}

public static class AuditEventKinds
{
    public const string SelfAssessment = "Self-assessment";
    public const string Internal = "Internal audit";
    public const string Surveillance = "Surveillance audit";
    public const string Certification = "Certification audit";
    public const string Recertification = "Recertification audit";

    public static readonly IReadOnlyList<string> All = [SelfAssessment, Internal, Surveillance, Certification, Recertification];

    public static string? Find(string? value) => All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>An audit on the calendar: self-assessment, surveillance or recertification.</summary>
public sealed class AuditEvent : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid FactoryId { get; set; }
    public Guid? StandardId { get; set; }
    public string Kind { get; set; } = AuditEventKinds.Surveillance;
    public DateTime ScheduledFor { get; set; }
    public string? Auditor { get; set; }
    /// <summary>Planned, Done or Cancelled.</summary>
    public string Status { get; set; } = "Planned";
    public string? Outcome { get; set; }
    public string? Notes { get; set; }
    public DateTime? CompletedAt { get; set; }
    /// <summary>The last reminder step sent (30, 7).</summary>
    public int? LastReminderDays { get; set; }

    public OrgNode? Factory { get; set; }
    public ComplianceStandard? Standard { get; set; }
}

/* ── Registers ───────────────────────────────────────────────────────────── */

public static class WasteTypes
{
    public static readonly IReadOnlyList<string> All = ["Used oil", "Scrap metal", "Batteries", "E-waste", "Fluorescent tubes", "Chemical containers", "Other"];

    public static string? Find(string? value) => All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>Waste handed to a licensed handler: what, how much, to whom, under which licence and consignment note.</summary>
public sealed class WasteDisposal : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid FactoryId { get; set; }
    public DateTime DisposedOn { get; set; }
    public string WasteType { get; set; } = "Other";
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "kg";
    public Guid? ContractorId { get; set; }
    public string HandlerName { get; set; } = string.Empty;
    /// <summary>The handler's NEMA (or equivalent) licence number.</summary>
    public string? LicenceNumber { get; set; }
    public string? ConsignmentNote { get; set; }
    public Guid? AttachmentId { get; set; }
    public string? Notes { get; set; }
    public string? RecordedByName { get; set; }

    public OrgNode? Factory { get; set; }
}

public static class FoodSafetyKinds
{
    public const string Cleaning = "Cleaning";
    public const string MetalDetector = "MetalDetector";
    public const string GlassCheck = "GlassCheck";

    public static readonly IReadOnlyList<string> All = [Cleaning, MetalDetector, GlassCheck];

    public static string? Find(string? value) => All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// A food safety record: a cleaning done, a metal detector test (each test piece rejected?), or a
/// check of the glass and hard plastic register. A failed check raises a corrective job.
/// </summary>
public sealed class FoodSafetyRecord : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid FactoryId { get; set; }
    public string Kind { get; set; } = FoodSafetyKinds.Cleaning;
    public DateTime DoneAt { get; set; }
    public Guid? AssetId { get; set; }
    /// <summary>The area or line, when not one asset.</summary>
    public string? Area { get; set; }
    public bool Passed { get; set; } = true;
    /// <summary>Cleaning: method and chemical used.</summary>
    public string? Method { get; set; }
    /// <summary>Metal detector: each test piece detected and rejected.</summary>
    public bool? FerrousRejected { get; set; }
    public bool? NonFerrousRejected { get; set; }
    public bool? StainlessRejected { get; set; }
    /// <summary>Glass check: the register items found intact, and any breakage.</summary>
    public int? ItemsChecked { get; set; }
    public int? ItemsDamaged { get; set; }
    public string? Notes { get; set; }
    public string DoneByName { get; set; } = string.Empty;
    public string? VerifiedByName { get; set; }
    public Guid? CorrectiveWorkOrderId { get; set; }

    public OrgNode? Factory { get; set; }
    public Asset? Asset { get; set; }
}

/// <summary>An item on the glass and hard plastic register: a light cover, sight glass, gauge face…</summary>
public sealed class GlassRegisterItem : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid FactoryId { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    /// <summary>Glass, Hard plastic or Perspex.</summary>
    public string Material { get; set; } = "Glass";
    public int Quantity { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    public OrgNode? Factory { get; set; }
}

/* ── Data protection ─────────────────────────────────────────────────────── */

/// <summary>Consent a person gave (or withdrew) for a use of their personal data, under the Kenya Data Protection Act 2019.</summary>
public sealed class ConsentRecord : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? TechnicianId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    /// <summary>What it is for, e.g. "Phone number for SMS job alerts".</summary>
    public string Purpose { get; set; } = string.Empty;
    public DateTime GivenAt { get; set; }
    /// <summary>Signed form, in the app, verbal…</summary>
    public string Method { get; set; } = "Signed form";
    public DateTime? WithdrawnAt { get; set; }
    public string? RecordedByName { get; set; }
}
