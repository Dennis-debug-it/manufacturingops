using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class TenantOperationsPolicy : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public bool RequireTechnicianSignature { get; set; } = true;
    public bool RequireClientSignature { get; set; } = true;
    public bool AllowRemoteCompletion { get; set; } = false;

    public Tenant? Tenant { get; set; }
}
