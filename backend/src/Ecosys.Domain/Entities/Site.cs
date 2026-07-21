using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class Site : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ClientId { get; set; }
    public string SiteCode { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public string SiteType { get; set; } = "Branch";
    public string Status { get; set; } = "Active";

    public string? StreetAddress { get; set; }
    public string? AreaEstate { get; set; }
    public string? TownCity { get; set; }
    public string? County { get; set; }
    public string? Country { get; set; }
    public string? Region { get; set; }

    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public string? AlternateContact { get; set; }

    public string? OperatingHours { get; set; }
    public string? AccessNotes { get; set; }
    public string? SpecialInstructions { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    public Tenant? Tenant { get; set; }
    public Client? Client { get; set; }
    public ICollection<OperationalSystem> Systems { get; set; } = new List<OperationalSystem>();
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
    public ICollection<ContractSite> ContractSites { get; set; } = new List<ContractSite>();
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
}
