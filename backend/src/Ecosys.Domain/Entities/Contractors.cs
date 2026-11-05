using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public static class ContractorStatuses
{
    public const string Approved = "Approved";
    /// <summary>Being vetted: cannot be given jobs yet.</summary>
    public const string Pending = "Pending";
    public const string Suspended = "Suspended";

    public static readonly IReadOnlyList<string> All = [Approved, Pending, Suspended];

    public static string? Find(string? value) => All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>An outside firm that does maintenance work: boiler specialists, electrical contractors, re-cutters, lift inspectors.</summary>
public sealed class Contractor : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>What they do, e.g. "Boiler servicing, refractory".</summary>
    public string? Services { get; set; }
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? TaxPin { get; set; }
    public string Status { get; set; } = ContractorStatuses.Approved;
    /// <summary>The supplier record used to raise purchase orders to this contractor.</summary>
    public Guid? SupplierId { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public string? ExternalId { get; set; }
    public string? ExternalSystem { get; set; }

    public Tenant? Tenant { get; set; }
    public Supplier? Supplier { get; set; }
    public ICollection<ContractorDocument> Documents { get; set; } = new List<ContractorDocument>();
}

public static class ContractorDocumentTypes
{
    public const string Insurance = "Insurance";
    public const string TaxCompliance = "Tax compliance";
    public const string NcaRegistration = "NCA registration";
    public const string DoshApproval = "DOSH approval";
    public const string TradeLicence = "Trade licence";
    public const string Other = "Other";

    public static readonly IReadOnlyList<string> All = [Insurance, TaxCompliance, NcaRegistration, DoshApproval, TradeLicence, Other];

    /// <summary>Without a current one of these a contractor cannot be given work.</summary>
    public static readonly IReadOnlySet<string> Required = new HashSet<string> { Insurance, TaxCompliance };

    public static string? Find(string? value) => All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>A compliance document a contractor holds (insurance, tax compliance, NCA, DOSH approval), with its expiry.</summary>
public sealed class ContractorDocument : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ContractorId { get; set; }
    public string DocumentType { get; set; } = ContractorDocumentTypes.Other;
    public string? Reference { get; set; }
    public DateTime? IssuedOn { get; set; }
    public DateTime? ExpiresOn { get; set; }
    public Guid? AttachmentId { get; set; }
    public string? Notes { get; set; }
    /// <summary>The last expiry alert step sent (30, 7, 0).</summary>
    public int? LastAlertDays { get; set; }

    public Contractor? Contractor { get; set; }
}

public static class AgreementStatuses
{
    public const string Active = "Active";
    public const string Expired = "Expired";
    public const string Cancelled = "Cancelled";
}

/// <summary>
/// An annual maintenance contract (AMC) with a contractor: what it covers, its response and repair
/// times, how often they visit, and when it runs out.
/// </summary>
public sealed class MaintenanceAgreement : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string AgreementNumber { get; set; } = string.Empty;
    public Guid ContractorId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Scope { get; set; }
    public Guid? FactoryId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal? Value { get; set; }
    public string Currency { get; set; } = "KES";
    /// <summary>SLA: hours to respond and to fix, applied to jobs given to the contractor under this agreement.</summary>
    public int? ResponseHours { get; set; }
    public int? RepairHours { get; set; }
    /// <summary>A scheduled visit every N months (1 monthly, 3 quarterly…); null for call-out only.</summary>
    public int? VisitEveryMonths { get; set; }
    public DateTime? NextVisitDue { get; set; }
    /// <summary>Alert this many days before the end, to renew or retender.</summary>
    public int RenewalNoticeDays { get; set; } = 60;
    public DateTime? RenewalAlertSentAt { get; set; }
    public string Status { get; set; } = AgreementStatuses.Active;

    public Contractor? Contractor { get; set; }
    public OrgNode? Factory { get; set; }
    public ICollection<MaintenanceAgreementAsset> Assets { get; set; } = new List<MaintenanceAgreementAsset>();
}

public sealed class MaintenanceAgreementAsset : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid AgreementId { get; set; }
    public Guid AssetId { get; set; }

    public MaintenanceAgreement? Agreement { get; set; }
    public Asset? Asset { get; set; }
}

public static class ContractorInvoiceStatuses
{
    public const string Received = "Received";
    /// <summary>Every line matched a completed job of this contractor.</summary>
    public const string Matched = "Matched";
    /// <summary>Something did not match; see the issues.</summary>
    public const string Query = "Query";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

/// <summary>A contractor's invoice, matched line by line to the jobs they completed before it is approved for payment.</summary>
public sealed class ContractorInvoice : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ContractorId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public string Currency { get; set; } = "KES";
    public decimal Amount { get; set; }
    public string Status { get; set; } = ContractorInvoiceStatuses.Received;
    /// <summary>Why it did not match, one issue per line.</summary>
    public string? MatchIssues { get; set; }
    public Guid? AttachmentId { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecidedByName { get; set; }
    public string? DecisionNote { get; set; }

    public Contractor? Contractor { get; set; }
    public ICollection<ContractorInvoiceLine> Lines { get; set; } = new List<ContractorInvoiceLine>();
}

public sealed class ContractorInvoiceLine : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }

    public ContractorInvoice? Invoice { get; set; }
    public WorkOrder? WorkOrder { get; set; }
}
