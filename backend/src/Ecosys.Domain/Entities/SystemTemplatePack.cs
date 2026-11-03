using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class SystemTemplatePack : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ContractorType { get; set; } = string.Empty;
    public bool IsSystem { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;

    public ICollection<AssetCategory> AssetCategories { get; set; } = new List<AssetCategory>();
    public ICollection<PmTemplate> ChecklistTemplates { get; set; } = new List<PmTemplate>();
    public ICollection<TenantTemplatePackInstallation> Installations { get; set; } = new List<TenantTemplatePackInstallation>();
}
