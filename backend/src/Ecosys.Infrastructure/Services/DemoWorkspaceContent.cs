using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

/// <summary>
/// The demo workspace's content: Kapkoros Tea Factory, a fictional CTC factory in Kericho with about four
/// months of maintenance history. Everything is created through the same services the screens use (the tea
/// template, the asset import, PM plans, jobs, compliance, stores and fleet), so the demo behaves exactly like a
/// customer's workspace; only the history is back-dated afterwards. Every date is relative to the moment of the
/// reset, so the demo always has work due today, this week and overdue.
/// </summary>
internal sealed partial class DemoWorkspaceService
{
    // Nested types cannot reach the primary-constructor parameters, so the builder goes through these.
    private Data.AppDbContext DbContextRef => dbContext;
    private Microsoft.AspNetCore.Identity.IPasswordHasher<User> PasswordHasherRef => passwordHasher;
    private IDocumentNumberingService NumberingRef => numbering;
    private IFactorySettingsService FactorySettingsServiceRef => factorySettingsService;
    private IAssetClassService AssetClassServiceRef => assetClassService;
    private IFactoryAssetImportService AssetImportServiceRef => assetImportService;
    private IFailureCodeService FailureCodeServiceRef => failureCodeService;
    private IMeterReadingService MeterReadingServiceRef => meterReadingService;
    private IConditionMonitor ConditionMonitorRef => conditionMonitor;
    private IFactoryPmService PmServiceRef => pmService;
    private IFactoryWorkOrderService WorkOrderServiceRef => workOrderService;
    private IComplianceService ComplianceServiceRef => complianceService;
    private IRunningCheckService RunningCheckServiceRef => runningCheckService;
    private ISparesService SparesServiceRef => sparesService;
    private IFleetService FleetServiceRef => fleetService;

    private const string FactoryCode = "KPK";
    private const int HistoryDays = 120;

    private sealed class DemoFactory(DemoWorkspaceService service, string password)
    {
        private readonly DateTime _now = DateTime.UtcNow;
        private readonly Guid _tenantId = DemoWorkspace.TenantId;
        // A fixed seed: the demo looks the same after every reset, apart from the dates.
        private readonly Random _random = new(20261108);
        private readonly Dictionary<string, User> _users = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Technician> _technicians = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Asset> _assets = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Guid> _failureCodes = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, MaterialItem> _parts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<Guid, Guid> _planByAsset = [];
        private int _preventiveCount;
        // History rows get their real creation date after the first save stamps "now" on them.
        private readonly List<(Ecosys.Domain.Common.AuditableEntity Entity, DateTime CreatedAt)> _createdDates = [];
        private OrgNode _factory = null!;
        private Store _store = null!;

        private Data.AppDbContext Db => service.DbContextRef;
        private FactoryActor Admin => new(_users["admin"].Id, _users["admin"].FullName);

        public async Task BuildAsync(CancellationToken ct)
        {
            AddWorkspaceSettings();
            await AddOrganisationAsync(ct);
            AddPeople();
            await Db.SaveChangesAsync(ct);

            await service.AssetClassServiceRef.InstallAsync(_tenantId, IndustryTemplates.TeaKey, ct);
            await service.FailureCodeServiceRef.EnsureSeededAsync(_tenantId, ct);
            await ImportAssetsAsync(ct);
            await AddMeterHistoryAsync(ct);
            await AddConditionMonitoringAsync(ct);

            await AddStoresAsync(ct);
            AddContractors();
            await Db.SaveChangesAsync(ct);

            await AddPlannedMaintenanceAsync(ct);
            await AddRunningChecksAsync(ct);
            await AddComplianceAsync(ct);
            await AddFleetAsync(ct);

            await AddJobHistoryAsync(ct);
            await AddOpenJobsAsync(ct);

            foreach (var (entity, createdAt) in _createdDates)
            {
                entity.CreatedAt = createdAt;
            }

            await Db.SaveChangesAsync(ct);
        }

        // ── Workspace, organisation and people ───────────────────────────────

        private void AddWorkspaceSettings()
        {
            Db.EmailSettings.Add(new EmailSetting
            {
                TenantId = _tenantId,
                // Off on purpose: the demo's users have no real mailboxes. The outbox marks anything queued for this
                // tenant as Disabled instead of sending it.
                IsEnabled = false,
                InboundEnabled = false,
                SenderName = DemoWorkspace.CompanyName,
                SenderAddress = $"noreply@{DemoWorkspace.Slug}.{DemoWorkspace.NoEmailDomain}",
            });
            Db.NotificationSettings.Add(new NotificationSetting { TenantId = _tenantId });
            Db.MonitoringSettings.Add(new MonitoringSetting { TenantId = _tenantId });
            Db.TenantSecurityPolicies.Add(new TenantSecurityPolicy { TenantId = _tenantId });
        }

        private async Task AddOrganisationAsync(CancellationToken ct)
        {
            var settings = await service.FactorySettingsServiceRef.GetOrCreateAsync(_tenantId, ct);
            settings.IndustryTemplate = IndustryTemplates.TeaKey;
            settings.ProductionUnit = IndustryTemplates.Tea.ProductionUnit;
            settings.ProductionUnitShort = IndustryTemplates.Tea.ProductionUnitShort;
            settings.WeeklyWindowEnabled = true;
            settings.WeeklyWindowDay = DayOfWeek.Monday;
            // The dry season is the low-crop overhaul period in Kericho.
            settings.LowCropStartMonth = 1;
            settings.LowCropEndMonth = 3;
            settings.TagPrefix = FactoryCode;

            var group = new OrgNode { TenantId = _tenantId, Level = OrgNodeLevels.Group, Name = "Kericho Highlands Tea", Code = "KHT", DisplayOrder = 1 };
            _factory = new OrgNode
            {
                TenantId = _tenantId, Parent = group, Level = OrgNodeLevels.Factory, Name = "Kapkoros Tea Factory", Code = FactoryCode,
                Location = "Kapkoros, Kericho County", DisplayOrder = 1, TemplateKey = IndustryTemplates.TeaKey,
            };
            Db.OrgNodes.AddRange(group, _factory);
            var order = 0;
            foreach (var section in IndustryTemplates.Tea.Sections)
            {
                Db.OrgNodes.Add(new OrgNode
                {
                    TenantId = _tenantId, Parent = _factory, Level = OrgNodeLevels.Section, Name = section.Name, Code = $"{FactoryCode}-{section.Code}",
                    Description = section.Description, DisplayOrder = ++order, TemplateKey = IndustryTemplates.TeaKey,
                });
            }
        }

