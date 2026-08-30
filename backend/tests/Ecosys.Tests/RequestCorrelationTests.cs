using Ecosys.Api.Observability;
using Ecosys.Shared.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ecosys.Tests;

/// <summary>
/// EC-P2-09. The error handlers hand the caller a <c>traceId</c> and tell them to quote it, but that
/// value appeared in no log line, so a user-reported trace id could not be looked up. These tests
/// assert the property that makes it worth returning: the id on the response and the id in the
/// logging scope are the same string.
/// </summary>
public sealed class RequestCorrelationTests
{
    [Fact]
    public async Task EveryRequest_CarriesTheSameTraceIdOnTheResponseAndInTheLoggingScope()
    {
        var sink = new ScopeSink();
        await using var factory = CreateFactory(sink);
        using var client = factory.CreateClient();

        // Any route will do — the middleware runs before routing, and an unauthenticated request is
        // handled entirely inside the pipeline without needing a database.
        var response = await client.GetAsync("/api/work-orders");

        var traceId = Assert.Single(response.Headers.GetValues(RequestCorrelationMiddleware.TraceIdHeaderName));
        Assert.False(string.IsNullOrWhiteSpace(traceId));
        Assert.Contains(traceId, sink.CapturedTraceIds);
    }

    private static WebApplicationFactory<Program> CreateFactory(ScopeSink sink)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.UseSetting(
                    "ConnectionStrings:DefaultConnection",
                    Environment.GetEnvironmentVariable("ECOSYS_TEST_DB_CONNECTION")
                        ?? "Host=localhost;Port=5432;Database=ecosys_test;Username=postgres;Password=postgres");
                builder.UseSetting("Jwt:Issuer", "ecosys-tests");
                builder.UseSetting("Jwt:Audience", "ecosys-tests");
                builder.UseSetting("Jwt:SigningKey", "12345678901234567890123456789012");
                builder.UseSetting("PlatformAdmin:Email", "platform.owner@ecosys.test");
                builder.UseSetting("PlatformAdmin:Password", "ValidPlatformAdmin123!");
                builder.UseSetting("PlatformAdmin:FullName", "Platform Owner");
                builder.UseSetting($"{AuthSecurityOptions.SectionName}:FailedLoginThreshold", "5");
                builder.UseSetting($"{AuthSecurityOptions.SectionName}:LockoutMinutes", "15");
                builder.UseSetting($"{AuthSecurityOptions.SectionName}:RateLimitPermitLimit", "20");
                builder.UseSetting($"{AuthSecurityOptions.SectionName}:RateLimitWindowSeconds", "60");
                builder.UseSetting($"{AuthSecurityOptions.SectionName}:RateLimitQueueLimit", "0");
                builder.ConfigureLogging(logging => logging.Services.AddSingleton<ILoggerProvider>(new ScopeCapturingLoggerProvider(sink)));
            });
    }

    private sealed class ScopeSink
    {
        private readonly object _gate = new();
        private readonly List<string> _traceIds = [];

        public IReadOnlyCollection<string> CapturedTraceIds
        {
            get
            {
                lock (_gate)
                {
                    return _traceIds.ToList();
                }
            }
        }

        public void Add(string traceId)
        {
            lock (_gate)
            {
                _traceIds.Add(traceId);
            }
        }
    }

    /// <summary>
    /// Reads what a real console provider would render: the scope values the shared external scope
    /// provider is holding at the moment a log line is written.
    /// </summary>
    private sealed class ScopeCapturingLoggerProvider(ScopeSink sink) : ILoggerProvider, ISupportExternalScope
    {
        private IExternalScopeProvider? _scopeProvider;

        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;

        public ILogger CreateLogger(string categoryName) => new ScopeCapturingLogger(sink, () => _scopeProvider);

        public void Dispose()
        {
        }
    }

    private sealed class ScopeCapturingLogger(ScopeSink sink, Func<IExternalScopeProvider?> scopeProvider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            scopeProvider()?.ForEachScope(
                (scope, target) =>
                {
                    if (scope is IEnumerable<KeyValuePair<string, object?>> values)
                    {
                        foreach (var value in values)
                        {
                            if (value.Key == "TraceId" && value.Value is string traceId)
                            {
                                target.Add(traceId);
                            }
                        }
                    }
                },
                sink);
        }
    }
}
