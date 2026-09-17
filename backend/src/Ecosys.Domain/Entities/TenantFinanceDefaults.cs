using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// Tenant-owned finance defaults (Phase 11) — mirrors the shape of the platform's own tax/finance
/// settings, but scoped per tenant. Currency itself is not duplicated here; it already lives on
/// <see cref="Tenant.Currency"/>.
/// </summary>
public sealed class TenantFinanceDefaults : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public bool EnableVat { get; set; } = true;
    public string TaxName { get; set; } = "VAT";
    public decimal DefaultVatRate { get; set; } = 16;
    public string DefaultPaymentTerms { get; set; } = "30 days";
    public int DefaultInvoiceDueDays { get; set; } = 30;
    public int DefaultQuotationValidityDays { get; set; } = 14;
    public bool DefaultExpenseApprovalRequired { get; set; } = true;

    public Tenant? Tenant { get; set; }
}
