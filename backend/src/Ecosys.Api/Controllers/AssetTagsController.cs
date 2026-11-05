using System.ComponentModel.DataAnnotations;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Ecosys.Shared.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ecosys.Api.Controllers;

/// <summary>
/// Asset identification tags: issuing, replacing damaged tags, batch label printing, and the
/// scan lookup that opens an asset from its QR code.
/// </summary>
[ApiController]
[Authorize]
public sealed class AssetTagsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IOrgScopeService orgScopeService,
    IAssetTagNumberService tagNumberService,
    IAssetLabelPdfRenderer labelRenderer,
    IAssetLifecycleService assetLifecycleService,
    IAuditLogService auditLogService,
    IOptions<PublicAppOptions> publicAppOptions) : TenantAwareControllerBase(tenantContext)
{
    private const int MaxBatch = 500;

    [HttpGet("api/assets/{assetId:guid}/tags")]
    public async Task<ActionResult<IReadOnlyCollection<AssetTagResponse>>> History(Guid assetId, CancellationToken cancellationToken)
    {
        await LoadAssetAsync(assetId, cancellationToken);
        var tags = await dbContext.AssetTags
            .Where(x => x.TenantId == TenantId && x.AssetId == assetId)
            .OrderByDescending(x => x.IssuedAt)
            .ToListAsync(cancellationToken);
        return Ok(tags.Select(Map).ToList());
    }

    /// <summary>
    /// Issues a tag. Leave the number blank to take the next one; give a number to register a
    /// pre-printed tag. An asset can have only one active tag: use replace for a damaged one.
    /// </summary>
    [HttpPost("api/assets/{assetId:guid}/tags")]
    public async Task<ActionResult<AssetTagResponse>> Issue(Guid assetId, [FromBody] IssueAssetTagRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var asset = await LoadAssetAsync(assetId, cancellationToken);
        EnsureTaggable(asset);
        var symbology = ResolveSymbology(request.Symbology);
        if (await dbContext.AssetTags.AnyAsync(x => x.TenantId == TenantId && x.AssetId == assetId && x.Status == AssetTagStatuses.Active, cancellationToken))
        {
            throw new BusinessRuleException("This asset already has an active tag. Replace it instead.");
        }

        var number = await ResolveNumberAsync(request.TagNumber, cancellationToken);
        var tag = NewTag(asset, number, symbology);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "asset-tag.issued", nameof(AssetTag), tag.Id.ToString(),
            $"Issued tag {tag.TagNumber} to asset '{asset.AssetName}'.", cancellationToken);

        return Ok(Map(tag));
    }

    /// <summary>Replaces a damaged or lost tag with a new number. The old number still resolves, with a warning.</summary>
    [HttpPost("api/asset-tags/{id:guid}/replace")]
    public async Task<ActionResult<AssetTagResponse>> Replace(Guid id, [FromBody] ReplaceAssetTagRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new BusinessRuleException("Say why the tag is being replaced (for example damaged, faded or missing).");
        }

        var old = await LoadTagAsync(id, cancellationToken);
        if (old.Status != AssetTagStatuses.Active)
        {
            throw new BusinessRuleException("Only an active tag can be replaced.");
        }

        var number = await ResolveNumberAsync(request.NewTagNumber, cancellationToken);
        old.Status = AssetTagStatuses.Replaced;
        old.RetiredAt = DateTime.UtcNow;
        old.RetiredReason = request.Reason.Trim();
        // Free the "one active tag" slot before the new tag is inserted.
        await dbContext.SaveChangesAsync(cancellationToken);

        var tag = NewTag(old.Asset!, number, ResolveSymbology(request.Symbology ?? old.Symbology));
        old.ReplacedByTagId = tag.Id;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "asset-tag.replaced", nameof(AssetTag), tag.Id.ToString(),
            $"Replaced tag {old.TagNumber} with {tag.TagNumber} on asset '{old.Asset!.AssetName}': {old.RetiredReason}", cancellationToken);

        return Ok(Map(tag));
    }

    [HttpPost("api/asset-tags/{id:guid}/retire")]
    public async Task<ActionResult<AssetTagResponse>> Retire(Guid id, [FromBody] RetireAssetTagRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var tag = await LoadTagAsync(id, cancellationToken);
        if (tag.Status != AssetTagStatuses.Active)
        {
            throw new BusinessRuleException("The tag is not active.");
        }

        tag.Status = AssetTagStatuses.Retired;
        tag.RetiredAt = DateTime.UtcNow;
        tag.RetiredReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Map(tag));
    }

    /// <summary>Issues tags to every untagged active asset in the selection (asset ids, or everything under a factory or section).</summary>
    [HttpPost("api/asset-tags/bulk-issue")]
    public async Task<ActionResult<BulkIssueTagsResponse>> BulkIssue([FromBody] BulkIssueTagsRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var symbology = ResolveSymbology(request.Symbology);
        var assets = await SelectAssetsAsync(request.AssetIds, request.OrgNodeId, cancellationToken);
        var tagged = await dbContext.AssetTags
            .Where(x => x.TenantId == TenantId && x.Status == AssetTagStatuses.Active)
            .Select(x => x.AssetId)
            .ToListAsync(cancellationToken);
        var untagged = assets
            .Where(x => !tagged.Contains(x.Id) && !string.Equals(x.Status, "Inactive", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.AssetCode)
            .ToList();
        if (untagged.Count > MaxBatch)
        {
            throw new BusinessRuleException($"Issue at most {MaxBatch} tags at a time.");
        }

        var numbers = await tagNumberService.ReserveAsync(TenantId, untagged.Count, cancellationToken);
        var issued = untagged.Zip(numbers, (asset, number) => NewTag(asset, number, symbology)).ToList();
        await dbContext.SaveChangesAsync(cancellationToken);
        if (issued.Count > 0)
        {
            await auditLogService.LogAsync(TenantId, UserId, "asset-tag.bulk-issued", nameof(AssetTag), issued[0].Id.ToString(),
                $"Issued {issued.Count} tag(s), {issued[0].TagNumber} to {issued[^1].TagNumber}.", cancellationToken);
        }

        return Ok(new BulkIssueTagsResponse(issued.Count, assets.Count - issued.Count, issued.Select(x => x.Id).ToList()));
    }

    /// <summary>
    /// A PDF sheet of labels for the given tags, or for the active tags of the given assets or
    /// everything under a factory or section. Each print is counted, so reprints are visible.
    /// </summary>
    [HttpPost("api/asset-tags/labels")]
    public async Task<IActionResult> Labels([FromBody] PrintLabelsRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var query = dbContext.AssetTags
            .Include(x => x.Asset)
            .Where(x => x.TenantId == TenantId && x.Status == AssetTagStatuses.Active);

        List<AssetTag> tags;
        if (request.TagIds is { Count: > 0 } tagIds)
        {
            tags = await query.Where(x => tagIds.Contains(x.Id)).ToListAsync(cancellationToken);
        }
        else
        {
            var assetIds = (await SelectAssetsAsync(request.AssetIds, request.OrgNodeId, cancellationToken)).Select(x => x.Id).ToList();
            tags = await query.Where(x => assetIds.Contains(x.AssetId)).ToListAsync(cancellationToken);
        }

        var scope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        tags = tags.Where(x => scope.Allows(x.Asset?.OrgNodeId)).OrderBy(x => x.TagNumber).ToList();
        if (tags.Count == 0)
        {
            throw new BusinessRuleException("None of the selected assets have an active tag. Issue tags first.");
        }

        if (tags.Count > MaxBatch)
        {
            throw new BusinessRuleException($"Print at most {MaxBatch} labels at a time.");
        }

        var paths = await LocationPathsAsync(cancellationToken);
        var company = await dbContext.Tenants.Where(x => x.Id == TenantId).Select(x => x.CompanyName).SingleOrDefaultAsync(cancellationToken) ?? string.Empty;
        var labels = tags.Select(x => new AssetLabel(
                x.TagNumber,
                ScanUrl(x.TagNumber),
                x.Asset!.AssetName,
                x.Asset.AssetCode,
                x.Asset.OrgNodeId.HasValue ? paths.GetValueOrDefault(x.Asset.OrgNodeId.Value) : null,
                x.Asset.Criticality))
            .ToList();
        var pdf = labelRenderer.Render(company, labels, string.IsNullOrWhiteSpace(request.Layout) ? AssetLabelLayouts.Small : request.Layout.Trim().ToLowerInvariant());

        var now = DateTime.UtcNow;
        foreach (var tag in tags)
        {
            tag.PrintCount++;
            tag.LastPrintedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return File(pdf, "application/pdf", $"asset-labels-{now:yyyyMMdd-HHmm}.pdf");
    }

    /// <summary>
    /// Opens an asset from a scanned code: a tag number, a scanned label link, or the asset code
    /// as a fallback for assets not yet tagged. Answers "not found" for assets outside the
    /// user's factories.
    /// </summary>
    [HttpGet("api/scan/{*code}")]
    public async Task<ActionResult<ScanResultResponse>> Scan(string code, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new BusinessRuleException("Scan or type a tag number.");
        }

        var normalized = tagNumberService.NormalizeCode(code);
        var tag = await dbContext.AssetTags.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.TagNumber == normalized, cancellationToken);
        var asset = tag is not null
            ? await dbContext.Assets.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == tag.AssetId, cancellationToken)
            : await dbContext.Assets.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.AssetCode.ToUpper() == normalized, cancellationToken);

        var scope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        if (asset is null || !scope.Allows(asset.OrgNodeId))
        {
            throw new NotFoundException($"No asset found for '{normalized}'.");
        }

        var paths = await LocationPathsAsync(cancellationToken);
        var parent = asset.ParentAssetId.HasValue
            ? await dbContext.Assets.Where(x => x.Id == asset.ParentAssetId).Select(x => new ScanAssetLink(x.Id, x.AssetCode, x.AssetName)).SingleOrDefaultAsync(cancellationToken)
            : null;
        var components = await dbContext.Assets
            .Where(x => x.TenantId == TenantId && x.ParentAssetId == asset.Id)
            .OrderBy(x => x.AssetName)
            .Select(x => new ScanAssetLink(x.Id, x.AssetCode, x.AssetName))
            .ToListAsync(cancellationToken);
        var activeTag = tag?.Status == AssetTagStatuses.Active
            ? tag
            : await dbContext.AssetTags.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.AssetId == asset.Id && x.Status == AssetTagStatuses.Active, cancellationToken);
        var meters = await dbContext.Meters
            .Where(x => x.TenantId == TenantId && x.AssetId == asset.Id && x.IsActive)
            .OrderByDescending(x => x.IsPrimary).ThenBy(x => x.Name)
            .Select(x => new ScanMeter(x.Id, x.Name, x.MeterType, x.Unit, x.CurrentValue, x.LastReadingAt))
            .ToListAsync(cancellationToken);
        var closed = Ecosys.Infrastructure.Services.WorkOrderStatuses.ClosedStatuses;
        var openWorkOrders = await dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId && x.AssetId == asset.Id && !closed.Contains(x.Status) && x.Status != "Completed")
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .Select(x => new ScanWorkOrder(x.Id, x.WorkOrderNumber, x.Title, x.Status, x.Priority, x.DueDate))
            .ToListAsync(cancellationToken);
        var history = await dbContext.AssetLifecycleEvents
            .Where(x => x.TenantId == TenantId && x.AssetId == asset.Id)
            .OrderByDescending(x => x.OccurredAt)
            .Take(5)
            .Select(x => new ScanHistoryItem(x.EventType, x.OccurredAt, x.FromValue, x.ToValue, x.Notes))
            .ToListAsync(cancellationToken);

        string? warning = tag switch
        {
            null when activeTag is null => "This asset has no tag yet.",
            null => $"Found by asset code. Its tag is {activeTag!.TagNumber}.",
            { Status: AssetTagStatuses.Replaced } => $"This tag was replaced{(activeTag is null ? string.Empty : $" by {activeTag.TagNumber}")}. Remove the old label.",
            { Status: AssetTagStatuses.Retired } => "This tag has been retired.",
            _ => null
        };
        if (string.Equals(asset.Status, "Inactive", StringComparison.OrdinalIgnoreCase))
        {
            warning = string.Join(" ", new[] { "This asset is retired.", warning }.Where(x => x is not null));
        }

        return Ok(new ScanResultResponse(
            normalized,
            tag is null ? "AssetCode" : "Tag",
            warning,
            activeTag?.TagNumber,
            asset.Id,
            asset.AssetCode,
            asset.AssetName,
            asset.Status,
            asset.OperationalStatus,
            asset.Criticality,
            asset.Manufacturer,
            asset.Model,
            asset.SerialNumber,
            asset.OrgNodeId,
            asset.OrgNodeId.HasValue ? paths.GetValueOrDefault(asset.OrgNodeId.Value) : null,
            parent,
            components,
            meters,
            openWorkOrders,
            history));
    }

    /// <summary>
    /// A compact copy of every asset the user can see, keyed by its tag numbers (old ones too) and
    /// asset code, for the phone to keep so scanning works with no signal. Refreshed when online.
    /// </summary>
    [HttpGet("api/scan/offline-index")]
    public async Task<ActionResult<IReadOnlyCollection<OfflineScanEntry>>> OfflineIndex(CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var scope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        var assets = (await dbContext.Assets.AsNoTracking()
                .Where(x => x.TenantId == TenantId && x.Status != "Inactive")
                .Select(x => new { x.Id, x.AssetCode, x.AssetName, x.OrgNodeId, x.ParentAssetId, x.Criticality, x.OperationalStatus, x.Manufacturer, x.Model, x.SerialNumber })
                .ToListAsync(cancellationToken))
            .Where(x => scope.Allows(x.OrgNodeId))
            .ToList();
        var ids = assets.Select(x => x.Id).ToHashSet();
        var codes = assets.ToDictionary(x => x.Id, x => x.AssetCode);
        var tags = (await dbContext.AssetTags.AsNoTracking()
                .Where(x => x.TenantId == TenantId)
                .Select(x => new { x.AssetId, x.TagNumber, x.Status })
                .ToListAsync(cancellationToken))
            .Where(x => ids.Contains(x.AssetId))
            .ToLookup(x => x.AssetId);
        var meters = (await dbContext.Meters.AsNoTracking()
                .Where(x => x.TenantId == TenantId && x.IsActive)
                .Select(x => new { x.AssetId, x.Id, x.Name, x.Unit, x.CurrentValue, x.LastReadingAt, x.IsCumulative })
                .ToListAsync(cancellationToken))
            .Where(x => ids.Contains(x.AssetId))
            .ToLookup(x => x.AssetId);
        var paths = await LocationPathsAsync(cancellationToken);

        return Ok(assets
            .OrderBy(x => x.AssetCode)
            .Select(x => new OfflineScanEntry(
                x.Id,
                x.AssetCode,
                x.AssetName,
                x.OrgNodeId.HasValue ? paths.GetValueOrDefault(x.OrgNodeId.Value) : null,
                x.ParentAssetId.HasValue ? codes.GetValueOrDefault(x.ParentAssetId.Value) : null,
                x.Criticality,
                x.OperationalStatus,
                x.Manufacturer,
                x.Model,
                x.SerialNumber,
                tags[x.Id].FirstOrDefault(t => t.Status == AssetTagStatuses.Active)?.TagNumber,
                tags[x.Id].Where(t => t.Status != AssetTagStatuses.Active).Select(t => t.TagNumber).ToList(),
                meters[x.Id].Select(m => new OfflineScanMeter(m.Id, m.Name, m.Unit, m.IsCumulative, m.CurrentValue, m.LastReadingAt)).ToList()))
            .ToList());
    }

    private void EnsureCanManage() => userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageAssets);

    private static void EnsureTaggable(Asset asset)
    {
        if (string.Equals(asset.Status, "Inactive", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("A retired asset can't be tagged.");
        }
    }

    private static string ResolveSymbology(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? AssetTagSymbologies.Qr
            : AssetTagSymbologies.Find(value) ?? throw new BusinessRuleException($"Tag type must be one of: {string.Join(", ", AssetTagSymbologies.All)}.");

    private async Task<string> ResolveNumberAsync(string? requested, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return (await tagNumberService.ReserveAsync(TenantId, 1, cancellationToken))[0];
        }

        var number = tagNumberService.NormalizeCode(requested);
        if (number.Length > 40 || number.Any(c => !char.IsLetterOrDigit(c) && c is not '-' and not '_' and not '.'))
        {
            throw new BusinessRuleException("A tag number can use letters, digits, '-', '_' and '.', up to 40 characters.");
        }

        // Tag numbers are never reused, even after a tag is replaced or retired.
        if (await dbContext.AssetTags.AnyAsync(x => x.TenantId == TenantId && x.TagNumber == number, cancellationToken))
        {
            throw new ConflictException($"Tag {number} has already been used.");
        }

        return number;
    }

    private AssetTag NewTag(Asset asset, string number, string symbology)
    {
        var tag = new AssetTag
        {
            TenantId = TenantId,
            AssetId = asset.Id,
            Asset = asset,
            TagNumber = number,
            Symbology = symbology
        };
        dbContext.AssetTags.Add(tag);
        var entry = assetLifecycleService.Record(asset, AssetLifecycleEventTypes.Tagged, UserId, UserDisplayName);
        entry.ToValue = number;
        return tag;
    }

    private async Task<List<Asset>> SelectAssetsAsync(IReadOnlyCollection<Guid>? assetIds, Guid? orgNodeId, CancellationToken cancellationToken)
    {
        var query = dbContext.Assets.Where(x => x.TenantId == TenantId);
        if (assetIds is { Count: > 0 })
        {
            var ids = assetIds.Distinct().ToList();
            query = query.Where(x => ids.Contains(x.Id));
        }
        else if (orgNodeId.HasValue)
        {
            var subtree = (await orgScopeService.GetSubtreeAsync(TenantId, orgNodeId.Value, cancellationToken)).ToList();
            query = query.Where(x => x.OrgNodeId.HasValue && subtree.Contains(x.OrgNodeId.Value));
        }
        else
        {
            throw new BusinessRuleException("Choose assets, or a factory or section.");
        }

        var scope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        return (await query.ToListAsync(cancellationToken)).Where(x => scope.Allows(x.OrgNodeId)).ToList();
    }

    /// <summary>"Factory › Section" (groups left out) for every node, for labels and scan results.</summary>
    private async Task<Dictionary<Guid, string>> LocationPathsAsync(CancellationToken cancellationToken)
    {
        var nodes = await dbContext.OrgNodes.Where(x => x.TenantId == TenantId).ToDictionaryAsync(x => x.Id, cancellationToken);
        return nodes.Values.ToDictionary(x => x.Id, x =>
        {
            var names = new List<string>();
            for (OrgNode? current = x; current is not null; current = current.ParentId.HasValue ? nodes.GetValueOrDefault(current.ParentId.Value) : null)
            {
                if (current.Level != OrgNodeLevels.Group)
                {
                    names.Insert(0, current.Name);
                }
            }

            return string.Join(" › ", names);
        });
    }

    private string ScanUrl(string tagNumber) => $"{publicAppOptions.Value.PublicUrl.TrimEnd('/')}/scan/{Uri.EscapeDataString(tagNumber)}";

    private async Task<Asset> LoadAssetAsync(Guid assetId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var asset = await dbContext.Assets.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == assetId, cancellationToken)
            ?? throw new NotFoundException("Asset was not found.");
        var scope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        return scope.Allows(asset.OrgNodeId) ? asset : throw new NotFoundException("Asset was not found.");
    }

    private async Task<AssetTag> LoadTagAsync(Guid id, CancellationToken cancellationToken)
    {
        var tag = await dbContext.AssetTags.Include(x => x.Asset).SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Tag was not found.");
        var scope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        return scope.Allows(tag.Asset?.OrgNodeId) ? tag : throw new NotFoundException("Tag was not found.");
    }

    private AssetTagResponse Map(AssetTag tag) => new(
        tag.Id,
        tag.AssetId,
        tag.TagNumber,
        tag.Symbology,
        tag.Status,
        ScanUrl(tag.TagNumber),
        tag.IssuedAt,
        tag.RetiredAt,
        tag.RetiredReason,
        tag.ReplacedByTagId,
        tag.PrintCount,
        tag.LastPrintedAt);
}

