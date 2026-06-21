using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class PlatformLead : AuditableEntity
{
    public string CompanyName { get; set; } = string.Empty;
    public string ContactPersonName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Country { get; set; }
    public string? Industry { get; set; }
    public string? CompanySize { get; set; }
    public string? Message { get; set; }
    public string? PreferredContactMethod { get; set; }
    public string Status { get; set; } = "New";
    public DateTime? ContactedAt { get; set; }
    public Guid? ConvertedTenantId { get; set; }
    public string? Notes { get; set; }

    public Tenant? ConvertedTenant { get; set; }
}
