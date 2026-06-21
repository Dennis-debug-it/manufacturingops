using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/tenant/license")]
public sealed class TenantLicenseController(
    ITenantContext tenantContext,
    ILicenseGuardService licenseGuardService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<TenantLicenseSnapshot>> Get(CancellationToken cancellationToken)
    {
        var snapshot = await licenseGuardService.GetSnapshotAsync(TenantId, cancellationToken);
        return Ok(snapshot);
    }

    [HttpGet("usage")]
    public async Task<ActionResult<LicenseUsageSnapshot>> GetUsage(CancellationToken cancellationToken)
    {
        var usage = await licenseGuardService.GetUsageAsync(TenantId, cancellationToken);
        return Ok(usage);
    }
}
