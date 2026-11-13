using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public static class AccessZoneLevels
{
    /// <summary>Reception, canteen, car park: every approved visitor may go there.</summary>
    public const string Public = "Public";
    /// <summary>Weighbridge, leaf receiving, stores: only visitors given the zone.</summary>
    public const string Controlled = "Controlled";
    /// <summary>Processing, boiler house, fuel store, substation: given the zone, and usually induction, PPE or a permit.</summary>
    public const string Restricted = "Restricted";

    public static readonly IReadOnlyList<string> All = [Public, Controlled, Restricted];

    public static string? Find(string? value) => All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// An area of a factory with its own entry rules: who may go in, and whether they need an escort,
/// a current safety induction, PPE or a permit to work. Visits list the zones they may enter;
/// security patrols use the same zones.
/// </summary>
public sealed class AccessZone : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    /// <summary>The factory the zone is in.</summary>
    public Guid FactoryId { get; set; }
    /// <summary>The section the zone matches, when it matches one.</summary>
    public Guid? OrgNodeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Level { get; set; } = AccessZoneLevels.Controlled;
    public bool RequiresEscort { get; set; }
    public bool RequiresInduction { get; set; }
    /// <summary>Only with an approved permit to work that is valid now.</summary>
    public bool RequiresPermit { get; set; }
    /// <summary>PPE to confirm at the gate, comma separated, e.g. "Hairnet, Safety boots".</summary>
    public string? RequiredPpe { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public OrgNode? Factory { get; set; }
    public OrgNode? OrgNode { get; set; }
}

/// <summary>
/// A person who visits, kept across visits so a repeat visitor is found by phone or ID. ID numbers are
/// never stored in full (Kenya Data Protection Act 2019): only the last four characters and a salted hash.
/// </summary>
public sealed class Visitor : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Company { get; set; }
    /// <summary>National ID, Passport, Alien ID, Driving licence.</summary>
    public string? IdType { get; set; }
    /// <summary>e.g. "••••5678".</summary>
    public string? IdNumberMasked { get; set; }
    /// <summary>Salted SHA-256 of the normalised ID number, to recognise a returning visitor.</summary>
    public string? IdNumberHash { get; set; }
    /// <summary>The contractor firm the person works for, when they come to do maintenance work.</summary>
    public Guid? ContractorId { get; set; }
    public bool IsBlocked { get; set; }
    public string? BlockedReason { get; set; }
    public string? BlockedByName { get; set; }
    public DateTime? LastVisitAt { get; set; }
    /// <summary>Set when the personal details were removed after the retention period.</summary>
    public DateTime? AnonymisedAt { get; set; }
    public Contractor? Contractor { get; set; }
}

public static class VisitStatuses
{
    /// <summary>Waiting for the host (a walk-in, or a visit raised by someone other than the host).</summary>
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    /// <summary>At least one person on the visit is inside.</summary>
    public const string OnSite = "OnSite";
    public const string Completed = "Completed";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlyList<string> All = [PendingApproval, Approved, OnSite, Completed, Rejected, Cancelled];
    public static readonly IReadOnlyList<string> Open = [PendingApproval, Approved, OnSite];
}

public static class VisitPurposes
{
    public const string Meeting = "Meeting";
    public const string Delivery = "Delivery";
    public const string ContractorWork = "Contractor work";
    public const string Inspection = "Inspection";
    public const string Audit = "Audit";
    public const string Interview = "Interview";
    public const string Other = "Other";

    public static readonly IReadOnlyList<string> All = [Meeting, Delivery, ContractorWork, Inspection, Audit, Interview, Other];

