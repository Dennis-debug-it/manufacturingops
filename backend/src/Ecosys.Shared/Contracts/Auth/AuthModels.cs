namespace Ecosys.Shared.Contracts.Auth;

public sealed class TokenRequest
{
    public string Email { get; set; } = string.Empty;
    public string? TenantCode { get; set; }
}

public sealed class TokenResponse
{
    public required string AccessToken { get; init; }
    public required DateTime ExpiresUtc { get; init; }
    public required string Role { get; init; }
    public Guid? TenantId { get; init; }
}
