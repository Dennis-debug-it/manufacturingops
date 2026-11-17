using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// Factory pricing on the platform: the price book (per factory per year), quotes worked out from it with
/// a PDF to send, and per-factory billing terms for a tenant's licence.
/// </summary>
[ApiController]
[Authorize(Policy = "PlatformAdminAccess")]
[Route("api/platform/pricing")]
public sealed class PlatformPricingController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IAuditLogService auditLogService,
    ILicenseGuardService licenseGuardService) : ControllerBase
{
    private const string Vendor = "Ecosys";

    // ── Price book ───────────────────────────────────────────────────────────

    [HttpGet("price-book")]
    public async Task<ActionResult<PriceBookResponse>> PriceBook(CancellationToken cancellationToken)
    {
        var book = await FactoryPricing.GetOrCreateAsync(dbContext, cancellationToken);
        return Ok(await MapBookAsync(book, cancellationToken));
    }

    [HttpPut("price-book")]
    public async Task<ActionResult<PriceBookResponse>> SavePriceBook([FromBody] SavePriceBookRequest request, CancellationToken cancellationToken)
    {
        var book = await FactoryPricing.GetOrCreateAsync(dbContext, cancellationToken);
        Validate(request);

        var plans = await dbContext.LicensePlans.ToListAsync(cancellationToken);
        foreach (var edition in request.Editions ?? [])
        {
            var plan = plans.SingleOrDefault(x => x.PlanCode == edition.Code) ?? throw new BusinessRuleException($"Unknown edition '{edition.Code}'.");
            plan.FactoryAnnualPrice = decimal.Round(edition.FactoryAnnualPrice, 0);
        }

        book.Currency = (request.Currency ?? "KES").Trim().ToUpperInvariant();
        book.VatPercent = request.VatPercent;
        book.VisitorsFactoryAnnualPrice = request.VisitorsFactoryAnnualPrice;
        book.SecurityFactoryAnnualPrice = request.SecurityFactoryAnnualPrice;
        book.SecuritySuiteFactoryAnnualPrice = request.SecuritySuiteFactoryAnnualPrice;
        book.IncludedOfficeUsersPerFactory = request.IncludedOfficeUsersPerFactory;
        book.IncludedFieldUsersPerFactory = request.IncludedFieldUsersPerFactory;
        book.ExtraUserMonthlyPrice = request.ExtraUserMonthlyPrice;
        book.DiscountTiersJson = FactoryPricing.ToJson((request.DiscountTiers ?? []).OrderBy(x => x.MinFactories).ToList());
        book.ImplementationFeeMin = request.ImplementationFeeMin;
        book.ImplementationFeeMax = request.ImplementationFeeMax;
        book.ImplementationFeeDefault = request.ImplementationFeeDefault;
        book.OnPremisesMultiplier = request.OnPremisesMultiplier;
        book.OnPremisesSetupFee = request.OnPremisesSetupFee;
        book.PilotMonths = request.PilotMonths;
        book.PilotFee = request.PilotFee;
        book.PilotCreditedToYearOne = request.PilotCreditedToYearOne;
        book.HardwareJson = FactoryPricing.ToJson((request.Hardware ?? []).Select(x => x with { Code = x.Code.Trim().ToLowerInvariant(), Name = x.Name.Trim(), Unit = string.IsNullOrWhiteSpace(x.Unit) ? "each" : x.Unit.Trim() }).ToList());
        book.QuoteValidDays = request.QuoteValidDays;
        book.QuoteTerms = string.IsNullOrWhiteSpace(request.QuoteTerms) ? null : request.QuoteTerms.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(null, tenantContext.GetRequiredUserId(), "platform.pricing.price-book.updated", nameof(PriceBook), book.Id.ToString(),
            $"Price book saved: editions {string.Join(", ", plans.OrderBy(x => x.PlanCode).Select(x => $"{x.PlanCode} {x.FactoryAnnualPrice:N0}"))}; visitors {book.VisitorsFactoryAnnualPrice:N0}, security {book.SecurityFactoryAnnualPrice:N0}, bundle {book.SecuritySuiteFactoryAnnualPrice:N0} per factory per year.",
            cancellationToken);
        return Ok(await MapBookAsync(book, cancellationToken));
    }

    private static void Validate(SavePriceBookRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Currency) || r.Currency.Trim().Length != 3) throw new BusinessRuleException("Currency is a three-letter code, e.g. KES.");
        var money = new[] { r.VisitorsFactoryAnnualPrice, r.SecurityFactoryAnnualPrice, r.SecuritySuiteFactoryAnnualPrice, r.ExtraUserMonthlyPrice, r.ImplementationFeeMin,
            r.ImplementationFeeMax, r.ImplementationFeeDefault, r.OnPremisesSetupFee, r.PilotFee };
        if (money.Any(x => x < 0) || (r.Editions ?? []).Any(x => x.FactoryAnnualPrice < 0)) throw new BusinessRuleException("Prices cannot be negative.");
        if (r.VatPercent is < 0 or > 50) throw new BusinessRuleException("VAT must be between 0 and 50%.");
        if (r.IncludedOfficeUsersPerFactory < 1 || r.IncludedFieldUsersPerFactory < 0) throw new BusinessRuleException("Each factory includes at least one office user.");
        if (r.ImplementationFeeMin > r.ImplementationFeeMax) throw new BusinessRuleException("The lowest implementation fee is above the highest.");
        if (r.ImplementationFeeDefault < r.ImplementationFeeMin || r.ImplementationFeeDefault > r.ImplementationFeeMax)
            throw new BusinessRuleException("The usual implementation fee must be within the range.");
        if (r.OnPremisesMultiplier is < 1 or > 5) throw new BusinessRuleException("The on-premises multiplier must be between 1 and 5.");
        if (r.PilotMonths is < 1 or > 12) throw new BusinessRuleException("A pilot lasts between 1 and 12 months.");
        if (r.QuoteValidDays is < 1 or > 365) throw new BusinessRuleException("A quote is valid for 1 to 365 days.");
        var tiers = r.DiscountTiers ?? [];
        if (tiers.Any(x => x.MinFactories < 2 || x.Percent is <= 0 or > 90)) throw new BusinessRuleException("A group discount starts at 2 or more factories and is between 0 and 90%.");
        if (tiers.Select(x => x.MinFactories).Distinct().Count() != tiers.Count) throw new BusinessRuleException("Two discounts start at the same number of factories.");
        var ordered = tiers.OrderBy(x => x.MinFactories).ToList();
        for (var i = 1; i < ordered.Count; i++)
            if (ordered[i].Percent <= ordered[i - 1].Percent) throw new BusinessRuleException("A bigger group should get a bigger discount.");
        var hardware = r.Hardware ?? [];
        if (hardware.Any(x => string.IsNullOrWhiteSpace(x.Code) || string.IsNullOrWhiteSpace(x.Name) || x.UnitCost < 0 || x.MarginPercent is < 0 or > 500))
            throw new BusinessRuleException("Each hardware item needs a code, a name, a cost and a margin of 0–500%.");
        if (hardware.Select(x => x.Code.Trim().ToLowerInvariant()).Distinct().Count() != hardware.Count) throw new BusinessRuleException("Two hardware items share a code.");
        if (hardware.Count > 40) throw new BusinessRuleException("Keep the hardware catalogue to 40 items.");
    }

    private async Task<PriceBookResponse> MapBookAsync(PriceBook book, CancellationToken cancellationToken)
    {
        var plans = await dbContext.LicensePlans.AsNoTracking().ToListAsync(cancellationToken);
        var editions = ServiceOpsPlans.Editions.Select(code => plans.SingleOrDefault(x => x.PlanCode == code))
            .Where(x => x is not null).Select(x => new EditionPrice(x!.PlanCode, x.DisplayName, x.FactoryAnnualPrice)).ToList();
        return new PriceBookResponse(editions, book.Currency, book.VatPercent, book.VisitorsFactoryAnnualPrice, book.SecurityFactoryAnnualPrice, book.SecuritySuiteFactoryAnnualPrice,
            book.IncludedOfficeUsersPerFactory, book.IncludedFieldUsersPerFactory, book.ExtraUserMonthlyPrice, FactoryPricing.Tiers(book),
            book.ImplementationFeeMin, book.ImplementationFeeMax, book.ImplementationFeeDefault, book.OnPremisesMultiplier, book.OnPremisesSetupFee,
            book.PilotMonths, book.PilotFee, book.PilotCreditedToYearOne, FactoryPricing.Hardware(book), book.QuoteValidDays, book.QuoteTerms, book.UpdatedAt ?? book.CreatedAt);
    }

    // ── Quotes ───────────────────────────────────────────────────────────────

    /// <summary>Works out a quote without saving it, for the live totals while one is being built.</summary>
    [HttpPost("preview")]
    public async Task<ActionResult<PricingResult>> Preview([FromBody] QuoteTermsRequest request, CancellationToken cancellationToken)
    {
        var book = await FactoryPricing.GetOrCreateAsync(dbContext, cancellationToken);
        return Ok(FactoryPricing.Calculate(book, await FactoryPricing.EditionPricesAsync(dbContext, cancellationToken), request.ToInput()));
    }

    [HttpGet("quotes")]
    public async Task<ActionResult<IReadOnlyList<QuoteRow>>> Quotes([FromQuery] string? status, CancellationToken cancellationToken)
    {
        var query = dbContext.PriceQuotes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        var quotes = await query.OrderByDescending(x => x.CreatedAt).Take(500).ToListAsync(cancellationToken);
        var tenants = await TenantNamesAsync(quotes.Select(x => x.TenantId), cancellationToken);
        return Ok(quotes.Select(x => MapRow(x, tenants)).ToList());
    }

    [HttpGet("quotes/{id:guid}")]
    public async Task<ActionResult<QuoteDetail>> Quote(Guid id, CancellationToken cancellationToken)
    {
        var quote = await dbContext.PriceQuotes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new NotFoundException("Quote not found.");
        return Ok(await MapDetailAsync(quote, cancellationToken));
    }

    [HttpPost("quotes")]
    public async Task<ActionResult<QuoteDetail>> CreateQuote([FromBody] SaveQuoteRequest request, CancellationToken cancellationToken)
    {
        var book = await FactoryPricing.GetOrCreateAsync(dbContext, cancellationToken);
        var quote = new PriceQuote { QuoteNumber = await NextNumberAsync(cancellationToken), ValidUntil = DateTime.UtcNow.Date.AddDays(book.QuoteValidDays), PreparedByName = PreparedBy() };
        await ApplyAsync(quote, request, book, cancellationToken);
        dbContext.PriceQuotes.Add(quote);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(quote.TenantId, tenantContext.GetRequiredUserId(), "platform.pricing.quote.created", nameof(PriceQuote), quote.Id.ToString(),
            $"{quote.QuoteNumber} for {quote.CustomerName}: {FactoryPricing.Money(quote.AnnualSubscription)} a year, {FactoryPricing.Money(quote.YearOneTotal)} in year one.", cancellationToken);
        return Ok(await MapDetailAsync(quote, cancellationToken));
    }

    /// <summary>Changes a quote and works it out again from today's price book. An accepted or declined quote is kept as it was.</summary>
    [HttpPut("quotes/{id:guid}")]
    public async Task<ActionResult<QuoteDetail>> UpdateQuote(Guid id, [FromBody] SaveQuoteRequest request, CancellationToken cancellationToken)
    {
        var quote = await dbContext.PriceQuotes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new NotFoundException("Quote not found.");
        if (quote.Status is QuoteStatuses.Accepted or QuoteStatuses.Declined) throw new BusinessRuleException($"This quote was {quote.Status.ToLowerInvariant()}; copy it to change the terms.");
        var book = await FactoryPricing.GetOrCreateAsync(dbContext, cancellationToken);
        await ApplyAsync(quote, request, book, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await MapDetailAsync(quote, cancellationToken));
    }

    [HttpPost("quotes/{id:guid}/copy")]
    public async Task<ActionResult<QuoteDetail>> CopyQuote(Guid id, CancellationToken cancellationToken)
    {
        var source = await dbContext.PriceQuotes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new NotFoundException("Quote not found.");
        var book = await FactoryPricing.GetOrCreateAsync(dbContext, cancellationToken);
        var quote = new PriceQuote { QuoteNumber = await NextNumberAsync(cancellationToken), ValidUntil = DateTime.UtcNow.Date.AddDays(book.QuoteValidDays), PreparedByName = PreparedBy() };
        await ApplyAsync(quote, SaveQuoteRequest.From(source), book, cancellationToken);
        dbContext.PriceQuotes.Add(quote);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await MapDetailAsync(quote, cancellationToken));
    }

    [HttpPost("quotes/{id:guid}/status")]
    public async Task<ActionResult<QuoteDetail>> SetStatus(Guid id, [FromBody] QuoteStatusRequest request, CancellationToken cancellationToken)
    {
        var quote = await dbContext.PriceQuotes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new NotFoundException("Quote not found.");
        var status = QuoteStatuses.All.FirstOrDefault(x => string.Equals(x, request.Status, StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessRuleException($"Status must be one of: {string.Join(", ", QuoteStatuses.All)}.");
        quote.Status = status;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(quote.TenantId, tenantContext.GetRequiredUserId(), "platform.pricing.quote.status", nameof(PriceQuote), quote.Id.ToString(),
            $"{quote.QuoteNumber} marked {status}.", cancellationToken);
        return Ok(await MapDetailAsync(quote, cancellationToken));
    }

    [HttpDelete("quotes/{id:guid}")]
    public async Task<IActionResult> DeleteQuote(Guid id, CancellationToken cancellationToken)
    {
        var quote = await dbContext.PriceQuotes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new NotFoundException("Quote not found.");
        if (quote.Status != QuoteStatuses.Draft) throw new BusinessRuleException("Only a draft can be deleted; mark a sent quote as declined instead.");
        dbContext.PriceQuotes.Remove(quote);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("quotes/{id:guid}/pdf")]
    public async Task<IActionResult> QuotePdfFile(Guid id, CancellationToken cancellationToken)
    {
        var quote = await dbContext.PriceQuotes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new NotFoundException("Quote not found.");
        var book = await FactoryPricing.GetOrCreateAsync(dbContext, cancellationToken);
        var result = FactoryPricing.Parse<PricingResult>(quote.ResultJson) ?? throw new BusinessRuleException("This quote has no worked-out prices; save it again.");
        var pdf = QuotePdf.Render(Vendor, quote, result, book.QuoteTerms);
        return File(pdf, "application/pdf", $"{quote.QuoteNumber}-{Slug(quote.CustomerName)}.pdf");
    }

    /// <summary>
    /// Puts an accepted quote's terms on the tenant's licence: its edition, Visitors and Security, and
    /// per-factory billing for that many factories, at the quote's price per factory.
    /// </summary>
    [HttpPost("quotes/{id:guid}/apply")]
    public async Task<ActionResult<TenantFactoryPricingResponse>> ApplyQuote(Guid id, CancellationToken cancellationToken)
    {
        var quote = await dbContext.PriceQuotes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new NotFoundException("Quote not found.");
        if (quote.Status != QuoteStatuses.Accepted) throw new BusinessRuleException("Mark the quote accepted before applying it to a workspace.");
        var tenantId = quote.TenantId ?? throw new BusinessRuleException("Link the quote to a workspace first.");
        var license = await LicenseAsync(tenantId, cancellationToken);
        var result = FactoryPricing.Parse<PricingResult>(quote.ResultJson) ?? throw new BusinessRuleException("Save the quote again before applying it.");
        if (quote.Edition is { } edition)
        {
            var plan = await dbContext.LicensePlans.SingleAsync(x => x.PlanCode == edition, cancellationToken);
            license.LicensePlanId = plan.Id;
            license.LicensePlan = plan;
        }

        license.OperationsAddOns = ServiceOpsPlans.NormalizeOperationsAddOns(
            new[] { quote.Visitors ? ServiceOpsPlans.Visitors : null, quote.Security ? ServiceOpsPlans.Security : null }.OfType<string>());
        var book = await FactoryPricing.GetOrCreateAsync(dbContext, cancellationToken);
        // The agreed price per factory is the quote's subscription without its extra users, spread over its factories.
        var extras = result.Lines.Where(x => x.Kind == PriceLineKinds.Subscription && x.Unit == "users").Sum(x => x.Amount);
        var agreed = decimal.Round((result.AnnualSubscription - extras) / quote.Factories, 0, MidpointRounding.AwayFromZero);
        ApplyTerms(license, book, new TenantFactoryTermsRequest(PricingBases.PerFactory, quote.Factories, agreed, quote.ExtraOfficeUsers, quote.ExtraFieldUsers));
        license.BillingCycle = BillingCycles.Annual;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(tenantId, tenantContext.GetRequiredUserId(), "platform.pricing.quote.applied", nameof(TenantLicense), license.Id.ToString(),
            $"{quote.QuoteNumber} applied: {quote.Factories} factories at {FactoryPricing.Money(agreed)} per factory per year, billed yearly.", cancellationToken);
        return Ok(await TenantTermsAsync(tenantId, cancellationToken));
    }

    private async Task ApplyAsync(PriceQuote quote, SaveQuoteRequest request, PriceBook book, CancellationToken cancellationToken)
    {
        var customer = request.CustomerName?.Trim();
        if (string.IsNullOrEmpty(customer) || customer.Length > 200) throw new BusinessRuleException("Enter who the quote is for.");
        if (request.TenantId is { } tenantId && !await dbContext.Tenants.AnyAsync(x => x.Id == tenantId && x.Id != PlatformConstants.RootTenantId, cancellationToken))
            throw new BusinessRuleException("That workspace was not found.");
        if (request.ContactEmail is { Length: > 0 } email && !email.Contains('@')) throw new BusinessRuleException("The contact email does not look right.");
        var terms = request.Terms ?? throw new BusinessRuleException("Choose what the quote includes.");
        var result = FactoryPricing.Calculate(book, await FactoryPricing.EditionPricesAsync(dbContext, cancellationToken), terms.ToInput());

        quote.CustomerName = customer;
        quote.ContactName = Trim(request.ContactName, 200);
        quote.ContactEmail = Trim(request.ContactEmail, 200);
        quote.TenantId = request.TenantId;
        quote.Notes = Trim(request.Notes, 2000);
        quote.Edition = string.IsNullOrWhiteSpace(terms.Edition) ? null : terms.Edition.Trim().ToLowerInvariant();
        quote.Visitors = terms.Visitors;
        quote.Security = terms.Security;
        quote.Factories = terms.Factories;
        quote.ExtraOfficeUsers = terms.ExtraOfficeUsers;
        quote.ExtraFieldUsers = terms.ExtraFieldUsers;
        quote.ImplementationFeePerFactory = terms.ImplementationFeePerFactory ?? book.ImplementationFeeDefault;
        quote.OnPremises = terms.OnPremises;
        quote.IncludePilot = terms.IncludePilot;
        quote.DiscountPercentOverride = terms.DiscountPercentOverride;
        quote.HardwareJson = terms.Hardware is { Count: > 0 } hw ? FactoryPricing.ToJson(hw.Where(x => x.Quantity > 0).ToList()) : null;
        quote.ResultJson = FactoryPricing.ToJson(result);
        quote.AnnualSubscription = result.AnnualSubscription;
        quote.YearOneTotal = result.YearOneTotal;
        if (request.ValidUntil is { } until)
        {
            if (until.Date < DateTime.UtcNow.Date) throw new BusinessRuleException("A quote cannot be valid until a date that has passed.");
            quote.ValidUntil = DateTime.SpecifyKind(until.Date, DateTimeKind.Utc);
        }
    }

    private async Task<string> NextNumberAsync(CancellationToken cancellationToken)
    {
        var prefix = $"Q-{DateTime.UtcNow:yyyy}-";
        var numbers = await dbContext.PriceQuotes.AsNoTracking().Where(x => x.QuoteNumber.StartsWith(prefix)).Select(x => x.QuoteNumber).ToListAsync(cancellationToken);
        var next = numbers.Select(x => int.TryParse(x[prefix.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max() + 1;
        return $"{prefix}{next:0000}";
    }

    private async Task<QuoteDetail> MapDetailAsync(PriceQuote quote, CancellationToken cancellationToken)
    {
        var tenants = await TenantNamesAsync([quote.TenantId], cancellationToken);
        return new QuoteDetail(MapRow(quote, tenants), SaveQuoteRequest.From(quote), FactoryPricing.Parse<PricingResult>(quote.ResultJson));
    }

    private static QuoteRow MapRow(PriceQuote x, IReadOnlyDictionary<Guid, string> tenants) => new(
        x.Id, x.QuoteNumber, x.CustomerName, x.ContactName, x.TenantId, x.TenantId is { } t && tenants.TryGetValue(t, out var name) ? name : null,
        x.Edition, x.Visitors, x.Security, x.Factories, x.IncludePilot, x.Status, x.ValidUntil, x.ValidUntil < DateTime.UtcNow.Date && x.Status is QuoteStatuses.Draft or QuoteStatuses.Sent,
        x.AnnualSubscription, x.YearOneTotal, x.PreparedByName, x.CreatedAt);

    private async Task<IReadOnlyDictionary<Guid, string>> TenantNamesAsync(IEnumerable<Guid?> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.OfType<Guid>().Distinct().ToList();
        if (wanted.Count == 0) return new Dictionary<Guid, string>();
        return await dbContext.Tenants.AsNoTracking().Where(x => wanted.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
    }

    // ── A tenant's per-factory terms ─────────────────────────────────────────

    [HttpGet("tenants/{tenantId:guid}")]
    public async Task<ActionResult<TenantFactoryPricingResponse>> TenantTerms(Guid tenantId, CancellationToken cancellationToken) =>
        Ok(await TenantTermsAsync(tenantId, cancellationToken));

    [HttpPut("tenants/{tenantId:guid}")]
    public async Task<ActionResult<TenantFactoryPricingResponse>> SaveTenantTerms(Guid tenantId, [FromBody] TenantFactoryTermsRequest request, CancellationToken cancellationToken)
    {
        var license = await LicenseAsync(tenantId, cancellationToken);
        var book = await FactoryPricing.GetOrCreateAsync(dbContext, cancellationToken);
        var before = $"{license.PricingBasis}, {license.LicensedFactories?.ToString() ?? "—"} factories, agreed {license.FactoryAnnualPriceOverride?.ToString("N0") ?? "list"}";
        ApplyTerms(license, book, request);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(tenantId, tenantContext.GetRequiredUserId(), "platform.pricing.tenant.terms", nameof(TenantLicense), license.Id.ToString(),
            $"Billing {before} → {license.PricingBasis}, {license.LicensedFactories?.ToString() ?? "—"} factories, agreed {license.FactoryAnnualPriceOverride?.ToString("N0") ?? "list"}; seats {license.OfficeSeats} office / {license.FieldSeats} field.",
            cancellationToken);
        return Ok(await TenantTermsAsync(tenantId, cancellationToken));
    }

    /// <summary>
    /// Sets the billing basis. Per factory, the licence's seat limits become the users included for that many
    /// factories plus any extra users bought, so the usual seat checks keep working.
    /// </summary>
    private static void ApplyTerms(TenantLicense license, PriceBook book, TenantFactoryTermsRequest request)
    {
        if (request.PricingBasis == PricingBases.PerSeat)
        {
            license.PricingBasis = PricingBases.PerSeat;
            return;
        }

        if (request.PricingBasis != PricingBases.PerFactory) throw new BusinessRuleException("Billing is per seat or per factory.");
        if (request.LicensedFactories is not { } factories || factories is < 1 or > 1000) throw new BusinessRuleException("Enter how many factories are licensed (1 to 1,000).");
        if (request.FactoryAnnualPriceOverride is < 0) throw new BusinessRuleException("The agreed price cannot be negative.");
        if (request.ExtraOfficeUsers < 0 || request.ExtraFieldUsers < 0) throw new BusinessRuleException("Extra users cannot be negative.");
        license.PricingBasis = PricingBases.PerFactory;
        license.LicensedFactories = factories;
        license.FactoryAnnualPriceOverride = request.FactoryAnnualPriceOverride is { } agreed ? decimal.Round(agreed, 0) : null;
        license.ExtraOfficeUsers = request.ExtraOfficeUsers;
        license.ExtraFieldUsers = request.ExtraFieldUsers;
        license.OfficeSeats = Math.Max(Math.Max(1, license.LicensePlan?.MinOfficeSeats ?? 1), book.IncludedOfficeUsersPerFactory * factories + request.ExtraOfficeUsers);
        license.FieldSeats = book.IncludedFieldUsersPerFactory * factories + request.ExtraFieldUsers;
    }

    private async Task<TenantLicense> LicenseAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Tenants.AnyAsync(x => x.Id == tenantId && x.Id != PlatformConstants.RootTenantId, cancellationToken)) throw new NotFoundException("Tenant was not found.");
        return await licenseGuardService.GetOrCreateTenantLicenseAsync(tenantId, cancellationToken);
    }

    private async Task<TenantFactoryPricingResponse> TenantTermsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var license = await LicenseAsync(tenantId, cancellationToken);
        var book = await FactoryPricing.GetOrCreateAsync(dbContext, cancellationToken);
        var factoriesInWorkspace = await dbContext.OrgNodes.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(x => x.TenantId == tenantId && x.Level == OrgNodeLevels.Factory && x.IsActive, cancellationToken);
        var addOns = ServiceOpsPlans.ParseOperationsAddOns(license.OperationsAddOns);
        var input = FactoryPricing.InputFor(license) with { Factories = Math.Max(1, license.LicensedFactories ?? factoriesInWorkspace) };
        PricingResult? result = null;
        string? problem = null;
        try { result = FactoryPricing.Calculate(book, await FactoryPricing.EditionPricesAsync(dbContext, cancellationToken), input); }
        catch (BusinessRuleException ex) { problem = ex.Message; }
        var whatsApp = await dbContext.LicenseAddOns.AsNoTracking().SingleOrDefaultAsync(x => x.Code == license.WhatsAppAddOn, cancellationToken);
        var (officeUsed, fieldUsed) = await SeatsUsedAsync(tenantId, cancellationToken);
        return new TenantFactoryPricingResponse(
            tenantId, license.PricingBasis, license.LicensePlan?.PlanCode, license.LicensePlan?.DisplayName, addOns.Contains(ServiceOpsPlans.Visitors), addOns.Contains(ServiceOpsPlans.Security),
            license.LicensedFactories, factoriesInWorkspace, license.FactoryAnnualPriceOverride, license.ExtraOfficeUsers, license.ExtraFieldUsers,
            license.OfficeSeats, license.FieldSeats, officeUsed, fieldUsed, license.BillingCycle,
            LicensePricing.MonthlyValue(license, whatsApp is null ? [] : [whatsApp], book), result, problem);
    }

    private async Task<(int Office, int Field)> SeatsUsedAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var roles = await dbContext.Users.IgnoreQueryFilters().AsNoTracking().Where(x => x.TenantId == tenantId && x.IsActive).Select(x => x.Role).ToListAsync(cancellationToken);
        return (roles.Count(x => SeatTypes.For(x) == SeatTypes.Office), roles.Count(x => SeatTypes.For(x) == SeatTypes.Field));
    }

    private string? PreparedBy() => tenantContext.Email;
    private static string? Trim(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim() is var t && t.Length > max ? t[..max] : value.Trim();
    private static string Slug(string value) => new string(value.Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray()).Trim().Replace(' ', '-');
}

public sealed record EditionPrice(string Code, string Name, decimal FactoryAnnualPrice);

public sealed record PriceBookResponse(
    IReadOnlyList<EditionPrice> Editions, string Currency, decimal VatPercent,
    decimal VisitorsFactoryAnnualPrice, decimal SecurityFactoryAnnualPrice, decimal SecuritySuiteFactoryAnnualPrice,
    int IncludedOfficeUsersPerFactory, int IncludedFieldUsersPerFactory, decimal ExtraUserMonthlyPrice, IReadOnlyList<DiscountTier> DiscountTiers,
    decimal ImplementationFeeMin, decimal ImplementationFeeMax, decimal ImplementationFeeDefault, decimal OnPremisesMultiplier, decimal OnPremisesSetupFee,
    int PilotMonths, decimal PilotFee, bool PilotCreditedToYearOne, IReadOnlyList<HardwareItem> Hardware, int QuoteValidDays, string? QuoteTerms, DateTime UpdatedAt);

public sealed record SavePriceBookRequest(
    IReadOnlyList<EditionPrice>? Editions, string? Currency, decimal VatPercent,
    decimal VisitorsFactoryAnnualPrice, decimal SecurityFactoryAnnualPrice, decimal SecuritySuiteFactoryAnnualPrice,
    int IncludedOfficeUsersPerFactory, int IncludedFieldUsersPerFactory, decimal ExtraUserMonthlyPrice, IReadOnlyList<DiscountTier>? DiscountTiers,
    decimal ImplementationFeeMin, decimal ImplementationFeeMax, decimal ImplementationFeeDefault, decimal OnPremisesMultiplier, decimal OnPremisesSetupFee,
    int PilotMonths, decimal PilotFee, bool PilotCreditedToYearOne, IReadOnlyList<HardwareItem>? Hardware, int QuoteValidDays, string? QuoteTerms);

public sealed record QuoteTermsRequest(
    string? Edition, bool Visitors, bool Security, int Factories, int ExtraOfficeUsers = 0, int ExtraFieldUsers = 0,
    decimal? ImplementationFeePerFactory = null, bool OnPremises = false, bool IncludePilot = false, decimal? DiscountPercentOverride = null,
    IReadOnlyList<HardwareRequest>? Hardware = null)
{
    public PricingInput ToInput() => new(Edition, Visitors, Security, Factories, ExtraOfficeUsers, ExtraFieldUsers, ImplementationFeePerFactory, OnPremises, IncludePilot,
        DiscountPercentOverride, Hardware);
}

public sealed record SaveQuoteRequest(
    string? CustomerName, string? ContactName, string? ContactEmail, Guid? TenantId, string? Notes, DateTime? ValidUntil, QuoteTermsRequest? Terms)
{
    public static SaveQuoteRequest From(PriceQuote q) => new(q.CustomerName, q.ContactName, q.ContactEmail, q.TenantId, q.Notes, q.ValidUntil,
        new QuoteTermsRequest(q.Edition, q.Visitors, q.Security, q.Factories, q.ExtraOfficeUsers, q.ExtraFieldUsers, q.ImplementationFeePerFactory, q.OnPremises, q.IncludePilot,
            q.DiscountPercentOverride, FactoryPricing.Parse<List<HardwareRequest>>(q.HardwareJson) ?? []));
}

public sealed record QuoteStatusRequest(string? Status);

public sealed record QuoteRow(
    Guid Id, string QuoteNumber, string CustomerName, string? ContactName, Guid? TenantId, string? TenantName, string? Edition, bool Visitors, bool Security,
    int Factories, bool IncludePilot, string Status, DateTime ValidUntil, bool Expired, decimal AnnualSubscription, decimal YearOneTotal, string? PreparedByName, DateTime CreatedAt);

public sealed record QuoteDetail(QuoteRow Quote, SaveQuoteRequest Request, PricingResult? Result);

public sealed record TenantFactoryTermsRequest(string? PricingBasis, int? LicensedFactories = null, decimal? FactoryAnnualPriceOverride = null, int ExtraOfficeUsers = 0, int ExtraFieldUsers = 0);

public sealed record TenantFactoryPricingResponse(
    Guid TenantId, string PricingBasis, string? Edition, string? EditionName, bool Visitors, bool Security,
    int? LicensedFactories, int FactoriesInWorkspace, decimal? FactoryAnnualPriceOverride, int ExtraOfficeUsers, int ExtraFieldUsers,
    int OfficeSeats, int FieldSeats, int OfficeSeatsUsed, int FieldSeatsUsed, string BillingCycle,
    decimal MonthlyValue, PricingResult? PerFactoryPrice, string? Problem);
