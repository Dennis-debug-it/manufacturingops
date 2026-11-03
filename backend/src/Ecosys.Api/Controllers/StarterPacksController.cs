using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/starter-packs")]
public sealed class StarterPacksController(
    IStarterPackService starterPackService,
    ITenantContext tenantContext,
    IUserAccessService userAccessService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<StarterPackSummary>>> List(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        return Ok(await starterPackService.ListStarterPacksAsync(TenantId, cancellationToken));
    }

    [HttpPost("{id:guid}/install")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StarterPackSummary>> Install(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        try
        {
            return Ok(await starterPackService.InstallStarterPackAsync(TenantId, id, UserId, cancellationToken));
        }
        catch (DbUpdateException ex)
        {
            // Catch any remaining database constraint violations and surface them as a
            // meaningful 409 rather than an unhandled 500.  The service already guards
            // against the common cases (duplicate-key via TemplatePackId and idempotency
            // via SourceAssetCategoryId / SourceTemplateId checks), so reaching here
            // indicates an unexpected constraint conflict.
            return Conflict(new
            {
                message = "A database conflict occurred while installing the starter pack. Please try again.",
                detail = ex.InnerException?.Message ?? ex.Message
            });
        }
    }
}
