using System.ComponentModel.DataAnnotations;
using Ecosys.Api.Common;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/assets")]
public sealed class AssetsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IPreventiveMaintenancePlanService preventiveMaintenancePlanService,
    IAuditLogService auditLogService,
    IUserAccessService userAccessService,
    IBranchAccessService branchAccessService,
    IDocumentNumberingService documentNumberingService,
    ILicenseGuardService licenseGuardService) : TenantAwareControllerBase(tenantContext)
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Active",
        "Inactive"
    };

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? branchId,
        [FromQuery] Guid? clientId,
        [FromQuery] Guid? siteId,
        [FromQuery] Guid? systemId,
        [FromQuery] Guid? categoryId,
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, branchId, cancellationToken);
        var normalizedStatus = NormalizeStatusFilter(status);
        var normalizedSearch = NormalizeSearch(search);

        var query = dbContext.Assets
            .Include(x => x.Client)
            .Include(x => x.Branch)
            .Include(x => x.Site)
            .Include(x => x.System)
            .Include(x => x.AssetCategory)
            .Include(x => x.CustomFieldValues)
                .ThenInclude(x => x.FieldDefinition)
            .Where(x => x.TenantId == TenantId)
            .WhereAccessible(scope, x => x.BranchId)
            .AsQueryable();

        if (clientId.HasValue)
        {
            query = query.Where(x => x.ClientId == clientId.Value);
        }

        if (siteId.HasValue)
        {
            query = query.Where(x => x.SiteId == siteId.Value);
        }

        if (systemId.HasValue)
        {
            query = query.Where(x => x.SystemId == systemId.Value);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(x => x.AssetCategoryId == categoryId.Value);
        }

        if (normalizedStatus == "active")
        {
            query = query.Where(x => x.Status != "Inactive");
        }
        else if (normalizedStatus == "inactive")
        {
            query = query.Where(x => x.Status == "Inactive");
        }

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var searchLower = normalizedSearch.ToLower();
            query = query.Where(x =>
                x.AssetName.ToLower().Contains(searchLower)
                || x.AssetCode.ToLower().Contains(searchLower)
                || (x.SerialNumber != null && x.SerialNumber.ToLower().Contains(searchLower))
                || (x.Manufacturer != null && x.Manufacturer.ToLower().Contains(searchLower))
                || (x.Model != null && x.Model.ToLower().Contains(searchLower))
                || (x.Location != null && x.Location.ToLower().Contains(searchLower))
                || (x.Client != null && x.Client.ClientName.ToLower().Contains(searchLower)));
        }

        var ordered = query.OrderBy(x => x.AssetName);

        if (QueryablePagingExtensions.IsPagingRequested(page, pageSize))
        {
            var paged = await ordered.ToPagedResultAsync(page, pageSize, cancellationToken);
            return Ok(paged.Map(Map));
        }

        var assets = await ordered.ToListAsync(cancellationToken);
        return Ok(assets.Select(Map).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AssetResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);

        var asset = await dbContext.Assets
            .Include(x => x.Client)
            .Include(x => x.Branch)
            .Include(x => x.Site)
            .Include(x => x.System)
            .Include(x => x.AssetCategory)
            .Include(x => x.CustomFieldValues)
                .ThenInclude(x => x.FieldDefinition)
            .Where(x => x.TenantId == TenantId && x.Status != "Inactive" && x.Id == id)
            .WhereAccessible(scope, x => x.BranchId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Asset was not found.");

        return Ok(Map(asset));
    }

    [HttpPost]
    public async Task<ActionResult<AssetResponse>> Create([FromBody] UpsertAssetRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageAssets);
        await licenseGuardService.EnsureTenantCanMutateAsync(TenantId, cancellationToken);
        Validate(request);
        await EnsureClientExistsAsync(request.ClientId, cancellationToken);
        await EnsureSiteIsValidAsync(request.ClientId, request.SiteId, cancellationToken);
        await EnsureSystemIsValidAsync(request.ClientId, request.SiteId, request.SystemId, cancellationToken);
        await EnsureCategoryRequestIsValidAsync(request.AssetCategoryId, request.CustomFieldValues, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.AssetCode))
        {
            await EnsureUniqueAssetCodeAsync(request.AssetCode, null, cancellationToken);
        }

        var branchId = await branchAccessService.ResolveBranchIdForWriteAsync(TenantId, request.BranchId, cancellationToken);
        var (frequencyValue, frequencyUnit) = ResolvePmFrequency(request);

        var asset = new Asset
        {
            TenantId = TenantId,
            BranchId = branchId,
            ClientId = request.ClientId,
            SiteId = request.SiteId,
            SystemId = request.SystemId,
            AssetCategoryId = request.AssetCategoryId,
            AssetName = request.AssetName.Trim(),
            AssetCode = string.IsNullOrWhiteSpace(request.AssetCode)
                ? await GenerateUniqueAssetCodeAsync(branchId, cancellationToken)
                : request.AssetCode.Trim(),
            AssetType = request.AssetType?.Trim(),
            OwnershipType = NormalizeOwnershipType(request.OwnershipType),
            Location = request.Location?.Trim(),
            SerialNumber = request.SerialNumber?.Trim(),
            Manufacturer = request.Manufacturer?.Trim(),
            Model = request.Model?.Trim(),
            InstallationDate = request.InstallationDate,
            WarrantyStartDate = ResolveWarrantyStartDate(request),
            WarrantyPeriodMonths = request.WarrantyPeriodMonths,
            WarrantyExpiryDate = ResolveWarrantyExpiryDate(request),
            RecommendedPmFrequency = BuildPmFrequencyLabel(request.RecommendedPmFrequency, frequencyValue, frequencyUnit),
            PmFrequencyValue = frequencyValue,
            PmFrequencyUnit = NormalizePmFrequencyUnit(frequencyUnit),
            AutoSchedulePm = request.AutoSchedulePm,
            LastPmDate = request.LastPmDate,
            NextPmDate = request.NextPmDate ?? CalculateNextPmDate(request.LastPmDate, request.InstallationDate, frequencyValue, frequencyUnit),
            Notes = request.Notes?.Trim(),
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Active" : request.Status.Trim()
        };

        dbContext.Assets.Add(asset);
        await dbContext.SaveChangesAsync(cancellationToken);
        await SaveCustomFieldValuesAsync(asset, request.CustomFieldValues, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await preventiveMaintenancePlanService.SyncForAssetAsync(asset, cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Asset created",
            nameof(Asset),
            asset.Id.ToString(),
            $"Created asset '{asset.AssetName}'.",
            cancellationToken);

        await dbContext.Entry(asset).Reference(x => x.Client).LoadAsync(cancellationToken);
        if (asset.BranchId.HasValue)
        {
            await dbContext.Entry(asset).Reference(x => x.Branch).LoadAsync(cancellationToken);
        }
        if (asset.SiteId.HasValue)
        {
            await dbContext.Entry(asset).Reference(x => x.Site).LoadAsync(cancellationToken);
        }
        if (asset.SystemId.HasValue)
        {
            await dbContext.Entry(asset).Reference(x => x.System).LoadAsync(cancellationToken);
        }
        if (asset.AssetCategoryId.HasValue)
        {
            await dbContext.Entry(asset).Reference(x => x.AssetCategory).LoadAsync(cancellationToken);
        }
        await dbContext.Entry(asset).Collection(x => x.CustomFieldValues).Query().Include(x => x.FieldDefinition).LoadAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = asset.Id }, Map(asset));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AssetResponse>> Update(Guid id, [FromBody] UpsertAssetRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageAssets);
        Validate(request);
        var asset = await dbContext.Assets
            .Include(x => x.Client)
            .Include(x => x.Branch)
            .Include(x => x.Site)
            .Include(x => x.System)
            .Include(x => x.AssetCategory)
            .Include(x => x.CustomFieldValues)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Asset was not found.");

        await EnsureClientExistsAsync(request.ClientId, cancellationToken);
        await EnsureSiteIsValidAsync(request.ClientId, request.SiteId, cancellationToken);
        await EnsureSystemIsValidAsync(request.ClientId, request.SiteId, request.SystemId, cancellationToken);
        await EnsureCategoryRequestIsValidAsync(request.AssetCategoryId, request.CustomFieldValues, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.AssetCode))
        {
            await EnsureUniqueAssetCodeAsync(request.AssetCode, asset.Id, cancellationToken);
        }

        var branchId = await branchAccessService.ResolveBranchIdForWriteAsync(TenantId, request.BranchId ?? asset.BranchId, cancellationToken);

        asset.BranchId = branchId;
        asset.ClientId = request.ClientId;
        asset.SiteId = request.SiteId;
        asset.SystemId = request.SystemId;
        asset.AssetCategoryId = request.AssetCategoryId;
        asset.AssetName = request.AssetName.Trim();
        asset.AssetCode = string.IsNullOrWhiteSpace(request.AssetCode) ? asset.AssetCode : request.AssetCode.Trim();
        asset.AssetType = request.AssetType?.Trim();
        asset.OwnershipType = NormalizeOwnershipType(request.OwnershipType);
        asset.Location = request.Location?.Trim();
        asset.SerialNumber = request.SerialNumber?.Trim();
        asset.Manufacturer = request.Manufacturer?.Trim();
        asset.Model = request.Model?.Trim();
        asset.InstallationDate = request.InstallationDate;
        asset.WarrantyStartDate = ResolveWarrantyStartDate(request);
        asset.WarrantyPeriodMonths = request.WarrantyPeriodMonths;
        asset.WarrantyExpiryDate = ResolveWarrantyExpiryDate(request);
        var (frequencyValue, frequencyUnit) = ResolvePmFrequency(request);
        asset.RecommendedPmFrequency = BuildPmFrequencyLabel(request.RecommendedPmFrequency, frequencyValue, frequencyUnit);
        asset.PmFrequencyValue = frequencyValue;
        asset.PmFrequencyUnit = NormalizePmFrequencyUnit(frequencyUnit);
        asset.AutoSchedulePm = request.AutoSchedulePm;
        asset.LastPmDate = request.LastPmDate;
        asset.NextPmDate = request.NextPmDate ?? CalculateNextPmDate(request.LastPmDate, request.InstallationDate, frequencyValue, frequencyUnit);
        asset.Notes = request.Notes?.Trim();
        asset.Status = string.IsNullOrWhiteSpace(request.Status) ? asset.Status : request.Status.Trim();

        await SaveCustomFieldValuesAsync(asset, request.CustomFieldValues, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await preventiveMaintenancePlanService.SyncForAssetAsync(asset, cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Asset updated",
            nameof(Asset),
            asset.Id.ToString(),
            $"Updated asset '{asset.AssetName}'.",
            cancellationToken);

        await dbContext.Entry(asset).Reference(x => x.Client).LoadAsync(cancellationToken);
        if (asset.BranchId.HasValue)
        {
            await dbContext.Entry(asset).Reference(x => x.Branch).LoadAsync(cancellationToken);
        }
        if (asset.SiteId.HasValue)
        {
            await dbContext.Entry(asset).Reference(x => x.Site).LoadAsync(cancellationToken);
        }
        if (asset.SystemId.HasValue)
        {
            await dbContext.Entry(asset).Reference(x => x.System).LoadAsync(cancellationToken);
        }
        if (asset.AssetCategoryId.HasValue)
        {
            await dbContext.Entry(asset).Reference(x => x.AssetCategory).LoadAsync(cancellationToken);
        }
        await dbContext.Entry(asset).Collection(x => x.CustomFieldValues).Query().Include(x => x.FieldDefinition).LoadAsync(cancellationToken);

        return Ok(Map(asset));
    }

    private async Task EnsureSiteIsValidAsync(Guid clientId, Guid? siteId, CancellationToken cancellationToken)
    {
        if (!siteId.HasValue)
        {
            return;
        }

        var site = await dbContext.Sites
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == siteId.Value, cancellationToken)
            ?? throw new BusinessRuleException("Site does not exist for this tenant.");

        if (site.ClientId != clientId)
        {
            throw new BusinessRuleException("Site does not belong to the selected client.");
        }

        if (!string.Equals(site.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("Select an active site for this asset.");
        }
    }

    private async Task EnsureSystemIsValidAsync(Guid clientId, Guid? siteId, Guid? systemId, CancellationToken cancellationToken)
    {
        if (!systemId.HasValue)
        {
            return;
        }

        var system = await dbContext.Systems.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == systemId.Value, cancellationToken)
            ?? throw new BusinessRuleException("System does not exist for this tenant.");

        if (system.ClientId != clientId)
        {
            throw new BusinessRuleException("System does not belong to the selected client.");
        }

        if (siteId.HasValue && system.SiteId != siteId.Value)
        {
            throw new BusinessRuleException("System does not belong to the selected site.");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageAssets);

        var asset = await dbContext.Assets.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Asset was not found.");

        asset.Status = "Inactive";
        asset.AutoSchedulePm = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        await preventiveMaintenancePlanService.SyncForAssetAsync(asset, cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Asset deleted",
            nameof(Asset),
            asset.Id.ToString(),
            $"Soft deleted asset '{asset.AssetName}'.",
            cancellationToken);

        return NoContent();
    }

    private static void Validate(UpsertAssetRequest request)
    {
        if (request.ClientId == Guid.Empty)
        {
            throw new BusinessRuleException("Client is required.");
        }

        if (string.IsNullOrWhiteSpace(request.AssetName))
        {
            throw new BusinessRuleException("Asset name is required.");
        }

        if (!string.IsNullOrWhiteSpace(request.Status) && !AllowedStatuses.Contains(request.Status.Trim()))
        {
            throw new BusinessRuleException("Invalid asset status.");
        }

        if (request.PmFrequencyValue.HasValue && request.PmFrequencyValue.Value <= 0)
        {
            throw new BusinessRuleException("PM frequency value must be greater than zero.");
        }

        if (request.LastPmDate.HasValue && request.LastPmDate.Value.Date > DateTime.UtcNow.Date.AddDays(1))
        {
            throw new BusinessRuleException("Last maintenance date cannot be in the future.");
        }

        if (request.WarrantyPeriodMonths is <= 0 or > MaxWarrantyPeriodMonths)
        {
            throw new BusinessRuleException($"Warranty period must be between 1 and {MaxWarrantyPeriodMonths} months.");
        }

        var warrantyStart = ResolveWarrantyStartDate(request);
        var warrantyExpiry = ResolveWarrantyExpiryDate(request);
        if (warrantyStart.HasValue && warrantyExpiry.HasValue && warrantyExpiry.Value.Date < warrantyStart.Value.Date)
        {
            throw new BusinessRuleException("Warranty expiry date cannot be before the warranty start date.");
        }
    }

    private const int MaxWarrantyPeriodMonths = 600;

    /// <summary>Warranty cover normally starts at installation unless a separate start date is given.</summary>
    private static DateTime? ResolveWarrantyStartDate(UpsertAssetRequest request) =>
        request.WarrantyStartDate
        ?? (request.WarrantyPeriodMonths.HasValue || request.WarrantyExpiryDate.HasValue ? request.InstallationDate : null);

    /// <summary>
    /// An explicit expiry date wins; otherwise the expiry is derived from the warranty start plus the
    /// warranty period, so entering "24 months" is enough to track the cover.
    /// </summary>
    private static DateTime? ResolveWarrantyExpiryDate(UpsertAssetRequest request)
    {
        if (request.WarrantyExpiryDate.HasValue)
        {
            return request.WarrantyExpiryDate;
        }

        var start = ResolveWarrantyStartDate(request);
        return start.HasValue && request.WarrantyPeriodMonths is > 0
            ? start.Value.AddMonths(request.WarrantyPeriodMonths.Value)
            : null;
    }

    private static string ResolveWarrantyStatus(DateTime? warrantyExpiryDate)
    {
        if (!warrantyExpiryDate.HasValue)
        {
            return "No Warranty";
        }

        var today = DateTime.UtcNow.Date;
        var expiry = warrantyExpiryDate.Value.Date;
        if (expiry < today)
        {
            return "Warranty Expired";
        }

        return expiry <= today.AddDays(30) ? "Warranty Expiring Soon" : "Under Warranty";
    }

    private static string NormalizeOwnershipType(string? ownershipType) =>
        string.IsNullOrWhiteSpace(ownershipType) ? "ClientOwned" : ownershipType.Trim();

    private static string? NormalizePmFrequencyUnit(string? unit)
    {
        if (string.IsNullOrWhiteSpace(unit))
        {
            return null;
        }

        return unit.Trim().ToLowerInvariant() switch
        {
            "day" or "days" => "days",
            "week" or "weeks" => "weeks",
            "month" or "months" => "months",
            "quarter" or "quarters" => "quarters",
            "year" or "years" => "years",
            _ => throw new BusinessRuleException("PM frequency unit must be days, weeks, months, quarters, or years.")
        };
    }

    private static string? BuildPmFrequencyLabel(string? currentLabel, int? value, string? unit)
    {
        if (value.HasValue && !string.IsNullOrWhiteSpace(unit))
        {
            return $"{value.Value} {NormalizePmFrequencyUnit(unit)}";
        }

        return string.IsNullOrWhiteSpace(currentLabel) ? null : currentLabel.Trim();
    }

    /// <summary>
    /// The next PM is one interval after the last one. With no PM on record the schedule is anchored
    /// to the installation date and rolled forward to the first due date from today, so an asset
    /// installed years ago is not reported as overdue for a service nobody has missed.
    /// </summary>
    private static DateTime? CalculateNextPmDate(DateTime? lastPmDate, DateTime? installationDate, int? frequencyValue, string? frequencyUnit)
    {
        if (!frequencyValue.HasValue || string.IsNullOrWhiteSpace(frequencyUnit))
        {
            return null;
        }

        if (frequencyValue.Value <= 0)
        {
            throw new BusinessRuleException("PM frequency value must be greater than zero.");
        }

        var unit = NormalizePmFrequencyUnit(frequencyUnit);
        if (lastPmDate.HasValue)
        {
            return AddPmInterval(lastPmDate.Value, frequencyValue.Value, unit);
        }

        if (!installationDate.HasValue)
        {
            return null;
        }

        var today = DateTime.UtcNow.Date;
        var anchor = installationDate.Value;
        DateTime? next = null;
        // Step from the anchor (rather than adding repeatedly) so month-end dates do not drift.
        for (var periods = 1; periods <= 100_000; periods++)
        {
            next = AddPmInterval(anchor, frequencyValue.Value * periods, unit);
            if (next is null || next.Value.Date >= today)
            {
                break;
            }
        }

        return next;
    }

    private static DateTime? AddPmInterval(DateTime from, int value, string? unit) => unit switch
    {
        "days" => from.AddDays(value),
        "weeks" => from.AddDays(value * 7),
        "months" => from.AddMonths(value),
        "quarters" => from.AddMonths(value * 3),
        "years" => from.AddYears(value),
        _ => null
    };

    /// <summary>
    /// Structured value + unit win; otherwise a free-text frequency such as "Monthly" or
    /// "Every 2 weeks" (older records, other clients) is converted so it drives the schedule.
    /// </summary>
    private static (int? Value, string? Unit) ResolvePmFrequency(UpsertAssetRequest request)
    {
        if (request.PmFrequencyValue.HasValue || !string.IsNullOrWhiteSpace(request.PmFrequencyUnit))
        {
            return (request.PmFrequencyValue, request.PmFrequencyUnit);
        }

        return PmFrequencyLabelParser.TryParse(request.RecommendedPmFrequency, out var value, out var unit)
            ? (value, unit)
            : (null, null);
    }

    private async Task EnsureClientExistsAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var clientExists = await dbContext.Clients.AnyAsync(x => x.TenantId == TenantId && x.IsActive && x.Id == clientId, cancellationToken);
        if (!clientExists)
        {
            throw new BusinessRuleException("Client does not exist for this tenant.");
        }
    }

    /// <summary>
    /// The numbering counter knows nothing about codes typed by hand or brought in by an import, so
    /// its next value can already be taken — and the (TenantId, AssetCode) unique index then fails the
    /// insert. Skip past any taken values; each call advances the counter, so this converges.
    /// </summary>
    private async Task<string> GenerateUniqueAssetCodeAsync(Guid? branchId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var candidate = await documentNumberingService.GenerateAsync(TenantId, branchId, DocumentTypes.Asset, cancellationToken);
            var normalized = candidate.ToLowerInvariant();
            if (!await dbContext.Assets.AnyAsync(x => x.TenantId == TenantId && x.AssetCode.ToLower() == normalized, cancellationToken))
            {
                return candidate;
            }
        }

        throw new BusinessRuleException("Could not generate a free asset code. Enter an asset code manually or adjust the Asset numbering rule.");
    }

    private async Task EnsureUniqueAssetCodeAsync(string assetCode, Guid? currentId, CancellationToken cancellationToken)
    {
        var normalizedCode = assetCode.Trim().ToLowerInvariant();
        var exists = await dbContext.Assets.AnyAsync(
            x => x.TenantId == TenantId
                && x.Id != currentId
                && x.AssetCode.ToLower() == normalizedCode,
            cancellationToken);

        if (exists)
        {
            throw new BusinessRuleException("Asset code must be unique per tenant.");
        }
    }

    private async Task EnsureCategoryRequestIsValidAsync(
        Guid? assetCategoryId,
        IReadOnlyCollection<UpsertAssetCustomFieldValueRequest>? customFieldValues,
        CancellationToken cancellationToken)
    {
        if (!assetCategoryId.HasValue)
        {
            if (customFieldValues is { Count: > 0 })
            {
                throw new BusinessRuleException("Asset category is required when custom field values are provided.");
            }

            return;
        }

        var category = await dbContext.AssetCategories
            .Include(x => x.Fields)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == assetCategoryId.Value && x.IsActive, cancellationToken)
            ?? throw new BusinessRuleException("Asset category does not exist for this tenant.");

        var requestedValues = customFieldValues ?? [];
        var fieldDefinitions = category.Fields.ToDictionary(x => x.Id, x => x);

        foreach (var item in requestedValues)
        {
            if (!fieldDefinitions.TryGetValue(item.FieldDefinitionId, out var definition))
            {
                throw new BusinessRuleException("One or more custom fields do not belong to the selected asset category.");
            }

            var normalizedValue = item.Value?.Trim() ?? string.Empty;
            if (definition.IsRequired && string.IsNullOrWhiteSpace(normalizedValue))
            {
                throw new BusinessRuleException($"Custom field '{definition.FieldLabel}' is required.");
            }
        }

        foreach (var requiredDefinition in fieldDefinitions.Values.Where(x => x.IsRequired))
        {
            var provided = requestedValues.Any(x => x.FieldDefinitionId == requiredDefinition.Id && !string.IsNullOrWhiteSpace(x.Value));
            if (!provided)
            {
                throw new BusinessRuleException($"Custom field '{requiredDefinition.FieldLabel}' is required.");
            }
        }
    }

    private async Task SaveCustomFieldValuesAsync(
        Asset asset,
        IReadOnlyCollection<UpsertAssetCustomFieldValueRequest>? customFieldValues,
        CancellationToken cancellationToken)
    {
        var existingValues = await dbContext.AssetCustomFieldValues
            .Where(x => x.TenantId == TenantId && x.AssetId == asset.Id)
            .ToListAsync(cancellationToken);

        dbContext.AssetCustomFieldValues.RemoveRange(existingValues);

        foreach (var item in customFieldValues ?? [])
        {
            var normalizedValue = item.Value?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedValue))
            {
                continue;
            }

            dbContext.AssetCustomFieldValues.Add(new AssetCustomFieldValue
            {
                TenantId = TenantId,
                AssetId = asset.Id,
                FieldDefinitionId = item.FieldDefinitionId,
                Value = normalizedValue
            });
        }
    }

    private static AssetResponse Map(Asset asset) =>
        new(
            asset.Id,
            asset.BranchId,
            asset.Branch?.Name,
            asset.ClientId,
            asset.Client?.ClientName,
            asset.SiteId,
            asset.Site?.SiteName,
            asset.SystemId,
            asset.System?.SystemName,
            asset.System?.SystemType,
            asset.AssetCategoryId,
            asset.AssetCategory?.Name,
            asset.AssetName,
            asset.AssetCode,
            asset.AssetType,
            asset.OwnershipType,
            asset.Location,
            asset.SerialNumber,
            asset.Manufacturer,
            asset.Model,
            asset.InstallationDate,
            asset.WarrantyExpiryDate,
            asset.WarrantyStartDate,
            asset.WarrantyPeriodMonths,
            asset.WarrantyExpiryDate.HasValue && asset.WarrantyExpiryDate.Value.Date >= DateTime.UtcNow.Date,
            ResolveWarrantyStatus(asset.WarrantyExpiryDate),
            asset.RecommendedPmFrequency,
            asset.PmFrequencyValue,
            asset.PmFrequencyUnit,
            asset.AutoSchedulePm,
            asset.LastPmDate,
            asset.NextPmDate,
            asset.Notes,
            asset.Status,
            asset.CreatedAt,
            asset.CustomFieldValues
                .OrderBy(x => x.FieldDefinition?.DisplayOrder ?? int.MaxValue)
                .ThenBy(x => x.FieldDefinition?.FieldLabel)
                .Select(x => new AssetCustomFieldValueResponse(
                    x.FieldDefinitionId,
                    x.FieldDefinition?.FieldName ?? string.Empty,
                    x.FieldDefinition?.FieldLabel ?? string.Empty,
                    x.FieldDefinition?.FieldType ?? "Text",
                    x.FieldDefinition?.Unit,
                    x.Value))
                .ToList());

    private static string NormalizeStatusFilter(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "active";
        }

        return status.Trim().ToLowerInvariant() switch
        {
            "active" => "active",
            "inactive" => "inactive",
            "all" => "all",
            _ => throw new BusinessRuleException("Status must be active, inactive, or all.")
        };
    }

    private static string? NormalizeSearch(string? search) =>
        string.IsNullOrWhiteSpace(search) ? null : search.Trim();
}

