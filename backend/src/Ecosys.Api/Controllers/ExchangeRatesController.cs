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
[Route("api/exchange-rates")]
public sealed class ExchangeRatesController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IAuditLogService auditLogService,
    IUserAccessService userAccessService) : TenantAwareControllerBase(tenantContext)
{
    /// <summary>Latest effective rate per currency pair.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ExchangeRateResponse>>> ListRates(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.FinanceViewRates, PermissionNames.FinanceView, PermissionNames.FinanceManage);

        var rates = await dbContext.ExchangeRates
            .Where(x => x.TenantId == TenantId)
            .ToListAsync(cancellationToken);

        var latest = rates
            .GroupBy(x => new { x.BaseCurrency, x.QuoteCurrency })
            .Select(g => g.OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.CreatedAt).First())
            .OrderBy(x => x.BaseCurrency).ThenBy(x => x.QuoteCurrency)
            .Select(MapResponse)
            .ToList();

        return Ok(latest);
    }

    /// <summary>Full history for one currency pair, most recent first.</summary>
    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyCollection<ExchangeRateResponse>>> GetHistory(
        [FromQuery] string baseCurrency, [FromQuery] string quoteCurrency, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.FinanceViewRates, PermissionNames.FinanceView, PermissionNames.FinanceManage);

        var b = Normalize(baseCurrency);
        var q = Normalize(quoteCurrency);
        var history = await dbContext.ExchangeRates
            .Where(x => x.TenantId == TenantId && x.BaseCurrency == b && x.QuoteCurrency == q)
            .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        return Ok(history.Select(MapResponse).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<ExchangeRateResponse>> UpsertRate(
        [FromBody] UpsertExchangeRateRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageRates);

        var b = Normalize(request.BaseCurrency);
        var q = Normalize(request.QuoteCurrency);
        if (b.Length is < 3 or > 4 || q.Length is < 3 or > 4)
            throw new BusinessRuleException("Currency codes must be 3–4 letters.");
        if (b == q)
            throw new BusinessRuleException("Base and quote currency must differ.");
        if (request.Rate <= 0)
            throw new BusinessRuleException("Rate must be greater than zero.");

        var updatedByName = await dbContext.Users
            .Where(x => x.Id == UserId)
            .Select(x => x.FullName)
            .FirstOrDefaultAsync(cancellationToken) ?? "Unknown";

        var effectiveFrom = request.EffectiveFrom is null
            ? DateTime.UtcNow
            : DateTime.SpecifyKind(request.EffectiveFrom.Value, DateTimeKind.Utc);

        var rate = new ExchangeRate
        {
            TenantId = TenantId,
            BaseCurrency = b,
            QuoteCurrency = q,
            Rate = request.Rate,
            EffectiveFrom = effectiveFrom,
            UpdatedByUserId = UserId,
            UpdatedByName = updatedByName,
        };
        dbContext.ExchangeRates.Add(rate);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "finance.exchange_rate.set", nameof(ExchangeRate), rate.Id.ToString(), $"Set {b}/{q} = {request.Rate}", cancellationToken);

        return Ok(MapResponse(rate));
    }

    private static string Normalize(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    private static ExchangeRateResponse MapResponse(ExchangeRate rate) => new(
        rate.Id, rate.BaseCurrency, rate.QuoteCurrency, rate.Rate, rate.EffectiveFrom, rate.UpdatedByName);
}

public sealed record ExchangeRateResponse(
    Guid Id, string BaseCurrency, string QuoteCurrency, decimal Rate, DateTime EffectiveFrom, string UpdatedByName);

public sealed record UpsertExchangeRateRequest(
    string BaseCurrency, string QuoteCurrency, decimal Rate, DateTime? EffectiveFrom = null);
