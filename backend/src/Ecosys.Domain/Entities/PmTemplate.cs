using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class PmTemplate : AuditableEntity
{
    public Guid? TenantId { get; set; }
    public Guid? TemplatePackId { get; set; }
    public Guid? SourceTemplateId { get; set; }
    public Guid? AssetCategoryId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public string WorkType { get; set; } = "PreventiveMaintenance";
    public int? DefaultIntervalMonths { get; set; }
    public decimal? EstimatedDurationHours { get; set; }
    public bool AutoScheduleByDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
    public bool IsPlatformSeed { get; set; }
    public bool IsSystemTemplate { get; set; }
    public string? OptionalReportSectionsJson { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public Tenant? Tenant { get; set; }
    public SystemTemplatePack? TemplatePack { get; set; }
    public AssetCategory? AssetCategory { get; set; }
    public ICollection<PmTemplateSection> Sections { get; set; } = new List<PmTemplateSection>();
    public ICollection<PmTemplateQuestion> Questions { get; set; } = new List<PmTemplateQuestion>();
    public ICollection<PreventiveMaintenancePlan> Plans { get; set; } = new List<PreventiveMaintenancePlan>();
    public ICollection<PmReport> Reports { get; set; } = new List<PmReport>();
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
}