        private void AddPeople()
        {
            var branch = new Branch { TenantId = _tenantId, Name = "Kapkoros Factory", Code = FactoryCode, Location = "Kericho", IsActive = true };
            Db.Branches.Add(branch);

            foreach (var login in Logins)
            {
                var user = new User
                {
                    TenantId = _tenantId,
                    DefaultBranchId = branch.Id,
                    FullName = login.FullName,
                    Username = login.Username,
                    Email = $"{login.Username}.{DemoWorkspace.Slug}@{DemoWorkspace.NoEmailDomain}",
                    HasEmail = false,
                    Role = login.Role,
                    JobTitle = login.JobTitle,
                    Department = "Maintenance",
                    IsActive = true,
                    HasAllBranchAccess = true,
                    MustChangePassword = false,
                    Permission = new UserPermission(),
                };
                user.PasswordHash = service.PasswordHasherRef.HashPassword(user, password);
                Db.Users.Add(user);
                _users[login.Username] = user;
            }

            AddTechnician("kevin", _users["technician"].Id, "Kevin Langat", "Electrical", "+254 711 000 101");
            AddTechnician("peter", null, "Peter Rotich", "Mechanical", "+254 711 000 102");
            AddTechnician("faith", null, "Faith Cherono", "Instrumentation", "+254 711 000 103");
            AddTechnician("samuel", null, "Samuel Koech", "Boiler", "+254 711 000 104");

            void AddTechnician(string key, Guid? userId, string name, string trade, string phone)
            {
                var technician = new Technician
                {
                    TenantId = _tenantId, UserId = userId, BranchId = branch.Id, FullName = name, Phone = phone,
                    Email = $"{key}.{DemoWorkspace.Slug}@{DemoWorkspace.NoEmailDomain}", SkillCategory = trade, Trade = trade, IsActive = true,
                };
                Db.Technicians.Add(technician);
                _technicians[key] = technician;
            }
        }

        // ── Assets and meters ────────────────────────────────────────────────

