using System.Linq.Expressions;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

public sealed record BranchQueryScope(
    bool HasConfiguredBranches,
    Guid? RequestedBranchId,
    IReadOnlyCollection<Guid> AccessibleBranchIds,
    bool IncludeUnassignedRecords);

public interface IBranchAccessService
{
    Task<BranchQueryScope> GetQueryScopeAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default);
    Task<Guid?> ResolveBranchIdForWriteAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default);
    Task EnsureCanAccessBranchAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Guid>> GetAccessibleBranchIdsAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

internal sealed class BranchAccessService(AppDbContext dbContext, ITenantContext tenantContext) : IBranchAccessService
{
    public async Task<BranchQueryScope> GetQueryScopeAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default)
    {
        var activeBranchIds = await dbContext.Branches
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (activeBranchIds.Count == 0)
        {
            return new BranchQueryScope(false, null, [], true);
        }

        if (tenantContext.IsAdmin)
        {
            if (requestedBranchId.HasValue && !activeBranchIds.Contains(requestedBranchId.Value))
            {
                throw new NotFoundException("Branch was not found.");
            }

            return new BranchQueryScope(
                true,
                requestedBranchId,
                activeBranchIds,
                !requestedBranchId.HasValue);
        }

        var access = await GetCurrentUserAccessAsync(tenantId, activeBranchIds, cancellationToken);
        if (requestedBranchId.HasValue && !access.AccessibleBranchIds.Contains(requestedBranchId.Value))
        {
            throw new ForbiddenException("You do not have access to the selected branch.");
        }

        return new BranchQueryScope(true, requestedBranchId, access.AccessibleBranchIds, false);
    }

    public async Task<Guid?> ResolveBranchIdForWriteAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default)
    {
        var activeBranchIds = await dbContext.Branches
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (activeBranchIds.Count == 0)
        {
            return null;
        }

        if (requestedBranchId.HasValue)
        {
            await EnsureCanAccessBranchAsync(tenantId, requestedBranchId.Value, cancellationToken);
            return requestedBranchId.Value;
        }

        if (tenantContext.IsAdmin)
        {
            var adminUser = await LoadCurrentUserAsync(tenantId, cancellationToken);
            var defaultBranchId = ResolveDefaultBranchId(activeBranchIds, adminUser.DefaultBranchId, []);
            if (defaultBranchId.HasValue)
            {
                return defaultBranchId.Value;
            }

            if (activeBranchIds.Count == 1)
            {
                return activeBranchIds[0];
            }

            throw new BusinessRuleException("Branch selection is required.");
        }

        var access = await GetCurrentUserAccessAsync(tenantId, activeBranchIds, cancellationToken);
        if (access.AccessibleBranchIds.Count == 0)
        {
            throw new ForbiddenException("You are not assigned to any active branches.");
        }

        var resolvedBranchId = ResolveDefaultBranchId(activeBranchIds, access.DefaultBranchId, access.AccessibleBranchIds);
        if (resolvedBranchId.HasValue)
        {
            return resolvedBranchId.Value;
        }

        if (access.AccessibleBranchIds.Count == 1)
        {
            return access.AccessibleBranchIds.First();
        }

        throw new BusinessRuleException("Branch selection is required.");
    }

    public async Task EnsureCanAccessBranchAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken = default)
    {
        var scope = await GetQueryScopeAsync(tenantId, branchId, cancellationToken);
        if (!scope.HasConfiguredBranches || scope.RequestedBranchId == branchId)
        {
            return;
        }

        throw new ForbiddenException("You do not have access to the selected branch.");
    }

    public async Task<IReadOnlyCollection<Guid>> GetAccessibleBranchIdsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var scope = await GetQueryScopeAsync(tenantId, null, cancellationToken);
        return scope.AccessibleBranchIds;
    }

    private async Task<UserBranchAccessSnapshot> GetCurrentUserAccessAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> activeBranchIds,
        CancellationToken cancellationToken)
    {
        var user = await LoadCurrentUserAsync(tenantId, cancellationToken);

        if (user.HasAllBranchAccess)
        {
            return new UserBranchAccessSnapshot(activeBranchIds.ToList(), user.DefaultBranchId);
        }

        var assignedBranchIds = user.BranchAssignments
            .Where(x => activeBranchIds.Contains(x.BranchId))
            .Select(x => x.BranchId)
            .Distinct()
            .ToList();

        var defaultBranchId = user.DefaultBranchId;
        if (!defaultBranchId.HasValue)
        {
            defaultBranchId = user.BranchAssignments
                .Where(x => x.IsDefault && assignedBranchIds.Contains(x.BranchId))
                .Select(x => (Guid?)x.BranchId)
                .FirstOrDefault();
        }

        return new UserBranchAccessSnapshot(assignedBranchIds, defaultBranchId);
    }

    private async Task<User> LoadCurrentUserAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await dbContext.Users
            .Include(x => x.BranchAssignments)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == tenantContext.GetRequiredUserId(), cancellationToken)
        ?? throw new ForbiddenException("User context was not found.");

    private static Guid? ResolveDefaultBranchId(
        IReadOnlyCollection<Guid> activeBranchIds,
        Guid? preferredDefaultBranchId,
        IReadOnlyCollection<Guid> accessibleBranchIds)
    {
        if (!preferredDefaultBranchId.HasValue || !activeBranchIds.Contains(preferredDefaultBranchId.Value))
        {
            return null;
        }

        if (accessibleBranchIds.Count == 0 || accessibleBranchIds.Contains(preferredDefaultBranchId.Value))
        {
            return preferredDefaultBranchId.Value;
        }

        return null;
    }

    private sealed record UserBranchAccessSnapshot(IReadOnlyCollection<Guid> AccessibleBranchIds, Guid? DefaultBranchId);
}

public static class BranchQueryScopeExtensions
{
    public static IQueryable<TEntity> WhereAccessible<TEntity>(
        this IQueryable<TEntity> query,
        BranchQueryScope scope,
        Expression<Func<TEntity, Guid?>> branchIdSelector)
    {
        if (!scope.HasConfiguredBranches)
        {
            return query;
        }

        var parameter = branchIdSelector.Parameters[0];
        var branchExpression = branchIdSelector.Body;

        Expression predicateBody;
        if (scope.RequestedBranchId.HasValue)
        {
            predicateBody = Expression.Equal(
                branchExpression,
                Expression.Constant(scope.RequestedBranchId, typeof(Guid?)));
        }
        else
        {
            if (scope.AccessibleBranchIds.Count == 0)
            {
                predicateBody = Expression.Constant(false);
            }
            else
            {
                var branchValue = Expression.Property(branchExpression, nameof(Nullable<Guid>.Value));
                var containsMethod = typeof(Enumerable)
                    .GetMethods()
                    .Single(x => x.Name == nameof(Enumerable.Contains) && x.GetParameters().Length == 2)
                    .MakeGenericMethod(typeof(Guid));

                var containsExpression = Expression.Call(
                    containsMethod,
                    Expression.Constant(scope.AccessibleBranchIds),
                    branchValue);

                var hasValueExpression = Expression.Property(branchExpression, nameof(Nullable<Guid>.HasValue));
                var assignedBranchExpression = Expression.AndAlso(hasValueExpression, containsExpression);

                predicateBody = scope.IncludeUnassignedRecords
                    ? Expression.OrElse(Expression.Not(hasValueExpression), assignedBranchExpression)
                    : assignedBranchExpression;
            }
        }

        return query.Where(Expression.Lambda<Func<TEntity, bool>>(predicateBody, parameter));
    }
}
