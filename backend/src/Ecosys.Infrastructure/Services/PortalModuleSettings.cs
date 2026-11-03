using System.Text.Json;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

/// <summary>
/// The tenant's client-portal switches, stored as JSON under the "portal" module key by
/// <c>PUT /api/settings/module/portal</c>. Missing keys fall back to the same defaults the settings page shows,
/// so a tenant that never saved the page gets a fully enabled portal.
/// </summary>
public sealed record PortalModuleSettings(
    bool AllowRequestSubmission,
    bool AllowQuoteResponses,
    bool ShowInvoices,
    bool ShowContracts,
    bool ShowAssetRegister,
    bool RequireApprovalForNewContacts,
    string? WelcomeMessage)
{
    public const string ModuleKey = "portal";

    public static readonly PortalModuleSettings Default = new(true, true, true, true, true, false, null);

    public static async Task<PortalModuleSettings> LoadAsync(AppDbContext dbContext, Guid tenantId, CancellationToken cancellationToken)
    {
        var json = await dbContext.TenantModuleSettings.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.ModuleKey == ModuleKey)
            .Select(x => x.ValueJson)
            .SingleOrDefaultAsync(cancellationToken);

        return Parse(json);
    }

    public static PortalModuleSettings Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Default;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Default;
            }

            var root = document.RootElement;
            return new PortalModuleSettings(
                ReadBool(root, "allowRequestSubmission", Default.AllowRequestSubmission),
                ReadBool(root, "allowQuoteResponses", Default.AllowQuoteResponses),
                ReadBool(root, "showInvoices", Default.ShowInvoices),
                ReadBool(root, "showContracts", Default.ShowContracts),
                ReadBool(root, "showAssetRegister", Default.ShowAssetRegister),
                ReadBool(root, "requireApprovalForNewContacts", Default.RequireApprovalForNewContacts),
                ReadString(root, "welcomeMessage"));
        }
        catch (JsonException)
        {
            return Default;
        }
    }

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

    private static string? ReadString(JsonElement root, string name) =>
        TryGetProperty(root, name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;

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
