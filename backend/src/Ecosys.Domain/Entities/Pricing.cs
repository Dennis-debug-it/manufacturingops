using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// The platform's price list for factory customers, priced per factory per year: add-on prices, the users
/// each factory includes, group discounts, one-off fees, the pilot offer and the hardware catalogue.
/// Edition prices per factory sit on <see cref="LicensePlan.FactoryAnnualPrice"/>. One row for the platform.
/// </summary>
public sealed class PriceBook : AuditableEntity
{
    public string Currency { get; set; } = "KES";
    /// <summary>VAT added on quotes, in percent (Kenya: 16).</summary>
    public decimal VatPercent { get; set; } = 16m;

    public decimal VisitorsFactoryAnnualPrice { get; set; } = 40_000m;
    public decimal SecurityFactoryAnnualPrice { get; set; } = 55_000m;
    /// <summary>Visitors and Security taken together, per factory per year (replaces the two prices).</summary>
    public decimal SecuritySuiteFactoryAnnualPrice { get; set; } = 85_000m;

    public int IncludedOfficeUsersPerFactory { get; set; } = 15;
    public int IncludedFieldUsersPerFactory { get; set; } = 30;
    /// <summary>Each user beyond those included, per month.</summary>
    public decimal ExtraUserMonthlyPrice { get; set; } = 1_000m;

    /// <summary>Group discounts by number of factories, JSON [{"minFactories":5,"percent":10}, …].</summary>
    public string DiscountTiersJson { get; set; } = """[{"minFactories":5,"percent":10},{"minFactories":20,"percent":20}]""";

    /// <summary>One-off implementation per factory: survey, data loading, tagging, checkpoints, training.</summary>
    public decimal ImplementationFeeMin { get; set; } = 150_000m;
    public decimal ImplementationFeeMax { get; set; } = 250_000m;
    public decimal ImplementationFeeDefault { get; set; } = 200_000m;

    /// <summary>On-premises installs pay the subscription times this, plus a setup fee.</summary>
    public decimal OnPremisesMultiplier { get; set; } = 1.5m;
    public decimal OnPremisesSetupFee { get; set; } = 500_000m;

    public int PilotMonths { get; set; } = 3;
    public decimal PilotFee { get; set; } = 60_000m;
    /// <summary>The pilot fee is taken off year one when the customer continues.</summary>
    public bool PilotCreditedToYearOne { get; set; } = true;

    /// <summary>Hardware sold at cost plus a margin, JSON [{"code":"nfc-tag","name":"…","unit":"each","unitCost":120,"marginPercent":30}, …].</summary>
    public string HardwareJson { get; set; } = DefaultHardwareJson;

    public int QuoteValidDays { get; set; } = 30;
    /// <summary>Printed at the foot of every quote.</summary>
    public string? QuoteTerms { get; set; } =
        "Prices are in Kenya shillings and exclude VAT unless shown. The subscription is billed yearly in advance. "
        + "Implementation is invoiced on signing. Hardware is supplied at cost plus handling.";

    public const string DefaultHardwareJson = """
        [{"code":"qr-label","name":"Weatherproof QR label (asset or checkpoint)","unit":"each","unitCost":60,"marginPercent":40},
         {"code":"nfc-tag","name":"NFC tag (NTAG215, outdoor)","unit":"each","unitCost":120,"marginPercent":40},
         {"code":"visitor-card","name":"Visitor access card with QR","unit":"each","unitCost":150,"marginPercent":30},
         {"code":"visitor-card-nfc","name":"Visitor access card with NFC chip","unit":"each","unitCost":250,"marginPercent":30},
         {"code":"guard-phone","name":"Android phone with NFC for a guard post","unit":"each","unitCost":16000,"marginPercent":15}]
        """;
}

public static class QuoteStatuses
{
    public const string Draft = "Draft";
    public const string Sent = "Sent";
    public const string Accepted = "Accepted";
    public const string Declined = "Declined";

    public static readonly IReadOnlyList<string> All = [Draft, Sent, Accepted, Declined];
}

/// <summary>A price quote for a prospect or a tenant, worked out from the price book when it was saved.</summary>
public sealed class PriceQuote : AuditableEntity
{
    /// <summary>e.g. "Q-2026-0007".</summary>
    public string QuoteNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    /// <summary>The workspace it is for, when the customer already has one.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>essentials, professional, enterprise, or null for Visitors/Security without Maintenance.</summary>
    public string? Edition { get; set; }
    public bool Visitors { get; set; }
    public bool Security { get; set; }
    public int Factories { get; set; } = 1;
    public int ExtraOfficeUsers { get; set; }
    public int ExtraFieldUsers { get; set; }
    public decimal ImplementationFeePerFactory { get; set; }
    public bool OnPremises { get; set; }
    public bool IncludePilot { get; set; }
    /// <summary>A discount agreed for this customer, replacing the group discount. Null uses the tiers.</summary>
    public decimal? DiscountPercentOverride { get; set; }
    /// <summary>Hardware asked for, JSON [{"code":"nfc-tag","quantity":40}].</summary>
    public string? HardwareJson { get; set; }
    public string? Notes { get; set; }

    public string Status { get; set; } = QuoteStatuses.Draft;
    public DateTime ValidUntil { get; set; }
    /// <summary>The lines and totals as worked out when last saved, JSON, so a later price change does not alter it.</summary>
    public string ResultJson { get; set; } = "{}";
    public decimal AnnualSubscription { get; set; }
    public decimal YearOneTotal { get; set; }
    public string? PreparedByName { get; set; }
    /// <summary>The year-one invoice raised from this quote.</summary>
    public Guid? InvoiceId { get; set; }
    /// <summary>The pilot invoice raised from this quote.</summary>
    public Guid? PilotInvoiceId { get; set; }
}

public static class PricingBases
{
    /// <summary>Office and field seats at the edition's seat prices (the original model).</summary>
    public const string PerSeat = "PerSeat";
    /// <summary>A price per factory per year from the price book, with users included per factory.</summary>
    public const string PerFactory = "PerFactory";
}
