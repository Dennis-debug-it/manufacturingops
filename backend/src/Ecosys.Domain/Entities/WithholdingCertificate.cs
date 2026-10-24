using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public static class WithholdingTaxTypes
{
    public const string Vat = "VAT";
    public const string IncomeTax = "IncomeTax";

    public static readonly string[] All = [Vat, IncomeTax];
}

/// <summary>
/// Tax a client withheld when paying an invoice, evidenced by their withholding certificate. It
/// settles that part of the invoice exactly like a payment, through <see cref="PaymentAllocation"/>
/// with <see cref="AllocationSourceTypes.Withholding"/>. Ecosys records it; it does not file with KRA.
/// </summary>
public sealed class WithholdingCertificate : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ClientId { get; set; }
    public Guid InvoiceId { get; set; }
    public string TaxType { get; set; } = WithholdingTaxTypes.Vat;
    public decimal? RatePercent { get; set; }
    public decimal Amount { get; set; }
    public string CertificateNumber { get; set; } = string.Empty;
    public DateOnly CertificateDate { get; set; }
    public string Status { get; set; } = "Confirmed"; // Confirmed | Reversed
    public string? Notes { get; set; }
    public Guid RecordedByUserId { get; set; }

    public Tenant? Tenant { get; set; }
    public Client? Client { get; set; }
    public TenantInvoice? Invoice { get; set; }
}
