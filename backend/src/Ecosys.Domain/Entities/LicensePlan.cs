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
    /// <summary>No longer read: email intake is included with the Operations module. Kept so existing data loads.</summary>
    public bool EmailIngestion { get; set; }
    public bool MonitoringIntegration { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<TenantLicense> TenantLicenses { get; set; } = new List<TenantLicense>();
}
