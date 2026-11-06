using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// Levels of the organisation tree, top to bottom. Assets hang off any node; the QR tag lives at
/// asset level. Levels a customer does not use are hidden through <see cref="FactorySettings"/>
/// rather than removed, so a single factory and a multi-factory group share one model.
/// </summary>
public static class OrgNodeLevels
{
    public const string Group = "Group";
    public const string Factory = "Factory";
    public const string Section = "Section";
    public const string SubSection = "SubSection";

    public static readonly IReadOnlyList<string> All = [Group, Factory, Section, SubSection];

    public static int Rank(string level) => level switch
    {
        Group => 0,
        Factory => 1,
        Section => 2,
        SubSection => 3,
        _ => -1
    };

    public static string? Normalize(string? level) =>
        All.FirstOrDefault(x => string.Equals(x, level?.Trim().Replace("-", string.Empty).Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// One node of the configurable group → factory → section → sub-section hierarchy
/// (scope §2 design rule 2: hierarchy as data, not fixed columns).
/// </summary>
public sealed class OrgNode : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? ParentId { get; set; }
    public string Level { get; set; } = OrgNodeLevels.Factory;
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Key of the template the node was created from (e.g. "tea"), so defaults can be told apart from customer edits.</summary>
    public string? TemplateKey { get; set; }

    /// <summary>Stable external identifiers for ERP mapping (scope §2 design rule 1).</summary>
    public string? ExternalId { get; set; }
    public string? ExternalSystem { get; set; }

    /// <summary>Factory level only: this factory's own weekly window day and on/off, instead of the tenant default.</summary>
    public DayOfWeek? WindowDay { get; set; }
    public bool? WindowEnabled { get; set; }

    public Tenant? Tenant { get; set; }
    public OrgNode? Parent { get; set; }
    public ICollection<OrgNode> Children { get; set; } = new List<OrgNode>();
    public ICollection<UserOrgNodeAssignment> UserAssignments { get; set; } = new List<UserOrgNodeAssignment>();
}

/// <summary>
/// Grants a user access to an org node and everything beneath it. A user with no assignments
/// sees the whole tenant (the default for admins and single-factory customers).
/// </summary>
public sealed class UserOrgNodeAssignment : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid OrgNodeId { get; set; }

    public Tenant? Tenant { get; set; }
    public User? User { get; set; }
    public OrgNode? OrgNode { get; set; }
}

/// <summary>
/// Tenant-wide factory maintenance settings (scope Phase 0: production unit, maintenance window
/// defaults, which hierarchy levels are shown). Nothing here is tea-specific: the production unit
/// is configurable so sugar, dairy or cement can use their own.
/// </summary>
public sealed class FactorySettings : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    /// <summary>Industry template pack the tenant started from, e.g. "tea".</summary>
    public string IndustryTemplate { get; set; } = "tea";
    public string ProductionUnit { get; set; } = "kg made tea";
    public string ProductionUnitShort { get; set; } = "kg MT";

    public bool UseGroupLevel { get; set; } = true;
    public bool UseSubSectionLevel { get; set; } = true;

    /// <summary>Default weekly maintenance window; factories can override it (Phase 3).</summary>
    public bool WeeklyWindowEnabled { get; set; } = true;
    public DayOfWeek WeeklyWindowDay { get; set; } = DayOfWeek.Monday;
    public TimeOnly WeeklyWindowStart { get; set; } = new(6, 0);
    public TimeOnly WeeklyWindowEnd { get; set; } = new(18, 0);

    /// <summary>Month numbers (1–12) of the usual low-crop overhaul season.</summary>
    public int? LowCropStartMonth { get; set; }
    public int? LowCropEndMonth { get; set; }

    /// <summary>Asset tags are numbered <c>{TagPrefix}-{000001}</c>; numbers are never reused.</summary>
    public string TagPrefix { get; set; } = "TAG";
    public int NextTagNumber { get; set; } = 1;

    /// <summary>New work orders wait for approval before they can be planned.</summary>
    public bool RequireWorkOrderApproval { get; set; } = true;
    /// <summary>Breakdowns skip approval: the machine is already down.</summary>
    public bool AutoApproveBreakdowns { get; set; } = true;
    /// <summary>The same fault on the same asset within this many days is flagged as rework.</summary>
    public int ReworkWindowDays { get; set; } = 30;

    /// <summary>Operators can report faults from an asset's QR label without logging in.</summary>
    public bool OperatorReportingEnabled { get; set; } = true;
    /// <summary>Secret in the label link that authorises an operator report; tag numbers alone are guessable.</summary>
    public string? OperatorReportKey { get; set; }

    /// <summary>Shifts as JSON [{"name":"Day","start":"06:00","end":"18:00"}, …]; running checks are due once per shift.</summary>
    public string ShiftsJson { get; set; } = "[{\"name\":\"Day\",\"start\":\"06:00\",\"end\":\"18:00\"},{\"name\":\"Night\",\"start\":\"18:00\",\"end\":\"06:00\"}]";

    /// <summary>Planning capacity: hours a technician has for planned work in a week (PM workload forecast).</summary>
    public decimal PmHoursPerTechnicianWeek { get; set; } = 30;

    /// <summary>A job carried over this many windows is escalated to supervisors…</summary>
    public int WindowCarryoverEscalateAt { get; set; } = 2;
    /// <summary>…and at this many it moves to the overhaul list.</summary>
    public int WindowCarryoverToOverhaulAt { get; set; } = 3;

    /// <summary>Value of one production unit (e.g. KES per kg made tea), to put a money figure on downtime.</summary>
    public decimal? ProductionUnitValue { get; set; }
    /// <summary>VAT rate (%) used where costs are entered gross.</summary>
    public decimal VatRatePercent { get; set; } = 16;
    /// <summary>Peak-season months (1–12, comma separated), e.g. the flush months; downtime is split peak vs low.</summary>
    public string PeakSeasonMonths { get; set; } = "3,4,5,10,11,12";
    /// <summary>Personal data of people who have left (contractors' training and PPE records, withdrawn consents) is anonymised after this many years.</summary>
    public int PersonalDataRetentionYears { get; set; } = 7;
    /// <summary>Where the tenant's data is hosted, stated for data protection notices.</summary>
    public string? DataResidency { get; set; }

    public Tenant? Tenant { get; set; }
}
