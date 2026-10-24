using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// An edition (Essentials, Professional or Enterprise). Which modules it includes is fixed in code
/// (<c>ServiceOpsPlans.EditionModules</c>); this row holds only what the platform can change: prices and
/// the minimum number of office seats. A price of zero means "not priced yet".
/// </summary>
public sealed class LicensePlan : AuditableEntity
{
    public string PlanCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal OfficeSeatMonthlyPrice { get; set; }
    public decimal OfficeSeatAnnualPrice { get; set; }
    public decimal FieldSeatMonthlyPrice { get; set; }
    public decimal FieldSeatAnnualPrice { get; set; }
    public int MinOfficeSeats { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    public bool IsPriced => OfficeSeatMonthlyPrice > 0 || OfficeSeatAnnualPrice > 0;

    public ICollection<TenantLicense> TenantLicenses { get; set; } = new List<TenantLicense>();
}
