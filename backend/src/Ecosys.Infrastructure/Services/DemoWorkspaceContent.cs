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
    private IVisitorService VisitorServiceRef => visitorService;
    private IVisitorCardService VisitorCardServiceRef => visitorCardService;
    private IPatrolService PatrolServiceRef => patrolService;

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
            await SaveBackdatedAsync(ct);

            // Everything so far is saved. Stop tracking it: the security content makes hundreds of small saves (one
            // per patrol scan), and each would otherwise re-check every row the demo has created.
            Db.ChangeTracker.Clear();
            await AddSecurityAsync(ct);
            await SaveBackdatedAsync(ct);
        }

        private async Task SaveBackdatedAsync(CancellationToken ct)
        {
            foreach (var (entity, createdAt) in _createdDates)
            {
                entity.CreatedAt = createdAt;
            }

            _createdDates.Clear();
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
                    Department = LoginGrants.ContainsKey(login.Username) ? "Security" : "Maintenance",
                    IsActive = true,
                    HasAllBranchAccess = true,
                    MustChangePassword = false,
                    Permission = new UserPermission(),
                };
                user.PasswordHash = service.PasswordHasherRef.HashPassword(user, password);
                Db.Users.Add(user);
                _users[login.Username] = user;
                foreach (var capability in LoginGrants.GetValueOrDefault(login.Username) ?? [])
                {
                    Db.UserPermissionOverrides.Add(new UserPermissionOverride { TenantId = _tenantId, UserId = user.Id, Capability = capability, IsAllowed = true });
                }
            }

            // The second night guard: patrols alternate between the two. Not a demo login, so its password is unusable.
            var collins = new User
            {
                TenantId = _tenantId, DefaultBranchId = branch.Id, FullName = "Collins Rono", Username = "collins",
                Email = $"collins.{DemoWorkspace.Slug}@{DemoWorkspace.NoEmailDomain}", HasEmail = false, Role = AppRoles.Technician,
                JobTitle = "Security Guard", Department = "Security", IsActive = true, HasAllBranchAccess = true, MustChangePassword = false,
                Permission = new UserPermission(),
            };
            collins.PasswordHash = service.PasswordHasherRef.HashPassword(collins, Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
            Db.Users.Add(collins);
            Db.UserPermissionOverrides.Add(new UserPermissionOverride { TenantId = _tenantId, UserId = collins.Id, Capability = PermissionNames.SecurityPatrol, IsAllowed = true });
            _users["collins"] = collins;

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
                // The latest is yesterday morning: "today 07:00" is still in the future before 07:00 UTC,
                // and the fuel log rejects future fill-ups, which would fail the whole reset.
                var odometer = v.Odo - 9 * 1_150m;
                for (var fill = 8; fill >= 0; fill--)
                {
                    odometer += 1_050m + _random.Next(0, 220);
                    var litres = Math.Round(odometer / 1m % 1 + 210m + _random.Next(0, 40), 0);
                    await service.FleetServiceRef.AddFuelAsync(_tenantId, profile.AssetId, new NewFuelLog(
                        _now.Date.AddDays(-14 * fill - 1).AddHours(7), litres, litres * 182m, odometer, true, "Kericho Rubis", null), Admin, ct);
                }
            }

            await Db.SaveChangesAsync(ct);
        }

        // ── Site security: visitors and patrols ─────────────────────────────

        /// <summary>
        /// The two Factory Operations add-ons, set up the way a customer would: the tea template's access zones and
        /// patrol checkpoints, a box of visitor and contractor cards at the gate, a week of night rounds (the guard
        /// app accepts scans up to a week old, so they go through the real scan checks), security incidents and shift
        /// handovers, and a month of visits with today's visitors still on site. Factory time is UTC+3.
        /// </summary>
        private async Task AddSecurityAsync(CancellationToken ct)
        {
            await service.VisitorServiceRef.SeedZonesAsync(_tenantId, _factory.Id, ct);
            await service.PatrolServiceRef.SeedCheckpointsAsync(_tenantId, _factory.Id, ct);
            await service.VisitorCardServiceRef.AddBatchAsync(_tenantId, new NewCardBatch(_factory.Id, "V-", 1, 40, VisitorCardKinds.Visitor, "Blue"), ct);
            await service.VisitorCardServiceRef.AddBatchAsync(_tenantId, new NewCardBatch(_factory.Id, "C-", 1, 15, VisitorCardKinds.Contractor, "Orange"), ct);

            await AddVisitsAsync(ct);
            await SaveBackdatedAsync(ct); // before the patrols, which stop tracking rows as they go
            await AddPatrolsAsync(ct);
        }

        private static DateTime FactoryLocal(DateTime utc) => utc.AddHours(3);

        private static DateTime FromFactoryLocal(DateTime local) => DateTime.SpecifyKind(local.AddHours(-3), DateTimeKind.Utc);

        private async Task AddPatrolsAsync(CancellationToken ct)
        {
            var patrol = service.PatrolServiceRef;
            var route = await Db.PatrolRoutes.AsNoTracking().Include(x => x.Checkpoints).ThenInclude(x => x.Checkpoint)
                .SingleAsync(x => x.TenantId == _tenantId, ct);
            var stops = route.Checkpoints.OrderBy(x => x.Sequence).Select(x => x.Checkpoint!).ToList();
            PatrolActor Guard(string key) => new(_users[key].Id, _users[key].FullName);

            // Six nights back (inside the guard app's one-week limit) through tomorrow night.
            const int nights = 6;
            await patrol.GenerateRunsAsync(_tenantId, _now.AddDays(-nights), TimeSpan.FromDays(nights) + TimeSpan.FromHours(36), ct);

            var runs = await Db.PatrolRuns.Where(x => x.TenantId == _tenantId && x.DueFrom < _now).OrderBy(x => x.DueFrom).ToListAsync(ct);
            var today = FactoryLocal(_now).Date;
            var slotOfNight = new Dictionary<DateTime, int>();
            foreach (var run in runs)
            {
                // A night starts at 18:00; the rounds after midnight belong to the night before.
                var local = FactoryLocal(run.DueFrom);
                var night = local.Hour < 12 ? local.Date.AddDays(-1) : local.Date;
                var slot = slotOfNight[night] = slotOfNight.GetValueOrDefault(night, -1) + 1;
                var nightsAgo = (today - night).Days;
                var guard = Guard(night.DayOfYear % 2 == 0 ? "guard" : "collins");

                // The stories a supervisor finds on the board: one missed round, one cut short, one out of order,
                // and the perimeter fence found cut.
                if (nightsAgo == 4 && slot == 3) continue;
                var lastStop = nightsAgo == 2 && slot == 4 ? 6 : stops.Count - 1;
                var order = Enumerable.Range(0, lastStop + 1).ToList();
                if (nightsAgo == 3 && slot == 1)
                {
                    order.Insert(4, 5); // the guard skipped ahead to the boiler house, then went back
                }

                var at = run.DueFrom.AddMinutes(2 + _random.Next(0, 7));
                foreach (var index in order)
                {
                    at = at.AddSeconds(120 + _random.Next(0, 90));
                    if (at > _now.AddMinutes(-1)) break; // tonight's round in progress: only what has been walked so far
                    var stop = stops[index];
                    var fenceCut = nightsAgo == 1 && slot == 2 && stop.Name == "Perimeter fence";
                    var answers = (stop.Questions ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(q => new ScanAnswer(q, !(fenceCut && q.StartsWith("Fence", StringComparison.Ordinal)))).ToList();
                    await patrol.ScanAsync(_tenantId, new NewScan(
                        Guid.NewGuid(), $"CHECKPOINT:{stop.TagToken}", at, run.Id, DeviceId: "Gate phone 1", Answers: answers,
                        Note: fenceCut ? "Fence cut behind the firewood store, a gap of about one metre. Fresh footprints inside." : null), guard, ct);
                }

                Db.ChangeTracker.Clear(); // each scan is saved as it is made; keep the next round's saves small
            }

            // Close the rounds whose time is up: the missed and the cut-short ones become Missed and Partial.
            await patrol.CloseOverdueAsync(_tenantId, _now, ct);

            var supervisor = _users["security"];
            var fence = await Db.Incidents.SingleAsync(x => x.TenantId == _tenantId && x.Category == IncidentCategories.Security, ct);
            fence.Severity = "Moderate";
            fence.ResponseStatus = SecurityResponseStatuses.Contained;
            fence.Status = IncidentStatuses.Investigating;
            fence.OwnerUserId = supervisor.Id;
            fence.OwnerName = supervisor.FullName;
            fence.ImmediateAction = "Gap closed with razor wire before dawn; the fencing contractor is booked to replace the panel.";

            var fuelStore = stops.Single(x => x.Name == "Fuel and firewood store");
            var theft = await patrol.ReportIncidentAsync(_tenantId, new NewSecurityIncident(
                _factory.Id, SecurityIncidentKinds.AttemptedTheft, "Attempted theft of firewood from the fuel store",
                "Two men were loading split firewood onto a handcart at the back of the fuel store. They ran towards the tea estate when the guard raised the alarm, leaving the cart and a cut padlock.",
                "Moderate", FromFactoryLocal(today.AddDays(-3).AddHours(3).AddMinutes(40)), fuelStore.Id,
                PersonsInvolved: "Two men, not employees.", ImmediateAction: "Alarm raised, store relocked with a new padlock, reported to Kapkoros police post."), Guard("collins"), ct);
            theft.ResponseStatus = SecurityResponseStatuses.Contained;
            theft.Status = IncidentStatuses.Investigating;
            theft.OwnerUserId = supervisor.Id;
            theft.OwnerName = supervisor.FullName;
            theft.PoliceObNumber = $"OB 14/{today.AddDays(-2):dd/MM/yyyy}";
            theft.ReportedToAuthority = true;

            var vehicle = await patrol.ReportIncidentAsync(_tenantId, new NewSecurityIncident(
                _factory.Id, SecurityIncidentKinds.SuspiciousVehicle, "Pickup parked with its lights off by the leaf shed gate",
                "A white pickup stopped outside the green leaf shed gate and drove off towards Kipchimchim when the guard approached with a torch.",
                "Minor", FromFactoryLocal(today.AddDays(-5).AddHours(2).AddMinutes(10)),
                PersonsInvolved: "Two men in the cab; registration partly read as KDE 4.", ImmediateAction: "Gate house told by radio; description logged."), Guard("guard"), ct);
            vehicle.ResponseStatus = SecurityResponseStatuses.Closed;
            vehicle.Status = IncidentStatuses.Closed;
            vehicle.OwnerUserId = supervisor.Id;
            vehicle.OwnerName = supervisor.FullName;
            vehicle.ClosedAt = FromFactoryLocal(today.AddDays(-5).AddHours(9));
            vehicle.ClosedByName = supervisor.FullName;

            // Morning handovers from the night guard to the day shift, acknowledged.
            for (var daysAgo = 2; daysAgo >= 0; daysAgo--)
            {
                var handedAt = FromFactoryLocal(today.AddDays(-daysAgo).AddHours(6).AddMinutes(5));
                if (handedAt > _now) continue;
                var night = today.AddDays(-daysAgo - 1);
                var (outgoing, incoming) = night.DayOfYear % 2 == 0 ? ("guard", "collins") : ("collins", "guard");
                var handover = await patrol.HandOverAsync(_tenantId, new NewHandover(_factory.Id, "Night shift", _users[incoming].Id,
                    daysAgo == 1 ? "Perimeter fence behind the firewood store patched with razor wire; keep an eye on it until the contractor replaces the panel." : "Quiet night, all rounds walked.",
                    "Gate keys, 2 radios, torch, patrol phone (charged)."), Guard(outgoing), ct);
                await patrol.AcknowledgeHandoverAsync(_tenantId, handover.Id, Guard(incoming), ct);
                handover.HandedOverAt = handedAt;
                handover.AcknowledgedAt = handedAt.AddMinutes(8);
                _createdDates.Add((handover, handedAt));
            }

            foreach (var incident in new[] { fence, theft, vehicle })
            {
                _createdDates.Add((incident, incident.OccurredAt.AddMinutes(10)));
            }

            await Db.SaveChangesAsync(ct);
        }

        private async Task AddVisitsAsync(CancellationToken ct)
        {
            var visitors = service.VisitorServiceRef;
            var zones = await Db.AccessZones.AsNoTracking().Where(x => x.TenantId == _tenantId).ToDictionaryAsync(x => x.Name, x => x.Id, ct);
            var escortZones = await Db.AccessZones.AsNoTracking().Where(x => x.TenantId == _tenantId && x.RequiresEscort).Select(x => x.Name).ToListAsync(ct);
            var gate = new VisitActor(_users["security"].Id, _users["security"].FullName, CanManage: true);
            VisitActor Host(string key) => new(_users[key].Id, _users[key].FullName);
            var today = FactoryLocal(_now).Date;
            var nextCard = new Dictionary<string, int> { ["V-"] = 0, ["C-"] = 0 };
            string Card(string prefix) => $"{prefix}{(nextCard[prefix] = nextCard[prefix] % 15 + 1):000}";

            // A month of visits, oldest first so the visit numbers run in date order. People who come back keep their
            // record (matched by ID number), and the gate gives the induction once and then sees it is current.
            (int DaysAgo, double At, double Hours, string Purpose, string Host, string[] Zones, (string Name, string Company, string Id)[] People, string? Vehicle)[] history =
            [
                (29, 9.5, 2, VisitPurposes.Meeting, "manager", ["Reception and offices"], [("Nancy Wambui", "KTDA Kericho region", "23415678")], "KCR 118B"),
                (27, 8.25, 7, VisitPurposes.ContractorWork, "planner", ["Processing hall", "Stores and workshop"], [("Hassan Omondi", "Precision Roller Works", "27788120"), ("Brian Otieno", "Precision Roller Works", "31120456")], "KCW 902M"),
                (26, 10, 1.5, VisitPurposes.Delivery, "stores", ["Stores and workshop"], [("Peter Mwangi", "Davis & Shirtliff", "22987311")], "KBZ 771T"),
                (24, 11, 3, VisitPurposes.Inspection, "admin", ["Boiler house"], [("Eng. Grace Atieno", "DOSHS Kericho", "20111987")], null),
                (22, 9, 6, VisitPurposes.ContractorWork, "admin", ["Boiler house"], [("Daniel Kimutai", "Rift Valley Boiler Services", "25566001"), ("Kelvin Mutua", "Rift Valley Boiler Services", "33450912")], "KCK 455R"),
                (20, 14, 1, VisitPurposes.Interview, "manager", ["Reception and offices"], [("Esther Chelangat", "", "35567120")], null),
                (19, 7.5, 1, VisitPurposes.Delivery, "stores", ["Weighbridge"], [("Gideon Langat", "Kericho Fuelwood Suppliers", "29001342")], "KAX 310J"),
                (17, 10.5, 4, VisitPurposes.Audit, "admin", ["Reception and offices", "Processing hall", "Packing and finished goods"], [("Mary Njoroge", "Rainforest Alliance", "21456789")], "KDB 640A"),
                (15, 9, 2, VisitPurposes.Meeting, "manager", ["Reception and offices"], [("Nancy Wambui", "KTDA Kericho region", "23415678")], "KCR 118B"),
                (13, 8, 8, VisitPurposes.ContractorWork, "planner", ["Stores and workshop"], [("Mercy Chepkemoi", "Kericho Electrical Contractors", "26678345")], "KCF 210D"),
                (12, 7.5, 1, VisitPurposes.Delivery, "stores", ["Weighbridge"], [("Gideon Langat", "Kericho Fuelwood Suppliers", "29001342")], "KAX 310J"),
                (10, 11, 1, VisitPurposes.Meeting, "admin", ["Reception and offices"], [("Samuel Rotich", "Equity Bank Kericho", "24456002")], null),
                (8, 9.5, 5, VisitPurposes.ContractorWork, "planner", ["Processing hall"], [("Hassan Omondi", "Precision Roller Works", "27788120")], "KCW 902M"),
                (6, 13, 1.5, VisitPurposes.Delivery, "stores", ["Stores and workshop"], [("Peter Mwangi", "Davis & Shirtliff", "22987311")], "KBZ 771T"),
                (5, 7.5, 1, VisitPurposes.Delivery, "stores", ["Weighbridge"], [("Gideon Langat", "Kericho Fuelwood Suppliers", "29001342")], "KAX 310J"),
                (3, 10, 2, VisitPurposes.Meeting, "manager", ["Reception and offices"], [("Paul Kiprono", "Ministry of Agriculture", "22001876"), ("Lucy Achieng", "Ministry of Agriculture", "30098712")], "GKB 412C"),
                (2, 9, 3, VisitPurposes.Inspection, "admin", ["Reception and offices", "Fuel and firewood store"], [("Insp. John Kariuki", "Kapkoros Police Post", "19876543")], null),
                (1, 15, 1, VisitPurposes.Interview, "manager", ["Reception and offices"], [("Collins Kiprop", "", "36678901")], null),
            ];

            foreach (var h in history)
            {
                var arrival = FromFactoryLocal(today.AddDays(-h.DaysAgo).AddHours(h.At));
                await AddCompletedVisitAsync(h.Purpose, h.Host, h.Zones, h.People, h.Vehicle, arrival, arrival.AddHours(h.Hours));
            }

            // Caught leaving with made tea under the firewood: blocked at the gate from now on.
            var banned = await Db.Visitors.SingleAsync(x => x.TenantId == _tenantId && x.FullName == "Gideon Langat", ct);
            await visitors.SetBlockedAsync(_tenantId, banned.Id, true, $"Found with 6 kg of made tea hidden in the fuelwood lorry on {today.AddDays(-5):dd MMM}. Do not admit.", gate, ct);

            // Today: a roller fitter at work in the processing hall with an escort, and the KTDA officer in a meeting.
            var morning = FromFactoryLocal(today.AddHours(8));
            var arrived = morning < _now.AddMinutes(-40) ? morning : _now.AddMinutes(-40);
            await AddOnSiteVisitAsync(VisitPurposes.ContractorWork, "planner", ["Processing hall"],
                [("Hassan Omondi", "Precision Roller Works", "27788120"), ("Brian Otieno", "Precision Roller Works", "31120456")], "KCW 902M", arrived, 9, "technician");
            await AddOnSiteVisitAsync(VisitPurposes.Meeting, "manager", ["Reception and offices"],
                [("Nancy Wambui", "KTDA Kericho region", "23415678")], "KCR 118B", arrived.AddMinutes(25), 3, null);

            // Coming later: a fuel oil delivery this afternoon and a KEBS audit tomorrow, both approved; a sales visit
            // the gate booked for the manager, still waiting for the manager to approve.
            await visitors.CreateAsync(_tenantId, new NewVisit(_factory.Id, VisitPurposes.Delivery,
                [new VisitorDetails("Joseph Kamau", "+254 720 551 230", null, "Vivo Energy", "National ID", "28890011")], _users["stores"].Id,
                _now.AddHours(3), _now.AddHours(5), [zones["Fuel and firewood store"]], VehicleRegistration: "KCU 802Z", Notes: "18,000 L furnace oil for the boiler day tank."), Host("stores"), ct);
            await visitors.CreateAsync(_tenantId, new NewVisit(_factory.Id, VisitPurposes.Audit,
                [new VisitorDetails("Faith Mutheu", "+254 722 117 905", null, "Kenya Bureau of Standards", "National ID", "25113478"),
                 new VisitorDetails("Ali Hussein", "+254 733 640 221", null, "Kenya Bureau of Standards", "National ID", "30772219")], _users["admin"].Id,
                FromFactoryLocal(today.AddDays(1).AddHours(9)), FromFactoryLocal(today.AddDays(1).AddHours(15)),
                [zones["Reception and offices"], zones["Processing hall"], zones["Packing and finished goods"]], Notes: "Annual product certification audit."), Host("admin"), ct);
            await visitors.CreateAsync(_tenantId, new NewVisit(_factory.Id, VisitPurposes.Meeting,
                [new VisitorDetails("Kevin Mbugua", "+254 711 902 334", null, "Bühler East Africa", "National ID", "29934410")], _users["manager"].Id,
                FromFactoryLocal(today.AddDays(1).AddHours(11)), FromFactoryLocal(today.AddDays(1).AddHours(12)),
                [zones["Reception and offices"]], Notes: "Wants to present a new colour sorter."), new VisitActor(_users["security"].Id, _users["security"].FullName), ct);

            async Task<List<VisitAttendee>> CreateAndCheckInAsync(string purpose, string host, string[] zoneNames, (string Name, string Company, string Id)[] people,
                string? registration, string? escort)
            {
                var visit = await visitors.CreateAsync(_tenantId, new NewVisit(_factory.Id, purpose,
                    people.Select(p => new VisitorDetails(p.Name, "+254 7" + p.Id[..8], null, p.Company.Length == 0 ? null : p.Company, "National ID", p.Id)).ToList(),
                    _users[host].Id, _now.AddMinutes(-5), _now.AddHours(4), zoneNames.Select(z => zones[z]).ToList(), VehicleRegistration: registration), Host(host), ct);
                var attendees = await Db.VisitAttendees.Where(x => x.VisitId == visit.Id).ToListAsync(ct);
                var needsEscort = zoneNames.Any(escortZones.Contains);
                var cardPrefix = purpose == VisitPurposes.ContractorWork ? "C-" : "V-";
                foreach (var attendee in attendees)
                {
                    await visitors.CheckInAsync(_tenantId, attendee.Id, new GateConfirmation(
                        IdVerified: true, PpeConfirmed: true, RecordInduction: true, EscortUserId: needsEscort ? _users[escort ?? host].Id : null, CardCode: Card(cardPrefix)), gate, ct);
                }

                return attendees;
            }

            async Task AddCompletedVisitAsync(string purpose, string host, string[] zoneNames, (string Name, string Company, string Id)[] people,
                string? registration, DateTime arrival, DateTime departure)
            {
                var attendees = await CreateAndCheckInAsync(purpose, host, zoneNames, people, registration, null);
                foreach (var attendee in attendees)
                {
                    await visitors.CheckOutAsync(_tenantId, attendee.Id, gate, ct);
                }

                await BackdateVisitAsync(attendees, arrival, departure);
            }

            async Task AddOnSiteVisitAsync(string purpose, string host, string[] zoneNames, (string Name, string Company, string Id)[] people,
                string? registration, DateTime arrival, double hours, string? escort)
            {
                var attendees = await CreateAndCheckInAsync(purpose, host, zoneNames, people, registration, escort);
                await BackdateVisitAsync(attendees, arrival, null);
                var visit = await Db.Visits.SingleAsync(x => x.Id == attendees[0].VisitId, ct);
                // Still within their time whenever the demo is reset, so they show as on site rather than overstayed.
                visit.ExpectedTo = arrival.AddHours(hours) > _now.AddHours(2) ? arrival.AddHours(hours) : _now.AddHours(2);
            }

            // The gate stamps everything "now"; move the visit, its people, cards and inductions to when it happened.
            async Task BackdateVisitAsync(List<VisitAttendee> attendees, DateTime arrival, DateTime? departure)
            {
                var visit = await Db.Visits.SingleAsync(x => x.Id == attendees[0].VisitId, ct);
                var booked = arrival.AddHours(-20);
                visit.ExpectedFrom = arrival.AddMinutes(-15);
                visit.ExpectedTo = (departure ?? arrival.AddHours(4)).AddMinutes(30);
                visit.DecidedAt = booked;
                visit.CompletedAt = departure;
                _createdDates.Add((visit, booked));
                var minute = 0;
                foreach (var attendee in attendees)
                {
                    var inAt = arrival.AddMinutes(minute++);
                    attendee.CheckedInAt = inAt;
                    attendee.CardIssuedAt = attendee.CardId is null ? null : inAt;
                    if (departure is { } outAt)
                    {
                        attendee.CheckedOutAt = outAt.AddMinutes(minute);
                        attendee.CardReturnedAt = attendee.CardId is null ? null : outAt.AddMinutes(minute);
                    }

                    _createdDates.Add((attendee, booked));
                    var visitor = await Db.Visitors.SingleAsync(x => x.Id == attendee.VisitorId, ct);
                    visitor.LastVisitAt = inAt;
                    foreach (var induction in await Db.TrainingRecords
                        .Where(x => x.TenantId == _tenantId && x.PersonName == visitor.FullName && x.CompletedOn == _now.Date).ToListAsync(ct))
                    {
                        induction.CompletedOn = FactoryLocal(inAt).Date;
                        induction.ExpiresOn = induction.CompletedOn.AddYears(1);
                    }
                }

                // The vehicle came in with them and, for a finished visit, the gate logged it out when they left.
                foreach (var movement in await Db.VehicleMovements.Where(x => x.VisitId == visit.Id).ToListAsync(ct))
                {
                    movement.EnteredAt = arrival;
                    movement.ExitedAt = departure?.AddMinutes(minute);
                    movement.ExitedByName = departure is null ? null : gate.Name;
                    _createdDates.Add((movement, arrival));
                }

                await Db.SaveChangesAsync(ct);
            }
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
