using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Data;

public static class GetOrCreateExtensions
{
    /// <summary>PostgreSQL <c>unique_violation</c>.</summary>
    private const string UniqueViolation = "23505";

    /// <summary>
    /// Returns the single row <paramref name="query"/> finds, creating it when there is none. A
    /// settings screen typically asks for the same settings twice at once on its first visit; both
    /// requests then miss and try to create the row, and the loser trips the unique index. That is
    /// not an error (the row now exists), so the loser reads what the winner wrote instead of
    /// failing with "a record with the same value already exists".
    /// </summary>
    public static async Task<T> GetOrCreateAsync<T>(
        this AppDbContext dbContext,
        IQueryable<T> query,
        Func<Task<T>> create,
        CancellationToken cancellationToken)
        where T : class
    {
        var existing = await query.SingleOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var created = await create();
        dbContext.Add(created);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return created;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            dbContext.Entry(created).State = EntityState.Detached;
            return await query.SingleAsync(cancellationToken);
        }
    }

    public static Task<T> GetOrCreateAsync<T>(this AppDbContext dbContext, IQueryable<T> query, Func<T> create, CancellationToken cancellationToken)
        where T : class =>
        dbContext.GetOrCreateAsync(query, () => Task.FromResult(create()), cancellationToken);

    internal static bool IsUniqueViolation(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException { SqlState: UniqueViolation })
            {
                return true;
            }
        }

        return false;
    }
}
