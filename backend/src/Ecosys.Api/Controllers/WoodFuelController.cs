using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using Ecosys.Api.Common;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// Wood fuel: suppliers and prices, deliveries weighed or measured and tested for moisture, the wood
/// yard's stacks and ledger, wood fed to boilers and dryers, counts, the daily made-tea figure and the
/// yard board. An add-on module (<c>woodfuel</c>).
/// </summary>
[ApiController]
[Authorize]
[Route("api/wood-fuel")]
public sealed class WoodFuelController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IOrgScopeService orgScopeService,
    IWoodFuelService woodFuel) : TenantAwareControllerBase(tenantContext)
{
    private bool CanManage => IsAdmin || HasPermission(PermissionNames.WoodFuelManage);
    private bool CanReceive => CanManage || HasPermission(PermissionNames.WoodFuelReceive);
    private bool CanIssue => CanManage || HasPermission(PermissionNames.WoodFuelIssue);
    private bool CanView => CanReceive || CanIssue;
    private WoodActor Actor => new(UserId, UserDisplayName ?? "Unknown", CanManage);

    private static readonly string[] BoilerWords = ["boiler", "dryer", "drier", "furnace", "thermic", "heater", "steam"];

    /* ── Lookups and settings ────────────────────────────────────────────── */

    /// <summary>Everything the forms need: factories, kinds of wood, suppliers, stacks and the boilers and dryers.</summary>
    [HttpGet("lookups")]
    public async Task<ActionResult<WoodLookups>> Lookups([FromQuery] Guid? factoryId, CancellationToken cancellationToken)
    {
        EnsureView();
        await woodFuel.EnsureSpeciesAsync(TenantId, cancellationToken);
        var visible = await orgScopeService.VisibleNodesAsync(TenantId, UserId, null, cancellationToken);
        var factories = await dbContext.OrgNodes.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.Level == OrgNodeLevels.Factory && (visible == null || visible.Contains(x.Id)))
            .OrderBy(x => x.Name).Select(x => new WoodNamed(x.Id, x.Name)).ToListAsync(cancellationToken);
        var species = await dbContext.FuelSpecies.AsNoTracking().Where(x => x.TenantId == TenantId)
            .OrderBy(x => x.Name).Select(x => new WoodSpeciesRow(x.Id, x.Name, x.KgPerStackedM3, x.EnergyMjPerKg, x.IsActive)).ToListAsync(cancellationToken);
        var suppliers = await dbContext.FuelSuppliers.AsNoTracking().Where(x => x.TenantId == TenantId && x.IsActive)
            .OrderBy(x => x.Name).Select(x => new WoodNamed(x.Id, x.Name)).ToListAsync(cancellationToken);

        var chosen = factoryId ?? factories.FirstOrDefault()?.Id;
        var stacks = new List<WoodNamed>();
        var assets = new List<WoodNamed>();
        WoodSettingsDto? settings = null;
        if (chosen is { } factory)
        {
            await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, factory, cancellationToken);
            stacks = await dbContext.WoodStacks.AsNoTracking()
                .Where(x => x.TenantId == TenantId && x.FactoryId == factory && x.IsActive && x.Status != WoodStackStatuses.Depleted)
                .OrderBy(x => x.Code).Select(x => new WoodNamed(x.Id, x.Code)).ToListAsync(cancellationToken);
            var subtree = await orgScopeService.GetSubtreeAsync(TenantId, factory, cancellationToken);
            var candidates = await dbContext.Assets.AsNoTracking()
                .Where(x => x.TenantId == TenantId && x.Status == "Active" && x.OrgNodeId != null && subtree.Contains(x.OrgNodeId.Value))
                .Select(x => new { x.Id, x.AssetName, x.AssetCode, x.AssetType })
                .ToListAsync(cancellationToken);
            var withFuelMeter = (await dbContext.Meters.AsNoTracking()
                .Where(x => x.TenantId == TenantId && x.MeterType == MeterTypes.Fuel && x.IsActive).Select(x => x.AssetId).Distinct().ToListAsync(cancellationToken)).ToHashSet();
            assets = candidates
                .Where(x => withFuelMeter.Contains(x.Id) || BoilerWords.Any(w => $"{x.AssetName} {x.AssetType}".Contains(w, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(x => x.AssetName).Select(x => new WoodNamed(x.Id, $"{x.AssetName} ({x.AssetCode})")).ToList();
            settings = ToDto(await woodFuel.SettingsAsync(TenantId, factory, cancellationToken));
        }

        return Ok(new WoodLookups(factories, chosen, species, suppliers, stacks, assets, settings, WoodMeasureBasis.All, WoodMoistureRules.All, FuelForms.All,
            FuelSupplierTypes.All, WoodStackStatuses.All, WoodAdjustmentReasons.All, CanManage, CanReceive, CanIssue));
    }

    [HttpGet("settings")]
    public async Task<ActionResult<WoodSettingsDto>> GetSettings([FromQuery] Guid factoryId, CancellationToken cancellationToken)
    {
        EnsureView();
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, factoryId, cancellationToken);
        return Ok(ToDto(await woodFuel.SettingsAsync(TenantId, factoryId, cancellationToken)));
    }

    [HttpPut("settings")]
    public async Task<ActionResult<WoodSettingsDto>> SaveSettings([FromBody] SaveWoodSettingsRequest request, CancellationToken cancellationToken)
    {
        EnsureManage();
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, request.FactoryId, cancellationToken);
        if (!await dbContext.OrgNodes.AnyAsync(x => x.TenantId == TenantId && x.Id == request.FactoryId && x.Level == OrgNodeLevels.Factory, cancellationToken))
            throw new BusinessRuleException("Choose the factory.");
        var settings = await dbContext.WoodFuelSettings.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.FactoryId == request.FactoryId, cancellationToken);
        if (settings is null)
        {
            settings = new WoodFuelSettings { TenantId = TenantId, FactoryId = request.FactoryId };
            dbContext.WoodFuelSettings.Add(settings);
        }

        settings.DefaultMeasureBasis = WoodMeasureBasis.Find(request.DefaultMeasureBasis) ?? throw new BusinessRuleException($"Measure basis must be one of: {string.Join(", ", WoodMeasureBasis.All)}.");
        settings.MoistureRule = WoodMoistureRules.Find(request.MoistureRule) ?? throw new BusinessRuleException($"Moisture rule must be one of: {string.Join(", ", WoodMoistureRules.All)}.");
        if (request.BaseMoisturePct is < 0 or > 100 || request.RejectMoisturePct is < 0 or > 100) throw new BusinessRuleException("Moisture limits are between 0 and 100 percent.");
        if (request.RejectMoisturePct <= request.BaseMoisturePct) throw new BusinessRuleException("The reject limit must be higher than the base moisture.");
        if (request.DeductionPctPerPoint is < 0 or > 20) throw new BusinessRuleException("The deduction is between 0 and 20 percent for each point.");
        if (request.DefaultKgPerLoad is <= 0) throw new BusinessRuleException("The weight of a load must be more than zero.");
        if (request.BurnWindowDays is < 1 or > 90) throw new BusinessRuleException("The burn window is between 1 and 90 days.");
        if (request.LowCoverDays is < 0 or > 60) throw new BusinessRuleException("Low cover is between 0 and 60 days.");
        if (request.SeasoningDays is < 0 or > 365) throw new BusinessRuleException("Seasoning is between 0 and 365 days.");
        if (request.TargetKgPerUnit is <= 0) throw new BusinessRuleException("The target must be more than zero.");
        settings.BaseMoisturePct = request.BaseMoisturePct;
        settings.RejectMoisturePct = request.RejectMoisturePct;
        settings.DeductionPctPerPoint = request.DeductionPctPerPoint;
        settings.PriceBandsJson = WoodFuelRules.SerialiseBands(request.PriceBands);
        settings.DefaultKgPerLoad = request.DefaultKgPerLoad;
        settings.BurnWindowDays = request.BurnWindowDays;
        settings.LowCoverDays = request.LowCoverDays;
        settings.SeasoningDays = request.SeasoningDays;
        settings.TargetKgPerUnit = request.TargetKgPerUnit;
        if (request.WetAlertMoisturePct is { } wetAlert)
        {
            if (wetAlert < request.BaseMoisturePct || wetAlert > request.RejectMoisturePct) throw new BusinessRuleException("The wet-wood alert must be between the base moisture and the reject limit.");
            settings.WetAlertMoisturePct = wetAlert;
        }
        else
        {
            // Not sent: keep the alert level, brought inside the limits if they moved.
            settings.WetAlertMoisturePct = Math.Min(request.RejectMoisturePct, Math.Max(request.BaseMoisturePct, settings.WetAlertMoisturePct));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(settings));
    }

    /* ── Kinds of wood ───────────────────────────────────────────────────── */

    [HttpGet("species")]
    public async Task<ActionResult<IReadOnlyList<WoodSpeciesRow>>> Species(CancellationToken cancellationToken)
    {
        EnsureView();
        await woodFuel.EnsureSpeciesAsync(TenantId, cancellationToken);
        return Ok(await dbContext.FuelSpecies.AsNoTracking().Where(x => x.TenantId == TenantId).OrderBy(x => x.Name)
            .Select(x => new WoodSpeciesRow(x.Id, x.Name, x.KgPerStackedM3, x.EnergyMjPerKg, x.IsActive)).ToListAsync(cancellationToken));
    }

    [HttpPost("species")]
    public async Task<ActionResult<WoodSpeciesRow>> CreateSpecies([FromBody] SaveSpeciesRequest request, CancellationToken cancellationToken)
    {
        EnsureManage();
        var species = new FuelSpecies { TenantId = TenantId };
        await ApplySpeciesAsync(species, request, cancellationToken);
        dbContext.FuelSpecies.Add(species);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new WoodSpeciesRow(species.Id, species.Name, species.KgPerStackedM3, species.EnergyMjPerKg, species.IsActive));
    }

    [HttpPut("species/{id:guid}")]
    public async Task<ActionResult<WoodSpeciesRow>> UpdateSpecies(Guid id, [FromBody] SaveSpeciesRequest request, CancellationToken cancellationToken)
    {
        EnsureManage();
        var species = await dbContext.FuelSpecies.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("That kind of wood was not found.");
        await ApplySpeciesAsync(species, request, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new WoodSpeciesRow(species.Id, species.Name, species.KgPerStackedM3, species.EnergyMjPerKg, species.IsActive));
    }

    private async Task ApplySpeciesAsync(FuelSpecies species, SaveSpeciesRequest request, CancellationToken cancellationToken)
    {
        var name = Required(request.Name, "Name");
        if (request.KgPerStackedM3 <= 0) throw new BusinessRuleException("Enter what a stacked cubic metre weighs, in kg.");
        if (request.EnergyMjPerKg is <= 0) throw new BusinessRuleException("The heating value must be more than zero.");
        if (await dbContext.FuelSpecies.AnyAsync(x => x.TenantId == TenantId && x.Id != species.Id && x.Name == name, cancellationToken))
            throw new BusinessRuleException($"{name} is already on the list.");
        species.Name = name;
        species.KgPerStackedM3 = request.KgPerStackedM3;
        species.EnergyMjPerKg = request.EnergyMjPerKg;
        species.IsActive = request.IsActive ?? species.IsActive;
    }

    /* ── Suppliers and prices ────────────────────────────────────────────── */

    [HttpGet("suppliers")]
    public async Task<ActionResult<IReadOnlyList<WoodSupplierDto>>> Suppliers([FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        EnsureView();
        var rows = await dbContext.FuelSuppliers.AsNoTracking().Where(x => x.TenantId == TenantId && (includeInactive || x.IsActive)).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        return Ok(rows.Select(x => ToDto(x, CanManage)).ToList());
    }

    [HttpPost("suppliers")]
    public async Task<ActionResult<WoodSupplierDto>> CreateSupplier([FromBody] SaveSupplierRequest request, CancellationToken cancellationToken)
    {
        EnsureManage();
        var supplier = new FuelSupplier { TenantId = TenantId };
        ApplySupplier(supplier, request);
        dbContext.FuelSuppliers.Add(supplier);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(supplier, true));
    }

    [HttpPut("suppliers/{id:guid}")]
    public async Task<ActionResult<WoodSupplierDto>> UpdateSupplier(Guid id, [FromBody] SaveSupplierRequest request, CancellationToken cancellationToken)
    {
        EnsureManage();
        var supplier = await dbContext.FuelSuppliers.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("That supplier was not found.");
        ApplySupplier(supplier, request);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(supplier, true));
    }

    private static void ApplySupplier(FuelSupplier supplier, SaveSupplierRequest request)
    {
        supplier.Name = Required(request.Name, "Name");
        supplier.Type = FuelSupplierTypes.Find(request.Type) ?? throw new BusinessRuleException($"Type must be one of: {string.Join(", ", FuelSupplierTypes.All)}.");
        supplier.Phone = Clean(request.Phone);
        supplier.PaymentDetails = Clean(request.PaymentDetails);
        supplier.KraPin = Clean(request.KraPin);
        // Only the last four digits of the national ID are kept (Data Protection Act 2019: keep no more than is needed).
        var digits = new string((request.NationalId ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());
        if (digits.Length > 0) supplier.IdLast4 = digits.Length <= 4 ? digits : digits[^4..];
        supplier.SourceArea = Clean(request.SourceArea);
        supplier.PermitNumber = Clean(request.PermitNumber);
        supplier.PermitExpiresOn = request.PermitExpiresOn is { } expiry ? DateTime.SpecifyKind(expiry.Date, DateTimeKind.Utc) : null;
        supplier.Notes = Clean(request.Notes);
        supplier.IsActive = request.IsActive ?? supplier.IsActive;
    }

    [HttpGet("prices")]
    public async Task<ActionResult<IReadOnlyList<WoodPriceDto>>> Prices(CancellationToken cancellationToken)
    {
        EnsureView();
        var rows = await dbContext.FuelPriceAgreements.AsNoTracking().Include(x => x.Supplier).Include(x => x.Species)
            .Where(x => x.TenantId == TenantId).OrderByDescending(x => x.ValidFrom).ToListAsync(cancellationToken);
        return Ok(rows.Select(ToDto).ToList());
    }

    [HttpPost("prices")]
    public async Task<ActionResult<WoodPriceDto>> CreatePrice([FromBody] SavePriceRequest request, CancellationToken cancellationToken)
    {
        EnsureManage();
        var price = new FuelPriceAgreement { TenantId = TenantId };
        await ApplyPriceAsync(price, request, cancellationToken);
        dbContext.FuelPriceAgreements.Add(price);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Single(await Prices(cancellationToken)).Single(x => x.Id == price.Id));
    }

    [HttpPut("prices/{id:guid}")]
    public async Task<ActionResult<WoodPriceDto>> UpdatePrice(Guid id, [FromBody] SavePriceRequest request, CancellationToken cancellationToken)
    {
        EnsureManage();
        var price = await dbContext.FuelPriceAgreements.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("That price was not found.");
        await ApplyPriceAsync(price, request, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Single(await Prices(cancellationToken)).Single(x => x.Id == price.Id));
    }

    private async Task ApplyPriceAsync(FuelPriceAgreement price, SavePriceRequest request, CancellationToken cancellationToken)
    {
        if (request.PricePerTonne < 0) throw new BusinessRuleException("The price cannot be negative.");
        if (request.ValidTo is { } to && to.Date < request.ValidFrom.Date) throw new BusinessRuleException("The end date is before the start date.");
        if (request.SupplierId is { } supplierId && !await dbContext.FuelSuppliers.AnyAsync(x => x.TenantId == TenantId && x.Id == supplierId, cancellationToken))
            throw new BusinessRuleException("That supplier was not found.");
        if (request.SpeciesId is { } speciesId && !await dbContext.FuelSpecies.AnyAsync(x => x.TenantId == TenantId && x.Id == speciesId, cancellationToken))
            throw new BusinessRuleException("That kind of wood was not found.");
        price.SupplierId = request.SupplierId;
        price.SpeciesId = request.SpeciesId;
        price.FuelForm = request.FuelForm is null ? null : FuelForms.Find(request.FuelForm) ?? throw new BusinessRuleException($"Form must be one of: {string.Join(", ", FuelForms.All)}.");
        price.PricePerTonne = request.PricePerTonne;
        price.ValidFrom = DateTime.SpecifyKind(request.ValidFrom.Date, DateTimeKind.Utc);
        price.ValidTo = request.ValidTo is { } end ? DateTime.SpecifyKind(end.Date, DateTimeKind.Utc) : null;
        price.Notes = Clean(request.Notes);
        price.IsActive = request.IsActive ?? price.IsActive;
    }

    /* ── Stacks ──────────────────────────────────────────────────────────── */

    [HttpPost("stacks")]
    public async Task<ActionResult<WoodNamed>> CreateStack([FromBody] SaveStackRequest request, CancellationToken cancellationToken)
    {
        EnsureReceive();
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, request.FactoryId, cancellationToken);
        var stack = new WoodStack { TenantId = TenantId, FactoryId = request.FactoryId, BuiltOn = OccurredAt.UtcNow };
        await ApplyStackAsync(stack, request, cancellationToken);
        dbContext.WoodStacks.Add(stack);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new WoodNamed(stack.Id, stack.Code));
    }

    [HttpPut("stacks/{id:guid}")]
    public async Task<ActionResult<WoodNamed>> UpdateStack(Guid id, [FromBody] SaveStackRequest request, CancellationToken cancellationToken)
    {
        EnsureReceive();
        var stack = await dbContext.WoodStacks.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("That stack was not found.");
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, stack.FactoryId, cancellationToken);
        await ApplyStackAsync(stack, request, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new WoodNamed(stack.Id, stack.Code));
    }

    private async Task ApplyStackAsync(WoodStack stack, SaveStackRequest request, CancellationToken cancellationToken)
    {
        var code = Required(request.Code, "Code");
        if (await dbContext.WoodStacks.AnyAsync(x => x.TenantId == TenantId && x.FactoryId == stack.FactoryId && x.Id != stack.Id && x.Code == code, cancellationToken))
            throw new BusinessRuleException($"Stack {code} already exists in this yard.");
        if (request.SpeciesId is { } speciesId && !await dbContext.FuelSpecies.AnyAsync(x => x.TenantId == TenantId && x.Id == speciesId, cancellationToken))
            throw new BusinessRuleException("That kind of wood was not found.");
        stack.Code = code;
        stack.SpeciesId = request.SpeciesId;
        stack.Status = request.Status is null ? stack.Status : WoodStackStatuses.Find(request.Status) ?? throw new BusinessRuleException($"Status must be one of: {string.Join(", ", WoodStackStatuses.All)}.");
        stack.Notes = Clean(request.Notes);
        stack.IsActive = request.IsActive ?? stack.IsActive;
    }

    /* ── Deliveries ──────────────────────────────────────────────────────── */

    [HttpGet("deliveries")]
    public async Task<ActionResult<IReadOnlyList<WoodDeliveryDto>>> Deliveries(
        [FromQuery] Guid? factoryId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] Guid? supplierId, [FromQuery] string? decision, [FromQuery] string? q, CancellationToken cancellationToken)
    {
        EnsureView();
        return Ok(await QueryDeliveriesAsync(factoryId, from, to, supplierId, decision, q, 300, cancellationToken));
    }

    [HttpGet("deliveries/{id:guid}")]
    public async Task<ActionResult<WoodDeliveryDto>> Delivery(Guid id, CancellationToken cancellationToken)
    {
        EnsureView();
        var row = await DeliveryQuery().SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("That delivery was not found.");
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, row.FactoryId, cancellationToken);
        return Ok(ToDto(row));
    }

    /// <summary>Receive a delivery. Works from a phone with no signal: the time it happened comes from the device.</summary>
    [HttpPost("deliveries")]
    public async Task<ActionResult<WoodDeliveryDto>> Receive([FromBody] ReceiveDeliveryRequest request, CancellationToken cancellationToken)
    {
        EnsureReceive();
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, request.FactoryId, cancellationToken);
        var delivery = await woodFuel.ReceiveAsync(TenantId, new NewWoodDelivery(request.FactoryId, request.SupplierId, request.SpeciesId, request.FuelForm, request.SourceArea,
            request.TransportPermit, request.SupplierNoteNumber, request.VehicleRegistration, request.DriverName, request.VehicleMovementId, request.MeasureBasis,
            request.GrossKg, request.TareKg, request.StackedM3, request.Loads, request.KgPerLoad, request.MoistureSamples, request.Reject, request.RejectReason,
            request.AcceptAboveMoistureLimit, request.PricePerTonne, request.StackId, request.Notes), Actor, cancellationToken);
        return Ok(ToDto(await DeliveryQuery().SingleAsync(x => x.Id == delivery.Id, cancellationToken)));
    }

    /// <summary>The deliveries of a period as a CSV for finance: what each load weighed, its moisture and what is payable.</summary>
    [HttpGet("deliveries/export")]
    public async Task<IActionResult> ExportDeliveries([FromQuery] Guid? factoryId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] Guid? supplierId, CancellationToken cancellationToken)
    {
        EnsureManage();
        var rows = await QueryDeliveriesAsync(factoryId, from, to, supplierId, null, null, 5000, cancellationToken);
        var csv = new StringBuilder();
        csv.AppendLine("Delivery,Received,Factory,Supplier,Phone,Payment details,Species,Form,Vehicle,Measured by,Net kg,Moisture %,Decision,Rejected kg,Deducted kg,Payable kg,Price per tonne,Price %,Amount payable,Stack,Supplier note,Permit");
        foreach (var r in rows.OrderBy(x => x.ReceivedAt))
        {
            string F(decimal? v) => v?.ToString("0.##", CultureInfo.InvariantCulture) ?? "";
            csv.AppendLine(string.Join(",", Csv.Escape(r.DeliveryNumber), r.ReceivedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), Csv.Escape(r.Factory), Csv.Escape(r.Supplier),
                Csv.Escape(r.SupplierPhone), Csv.Escape(r.SupplierPaymentDetails), Csv.Escape(r.Species), Csv.Escape(r.FuelForm), Csv.Escape(r.VehicleRegistration), Csv.Escape(r.MeasureBasis),
                F(r.NetKg), F(r.AverageMoisturePct), Csv.Escape(r.Decision), F(r.RejectedKg), F(r.DeductedKg), F(r.PayableKg), F(r.PricePerTonne), F(r.PricePercent), F(r.AmountPayable),
                Csv.Escape(r.Stack), Csv.Escape(r.SupplierNoteNumber), Csv.Escape(r.TransportPermit)));
        }

        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv", "wood-deliveries.csv");
    }

    private IQueryable<WoodDelivery> DeliveryQuery() =>
        dbContext.WoodDeliveries.AsNoTracking().Include(x => x.Factory).Include(x => x.Supplier).Include(x => x.Species).Include(x => x.Stack).Include(x => x.MoistureSamples);

    private async Task<List<WoodDeliveryDto>> QueryDeliveriesAsync(Guid? factoryId, DateTime? from, DateTime? to, Guid? supplierId, string? decision, string? q, int take, CancellationToken cancellationToken)
    {
        var nodes = await orgScopeService.VisibleNodesAsync(TenantId, UserId, factoryId, cancellationToken);
        var end = to ?? DateTime.UtcNow.AddDays(1);
        var start = from ?? end.AddDays(-30);
        var query = DeliveryQuery().Where(x => x.TenantId == TenantId && (nodes == null || nodes.Contains(x.FactoryId)) && x.ReceivedAt >= start && x.ReceivedAt < end);
        if (supplierId is { } supplier) query = query.Where(x => x.SupplierId == supplier);
        if (!string.IsNullOrWhiteSpace(decision)) query = query.Where(x => x.Decision == decision);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(x => x.DeliveryNumber.ToLower().Contains(term) || (x.VehicleRegistration != null && x.VehicleRegistration.ToLower().Contains(term))
                || (x.Supplier != null && x.Supplier.Name.ToLower().Contains(term)) || (x.SupplierNoteNumber != null && x.SupplierNoteNumber.ToLower().Contains(term)));
        }

        return (await query.OrderByDescending(x => x.ReceivedAt).Take(take).ToListAsync(cancellationToken)).Select(ToDto).ToList();
    }

    /* ── Issues and counts ───────────────────────────────────────────────── */

    [HttpGet("issues")]
    public async Task<ActionResult<IReadOnlyList<WoodIssueDto>>> Issues([FromQuery] Guid? factoryId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        EnsureView();
        var nodes = await orgScopeService.VisibleNodesAsync(TenantId, UserId, factoryId, cancellationToken);
        var end = to ?? DateTime.UtcNow.AddDays(1);
        var start = from ?? end.AddDays(-14);
        var rows = await dbContext.WoodIssues.AsNoTracking().Include(x => x.Asset).Include(x => x.Stack)
            .Where(x => x.TenantId == TenantId && (nodes == null || nodes.Contains(x.FactoryId)) && x.IssuedAt >= start && x.IssuedAt < end)
            .OrderByDescending(x => x.IssuedAt).Take(500).ToListAsync(cancellationToken);
        return Ok(rows.Select(x => new WoodIssueDto(x.Id, x.FactoryId, x.AssetId, x.Asset?.AssetName ?? string.Empty, x.StackId, x.Stack?.Code, x.IssuedAt, x.Shift, x.Kg, x.Notes, x.IssuedByName, x.MeterReadingId != null)).ToList());
    }

    [HttpPost("issues")]
    public async Task<ActionResult<WoodIssueDto>> Issue([FromBody] IssueWoodRequest request, CancellationToken cancellationToken)
    {
        EnsureIssue();
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, request.FactoryId, cancellationToken);
        var issue = await woodFuel.IssueAsync(TenantId, new NewWoodIssue(request.FactoryId, request.AssetId, request.StackId, request.Kg, request.Loads, request.KgPerLoad, request.Shift, request.Notes), Actor, cancellationToken);
        var stack = issue.StackId is { } id ? await dbContext.WoodStacks.AsNoTracking().Where(x => x.Id == id).Select(x => x.Code).SingleOrDefaultAsync(cancellationToken) : null;
        var asset = await dbContext.Assets.AsNoTracking().Where(x => x.Id == issue.AssetId).Select(x => x.AssetName).SingleAsync(cancellationToken);
        return Ok(new WoodIssueDto(issue.Id, issue.FactoryId, issue.AssetId, asset, issue.StackId, stack, issue.IssuedAt, issue.Shift, issue.Kg, issue.Notes, issue.IssuedByName, issue.MeterReadingId != null));
    }

    [HttpGet("counts")]
    public async Task<ActionResult<IReadOnlyList<WoodCountDto>>> Counts([FromQuery] Guid? factoryId, CancellationToken cancellationToken)
    {
        EnsureView();
        var nodes = await orgScopeService.VisibleNodesAsync(TenantId, UserId, factoryId, cancellationToken);
        var rows = await dbContext.WoodStockCounts.AsNoTracking().Include(x => x.Stack)
            .Where(x => x.TenantId == TenantId && (nodes == null || nodes.Contains(x.FactoryId))).OrderByDescending(x => x.CountedAt).Take(200).ToListAsync(cancellationToken);
        return Ok(rows.Select(ToDto).ToList());
    }

    /// <summary>Count a stack. The difference from the ledger is posted at once, so counting is for wood managers and yard clerks only.</summary>
    [HttpPost("counts")]
    public async Task<ActionResult<WoodCountDto>> Count([FromBody] CountStackRequest request, CancellationToken cancellationToken)
    {
        EnsureReceive();
        var stack = await dbContext.WoodStacks.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == request.StackId, cancellationToken)
            ?? throw new NotFoundException("That stack was not found.");
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, stack.FactoryId, cancellationToken);
        var count = await woodFuel.CountAsync(TenantId, new NewWoodCount(request.StackId, request.CountedKg, request.StackedM3, request.Reason, request.Notes), Actor, cancellationToken);
        count.Stack = stack;
        return Ok(ToDto(count));
    }

    /* ── Production and the yard board ───────────────────────────────────── */

    [HttpGet("production")]
    public async Task<ActionResult<IReadOnlyList<ProductionDayDto>>> Production([FromQuery] Guid factoryId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        EnsureView();
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, factoryId, cancellationToken);
        var end = (to ?? DateTime.UtcNow).Date.AddDays(1);
        var start = (from ?? end.AddDays(-31)).Date;
        var rows = await dbContext.ProductionDays.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.FactoryId == factoryId && x.Date >= start && x.Date < end).OrderByDescending(x => x.Date).ToListAsync(cancellationToken);
        return Ok(rows.Select(x => new ProductionDayDto(x.Id, x.FactoryId, x.Date, x.MadeTeaKg, x.GreenLeafKg, x.Notes, x.EnteredByName)).ToList());
    }

    /// <summary>Enter or correct a day's made tea. One row a day; saving the same day again replaces it.</summary>
    [HttpPut("production")]
    public async Task<ActionResult<ProductionDayDto>> SaveProduction([FromBody] SaveProductionRequest request, CancellationToken cancellationToken)
    {
        EnsureManage();
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, request.FactoryId, cancellationToken);
        if (!await dbContext.OrgNodes.AnyAsync(x => x.TenantId == TenantId && x.Id == request.FactoryId && x.Level == OrgNodeLevels.Factory, cancellationToken))
            throw new BusinessRuleException("Choose the factory.");
        if (request.MadeTeaKg < 0 || request.GreenLeafKg is < 0) throw new BusinessRuleException("Weights cannot be negative.");
        var date = DateTime.SpecifyKind(request.Date.Date, DateTimeKind.Utc);
        if (date > DateTime.UtcNow.Date.AddDays(1)) throw new BusinessRuleException("The date is in the future.");
        var day = await dbContext.ProductionDays.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.FactoryId == request.FactoryId && x.Date == date, cancellationToken);
        if (day is null)
        {
            day = new ProductionDay { TenantId = TenantId, FactoryId = request.FactoryId, Date = date };
            dbContext.ProductionDays.Add(day);
        }

        day.MadeTeaKg = Math.Round(request.MadeTeaKg, 1);
        day.GreenLeafKg = request.GreenLeafKg is { } leaf ? Math.Round(leaf, 1) : null;
        day.Notes = Clean(request.Notes);
        day.EnteredByName = UserDisplayName ?? "Unknown";
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new ProductionDayDto(day.Id, day.FactoryId, day.Date, day.MadeTeaKg, day.GreenLeafKg, day.Notes, day.EnteredByName));
    }

    /// <summary>The yard board: stock, cover, stacks, a period's receipts, burn and cost, suppliers and the day by day.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<WoodYardSummary>> Summary([FromQuery] Guid factoryId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        EnsureView();
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, factoryId, cancellationToken);
        var end = to ?? DateTime.UtcNow.Date.AddDays(1);
        var start = from ?? end.AddDays(-30);
        if (end <= start) throw new BusinessRuleException("The end of the period is before its start.");
        return Ok(await woodFuel.SummaryAsync(TenantId, factoryId, start, end, cancellationToken));
    }

    /* ── Helpers ─────────────────────────────────────────────────────────── */

    private void EnsureView() { if (!CanView) throw new ForbiddenException("You do not have access to wood fuel."); }
    private void EnsureReceive() { if (!CanReceive) throw new ForbiddenException("Receiving wood is for the yard clerk and wood fuel managers."); }
    private void EnsureIssue() { if (!CanIssue) throw new ForbiddenException("Recording wood fed to a boiler is for the boiler house and wood fuel managers."); }
    private void EnsureManage() { if (!CanManage) throw new ForbiddenException("This is for wood fuel managers."); }

    private static T Single<T>(ActionResult<T> result) => result.Value ?? (T)((ObjectResult)result.Result!).Value!;
    private static string Required(string? value, string label) => string.IsNullOrWhiteSpace(value) ? throw new BusinessRuleException($"{label} is required.") : value.Trim();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static WoodSettingsDto ToDto(WoodFuelSettings s) => new(s.FactoryId, s.DefaultMeasureBasis, s.MoistureRule, s.BaseMoisturePct, s.RejectMoisturePct, s.DeductionPctPerPoint,
        WoodFuelRules.ParseBands(s.PriceBandsJson), s.DefaultKgPerLoad, s.BurnWindowDays, s.LowCoverDays, s.SeasoningDays, s.TargetKgPerUnit, s.WetAlertMoisturePct);

    private static WoodSupplierDto ToDto(FuelSupplier s, bool canSeePayment) => new(s.Id, s.Name, s.Type, s.Phone, canSeePayment ? s.PaymentDetails : null, canSeePayment ? s.KraPin : null,
        s.IdLast4, s.SourceArea, s.PermitNumber, s.PermitExpiresOn, s.PermitExpiresOn is { } due && due < DateTime.UtcNow.Date, s.Notes, s.IsActive);

    private static WoodPriceDto ToDto(FuelPriceAgreement p) => new(p.Id, p.SupplierId, p.Supplier?.Name, p.SpeciesId, p.Species?.Name, p.FuelForm, p.PricePerTonne, p.ValidFrom, p.ValidTo, p.Notes, p.IsActive);

    private static WoodCountDto ToDto(WoodStockCount c) => new(c.Id, c.StackId, c.Stack?.Code ?? string.Empty, c.CountedAt, c.LedgerKg, c.CountedKg, c.VarianceKg, c.Method, c.Reason, c.Notes, c.CountedByName);

    private WoodDeliveryDto ToDto(WoodDelivery d) => new(d.Id, d.DeliveryNumber, d.FactoryId, d.Factory?.Name ?? string.Empty, d.ReceivedAt, d.SupplierId, d.Supplier?.Name ?? string.Empty,
        CanManage ? d.Supplier?.Phone ?? string.Empty : string.Empty, CanManage ? d.Supplier?.PaymentDetails ?? string.Empty : string.Empty,
        d.SpeciesId, d.Species?.Name ?? string.Empty, d.FuelForm, d.SourceArea, d.TransportPermit, d.SupplierNoteNumber, d.VehicleRegistration ?? string.Empty, d.DriverName,
        d.MeasureBasis, d.GrossKg, d.TareKg, d.StackedM3, d.Loads, d.KgPerLoad, d.NetKg, d.AverageMoisturePct, d.MoistureSamples.OrderBy(x => x.MeasuredAt).Select(x => x.ValuePct).ToList(),
        d.Decision, d.DecisionReason, d.RejectedKg, d.DeductedKg, d.PayableKg, d.PricePerTonne, d.PricePercent, d.AmountPayable, d.StackId, d.Stack?.Code ?? string.Empty, d.ReceivedByName, d.Notes);
}

