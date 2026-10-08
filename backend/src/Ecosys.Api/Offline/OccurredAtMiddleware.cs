using System.Globalization;
using Ecosys.Infrastructure.Services;

namespace Ecosys.Api.Offline;

/// <summary>
/// Dates a synced offline action at the time it happened on the phone (<c>X-Occurred-At</c>, ISO
/// 8601). Only honoured together with an <c>Idempotency-Key</c>, i.e. on actions the technician app
/// queued; see <see cref="OccurredAt"/> for the limits.
/// </summary>
public sealed class OccurredAtMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Occurred-At";

    public async Task InvokeAsync(HttpContext context)
    {
        var raw = context.Request.Headers[HeaderName].ToString();
        if (raw.Length > 0
            && context.Request.Headers.ContainsKey(IdempotencyFilter.HeaderName)
            && DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var occurredAt))
        {
            OccurredAt.TrySet(occurredAt);
        }

        try
        {
            await next(context);
        }
        finally
        {
            OccurredAt.Clear();
        }
    }
}