public sealed record AssetTagResponse(
    Guid Id,
    Guid AssetId,
    string TagNumber,
    string Symbology,
    string Status,
    string ScanUrl,
    DateTime IssuedAt,
    DateTime? RetiredAt,
    string? RetiredReason,
    Guid? ReplacedByTagId,
    int PrintCount,
    DateTime? LastPrintedAt);

public sealed record IssueAssetTagRequest([StringLength(40)] string? TagNumber = null, [StringLength(20)] string? Symbology = null);

public sealed record ReplaceAssetTagRequest([StringLength(500)] string Reason, [StringLength(40)] string? NewTagNumber = null, [StringLength(20)] string? Symbology = null);

public sealed record RetireAssetTagRequest([StringLength(500)] string? Reason);

public sealed record BulkIssueTagsRequest(IReadOnlyCollection<Guid>? AssetIds, Guid? OrgNodeId, [StringLength(20)] string? Symbology = null);

public sealed record BulkIssueTagsResponse(int Issued, int Skipped, IReadOnlyCollection<Guid> TagIds);

public sealed record PrintLabelsRequest(IReadOnlyCollection<Guid>? TagIds, IReadOnlyCollection<Guid>? AssetIds, Guid? OrgNodeId, [StringLength(20)] string? Layout = null);

