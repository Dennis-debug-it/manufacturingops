using System.Globalization;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services.Factory;

public sealed record FactoryImportRow(int RowNumber, string? AssetCode, string? AssetName, IReadOnlyList<string> Errors);

public sealed record FactoryImportPreview(int TotalRows, int ValidRows, IReadOnlyList<FactoryImportRow> Rows);

public sealed record FactoryImportResult(int Created, int Tagged);

/// <summary>
/// Bulk onboarding of a factory's asset register from a spreadsheet (scope Phase 1). Location is
/// the org node code; class is matched by name or code; a parent can be an existing asset or a row
/// in the same file. Commit is all-or-nothing: nothing is saved while any row has an error.
/// </summary>
public interface IFactoryAssetImportService
{
    Task<FactoryImportPreview> PreviewAsync(Guid tenantId, Guid userId, IReadOnlyList<IReadOnlyDictionary<string, string?>> rows, CancellationToken cancellationToken = default);

    Task<FactoryImportResult> CommitAsync(Guid tenantId, Guid userId, string userName, IReadOnlyList<IReadOnlyDictionary<string, string?>> rows, CancellationToken cancellationToken = default);
}

public static class FactoryAssetImportColumns
{
    public const string AssetCode = "AssetCode";
    public const string AssetName = "AssetName";
    public const string Location = "Location";
    public const string AssetClass = "AssetClass";
    public const string ParentAssetCode = "ParentAssetCode";
    public const string Criticality = "Criticality";
    public const string Manufacturer = "Manufacturer";
    public const string Model = "Model";
    public const string SerialNumber = "SerialNumber";
    public const string YearOfManufacture = "YearOfManufacture";
    public const string InstallationDate = "InstallationDate";
    public const string CommissioningDate = "CommissioningDate";
    public const string WarrantyExpiryDate = "WarrantyExpiryDate";
    public const string LostProductionPerHour = "LostProductionPerHour";
    public const string OperationalStatus = "OperationalStatus";
    public const string TagNumber = "TagNumber";
    public const string ExternalId = "ExternalId";
    public const string ExternalSystem = "ExternalSystem";
    public const string Notes = "Notes";

    public static readonly IReadOnlyList<string> Required = [AssetCode, AssetName, Location];

    public static readonly IReadOnlyList<string> All =
    [
        AssetCode, AssetName, Location, AssetClass, ParentAssetCode, Criticality, Manufacturer, Model, SerialNumber,
        YearOfManufacture, InstallationDate, CommissioningDate, WarrantyExpiryDate, LostProductionPerHour,
        OperationalStatus, TagNumber, ExternalId, ExternalSystem, Notes
    ];
}

