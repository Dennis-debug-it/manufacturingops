using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class MaterialIssueLine : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid MaterialIssueId { get; set; }
    public Guid MaterialItemId { get; set; }
    public decimal QuantityRequested { get; set; }
    public decimal QuantityIssued { get; set; }
    public decimal QuantityReturned { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal? UnitCost { get; set; }
    public string? Notes { get; set; }

    public MaterialIssue? MaterialIssue { get; set; }
    public MaterialItem? MaterialItem { get; set; }
}
