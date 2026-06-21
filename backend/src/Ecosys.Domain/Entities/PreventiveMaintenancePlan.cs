using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class PreventiveMaintenancePlan : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid AssetId { get; set; }
    public Guid? SiteId { get; set; }
    public Guid? PmTemplateId { get; set; }
    public Guid? DefaultAssignmentGroupId { get; set; }

    public string TriggerType { get; set; } = "Calendar";
    public string Frequency { get; set; } = string.Empty;
    public string FrequencyUnit { get; set; } = "Monthly";
    public int FrequencyInterval { get; set; } = 1;
    public int? PreferredDayOfWeek { get; set; }
    public int? PreferredDayOfMonth { get; set; }
    public decimal? MeterInterval { get; set; }
    public decimal? MeterBuffer { get; set; }
    public int DaysBeforeDue { get; set; } = 3;

    public bool AutoSchedule { get; set; }
    public bool AutoAssign { get; set; }
    public bool NotifyOnGeneration { get; set; }

    public DateTime? LastPmDate { get; set; }
    public DateTime? NextPmDate { get; set; }
    public DateTime? LastGeneratedAt { get; set; }
    public Guid? LastPmWorkOrderId { get; set; }

    public string Status { get; set; } = "Active";

    public Tenant? Tenant { get; set; }
    public Branch? Branch { get; set; }
    public Asset? Asset { get; set; }
    public Site? Site { get; set; }
    public PmTemplate? PmTemplate { get; set; }
    public AssignmentGroup? DefaultAssignmentGroup { get; set; }
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
}
