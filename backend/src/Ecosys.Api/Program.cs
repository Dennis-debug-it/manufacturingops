using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Ecosys.Api.Configuration;
using Ecosys.Api.Observability;
using Microsoft.AspNetCore.HttpOverrides;
using Ecosys.Api.Hubs;
using Ecosys.Infrastructure;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Options;
using Ecosys.Infrastructure.Security;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Errors;
using Ecosys.Shared.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
const string EcosysCorsPolicyName = "EcosysCors";

var environmentOverrides = BuildEnvironmentOverrides();
if (environmentOverrides.Count > 0)
{
    builder.Configuration.AddInMemoryCollection(environmentOverrides);
}

var jwtOptions = StartupSecurityValidation.ValidateJwtOptions(
    builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>());
var authSecurityOptions = StartupSecurityValidation.ValidateAuthSecurityOptions(
    builder.Configuration.GetSection(AuthSecurityOptions.SectionName).Get<AuthSecurityOptions>());
_ = StartupSecurityValidation.ValidatePlatformAdminOptions(
    builder.Configuration.GetSection(PlatformAdminOptions.SectionName).Get<PlatformAdminOptions>());

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.Configure<PlatformAdminOptions>(builder.Configuration.GetSection(PlatformAdminOptions.SectionName));
builder.Services.Configure<PublicAppOptions>(builder.Configuration.GetSection(PublicAppOptions.SectionName));
builder.Services.Configure<AuthSecurityOptions>(builder.Configuration.GetSection(AuthSecurityOptions.SectionName));
builder.Services.Configure<TimeZoneOptions>(builder.Configuration.GetSection(TimeZoneOptions.SectionName));
builder.Services.AddSingleton<IRuntimeMetrics, RuntimeMetrics>();
builder.Services.AddHttpClient();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddScoped<IServiceOpsNotifier, SignalRServiceOpsNotifier>();

var allowedCorsOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()
    ?.Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Select(origin => origin.Trim())
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(EcosysCorsPolicyName, policy =>
    {
        if (allowedCorsOrigins.Length > 0)
        {
            policy.WithOrigins(allowedCorsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
            return;
        }

        policy.SetIsOriginAllowed(_ => false);
    });
});

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        // SignalR sends the JWT as a query string for WebSocket connections
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            message = "Too many authentication requests. Please try again shortly.",
            errorCode = "RATE_LIMITED"
        }, cancellationToken);
    };

    options.AddPolicy("PublicEndpoints", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            $"public:{clientIp}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromSeconds(60),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            });
    });

    options.AddPolicy("AuthEndpoints", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var partitionKey = $"{httpContext.Request.Path}:{clientIp}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authSecurityOptions.RateLimitPermitLimit,
                Window = TimeSpan.FromSeconds(authSecurityOptions.RateLimitWindowSeconds),
                QueueLimit = authSecurityOptions.RateLimitQueueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            });
    });
});