        private async Task ImportAssetsAsync(CancellationToken ct)
        {
            // Code, name, section, class, criticality, manufacturer, model, year, lost kg MT per hour when down.
            (string Code, string Name, string Section, string Class, string? Crit, string Make, string Model, int Year, decimal? Lost)[] register =
            [
                ("KPK-WB-01", "Weighbridge 60 t", "LEAF", "Weighbridge", null, "Avery Scales EA", "WB-60", 2016, null),
                ("KPK-CNV-01", "Green leaf elevator", "LEAF", "Conveyor", null, "Rift Valley Engineering", "LE-12", 2014, null),
                ("KPK-WT-01", "Withering trough 1", "WITH", "Withering trough", null, "Rift Valley Engineering", "WT-30", 2012, null),
                ("KPK-WT-02", "Withering trough 2", "WITH", "Withering trough", null, "Rift Valley Engineering", "WT-30", 2012, null),
                ("KPK-WT-03", "Withering trough 3", "WITH", "Withering trough", null, "Rift Valley Engineering", "WT-30", 2015, null),
                ("KPK-WT-04", "Withering trough 4", "WITH", "Withering trough", null, "Rift Valley Engineering", "WT-30", 2015, null),
                ("KPK-WF-01", "Withering fan 1", "WITH", "Withering fan", null, "Highland Air Systems", "AXF-1250", 2018, 120m),
                ("KPK-WF-02", "Withering fan 2", "WITH", "Withering fan", null, "Highland Air Systems", "AXF-1250", 2018, 120m),
                ("KPK-WF-03", "Withering fan 3", "WITH", "Withering fan", null, "Highland Air Systems", "AXF-1250", 2015, 120m),
                ("KPK-WF-04", "Withering fan 4", "WITH", "Withering fan", null, "Highland Air Systems", "AXF-1250", 2015, 120m),
                ("KPK-RV-01", "Rotorvane 15\"", "ROLL", "Rotorvane", null, "Kericho Process Equipment", "RV-15", 2017, 650m),
                ("KPK-CTC-01", "CTC triplex line 1", "ROLL", "CTC machine", null, "Kericho Process Equipment", "CTC-3X36", 2016, 650m),
                ("KPK-CTC-02", "CTC triplex line 2", "ROLL", "CTC machine", null, "Kericho Process Equipment", "CTC-3X36", 2019, 650m),
                ("KPK-CFU-01", "Continuous fermenting unit 1", "FERM", "Continuous fermenting unit", null, "Kericho Process Equipment", "CFM-8", 2016, 300m),
                ("KPK-CFU-02", "Continuous fermenting unit 2", "FERM", "Continuous fermenting unit", null, "Kericho Process Equipment", "CFM-8", 2019, 300m),
                ("KPK-FBD-01", "Fluid bed dryer 1", "DRY", "Fluid bed dryer", null, "Mombasa Thermal Works", "FBD-1000", 2015, 650m),
                ("KPK-FBD-02", "Fluid bed dryer 2", "DRY", "Fluid bed dryer", null, "Mombasa Thermal Works", "FBD-1000", 2020, 650m),
                ("KPK-FX-01", "Fibre extractor", "SORT", "Sorting and grading machine", null, "Kericho Process Equipment", "FX-4", 2017, null),
                ("KPK-VS-01", "Vibro sorter", "SORT", "Sorting and grading machine", null, "Kericho Process Equipment", "VS-6", 2017, null),
                ("KPK-CS-01", "Colour sorter", "SORT", "Sorting and grading machine", "A", "Optisort", "CS-128", 2021, null),
                ("KPK-PK-01", "Bulk packer", "PACK", "Packing machine", null, "PackRite Africa", "BP-50", 2018, null),
                ("KPK-BLR-01", "Boiler 1 (wood-fired, 10 t/h)", "BOIL", "Boiler", null, "Mombasa Boilerworks", "WTB-10", 2011, 650m),
                ("KPK-BLR-02", "Boiler 2 (wood-fired, 6 t/h)", "BOIL", "Boiler", null, "Mombasa Boilerworks", "WTB-6", 2018, 400m),
                ("KPK-BFP-01", "Boiler feed pump 1", "BOIL", "Pump", "A", "Grundfos", "CR 15-10", 2019, null),
                ("KPK-GEN-01", "Standby generator 500 kVA", "UTIL", "Generator", null, "Perkins", "2806A-E18", 2014, null),
                ("KPK-TX-01", "Transformer 1000 kVA", "UTIL", "Transformer", null, "Kenya Transformers", "TX-1000", 2010, null),
                ("KPK-CMP-01", "Air compressor", "UTIL", "Compressor", null, "Atlas Copco", "GA 22", 2017, null),
            ];

            var rows = register.Select((x, index) => (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>
            {
                [FactoryAssetImportColumns.AssetCode] = x.Code,
                [FactoryAssetImportColumns.AssetName] = x.Name,
                [FactoryAssetImportColumns.Location] = $"{FactoryCode}-{x.Section}",
                [FactoryAssetImportColumns.AssetClass] = x.Class,
                [FactoryAssetImportColumns.Criticality] = x.Crit,
                [FactoryAssetImportColumns.Manufacturer] = x.Make,
                [FactoryAssetImportColumns.Model] = x.Model,
                [FactoryAssetImportColumns.SerialNumber] = $"{x.Model}-{x.Year}-{100 + index:000}",
                [FactoryAssetImportColumns.YearOfManufacture] = x.Year.ToString(),
                [FactoryAssetImportColumns.InstallationDate] = new DateTime(x.Year, 3, 1).ToString("yyyy-MM-dd"),
                [FactoryAssetImportColumns.CommissioningDate] = new DateTime(x.Year, 4, 1).ToString("yyyy-MM-dd"),
                [FactoryAssetImportColumns.LostProductionPerHour] = x.Lost?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                [FactoryAssetImportColumns.TagNumber] = $"{FactoryCode}-{index + 1:000000}",
            }).ToList();

            var admin = _users["admin"];
            await service.AssetImportServiceRef.CommitAsync(_tenantId, admin.Id, admin.FullName, rows, ct);

            var settings = await service.FactorySettingsServiceRef.GetOrCreateAsync(_tenantId, ct);
            settings.NextTagNumber = rows.Count + 1;
            foreach (var asset in await Db.Assets.Where(x => x.TenantId == _tenantId).ToListAsync(ct))
            {
                _assets[asset.AssetCode] = asset;
            }

            await Db.SaveChangesAsync(ct);
        }

        /// <summary>Weekly run-hours and production readings over the history, so PM by hours, KPIs and trends have data.</summary>
        private async Task AddMeterHistoryAsync(CancellationToken ct)
        {
            var meters = await Db.Meters.Where(x => x.TenantId == _tenantId && x.IsActive && (x.MeterType == MeterTypes.RunHours || x.MeterType == MeterTypes.Production || x.MeterType == MeterTypes.Energy)).ToListAsync(ct);
            foreach (var meter in meters)
            {
                var asset = _assets.Values.First(x => x.Id == meter.AssetId);
                // Machines run about 20 hours a day in peak crop; standby plant far less.
                var perWeek = meter.MeterType switch
                {
                    MeterTypes.RunHours => asset.AssetCode.StartsWith("KPK-GEN", StringComparison.Ordinal) ? 6m : 135m,
                    MeterTypes.Production => 26_000m,
                    _ => 42_000m,
                };
                var value = meter.MeterType == MeterTypes.RunHours ? 18_000m + _random.Next(0, 9_000) : 1_500_000m + _random.Next(0, 400_000);
                for (var week = HistoryDays / 7; week >= 0; week--)
                {
                    value += Math.Round(perWeek * (0.85m + (decimal)_random.NextDouble() * 0.3m));
                    await service.MeterReadingServiceRef.RecordAsync(meter, new NewMeterReading(value, _now.AddDays(-7 * week).AddHours(-2), MeterReadingSources.Manual, UserId: _users["technician"].Id, UserName: _users["technician"].FullName), ct);
                }
            }

            await Db.SaveChangesAsync(ct);
        }

        /// <summary>
        /// Two condition meters fed by a sensor gateway: CTC line 1's drive-end bearing vibration drifting up into
        /// its warning band (the reliability story), and Boiler 1's steam pressure, steady and normal.
        /// </summary>
        private async Task AddConditionMonitoringAsync(CancellationToken ct)
        {
            Db.SensorGateways.Add(new SensorGateway
            {
                TenantId = _tenantId, Name = "Rolling room PLC", Description = "Siemens S7 PLC in the rolling room, publishing every 5 minutes.",
                OrgNodeId = _assets["KPK-CTC-01"].OrgNodeId, KeyPrefix = "demo0000", KeyHash = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
                SampleSeconds = 300, LastSeenAt = _now.AddMinutes(-4), AcceptedCount = 34_512,
            });

            var vibration = new Meter
            {
                TenantId = _tenantId, AssetId = _assets["KPK-CTC-01"].Id, Name = "Drive-end bearing vibration", MeterType = MeterTypes.Vibration, Unit = "mm/s",
                IsCumulative = false, WarningMax = 4.5m, MaxThreshold = 7.1m, ConditionAction = ConditionActions.WorkOrder, TriggerAfterReadings = 2, AlarmPriority = "High",
                ExternalId = "CTC1.DE_VIB", ExternalSystem = "Sensor",
            };
            var pressure = new Meter
            {
                TenantId = _tenantId, AssetId = _assets["KPK-BLR-01"].Id, Name = "Steam pressure", MeterType = MeterTypes.Pressure, Unit = "bar",
                IsCumulative = false, WarningMin = 7m, WarningMax = 10.5m, MinThreshold = 6m, MaxThreshold = 11m, ConditionAction = ConditionActions.Alert,
                ExternalId = "BLR1.STEAM_P", ExternalSystem = "Sensor",
            };
            Db.Meters.AddRange(vibration, pressure);
            await Db.SaveChangesAsync(ct);

            for (var day = 30; day >= 0; day--)
            {
                // A slow rise from about 2.4 to 5.2 mm/s over the month: worn bearing, still running. Each reading is
                // evaluated as it arrives, as the sensor feed does, so the warning starts on the day it was crossed.
                var value = Math.Round(2.4m + (30 - day) * 0.095m + (decimal)(_random.NextDouble() * 0.25), 2);
                var at = _now.AddDays(-day).AddHours(-1);
                var reading = await service.MeterReadingServiceRef.RecordAsync(vibration, new NewMeterReading(value, at, MeterReadingSources.Sensor), ct);
                await Db.SaveChangesAsync(ct);
                await service.ConditionMonitorRef.EvaluateAsync(vibration, reading, Admin, ct);
                var steam = await service.MeterReadingServiceRef.RecordAsync(pressure, new NewMeterReading(Math.Round(8.6m + (decimal)(_random.NextDouble() * 0.8), 1), at, MeterReadingSources.Sensor), ct);
                await Db.SaveChangesAsync(ct);
                await service.ConditionMonitorRef.EvaluateAsync(pressure, steam, Admin, ct);
                await Db.SaveChangesAsync(ct);
            }
        }

        // ── Stores and contractors ───────────────────────────────────────────

        private async Task AddStoresAsync(CancellationToken ct)
        {
            _store = new Store { TenantId = _tenantId, FactoryId = _factory.Id, Code = $"{FactoryCode}-MAIN", Name = "Main engineering store", Location = "Workshop block", IsMain = true };
            Db.Stores.Add(_store);
            await Db.SaveChangesAsync(ct);

            // Code, name, category, unit, unit cost (KES), reorder level, quantity in stock, critical spare.
            (string Code, string Name, string Category, string Unit, decimal Cost, decimal Reorder, decimal Stock, bool Critical)[] parts =
            [
                ("BRG-22316", "Spherical roller bearing 22316", "Bearings", "pcs", 18_500m, 4, 6, true),
                ("BRG-6310", "Deep groove ball bearing 6310-2RS", "Bearings", "pcs", 3_200m, 6, 14, false),
                ("BLT-SPB2000", "V-belt SPB 2000", "Belts", "pcs", 1_450m, 12, 9, false),
                ("CTC-RLR", "CTC roller (re-cut exchange)", "CTC parts", "pcs", 145_000m, 2, 1, true),
                ("CTC-SEG", "CTC roller segment set", "CTC parts", "set", 62_000m, 1, 2, true),
                ("FBD-MESH", "Dryer bed perforated plate", "Dryer parts", "pcs", 38_000m, 1, 2, false),
                ("WTR-MESH", "Withering trough mesh 2 m x 30 m", "Withering", "roll", 24_000m, 2, 3, false),
                ("GRS-EP2", "Grease EP2", "Lubricants", "kg", 650m, 40, 120, false),
                ("OIL-68", "Gear oil ISO VG 68", "Lubricants", "L", 520m, 60, 210, false),
                ("BLR-GG", "Boiler gauge glass", "Boiler parts", "pcs", 2_800m, 4, 3, true),
                ("BLR-SV", "Boiler safety valve 2\"", "Boiler parts", "pcs", 54_000m, 1, 1, true),
                ("FLT-AIR", "Generator air filter", "Filters", "pcs", 4_100m, 2, 4, false),
                ("FLT-OIL", "Generator oil filter", "Filters", "pcs", 1_900m, 4, 6, false),
                ("CNT-32A", "Contactor 32 A, 230 V coil", "Electrical", "pcs", 6_800m, 3, 5, false),
            ];

            foreach (var part in parts)
            {
                var item = new MaterialItem
                {
                    TenantId = _tenantId, ItemCode = part.Code, ItemName = part.Name, Category = part.Category, UnitOfMeasure = part.Unit,
                    UnitCost = part.Cost, ReorderLevel = part.Reorder, ReorderQuantity = part.Reorder * 2, IsCriticalSpare = part.Critical, IsActive = true,
                };
                Db.MaterialItems.Add(item);
                _parts[part.Code] = item;
            }

            await Db.SaveChangesAsync(ct);
            foreach (var part in parts)
            {
                await service.SparesServiceRef.ReceiveAsync(_store, _parts[part.Code], part.Stock, part.Cost, "KES", "Opening stock", null, new FactoryActor(_users["stores"].Id, _users["stores"].FullName), ct);
            }

            await Db.SaveChangesAsync(ct);
        }

        private void AddContractors()
        {
            Db.Contractors.AddRange(
                new Contractor { TenantId = _tenantId, Name = "Rift Valley Boiler Services", Services = "Boiler inspection, refractory, safety valve testing", ContactPerson = "Daniel Kimutai", Phone = "+254 722 300 401", RegistrationNumber = "PVT-2016-0411", Status = ContractorStatuses.Approved },
                new Contractor { TenantId = _tenantId, Name = "Kericho Electrical Contractors", Services = "HV switchgear, transformer servicing, electrical installations", ContactPerson = "Mercy Chepkemoi", Phone = "+254 722 300 402", RegistrationNumber = "PVT-2012-0918", Status = ContractorStatuses.Approved },
                new Contractor { TenantId = _tenantId, Name = "Precision Roller Works", Services = "CTC roller re-cutting and balancing", ContactPerson = "Hassan Omondi", Phone = "+254 722 300 403", RegistrationNumber = "PVT-2018-1203", Status = ContractorStatuses.Approved });
        }

        // ── Planned maintenance, running checks, compliance ──────────────────

        private async Task AddPlannedMaintenanceAsync(CancellationToken ct)
        {
            // The weekly window routines (belts, cleaning, greasing) for every matching machine, starting next Monday.
            var daysToMonday = ((int)DayOfWeek.Monday - (int)_now.DayOfWeek + 7) % 7;
            var nextMonday = _now.Date.AddDays(daysToMonday == 0 ? 7 : daysToMonday).AddHours(6);
            await WindowRoutines.InstallAsync(Db, service.PmServiceRef, _tenantId, _factory.Id, nextMonday, ct);

            // By running hours: CTC lines and dryers.
            foreach (var code in new[] { "KPK-CTC-01", "KPK-CTC-02", "KPK-FBD-01", "KPK-FBD-02" })
            {
                var meter = await Db.Meters.FirstAsync(x => x.AssetId == _assets[code].Id && x.MeterType == MeterTypes.RunHours, ct);
                var interval = code.StartsWith("KPK-CTC", StringComparison.Ordinal) ? 500m : 1_000m;
                await service.PmServiceRef.SaveAsync(_tenantId, null, new SaveFactoryPmPlan(
                    $"{(interval == 500m ? "Roller and gearbox service" : "Dryer service")} every {interval:0} h – {code}", _assets[code].Id, null, PmTriggerTypes.Meter, PmFrequencyUnits.Months, 1,
                    null, meter.Id, interval, (meter.CurrentValue ?? 0) + interval * 0.15m, PmScheduleModes.Floating, 10, 0, "High", 6m, true, "Manufacturer's interval", null), ct);
            }

            // By calendar: the boilers, generator, transformer and compressor.
            (string Code, string Name, string Unit, int Every, int DueInDays, decimal Hours)[] calendar =
            [
                ("KPK-BLR-01", "Boiler 1 monthly service", PmFrequencyUnits.Months, 1, 3, 8m),
                ("KPK-BLR-02", "Boiler 2 monthly service", PmFrequencyUnits.Months, 1, 12, 6m),
                ("KPK-GEN-01", "Generator 250 h / quarterly service", PmFrequencyUnits.Months, 3, -2, 4m),
                ("KPK-TX-01", "Transformer oil test and inspection", PmFrequencyUnits.Years, 1, 40, 3m),
                ("KPK-CMP-01", "Compressor service", PmFrequencyUnits.Months, 3, 9, 3m),
                ("KPK-WB-01", "Weighbridge cleaning and load-cell check", PmFrequencyUnits.Months, 1, 5, 2m),
            ];
            foreach (var plan in calendar)
            {
                await service.PmServiceRef.SaveAsync(_tenantId, null, new SaveFactoryPmPlan(
                    plan.Name, _assets[plan.Code].Id, null, PmTriggerTypes.Calendar, plan.Unit, plan.Every, _now.Date.AddDays(plan.DueInDays).AddHours(7),
                    null, null, null, PmScheduleModes.Fixed, 0, 7, "Medium", plan.Hours, plan.Code.StartsWith("KPK-BLR", StringComparison.Ordinal), "Manufacturer's schedule", null), ct);
            }

            await Db.SaveChangesAsync(ct);
        }

        private async Task AddRunningChecksAsync(CancellationToken ct)
        {
            await RunningCheckTemplates.InstallAsync(Db, _tenantId, ct);
            await Db.SaveChangesAsync(ct);

            var templates = await Db.PmTemplates.Where(x => x.TenantId == _tenantId && x.WorkType == RunningCheckTemplates.WorkType && x.AssetCategoryId != null).ToListAsync(ct);
            foreach (var template in templates)
            {
                var assets = _assets.Values.Where(x => x.AssetCategoryId == template.AssetCategoryId).OrderBy(x => x.AssetCode).ToList();
                if (assets.Count == 0)
                {
                    continue;
                }

                var schedule = new CheckSchedule
                {
                    TenantId = _tenantId, Name = template.Name, PmTemplateId = template.Id,
                    Frequency = RunningCheckTemplates.SuggestedFrequency(template.Name) ?? CheckFrequencies.PerShift,
                    OrgNodeId = assets[0].OrgNodeId ?? _factory.Id, IsRoute = assets.Count > 2,
                };
                foreach (var (asset, index) in assets.Select((a, i) => (a, i)))
                {
                    schedule.Assets.Add(new CheckScheduleAsset { AssetId = asset.Id, Sequence = index + 1 });
                }

                Db.CheckSchedules.Add(schedule);
            }

            await Db.SaveChangesAsync(ct);
            // This shift's checks, so the technician's list is not empty.
            await service.RunningCheckServiceRef.EnsureCurrentRunsAsync(_tenantId, _now, ct);
            await Db.SaveChangesAsync(ct);
        }

        private async Task AddComplianceAsync(CancellationToken ct)
        {
            await service.ComplianceServiceRef.InstallAsync(_tenantId, _factory.Id, ct);
            await Db.SaveChangesAsync(ct);

            var items = await Db.ComplianceItems.Where(x => x.TenantId == _tenantId).OrderBy(x => x.Name).ToListAsync(ct);
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                // Most were done a few months ago; one boiler inspection is overdue and a calibration is due soon,
                // so the statutory screen has something in every state.
                var lastDone = item.Name.Contains("Boiler", StringComparison.OrdinalIgnoreCase) && item.Name.Contains("BLR-01", StringComparison.Ordinal)
                    ? _now.Date.AddMonths(-item.IntervalMonths).AddDays(-9)
                    : item.Kind == ComplianceKinds.Calibration
                        ? _now.Date.AddMonths(-item.IntervalMonths).AddDays(18)
                        : _now.Date.AddDays(-30 - (i * 23 % 200));
                item.LastDoneAt = lastDone;
                item.DueDate = lastDone.AddMonths(item.IntervalMonths);
                item.OwnerUserId = _users["admin"].Id;
            }

            await Db.SaveChangesAsync(ct);
        }

