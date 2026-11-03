using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize]
public sealed class SystemsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet("api/clients/{clientId:guid}/sites/{siteId:guid}/systems")]
    public async Task<ActionResult<IReadOnlyCollection<SystemRecordResponse>>> List(Guid clientId, Guid siteId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var systems = await QuerySystems()
            .Where(x => x.ClientId == clientId && x.SiteId == siteId)
            .OrderBy(x => x.SystemName)
            .ToListAsync(cancellationToken);

        var systemIds = systems.Select(x => x.Id).ToList();
        var assetCounts = await dbContext.Assets
            .Where(x => x.TenantId == TenantId && x.SystemId.HasValue && systemIds.Contains(x.SystemId.Value) && x.Status != "Inactive")
            .GroupBy(x => x.SystemId!.Value)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        var openWorkOrderCounts = await dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId && x.SystemId.HasValue && systemIds.Contains(x.SystemId.Value) && x.Status != "Completed" && x.Status != "Cancelled" && x.Status != "Closed")
            .GroupBy(x => x.SystemId!.Value)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        return Ok(systems.Select(system => Map(system,
            assetCounts.TryGetValue(system.Id, out var assetCount) ? assetCount : 0,
            openWorkOrderCounts.TryGetValue(system.Id, out var workOrderCount) ? workOrderCount : 0)).ToList());
    }

    [HttpGet("api/clients/{clientId:guid}/sites/{siteId:guid}/systems/{systemId:guid}")]
    public async Task<ActionResult<SystemDetailResponse>> Get(Guid clientId, Guid siteId, Guid systemId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        return Ok(await BuildSystemDetailAsync(systemId, clientId, siteId, cancellationToken));
    }

    [HttpGet("api/systems/{id:guid}")]
    public async Task<ActionResult<SystemDetailResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var system = await QuerySystems()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("System not found.");

        return Ok(await BuildSystemDetailAsync(system.Id, system.ClientId, system.SiteId, cancellationToken));
    }

    [HttpPost("api/clients/{clientId:guid}/sites/{siteId:guid}/systems")]
    public async Task<ActionResult<SystemRecordResponse>> Create(Guid clientId, Guid siteId, [FromBody] UpsertSystemRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        Validate(request);
        await EnsureSiteBelongsToClientAsync(clientId, siteId, cancellationToken);

        var exists = await dbContext.Systems.AnyAsync(
            x => x.TenantId == TenantId && x.SiteId == siteId && x.SystemName.ToLower() == request.SystemName.Trim().ToLower(),
            cancellationToken);
        if (exists)
        {
            throw new BusinessRuleException("System name already exists for this site.");
        }

        var system = new OperationalSystem
        {
            TenantId = TenantId,
            ClientId = clientId,
            SiteId = siteId,
            SystemName = request.SystemName.Trim(),
            SystemType = request.SystemType.Trim(),
            Description = Normalize(request.Description),
            Status = NormalizeStatus(request.Status),
            Manufacturer = Normalize(request.Manufacturer),
            Model = Normalize(request.Model),
            SerialNumber = Normalize(request.SerialNumber),
            InstallationDate = DateTimeNormalizationHelper.NormalizeNullableToUtc(request.InstallationDate),
            Notes = Normalize(request.Notes)
        };

        dbContext.Systems.Add(system);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Map(system, 0, 0));
    }

    [HttpPut("api/clients/{clientId:guid}/sites/{siteId:guid}/systems/{systemId:guid}")]
    public async Task<ActionResult<SystemRecordResponse>> Update(Guid clientId, Guid siteId, Guid systemId, [FromBody] UpsertSystemRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        Validate(request);
        var system = await dbContext.Systems.SingleOrDefaultAsync(
            x => x.TenantId == TenantId && x.ClientId == clientId && x.SiteId == siteId && x.Id == systemId,
            cancellationToken) ?? throw new NotFoundException("System not found.");

        system.SystemName = request.SystemName.Trim();
        system.SystemType = request.SystemType.Trim();
        system.Description = Normalize(request.Description);
        system.Status = NormalizeStatus(request.Status);
        system.Manufacturer = Normalize(request.Manufacturer);
        system.Model = Normalize(request.Model);
        system.SerialNumber = Normalize(request.SerialNumber);
        system.InstallationDate = DateTimeNormalizationHelper.NormalizeNullableToUtc(request.InstallationDate);
        system.Notes = Normalize(request.Notes);
        await dbContext.SaveChangesAsync(cancellationToken);

        var assetCount = await dbContext.Assets.CountAsync(x => x.TenantId == TenantId && x.SystemId == systemId && x.Status != "Inactive", cancellationToken);
        var openWorkOrderCount = await dbContext.WorkOrders.CountAsync(x => x.TenantId == TenantId && x.SystemId == systemId && x.Status != "Completed" && x.Status != "Cancelled" && x.Status != "Closed", cancellationToken);
        return Ok(Map(system, assetCount, openWorkOrderCount));
    }

    [HttpPut("api/systems/{systemId:guid}")]
    public async Task<ActionResult<SystemRecordResponse>> UpdateById(Guid systemId, [FromBody] UpsertSystemRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var system = await dbContext.Systems
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == systemId, cancellationToken)
            ?? throw new NotFoundException("System not found.");

        return await Update(system.ClientId, system.SiteId, systemId, request, cancellationToken);
    }

    [HttpDelete("api/systems/{systemId:guid}")]
    public async Task<IActionResult> Delete(Guid systemId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var system = await dbContext.Systems
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == systemId, cancellationToken)
            ?? throw new NotFoundException("System not found.");

        dbContext.Systems.Remove(system);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private IQueryable<OperationalSystem> QuerySystems() =>
        dbContext.Systems
            .Include(x => x.Client)
            .Include(x => x.Site)
            .Where(x => x.TenantId == TenantId);

    private async Task<SystemDetailResponse> BuildSystemDetailAsync(Guid systemId, Guid clientId, Guid siteId, CancellationToken cancellationToken)
    {
        var system = await QuerySystems()
            .SingleOrDefaultAsync(x => x.ClientId == clientId && x.SiteId == siteId && x.Id == systemId, cancellationToken)
            ?? throw new NotFoundException("System not found.");

        var assets = await dbContext.Assets
            .Include(x => x.AssetCategory)
            .Where(x => x.TenantId == TenantId && x.SystemId == systemId && x.Status != "Inactive")
            .OrderBy(x => x.AssetName)
            .ToListAsync(cancellationToken);

        var assetIds = assets.Select(x => x.Id).ToList();
        var openWorkOrderCounts = await dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId && x.AssetId.HasValue && assetIds.Contains(x.AssetId.Value) && x.Status != "Completed" && x.Status != "Cancelled" && x.Status != "Closed")
            .GroupBy(x => x.AssetId!.Value)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        var workOrders = await dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId && x.SystemId == systemId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .Select(x => new SystemWorkOrderResponse(x.Id, x.WorkOrderNumber, x.Title, x.Status, x.WorkType, x.Priority, x.AssetId, x.Asset != null ? x.Asset.AssetName : null))
            .ToListAsync(cancellationToken);

        var relatedDocuments = await dbContext.KnowledgeDocuments
            .Where(x => x.TenantId == TenantId && x.LinkedSystemId == systemId)
            .OrderByDescending(x => x.UploadedAt)
            .Take(10)
            .Select(x => new RelatedSystemDocumentResponse(x.Id, x.Title, x.FileName, x.Status, x.UploadedAt))
            .ToListAsync(cancellationToken);

        return new SystemDetailResponse(
            Map(system, assets.Count, workOrders.Count(x => x.Status is not "Completed" and not "Cancelled" and not "Closed")),
            assets.Select(asset => new SystemAssetResponse(
                asset.Id,
                asset.AssetName,
                asset.AssetCode,
                asset.AssetType,
                asset.AssetCategory != null ? asset.AssetCategory.Name : null,
                asset.Status,
                asset.LastPmDate,
                asset.NextPmDate,
                openWorkOrderCounts.TryGetValue(asset.Id, out var assetOpenCount) ? assetOpenCount : 0)).ToList(),
            workOrders,
            assets.Count(x => x.NextPmDate.HasValue && x.NextPmDate.Value.Date >= DateTime.UtcNow.Date && x.NextPmDate.Value.Date <= DateTime.UtcNow.Date.AddDays(30)),
            relatedDocuments);
    }

    private async Task EnsureSiteBelongsToClientAsync(Guid clientId, Guid siteId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Sites.AnyAsync(x => x.TenantId == TenantId && x.ClientId == clientId && x.Id == siteId, cancellationToken);
        if (!exists)
        {
            throw new BusinessRuleException("Site does not belong to the selected client.");
        }
    }

    private static void Validate(UpsertSystemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SystemName))
        {
            throw new BusinessRuleException("System name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.SystemType))
        {
            throw new BusinessRuleException("System type is required.");
        }
    }

    private static string NormalizeStatus(string? status) =>
        string.IsNullOrWhiteSpace(status) ? "Active" : status.Trim();

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static SystemRecordResponse Map(OperationalSystem system, int assetCount, int openWorkOrderCount) =>
        new(
            system.Id,
            system.TenantId,
            system.ClientId,
            system.Client?.ClientName,
            system.SiteId,
            system.Site?.SiteName,
            system.SystemName,
            system.SystemType,
            system.Description,
            system.Status,
            system.Manufacturer,
            system.Model,
            system.SerialNumber,
            system.InstallationDate,
            system.Notes,
            assetCount,
            openWorkOrderCount,
            system.CreatedAt,
            system.UpdatedAt);
}

