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
public sealed class SitesController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IDocumentNumberingService numberingService) : TenantAwareControllerBase(tenantContext)
{
    private static readonly string[] AllowedSiteStatuses = ["Active", "Inactive", "Closed"];
    private static readonly string[] AllowedSiteTypes = ["Branch", "HQ", "Data Centre", "Warehouse", "Factory", "Other"];

    [HttpGet("api/clients/{clientId:guid}/sites")]
    public async Task<IActionResult> List(Guid clientId, [FromQuery] string? region, [FromQuery] string? status, [FromQuery] string? q, CancellationToken ct)
    {
        var query = dbContext.Sites
            .Where(x => x.TenantId == TenantId && x.ClientId == clientId);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == NormalizeSiteStatus(status));
        if (!string.IsNullOrWhiteSpace(region))
            query = query.Where(x => x.Region == region);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(x => x.SiteName.Contains(q) || x.SiteCode.Contains(q));

        var sites = await query.OrderBy(x => x.SiteName).ToListAsync(ct);
        return Ok(sites.Select(MapSite));
    }

    [HttpPost("api/clients/{clientId:guid}/sites")]
    public async Task<IActionResult> Create(Guid clientId, [FromBody] UpsertSiteRequest request, CancellationToken ct)
    {
        userAccessService.EnsureAdmin();
        Validate(request);

        var clientExists = await dbContext.Clients
            .AnyAsync(x => x.TenantId == TenantId && x.Id == clientId, ct);
        if (!clientExists)
            throw new NotFoundException("Client not found.");

        var siteCode = await numberingService.GenerateAsync(TenantId, null, DocumentTypes.Site, ct);

        var site = new Site
        {
            TenantId = TenantId,
            ClientId = clientId,
            SiteCode = siteCode,
            SiteName = request.SiteName.Trim(),
            SiteType = NormalizeSiteType(request.SiteType),
            Status = NormalizeSiteStatus(request.Status),
            StreetAddress = NormalizeOptional(request.StreetAddress),
            AreaEstate = NormalizeOptional(request.AreaEstate),
            TownCity = NormalizeOptional(request.TownCity),
            County = NormalizeOptional(request.County),
            Country = NormalizeOptional(request.Country),
            Region = NormalizeOptional(request.Region),
            ContactPerson = NormalizeOptional(request.ContactPerson),
            ContactPhone = NormalizeOptional(request.ContactPhone),
            ContactEmail = NormalizeOptional(request.ContactEmail),
            AlternateContact = NormalizeOptional(request.AlternateContact),
            OperatingHours = NormalizeOptional(request.OperatingHours),
            AccessNotes = NormalizeOptional(request.AccessNotes),
            SpecialInstructions = NormalizeOptional(request.SpecialInstructions)
        };
        dbContext.Sites.Add(site);
        await dbContext.SaveChangesAsync(ct);
        return Ok(MapSite(site));
    }

    [HttpGet("api/clients/{clientId:guid}/sites/{siteId:guid}")]
    public async Task<IActionResult> Get(Guid clientId, Guid siteId, CancellationToken ct)
    {
        var site = await dbContext.Sites
            .Include(x => x.Client)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ClientId == clientId && x.Id == siteId, ct)
            ?? throw new NotFoundException("Site not found.");

        var systems = await dbContext.Systems
            .Where(x => x.TenantId == TenantId && x.ClientId == clientId && x.SiteId == siteId)
            .OrderBy(x => x.SystemName)
            .ToListAsync(ct);
        var systemIds = systems.Select(x => x.Id).ToList();

        var assets = await dbContext.Assets
            .Include(x => x.System)
            .Where(x => x.TenantId == TenantId && x.ClientId == clientId && x.SiteId == siteId && x.Status != "Inactive")
            .OrderBy(x => x.AssetName)
            .ToListAsync(ct);

        var workOrders = await dbContext.WorkOrders
            .Include(x => x.Asset)
            .Include(x => x.System)
            .Where(x => x.TenantId == TenantId && x.ClientId == clientId && x.SiteId == siteId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        var assetCount = assets.Count;
        var openWoCount = workOrders.Count(x => x.Status != "Completed" && x.Status != "Cancelled" && x.Status != "Closed");
        var upcomingPmCount = assets.Count(x => x.NextPmDate.HasValue && x.NextPmDate.Value.Date >= DateTime.UtcNow.Date && x.NextPmDate.Value.Date <= DateTime.UtcNow.Date.AddDays(30));
        var systemAssetCounts = assets
            .Where(x => x.SystemId.HasValue)
            .GroupBy(x => x.SystemId!.Value)
            .ToDictionary(x => x.Key, x => x.Count());
        var systemOpenWorkOrderCounts = workOrders
            .Where(x => x.SystemId.HasValue && x.Status != "Completed" && x.Status != "Cancelled" && x.Status != "Closed")
            .GroupBy(x => x.SystemId!.Value)
            .ToDictionary(x => x.Key, x => x.Count());

        return Ok(new SiteDetailResponse(
            MapSite(site),
            site.Client?.ClientName,
            systems.Count,
            assetCount,
            openWoCount,
            upcomingPmCount,
            systems.Select(system => new SiteSystemSummaryResponse(
                system.Id,
                system.SystemName,
                system.SystemType,
                system.Status,
                system.Manufacturer,
                system.Model,
                system.SerialNumber,
                system.InstallationDate,
                system.Notes,
                systemAssetCounts.TryGetValue(system.Id, out var systemAssetCount) ? systemAssetCount : 0,
                systemOpenWorkOrderCounts.TryGetValue(system.Id, out var systemOpenCount) ? systemOpenCount : 0,
                system.CreatedAt,
                system.UpdatedAt)).ToList(),
            assets.Select(asset => new SiteAssetSummaryResponse(
                asset.Id,
                asset.SystemId,
                asset.System?.SystemName,
                asset.AssetName,
                asset.AssetCode,
                asset.AssetType,
                asset.Status,
                asset.LastPmDate,
                asset.NextPmDate)).ToList(),
            workOrders.Select(workOrder => new SiteWorkOrderSummaryResponse(
                workOrder.Id,
                workOrder.WorkOrderNumber,
                workOrder.Title,
                workOrder.Status,
                WorkOrderTypes.Normalize(workOrder.WorkType, WorkOrderTypes.CorrectiveMaintenance),
                workOrder.Priority,
                workOrder.SystemId,
                workOrder.System?.SystemName,
                workOrder.AssetId,
                workOrder.Asset?.AssetName,
                workOrder.CreatedAt,
                workOrder.DueDate)).ToList()));
    }

    [HttpPut("api/clients/{clientId:guid}/sites/{siteId:guid}")]
    public async Task<IActionResult> Update(Guid clientId, Guid siteId, [FromBody] UpsertSiteRequest request, CancellationToken ct)
    {
        userAccessService.EnsureAdmin();
        Validate(request);

        var site = await dbContext.Sites
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ClientId == clientId && x.Id == siteId, ct)
            ?? throw new NotFoundException("Site not found.");

        site.SiteName = request.SiteName.Trim();
        site.SiteType = NormalizeSiteType(request.SiteType);
        site.Status = NormalizeSiteStatus(request.Status);
        site.StreetAddress = NormalizeOptional(request.StreetAddress);
        site.AreaEstate = NormalizeOptional(request.AreaEstate);
        site.TownCity = NormalizeOptional(request.TownCity);
        site.County = NormalizeOptional(request.County);
        site.Country = NormalizeOptional(request.Country);
        site.Region = NormalizeOptional(request.Region);
        site.ContactPerson = NormalizeOptional(request.ContactPerson);
        site.ContactPhone = NormalizeOptional(request.ContactPhone);
        site.ContactEmail = NormalizeOptional(request.ContactEmail);
        site.AlternateContact = NormalizeOptional(request.AlternateContact);
        site.OperatingHours = NormalizeOptional(request.OperatingHours);
        site.AccessNotes = NormalizeOptional(request.AccessNotes);
        site.SpecialInstructions = NormalizeOptional(request.SpecialInstructions);

        await dbContext.SaveChangesAsync(ct);
        return Ok(MapSite(site));
    }

    [HttpDelete("api/clients/{clientId:guid}/sites/{siteId:guid}")]
    public async Task<IActionResult> Deactivate(Guid clientId, Guid siteId, CancellationToken ct)
    {
        userAccessService.EnsureAdmin();

        var site = await dbContext.Sites
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ClientId == clientId && x.Id == siteId, ct)
            ?? throw new NotFoundException("Site not found.");

        site.Status = "Inactive";
        await dbContext.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("api/sites/search")]
    public async Task<IActionResult> Search([FromQuery] Guid? clientId, [FromQuery] string? q, [FromQuery] string? region, CancellationToken ct)
    {
        var query = dbContext.Sites
            .Where(x => x.TenantId == TenantId && x.Status == "Active");

        if (clientId.HasValue)
            query = query.Where(x => x.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(region))
            query = query.Where(x => x.Region == region);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(x => x.SiteName.Contains(q) || x.SiteCode.Contains(q));

        var sites = await query
            .OrderBy(x => x.SiteName)
            .Take(25)
            .ToListAsync(ct);

        return Ok(sites.Select(MapSite));
    }

    private static void Validate(UpsertSiteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SiteName))
            throw new BusinessRuleException("Site name is required.");

        _ = NormalizeSiteType(request.SiteType);
        _ = NormalizeSiteStatus(request.Status);
    }

    private static string NormalizeSiteType(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "Branch" : value.Trim();
        var match = AllowedSiteTypes.SingleOrDefault(item => string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase));
        return match ?? throw new BusinessRuleException("Site type must be Branch, HQ, Data Centre, Warehouse, Factory, or Other.");
    }

    private static string NormalizeSiteStatus(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "Active" : value.Trim();
        var match = AllowedSiteStatuses.SingleOrDefault(item => string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase));
        return match ?? throw new BusinessRuleException("Site status must be Active, Inactive, or Closed.");
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static object MapSite(Site s) => new
    {
        s.Id,
        s.TenantId,
        s.ClientId,
        s.SiteCode,
        s.SiteName,
        s.SiteType,
        s.Status,
        s.StreetAddress,
        s.AreaEstate,
        s.TownCity,
        s.County,
        s.Country,
        s.Region,
        s.ContactPerson,
        s.ContactPhone,
        s.ContactEmail,
        s.AlternateContact,
        s.OperatingHours,
        s.AccessNotes,
        s.SpecialInstructions,
        s.CreatedAt,
        s.UpdatedAt
    };
}

