namespace Ecosys.Shared.Contracts.Auth;

public interface IAuthService
{
    Task<TokenResponse> CreateTokenAsync(TokenRequest request, CancellationToken cancellationToken = default);
}
