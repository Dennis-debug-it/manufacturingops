using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class RateCard : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "Labour";       // Labour|Callout|Service|Transport|Material|Other
    public string? Description { get; set; }
    public decimal UnitRate { get; set; }
    public string Unit { get; set; } = "hour";              // hour|visit|km|unit|day|job
    public string? SkillLevel { get; set; }                 // Junior|Mid|Senior|Specialist|null=Any
    public string TimeType { get; set; } = "Standard";     // Standard|Overtime|Emergency|Weekend|Holiday
    public bool IsDefault { get; set; } = false;
    public decimal? TaxRate { get; set; }                   // null = use tenant default
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
}
