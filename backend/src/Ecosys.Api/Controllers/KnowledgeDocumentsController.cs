using System.Text.RegularExpressions;
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
[Route("api/knowledge")]
public sealed class KnowledgeDocumentsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IFileStorageService fileStorageService,
    IKnowledgeDocumentProcessingService processingService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".txt", ".md", ".png", ".jpg", ".jpeg", ".webp", ".xlsx"
    };

    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    [HttpPost("documents/upload")]
    [RequestSizeLimit(12_000_000)]
    public async Task<ActionResult<KnowledgeDocumentResponse>> Upload(
        [FromForm] string title,
        [FromForm] string? description,
        [FromForm] Guid? categoryId,
        [FromForm] string? tags,
        [FromForm] Guid? linkedClientId,
        [FromForm] Guid? linkedSiteId,
        [FromForm] Guid? linkedSystemId,
        [FromForm] Guid? linkedAssetId,
        [FromForm] Guid? linkedWorkOrderId,
        [FromForm] string? status,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        EnsureKnowledgeManageAccess();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new BusinessRuleException("Document title is required.");
        }

        if (file is null || file.Length == 0)
        {
            throw new BusinessRuleException("A document file is required.");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            throw new BusinessRuleException("Document exceeds the 10 MB size limit.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new BusinessRuleException("Document file type is not supported.");
        }

        await EnsureLinkedEntitiesAsync(linkedClientId, linkedSiteId, linkedSystemId, linkedAssetId, linkedWorkOrderId, cancellationToken);
        await using var uploadStream = file.OpenReadStream();
        var uploadResult = await fileStorageService.UploadAsync(uploadStream, file.FileName, file.ContentType ?? "application/octet-stream", TenantId, cancellationToken);

        await using var processingStream = await fileStorageService.DownloadAsync(uploadResult.StoragePath, cancellationToken);
        var processed = await processingService.ProcessAsync(processingStream, file.FileName, file.ContentType ?? "application/octet-stream", cancellationToken);

        var document = new KnowledgeDocument
        {
            TenantId = TenantId,
            Title = title.Trim(),
            Description = Normalize(description),
            CategoryId = categoryId,
            TagsJson = SerializeTags(tags),
            LinkedClientId = linkedClientId,
            LinkedSiteId = linkedSiteId,
            LinkedSystemId = linkedSystemId,
            LinkedAssetId = linkedAssetId,
            LinkedWorkOrderId = linkedWorkOrderId,
            UploadedByUserId = UserId,
            UploadedAt = DateTime.UtcNow,
            Version = 1,
            Status = NormalizeDocumentStatus(status),
            FileName = file.FileName,
            FileType = extension.TrimStart('.').ToLowerInvariant(),
            FileSize = uploadResult.FileSize,
            StoragePath = uploadResult.StoragePath,
            ExtractedText = string.IsNullOrWhiteSpace(processed.ExtractedText) ? null : processed.ExtractedText
        };

        dbContext.KnowledgeDocuments.Add(document);
        await dbContext.SaveChangesAsync(cancellationToken);
        await ReplaceChunksAsync(document, processed.Chunks, cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "knowledge.document.uploaded", nameof(KnowledgeDocument), document.Id.ToString(), $"Uploaded knowledge document '{document.Title}'.", cancellationToken);

        return Ok(await MapDocumentAsync(document.Id, cancellationToken));
    }

    [HttpGet("documents")]
    public async Task<ActionResult<IReadOnlyCollection<KnowledgeDocumentResponse>>> ListDocuments(CancellationToken cancellationToken)
    {
        EnsureKnowledgeViewAccess();
        var documents = await dbContext.KnowledgeDocuments
            .Where(x => x.TenantId == TenantId)
            .OrderByDescending(x => x.UploadedAt)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var results = new List<KnowledgeDocumentResponse>(documents.Count);
        foreach (var id in documents)
        {
            results.Add(await MapDocumentAsync(id, cancellationToken));
        }

        return Ok(results);
    }

    [HttpGet("documents/{id:guid}")]
    public async Task<ActionResult<KnowledgeDocumentResponse>> GetDocument(Guid id, CancellationToken cancellationToken)
    {
        EnsureKnowledgeViewAccess();
        return Ok(await MapDocumentAsync(id, cancellationToken));
    }

    [HttpGet("documents/{id:guid}/download")]
    public async Task<IActionResult> DownloadDocument(Guid id, CancellationToken cancellationToken)
    {
        EnsureKnowledgeViewAccess();
        var document = await LoadDocumentAsync(id, cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "knowledge.document.downloaded", nameof(KnowledgeDocument), document.Id.ToString(), $"Downloaded knowledge document '{document.Title}'.", cancellationToken);
        var stream = await fileStorageService.DownloadAsync(document.StoragePath, cancellationToken);
        return File(stream, GetMimeType(document.FileType), document.FileName, enableRangeProcessing: true);
    }

    [HttpDelete("documents/{id:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid id, CancellationToken cancellationToken)
    {
        EnsureKnowledgeManageAccess();
        var document = await LoadDocumentAsync(id, cancellationToken);
        await fileStorageService.DeleteAsync(document.StoragePath, cancellationToken);
        dbContext.KnowledgeDocuments.Remove(document);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "knowledge.document.deleted", nameof(KnowledgeDocument), document.Id.ToString(), $"Deleted knowledge document '{document.Title}'.", cancellationToken);
        return NoContent();
    }

    [HttpPost("documents/{id:guid}/reindex")]
    public async Task<ActionResult<KnowledgeDocumentResponse>> ReindexDocument(Guid id, CancellationToken cancellationToken)
    {
        EnsureKnowledgeManageAccess();
        var document = await LoadDocumentAsync(id, cancellationToken);
        await using var stream = await fileStorageService.DownloadAsync(document.StoragePath, cancellationToken);
        var processed = await processingService.ProcessAsync(stream, document.FileName, document.FileType, cancellationToken);
        document.ExtractedText = string.IsNullOrWhiteSpace(processed.ExtractedText) ? null : processed.ExtractedText;
        await ReplaceChunksAsync(document, processed.Chunks, cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "knowledge.document.reindexed", nameof(KnowledgeDocument), document.Id.ToString(), $"Reindexed knowledge document '{document.Title}'.", cancellationToken);
        return Ok(await MapDocumentAsync(document.Id, cancellationToken));
    }

    [HttpPost("ask")]
    public async Task<ActionResult<KnowledgeAskResponse>> Ask([FromBody] KnowledgeAskRequest request, CancellationToken cancellationToken)
    {
        EnsureKnowledgeViewAccess();
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            throw new BusinessRuleException("Question is required.");
        }

        var tokens = Tokenize(request.Question);
        var documentChunks = await BuildDocumentMatchesAsync(tokens, request, cancellationToken);
        var articleMatches = await BuildArticleMatchesAsync(tokens, cancellationToken);
        var contextualMatches = await BuildContextMatchesAsync(tokens, request, cancellationToken);

        var sources = documentChunks
            .Concat(articleMatches)
            .Concat(contextualMatches)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Title)
            .Take(6)
            .Select(x => x.Source)
            .ToList();

        await auditLogService.LogAsync(TenantId, UserId, "knowledge.ask.executed", nameof(KnowledgeDocument), request.ContextId?.ToString() ?? "knowledge", $"Asked Knowledge Centre: {request.Question.Trim()}", cancellationToken);

        if (sources.Count == 0)
        {
            return Ok(new KnowledgeAskResponse(
                "I could not find this in the available Knowledge Centre documents.",
                []));
        }

        var answer = string.Join(" ", sources.Take(3).Select(source => source.Excerpt).Where(x => !string.IsNullOrWhiteSpace(x)));
        return Ok(new KnowledgeAskResponse(answer, sources));
    }

    private async Task<List<ScoredKnowledgeSource>> BuildDocumentMatchesAsync(IReadOnlyCollection<string> tokens, KnowledgeAskRequest request, CancellationToken cancellationToken)
    {
        var query = dbContext.KnowledgeDocumentChunks
            .Include(x => x.Document)
            .Where(x => x.TenantId == TenantId);

        query = ApplyContextFilter(query, request);
        var chunks = await query.ToListAsync(cancellationToken);
        return chunks
            .Select(chunk => new ScoredKnowledgeSource(
                ScoreText(chunk.ChunkText, tokens),
                new KnowledgeAskSourceResponse(
                    "document_chunk",
                    chunk.Id,
                    chunk.Document?.Title ?? chunk.Document?.FileName ?? "Document",
                    BuildExcerpt(chunk.ChunkText, tokens),
                    chunk.PageNumber,
                    $"/knowledge?documentId={chunk.DocumentId}",
                    ScoreText(chunk.ChunkText, tokens) > 0 ? "matched" : "low")))
            .Where(x => x.Score > 0)
            .ToList();
    }

    private async Task<List<ScoredKnowledgeSource>> BuildArticleMatchesAsync(IReadOnlyCollection<string> tokens, CancellationToken cancellationToken)
    {
        var articles = await dbContext.KnowledgeArticles
            .Where(x => x.TenantId == TenantId && x.Status == "Published")
            .ToListAsync(cancellationToken);
        return articles
            .Select(article =>
            {
                var body = $"{article.Title} {article.Summary} {article.Body}";
                var score = ScoreText(body, tokens);
                return new ScoredKnowledgeSource(
                    score,
                    new KnowledgeAskSourceResponse("article", article.Id, article.Title, BuildExcerpt(article.Body, tokens), null, $"/knowledge/{article.Id}", score > 0 ? "matched" : "low"));
            })
            .Where(x => x.Score > 0)
            .ToList();
    }

    private async Task<List<ScoredKnowledgeSource>> BuildContextMatchesAsync(IReadOnlyCollection<string> tokens, KnowledgeAskRequest request, CancellationToken cancellationToken)
    {
        if (!request.ContextId.HasValue)
        {
            return [];
        }

        return request.ContextType?.Trim().ToLowerInvariant() switch
        {
            "work_order" => await dbContext.WorkOrders
                .Where(x => x.TenantId == TenantId && x.Id == request.ContextId.Value)
                .Select(x => new { x.Id, x.Title, Text = $"{x.Title} {x.Description} {x.JobCardNotes} {x.WorkDoneNotes}" })
                .ToListAsync(cancellationToken)
                .ContinueWith(task => task.Result
                    .Select(x => CreateContextSource("work_order", x.Id, x.Title, x.Text, tokens, $"/work-orders/{x.Id}"))
                    .Where(x => x.Score > 0)
                    .ToList(), cancellationToken),
            "asset" => await dbContext.Assets
                .Where(x => x.TenantId == TenantId && x.Id == request.ContextId.Value)
                .Select(x => new { x.Id, x.AssetName, Text = $"{x.AssetName} {x.AssetCode} {x.AssetType} {x.Manufacturer} {x.Model} {x.Notes}" })
                .ToListAsync(cancellationToken)
                .ContinueWith(task => task.Result
                    .Select(x => CreateContextSource("asset", x.Id, x.AssetName, x.Text, tokens, $"/assets?assetId={x.Id}"))
                    .Where(x => x.Score > 0)
                    .ToList(), cancellationToken),
            "client" => await dbContext.Clients
                .Where(x => x.TenantId == TenantId && x.Id == request.ContextId.Value)
                .Select(x => new { x.Id, x.ClientName, Text = $"{x.ClientName} {x.Location} {x.ContactPerson} {x.Notes}" })
                .ToListAsync(cancellationToken)
                .ContinueWith(task => task.Result
                    .Select(x => CreateContextSource("client", x.Id, x.ClientName, x.Text, tokens, $"/clients/{x.Id}"))
                    .Where(x => x.Score > 0)
                    .ToList(), cancellationToken),
            "site" => await dbContext.Sites
                .Where(x => x.TenantId == TenantId && x.Id == request.ContextId.Value)
                .Select(x => new { x.Id, x.SiteName, Text = $"{x.SiteName} {x.AccessNotes} {x.SpecialInstructions} {x.OperatingHours}" })
                .ToListAsync(cancellationToken)
                .ContinueWith(task => task.Result
                    .Select(x => CreateContextSource("site", x.Id, x.SiteName, x.Text, tokens, $"/sites?siteId={x.Id}"))
                    .Where(x => x.Score > 0)
                    .ToList(), cancellationToken),
            "system" => await dbContext.Systems
                .Where(x => x.TenantId == TenantId && x.Id == request.ContextId.Value)
                .Select(x => new { x.Id, x.SystemName, Text = $"{x.SystemName} {x.SystemType} {x.Description} {x.Notes}" })
                .ToListAsync(cancellationToken)
                .ContinueWith(task => task.Result
                    .Select(x => CreateContextSource("system", x.Id, x.SystemName, x.Text, tokens, $"/systems/{x.Id}"))
                    .Where(x => x.Score > 0)
                    .ToList(), cancellationToken),
            _ => []
        };
    }

    private IQueryable<KnowledgeDocumentChunk> ApplyContextFilter(IQueryable<KnowledgeDocumentChunk> query, KnowledgeAskRequest request)
    {
        if (!request.ContextId.HasValue)
        {
            return query;
        }

        return request.ContextType?.Trim().ToLowerInvariant() switch
        {
            "client" => query.Where(x => x.Document != null && x.Document.LinkedClientId == request.ContextId.Value),
            "site" => query.Where(x => x.Document != null && x.Document.LinkedSiteId == request.ContextId.Value),
            "system" => query.Where(x => x.Document != null && x.Document.LinkedSystemId == request.ContextId.Value),
            "asset" => query.Where(x => x.Document != null && x.Document.LinkedAssetId == request.ContextId.Value),
            "work_order" => query.Where(x => x.Document != null && x.Document.LinkedWorkOrderId == request.ContextId.Value),
            _ => query
        };
    }

    private async Task EnsureLinkedEntitiesAsync(Guid? clientId, Guid? siteId, Guid? systemId, Guid? assetId, Guid? workOrderId, CancellationToken cancellationToken)
    {
        if (clientId.HasValue && !await dbContext.Clients.AnyAsync(x => x.TenantId == TenantId && x.Id == clientId.Value, cancellationToken))
        {
            throw new BusinessRuleException("Linked client was not found.");
        }

        if (siteId.HasValue && !await dbContext.Sites.AnyAsync(x => x.TenantId == TenantId && x.Id == siteId.Value, cancellationToken))
        {
            throw new BusinessRuleException("Linked site was not found.");
        }

        if (systemId.HasValue && !await dbContext.Systems.AnyAsync(x => x.TenantId == TenantId && x.Id == systemId.Value, cancellationToken))
        {
            throw new BusinessRuleException("Linked system was not found.");
        }

        if (assetId.HasValue && !await dbContext.Assets.AnyAsync(x => x.TenantId == TenantId && x.Id == assetId.Value, cancellationToken))
        {
            throw new BusinessRuleException("Linked asset was not found.");
        }

        if (workOrderId.HasValue && !await dbContext.WorkOrders.AnyAsync(x => x.TenantId == TenantId && x.Id == workOrderId.Value, cancellationToken))
        {
            throw new BusinessRuleException("Linked work order was not found.");
        }
    }

    private async Task ReplaceChunksAsync(KnowledgeDocument document, IReadOnlyCollection<KnowledgeDocumentChunkDraft> chunks, CancellationToken cancellationToken)
    {
        var existing = await dbContext.KnowledgeDocumentChunks.Where(x => x.TenantId == TenantId && x.DocumentId == document.Id).ToListAsync(cancellationToken);
        if (existing.Count > 0)
        {
            dbContext.KnowledgeDocumentChunks.RemoveRange(existing);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        foreach (var chunk in chunks)
        {
            dbContext.KnowledgeDocumentChunks.Add(new KnowledgeDocumentChunk
            {
                TenantId = TenantId,
                DocumentId = document.Id,
                PageNumber = chunk.PageNumber,
                SectionTitle = chunk.SectionTitle,
                ChunkText = chunk.ChunkText,
                ChunkOrder = chunk.ChunkOrder
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<KnowledgeDocument> LoadDocumentAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.KnowledgeDocuments.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
        ?? throw new NotFoundException("Knowledge document not found.");

    private async Task<KnowledgeDocumentResponse> MapDocumentAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await dbContext.KnowledgeDocuments
            .Include(x => x.Category)
            .Include(x => x.LinkedClient)
            .Include(x => x.LinkedSite)
            .Include(x => x.LinkedSystem)
            .Include(x => x.LinkedAsset)
            .Include(x => x.LinkedWorkOrder)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Knowledge document not found.");
        var chunkCount = await dbContext.KnowledgeDocumentChunks.CountAsync(x => x.TenantId == TenantId && x.DocumentId == document.Id, cancellationToken);

        return new KnowledgeDocumentResponse(
            document.Id,
            document.TenantId,
            document.Title,
            document.Description,
            document.CategoryId,
            document.Category?.Name,
            DeserializeTags(document.TagsJson),
            document.LinkedClientId,
            document.LinkedClient?.ClientName,
            document.LinkedSiteId,
            document.LinkedSite?.SiteName,
            document.LinkedSystemId,
            document.LinkedSystem?.SystemName,
            document.LinkedAssetId,
            document.LinkedAsset?.AssetName,
            document.LinkedWorkOrderId,
            document.LinkedWorkOrder?.WorkOrderNumber,
            document.UploadedByUserId,
            document.UploadedAt,
            document.Version,
            document.Status,
            document.FileName,
            document.FileType,
            document.FileSize,
            $"/api/knowledge/documents/{document.Id}/download",
            chunkCount);
    }

    private static IReadOnlyCollection<string> Tokenize(string question) =>
        Regex.Split(question.ToLowerInvariant(), @"[^a-z0-9]+")
            .Where(x => x.Length > 2)
            .Distinct()
            .ToArray();

    private static int ScoreText(string? text, IReadOnlyCollection<string> tokens)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var value = text.ToLowerInvariant();
        return tokens.Sum(token => value.Contains(token) ? 1 : 0);
    }

    private static string BuildExcerpt(string? text, IReadOnlyCollection<string> tokens)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = Regex.Replace(text, "\\s+", " ").Trim();
        var firstToken = tokens.FirstOrDefault(token => normalized.Contains(token, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(firstToken))
        {
            return normalized.Length <= 220 ? normalized : $"{normalized[..220]}...";
        }

        var index = normalized.IndexOf(firstToken, StringComparison.OrdinalIgnoreCase);
        var start = Math.Max(0, index - 60);
        var length = Math.Min(220, normalized.Length - start);
        return normalized.Substring(start, length).Trim();
    }

    private static ScoredKnowledgeSource CreateContextSource(string sourceType, Guid id, string title, string? text, IReadOnlyCollection<string> tokens, string path)
    {
        var score = ScoreText(text, tokens);
        return new ScoredKnowledgeSource(score, new KnowledgeAskSourceResponse(sourceType, id, title, BuildExcerpt(text, tokens), null, path, score > 0 ? "matched" : "low"));
    }

    private static string SerializeTags(string? rawTags)
    {
        var tags = DeserializeTags(rawTags);
        return tags.Count == 0 ? string.Empty : string.Join('|', tags);
    }

    private static IReadOnlyCollection<string> DeserializeTags(string? rawTags) =>
        string.IsNullOrWhiteSpace(rawTags)
            ? []
            : rawTags.Split(['|', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private static string NormalizeDocumentStatus(string? status)
    {
        var normalized = string.IsNullOrWhiteSpace(status) ? "draft" : status.Trim().ToLowerInvariant();
        return normalized switch
        {
            "draft" or "published" or "archived" => normalized,
            _ => throw new BusinessRuleException("Document status must be draft, published, or archived.")
        };
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string GetMimeType(string fileType) => fileType.ToLowerInvariant() switch
    {
        "pdf" => "application/pdf",
        "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "txt" => "text/plain",
        "md" => "text/markdown",
        "png" => "image/png",
        "jpg" or "jpeg" => "image/jpeg",
        "webp" => "image/webp",
        "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        _ => "application/octet-stream"
    };

    private sealed record ScoredKnowledgeSource(int Score, KnowledgeAskSourceResponse Source)
    {
        public string Title => Source.Title;
    }

    private void EnsureKnowledgeViewAccess()
    {
        userAccessService.EnsureAdminOrAnyPermission(
            PermissionNames.CanViewKnowledge,
            PermissionNames.CanManageKnowledge);
    }

    private void EnsureKnowledgeManageAccess()
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageKnowledge);
    }
}

public sealed record KnowledgeDocumentResponse(
    Guid Id,
    Guid TenantId,
    string Title,
    string? Description,
    Guid? CategoryId,
    string? CategoryName,
    IReadOnlyCollection<string> Tags,
    Guid? LinkedClientId,
    string? LinkedClientName,
    Guid? LinkedSiteId,
    string? LinkedSiteName,
    Guid? LinkedSystemId,
    string? LinkedSystemName,
    Guid? LinkedAssetId,
    string? LinkedAssetName,
    Guid? LinkedWorkOrderId,
    string? LinkedWorkOrderNumber,
    Guid UploadedByUserId,
    DateTime UploadedAt,
    int Version,
    string Status,
    string FileName,
    string FileType,
    long FileSize,
    string DownloadPath,
    int ChunkCount);

public sealed record KnowledgeAskRequest(string Question, string? ContextType, Guid? ContextId, bool IncludeDocuments = true);
public sealed record KnowledgeAskResponse(string Answer, IReadOnlyCollection<KnowledgeAskSourceResponse> Sources);
public sealed record KnowledgeAskSourceResponse(string SourceType, Guid SourceId, string Title, string Excerpt, int? PageNumber, string Path, string Status);
