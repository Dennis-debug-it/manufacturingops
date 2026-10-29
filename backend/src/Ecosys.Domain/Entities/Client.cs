using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class Client : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string? ClientType { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Location { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? SlaPlan { get; set; }
    public Guid? SlaDefinitionId { get; set; }
    public DateTime? ActiveStartDate { get; set; }
    public int? ActiveDurationValue { get; set; }
    public string? ActiveDurationUnit { get; set; }
    public DateTime? ActiveEndDate { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>
    /// A company sales is talking to that is not a customer yet. Prospects are left out of client
    /// pickers and operational lists; winning a deal or raising a work order makes them a client.
    /// </summary>
    public bool IsProspect { get; set; }

    // ── Credit terms ──────────────────────────────────────────────────────────
    public Guid? DefaultCreditTermId { get; set; }
    public decimal? CreditLimit { get; set; }         // client-level override of credit term limit
    public decimal CurrentBalance { get; set; } = 0;  // outstanding invoiced amount

    public Tenant? Tenant { get; set; }
    public SlaDefinition? SlaDefinition { get; set; }
    public CreditTerm? DefaultCreditTerm { get; set; }
    public ICollection<Site> Sites { get; set; } = new List<Site>();
    public ICollection<OperationalSystem> Systems { get; set; } = new List<OperationalSystem>();
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
    public ICollection<ClientPreferentialRate> PreferentialRates { get; set; } = new List<ClientPreferentialRate>();
    public ICollection<TenantInvoice> Invoices { get; set; } = new List<TenantInvoice>();
    public ICollection<ClientPortalUser> PortalUsers { get; set; } = new List<ClientPortalUser>();
}
