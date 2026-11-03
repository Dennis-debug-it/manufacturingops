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
[Route("api/finance/credit-terms")]
public sealed class CreditTermsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IAuditLogService auditLogService,
    IUserAccessService userAccessService) : TenantAwareControllerBase(tenantContext)
{
    // ── List ─────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<CreditTermResponse>>> List(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceView);

        await EnsureDefaultTermsSeededAsync(cancellationToken);

        var terms = await dbContext.CreditTerms
            .Where(x => x.TenantId == TenantId && x.IsActive)
            .OrderBy(x => x.NetDays)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return Ok(terms.Select(MapTerm).ToList());
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [HttpPost]
    public async Task<ActionResult<CreditTermResponse>> Create(
        [FromBody] UpsertCreditTermRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageRates);

        var code = request.Code.Trim().ToUpperInvariant();
        var exists = await dbContext.CreditTerms.AnyAsync(
            x => x.TenantId == TenantId && x.Code == code, cancellationToken);
        if (exists)
            throw new BusinessRuleException($"A credit term with code '{code}' already exists.");

        if (request.IsDefault)
            await ClearDefaultAsync(cancellationToken);

        var term = new CreditTerm
        {
            TenantId = TenantId,
            Name = request.Name.Trim(),
            Code = code,
            NetDays = request.NetDays,
            IsDepositRequired = request.IsDepositRequired,
            DepositPercent = request.DepositPercent,
            CreditLimit = request.CreditLimit,
            Description = request.Description?.Trim(),
            IsDefault = request.IsDefault,
            IsActive = true,
        };

        dbContext.CreditTerms.Add(term);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "finance.credit_term.created", nameof(CreditTerm), term.Id.ToString(), $"Created credit term '{term.Name}'", cancellationToken);

        return CreatedAtAction(nameof(List), new { id = term.Id }, MapTerm(term));
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CreditTermResponse>> Update(
        Guid id, [FromBody] UpsertCreditTermRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageRates);

        var term = await dbContext.CreditTerms
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Credit term not found.");

        var code = request.Code.Trim().ToUpperInvariant();
        var codeConflict = await dbContext.CreditTerms.AnyAsync(
            x => x.TenantId == TenantId && x.Code == code && x.Id != id, cancellationToken);
        if (codeConflict)
            throw new BusinessRuleException($"A credit term with code '{code}' already exists.");

        if (request.IsDefault && !term.IsDefault)
            await ClearDefaultAsync(cancellationToken);

        term.Name = request.Name.Trim();
        term.Code = code;
        term.NetDays = request.NetDays;
        term.IsDepositRequired = request.IsDepositRequired;
        term.DepositPercent = request.DepositPercent;
        term.CreditLimit = request.CreditLimit;
        term.Description = request.Description?.Trim();
        term.IsDefault = request.IsDefault;

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "finance.credit_term.updated", nameof(CreditTerm), term.Id.ToString(), $"Updated credit term '{term.Name}'", cancellationToken);

        return Ok(MapTerm(term));
    }

    // ── Set Default ──────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/set-default")]
    public async Task<ActionResult<CreditTermResponse>> SetDefault(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageRates);

        var term = await dbContext.CreditTerms
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id && x.IsActive, cancellationToken)
            ?? throw new NotFoundException("Credit term not found.");

        await ClearDefaultAsync(cancellationToken);
        term.IsDefault = true;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(MapTerm(term));
    }

    // ── Soft Delete ──────────────────────────────────────────────────────────

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageRates);

        var term = await dbContext.CreditTerms
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Credit term not found.");

        term.IsActive = false;
        if (term.IsDefault) term.IsDefault = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "finance.credit_term.deleted", nameof(CreditTerm), term.Id.ToString(), $"Deleted credit term '{term.Name}'", cancellationToken);

        return NoContent();
    }

    // ── Client Credit Summary ────────────────────────────────────────────────

    [HttpGet("/api/clients/{clientId:guid}/credit-summary")]
    public async Task<ActionResult<ClientCreditSummaryResponse>> GetClientCreditSummary(
        Guid clientId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceView);

        var client = await dbContext.Clients
            .Include(x => x.DefaultCreditTerm)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == clientId, cancellationToken)
            ?? throw new NotFoundException("Client not found.");

        var now = DateTime.UtcNow;
        var overdueAmount = await dbContext.TenantInvoices
            .Where(x => x.TenantId == TenantId && x.ClientId == clientId
                        && x.Balance > 0 && x.DueDate < now
                        && x.Status != "Void" && x.Status != "Paid")
            .SumAsync(x => x.Balance, cancellationToken);

        var oldestInvoice = await dbContext.TenantInvoices
            .Where(x => x.TenantId == TenantId && x.ClientId == clientId
                        && x.Balance > 0 && x.Status != "Void" && x.Status != "Paid")
            .OrderBy(x => x.InvoiceDate)
            .Select(x => (DateTime?)x.InvoiceDate)
            .FirstOrDefaultAsync(cancellationToken);

        var effectiveLimit = client.CreditLimit ?? client.DefaultCreditTerm?.CreditLimit;
        var availableCredit = effectiveLimit.HasValue
            ? effectiveLimit.Value - client.CurrentBalance
            : (decimal?)null;

        return Ok(new ClientCreditSummaryResponse(
            CurrentBalance: client.CurrentBalance,
            CreditLimit: effectiveLimit,
            AvailableCredit: availableCredit,
            OverdueAmount: overdueAmount,
            OldestInvoiceDate: oldestInvoice?.ToString("yyyy-MM-dd"),
            TermName: client.DefaultCreditTerm?.Name));
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task ClearDefaultAsync(CancellationToken cancellationToken)
    {
        var current = await dbContext.CreditTerms
            .Where(x => x.TenantId == TenantId && x.IsDefault)
            .ToListAsync(cancellationToken);
        foreach (var t in current) t.IsDefault = false;
    }

    /// <summary>
    /// Seeds 5 default credit terms for a tenant if none exist yet.
    /// </summary>
    private async Task EnsureDefaultTermsSeededAsync(CancellationToken cancellationToken)
    {
        var any = await dbContext.CreditTerms.AnyAsync(x => x.TenantId == TenantId, cancellationToken);
        if (any) return;

        var seeds = new[]
        {
            new CreditTerm { TenantId = TenantId, Name = "Due on Receipt",               Code = "DOR",   NetDays = 0,  IsDepositRequired = false, DepositPercent = 0,  IsDefault = false, Description = "Payment due immediately on receipt." },
            new CreditTerm { TenantId = TenantId, Name = "Net 15",                        Code = "NET15", NetDays = 15, IsDepositRequired = false, DepositPercent = 0,  IsDefault = false, Description = "Payment due within 15 days." },
            new CreditTerm { TenantId = TenantId, Name = "Net 30",                        Code = "NET30", NetDays = 30, IsDepositRequired = false, DepositPercent = 0,  IsDefault = true,  Description = "Payment due within 30 days." },
            new CreditTerm { TenantId = TenantId, Name = "Net 60",                        Code = "NET60", NetDays = 60, IsDepositRequired = false, DepositPercent = 0,  IsDefault = false, Description = "Payment due within 60 days." },
            new CreditTerm { TenantId = TenantId, Name = "50% Deposit + Balance on Completion", Code = "DEP50", NetDays = 0, IsDepositRequired = true,  DepositPercent = 50, IsDefault = false, Description = "50% deposit due upfront; balance due on project completion." },
        };

        dbContext.CreditTerms.AddRange(seeds);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static CreditTermResponse MapTerm(CreditTerm t) =>
        new(t.Id, t.TenantId, t.Name, t.Code, t.NetDays, t.IsDepositRequired,
            t.DepositPercent, t.CreditLimit, t.Description, t.IsDefault, t.IsActive, t.CreatedAt);
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public sealed record UpsertCreditTermRequest(
    string Name,
    string Code,
    int NetDays,
    bool IsDepositRequired,
    decimal DepositPercent,
    decimal? CreditLimit,
    string? Description,
    bool IsDefault);

public sealed record CreditTermResponse(
    Guid Id,
    Guid TenantId,
    string Name,
    string Code,
    int NetDays,
    bool IsDepositRequired,
    decimal DepositPercent,
    decimal? CreditLimit,
    string? Description,
    bool IsDefault,
    bool IsActive,
    DateTime CreatedAt);

public sealed record ClientCreditSummaryResponse(
    decimal CurrentBalance,
    decimal? CreditLimit,
    decimal? AvailableCredit,
    decimal OverdueAmount,
    string? OldestInvoiceDate,
    string? TermName);
