using System.ComponentModel.DataAnnotations;
using Ecosys.Api.Common;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>Versioned documents on assets: manuals, drawings, datasheets, certificates and procedures.</summary>
[ApiController]
[Authorize]
public sealed class AssetDocumentsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IOrgScopeService orgScopeService,
    IFileStorageService fileStorageService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    /// <summary>Equipment manuals and drawings are often large scanned PDFs.</summary>
    private const long MaxFileBytes = 25 * 1024 * 1024;
    private const string AttachmentEntityType = nameof(AssetDocument);

    [HttpGet("api/assets/{assetId:guid}/documents")]
    public async Task<ActionResult<IReadOnlyCollection<AssetDocumentResponse>>> List(Guid assetId, [FromQuery] bool includeArchived, CancellationToken cancellationToken)
    {
        await LoadAssetAsync(assetId, cancellationToken);
        var documents = await dbContext.AssetDocuments
            .Include(x => x.Versions).ThenInclude(x => x.Attachment)
            .Where(x => x.TenantId == TenantId && x.AssetId == assetId && (includeArchived || !x.IsArchived))
            .OrderBy(x => x.DocumentType).ThenBy(x => x.Title)
            .ToListAsync(cancellationToken);
        return Ok(documents.Select(Map).ToList());
    }

    [HttpPost("api/assets/{assetId:guid}/documents")]
    [RequestSizeLimit(MaxFileBytes + 1_000_000)]
    public async Task<ActionResult<AssetDocumentResponse>> Create(Guid assetId, [FromForm] CreateAssetDocumentForm form, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var asset = await LoadAssetAsync(assetId, cancellationToken);
        if (string.IsNullOrWhiteSpace(form.Title))
        {
            throw new BusinessRuleException("Give the document a title.");
        }

        var type = AssetDocumentTypes.Find(form.DocumentType)
            ?? throw new BusinessRuleException($"Document type must be one of: {string.Join(", ", AssetDocumentTypes.All)}.");
        await EnsureFileAsync(form.File, cancellationToken);

        var document = new AssetDocument
        {
            TenantId = TenantId,
            AssetId = asset.Id,
            Title = form.Title.Trim(),
            DocumentType = type,
            Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim(),
            ExpiryDate = form.ExpiryDate?.Date
        };
        dbContext.AssetDocuments.Add(document);
        await AddVersionAsync(document, form.File!, form.Notes, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "asset-document.created", nameof(AssetDocument), document.Id.ToString(),
            $"Added {type.ToLowerInvariant()} '{document.Title}' to asset '{asset.AssetName}'.", cancellationToken);

        return Ok(Map(document));
    }

    /// <summary>Uploads a new version; the previous ones stay available.</summary>
    [HttpPost("api/asset-documents/{id:guid}/versions")]
    [RequestSizeLimit(MaxFileBytes + 1_000_000)]
    public async Task<ActionResult<AssetDocumentResponse>> AddVersion(Guid id, [FromForm] AddDocumentVersionForm form, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var document = await LoadDocumentAsync(id, cancellationToken);
        if (document.IsArchived)
        {
            throw new BusinessRuleException("The document is archived. Restore it before adding a version.");
        }

        await EnsureFileAsync(form.File, cancellationToken);
        var version = await AddVersionAsync(document, form.File!, form.Notes, cancellationToken);
        if (form.ExpiryDate.HasValue)
        {
            document.ExpiryDate = form.ExpiryDate.Value.Date;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "asset-document.versioned", nameof(AssetDocument), document.Id.ToString(),
            $"Uploaded version {version.VersionNumber} of '{document.Title}'.", cancellationToken);

        return Ok(Map(document));
    }

    [HttpPut("api/asset-documents/{id:guid}")]
    public async Task<ActionResult<AssetDocumentResponse>> Update(Guid id, [FromBody] UpdateAssetDocumentRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var document = await LoadDocumentAsync(id, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new BusinessRuleException("Give the document a title.");
        }

        document.Title = request.Title.Trim();
        document.DocumentType = AssetDocumentTypes.Find(request.DocumentType)
            ?? throw new BusinessRuleException($"Document type must be one of: {string.Join(", ", AssetDocumentTypes.All)}.");
        document.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        document.ExpiryDate = request.ExpiryDate?.Date;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Map(document));
    }

    [HttpPost("api/asset-documents/{id:guid}/archive")]
    public Task<ActionResult<AssetDocumentResponse>> Archive(Guid id, CancellationToken cancellationToken) => SetArchivedAsync(id, true, cancellationToken);

    [HttpPost("api/asset-documents/{id:guid}/restore")]
    public Task<ActionResult<AssetDocumentResponse>> Restore(Guid id, CancellationToken cancellationToken) => SetArchivedAsync(id, false, cancellationToken);

    /// <summary>Downloads a version (the current one when no number is given).</summary>
    [HttpGet("api/asset-documents/{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, [FromQuery] int? version, CancellationToken cancellationToken)
    {
        var document = await LoadDocumentAsync(id, cancellationToken);
        var number = version ?? document.CurrentVersion;
        var entry = document.Versions.SingleOrDefault(x => x.VersionNumber == number)
            ?? throw new NotFoundException($"Version {number} was not found.");
        var attachment = entry.Attachment!;
        var stream = await fileStorageService.DownloadAsync(attachment.StoragePath, cancellationToken);
        return File(stream, attachment.MimeType, attachment.FileName, enableRangeProcessing: true);
    }

    private async Task<ActionResult<AssetDocumentResponse>> SetArchivedAsync(Guid id, bool archived, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var document = await LoadDocumentAsync(id, cancellationToken);
        document.IsArchived = archived;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Map(document));
    }

    private async Task<AssetDocumentVersion> AddVersionAsync(AssetDocument document, IFormFile file, string? notes, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var stored = await fileStorageService.UploadAsync(stream, file.FileName, file.ContentType, TenantId, cancellationToken);
        var attachment = new Attachment
        {
            TenantId = TenantId,
            EntityType = AttachmentEntityType,
            EntityId = document.Id,
            FileName = file.FileName,
            FileSize = stored.FileSize,
            MimeType = file.ContentType,
            StoragePath = stored.StoragePath,
            UploadedByUserId = UserId
        };
        attachment.PublicUrl = $"/api/asset-documents/{document.Id}/download?version={document.CurrentVersion + 1}";
        dbContext.Attachments.Add(attachment);

        var version = new AssetDocumentVersion
        {
            TenantId = TenantId,
            DocumentId = document.Id,
            VersionNumber = document.CurrentVersion + 1,
            AttachmentId = attachment.Id,
            Attachment = attachment,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            UploadedByUserId = UserId,
            UploadedByName = UserDisplayName
        };
        // Added to its own set: ids are set on construction, so EF would take a new version reached
        // only through a tracked document's collection for an existing row and try to update it.
        // The document is tracked, so EF's fix-up puts the version in document.Versions.
        dbContext.AssetDocumentVersions.Add(version);
        document.CurrentVersion = version.VersionNumber;
        return version;
    }

    private static async Task EnsureFileAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (await UploadedFileRules.CheckAsync(file, MaxFileBytes, cancellationToken) is { } problem)
        {
            throw new BusinessRuleException(problem);
        }
    }

    private void EnsureCanManage() => userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageAssets);

    private async Task<Asset> LoadAssetAsync(Guid assetId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var asset = await dbContext.Assets.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == assetId, cancellationToken)
            ?? throw new NotFoundException("Asset was not found.");
        var scope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        return scope.Allows(asset.OrgNodeId) ? asset : throw new NotFoundException("Asset was not found.");
    }

    private async Task<AssetDocument> LoadDocumentAsync(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var document = await dbContext.AssetDocuments
            .Include(x => x.Asset)
            .Include(x => x.Versions).ThenInclude(x => x.Attachment)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Document was not found.");
        var scope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        return scope.Allows(document.Asset?.OrgNodeId) ? document : throw new NotFoundException("Document was not found.");
    }

    private static AssetDocumentResponse Map(AssetDocument document) => new(
        document.Id,
        document.AssetId,
        document.Title,
        document.DocumentType,
        document.Description,
        document.CurrentVersion,
        document.ExpiryDate,
        document.ExpiryDate.HasValue && document.ExpiryDate.Value.Date < DateTime.UtcNow.Date,
        document.IsArchived,
        document.Versions
            .OrderByDescending(x => x.VersionNumber)
            .Select(x => new AssetDocumentVersionResponse(
                x.VersionNumber,
                x.Attachment?.FileName ?? string.Empty,
                x.Attachment?.FileSize ?? 0,
                x.Attachment?.MimeType ?? string.Empty,
                $"/api/asset-documents/{document.Id}/download?version={x.VersionNumber}",
                x.Notes,
                x.UploadedByName,
                x.CreatedAt))
            .ToList());
}

public sealed class CreateAssetDocumentForm
{
    [StringLength(200)] public string? Title { get; set; }
    [StringLength(30)] public string? DocumentType { get; set; }
    [StringLength(1000)] public string? Description { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public IFormFile? File { get; set; }
}

public sealed class AddDocumentVersionForm
{
    [StringLength(1000)] public string? Notes { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public IFormFile? File { get; set; }
}

public sealed record UpdateAssetDocumentRequest(
    [StringLength(200)] string Title,
    [StringLength(30)] string DocumentType,
    [StringLength(1000)] string? Description,
    DateTime? ExpiryDate);

public sealed record AssetDocumentVersionResponse(
    int VersionNumber,
    string FileName,
    long FileSize,
    string MimeType,
    string DownloadUrl,
    string? Notes,
    string? UploadedByName,
    DateTime UploadedAt);

public sealed record AssetDocumentResponse(
    Guid Id,
    Guid AssetId,
    string Title,
    string DocumentType,
    string? Description,
    int CurrentVersion,
    DateTime? ExpiryDate,
    bool IsExpired,
    bool IsArchived,
    IReadOnlyCollection<AssetDocumentVersionResponse> Versions);