public sealed record UpsertSystemRequest(
    string SystemName,
    string SystemType,
    string? Description,
    string? Status,
    string? Manufacturer,
    string? Model,
    string? SerialNumber,
    DateTime? InstallationDate,
    string? Notes);

public sealed record SystemRecordResponse(
    Guid Id,
    Guid TenantId,
    Guid ClientId,
    string? ClientName,
    Guid SiteId,
    string? SiteName,
    string SystemName,
    string SystemType,
    string? Description,
    string Status,
    string? Manufacturer,
    string? Model,
    string? SerialNumber,
    DateTime? InstallationDate,
    string? Notes,
    int AssetCount,
    int OpenWorkOrders,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record SystemAssetResponse(Guid Id, string AssetName, string AssetCode, string? AssetType, string? AssetCategory, string Status, DateTime? LastPmServiceDate, DateTime? NextPmDueDate, int OpenWorkOrders);
public sealed record SystemWorkOrderResponse(Guid Id, string WorkOrderNumber, string Title, string Status, string WorkType, string Priority, Guid? AssetId, string? AssetName);
public sealed record RelatedSystemDocumentResponse(Guid Id, string Title, string FileName, string Status, DateTime UploadedAt);
public sealed record SystemDetailResponse(SystemRecordResponse System, IReadOnlyCollection<SystemAssetResponse> Assets, IReadOnlyCollection<SystemWorkOrderResponse> WorkOrders, int UpcomingPmCount, IReadOnlyCollection<RelatedSystemDocumentResponse> RelatedDocuments);
