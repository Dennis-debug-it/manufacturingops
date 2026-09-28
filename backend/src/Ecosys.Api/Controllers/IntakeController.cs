using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Ecosys.Api.Controllers;

[ApiController]
[AllowAnonymous]
[EnableRateLimiting("PublicEndpoints")]
[Route("api/intake")]
public sealed class IntakeController(
    AppDbContext dbContext,
    ILicenseGuardService licenseGuardService,
    IDocumentNumberingService documentNumberingService)
    : ControllerBase
{
    [HttpPost("email/webhook")]
    public async Task<ActionResult<IntakeResponse>> EmailWebhook([FromBody] EmailIntakeRequest request, CancellationToken cancellationToken)
    {
        // Secret first, licence second. The other order is a pre-auth oracle: an unlicensed tenant
        // answers differently from an unknown one, so a caller with no secret at all could probe
        // which tenant ids exist and which features they hold.
        await ValidateSecretAsync(request.TenantId, request.Secret, cancellationToken);
        await EnsureTenantAcceptsIntakeAsync(request.TenantId, cancellationToken);
        await licenseGuardService.EnsureFeatureEnabledAsync(request.TenantId, LicenseFeatures.EmailIngestion, cancellationToken);
        var workOrder = await CreateWorkOrderAsync(request.TenantId, request.BranchId, request.ClientId, request.AssetId, request.Subject, request.Body, request.Priority, cancellationToken);
        return Ok(new IntakeResponse(workOrder.Id, workOrder.WorkOrderNumber));
    }

    [HttpPost("monitoring/webhook")]
    public async Task<ActionResult<IntakeResponse>> MonitoringWebhook([FromBody] MonitoringIntakeRequest request, CancellationToken cancellationToken)
    {
        await ValidateSecretAsync(request.TenantId, request.Secret, cancellationToken);
        await EnsureTenantAcceptsIntakeAsync(request.TenantId, cancellationToken);
        await licenseGuardService.EnsureFeatureEnabledAsync(request.TenantId, LicenseFeatures.MonitoringIntegration, cancellationToken);
        var workOrder = await CreateWorkOrderAsync(request.TenantId, request.BranchId, request.ClientId, request.AssetId, request.Title, request.Description, request.Priority, cancellationToken);
        return Ok(new IntakeResponse(workOrder.Id, workOrder.WorkOrderNumber));
    }

    [HttpPost("/api/integrations/webhooks/{endpointSlug}")]
    public async Task<ActionResult<IntakeResponse>> ReceiveMonitoringWebhook(string endpointSlug, [FromBody] MonitoringWebhookPayload request, CancellationToken cancellationToken)
    {
        var integration = await dbContext.MonitoringWebhookIntegrations
            .SingleOrDefaultAsync(x => x.EndpointSlug == endpointSlug, cancellationToken)
            ?? throw new NotFoundException("Webhook endpoint was not found.");

        var secret = Request.Headers["X-Webhook-Secret"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ForbiddenException("Webhook secret is invalid.");
        }

        if (!FixedTimeEquals(HashSecret(secret), integration.SecretHash))
        {
            throw new ForbiddenException("Webhook secret is invalid.");
        }

        await EnsureTenantAcceptsIntakeAsync(integration.TenantId, cancellationToken);
        await licenseGuardService.EnsureFeatureEnabledAsync(integration.TenantId, LicenseFeatures.MonitoringIntegration, cancellationToken);

        integration.LastReceivedAt = DateTime.UtcNow;
        integration.LastStatus = "Received";
        integration.LastError = null;

        if (!integration.IsActive || !integration.CreateWorkOrderOnAlert)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return Accepted(new IntakeResponse(Guid.Empty, "IGNORED"));
        }

        var clientId = request.ClientId ?? integration.DefaultClientId
            ?? throw new BusinessRuleException("A default client must be configured before webhook alerts can create work orders.");

        var workOrder = await CreateWorkOrderAsync(
            integration.TenantId,
            request.BranchId ?? integration.DefaultBranchId,
            clientId,
            request.AssetId ?? integration.DefaultAssetId,
            string.IsNullOrWhiteSpace(request.Title) ? $"{integration.Name} alert" : request.Title,
            request.Description,
            request.Priority ?? integration.DefaultPriority,
            cancellationToken);

        integration.LastStatus = "Work order created";
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new IntakeResponse(workOrder.Id, workOrder.WorkOrderNumber));
    }

    /// <summary>
    /// Anonymous webhooks never pass through TenantLicenseEnforcementMiddleware (it only runs for
    /// authenticated tenant requests), so the same access rules are applied here: a deactivated,
    /// suspended or read-only (expired) tenant must not keep receiving new work orders (TM-P2-04).
    /// </summary>
    private async Task EnsureTenantAcceptsIntakeAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenantIsActive = await dbContext.Tenants.Where(x => x.Id == tenantId).Select(x => x.IsActive).SingleOrDefaultAsync(cancellationToken);
        if (!tenantIsActive)
        {
            throw new ForbiddenException("This workspace is not accepting new requests.");
        }

        await licenseGuardService.EnsureTenantCanMutateAsync(tenantId, cancellationToken);
    }

    private async Task ValidateSecretAsync(Guid tenantId, string secret, CancellationToken cancellationToken)
    {
        var secretHash = HashSecret(secret);
        var integrationExists = await dbContext.MonitoringWebhookIntegrations.AnyAsync(
            x => x.TenantId == tenantId && x.SecretHash == secretHash && x.IsActive,
            cancellationToken);

        if (integrationExists)
        {
            return;
        }

        var settings = await dbContext.MonitoringSettings.SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            ?? throw new ForbiddenException("Monitoring intake is not configured.");

        if (string.IsNullOrWhiteSpace(settings.WebhookSecret) || !FixedTimeEquals(settings.WebhookSecret, secret))
        {
            throw new ForbiddenException("Webhook secret is invalid.");
        }
    }

    private static bool FixedTimeEquals(string expected, string actual) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));

    private async Task<Domain.Entities.WorkOrder> CreateWorkOrderAsync(Guid tenantId, Guid? branchId, Guid clientId, Guid? assetId, string title, string? description, string? priority, CancellationToken cancellationToken)
    {
        await licenseGuardService.EnsureTenantCanMutateAsync(tenantId, cancellationToken);
        await EnsureReferencesBelongToTenantAsync(tenantId, branchId, clientId, assetId, cancellationToken);

        if (string.IsNullOrWhiteSpace(title))
            throw new BusinessRuleException("A work order title is required.");
        if (!await dbContext.Clients.AnyAsync(x => x.Id == clientId && x.TenantId == tenantId && x.IsActive, cancellationToken))
            throw new BusinessRuleException("Client was not found for this tenant.");
        if (branchId.HasValue && !await dbContext.Branches.AnyAsync(
                x => x.Id == branchId && x.TenantId == tenantId && x.IsActive, cancellationToken))
            throw new BusinessRuleException("Branch was not found for this tenant.");

        Domain.Entities.Asset? asset = null;
        if (assetId.HasValue)
        {
            asset = await dbContext.Assets.SingleOrDefaultAsync(
                x => x.Id == assetId && x.TenantId == tenantId && x.ClientId == clientId, cancellationToken)
                ?? throw new BusinessRuleException("Asset was not found for this client and tenant.");
            if (branchId.HasValue && asset.BranchId != branchId)
                throw new BusinessRuleException("Asset does not belong to the selected branch.");
            branchId ??= asset.BranchId;
            if (branchId.HasValue && !await dbContext.Branches.AnyAsync(
                    x => x.Id == branchId && x.TenantId == tenantId && x.IsActive, cancellationToken))
                throw new BusinessRuleException("Asset branch was not found for this tenant.");
        }

        var workOrder = new Domain.Entities.WorkOrder
        {
            TenantId = tenantId,
            BranchId = branchId,
            ClientId = clientId,
            AssetId = assetId,
            SiteId = asset?.SiteId,
            Title = title.Trim(),
            Description = description?.Trim(),
            Priority = string.IsNullOrWhiteSpace(priority) ? "Medium" : priority.Trim(),
            Status = "Open",
            WorkOrderNumber = await documentNumberingService.GenerateAsync(tenantId, branchId, DocumentTypes.WorkOrder, cancellationToken)
        };

        dbContext.WorkOrders.Add(workOrder);
        await dbContext.SaveChangesAsync(cancellationToken);
        return workOrder;
    }

    /// <summary>
    /// Every id on this controller arrives from the caller. The global tenant query filter is
    /// bypassed for an unauthenticated principal and the work order has no composite tenant foreign
    /// key, so without this check a holder of one tenant's webhook secret can attach another
    /// tenant's client, asset or branch to a new work order — which every later
    /// <c>.Include(x =&gt; x.Client)</c> then surfaces in the UI and in generated PDFs.
    /// </summary>
    private async Task EnsureReferencesBelongToTenantAsync(Guid tenantId, Guid? branchId, Guid clientId, Guid? assetId, CancellationToken cancellationToken)
    {
        var clientBelongs = await dbContext.Clients.AnyAsync(x => x.Id == clientId && x.TenantId == tenantId, cancellationToken);
        if (!clientBelongs)
        {
            throw new BusinessRuleException("Client was not found for this tenant.");
        }

        if (assetId.HasValue)
        {
            var assetBelongs = await dbContext.Assets.AnyAsync(x => x.Id == assetId.Value && x.TenantId == tenantId, cancellationToken);
            if (!assetBelongs)
            {
                throw new BusinessRuleException("Asset was not found for this tenant.");
            }
        }

        if (branchId.HasValue)
        {
            var branchBelongs = await dbContext.Branches.AnyAsync(x => x.Id == branchId.Value && x.TenantId == tenantId, cancellationToken);
            if (!branchBelongs)
            {
                throw new BusinessRuleException("Branch was not found for this tenant.");
            }
        }
    }

    private static string HashSecret(string secret) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret.Trim())));
}

public sealed record EmailIntakeRequest(Guid TenantId, Guid? BranchId, Guid ClientId, Guid? AssetId, string Subject, string? Body, string? Priority, string Secret);
public sealed record MonitoringIntakeRequest(Guid TenantId, Guid? BranchId, Guid ClientId, Guid? AssetId, string Title, string? Description, string? Priority, string Secret);
public sealed record MonitoringWebhookPayload(Guid? ClientId, Guid? AssetId, Guid? BranchId, string? Title, string? Description, string? Priority);
public sealed record IntakeResponse(Guid WorkOrderId, string WorkOrderNumber);
