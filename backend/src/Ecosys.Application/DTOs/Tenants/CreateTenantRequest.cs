using System.ComponentModel.DataAnnotations;

namespace Ecosys.Application.DTOs.Tenants;

public sealed class CreateTenantRequest
{
    [Required]
    [StringLength(150, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 2)]
    public string Code { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(256)]
    public string? ContactEmail { get; set; }
}
