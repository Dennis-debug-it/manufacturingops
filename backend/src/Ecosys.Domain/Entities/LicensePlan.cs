using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class LicensePlan : AuditableEntity
{
    public string PlanCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal MonthlyPrice { get; set; }
    public decimal AnnualPrice { get; set; }
    public int? MaxBranches { get; set; }
    public string? ModulesIncluded { get; set; }
    public bool EmailIngestion { get; set; }
    public bool MonitoringIntegration { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<TenantLicense> TenantLicenses { get; set; } = new List<TenantLicense>();
}
