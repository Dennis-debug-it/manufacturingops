using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Ecosys.Shared.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Ecosys.Infrastructure.Services;

public sealed record PortalMagicLinkResult(string Token, Guid PortalUserId, Guid ClientId, Guid TenantId, string FullName, string Email, string ClientName, string CompanyName);

public interface IPortalAuthService
{
    Task RequestMagicLinkAsync(string email, string magicLinkBaseUrl, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);
    Task<PortalMagicLinkResult> VerifyMagicLinkAsync(string token, CancellationToken cancellationToken = default);
}

internal sealed class PortalAuthService(
    AppDbContext dbContext,
    IOptions<JwtOptions> jwtOptions,
    IEmailOutboxService emailOutboxService,
    IAuditLogService auditLogService) : IPortalAuthService
{
    private const int LinkExpiryMinutes = 30;
    private const int SessionExpiryMinutes = 60 * 24; // 24h — portal sessions outlive a single staff shift by design

    public async Task RequestMagicLinkAsync(string email, string magicLinkBaseUrl, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var portalUser = await dbContext.ClientPortalUsers
            .IgnoreQueryFilters()
            .Include(x => x.Client)
            .Include(x => x.Tenant)
            .SingleOrDefaultAsync(x => x.Email.ToLower() == normalizedEmail && x.IsActive, cancellationToken);

        // Always return silently on a miss — do not reveal whether an email is registered.
        if (portalUser is null || portalUser.Client is null || portalUser.Tenant is null || !portalUser.Tenant.IsActive)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var rawToken = CreateRawToken();
        dbContext.ClientPortalLoginTokens.Add(new ClientPortalLoginToken
        {
            ClientPortalUserId = portalUser.Id,
            TokenHash = HashToken(rawToken),
            ExpiresAt = now.AddMinutes(LinkExpiryMinutes),
            RequestedIp = ipAddress,
            UserAgent = userAgent,
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        var link = $"{magicLinkBaseUrl.TrimEnd('/')}/portal/verify#{rawToken}";
        var html = $"""
            <p>Hello {System.Net.WebUtility.HtmlEncode(portalUser.FullName)},</p>
            <p>Use the link below to sign in to the {System.Net.WebUtility.HtmlEncode(portalUser.Tenant.CompanyName)} client portal. This link expires in {LinkExpiryMinutes} minutes and can only be used once.</p>
            <p><a href="{link}">Sign in to the client portal</a></p>
            <p>If you didn't request this, you can safely ignore this email.</p>
            """;

        await emailOutboxService.QueueEmailAsync(
            new QueueEmailRequest(
                portalUser.TenantId,
                "portal.magic-link",
                "portal-magic-link",
                portalUser.Email,
                portalUser.FullName,
                portalUser.Tenant.CompanyName,
                null,
                null,
                $"Sign in to the {portalUser.Tenant.CompanyName} client portal",
                html,
                null,
                null),
            cancellationToken);

        await auditLogService.LogAsync(
            portalUser.TenantId,
            null,
            "portal.auth.link-requested",
            nameof(ClientPortalUser),
            portalUser.Id.ToString(),
            $"Magic link requested for portal contact '{portalUser.Email}'.",
            actorName: portalUser.FullName,
            cancellationToken: cancellationToken);
    }

    public async Task<PortalMagicLinkResult> VerifyMagicLinkAsync(string token, CancellationToken cancellationToken = default)
    {
        const string invalidMessage = "This sign-in link is invalid or has expired. Request a new one.";

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ForbiddenException(invalidMessage);
        }

        var tokenHash = HashToken(token.Trim());
        var now = DateTime.UtcNow;
        var loginToken = await dbContext.ClientPortalLoginTokens
            .IgnoreQueryFilters()
            .Include(x => x.ClientPortalUser)
            .ThenInclude(x => x!.Client)
            .Include(x => x.ClientPortalUser)
            .ThenInclude(x => x!.Tenant)
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        if (loginToken is null
            || loginToken.ConsumedAt.HasValue
            || loginToken.ExpiresAt <= now
            || loginToken.ClientPortalUser is null
            || !loginToken.ClientPortalUser.IsActive
            || loginToken.ClientPortalUser.Client is null
            || loginToken.ClientPortalUser.Tenant is null
            || !loginToken.ClientPortalUser.Tenant.IsActive)
        {
            throw new ForbiddenException(invalidMessage);
        }

        var portalUser = loginToken.ClientPortalUser;
        loginToken.ConsumedAt = now;
        portalUser.LastLoginAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            portalUser.TenantId,
            null,
            "portal.auth.login",
            nameof(ClientPortalUser),
            portalUser.Id.ToString(),
            $"Portal contact '{portalUser.Email}' signed in.",
            actorName: portalUser.FullName,
            cancellationToken: cancellationToken);

        var jwt = CreateToken(portalUser);
        return new PortalMagicLinkResult(
            jwt,
            portalUser.Id,
            portalUser.ClientId,
            portalUser.TenantId,
            portalUser.FullName,
            portalUser.Email,
            portalUser.Client.ClientName,
            portalUser.Tenant.CompanyName);
    }

    private string CreateToken(ClientPortalUser portalUser)
    {
        var options = jwtOptions.Value;
        var expiresUtc = DateTime.UtcNow.AddMinutes(SessionExpiryMinutes);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, portalUser.Id.ToString()),
            new(TenantClaimTypes.PortalUserId, portalUser.Id.ToString()),
            new(TenantClaimTypes.ClientId, portalUser.ClientId.ToString()),
            new(TenantClaimTypes.TenantId, portalUser.TenantId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.Email, portalUser.Email),
            new(ClaimTypes.Name, portalUser.FullName),
            new(ClaimTypes.Role, AppRoles.PortalClient),
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var jwt = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresUtc,
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private static string CreateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
