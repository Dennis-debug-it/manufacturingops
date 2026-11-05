using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public static class AssetTagStatuses
{
    public const string Active = "Active";
    /// <summary>Damaged or lost and replaced by a new tag; scanning it still finds the asset, with a warning.</summary>
    public const string Replaced = "Replaced";
    /// <summary>Removed, e.g. because the asset was disposed of.</summary>
    public const string Retired = "Retired";
}

public static class AssetTagSymbologies
{
    public const string Qr = "QR";
    public const string Barcode = "Barcode";
    public const string Nfc = "NFC";

    public static readonly IReadOnlyList<string> All = [Qr, Barcode, Nfc];

    public static string? Find(string? value) =>
        All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// A physical identification tag fixed to an asset. Tag numbers are never reused, so an old
/// label found on the floor can always be traced; an asset has at most one active tag.
/// </summary>
public sealed class AssetTag : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid AssetId { get; set; }
    public string TagNumber { get; set; } = string.Empty;
    public string Symbology { get; set; } = AssetTagSymbologies.Qr;
    public string Status { get; set; } = AssetTagStatuses.Active;
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RetiredAt { get; set; }
    public string? RetiredReason { get; set; }
    public Guid? ReplacedByTagId { get; set; }
    public int PrintCount { get; set; }
    public DateTime? LastPrintedAt { get; set; }

    public Tenant? Tenant { get; set; }
    public Asset? Asset { get; set; }
}
