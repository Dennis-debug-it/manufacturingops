using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class TenantTemplatePackInstallation : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid SourceTemplatePackId { get; set; }
    public Guid? InstalledByUserId { get; set; }
    public DateTime InstalledAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Installed";
    public string? Notes { get; set; }

    public Tenant? Tenant { get; set; }
    public SystemTemplatePack? SourceTemplatePack { get; set; }
}
