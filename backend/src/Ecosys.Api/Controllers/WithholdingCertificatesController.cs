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
/// Tax a client withheld when paying (D10). A certificate settles part of an invoice through the
/// same allocation ledger as payments, so balances, overdue status and statements need no special
/// cases. Works on every invoice, not only project invoices.
/// </summary>
[ApiController]
[Authorize]
[Route("api")]
public sealed class WithholdingCertificatesController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet("withholding-certificates")]
    public async Task<ActionResult<IReadOnlyCollection<WithholdingCertificateResponse>>> List(
        [FromQuery] Guid? clientId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceView);
        var query = dbContext.WithholdingCertificates.Include(x => x.Invoice).Include(x => x.Client).Where(x => x.TenantId == TenantId);
        if (clientId.HasValue)
            query = query.Where(x => x.ClientId == clientId);
        var certificates = await query.OrderByDescending(x => x.CertificateDate).Take(500).ToListAsync(cancellationToken);
        return Ok(certificates.Select(Map).ToList());
    }

    [HttpGet("invoices/{invoiceId:guid}/withholding-certificates")]
    public async Task<ActionResult<IReadOnlyCollection<WithholdingCertificateResponse>>> ListForInvoice(Guid invoiceId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceView);
        var certificates = await dbContext.WithholdingCertificates
            .Include(x => x.Invoice).Include(x => x.Client)
            .Where(x => x.TenantId == TenantId && x.InvoiceId == invoiceId)
            .OrderBy(x => x.CertificateDate)
            .ToListAsync(cancellationToken);
        return Ok(certificates.Select(Map).ToList());
    }

    [HttpPost("invoices/{invoiceId:guid}/withholding-certificates")]
    public async Task<ActionResult<WithholdingCertificateResponse>> Record(Guid invoiceId, [FromBody] RecordWithholdingRequest request, CancellationToken cancellationToken)
    {
        // Same permission as recording a payment: both settle an invoice.
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);
        var invoice = await dbContext.TenantInvoices.Include(x => x.Client)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == invoiceId, cancellationToken)
            ?? throw new NotFoundException("Invoice not found.");

        if (invoice.Status is "Void" or "Draft")
            throw new BusinessRuleException($"A {invoice.Status.ToLowerInvariant()} invoice can't take a withholding certificate.");
        if (invoice.DocumentType != "Invoice")
            throw new BusinessRuleException("Withholding applies to tax invoices, not proforma invoices.");
        if (request.Amount <= 0)
            throw new BusinessRuleException("Amount must be greater than zero.");
        if (request.Amount > invoice.Balance)
            throw new BusinessRuleException($"The amount is more than the invoice's outstanding balance ({invoice.Currency} {invoice.Balance:N2}).");
        if (string.IsNullOrWhiteSpace(request.CertificateNumber))
            throw new BusinessRuleException("Certificate number is required.");
        var taxType = WithholdingTaxTypes.All.FirstOrDefault(x => string.Equals(x, request.TaxType?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessRuleException("Tax type must be VAT or IncomeTax.");
        var certificateNumber = request.CertificateNumber.Trim();
        if (await dbContext.WithholdingCertificates.AnyAsync(x => x.TenantId == TenantId && x.ClientId == invoice.ClientId
                && x.CertificateNumber == certificateNumber && x.Status == "Confirmed", cancellationToken))
            throw new BusinessRuleException($"Certificate {certificateNumber} is already recorded for this client.");

        var certificate = new WithholdingCertificate
        {
            TenantId = TenantId,
            ClientId = invoice.ClientId,
            InvoiceId = invoice.Id,
            TaxType = taxType,
            RatePercent = request.RatePercent,
            Amount = request.Amount,
            CertificateNumber = certificateNumber,
            CertificateDate = request.CertificateDate,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            RecordedByUserId = UserId,
        };
        dbContext.WithholdingCertificates.Add(certificate);
        await InvoiceBalanceCalculator.ApplyAllocationsAsync(dbContext, TenantId, AllocationSourceTypes.Withholding, certificate.Id,
            new[] { (invoice, certificate.Amount) }, cancellationToken);
        await InvoiceBalanceCalculator.RecalcClientBalancesAsync(dbContext, TenantId, [invoice], cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(TenantId, UserId, "finance.withholding.recorded", nameof(WithholdingCertificate), certificate.Id.ToString(),
            $"Withholding {taxType} certificate {certificateNumber} ({invoice.Currency} {certificate.Amount:N2}) recorded against invoice {invoice.InvoiceNumber}.", cancellationToken);
        certificate.Invoice = invoice;
        return Ok(Map(certificate));
    }

    [HttpPost("withholding-certificates/{id:guid}/reverse")]
    public async Task<ActionResult<WithholdingCertificateResponse>> Reverse(Guid id, [FromBody] ReverseWithholdingRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);
        var certificate = await dbContext.WithholdingCertificates.Include(x => x.Invoice).Include(x => x.Client)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Withholding certificate not found.");
        if (certificate.Status != "Confirmed")
            throw new BusinessRuleException("This certificate is already reversed.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new BusinessRuleException("Give a reason for reversing the certificate.");

        certificate.Status = "Reversed";
        certificate.Notes = string.IsNullOrWhiteSpace(certificate.Notes) ? $"Reversed: {request.Reason.Trim()}" : $"{certificate.Notes}\nReversed: {request.Reason.Trim()}";
        var invoices = await InvoiceBalanceCalculator.ReverseAllocationsForSourceAsync(dbContext, TenantId, AllocationSourceTypes.Withholding, certificate.Id, cancellationToken);
        await InvoiceBalanceCalculator.RecalcClientBalancesAsync(dbContext, TenantId, invoices, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(TenantId, UserId, "finance.withholding.reversed", nameof(WithholdingCertificate), certificate.Id.ToString(),
            $"Withholding certificate {certificate.CertificateNumber} reversed. Reason: {request.Reason.Trim()}", cancellationToken);
        return Ok(Map(certificate));
    }

    private static WithholdingCertificateResponse Map(WithholdingCertificate x) => new(
        x.Id, x.InvoiceId, x.Invoice?.InvoiceNumber, x.ClientId, x.Client?.ClientName, x.TaxType, x.RatePercent, x.Amount,
        x.Invoice?.Currency ?? "KES", x.CertificateNumber, x.CertificateDate, x.Status, x.Notes, x.CreatedAt);
}

public sealed record WithholdingCertificateResponse(
    Guid Id, Guid InvoiceId, string? InvoiceNumber, Guid ClientId, string? ClientName, string TaxType, decimal? RatePercent,
    decimal Amount, string Currency, string CertificateNumber, DateOnly CertificateDate, string Status, string? Notes, DateTime CreatedAt);

public sealed record RecordWithholdingRequest(string TaxType, decimal? RatePercent, decimal Amount, string CertificateNumber, DateOnly CertificateDate, string? Notes);

public sealed record ReverseWithholdingRequest(string? Reason);
