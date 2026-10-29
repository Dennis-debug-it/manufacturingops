using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// Who may use the sales CRM: the <see cref="PermissionNames.CrmView"/> and
/// <see cref="PermissionNames.CrmManage"/> capabilities, granted per role in the role matrix (and
/// adjustable per user or permission group like any other capability). Admin holds both.
/// </summary>
internal static class SalesCrmAccess
{
    public static bool CanManage(ITenantContext tenantContext) =>
        tenantContext.IsAdmin || tenantContext.HasPermission(PermissionNames.CrmManage);

    public static bool CanView(ITenantContext tenantContext) =>
        CanManage(tenantContext) || tenantContext.HasPermission(PermissionNames.CrmView);

    /// <summary>The active users who can manage the pipeline, and so can own a deal or RFP/RFQ.</summary>
    public static async Task<IReadOnlyList<SalesOwnerOption>> ListEligibleOwnersAsync(
        AppDbContext dbContext, IRolePermissionService rolePermissionService, Guid tenantId, CancellationToken cancellationToken) =>
        (await SalesOwnerDirectory.ListAsync(dbContext, rolePermissionService, tenantId, cancellationToken))
            .Select(x => new SalesOwnerOption(x.Id, x.FullName, x.Role))
            .ToList();

    /// <summary>Resolves an owner for a deal or tender: an active user of this tenant who can manage the pipeline.</summary>
    public static async Task<string> ResolveOwnerNameAsync(
        AppDbContext dbContext, IRolePermissionService rolePermissionService, Guid tenantId, Guid ownerUserId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Users.AnyAsync(x => x.TenantId == tenantId && x.Id == ownerUserId, cancellationToken))
            throw new NotFoundException("Owner not found.");

        var eligible = await ListEligibleOwnersAsync(dbContext, rolePermissionService, tenantId, cancellationToken);
        return eligible.FirstOrDefault(x => x.Id == ownerUserId)?.FullName
            ?? throw new BusinessRuleException("The owner must be an active user who can manage the sales pipeline.");
    }
}

/// <summary>
/// Converts deal and tender values into the tenant's reporting currency (<c>Tenant.Currency</c>)
/// using the tenant's own exchange rates — the same table Finance › Exchange Rates maintains, so
/// one rate is entered once and used everywhere. The latest rate already in effect wins, and a
/// rate entered either way round (USD→KES or KES→USD) is used. A currency with no rate converts
/// to null: totals leave it out and say so instead of adding foreign amounts as if they were local.
/// </summary>
internal sealed class SalesCurrencyConverter
{
    private readonly Dictionary<string, SalesCurrencyRate> _rates;

    private SalesCurrencyConverter(string reportingCurrency, Dictionary<string, SalesCurrencyRate> rates)
    {
        ReportingCurrency = reportingCurrency;
        _rates = rates;
    }

    public string ReportingCurrency { get; }

    public IReadOnlyCollection<SalesCurrencyRate> Rates => _rates.Values.OrderBy(x => x.Currency).ToList();

    public static async Task<SalesCurrencyConverter> LoadAsync(AppDbContext dbContext, Guid tenantId, CancellationToken cancellationToken)
    {
        var configured = await dbContext.Tenants
            .AsNoTracking()
            .Where(x => x.Id == tenantId)
            .Select(x => x.Currency)
            .FirstOrDefaultAsync(cancellationToken);
        var reporting = Normalize(configured);
        if (reporting.Length == 0) reporting = "KES";

        var now = DateTime.UtcNow;
        var rows = await dbContext.ExchangeRates
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.EffectiveFrom <= now && x.Rate > 0
                && (x.BaseCurrency == reporting || x.QuoteCurrency == reporting))
            .ToListAsync(cancellationToken);

