using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecosys.Api.Controllers;

/// <summary>
/// Role dashboards (Release 3, part 4): the caller's layout, one widget's figures, and the admin's
/// per-role hiding. Shared on every plan; each widget enforces its own module and permission, so a
/// widget the tenant or the caller can't have answers 403 whatever the saved layout says.
/// </summary>
[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed class RoleDashboardController(
    IRoleDashboardService roleDashboardService,
    ITenantContext tenantContext) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet("layout")]
    public async Task<ActionResult<DashboardLayoutResponse>> GetLayout(CancellationToken cancellationToken) =>
        Ok(await roleDashboardService.GetLayoutAsync(cancellationToken));

    [HttpPut("layout")]
    public async Task<ActionResult<DashboardLayoutResponse>> SaveLayout([FromBody] SaveDashboardLayoutRequest request, CancellationToken cancellationToken) =>
        Ok(await roleDashboardService.SaveLayoutAsync(request.Items ?? [], cancellationToken));

    /// <summary>Reset to default: forgets the personal layout.</summary>
    [HttpDelete("layout")]
    public async Task<ActionResult<DashboardLayoutResponse>> ResetLayout(CancellationToken cancellationToken) =>
        Ok(await roleDashboardService.ResetLayoutAsync(cancellationToken));

    /// <param name="refresh">Skip the 5-minute cache and recompute.</param>
    [HttpGet("widgets/{key}")]
    public async Task<ActionResult<DashboardWidgetData>> GetWidget(string key, [FromQuery] Guid? branchId, [FromQuery] bool refresh, CancellationToken cancellationToken) =>
        Ok(await roleDashboardService.GetWidgetAsync(key, branchId, refresh, cancellationToken));

    [HttpGet("settings")]
    public async Task<ActionResult<DashboardAdminSettingsResponse>> GetSettings(CancellationToken cancellationToken) =>
        Ok(await roleDashboardService.GetAdminSettingsAsync(cancellationToken));

    [HttpPut("settings/roles/{role}")]
    public async Task<ActionResult<DashboardAdminSettingsResponse>> SetHidden(string role, [FromBody] SetDashboardHiddenWidgetsRequest request, CancellationToken cancellationToken) =>
        Ok(await roleDashboardService.SetHiddenAsync(role, request.HiddenKeys ?? [], cancellationToken));
}

public sealed record SaveDashboardLayoutRequest(IReadOnlyList<DashboardLayoutItem>? Items);

public sealed record SetDashboardHiddenWidgetsRequest(IReadOnlyList<string>? HiddenKeys);
