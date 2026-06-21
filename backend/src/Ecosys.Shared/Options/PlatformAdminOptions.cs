namespace Ecosys.Shared.Options;

public sealed class PlatformAdminOptions
{
    public const string SectionName = "PlatformAdmin";

    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = "Platform SuperAdmin";
}
