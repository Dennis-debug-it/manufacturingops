using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class PartsRequestItem : AuditableEntity
{
    public Guid PartsRequestId { get; set; }
    public Guid? InventoryItemId { get; set; }
    public string? Description { get; set; }
    public decimal QuantityRequested { get; set; }
    public decimal? QuantityApproved { get; set; }

    public PartsRequest? PartsRequest { get; set; }
    public MaterialItem? InventoryItem { get; set; }
}