builder.Services.AddAuthorization(options =>
{
    // Defence in depth: a role claim alone must NEVER be enough to reach the platform — the caller
    // must also belong to the platform root tenant. Platform and tenant role names no longer overlap
    // (see AppRoles.IsPlatformRole), but this assertion means a future name clash cannot become an
    // authorization bypass on its own.
    static bool IsPlatformTenant(AuthorizationHandlerContext context) =>
        Guid.TryParse(context.User.FindFirst(TenantClaimTypes.TenantId)?.Value, out var tenantId)
        && tenantId == PlatformConstants.RootTenantId;

    void AddPlatformPolicy(string name, params string[] roles) =>
        options.AddPolicy(name, policy => policy
            .RequireRole(roles)
            .RequireAssertion(IsPlatformTenant));

    AddPlatformPolicy("PlatformAccess", AppRoles.PlatformRoles);
    AddPlatformPolicy("PlatformOwnerOnly", AppRoles.SuperAdmin, AppRoles.PlatformOwner);
    AddPlatformPolicy("PlatformAdminAccess", AppRoles.SuperAdmin, AppRoles.PlatformOwner, AppRoles.PlatformSuperAdmin, AppRoles.PlatformAdmin, AppRoles.PlatformAdminRole);
    AddPlatformPolicy("PlatformReadOnlyAccess", AppRoles.SuperAdmin, AppRoles.PlatformOwner, AppRoles.PlatformSuperAdmin, AppRoles.PlatformAdmin, AppRoles.PlatformAdminRole, AppRoles.Auditor, AppRoles.ReadOnlyAuditor, AppRoles.Support, AppRoles.SupportAdmin, AppRoles.Finance, AppRoles.FinanceAdmin);
    AddPlatformPolicy("PlatformFinanceAccess", AppRoles.SuperAdmin, AppRoles.PlatformOwner, AppRoles.PlatformSuperAdmin, AppRoles.PlatformAdmin, AppRoles.PlatformAdminRole, AppRoles.Finance, AppRoles.FinanceAdmin);
    AddPlatformPolicy("PlatformSettingsAccess", AppRoles.SuperAdmin, AppRoles.PlatformSuperAdmin, AppRoles.PlatformAdmin);

    options.AddPolicy(AuthorizationPolicies.PortalAccess, policy => policy.RequireRole(AppRoles.PortalClient));

    // The tenant workspace boundary. Both a client-portal token and a platform-staff token carry a
    // tenant_id claim, so "resolves a tenant" is NOT the same question as "is staff of that tenant":
    // relying on the former let a portal login read the whole client register and silently deactivate
    // another customer's account (EC-001), and let platform staff fall into tenant query paths that
    // fail as 500s (EC-020). Both are excluded here, at the filter — before any action body runs and
    // therefore before any EF query can commit anything.
    //
    // The membership rule itself lives in AppRoles.IsTenantWorkspacePrincipal — one definition, so the
    // policy and the tests cannot drift apart, and so the reasoning sits next to the role lists it
    // depends on. Carried by TenantAwareControllerBase, so every tenant controller inherits it.
    options.AddPolicy(AuthorizationPolicies.TenantStaff, policy => policy
        .RequireAuthenticatedUser()
        .RequireAssertion(static context => AppRoles.IsTenantWorkspacePrincipal(context.User)));

    // Fail closed. Without this, an endpoint that carries no authorization metadata at all — a new
    // controller that forgets [Authorize], a route added outside the tenant base class — is reachable
    // anonymously, and nothing about the code says so. The fallback flips that default: no metadata
    // now means "authenticated user required", and being public becomes something an endpoint has to
    // ask for. [AllowAnonymous] still wins wherever it is declared, so every genuinely public surface
    // (auth, portal auth, intake webhooks, public work order / CSAT links, the health probes below)
    // must carry it explicitly.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = GetValidationErrors(context.ModelState);
        return new BadRequestObjectResult(new
        {
            message = errors.FirstOrDefault() ?? "The request payload is invalid.",
            errorCode = "VALIDATION_ERROR",
            errors
        });
    };
});
builder.Services.AddProblemDetails();
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(180);
    options.IncludeSubDomains = true;
});
// Swagger is a development-only surface. Registering it (and mapping it below) only in
// Development means the OpenAPI generator/UI is never even built in Production — defense in depth
// on top of the environment gate for the middleware (X-01).
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Ecosys ServiceOps API",
            Version = "v1"
        });

        var bearerScheme = new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Description = "Paste a JWT bearer token here.",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        };

        options.AddSecurityDefinition("Bearer", bearerScheme);
    });
}

var app = builder.Build();

// Correlation first, so every log line written for this request — including the ones the error
// handler below writes — carries the same traceId the caller is handed in the response (EC-P2-09).
app.UseMiddleware<RequestCorrelationMiddleware>();

// Record request latency / status codes for the platform health page. So early in the pipeline that
// it captures the full request time and the final status (including errors mapped by the handler below).
app.UseMiddleware<RequestMetricsMiddleware>();

