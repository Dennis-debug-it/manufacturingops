using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/kip")]
public sealed class KipController(ITenantContext tenantContext) : TenantAwareControllerBase(tenantContext)
{
    [HttpPost("query")]
    public ActionResult<KipQueryResponse> Query([FromBody] KipQueryRequest request)
    {
        return Ok(new KipQueryResponse("KIP is not yet active. Check back soon."));
    }
}

public sealed record KipQueryRequest(KipContextRequest Context, string Message);

public sealed record KipContextRequest(
    string Screen,
    string? EntityType,
    string? EntityId,
    object? EntitySummary,
    Guid TenantId,
    Guid UserId,
    string UserRole,
    string Timestamp);

public sealed record KipQueryResponse(string Response);