        // ── Fleet ────────────────────────────────────────────────────────────

        private async Task AddFleetAsync(CancellationToken ct)
        {
            var leafSection = _assets["KPK-WB-01"].OrgNodeId;
            (string Reg, string Type, string Make, string Model, int Year, decimal Payload, string Route, string Driver, int InsuranceDays, int InspectionDays, decimal Odo)[] vehicles =
            [
                ("KDA 512X", VehicleTypes.Lorry, "Isuzu", "FSR", 2019, 7_000m, "Kapkoros – Chebown buying centres", "Wilson Kirui", 140, 60, 186_400m),
                ("KCY 207L", VehicleTypes.Lorry, "Isuzu", "FSR", 2016, 7_000m, "Kapkoros – Kipchimchim buying centres", "Patrick Ngeno", 9, 200, 312_900m),
                ("KDF 880Q", VehicleTypes.Lorry, "Mitsubishi", "Canter", 2022, 3_500m, "Kapkoros – Sosiot buying centres", "Ann Chepngetich", 250, -6, 64_200m),
                ("KCZ 341T", VehicleTypes.Tractor, "Massey Ferguson", "MF 375", 2015, 4_000m, "Estate collection (trailer)", "Joel Bett", 90, 120, 21_300m),
            ];

            foreach (var v in vehicles)
            {
                var profile = await service.FleetServiceRef.CreateAsync(_tenantId, new SaveVehicle(
                    v.Reg, v.Type, leafSection, v.Make, v.Model, v.Year, v.Payload, "Diesel", v.Route, v.Driver, "+254 733 000 000",
                    _now.Date.AddDays(v.InsuranceDays), _now.Date.AddDays(v.InspectionDays), v.Type == VehicleTypes.Lorry ? _now.Date.AddDays(v.InspectionDays + 30) : null,
                    null, null), Admin, ct);

                // Fortnightly full-tank fill-ups with the odometer, oldest first so the readings climb.
                var odometer = v.Odo - 9 * 1_150m;
                for (var fill = 8; fill >= 0; fill--)
                {
                    odometer += 1_050m + _random.Next(0, 220);
                    var litres = Math.Round(odometer / 1m % 1 + 210m + _random.Next(0, 40), 0);
                    await service.FleetServiceRef.AddFuelAsync(_tenantId, profile.AssetId, new NewFuelLog(
                        _now.Date.AddDays(-14 * fill).AddHours(7), litres, litres * 182m, odometer, true, "Kericho Rubis", null), Admin, ct);
                }
            }

            await Db.SaveChangesAsync(ct);
        }