// Content-Security-Policy (EC-024). This process serves three different kinds of response and one
// blanket policy cannot be right for all of them, so the policy is chosen per request:
//
//  * API JSON, file downloads and wwwroot assets get a lockdown. Nothing here is an application
//    document: every File(...) result in this API passes a download name, so the browser saves it
//    instead of rendering it, and the SPA that consumes the JSON is served by nginx (below), not
//    from here. `img-src 'self' data:` is the single concession — wwwroot serves tenant logos under
//    /uploads/logos, and a directly-opened image is displayed inside a browser-generated image
//    document whose <img> would otherwise be blocked by `default-src 'none'`, blanking the page.
//    Everything active (script, style, frame, object, form posts, <base>) stays denied.
//  * Swagger UI, which exists only in Development, needs its own policy: Swashbuckle's index.html
//    carries an inline bootstrap <script> and swagger-ui applies inline styles, so the lockdown
//    above renders it as a blank screen. It is still restricted to same-origin assets and still
//    forbids remote script, framing and cross-origin form posts.
//
// The SPA's own CSP is NOT set here — the browser only honours the policy delivered with the
// document, and the document comes from nginx. It already carries an app-shaped policy; see
// ecosys-serviceops-frontend/nginx.conf.
const string apiContentSecurityPolicy =
    "default-src 'none'; img-src 'self' data:; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
const string swaggerContentSecurityPolicy =
    "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; "
    + "img-src 'self' data:; font-src 'self' data:; connect-src 'self'; "
    + "frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

