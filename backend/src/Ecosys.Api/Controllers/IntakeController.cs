using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Ecosys.Api.Controllers;

[ApiController]
[AllowAnonymous]
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
        await licenseGuardService.EnsureFeatureEnabledAsync(request.TenantId, LicenseFeatures.EmailIngestion, cancellationToken);
        await ValidateSecretAsync(request.TenantId, request.Secret, cancellationToken);
        var workOrder = await CreateWorkOrderAsync(request.TenantId, request.BranchId, request.ClientId, request.AssetId, request.Subject, request.Body, request.Priority, cancellationToken);
        return Ok(new IntakeResponse(workOrder.Id, workOrder.WorkOrderNumber));
    }

    [HttpPost("monitoring/webhook")]
    public async Task<ActionResult<IntakeResponse>> MonitoringWebhook([FromBody] MonitoringIntakeRequest request, CancellationToken cancellationToken)
    {
        await licenseGuardService.EnsureFeatureEnabledAsync(request.TenantId, LicenseFeatures.MonitoringIntegration, cancellationToken);
        await ValidateSecretAsync(request.TenantId, request.Secret, cancellationToken);
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

        if (!string.Equals(HashSecret(secret), integration.SecretHash, StringComparison.Ordinal))
        {
            throw new ForbiddenException("Webhook secret is invalid.");
        }

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

        if (string.IsNullOrWhiteSpace(settings.WebhookSecret) || !string.Equals(settings.WebhookSecret, secret, StringComparison.Ordinal))
        {
            throw new ForbiddenException("Webhook secret is invalid.");
        }
    }

    private async Task<Domain.Entities.WorkOrder> CreateWorkOrderAsync(Guid tenantId, Guid? branchId, Guid clientId, Guid? assetId, string title, string? description, string? priority, CancellationToken cancellationToken)
    {
        await licenseGuardService.EnsureCanCreateWorkOrderAsync(tenantId, cancellationToken);

        var workOrder = new Domain.Entities.WorkOrder
        {
            TenantId = tenantId,
            BranchId = branchId,
            ClientId = clientId,
            AssetId = assetId,
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

    private static string HashSecret(string secret) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret.Trim())));
}

public sealed record EmailIntakeRequest(Guid TenantId, Guid? BranchId, Guid ClientId, Guid? AssetId, string Subject, string? Body, string? Priority, string Secret);
public sealed record MonitoringIntakeRequest(Guid TenantId, Guid? BranchId, Guid ClientId, Guid? AssetId, string Title, string? Description, string? Priority, string Secret);
public sealed record MonitoringWebhookPayload(Guid? ClientId, Guid? AssetId, Guid? BranchId, string? Title, string? Description, string? Priority);
public sealed record IntakeResponse(Guid WorkOrderId, string WorkOrderNumber);
