using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public static class Trades
{
    public const string Fitter = "Fitter";
    public const string Electrician = "Electrician";
    public const string BoilerOperator = "Boiler operator";
    public const string InstrumentTechnician = "Instrument technician";
    public const string Welder = "Welder";
    public const string Mechanic = "Mechanic";
    public const string General = "General hand";

    public static readonly IReadOnlyList<string> All = [Fitter, Electrician, BoilerOperator, InstrumentTechnician, Welder, Mechanic, General];

    public static string? Find(string? value) => All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>A skill a technician has, at a level (1 learning, 2 competent, 3 can train others).</summary>
public sealed class TechnicianSkill : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid TechnicianId { get; set; }
    public string Skill { get; set; } = string.Empty;
    public int Level { get; set; } = 2;

    public Technician? Technician { get; set; }
}

/// <summary>
/// A licence or ticket a technician holds (electrical licence, boiler operator, welding, working
/// at heights), with its expiry. The types are the tenant's certification definitions.
/// </summary>
public sealed class TechnicianCertificate : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid TechnicianId { get; set; }
    public Guid CertificationDefinitionId { get; set; }
    public string? CertificateNumber { get; set; }
    public DateTime? IssuedOn { get; set; }
    public DateTime? ExpiresOn { get; set; }
    public Guid? AttachmentId { get; set; }
    public string? Notes { get; set; }
    /// <summary>The last "expires in N days" alert sent (30, 7) for the current expiry, and whether the expired alert went.</summary>
    public int? LastAlertDays { get; set; }

    public Technician? Technician { get; set; }
    public CertificationDefinition? CertificationDefinition { get; set; }
}

public static class RosterKinds
{
    /// <summary>On a shift (the factory's shift name says which).</summary>
    public const string Shift = "Shift";
    /// <summary>Off, but on call for breakdowns.</summary>
    public const string OnCall = "OnCall";
    public const string Off = "Off";

    public static readonly IReadOnlyList<string> All = [Shift, OnCall, Off];
}

/// <summary>Where a technician is on a day: which shift, on call, or off.</summary>
public sealed class RosterEntry : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid TechnicianId { get; set; }
    public DateOnly WorkDate { get; set; }
    public string Kind { get; set; } = RosterKinds.Shift;
    public string? ShiftName { get; set; }
    public string? Note { get; set; }

    public Technician? Technician { get; set; }
}
