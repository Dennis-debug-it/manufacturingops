using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ecosys.Api.Controllers;

public sealed record PlatformTenantWhatsAppResponse(Guid TenantId, bool Enabled, int UsedThisMonth, int MonthlyCap, bool Configured);
public sealed record SetPlatformTenantWhatsAppRequest(bool Enabled);

/// <summary>
/// The tenant feature flag <c>whatsapp</c> (Release 3 design 6). Ecosys turns it on per tenant, because
/// Ecosys pays Meta for every message from the shared number.
/// </summary>
[ApiController]
[Authorize(Policy = "PlatformAccess")]
[Route("api/platform/whatsapp/tenants/{tenantId:guid}")]
public sealed class PlatformWhatsAppController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IAuditLogService auditLogService,
    IWhatsAppOutbox outbox,
    IWhatsAppAccountResolver accountResolver,
    IOptions<WhatsAppOptions> options) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PlatformTenantWhatsAppResponse>> Get(Guid tenantId, CancellationToken cancellationToken) =>
        Ok(await BuildAsync(tenantId, cancellationToken));

    [HttpPut]
    public async Task<ActionResult<PlatformTenantWhatsAppResponse>> Set(Guid tenantId, [FromBody] SetPlatformTenantWhatsAppRequest request, CancellationToken cancellationToken)
    {
        if (tenantId == PlatformConstants.RootTenantId)
            throw new BusinessRuleException("The platform workspace doesn't send WhatsApp reminders.");
        var license = await dbContext.TenantLicenses.SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("This tenant has no license yet.");
        license.WhatsAppEnabled = request.Enabled;
        license.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(tenantId, tenantContext.UserId, request.Enabled ? "platform.whatsapp.enabled" : "platform.whatsapp.disabled",
            "TenantLicense", license.Id.ToString(), request.Enabled ? "WhatsApp reminders turned on." : "WhatsApp reminders turned off.", cancellationToken);
        return Ok(await BuildAsync(tenantId, cancellationToken));
    }

    private async Task<PlatformTenantWhatsAppResponse> BuildAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var license = await dbContext.TenantLicenses.AsNoTracking().Where(x => x.TenantId == tenantId).Select(x => new { x.WhatsAppEnabled }).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("This tenant has no license yet.");
        return new PlatformTenantWhatsAppResponse(tenantId, license.WhatsAppEnabled,
            await outbox.CountThisMonthAsync(tenantId, DateTime.UtcNow, cancellationToken), options.Value.MonthlyCapPerTenant,
            accountResolver.Resolve(tenantId) is not null);
    }
}
