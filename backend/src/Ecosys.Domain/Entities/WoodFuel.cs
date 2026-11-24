using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>How a delivery's quantity was found.</summary>
public static class WoodMeasureBasis
{
    /// <summary>Gross on the way in, tare on the way out.</summary>
    public const string Weighbridge = "Weighbridge";
    /// <summary>Length × width × height of the stacked load, in cubic metres, times the species' density.</summary>
    public const string StackedVolume = "StackedVolume";
    /// <summary>Tractor or lorry loads at a set weight each.</summary>
    public const string LoadCount = "LoadCount";

    public static readonly IReadOnlyList<string> All = [Weighbridge, StackedVolume, LoadCount];

    public static string? Find(string? value) => All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>What wet wood costs the factory.</summary>
public static class WoodMoistureRules
{
    /// <summary>Payable kg is cut by a percentage for each moisture point over the base.</summary>
    public const string DeductKg = "DeductKg";
    /// <summary>The whole load is paid at a lower share of the price, by moisture band.</summary>
    public const string PriceBands = "PriceBands";

    public static readonly IReadOnlyList<string> All = [DeductKg, PriceBands];

    public static string? Find(string? value) => All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

public static class WoodDecisions
{
    public const string Accepted = "Accepted";
    public const string AcceptedWithDeduction = "AcceptedWithDeduction";
    public const string Rejected = "Rejected";
}

public static class FuelForms
{
    public const string Logs = "Logs";
    public const string Splits = "Splits";
    public const string Slabs = "Slabs";
    public const string Offcuts = "Offcuts";
    public const string Briquettes = "Briquettes";
    public const string OtherBiomass = "OtherBiomass";

    public static readonly IReadOnlyList<string> All = [Logs, Splits, Slabs, Offcuts, Briquettes, OtherBiomass];

    public static string? Find(string? value) => All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

public static class FuelSupplierTypes
{
    public const string Farmer = "Farmer";
    public const string Merchant = "Merchant";
    public const string Cooperative = "Cooperative";
    public const string OwnWoodlot = "OwnWoodlot";

    public static readonly IReadOnlyList<string> All = [Farmer, Merchant, Cooperative, OwnWoodlot];

    public static string? Find(string? value) => All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

public static class WoodStackStatuses
{
    public const string Receiving = "Receiving";
    public const string Seasoning = "Seasoning";
    public const string Ready = "Ready";
    public const string Depleted = "Depleted";

    public static readonly IReadOnlyList<string> All = [Receiving, Seasoning, Ready, Depleted];

    public static string? Find(string? value) => All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

public static class WoodMovementKinds
{
    public const string Receipt = "Receipt";
    public const string Issue = "Issue";
    public const string Adjustment = "Adjustment";
}

/// <summary>Why a count changed the balance.</summary>
public static class WoodAdjustmentReasons
{
    public const string CountVariance = "CountVariance";
    public const string DryingLoss = "DryingLoss";
    public const string Spillage = "Spillage";

    public static readonly IReadOnlyList<string> All = [CountVariance, DryingLoss, Spillage];

    public static string? Find(string? value) => All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// How one factory buys and burns wood. One row per factory; a factory without a row uses the defaults
/// in the properties below, which are starting points to confirm with each factory.
/// </summary>
public sealed class WoodFuelSettings : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid FactoryId { get; set; }
    /// <summary>The basis the delivery form starts on; each delivery can use another.</summary>
    public string DefaultMeasureBasis { get; set; } = WoodMeasureBasis.Weighbridge;
    public string MoistureRule { get; set; } = WoodMoistureRules.DeductKg;
    /// <summary>The wetness the price assumes (percent, wet basis).</summary>
    public decimal BaseMoisturePct { get; set; } = 25m;
    /// <summary>A load wetter than this is rejected.</summary>
    public decimal RejectMoisturePct { get; set; } = 45m;
    /// <summary>DeductKg rule: percent of the weight taken off for each point over the base.</summary>
    public decimal DeductionPctPerPoint { get; set; } = 1.5m;
    /// <summary>PriceBands rule: JSON list of { upToPct, percentOfPrice }, driest first.</summary>
    public string? PriceBandsJson { get; set; }
    /// <summary>Weight of one load, for deliveries counted in loads.</summary>
    public decimal? DefaultKgPerLoad { get; set; }
    /// <summary>Days of recent burning the average daily use is taken over.</summary>
    public int BurnWindowDays { get; set; } = 14;
    /// <summary>Below this many days of cover the yard board warns.</summary>
    public int LowCoverDays { get; set; } = 5;
    /// <summary>Days a new stack seasons before it is ready.</summary>
    public int SeasoningDays { get; set; } = 28;
    /// <summary>Wood a factory aims to burn per unit it makes (kg wood per kg made tea).</summary>
    public decimal? TargetKgPerUnit { get; set; }