public sealed record WoodNamed(Guid Id, string Name);
public sealed record WoodSpeciesRow(Guid Id, string Name, decimal KgPerStackedM3, decimal? EnergyMjPerKg, bool IsActive);
public sealed record SaveSpeciesRequest([StringLength(100)] string Name, decimal KgPerStackedM3, decimal? EnergyMjPerKg = null, bool? IsActive = null);

public sealed record WoodSettingsDto(Guid FactoryId, string DefaultMeasureBasis, string MoistureRule, decimal BaseMoisturePct, decimal RejectMoisturePct, decimal DeductionPctPerPoint,
    IReadOnlyList<WoodPriceBand> PriceBands, decimal? DefaultKgPerLoad, int BurnWindowDays, int LowCoverDays, int SeasoningDays, decimal? TargetKgPerUnit, decimal WetAlertMoisturePct);
public sealed record SaveWoodSettingsRequest(Guid FactoryId, string DefaultMeasureBasis, string MoistureRule, decimal BaseMoisturePct, decimal RejectMoisturePct, decimal DeductionPctPerPoint,
    IReadOnlyList<WoodPriceBand>? PriceBands, decimal? DefaultKgPerLoad, int BurnWindowDays, int LowCoverDays, int SeasoningDays, decimal? TargetKgPerUnit, decimal? WetAlertMoisturePct = null);

