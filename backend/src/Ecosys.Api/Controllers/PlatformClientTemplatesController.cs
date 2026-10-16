using Ecosys.Infrastructure.Reports;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecosys.Api.Controllers;

/// <summary>Installs client-specific checklist + report templates into one tenant's workspace.</summary>
[ApiController]
[Authorize(Policy = "PlatformAdminAccess")]
[Route("api/platform/tenants/{tenantId:guid}/client-templates")]
public sealed class PlatformClientTemplatesController(
    IClientTemplateInstaller installer,
    IAuditLogService auditLogService,
    ITenantContext tenantContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ClientTemplateSummary>>> List(Guid tenantId, CancellationToken cancellationToken) =>
        Ok(await installer.ListAsync(tenantId, cancellationToken));

    [HttpPost("{key}/install")]
    public async Task<ActionResult<ClientTemplateInstallResult>> Install(Guid tenantId, string key, CancellationToken cancellationToken)
    {
        var result = await installer.InstallAsync(tenantId, key, cancellationToken);
        await auditLogService.LogAsync(
            tenantId,
            tenantContext.UserId,
            result.Created ? "Client template installed" : "Client template updated",
            "PmTemplate",
            result.TemplateId.ToString(),
            $"Platform installed '{result.Name}' (v{result.Version}).",
            cancellationToken);
        return Ok(result);
    }
}