        var rates = rows
            .Where(x => x.BaseCurrency != x.QuoteCurrency)
            .Select(x => x.QuoteCurrency == reporting
                // 1 USD = 129 KES: a USD amount times 129 is KES.
                ? new SalesCurrencyRate(x.BaseCurrency, x.Rate, x.EffectiveFrom, x.CreatedAt, x.UpdatedByName)
                // Entered the other way round (1 KES = 0.0077 USD): invert it.
                : new SalesCurrencyRate(x.QuoteCurrency, 1m / x.Rate, x.EffectiveFrom, x.CreatedAt, x.UpdatedByName))
            .GroupBy(x => x.Currency)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.EnteredAt).First(),
                StringComparer.OrdinalIgnoreCase);

        return new SalesCurrencyConverter(reporting, rates);
    }

    public bool CanConvert(string currency) =>
        string.Equals(Normalize(currency), ReportingCurrency, StringComparison.Ordinal) || _rates.ContainsKey(Normalize(currency));

    public decimal? Convert(decimal amount, string currency)
    {
        var code = Normalize(currency);
        if (code == ReportingCurrency) return amount;
        return _rates.TryGetValue(code, out var rate) ? Math.Round(amount * rate.RateToReporting, 2) : null;
    }

    public static string Normalize(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();
}

/// <summary>How many units of the reporting currency one unit of <see cref="Currency"/> is worth.</summary>
public sealed record SalesCurrencyRate(string Currency, decimal RateToReporting, DateTime EffectiveFrom, DateTime EnteredAt, string UpdatedByName);

/// <summary>
/// Keeps a deal in step with the quotations raised from it, whether the client answers in the
/// portal or staff record the answer. Accepting a quotation wins the deal at the quoted amount; a
/// declined one leaves the deal open (a revised quote may still win it) with a follow-up to find
/// out why. A deal already closed is never reopened or flipped; the answer is only logged on it.
/// Changes are added to the context; the caller saves.
/// </summary>
internal static class SalesQuotationSync
{
    public static async Task OnAnsweredAsync(
        AppDbContext dbContext, TenantQuotation quote, bool accepted, string? reason, Guid? actorUserId, string? actorName, string via,
        CancellationToken cancellationToken)
    {
        if (!quote.SalesOpportunityId.HasValue) return;
        var opportunity = await dbContext.SalesOpportunities
            .SingleOrDefaultAsync(x => x.TenantId == quote.TenantId && x.Id == quote.SalesOpportunityId.Value, cancellationToken);
        if (opportunity is null) return;

        var now = DateTime.UtcNow;
        var amount = $"{quote.Currency} {quote.TotalAmount:N2}";
        string summary;
        if (accepted)
        {
            summary = $"Quotation {quote.QuoteNumber} ({amount}) accepted {via}.";
            if (!SalesStages.IsClosed(opportunity.Stage))
            {
                opportunity.Stage = SalesStages.Won;
                opportunity.Probability = SalesStages.DefaultProbability(SalesStages.Won);
                opportunity.WonAt = now;
                opportunity.EstimatedValue = quote.TotalAmount;
                opportunity.Currency = quote.Currency;
                summary += " Deal marked Won.";
                await ProspectClients.PromoteAsync(dbContext, opportunity.TenantId, opportunity.ClientId, cancellationToken);
            }
        }
        else
        {
            summary = $"Quotation {quote.QuoteNumber} ({amount}) declined {via}" + (string.IsNullOrWhiteSpace(reason) ? "." : $": {reason.Trim()}");
            if (!SalesStages.IsClosed(opportunity.Stage))
            {
                dbContext.SalesActivities.Add(new SalesActivity
                {
                    TenantId = opportunity.TenantId,
                    OpportunityId = opportunity.Id,
                    Type = "FollowUp",
                    Summary = $"Quotation {quote.QuoteNumber} was declined: find out why, and whether a revised quote would win it.",
                    OccurredAt = now,
                    DueAt = now.AddDays(1),
                    LoggedByUserId = actorUserId,
                    LoggedByName = actorName,
                });
            }
        }

        dbContext.SalesActivities.Add(new SalesActivity
        {
            TenantId = opportunity.TenantId,
            OpportunityId = opportunity.Id,
            Type = SalesActivityTypes.System,
            Summary = summary,
            OccurredAt = now,
            LoggedByUserId = actorUserId,
            LoggedByName = actorName,
        });
        opportunity.LastActivityAt = now;
    }
}

/// <summary>
/// Who sees which deals. By default everyone with CRM access sees the whole pipeline. A workspace
/// can limit people to the deals, RFP/RFQs and follow-ups they own (Sales settings), the common
/// "owned only" setting in CRMs such as HubSpot; admins and anyone granted
/// <see cref="PermissionNames.CrmViewAll"/> (sales managers by default) still see everything.
/// </summary>
internal static class SalesVisibility
{
    public const string All = "All";
    public const string Own = "Own";

