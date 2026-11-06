using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Ecosys.Infrastructure.Services;

/// <summary>One figure on a widget. <see cref="Link"/> is the in-app list behind it.</summary>
public sealed record DashboardStat(string Label, decimal Value, string Format, string? Currency, string Link, string? Tone = null);

/// <summary>One row of a widget's short list.</summary>
public sealed record DashboardListItem(string Label, string? Detail, decimal? Value, string? Format, string? Currency, string Link, string? Tone = null);

public sealed record DashboardWidgetData(
    string Key,
    string Title,
    IReadOnlyList<DashboardStat> Stats,
    IReadOnlyList<DashboardListItem> Items,
    string? Note,
    DateTime GeneratedAt);

/// <param name="Areas">The data areas the widget reads; a live <c>DashboardChanged</c> naming one means refetch it.</param>
public sealed record DashboardWidgetInfo(string Key, string Title, string Description, string DefaultSize, IReadOnlyList<string> Modules, IReadOnlyList<string> Areas);

public sealed record DashboardLayoutResponse(
    string Role,
    bool IsCustomized,
    IReadOnlyList<DashboardLayoutItem> Items,
    IReadOnlyList<DashboardWidgetInfo> Available);

public sealed record DashboardRoleSettings(string Role, IReadOnlyList<string> DefaultKeys, IReadOnlyList<string> HiddenKeys);

public sealed record DashboardAdminSettingsResponse(IReadOnlyList<DashboardWidgetInfo> Widgets, IReadOnlyList<DashboardRoleSettings> Roles);

/// <summary>The modules the tenant's plan includes. A seam over the license snapshot so tests can set it directly.</summary>
public interface ITenantModuleResolver
{
    Task<IReadOnlyCollection<string>> GetModulesAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

internal sealed class LicenseTenantModuleResolver(ILicenseGuardService licenseGuardService) : ITenantModuleResolver
{
    public async Task<IReadOnlyCollection<string>> GetModulesAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        (await licenseGuardService.GetSnapshotAsync(tenantId, cancellationToken)).Modules;
}

public interface IRoleDashboardService
{
    Task<DashboardLayoutResponse> GetLayoutAsync(CancellationToken cancellationToken = default);
    Task<DashboardLayoutResponse> SaveLayoutAsync(IReadOnlyList<DashboardLayoutItem> items, CancellationToken cancellationToken = default);
    Task<DashboardLayoutResponse> ResetLayoutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// One widget's figures. Refuses (403) a widget whose module the tenant lacks, that the caller has no
    /// permission for, or that an admin has hidden for their role, whatever their saved layout says.
    /// </summary>
    Task<DashboardWidgetData> GetWidgetAsync(string key, Guid? branchId, bool refresh, CancellationToken cancellationToken = default);

