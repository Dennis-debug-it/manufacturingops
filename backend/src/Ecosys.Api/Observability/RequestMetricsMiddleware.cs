using System.Diagnostics;

namespace Ecosys.Api.Observability;

/// <summary>
/// Records each HTTP request's duration and status code into <see cref="IRuntimeMetrics"/> so the
/// platform health page can report rolling request latency and 5xx error rate.
/// </summary>
public sealed class RequestMetricsMiddleware(RequestDelegate next, IRuntimeMetrics metrics)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            metrics.Record(context.Response.StatusCode, stopwatch.Elapsed.TotalMilliseconds);
        }
    }
}
