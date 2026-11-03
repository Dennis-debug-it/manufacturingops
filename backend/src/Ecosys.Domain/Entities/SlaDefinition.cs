using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class SlaDefinition : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }

    public Tenant? Tenant { get; set; }
    public ICollection<SlaRule> Rules { get; set; } = new List<SlaRule>();
    public ICollection<Client> Clients { get; set; } = new List<Client>();
}
