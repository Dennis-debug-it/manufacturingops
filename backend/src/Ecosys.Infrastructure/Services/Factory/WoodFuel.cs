using System.Text.Json;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services.Factory;

/// <summary>One step of the PriceBands rule: loads up to this moisture are paid at this share of the price.</summary>
public sealed record WoodPriceBand(decimal UpToPct, decimal PercentOfPrice);

/// <summary>What a delivery turned out to be worth.</summary>
public sealed record WoodAssessment(string Decision, decimal RejectedKg, decimal DeductedKg, decimal PayableKg, decimal PricePercent, decimal Amount, string? Reason);

/// <summary>
/// The arithmetic of buying wood, kept apart from the database so it can be tested on its own. The
/// numbers it works from (base moisture, reject limit, deduction, bands) come from the factory's
/// settings; none of them is built in.
/// </summary>
public static class WoodFuelRules
{
    /// <summary>Starting bands for the PriceBands rule, to be confirmed with the factory.</summary>
    public static readonly IReadOnlyList<WoodPriceBand> DefaultBands = [new(30, 100), new(35, 90), new(40, 80), new(45, 65)];

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    /// <summary>The factory's bands, driest first; the defaults when none are stored or they can't be read.</summary>
    public static IReadOnlyList<WoodPriceBand> ParseBands(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return DefaultBands;
        try
        {
            var bands = JsonSerializer.Deserialize<List<WoodPriceBand>>(json, Json);
            return bands is { Count: > 0 } ? bands.OrderBy(x => x.UpToPct).ToList() : DefaultBands;
        }
        catch (JsonException)
        {
            return DefaultBands;
        }
    }

    /// <summary>Checks and stores bands as JSON; null for the defaults.</summary>
    public static string? SerialiseBands(IReadOnlyList<WoodPriceBand>? bands)
    {
        if (bands is null || bands.Count == 0) return null;
        if (bands.Any(x => x.UpToPct is <= 0 or > 100 || x.PercentOfPrice is < 0 or > 100))
            throw new BusinessRuleException("Each band needs a moisture between 0 and 100 and a share of the price between 0 and 100.");
        if (bands.Select(x => x.UpToPct).Distinct().Count() != bands.Count)
            throw new BusinessRuleException("Two bands cannot stop at the same moisture.");
        return JsonSerializer.Serialize(bands.OrderBy(x => x.UpToPct).ToList());
    }

    /// <summary>The weight received, by the basis the delivery was measured on.</summary>
    public static decimal NetKg(string basis, decimal? grossKg, decimal? tareKg, decimal? stackedM3, decimal? loads, decimal? kgPerLoad, decimal? densityKgPerM3)
    {
        switch (basis)
        {
            case WoodMeasureBasis.Weighbridge:
                if (grossKg is not > 0) throw new BusinessRuleException("Enter the gross weight from the weighbridge.");
                if (tareKg is null or < 0) throw new BusinessRuleException("Enter the tare weight from the weighbridge.");
                if (tareKg >= grossKg) throw new BusinessRuleException("The tare weight must be lower than the gross weight.");
                return Math.Round(grossKg.Value - tareKg.Value, 1);
            case WoodMeasureBasis.StackedVolume:
                if (stackedM3 is not > 0) throw new BusinessRuleException("Enter the stacked volume in cubic metres.");
                if (densityKgPerM3 is not > 0) throw new BusinessRuleException("Choose the species: its weight per stacked cubic metre is needed to turn the volume into kg.");
                return Math.Round(stackedM3.Value * densityKgPerM3.Value, 1);
            case WoodMeasureBasis.LoadCount:
                if (loads is not > 0) throw new BusinessRuleException("Enter how many loads came.");
                if (kgPerLoad is not > 0) throw new BusinessRuleException("Enter the weight of one load, or set a default for the factory.");
                return Math.Round(loads.Value * kgPerLoad.Value, 1);
            default:
                throw new BusinessRuleException($"Measure basis must be one of: {string.Join(", ", WoodMeasureBasis.All)}.");
        }
    }

