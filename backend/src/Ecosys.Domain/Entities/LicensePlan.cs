using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class LicensePlan : AuditableEntity
{
    public string PlanCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal MonthlyPrice { get; set; }
    public decimal AnnualPrice { get; set; }
    public int? MaxUsers { get; set; }
    public int? MaxBranches { get; set; }
    public int? MaxAssets { get; set; }
    public int? MonthlyWorkOrders { get; set; }
    public string? ModulesIncluded { get; set; }
    public bool EmailIngestion { get; set; }
    public bool MonitoringIntegration { get; set; }
    public bool AdvancedReports { get; set; }
    public bool ClientPortal { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<TenantLicense> TenantLicenses { get; set; } = new List<TenantLicense>();
}