public sealed record ScanAssetLink(Guid Id, string AssetCode, string AssetName);

public sealed record ScanMeter(Guid Id, string Name, string MeterType, string Unit, decimal? CurrentValue, DateTime? LastReadingAt);

public sealed record ScanWorkOrder(Guid Id, string WorkOrderNumber, string Title, string Status, string Priority, DateTime? DueDate);

public sealed record ScanHistoryItem(string EventType, DateTime OccurredAt, string? FromValue, string? ToValue, string? Notes);

public sealed record ScanResultResponse(
    string Code,
    string MatchedBy,
    string? Warning,
    string? ActiveTagNumber,
    Guid AssetId,
    string AssetCode,
    string AssetName,
    string Status,
    string OperationalStatus,
    string? Criticality,
    string? Manufacturer,
    string? Model,
    string? SerialNumber,
    Guid? OrgNodeId,
    string? Location,
    ScanAssetLink? Parent,
    IReadOnlyCollection<ScanAssetLink> Components,
    IReadOnlyCollection<ScanMeter> Meters,
    IReadOnlyCollection<ScanWorkOrder> OpenWorkOrders,
    IReadOnlyCollection<ScanHistoryItem> RecentHistory);

public sealed record OfflineScanMeter(Guid Id, string Name, string Unit, bool IsCumulative, decimal? CurrentValue, DateTime? LastReadingAt);

public sealed record OfflineScanEntry(
    Guid AssetId,
    string AssetCode,
    string AssetName,
    string? Location,
    string? ParentAssetCode,
    string? Criticality,
    string OperationalStatus,
    string? Manufacturer,
    string? Model,
    string? SerialNumber,
    string? TagNumber,
    IReadOnlyCollection<string> OldTagNumbers,
    IReadOnlyCollection<OfflineScanMeter> Meters);