        // ── Jobs ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Four months of finished work: repeat failures on CTC line 1 and withering fan 3 (so reliability has a
        /// story), occasional breakdowns elsewhere, and completed preventive jobs. Written directly as closed jobs
        /// with their real dates, labour and parts, which is what the reports read.
        /// </summary>
        private async Task AddJobHistoryAsync(CancellationToken ct)
        {
            var codes = await Db.FailureCodes.Where(x => x.TenantId == _tenantId).ToListAsync(ct);
            foreach (var code in codes)
            {
                _failureCodes[$"{code.Kind}:{code.Code}"] = code.Id;
            }

            // Past preventive jobs belong to the asset's weekly routine, so PM compliance has a history.
            foreach (var plan in await Db.PreventiveMaintenancePlans.Where(x => x.TenantId == _tenantId).OrderBy(x => x.Name).ToListAsync(ct))
            {
                _planByAsset.TryAdd(plan.AssetId, plan.Id);
            }

            // Asset, days ago, downtime hours, title, problem, cause, remedy, technician, part used.
            (string Asset, int DaysAgo, decimal Hours, string Title, string Problem, string Cause, string Remedy, string Tech, string? Part)[] breakdowns =
            [
                ("KPK-CTC-01", 112, 5.5m, "CTC line 1 stopped: roller teeth blunt", "CTC-TTH", "2.4", "REP", "peter", "CTC-SEG"),
                ("KPK-CTC-01", 86, 3.0m, "CTC line 1 knocking at drive end", "NOI", "1.5", "ADJ", "peter", null),
                ("KPK-CTC-01", 61, 7.0m, "CTC line 1 drive-end bearing seized", "OHE", "LUB", "REP", "peter", "BRG-22316"),
                ("KPK-CTC-01", 38, 4.5m, "CTC line 1 roller gap drifting", "CTC-GAP", "3.4", "ADJ", "peter", null),
                ("KPK-CTC-01", 17, 6.0m, "CTC line 1 drive-end bearing hot again", "OHE", "1.3", "REP", "peter", "BRG-22316"),
                ("KPK-WF-03", 104, 2.0m, "Withering fan 3 belts slipping", "SER", "2.4", "REP", "kevin", "BLT-SPB2000"),
                ("KPK-WF-03", 71, 2.5m, "Withering fan 3 tripping on overload", "AIR", "4.4", "RPR", "kevin", "CNT-32A"),
                ("KPK-WF-03", 44, 1.5m, "Withering fan 3 belts broken", "SER", "2.5", "REP", "kevin", "BLT-SPB2000"),
                ("KPK-WF-03", 12, 3.0m, "Withering fan 3 impeller vibrating", "WF-IMP", "1.2", "RPR", "kevin", null),
                ("KPK-FBD-01", 95, 8.0m, "Fire in dryer 1 chamber", "FBD-FIRE", "5.2", "CMB", "samuel", "FBD-MESH"),
                ("KPK-FBD-01", 29, 3.5m, "Dryer 1 bed choking", "FBD-BED", "5.1", "SVC", "samuel", null),
                ("KPK-BLR-01", 77, 4.0m, "Boiler 1 low water level trip", "BLR-WTR", "3.1", "RPR", "samuel", "BLR-GG"),
                ("KPK-BLR-01", 22, 2.0m, "Boiler 1 low steam pressure", "BLR-LOW", "5.1", "SVC", "samuel", null),
                ("KPK-RV-01", 55, 2.5m, "Rotorvane choking", "RV-CHK", "OPR", "SVC", "peter", null),
                ("KPK-WB-01", 66, 1.0m, "Weighbridge reading high", "WB-CAL", "3.4", "ADJ", "faith", null),
                ("KPK-GEN-01", 48, 1.5m, "Generator failed to start on mains failure", "SER", "4.3", "RPR", "kevin", "CNT-32A"),
                ("KPK-CMP-01", 33, 2.0m, "Compressor air leak", "ELU", "1.1", "RPR", "peter", null),
            ];

            foreach (var job in breakdowns)
            {
                var at = _now.Date.AddDays(-job.DaysAgo).AddHours(6 + _random.Next(0, 12));
                await AddClosedJobAsync(job.Asset, job.Title, at, job.Hours, isBreakdown: true, job.Tech,
                    _failureCodes.GetValueOrDefault($"{FailureCodeKinds.Problem}:{job.Problem}"),
                    _failureCodes.GetValueOrDefault($"{FailureCodeKinds.Cause}:{job.Cause}"),
                    _failureCodes.GetValueOrDefault($"{FailureCodeKinds.Remedy}:{job.Remedy}"),
                    job.Part, ct);
            }

            // Preventive work done in past weekly windows: one Monday a week, three or four jobs each.
            var routineAssets = new[] { "KPK-CTC-02", "KPK-FBD-02", "KPK-WF-01", "KPK-WF-02", "KPK-CFU-01", "KPK-CFU-02", "KPK-PK-01", "KPK-BLR-02", "KPK-FX-01", "KPK-VS-01" };
            for (var week = HistoryDays / 7; week >= 1; week--)
            {
                var monday = _now.Date.AddDays(-7 * week - ((int)_now.DayOfWeek + 6) % 7).AddHours(7);
                for (var j = 0; j < 3 + week % 2; j++)
                {
                    var code = routineAssets[(week * 3 + j) % routineAssets.Length];
                    await AddClosedJobAsync(code, $"Weekly window service – {_assets[code].AssetName}", monday.AddHours(j), 2m + j * 0.5m, isBreakdown: false,
                        j % 2 == 0 ? "peter" : "kevin", null, null, null, j == 0 ? "GRS-EP2" : null, ct);
                }
            }

            await Db.SaveChangesAsync(ct);
        }

