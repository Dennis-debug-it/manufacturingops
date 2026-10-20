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

    public Tenant? Tenant { get; set; }
    public Client? Client { get; set; }
    public Site? Site { get; set; }
    public ICollection<SalesActivity> Activities { get; set; } = new List<SalesActivity>();
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
    public ICollection<TenantQuotation> Quotations { get; set; } = new List<TenantQuotation>();
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
    public Guid? LoggedByUserId { get; set; }
    public string? LoggedByName { get; set; }

    public SalesOpportunity? Opportunity { get; set; }
}
