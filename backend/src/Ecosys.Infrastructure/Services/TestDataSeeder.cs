// TEST CREDENTIALS — DELETE BEFORE PRODUCTION
using Ecosys.Domain.Entities;
using Ecosys.Domain.Enums;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecosys.Infrastructure.Services;

public interface ITestDataSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}

internal sealed class TestDataSeeder(
    AppDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    IOptions<PlatformAdminOptions> platformAdminOptions,
    ILogger<TestDataSeeder> logger) : ITestDataSeeder
{
    // ── Fixed GUIDs so re-runs are idempotent ────────────────────────────────
    private static readonly Guid DemoTenantId       = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid DemoBranchId       = Guid.Parse("10000000-0000-0000-0000-000000000010");
    private static readonly Guid ManagerUserId      = Guid.Parse("10000000-0000-0000-0000-000000000020");
    private static readonly Guid TechUserId         = Guid.Parse("10000000-0000-0000-0000-000000000030");
    private static readonly Guid TechnicianId       = Guid.Parse("10000000-0000-0000-0000-000000000031");
    private static readonly Guid ClientId           = Guid.Parse("10000000-0000-0000-0000-000000000040");
    private static readonly Guid SiteAId            = Guid.Parse("10000000-0000-0000-0000-000000000050");
    private static readonly Guid SiteBId            = Guid.Parse("10000000-0000-0000-0000-000000000051");
    private static readonly Guid AssetAId           = Guid.Parse("10000000-0000-0000-0000-000000000060");
    private static readonly Guid AssetBId           = Guid.Parse("10000000-0000-0000-0000-000000000061");
    private static readonly Guid ContractGeneratorId = Guid.Parse("10000000-0000-0000-0000-000000000080");
    private static readonly Guid ContractHvacId      = Guid.Parse("10000000-0000-0000-0000-000000000081");
    private static readonly Guid ContractAdhocId     = Guid.Parse("10000000-0000-0000-0000-000000000082");
    private static readonly Guid GeneratorRateCardId = Guid.Parse("10000000-0000-0000-0000-000000000083");
    private static readonly Guid WorkOrder1Id       = Guid.Parse("10000000-0000-0000-0000-000000000070");
    private static readonly Guid WorkOrder2Id       = Guid.Parse("10000000-0000-0000-0000-000000000071");
    private static readonly Guid WorkOrder3Id       = Guid.Parse("10000000-0000-0000-0000-000000000072");

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // Idempotency guard — skip if demo tenant already exists
        if (await dbContext.Tenants.AnyAsync(x => x.Id == DemoTenantId, cancellationToken))
        {
            await UpgradeDemoTechnicianRoleAsync(cancellationToken);
            logger.LogInformation("Demo seed data already present — skipping.");
            return;
        }

        logger.LogInformation("Seeding demo test data…");

        var now = DateTime.UtcNow;

        // ── 1. Tenant ────────────────────────────────────────────────────────
        var tenant = new Tenant
        {
            Id          = DemoTenantId,
            Name        = "Ecosys Demo Company",
            CompanyName = "Ecosys Demo Company",
            Slug        = "demo",
            Email       = "admin@demo.co.ke",
            ContactName = "Dennis Yegon",
            ContactEmail = "manager@demo.co.ke",
            Phone       = "+254700000000",
            Country     = "Kenya",
            Industry    = "Facilities Management",
            City        = "Nairobi",
            Status      = "Active",
            LicenseStatus = "Trial",
            PlanName    = "Ecosys ServiceOps Suite",
            MaxUsers    = 5,
            MaxBranches = 1,
            IsActive    = true,
            TrialStartsAt = now,
            TrialEndsAt   = now.AddDays(30),
            SubscriptionStartsAt = now,
            CreatedAt   = now,
        };
        dbContext.Tenants.Add(tenant);

        // ── 2. Tenant license ────────────────────────────────────────────────
        var trialPlan = await dbContext.LicensePlans
            .SingleAsync(x => x.PlanCode == ServiceOpsPlans.Professional, cancellationToken);

        dbContext.TenantLicenses.Add(new TenantLicense
        {
            TenantId      = DemoTenantId,
            LicensePlanId = trialPlan.Id,
            OfficeSeats   = 5,
            FieldSeats    = 5,
            Status        = "Trial",
            StartsAt      = now,
            TrialEndsAt   = now.AddDays(30),
            ExpiresAt     = now.AddDays(30),
            CreatedAt     = now,
        });

        // ── 3. Support records ────────────────────────────────────────────────
        dbContext.EmailSettings.Add(new EmailSetting
        {
            TenantId             = DemoTenantId,
            Host                 = "localhost",
            Port                 = 25,
            UseSsl               = false,
            SenderName           = "Ecosys Demo Company",
            SenderAddress        = "admin@demo.co.ke",
            CreatedAt            = now,
        });

        dbContext.NotificationSettings.Add(new NotificationSetting
        {
            TenantId  = DemoTenantId,
            CreatedAt = now,
        });

        dbContext.MonitoringSettings.Add(new MonitoringSetting
        {
            TenantId  = DemoTenantId,
            CreatedAt = now,
        });

        dbContext.TenantSecurityPolicies.Add(new TenantSecurityPolicy
        {
            TenantId  = DemoTenantId,
            CreatedAt = now,
        });

        // ── 4. Branch ─────────────────────────────────────────────────────────
        dbContext.Branches.Add(new Branch
        {
            Id        = DemoBranchId,
            TenantId  = DemoTenantId,
            Name      = "Head Office",
            Code      = "HQ",
            Location  = "Nairobi",
            IsActive  = true,
            CreatedAt = now,
        });

        // ── 5. Users ──────────────────────────────────────────────────────────
        // 5a. Tenant Manager (Admin role)
        var manager = new User
        {
            Id                 = ManagerUserId,
            TenantId           = DemoTenantId,
            DefaultBranchId    = DemoBranchId,
            FullName           = "Demo Manager",
            Email              = "manager@demo.co.ke",
            Role               = AppRoles.Admin,
            JobTitle           = "Operations Manager",
            IsActive           = true,
            HasAllBranchAccess = true,
            MustChangePassword = false,
            CreatedAt          = now,
        };
        manager.PasswordHash = passwordHasher.HashPassword(manager, "Manager@1234");
        manager.Permission = new UserPermission
        {
            UserId                    = ManagerUserId,
            CanViewWorkOrders         = true,
            CanCreateWorkOrders       = true,
            CanAssignWorkOrders       = true,
            CanCompleteWorkOrders     = true,
            CanApproveMaterials       = true,
            CanIssueMaterials         = true,
            CanManageAssets           = true,
            CanManageSettings         = true,
            CanViewReports            = true,
            CanViewFinance            = true,
            CanManageFinance          = true,
            CanViewFinanceRates       = true,
            CanManageFinanceRates     = true,
            CanViewFinanceApprovals   = true,
            CanManageFinanceApprovals = true,
            CanViewBillableWork       = true,
            CanManageBillableWork     = true,
            CanOverrideMaterialStock = true,
            CanViewCrm = true,
            CanManageCrm = true,
            CreatedAt                 = now,
        };
        dbContext.Users.Add(manager);

        // 5b. Technician (Technician role + Technician profile)
        var tech = new User
        {
            Id                 = TechUserId,
            TenantId           = DemoTenantId,
            DefaultBranchId    = DemoBranchId,
            FullName           = "Demo Technician",
            Email              = "tech@demo.co.ke",
            Phone              = "0700000001",
            Role               = AppRoles.Technician,
            JobTitle           = "Field Technician",
            IsActive           = true,
            HasAllBranchAccess = false,
            MustChangePassword = false,
            CreatedAt          = now,
        };
        tech.PasswordHash = passwordHasher.HashPassword(tech, "Tech@1234");
        tech.Permission = new UserPermission
        {
            UserId                = TechUserId,
            CanViewWorkOrders     = true,
            CanCompleteWorkOrders = true,
            CreatedAt             = now,
        };
        dbContext.Users.Add(tech);
        // Without a branch row a branch-scoped user sees no work orders at all, even their own.
        dbContext.UserBranchAssignments.Add(new UserBranchAssignment
        {
            TenantId  = DemoTenantId,
            UserId    = TechUserId,
            BranchId  = DemoBranchId,
            IsDefault = true,
            CreatedAt = now,
        });

        // 5c. Technician profile linked to tech user
        dbContext.Technicians.Add(new Technician
        {
            Id            = TechnicianId,
            TenantId      = DemoTenantId,
            UserId        = TechUserId,
            BranchId      = DemoBranchId,
            FullName      = "Demo Technician",
            Email         = "tech@demo.co.ke",
            Phone         = "0700000001",
            SkillCategory = "Electrical",
            IsActive      = true,
            CreatedAt     = now,
        });

        // ── 6. Client ─────────────────────────────────────────────────────────
        dbContext.Clients.Add(new Client
        {
            Id            = ClientId,
            TenantId      = DemoTenantId,
            ClientName    = "Nairobi Hospital",
            ClientType    = "Corporate",
            Email         = "john@nairobihospital.co.ke",
            Phone         = "0700111222",
            ContactPerson = "John Kamau",
            ContactPhone  = "0700111222",
            Location      = "Nairobi, Kenya",
            IsActive      = true,
            CreatedAt     = now,
        });

        // ── 7. Sites ──────────────────────────────────────────────────────────
        dbContext.Sites.Add(new Site
        {
            Id          = SiteAId,
            TenantId    = DemoTenantId,
            ClientId    = ClientId,
            SiteCode    = "NH-MAIN",
            SiteName    = "Nairobi Hospital – Main Campus",
            SiteType    = "Main",
            Status      = "Active",
            TownCity    = "Nairobi",
            County      = "Nairobi",
            Country     = "Kenya",
            StreetAddress = "Argwings Kodhek Road",
            ContactPerson = "John Kamau",
            ContactPhone  = "0700111222",
            ContactEmail  = "john@nairobihospital.co.ke",
            CreatedAt   = now,
        });

        dbContext.Sites.Add(new Site
        {
            Id          = SiteBId,
            TenantId    = DemoTenantId,
            ClientId    = ClientId,
            SiteCode    = "NH-ANNEX",
            SiteName    = "Nairobi Hospital – Annex",
            SiteType    = "Branch",
            Status      = "Active",
            TownCity    = "Nairobi",
            County      = "Nairobi",
            Country     = "Kenya",
            StreetAddress = "Ralph Bunche Road",
            ContactPerson = "Jane Wanjiku",
            ContactPhone  = "0700111223",
            CreatedAt   = now,
        });

        // ── 8. Assets ─────────────────────────────────────────────────────────
        dbContext.Assets.Add(new Asset
        {
            Id            = AssetAId,
            TenantId      = DemoTenantId,
            BranchId      = DemoBranchId,
            ClientId      = ClientId,
            SiteId        = SiteAId,
            AssetName     = "Generator – 100KVA",
            AssetCode     = "GEN-001",
            AssetType     = "Generator",
            OwnershipType = "ClientOwned",
            Manufacturer  = "Perkins",
            Model         = "100KVA Silent",
            Status        = "Active",
            SerialNumber  = "PK-2021-001",
            Location      = "Plant Room – Ground Floor",
            CreatedAt     = now,
        });

        dbContext.Assets.Add(new Asset
        {
            Id            = AssetBId,
            TenantId      = DemoTenantId,
            BranchId      = DemoBranchId,
            ClientId      = ClientId,
            SiteId        = SiteAId,
            AssetName     = "Air Handling Unit – Ward A",
            AssetCode     = "AHU-001",
            AssetType     = "HVAC",
            OwnershipType = "ClientOwned",
            Manufacturer  = "Carrier",
            Model         = "AHU 500",
            Status        = "Active",
            SerialNumber  = "CA-2020-042",
            Location      = "Roof – Block A",
            CreatedAt     = now,
        });

        // ── 9. Work Orders ────────────────────────────────────────────────────
        dbContext.WorkOrders.Add(new WorkOrder
        {
            Id                  = WorkOrder1Id,
            TenantId            = DemoTenantId,
            BranchId            = DemoBranchId,
            ClientId            = ClientId,
            SiteId              = SiteAId,
            AssetId             = AssetAId,
            WorkOrderNumber     = "WO-2026-0001",
            Title               = "Routine Generator Maintenance",
            Description         = "Quarterly inspection and oil change for the 100KVA generator.",
            Priority            = "Medium",
            Status              = "Open",
            WorkType            = "PreventiveMaintenance",
            ContractId          = ContractGeneratorId,
            CoverageStatus      = CoverageStatus.Covered.ToString(),
            BillingModel        = BillingModel.PreferentialRates.ToString(),
            AppliedSlaSource    = "Contract",
            AppliedRateSource   = "ContractRateCardDefault",
            EstimatedCharge     = 5000m,
            PricingNotes        = "Preferential generator AMC rates apply.",
            AssignedTechnicianId = TechnicianId,
            DueDate             = now.AddDays(7),
            NotifyClient        = true,
            ClientContactEmail  = "john@nairobihospital.co.ke",
            CreatedAt           = now,
        });

        dbContext.WorkOrders.Add(new WorkOrder
        {
            Id                  = WorkOrder2Id,
            TenantId            = DemoTenantId,
            BranchId            = DemoBranchId,
            ClientId            = ClientId,
            SiteId              = SiteAId,
            AssetId             = AssetBId,
            WorkOrderNumber     = "WO-2026-0002",
            Title               = "AHU Fault – Unusual Noise",
            Description         = "Client reported unusual vibration noise from the AHU in Ward A. Technician to diagnose and repair.",
            Priority            = "High",
            Status              = "In Progress",
            WorkType            = "CorrectiveMaintenance",
            AssignedTechnicianId = TechnicianId,
            WorkStartedAt       = now.AddHours(-2),
            DueDate             = now.AddDays(1),
            NotifyClient        = true,
            ClientContactEmail  = "john@nairobihospital.co.ke",
            CreatedAt           = now.AddDays(-1),
        });

        dbContext.WorkOrders.Add(new WorkOrder
        {
            Id                  = WorkOrder3Id,
            TenantId            = DemoTenantId,
            BranchId            = DemoBranchId,
            ClientId            = ClientId,
            SiteId              = SiteBId,
            WorkOrderNumber     = "WO-2026-0003",
            Title               = "Electrical Socket Replacement – Room 12",
            Description         = "Replace damaged 3-pin sockets in consultation Room 12, Annex block.",
            Priority            = "Low",
            Status              = "Completed",
            WorkType            = "CorrectiveMaintenance",
            ContractId          = ContractAdhocId,
            CoverageStatus      = CoverageStatus.NotCovered.ToString(),
            BillingModel        = BillingModel.ChargeableCallout.ToString(),
            AppliedRateSource   = "StandardChargeableFallback",
            EstimatedCharge     = 12000m,
            FinalCharge         = 12000m,
            PricingNotes        = "Adhoc chargeable support agreement.",
            AssignedTechnicianId = TechnicianId,
            WorkStartedAt       = now.AddDays(-3),
            CompletedAt         = now.AddDays(-2),
            WorkDoneNotes       = "Replaced 4 damaged sockets. All tested and confirmed operational.",
            NotifyClient        = true,
            ClientContactEmail  = "john@nairobihospital.co.ke",
            CreatedAt           = now.AddDays(-4),
        });

        // —— 10. Contracts —— 
        dbContext.Contracts.AddRange(
            new Contract
            {
                Id = ContractGeneratorId,
                TenantId = DemoTenantId,
                ClientId = ClientId,
                ContractNumber = "AMC-2026-0041",
                Name = "Generator Comprehensive Maintenance Contract",
                ContractType = ContractType.ComprehensiveMaintenance,
                Status = ContractStatus.Active,
                StartDate = now.Date.AddMonths(-2),
                EndDate = now.Date.AddMonths(10),
                DurationMonths = 12,
                BillingModel = BillingModel.PreferentialRates,
                Currency = "KES",
                ContractValue = 1250000m,
                RenewalNoticeDays = 30,
                Description = "Comprehensive generator support with preferential parts and labour rates.",
                IncludedServices = "Routine PM, inspection, callout triage",
                ExcludedServices = "Major overhaul, alternator rewind",
                Notes = "Generator shutdown approval required before major works.",
                RenewalOwnerUserId = ManagerUserId,
                AccountManagerUserId = ManagerUserId,
                InternalApproverUserId = ManagerUserId,
                ActivatedAt = now.AddMonths(-2),
                AutoExpireEnabled = true,
                RenewalRisk = RenewalRisk.Low,
                GracePeriodDays = 7,
                AllowPreferentialRatesDuringGracePeriod = true,
                AllowSlaDuringGracePeriod = true,
                ResponseTimeHours = 4,
                ResolutionTimeHours = 24,
                WorkingHoursPolicy = "BusinessHours",
                PauseSlaOnHold = true,
                EscalationEmail = "manager@demo.co.ke",
                PriorityLevel = "High",
                GracePeriodAllowsSla = true,
                CreatedAt = now,
            },
            new Contract
            {
                Id = ContractHvacId,
                TenantId = DemoTenantId,
                ClientId = ClientId,
                ContractNumber = "PM-2026-0007",
                Name = "HVAC Preventive Maintenance Contract",
                ContractType = ContractType.PreventiveMaintenance,
                Status = ContractStatus.Active,
                StartDate = now.Date.AddMonths(-1),
                EndDate = now.Date.AddMonths(11),
                DurationMonths = 12,
                BillingModel = BillingModel.Included,
                Currency = "KES",
                ContractValue = 840000m,
                RenewalNoticeDays = 60,
                Description = "Scheduled HVAC preventive maintenance for critical ward systems.",
                IncludedServices = "Quarterly PM, filter changes, belts inspection",
                ExcludedServices = "Compressor replacement",
                Notes = "Keep ward disruption to a minimum during service windows.",
                RenewalOwnerUserId = ManagerUserId,
                AccountManagerUserId = ManagerUserId,
                InternalApproverUserId = ManagerUserId,
                ActivatedAt = now.AddMonths(-1),
                AutoExpireEnabled = true,
                RenewalRisk = RenewalRisk.Low,
                GracePeriodDays = 5,
                AllowPreferentialRatesDuringGracePeriod = false,
                AllowSlaDuringGracePeriod = true,
                ResponseTimeHours = 6,
                ResolutionTimeHours = 48,
                WorkingHoursPolicy = "BusinessHours",
                PauseSlaOnHold = true,
                EscalationEmail = "manager@demo.co.ke",
                PriorityLevel = "Medium",
                GracePeriodAllowsSla = true,
                CreatedAt = now,
            },
            new Contract
            {
                Id = ContractAdhocId,
                TenantId = DemoTenantId,
                ClientId = ClientId,
                ContractNumber = "ADHOC-2026-0012",
                Name = "Adhoc Pay As You Go Support",
                ContractType = ContractType.AdhocPayAsYouGo,
                Status = ContractStatus.Active,
                StartDate = now.Date.AddDays(-15),
                EndDate = now.Date.AddMonths(6),
                DurationMonths = 6,
                BillingModel = BillingModel.ChargeableCallout,
                Currency = "KES",
                ContractValue = 0m,
                RenewalNoticeDays = 14,
                Description = "Non-entitlement support agreement with standard chargeable callout treatment.",
                IncludedServices = "Dispatch coordination only",
                ExcludedServices = "Labour, parts, travel",
                Notes = "Use standard chargeable rates when no other coverage applies.",
                RenewalOwnerUserId = ManagerUserId,
                AccountManagerUserId = ManagerUserId,
                InternalApproverUserId = ManagerUserId,
                ActivatedAt = now.AddDays(-15),
                AutoExpireEnabled = true,
                RenewalRisk = RenewalRisk.Low,
                GracePeriodDays = 0,
                AllowPreferentialRatesDuringGracePeriod = false,
                AllowSlaDuringGracePeriod = false,
                WorkingHoursPolicy = "BusinessHours",
                PauseSlaOnHold = true,
                EscalationEmail = "manager@demo.co.ke",
                PriorityLevel = "Standard",
                CreatedAt = now,
            });

        dbContext.ContractSites.AddRange(
            new ContractSite { TenantId = DemoTenantId, ContractId = ContractGeneratorId, SiteId = SiteAId, CreatedAt = now },
            new ContractSite { TenantId = DemoTenantId, ContractId = ContractHvacId, SiteId = SiteAId, CreatedAt = now },
            new ContractSite { TenantId = DemoTenantId, ContractId = ContractAdhocId, SiteId = SiteBId, CreatedAt = now });

        dbContext.ContractAssets.AddRange(
            new ContractAsset { TenantId = DemoTenantId, ContractId = ContractGeneratorId, AssetId = AssetAId, CreatedAt = now },
            new ContractAsset { TenantId = DemoTenantId, ContractId = ContractHvacId, AssetId = AssetBId, CreatedAt = now });

        dbContext.ContractAssetCategories.AddRange(
            new ContractAssetCategory { TenantId = DemoTenantId, ContractId = ContractGeneratorId, AssetCategoryName = "Generator", CreatedAt = now },
            new ContractAssetCategory { TenantId = DemoTenantId, ContractId = ContractHvacId, AssetCategoryName = "HVAC", CreatedAt = now },
            new ContractAssetCategory { TenantId = DemoTenantId, ContractId = ContractAdhocId, AssetCategoryName = "UPS", CreatedAt = now });

        dbContext.ContractWorkOrderTypes.AddRange(
            new ContractWorkOrderType { TenantId = DemoTenantId, ContractId = ContractGeneratorId, WorkOrderTypeName = "CorrectiveMaintenance", CreatedAt = now },
            new ContractWorkOrderType { TenantId = DemoTenantId, ContractId = ContractGeneratorId, WorkOrderTypeName = "PreventiveMaintenance", CreatedAt = now },
            new ContractWorkOrderType { TenantId = DemoTenantId, ContractId = ContractHvacId, WorkOrderTypeName = "PreventiveMaintenance", CreatedAt = now });

        foreach (var contractId in new[] { ContractGeneratorId, ContractHvacId, ContractAdhocId })
        {
            foreach (var days in new[] { 90, 60, 30, 14, 7, 0 })
            {
                dbContext.ContractReminderRules.Add(new ContractReminderRule
                {
                    TenantId = DemoTenantId,
                    ContractId = contractId,
                    DaysBeforeExpiry = days,
                    NotifyAccountManager = true,
                    NotifyRenewalOwner = true,
                    NotifyTenantAdmin = true,
                    EmailEnabled = true,
                    InAppEnabled = false,
                    IsActive = true,
                    CreatedAt = now,
                });
            }
        }

        dbContext.ContractRateCards.Add(new ContractRateCard
        {
            Id = GeneratorRateCardId,
            TenantId = DemoTenantId,
            ContractId = ContractGeneratorId,
            Name = "Generator AMC Preferential Rates",
            Description = "Negotiated generator maintenance rates for contract customers.",
            Currency = "KES",
            IsDefault = true,
            EffectiveFrom = now.Date.AddMonths(-2),
            EffectiveTo = now.Date.AddMonths(10),
            IsActive = true,
            CreatedAt = now,
        });

        dbContext.ContractRateRules.Add(new ContractRateRule
        {
            TenantId = DemoTenantId,
            RateCardId = GeneratorRateCardId,
            WorkOrderTypeName = "CorrectiveMaintenance",
            AssetCategoryName = "Generator",
            BillingModel = BillingModel.PreferentialRates,
            CalloutFee = 0m,
            LabourHourlyRate = 2500m,
            OvertimeHourlyRate = 4000m,
            WeekendHourlyRate = 4500m,
            EmergencyMultiplier = 1.5m,
            MaterialMarkupPercent = 10m,
            DiscountPercent = 20m,
            MinimumBillableHours = 2m,
            IncludedLabourHours = 0m,
            VatApplicable = true,
            Notes = "Apply preferential generator AMC pricing.",
            IsActive = true,
            CreatedAt = now,
        });

        dbContext.ContractPmEntitlements.AddRange(
            new ContractPmEntitlement
            {
                TenantId = DemoTenantId,
                ContractId = ContractGeneratorId,
                AssetId = AssetAId,
                WorkOrderType = "PreventiveMaintenance",
                VisitsPerPeriod = 12,
                PeriodType = PeriodType.Annual,
                IncludedVisits = 12,
                UsedVisits = 0,
                RemainingVisits = 12,
                EffectiveFrom = now.Date.AddMonths(-2),
                EffectiveTo = now.Date.AddMonths(10),
                IsActive = true,
                Notes = "Generator PM: 12 visits per year",
                CreatedAt = now,
            },
            new ContractPmEntitlement
            {
                TenantId = DemoTenantId,
                ContractId = ContractHvacId,
                AssetId = AssetBId,
                WorkOrderType = "PreventiveMaintenance",
                VisitsPerPeriod = 4,
                PeriodType = PeriodType.Annual,
                IncludedVisits = 4,
                UsedVisits = 0,
                RemainingVisits = 4,
                EffectiveFrom = now.Date.AddMonths(-1),
                EffectiveTo = now.Date.AddMonths(11),
                IsActive = true,
                Notes = "HVAC PM: 4 visits per year",
                CreatedAt = now,
            },
            new ContractPmEntitlement
            {
                TenantId = DemoTenantId,
                ContractId = ContractAdhocId,
                AssetCategory = "UPS",
                WorkOrderType = "PreventiveMaintenance",
                VisitsPerPeriod = 2,
                PeriodType = PeriodType.Annual,
                IncludedVisits = 2,
                UsedVisits = 0,
                RemainingVisits = 2,
                EffectiveFrom = now.Date.AddDays(-15),
                EffectiveTo = now.Date.AddMonths(6),
                IsActive = true,
                Notes = "UPS PM: 2 visits per year",
                CreatedAt = now,
            });

        dbContext.ContractLifecycleEvents.AddRange(
            new ContractLifecycleEvent { TenantId = DemoTenantId, ContractId = ContractGeneratorId, EventType = ContractLifecycleEventType.Created, NewStatus = ContractStatus.Active, Note = "Seeded generator contract", CreatedByUserId = ManagerUserId, CreatedAt = now },
            new ContractLifecycleEvent { TenantId = DemoTenantId, ContractId = ContractHvacId, EventType = ContractLifecycleEventType.Created, NewStatus = ContractStatus.Active, Note = "Seeded HVAC contract", CreatedByUserId = ManagerUserId, CreatedAt = now },
            new ContractLifecycleEvent { TenantId = DemoTenantId, ContractId = ContractAdhocId, EventType = ContractLifecycleEventType.Created, NewStatus = ContractStatus.Active, Note = "Seeded adhoc contract", CreatedByUserId = ManagerUserId, CreatedAt = now });

        // EC-019: work_order_technician_assignments is the source of truth for assignment; the
        // legacy work_orders."AssignedTechnicianId" column above is a derived mirror. Seeding the
        // column alone produced demo work orders that named a technician while the assignment model
        // reported "Unassigned" — which is exactly how the defect was found. Seed both, so the demo
        // tenant satisfies the same invariant the application maintains.
        foreach (var workOrderId in new[] { WorkOrder1Id, WorkOrder2Id, WorkOrder3Id })
        {
            dbContext.WorkOrderTechnicianAssignments.Add(new WorkOrderTechnicianAssignment
            {
                TenantId = DemoTenantId,
                WorkOrderId = workOrderId,
                TechnicianId = TechnicianId,
                IsLead = true,
                Status = "Pending",
                AssignedByUserId = ManagerUserId,
                AssignedAt = now,
                CreatedAt = now,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Demo seed data created successfully.");
        // EC-025: this block used to advertise "admin@ecosys.io / Admin@1234" as the platform admin.
        // No such account is ever created — the platform admin comes from PlatformAdmin:Email /
        // PlatformAdmin:Password via IPlatformBootstrapService. Only accounts this seeder actually
        // creates get their password printed here; the platform admin is named so developers know
        // which address to use, and its password stays where the operator configured it.
        var platformAdminEmail = platformAdminOptions.Value.Email;
        logger.LogInformation("=== ECOSYS DEV SEED CREDENTIALS (development only) ===");
        logger.LogInformation("Tenant Manager:  manager@demo.co.ke  / Manager@1234");
        logger.LogInformation("Technician:      tech@demo.co.ke     / Tech@1234");
        logger.LogInformation(
            "Platform Admin:  {PlatformAdminEmail} (not seeded here — password is PlatformAdmin:Password in configuration)",
            string.IsNullOrWhiteSpace(platformAdminEmail) ? "<PlatformAdmin:Email not configured>" : platformAdminEmail);
        logger.LogInformation("======================================================");
    }

    /// <summary>
    /// Older demo databases seeded the demo technician with the generic User role, whose permission
    /// template drops "complete work orders", and without a branch row, which hides every work order
    /// from a branch-scoped user — so the account could not use the technician app.
    /// </summary>
    private async Task UpgradeDemoTechnicianRoleAsync(CancellationToken cancellationToken)
    {
        var tech = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == TechUserId, cancellationToken);
        if (tech is null)
        {
            return;
        }

        var changed = false;
        if (tech.Role == AppRoles.User)
        {
            tech.Role = AppRoles.Technician;
            changed = true;
        }

        if (!tech.HasAllBranchAccess
            && !await dbContext.UserBranchAssignments.AnyAsync(x => x.UserId == TechUserId, cancellationToken)
            && await dbContext.Branches.AnyAsync(x => x.Id == DemoBranchId, cancellationToken))
        {
            dbContext.UserBranchAssignments.Add(new UserBranchAssignment
            {
                TenantId  = DemoTenantId,
                UserId    = TechUserId,
                BranchId  = DemoBranchId,
                IsDefault = true,
                CreatedAt = DateTime.UtcNow,
            });
            changed = true;
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Upgraded the demo technician account (Technician role and branch access).");
        }
    }
}
