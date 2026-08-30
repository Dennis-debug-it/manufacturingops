using System.ComponentModel.DataAnnotations;
using Ecosys.Api.Common;
using Ecosys.Domain.Entities;
using Ecosys.Domain.Enums;
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
[Route("api/clients")]
public sealed class ClientsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var normalizedStatus = NormalizeStatusFilter(status);
        var normalizedSearch = NormalizeSearch(search);

        var query = dbContext.Clients
            .Include(x => x.SlaDefinition)
            .Where(x => x.TenantId == TenantId);

        if (normalizedStatus == "active")
        {
            query = query.Where(x => x.IsActive);
        }
        else if (normalizedStatus == "inactive")
        {
            query = query.Where(x => !x.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var searchLower = normalizedSearch.ToLower();
            query = query.Where(x =>
                x.ClientName.ToLower().Contains(searchLower) ||
                (x.ContactPerson != null && x.ContactPerson.ToLower().Contains(searchLower)) ||
                (x.Email != null && x.Email.ToLower().Contains(searchLower)) ||
                (x.Phone != null && x.Phone.ToLower().Contains(searchLower)) ||
                (x.ContactPhone != null && x.ContactPhone.ToLower().Contains(searchLower)) ||
                (x.Location != null && x.Location.ToLower().Contains(searchLower)));
        }

        var ordered = query.OrderBy(x => x.ClientName);

        if (QueryablePagingExtensions.IsPagingRequested(page, pageSize))
        {
            var pagedClients = await ordered.ToPagedResultAsync(page, pageSize, cancellationToken);
            var pageCounts = await BuildClientCountsAsync(pagedClients.Items.Select(x => x.Id).ToList(), cancellationToken);
            return Ok(pagedClients.Map(client => Map(client, pageCounts)));
        }

        var clients = await ordered.ToListAsync(cancellationToken);
        var counts = await BuildClientCountsAsync(clients.Select(x => x.Id).ToList(), cancellationToken);

        return Ok(clients.Select(client => Map(client, counts)).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClientResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var client = await dbContext.Clients
            .Include(x => x.SlaDefinition)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Client was not found.");

        var counts = await BuildClientCountsAsync([client.Id], cancellationToken);
        return Ok(Map(client, counts));
    }

    [HttpGet("{id:guid}/summary")]
    public async Task<ActionResult<ClientSummaryResponse>> GetSummary(Guid id, CancellationToken cancellationToken)
    {
        var client = await dbContext.Clients
            .Include(x => x.SlaDefinition)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Client was not found.");

        var counts = await BuildClientCountsAsync([client.Id], cancellationToken);
        var sites = await dbContext.Sites
            .Where(x => x.TenantId == TenantId && x.ClientId == id)
            .OrderBy(x => x.SiteName)
            .ToListAsync(cancellationToken);

        var siteIds = sites.Select(x => x.Id).ToList();
        var systemCounts = await dbContext.Systems
            .Where(x => x.TenantId == TenantId && siteIds.Contains(x.SiteId))
            .GroupBy(x => x.SiteId)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        var assetCounts = await dbContext.Assets
            .Where(x => x.TenantId == TenantId && x.SiteId.HasValue && siteIds.Contains(x.SiteId.Value) && x.Status != "Inactive")
            .GroupBy(x => x.SiteId!.Value)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        var openWorkOrderCounts = await dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId && x.SiteId.HasValue && siteIds.Contains(x.SiteId.Value) && x.Status != "Completed" && x.Status != "Cancelled" && x.Status != "Closed")
            .GroupBy(x => x.SiteId!.Value)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        return Ok(new ClientSummaryResponse(
            Map(client, counts),
            GetCount(counts.SiteCounts, client.Id),
            GetCount(counts.SystemCounts, client.Id),
            GetCount(counts.AssetCounts, client.Id),
            GetCount(counts.OpenWorkOrderCounts, client.Id),
            GetCount(counts.UpcomingPmCounts, client.Id),
            sites.Select(site => new ClientSiteSummaryResponse(
                site.Id,
                site.SiteCode,
                site.SiteName,
                site.SiteType,
                BuildSiteLocation(site),
                site.ContactPerson,
                GetCount(systemCounts, site.Id),
                GetCount(assetCounts, site.Id),
                GetCount(openWorkOrderCounts, site.Id),
                site.Status,
                site.CreatedAt,
                site.UpdatedAt)).ToList()));
    }

    [HttpPost]
    public async Task<ActionResult<ClientResponse>> Create([FromBody] UpsertClientRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManageClients();
        Validate(request);
        await EnsureUniqueNameAsync(request.ClientName, null, cancellationToken);

        var client = new Client
        {
            TenantId = TenantId,
            ClientName = request.ClientName.Trim(),
            ClientType = request.ClientType?.Trim(),
            Email = request.Email?.Trim(),
            Phone = request.Phone?.Trim(),
            Location = request.Location?.Trim(),
            ContactPerson = request.ContactPerson?.Trim(),
            ContactPhone = request.ContactPhone?.Trim(),
            SlaDefinitionId = request.SlaDefinitionId,
            ActiveStartDate = request.ActiveStartDate,
            ActiveDurationValue = request.ActiveDurationValue,
            ActiveDurationUnit = NormalizeDurationUnit(request.ActiveDurationUnit),
            ActiveEndDate = CalculateActiveEndDate(request.ActiveStartDate, request.ActiveDurationValue, request.ActiveDurationUnit),
            Notes = request.Notes?.Trim(),
            IsActive = request.IsActive ?? true
        };

        await EnsureSlaDefinitionExistsAsync(client.SlaDefinitionId, cancellationToken);

        dbContext.Clients.Add(client);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Client created",
            nameof(Client),
            client.Id.ToString(),
            $"Created client '{client.ClientName}'.",
            cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = client.Id }, Map(client));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ClientResponse>> Update(Guid id, [FromBody] UpsertClientRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManageClients();
        Validate(request);
        var client = await dbContext.Clients.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Client was not found.");

        await EnsureUniqueNameAsync(request.ClientName, client.Id, cancellationToken);

        client.ClientName = request.ClientName.Trim();
        client.ClientType = request.ClientType?.Trim();
        client.Email = request.Email?.Trim();
        client.Phone = request.Phone?.Trim();
        client.Location = request.Location?.Trim();
        client.ContactPerson = request.ContactPerson?.Trim();
        client.ContactPhone = request.ContactPhone?.Trim();
        client.SlaDefinitionId = request.SlaDefinitionId;
        client.ActiveStartDate = request.ActiveStartDate;
        client.ActiveDurationValue = request.ActiveDurationValue;
        client.ActiveDurationUnit = NormalizeDurationUnit(request.ActiveDurationUnit);
        client.ActiveEndDate = CalculateActiveEndDate(request.ActiveStartDate, request.ActiveDurationValue, request.ActiveDurationUnit);
        client.Notes = request.Notes?.Trim();
        client.IsActive = request.IsActive ?? client.IsActive;

        await EnsureSlaDefinitionExistsAsync(client.SlaDefinitionId, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Client updated",
            nameof(Client),
            client.Id.ToString(),
            $"Updated client '{client.ClientName}'.",
            cancellationToken);

        return Ok(Map(client));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanManageClients();

        var client = await dbContext.Clients.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Client was not found.");

        await DeactivateClientAsync(client, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}/permanent")]
    public async Task<IActionResult> DeletePermanent(Guid id, CancellationToken cancellationToken)
    {
        // A permanent delete destroys history rather than archiving it, so it is admin-only
        // regardless of who holds the master-data permission that guards the other mutations.
        userAccessService.EnsureAdmin();

        var client = await dbContext.Clients.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Client was not found.");

        var blockers = await GetDeletionBlockersAsync(client, cancellationToken);
        if (!blockers.CanDelete)
        {
            // 409 rather than 400: the request is well formed, it is the current state of the
            // client's history that forbids it. This mirrors what PostgreSQL would raise (23503)
            // if a Restrict child slipped past this check.
            throw new ConflictException(blockers.BlockingReason!);
        }

        dbContext.Clients.Remove(client);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Client permanently deleted",
            nameof(Client),
            client.Id.ToString(),
            $"Permanently deleted client '{client.ClientName}'.",
            cancellationToken);

        return NoContent();
    }

    [HttpPatch("{id:guid}/activate")]
    public async Task<ActionResult<ClientResponse>> Activate(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanManageClients();

        var client = await dbContext.Clients.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Client was not found.");

        if (client.IsActive)
        {
            var clientCounts = await BuildClientCountsAsync([client.Id], cancellationToken);
            return Ok(Map(client, clientCounts));
        }

        client.IsActive = true;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "ClientActivated",
            nameof(Client),
            client.Id.ToString(),
            $"Client '{client.ClientName}' was activated.",
            cancellationToken);

        var clientCountsAfterActivate = await BuildClientCountsAsync([client.Id], cancellationToken);
        return Ok(Map(client, clientCountsAfterActivate));
    }

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<ActionResult<ClientResponse>> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanManageClients();

        var client = await dbContext.Clients.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Client was not found.");

        if (!client.IsActive)
        {
            var clientCounts = await BuildClientCountsAsync([client.Id], cancellationToken);
            return Ok(Map(client, clientCounts));
        }

        await DeactivateClientAsync(client, cancellationToken);
        var clientCountsAfterDeactivate = await BuildClientCountsAsync([client.Id], cancellationToken);
        return Ok(Map(client, clientCountsAfterDeactivate));
    }

    [HttpPost("{id:guid}/validate-deletion")]
    public async Task<ActionResult<ClientDeletionCheckResponse>> ValidateDeletion(Guid id, CancellationToken cancellationToken)
    {
        var client = await dbContext.Clients.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Client was not found.");

        var blockers = await GetDeletionBlockersAsync(client, cancellationToken);

        return Ok(new ClientDeletionCheckResponse(
            blockers.CanDelete,
            blockers.BlockingReason,
            blockers.WorkOrderCount,
            blockers.InvoiceCount,
            blockers.ContractCount,
            blockers.AssetCount,
            blockers.SiteCount));
    }

    // There is no client-specific capability. Clients are master data alongside assets and sites,
    // and the clients importer already gates on CanManageAssets, so the single-record mutations use
    // the same constant; splitting them would let a bulk import do what a single edit cannot.
    private void EnsureCanManageClients() => userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageAssets);

    private async Task DeactivateClientAsync(Client client, CancellationToken cancellationToken)
    {
        client.IsActive = false;

        var linkedSites = await dbContext.Sites
            .Where(x => x.TenantId == TenantId && x.ClientId == client.Id && x.Status != "Inactive")
            .ToListAsync(cancellationToken);

        foreach (var site in linkedSites)
        {
            site.Status = "Inactive";
        }

        var linkedAssets = await dbContext.Assets
            .Where(x => x.TenantId == TenantId && x.ClientId == client.Id && x.Status != "Inactive")
            .ToListAsync(cancellationToken);

        foreach (var asset in linkedAssets)
        {
            asset.Status = "Inactive";
            asset.AutoSchedulePm = false;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "ClientDeactivated",
            nameof(Client),
            client.Id.ToString(),
            $"Client '{client.ClientName}' was deactivated.",
            cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "LinkedSitesDeactivated",
            nameof(Client),
            client.Id.ToString(),
            $"Deactivated {linkedSites.Count} linked site(s) for client '{client.ClientName}'.",
            cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "LinkedAssetsDeactivated",
            nameof(Client),
            client.Id.ToString(),
            $"Deactivated {linkedAssets.Count} linked asset(s) for client '{client.ClientName}'.",
            cancellationToken);
    }

    private static void Validate(UpsertClientRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ClientName))
        {
            throw new BusinessRuleException("Client name is required.");
        }

        if (request.ActiveDurationValue.HasValue && request.ActiveDurationValue.Value <= 0)
        {
            throw new BusinessRuleException("Active duration must be positive.");
        }

        if (request.ActiveDurationValue.HasValue && !request.ActiveStartDate.HasValue)
        {
            throw new BusinessRuleException("Active start date is required when an active duration is provided.");
        }
    }

    private async Task EnsureUniqueNameAsync(string clientName, Guid? currentId, CancellationToken cancellationToken)
    {
        var normalizedName = clientName.Trim().ToLowerInvariant();
        var exists = await dbContext.Clients.AnyAsync(
            x => x.TenantId == TenantId
                && x.IsActive
                && x.Id != currentId
                && x.ClientName.ToLower() == normalizedName,
            cancellationToken);

        if (exists)
        {
            throw new BusinessRuleException("Client name already exists for this tenant.");
        }
    }

    private async Task EnsureSlaDefinitionExistsAsync(Guid? slaDefinitionId, CancellationToken cancellationToken)
    {
        if (!slaDefinitionId.HasValue)
        {
            return;
        }

        var exists = await dbContext.SlaDefinitions.AnyAsync(
            x => x.TenantId == TenantId && x.IsActive && x.Id == slaDefinitionId.Value,
            cancellationToken);

        if (!exists)
        {
            throw new BusinessRuleException("SLA definition was not found for this tenant.");
        }
    }

    private async Task<ClientCountBundle> BuildClientCountsAsync(IReadOnlyCollection<Guid> clientIds, CancellationToken cancellationToken)
    {
        if (clientIds.Count == 0)
        {
            return new ClientCountBundle(EmptyCounts, EmptyCounts, EmptyCounts, EmptyCounts, EmptyCounts);
        }

        var siteCounts = await dbContext.Sites
            .Where(x => x.TenantId == TenantId && clientIds.Contains(x.ClientId))
            .GroupBy(x => x.ClientId)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        var systemCounts = await dbContext.Systems
            .Where(x => x.TenantId == TenantId && clientIds.Contains(x.ClientId))
            .GroupBy(x => x.ClientId)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        var assetCounts = await dbContext.Assets
            .Where(x => x.TenantId == TenantId && clientIds.Contains(x.ClientId) && x.Status != "Inactive")
            .GroupBy(x => x.ClientId)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        var openWorkOrderCounts = await dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId && clientIds.Contains(x.ClientId) && x.Status != "Completed" && x.Status != "Cancelled" && x.Status != "Closed")
            .GroupBy(x => x.ClientId)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        var upcomingThreshold = DateTime.UtcNow.Date.AddDays(30);
        var upcomingPmCounts = await dbContext.Assets
            .Where(x => x.TenantId == TenantId
                && clientIds.Contains(x.ClientId)
                && x.Status != "Inactive"
                && x.NextPmDate.HasValue
                && x.NextPmDate.Value.Date >= DateTime.UtcNow.Date
                && x.NextPmDate.Value.Date <= upcomingThreshold)
            .GroupBy(x => x.ClientId)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        return new ClientCountBundle(siteCounts, systemCounts, assetCounts, openWorkOrderCounts, upcomingPmCounts);
    }

    private static int GetCount(IReadOnlyDictionary<Guid, int> counts, Guid key) =>
        counts.TryGetValue(key, out var count) ? count : 0;

    private static string? BuildSiteLocation(Site site)
    {
        var parts = new[] { site.TownCity, site.County, site.Country }.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static ClientResponse Map(Client client, ClientCountBundle? counts = null) =>
        new(
            client.Id,
            client.ClientName,
            client.ClientType,
            client.Email,
            client.Phone,
            client.Location,
            client.ContactPerson,
            client.ContactPhone,
            client.SlaDefinitionId,
            client.SlaDefinition?.PlanName,
            client.ActiveStartDate,
            client.ActiveDurationValue,
            client.ActiveDurationUnit,
            client.ActiveEndDate,
            ComputeActiveStatus(client),
            client.Notes,
            client.IsActive,
            client.CreatedAt,
            GetCount(counts?.SiteCounts ?? EmptyCounts, client.Id),
            GetCount(counts?.SystemCounts ?? EmptyCounts, client.Id),
            GetCount(counts?.AssetCounts ?? EmptyCounts, client.Id),
            GetCount(counts?.OpenWorkOrderCounts ?? EmptyCounts, client.Id));

    private static readonly IReadOnlyDictionary<Guid, int> EmptyCounts = new Dictionary<Guid, int>();

    private static string? NormalizeDurationUnit(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "day" or "days" => "days",
            "month" or "months" => "months",
            "year" or "years" => "years",
            _ => throw new BusinessRuleException("Active duration unit must be days, months, or years.")
        };
    }

    private static DateTime? CalculateActiveEndDate(DateTime? startDate, int? durationValue, string? durationUnit)
    {
        if (!durationValue.HasValue)
        {
            return null;
        }

        if (!startDate.HasValue)
        {
            throw new BusinessRuleException("Active start date is required when an active duration is provided.");
        }

        if (durationValue.Value <= 0)
        {
            throw new BusinessRuleException("Active duration must be positive.");
        }

        return NormalizeDurationUnit(durationUnit) switch
        {
            "days" => startDate.Value.AddDays(durationValue.Value),
            "months" => startDate.Value.AddMonths(durationValue.Value),
            "years" => startDate.Value.AddYears(durationValue.Value),
            _ => throw new BusinessRuleException("Active duration unit must be days, months, or years.")
        };
    }

    private static string ComputeActiveStatus(Client client)
    {
        if (!client.IsActive)
        {
            return "inactive";
        }

        if (!client.ActiveEndDate.HasValue)
        {
            return "active";
        }

        var today = DateTime.UtcNow.Date;
        var endDate = client.ActiveEndDate.Value.Date;
        if (endDate < today)
        {
            return "expired";
        }

        return endDate <= today.AddDays(30) ? "expiring" : "active";
    }

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

    // Work orders, assets, systems, contracts, invoices, payments, credit notes and quotations
    // all hold a Restrict foreign key to Client, so a permanent delete is only possible for a
    // client that never accumulated operational history. Sites, portal users and preferential
    // rates cascade and do not block.
    private async Task<ClientDeletionBlockers> GetDeletionBlockersAsync(Client client, CancellationToken cancellationToken)
    {
        return new ClientDeletionBlockers(
            await dbContext.WorkOrders.CountAsync(x => x.TenantId == TenantId && x.ClientId == client.Id, cancellationToken),
            await dbContext.Assets.CountAsync(x => x.TenantId == TenantId && x.ClientId == client.Id, cancellationToken),
            await dbContext.Systems.CountAsync(x => x.TenantId == TenantId && x.ClientId == client.Id, cancellationToken),
            await dbContext.Contracts.CountAsync(x => x.TenantId == TenantId && x.ClientId == client.Id, cancellationToken),
            await dbContext.TenantInvoices.CountAsync(x => x.TenantId == TenantId && x.ClientId == client.Id, cancellationToken),
            await dbContext.TenantPayments.CountAsync(x => x.TenantId == TenantId && x.ClientId == client.Id, cancellationToken),
            await dbContext.CreditNotes.CountAsync(x => x.TenantId == TenantId && x.ClientId == client.Id, cancellationToken),
            await dbContext.TenantQuotations.CountAsync(x => x.TenantId == TenantId && x.ClientId == client.Id, cancellationToken),
            await dbContext.Sites.CountAsync(x => x.TenantId == TenantId && x.ClientId == client.Id, cancellationToken));
    }
}

