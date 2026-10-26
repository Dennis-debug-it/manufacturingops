using System.Text.Json;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

/// <summary>
/// The tenant's inventory rules, stored as JSON under the "inventory" module key by
/// <c>PUT /api/settings/module/inventory</c> (Settings → Inventory). Missing keys fall back to the same
/// defaults the settings page shows, so a tenant that never saved the page behaves as the page says.
/// </summary>
/// <param name="CriticalStockPercent">A part at or below this percentage of its reorder level is Critical (any part at or below its reorder level is Low).</param>
/// <param name="AutoDeductOnWorkOrderComplete">Completing a work order books its still-open issued material requests as fully used.</param>
/// <param name="RequireApprovalAboveValue">Material issues worth more than this (in the stock currency) wait for approval; 0 turns the rule off.</param>
/// <param name="AllowNegativeStock">Issues, job usage and adjustments may take a balance below zero. Transfers and van loads never can.</param>
/// <param name="DefaultReorderLeadDays">Days a supplier takes to deliver when the supplier has no lead time of its own.</param>
public sealed record InventoryModuleSettings(
    decimal CriticalStockPercent,
    bool AutoDeductOnWorkOrderComplete,
    decimal RequireApprovalAboveValue,
    bool AllowNegativeStock,
    int DefaultReorderLeadDays)
{
    public const string ModuleKey = "inventory";

    public static readonly InventoryModuleSettings Default = new(20, true, 50000, false, 7);

    public static async Task<InventoryModuleSettings> LoadAsync(AppDbContext dbContext, Guid tenantId, CancellationToken cancellationToken)
    {
        var json = await dbContext.TenantModuleSettings.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.ModuleKey == ModuleKey)
            .Select(x => x.ValueJson)
            .SingleOrDefaultAsync(cancellationToken);

        return Parse(json);
    }

    public static InventoryModuleSettings Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Default;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Default;
            }

            return new InventoryModuleSettings(
                Math.Clamp(ReadDecimal(root, "lowStockThresholdPercent", Default.CriticalStockPercent), 0, 100),
                ReadBool(root, "autoDeductOnWorkOrderComplete", Default.AutoDeductOnWorkOrderComplete),
                Math.Max(0, ReadDecimal(root, "requireApprovalAboveValue", Default.RequireApprovalAboveValue)),
                ReadBool(root, "allowNegativeStock", Default.AllowNegativeStock),
                (int)Math.Clamp(ReadDecimal(root, "defaultReorderLeadDays", Default.DefaultReorderLeadDays), 0, 365));
        }
        catch (JsonException)
        {
            return Default;
        }
    }

    /// <summary>True when on-hand stock is at or below the critical share of a positive reorder level.</summary>
    public bool IsCritical(decimal quantityOnHand, decimal reorderLevel) =>
        reorderLevel > 0 && quantityOnHand <= reorderLevel * CriticalStockPercent / 100m;

    private static bool ReadBool(JsonElement root, string name, bool fallback)
    {
        if (!TryGetProperty(root, name, out var value))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => fallback,
        };
    }

    // The settings form posts numbers, but an older save or a hand edit may hold them as strings.
    private static decimal ReadDecimal(JsonElement root, string name, decimal fallback)
    {
        if (!TryGetProperty(root, name, out var value))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
            JsonValueKind.String when decimal.TryParse(value.GetString(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => fallback,
        };
    }

    private static bool TryGetProperty(JsonElement root, string name, out JsonElement value)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
