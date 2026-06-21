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
    public Task<ActionResult<TenantLicenseSnapshot>> Activate(Guid tenantId, CancellationToken cancellationToken) =>
        UpdateStatusAsync(tenantId, "Active", cancellationToken);

    [HttpPost("{tenantId:guid}/suspend")]
    public Task<ActionResult<TenantLicenseSnapshot>> Suspend(Guid tenantId, CancellationToken cancellationToken) =>
        UpdateStatusAsync(tenantId, "Suspended", cancellationToken);

    private async Task<ActionResult<TenantLicenseSnapshot>> UpdateStatusAsync(Guid tenantId, string status, CancellationToken cancellationToken)
    {
        var license = await dbContext.TenantLicenses.SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant license was not found.");

        license.Status = status;
        license.SuspendedAt = status == "Suspended" ? DateTime.UtcNow : null;

        var tenant = await dbContext.Tenants.SingleAsync(x => x.Id == tenantId, cancellationToken);
        if (status == "Active")
        {
            tenant.IsActive = true;
            tenant.Status = "Active";
            tenant.SuspendedAt = null;
        }
        else
        {
            tenant.IsActive = false;
            tenant.Status = "Suspended";
            tenant.SuspendedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(tenantId, tenantContext.GetRequiredUserId(), $"platform.license.{status.ToLowerInvariant()}", "TenantLicense", license.Id.ToString(), $"Tenant license set to {status}.", cancellationToken: cancellationToken);
        return Ok(await licenseGuardService.GetSnapshotAsync(tenantId, cancellationToken));
    }
}
