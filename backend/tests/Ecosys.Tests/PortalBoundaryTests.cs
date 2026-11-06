using System.Reflection;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

/// <summary>
/// EC-001. A client-portal JWT (<see cref="AppRoles.PortalClient"/>, issued by
/// <c>/api/portal/auth/verify</c>) deliberately carries a <c>tenant_id</c> claim so portal queries can
/// be scoped to the tenant the portal user belongs to. That made "resolves a tenant" indistinguishable
/// from "is staff of that tenant": every tenant-internal endpoint resolved a tenant for a portal caller
/// and handed back the whole tenant's rows. Proven at runtime — a portal contact of one client read the
/// tenant's entire client register, every site and every contract in the tenant, and then reached
/// <c>DELETE /api/clients/{other}</c>: that call deactivated another customer's account and cascaded its
/// sites and assets to Inactive, committed, and only afterwards failed on the audit write (which
/// dereferences a user id a portal token does not carry). The caller saw 403; the change was durable and
/// unaudited. Authorization that runs after the write is not authorization.
///
/// The fix is a boundary that must be opted OUT of: the <see cref="AuthorizationPolicies.TenantStaff"/>
/// policy sits on <see cref="TenantAwareControllerBase"/>, so every tenant controller inherits it and a
/// controller added tomorrow is inside it without anyone remembering a guard call. These tests pin both
/// halves — that the boundary is actually attached to the leaked routes, and that a portal principal is
/// refused if it ever reaches the code behind them anyway.
/// </summary>
public sealed class PortalBoundaryTests
{
    /// <summary>
    /// The tenant-internal GET routes that a portal token reached before the fix. Templates are written
    /// as the router sees them, with parameter names collapsed by <see cref="NormalizeRoute"/>.
    /// </summary>
    public static readonly string[] LeakedRoutes =
    [
        "/api/clients",
        "/api/clients/{id}",
        "/api/clients/{id}/summary",
        "/api/clients/{clientId}/sites",
        "/api/clients/{clientId}/contracts",
        "/api/clients/{clientId}/sites/{siteId}/systems",
        "/api/sites/search",
        "/api/sites/{siteId}/contracts",
        "/api/assets/{assetId}/contracts",
        "/api/contracts",
        "/api/asset-templates",
        "/api/assignment-groups",
        "/api/settings/assignment-groups",
        "/api/approvals/pending",
        "/api/attachments/entity/{entityType}/{entityId}",
        "/api/whatsapp/conversations",
        "/api/whatsapp/templates",
        "/api/access/capabilities",
        "/api/attendance/leave-types",
        "/api/workorders/repeat-visit-check",
        "/api/tenant/license",
        "/api/tenant/license/usage",
        "/api/settings/license"
    ];

    /// <summary>
    /// The state-changing client routes. Read leakage was only half of EC-001: a portal token also
    /// reached <c>DELETE /api/clients/{id}</c>, which deactivated the target client and cascaded every
    /// linked site and asset to Inactive, committed it, and only then hit the authorization failure
    /// (the response was 403 and no audit row was written — the damage was already durable).
    /// Kept as its own list so a future edit cannot quietly shrink coverage back to GETs.
    /// </summary>
    public static readonly string[] DestructiveClientRoutes =
    [
        "/api/clients/{id}",
        "/api/clients/{id}/permanent",
        "/api/clients/{id}/deactivate",
        "/api/clients/{id}/activate",
        "/api/clients/{id}/validate-deletion"
    ];

    public static TheoryData<string> LeakedRouteCases() => AsCases(LeakedRoutes);

    public static TheoryData<string> DestructiveRouteCases() => AsCases(DestructiveClientRoutes);

    private static TheoryData<string> AsCases(IEnumerable<string> routes)
    {
        var data = new TheoryData<string>();
        foreach (var route in routes)
        {
            data.Add(route);
        }

        return data;
    }

    // ── The boundary is attached ──────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(LeakedRouteCases))]
    public void EveryLeakedRoute_IsBehindTheTenantStaffPolicy(string route)
    {
        var owners = ControllersServing(route);

        Assert.True(owners.Count > 0, $"No controller action serves {route}; the route list is stale.");

        foreach (var owner in owners)
        {
            Assert.True(
                RequiresTenantStaff(owner),
                $"{route} is served by {owner.Name}, which does not require the "
                + $"'{AuthorizationPolicies.TenantStaff}' policy. A portal token would reach it.");
        }
    }

