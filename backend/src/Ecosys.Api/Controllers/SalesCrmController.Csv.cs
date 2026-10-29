using System.Globalization;
using System.Text;
using Ecosys.Api.Common;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// CSV in and out of the pipeline. Exports follow the same visibility as the screens. Imports map
/// columns by header name (any order, extra columns ignored), are previewed before anything is
/// saved, and create deals exactly as the deal form does: prospects and contacts are matched or
/// created the same way, and a row already in the pipeline is refused so re-running a file is safe.
/// </summary>
public sealed partial class SalesCrmController
{
    private static readonly string[] DealImportHeaders =
    [
        "Title", "Company", "ContactName", "ContactEmail", "ContactPhone", "Stage", "EstimatedValue", "Currency",
        "ExpectedCloseDate", "Owner", "Source", "ServiceType", "Location", "Description", "LostReason",
    ];

    [HttpGet("export/deals")]
    public async Task<IActionResult> ExportDeals([FromQuery] string? stage, [FromQuery] Guid? ownerUserId, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var scope = await OwnerScopeAsync(cancellationToken);
        var query = dbContext.SalesOpportunities.AsNoTracking().Include(x => x.Client)
            .Where(x => x.TenantId == TenantId && (scope == null || x.OwnerUserId == scope));
        if (ownerUserId.HasValue) query = query.Where(x => x.OwnerUserId == ownerUserId);
        if (!string.IsNullOrWhiteSpace(stage))
            query = string.Equals(stage, "Open", StringComparison.OrdinalIgnoreCase)
                ? query.Where(x => x.Stage != SalesStages.Won && x.Stage != SalesStages.Lost)
                : query.Where(x => x.Stage == SalesStages.Normalize(stage));
        var deals = await query.OrderBy(x => x.CreatedAt).ToListAsync(cancellationToken);

        return CsvFile("deals.csv",
            "Title,Company,Prospect,Contact,Email,Phone,Stage,Value,Currency,Probability,Expected Close,Owner,Source,Service Type,Location,Created,Won,Lost,Lost Reason",
            deals.Select(x => new object?[]
            {
                x.Title, x.Client?.ClientName ?? x.CompanyName, x.Client?.IsProspect == true ? "Yes" : "No", x.ContactName, x.ContactEmail, x.ContactPhone,
                x.Stage, x.EstimatedValue.ToString(CultureInfo.InvariantCulture), x.Currency, x.Probability, Day(x.ExpectedCloseDate), x.OwnerName,
                x.Source, x.ServiceType, x.Location, Day(x.CreatedAt), Day(x.WonAt), Day(x.LostAt), x.LostReason,
            }));
    }

    /// <summary>The people at clients and prospects; limited to the clients of one's own deals when the workspace shows own deals only.</summary>
    [HttpGet("export/contacts")]
    public async Task<IActionResult> ExportContacts(CancellationToken cancellationToken)
    {
        EnsureCanView();
        var scope = await OwnerScopeAsync(cancellationToken);
        var query = dbContext.ClientContacts.AsNoTracking().Include(x => x.Client)
            .Where(x => x.TenantId == TenantId && x.IsActive);
        if (scope.HasValue)
        {
            var ownClients = dbContext.SalesOpportunities.Where(x => x.TenantId == TenantId && x.OwnerUserId == scope && x.ClientId != null).Select(x => x.ClientId!.Value);
            query = query.Where(x => ownClients.Contains(x.ClientId));
        }
        var contacts = await query.OrderBy(x => x.Client!.ClientName).ThenByDescending(x => x.IsPrimary).ThenBy(x => x.FullName).ToListAsync(cancellationToken);

        return CsvFile("contacts.csv", "Client,Prospect,Name,Job Title,Role,Email,Phone,Main Contact",
            contacts.Select(x => new object?[]
            {
                x.Client?.ClientName, x.Client?.IsProspect == true ? "Yes" : "No", x.FullName, x.JobTitle, x.Role, x.Email, x.Phone, x.IsPrimary ? "Yes" : "No",
            }));
    }

