using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Ecosys.Api.Controllers;

/// <summary>
/// The platform-operated demo workspace (see <see cref="DemoWorkspace"/>). Platform admins create it, reset it to
/// fresh seeded content, and hand out a portal sign-in link — the demo's portal contact has no mailbox.
/// </summary>
[ApiController]
[Authorize(Policy = "PlatformAdminAccess")]
[Route("api/platform/demo-workspace")]
public sealed class PlatformDemoWorkspaceController(
    IDemoWorkspaceService demoWorkspaceService,
    ITenantContext tenantContext,
    IOptions<PublicAppOptions> publicAppOptions) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DemoWorkspaceStatus>> Get(CancellationToken cancellationToken) =>
        Ok(await demoWorkspaceService.GetStatusAsync(cancellationToken));

    /// <summary>Creates the workspace on first use; afterwards wipes it and re-seeds. Returns the new shared password once.</summary>
    [HttpPost("reset")]
    public async Task<ActionResult<DemoWorkspaceResetResult>> Reset(CancellationToken cancellationToken) =>
        Ok(await demoWorkspaceService.ResetAsync(tenantContext.UserId, tenantContext.Email, cancellationToken));

    [HttpPost("portal-link")]
    public async Task<ActionResult<DemoPortalLinkResponse>> IssuePortalLink(CancellationToken cancellationToken)
    {
        // The link opens the app the admin is using (their Origin) so it also works on staging and locally.
        var baseUrl = Request.Headers.Origin.FirstOrDefault() is { Length: > 0 } origin && Uri.TryCreate(origin, UriKind.Absolute, out _)
            ? origin
            : publicAppOptions.Value.PublicUrl;
        var link = await demoWorkspaceService.IssuePortalSignInLinkAsync(
            baseUrl,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);
        return Ok(new DemoPortalLinkResponse(link));
    }
}

public sealed record DemoPortalLinkResponse(string Url);
