using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class CertificationDefinition : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Issuer { get; set; }
    public string? Description { get; set; }
    public int? RenewalPeriodMonths { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
    public ICollection<UserCertification> Users { get; set; } = new List<UserCertification>();
}
