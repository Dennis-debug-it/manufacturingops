using System.Text.Json;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ecosys.Api.Offline;

/// <summary>
/// Makes a write safe to send twice. The technician app queues actions while offline and replays
/// them when the signal returns, each with its own <c>Idempotency-Key</c>. If a replay reaches the
/// server after the original was already applied (the response was lost on the way back), the
/// stored response is returned and nothing is applied again. Only successful (2xx) responses are
/// stored, so a request that failed can simply be retried. Requests without the header are untouched.
/// </summary>
public sealed class IdempotencyFilter(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IServiceScopeFactory scopeFactory,
    IOptions<JsonOptions> jsonOptions,
    ILogger<IdempotencyFilter> logger) : IAsyncActionFilter
{
    public const string HeaderName = "Idempotency-Key";
    public const string ReplayHeaderName = "Idempotent-Replay";
    private const int MaxKeyLength = 100;
    private static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        var key = request.Headers[HeaderName].ToString().Trim();
        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method)
            || key.Length == 0 || key.Length > MaxKeyLength
            || tenantContext.TenantId is not Guid tenantId || tenantContext.UserId is not Guid userId)
        {
            await next();
            return;
        }

        var path = request.Path.Value ?? string.Empty;
        var existing = await dbContext.IdempotencyRecords.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.UserId == userId && x.Key == key, context.HttpContext.RequestAborted);
        if (existing is not null)
        {
            if (!string.Equals(existing.Method, request.Method, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(existing.Path, path, StringComparison.OrdinalIgnoreCase))
            {
                context.Result = new ConflictObjectResult(new { message = "This Idempotency-Key was already used for a different request.", errorCode = "IDEMPOTENCY_KEY_REUSED" });
                return;
            }

            context.HttpContext.Response.Headers[ReplayHeaderName] = "true";
            context.Result = existing.ResponseBody is null
                ? new StatusCodeResult(existing.StatusCode)
                : new ContentResult { Content = existing.ResponseBody, ContentType = "application/json; charset=utf-8", StatusCode = existing.StatusCode };
            return;
        }

        var executed = await next();
        if (executed.Exception is not null && !executed.ExceptionHandled)
        {
            return;
        }

        int statusCode;
        string? body;
        switch (executed.Result)
        {
            case ObjectResult objectResult:
                statusCode = objectResult.StatusCode ?? StatusCodes.Status200OK;
                body = objectResult.Value is null
                    ? null
                    : JsonSerializer.Serialize(objectResult.Value, objectResult.Value.GetType(), jsonOptions.Value.JsonSerializerOptions);
                break;
            case StatusCodeResult statusCodeResult:
                statusCode = statusCodeResult.StatusCode;
                body = null;
                break;
            case EmptyResult:
                statusCode = StatusCodes.Status200OK;
                body = null;
                break;
            default:
                // Files and other streamed results aren't replayable; none of the offline actions return one.
                return;
        }

        if (statusCode is < 200 or >= 300)
        {
            return;
        }

        await SaveAsync(new IdempotencyRecord
        {
            TenantId = tenantId,
            UserId = userId,
            Key = key,
            Method = request.Method,
            Path = path,
            StatusCode = statusCode,
            ResponseBody = body,
        });
    }

    /// <summary>
    /// Stored in a separate context so nothing the action left tracked is saved with it, and so a
    /// failure here (e.g. a concurrent duplicate winning the unique index) can't undo the action.
    /// </summary>
    private async Task SaveAsync(IdempotencyRecord record)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.IdempotencyRecords.Add(record);

            // Keys only need to outlive a phone's offline spell; drop this user's old ones as we go.
            var cutoff = DateTime.UtcNow - Retention;
            db.IdempotencyRecords.RemoveRange(await db.IdempotencyRecords
                .Where(x => x.TenantId == record.TenantId && x.UserId == record.UserId && x.CreatedAt < cutoff)
                .Take(200)
                .ToListAsync());

            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Could not store the response for Idempotency-Key {Key}; a replay would apply the action again.", record.Key);
        }
    }
}
