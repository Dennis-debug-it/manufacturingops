using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class MonitoringWebhookIntegration : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ToolType { get; set; } = "Generic Webhook";
    public string EndpointSlug { get; set; } = string.Empty;
    public string SecretHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public Guid? DefaultClientId { get; set; }
    public Guid? DefaultAssetId { get; set; }
    public Guid? DefaultBranchId { get; set; }
    public Guid? DefaultAssignmentGroupId { get; set; }
    public string DefaultPriority { get; set; } = "Medium";
    public bool CreateWorkOrderOnAlert { get; set; } = true;
    public string? PayloadMappingJson { get; set; }
    public DateTime? LastReceivedAt { get; set; }
    public string? LastStatus { get; set; }
    public string? LastError { get; set; }

    public Tenant? Tenant { get; set; }
    public Client? DefaultClient { get; set; }
    public Asset? DefaultAsset { get; set; }
    public Branch? DefaultBranch { get; set; }
    public AssignmentGroup? DefaultAssignmentGroup { get; set; }
}