    public static string? Find(string? value) => All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

public static class VisitSources
{
    /// <summary>Raised ahead by the host or an office user.</summary>
    public const string PreRegistered = "PreRegistered";
    /// <summary>Raised at the gate when the visitor arrived.</summary>
    public const string WalkIn = "WalkIn";
    /// <summary>Raised from a standing pass when its holder arrived.</summary>
    public const string Pass = "Pass";
}

/// <summary>A visit to one factory by one or more people, for a host, within an expected time.</summary>
public sealed class Visit : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string VisitNumber { get; set; } = string.Empty;
    public Guid FactoryId { get; set; }
    public string Purpose { get; set; } = VisitPurposes.Meeting;
    public Guid HostUserId { get; set; }
    public string HostName { get; set; } = string.Empty;
    public DateTime ExpectedFrom { get; set; }
    public DateTime ExpectedTo { get; set; }
    public string Status { get; set; } = VisitStatuses.PendingApproval;
    public string Source { get; set; } = VisitSources.PreRegistered;
    /// <summary>Contractor work: the job they come to do.</summary>
    public Guid? WorkOrderId { get; set; }
    /// <summary>The permit that lets them into a zone that needs one.</summary>
    public Guid? WorkPermitId { get; set; }
    public Guid? ContractorId { get; set; }
    public string? VehicleRegistration { get; set; }
    /// <summary>The standing pass this visit was raised from.</summary>
    public Guid? PassId { get; set; }
    public string? Notes { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public Guid? DecidedByUserId { get; set; }
    public string? DecidedByName { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionNote { get; set; }
    public DateTime? CompletedAt { get; set; }
    public OrgNode? Factory { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public WorkPermit? WorkPermit { get; set; }
    public Contractor? Contractor { get; set; }
    public ICollection<VisitAttendee> Attendees { get; set; } = new List<VisitAttendee>();
    public ICollection<VisitZone> Zones { get; set; } = new List<VisitZone>();
}

/// <summary>One person on a visit: their badge, when they came in and left, and what the gate confirmed.</summary>
public sealed class VisitAttendee : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VisitId { get; set; }
    public Guid VisitorId { get; set; }
    /// <summary>Printed on the badge, e.g. "VIS-000123-1".</summary>
    public string BadgeNumber { get; set; } = string.Empty;
    /// <summary>Random value in the badge QR code; carries no personal data.</summary>
    public string BadgeToken { get; set; } = string.Empty;
    public DateTime? CheckedInAt { get; set; }
    public string? CheckedInByName { get; set; }
    public DateTime? CheckedOutAt { get; set; }
    public string? CheckedOutByName { get; set; }
    public bool IdVerified { get; set; }
    public bool InductionConfirmed { get; set; }
    public bool PpeConfirmed { get; set; }
    public Guid? EscortUserId { get; set; }
    public string? EscortName { get; set; }
    /// <summary>Why the gate let them in although a check failed, and who decided.</summary>
    public string? OverrideReason { get; set; }
    public string? OverriddenByName { get; set; }
    /// <summary>Emergency roll call: marked as accounted for at the assembly point.</summary>
    public DateTime? AccountedForAt { get; set; }
    public string? AccountedForByName { get; set; }
    public Visit? Visit { get; set; }
    public Visitor? Visitor { get; set; }
}

/// <summary>A zone a visit may enter, beyond the factory's public zones.</summary>
public sealed class VisitZone : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VisitId { get; set; }
    public Guid AccessZoneId { get; set; }
    public Visit? Visit { get; set; }
    public AccessZone? AccessZone { get; set; }
}

public static class VehiclePurposes
{
    public const string Visitor = "Visitor";
    public const string Delivery = "Delivery";
    public const string Contractor = "Contractor";
    /// <summary>Collecting made tea, firewood suppliers, waste removal.</summary>
    public const string Collection = "Collection";
    public const string Staff = "Staff";
    public const string Other = "Other";

    public static readonly IReadOnlyList<string> All = [Visitor, Delivery, Contractor, Collection, Staff, Other];

    public static string? Find(string? value) => All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>A vehicle coming in through a factory gate and, later, going out.</summary>
public sealed class VehicleMovement : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid FactoryId { get; set; }
    /// <summary>Normalised, e.g. "KCA123A".</summary>
    public string Registration { get; set; } = string.Empty;
    public string Purpose { get; set; } = VehiclePurposes.Visitor;
    public string? Description { get; set; }
    public string? DriverName { get; set; }
    public string? DriverPhone { get; set; }
    /// <summary>The visit the vehicle came with, when there is one.</summary>
    public Guid? VisitId { get; set; }
    public DateTime EnteredAt { get; set; }
    public string EnteredByName { get; set; } = string.Empty;
    public DateTime? ExitedAt { get; set; }
    public string? ExitedByName { get; set; }
    /// <summary>What the gate saw: load, seal number, search done.</summary>
    public string? Notes { get; set; }
    public string? ExitNotes { get; set; }
    public OrgNode? Factory { get; set; }
    public Visit? Visit { get; set; }
}

public static class VisitorPassStatuses
{
    public const string Active = "Active";
    public const string Revoked = "Revoked";
}

/// <summary>
/// A standing pass for someone who comes regularly (a contractor on an annual agreement, a supplier's
/// driver): on the days and hours it allows, scanning its badge raises the day's visit, already
/// approved. Every gate check still runs each day: papers, induction, permit, blocklist.
/// </summary>
public sealed class VisitorPass : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string PassNumber { get; set; } = string.Empty;
    public Guid VisitorId { get; set; }
    public Guid FactoryId { get; set; }
    public Guid HostUserId { get; set; }
    public string HostName { get; set; } = string.Empty;
    public Guid? ContractorId { get; set; }
    public string Purpose { get; set; } = VisitPurposes.ContractorWork;
    /// <summary>First and last day (factory local dates).</summary>
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    /// <summary>ISO weekdays it is good for, comma separated: 1 = Monday … 7 = Sunday.</summary>
    public string Days { get; set; } = "1,2,3,4,5";
    /// <summary>Daily hours in factory local time.</summary>
    public TimeOnly DailyFrom { get; set; } = new(7, 0);
    public TimeOnly DailyTo { get; set; } = new(17, 0);
    /// <summary>Random value in the pass badge's QR code.</summary>
    public string BadgeToken { get; set; } = string.Empty;
    public string Status { get; set; } = VisitorPassStatuses.Active;
    public string? VehicleRegistration { get; set; }
    public string? Notes { get; set; }
    public string IssuedByName { get; set; } = string.Empty;
    public DateTime? RevokedAt { get; set; }
    public string? RevokedByName { get; set; }
    public string? RevokedReason { get; set; }
    public Visitor? Visitor { get; set; }
    public OrgNode? Factory { get; set; }
    public Contractor? Contractor { get; set; }
    public ICollection<VisitorPassZone> Zones { get; set; } = new List<VisitorPassZone>();
}

public sealed class VisitorPassZone : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid PassId { get; set; }
    public Guid AccessZoneId { get; set; }
    public VisitorPass? Pass { get; set; }
    public AccessZone? AccessZone { get; set; }
}
