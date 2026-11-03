using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// The website enquiry form (Settings › Sales). Each workspace gets a public link holding a random key,
/// shown as a page the workspace links to or embeds on its website; what people send becomes a lead
/// (<see cref="SalesInboundLeads"/>). Unknown or retired keys, a switched-off form and plans without the
/// CRM all answer the same "not found", so the endpoint says nothing about which workspaces exist.
/// A hidden field that people leave empty catches most bots.
/// </summary>
[ApiController]
[AllowAnonymous]
[EnableRateLimiting("PublicEndpoints")]
[Route("api/public/lead-form/{key}")]
public sealed class SalesLeadFormController(
    AppDbContext dbContext,
    ISalesOwnerResolver ownerResolver,
    IPushNotificationQueue pushNotificationQueue,
    ILicenseGuardService licenseGuardService) : ControllerBase
{
    public static string NewKey() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    [HttpGet]
    public async Task<ActionResult<SalesLeadFormInfo>> Get(string key, CancellationToken cancellationToken)
    {
        var tenantId = await ResolveAsync(key, cancellationToken);
        var name = await dbContext.Tenants.AsNoTracking().Where(x => x.Id == tenantId)
            .Select(x => x.CompanyName != "" ? x.CompanyName : x.Name).FirstAsync(cancellationToken);
        return Ok(new SalesLeadFormInfo(name));
    }

    [HttpPost]
    public async Task<ActionResult<SalesLeadFormReceipt>> Submit(string key, [FromBody] SalesLeadFormRequest request, CancellationToken cancellationToken)
    {
        var tenantId = await ResolveAsync(key, cancellationToken);
        // Bots fill every field; people never see this one. Answer as if it worked.
        if (!string.IsNullOrWhiteSpace(request.Website))
            return Ok(new SalesLeadFormReceipt(true));
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BusinessRuleException("Please tell us your name.");
        if (string.IsNullOrWhiteSpace(request.Email) && string.IsNullOrWhiteSpace(request.Phone))
            throw new BusinessRuleException("Please give an email address or a phone number so we can reach you.");
        if (!string.IsNullOrWhiteSpace(request.Email) && !System.Net.Mail.MailAddress.TryCreate(request.Email.Trim(), out _))
            throw new BusinessRuleException("That email address doesn't look right.");
        if (string.IsNullOrWhiteSpace(request.Message))
            throw new BusinessRuleException("Please tell us what you need.");

        await SalesInboundLeads.CreateAsync(dbContext, ownerResolver, pushNotificationQueue, tenantId,
            new SalesInboundLead(request.Company, request.Name, request.Email, request.Phone, request.Location, request.Message, "Website"), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new SalesLeadFormReceipt(true));
    }

    private async Task<Guid> ResolveAsync(string key, CancellationToken cancellationToken)
    {
        var normalized = key?.Trim().ToLowerInvariant();
        var tenantId = string.IsNullOrEmpty(normalized) || normalized.Length > 64 ? null : await dbContext.TenantSalesSettings.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.WebFormEnabled && x.WebFormKey == normalized)
            .Select(x => (Guid?)x.TenantId)
            .FirstOrDefaultAsync(cancellationToken);
        if (tenantId is null)
            throw new NotFoundException("This form is not available.");
        var snapshot = await licenseGuardService.GetSnapshotAsync(tenantId.Value, cancellationToken);
        if (snapshot.IsSuspended || !ServiceOpsPlans.RequiredModules("SalesCrm", "/api/crm").All(m => snapshot.Modules.Contains(m, StringComparer.OrdinalIgnoreCase)))
            throw new NotFoundException("This form is not available.");
        return tenantId.Value;
    }
}

public sealed record SalesLeadFormInfo(string CompanyName);

public sealed record SalesLeadFormRequest(
    [StringLength(150)] string? Name,
    [StringLength(200)] string? Company,
    [StringLength(256)] string? Email,
    [StringLength(50)] string? Phone,
    [StringLength(250)] string? Location,
    [StringLength(3000)] string? Message,
    [StringLength(200)] string? Website = null);

public sealed record SalesLeadFormReceipt(bool Received);