    /// <summary>
    /// Decides what a load is worth. Wood wetter than the reject limit is rejected unless the clerk
    /// rejected it already. Otherwise the factory's rule applies: DeductKg takes a share off the weight
    /// for each moisture point over the base; PriceBands pays the whole load at a share of the price.
    /// </summary>
    public static WoodAssessment Assess(WoodFuelSettings settings, decimal netKg, decimal? moisturePct, decimal pricePerTonne, bool rejected, string? rejectReason, bool allowAboveRejectLimit = false)
    {
        if (!rejected && !allowAboveRejectLimit && moisturePct is { } m && m > settings.RejectMoisturePct)
        {
            rejected = true;
            rejectReason = $"Moisture {m:0.#}% is above the {settings.RejectMoisturePct:0.#}% limit.";
        }

        if (rejected)
            return new WoodAssessment(WoodDecisions.Rejected, netKg, 0, 0, 0, 0, string.IsNullOrWhiteSpace(rejectReason) ? "Rejected." : rejectReason.Trim());

        var deducted = 0m;
        var percent = 100m;
        string? reason = null;
        if (moisturePct is { } wet && wet > settings.BaseMoisturePct)
        {
            if (string.Equals(settings.MoistureRule, WoodMoistureRules.PriceBands, StringComparison.OrdinalIgnoreCase))
            {
                var bands = ParseBands(settings.PriceBandsJson);
                var band = bands.FirstOrDefault(x => wet <= x.UpToPct) ?? bands[^1];
                percent = band.PercentOfPrice;
                if (percent < 100) reason = $"Moisture {wet:0.#}%: paid at {percent:0.#}% of the price.";
            }
            else
            {
                var cut = Math.Min(100m, (wet - settings.BaseMoisturePct) * settings.DeductionPctPerPoint);
                deducted = Math.Round(netKg * cut / 100m, 1);
                if (deducted > 0) reason = $"Moisture {wet:0.#}% is {wet - settings.BaseMoisturePct:0.#} points over {settings.BaseMoisturePct:0.#}%: {deducted:0.#} kg not paid.";
            }
        }

        var payable = netKg - deducted;
        var amount = Math.Round(payable / 1000m * pricePerTonne * percent / 100m, 2);
        var decision = deducted > 0 || percent < 100 ? WoodDecisions.AcceptedWithDeduction : WoodDecisions.Accepted;
        return new WoodAssessment(decision, 0, deducted, payable, percent, amount, reason);
    }
}

public sealed record WoodActor(Guid? UserId, string Name, bool CanManage);

public sealed record NewWoodDelivery(
    Guid FactoryId,
    Guid? SupplierId,
    Guid? SpeciesId,
    string? FuelForm,
    string? SourceArea,
    string? TransportPermit,
    string? SupplierNoteNumber,
    string? VehicleRegistration,
    string? DriverName,
    Guid? VehicleMovementId,
    string? MeasureBasis,
    decimal? GrossKg,
    decimal? TareKg,
    decimal? StackedM3,
    decimal? Loads,
    decimal? KgPerLoad,
    IReadOnlyList<decimal>? MoistureSamples,
    bool Reject,
    string? RejectReason,
    bool AcceptAboveMoistureLimit,
    decimal? PricePerTonne,
    Guid? StackId,
    string? Notes);

public sealed record NewWoodIssue(Guid FactoryId, Guid AssetId, Guid? StackId, decimal? Kg, decimal? Loads, decimal? KgPerLoad, string? Shift, string? Notes);

public sealed record NewWoodCount(Guid StackId, decimal? CountedKg, decimal? StackedM3, string? Reason, string? Notes);

public sealed record WoodStackRow(Guid Id, string Code, string Status, string? Species, decimal Kg, decimal? AverageMoisturePct, DateTime BuiltOn, DateTime? ReadyFrom, bool IsReady);
public sealed record WoodSupplierRow(Guid? SupplierId, string Supplier, int Deliveries, decimal NetKg, decimal? AverageMoisturePct, int Rejected, decimal RejectedKg, decimal Amount);
public sealed record WoodDailyRow(DateTime Date, decimal ReceivedKg, decimal IssuedKg, decimal MadeTeaKg, decimal? KgPerUnit);

public sealed record WoodYardSummary(
    Guid FactoryId,
    DateTime From,
    DateTime To,
    decimal StockKg,
    decimal UnstackedKg,
    decimal? AverageDailyBurnKg,
    decimal? DaysOfCover,
    bool LowCover,
    int LowCoverDays,
    decimal OpeningKg,
    decimal ReceivedKg,
    decimal IssuedKg,
    decimal AdjustmentKg,
    decimal ClosingKg,
    decimal RejectedKg,
    decimal DeductedKg,
    decimal AmountPayable,
    decimal? AverageMoisturePct,
    decimal MadeTeaKg,
    decimal? KgPerUnit,
    decimal? CostPerUnit,
    decimal? TargetKgPerUnit,
    string FuelUnit,
    IReadOnlyList<WoodStackRow> Stacks,
    IReadOnlyList<WoodSupplierRow> Suppliers,
    IReadOnlyList<WoodDailyRow> Daily);

