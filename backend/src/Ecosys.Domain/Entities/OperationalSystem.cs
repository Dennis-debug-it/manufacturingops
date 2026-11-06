using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class OperationalSystem : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ClientId { get; set; }
    public Guid SiteId { get; set; }
    public string SystemName { get; set; } = string.Empty;
    public string SystemType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "Active";
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? InstallationDate { get; set; }
    public string? Notes { get; set; }

    public Tenant? Tenant { get; set; }
    public Client? Client { get; set; }
    public Site? Site { get; set; }
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
    public ICollection<MaterialIssue> MaterialIssues { get; set; } = new List<MaterialIssue>();
}
