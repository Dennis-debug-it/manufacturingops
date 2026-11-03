using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class TenantSecurityPolicy : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public int MinPasswordLength { get; set; } = 8;
    public bool RequireUppercase { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireDigit { get; set; } = true;
    public bool RequireSpecialCharacter { get; set; }
    public int PasswordRotationDays { get; set; } = 90;
    public int SessionTimeoutMinutes { get; set; } = 60;
    public bool RequireMfa { get; set; }

    public Tenant? Tenant { get; set; }
}