    [Theory]
    [MemberData(nameof(DestructiveRouteCases))]
    public void EveryDestructiveClientRoute_IsBehindTheTenantStaffPolicy(string route)
    {
        var owners = ControllersServing(route);

        Assert.True(owners.Count > 0, $"No controller action serves {route}; the route list is stale.");

        foreach (var owner in owners)
        {
            Assert.True(
                RequiresTenantStaff(owner),
                $"{route} is served by {owner.Name}, which does not require the "
                + $"'{AuthorizationPolicies.TenantStaff}' policy. A portal token would reach it and, "
                + "because the write commits before the audit call that fails, the damage would persist.");
        }
    }

    [Fact]
    public void EveryTenantController_InheritsTheTenantStaffPolicy()
    {
        // The point of putting the policy on the base class: this holds for controllers nobody has
        // written yet. If a future controller opts out with its own [Authorize], it fails here and the
        // author has to justify leaving the tenant boundary rather than silently falling outside it.
        var uncovered = ConcreteControllers()
            .Where(type => typeof(TenantAwareControllerBase).IsAssignableFrom(type))
            .Where(type => !RequiresTenantStaff(type))
            .Select(type => type.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.Empty(uncovered);
    }

    [Fact]
    public void TenantControllers_DoNotOpenAnonymousHoles()
    {
        // [AllowAnonymous] beats any policy anywhere in the hierarchy, so it is the one way to fall out
        // of the inherited boundary by accident.
        var anonymous = ConcreteControllers()
            .Where(type => typeof(TenantAwareControllerBase).IsAssignableFrom(type))
            .Where(type =>
                type.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any()
                || type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Any(method => method.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any()))
            .Select(type => type.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.Empty(anonymous);
    }

    [Fact]
    public void AnnouncementsActive_IsAnAnonymousPlatformFeed_NotATenantLeak()
    {
        // The 24th reachable route. It is [AllowAnonymous] by design and returns published
        // platform-wide announcements only — no tenant or client rows — so a portal token reaching it
        // is no different from an unauthenticated caller reaching it. Pinned here so the exception
        // stays a deliberate one; if it ever starts serving tenant data this test is the reminder.
        var action = typeof(PlatformAnnouncementsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(method => method
                .GetCustomAttributes<HttpMethodAttribute>(inherit: true)
                .Any(http => http.Template == "/api/announcements/active"));

        Assert.NotEmpty(action.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true));
        Assert.False(typeof(TenantAwareControllerBase).IsAssignableFrom(typeof(PlatformAnnouncementsController)));
    }

    // ── The portal side is untouched ──────────────────────────────────────────

    [Theory]
    [InlineData(typeof(PortalController))]
    [InlineData(typeof(PortalAuthController))]
    [InlineData(typeof(PortalProjectsController))]
    public void PortalControllers_AreOutsideTheTenantBoundary(Type controllerType)
    {
        // If a portal controller ever derived from TenantAwareControllerBase it would inherit the
        // TenantStaff policy and lock every portal user out of their own portal.
        Assert.False(typeof(TenantAwareControllerBase).IsAssignableFrom(controllerType));
        Assert.False(RequiresTenantStaff(controllerType));
    }

    [Theory]
    [InlineData(typeof(PortalController))]
    [InlineData(typeof(PortalProjectsController))]
    public void PortalControllers_StillRequireThePortalPolicy(Type controllerType)
    {
        Assert.Contains(
            controllerType.GetCustomAttributes<AuthorizeAttribute>(inherit: true),
            attribute => attribute.Policy == AuthorizationPolicies.PortalAccess);
    }

    [Fact]
    public void PortalContext_StillResolvesAPortalPrincipal()
    {
        var clientId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var portalContext = PortalContextFor(PortalPrincipal(tenantId, clientId));

        Assert.True(portalContext.IsAuthenticated);
        Assert.Equal(tenantId, portalContext.GetRequiredTenantId());
        Assert.Equal(clientId, portalContext.GetRequiredClientId());
    }

    [Fact]
    public void PortalContext_RejectsATenantStaffPrincipal()
    {
        // The mirror image of the defect: staff tokens must not reach portal endpoints either.
        var portalContext = PortalContextFor(TenantStaffPrincipal(AppRoles.Admin, Guid.NewGuid()));

        Assert.False(portalContext.IsAuthenticated);
    }

    // ── The portal principal is recognised, and refused, in code ──────────────

    [Fact]
    public void TenantContext_ClassifiesAPortalPrincipalAsPortal_NotAsStaff()
    {
        var context = TenantContextFor(PortalPrincipal(Guid.NewGuid(), Guid.NewGuid()));

        Assert.True(context.IsPortalClient);
        Assert.False(context.IsAdmin);
        Assert.False(context.IsSuperAdmin);
        // The tenant still resolves — that is precisely why the role has to be checked separately.
        Assert.NotNull(context.TenantId);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Manager")]
    [InlineData("Dispatcher")]
    [InlineData("Technician")]
    [InlineData("finance")]
    [InlineData("Viewer")]
    public void TenantContext_DoesNotMistakeStaffForPortalClients(string tenantRole)
    {
        Assert.False(TenantContextFor(TenantStaffPrincipal(tenantRole, Guid.NewGuid())).IsPortalClient);
    }

    [Fact]
    public void UserAccessService_RefusesAPortalCallerOnEveryEntryPoint()
    {
        var accessService = UserAccessServiceFor(TenantContextFor(PortalPrincipal(Guid.NewGuid(), Guid.NewGuid())));

        Assert.Throws<ForbiddenException>(accessService.EnsureTenantOperationalAccess);
        Assert.Throws<ForbiddenException>(accessService.EnsureAdmin);
        Assert.Throws<ForbiddenException>(() => accessService.EnsureAdminOrPermission(PermissionNames.CanViewWorkOrders));
        Assert.Throws<ForbiddenException>(() => accessService.EnsureAdminOrAnyPermission(PermissionNames.CanViewWorkOrders));
    }

    [Fact]
    public void UserAccessService_StillAllowsTenantStaff()
    {
        var accessService = UserAccessServiceFor(TenantContextFor(TenantStaffPrincipal(AppRoles.Admin, Guid.NewGuid())));

        accessService.EnsureTenantOperationalAccess();
        accessService.EnsureAdmin();
    }

    /// <summary>
    /// Asserts the call is refused with a <see cref="ForbiddenException"/>, accepting it wrapped.
    /// <para>
    /// A guard that fires from inside a LINQ expression tree never surfaces as the bare type: EF Core
    /// evaluates captured members (the <c>TenantId</c> getter in <c>.Where(x =&gt; x.TenantId == TenantId)</c>)
    /// in <c>ExpressionTreeFuncletizer</c> and wraps anything they throw in an
    /// <see cref="InvalidOperationException"/>. The global handler in <c>Program.cs</c> walks the inner
    /// chain for exactly this reason, so matching on the chain here asserts what the caller actually
    /// receives — a 403 — rather than an implementation detail of where the guard happened to run.
    /// </para>
    /// </summary>
    private static async Task AssertForbiddenAsync(Func<Task> action)
    {
        var exception = await Record.ExceptionAsync(action);
        Assert.NotNull(exception);

        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is ForbiddenException)
            {
                return;
            }
        }

        Assert.Fail($"Expected a ForbiddenException in the exception chain, but got: {exception}");
    }

    // ── The two proven exploits, replayed against the controller ──────────────

    [Fact]
    public async Task PortalCaller_CannotReadTheTenantsClientRegister()
    {
        var databaseName = $"ecosys-portal-boundary-{Guid.NewGuid():N}";
        var fixture = await SeedAsync(databaseName);
        var portalContext = TenantContextFor(PortalPrincipal(fixture.TenantId, fixture.OwnClientId));

        await using var dbContext = CreateDbContext(databaseName, portalContext);
        var controller = new ClientsController(dbContext, portalContext, UserAccessServiceFor(portalContext), new NoOpAuditLogService());

        // The tenant query filter happily resolves this caller's tenant, so nothing below the guard
        // would stop the read — the guard is the whole defence.
        await AssertForbiddenAsync(() =>
            controller.GetAll(search: null, status: null, page: null, pageSize: null, CancellationToken.None));

        await AssertForbiddenAsync(() =>
            controller.Get(fixture.RivalClientId, CancellationToken.None));
    }

    [Fact]
    public async Task PortalCaller_IsRefusedOnEveryDestructiveClientAction()
    {
        var databaseName = $"ecosys-portal-boundary-{Guid.NewGuid():N}";
        var fixture = await SeedAsync(databaseName);
        var portalContext = TenantContextFor(PortalPrincipal(fixture.TenantId, fixture.OwnClientId));

        await using (var dbContext = CreateDbContext(databaseName, portalContext))
        {
            var controller = new ClientsController(dbContext, portalContext, UserAccessServiceFor(portalContext), new NoOpAuditLogService());

            await AssertForbiddenAsync(() =>
                controller.Delete(fixture.RivalClientId, CancellationToken.None));
            await AssertForbiddenAsync(() =>
                controller.DeletePermanent(fixture.RivalClientId, CancellationToken.None));
            await AssertForbiddenAsync(() =>
                controller.Deactivate(fixture.RivalClientId, CancellationToken.None));
            await AssertForbiddenAsync(() =>
                controller.Activate(fixture.RivalClientId, CancellationToken.None));
            await AssertForbiddenAsync(() =>
                controller.ValidateDeletion(fixture.RivalClientId, CancellationToken.None));
        }

        // The refusal has to happen BEFORE anything is written. DELETE /api/clients/{id} used to
        // deactivate the client, cascade its sites and assets to Inactive, commit, and only then throw
        // on the audit call — a 403 response over a durable, unaudited change. The guard now fires on
        // the very first TenantId access, above every query. Verified from a fresh context so a
        // rejected-but-still-committed change cannot hide in the one that made it.
        await using var verifyContext = CreateDbContext(databaseName);
        var rival = await verifyContext.Clients.SingleAsync(x => x.Id == fixture.RivalClientId);

        Assert.True(rival.IsActive);
        Assert.Equal(2, await verifyContext.Clients.CountAsync(x => x.TenantId == fixture.TenantId));
    }

    // ── The policy's membership rule ──────────────────────────────────────────

    [Fact]
    public void TenantStaffPolicy_ExcludesPortalLogins()
    {
        Assert.False(AppRoles.IsTenantWorkspacePrincipal(PortalPrincipal(Guid.NewGuid(), Guid.NewGuid())));
    }

    [Fact]
    public void PortalClient_IsNeitherATenantRoleNorAPlatformRole()
    {
        // It belongs to neither list, so no existing classifier accidentally admits it anywhere.
        Assert.True(AppRoles.IsPortalRole(AppRoles.PortalClient));
        Assert.False(AppRoles.IsTenantRole(AppRoles.PortalClient));
        Assert.False(AppRoles.IsPlatformRole(AppRoles.PortalClient));
        Assert.DoesNotContain(AppRoles.PortalClient, AppRoles.AllRoles);
    }

    [Fact]
    public void TenantStaffPolicy_ExcludesEveryPlatformRole()
    {
        // EC-020: platform staff carry a tenant_id claim too, so they passed a portal-only check,
        // reached the action, and blew up inside the query instead of being refused at the filter.
        // Mirrors AppRoles.PlatformRoles in full rather than hand-picking SuperAdmin, so a platform
        // role added later is excluded automatically.
        foreach (var platformRole in AppRoles.PlatformRoles)
        {
            Assert.False(
                AppRoles.IsTenantWorkspacePrincipal(TenantStaffPrincipal(platformRole, PlatformConstants.RootTenantId)),
                $"Platform role '{platformRole}' passes the {AuthorizationPolicies.TenantStaff} policy.");
        }
    }

    [Fact]
    public void TenantStaffPolicy_AgreesWithTenantContextAboutPlatformStaff()
    {
        // The policy and ITenantContext.IsSuperAdmin must classify the same principals the same way;
        // if they drift, one of them is a bypass.
        foreach (var platformRole in AppRoles.PlatformRoles)
        {
            var principal = TenantStaffPrincipal(platformRole, PlatformConstants.RootTenantId);

            Assert.True(TenantContextFor(principal).IsSuperAdmin, $"'{platformRole}' is not platform staff to ITenantContext.");
            Assert.False(AppRoles.IsTenantWorkspacePrincipal(principal), $"'{platformRole}' is tenant staff to the policy.");
        }
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Manager")]
    [InlineData("Dispatcher")]
    [InlineData("Technician")]
    [InlineData("finance")]
    [InlineData("Viewer")]
    [InlineData("User")]
    public void TenantStaffPolicy_AdmitsEveryTenantRole(string tenantRole)
    {
        // The other half of the boundary: locking real staff out would be the "fix" that gets loosened.
        Assert.True(AppRoles.IsTenantWorkspacePrincipal(TenantStaffPrincipal(tenantRole, Guid.NewGuid())));
    }

    // ── Refusals must survive the trip back through the error handler ─────────

    [Fact]
    public async Task AppExceptionFromInsideAnExpressionTree_KeepsItsStatusCode()
    {
        // EF Core does not call a property referenced inside a query where it is written. It captures
        // it and evaluates it later, during query compilation, wrapping anything thrown in an
        // InvalidOperationException. `.Where(x => x.TenantId == TenantId)` is exactly that shape, so a
        // ForbiddenException from the tenant guard reached the global handler disguised, missed its
        // `exception is AppException` test, and came back as a generic 500 instead of a 403 — for every
        // AppException thrown from an expression tree anywhere in the repo, not just this one.
        var guard = new ThrowingTenantGuard();

        await using var dbContext = CreateDbContext($"ecosys-portal-boundary-{Guid.NewGuid():N}");

        var thrown = await Record.ExceptionAsync(() =>
            dbContext.Clients.Where(x => x.TenantId == guard.TenantId).ToListAsync());

        Assert.NotNull(thrown);

        // Whether the provider wraps it or lets it through, the handler's rule has to find it.
        var forbidden = Assert.IsType<ForbiddenException>(FirstHandledException(thrown!)!);

        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public void UnrecognisedExceptions_AreNotRemappedByTheUnwrapRule()
    {
        // The conservative half: a genuine InvalidOperationException with nothing the handler knows how
        // to represent must stay a 500 rather than being coerced into some other status.
        var thrown = new InvalidOperationException("boom", new TimeoutException("inner"));

        Assert.Null(FirstHandledException(thrown));
    }

    /// <summary>
    /// The rule the global exception handler in <c>Program.cs</c> applies: if the exception itself is
    /// not one the handler can represent, walk the inner chain for the first one that is.
    /// </summary>
    private static Exception? FirstHandledException(Exception exception)
    {
        if (exception is AppException)
        {
            return exception;
        }

        for (var inner = exception.InnerException; inner is not null; inner = inner.InnerException)
        {
            if (inner is AppException)
            {
                return inner;
            }
        }

        return null;
    }

    private sealed class ThrowingTenantGuard
    {
        public Guid TenantId => throw new ForbiddenException("Tenant context is required.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool RequiresTenantStaff(Type controllerType) =>
        controllerType
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Any(attribute => attribute.Policy == AuthorizationPolicies.TenantStaff);

    private static IReadOnlyList<Type> ControllersServing(string route)
    {
        var normalized = NormalizeRoute(route);

        return ConcreteControllers()
            .Where(type => RouteTemplatesFor(type).Contains(normalized, StringComparer.Ordinal))
            .ToArray();
    }

    private static IEnumerable<Type> ConcreteControllers() =>
        typeof(TenantAwareControllerBase).Assembly
            .GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type));

    /// <summary>
    /// Every route this controller serves, composed the way attribute routing composes them: an action
    /// template starting with "/" is absolute and ignores the controller prefix, anything else is
    /// appended to each [Route] on the controller (some carry more than one).
    /// </summary>
    private static IReadOnlyCollection<string> RouteTemplatesFor(Type controllerType)
    {
        var prefixes = controllerType
            .GetCustomAttributes<RouteAttribute>(inherit: true)
            .Select(attribute => attribute.Template ?? string.Empty)
            .ToArray();

        if (prefixes.Length == 0)
        {
            prefixes = [string.Empty];
        }

        var routes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var method in controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            foreach (var http in method.GetCustomAttributes<HttpMethodAttribute>(inherit: true))
            {
                var template = http.Template ?? string.Empty;

                if (template.StartsWith('/'))
                {
                    routes.Add(NormalizeRoute(template));
                    continue;
                }

                foreach (var prefix in prefixes)
                {
                    routes.Add(NormalizeRoute($"{prefix}/{template}"));
                }
            }
        }

        return routes;
    }

    /// <summary>
    /// Route templates only have to agree on shape, not on parameter names or constraints:
    /// "/api/clients/{id:guid}/summary" and "/api/clients/{clientId}/summary" are the same route.
    /// </summary>
    private static string NormalizeRoute(string template)
    {
        var collapsed = Regex.Replace(template, @"\{[^}]*\}", "{}");
        var segments = collapsed.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return string.Join('/', segments).ToLowerInvariant();
    }

    private static ClaimsPrincipal PortalPrincipal(Guid tenantId, Guid clientId) =>
        PrincipalWith(
        [
            new Claim(ClaimTypes.Role, AppRoles.PortalClient),
            new Claim(TenantClaimTypes.TenantId, tenantId.ToString()),
            new Claim(TenantClaimTypes.ClientId, clientId.ToString()),
            new Claim(TenantClaimTypes.PortalUserId, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Email, "contact@customer.test"),
            new Claim(ClaimTypes.Name, "Portal Contact")
        ]);

    private static ClaimsPrincipal TenantStaffPrincipal(string role, Guid tenantId) =>
        PrincipalWith(
        [
            new Claim(ClaimTypes.Role, role),
            new Claim(TenantClaimTypes.TenantId, tenantId.ToString()),
            new Claim(TenantClaimTypes.UserId, Guid.NewGuid().ToString())
        ]);

    private static ClaimsPrincipal PrincipalWith(Claim[] claims) =>
        new(new ClaimsIdentity(
            claims,
            authenticationType: "Test",
            nameType: ClaimTypes.Name,
            roleType: ClaimTypes.Role));

    private static ITenantContext TenantContextFor(ClaimsPrincipal principal) =>
        (ITenantContext)CreateInfrastructureInstance("Ecosys.Infrastructure.Security.HttpTenantContext", AccessorFor(principal));

    private static IPortalContext PortalContextFor(ClaimsPrincipal principal) =>
        (IPortalContext)CreateInfrastructureInstance("Ecosys.Infrastructure.Security.HttpPortalContext", AccessorFor(principal));

    private static IUserAccessService UserAccessServiceFor(ITenantContext tenantContext) =>
        (IUserAccessService)CreateInfrastructureInstance("Ecosys.Infrastructure.Services.UserAccessService", tenantContext);

    private static IHttpContextAccessor AccessorFor(ClaimsPrincipal principal) =>
        new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };

    private static object CreateInfrastructureInstance(string typeName, object argument)
    {
        var type = typeof(AppDbContext).Assembly.GetType(typeName, throwOnError: true)!;

        return Activator.CreateInstance(
            type,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args: [argument],
            culture: null)!;
    }

    private static AppDbContext CreateDbContext(string databaseName, ITenantContext? tenantContext = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new AppDbContext(options, tenantContext);
    }

    private static async Task<PortalFixture> SeedAsync(string databaseName)
    {
        await using var dbContext = CreateDbContext(databaseName);

        var tenantId = Guid.NewGuid();

        var ownClient = new Client
        {
            TenantId = tenantId,
            ClientName = "Own Client",
            Email = "own@customer.test",
            IsActive = true
        };

        var rivalClient = new Client
        {
            TenantId = tenantId,
            ClientName = "CONFIDENTIAL RIVAL CO",
            Email = "ceo@rival.test",
            ContactPerson = "Rival CEO",
            ContactPhone = "+254700000000",
            IsActive = true
        };

        dbContext.Clients.AddRange(ownClient, rivalClient);
        await dbContext.SaveChangesAsync();

        return new PortalFixture(tenantId, ownClient.Id, rivalClient.Id);
    }

    private sealed record PortalFixture(Guid TenantId, Guid OwnClientId, Guid RivalClientId);

    private sealed class NoOpAuditLogService : IAuditLogService
    {
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, string severity = "Info", string? actorName = null, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
