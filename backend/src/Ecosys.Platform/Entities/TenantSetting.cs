using Ecosys.Shared.Common;

namespace Ecosys.Platform.Entities;

public sealed class TenantSetting : TenantEntity
{
    public string CompanyName { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string PrimaryColor { get; set; } = "#0F766E";
    public string SecondaryColor { get; set; } = "#0B1020";
    public string AccentColor { get; set; } = "#F59E0B";
    public string EmailSenderName { get; set; } = "Ecosys";
    public string EmailSenderAddress { get; set; } = "noreply@ecosys.local";
    public bool ShowPoweredBy { get; set; } = true;

    public Tenant? Tenant { get; set; }
}
