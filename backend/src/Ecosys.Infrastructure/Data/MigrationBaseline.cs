using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Data;

/// <summary>
/// The migration history was squashed into one baseline. A database built from the old history
/// already has the tables, so applying the baseline to it would fail half-way; this stops start-up
/// first, with a message saying what to do.
/// </summary>
public static class MigrationBaseline
{
    public const string BaselineMigrationId = "20261108043357_InitialBaseline";

    /// <summary>Throws when the database was migrated with the pre-baseline history.</summary>
    public static async Task EnsureCompatibleAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var applied = (await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        if (applied.Count > 0 && !applied.Contains(BaselineMigrationId))
        {
            throw new InvalidOperationException(
                "This database was created with the migration history from before the squash into "
                + $"{BaselineMigrationId}. Recreate the database (or restore it into a fresh one built from the baseline); "
                + "it cannot be upgraded in place.");
        }
    }
}