public sealed record UpsertAssetRequest(
    Guid ClientId,
    Guid? BranchId,
    Guid? SiteId,
    Guid? SystemId,
    Guid? AssetCategoryId,
    [StringLength(200)] string AssetName,
    [StringLength(100)] string? AssetCode,
    [StringLength(100)] string? AssetType,
    [StringLength(50)] string? OwnershipType,
    [StringLength(200)] string? Location,
    [StringLength(100)] string? SerialNumber,
    [StringLength(150)] string? Manufacturer,
    [StringLength(150)] string? Model,
    DateTime? InstallationDate,
    DateTime? WarrantyExpiryDate,
    [StringLength(50)] string? RecommendedPmFrequency,
    int? PmFrequencyValue,
    [StringLength(30)] string? PmFrequencyUnit,
    bool AutoSchedulePm,
    DateTime? LastPmDate,
    DateTime? NextPmDate,
    [StringLength(2000)] string? Notes,
    [StringLength(50)] string? Status,
    IReadOnlyCollection<UpsertAssetCustomFieldValueRequest>? CustomFieldValues,
    DateTime? WarrantyStartDate = null,
    int? WarrantyPeriodMonths = null);

public sealed record AssetResponse(
    Guid Id,
    Guid? BranchId,
    string? BranchName,
    Guid ClientId,
    string? ClientName,
    Guid? SiteId,
    string? SiteName,
    Guid? SystemId,
    string? SystemName,
    string? SystemType,
    Guid? AssetCategoryId,
    string? AssetCategoryName,
    string AssetName,
    string AssetCode,
    string? AssetType,
    string? OwnershipType,
    string? Location,
    string? SerialNumber,
    string? Manufacturer,
    string? Model,
    DateTime? InstallationDate,
    DateTime? WarrantyExpiryDate,
    DateTime? WarrantyStartDate,
    int? WarrantyPeriodMonths,
    bool IsUnderWarranty,
    string WarrantyStatus,
    string? RecommendedPmFrequency,
    int? PmFrequencyValue,
    string? PmFrequencyUnit,
    bool AutoSchedulePm,
    DateTime? LastPmDate,
    DateTime? NextPmDate,
    string? Notes,
    string Status,
    DateTime CreatedAt,
    IReadOnlyCollection<AssetCustomFieldValueResponse> CustomFieldValues);

public sealed record UpsertAssetCustomFieldValueRequest(
    Guid FieldDefinitionId,
    string? Value);

public sealed record AssetCustomFieldValueResponse(
    Guid FieldDefinitionId,
    string FieldName,
    string FieldLabel,
    string FieldType,
    string? Unit,
    string Value);
