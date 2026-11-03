using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Common;

/// <summary>
/// A single page of a larger result set. List endpoints return this envelope when the caller
/// passes <c>page</c>/<c>pageSize</c>; without those params they keep returning a bare array for
/// backward compatibility.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>Projects each item to a response type while preserving the paging metadata.</summary>
    public PagedResult<TResult> Map<TResult>(Func<T, TResult> selector) =>
        new(Items.Select(selector).ToList(), Page, PageSize, TotalCount);
}

public static class QueryablePagingExtensions
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    /// <summary>True when the caller asked for pagination (either param present).</summary>
    public static bool IsPagingRequested(int? page, int? pageSize) => page.HasValue || pageSize.HasValue;

    public static (int Page, int PageSize) Normalize(int? page, int? pageSize)
    {
        var normalizedPage = page.GetValueOrDefault(1);
        if (normalizedPage < 1)
        {
            normalizedPage = 1;
        }

        var normalizedSize = pageSize.GetValueOrDefault(DefaultPageSize);
        if (normalizedSize < 1)
        {
            normalizedSize = DefaultPageSize;
        }
        else if (normalizedSize > MaxPageSize)
        {
            normalizedSize = MaxPageSize;
        }

        return (normalizedPage, normalizedSize);
    }

    /// <summary>Counts the query and returns the requested slice, running the count and page
    /// query against the database (for a translatable <see cref="IQueryable{T}"/>).</summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> query, int? page, int? pageSize, CancellationToken cancellationToken = default)
    {
        var (normalizedPage, normalizedSize) = Normalize(page, pageSize);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((normalizedPage - 1) * normalizedSize)
            .Take(normalizedSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<T>(items, normalizedPage, normalizedSize, totalCount);
    }

    /// <summary>Pages an already-materialized, in-memory-filtered list (used where the filter
    /// predicates can't be translated to SQL).</summary>
    public static PagedResult<T> ToPagedResult<T>(this IReadOnlyList<T> source, int? page, int? pageSize)
    {
        var (normalizedPage, normalizedSize) = Normalize(page, pageSize);
        var items = source
            .Skip((normalizedPage - 1) * normalizedSize)
            .Take(normalizedSize)
            .ToList();
        return new PagedResult<T>(items, normalizedPage, normalizedSize, source.Count);
    }
}
