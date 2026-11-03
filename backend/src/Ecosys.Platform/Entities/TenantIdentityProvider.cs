using Ecosys.Shared.Common;

namespace Ecosys.Platform.Entities;

public sealed class TenantIdentityProvider : TenantEntity
{
    public string ProviderName { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string Authority { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}
