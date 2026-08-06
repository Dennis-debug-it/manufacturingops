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
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

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
    // Platform roles ("Finance", "Support", "Auditor", ...) share a string namespace with
    // tenant roles. A role claim alone must therefore NEVER be enough to reach the platform:
    // the caller must also belong to the platform root tenant. Without this, a tenant user
    // whose role happened to be "finance" would satisfy the platform policies below.
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

    options.AddPolicy("PortalAccess", policy => policy.RequireRole(AppRoles.PortalClient));
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

// Record request latency / status codes for the platform health page. First in the pipeline so it
// captures the full request time and the final status (including errors mapped by the handler below).
app.UseMiddleware<RequestMetricsMiddleware>();

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
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

        if (exception is BusinessRuleException businessRuleException)
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

        if (exception is JsonException or BadHttpRequestException)
        {
            logger.LogWarning(exception, "Invalid request payload.");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Results.Json(new
            {
                message = "The request payload is invalid.",
                errorCode = "VALIDATION_ERROR"
            }).ExecuteAsync(context);
            return;
        }

        if (exception is AppException appException)
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

        if (exception is DbUpdateConcurrencyException concurrencyException)
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

        if (exception is DbUpdateException dbUpdateException)
        {
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

// Defaults trust loopback only; a reverse proxy on another host must be listed in
// ECOSYS_KNOWN_PROXIES (comma-separated IPs) or client IPs collapse to the proxy IP
// and every user shares one auth rate-limit bucket.
var knownProxiesEnv = Environment.GetEnvironmentVariable("ECOSYS_KNOWN_PROXIES");
if (!string.IsNullOrWhiteSpace(knownProxiesEnv))
{
    foreach (var entry in knownProxiesEnv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        if (System.Net.IPAddress.TryParse(entry, out var proxyAddress))
        {
            forwardedHeadersOptions.KnownProxies.Add(proxyAddress);
        }
    }
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
app.MapHealthChecks("/health");

app.Run();

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
