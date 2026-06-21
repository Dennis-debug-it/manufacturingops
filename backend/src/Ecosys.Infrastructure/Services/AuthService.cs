using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ecosys.Infrastructure.Persistence;
using Ecosys.Platform.Entities;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Auth;
using Ecosys.Shared.Errors;
using Ecosys.Shared.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Ecosys.Infrastructure.Services;

internal sealed class AuthService(EcosysDbContext dbContext, IOptions<JwtOptions> jwtOptions) : IAuthService
{
    public async Task<TokenResponse> CreateTokenAsync(TokenRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new BusinessRuleException("Email is required.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        AppUser? user;
        Guid? tenantId = null;

        if (string.IsNullOrWhiteSpace(request.TenantCode))
        {
            user = await dbContext.Users
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(
                    x => x.Email.ToLower() == normalizedEmail
                        && x.Role == AppRoles.SuperAdmin
                        && x.TenantId == PlatformConstants.RootTenantId
                        && x.IsActive,
                    cancellationToken);
        }
        else
        {
            var tenantCode = request.TenantCode.Trim().ToUpperInvariant();
            var tenant = await dbContext.Tenants
                .SingleOrDefaultAsync(x => x.Code == tenantCode && x.IsActive, cancellationToken)
                ?? throw new NotFoundException("Tenant was not found or is inactive.");

            tenantId = tenant.Id;
            user = await dbContext.Users
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(
                    x => x.Email.ToLower() == normalizedEmail
                        && x.TenantId == tenant.Id
                        && x.IsActive,
                    cancellationToken);
        }

        if (user is null)
        {
            throw new NotFoundException("User was not found.");
        }

        var options = jwtOptions.Value;
        var expiresUtc = DateTime.UtcNow.AddMinutes(options.ExpiryMinutes);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(TenantClaimTypes.UserId, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role)
        };

        if (tenantId.HasValue)
        {
            claims.Add(new Claim(TenantClaimTypes.TenantId, tenantId.Value.ToString()));
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresUtc,
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        return new TokenResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresUtc = expiresUtc,
            Role = user.Role,
            TenantId = tenantId
        };
    }
}