    [HttpGet("import/deals/template")]
    public async Task<IActionResult> DealImportTemplate(CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(cancellationToken);
        return CsvFile("deals_import_template.csv", string.Join(',', DealImportHeaders),
        [
            ["Rooftop solar for warehouse", "Baraka Logistics", "Wanjiru Otieno", "wanjiru@baraka.co.ke", "+254700000200", "Qualified", "1250000", "KES",
                DateTime.UtcNow.AddMonths(2).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "", "Referral", "", "Mombasa Road", "20 kWp grid-tie system", ""],
        ]);
    }

    [HttpPost("import/deals/preview")]
    public async Task<ActionResult<ImportPreviewResponse>> PreviewDealImport([FromForm] CsvUploadRequest request, CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(cancellationToken);
        var rows = await ReadDealRowsAsync(request.File, cancellationToken);
        var context = await DealImportContext.LoadAsync(dbContext, rolePermissionService, TenantId, cancellationToken);
        var preview = rows.Select(row =>
        {
            var parsed = context.Parse(row.Values);
            var values = new Dictionary<string, string?>(row.Values, StringComparer.OrdinalIgnoreCase) { ["Result"] = parsed.Errors.Count == 0 ? parsed.ClientNote : null };
            return new ImportPreviewRowResponse(row.RowNumber, values, parsed.Errors.Count == 0, parsed.Errors);
        }).ToList();
        return Ok(new ImportPreviewResponse("Deals", preview.Count, preview));
    }

