using Ecosys.Shared.Common;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

internal static class ServiceOpsSupport
{
    public static async Task EnsureExistsAsync<TEntity>(DbSet<TEntity> dbSet, Guid id, Guid tenantId, string label, CancellationToken cancellationToken)
        where TEntity : class, ITenantEntity
    {
        var exists = await dbSet.AnyAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException($"{label} was not found.");
        }
    }

    public static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