sealed record ClientDeletionBlockers(
    int WorkOrderCount,
    int AssetCount,
    int SystemCount,
    int ContractCount,
    int InvoiceCount,
    int PaymentCount,
    int CreditNoteCount,
    int QuotationCount,
    int SiteCount)
{
    public bool CanDelete =>
        WorkOrderCount == 0
        && AssetCount == 0
        && SystemCount == 0
        && ContractCount == 0
        && InvoiceCount == 0
        && PaymentCount == 0
        && CreditNoteCount == 0
        && QuotationCount == 0;

    public string? BlockingReason
    {
        get
        {
            if (CanDelete)
            {
                return null;
            }

            var parts = new List<string>();
            Add(parts, WorkOrderCount, "work order");
            Add(parts, AssetCount, "asset");
            Add(parts, SystemCount, "system");
            Add(parts, ContractCount, "contract");
            Add(parts, InvoiceCount, "invoice");
            Add(parts, PaymentCount, "payment");
            Add(parts, CreditNoteCount, "credit note");
            Add(parts, QuotationCount, "quotation");

            return $"This client has {string.Join(", ", parts)} on record and cannot be permanently deleted. Archive it instead to retain the history.";
        }
    }

    private static void Add(List<string> parts, int count, string noun)
    {
        if (count > 0)
        {
            parts.Add($"{count} {noun}{(count == 1 ? string.Empty : "s")}");
        }
    }
}