    [HttpPost("import/deals/commit")]
    public async Task<ActionResult<ImportCommitResponse>> CommitDealImport([FromForm] CsvUploadRequest request, CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(cancellationToken);
        var rows = await ReadDealRowsAsync(request.File, cancellationToken);
        var context = await DealImportContext.LoadAsync(dbContext, rolePermissionService, TenantId, cancellationToken);
        var importerName = await ResolveUserNameAsync(UserId, cancellationToken);
        var errors = new List<ImportCommitRowErrorResponse>();
        var created = 0;

        foreach (var row in rows)
        {
            var parsed = context.Parse(row.Values);
            if (parsed.Errors.Count > 0 || parsed.Request is null)
            {
                errors.Add(new ImportCommitRowErrorResponse(row.RowNumber, parsed.Errors));
                continue;
            }
            try
            {
                var opportunity = new SalesOpportunity { TenantId = TenantId, Stage = SalesStages.Lead };
                await ApplyAsync(opportunity, parsed.Request, cancellationToken);
                await ProspectClients.LinkAsync(dbContext, opportunity, null, cancellationToken);
                await LinkDealFieldsContactAsync(opportunity, cancellationToken);
                if (!parsed.Request.OwnerUserId.HasValue)
                {
                    // A matching lead assignment rule decides; otherwise the deal is the importer's.
                    var byRule = await salesOwnerResolver.PickByRuleAsync(TenantId, new SalesLeadFacts(opportunity.Source, opportunity.ServiceType, opportunity.Location), cancellationToken);
                    opportunity.OwnerUserId = byRule?.Id ?? UserId;
                    opportunity.OwnerName = byRule?.FullName ?? importerName;
                }
                opportunity.Stage = parsed.Stage;
                opportunity.Probability = SalesStages.DefaultProbability(parsed.Stage);
                var closedOn = opportunity.ExpectedCloseDate ?? DateTime.UtcNow;
                if (parsed.Stage == SalesStages.Won)
                {
                    opportunity.WonAt = closedOn;
                    await ProspectClients.PromoteAsync(dbContext, TenantId, opportunity.ClientId, cancellationToken);
                }
                if (parsed.Stage == SalesStages.Lost)
                {
                    opportunity.LostAt = closedOn;
                    opportunity.LostReason = parsed.LostReason;
                }
                dbContext.SalesOpportunities.Add(opportunity);
                await AddSystemActivityAsync(opportunity, "Imported from a spreadsheet.", cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                context.Remember(opportunity.Title, opportunity.CompanyName ?? opportunity.ContactName);
                created++;
            }
            catch (Exception exception) when (exception is Shared.Errors.BusinessRuleException or Shared.Errors.NotFoundException)
            {
                dbContext.ChangeTracker.Clear();
                errors.Add(new ImportCommitRowErrorResponse(row.RowNumber, [exception.Message]));
            }
        }

        await auditLogService.LogAsync(TenantId, UserId, "crm.deals.imported", nameof(SalesOpportunity), TenantId.ToString(), $"Imported {created} deal(s) from a spreadsheet; {errors.Count} row(s) skipped.", cancellationToken);
        return Ok(new ImportCommitResponse(rows.Count, created, errors.Count, 0, errors));
    }

    private sealed record DealRow(int RowNumber, IReadOnlyDictionary<string, string?> Values);

    /// <summary>Reads a CSV whose first row names the columns; header names are matched ignoring case and spaces.</summary>
    private static async Task<List<DealRow>> ReadDealRowsAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            throw new Shared.Errors.BusinessRuleException("Choose a CSV file to import.");
        if (!string.Equals(Path.GetExtension(file.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
            throw new Shared.Errors.BusinessRuleException("Only CSV files are supported.");

        using var reader = new StreamReader(file.OpenReadStream());
        var header = await reader.ReadLineAsync(cancellationToken);
        if (header is null)
            return [];
        static string Key(string value) => new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        var columns = ImportController.ParseCsvLine(header.TrimStart('﻿'))
            .Select(name => DealImportHeaders.FirstOrDefault(known => Key(known) == Key(name)))
            .ToList();
        if (!columns.Contains("Title"))
            throw new Shared.Errors.BusinessRuleException("The file needs a Title column. Download the template to see the columns.");

        var rows = new List<DealRow>();
        var lineNumber = 1;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (rows.Count >= 2000)
                throw new Shared.Errors.BusinessRuleException("Import at most 2,000 deals at a time.");
            var values = ImportController.ParseCsvLine(line);
            var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var known in DealImportHeaders) row[known] = null;
            for (var i = 0; i < columns.Count && i < values.Count; i++)
                if (columns[i] is { } name) row[name] = string.IsNullOrWhiteSpace(values[i]) ? null : values[i].Trim();
            rows.Add(new DealRow(lineNumber, row));
        }
        return rows;
    }

    /// <summary>Everything a row is checked against, loaded once per file.</summary>
    private sealed class DealImportContext
    {
        private readonly IReadOnlyList<SalesOwnerOption> owners;
        private readonly IReadOnlyList<(string Key, string Name, bool IsProspect)> clients;
        private readonly HashSet<string> existingDeals;

        private DealImportContext(IReadOnlyList<SalesOwnerOption> owners, IReadOnlyList<(string, string, bool)> clients, HashSet<string> existingDeals, IReadOnlyDictionary<Guid, string> ownerEmails)
        {
            this.owners = owners;
            this.clients = clients;
            this.existingDeals = existingDeals;
            OwnerEmails = ownerEmails;
        }

        private IReadOnlyDictionary<Guid, string> OwnerEmails { get; }

        public static async Task<DealImportContext> LoadAsync(
            Infrastructure.Data.AppDbContext db, Infrastructure.Services.IRolePermissionService rolePermissions, Guid tenantId, CancellationToken cancellationToken)
        {
            var owners = await SalesCrmAccess.ListEligibleOwnersAsync(db, rolePermissions, tenantId, cancellationToken);
            var ownerIds = owners.Select(x => x.Id).ToList();
            var emails = await db.Users.AsNoTracking().Where(x => x.TenantId == tenantId && ownerIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Email.ToLowerInvariant(), cancellationToken);
            var clients = (await db.Clients.AsNoTracking().Where(x => x.TenantId == tenantId && x.IsActive)
                    .Select(x => new { x.ClientName, x.IsProspect }).ToListAsync(cancellationToken))
                .Select(x => (ClientMatching.NameKey(x.ClientName), x.ClientName, x.IsProspect)).ToList();
            var deals = (await db.SalesOpportunities.AsNoTracking().Where(x => x.TenantId == tenantId)
                    .Select(x => new { x.Title, Party = x.Client != null ? x.Client.ClientName : x.CompanyName ?? x.ContactName }).ToListAsync(cancellationToken))
                .Select(x => DealKey(x.Title, x.Party)).ToHashSet();
            return new DealImportContext(owners, clients, deals, emails);
        }

        public void Remember(string title, string? party) => existingDeals.Add(DealKey(title, party));

        private static string DealKey(string title, string? party) => $"{ClientMatching.NameKey(title)}|{ClientMatching.NameKey(party)}";

        public ParsedDealRow Parse(IReadOnlyDictionary<string, string?> row)
        {
            var errors = new List<string>();
            var title = row["Title"];
            var company = row["Company"];
            var contact = row["ContactName"];
            if (string.IsNullOrWhiteSpace(title)) errors.Add("Title is required.");
            if (string.IsNullOrWhiteSpace(company) && string.IsNullOrWhiteSpace(contact)) errors.Add("Give a Company or a ContactName.");

            var stage = SalesStages.Lead;
            if (!string.IsNullOrWhiteSpace(row["Stage"]))
            {
                var match = SalesStages.All.FirstOrDefault(x => string.Equals(x, row["Stage"]!.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));
                if (match is null) errors.Add($"Unknown stage '{row["Stage"]}'. Use one of: {string.Join(", ", SalesStages.All)}.");
                else stage = match;
            }
            if (stage == SalesStages.Lost && string.IsNullOrWhiteSpace(row["LostReason"])) errors.Add("A Lost deal needs a LostReason.");

            decimal value = 0;
            if (!string.IsNullOrWhiteSpace(row["EstimatedValue"])
                && !decimal.TryParse(row["EstimatedValue"]!.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out value))
                errors.Add($"EstimatedValue '{row["EstimatedValue"]}' is not a number.");
            if (value < 0) errors.Add("EstimatedValue cannot be negative.");

            DateTime? close = null;
            if (!string.IsNullOrWhiteSpace(row["ExpectedCloseDate"]))
            {
                if (DateTime.TryParseExact(row["ExpectedCloseDate"], ["yyyy-MM-dd", "d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                    close = DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
                else errors.Add($"ExpectedCloseDate '{row["ExpectedCloseDate"]}' is not a date (use 2026-12-31).");
            }

            Guid? ownerId = null;
            if (!string.IsNullOrWhiteSpace(row["Owner"]))
            {
                var wanted = row["Owner"]!.Trim();
                var owner = owners.FirstOrDefault(x => OwnerEmails.TryGetValue(x.Id, out var email) && email == wanted.ToLowerInvariant())
                    ?? owners.FirstOrDefault(x => string.Equals(x.FullName, wanted, StringComparison.OrdinalIgnoreCase));
                if (owner is null) errors.Add($"Owner '{wanted}' is not someone who can manage the pipeline.");
                else ownerId = owner.Id;
            }

            var party = !string.IsNullOrWhiteSpace(company) ? company : contact;
            if (!string.IsNullOrWhiteSpace(title) && existingDeals.Contains(DealKey(title, party)))
                errors.Add("Already in the pipeline (same title and company).");

            var partyKey = ClientMatching.NameKey(party);
            var existing = clients.FirstOrDefault(x => x.Key == partyKey && partyKey.Length > 0);
            var clientNote = existing.Name is null ? "New prospect" : existing.IsProspect ? $"Existing prospect: {existing.Name}" : $"Existing client: {existing.Name}";

            var request = errors.Count > 0 ? null : new UpsertSalesOpportunityRequest(
                title!.Trim(), null, null, company, contact, row["ContactEmail"], row["ContactPhone"], row["Location"], row["Source"],
                row["ServiceType"], row["Description"], value, row["Currency"], null, close, ownerId);
            return new ParsedDealRow(request, stage, row["LostReason"], errors, clientNote);
        }
    }

    private sealed record ParsedDealRow(UpsertSalesOpportunityRequest? Request, string Stage, string? LostReason, IReadOnlyList<string> Errors, string ClientNote);

    private static string? Day(DateTime? value) => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private FileContentResult CsvFile(string fileName, string header, IEnumerable<object?[]> rows)
    {
        var csv = new StringBuilder();
        csv.AppendLine(header);
        foreach (var row in rows)
            csv.AppendLine(Csv.Row(row));
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv", fileName);
    }
}
