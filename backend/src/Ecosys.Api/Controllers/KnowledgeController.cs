using System.Text;
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
public sealed class KnowledgeController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet("articles")]
    public async Task<ActionResult<IReadOnlyCollection<KnowledgeArticleListItemResponse>>> ListArticles(
        [FromQuery] string? q,
        [FromQuery] Guid? categoryId,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        EnsureKnowledgeViewAccess();

        var articles = await QueryArticles()
            .Where(x => string.IsNullOrWhiteSpace(status) || x.Status == status)
            .Where(x => !categoryId.HasValue || x.CategoryId == categoryId.Value)
            .ToListAsync(cancellationToken);

        var filtered = articles
            .Where(CanViewArticle)
            .Where(x => MatchesSearch(x, q))
            .OrderByDescending(x => x.PublishedAt ?? x.UpdatedAt ?? x.CreatedAt)
            .Select(MapListItem)
            .ToList();

        return Ok(filtered);
    }

    [HttpPost("articles")]
    public async Task<ActionResult<KnowledgeArticleDetailResponse>> CreateArticle([FromBody] UpsertKnowledgeArticleRequest request, CancellationToken cancellationToken)
    {
        EnsureKnowledgeManageAccess();
        ValidateArticleRequest(request);

        var article = new KnowledgeArticle
        {
            TenantId = TenantId,
            Title = request.Title.Trim(),
            Slug = await BuildUniqueSlugAsync(request.Title, null, cancellationToken),
            Summary = request.Summary?.Trim(),
            Body = request.Body.Trim(),
            CategoryId = await ResolveCategoryIdAsync(request.CategoryId, cancellationToken),
            Status = NormalizeStatus(request.Status),
            Visibility = NormalizeVisibility(request.Visibility),
            CreatedByUserId = UserId,
            UpdatedByUserId = UserId,
            PublishedAt = string.Equals(request.Status, "Published", StringComparison.OrdinalIgnoreCase) ? DateTime.UtcNow : null
        };

        dbContext.KnowledgeArticles.Add(article);
        await dbContext.SaveChangesAsync(cancellationToken);

        await ReplaceTagsAsync(article, request.Tags, cancellationToken);
        await CreateVersionAsync(article, 1, UserId, cancellationToken);

        var persisted = await LoadArticleAsync(article.Id, cancellationToken);
        return CreatedAtAction(nameof(GetArticle), new { id = article.Id }, await MapDetailAsync(persisted, cancellationToken));
    }

    [HttpGet("articles/{id:guid}")]
    public async Task<ActionResult<KnowledgeArticleDetailResponse>> GetArticle(Guid id, CancellationToken cancellationToken)
    {
        EnsureKnowledgeViewAccess();
        var article = await LoadArticleAsync(id, cancellationToken);
        if (!CanViewArticle(article))
        {
            throw new NotFoundException("Knowledge article not found.");
        }

        return Ok(await MapDetailAsync(article, cancellationToken));
    }

    [HttpPut("articles/{id:guid}")]
    public async Task<ActionResult<KnowledgeArticleDetailResponse>> UpdateArticle(Guid id, [FromBody] UpsertKnowledgeArticleRequest request, CancellationToken cancellationToken)
    {
        EnsureKnowledgeManageAccess();
        ValidateArticleRequest(request);

        var article = await LoadArticleAsync(id, cancellationToken);
        article.Title = request.Title.Trim();
        article.Slug = await BuildUniqueSlugAsync(request.Title, article.Id, cancellationToken);
        article.Summary = request.Summary?.Trim();
        article.Body = request.Body.Trim();
        article.CategoryId = await ResolveCategoryIdAsync(request.CategoryId, cancellationToken);
        article.Status = NormalizeStatus(request.Status);
        article.Visibility = NormalizeVisibility(request.Visibility);
        article.UpdatedByUserId = UserId;
        article.PublishedAt = string.Equals(article.Status, "Published", StringComparison.OrdinalIgnoreCase)
            ? article.PublishedAt ?? DateTime.UtcNow
            : null;

        await dbContext.SaveChangesAsync(cancellationToken);
        await ReplaceTagsAsync(article, request.Tags, cancellationToken);
        await CreateVersionAsync(article, await GetNextVersionNumberAsync(article.Id, cancellationToken), UserId, cancellationToken);

        return Ok(await MapDetailAsync(await LoadArticleAsync(article.Id, cancellationToken), cancellationToken));
    }

    [HttpDelete("articles/{id:guid}")]
    public async Task<IActionResult> DeleteArticle(Guid id, CancellationToken cancellationToken)
    {
        EnsureKnowledgeManageAccess();
        var article = await LoadArticleAsync(id, cancellationToken);
        dbContext.KnowledgeArticles.Remove(article);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("articles/{id:guid}/publish")]
    public async Task<ActionResult<KnowledgeArticleDetailResponse>> PublishArticle(Guid id, CancellationToken cancellationToken)
    {
        EnsureKnowledgeManageAccess();
        var article = await LoadArticleAsync(id, cancellationToken);
        article.Status = "Published";
        article.PublishedAt = DateTime.UtcNow;
        article.UpdatedByUserId = UserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        await CreateVersionAsync(article, await GetNextVersionNumberAsync(article.Id, cancellationToken), UserId, cancellationToken);
        return Ok(await MapDetailAsync(await LoadArticleAsync(article.Id, cancellationToken), cancellationToken));
    }

    [HttpPost("articles/{id:guid}/archive")]
    public async Task<ActionResult<KnowledgeArticleDetailResponse>> ArchiveArticle(Guid id, CancellationToken cancellationToken)
    {
        EnsureKnowledgeManageAccess();
        var article = await LoadArticleAsync(id, cancellationToken);
        article.Status = "Archived";
        article.UpdatedByUserId = UserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await MapDetailAsync(await LoadArticleAsync(article.Id, cancellationToken), cancellationToken));
    }

    [HttpPost("articles/from-work-order/{workOrderId:guid}")]
    public async Task<ActionResult<KnowledgeArticleDetailResponse>> DraftFromWorkOrder(Guid workOrderId, CancellationToken cancellationToken)
    {
        EnsureKnowledgeManageAccess();

        var workOrder = await dbContext.WorkOrders
            .Include(x => x.Client)
            .Include(x => x.Site)
            .Include(x => x.Asset)
            .ThenInclude(x => x!.AssetCategory)
            .Include(x => x.MaterialUsages)
            .ThenInclude(x => x.MaterialItem)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == workOrderId, cancellationToken)
            ?? throw new NotFoundException("Work order not found.");

        var tagNames = new[]
        {
            workOrder.Asset?.AssetCategory?.Name,
            workOrder.Client?.ClientName,
            workOrder.Site?.SiteName,
            "Draft from work order"
        };

        var article = new KnowledgeArticle
        {
            TenantId = TenantId,
            Title = $"Draft from {workOrder.WorkOrderNumber} - {workOrder.Title}",
            Slug = await BuildUniqueSlugAsync($"draft-from-{workOrder.WorkOrderNumber}-{workOrder.Title}", null, cancellationToken),
            Summary = BuildDraftSummary(workOrder),
            Body = BuildDraftBody(workOrder),
            Status = "Draft",
            Visibility = "Internal",
            CreatedByUserId = UserId,
            UpdatedByUserId = UserId,
            SourceWorkOrderId = workOrder.Id
        };

        dbContext.KnowledgeArticles.Add(article);
        await dbContext.SaveChangesAsync(cancellationToken);
        await ReplaceTagsAsync(article, tagNames, cancellationToken);
        await CreateVersionAsync(article, 1, UserId, cancellationToken);

        return CreatedAtAction(nameof(GetArticle), new { id = article.Id }, await MapDetailAsync(await LoadArticleAsync(article.Id, cancellationToken), cancellationToken));
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyCollection<KnowledgeCategoryResponse>>> ListCategories(CancellationToken cancellationToken)
    {
        EnsureKnowledgeViewAccess();
        var categories = await dbContext.KnowledgeCategories
            .Where(x => x.TenantId == TenantId && x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return Ok(categories.Select(MapCategory).ToList());
    }

    [HttpPost("categories")]
    public async Task<ActionResult<KnowledgeCategoryResponse>> CreateCategory([FromBody] UpsertKnowledgeCategoryRequest request, CancellationToken cancellationToken)
    {
        EnsureKnowledgeManageAccess();
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessRuleException("Category name is required.");
        }

        var category = new KnowledgeCategory
        {
            TenantId = TenantId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Icon = request.Icon?.Trim(),
            DisplayOrder = request.DisplayOrder,
            IsPublic = request.IsPublic,
            IsActive = true
        };

        dbContext.KnowledgeCategories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapCategory(category));
    }

    [HttpPut("categories/{id:guid}")]
    public async Task<ActionResult<KnowledgeCategoryResponse>> UpdateCategory(Guid id, [FromBody] UpsertKnowledgeCategoryRequest request, CancellationToken cancellationToken)
    {
        EnsureKnowledgeManageAccess();
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessRuleException("Category name is required.");
        }

        var category = await dbContext.KnowledgeCategories
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Knowledge category not found.");

        category.Name = request.Name.Trim();
        category.Description = request.Description?.Trim();
        category.Icon = request.Icon?.Trim();
        category.DisplayOrder = request.DisplayOrder;
        category.IsPublic = request.IsPublic;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapCategory(category));
    }

    [HttpDelete("categories/{id:guid}")]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken cancellationToken)
    {
        EnsureKnowledgeManageAccess();
        var category = await dbContext.KnowledgeCategories
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Knowledge category not found.");

        var hasArticles = await dbContext.KnowledgeArticles.AnyAsync(x => x.TenantId == TenantId && x.CategoryId == id, cancellationToken);
        if (hasArticles)
        {
            throw new BusinessRuleException("Move or delete the articles in this category before deleting it.");
        }

        dbContext.KnowledgeCategories.Remove(category);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyCollection<KnowledgeArticleListItemResponse>>> Search(
        [FromQuery] string? q,
        [FromQuery] Guid? categoryId,
        CancellationToken cancellationToken)
    {
        EnsureKnowledgeViewAccess();
        var articles = await QueryArticles()
            .Where(x => x.Status == "Published")
            .Where(x => !categoryId.HasValue || x.CategoryId == categoryId.Value)
            .ToListAsync(cancellationToken);

        var results = articles
            .Where(CanViewArticle)
            .Where(x => MatchesSearch(x, q))
            .OrderByDescending(x => ScoreArticle(x, q, null))
            .ThenByDescending(x => x.PublishedAt ?? x.UpdatedAt ?? x.CreatedAt)
            .Select(MapListItem)
            .ToList();

        return Ok(results);
    }

    [HttpGet("articles/{id:guid}/related")]
    public async Task<ActionResult<IReadOnlyCollection<KnowledgeArticleListItemResponse>>> Related(Guid id, CancellationToken cancellationToken)
    {
        EnsureKnowledgeViewAccess();
        var article = await LoadArticleAsync(id, cancellationToken);
        var candidates = await QueryArticles()
            .Where(x => x.Id != article.Id && x.Status == "Published")
            .ToListAsync(cancellationToken);

        var related = candidates
            .Where(CanViewArticle)
            .Select(candidate => new { Article = candidate, Score = ScoreArticle(candidate, article.Title, article) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Article.PublishedAt ?? x.Article.UpdatedAt ?? x.Article.CreatedAt)
            .Take(5)
            .Select(x => MapListItem(x.Article))
            .ToList();

        return Ok(related);
    }

    [HttpGet("suggestions/work-order/{workOrderId:guid}")]
    public async Task<ActionResult<IReadOnlyCollection<KnowledgeArticleListItemResponse>>> SuggestForWorkOrder(Guid workOrderId, CancellationToken cancellationToken)
    {
        EnsureKnowledgeViewAccess();

        var workOrder = await dbContext.WorkOrders
            .Include(x => x.Client)
            .Include(x => x.Site)
            .Include(x => x.Asset)
            .ThenInclude(x => x!.AssetCategory)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == workOrderId, cancellationToken)
            ?? throw new NotFoundException("Work order not found.");

        var searchText = string.Join(' ', new[]
        {
            workOrder.Title,
            workOrder.Description,
            workOrder.JobCardNotes,
            workOrder.WorkDoneNotes,
            workOrder.Asset?.AssetCategory?.Name,
            workOrder.Client?.ClientName,
            workOrder.Site?.SiteName,
            workOrder.Asset?.AssetName
        }.Where(x => !string.IsNullOrWhiteSpace(x)));

        var articles = await QueryArticles()
            .Where(x => x.Status == "Published")
            .ToListAsync(cancellationToken);

        var suggestions = articles
            .Where(CanViewArticle)
            .Select(article => new { Article = article, Score = ScoreArticle(article, searchText, null) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Article.PublishedAt ?? x.Article.UpdatedAt ?? x.Article.CreatedAt)
            .Take(5)
            .Select(x => MapListItem(x.Article))
            .ToList();

        return Ok(suggestions);
    }

    private IQueryable<KnowledgeArticle> QueryArticles() =>
        dbContext.KnowledgeArticles
            .Include(x => x.Category)
            .Include(x => x.CreatedByUser)
            .Include(x => x.UpdatedByUser)
            .Include(x => x.ArticleTags)
            .ThenInclude(x => x.Tag)
            .Include(x => x.Versions)
            .Where(x => x.TenantId == TenantId);

    private async Task<KnowledgeArticle> LoadArticleAsync(Guid id, CancellationToken cancellationToken) =>
        await QueryArticles().SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
        ?? throw new NotFoundException("Knowledge article not found.");

    private bool CanViewArticle(KnowledgeArticle article)
    {
        if (article.Visibility == "AdminOnly")
        {
            return IsAdmin;
        }

        return true;
    }

    private static bool MatchesSearch(KnowledgeArticle article, string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return true;
        }

        var term = q.Trim();
        return article.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
            || (article.Summary?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
            || article.Body.Contains(term, StringComparison.OrdinalIgnoreCase)
            || (article.Category?.Name?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
            || article.ArticleTags.Any(x => x.Tag != null && x.Tag.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static int ScoreArticle(KnowledgeArticle article, string? searchText, KnowledgeArticle? relatedSeed)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return 1;
        }

        var tokens = searchText
            .Split([' ', '-', ',', '.', '/', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length > 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var score = 0;
        foreach (var token in tokens)
        {
            if (article.Title.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                score += 4;
            }
            if (article.Summary?.Contains(token, StringComparison.OrdinalIgnoreCase) == true)
            {
                score += 2;
            }
            if (article.Body.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                score += 1;
            }
            if (article.Category?.Name?.Contains(token, StringComparison.OrdinalIgnoreCase) == true)
            {
                score += 3;
            }
            if (article.ArticleTags.Any(x => x.Tag != null && x.Tag.Name.Contains(token, StringComparison.OrdinalIgnoreCase)))
            {
                score += 3;
            }
        }

        if (relatedSeed is not null && article.CategoryId.HasValue && article.CategoryId == relatedSeed.CategoryId)
        {
            score += 3;
        }

        if (relatedSeed is not null)
        {
            var sharedTags = article.ArticleTags
                .Select(x => x.Tag?.Slug)
                .Intersect(relatedSeed.ArticleTags.Select(x => x.Tag?.Slug), StringComparer.OrdinalIgnoreCase)
                .Count();
            score += sharedTags * 3;
        }

        return score;
    }

    private async Task ReplaceTagsAsync(KnowledgeArticle article, IEnumerable<string?>? tagNames, CancellationToken cancellationToken)
    {
        var existing = await dbContext.KnowledgeArticleTags
            .Where(x => x.ArticleId == article.Id)
            .ToListAsync(cancellationToken);
        if (existing.Count > 0)
        {
            dbContext.KnowledgeArticleTags.RemoveRange(existing);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var normalizedTags = (tagNames ?? [])
            .Select(x => x?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();

        foreach (var tagName in normalizedTags)
        {
            var slug = Slugify(tagName);
            var tag = await dbContext.KnowledgeTags.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Slug == slug, cancellationToken);
            if (tag is null)
            {
                tag = new KnowledgeTag
                {
                    TenantId = TenantId,
                    Name = tagName,
                    Slug = slug
                };
                dbContext.KnowledgeTags.Add(tag);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            dbContext.KnowledgeArticleTags.Add(new KnowledgeArticleTag
            {
                TenantId = TenantId,
                ArticleId = article.Id,
                TagId = tag.Id
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> GetNextVersionNumberAsync(Guid articleId, CancellationToken cancellationToken)
    {
        var current = await dbContext.KnowledgeArticleVersions
            .Where(x => x.ArticleId == articleId)
            .MaxAsync(x => (int?)x.VersionNumber, cancellationToken);
        return (current ?? 0) + 1;
    }

    private async Task CreateVersionAsync(KnowledgeArticle article, int versionNumber, Guid actorUserId, CancellationToken cancellationToken)
    {
        dbContext.KnowledgeArticleVersions.Add(new KnowledgeArticleVersion
        {
            TenantId = article.TenantId,
            ArticleId = article.Id,
            VersionNumber = versionNumber,
            Title = article.Title,
            Body = article.Body,
            UpdatedByUserId = actorUserId
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Guid?> ResolveCategoryIdAsync(Guid? categoryId, CancellationToken cancellationToken)
    {
        if (!categoryId.HasValue)
        {
            return null;
        }

        var exists = await dbContext.KnowledgeCategories.AnyAsync(x => x.TenantId == TenantId && x.Id == categoryId.Value, cancellationToken);
        if (!exists)
        {
            throw new BusinessRuleException("Knowledge category not found for this tenant.");
        }

        return categoryId.Value;
    }

    private async Task<string> BuildUniqueSlugAsync(string title, Guid? currentArticleId, CancellationToken cancellationToken)
    {
        var baseSlug = Slugify(title);
        var candidate = baseSlug;
        var counter = 2;

        while (await dbContext.KnowledgeArticles.AnyAsync(
                   x => x.TenantId == TenantId && x.Slug == candidate && (!currentArticleId.HasValue || x.Id != currentArticleId.Value),
                   cancellationToken))
        {
            candidate = $"{baseSlug}-{counter++}";
        }

        return candidate;
    }

    private static string Slugify(string value)
    {
        var builder = new StringBuilder();
        var lastWasDash = false;

        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                lastWasDash = false;
            }
            else if (!lastWasDash)
            {
                builder.Append('-');
                lastWasDash = true;
            }
        }

        return builder.ToString().Trim('-');
    }

    private static string NormalizeStatus(string? status)
    {
        var value = string.IsNullOrWhiteSpace(status) ? "Draft" : status.Trim();
        return value switch
        {
            "Draft" or "Published" or "Archived" => value,
            _ => throw new BusinessRuleException("Knowledge status must be Draft, Published, or Archived.")
        };
    }

    private static string NormalizeVisibility(string? visibility)
    {
        var value = string.IsNullOrWhiteSpace(visibility) ? "Internal" : visibility.Trim();
        return value switch
        {
            "Internal" or "TechnicianOnly" or "AdminOnly" or "Public" => value,
            _ => throw new BusinessRuleException("Knowledge visibility must be Internal, TechnicianOnly, AdminOnly, or Public.")
        };
    }

    private static void ValidateArticleRequest(UpsertKnowledgeArticleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new BusinessRuleException("Article title is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            throw new BusinessRuleException("Article body is required.");
        }
    }

    private async Task<KnowledgeArticleDetailResponse> MapDetailAsync(KnowledgeArticle article, CancellationToken cancellationToken)
    {
        var related = await QueryArticles()
            .Where(x => x.Id != article.Id && x.Status == "Published")
            .ToListAsync(cancellationToken);

        var relatedItems = related
            .Where(CanViewArticle)
            .Select(candidate => new { Article = candidate, Score = ScoreArticle(candidate, article.Title, article) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(5)
            .Select(x => MapListItem(x.Article))
            .ToList();

        return new KnowledgeArticleDetailResponse(
            article.Id,
            article.Title,
            article.Slug,
            article.Summary,
            article.Body,
            article.CategoryId,
            article.Category?.Name,
            article.Status,
            article.Visibility,
            article.CreatedByUserId,
            article.CreatedByUser?.FullName,
            article.UpdatedByUserId,
            article.UpdatedByUser?.FullName,
            article.CreatedAt,
            article.UpdatedAt,
            article.PublishedAt,
            article.SourceWorkOrderId,
            article.ViewCount,
            article.HelpfulCount,
            article.NotHelpfulCount,
            article.ArticleTags
                .Where(x => x.Tag is not null)
                .Select(x => x.Tag!.Name)
                .OrderBy(x => x)
                .ToArray(),
            article.Versions
                .OrderByDescending(x => x.VersionNumber)
                .Select(x => new KnowledgeArticleVersionResponse(x.Id, x.VersionNumber, x.Title, x.Body, x.UpdatedByUserId, x.CreatedAt))
                .ToArray(),
            relatedItems);
    }

    private static KnowledgeArticleListItemResponse MapListItem(KnowledgeArticle article) =>
        new(
            article.Id,
            article.Title,
            article.Slug,
            article.Summary,
            article.CategoryId,
            article.Category?.Name,
            article.Status,
            article.Visibility,
            article.PublishedAt,
            article.UpdatedAt ?? article.CreatedAt,
            article.ViewCount,
            article.HelpfulCount,
            article.NotHelpfulCount,
            article.ArticleTags
                .Where(x => x.Tag is not null)
                .Select(x => x.Tag!.Name)
                .OrderBy(x => x)
                .ToArray());

    private static KnowledgeCategoryResponse MapCategory(KnowledgeCategory category) =>
        new(category.Id, category.Name, category.Description, category.Icon, category.DisplayOrder, category.IsActive, category.IsPublic);

    private static string BuildDraftSummary(WorkOrder workOrder)
    {
        var location = workOrder.Site?.SiteName ?? workOrder.Client?.ClientName ?? workOrder.Asset?.AssetName ?? "field job";
        return $"{workOrder.Title} resolved on {location}.";
    }

    private static string BuildDraftBody(WorkOrder workOrder)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {workOrder.Title}");
        builder.AppendLine();
        builder.AppendLine("## Context");
        builder.AppendLine($"- Work order: {workOrder.WorkOrderNumber}");
        builder.AppendLine($"- Client: {workOrder.Client?.ClientName ?? "Not recorded"}");
        builder.AppendLine($"- Site: {workOrder.Site?.SiteName ?? "Not recorded"}");
        builder.AppendLine($"- Asset: {workOrder.Asset?.AssetName ?? "Not recorded"}");
        builder.AppendLine();
        builder.AppendLine("## Problem");
        builder.AppendLine(workOrder.Description ?? "Add the reported issue.");
        builder.AppendLine();
        builder.AppendLine("## Findings");
        builder.AppendLine(workOrder.JobCardNotes ?? "Add diagnostic findings.");
        builder.AppendLine();
        builder.AppendLine("## Resolution");
        builder.AppendLine(workOrder.WorkDoneNotes ?? "Add the repair steps taken.");
        builder.AppendLine();
        builder.AppendLine("## Materials used");
        if (workOrder.MaterialUsages.Count == 0)
        {
            builder.AppendLine("- Add materials used.");
        }
        else
        {
            foreach (var usage in workOrder.MaterialUsages)
            {
                builder.AppendLine($"- {usage.MaterialItem?.ItemName ?? "Material"} x {usage.QuantityUsed}");
            }
        }
        builder.AppendLine();
        builder.AppendLine("## Reuse notes");
        builder.AppendLine("- Add repeatable troubleshooting steps.");
        builder.AppendLine("- Add safety checks and verification steps.");
        return builder.ToString().Trim();
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

public sealed record UpsertKnowledgeArticleRequest(
    string Title,
    string? Summary,
    string Body,
    Guid? CategoryId,
    string Status,
    string Visibility,
    IReadOnlyCollection<string>? Tags);

public sealed record UpsertKnowledgeCategoryRequest(string Name, string? Description, string? Icon, int DisplayOrder, bool IsPublic);

public sealed record KnowledgeCategoryResponse(Guid Id, string Name, string? Description, string? Icon, int DisplayOrder, bool IsActive, bool IsPublic);

public sealed record KnowledgeArticleListItemResponse(
    Guid Id,
    string Title,
    string Slug,
    string? Summary,
    Guid? CategoryId,
    string? CategoryName,
    string Status,
    string Visibility,
    DateTime? PublishedAt,
    DateTime UpdatedAt,
    int ViewCount,
    int HelpfulCount,
    int NotHelpfulCount,
    IReadOnlyCollection<string> Tags);

public sealed record KnowledgeArticleVersionResponse(Guid Id, int VersionNumber, string Title, string Body, Guid? UpdatedByUserId, DateTime CreatedAt);

public sealed record KnowledgeArticleDetailResponse(
    Guid Id,
    string Title,
    string Slug,
    string? Summary,
    string Body,
    Guid? CategoryId,
    string? CategoryName,
    string Status,
    string Visibility,
    Guid CreatedByUserId,
    string? CreatedByName,
    Guid? UpdatedByUserId,
    string? UpdatedByName,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? PublishedAt,
    Guid? SourceWorkOrderId,
    int ViewCount,
    int HelpfulCount,
    int NotHelpfulCount,
    IReadOnlyCollection<string> Tags,
    IReadOnlyCollection<KnowledgeArticleVersionResponse> Versions,
    IReadOnlyCollection<KnowledgeArticleListItemResponse> RelatedArticles);