    public static string Normalize(string? value) =>
        string.Equals(value?.Trim(), Own, StringComparison.OrdinalIgnoreCase) ? Own : All;

    /// <summary>The user the caller is limited to, or null when they see every deal.</summary>
    public static async Task<Guid?> OwnerScopeAsync(AppDbContext dbContext, ITenantContext tenantContext, Guid tenantId, CancellationToken cancellationToken)
    {
        if (tenantContext.IsAdmin || tenantContext.HasPermission(PermissionNames.CrmViewAll))
            return null;
        var visibility = await dbContext.TenantSalesSettings.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => x.DealVisibility)
            .FirstOrDefaultAsync(cancellationToken);
        return Normalize(visibility) == Own ? tenantContext.GetRequiredUserId() : null;
    }
}

/// <summary>
/// Prospects are client records flagged <see cref="Client.IsProspect"/>, so a company has one record
/// from first contact on: a deal entered with only a company or contact name is linked to a prospect
/// client (an existing client or prospect of the same name, else a new one), and the prospect becomes
/// a client once a deal with them is won, a work order is raised for them, or someone converts it.
/// </summary>
internal static class ProspectClients
{
    /// <summary>
    /// Gives a deal with no client a client record. <paramref name="previousClientId"/> is the client the
    /// deal had before this edit: a prospect only this deal uses is kept and renamed with it, so editing
    /// a prospect deal never leaves a stray prospect behind; one shared with other deals is left as is.
    /// </summary>
    public static async Task LinkAsync(AppDbContext dbContext, SalesOpportunity opportunity, Guid? previousClientId, CancellationToken cancellationToken)
    {
        if (opportunity.ClientId.HasValue)
            return;
        var name = !string.IsNullOrWhiteSpace(opportunity.CompanyName) ? opportunity.CompanyName.Trim() : opportunity.ContactName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return;

        Client? client = null;
        if (previousClientId.HasValue
            && !await dbContext.SalesOpportunities.AnyAsync(x => x.TenantId == opportunity.TenantId && x.ClientId == previousClientId && x.Id != opportunity.Id, cancellationToken))
        {
            client = await dbContext.Clients.SingleOrDefaultAsync(x => x.TenantId == opportunity.TenantId && x.Id == previousClientId && x.IsProspect, cancellationToken);
            if (client is not null)
                client.ClientName = name;
        }
        if (client is null)
        {
            // Same company ignoring case, punctuation and legal suffixes ("Baraka Ltd" = "baraka").
            var key = ClientMatching.NameKey(name);
            var firstWord = key.Split(' ')[0];
            var candidates = await dbContext.Clients
                .Where(x => x.TenantId == opportunity.TenantId && x.IsActive && x.ClientName.ToLower().Contains(firstWord))
                .ToListAsync(cancellationToken);
            client = candidates
                .Where(x => ClientMatching.NameKey(x.ClientName) == key)
                .OrderBy(x => x.IsProspect)
                .FirstOrDefault();
        }
        if (client is null)
        {
            client = new Client
            {
                TenantId = opportunity.TenantId,
                ClientName = name,
                IsProspect = true,
                IsActive = true,
                Notes = $"Prospect from sales opportunity '{opportunity.Title}'.",
            };
            dbContext.Clients.Add(client);
        }
        if (client.IsProspect)
        {
            client.ContactPerson = opportunity.ContactName ?? client.ContactPerson;
            client.Email = opportunity.ContactEmail ?? client.Email;
            client.Phone = opportunity.ContactPhone ?? client.Phone;
            client.ContactPhone = opportunity.ContactPhone ?? client.ContactPhone;
            client.Location = opportunity.Location ?? client.Location;
        }
        opportunity.ClientId = client.Id;
    }

    /// <summary>Makes a prospect a client. Returns true when it was a prospect. Changes are added to the context.</summary>
    public static async Task<bool> PromoteAsync(AppDbContext dbContext, Guid tenantId, Guid? clientId, CancellationToken cancellationToken)
    {
        if (!clientId.HasValue)
            return false;
        // The prospect may have been added in this same request and not saved yet.
        var client = dbContext.Clients.Local.FirstOrDefault(x => x.Id == clientId)
            ?? await dbContext.Clients.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == clientId, cancellationToken);
        if (client is null || client.TenantId != tenantId || !client.IsProspect)
            return false;
        client.IsProspect = false;
        return true;
    }
}
