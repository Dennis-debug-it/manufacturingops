namespace Ecosys.Platform.Contracts.Settings;

public class TenantSettingsRequest
{
    public string CompanyName { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string PrimaryColor { get; set; } = "#0F766E";
    public string SecondaryColor { get; set; } = "#0B1020";
    public string AccentColor { get; set; } = "#F59E0B";
    public string EmailSenderName { get; set; } = "Ecosys";
    public string EmailSenderAddress { get; set; } = "noreply@ecosys.local";
    public bool ShowPoweredBy { get; set; } = true;
}

public sealed class TenantSettingsResponse : TenantSettingsRequest
{
    public Guid TenantId { get; init; }
}