internal sealed class FactoryAssetImportService(
    AppDbContext dbContext,
    IOrgScopeService orgScopeService,
    IAssetLifecycleService assetLifecycleService,
    IAssetClassService assetClassService,
    IAssetTagNumberService tagNumberService) : IFactoryAssetImportService
{
    private sealed record Parsed(int RowNumber, IReadOnlyDictionary<string, string?> Raw, List<string> Errors)
    {
        public string? Code => Value(FactoryAssetImportColumns.AssetCode);
        public string? Value(string column) => Raw.TryGetValue(column, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
        public OrgNode? Location { get; set; }
        public AssetCategory? Class { get; set; }
        public Asset? ExistingParent { get; set; }
        public string? ParentCode { get; set; }
        public string? Criticality { get; set; }
        public string? OperationalStatus { get; set; }
        public string? TagNumber { get; set; }
        public int? Year { get; set; }
        public DateTime? Installed { get; set; }
        public DateTime? Commissioned { get; set; }
        public DateTime? WarrantyEnds { get; set; }
        public decimal? LostPerHour { get; set; }
    }

    public async Task<FactoryImportPreview> PreviewAsync(Guid tenantId, Guid userId, IReadOnlyList<IReadOnlyDictionary<string, string?>> rows, CancellationToken cancellationToken = default)
    {
        var parsed = await ValidateAsync(tenantId, userId, rows, cancellationToken);
        return new FactoryImportPreview(
            parsed.Count,
            parsed.Count(x => x.Errors.Count == 0),
            parsed.Select(x => new FactoryImportRow(x.RowNumber, x.Code, x.Value(FactoryAssetImportColumns.AssetName), x.Errors)).ToList());
    }

    public async Task<FactoryImportResult> CommitAsync(Guid tenantId, Guid userId, string userName, IReadOnlyList<IReadOnlyDictionary<string, string?>> rows, CancellationToken cancellationToken = default)
    {
        var parsed = await ValidateAsync(tenantId, userId, rows, cancellationToken);
        var invalid = parsed.Count(x => x.Errors.Count > 0);
        if (invalid > 0)
        {
            throw new BusinessRuleException($"{invalid} row(s) have errors. Fix them and import again; nothing was saved.");
        }

        if (parsed.Count == 0)
        {
            throw new BusinessRuleException("The file has no assets.");
        }

        // Parents before their components, so every parent exists when its child is created.
        var byCode = parsed.ToDictionary(x => x.Code!, StringComparer.OrdinalIgnoreCase);
        var created = new Dictionary<string, Asset>(StringComparer.OrdinalIgnoreCase);
        var tagged = 0;
        foreach (var row in parsed.OrderBy(x => Depth(x, byCode)))
        {
            var parent = row.ExistingParent ?? (row.ParentCode is null ? null : created[row.ParentCode]);
            var asset = new Asset
            {
                TenantId = tenantId,
                AssetCode = row.Code!,
                AssetName = row.Value(FactoryAssetImportColumns.AssetName)!,
                OrgNodeId = row.Location?.Id ?? parent?.OrgNodeId,
                ParentAssetId = parent?.Id,
                AssetCategoryId = row.Class?.Id,
                AssetCategory = row.Class,
                AssetType = row.Class?.Name,
                OwnershipType = "Owned",
                Criticality = row.Criticality,
                OperationalStatus = row.OperationalStatus ?? AssetOperationalStatuses.Operational,
                Manufacturer = row.Value(FactoryAssetImportColumns.Manufacturer),
                Model = row.Value(FactoryAssetImportColumns.Model),
                SerialNumber = row.Value(FactoryAssetImportColumns.SerialNumber),
                YearOfManufacture = row.Year,
                InstallationDate = row.Installed,
                CommissioningDate = row.Commissioned,
                WarrantyExpiryDate = row.WarrantyEnds,
                LostProductionPerHour = row.LostPerHour,
                ExternalId = row.Value(FactoryAssetImportColumns.ExternalId),
                ExternalSystem = row.Value(FactoryAssetImportColumns.ExternalSystem),
                Notes = row.Value(FactoryAssetImportColumns.Notes),
                Status = "Active"
            };
            dbContext.Assets.Add(asset);
            created[asset.AssetCode] = asset;
            await assetClassService.ApplyDefaultsAsync(asset, cancellationToken);

            var registered = assetLifecycleService.Record(asset, AssetLifecycleEventTypes.Registered, userId, userName, "Imported from spreadsheet.");
            registered.ToValue = row.Location?.Name;
            if (asset.CommissioningDate.HasValue)
            {
                assetLifecycleService.Record(asset, AssetLifecycleEventTypes.Commissioned, userId, userName, null, asset.CommissioningDate);
            }

            if (row.TagNumber is not null)
            {
                dbContext.AssetTags.Add(new AssetTag { TenantId = tenantId, AssetId = asset.Id, TagNumber = row.TagNumber });
                assetLifecycleService.Record(asset, AssetLifecycleEventTypes.Tagged, userId, userName).ToValue = row.TagNumber;
                tagged++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new FactoryImportResult(created.Count, tagged);
    }

    private async Task<List<Parsed>> ValidateAsync(Guid tenantId, Guid userId, IReadOnlyList<IReadOnlyDictionary<string, string?>> rows, CancellationToken cancellationToken)
    {
        var scope = await orgScopeService.GetScopeAsync(tenantId, userId, cancellationToken);
        var nodes = await dbContext.OrgNodes.Where(x => x.TenantId == tenantId).ToListAsync(cancellationToken);
        var nodesByCode = nodes.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
        var classes = await dbContext.AssetCategories.Where(x => x.TenantId == tenantId && x.IsActive).ToListAsync(cancellationToken);
        var existingAssets = (await dbContext.Assets.Where(x => x.TenantId == tenantId).ToListAsync(cancellationToken))
            .GroupBy(x => x.AssetCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var usedTags = (await dbContext.AssetTags.Where(x => x.TenantId == tenantId).Select(x => x.TagNumber).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var parsed = rows.Select((raw, index) => new Parsed(index + 2, raw, [])).ToList();
        var codesInFile = parsed.Where(x => x.Code is not null).GroupBy(x => x.Code!, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);
        var tagsInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in parsed)
        {
            var errors = row.Errors;
            // A component may leave Location blank: it takes its parent's.
            var hasParent = row.Value(FactoryAssetImportColumns.ParentAssetCode) is not null;
            foreach (var column in FactoryAssetImportColumns.Required.Where(c => row.Value(c) is null && !(hasParent && c == FactoryAssetImportColumns.Location)))
            {
                errors.Add($"{column} is required.");
            }

            if (row.Code is { } code)
            {
                if (code.Length > 100) errors.Add("AssetCode is longer than 100 characters.");
                if (existingAssets.ContainsKey(code)) errors.Add($"Asset {code} already exists.");
                if (codesInFile[code] > 1) errors.Add($"Asset {code} appears more than once in the file.");
            }

            if (row.Value(FactoryAssetImportColumns.AssetName) is { Length: > 200 }) errors.Add("AssetName is longer than 200 characters.");

            if (row.Value(FactoryAssetImportColumns.Location) is { } locationCode)
            {
                if (!nodesByCode.TryGetValue(locationCode, out var node)) errors.Add($"Location '{locationCode}' is not a factory, section or sub-section code.");
                else if (!node.IsActive) errors.Add($"Location '{locationCode}' is inactive.");
                else if (node.Level == OrgNodeLevels.Group) errors.Add("Assets sit in a factory, section or sub-section, not a group.");
                else if (!scope.Allows(node.Id)) errors.Add($"You don't have access to '{locationCode}'.");
                else row.Location = node;
            }

            if (row.Value(FactoryAssetImportColumns.AssetClass) is { } className)
            {
                row.Class = classes.FirstOrDefault(x => string.Equals(x.Name, className, StringComparison.OrdinalIgnoreCase))
                    ?? classes.FirstOrDefault(x => string.Equals(x.Code, className, StringComparison.OrdinalIgnoreCase));
                if (row.Class is null) errors.Add($"Asset class '{className}' was not found.");
            }

            if (row.Value(FactoryAssetImportColumns.ParentAssetCode) is { } parentCode)
            {
                if (string.Equals(parentCode, row.Code, StringComparison.OrdinalIgnoreCase)) errors.Add("An asset cannot be its own parent.");
                else if (existingAssets.TryGetValue(parentCode, out var parent))
                {
                    if (!scope.Allows(parent.OrgNodeId)) errors.Add($"Parent asset {parentCode} was not found.");
                    else row.ExistingParent = parent;
                }
                else if (codesInFile.ContainsKey(parentCode)) row.ParentCode = parentCode;
                else errors.Add($"Parent asset {parentCode} was not found in the register or the file.");
            }

            if (row.Value(FactoryAssetImportColumns.Criticality) is { } criticality)
            {
                try { row.Criticality = AssetCriticality.Normalize(criticality); }
                catch (ArgumentException) { errors.Add("Criticality must be A, B or C."); }
            }

            if (row.Value(FactoryAssetImportColumns.OperationalStatus) is { } status)
            {
                row.OperationalStatus = AssetOperationalStatuses.Find(status);
                if (row.OperationalStatus is null) errors.Add($"OperationalStatus must be one of: {string.Join(", ", AssetOperationalStatuses.All)}.");
            }

            row.Year = ParseInt(row, FactoryAssetImportColumns.YearOfManufacture, errors);
            if (row.Year is < 1900 || row.Year > DateTime.UtcNow.Year + 1) errors.Add("YearOfManufacture is not a valid year.");
            row.Installed = ParseDate(row, FactoryAssetImportColumns.InstallationDate, errors);
            row.Commissioned = ParseDate(row, FactoryAssetImportColumns.CommissioningDate, errors);
            row.WarrantyEnds = ParseDate(row, FactoryAssetImportColumns.WarrantyExpiryDate, errors);
            row.LostPerHour = ParseDecimal(row, FactoryAssetImportColumns.LostProductionPerHour, errors);
            if (row.LostPerHour < 0) errors.Add("LostProductionPerHour cannot be negative.");

            if (row.Value(FactoryAssetImportColumns.TagNumber) is { } tag)
            {
                var number = tagNumberService.NormalizeCode(tag);
                if (usedTags.Contains(number)) errors.Add($"Tag {number} has already been used.");
                else if (!tagsInFile.Add(number)) errors.Add($"Tag {number} appears more than once in the file.");
                else row.TagNumber = number;
            }
        }

        // A parent named in the file must itself be valid, and parents must not loop.
        var byCode = parsed.Where(x => x.Code is not null && codesInFile[x.Code] == 1).ToDictionary(x => x.Code!, StringComparer.OrdinalIgnoreCase);
        foreach (var row in parsed.Where(x => x.ParentCode is not null))
        {
            if (!byCode.TryGetValue(row.ParentCode!, out var parentRow) || parentRow.Errors.Count > 0)
            {
                row.Errors.Add($"Parent asset {row.ParentCode} has errors in this file.");
            }
            else if (Depth(row, byCode) < 0)
            {
                row.Errors.Add("Parent assets in this file form a loop.");
            }
        }

        return parsed;
    }

    /// <summary>How many in-file parents sit above the row; -1 when they loop.</summary>
    private static int Depth(Parsed row, IReadOnlyDictionary<string, Parsed> byCode)
    {
        var depth = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var current = row; current.ParentCode is not null; current = byCode[current.ParentCode])
        {
            if (!seen.Add(current.Code!) || !byCode.ContainsKey(current.ParentCode))
            {
                return -1;
            }

            depth++;
        }

        return depth;
    }

    private static int? ParseInt(Parsed row, string column, List<string> errors)
    {
        if (row.Value(column) is not { } value) return null;
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) && number == Math.Floor(number)) return (int)number;
        errors.Add($"{column} must be a whole number.");
        return null;
    }

    private static decimal? ParseDecimal(Parsed row, string column, List<string> errors)
    {
        if (row.Value(column) is not { } value) return null;
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)) return number;
        errors.Add($"{column} must be a number.");
        return null;
    }

    private static DateTime? ParseDate(Parsed row, string column, List<string> errors)
    {
        if (row.Value(column) is not { } value) return null;
        string[] formats = ["yyyy-MM-dd", "yyyy-MM-dd HH:mm", "d/M/yyyy", "dd/MM/yyyy", "d-MMM-yyyy", "d MMM yyyy"];
        if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date)) return date.Date;
        errors.Add($"{column} must be a date such as 2024-03-31.");
        return null;
    }
}