public sealed record WoodLookups(
    IReadOnlyList<WoodNamed> Factories, Guid? FactoryId, IReadOnlyList<WoodSpeciesRow> Species, IReadOnlyList<WoodNamed> Suppliers, IReadOnlyList<WoodNamed> Stacks,
    IReadOnlyList<WoodNamed> Assets, WoodSettingsDto? Settings, IReadOnlyList<string> MeasureBases, IReadOnlyList<string> MoistureRules, IReadOnlyList<string> FuelForms,
    IReadOnlyList<string> SupplierTypes, IReadOnlyList<string> StackStatuses, IReadOnlyList<string> AdjustmentReasons, bool CanManage, bool CanReceive, bool CanIssue);

public sealed record WoodSupplierDto(Guid Id, string Name, string Type, string? Phone, string? PaymentDetails, string? KraPin, string? IdLast4, string? SourceArea,
    string? PermitNumber, DateTime? PermitExpiresOn, bool PermitExpired, string? Notes, bool IsActive);
public sealed record SaveSupplierRequest([StringLength(200)] string Name, string Type, [StringLength(30)] string? Phone = null, [StringLength(300)] string? PaymentDetails = null,
    [StringLength(20)] string? KraPin = null, [StringLength(40)] string? NationalId = null, [StringLength(200)] string? SourceArea = null, [StringLength(60)] string? PermitNumber = null,
    DateTime? PermitExpiresOn = null, [StringLength(1000)] string? Notes = null, bool? IsActive = null);

