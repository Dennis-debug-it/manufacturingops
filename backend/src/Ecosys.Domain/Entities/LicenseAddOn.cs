using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// An optional add-on a tenant can take with any edition (today only the two WhatsApp add-ons). Every
/// price and allowance is edited by the platform; a tenant points at one through
/// <see cref="TenantLicense.WhatsAppAddOn"/>.
/// </summary>
public sealed class LicenseAddOn : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal MonthlyPrice { get; set; }
    public decimal AnnualPrice { get; set; }
    /// <summary>WhatsApp messages included each month.</summary>
    public int IncludedMessages { get; set; }
    public decimal ExtraMessagePrice { get; set; }
    /// <summary>Monthly price of each extra dedicated number (WhatsApp Business only).</summary>
    public decimal ExtraNumberPrice { get; set; }
    public bool IsActive { get; set; } = true;
}
