using System.Text;
using Ecosys.Api.Common;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/import")]
public sealed class ImportController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IPreventiveMaintenancePlanService preventiveMaintenancePlanService,
    IAuditLogService auditLogService,
    IUserPermissionTemplateService permissionTemplateService,
    IPasswordHasher<User> passwordHasher,
    IUserCredentialDeliveryService credentialDeliveryService,
    ITemporaryPasswordService temporaryPasswordService,
    IDocumentNumberingService documentNumberingService,
    ILicenseGuardService licenseGuardService,
    IStockLedgerService stockLedgerService,
    IBranchAccessService branchAccessService) : TenantAwareControllerBase(tenantContext)
{
    private static readonly string[] ClientHeaders =
    [
        "ClientName",
        "ContactPerson",
        "Email",
        "Phone",
        "Country",
        "County",
        "City",
        "Address",
        "Status"
    ];

    private static readonly string[] AssetHeaders =
    [
        "ClientName",
        "ClientId",
        "SiteName",
        "SiteId",
        "SystemName",
        "SystemId",
        "SystemType",
        "AssetName",
        "AssetCode",
        "AssetType",
        "AssetCategory",
        "OwnershipType",
        "InstallationDate",
        "LastPmServiceDate",
        "PmFrequency",
        "PmFrequencyUnit",
        "NextPmDueDate",
        "AutoSchedulePm",
        "BranchName",
        "BranchId"
    ];

    private static readonly string[] UserHeaders =
    [
        "FullName",
        "Email",
        "Phone",
        "Role",
        "JobTitle",
        "BranchName",
        "IsTechnician",
        "Status"
    ];

    private static readonly string[] BranchHeaders =
    [
        "BranchName",
        "Code",
        "Country",
        "County",
        "City",
        "Address",
        "Status"
    ];

    private static readonly string[] MaterialHeaders =
    [
        "Material Name",
        "Material Code",
        "Category",
        "Unit",
        "Opening Quantity",
        "Reorder Level",
        "Unit Cost",
        "Branch"
    ];

    private static readonly string[] SiteHeaders =
    [
        "ClientName",
        "ClientId",
        "SiteName",
        "SiteType",
        "Country",
        "County",
        "City",
        "Address",
        "ContactPerson",
        "ContactPhone",
        "ContactEmail",
        "Status"
    ];

    private static readonly string[] AllowedImportSiteTypes = ["Branch", "HQ", "Data Centre", "Warehouse", "Factory", "Other"];
    private static readonly string[] AllowedImportSiteStatuses = ["Active", "Inactive", "Closed"];

    [HttpGet("templates/clients")]
    public async Task<IActionResult> DownloadClientTemplate(CancellationToken cancellationToken)
    {
        EnsureCanImportClients();
        await LogTemplateDownloadAsync("clients", cancellationToken);
        return BuildTemplateFile(ClientHeaders, ["ABC Factory", "John Mwangi", "john@abcfactory.co.ke", "+254700000000", "Kenya", "Nairobi", "Nairobi", "Industrial Area", "Active"], "clients_import_template.csv");
    }

    [HttpGet("templates/assets")]
    public async Task<IActionResult> DownloadAssetTemplate(CancellationToken cancellationToken)
    {
        EnsureCanImportAssets();
        await LogTemplateDownloadAsync("assets", cancellationToken);
        return BuildTemplateFile(AssetHeaders, ["ABC Factory", "", "Main Site", "", "Generator System", "", "Generator", "Generator 500kVA", "GEN-001", "Generator", "Power", "ClientOwned", "2025-01-15", "2026-04-01", "3", "months", "", "true", "Head Office", ""], "assets_import_template.csv");
    }

    [HttpGet("templates/users")]
    public async Task<IActionResult> DownloadUserTemplate(CancellationToken cancellationToken)
    {
        EnsureCanImportUsers();
        await LogTemplateDownloadAsync("users", cancellationToken);
        return BuildTemplateFile(UserHeaders, ["Dennis Yegon", "dennis@example.com", "+254700000000", "TenantAdmin", "Operations Manager", "Head Office", "false", "Active"], "users_import_template.csv");
    }

    [HttpGet("templates/branches")]
    public async Task<IActionResult> DownloadBranchTemplate(CancellationToken cancellationToken)
    {
        EnsureCanImportBranches();
        await LogTemplateDownloadAsync("branches", cancellationToken);
        return BuildTemplateFile(BranchHeaders, ["Head Office", "HO", "Kenya", "Nairobi", "Nairobi", "Upper Hill", "Active"], "branches_import_template.csv");
    }

    [HttpGet("templates/materials")]
    public async Task<IActionResult> DownloadMaterialTemplate(CancellationToken cancellationToken)
    {
        EnsureCanImportMaterials();
        await LogTemplateDownloadAsync("materials", cancellationToken);
        return BuildTemplateFile(MaterialHeaders, ["Generator Oil", "MAT-001", "Consumables", "Litres", "20", "5", "1200", "Head Office"], "materials_import_template.csv");
    }

    [HttpGet("templates/sites")]
    public async Task<IActionResult> DownloadSiteTemplate(CancellationToken cancellationToken)
    {
        EnsureCanImportSites();
        await LogTemplateDownloadAsync("sites", cancellationToken);
        return BuildTemplateFile(SiteHeaders, ["ABC Factory", "", "Main Campus", "Branch", "Kenya", "Nairobi", "Nairobi", "Industrial Area", "Jane Wanjiku", "+254700000000", "jane@abcfactory.co.ke", "Active"], "sites_import_template.csv");
    }

    [HttpPost("clients/preview")]
    public async Task<ActionResult<ImportPreviewResponse>> PreviewClients([FromForm] CsvUploadRequest request, CancellationToken cancellationToken)
    {
        EnsureCanImportClients();
        var rows = await BuildClientPreviewAsync(request.File, cancellationToken);
        return Ok(new ImportPreviewResponse("Clients", rows.Count, rows));
    }

    [HttpPost("assets/preview")]
    public async Task<ActionResult<ImportPreviewResponse>> PreviewAssets([FromForm] CsvUploadRequest request, CancellationToken cancellationToken)
    {
        EnsureCanImportAssets();
        var rows = await BuildAssetPreviewAsync(request.File, cancellationToken);
        return Ok(new ImportPreviewResponse("Assets", rows.Count, rows));
    }

    [HttpPost("users/preview")]
    public async Task<ActionResult<ImportPreviewResponse>> PreviewUsers([FromForm] CsvUploadRequest request, CancellationToken cancellationToken)
    {
        EnsureCanImportUsers();
        var rows = await BuildUserPreviewAsync(request.File, cancellationToken);
        return Ok(new ImportPreviewResponse("Users", rows.Count, rows));
    }

    [HttpPost("branches/preview")]
    public async Task<ActionResult<ImportPreviewResponse>> PreviewBranches([FromForm] CsvUploadRequest request, CancellationToken cancellationToken)
    {
        EnsureCanImportBranches();
        var rows = await BuildBranchPreviewAsync(request.File, cancellationToken);
        return Ok(new ImportPreviewResponse("Branches", rows.Count, rows));
    }

    [HttpPost("materials/preview")]
    public async Task<ActionResult<ImportPreviewResponse>> PreviewMaterials([FromForm] CsvUploadRequest request, CancellationToken cancellationToken)
    {
        EnsureCanImportMaterials();
        var rows = await BuildMaterialPreviewAsync(request.File, cancellationToken);
        return Ok(new ImportPreviewResponse("Materials", rows.Count, rows));
    }

    [HttpPost("sites/preview")]
    public async Task<ActionResult<ImportPreviewResponse>> PreviewSites([FromForm] CsvUploadRequest request, CancellationToken cancellationToken)
    {
        EnsureCanImportSites();
        var rows = await BuildSitePreviewAsync(request.File, cancellationToken);
        return Ok(new ImportPreviewResponse("Sites", rows.Count, rows));
    }

    [HttpPost("sites/commit")]
    public async Task<ActionResult<ImportCommitResponse>> CommitSites([FromForm] CsvUploadRequest request, CancellationToken cancellationToken)
    {
        EnsureCanImportSites();
        var rows = await BuildSitePreviewAsync(request.File, cancellationToken);
        var successful = 0;
        var failed = 0;
        var rowErrors = new List<ImportCommitRowErrorResponse>();

        var clients = await dbContext.Clients
            .Where(x => x.TenantId == TenantId && x.IsActive)
            .ToListAsync(cancellationToken);
        var clientById = clients.ToDictionary(x => x.Id.ToString().ToLower(), x => x, StringComparer.OrdinalIgnoreCase);
        var clientByName = clients.ToDictionary(x => x.ClientName.ToLower(), x => x, StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            if (!row.IsValid)
            {
                failed++;
                rowErrors.Add(new ImportCommitRowErrorResponse(row.RowNumber, row.Errors.ToArray()));
                continue;
            }

            var client = ResolveImportClient(row.RawValues, clientById, clientByName);
            if (client is null)
            {
                failed++;
                rowErrors.Add(new ImportCommitRowErrorResponse(row.RowNumber, ["Client could not be resolved."]));
                continue;
            }

            var siteCode = await documentNumberingService.GenerateAsync(TenantId, null, DocumentTypes.Site, cancellationToken);
            dbContext.Sites.Add(new Site
            {
                TenantId = TenantId,
                ClientId = client.Id,
                SiteCode = siteCode,
                SiteName = GetValue(row.RawValues, "SiteName")!,
                SiteType = NormalizeImportSiteType(GetValue(row.RawValues, "SiteType")),
                Status = NormalizeImportSiteStatus(GetValue(row.RawValues, "Status")),
                Country = GetValue(row.RawValues, "Country"),
                County = GetValue(row.RawValues, "County"),
                TownCity = GetValue(row.RawValues, "City"),
                StreetAddress = GetValue(row.RawValues, "Address"),
                ContactPerson = GetValue(row.RawValues, "ContactPerson"),
                ContactPhone = GetValue(row.RawValues, "ContactPhone"),
                ContactEmail = GetValue(row.RawValues, "ContactEmail")
            });
            successful++;
        }

        dbContext.ImportBatches.Add(new ImportBatch
        {
            TenantId = TenantId,
            ImportType = "Sites",
            FileName = request.File.FileName,
            TotalRows = rows.Count,
            SuccessfulRows = successful,
            FailedRows = failed,
            Status = failed == 0 ? "Completed" : "CompletedWithErrors"
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Import completed",
            nameof(ImportBatch),
            "sites",
            $"Completed site import with {successful} successful rows and {failed} failed rows.",
            cancellationToken);

        return Ok(new ImportCommitResponse(rows.Count, successful, failed, failed, rowErrors));
    }

    [HttpPost("clients/commit")]
    public async Task<ActionResult<ImportCommitResponse>> CommitClients([FromForm] CsvUploadRequest request, CancellationToken cancellationToken)
    {
        EnsureCanImportClients();
        var rows = await BuildClientPreviewAsync(request.File, cancellationToken);
        var successful = 0;
        var failed = 0;

        foreach (var row in rows)
        {
            if (!row.IsValid)
            {
                failed++;
                continue;
            }

            var client = new Client
            {
                TenantId = TenantId,
                ClientName = row.RawValues["ClientName"]!,
                Email = row.RawValues["Email"],
                Phone = row.RawValues["Phone"],
                Location = BuildJoinedValue(row.RawValues["Address"], row.RawValues["City"], row.RawValues["County"], row.RawValues["Country"]),
                ContactPerson = row.RawValues["ContactPerson"],
                Notes = BuildStatusNote(row.RawValues["Status"]),
                IsActive = IsActiveStatus(row.RawValues["Status"])
            };

            dbContext.Clients.Add(client);
            successful++;
        }

        var skipped = failed;
        dbContext.ImportBatches.Add(new ImportBatch
        {
            TenantId = TenantId,
            ImportType = "Clients",
            FileName = request.File.FileName,
            TotalRows = rows.Count,
            SuccessfulRows = successful,
            FailedRows = failed,
            Status = failed == 0 ? "Completed" : "CompletedWithErrors"
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Import completed",
            nameof(ImportBatch),
            "clients",
            $"Completed client import with {successful} successful rows and {failed} failed rows.",
            cancellationToken);

        return Ok(new ImportCommitResponse(rows.Count, successful, failed, skipped));
    }

    [HttpPost("assets/commit")]
    public async Task<ActionResult<ImportCommitResponse>> CommitAssets([FromForm] CsvUploadRequest request, [FromForm(Name = "AllowCreateSystems")] bool allowCreateSystems, CancellationToken cancellationToken)
    {
        EnsureCanImportAssets();
        var rows = await BuildAssetPreviewAsync(request.File, cancellationToken);
        var successful = 0;
        var failed = 0;
        var rowErrors = new List<ImportCommitRowErrorResponse>();

        var clients = await dbContext.Clients
            .Where(x => x.TenantId == TenantId && x.IsActive)
            .ToListAsync(cancellationToken);
        var branchByName = await dbContext.Branches
            .Where(x => x.TenantId == TenantId && x.IsActive)
            .ToDictionaryAsync(x => x.Name.ToLower(), x => x.Id, cancellationToken);
        var branchById = await dbContext.Branches
            .Where(x => x.TenantId == TenantId && x.IsActive)
            .ToDictionaryAsync(x => x.Id.ToString().ToLower(), x => x.Id, cancellationToken);
        var sites = await dbContext.Sites
            .Where(x => x.TenantId == TenantId && x.Status == "Active")
            .ToListAsync(cancellationToken);
        var systems = await dbContext.Systems
            .Where(x => x.TenantId == TenantId)
            .ToListAsync(cancellationToken);
        var systemById = systems.ToDictionary(x => x.Id.ToString().ToLower(), x => x, StringComparer.OrdinalIgnoreCase);
        var categoryByName = await dbContext.AssetCategories
            .Where(x => x.TenantId == TenantId && x.IsActive)
            .ToDictionaryAsync(x => x.Name.ToLower(), x => x.Id, cancellationToken);
        var clientById = clients.ToDictionary(x => x.Id.ToString().ToLower(), x => x, StringComparer.OrdinalIgnoreCase);
        var clientByName = clients.ToDictionary(x => x.ClientName.ToLower(), x => x, StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            if (!row.IsValid)
            {
                failed++;
                rowErrors.Add(new ImportCommitRowErrorResponse(row.RowNumber, row.Errors.ToArray()));
                continue;
            }

            var commitErrors = new List<string>();
            Client? client = null;
            if (!string.IsNullOrWhiteSpace(row.RawValues["ClientId"]) && clientById.TryGetValue(row.RawValues["ClientId"]!.Trim().ToLowerInvariant(), out var resolvedClientById))
            {
                client = resolvedClientById;
            }
            else if (!string.IsNullOrWhiteSpace(row.RawValues["ClientName"]) && clientByName.TryGetValue(row.RawValues["ClientName"]!.Trim().ToLowerInvariant(), out var resolvedClientByName))
            {
                client = resolvedClientByName;
            }

            if (client is null)
            {
                commitErrors.Add("ClientName or ClientId must resolve to an existing client.");
            }

            var branchId = ResolveImportGuid(row.RawValues["BranchId"], branchById)
                ?? ResolveImportGuid(row.RawValues["BranchName"], branchByName);
            var site = ResolveSiteForImport(client?.Id, row.RawValues["SiteId"], row.RawValues["SiteName"], sites);
            var siteId = site?.Id;
            OperationalSystem? system = null;
            if (!string.IsNullOrWhiteSpace(row.RawValues["SystemId"]) && systemById.TryGetValue(row.RawValues["SystemId"]!.Trim().ToLowerInvariant(), out var resolvedSystemById))
            {
                system = resolvedSystemById;
            }
            var assetCategoryId = ResolveImportGuid(row.RawValues["AssetCategory"], categoryByName);
            var installationDate = ParseNullableDate(row.RawValues["InstallationDate"]);
            var lastPmDate = ParseNullableDate(row.RawValues["LastPmServiceDate"]);
            var nextPmDate = ParseNullableDate(row.RawValues["NextPmDueDate"]);
            var frequencyValue = ParseNullableInt(row.RawValues["PmFrequency"]);
            var frequencyUnit = NormalizePmFrequencyUnit(row.RawValues["PmFrequencyUnit"], out var frequencyUnitValid);
            if (!frequencyUnitValid)
            {
                commitErrors.Add("PmFrequencyUnit must be days, weeks, months, quarters, or years.");
            }
            if (!string.IsNullOrWhiteSpace(row.RawValues["PmFrequency"]) && !frequencyValue.HasValue)
            {
                commitErrors.Add("PmFrequency must be a whole number.");
            }
            if (!siteId.HasValue)
            {
                commitErrors.Add("SiteName or SiteId must resolve under the selected client.");
            }
            if (!string.IsNullOrWhiteSpace(row.RawValues["SystemName"]) && !siteId.HasValue)
            {
                commitErrors.Add("SiteName or SiteId is required when SystemName is provided.");
            }
            if (!TryParseAutoSchedule(row.RawValues["AutoSchedulePm"], out var autoSchedulePm))
            {
                commitErrors.Add("AutoSchedulePm must be true/false or yes/no.");
            }

            if (commitErrors.Count == 0 && !string.IsNullOrWhiteSpace(row.RawValues["SystemName"]) && siteId.HasValue)
            {
                system ??= systems.FirstOrDefault(x =>
                    x.SiteId == siteId.Value
                    && string.Equals(x.SystemName, row.RawValues["SystemName"]!.Trim(), StringComparison.OrdinalIgnoreCase));

                if (system is null)
                {
                    if (!allowCreateSystems)
                    {
                        commitErrors.Add("SystemName does not exist for this site. Re-run with AllowCreateSystems enabled.");
                    }
                    else
                    {
                        system = new OperationalSystem
                        {
                            TenantId = TenantId,
                            ClientId = client!.Id,
                            SiteId = siteId.Value,
                            SystemName = row.RawValues["SystemName"]!.Trim(),
                            SystemType = string.IsNullOrWhiteSpace(row.RawValues["SystemType"]) ? "General" : row.RawValues["SystemType"]!.Trim(),
                            Status = "Active"
                        };
                        dbContext.Systems.Add(system);
                        await dbContext.SaveChangesAsync(cancellationToken);
                        systems.Add(system);
                    }
                }
            }

            if (commitErrors.Count > 0)
            {
                failed++;
                rowErrors.Add(new ImportCommitRowErrorResponse(row.RowNumber, commitErrors.ToArray()));
                continue;
            }

            var asset = new Asset
            {
                TenantId = TenantId,
                ClientId = client!.Id,
                BranchId = branchId,
                SiteId = siteId,
                SystemId = system?.Id,
                AssetCategoryId = assetCategoryId,
                AssetName = row.RawValues["AssetName"]!,
                AssetCode = row.RawValues["AssetCode"]!,
                AssetType = row.RawValues["AssetType"],
                OwnershipType = string.IsNullOrWhiteSpace(row.RawValues["OwnershipType"]) ? "ClientOwned" : row.RawValues["OwnershipType"]!,
                InstallationDate = installationDate,
                RecommendedPmFrequency = BuildImportedPmFrequencyLabel(frequencyValue, frequencyUnit),
                PmFrequencyValue = frequencyValue,
                PmFrequencyUnit = frequencyUnit,
                AutoSchedulePm = autoSchedulePm,
                LastPmDate = lastPmDate,
                NextPmDate = nextPmDate ?? CalculateNextPmDate(lastPmDate, frequencyValue, frequencyUnit),
                Notes = null,
                Status = "Active",
            };

            dbContext.Assets.Add(asset);
            await dbContext.SaveChangesAsync(cancellationToken);
            await preventiveMaintenancePlanService.SyncForAssetAsync(asset, cancellationToken);
            successful++;
        }

        var skipped = failed;
        dbContext.ImportBatches.Add(new ImportBatch
        {
            TenantId = TenantId,
            ImportType = "Assets",
            FileName = request.File.FileName,
            TotalRows = rows.Count,
            SuccessfulRows = successful,
            FailedRows = failed,
            Status = failed == 0 ? "Completed" : "CompletedWithErrors"
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Import completed",
            nameof(ImportBatch),
            "assets",
            $"Completed asset import with {successful} successful rows and {failed} failed rows.",
            cancellationToken);

        return Ok(new ImportCommitResponse(rows.Count, successful, failed, skipped, rowErrors));
    }

    [HttpPost("users/commit")]
    public async Task<ActionResult<ImportCommitResponse>> CommitUsers([FromForm] CsvUploadRequest request, CancellationToken cancellationToken)
    {
        EnsureCanImportUsers();
        var rows = await BuildUserPreviewAsync(request.File, cancellationToken);

        // Bulk import is subject to the same licensed-seat rule as every other way of adding a user: the
        // whole batch's active rows must fit, checked before any row is written (TM-P1-07).
        var incomingActiveRoles = rows.Where(row => row.IsValid && IsActiveStatus(row.RawValues["Status"])).Select(row => NormalizeImportedRole(row.RawValues["Role"])).ToList();
        await licenseGuardService.EnsureSeatsAvailableAsync(TenantId, incomingActiveRoles, cancellationToken);

        var branchLookup = await dbContext.Branches
            .Where(x => x.TenantId == TenantId && x.IsActive)
            .ToDictionaryAsync(x => x.Name.ToLower(), x => x.Id, cancellationToken);

        var successful = 0;
        var failed = 0;
        var rowErrors = new List<ImportCommitRowErrorResponse>();
        var createdAccounts = new List<string>();
        var createdUserIds = new List<Guid>();

        foreach (var row in rows)
        {
            if (!row.IsValid)
            {
                failed++;
                rowErrors.Add(new ImportCommitRowErrorResponse(row.RowNumber, row.Errors.ToArray()));
                continue;
            }

            var role = NormalizeImportedRole(row.RawValues["Role"]);
            var branchId = ResolveBranchId(row.RawValues["BranchName"], branchLookup);
            var jobTitle = Normalize(row.RawValues["JobTitle"]);
            var temporaryPassword = temporaryPasswordService.Generate();
            var user = new User
            {
                TenantId = TenantId,
                FullName = row.RawValues["FullName"]!,
                Email = row.RawValues["Email"]!.Trim().ToLowerInvariant(),
                Role = role,
                JobTitle = jobTitle,
                IsActive = IsActiveStatus(row.RawValues["Status"]),
                HasAllBranchAccess = string.Equals(role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase),
                MustChangePassword = true,
                DefaultBranchId = branchId,
                Permission = ToPermissionEntity(permissionTemplateService.GetDefaultPermissions(role, jobTitle))
            };
            user.PasswordHash = passwordHasher.HashPassword(user, temporaryPassword);

            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(cancellationToken);
            createdUserIds.Add(user.Id);
            createdAccounts.Add($"'{user.Email}' as {user.Role}");

            if (ParseBoolean(row.RawValues["IsTechnician"]))
            {
                dbContext.Technicians.Add(new Technician
                {
                    TenantId = TenantId,
                    UserId = user.Id,
                    BranchId = branchId,
                    FullName = user.FullName,
                    Email = user.Email,
                    Phone = Normalize(row.RawValues["Phone"]),
                    IsActive = user.IsActive
                });
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var delivery = await credentialDeliveryService.SendAsync(
                TenantId,
                user,
                new UserCredentialDeliveryRequest(
                    "password-reset",
                    credentialDeliveryService.BuildLoginUrl(),
                    temporaryPassword,
                    null,
                    true),
                cancellationToken);

            if (delivery.Success)
            {
                user.LastCredentialSentAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            successful++;
        }

        var skipped = failed;
        dbContext.ImportBatches.Add(new ImportBatch
        {
            TenantId = TenantId,
            ImportType = "Users",
            FileName = request.File.FileName,
            TotalRows = rows.Count,
            SuccessfulRows = successful,
            FailedRows = failed,
            Status = failed == 0 ? "Completed" : "CompletedWithErrors"
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Import completed",
            nameof(ImportBatch),
            "users",
            $"Completed user import with {successful} successful rows and {failed} failed rows.",
            cancellationToken);

        // A user import is a privilege grant: a row carrying Role=TenantAdmin mints a full tenant
        // administrator. The row count above cannot show that, so name every account created and the
        // role it was granted, the same way UsersController.Create records a single account.
        if (createdAccounts.Count > 0)
        {
            await auditLogService.LogAsync(
                TenantId,
                UserId,
                "Users imported",
                nameof(User),
                string.Join(",", createdUserIds),
                $"Created {createdAccounts.Count} user account(s) by import: {string.Join(", ", createdAccounts)}.",
                cancellationToken);
        }

        return Ok(new ImportCommitResponse(rows.Count, successful, failed, skipped, rowErrors));
    }

    [HttpPost("branches/commit")]
    public async Task<ActionResult<ImportCommitResponse>> CommitBranches([FromForm] CsvUploadRequest request, CancellationToken cancellationToken)
    {
        EnsureCanImportBranches();
        var rows = await BuildBranchPreviewAsync(request.File, cancellationToken);
        var successful = 0;
        var failed = 0;

        foreach (var row in rows)
        {
            if (!row.IsValid)
            {
                failed++;
                continue;
            }

            dbContext.Branches.Add(new Branch
            {
                TenantId = TenantId,
                Name = row.RawValues["BranchName"]!,
                Code = row.RawValues["Code"]!.Trim().ToUpperInvariant(),
                Location = BuildJoinedValue(row.RawValues["City"], row.RawValues["County"], row.RawValues["Country"]),
                Address = Normalize(row.RawValues["Address"]),
                IsActive = IsActiveStatus(row.RawValues["Status"])
            });
            successful++;
        }

        var skipped = failed;
        dbContext.ImportBatches.Add(new ImportBatch
        {
            TenantId = TenantId,
            ImportType = "Branches",
            FileName = request.File.FileName,
            TotalRows = rows.Count,
            SuccessfulRows = successful,
            FailedRows = failed,
            Status = failed == 0 ? "Completed" : "CompletedWithErrors"
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Import completed",
            nameof(ImportBatch),
            "branches",
            $"Completed branch import with {successful} successful rows and {failed} failed rows.",
            cancellationToken);

        return Ok(new ImportCommitResponse(rows.Count, successful, failed, skipped));
    }

    [HttpPost("materials/commit")]
    public async Task<ActionResult<ImportCommitResponse>> CommitMaterials([FromForm] CsvUploadRequest request, CancellationToken cancellationToken)
    {
        EnsureCanImportMaterials();
        var rows = await BuildMaterialPreviewAsync(request.File, cancellationToken);
        var branchLookup = await dbContext.Branches
            .Where(x => x.TenantId == TenantId && x.IsActive)
            .ToDictionaryAsync(x => x.Name.ToLower(), x => x.Id, cancellationToken);

        var successful = 0;
        var failed = 0;

        // Opening quantities go through the stock ledger, like a part created by hand: that gives the
        // branch a stock balance and the part an INITIAL-STOCK receipt. Writing QuantityOnHand directly
        // left the stock with no branch and no movement, so it never showed per branch or in the ledger.
        await stockLedgerService.ExecuteAsync(async ct =>
        {
            foreach (var row in rows)
            {
                if (!row.IsValid)
                {
                    failed++;
                    continue;
                }

                var item = new MaterialItem
                {
                    TenantId = TenantId,
                    ItemName = row.RawValues["Material Name"]!.Trim(),
                    ItemCode = row.RawValues["Material Code"]!.Trim(),
                    Category = string.IsNullOrWhiteSpace(row.RawValues["Category"]) ? null : row.RawValues["Category"]!.Trim(),
                    UnitOfMeasure = row.RawValues["Unit"]!.Trim(),
                    QuantityOnHand = 0,
                    ReorderLevel = ParseDecimal(row.RawValues["Reorder Level"]) ?? 0,
                    UnitCost = ParseDecimal(row.RawValues["Unit Cost"]),
                    IsActive = true
                };

                dbContext.MaterialItems.Add(item);

                var openingQuantity = ParseDecimal(row.RawValues["Opening Quantity"]) ?? 0;
                if (openingQuantity > 0)
                {
                    var branchName = GetValue(row.RawValues, "Branch");
                    Guid? requestedBranchId = !string.IsNullOrWhiteSpace(branchName) && branchLookup.TryGetValue(branchName.Trim().ToLowerInvariant(), out var foundBranchId)
                        ? foundBranchId
                        : null;
                    var branchId = await branchAccessService.ResolveBranchIdForWriteAsync(TenantId, requestedBranchId, ct);
                    await stockLedgerService.RecordReceiptAsync(item, branchId, openingQuantity, UserId, "Initial stock balance (import)", "INITIAL-STOCK", cancellationToken: ct);
                }

                successful++;
            }

            dbContext.ImportBatches.Add(new ImportBatch
            {
                TenantId = TenantId,
                ImportType = "Materials",
                FileName = request.File.FileName,
                TotalRows = rows.Count,
                SuccessfulRows = successful,
                FailedRows = failed,
                Status = failed == 0 ? "Completed" : "CompletedWithErrors"
            });

            await dbContext.SaveChangesAsync(ct);
        }, cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Import completed",
            nameof(ImportBatch),
            "materials",
            $"Completed material import with {successful} successful rows and {failed} failed rows.",
            cancellationToken);

        return Ok(new ImportCommitResponse(rows.Count, successful, failed, failed));
    }

    private async Task<List<ImportPreviewRowResponse>> BuildClientPreviewAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var records = await ReadCsvAsync(file, ClientHeaders.Length, cancellationToken);
        var existingNames = await dbContext.Clients
            .Where(x => x.TenantId == TenantId && x.IsActive)
            .Select(x => x.ClientName.ToLower())
            .ToListAsync(cancellationToken);
        var seen = new HashSet<string>(existingNames);
        var results = new List<ImportPreviewRowResponse>();

        foreach (var record in records)
        {
            var rawValues = ToDictionary(ClientHeaders, record.Values);
            var errors = new List<string>();
            var name = GetValue(rawValues, "ClientName");

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add("Client Name is required.");
            }
            else if (!seen.Add(name.ToLowerInvariant()))
            {
                errors.Add("Duplicate client name for this tenant.");
            }

            results.Add(new ImportPreviewRowResponse(record.RowNumber, rawValues, errors.Count == 0, errors));
        }

        return results;
    }

    private async Task<List<ImportPreviewRowResponse>> BuildSitePreviewAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var records = await ReadCsvAsync(file, SiteHeaders.Length, cancellationToken);
        var clients = await dbContext.Clients
            .Where(x => x.TenantId == TenantId && x.IsActive)
            .ToListAsync(cancellationToken);
        var clientById = clients.ToDictionary(x => x.Id.ToString().ToLower(), x => x, StringComparer.OrdinalIgnoreCase);
        var clientByName = clients.ToDictionary(x => x.ClientName.ToLower(), x => x, StringComparer.OrdinalIgnoreCase);
        var existingSiteKeys = new HashSet<string>(await dbContext.Sites
            .Where(x => x.TenantId == TenantId && x.Status != "Closed")
            .Select(x => x.ClientId.ToString().ToLower() + "|" + x.SiteName.ToLower())
            .ToListAsync(cancellationToken));

        var results = new List<ImportPreviewRowResponse>();
        foreach (var record in records)
        {
            var rawValues = ToDictionary(SiteHeaders, record.Values);
            var errors = new List<string>();

            var siteName = GetValue(rawValues, "SiteName");
            if (string.IsNullOrWhiteSpace(siteName))
            {
                errors.Add("Site Name is required.");
            }

            var client = ResolveImportClient(rawValues, clientById, clientByName);
            if (client is null)
            {
                errors.Add("Client Name or Client Id must match an existing active client.");
            }

            var siteType = GetValue(rawValues, "SiteType");
            if (!string.IsNullOrWhiteSpace(siteType) && !AllowedImportSiteTypes.Contains(siteType, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add("Site type must be Branch, HQ, Data Centre, Warehouse, Factory, or Other.");
            }

            var status = GetValue(rawValues, "Status");
            if (!string.IsNullOrWhiteSpace(status) && !AllowedImportSiteStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add("Status must be Active, Inactive, or Closed.");
            }

            if (client is not null && !string.IsNullOrWhiteSpace(siteName)
                && !existingSiteKeys.Add(client.Id.ToString().ToLower() + "|" + siteName.ToLowerInvariant()))
            {
                errors.Add("Duplicate site name for this client.");
            }

            results.Add(new ImportPreviewRowResponse(record.RowNumber, rawValues, errors.Count == 0, errors));
        }

        return results;
    }

    private static Client? ResolveImportClient(
        IReadOnlyDictionary<string, string?> rawValues,
        IReadOnlyDictionary<string, Client> clientById,
        IReadOnlyDictionary<string, Client> clientByName)
    {
        var clientId = GetValue(rawValues, "ClientId");
        if (!string.IsNullOrWhiteSpace(clientId) && clientById.TryGetValue(clientId.ToLowerInvariant(), out var byId))
        {
            return byId;
        }

        var clientName = GetValue(rawValues, "ClientName");
        if (!string.IsNullOrWhiteSpace(clientName) && clientByName.TryGetValue(clientName.ToLowerInvariant(), out var byName))
        {
            return byName;
        }

        return null;
    }

    private static string NormalizeImportSiteType(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "Branch"
            : AllowedImportSiteTypes.Single(item => string.Equals(item, value.Trim(), StringComparison.OrdinalIgnoreCase));

    private static string NormalizeImportSiteStatus(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "Active"
            : AllowedImportSiteStatuses.Single(item => string.Equals(item, value.Trim(), StringComparison.OrdinalIgnoreCase));

    private async Task<List<ImportPreviewRowResponse>> BuildAssetPreviewAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var records = await ReadCsvAsync(file, AssetHeaders.Length, cancellationToken);
        var clients = await dbContext.Clients
            .Where(x => x.TenantId == TenantId && x.IsActive)
            .ToListAsync(cancellationToken);
        var branchNames = await dbContext.Branches
            .Where(x => x.TenantId == TenantId && x.IsActive)
            .Select(x => x.Name.ToLower())
            .ToListAsync(cancellationToken);
        var branchIds = await dbContext.Branches
            .Where(x => x.TenantId == TenantId && x.IsActive)
            .Select(x => x.Id.ToString().ToLower())
            .ToListAsync(cancellationToken);
        var sites = await dbContext.Sites
            .Where(x => x.TenantId == TenantId && x.Status == "Active")
            .ToListAsync(cancellationToken);
        var categoryNames = await dbContext.AssetCategories
            .Where(x => x.TenantId == TenantId && x.IsActive)
            .Select(x => x.Name.ToLower())
            .ToListAsync(cancellationToken);
        var existingCodes = await dbContext.Assets
            .Where(x => x.TenantId == TenantId)
            .Select(x => x.AssetCode.ToLower())
            .ToListAsync(cancellationToken);
        var clientLookup = new HashSet<string>(clients.Select(x => x.ClientName.ToLower()));
        var clientIdLookup = new HashSet<string>(clients.Select(x => x.Id.ToString().ToLower()));
        var branchLookup = new HashSet<string>(branchNames);
        var branchIdLookup = new HashSet<string>(branchIds);
        var categoryLookup = new HashSet<string>(categoryNames);
        var codeLookup = new HashSet<string>(existingCodes);
        var results = new List<ImportPreviewRowResponse>();

        foreach (var record in records)
        {
            var rawValues = ToDictionary(AssetHeaders, record.Values);
            var errors = new List<string>();
            var clientName = GetValue(rawValues, "ClientName");
            var clientId = GetValue(rawValues, "ClientId");
            var branchName = GetValue(rawValues, "BranchName");
            var assetName = GetValue(rawValues, "AssetName");

            if (string.IsNullOrWhiteSpace(clientName) && string.IsNullOrWhiteSpace(clientId))
            {
                errors.Add("ClientName or ClientId is required.");
            }
            else if (!string.IsNullOrWhiteSpace(clientId) && !clientIdLookup.Contains(clientId.ToLowerInvariant()))
            {
                errors.Add("ClientId must exist.");
            }
            else if (string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientName) && !clientLookup.Contains(clientName.ToLowerInvariant()))
            {
                errors.Add("ClientName must exist.");
            }

            if (!string.IsNullOrWhiteSpace(branchName) && !branchLookup.Contains(branchName.ToLowerInvariant()))
            {
                errors.Add("BranchName must exist when provided.");
            }

            if (!string.IsNullOrWhiteSpace(rawValues["BranchId"]) && !branchIdLookup.Contains(rawValues["BranchId"]!.Trim().ToLowerInvariant()))
            {
                errors.Add("BranchId must exist when provided.");
            }

            var resolvedClientId = !string.IsNullOrWhiteSpace(clientId) && Guid.TryParse(clientId, out var parsedClientId)
                ? parsedClientId
                : clients.FirstOrDefault(x => string.Equals(x.ClientName, clientName, StringComparison.OrdinalIgnoreCase))?.Id;
            var resolvedSite = ResolveSiteForImport(resolvedClientId, rawValues["SiteId"], rawValues["SiteName"], sites);

            if (string.IsNullOrWhiteSpace(rawValues["SiteName"]) && string.IsNullOrWhiteSpace(rawValues["SiteId"]))
            {
                errors.Add("SiteName or SiteId is required.");
            }
            else if (resolvedClientId.HasValue && resolvedSite is null)
            {
                errors.Add("SiteName or SiteId must exist under the selected client.");
            }

            if (!string.IsNullOrWhiteSpace(rawValues["ClientId"]) && !Guid.TryParse(rawValues["ClientId"], out _))
            {
                errors.Add("ClientId must be a valid GUID when provided.");
            }

            if (!string.IsNullOrWhiteSpace(rawValues["SystemId"]) && !Guid.TryParse(rawValues["SystemId"], out _))
            {
                errors.Add("SystemId must be a valid GUID when provided.");
            }

            if (!string.IsNullOrWhiteSpace(rawValues["AssetCategory"]) && !categoryLookup.Contains(rawValues["AssetCategory"]!.Trim().ToLowerInvariant()))
            {
                errors.Add("AssetCategory must exist when provided.");
            }

            if (string.IsNullOrWhiteSpace(assetName))
            {
                errors.Add("AssetName is required.");
            }

            if (string.IsNullOrWhiteSpace(rawValues["AssetCode"]))
            {
                errors.Add("AssetCode is required.");
            }
            else if (!codeLookup.Add(rawValues["AssetCode"]!.Trim().ToLowerInvariant()))
            {
                errors.Add("AssetCode must be unique per tenant.");
            }

            if (!string.IsNullOrWhiteSpace(rawValues["InstallationDate"]) && ParseNullableDate(rawValues["InstallationDate"]) is null)
            {
                errors.Add("InstallationDate must be a valid date.");
            }

            if (!string.IsNullOrWhiteSpace(rawValues["LastPmServiceDate"]) && ParseNullableDate(rawValues["LastPmServiceDate"]) is null)
            {
                errors.Add("LastPmServiceDate must be a valid date.");
            }

            if (!string.IsNullOrWhiteSpace(rawValues["NextPmDueDate"]) && ParseNullableDate(rawValues["NextPmDueDate"]) is null)
            {
                errors.Add("NextPmDueDate must be a valid date.");
            }

            if (!string.IsNullOrWhiteSpace(rawValues["PmFrequency"]) && ParseNullableInt(rawValues["PmFrequency"]) is null)
            {
                errors.Add("PmFrequency must be a whole number.");
            }

            if (!string.IsNullOrWhiteSpace(rawValues["PmFrequencyUnit"]) && NormalizePmFrequencyUnit(rawValues["PmFrequencyUnit"], out var validUnit) is null && !validUnit)
            {
                errors.Add("PmFrequencyUnit must be days, weeks, months, quarters, or years.");
            }

            if (!string.IsNullOrWhiteSpace(rawValues["SystemName"]) && string.IsNullOrWhiteSpace(rawValues["SiteName"]) && string.IsNullOrWhiteSpace(rawValues["SiteId"]))
            {
                errors.Add("SiteName or SiteId is required when SystemName is provided.");
            }

            if (!TryParseAutoSchedule(rawValues["AutoSchedulePm"], out _))
            {
                errors.Add("AutoSchedulePm must be true/false or yes/no.");
            }

            results.Add(new ImportPreviewRowResponse(record.RowNumber, rawValues, errors.Count == 0, errors));
        }

        return results;
    }

    private async Task<List<ImportPreviewRowResponse>> BuildUserPreviewAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var records = await ReadCsvAsync(file, UserHeaders.Length, cancellationToken);
        var existingEmails = await dbContext.Users
            .Where(x => x.TenantId == TenantId)
            .Select(x => x.Email.ToLower())
            .ToListAsync(cancellationToken);
        var branchNames = await dbContext.Branches
            .Where(x => x.TenantId == TenantId && x.IsActive)
            .Select(x => x.Name.ToLower())
            .ToListAsync(cancellationToken);

        var emailLookup = new HashSet<string>(existingEmails);
        var branchLookup = new HashSet<string>(branchNames);
        var results = new List<ImportPreviewRowResponse>();

        foreach (var record in records)
        {
            var rawValues = ToDictionary(UserHeaders, record.Values);
            var errors = new List<string>();
            var fullName = GetValue(rawValues, "FullName");
            var email = GetValue(rawValues, "Email");
            var branchName = GetValue(rawValues, "BranchName");

            if (string.IsNullOrWhiteSpace(fullName))
            {
                errors.Add("FullName is required.");
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                errors.Add("Email is required.");
            }
            else if (!email.Contains('@'))
            {
                errors.Add("Email must be valid.");
            }
            else if (!emailLookup.Add(email.ToLowerInvariant()))
            {
                errors.Add("Email must be unique per tenant.");
            }

            if (!string.IsNullOrWhiteSpace(branchName) && !branchLookup.Contains(branchName.ToLowerInvariant()))
            {
                errors.Add("BranchName must exist when provided.");
            }

            try
            {
                NormalizeImportedRole(rawValues["Role"]);
            }
            catch (Exception error) when (error is BusinessRuleException or ForbiddenException)
            {
                errors.Add(error.Message);
            }

            results.Add(new ImportPreviewRowResponse(record.RowNumber, rawValues, errors.Count == 0, errors));
        }

        return results;
    }

    private async Task<List<ImportPreviewRowResponse>> BuildBranchPreviewAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var records = await ReadCsvAsync(file, BranchHeaders.Length, cancellationToken);
        var existingNames = await dbContext.Branches
            .Where(x => x.TenantId == TenantId)
            .Select(x => x.Name.ToLower())
            .ToListAsync(cancellationToken);
        var existingCodes = await dbContext.Branches
            .Where(x => x.TenantId == TenantId)
            .Select(x => x.Code.ToLower())
            .ToListAsync(cancellationToken);

        var nameLookup = new HashSet<string>(existingNames);
        var codeLookup = new HashSet<string>(existingCodes);
        var results = new List<ImportPreviewRowResponse>();

        foreach (var record in records)
        {
            var rawValues = ToDictionary(BranchHeaders, record.Values);
            var errors = new List<string>();
            var branchName = GetValue(rawValues, "BranchName");
            var code = GetValue(rawValues, "Code");

            if (string.IsNullOrWhiteSpace(branchName))
            {
                errors.Add("BranchName is required.");
            }
            else if (!nameLookup.Add(branchName.ToLowerInvariant()))
            {
                errors.Add("BranchName must be unique per tenant.");
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                errors.Add("Code is required.");
            }
            else if (!codeLookup.Add(code.ToLowerInvariant()))
            {
                errors.Add("Code must be unique per tenant.");
            }

            results.Add(new ImportPreviewRowResponse(record.RowNumber, rawValues, errors.Count == 0, errors));
        }

        return results;
    }

    private async Task<List<ImportPreviewRowResponse>> BuildMaterialPreviewAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var records = await ReadCsvAsync(file, MaterialHeaders.Length, cancellationToken);
        var existingCodes = await dbContext.MaterialItems
            .Where(x => x.TenantId == TenantId && x.IsActive)
            .Select(x => x.ItemCode.ToLower())
            .ToListAsync(cancellationToken);
        var branchNames = await dbContext.Branches
            .Where(x => x.TenantId == TenantId && x.IsActive)
            .Select(x => x.Name.ToLower())
            .ToListAsync(cancellationToken);

        var codeLookup = new HashSet<string>(existingCodes);
        var branchLookup = new HashSet<string>(branchNames);
        var results = new List<ImportPreviewRowResponse>();

        foreach (var record in records)
        {
            var rawValues = ToDictionary(MaterialHeaders, record.Values);
            var errors = new List<string>();
            var itemName = GetValue(rawValues, "Material Name");
            var itemCode = GetValue(rawValues, "Material Code");
            var unit = GetValue(rawValues, "Unit");
            var branchName = GetValue(rawValues, "Branch");

            if (string.IsNullOrWhiteSpace(itemName))
            {
                errors.Add("Material Name is required.");
            }

            if (string.IsNullOrWhiteSpace(itemCode))
            {
                errors.Add("Material Code is required.");
            }
            else if (!codeLookup.Add(itemCode.ToLowerInvariant()))
            {
                errors.Add("Material Code must be unique per tenant.");
            }

            if (string.IsNullOrWhiteSpace(unit))
            {
                errors.Add("Unit is required.");
            }

            if (!string.IsNullOrWhiteSpace(branchName) && !branchLookup.Contains(branchName.ToLowerInvariant()))
            {
                errors.Add("Branch must exist when provided.");
            }

            if (!TryParseDecimal(rawValues["Opening Quantity"], out _))
            {
                errors.Add("Opening Quantity must be numeric.");
            }

            if (!TryParseDecimal(rawValues["Reorder Level"], out _))
            {
                errors.Add("Reorder Level must be numeric.");
            }

            if (!TryParseDecimal(rawValues["Unit Cost"], out _, true))
            {
                errors.Add("Unit Cost must be numeric.");
            }

            results.Add(new ImportPreviewRowResponse(record.RowNumber, rawValues, errors.Count == 0, errors));
        }

        return results;
    }

    private static async Task<List<CsvRecord>> ReadCsvAsync(IFormFile file, int expectedColumnCount, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new BusinessRuleException("CSV file is required.");
        }

        if (!string.Equals(Path.GetExtension(file.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("Only CSV files are supported.");
        }

        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        var lines = new List<string>();
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            lines.Add(line);
        }

        if (lines.Count == 0)
        {
            return [];
        }

        var headerValues = ParseCsvLine(lines[0]);
        if (headerValues.Count < expectedColumnCount)
        {
            throw new BusinessRuleException("The import file is missing one or more required headers.");
        }

        var dataLines = lines.Skip(1).Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
        var records = new List<CsvRecord>();

        for (var index = 0; index < dataLines.Count; index++)
        {
            var values = ParseCsvLine(dataLines[index]);
            if (values.Count < expectedColumnCount)
            {
                values.AddRange(Enumerable.Repeat(string.Empty, expectedColumnCount - values.Count));
            }

            records.Add(new CsvRecord(index + 2, values.Take(expectedColumnCount).ToArray()));
        }

        return records;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                values.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        values.Add(current.ToString().Trim());
        return values;
    }

    private static Dictionary<string, string?> ToDictionary(string[] headers, string[] values)
    {
        var dictionary = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Length; i++)
        {
            dictionary[headers[i]] = i < values.Length ? values[i] : null;
        }

        return dictionary;
    }

    private static string? GetValue(IReadOnlyDictionary<string, string?> values, string key) =>
        values.TryGetValue(key, out var value) ? value?.Trim() : null;

    private static bool ParseAutoSchedule(string? value, out bool result)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "yes":
            case "true":
                result = true;
                return true;
            case "no":
            case "false":
            case "":
            case null:
                result = false;
                return true;
            default:
                result = false;
                return false;
        }
    }

    private static string NormalizeImportedRole(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "technician" => AppRoles.Technician,
            "materialscontroller" or "materials controller" or "material controller" or "storekeeper" or "store keeper" => AppRoles.MaterialsController,
            null or "" or "user" or "supervisor" or "normaluser" or "normal user" or "requester" => AppRoles.User,
            "tenantadmin" or "tenant admin" or "admin" => AppRoles.Admin,
            "platformowner" or "platform owner" or "superadmin" or "super admin" => throw new ForbiddenException("Only platform owners can create PlatformOwner accounts."),
            _ => throw new BusinessRuleException("Role must be TenantAdmin, Supervisor, Technician, Materials Controller, NormalUser, or User.")
        };
    }

    private static bool ParseBoolean(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized is "true" or "yes" or "1";
    }

    private static bool IsActiveStatus(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized is null or "" or "active" or "enabled" or "true";
    }

    private static string? BuildStatusNote(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : $"Imported status: {value.Trim()}";

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? BuildJoinedValue(params string?[] values)
    {
        var parts = values.Select(Normalize).Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static string? AppendNote(string? first, string? second)
    {
        var parts = new[] { Normalize(first), Normalize(second) }.Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
        return parts.Count == 0 ? null : string.Join(" | ", parts);
    }

    private static bool TryParseDecimal(string? value, out decimal result, bool allowEmpty = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = 0;
            return allowEmpty;
        }

        return decimal.TryParse(value, out result);
    }

    private static decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return decimal.TryParse(value, out var result) ? result : null;
    }

    private static Guid? ResolveBranchId(string? branchName, IReadOnlyDictionary<string, Guid> branchLookup)
    {
        if (string.IsNullOrWhiteSpace(branchName))
        {
            return null;
        }

        return branchLookup.TryGetValue(branchName.Trim().ToLowerInvariant(), out var branchId) ? branchId : null;
    }

    private static UserPermission ToPermissionEntity(UserPermissionsModel permissions) =>
        new()
        {
            CanViewWorkOrders = permissions.CanViewWorkOrders,
            CanCreateWorkOrders = permissions.CanCreateWorkOrders,
            CanAssignWorkOrders = permissions.CanAssignWorkOrders,
            CanCompleteWorkOrders = permissions.CanCompleteWorkOrders,
            CanApproveMaterials = permissions.CanApproveMaterials,
            CanIssueMaterials = permissions.CanIssueMaterials,
            CanManageAssets = permissions.CanManageAssets,
            CanManageSettings = permissions.CanManageSettings,
            CanViewReports = permissions.CanViewReports,
            CanViewFinance = permissions.CanViewFinance,
            CanManageFinance = permissions.CanManageFinance,
            CanViewFinanceRates = permissions.CanViewFinanceRates,
            CanManageFinanceRates = permissions.CanManageFinanceRates,
            CanViewFinanceApprovals = permissions.CanViewFinanceApprovals,
            CanManageFinanceApprovals = permissions.CanManageFinanceApprovals,
            CanViewBillableWork = permissions.CanViewBillableWork,
            CanManageBillableWork = permissions.CanManageBillableWork,
            CanOverrideMaterialStock = permissions.CanOverrideMaterialStock,
            CanViewCrm = permissions.CanViewCrm,
            CanManageCrm = permissions.CanManageCrm
        };

    // Every action here previews or commits a privileged bulk mutation, so each one carries the same
    // guard as the single-record controller for the entity it writes. The user import creates accounts
    // and grants roles, which is admin-only in UsersController; branches and sites are admin-only in
    // BranchesController and SitesController and stay that way here.
    private void EnsureCanImportUsers() => userAccessService.EnsureAdmin();

    private void EnsureCanImportClients() => userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageAssets);

    private void EnsureCanImportAssets() => userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageAssets);

    private void EnsureCanImportMaterials() => userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageMaterials);

    private void EnsureCanImportBranches() => userAccessService.EnsureAdmin();

    private void EnsureCanImportSites() => userAccessService.EnsureAdmin();

    private Task LogTemplateDownloadAsync(string templateType, CancellationToken cancellationToken) =>
        auditLogService.LogAsync(
            TenantId,
            UserId,
            "Import template downloaded",
            nameof(ImportBatch),
            templateType,
            $"Downloaded {templateType} import template.",
            cancellationToken);

    private static IActionResult BuildTemplateFile(IReadOnlyList<string> headers, IReadOnlyList<string> sampleRow, string fileName)
    {
        var content = Csv.Row(headers) + Environment.NewLine + Csv.Row(sampleRow) + Environment.NewLine;
        return new FileContentResult(Encoding.UTF8.GetBytes(content), "text/csv")
        {
            FileDownloadName = fileName
        };
    }

    private static DateTime? ParseNullableDate(string? value) =>
        DateTime.TryParse(value, out var parsed) ? parsed : null;

    private static DateTime? CalculateNextPmDate(DateTime? lastPmDate, int? frequencyValue, string? frequencyUnit)
    {
        if (!lastPmDate.HasValue || !frequencyValue.HasValue || string.IsNullOrWhiteSpace(frequencyUnit))
        {
            return null;
        }

        return NormalizePmFrequencyUnit(frequencyUnit, out _) switch
        {
            "days" => lastPmDate.Value.AddDays(frequencyValue.Value),
            "weeks" => lastPmDate.Value.AddDays(frequencyValue.Value * 7),
            "months" => lastPmDate.Value.AddMonths(frequencyValue.Value),
            "quarters" => lastPmDate.Value.AddMonths(frequencyValue.Value * 3),
            "years" => lastPmDate.Value.AddYears(frequencyValue.Value),
            _ => null
        };
    }

    private static int? ParseNullableInt(string? value) =>
        int.TryParse(value, out var parsed) ? parsed : null;

    private static string? NormalizePmFrequencyUnit(string? value, out bool isValid)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            isValid = true;
            return null;
        }

        var normalized = value.Trim().ToLowerInvariant() switch
        {
            "day" or "days" => "days",
            "week" or "weeks" => "weeks",
            "month" or "months" => "months",
            "quarter" or "quarters" => "quarters",
            "year" or "years" => "years",
            _ => null
        };

        isValid = normalized is not null;

        return normalized;
    }

    private static bool TryParseAutoSchedule(string? value, out bool result) =>
        ParseAutoSchedule(value, out result);

    private static Guid? ResolveImportGuid(string? rawValue, IReadOnlyDictionary<string, Guid> lookup)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        return lookup.TryGetValue(rawValue.Trim().ToLowerInvariant(), out var parsed) ? parsed : null;
    }

    private static Site? ResolveSiteForImport(Guid? clientId, string? rawSiteId, string? rawSiteName, IReadOnlyCollection<Site> sites)
    {
        if (!clientId.HasValue)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(rawSiteId) && Guid.TryParse(rawSiteId, out var siteId))
        {
            return sites.FirstOrDefault(x => x.ClientId == clientId.Value && x.Id == siteId);
        }

        if (!string.IsNullOrWhiteSpace(rawSiteName))
        {
            return sites.FirstOrDefault(x => x.ClientId == clientId.Value && string.Equals(x.SiteName, rawSiteName.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private static string? BuildImportedPmFrequencyLabel(int? frequencyValue, string? frequencyUnit) =>
        frequencyValue.HasValue && !string.IsNullOrWhiteSpace(frequencyUnit)
            ? $"{frequencyValue.Value} {frequencyUnit}"
            : null;

    private sealed record CsvRecord(int RowNumber, string[] Values);
}

public sealed record CsvUploadRequest(IFormFile File);

public sealed record ImportPreviewResponse(string ImportType, int TotalRows, IReadOnlyCollection<ImportPreviewRowResponse> Rows);

public sealed record ImportPreviewRowResponse(int RowNumber, IReadOnlyDictionary<string, string?> RawValues, bool IsValid, IReadOnlyCollection<string> Errors);

public sealed record ImportCommitResponse(int TotalRows, int SuccessfulRows, int FailedRows, int SkippedRows, IReadOnlyCollection<ImportCommitRowErrorResponse>? RowErrors = null);
public sealed record ImportCommitRowErrorResponse(int RowNumber, IReadOnlyCollection<string> Errors);
