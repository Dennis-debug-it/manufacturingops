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

                if (row.Action == "Update")
                {
                    await UpdateImportedMaterialAsync(row, branchLookup, ct);
                    successful++;
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

    /// <summary>
    /// A materials row whose code is already in the catalogue: refreshes the part's details, and when a
    /// quantity is given sets the stock held at the row's branch to it with one adjustment.
    /// </summary>
    private async Task UpdateImportedMaterialAsync(ImportPreviewRowResponse row, IReadOnlyDictionary<string, Guid> branchLookup, CancellationToken ct)
    {
        var code = row.RawValues["Material Code"]!.Trim();
        var item = await dbContext.MaterialItems
            .Include(x => x.BranchStocks)
            .SingleAsync(x => x.TenantId == TenantId && x.IsActive && x.ItemCode.ToLower() == code.ToLower(), ct);
        item.ItemName = row.RawValues["Material Name"]!.Trim();
        if (!string.IsNullOrWhiteSpace(row.RawValues["Category"])) item.Category = row.RawValues["Category"]!.Trim();
        item.UnitOfMeasure = row.RawValues["Unit"]!.Trim();
        if (ParseDecimal(row.RawValues["Unit Cost"]) is { } cost) item.UnitCost = cost;

        var branchName = GetValue(row.RawValues, "Branch");
        Guid? requestedBranchId = !string.IsNullOrWhiteSpace(branchName) && branchLookup.TryGetValue(branchName.ToLowerInvariant(), out var found) ? found : null;
        if (ParseDecimal(row.RawValues["Reorder Level"]) is { } reorder)
        {
            item.ReorderLevel = reorder;
            if (requestedBranchId is Guid branchForLevel && item.BranchStocks.SingleOrDefault(x => x.BranchId == branchForLevel) is { } levelStock)
                levelStock.ReorderLevel = reorder;
        }

        if (ParseDecimal(row.RawValues["Opening Quantity"]) is not { } target)
            return;
        var branchId = await branchAccessService.ResolveBranchIdForWriteAsync(TenantId, requestedBranchId, ct);
        var current = branchId is Guid id
            ? item.BranchStocks.SingleOrDefault(x => x.BranchId == id)?.QuantityOnHand ?? 0
            : item.QuantityOnHand;
        var delta = target - current;
        if (delta != 0)
            await stockLedgerService.RecordAdjustmentAsync(item, branchId, delta, UserId, "Bulk import stock update", "IMPORT", cancellationToken: ct);
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
        // A code already in the catalogue updates that part (re-uploading a stock list with new prices
        // or quantities); before, every such row was rejected as a duplicate.
        var existing = await dbContext.MaterialItems
            .Where(x => x.TenantId == TenantId)
            .Select(x => new { Code = x.ItemCode.ToLower(), x.IsActive })
            .ToListAsync(cancellationToken);
        var activeCodes = existing.Where(x => x.IsActive).Select(x => x.Code).ToHashSet();
        var inactiveCodes = existing.Where(x => !x.IsActive).Select(x => x.Code).ToHashSet();
        var branchNames = await dbContext.Branches
            .Where(x => x.TenantId == TenantId && x.IsActive)
            .Select(x => x.Name.ToLower())
            .ToListAsync(cancellationToken);

        var seenInFile = new HashSet<string>();
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
            var isUpdate = !string.IsNullOrWhiteSpace(itemCode) && activeCodes.Contains(itemCode.ToLowerInvariant());

            if (string.IsNullOrWhiteSpace(itemName))
            {
                errors.Add("Material Name is required.");
            }

            if (string.IsNullOrWhiteSpace(itemCode))
            {
                errors.Add("Material Code is required.");
            }
            else if (!seenInFile.Add(itemCode.ToLowerInvariant()))
            {
                errors.Add("Material Code appears more than once in this file.");
            }
            else if (inactiveCodes.Contains(itemCode.ToLowerInvariant()))
            {
                errors.Add("Material Code belongs to a deactivated part; use a different code.");
            }

            if (string.IsNullOrWhiteSpace(unit))
            {
                errors.Add("Unit is required.");
            }

            if (!string.IsNullOrWhiteSpace(branchName) && !branchLookup.Contains(branchName.ToLowerInvariant()))
            {
                errors.Add("Branch must exist when provided.");
            }

            // For an existing part a blank quantity leaves its stock alone; a number sets the stock held
            // at the row's branch to that figure (booked as an adjustment).
            if (!TryParseDecimal(rawValues["Opening Quantity"], out var quantity, isUpdate) || quantity < 0)
            {
                errors.Add(isUpdate ? "Opening Quantity must be a number of 0 or more, or blank to leave stock as it is." : "Opening Quantity must be a number of 0 or more.");
            }

            if (!TryParseDecimal(rawValues["Reorder Level"], out _, isUpdate))
            {
                errors.Add("Reorder Level must be numeric.");
            }

            if (!TryParseDecimal(rawValues["Unit Cost"], out _, true))
            {
                errors.Add("Unit Cost must be numeric.");
            }

            results.Add(new ImportPreviewRowResponse(record.RowNumber, rawValues, errors.Count == 0, errors) { Action = isUpdate ? "Update" : "Create" });
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

    internal static List<string> ParseCsvLine(string line)
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

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? BuildJoinedValue(params string?[] values)
    {
        var parts = values.Select(Normalize).Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
        return parts.Count == 0 ? null : string.Join(", ", parts);
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
            CanOverrideMaterialStock = permissions.CanOverrideMaterialStock,
        };

    // Every action here previews or commits a privileged bulk mutation, so each one carries the same
    // guard as the single-record controller for the entity it writes. The user import creates accounts
    // and grants roles, which is admin-only in UsersController; branches and sites are admin-only in
    // BranchesController and SitesController and stay that way here.
    private void EnsureCanImportUsers() => userAccessService.EnsureAdmin();

    private void EnsureCanImportMaterials() => userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageMaterials);

    private void EnsureCanImportBranches() => userAccessService.EnsureAdmin();

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

    private sealed record CsvRecord(int RowNumber, string[] Values);
}

public sealed record CsvUploadRequest(IFormFile File);

public sealed record ImportPreviewResponse(string ImportType, int TotalRows, IReadOnlyCollection<ImportPreviewRowResponse> Rows);

public sealed record ImportPreviewRowResponse(int RowNumber, IReadOnlyDictionary<string, string?> RawValues, bool IsValid, IReadOnlyCollection<string> Errors)
{
    /// <summary>Materials only: Create (a new part) or Update (the code is already in the catalogue).</summary>
    public string? Action { get; init; }
}

public sealed record ImportCommitResponse(int TotalRows, int SuccessfulRows, int FailedRows, int SkippedRows, IReadOnlyCollection<ImportCommitRowErrorResponse>? RowErrors = null);
public sealed record ImportCommitRowErrorResponse(int RowNumber, IReadOnlyCollection<string> Errors);