var swaggerIsMapped = app.Environment.IsDevelopment();

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

    var path = context.Request.Path;

    headers["Content-Security-Policy"] = swaggerIsMapped && path.StartsWithSegments("/swagger")
        ? swaggerContentSecurityPolicy
        : apiContentSecurityPolicy;

    // COOP only affects documents (Swagger, a directly opened attachment or logo); it severs the
    // window handle an opener page would otherwise keep. It has no effect on fetch/XHR, so the
    // cross-origin SPA is untouched.
    headers["Cross-Origin-Opener-Policy"] = "same-origin";

    // CORP is consulted only for `no-cors` subresource loads — never for the CORS-mode fetches the
    // SPA makes against /api, and never for WebSocket upgrades on /hubs — so `same-origin` costs the
    // documented cross-origin SPA setup nothing while stopping a third-party page from pulling API
    // responses in as images/scripts. Tenant logos are the deliberate exception: TenantController
    // hands the SPA an absolute {apiOrigin}/uploads/logos/... URL that is rendered with <img src>,
    // which IS a no-cors load, so wwwroot stays explicitly cross-origin.
    headers["Cross-Origin-Resource-Policy"] = path.StartsWithSegments("/uploads")
        ? "cross-origin"
        : "same-origin";

    await next();
});

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception exception) when (!context.Response.HasStarted)
    {
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("GlobalExceptionHandler");

        // EF Core evaluates query-parameter expressions lazily. `.Where(x => x.TenantId == TenantId)`
        // does not call the TenantId getter where it is written: the getter is captured on `this` and
        // run later, during query compilation, by ParameterExtractingExpressionVisitor — which wraps
        // anything thrown there in an InvalidOperationException. So a ForbiddenException,
        // NotFoundException or BusinessRuleException raised from inside an expression tree arrived
        // here disguised, missed every type test below, and came back as a generic 500 with its
        // intended status code silently lost. That affected every such throw in the repo, not just the
        // tenant guards. Unwrap to the innermost exception this handler already knows how to
        // represent; anything unrecognised is left exactly as it was and still ends up a 500.
        var handled = exception;

        if (handled is not (AppException or JsonException or BadHttpRequestException))
        {
            var inner = exception.InnerException;

            while (inner is not null)
            {
                if (inner is AppException or JsonException or BadHttpRequestException)
                {
                    // Log the outer chain before discarding it, so the wrapping context is not lost.
                    logger.LogWarning(exception, "Handled exception recovered from a wrapping exception; mapping it to its intended response.");
                    handled = inner;
                    break;
                }

                inner = inner.InnerException;
            }
        }

        if (handled is BusinessRuleException businessRuleException)
        {
            logger.LogWarning(businessRuleException, "Business rule violation while processing request.");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Results.Json(new
            {
                message = businessRuleException.Message,
                errorCode = "BUSINESS_RULE_VIOLATION"
            }).ExecuteAsync(context);
            return;
        }

        if (handled is JsonException or BadHttpRequestException)
        {
            logger.LogWarning(handled, "Invalid request payload.");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Results.Json(new
            {
                message = "The request payload is invalid.",
                errorCode = "VALIDATION_ERROR"
            }).ExecuteAsync(context);
            return;
        }

        if (handled is AppException appException)
        {
            logger.LogWarning(appException, "Request failed with handled application exception.");
            context.Response.StatusCode = appException.StatusCode;

            if (appException.StatusCode == StatusCodes.Status401Unauthorized)
            {
                await Results.Json(new
                {
                    message = appException.Message,
                    errorCode = "UNAUTHORIZED"
                }).ExecuteAsync(context);
                return;
            }

            var extensions = new Dictionary<string, object?>
            {
                ["traceId"] = context.TraceIdentifier
            };

            if (appException is ILocalizedErrorCode codedException)
            {
                extensions["errorCode"] = codedException.ErrorCode;
            }

            await Results.Problem(
                statusCode: appException.StatusCode,
                title: appException.Message,
                extensions: extensions).ExecuteAsync(context);
            return;
        }

        if (handled is DbUpdateConcurrencyException concurrencyException)
        {
            logger.LogWarning(concurrencyException, "Concurrent update conflict while processing request.");
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "This record was changed by someone else. Refresh and try again.",
                extensions: new Dictionary<string, object?>
                {
                    ["traceId"] = context.TraceIdentifier,
                    ["errorCode"] = "CONCURRENCY_CONFLICT"
                }).ExecuteAsync(context);
            return;
        }

        if (handled is DbUpdateException dbUpdateException)
        {
            // A constraint the database enforces is a business-rule failure that happened one layer
            // lower down, not a server fault: a duplicate email or a reference to a row that is gone
            // is entirely the caller's doing and entirely their business to fix. Mapping the whole
            // family to 500 hid that — creating a user whose email already exists came back as an
            // opaque "A database error occurred". Translate the two SQLSTATEs that are unambiguously
            // caller-caused and leave everything else exactly as it was.
            //
            // The messages stay deliberately generic: the constraint name, column and conflicting
            // value describe the schema and, for a cross-tenant unique index, confirm that some other
            // tenant already holds that value. The detail is logged, not returned.
            var postgresException = FindPostgresException(dbUpdateException);

            if (postgresException?.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation)
            {
                logger.LogWarning(dbUpdateException, "Database constraint violation ({SqlState}) while processing request.", postgresException.SqlState);
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                await Results.Json(new
                {
                    message = postgresException.SqlState == PostgresErrorCodes.UniqueViolation
                        ? "A record with these details already exists."
                        : "This record refers to something that no longer exists, or is still referenced by other records.",
                    errorCode = postgresException.SqlState == PostgresErrorCodes.UniqueViolation
                        ? "UNIQUE_VIOLATION"
                        : "FOREIGN_KEY_VIOLATION"
                }, statusCode: StatusCodes.Status409Conflict).ExecuteAsync(context);
                return;
            }

            logger.LogError(dbUpdateException, "Database update failed while processing request.");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "A database error occurred while saving changes.",
                extensions: new Dictionary<string, object?>
                {
                    ["traceId"] = context.TraceIdentifier
                }).ExecuteAsync(context);
            return;
        }

        if (exception is not null)
        {
            logger.LogError(exception, "Unhandled exception while processing request.");
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "An unexpected error occurred.",
            extensions: new Dictionary<string, object?>
            {
                ["traceId"] = context.TraceIdentifier
            }).ExecuteAsync(context);
    }
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.RoutePrefix = "swagger");
}

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    try
    {
        await dbContext.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Database migration failed. The application cannot start safely until pending migrations are applied.");
        throw;
    }

    try
    {
        var platformBootstrapService = scope.ServiceProvider.GetRequiredService<IPlatformBootstrapService>();
        var starterPackService = scope.ServiceProvider.GetRequiredService<IStarterPackService>();
        await platformBootstrapService.EnsurePlatformAdminAsync();
        await starterPackService.EnsureSystemStarterPacksSeededAsync();

        if (app.Environment.IsDevelopment())
        {
            var testDataSeeder = scope.ServiceProvider.GetRequiredService<ITestDataSeeder>();
            await testDataSeeder.SeedAsync();
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Post-migration bootstrap completed with errors.");
    }
}

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};

