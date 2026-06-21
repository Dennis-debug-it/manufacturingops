using Ecosys.Shared.Common;

namespace Ecosys.Platform.Entities;

public sealed class EmailSetting : TenantEntity
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 25;
    public bool UseSsl { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string SenderName { get; set; } = "Ecosys";
    public string SenderAddress { get; set; } = "noreply@ecosys.local";
}
