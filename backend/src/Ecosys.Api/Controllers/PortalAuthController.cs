using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Ecosys.Shared.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ecosys.Api.Controllers;

[ApiController]
[EnableRateLimiting("AuthEndpoints")]
[Route("api/portal/auth")]
public sealed class PortalAuthController(IPortalAuthService portalAuthService, AppDbContext dbContext, IPortalContext portalContext, IOptions<PublicAppOptions> publicAppOptions) : ControllerBase
{
    [Authorize(Policy = "PortalAccess")]
    [HttpGet("me")]
    public async Task<ActionResult<PortalVerifyResponse>> GetCurrentPortalUser(CancellationToken cancellationToken)
    {
        var portalUserId = portalContext.GetRequiredPortalUserId();
        var portalUser = await dbContext.ClientPortalUsers
            .Include(x => x.Client)
            .Include(x => x.Tenant)
            .SingleOrDefaultAsync(x => x.Id == portalUserId && x.IsActive, cancellationToken)
            ?? throw new UnauthorizedException("Portal session is no longer valid.");

        if (portalUser.Client is null || !portalUser.Client.IsActive
            || portalUser.Client.TenantId != portalUser.TenantId
            || portalUser.ClientId != portalContext.GetRequiredClientId()
            || portalUser.TenantId != portalContext.GetRequiredTenantId()
            || portalUser.Tenant is null || !portalUser.Tenant.IsActive)
        {
            throw new UnauthorizedException("Portal session is no longer valid.");
        }

        return Ok(new PortalVerifyResponse(
            string.Empty,
            new PortalUserDto(portalUser.Id, portalUser.FullName, portalUser.Email),
            new PortalClientDto(portalUser.ClientId, portalUser.Client.ClientName),
            new PortalTenantDto(portalUser.TenantId, portalUser.Tenant.CompanyName)));
    }

    private const string GenericMessage = "If this email is registered for portal access, a sign-in link has been sent.";

    [HttpPost("request-link")]
    public async Task<ActionResult<PortalMessageResponse>> RequestLink([FromBody] PortalRequestLinkRequest request, CancellationToken cancellationToken)
    {
        await portalAuthService.RequestMagicLinkAsync(
            request.Email,
            ResolveOrigin(),
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);

        return Ok(new PortalMessageResponse(GenericMessage));
    }

    [HttpPost("verify")]
    public async Task<ActionResult<PortalVerifyResponse>> Verify([FromBody] PortalVerifyRequest request, CancellationToken cancellationToken)
    {
        var result = await portalAuthService.VerifyMagicLinkAsync(request.Token, cancellationToken);

        return Ok(new PortalVerifyResponse(
            result.Token,
            new PortalUserDto(result.PortalUserId, result.FullName, result.Email),
            new PortalClientDto(result.ClientId, result.ClientName),
            new PortalTenantDto(result.TenantId, result.CompanyName)));
    }

    [Authorize(Policy = "PortalAccess")]
    [HttpPost("logout")]
    public IActionResult Logout() => Ok();

    private string ResolveOrigin()
    {
        var configuredUrl = publicAppOptions.Value.PublicUrl?.Trim();
        if (Uri.TryCreate(configuredUrl, UriKind.Absolute, out var uri)
            && uri.Scheme is "https" or "http" && string.IsNullOrEmpty(uri.UserInfo)
            && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment))
        {
            return uri.AbsoluteUri.TrimEnd('/');
        }

        throw new InvalidOperationException("App:PublicUrl must be configured for authentication links.");
    }
}

public sealed record PortalRequestLinkRequest(string Email);
public sealed record PortalVerifyRequest(string Token);
public sealed record PortalMessageResponse(string Message);
public sealed record PortalUserDto(Guid PortalUserId, string FullName, string Email);
public sealed record PortalClientDto(Guid ClientId, string ClientName);
public sealed record PortalTenantDto(Guid TenantId, string CompanyName);
public sealed record PortalVerifyResponse(string Token, PortalUserDto User, PortalClientDto Client, PortalTenantDto Tenant);
