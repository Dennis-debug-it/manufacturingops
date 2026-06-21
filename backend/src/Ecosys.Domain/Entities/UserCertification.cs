using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class UserCertification : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid CertificationDefinitionId { get; set; }
    public string? CertificateNumber { get; set; }
    public DateTime? IssuedOn { get; set; }
    public DateTime? ExpiresOn { get; set; }
    public string? Notes { get; set; }

    public Tenant? Tenant { get; set; }
    public User? User { get; set; }
    public CertificationDefinition? CertificationDefinition { get; set; }
}
