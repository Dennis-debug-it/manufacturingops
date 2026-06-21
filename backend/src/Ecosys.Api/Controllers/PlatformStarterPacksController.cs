using Ecosys.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize(Policy = "PlatformAdminAccess")]
[Route("api/platform/starter-packs")]
public sealed class PlatformStarterPacksController(IStarterPackService starterPackService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<StarterPackSummary>>> List(CancellationToken cancellationToken) =>
        Ok(await starterPackService.ListStarterPacksAsync(null, cancellationToken));
}
