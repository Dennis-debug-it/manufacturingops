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
[Route("api/attachments")]
public sealed class AttachmentsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IFileStorageService fileStorageService) : TenantAwareControllerBase(tenantContext)
{
    private static readonly HashSet<string> AllowedEntityTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "WorkOrder",
        "Asset",
        "Site",
        "Client"
    };

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp",
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "text/csv"
    };

    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    private static readonly Dictionary<string, string[]> AllowedExtensionsByMimeType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = [".jpg", ".jpeg"],
        ["image/png"] = [".png"],
        ["image/webp"] = [".webp"],
        ["application/pdf"] = [".pdf"],
        ["application/msword"] = [".doc"],
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = [".docx"],
        ["application/vnd.ms-excel"] = [".xls"],
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = [".xlsx"],
        ["text/csv"] = [".csv"]
    };

    [HttpPost("upload")]
    [RequestSizeLimit(6_000_000)]
    public async Task<ActionResult<AttachmentDto>> Upload(
        [FromForm] string entityType,
        [FromForm] string entityId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entityType))
            return BadRequest(new { message = "entityType is required.", errorCode = "VALIDATION_ERROR" });

        if (!AllowedEntityTypes.Contains(entityType))
            return BadRequest(new { message = $"Entity type '{entityType}' is not supported.", errorCode = "VALIDATION_ERROR" });

        if (!Guid.TryParse(entityId, out var entityGuid))
            return BadRequest(new { message = "entityId must be a valid GUID.", errorCode = "VALIDATION_ERROR" });

        if (file is null || file.Length == 0)
            return BadRequest(new { message = "No file was provided.", errorCode = "VALIDATION_ERROR" });

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(new { message = "File exceeds the 5 MB size limit.", errorCode = "VALIDATION_ERROR" });

        var mimeType = file.ContentType ?? "application/octet-stream";
        if (!AllowedMimeTypes.Contains(mimeType))
            return BadRequest(new { message = $"File type '{mimeType}' is not permitted.", errorCode = "VALIDATION_ERROR" });

        var fileExtension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(fileExtension)
            || !AllowedExtensionsByMimeType.TryGetValue(mimeType, out var allowedExtensions)
            || !allowedExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = $"File extension '{fileExtension}' does not match an allowed file type.", errorCode = "VALIDATION_ERROR" });
        }

        var tenantId = TenantId;
        var userId = UserId;
        await EnsureEntityExistsAsync(entityType, entityGuid, tenantId, cancellationToken);

        await using var stream = file.OpenReadStream();
        var result = await fileStorageService.UploadAsync(stream, file.FileName, mimeType, tenantId, cancellationToken);

        var attachment = new Attachment
        {
            TenantId = tenantId,
            EntityType = entityType,
            EntityId = entityGuid,
            FileName = file.FileName,
            FileSize = result.FileSize,
            MimeType = mimeType,
            StoragePath = result.StoragePath,
            PublicUrl = string.Empty,
            UploadedByUserId = userId
        };

        attachment.PublicUrl = BuildDownloadUrl(attachment.Id);
        dbContext.Attachments.Add(attachment);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(MapDto(attachment));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AttachmentDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var attachment = await dbContext.Attachments
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Attachment not found.");

        return Ok(MapDto(attachment));
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var attachment = await dbContext.Attachments
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Attachment not found.");

        var stream = await fileStorageService.DownloadAsync(attachment.StoragePath, cancellationToken);
        return File(stream, attachment.MimeType, attachment.FileName, enableRangeProcessing: true);
    }

    [HttpGet("entity/{entityType}/{entityId:guid}")]
    public async Task<ActionResult<IReadOnlyCollection<AttachmentDto>>> GetByEntity(
        string entityType,
        Guid entityId,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var attachments = await dbContext.Attachments
            .Where(x => x.TenantId == tenantId && x.EntityType == entityType && x.EntityId == entityId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(attachments.Select(MapDto).ToList());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var userId = UserId;

        var attachment = await dbContext.Attachments
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Attachment not found.");

        if (attachment.UploadedByUserId != userId && !IsAdmin)
            throw new ForbiddenException("Only the uploader or an admin can delete this attachment.");

        await fileStorageService.DeleteAsync(attachment.StoragePath, cancellationToken);
        dbContext.Attachments.Remove(attachment);
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static AttachmentDto MapDto(Attachment a) => new(
        a.Id,
        a.EntityType,
        a.EntityId,
        a.FileName,
        a.FileSize,
        a.MimeType,
        BuildDownloadUrl(a.Id),
        a.UploadedByUserId,
        a.CreatedAt);

    private static string BuildDownloadUrl(Guid attachmentId) => $"/api/attachments/{attachmentId}/download";

    private async Task EnsureEntityExistsAsync(string entityType, Guid entityId, Guid tenantId, CancellationToken cancellationToken)
    {
        var exists = entityType switch
        {
            "WorkOrder" => await dbContext.WorkOrders.AnyAsync(x => x.Id == entityId && x.TenantId == tenantId, cancellationToken),
            "Asset" => await dbContext.Assets.AnyAsync(x => x.Id == entityId && x.TenantId == tenantId, cancellationToken),
            "Site" => await dbContext.Sites.AnyAsync(x => x.Id == entityId && x.TenantId == tenantId, cancellationToken),
            "Client" => await dbContext.Clients.AnyAsync(x => x.Id == entityId && x.TenantId == tenantId, cancellationToken),
            _ => false
        };

        if (!exists)
        {
            throw new NotFoundException($"{entityType} not found.");
        }
    }
}

public sealed record AttachmentDto(
    Guid Id,
    string EntityType,
    Guid EntityId,
    string FileName,
    long FileSize,
    string MimeType,
    string PublicUrl,
    Guid UploadedByUserId,
    DateTime CreatedAt);
