using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

public sealed record PlatformOperationsModulesResponse(Guid TenantId, IReadOnlyList<string> Enabled, IReadOnlyList<string> Available);

public sealed record SetPlatformOperationsModulesRequest(IReadOnlyList<string>? Modules);

/// <summary>
/// The Factory Operations add-ons a tenant takes with its edition: Visitor Management (<c>visitors</c>)
/// and Security Patrol (<c>security</c>). Ecosys turns them on per tenant as they are sold.
/// </summary>
[ApiController]
[Authorize(Policy = "PlatformAccess")]
[Route("api/platform/operations-modules/tenants/{tenantId:guid}")]
public sealed class PlatformOperationsModulesController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IAuditLogService auditLogService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PlatformOperationsModulesResponse>> Get(Guid tenantId, CancellationToken cancellationToken)
    {
        var license = await dbContext.TenantLicenses.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("This tenant has no license yet.");
        return Ok(new PlatformOperationsModulesResponse(tenantId, ServiceOpsPlans.ParseOperationsAddOns(license.OperationsAddOns), ServiceOpsPlans.OperationsAddOnModules));
    }

    [HttpPut]
    public async Task<ActionResult<PlatformOperationsModulesResponse>> Set(Guid tenantId, [FromBody] SetPlatformOperationsModulesRequest request, CancellationToken cancellationToken)
    {
        if (tenantId == PlatformConstants.RootTenantId)
            throw new BusinessRuleException("The platform workspace doesn't take add-ons.");
        var license = await dbContext.TenantLicenses.SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("This tenant has no license yet.");
        var before = license.OperationsAddOns;
        license.OperationsAddOns = ServiceOpsPlans.NormalizeOperationsAddOns(request.Modules);
        license.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(tenantId, tenantContext.UserId, "platform.operations-modules.changed", "TenantLicense", license.Id.ToString(),
            $"Factory Operations add-ons: {before ?? "none"} → {license.OperationsAddOns ?? "none"}.", cancellationToken);
        return Ok(new PlatformOperationsModulesResponse(tenantId, ServiceOpsPlans.ParseOperationsAddOns(license.OperationsAddOns), ServiceOpsPlans.OperationsAddOnModules));
    }
}