    public Tenant? Tenant { get; set; }
    public OrgNode? Factory { get; set; }
}

/// <summary>A kind of wood or biomass, and what a stacked cubic metre of it weighs.</summary>
public sealed class FuelSpecies : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Weight of one stacked cubic metre at the base moisture; confirm with the factory.</summary>
    public decimal KgPerStackedM3 { get; set; }
    /// <summary>Optional heating value, MJ per kg, for comparing fuels.</summary>
    public decimal? EnergyMjPerKg { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Someone who sells wood to the factory (or its own woodlot).</summary>
public sealed class FuelSupplier : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = FuelSupplierTypes.Farmer;
    public string? Phone { get; set; }
    /// <summary>M-Pesa number or bank details, as finance needs them.</summary>
    public string? PaymentDetails { get; set; }
    public string? KraPin { get; set; }
    /// <summary>Only the last four of the national ID are kept.</summary>
    public string? IdLast4 { get; set; }
    public string? SourceArea { get; set; }
    public string? PermitNumber { get; set; }
    public DateTime? PermitExpiresOn { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>The price per tonne, for a supplier (or everyone), species and form, over a period.</summary>
public sealed class FuelPriceAgreement : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    /// <summary>Null: applies to every supplier.</summary>
    public Guid? SupplierId { get; set; }
    public Guid? SpeciesId { get; set; }
    public string? FuelForm { get; set; }
    public decimal PricePerTonne { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public FuelSupplier? Supplier { get; set; }
    public FuelSpecies? Species { get; set; }
}

/// <summary>A pile of wood in the yard. Its balance comes from the ledger.</summary>
public sealed class WoodStack : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid FactoryId { get; set; }
    public string Code { get; set; } = string.Empty;
    public Guid? SpeciesId { get; set; }
    public string Status { get; set; } = WoodStackStatuses.Receiving;
    public DateTime BuiltOn { get; set; } = DateTime.UtcNow;
    public DateTime? ReadyFrom { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public OrgNode? Factory { get; set; }
    public FuelSpecies? Species { get; set; }
}

/// <summary>One load of wood received at a factory gate.</summary>
public sealed class WoodDelivery : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid FactoryId { get; set; }
    public string DeliveryNumber { get; set; } = string.Empty;
    public Guid? SupplierId { get; set; }
    public Guid? SpeciesId { get; set; }
    public string FuelForm { get; set; } = FuelForms.Logs;
    public string? SourceArea { get; set; }
    public string? TransportPermit { get; set; }
    public string? SupplierNoteNumber { get; set; }
    public string? VehicleRegistration { get; set; }
    public string? DriverName { get; set; }
    /// <summary>The gate log entry the truck came in on.</summary>
    public Guid? VehicleMovementId { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    public string MeasureBasis { get; set; } = WoodMeasureBasis.Weighbridge;
    public decimal? GrossKg { get; set; }
    public decimal? TareKg { get; set; }
    public decimal? StackedM3 { get; set; }
    public decimal? Loads { get; set; }
    public decimal? KgPerLoad { get; set; }
    /// <summary>The weight received, however it was found.</summary>
    public decimal NetKg { get; set; }

    /// <summary>Average of the moisture samples; null when none was taken.</summary>
    public decimal? AverageMoisturePct { get; set; }
    public string Decision { get; set; } = WoodDecisions.Accepted;
    public string? DecisionReason { get; set; }
    public decimal RejectedKg { get; set; }
    /// <summary>Kg not paid for because the wood was wet (DeductKg rule).</summary>
    public decimal DeductedKg { get; set; }
    public decimal PayableKg { get; set; }
    public decimal PricePerTonne { get; set; }
    /// <summary>PriceBands rule: the share of the price the load is paid at.</summary>
    public decimal PricePercent { get; set; } = 100m;
    public decimal AmountPayable { get; set; }

    public Guid? StackId { get; set; }
    public Guid? ReceivedByUserId { get; set; }
    public string ReceivedByName { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public OrgNode? Factory { get; set; }
    public FuelSupplier? Supplier { get; set; }
    public FuelSpecies? Species { get; set; }
    public WoodStack? Stack { get; set; }
    public ICollection<WoodMoistureSample> MoistureSamples { get; set; } = new List<WoodMoistureSample>();
}

public sealed class WoodMoistureSample : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid DeliveryId { get; set; }
    public decimal ValuePct { get; set; }
    public DateTime MeasuredAt { get; set; } = DateTime.UtcNow;

    public WoodDelivery? Delivery { get; set; }
}

/// <summary>
/// The yard's ledger: every kg in or out, signed. Stock is the sum of these; nothing else holds a
/// quantity.
/// </summary>
public sealed class WoodStockMovement : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid FactoryId { get; set; }
    public Guid? StackId { get; set; }
    public string Kind { get; set; } = WoodMovementKinds.Receipt;
    /// <summary>Positive in, negative out.</summary>
    public decimal KgChange { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public Guid? DeliveryId { get; set; }
    public Guid? IssueId { get; set; }
    public Guid? CountId { get; set; }
    public string? Reason { get; set; }
    public string ByName { get; set; } = string.Empty;

    public WoodStack? Stack { get; set; }
}

/// <summary>Wood fed to a boiler, dryer or furnace.</summary>
public sealed class WoodIssue : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid FactoryId { get; set; }
    public Guid AssetId { get; set; }
    public Guid? StackId { get; set; }
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public string? Shift { get; set; }
    public decimal Kg { get; set; }
    public string? Notes { get; set; }
    public Guid? IssuedByUserId { get; set; }
    public string IssuedByName { get; set; } = string.Empty;
    /// <summary>The reading this issue added to the asset's wood meter, when it could.</summary>
    public Guid? MeterReadingId { get; set; }

    public Asset? Asset { get; set; }
    public WoodStack? Stack { get; set; }
}

/// <summary>A count of one stack, and the adjustment it caused.</summary>
public sealed class WoodStockCount : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid FactoryId { get; set; }
    public Guid StackId { get; set; }
    public DateTime CountedAt { get; set; } = DateTime.UtcNow;
    public decimal LedgerKg { get; set; }
    public decimal CountedKg { get; set; }
    public decimal VarianceKg { get; set; }
    /// <summary>Measured (cubic metres worked to kg) or Weighed.</summary>
    public string Method { get; set; } = "Measured";
    public string Reason { get; set; } = WoodAdjustmentReasons.CountVariance;
    public string? Notes { get; set; }
    public string CountedByName { get; set; } = string.Empty;

    public WoodStack? Stack { get; set; }
}

/// <summary>What the factory made on a day. Typed in by hand; every wood-per-unit figure divides by it.</summary>
public sealed class ProductionDay : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid FactoryId { get; set; }
    public DateTime Date { get; set; }
    public decimal MadeTeaKg { get; set; }
    public decimal? GreenLeafKg { get; set; }
    public string? Notes { get; set; }
    public string EnteredByName { get; set; } = string.Empty;
}