public interface IWoodFuelService
{
    /// <summary>The factory's settings; the defaults, unsaved, when it has none yet.</summary>
    Task<WoodFuelSettings> SettingsAsync(Guid tenantId, Guid factoryId, CancellationToken cancellationToken = default);

    /// <summary>Adds the usual kinds of wood and biomass the first time a workspace uses the module.</summary>
    Task EnsureSpeciesAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>The price per tonne that applies on a date: the most specific live agreement, newest first.</summary>
    Task<FuelPriceAgreement?> FindPriceAsync(Guid tenantId, Guid? supplierId, Guid? speciesId, string? fuelForm, DateTime at, CancellationToken cancellationToken = default);

    /// <summary>Records a delivery: finds the weight, averages the moisture, decides and prices it, and puts accepted wood into the ledger.</summary>
    Task<WoodDelivery> ReceiveAsync(Guid tenantId, NewWoodDelivery delivery, WoodActor actor, CancellationToken cancellationToken = default);

    /// <summary>Records wood fed to a boiler or dryer: takes it from the ledger and adds it to the asset's wood meter.</summary>
    Task<WoodIssue> IssueAsync(Guid tenantId, NewWoodIssue issue, WoodActor actor, CancellationToken cancellationToken = default);

    /// <summary>Records a count of a stack and posts the difference from the ledger.</summary>
    Task<WoodStockCount> CountAsync(Guid tenantId, NewWoodCount count, WoodActor actor, CancellationToken cancellationToken = default);

    Task<decimal> StockKgAsync(Guid tenantId, Guid factoryId, Guid? stackId, CancellationToken cancellationToken = default);

    Task<WoodYardSummary> SummaryAsync(Guid tenantId, Guid factoryId, DateTime from, DateTime to, CancellationToken cancellationToken = default);
}

internal sealed class WoodFuelService(AppDbContext dbContext, IMeterReadingService meterReadings, IOrgScopeService orgScope) : IWoodFuelService
{
    /// <summary>Usual kinds of wood, with a first guess of what a stacked cubic metre weighs. To be confirmed with the factory.</summary>
    private static readonly (string Name, decimal Kg)[] DefaultSpecies =
    [
        ("Eucalyptus", 380), ("Cypress", 330), ("Pine", 320), ("Grevillea", 350), ("Black wattle", 420),
        ("Mixed hardwood", 400), ("Briquettes", 650), ("Other biomass", 300),
    ];

    private const string FuelMeterUnit = "kg";

