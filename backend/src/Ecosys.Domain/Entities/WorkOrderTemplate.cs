using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class WorkOrderTemplate : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string JobType { get; set; } = "CorrectiveMaintenance";
    public string? Description { get; set; }
    public int? EstimatedDurationMins { get; set; }
    public string DefaultPriority { get; set; } = "Medium";
    public string? RequiredSkillsJson { get; set; }
    public Guid? ChecklistTemplateId { get; set; }
    public string? DefaultMaterialsJson { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
    public PmTemplate? ChecklistTemplate { get; set; }
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
}
