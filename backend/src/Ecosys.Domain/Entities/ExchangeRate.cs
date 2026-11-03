using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// A manually maintained currency conversion rate used for multi-currency invoicing.
/// One row per (base, quote) pair per effective date; the latest effective row wins.
/// </summary>
public sealed class ExchangeRate : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string BaseCurrency { get; set; } = string.Empty;   // e.g. USD
    public string QuoteCurrency { get; set; } = string.Empty;  // e.g. KES
    public decimal Rate { get; set; }
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedByUserId { get; set; }
    public string UpdatedByName { get; set; } = string.Empty;

    public Tenant? Tenant { get; set; }
}