        private async Task AddClosedJobAsync(string assetCode, string title, DateTime at, decimal hours, bool isBreakdown, string techKey,
            Guid? problem, Guid? cause, Guid? remedy, string? part, CancellationToken ct)
        {
            var asset = _assets[assetCode];
            var technician = _technicians[techKey];
            var started = at.AddMinutes(isBreakdown ? 25 : 0);
            var completed = started.AddHours((double)hours);
            var job = new WorkOrder
            {
                TenantId = _tenantId,
                WorkOrderNumber = await service.NumberingRef.GenerateAsync(_tenantId, null, DocumentTypes.WorkOrder, ct),
                Title = title,
                AssetId = asset.Id,
                OrgNodeId = asset.OrgNodeId,
                WorkType = isBreakdown ? FactoryWorkTypes.Corrective : FactoryWorkTypes.Preventive,
                IsPreventiveMaintenance = !isBreakdown,
                Priority = isBreakdown ? (asset.Criticality == "A" ? "Critical" : "High") : "Medium",
                Status = WorkOrderStatuses.Closed,
                RequestSource = isBreakdown ? FactoryRequestSources.Operator : FactoryRequestSources.Preventive,
                RequestedByName = isBreakdown ? "Shift supervisor" : null,
                IsBreakdown = isBreakdown,
                NeedsLineStop = !isBreakdown,
                DowntimeStartedAt = isBreakdown ? at : null,
                DowntimeEndedAt = isBreakdown ? completed : null,
                ApprovedAt = at.AddMinutes(5),
                ApprovedByUserId = _users["manager"].Id,
                PlannedAt = at.AddMinutes(10),
                ScheduledStartAt = started,
                WorkStartedAt = started,
                CompletedAt = completed,
                VerifiedAt = completed.AddHours(2),
                VerifiedByUserId = _users["manager"].Id,
                ClosedAt = completed.AddHours(20),
                ClosedByUserId = _users["manager"].Id,
                DueDate = at.Date.AddDays(1),
                EstimatedDuration = hours,
                ActualDuration = hours,
                AssignedTechnicianId = technician.Id,
                LeadTechnicianId = technician.Id,
                AssignedTechnicianIdsJson = System.Text.Json.JsonSerializer.Serialize(new[] { technician.Id }),
                AssignmentType = "IndividualTechnician",
                WorkDoneNotes = isBreakdown ? "Fault found and corrected; machine run and handed back to production." : "Routine completed as per the checklist.",
                FailureProblemCodeId = problem,
                FailureCauseCodeId = cause,
                FailureRemedyCodeId = remedy,
            };
            Db.WorkOrders.Add(job);
            _createdDates.Add((job, at));

            if (!isBreakdown && _planByAsset.TryGetValue(asset.Id, out var planId))
            {
                job.PreventiveMaintenancePlanId = planId;
                // About one in eight routines ran a few days late, so compliance is good but not perfect.
                var late = ++_preventiveCount % 8 == 0;
                var done = late ? completed.AddDays(3) : completed;
                var planEvent = new PmPlanEvent
                {
                    TenantId = _tenantId, PlanId = planId, WorkOrderId = job.Id, Kind = PmPlanEventKinds.Completed, DueDate = at.Date,
                    OccurredAt = done, OnTime = !late, ActorName = technician.FullName,
                };
                Db.PmPlanEvents.Add(planEvent);
                _createdDates.Add((planEvent, done));
            }

            // The assignment row is the source of truth; the job's technician columns only mirror it.
            var assignment = new WorkOrderTechnicianAssignment
            {
                TenantId = _tenantId, WorkOrderId = job.Id, TechnicianId = technician.Id, IsLead = true, AssignedByUserId = _users["planner"].Id,
                Status = "Accepted", AssignedAt = at.AddMinutes(10), AcceptedAt = at.AddMinutes(12),
            };
            Db.WorkOrderTechnicianAssignments.Add(assignment);
            _createdDates.Add((assignment, at.AddMinutes(10)));

            var log = new WorkOrderTimeLog
            {
                TenantId = _tenantId, WorkOrderId = job.Id, TechnicianId = technician.Id, TechnicianName = technician.FullName,
                StartedAt = started, EndedAt = completed, DurationMinutes = (int)(hours * 60), LabourCost = Math.Round(hours * 850m),
            };
            Db.WorkOrderTimeLogs.Add(log);
            _createdDates.Add((log, completed));

            if (part is not null)
            {
                var material = _parts[part];
                var usage = new WorkOrderMaterialUsage
                {
                    TenantId = _tenantId, WorkOrderId = job.Id, MaterialItemId = material.Id, AssetId = asset.Id,
                    QuantityUsed = material.UnitOfMeasure == "kg" ? 2 : 1, UnitCost = material.UnitCost, UsedByUserId = _users["stores"].Id, UsedAt = started.AddMinutes(30),
                };
                Db.WorkOrderMaterialUsages.Add(usage);
                _createdDates.Add((usage, started.AddMinutes(30)));
            }
        }