public sealed record UpsertClientRequest(
    [StringLength(200)] string ClientName,
    [StringLength(100)] string? ClientType,
    [StringLength(256)] string? Email,
    [StringLength(40)] string? Phone,
    [StringLength(200)] string? Location,
    [StringLength(200)] string? ContactPerson,
    [StringLength(40)] string? ContactPhone,
    Guid? SlaDefinitionId,
    DateTime? ActiveStartDate,
    int? ActiveDurationValue,
    [StringLength(20)] string? ActiveDurationUnit,
    [StringLength(2000)] string? Notes,
    bool? IsActive);

public sealed record ClientResponse(
    Guid Id,
    string ClientName,
    string? ClientType,
    string? Email,
    string? Phone,
    string? Location,
    string? ContactPerson,
    string? ContactPhone,
    Guid? SlaDefinitionId,
    string? SlaDefinitionName,
    DateTime? ActiveStartDate,
    int? ActiveDurationValue,
    string? ActiveDurationUnit,
    DateTime? ActiveEndDate,
    string ActiveStatus,
    string? Notes,
    bool IsActive,
    DateTime CreatedAt,
    int SiteCount,
    int SystemCount,
    int AssetCount,
    int OpenWorkOrderCount);

public sealed record ClientSummaryResponse(
    ClientResponse Client,
    int SiteCount,
    int SystemCount,
    int AssetCount,
    int OpenWorkOrderCount,
    int UpcomingPmCount,
    IReadOnlyCollection<ClientSiteSummaryResponse> Sites);

public sealed record ClientSiteSummaryResponse(
    Guid SiteId,
    string SiteCode,
    string SiteName,
    string SiteType,
    string? Location,
    string? ContactPerson,
    int SystemCount,
    int AssetCount,
    int OpenWorkOrderCount,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

sealed record ClientCountBundle(
    IReadOnlyDictionary<Guid, int> SiteCounts,
    IReadOnlyDictionary<Guid, int> SystemCounts,
    IReadOnlyDictionary<Guid, int> AssetCounts,
    IReadOnlyDictionary<Guid, int> OpenWorkOrderCounts,
    IReadOnlyDictionary<Guid, int> UpcomingPmCounts);

public sealed record ClientDeletionCheckResponse(
    bool CanDelete,
    string? BlockingReason,
    int WorkOrderCount,
    int InvoiceCount,
    int ContractCount,
    int AssetCount,
    int SiteCount);
