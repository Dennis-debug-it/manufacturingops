using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize(Policy = "PortalAccess")]
[Route("api/portal/knowledge")]
public sealed class PortalKnowledgeController(AppDbContext dbContext, IPortalContext portalContext) : ControllerBase
{
    private Guid TenantId => portalContext.GetRequiredTenantId();

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyCollection<PortalKnowledgeCategoryResponse>>> ListCategories(CancellationToken cancellationToken)
    {
        var categories = await dbContext.KnowledgeCategories
            .Where(x => x.TenantId == TenantId && x.IsActive && x.IsPublic)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var articleCounts = await dbContext.KnowledgeArticles
            .Where(x => x.TenantId == TenantId && x.Status == "Published" && x.Visibility == "Public" && x.CategoryId != null)
            .GroupBy(x => x.CategoryId!.Value)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var countsByCategory = articleCounts.ToDictionary(x => x.CategoryId, x => x.Count);

        return Ok(categories
            .Select(category => new PortalKnowledgeCategoryResponse(
                category.Id,
                category.Name,
                category.Description,
                category.Icon,
                countsByCategory.GetValueOrDefault(category.Id)))
            .ToList());
    }

    [HttpGet("articles")]
    public async Task<ActionResult<IReadOnlyCollection<PortalKnowledgeArticleListItemResponse>>> ListArticles(
        [FromQuery] Guid? categoryId,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var articles = await QueryPublicArticles()
            .Where(x => !categoryId.HasValue || x.CategoryId == categoryId.Value)
            .OrderByDescending(x => x.PublishedAt ?? x.UpdatedAt ?? x.CreatedAt)
            .ToListAsync(cancellationToken);

        var filtered = articles
            .Where(x => MatchesSearch(x, search))
            .Select(MapListItem)
            .ToList();

        return Ok(filtered);
    }

    [HttpGet("articles/{slug}")]
    public async Task<ActionResult<PortalKnowledgeArticleDetailResponse>> GetArticle(string slug, CancellationToken cancellationToken)
    {
        var article = await QueryPublicArticles()
            .SingleOrDefaultAsync(x => x.Slug == slug, cancellationToken)
            ?? throw new NotFoundException("Article was not found.");

        article.ViewCount += 1;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(MapDetail(article));
    }

    [HttpPost("articles/{id:guid}/feedback")]
    public async Task<ActionResult<PortalKnowledgeFeedbackResponse>> SubmitFeedback(
        Guid id,
        [FromBody] PortalKnowledgeFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        var article = await QueryPublicArticles()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Article was not found.");

        var comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        if (comment is { Length: > 2000 })
        {
            comment = comment[..2000];
        }

        dbContext.KnowledgeArticleFeedback.Add(new KnowledgeArticleFeedback
        {
            TenantId = TenantId,
            ArticleId = article.Id,
            ClientPortalUserId = portalContext.PortalUserId,
            IsHelpful = request.IsHelpful,
            Comment = comment
        });

        if (request.IsHelpful)
        {
            article.HelpfulCount += 1;
        }
        else
        {
            article.NotHelpfulCount += 1;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new PortalKnowledgeFeedbackResponse("Thanks for letting us know!"));
    }

    private IQueryable<KnowledgeArticle> QueryPublicArticles() =>
        dbContext.KnowledgeArticles
            .Include(x => x.Category)
            .Include(x => x.ArticleTags)
            .ThenInclude(x => x.Tag)
            .Where(x => x.TenantId == TenantId && x.Status == "Published" && x.Visibility == "Public");

    private static bool MatchesSearch(KnowledgeArticle article, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var term = search.Trim();
        return article.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
            || (article.Summary?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
            || article.Body.Contains(term, StringComparison.OrdinalIgnoreCase)
            || article.ArticleTags.Any(x => x.Tag != null && x.Tag.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static PortalKnowledgeArticleListItemResponse MapListItem(KnowledgeArticle article) =>
        new(
            article.Id,
            article.Title,
            article.Slug,
            article.Summary,
            article.CategoryId,
            article.Category?.Name,
            article.PublishedAt,
            article.ViewCount,
            article.HelpfulCount,
            article.ArticleTags
                .Where(x => x.Tag is not null)
                .Select(x => x.Tag!.Name)
                .OrderBy(x => x)
                .ToArray());

    private static PortalKnowledgeArticleDetailResponse MapDetail(KnowledgeArticle article) =>
        new(
            article.Id,
            article.Title,
            article.Slug,
            article.Summary,
            article.Body,
            article.CategoryId,
            article.Category?.Name,
            article.PublishedAt,
            article.ViewCount,
            article.HelpfulCount,
            article.NotHelpfulCount,
            article.ArticleTags
                .Where(x => x.Tag is not null)
                .Select(x => x.Tag!.Name)
                .OrderBy(x => x)
                .ToArray());
}

public sealed record PortalKnowledgeCategoryResponse(Guid Id, string Name, string? Description, string? Icon, int ArticleCount);

public sealed record PortalKnowledgeArticleListItemResponse(
    Guid Id,
    string Title,
    string Slug,
    string? Summary,
    Guid? CategoryId,
    string? CategoryName,
    DateTime? PublishedAt,
    int ViewCount,
    int HelpfulCount,
    IReadOnlyCollection<string> Tags);

public sealed record PortalKnowledgeArticleDetailResponse(
    Guid Id,
    string Title,
    string Slug,
    string? Summary,
    string Body,
    Guid? CategoryId,
    string? CategoryName,
    DateTime? PublishedAt,
    int ViewCount,
    int HelpfulCount,
    int NotHelpfulCount,
    IReadOnlyCollection<string> Tags);

public sealed record PortalKnowledgeFeedbackRequest(bool IsHelpful, string? Comment);
public sealed record PortalKnowledgeFeedbackResponse(string Message);
