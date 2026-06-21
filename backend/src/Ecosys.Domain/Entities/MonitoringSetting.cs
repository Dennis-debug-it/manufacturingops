using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class MonitoringSetting : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string ProviderName { get; set; } = "Generic";
    public string? EndpointLabel { get; set; }
    public string? WebhookSecret { get; set; }
    public Guid? DefaultBranchId { get; set; }
    public string DefaultPriority { get; set; } = "Medium";
    public bool AutoCreateWorkOrders { get; set; } = true;
    public bool IsEnabled { get; set; }

    public Tenant? Tenant { get; set; }
    public Branch? DefaultBranch { get; set; }
}
