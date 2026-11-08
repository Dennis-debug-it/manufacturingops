using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    ITenantContext tenantContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DemoWorkspaceStatus>> Get(CancellationToken cancellationToken) =>
        Ok(await demoWorkspaceService.GetStatusAsync(cancellationToken));

    /// <summary>Creates the workspace on first use; afterwards wipes it and re-seeds. Returns the new shared password once.</summary>
    [HttpPost("reset")]
    public async Task<ActionResult<DemoWorkspaceResetResult>> Reset(CancellationToken cancellationToken) =>
        Ok(await demoWorkspaceService.ResetAsync(tenantContext.UserId, tenantContext.Email, cancellationToken));

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
