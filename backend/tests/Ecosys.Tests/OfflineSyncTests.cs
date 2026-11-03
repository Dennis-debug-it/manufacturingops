using Ecosys.Api.Offline;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ecosys.Tests;

/// <summary>Server side of the technician app's offline queue: safe replays and on-site times.</summary>
public sealed class OfflineSyncTests
{
    [Fact]
    public void OccurredAt_AcceptsRecentPastTimes_AndIgnoresImplausibleOnes()
    {
        var now = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
        try
        {
            Assert.True(OccurredAt.TrySet(now.AddHours(-5), now));
            Assert.Equal(now.AddHours(-5), OccurredAt.UtcNow);

            // A phone slightly ahead counts as now; far ahead or more than a week old is ignored.
            Assert.True(OccurredAt.TrySet(now.AddMinutes(1), now));
            Assert.Equal(now, OccurredAt.UtcNow);
            OccurredAt.Clear();
            Assert.False(OccurredAt.TrySet(now.AddHours(1), now));
            Assert.False(OccurredAt.TrySet(now.AddDays(-8), now));
            Assert.False(OccurredAt.IsDeferred);
        }
        finally
        {
            OccurredAt.Clear();
        }
    }

    [Fact]
    public async Task IdempotencyFilter_ReplaysTheStoredResponse_WithoutRunningTheActionAgain()
    {
        var (services, tenant, user) = CreateServices();
        var runs = 0;

        async Task<IActionResult?> Send(string key, string path = "/api/workorders/1/start")
        {
            using var scope = services.CreateScope();
            var filter = CreateFilter(scope.ServiceProvider, tenant, user);
            var context = CreateContext(scope.ServiceProvider, key, path);
            await filter.OnActionExecutionAsync(context, () =>
            {
                runs++;
                return Task.FromResult(new ActionExecutedContext(context, [], new object()) { Result = new OkObjectResult(new { status = "In Progress", run = runs }) });
            });
            return context.Result;
        }

        Assert.Null(await Send("key-1"));                 // first time: the action runs
        var replay = Assert.IsType<ContentResult>(await Send("key-1"));
        Assert.Equal(1, runs);
        Assert.Equal(200, replay.StatusCode);
        Assert.Equal("{\"status\":\"In Progress\",\"run\":1}", replay.Content);

        Assert.Null(await Send("key-2"));                 // a different action runs normally
        Assert.Equal(2, runs);

        // The same key on a different endpoint is a client bug, not a replay.
        Assert.IsType<ConflictObjectResult>(await Send("key-1", "/api/workorders/1/complete"));
        Assert.Equal(2, runs);
    }

    [Fact]
    public async Task IdempotencyFilter_DoesNotStoreFailures_SoTheyCanBeRetried()
    {
        var (services, tenant, user) = CreateServices();
        var runs = 0;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var scope = services.CreateScope();
            var context = CreateContext(scope.ServiceProvider, "key-3", "/api/attendance/me/clock-in");
            await CreateFilter(scope.ServiceProvider, tenant, user).OnActionExecutionAsync(context, () =>
            {
                runs++;
                return Task.FromResult(new ActionExecutedContext(context, [], new object()) { Result = new BadRequestObjectResult(new { message = "You are already clocked in." }) });
            });
        }

        Assert.Equal(2, runs);
        using var check = services.CreateScope();
        Assert.Empty(await check.ServiceProvider.GetRequiredService<AppDbContext>().IdempotencyRecords.ToListAsync());
    }

    private static (ServiceProvider Services, Guid Tenant, Guid User) CreateServices()
    {
        var databaseName = $"ecosys-idempotency-{Guid.NewGuid():N}";
        var services = new ServiceCollection()
            .AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName))
            .BuildServiceProvider();
        return (services, Guid.NewGuid(), Guid.NewGuid());
    }

    private static IdempotencyFilter CreateFilter(IServiceProvider provider, Guid tenantId, Guid userId) =>
        new(
            provider.GetRequiredService<AppDbContext>(),
            new UserContext(tenantId, userId),
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new JsonOptions { JsonSerializerOptions = { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase } }),
            NullLogger<IdempotencyFilter>.Instance);

    private static ActionExecutingContext CreateContext(IServiceProvider provider, string key, string path)
    {
        var httpContext = new DefaultHttpContext { RequestServices = provider };
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Path = path;
        httpContext.Request.Headers[IdempotencyFilter.HeaderName] = key;
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), new object());
    }

    private sealed class UserContext(Guid tenantId, Guid userId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
        public Guid? SessionId => null;
        public string? Email => "tech@acme.test";
        public string? Role => AppRoles.Technician;
        public string? JobTitle => null;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public bool IsAdmin => false;
        public bool HasRole(string role) => string.Equals(role, AppRoles.Technician, StringComparison.OrdinalIgnoreCase);
        public bool HasPermission(string permissionName) => true;
        public Guid GetRequiredTenantId() => tenantId;
        public Guid GetRequiredUserId() => userId;
        public Guid GetRequiredSessionId() => throw new InvalidOperationException();
    }
}
