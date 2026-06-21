using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class MaterialRequestLine : AuditableEntity
{
    public Guid MaterialRequestId { get; set; }
    public Guid MaterialItemId { get; set; }
    public decimal QuantityRequested { get; set; }
    public decimal QuantityIssued { get; set; }
    public decimal QuantityUsed { get; set; }
    public decimal QuantityReturned { get; set; }

    public MaterialRequest? MaterialRequest { get; set; }
    public MaterialItem? MaterialItem { get; set; }
}
