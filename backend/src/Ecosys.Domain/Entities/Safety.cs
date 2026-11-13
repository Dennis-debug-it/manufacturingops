using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public static class PermitTypes
{
    public const string HotWork = "HotWork";
    public const string ConfinedSpace = "ConfinedSpace";
    public const string Electrical = "Electrical";
    public const string WorkingAtHeights = "WorkingAtHeights";
    public const string General = "General";

    public static readonly IReadOnlyList<string> All = [HotWork, ConfinedSpace, Electrical, WorkingAtHeights, General];

    public static string? Find(string? value) => All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

public static class PermitStatuses
{
    /// <summary>Filled in, waiting for the authorising person.</summary>
    public const string Requested = "Requested";
    /// <summary>Authorised; the work may start inside its validity.</summary>
    public const string Approved = "Approved";
    /// <summary>Work finished, area made safe, permit handed back.</summary>
    public const string Closed = "Closed";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";
}

/// <summary>
/// A permit to work for a high-risk job: what is dangerous, the precautions checked before the
/// work, who authorised it and for how long, and the close-out when the area is made safe again.
/// </summary>
public sealed class WorkPermit : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string PermitNumber { get; set; } = string.Empty;
    public Guid WorkOrderId { get; set; }
    public string PermitType { get; set; } = PermitTypes.General;
    public string Status { get; set; } = PermitStatuses.Requested;
    public string? Hazards { get; set; }
    public string? WorkDescription { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public string? IssuedToName { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public string? RequestedByName { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? DecisionNote { get; set; }
    /// <summary>Confined space: the gas test before entry (O₂ %, LEL %, CO ppm, H₂S ppm).</summary>
    public string? GasTest { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? ClosedByName { get; set; }
    public string? CloseOutNotes { get; set; }

    public WorkOrder? WorkOrder { get; set; }
    public ICollection<WorkPermitCheck> Checks { get; set; } = new List<WorkPermitCheck>();
}

/// <summary>A precaution on the permit, ticked before it is authorised.</summary>
public sealed class WorkPermitCheck : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid PermitId { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public bool Done { get; set; }
    public string? DoneByName { get; set; }

    public WorkPermit? Permit { get; set; }
}

public static class EnergyTypes
{
    public static readonly IReadOnlyList<string> All = ["Electrical", "Mechanical", "Pneumatic", "Hydraulic", "Steam", "Thermal", "Gravity", "Chemical"];

    public static string? Find(string? value) => All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>A point where an asset's energy is isolated and locked: a breaker, valve or pin.</summary>
public sealed class IsolationPoint : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid AssetId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string EnergyType { get; set; } = "Electrical";
    public string Description { get; set; } = string.Empty;
    public string? Location { get; set; }
    /// <summary>How to isolate and prove it dead.</summary>
    public string? Method { get; set; }
    public int Sequence { get; set; }
    public bool IsActive { get; set; } = true;

    public Asset? Asset { get; set; }
}

/// <summary>A lock put on an isolation point for a job, and its removal.</summary>
public sealed class LotoLock : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid? PermitId { get; set; }
    public Guid IsolationPointId { get; set; }
    public string LockNumber { get; set; } = string.Empty;
    public string AppliedByName { get; set; } = string.Empty;
    public DateTime AppliedAt { get; set; }
    /// <summary>Tried to start it and proved zero energy.</summary>
    public bool ZeroEnergyVerified { get; set; }
    public string? RemovedByName { get; set; }
    public DateTime? RemovedAt { get; set; }

    public WorkOrder? WorkOrder { get; set; }
    public IsolationPoint? IsolationPoint { get; set; }
}

public static class IncidentKinds
{
    public const string Injury = "Injury";
    public const string NearMiss = "NearMiss";
    public const string PropertyDamage = "PropertyDamage";
    public const string Fire = "Fire";
    public const string Environmental = "Environmental";

    public static readonly IReadOnlyList<string> All = [Injury, NearMiss, PropertyDamage, Fire, Environmental];
}

public static class IncidentSeverities
{
    public static readonly IReadOnlyList<string> All = ["Minor", "Moderate", "Serious", "Fatal"];
}

public static class IncidentStatuses
{
    public const string Open = "Open";
    public const string Investigating = "Investigating";
    public const string Closed = "Closed";
}

/// <summary>An accident, near miss or damage, linked to the asset and the job raised to put it right.</summary>
public sealed class Incident : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string IncidentNumber { get; set; } = string.Empty;
    public string Kind { get; set; } = IncidentKinds.NearMiss;
    public string Severity { get; set; } = "Minor";
    public DateTime OccurredAt { get; set; }
    public Guid OrgNodeId { get; set; }
    public Guid? AssetId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? PersonsInvolved { get; set; }
    public string? ImmediateAction { get; set; }
    public string? RootCause { get; set; }
    public string? Lessons { get; set; }
    /// <summary>Lost days, for injuries (lost-time injury frequency).</summary>
    public int? DaysLost { get; set; }
    /// <summary>Reported to DOSH (OSHA 2007 s.21 for injuries and dangerous occurrences).</summary>
    public bool ReportedToAuthority { get; set; }
    public string Status { get; set; } = IncidentStatuses.Open;
    public string ReportedByName { get; set; } = string.Empty;
    public Guid? CorrectiveWorkOrderId { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? ClosedByName { get; set; }

    /// <summary>Safety (accidents, near misses) or Security (theft, intrusion, patrol findings). One register for both.</summary>
    public string Category { get; set; } = IncidentCategories.Safety;
    /// <summary>Security: who is dealing with it.</summary>
    public Guid? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    /// <summary>Security: Reported → Responding → Contained → Closed.</summary>
    public string? ResponseStatus { get; set; }
    public Guid? CheckpointId { get; set; }
    public Guid? PatrolRunId { get; set; }
    public Guid? VisitId { get; set; }
    /// <summary>Kenya Police Occurrence Book number, when it was reported to the police.</summary>
    public string? PoliceObNumber { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public OrgNode? OrgNode { get; set; }
    public Asset? Asset { get; set; }
    public WorkOrder? CorrectiveWorkOrder { get; set; }
}

/// <summary>Training a person completed: safety induction, a skill course, a refresher. Technicians or contractors.</summary>
public sealed class TrainingRecord : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? TechnicianId { get; set; }
    /// <summary>For contractors and others without a technician record.</summary>
    public string PersonName { get; set; } = string.Empty;
    public string? Company { get; set; }
    public bool IsContractor { get; set; }
    public string Course { get; set; } = string.Empty;
    public string Category { get; set; } = "Induction";
    public DateTime CompletedOn { get; set; }
    public DateTime? ExpiresOn { get; set; }
    public string? Trainer { get; set; }
    public string? Notes { get; set; }

    public Technician? Technician { get; set; }
}

/// <summary>Protective equipment handed to a person.</summary>
public sealed class PpeIssue : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? TechnicianId { get; set; }
    public string PersonName { get; set; } = string.Empty;
    public string Item { get; set; } = string.Empty;
    public string? Size { get; set; }
    public int Quantity { get; set; } = 1;
    public DateTime IssuedOn { get; set; }
    public string? IssuedByName { get; set; }
    /// <summary>When it should be replaced (e.g. a harness after 5 years).</summary>
    public DateTime? ReplaceBy { get; set; }
    public string? Notes { get; set; }

    public Technician? Technician { get; set; }
}