    Task<DashboardAdminSettingsResponse> GetAdminSettingsAsync(CancellationToken cancellationToken = default);
    Task<DashboardAdminSettingsResponse> SetHiddenAsync(string role, IReadOnlyCollection<string> hiddenKeys, CancellationToken cancellationToken = default);
}

internal sealed class RoleDashboardService(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IBranchAccessService branchAccessService,
    ITenantModuleResolver moduleResolver,
    IDashboardSettingsService settingsService,
    RoleDashboardQueries queries,
    IMemoryCache cache,
    IDashboardDataVersions dataVersions) : IRoleDashboardService
{
    /// <summary>
    /// A backstop: a save to a widget's data areas already makes it recompute (see <see cref="CacheKey"/>),
    /// but time-based figures ("overdue", "today") change without any write.
    /// </summary>
    public static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private sealed record Viewer(Guid TenantId, Guid UserId, string Role, IReadOnlyList<string> Defaults, IReadOnlySet<string> Allowed, IReadOnlyCollection<string> Modules);

    public async Task<DashboardLayoutResponse> GetLayoutAsync(CancellationToken cancellationToken = default)
    {
        var viewer = await ResolveViewerAsync(cancellationToken);
        var row = await LoadRowAsync(viewer, tracked: false, cancellationToken);
        var saved = row is null ? null : DashboardLayoutRules.Parse(row.LayoutJson) ?? new StoredDashboardLayout();
        return Response(viewer, row is not null, DashboardLayoutRules.Resolve(saved, viewer.Defaults, viewer.Allowed));
    }

    public async Task<DashboardLayoutResponse> SaveLayoutAsync(IReadOnlyList<DashboardLayoutItem> items, CancellationToken cancellationToken = default)
    {
        var viewer = await ResolveViewerAsync(cancellationToken);
        var row = await LoadRowAsync(viewer, tracked: true, cancellationToken);
        var previous = row is null ? null : DashboardLayoutRules.Parse(row.LayoutJson);
        var stored = DashboardLayoutRules.Sanitize(items ?? [], previous, viewer.Defaults, viewer.Allowed);
        if (row is null)
        {
            row = new UserDashboardLayout { TenantId = viewer.TenantId, UserId = viewer.UserId };
            dbContext.UserDashboardLayouts.Add(row);
        }
        row.LayoutJson = DashboardLayoutRules.Serialize(stored);
        row.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Response(viewer, true, DashboardLayoutRules.Resolve(stored, viewer.Defaults, viewer.Allowed));
    }

    public async Task<DashboardLayoutResponse> ResetLayoutAsync(CancellationToken cancellationToken = default)
    {
        var viewer = await ResolveViewerAsync(cancellationToken);
        var row = await LoadRowAsync(viewer, tracked: true, cancellationToken);
        if (row is not null)
        {
            dbContext.UserDashboardLayouts.Remove(row);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        return Response(viewer, false, DashboardLayoutRules.Resolve(null, viewer.Defaults, viewer.Allowed));
    }

    public async Task<DashboardWidgetData> GetWidgetAsync(string key, Guid? branchId, bool refresh, CancellationToken cancellationToken = default)
    {
        var widget = DashboardWidgetCatalog.Find(key) ?? throw new NotFoundException("Dashboard widget not found.");
        var viewer = await ResolveViewerAsync(cancellationToken);
        // Module first, so a tenant without the module gets the licence message rather than a permission one.
        ServiceOpsPlans.EnsureModules(viewer.Modules, widget.Modules);
        if (!viewer.Allowed.Contains(widget.Key))
            throw new ForbiddenException("You do not have access to this dashboard widget.");

        var scope = await branchAccessService.GetQueryScopeAsync(viewer.TenantId, branchId, cancellationToken);
        // Read the versions before the query runs: a save landing mid-query bumps them, so this result
        // is filed under the old key and the next read recomputes.
        var cacheKey = CacheKey(viewer, widget, scope, dataVersions);
        if (!refresh && cache.TryGetValue(cacheKey, out DashboardWidgetData? cached) && cached is not null)
            return cached;

        var data = await queries.RunAsync(widget, new DashboardQueryViewer(viewer.TenantId, viewer.UserId, tenantContext.IsAdmin, tenantContext.HasPermission, scope), cancellationToken);
        cache.Set(cacheKey, data, CacheDuration);
        return data;
    }

    public async Task<DashboardAdminSettingsResponse> GetAdminSettingsAsync(CancellationToken cancellationToken = default)
    {
        userAccessService.EnsureAdmin();
        var tenantId = tenantContext.GetRequiredTenantId();
        return AdminResponse(await moduleResolver.GetModulesAsync(tenantId, cancellationToken), await settingsService.GetAsync(tenantId, cancellationToken));
    }

    public async Task<DashboardAdminSettingsResponse> SetHiddenAsync(string role, IReadOnlyCollection<string> hiddenKeys, CancellationToken cancellationToken = default)
    {
        userAccessService.EnsureAdmin();
        var tenantId = tenantContext.GetRequiredTenantId();
        var settings = await settingsService.SetHiddenAsync(tenantId, role, hiddenKeys ?? [], cancellationToken);
        return AdminResponse(await moduleResolver.GetModulesAsync(tenantId, cancellationToken), settings);
    }

    private static DashboardAdminSettingsResponse AdminResponse(IReadOnlyCollection<string> modules, DashboardSettingsModel settings)
    {
        var widgets = DashboardWidgetCatalog.All.Where(x => HasModules(modules, x)).Select(Info).ToList();
        var roles = DashboardRoleDefaults.ConfigurableRoles
            .Select(role => new DashboardRoleSettings(role, DashboardRoleDefaults.For(role), settings.HiddenFor(role)))
            .ToList();
        return new DashboardAdminSettingsResponse(widgets, roles);
    }

    private async Task<Viewer> ResolveViewerAsync(CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var tenantId = tenantContext.GetRequiredTenantId();
        var userId = tenantContext.GetRequiredUserId();
        var role = DashboardRoleDefaults.Normalize(tenantContext.Role);
        var modules = await moduleResolver.GetModulesAsync(tenantId, cancellationToken);
        var settings = await settingsService.GetAsync(tenantId, cancellationToken);

        const bool isProjectManager = false;

        var hidden = settings.HiddenFor(role)
            .Concat(isProjectManager ? settings.HiddenFor(DashboardRoleDefaults.ProjectManager) : [])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allowed = DashboardWidgetCatalog.All
            .Where(x => HasModules(modules, x) && HasPermission(x) && !hidden.Contains(x.Key))
            .Select(x => x.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new Viewer(tenantId, userId, role, DashboardRoleDefaults.For(role, isProjectManager), allowed, modules);
    }

    private bool HasPermission(DashboardWidgetDefinition widget) =>
        tenantContext.IsAdmin || widget.AnyPermissions.Count == 0 || widget.AnyPermissions.Any(tenantContext.HasPermission);

    private static bool HasModules(IReadOnlyCollection<string> modules, DashboardWidgetDefinition widget) =>
        widget.Modules.All(m => modules.Contains(m, StringComparer.OrdinalIgnoreCase));

    private Task<UserDashboardLayout?> LoadRowAsync(Viewer viewer, bool tracked, CancellationToken cancellationToken)
    {
        var query = dbContext.UserDashboardLayouts.Where(x => x.TenantId == viewer.TenantId && x.UserId == viewer.UserId);
        return (tracked ? query : query.AsNoTracking()).SingleOrDefaultAsync(cancellationToken);
    }

    private static DashboardLayoutResponse Response(Viewer viewer, bool customized, IReadOnlyList<DashboardLayoutItem> items) =>
        new(viewer.Role, customized, items,
            DashboardWidgetCatalog.All.Where(x => viewer.Allowed.Contains(x.Key)).Select(Info).ToList());

    private static DashboardWidgetInfo Info(DashboardWidgetDefinition x) => new(x.Key, x.Title, x.Description, x.DefaultSize, x.Modules, x.Areas);

    /// <summary>
    /// Per tenant, widget and scope. Branch-scoped widgets key on the requested branch and the caller's
    /// branch access; per-user widgets also on the caller. The tenant's version of each of the widget's
    /// data areas is part of the key, so a save to that data retires the entry (it then just expires);
    /// Refresh re-runs the query and replaces it too.
    /// </summary>
    private static string CacheKey(Viewer viewer, DashboardWidgetDefinition widget, BranchQueryScope scope, IDashboardDataVersions versions)
    {
        var branch = $"{scope.HasConfiguredBranches}:{scope.RequestedBranchId}:{scope.IncludeUnassignedRecords}:{string.Join(',', scope.AccessibleBranchIds.OrderBy(x => x))}";
        var scopeKey = widget.Scope switch
        {
            DashboardWidgetScope.Tenant => "tenant",
            DashboardWidgetScope.Branch => branch,
            _ => $"{branch}:user:{viewer.UserId}",
        };
        var data = string.Join('.', widget.Areas.Select(area => versions.Get(viewer.TenantId, area)));
        return $"role-dashboard:{viewer.TenantId}:{widget.Key}:{scopeKey}:v{data}";
    }
}