// The shipped compose stack puts nginx in front of this process on a container network, so the
// socket peer is the proxy, not the user. ForwardedHeaders only rewrites RemoteIpAddress when the
// peer is trusted, and the built-in trust list is loopback only — which the proxy is not — so
// X-Forwarded-For was discarded and every caller collapsed into one identity: one shared auth
// rate-limit bucket (20 attempts locked login for everyone) and the proxy's IP in every audit row.
//
// ECOSYS_KNOWN_PROXIES (comma-separated; plain IPs or CIDR blocks, since a container's proxy address
// is assigned by Docker and is better expressed as the subnet) declares who may speak for the client.
// It replaces the defaults rather than extending them: the middleware stops at the first untrusted
// hop, so leaving loopback in the list lets anything that can reach this process over loopback claim
// any client IP it likes. Nothing is trusted unless it is named here.
var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
var knownProxiesEnv = Environment.GetEnvironmentVariable("ECOSYS_KNOWN_PROXIES");
var trustedProxyCount = 0;

if (!string.IsNullOrWhiteSpace(knownProxiesEnv))
{
    var knownProxies = new List<System.Net.IPAddress>();
    var knownNetworks = new List<System.Net.IPNetwork>();

    foreach (var entry in knownProxiesEnv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        if (System.Net.IPAddress.TryParse(entry, out var proxyAddress))
        {
            knownProxies.Add(proxyAddress);
        }
        else if (TryParseProxyNetwork(entry, out var proxyNetwork))
        {
            knownNetworks.Add(proxyNetwork);
        }
        else
        {
            startupLogger.LogWarning("Ignoring unparseable ECOSYS_KNOWN_PROXIES entry '{Entry}'. Expected an IP address or a CIDR block.", entry);
        }
    }

    trustedProxyCount = knownProxies.Count + knownNetworks.Count;

    if (trustedProxyCount > 0)
    {
        // Clear before adding: the defaults are populated by the options constructor, and anything
        // left in them stays trusted.
        forwardedHeadersOptions.KnownProxies.Clear();
        forwardedHeadersOptions.KnownIPNetworks.Clear();

        foreach (var proxyAddress in knownProxies)
        {
            forwardedHeadersOptions.KnownProxies.Add(proxyAddress);
        }

        foreach (var proxyNetwork in knownNetworks)
        {
            forwardedHeadersOptions.KnownIPNetworks.Add(proxyNetwork);
        }
    }
}

if (trustedProxyCount == 0)
{
    startupLogger.LogWarning(
        "ECOSYS_KNOWN_PROXIES is not set, so X-Forwarded-For is not trusted. Behind a reverse proxy this means "
        + "audit logs record the proxy's address and every caller shares a single authentication rate-limit bucket. "
        + "Set ECOSYS_KNOWN_PROXIES to the proxy's IP address or subnet.");
}

app.UseForwardedHeaders(forwardedHeadersOptions);
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseCors(EcosysCorsPolicyName);
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<UserSessionHeartbeatMiddleware>();
app.UseMiddleware<PasswordChangeEnforcementMiddleware>();
app.UseMiddleware<TenantLicenseEnforcementMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ServiceOpsHub>("/hubs/serviceops");
// Three probes, because "is the process alive" and "can it serve traffic" are different questions and
// answering both with one endpoint gets the process killed for a problem it did not cause: a momentary
// database blip failed /health, and an orchestrator reading that as liveness restarts a container that
// was working perfectly and would have recovered on its own.
//
//  * /health/live  — no checks at all. It answers only if the process is running and its request
//                    pipeline works, which is exactly what a liveness probe is entitled to restart on.
//  * /health/ready — the database check. A readiness failure takes the instance out of rotation and
//                    puts it back when the dependency returns; nothing is killed.
//  * /health       — unchanged, still database-backed. The shipped compose healthcheck and anything
//                    else already pointed at it keep working.
//
// All three are anonymous: the fallback policy above would otherwise demand a token from a probe.
app.MapHealthChecks("/health").AllowAnonymous();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = static _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = static registration => registration.Name == "postgres" }).AllowAnonymous();

