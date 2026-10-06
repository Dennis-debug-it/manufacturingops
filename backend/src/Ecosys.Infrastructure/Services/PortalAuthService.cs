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

    /// <summary>
    /// Issues a one-time sign-in link for an active portal contact and returns it to the caller instead of
    /// emailing it. Only for platform-operated workspaces (the demo) whose contacts have no real mailbox.
    /// </summary>
    Task<string> IssueSignInLinkAsync(Guid portalUserId, string magicLinkBaseUrl, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Staff-initiated invitation: emails the contact their permanent portal link (see
    /// <see cref="GetAccessLinkAsync"/>) plus the sign-in page address.
    /// </summary>
    Task SendInvitationAsync(Guid portalUserId, string portalBaseUrl, Guid? invitedByUserId, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);

    /// <summary>
    /// The contact's permanent, full-access portal link - it never expires. Issued on first use; with
    /// <paramref name="reset"/> a new one replaces it and the old link stops working. Deactivating the
    /// contact also stops it. Anyone holding the link acts as this contact, so it is audited.
    /// </summary>
    Task<string> GetAccessLinkAsync(Guid portalUserId, string portalBaseUrl, Guid? actorUserId, bool reset, CancellationToken cancellationToken = default);

    /// <summary>Signs in with a permanent portal link; rejects reset links and inactive contacts.</summary>
    Task<PortalMagicLinkResult> OpenAccessLinkAsync(string link, CancellationToken cancellationToken = default);
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
        if (portalUser is null || portalUser.Client is null || !portalUser.Client.IsActive
            || portalUser.Client.TenantId != portalUser.TenantId || portalUser.Tenant is null || !portalUser.Tenant.IsActive)
        {
            return;
        }

        var link = await CreateLinkAsync(portalUser, magicLinkBaseUrl, ipAddress, userAgent, cancellationToken);
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

    public async Task<string> IssueSignInLinkAsync(Guid portalUserId, string magicLinkBaseUrl, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
    {
        var portalUser = await dbContext.ClientPortalUsers
            .IgnoreQueryFilters()
            .Include(x => x.Client)
            .Include(x => x.Tenant)
            .SingleOrDefaultAsync(x => x.Id == portalUserId, cancellationToken);

        // Same eligibility as an emailed link: the link must not open a door the login page would keep shut.
        if (portalUser is null || !portalUser.IsActive || portalUser.Client is null || !portalUser.Client.IsActive
            || portalUser.Client.TenantId != portalUser.TenantId || portalUser.Tenant is null || !portalUser.Tenant.IsActive)
        {
            throw new BusinessRuleException("This portal contact cannot sign in.");
        }

        var link = await CreateLinkAsync(portalUser, magicLinkBaseUrl, ipAddress, userAgent, cancellationToken);

        await auditLogService.LogAsync(
            portalUser.TenantId,
            null,
            "portal.auth.link-issued",
            nameof(ClientPortalUser),
            portalUser.Id.ToString(),
            $"Sign-in link issued directly (not emailed) for portal contact '{portalUser.Email}'.",
            cancellationToken: cancellationToken);

        return link;
    }

    public async Task SendInvitationAsync(Guid portalUserId, string portalBaseUrl, Guid? invitedByUserId, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
    {
        var portalUser = await LoadEligibleAsync(portalUserId, cancellationToken);
        var link = await EnsureAccessLinkAsync(portalUser, portalBaseUrl, reset: false, cancellationToken);
        var loginUrl = $"{portalBaseUrl.TrimEnd('/')}/portal/login";
        var company = System.Net.WebUtility.HtmlEncode(portalUser.Tenant!.CompanyName);
        var html = $"""
            <p>Hello {System.Net.WebUtility.HtmlEncode(portalUser.FullName)},</p>
            <p>{company} has given you access to its client portal, where you can raise service requests and follow your work orders, quotations and invoices.</p>
            <p><a href="{link}">Open the client portal</a></p>
            <p>This is your personal link and it doesn't expire - bookmark it, and please don't share it. You can also sign in at <a href="{loginUrl}">{System.Net.WebUtility.HtmlEncode(loginUrl)}</a> with <strong>{System.Net.WebUtility.HtmlEncode(portalUser.Email)}</strong>.</p>
            """;

        await emailOutboxService.QueueEmailAsync(
            new QueueEmailRequest(
                portalUser.TenantId, "portal.invitation", "portal-invitation",
                portalUser.Email, portalUser.FullName, portalUser.Tenant.CompanyName, null, null,
                $"You're invited to the {portalUser.Tenant.CompanyName} client portal",
                html, null, invitedByUserId),
            cancellationToken);

        await auditLogService.LogAsync(portalUser.TenantId, invitedByUserId, "portal.contact.invitation-sent", nameof(ClientPortalUser), portalUser.Id.ToString(),
            $"Portal invitation with permanent link emailed to '{portalUser.Email}'.", cancellationToken: cancellationToken);
    }

    public async Task<string> GetAccessLinkAsync(Guid portalUserId, string portalBaseUrl, Guid? actorUserId, bool reset, CancellationToken cancellationToken = default)
    {
        var portalUser = await LoadEligibleAsync(portalUserId, cancellationToken);
        var link = await EnsureAccessLinkAsync(portalUser, portalBaseUrl, reset, cancellationToken);
        await auditLogService.LogAsync(portalUser.TenantId, actorUserId,
            reset ? "portal.contact.access-link-reset" : "portal.contact.access-link-copied", nameof(ClientPortalUser), portalUser.Id.ToString(),
            reset ? $"Permanent portal link for '{portalUser.Email}' reset; the previous link no longer works."
                  : $"Permanent portal link for '{portalUser.Email}' copied by staff.",
            cancellationToken: cancellationToken);
        return link;
    }

    public async Task<PortalMagicLinkResult> OpenAccessLinkAsync(string link, CancellationToken cancellationToken = default)
    {
        const string invalidMessage = "This portal link is no longer valid. Ask your service provider for a new link, or sign in with your email.";
        var parts = (link ?? string.Empty).Trim().Split('.', 2);
        if (parts.Length != 2 || !Guid.TryParseExact(parts[0], "N", out var portalUserId))
        {
            throw new ForbiddenException(invalidMessage);
        }

        var portalUser = await dbContext.ClientPortalUsers
            .IgnoreQueryFilters()
            .Include(x => x.Client)
            .Include(x => x.Tenant)
            .SingleOrDefaultAsync(x => x.Id == portalUserId, cancellationToken);
        if (portalUser?.AccessLinkNonce is null || !portalUser.IsActive || portalUser.Client is null || !portalUser.Client.IsActive
            || portalUser.Client.TenantId != portalUser.TenantId || portalUser.Tenant is null || !portalUser.Tenant.IsActive
            || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(AccessLinkSecret(portalUser)), Encoding.ASCII.GetBytes(parts[1])))
        {
            throw new ForbiddenException(invalidMessage);
        }

        portalUser.LastLoginAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(portalUser.TenantId, null, "portal.auth.login", nameof(ClientPortalUser), portalUser.Id.ToString(),
            $"Portal contact '{portalUser.Email}' signed in with their permanent portal link.", actorName: portalUser.FullName, cancellationToken: cancellationToken);

        return new PortalMagicLinkResult(CreateToken(portalUser), portalUser.Id, portalUser.ClientId, portalUser.TenantId,
            portalUser.FullName, portalUser.Email, portalUser.Client.ClientName, portalUser.Tenant.CompanyName);
    }

    private async Task<string> EnsureAccessLinkAsync(ClientPortalUser portalUser, string portalBaseUrl, bool reset, CancellationToken cancellationToken)
    {
        if (reset || portalUser.AccessLinkNonce is null)
        {
            portalUser.AccessLinkNonce = CreateRawToken();
            portalUser.AccessLinkCreatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return $"{portalBaseUrl.TrimEnd('/')}/portal/access#{portalUser.Id:N}.{AccessLinkSecret(portalUser)}";
    }

    /// <summary>
    /// HMAC of the contact id and nonce under the JWT signing key: the database holds only the nonce,
    /// so a leaked backup doesn't yield working links. Rotating the signing key invalidates all links.
    /// </summary>
    private string AccessLinkSecret(ClientPortalUser portalUser)
    {
        var mac = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(jwtOptions.Value.SigningKey),
            Encoding.UTF8.GetBytes($"portal-access-link:{portalUser.Id:N}:{portalUser.AccessLinkNonce}"));
        return Convert.ToBase64String(mac).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private async Task<ClientPortalUser> LoadEligibleAsync(Guid portalUserId, CancellationToken cancellationToken)
    {
        var portalUser = await dbContext.ClientPortalUsers
            .IgnoreQueryFilters()
            .Include(x => x.Client)
            .Include(x => x.Tenant)
            .SingleOrDefaultAsync(x => x.Id == portalUserId, cancellationToken);

        // Same eligibility as the login page: a link must not open a door sign-in would keep shut.
        if (portalUser is null || !portalUser.IsActive || portalUser.Client is null || !portalUser.Client.IsActive
            || portalUser.Client.TenantId != portalUser.TenantId || portalUser.Tenant is null || !portalUser.Tenant.IsActive)
        {
            throw new BusinessRuleException("This portal contact is inactive or awaiting approval, so they cannot sign in yet.");
        }

        return portalUser;
    }

    private async Task<string> CreateLinkAsync(ClientPortalUser portalUser, string magicLinkBaseUrl, string? ipAddress, string? userAgent, CancellationToken cancellationToken)
    {
        var rawToken = CreateRawToken();
        dbContext.ClientPortalLoginTokens.Add(new ClientPortalLoginToken
        {
            ClientPortalUserId = portalUser.Id,
            TokenHash = HashToken(rawToken),
            ExpiresAt = DateTime.UtcNow.AddMinutes(LinkExpiryMinutes),
            RequestedIp = ipAddress,
            UserAgent = userAgent,
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return $"{magicLinkBaseUrl.TrimEnd('/')}/portal/verify#{rawToken}";
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
            || !loginToken.ClientPortalUser.Client.IsActive
            || loginToken.ClientPortalUser.Client.TenantId != loginToken.ClientPortalUser.TenantId
            || loginToken.ClientPortalUser.Tenant is null
            || !loginToken.ClientPortalUser.Tenant.IsActive)
        {
            throw new ForbiddenException(invalidMessage);
        }

        var portalUser = loginToken.ClientPortalUser;
        // Claim in one database statement: simultaneous verification requests must
        // never both exchange the same one-use link for a session.
        var claimed = await dbContext.ClientPortalLoginTokens
            .IgnoreQueryFilters()
            .Where(x => x.Id == loginToken.Id && x.ConsumedAt == null && x.ExpiresAt > now
                && x.ClientPortalUser != null && x.ClientPortalUser.IsActive
                && x.ClientPortalUser.TenantId == portalUser.TenantId
                && x.ClientPortalUser.ClientId == portalUser.ClientId
                && x.ClientPortalUser.Tenant != null && x.ClientPortalUser.Tenant.IsActive
                && x.ClientPortalUser.Client != null
                && x.ClientPortalUser.Client.IsActive
                && x.ClientPortalUser.Client.TenantId == x.ClientPortalUser.TenantId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ConsumedAt, now), cancellationToken);
        if (claimed != 1)
        {
            throw new ForbiddenException(invalidMessage);
        }

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
