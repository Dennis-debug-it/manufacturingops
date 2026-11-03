using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class ImportBatch : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string ImportType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int SuccessfulRows { get; set; }
    public int FailedRows { get; set; }
    public string Status { get; set; } = string.Empty;

    public Tenant? Tenant { get; set; }
}
