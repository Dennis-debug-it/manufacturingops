using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// One trip a technician makes to a work order's site: travel start, arrival, departure. A job that
/// spans several days has one visit per day (or per trip), so travel and on-site time can be totalled
/// per technician across the whole job. Written by the existing en-route / arrival / departure actions.
/// </summary>
public sealed class WorkOrderVisit : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid TechnicianId { get; set; }
    public DateTime? TravelStartedAt { get; set; }
    public DateTime? ArrivedAt { get; set; }
    public DateTime? DepartedAt { get; set; }
    /// <summary>Travel start → arrival, set on arrival.</summary>
    public int TravelMinutes { get; set; }
    /// <summary>Arrival → departure, set on departure.</summary>
    public int OnSiteMinutes { get; set; }
    public string? Notes { get; set; }

    public Tenant? Tenant { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public Technician? Technician { get; set; }
}
