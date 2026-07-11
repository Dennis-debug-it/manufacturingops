using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// A van is a mobile stock-holding location, typically assigned to a technician and based at a
/// home branch. Stock is loaded onto a van from a branch, returned to a branch, or consumed on a job.
/// </summary>
public sealed class Van : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? RegistrationNumber { get; set; }
    public Guid? TechnicianId { get; set; }
    public Guid? HomeBranchId { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
    public Technician? Technician { get; set; }
    public Branch? HomeBranch { get; set; }
    public ICollection<VanMaterialStock> MaterialStocks { get; set; } = new List<VanMaterialStock>();
}
