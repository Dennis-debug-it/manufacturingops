using System.Text.Json;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

public sealed record DashboardLayoutItem(string Key, string Size);

/// <summary>The JSON kept in <see cref="UserDashboardLayout.LayoutJson"/>.</summary>
public sealed record StoredDashboardLayout
{
    public IReadOnlyList<DashboardLayoutItem> Items { get; init; } = [];
    /// <summary>Role-default widgets this user has already been offered; a default not in here is new to them.</summary>
    public IReadOnlyList<string> SeenDefaults { get; init; } = [];
}

/// <summary>Pure layout rules, shared by the API and the tests.</summary>
public static class DashboardLayoutRules
{
    public const int MaxItems = 40;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// What the user sees. No saved layout: the role default. A saved layout: its widgets in order,
    /// less any the user may no longer see, then any role default they have not been offered yet.
    /// </summary>
    public static IReadOnlyList<DashboardLayoutItem> Resolve(StoredDashboardLayout? saved, IReadOnlyList<string> defaults, IReadOnlySet<string> allowed)
    {
        if (saved is null)
            return [.. defaults.Where(allowed.Contains).Distinct(StringComparer.OrdinalIgnoreCase).Select(DefaultItem)];

        var items = Clean(saved.Items, allowed);
        var seen = saved.SeenDefaults.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var key in defaults.Where(allowed.Contains))
        {
            if (!seen.Contains(key) && items.All(x => !string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase)))
                items.Add(DefaultItem(key));
        }
        return items;
    }

    /// <summary>
    /// What a save stores: only widgets the user may see (anything else is dropped, not refused, so a
    /// change of access while the editor was open cannot block the save), each once, with a valid size.
    /// Every default the user can currently see counts as offered from now on.
    /// </summary>
    public static StoredDashboardLayout Sanitize(IEnumerable<DashboardLayoutItem> requested, StoredDashboardLayout? previous,
        IReadOnlyList<string> defaults, IReadOnlySet<string> allowed) => new()
    {
        Items = Clean(requested, allowed).Take(MaxItems).ToList(),
        SeenDefaults = [.. (previous?.SeenDefaults ?? []).Concat(defaults.Where(allowed.Contains)).Distinct(StringComparer.OrdinalIgnoreCase)],
    };

    public static StoredDashboardLayout? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<StoredDashboardLayout>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string Serialize(StoredDashboardLayout layout) => JsonSerializer.Serialize(layout, JsonOptions);

    private static List<DashboardLayoutItem> Clean(IEnumerable<DashboardLayoutItem> items, IReadOnlySet<string> allowed)
    {
        var result = new List<DashboardLayoutItem>();
        foreach (var item in items)
        {
            var widget = DashboardWidgetCatalog.Find(item?.Key);
            if (widget is null || !allowed.Contains(widget.Key) || result.Any(x => x.Key == widget.Key))
                continue;
            result.Add(new DashboardLayoutItem(widget.Key, DashboardWidgetSizes.IsSupported(item!.Size) ? item.Size : widget.DefaultSize));
        }
        return result;
    }

    private static DashboardLayoutItem DefaultItem(string key)
    {
        var widget = DashboardWidgetCatalog.Find(key)!;
        return new DashboardLayoutItem(widget.Key, widget.DefaultSize);
    }
}

/// <summary>Tenant dashboard settings, stored in the <c>dashboards</c> <see cref="TenantModuleSetting"/>.</summary>
public sealed record DashboardSettingsModel
{
    /// <summary>Widget keys an admin has hidden, per role (see <see cref="DashboardRoleDefaults.ConfigurableRoles"/>).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> HiddenByRole { get; init; } = new Dictionary<string, IReadOnlyList<string>>();

    public IReadOnlyList<string> HiddenFor(string role) =>
        HiddenByRole.FirstOrDefault(x => string.Equals(x.Key, role, StringComparison.OrdinalIgnoreCase)).Value ?? [];
}

public interface IDashboardSettingsService
{
    Task<DashboardSettingsModel> GetAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<DashboardSettingsModel> SetHiddenAsync(Guid tenantId, string role, IReadOnlyCollection<string> hiddenKeys, CancellationToken cancellationToken = default);
}

internal sealed class DashboardSettingsService(AppDbContext dbContext) : IDashboardSettingsService
{
    public const string ModuleKey = "dashboards";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<DashboardSettingsModel> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var json = await dbContext.TenantModuleSettings.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.ModuleKey == ModuleKey)
            .Select(x => x.ValueJson)
            .SingleOrDefaultAsync(cancellationToken);
        return Parse(json);
    }

    public async Task<DashboardSettingsModel> SetHiddenAsync(Guid tenantId, string role, IReadOnlyCollection<string> hiddenKeys, CancellationToken cancellationToken = default)
    {
        var canonicalRole = DashboardRoleDefaults.ConfigurableRoles.FirstOrDefault(x => string.Equals(x, role, StringComparison.OrdinalIgnoreCase))
            ?? throw new Shared.Errors.BusinessRuleException($"Unknown dashboard role '{role}'.");
        var keys = new List<string>();
        foreach (var key in hiddenKeys)
        {
            var widget = DashboardWidgetCatalog.Find(key) ?? throw new Shared.Errors.BusinessRuleException($"Unknown dashboard widget '{key}'.");
            if (!keys.Contains(widget.Key))
                keys.Add(widget.Key);
        }

        var row = await dbContext.TenantModuleSettings
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.ModuleKey == ModuleKey, cancellationToken);
        if (row is null)
        {
            row = new TenantModuleSetting { TenantId = tenantId, ModuleKey = ModuleKey };
            dbContext.TenantModuleSettings.Add(row);
        }

        var current = Parse(row.ValueJson);
        var hidden = current.HiddenByRole
            .Where(x => !string.Equals(x.Key, canonicalRole, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(x => x.Key, x => x.Value);
        if (keys.Count > 0)
            hidden[canonicalRole] = keys;
        var updated = current with { HiddenByRole = hidden };
        row.ValueJson = JsonSerializer.Serialize(updated, JsonOptions);
        await dbContext.SaveChangesAsync(cancellationToken);
        return updated;
    }

    private static DashboardSettingsModel Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new DashboardSettingsModel();
        try
        {
            return JsonSerializer.Deserialize<DashboardSettingsModel>(json, JsonOptions) ?? new DashboardSettingsModel();
        }
        catch (JsonException)
        {
            return new DashboardSettingsModel();
        }
    }
}