public sealed record WoodPriceDto(Guid Id, Guid? SupplierId, string? Supplier, Guid? SpeciesId, string? Species, string? FuelForm, decimal PricePerTonne, DateTime ValidFrom, DateTime? ValidTo, string? Notes, bool IsActive);
public sealed record SavePriceRequest(Guid? SupplierId, Guid? SpeciesId, string? FuelForm, decimal PricePerTonne, DateTime ValidFrom, DateTime? ValidTo = null, [StringLength(500)] string? Notes = null, bool? IsActive = null);

public sealed record SaveStackRequest(Guid FactoryId, [StringLength(30)] string Code, Guid? SpeciesId = null, string? Status = null, [StringLength(500)] string? Notes = null, bool? IsActive = null);

public sealed record ReceiveDeliveryRequest(
    Guid FactoryId, Guid? SupplierId, Guid? SpeciesId, string? FuelForm, [StringLength(200)] string? SourceArea, [StringLength(60)] string? TransportPermit,
    [StringLength(60)] string? SupplierNoteNumber, [StringLength(30)] string? VehicleRegistration, [StringLength(200)] string? DriverName, Guid? VehicleMovementId,
    string? MeasureBasis, decimal? GrossKg, decimal? TareKg, decimal? StackedM3, decimal? Loads, decimal? KgPerLoad, IReadOnlyList<decimal>? MoistureSamples,
    bool Reject = false, [StringLength(300)] string? RejectReason = null, bool AcceptAboveMoistureLimit = false, decimal? PricePerTonne = null, Guid? StackId = null,
    [StringLength(1000)] string? Notes = null);

