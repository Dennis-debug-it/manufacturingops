using Ecosys.Platform.Enums;

namespace Ecosys.Platform.Contracts.Tenants;

public sealed class CreateTenantRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Plan { get; set; } = "Standard";
}

public sealed class TenantResponse
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Code { get; init; }
    public required string Plan { get; init; }
    public SubscriptionStatus SubscriptionStatus { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedUtc { get; init; }
}
