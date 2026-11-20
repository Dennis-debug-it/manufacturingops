using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace Ecosys.Api.Controllers;

/// <summary>
/// The platform-operated demo workspace (see <see cref="DemoWorkspace"/>): a tea factory with a few months of
/// maintenance history. Platform admins create it, reset it to fresh data dated from today, and open it as any of
/// its logins to show a prospect around.
/// </summary>
[ApiController]
[Authorize(Policy = "PlatformAdminAccess")]
[Route("api/platform/demo-workspace")]
public sealed class PlatformDemoWorkspaceController(
    IDemoWorkspaceService demoWorkspaceService,
    IMvpAuthService authService,
    ITenantTimeZoneService tenantTimeZoneService,
    ITenantContext tenantContext,
    IHostApplicationLifetime applicationLifetime) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DemoWorkspaceStatus>> Get(CancellationToken cancellationToken) =>
        Ok(await demoWorkspaceService.GetStatusAsync(cancellationToken));

    /// <summary>
    /// Creates the workspace on first use; afterwards wipes it and re-seeds. Returns the new shared password once.
    /// Seeding takes from about 20 seconds to over a minute on a small server, which can outlast a reverse proxy's
    /// read timeout. The reset therefore ignores the request being aborted (it is one transaction and finishes or
    /// rolls back as a whole) and stops only when the app shuts down; the page reloads the status afterwards.
    /// </summary>
    [HttpPost("reset")]
    public async Task<ActionResult<DemoWorkspaceResetResult>> Reset() =>
        Ok(await demoWorkspaceService.ResetAsync(tenantContext.UserId, tenantContext.Email, applicationLifetime.ApplicationStopping));

    /// <summary>
    /// Opens the demo as one of its logins without a password: an impersonation session, so it stays in the
    /// presenter's tab and leaving it returns them to the platform. Only the demo's own logins can be opened.
    /// </summary>
    [HttpPost("open")]
    public async Task<ActionResult<TenantImpersonationResponse>> Open([FromBody] OpenDemoRequest? request, CancellationToken cancellationToken)
    {
        var userId = await demoWorkspaceService.FindLoginUserIdAsync(request?.Username, cancellationToken);
        var result = await authService.ImpersonateUserAsync(
            DemoWorkspace.TenantId, userId, tenantContext.GetRequiredUserId(), tenantContext.Email ?? "Platform admin", "Demo workspace", cancellationToken);
        return Ok(new TenantImpersonationResponse(
            result.Token,
            new LoginUserDto(result.UserId, result.FullName, result.Email, result.Role, result.JobTitle, null, false, PlatformTenantsController.MapPermissions(result.Permissions)),
            new LoginTenantDto(
                result.TenantId,
                result.CompanyName,
                result.Country,
                result.Industry,
                result.LogoUrl,
                result.ShowPoweredByEcosys,
                await tenantTimeZoneService.GetTimeZoneIdAsync(result.TenantId, cancellationToken)),
            result.ImpersonatedByUserId,
            result.ImpersonatedByName,
            result.StartedAt));
    }
}

public sealed record OpenDemoRequest(string? Username);