public sealed record WoodDeliveryDto(
    Guid Id, string DeliveryNumber, Guid FactoryId, string Factory, DateTime ReceivedAt, Guid? SupplierId, string Supplier, string SupplierPhone, string SupplierPaymentDetails,
    Guid? SpeciesId, string Species, string FuelForm, string? SourceArea, string? TransportPermit, string? SupplierNoteNumber, string VehicleRegistration, string? DriverName,
    string MeasureBasis, decimal? GrossKg, decimal? TareKg, decimal? StackedM3, decimal? Loads, decimal? KgPerLoad, decimal NetKg, decimal? AverageMoisturePct, IReadOnlyList<decimal> MoistureSamples,
    string Decision, string? DecisionReason, decimal RejectedKg, decimal DeductedKg, decimal PayableKg, decimal PricePerTonne, decimal PricePercent, decimal AmountPayable,
    Guid? StackId, string Stack, string ReceivedBy, string? Notes);

public sealed record IssueWoodRequest(Guid FactoryId, Guid AssetId, Guid? StackId, decimal? Kg, decimal? Loads, decimal? KgPerLoad, [StringLength(30)] string? Shift = null, [StringLength(500)] string? Notes = null);
public sealed record WoodIssueDto(Guid Id, Guid FactoryId, Guid AssetId, string Asset, Guid? StackId, string? Stack, DateTime IssuedAt, string? Shift, decimal Kg, string? Notes, string IssuedBy, bool OnMeter);

public sealed record CountStackRequest(Guid StackId, decimal? CountedKg, decimal? StackedM3, string? Reason = null, [StringLength(500)] string? Notes = null);
public sealed record WoodCountDto(Guid Id, Guid StackId, string Stack, DateTime CountedAt, decimal LedgerKg, decimal CountedKg, decimal VarianceKg, string Method, string Reason, string? Notes, string CountedBy);

public sealed record SaveProductionRequest(Guid FactoryId, DateTime Date, decimal MadeTeaKg, decimal? GreenLeafKg = null, [StringLength(500)] string? Notes = null);
public sealed record ProductionDayDto(Guid Id, Guid FactoryId, DateTime Date, decimal MadeTeaKg, decimal? GreenLeafKg, string? Notes, string EnteredBy);
