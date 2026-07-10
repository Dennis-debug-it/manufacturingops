using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>Platform-admin support tools that operate on a target tenant workspace.</summary>
[ApiController]
[Authorize(Policy = "PlatformSettingsAccess")]
[Route("api/platform/support")]
public sealed class PlatformSupportToolsController(
    AppDbContext dbContext,
    IEmailOutboxService emailOutboxService,
    IAuditLogService auditLogService,
    ITenantContext tenantContext) : ControllerBase
{
    /// <summary>Re-queue the workspace welcome email to a tenant's contact address.</summary>
    [HttpPost("tenants/{tenantId:guid}/resend-welcome")]
    public async Task<ActionResult<SupportActionResult>> ResendWelcome(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await LoadTenantAsync(tenantId, cancellationToken);
        var recipient = NormalizeEmail(tenant.ContactEmail ?? tenant.Email)
            ?? throw new BusinessRuleException("Tenant has no contact email on file.");

        var rootEmail = await dbContext.EmailSettings.SingleOrDefaultAsync(x => x.TenantId == PlatformConstants.RootTenantId, cancellationToken);
        var senderName = rootEmail?.SenderName ?? "Ecosys";
        var senderEmail = rootEmail?.SenderAddress ?? "no-reply@ecosys.io";

        var subject = $"Welcome to Ecosys — {tenant.Name}";
        var body = $"Hello {tenant.ContactEmail ?? tenant.Name},\n\nYour Ecosys workspace '{tenant.Name}' is ready. " +
                   "Sign in with your administrator credentials to get started.\n\n— The Ecosys Team";

        await emailOutboxService.QueueEmailAsync(
            new QueueEmailRequest(
                PlatformConstants.RootTenantId,
                "platform.support.welcome-resend",
                "workspace-welcome",
                recipient,
                tenant.Name,
                senderName,
                senderEmail,
                null,
                subject,
                $"<p>{System.Net.WebUtility.HtmlEncode(body).Replace("\n", "<br/>")}</p>",
                body,
                tenantContext.UserId),
            cancellationToken);

        await auditLogService.LogAsync(null, tenantContext.UserId, "platform.support.welcome-resent", nameof(Tenant), tenantId.ToString(), $"Welcome email re-queued to {recipient}.", cancellationToken);
        return Ok(new SupportActionResult(true, $"Welcome email queued to {recipient}."));
    }

    /// <summary>Reset a document numbering sequence for a tenant (unlocked sequences only).</summary>
    [HttpPost("tenants/{tenantId:guid}/reset-numbering")]
    public async Task<ActionResult<SupportActionResult>> ResetNumbering(Guid tenantId, [FromBody] ResetNumberingRequest request, CancellationToken cancellationToken)
    {
        await LoadTenantAsync(tenantId, cancellationToken);
        var docType = DocumentTypes.Normalize(request.DocumentType);
        var nextNumber = request.NextNumber is { } n && n > 0 ? n : 1;

        var setting = await dbContext.NumberingSettings
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.DocumentType == docType && x.BranchId == null, cancellationToken)
            ?? throw new NotFoundException($"No numbering sequence configured for {docType}.");
        if (setting.IsLocked)
            throw new BusinessRuleException($"The {docType} numbering sequence is locked and cannot be reset.");

        var previous = setting.NextNumber;
        setting.NextNumber = nextNumber;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(null, tenantContext.UserId, "platform.support.numbering-reset", nameof(NumberingSetting), setting.Id.ToString(), $"{docType} numbering reset from {previous} to {nextNumber}.", cancellationToken);
        return Ok(new SupportActionResult(true, $"{docType} numbering reset to {nextNumber} (was {previous})."));
    }

    /// <summary>Recompute and return a tenant's live record counts — a diagnostic refresh.</summary>
    [HttpPost("tenants/{tenantId:guid}/reindex")]
    public async Task<ActionResult<TenantReindexResult>> Reindex(Guid tenantId, CancellationToken cancellationToken)
    {
        await LoadTenantAsync(tenantId, cancellationToken);
        var counts = await ComputeCountsAsync(tenantId, cancellationToken);
        await auditLogService.LogAsync(null, tenantContext.UserId, "platform.support.reindex", nameof(Tenant), tenantId.ToString(), "Tenant record counts recomputed.", cancellationToken);
        return Ok(new TenantReindexResult(true, DateTime.UtcNow, counts));
    }

    /// <summary>Export a tenant's core data as a downloadable JSON bundle.</summary>
    [HttpGet("tenants/{tenantId:guid}/export")]
    public async Task<IActionResult> Export(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await LoadTenantAsync(tenantId, cancellationToken);
        var counts = await ComputeCountsAsync(tenantId, cancellationToken);

        var clients = await dbContext.Clients.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.ClientName)
            .Select(x => new { x.Id, x.ClientName, x.Email, x.Phone })
            .Take(1000)
            .ToListAsync(cancellationToken);

        var workOrders = await dbContext.WorkOrders.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.WorkOrderNumber, x.Title, x.Status, x.CreatedAt })
            .Take(500)
            .ToListAsync(cancellationToken);

        var bundle = new
        {
            exportedAt = DateTime.UtcNow,
            tenant = new { tenant.Id, tenant.Name, tenant.CompanyName, tenant.Email, tenant.Country, tenant.IsActive },
            counts,
            clients,
            workOrders,
        };

        var json = JsonSerializer.SerializeToUtf8Bytes(bundle, new JsonSerializerOptions { WriteIndented = true });
        await auditLogService.LogAsync(null, tenantContext.UserId, "platform.support.export", nameof(Tenant), tenantId.ToString(), "Tenant data exported.", cancellationToken);
        var slug = new string((tenant.Name ?? "tenant").Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return File(json, "application/json", $"tenant-{slug}-export-{DateTime.UtcNow:yyyyMMdd}.json");
    }

    private async Task<TenantRecordCounts> ComputeCountsAsync(Guid tenantId, CancellationToken cancellationToken) => new(
        await dbContext.WorkOrders.CountAsync(x => x.TenantId == tenantId, cancellationToken),
        await dbContext.Clients.CountAsync(x => x.TenantId == tenantId, cancellationToken),
        await dbContext.Assets.CountAsync(x => x.TenantId == tenantId, cancellationToken),
        await dbContext.TenantInvoices.CountAsync(x => x.TenantId == tenantId, cancellationToken),
        await dbContext.Contracts.CountAsync(x => x.TenantId == tenantId, cancellationToken),
        await dbContext.Users.CountAsync(x => x.TenantId == tenantId, cancellationToken));

    private async Task<Tenant> LoadTenantAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await dbContext.Tenants.SingleOrDefaultAsync(x => x.Id == tenantId, cancellationToken)
        ?? throw new NotFoundException("Tenant not found.");

    private static string? NormalizeEmail(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}

public sealed record SupportActionResult(bool Success, string Message);
public sealed record ResetNumberingRequest([Required] string DocumentType, long? NextNumber);
public sealed record TenantRecordCounts(int WorkOrders, int Clients, int Assets, int Invoices, int Contracts, int Users);
public sealed record TenantReindexResult(bool Success, DateTime RecomputedAt, TenantRecordCounts Counts);