public sealed record UpsertSiteRequest(
    string SiteName,
    string? SiteType,
    string? Status,
    string? StreetAddress,
    string? AreaEstate,
    string? TownCity,
    string? County,
    string? Country,
    string? Region,
    string? ContactPerson,
    string? ContactPhone,
    string? ContactEmail,
    string? AlternateContact,
    string? OperatingHours,
    string? AccessNotes,
    string? SpecialInstructions);

public sealed record SiteDetailResponse(
    object Site,
    string? ClientName,
    int SystemCount,
    int AssetCount,
    int OpenWorkOrders,
    int UpcomingPmCount,
    IReadOnlyCollection<SiteSystemSummaryResponse> Systems,
    IReadOnlyCollection<SiteAssetSummaryResponse> Assets,
    IReadOnlyCollection<SiteWorkOrderSummaryResponse> WorkOrders);

public sealed record SiteSystemSummaryResponse(
    Guid SystemId,
    string SystemName,
    string SystemType,
    string Status,
    string? Manufacturer,
    string? Model,
    string? SerialNumber,
    DateTime? InstallationDate,
    string? Notes,
    int AssetCount,
    int OpenWorkOrderCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record SiteAssetSummaryResponse(
    Guid AssetId,
    Guid? SystemId,
    string? SystemName,
    string AssetName,
    string AssetCode,
    string? AssetType,
    string Status,
    DateTime? LastPmServiceDate,
    DateTime? NextPmDueDate);

public sealed record SiteWorkOrderSummaryResponse(
    Guid WorkOrderId,
    string WorkOrderNumber,
    string Title,
    string Status,
    string WorkType,
    string Priority,
    Guid? SystemId,
    string? SystemName,
    Guid? AssetId,
    string? AssetName,
    DateTime CreatedAt,
    DateTime? DueDate);