app.Run();

// EF wraps the provider exception, and Npgsql itself sometimes wraps again (batched commands), so
// walk the chain rather than testing InnerException alone.
static PostgresException? FindPostgresException(Exception exception)
{
    for (var current = exception.InnerException; current is not null; current = current.InnerException)
    {
        if (current is PostgresException postgresException)
        {
            return postgresException;
        }
    }

    return null;
}

static bool TryParseProxyNetwork(string value, out System.Net.IPNetwork network) =>
    System.Net.IPNetwork.TryParse(value, out network);

static string[] GetValidationErrors(ModelStateDictionary modelState)
{
    return modelState.Values
        .SelectMany(entry => entry.Errors)
        .Select(SanitizeValidationError)
        .Distinct(StringComparer.Ordinal)
        .ToArray();
}

// Never surface raw deserializer output — JSON paths, byte positions, and target CLR type names
// leak internals and aid probing (X-04). Binding/parse failures carry an Exception (JsonException,
// FormatException, …) or a framework-generated parser message; those become a generic message,
// while genuine validation messages (DataAnnotations) are preserved.
static string SanitizeValidationError(ModelError error)
{
    const string generic = "The request payload is invalid.";
    if (error.Exception is not null || string.IsNullOrWhiteSpace(error.ErrorMessage) || LooksLikeParserMessage(error.ErrorMessage))
    {
        return generic;
    }

    return error.ErrorMessage;
}

static bool LooksLikeParserMessage(string message) =>
    message.Contains("Path: $", StringComparison.Ordinal)
    || message.Contains("LineNumber", StringComparison.OrdinalIgnoreCase)
    || message.Contains("BytePositionInLine", StringComparison.OrdinalIgnoreCase)
    || message.Contains("could not be converted", StringComparison.OrdinalIgnoreCase)
    || message.Contains("is not valid for", StringComparison.OrdinalIgnoreCase)
    || message.Contains("JSON", StringComparison.OrdinalIgnoreCase);

static Dictionary<string, string?> BuildEnvironmentOverrides()
{
    var overrides = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    AddOverride("ECOSYS_DB_CONNECTION", "ConnectionStrings:DefaultConnection");
    AddOverride("ECOSYS_JWT_SIGNING_KEY", "Jwt:SigningKey");
    AddOverride("ECOSYS_SMTP_HOST", "Smtp:Host");
    AddOverride("ECOSYS_SMTP_PORT", "Smtp:Port");
    AddOverride("ECOSYS_SMTP_USE_SSL", "Smtp:UseSsl");
    AddOverride("ECOSYS_SMTP_USERNAME", "Smtp:Username");
    AddOverride("ECOSYS_SMTP_PASSWORD", "Smtp:Password");
    AddOverride("ECOSYS_PLATFORM_ADMIN_EMAIL", "PlatformAdmin:Email");
    AddOverride("ECOSYS_PLATFORM_ADMIN_PASSWORD", "PlatformAdmin:Password");
    AddOverride("ECOSYS_PLATFORM_ADMIN_FULL_NAME", "PlatformAdmin:FullName");

    var corsOrigins = Environment.GetEnvironmentVariable("ECOSYS_CORS_ALLOWED_ORIGINS");
    if (!string.IsNullOrWhiteSpace(corsOrigins))
    {
        var origins = corsOrigins
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var index = 0; index < origins.Length; index++)
        {
            overrides[$"Cors:AllowedOrigins:{index}"] = origins[index];
        }
    }

    return overrides;

    void AddOverride(string envName, string configKey)
    {
        var value = Environment.GetEnvironmentVariable(envName);
        if (!string.IsNullOrWhiteSpace(value))
        {
            overrides[configKey] = value;
        }
    }
}

public partial class Program;
