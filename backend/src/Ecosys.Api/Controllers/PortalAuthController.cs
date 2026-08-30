using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[EnableRateLimiting("AuthEndpoints")]
[Route("api/portal/auth")]
public sealed class PortalAuthController(IPortalAuthService portalAuthService, AppDbContext dbContext, IPortalContext portalContext) : ControllerBase
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

        if (portalUser.Client is null || portalUser.Tenant is null || !portalUser.Tenant.IsActive)
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

    // Public by design: this is how a portal user obtains a token in the first place, so it has to
    // opt out of the global FallbackPolicy that Program.cs applies to unannotated endpoints.
    [AllowAnonymous]
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

    [AllowAnonymous]
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
        var origin = Request.Headers.Origin.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(origin))
        {
            return origin.TrimEnd('/');
        }

        return $"{Request.Scheme}://{Request.Host.Value}".TrimEnd('/');
    }
}

public sealed record PortalRequestLinkRequest(string Email);
public sealed record PortalVerifyRequest(string Token);
public sealed record PortalMessageResponse(string Message);
public sealed record PortalUserDto(Guid PortalUserId, string FullName, string Email);
public sealed record PortalClientDto(Guid ClientId, string ClientName);
public sealed record PortalTenantDto(Guid TenantId, string CompanyName);
public sealed record PortalVerifyResponse(string Token, PortalUserDto User, PortalClientDto Client, PortalTenantDto Tenant);