        /// <summary>Today's work, in every state, raised and moved through the real job service.</summary>
        private async Task AddOpenJobsAsync(CancellationToken ct)
        {
            var planner = new FactoryActor(_users["planner"].Id, _users["planner"].FullName);
            var manager = new FactoryActor(_users["manager"].Id, _users["manager"].FullName);
            var technician = new FactoryActor(_users["technician"].Id, _users["technician"].FullName);

            async Task<WorkOrder> Raise(string asset, string title, string priority, bool breakdown = false, string workType = FactoryWorkTypes.Corrective, string? description = null, int dueInDays = 2, decimal? hours = null, string source = FactoryRequestSources.Office, double startedHoursAgo = 0) =>
                await service.WorkOrderServiceRef.CreateAsync(_tenantId, breakdown ? technician : planner, new CreateFactoryWorkOrder(
                    title, workType, priority, _assets[asset].Id, null, description, breakdown, !breakdown && workType == FactoryWorkTypes.Corrective,
                    _now.Date.AddDays(dueInDays).AddHours(17), breakdown ? _now.AddHours(-startedHoursAgo) : null, hours, source,
                    breakdown ? "Shift supervisor" : null), ct);

            Task Move(WorkOrder job, FactoryActor actor, FactoryTransition transition) => service.WorkOrderServiceRef.ApplyAsync(job, actor, transition, ct);
            FactoryTransition AssignTo(params string[] techs) => new(FactoryWorkOrderActions.Assign, TechnicianIds: techs.Select(x => _technicians[x].Id).ToList(), LeadTechnicianId: _technicians[techs[0]].Id,
                ScheduledStartAt: _now.AddHours(1), ScheduledEndAt: _now.AddHours(4));

            // A breakdown in progress right now on the main rolling line.
            var ctc = await Raise("KPK-CTC-02", "CTC line 2 tripped: main drive motor overload", "Critical", breakdown: true, description: "Line tripped twice this shift. Drive motor hot to touch.", dueInDays: 0, startedHoursAgo: 1.5, source: FactoryRequestSources.Operator);
            await Move(ctc, manager, AssignTo("kevin", "peter"));
            await Move(ctc, technician, new FactoryTransition(FactoryWorkOrderActions.Start));

            // Waiting on a spare: the colour sorter.
            var sorter = await Raise("KPK-CS-01", "Colour sorter ejector valves sticking", "High", description: "About 3% good tea rejected. Two ejector banks affected.", dueInDays: 3, hours: 4m);
            await Move(sorter, manager, new FactoryTransition(FactoryWorkOrderActions.Approve));
            await Move(sorter, planner, AssignTo("faith"));
            await Move(sorter, technician, new FactoryTransition(FactoryWorkOrderActions.Start));
            await Move(sorter, technician, new FactoryTransition(FactoryWorkOrderActions.Hold, Reason: "Ejector valve kit on order from the supplier.", HoldReason: WorkOrderHoldReasons.AwaitingSpares));

            // Needs approval (raised from a running check).
            await Raise("KPK-WT-02", "Withering trough 2 mesh torn at the far end", "Medium", description: "Tear about 40 cm; leaf falling through.", dueInDays: 5, hours: 3m, source: FactoryRequestSources.RunningCheck);
            await Raise("KPK-PK-01", "Bulk packer weighing head drifting", "Medium", dueInDays: 4, hours: 2m);

            // Approved and planned for the next weekly window (line-stop work).
            var bearing = await Raise("KPK-CTC-01", "Replace CTC line 1 drive-end bearing (vibration rising)", "High", description: "Condition monitoring shows vibration in the warning band and rising. Plan for the Monday window.", dueInDays: 6, hours: 6m);
            await Move(bearing, manager, new FactoryTransition(FactoryWorkOrderActions.Approve));
            await Move(bearing, planner, new FactoryTransition(FactoryWorkOrderActions.Plan, ScheduledStartAt: NextMondayAt(7), ScheduledEndAt: NextMondayAt(13), EstimatedHours: 6m));
            await service.SparesServiceRef.ReserveAsync(_store, _parts["BRG-22316"], 1, bearing, false, planner, ct);

            // Assigned and due today.
            var fan = await Raise("KPK-WF-03", "Balance withering fan 3 impeller", "Medium", dueInDays: 0, hours: 3m);
            await Move(fan, manager, new FactoryTransition(FactoryWorkOrderActions.Approve));
            await Move(fan, planner, AssignTo("kevin"));

            // Done, waiting for the supervisor to verify.
            var boiler = await Raise("KPK-BLR-01", "Replace boiler 1 gauge glass", "High", dueInDays: -1, hours: 1.5m);
            await Move(boiler, manager, new FactoryTransition(FactoryWorkOrderActions.Approve));
            await Move(boiler, planner, AssignTo("samuel"));
            await Move(boiler, technician, new FactoryTransition(FactoryWorkOrderActions.Start));
            await service.SparesServiceRef.IssueAsync(_store, _parts["BLR-GG"], 1, boiler, null, null, new FactoryActor(_users["stores"].Id, _users["stores"].FullName), ct);
            await Move(boiler, technician, new FactoryTransition(FactoryWorkOrderActions.Complete, WorkDone: "Gauge glass and seals replaced, tested at working pressure, no leaks.",
                ProblemCodeId: _failureCodes.GetValueOrDefault($"{FailureCodeKinds.Problem}:ELU"), CauseCodeId: _failureCodes.GetValueOrDefault($"{FailureCodeKinds.Cause}:2.4"),
                RemedyCodeId: _failureCodes.GetValueOrDefault($"{FailureCodeKinds.Remedy}:REP")));

            // Overdue: should have been done last week.
            var weighbridge = await Raise("KPK-WB-01", "Clear weighbridge pit drainage", "Low", workType: FactoryWorkTypes.Inspection, dueInDays: -4, hours: 2m);
            await Move(weighbridge, manager, new FactoryTransition(FactoryWorkOrderActions.Approve));

            await Db.SaveChangesAsync(ct);
        }

        private DateTime NextMondayAt(int hour)
        {
            var days = ((int)DayOfWeek.Monday - (int)_now.DayOfWeek + 7) % 7;
            return _now.Date.AddDays(days == 0 ? 7 : days).AddHours(hour);
        }
    }
}
