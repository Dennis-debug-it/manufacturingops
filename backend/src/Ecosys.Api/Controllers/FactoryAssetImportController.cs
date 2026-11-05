using Ecosys.Api.Common;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>Bulk asset onboarding for factories from an Excel or CSV file: download the template, preview, then commit.</summary>
[ApiController]
[Authorize]
[Route("api/import/factory-assets")]
public sealed class FactoryAssetImportController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IOrgScopeService orgScopeService,
    IFactoryAssetImportService importService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    private const long MaxFileBytes = 10 * 1024 * 1024;

    /// <summary>
    /// An .xlsx template: the asset sheet with an example row, plus the factory's own location
    /// codes and asset classes on separate sheets to copy from.
    /// </summary>
    [HttpGet("template")]
    public async Task<IActionResult> Template(CancellationToken cancellationToken)
    {
        EnsureCanImport();
        var scope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        var nodes = (await dbContext.OrgNodes.Where(x => x.TenantId == TenantId && x.IsActive && x.Level != OrgNodeLevels.Group).ToListAsync(cancellationToken))
            .Where(x => scope.Allows(x.Id))
            .OrderBy(x => x.Code)
            .ToList();
        var classes = await dbContext.AssetCategories.Where(x => x.TenantId == TenantId && x.IsActive).OrderBy(x => x.ParentCategoryName).ThenBy(x => x.Name).ToListAsync(cancellationToken);
        var exampleLocation = nodes.FirstOrDefault(x => x.Level == OrgNodeLevels.Section)?.Code ?? nodes.FirstOrDefault()?.Code ?? "CHL-DRY";

        var headers = FactoryAssetImportColumns.All.Select(x => FactoryAssetImportColumns.Required.Contains(x) ? $"{x}*" : x).ToList();
        IReadOnlyList<string?> example = [
            "FBD-01", "Fluid bed dryer 1", exampleLocation, classes.FirstOrDefault(x => x.Name == "Fluid bed dryer")?.Name ?? classes.FirstOrDefault()?.Name, null, "A",
            "Manufacturer Ltd", "FBD-2000", "SN12345", "2018", "2018-06-01", "2018-07-15", "2027-07-15", "450", "Operational", null, null, null, "Main dryer"];
        IReadOnlyList<string?> component = [
            "FBD-01-FAN", "Dryer main fan", null, null, "FBD-01", "B", null, null, null, null, null, null, null, null, null, null, null, null, "Takes its parent's location"];

        var file = TabularFiles.WriteXlsx([
            new SheetSpec("Assets", headers, [example, component]),
            new SheetSpec("Locations", ["Code", "Name", "Level"], nodes.Select(x => (IReadOnlyList<string?>)[x.Code, x.Name, x.Level]).ToList()),
            new SheetSpec("Asset classes", ["Name", "Code", "Group", "Default criticality"], classes.Select(x => (IReadOnlyList<string?>)[x.Name, x.Code, x.ParentCategoryName, x.DefaultCriticality]).ToList()),
            new SheetSpec("Help", ["Column", "What to enter"], Help.Select(x => (IReadOnlyList<string?>)[x.Column, x.Text]).ToList()),
        ]);
        return File(file, TabularFiles.XlsxContentType, "factory-assets-import.xlsx");
    }

    [HttpPost("preview")]
    [RequestSizeLimit(MaxFileBytes)]
    public async Task<ActionResult<FactoryImportPreview>> Preview(IFormFile file, CancellationToken cancellationToken)
    {
        EnsureCanImport();
        return Ok(await importService.PreviewAsync(TenantId, UserId, Read(file), cancellationToken));
    }

    [HttpPost("commit")]
    [RequestSizeLimit(MaxFileBytes)]
    public async Task<ActionResult<FactoryImportResult>> Commit(IFormFile file, CancellationToken cancellationToken)
    {
        EnsureCanImport();
        var result = await importService.CommitAsync(TenantId, UserId, UserDisplayName, Read(file), cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "asset.imported", nameof(Asset), TenantId.ToString(),
            $"Imported {result.Created} asset(s) from '{file.FileName}' ({result.Tagged} with tags).", cancellationToken);
        return Ok(result);
    }

    private void EnsureCanImport() => userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageAssets);

    private static IReadOnlyList<IReadOnlyDictionary<string, string?>> Read(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            throw new BusinessRuleException("Choose a file to import.");
        }

        if (file.Length > MaxFileBytes)
        {
            throw new BusinessRuleException("The file is larger than 10 MB.");
        }

        using var stream = file.OpenReadStream();
        try
        {
            return TabularFiles.Read(stream, file.FileName);
        }
        catch (Exception ex) when (ex is not AppException)
        {
            throw new BusinessRuleException("The file could not be read. Save it as .xlsx or .csv and try again.");
        }
    }

    private static readonly (string Column, string Text)[] Help =
    [
        ("AssetCode*", "Your unique number for the asset, e.g. CHL-FBD-01."),
        ("AssetName*", "What people call it."),
        ("Location*", "A code from the Locations sheet (factory, section or sub-section). A component can leave it blank to take its parent's."),
        ("AssetClass", "A name or code from the Asset classes sheet. Sets the class's fields, standard meters and suggested criticality."),
        ("ParentAssetCode", "For a component: the AssetCode of the machine it belongs to, already registered or in this file."),
        ("Criticality", "A, B or C. Blank takes the class's suggestion."),
        ("YearOfManufacture", "e.g. 2018."),
        ("InstallationDate / CommissioningDate / WarrantyExpiryDate", "Dates as 2024-03-31."),
        ("LostProductionPerHour", "Production lost per hour of downtime, in your production unit (e.g. kg made tea)."),
        ("OperationalStatus", "Operational, Standby or Down. Blank means Operational."),
        ("TagNumber", "A pre-printed tag already fixed to the asset. Leave blank and issue tags later."),
        ("ExternalId / ExternalSystem", "The asset's number in your ERP, and which ERP."),
    ];
}
