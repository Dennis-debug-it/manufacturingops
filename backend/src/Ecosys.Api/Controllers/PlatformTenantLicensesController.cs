using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize(Policy = "PlatformReadOnlyAccess")]
[Route("api/platform/tenant-licenses")]
public sealed class PlatformTenantLicensesController(
    AppDbContext dbContext,
    ILicenseGuardService licenseGuardService,
    ITenantContext tenantContext,
    IAuditLogService auditLogService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<TenantLicenseSnapshot>>> GetAll(CancellationToken cancellationToken)
    {
        var tenantIds = await dbContext.Tenants
            .Where(x => x.Id != PlatformConstants.RootTenantId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var snapshots = new List<TenantLicenseSnapshot>(tenantIds.Count);
        foreach (var tenantId in tenantIds)
        {
            snapshots.Add(await licenseGuardService.GetSnapshotAsync(tenantId, cancellationToken));
        }

        return Ok(snapshots.OrderBy(x => x.PlanName).ToList());
    }

    [HttpPost("{tenantId:guid}/activate")]
    [Authorize(Policy = "PlatformAdminAccess")]
    public Task<ActionResult<TenantLicenseSnapshot>> Activate(Guid tenantId, CancellationToken cancellationToken) =>
        UpdateStatusAsync(tenantId, "Active", cancellationToken);

    [HttpPost("{tenantId:guid}/suspend")]
    [Authorize(Policy = "PlatformAdminAccess")]
    public Task<ActionResult<TenantLicenseSnapshot>> Suspend(Guid tenantId, CancellationToken cancellationToken) =>
        UpdateStatusAsync(tenantId, "Suspended", cancellationToken);

    private async Task<ActionResult<TenantLicenseSnapshot>> UpdateStatusAsync(Guid tenantId, string status, CancellationToken cancellationToken)
    {
        var license = await dbContext.TenantLicenses
            .Include(x => x.Tenant)
            .Include(x => x.LicensePlan)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant license was not found.");

        var now = DateTime.UtcNow;
        if (status == "Suspended")
        {
            TenantLifecycleState.Suspend(license, LicenseSuspensionReasons.Manual, now);
        }
        else if (TenantLifecycleState.IsSuspended(license))
        {
            TenantLifecycleState.LiftSuspension(license);
        }
        else
        {
            license.Status = LicenseStatuses.Active;
            license.CancelledAt = null;
        }

        // Same projection every other lifecycle writer uses, so the tenant row (and the platform tenant
        // page's license badge) always matches what is enforced (TM-P1-02).
        var tenant = license.Tenant ?? await dbContext.Tenants.SingleAsync(x => x.Id == tenantId, cancellationToken);
        TenantLifecycleState.Sync(tenant, license, now);

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(tenantId, tenantContext.GetRequiredUserId(), $"platform.license.{status.ToLowerInvariant()}", "TenantLicense", license.Id.ToString(), $"Tenant license set to {status}.", cancellationToken: cancellationToken);
        return Ok(await licenseGuardService.GetSnapshotAsync(tenantId, cancellationToken));
    }
}