    public async Task<WoodFuelSettings> SettingsAsync(Guid tenantId, Guid factoryId, CancellationToken cancellationToken = default) =>
        await dbContext.WoodFuelSettings.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.FactoryId == factoryId, cancellationToken)
        ?? new WoodFuelSettings { TenantId = tenantId, FactoryId = factoryId };

    public async Task EnsureSpeciesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (await dbContext.FuelSpecies.AnyAsync(x => x.TenantId == tenantId, cancellationToken)) return;
        foreach (var (name, kg) in DefaultSpecies)
            dbContext.FuelSpecies.Add(new FuelSpecies { TenantId = tenantId, Name = name, KgPerStackedM3 = kg });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<FuelPriceAgreement?> FindPriceAsync(Guid tenantId, Guid? supplierId, Guid? speciesId, string? fuelForm, DateTime at, CancellationToken cancellationToken = default)
    {
        var form = FuelForms.Find(fuelForm);
        var live = await dbContext.FuelPriceAgreements.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive && x.ValidFrom <= at && (x.ValidTo == null || x.ValidTo >= at)
                && (x.SupplierId == null || x.SupplierId == supplierId)
                && (x.SpeciesId == null || x.SpeciesId == speciesId)
                && (x.FuelForm == null || x.FuelForm == form))
            .ToListAsync(cancellationToken);
        // The most specific agreement wins: the supplier's own, then one for the species, then the form.
        return live.OrderByDescending(x => (x.SupplierId != null ? 4 : 0) + (x.SpeciesId != null ? 2 : 0) + (x.FuelForm != null ? 1 : 0))
            .ThenByDescending(x => x.ValidFrom).FirstOrDefault();
    }

    public async Task<WoodDelivery> ReceiveAsync(Guid tenantId, NewWoodDelivery d, WoodActor actor, CancellationToken cancellationToken = default)
    {
        await RequireFactoryAsync(tenantId, d.FactoryId, cancellationToken);
        var settings = await SettingsAsync(tenantId, d.FactoryId, cancellationToken);
        var basis = d.MeasureBasis is null ? settings.DefaultMeasureBasis : WoodMeasureBasis.Find(d.MeasureBasis)
            ?? throw new BusinessRuleException($"Measure basis must be one of: {string.Join(", ", WoodMeasureBasis.All)}.");

        FuelSupplier? supplier = null;
        if (d.SupplierId is { } supplierId)
            supplier = await dbContext.FuelSuppliers.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == supplierId, cancellationToken)
                ?? throw new BusinessRuleException("That supplier was not found.");
        FuelSpecies? species = null;
        if (d.SpeciesId is { } speciesId)
            species = await dbContext.FuelSpecies.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == speciesId, cancellationToken)
                ?? throw new BusinessRuleException("That kind of wood was not found.");
        var form = d.FuelForm is null ? FuelForms.Logs : FuelForms.Find(d.FuelForm)
            ?? throw new BusinessRuleException($"Form must be one of: {string.Join(", ", FuelForms.All)}.");

        var net = WoodFuelRules.NetKg(basis, d.GrossKg, d.TareKg, d.StackedM3, d.Loads, d.KgPerLoad ?? settings.DefaultKgPerLoad, species?.KgPerStackedM3);

        var samples = (d.MoistureSamples ?? []).ToList();
        if (samples.Any(x => x is < 0 or > 100)) throw new BusinessRuleException("Each moisture reading must be between 0 and 100 percent.");
        decimal? moisture = samples.Count == 0 ? null : Math.Round(samples.Average(), 2);

        var receivedAt = OccurredAt.UtcNow;
        var price = d.PricePerTonne ?? (await FindPriceAsync(tenantId, d.SupplierId, d.SpeciesId, form, receivedAt, cancellationToken))?.PricePerTonne ?? 0m;
        if (price < 0) throw new BusinessRuleException("The price cannot be negative.");
        if (d.AcceptAboveMoistureLimit && !actor.CanManage) throw new ForbiddenException("Only a wood fuel manager can accept wood wetter than the limit.");
        var assessment = WoodFuelRules.Assess(settings, net, moisture, price, d.Reject, d.RejectReason, d.AcceptAboveMoistureLimit);

        WoodStack? stack = null;
        if (d.StackId is { } stackId && assessment.Decision != WoodDecisions.Rejected)
        {
            stack = await dbContext.WoodStacks.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.FactoryId == d.FactoryId && x.Id == stackId, cancellationToken)
                ?? throw new BusinessRuleException("That stack is not in this factory's yard.");
            if (stack.Status == WoodStackStatuses.Depleted || !stack.IsActive) throw new BusinessRuleException($"Stack {stack.Code} is closed. Choose another or open it again.");
        }

        var delivery = new WoodDelivery
        {
            TenantId = tenantId,
            FactoryId = d.FactoryId,
            DeliveryNumber = await NextDeliveryNumberAsync(tenantId, cancellationToken),
            SupplierId = supplier?.Id,
            SpeciesId = species?.Id,
            FuelForm = form,
            SourceArea = Clean(d.SourceArea, 200) ?? supplier?.SourceArea,
            TransportPermit = Clean(d.TransportPermit, 60) ?? supplier?.PermitNumber,
            SupplierNoteNumber = Clean(d.SupplierNoteNumber, 60),
            VehicleRegistration = VisitorService.NormaliseRegistration(d.VehicleRegistration),
            DriverName = Clean(d.DriverName, 200),
            VehicleMovementId = d.VehicleMovementId,
            ReceivedAt = receivedAt,
            MeasureBasis = basis,
            GrossKg = basis == WoodMeasureBasis.Weighbridge ? d.GrossKg : null,
            TareKg = basis == WoodMeasureBasis.Weighbridge ? d.TareKg : null,
            StackedM3 = basis == WoodMeasureBasis.StackedVolume ? d.StackedM3 : null,
            Loads = basis == WoodMeasureBasis.LoadCount ? d.Loads : null,
            KgPerLoad = basis == WoodMeasureBasis.LoadCount ? d.KgPerLoad ?? settings.DefaultKgPerLoad : null,
            NetKg = net,
            AverageMoisturePct = moisture,
            Decision = assessment.Decision,
            DecisionReason = assessment.Reason,
            RejectedKg = assessment.RejectedKg,
            DeductedKg = assessment.DeductedKg,
            PayableKg = assessment.PayableKg,
            PricePerTonne = price,
            PricePercent = assessment.PricePercent,
            AmountPayable = assessment.Amount,
            StackId = stack?.Id,
            ReceivedByUserId = actor.UserId,
            ReceivedByName = actor.Name,
            Notes = Clean(d.Notes, 1000),
        };
        foreach (var value in samples)
            delivery.MoistureSamples.Add(new WoodMoistureSample { TenantId = tenantId, ValuePct = value, MeasuredAt = receivedAt });
        dbContext.WoodDeliveries.Add(delivery);

        if (assessment.Decision != WoodDecisions.Rejected)
        {
            dbContext.WoodStockMovements.Add(new WoodStockMovement
            {
                TenantId = tenantId, FactoryId = d.FactoryId, StackId = stack?.Id, Kind = WoodMovementKinds.Receipt,
                KgChange = net, OccurredAt = receivedAt, DeliveryId = delivery.Id, Reason = delivery.DeliveryNumber, ByName = actor.Name,
            });
            if (stack is not null)
            {
                // Wood added to a stack restarts its seasoning clock.
                var readyFrom = receivedAt.AddDays(settings.SeasoningDays);
                if (stack.ReadyFrom is null || stack.ReadyFrom < readyFrom) stack.ReadyFrom = readyFrom;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return delivery;
    }

    public async Task<WoodIssue> IssueAsync(Guid tenantId, NewWoodIssue i, WoodActor actor, CancellationToken cancellationToken = default)
    {
        await RequireFactoryAsync(tenantId, i.FactoryId, cancellationToken);
        var settings = await SettingsAsync(tenantId, i.FactoryId, cancellationToken);

        var asset = await dbContext.Assets.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == i.AssetId, cancellationToken)
            ?? throw new BusinessRuleException("Choose the boiler or dryer the wood went to.");
        var subtree = await orgScope.GetSubtreeAsync(tenantId, i.FactoryId, cancellationToken);
        if (asset.OrgNodeId is not { } nodeId || !subtree.Contains(nodeId)) throw new BusinessRuleException($"{asset.AssetName} is not in this factory.");

        var kg = i.Kg ?? (i.Loads is > 0 && (i.KgPerLoad ?? settings.DefaultKgPerLoad) is > 0 ? i.Loads * (i.KgPerLoad ?? settings.DefaultKgPerLoad) : null);
        if (kg is not > 0) throw new BusinessRuleException("Enter the kg fed, or the loads and the weight of a load.");
        kg = Math.Round(kg.Value, 1);

        WoodStack? stack = null;
        if (i.StackId is { } stackId)
            stack = await dbContext.WoodStacks.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.FactoryId == i.FactoryId && x.Id == stackId, cancellationToken)
                ?? throw new BusinessRuleException("That stack is not in this factory's yard.");
        // Wood comes out of the stack named, or, with none named, out of wood that was received without a stack.
        // Letting a stack's wood leave "unstacked" would leave that balance below zero.
        Guid? bucket = stack?.Id;
        var available = await dbContext.WoodStockMovements.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.FactoryId == i.FactoryId && x.StackId == bucket)
            .SumAsync(x => (decimal?)x.KgChange, cancellationToken) ?? 0m;
        if (kg > available)
            throw new BusinessRuleException(stack is null
                ? $"Choose the stack the wood came from. Only {available:0.#} kg was received without a stack."
                : $"Stack {stack.Code} holds {available:0.#} kg. Choose another stack, or count this one if it has more.");

        var issuedAt = OccurredAt.UtcNow;
        var issue = new WoodIssue
        {
            TenantId = tenantId, FactoryId = i.FactoryId, AssetId = asset.Id, StackId = stack?.Id, IssuedAt = issuedAt,
            Shift = Clean(i.Shift, 30), Kg = kg.Value, Notes = Clean(i.Notes, 500), IssuedByUserId = actor.UserId, IssuedByName = actor.Name,
        };
        dbContext.WoodIssues.Add(issue);
        dbContext.WoodStockMovements.Add(new WoodStockMovement
        {
            TenantId = tenantId, FactoryId = i.FactoryId, StackId = stack?.Id, Kind = WoodMovementKinds.Issue,
            KgChange = -kg.Value, OccurredAt = issuedAt, IssueId = issue.Id, Reason = asset.AssetName, ByName = actor.Name,
        });
        if (stack is not null && available - kg.Value <= 0.05m) stack.Status = WoodStackStatuses.Depleted;

        issue.MeterReadingId = await RecordOnMeterAsync(tenantId, asset, kg.Value, issuedAt, actor, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return issue;
    }

    /// <summary>
    /// Adds the kg to the asset's wood meter, so the boiler's fuel history and the existing fuel-per-unit
    /// figure see it without a second entry. Skipped, not failed, when the entry is older than the meter's
    /// latest reading: a queued entry from a phone must never be lost for that.
    /// </summary>
    private async Task<Guid?> RecordOnMeterAsync(Guid tenantId, Asset asset, decimal kg, DateTime at, WoodActor actor, CancellationToken cancellationToken)
    {
        var meter = await dbContext.Meters.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.AssetId == asset.Id && x.MeterType == MeterTypes.Fuel && x.IsActive && x.Unit == FuelMeterUnit, cancellationToken);
        if (meter is null)
        {
            meter = new Meter { TenantId = tenantId, AssetId = asset.Id, Name = "Wood fuel burnt", MeterType = MeterTypes.Fuel, Unit = FuelMeterUnit, IsCumulative = true };
            dbContext.Meters.Add(meter);
        }
        else if (meter.LastReadingAt is { } last && at < last)
        {
            return null;
        }

        try
        {
            var reading = await meterReadings.RecordAsync(meter,
                new NewMeterReading((meter.CurrentValue ?? 0m) + kg, at, MeterReadingSources.Manual, false, "Wood issued to this asset", actor.UserId, actor.Name), cancellationToken);
            return reading.Id;
        }
        catch (BusinessRuleException)
        {
            return null;
        }
    }

    public async Task<WoodStockCount> CountAsync(Guid tenantId, NewWoodCount c, WoodActor actor, CancellationToken cancellationToken = default)
    {
        var stack = await dbContext.WoodStacks.Include(x => x.Species).SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == c.StackId, cancellationToken)
            ?? throw new BusinessRuleException("That stack was not found.");
        decimal counted;
        string method;
        if (c.StackedM3 is { } m3)
        {
            if (m3 < 0) throw new BusinessRuleException("The volume cannot be negative.");
            var density = stack.Species?.KgPerStackedM3 ?? throw new BusinessRuleException($"Set the kind of wood for stack {stack.Code}: its weight per cubic metre turns the volume into kg.");
            counted = Math.Round(m3 * density, 1);
            method = "Measured";
        }
        else if (c.CountedKg is { } kg && kg >= 0)
        {
            counted = Math.Round(kg, 1);
            method = "Weighed";
        }
        else
        {
            throw new BusinessRuleException("Enter the weight counted, or the stacked volume.");
        }

        var reason = c.Reason is null ? WoodAdjustmentReasons.CountVariance : WoodAdjustmentReasons.Find(c.Reason)
            ?? throw new BusinessRuleException($"Reason must be one of: {string.Join(", ", WoodAdjustmentReasons.All)}.");
        var ledger = await StockKgAsync(tenantId, stack.FactoryId, stack.Id, cancellationToken);
        var count = new WoodStockCount
        {
            TenantId = tenantId, FactoryId = stack.FactoryId, StackId = stack.Id, CountedAt = OccurredAt.UtcNow, LedgerKg = ledger,
            CountedKg = counted, VarianceKg = Math.Round(counted - ledger, 1), Method = method, Reason = reason, Notes = Clean(c.Notes, 500), CountedByName = actor.Name,
        };
        dbContext.WoodStockCounts.Add(count);
        if (count.VarianceKg != 0)
        {
            dbContext.WoodStockMovements.Add(new WoodStockMovement
            {
                TenantId = tenantId, FactoryId = stack.FactoryId, StackId = stack.Id, Kind = WoodMovementKinds.Adjustment,
                KgChange = count.VarianceKg, OccurredAt = count.CountedAt, CountId = count.Id, Reason = reason, ByName = actor.Name,
            });
        }

        if (counted <= 0.05m) stack.Status = WoodStackStatuses.Depleted;
        await dbContext.SaveChangesAsync(cancellationToken);
        return count;
    }

    public async Task<decimal> StockKgAsync(Guid tenantId, Guid factoryId, Guid? stackId, CancellationToken cancellationToken = default) =>
        await dbContext.WoodStockMovements.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.FactoryId == factoryId && (stackId == null || x.StackId == stackId))
            .SumAsync(x => (decimal?)x.KgChange, cancellationToken) ?? 0m;

    public async Task<WoodYardSummary> SummaryAsync(Guid tenantId, Guid factoryId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var settings = await SettingsAsync(tenantId, factoryId, cancellationToken);
        var now = DateTime.UtcNow;

        var movements = await dbContext.WoodStockMovements.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.FactoryId == factoryId)
            .Select(x => new { x.StackId, x.Kind, x.KgChange, x.OccurredAt }).ToListAsync(cancellationToken);
        var stock = movements.Sum(x => x.KgChange);
        var unstacked = movements.Where(x => x.StackId == null).Sum(x => x.KgChange);
        var inPeriod = movements.Where(x => x.OccurredAt >= from && x.OccurredAt < to).ToList();
        var opening = movements.Where(x => x.OccurredAt < from).Sum(x => x.KgChange);
        var received = inPeriod.Where(x => x.Kind == WoodMovementKinds.Receipt).Sum(x => x.KgChange);
        var issued = -inPeriod.Where(x => x.Kind == WoodMovementKinds.Issue).Sum(x => x.KgChange);
        var adjustment = inPeriod.Where(x => x.Kind == WoodMovementKinds.Adjustment).Sum(x => x.KgChange);
        var closing = movements.Where(x => x.OccurredAt < to).Sum(x => x.KgChange);

        var windowStart = now.AddDays(-Math.Max(1, settings.BurnWindowDays));
        var burnt = -movements.Where(x => x.Kind == WoodMovementKinds.Issue && x.OccurredAt >= windowStart).Sum(x => x.KgChange);
        decimal? dailyBurn = burnt > 0 ? Math.Round(burnt / Math.Max(1, settings.BurnWindowDays), 1) : null;
        decimal? cover = dailyBurn is > 0 ? Math.Round(stock / dailyBurn.Value, 1) : null;

        var deliveries = await dbContext.WoodDeliveries.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.FactoryId == factoryId && x.ReceivedAt >= from && x.ReceivedAt < to)
            .Select(x => new { x.SupplierId, Supplier = x.Supplier != null ? x.Supplier.Name : null, x.NetKg, x.AverageMoisturePct, x.Decision, x.RejectedKg, x.DeductedKg, x.AmountPayable }).ToListAsync(cancellationToken);
        var accepted = deliveries.Where(x => x.Decision != WoodDecisions.Rejected).ToList();
        var amount = Math.Round(deliveries.Sum(x => x.AmountPayable), 2);
        var moistureValues = accepted.Where(x => x.AverageMoisturePct != null).ToList();
        decimal? avgMoisture = moistureValues.Count == 0 || moistureValues.Sum(x => x.NetKg) <= 0 ? null
            : Math.Round(moistureValues.Sum(x => x.AverageMoisturePct!.Value * x.NetKg) / moistureValues.Sum(x => x.NetKg), 1);

        var production = await dbContext.ProductionDays.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.FactoryId == factoryId && x.Date >= from.Date && x.Date < to.Date)
            .Select(x => new { x.Date, x.MadeTeaKg }).ToListAsync(cancellationToken);
        var madeTea = production.Sum(x => x.MadeTeaKg);
        decimal? kgPerUnit = madeTea > 0 && issued > 0 ? Math.Round(issued / madeTea, 3) : null;
        // What the burnt wood cost: the average price paid per kg received in the period.
        var acceptedKg = accepted.Sum(x => x.NetKg);
        decimal? costPerUnit = madeTea > 0 && issued > 0 && acceptedKg > 0 ? Math.Round(issued * (amount / acceptedKg) / madeTea, 2) : null;

        var rawStacks = await dbContext.WoodStacks.AsNoTracking().Include(x => x.Species)
            .Where(x => x.TenantId == tenantId && x.FactoryId == factoryId && x.IsActive).OrderBy(x => x.Code).ToListAsync(cancellationToken);
        var stackIntake = await dbContext.WoodDeliveries.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.FactoryId == factoryId && x.StackId != null && x.AverageMoisturePct != null && x.Decision != WoodDecisions.Rejected)
            .Select(x => new { x.StackId, x.NetKg, Moisture = x.AverageMoisturePct!.Value }).ToListAsync(cancellationToken);
        var stacks = rawStacks.Select(s =>
        {
            var mine = stackIntake.Where(x => x.StackId == s.Id).ToList();
            var weight = mine.Sum(x => x.NetKg);
            decimal? wet = weight > 0 ? Math.Round(mine.Sum(x => x.Moisture * x.NetKg) / weight, 1) : null;
            var kg = movements.Where(x => x.StackId == s.Id).Sum(x => x.KgChange);
            var ready = s.Status == WoodStackStatuses.Ready || (s.Status != WoodStackStatuses.Depleted && s.ReadyFrom is { } due && due <= now);
            return new WoodStackRow(s.Id, s.Code, s.Status, s.Species?.Name, Math.Round(kg, 1), wet, s.BuiltOn, s.ReadyFrom, ready);
        }).ToList();

        var suppliers = deliveries.GroupBy(x => new { x.SupplierId, Name = x.Supplier ?? "No supplier recorded" })
            .Select(g =>
            {
                var good = g.Where(x => x.Decision != WoodDecisions.Rejected && x.AverageMoisturePct != null).ToList();
                return new WoodSupplierRow(g.Key.SupplierId, g.Key.Name, g.Count(), Math.Round(g.Where(x => x.Decision != WoodDecisions.Rejected).Sum(x => x.NetKg), 1),
                    good.Count == 0 ? null : Math.Round(good.Average(x => x.AverageMoisturePct!.Value), 1),
                    g.Count(x => x.Decision == WoodDecisions.Rejected), Math.Round(g.Sum(x => x.RejectedKg), 1), Math.Round(g.Sum(x => x.AmountPayable), 2));
            }).OrderByDescending(x => x.NetKg).ToList();

        var daily = new List<WoodDailyRow>();
        for (var day = from.Date; day < to.Date && day <= from.Date.AddDays(92); day = day.AddDays(1))
        {
            var rows = inPeriod.Where(x => x.OccurredAt.Date == day).ToList();
            var dayIssued = -rows.Where(x => x.Kind == WoodMovementKinds.Issue).Sum(x => x.KgChange);
            var dayReceived = rows.Where(x => x.Kind == WoodMovementKinds.Receipt).Sum(x => x.KgChange);
            var tea = production.Where(x => x.Date.Date == day).Sum(x => x.MadeTeaKg);
            if (dayIssued == 0 && dayReceived == 0 && tea == 0) continue;
            daily.Add(new WoodDailyRow(day, Math.Round(dayReceived, 1), Math.Round(dayIssued, 1), tea, tea > 0 && dayIssued > 0 ? Math.Round(dayIssued / tea, 3) : null));
        }

        return new WoodYardSummary(factoryId, from, to, Math.Round(stock, 1), Math.Round(unstacked, 1), dailyBurn, cover,
            cover is { } c && c < settings.LowCoverDays, settings.LowCoverDays,
            Math.Round(opening, 1), Math.Round(received, 1), Math.Round(issued, 1), Math.Round(adjustment, 1), Math.Round(closing, 1),
            Math.Round(deliveries.Sum(x => x.RejectedKg), 1), Math.Round(deliveries.Sum(x => x.DeductedKg), 1), amount, avgMoisture,
            madeTea, kgPerUnit, costPerUnit, settings.TargetKgPerUnit, "kg", stacks, suppliers, daily);
    }

    private async Task RequireFactoryAsync(Guid tenantId, Guid factoryId, CancellationToken cancellationToken)
    {
        if (!await dbContext.OrgNodes.AnyAsync(x => x.TenantId == tenantId && x.Id == factoryId && x.Level == OrgNodeLevels.Factory, cancellationToken))
            throw new BusinessRuleException("Choose the factory.");
    }

    /// <summary>WF-000001, counted across the tenant and including deliveries added but not yet saved.</summary>
    private async Task<string> NextDeliveryNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var count = await dbContext.WoodDeliveries.CountAsync(x => x.TenantId == tenantId, cancellationToken)
            + dbContext.ChangeTracker.Entries<WoodDelivery>().Count(x => x.State == EntityState.Added && x.Entity.TenantId == tenantId);
        return $"WF-{count + 1:000000}";
    }

    private static string? Clean(string? value, int max)
    {
        var text = value?.Trim();
        return string.IsNullOrEmpty(text) ? null : text.Length > max ? text[..max] : text;
    }
}
