namespace Ecosys.Api.Observability;

/// <summary>
/// Puts the request's trace identifier into the logging scope, so every log line written while the
/// request is being handled carries it.
/// <para>
/// The error handlers in <c>Program.cs</c> hand the caller <c>traceId</c> =
/// <see cref="HttpContext.TraceIdentifier"/> in the response body and tell them to quote it. That
/// value appeared in no log line, so a user-reported trace id could not be looked up and returning
/// it achieved nothing (EC-P2-09). The same identifier is used here, so what the caller reads out is
/// exactly what is greppable in the logs.
/// </para>
/// <para>
/// Scopes are only rendered by providers that are told to include them; <c>Logging:Console</c> in
/// appsettings.json turns that on. The scope is opened on one logger but the factory's scope
/// provider is shared, so it applies to every logger resolved further down the request.
/// </para>
/// </summary>
public sealed class RequestCorrelationMiddleware(RequestDelegate next, ILogger<RequestCorrelationMiddleware> logger)
{
    /// <summary>
    /// Returned on every response, not just errors. A caller reporting "the list came back empty at
    /// 09:14" has no error body to quote, and this is the value support needs to find the request.
    /// </summary>
    public const string TraceIdHeaderName = "X-Trace-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = context.TraceIdentifier;
        context.Response.Headers[TraceIdHeaderName] = traceId;

        using (logger.BeginScope(new Dictionary<string, object?> { ["TraceId"] = traceId }))
        {
            await next(context);
        }
    }
}
