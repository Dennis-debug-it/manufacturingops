using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// A tenant's sales deal: a prospect or existing client the tenant is trying to win work from.
/// Distinct from the platform CRM (<see cref="CrmContact"/>, <see cref="PlatformLead"/>), which is
/// Ecosys selling to tenants. A won opportunity is the hand-off point into operations: it becomes a
/// client (when it was a prospect), a quotation and/or one or more work orders, each of which keeps
/// a <c>SalesOpportunityId</c> back-link.
/// </summary>
public sealed class SalesOpportunity : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Stage { get; set; } = "Lead"; // Lead|Qualified|SiteSurvey|Proposal|Negotiation|Won|Lost

    /// <summary>Set once the deal is tied to a client record; null while it is still a prospect.</summary>
    public Guid? ClientId { get; set; }
    public Guid? SiteId { get; set; }

    // Prospect details, used to create the client when the deal is won.
    public string? CompanyName { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Location { get; set; }

    public string? Source { get; set; } // Referral|Website|Walk-in|Cold call|Tender|Existing client|Other
    /// <summary>The kind of job this deal turns into; used as the work type on conversion.</summary>
    public string? ServiceType { get; set; }
    public string? Description { get; set; }
    public decimal EstimatedValue { get; set; }
    public string Currency { get; set; } = "KES";
    public int Probability { get; set; }
    public DateTime? ExpectedCloseDate { get; set; }
    public Guid? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public string? LostReason { get; set; }
    public DateTime? WonAt { get; set; }
    public DateTime? LostAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
    /// <summary>Set on a renewal deal: the contract it renews. Renewing that contract wins the deal; letting it lapse loses it.</summary>
    public Guid? RenewalOfContractId { get; set; }
    /// <summary>Set on a lead a technician flagged from a job: the work order it was raised on.</summary>
    public Guid? SourceWorkOrderId { get; set; }

    public Tenant? Tenant { get; set; }
    public Client? Client { get; set; }
    public Site? Site { get; set; }
    public ICollection<SalesActivity> Activities { get; set; } = new List<SalesActivity>();
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
    public ICollection<TenantQuotation> Quotations { get; set; } = new List<TenantQuotation>();
    public ICollection<SalesTender> Tenders { get; set; } = new List<SalesTender>();
}

/// <summary>A call, email, meeting, site visit, note or follow-up logged against an opportunity.</summary>
public sealed class SalesActivity : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid OpportunityId { get; set; }
    public string Type { get; set; } = "Note"; // Call|Email|Meeting|SiteVisit|Note|FollowUp|System
    public string Summary { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    /// <summary>For follow-ups: when it is due. Open until <see cref="CompletedAt"/> is set.</summary>
    public DateTime? DueAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    /// <summary>When the owner was last reminded about this follow-up, and how many times in all.</summary>
    public DateTime? ReminderSentAt { get; set; }
    public int ReminderCount { get; set; }
    public Guid? LoggedByUserId { get; set; }
    public string? LoggedByName { get; set; }

    public SalesOpportunity? Opportunity { get; set; }
}

/// <summary>
/// A request for proposal (RFP) or request for quotation (RFQ) the tenant has received from a client
/// or prospect. Tracked from receipt through the bid / no-bid decision and submission to the
/// award. A tender the tenant bids on usually gets an opportunity, so the award flows into the
/// same won-deal → work order hand-off; the outcome is mirrored onto that opportunity.
/// </summary>
public sealed class SalesTender : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Type { get; set; } = "RFQ"; // RFP|RFQ
    /// <summary>The issuer's own reference, e.g. "KPLC/RFQ/2026/114".</summary>
    public string? Reference { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = "Received"; // Received|Reviewing|Preparing|Submitted|Won|Lost|NoBid|Cancelled

    public Guid? ClientId { get; set; }
    public string? IssuerName { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClarificationDeadline { get; set; }
    public DateTime SubmissionDeadline { get; set; }
    /// <summary>How the bid is lodged: email, portal, hand delivery…</summary>
    public string? SubmissionMethod { get; set; }
    public bool BidBondRequired { get; set; }
    public decimal? BidBondAmount { get; set; }

    public string? Description { get; set; }
    public decimal EstimatedValue { get; set; }
    public string Currency { get; set; } = "KES";
    /// <summary>The amount actually bid, captured when the tender is marked submitted.</summary>
    public decimal? SubmittedValue { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    /// <summary>Why it was a no-bid, lost or cancelled; or award notes when won.</summary>
    public string? OutcomeNotes { get; set; }

    public Guid? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public Guid? OpportunityId { get; set; }
    /// <summary>
    /// Which deadline reminders have gone out: 0 none, 1 the three-day warning, 2 the final-day
    /// warning. Reset when the deadline moves, so a new date gets its own reminders.
    /// </summary>
    public int DeadlineReminderStage { get; set; }

    public Tenant? Tenant { get; set; }
    public Client? Client { get; set; }
    public SalesOpportunity? Opportunity { get; set; }
}

/// <summary>
/// Per-workspace sales CRM settings. A workspace with no row uses the defaults below, so nothing has
/// to be set up before the CRM works.
/// </summary>
public sealed class TenantSalesSettings : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    /// <summary>Who gets deals the system creates when no better owner can be found (renewals, technician leads).</summary>
    public Guid? DefaultOwnerUserId { get; set; }
    /// <summary>"All": everyone with CRM access sees every deal. "Own": people see their own deals unless they may see all.</summary>
    public string DealVisibility { get; set; } = "All";
    /// <summary>Open a renewal deal when a contract enters its renewal notice window.</summary>
    public bool CreateRenewalDeals { get; set; } = true;
}
