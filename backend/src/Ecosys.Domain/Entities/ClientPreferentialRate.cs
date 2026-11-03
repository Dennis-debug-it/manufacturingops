using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class ClientPreferentialRate : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ClientId { get; set; }
    public string RateName { get; set; } = string.Empty;
    public string RateType { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal DefaultRate { get; set; }
    public decimal PreferentialRate { get; set; }
    public string Currency { get; set; } = "KES";
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string Status { get; set; } = "Active";
    public bool RequiresApproval { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public Tenant? Tenant { get; set; }
    public Client? Client { get; set; }
    public User? CreatedByUser { get; set; }
    public User? ApprovedByUser { get; set; }
}
