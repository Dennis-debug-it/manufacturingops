using System.Reflection;
using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Domain.Enums;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ecosys.Tests;

public sealed class ContractManagementFlowTests
{
    [Fact]
    public async Task CreateContract_CreatesDefaultReminderRules_AndCanBeViewed()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var lifecycleService = new FakeContractLifecycleService(dbContext);

        var controller = new ContractsController(
            dbContext,
            new FakeTenantContext(fixture.TenantId, fixture.UserId),
            new AllowAllContractAccessService(),
            lifecycleService,
            new StubContractPricingService(),
            new AuditLogService(dbContext, new HttpContextAccessor()));

        var request = new UpsertContractRequest(
            "AMC-2026-0041",
            fixture.ClientId,
            "Generator AMC",
            ContractType.ComprehensiveMaintenance,
            ContractStatus.Active,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            12,
            BillingModel.PreferentialRates,
            "KES",
            1250000m,
            30,
            "Annual maintenance contract",
            "PM, inspection, emergency support",
            "Overhauls",
            "Demo contract",
            fixture.UserId,
            fixture.UserId,
            fixture.UserId,
            true,
            7,
            true,
            true,
            4m,
            24m,
            "BusinessHours",
            true,
            "renewals@acme.test",
            "High",
            true,
            [fixture.SiteId],
            [fixture.AssetId],
            [new ContractAssetCategoryRequest(null, "Generator")],
            [new ContractWorkOrderTypeRequest(null, WorkOrderTypes.PreventiveMaintenance)],
            [new UpsertContractPmEntitlementRequest(fixture.AssetId, null, WorkOrderTypes.PreventiveMaintenance, 12, PeriodType.Annual, 12, 0, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc), true, "Generator PM")]);

        var createResult = await controller.Create(request, CancellationToken.None);
        var created = Assert.IsType<CreatedAtActionResult>(createResult.Result);
        var payload = Assert.IsType<ContractDetailResponse>(created.Value);

        Assert.Equal("AMC-2026-0041", payload.ContractNumber);
        Assert.Equal(6, payload.ReminderRules.Count);
        Assert.Contains(payload.ReminderRules, item => item.DaysBeforeExpiry == 90);
        Assert.Contains(payload.Sites, item => item.SiteId == fixture.SiteId);
        Assert.Contains(payload.Assets, item => item.AssetId == fixture.AssetId);

        var getResult = await controller.Get(payload.Id, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(getResult.Result);
        var viewed = Assert.IsType<ContractDetailResponse>(ok.Value);
        Assert.Equal(payload.Id, viewed.Id);
    }

    [Fact]
    public async Task LifecycleService_TransitionsDraftToActive_AndCalculatesRiskBands()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var lifecycleService = new FakeContractLifecycleService(dbContext);

        var contract = new Contract
        {
            TenantId = fixture.TenantId,
            ClientId = fixture.ClientId,
            ContractNumber = "CNT-LIFE-001",
            Name = "Lifecycle test",
            ContractType = ContractType.SlaSupport,
            Status = ContractStatus.Draft,
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            BillingModel = BillingModel.Included,
            Currency = "KES",
            RenewalNoticeDays = 30
        };
        dbContext.Contracts.Add(contract);
        await dbContext.SaveChangesAsync();

        await lifecycleService.SubmitForApprovalAsync(fixture.TenantId, contract.Id, fixture.UserId, "Ready", CancellationToken.None);
        await lifecycleService.ApproveAsync(fixture.TenantId, contract.Id, fixture.UserId, "Approved", CancellationToken.None);
        await lifecycleService.ActivateAsync(fixture.TenantId, contract.Id, fixture.UserId, "Go live", CancellationToken.None);

        var refreshed = await dbContext.Contracts.SingleAsync(x => x.Id == contract.Id);
        Assert.Equal(ContractStatus.Active, refreshed.Status);
        Assert.NotNull(refreshed.ActivatedAt);
        Assert.Equal(RenewalRisk.Low, lifecycleService.CalculateRenewalRisk(DateTime.UtcNow.Date.AddDays(61), DateTime.UtcNow));
        Assert.Equal(RenewalRisk.Medium, lifecycleService.CalculateRenewalRisk(DateTime.UtcNow.Date.AddDays(45), DateTime.UtcNow));
        Assert.Equal(RenewalRisk.High, lifecycleService.CalculateRenewalRisk(DateTime.UtcNow.Date.AddDays(20), DateTime.UtcNow));
        Assert.Equal(RenewalRisk.Critical, lifecycleService.CalculateRenewalRisk(DateTime.UtcNow.Date.AddDays(5), DateTime.UtcNow));
        Assert.Equal(RenewalRisk.Expired, lifecycleService.CalculateRenewalRisk(DateTime.UtcNow.Date.AddDays(-1), DateTime.UtcNow));
    }

    [Fact]
    public async Task CoverageAndPricing_SelectCoveredContractAndPreferentialRateRule()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var coverageService = CreateCoverageService(dbContext);
        var pricingService = CreatePricingService(dbContext, coverageService);

        var contract = await SeedActiveContractAsync(dbContext, fixture, ContractStatus.Active, BillingModel.PreferentialRates, allowGraceRates: true, allowGraceSla: true);
        var card = new ContractRateCard
        {
            TenantId = fixture.TenantId,
            ContractId = contract.Id,
            Name = "Generator AMC Preferential Rates",
            Currency = "KES",
            IsDefault = true,
            EffectiveFrom = DateTime.UtcNow.Date.AddDays(-10),
            EffectiveTo = DateTime.UtcNow.Date.AddDays(100),
            IsActive = true
        };
        dbContext.ContractRateCards.Add(card);
        await dbContext.SaveChangesAsync();

        dbContext.ContractRateRules.Add(new ContractRateRule
        {
            TenantId = fixture.TenantId,
            RateCardId = card.Id,
            SiteId = fixture.SiteId,
            AssetCategoryName = "Generator",
            WorkOrderTypeName = WorkOrderTypes.CorrectiveMaintenance,
            BillingModel = BillingModel.PreferentialRates,
            LabourHourlyRate = 2500m,
            OvertimeHourlyRate = 4000m,
            WeekendHourlyRate = 4500m,
            EmergencyMultiplier = 1.5m,
            MaterialMarkupPercent = 10m,
            DiscountPercent = 20m,
            MinimumBillableHours = 2m,
            IsActive = true
        });
        await dbContext.SaveChangesAsync();

        var coverage = await coverageService.EvaluateAsync(
            fixture.TenantId,
            fixture.ClientId,
            fixture.SiteId,
            fixture.AssetId,
            WorkOrderTypes.CorrectiveMaintenance,
            WorkOrderTypes.CorrectiveMaintenance,
            DateTime.UtcNow,
            true,
            CancellationToken.None);

        var pricing = await pricingService.EvaluateAsync(
            fixture.TenantId,
            fixture.ClientId,
            fixture.SiteId,
            fixture.AssetId,
            WorkOrderTypes.CorrectiveMaintenance,
            "High",
            DateTime.UtcNow,
            2m,
            1000m,
            true,
            CancellationToken.None);

        Assert.Equal(CoverageStatus.Covered, coverage.CoverageStatus);
        Assert.Equal(contract.Id, coverage.ContractId);
        Assert.Equal(BillingModel.PreferentialRates, pricing.BillingModel);
        Assert.Equal("ContractRateRule", pricing.AppliedRateSource);
        Assert.Equal(2500m, pricing.LabourHourlyRate);
        Assert.Contains(pricing.Warnings, item => item.Contains("Preferential", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task OperationalService_UsesFallbackForExpiredOrSuspendedContracts_AndCountsPmEntitlementOnce()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var coverageService = CreateCoverageService(dbContext);
        var pricingService = CreatePricingService(dbContext, coverageService);
        var operationalService = CreateOperationalService(dbContext, coverageService, pricingService);

        var expiredContract = await SeedActiveContractAsync(dbContext, fixture, ContractStatus.Expired, BillingModel.PreferentialRates, allowGraceRates: false, allowGraceSla: false, endDate: DateTime.UtcNow.Date.AddDays(-10));

        var expiredWorkOrder = new WorkOrder
        {
            TenantId = fixture.TenantId,
            ClientId = fixture.ClientId,
            SiteId = fixture.SiteId,
            AssetId = fixture.AssetId,
            WorkOrderNumber = "WO-CON-EXPIRED",
            Title = "Expired contract test",
            Priority = "High",
            Status = WorkOrderStatuses.Open,
            WorkType = WorkOrderTypes.CorrectiveMaintenance,
            EstimatedDuration = 2m,
            EstimatedMaterialCost = 500m
        };
        dbContext.WorkOrders.Add(expiredWorkOrder);
        await dbContext.SaveChangesAsync();

        var expiredSnapshot = await operationalService.ApplyOperationalSnapshotAsync(expiredWorkOrder, CancellationToken.None);
        Assert.Equal(CoverageStatus.NotCovered, expiredSnapshot.Coverage.CoverageStatus);
        Assert.Equal(BillingModel.ChargeableCallout, expiredSnapshot.Pricing.BillingModel);

        var suspendedContract = await SeedActiveContractAsync(dbContext, fixture, ContractStatus.Suspended, BillingModel.Included, allowGraceRates: false, allowGraceSla: false);
        var suspendedWorkOrder = new WorkOrder
        {
            TenantId = fixture.TenantId,
            ClientId = fixture.ClientId,
            SiteId = fixture.SiteId,
            AssetId = fixture.AssetId,
            WorkOrderNumber = "WO-CON-SUSPENDED",
            Title = "Suspended contract test",
            Priority = "Medium",
            Status = WorkOrderStatuses.Open,
            WorkType = WorkOrderTypes.CorrectiveMaintenance
        };
        dbContext.WorkOrders.Add(suspendedWorkOrder);
        await dbContext.SaveChangesAsync();

        var suspendedSnapshot = await operationalService.ApplyOperationalSnapshotAsync(suspendedWorkOrder, CancellationToken.None);
        Assert.Equal(CoverageStatus.NotCovered, suspendedSnapshot.Coverage.CoverageStatus);

        dbContext.Contracts.Remove(expiredContract);
        dbContext.Contracts.Remove(suspendedContract);
        await dbContext.SaveChangesAsync();

        var pmContract = await SeedActiveContractAsync(dbContext, fixture, ContractStatus.Active, BillingModel.Included, allowGraceRates: true, allowGraceSla: true);
        var entitlement = new ContractPmEntitlement
        {
            TenantId = fixture.TenantId,
            ContractId = pmContract.Id,
            AssetId = fixture.AssetId,
            WorkOrderType = WorkOrderTypes.PreventiveMaintenance,
            VisitsPerPeriod = 12,
            PeriodType = PeriodType.Annual,
            IncludedVisits = 12,
            UsedVisits = 0,
            RemainingVisits = 12,
            EffectiveFrom = DateTime.UtcNow.Date.AddDays(-1),
            EffectiveTo = DateTime.UtcNow.Date.AddDays(30),
            IsActive = true
        };
        dbContext.ContractPmEntitlements.Add(entitlement);

        var pmWorkOrder = new WorkOrder
        {
            TenantId = fixture.TenantId,
            ClientId = fixture.ClientId,
            SiteId = fixture.SiteId,
            AssetId = fixture.AssetId,
            WorkOrderNumber = "WO-CON-PM",
            Title = "PM visit",
            Priority = "Low",
            Status = WorkOrderStatuses.Completed,
            WorkType = WorkOrderTypes.PreventiveMaintenance,
            IsPreventiveMaintenance = true,
            ContractId = pmContract.Id,
            PricingSnapshotJson = "{}"
        };
        dbContext.WorkOrders.Add(pmWorkOrder);
        await dbContext.SaveChangesAsync();

        await operationalService.IncrementPmEntitlementUsageIfNeededAsync(fixture.TenantId, pmWorkOrder.Id, CancellationToken.None);
        await operationalService.IncrementPmEntitlementUsageIfNeededAsync(fixture.TenantId, pmWorkOrder.Id, CancellationToken.None);

        var refreshedEntitlement = await dbContext.ContractPmEntitlements.SingleAsync(x => x.Id == entitlement.Id);
        Assert.Equal(1, refreshedEntitlement.UsedVisits);
        Assert.Equal(11, refreshedEntitlement.RemainingVisits);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-contract-tests-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static IContractCoverageService CreateCoverageService(AppDbContext dbContext)
    {
        var type = typeof(AppDbContext).Assembly.GetType("Ecosys.Infrastructure.Services.ContractCoverageService", throwOnError: true)!;
        return (IContractCoverageService)Activator.CreateInstance(type, dbContext)!;
    }

    private static IContractPricingService CreatePricingService(AppDbContext dbContext, IContractCoverageService coverageService)
    {
        var type = typeof(AppDbContext).Assembly.GetType("Ecosys.Infrastructure.Services.ContractPricingService", throwOnError: true)!;
        return (IContractPricingService)Activator.CreateInstance(type, dbContext, coverageService)!;
    }

    private static IContractOperationalService CreateOperationalService(AppDbContext dbContext, IContractCoverageService coverageService, IContractPricingService pricingService)
    {
        var type = typeof(AppDbContext).Assembly.GetType("Ecosys.Infrastructure.Services.ContractOperationalService", throwOnError: true)!;
        return (IContractOperationalService)Activator.CreateInstance(type, dbContext, coverageService, pricingService, new NoOpSlaService())!;
    }

    private static async Task<FixtureData> SeedFixtureAsync(AppDbContext dbContext)
    {
        var tenant = new Tenant
        {
            Name = "Acme",
            Slug = $"acme-{Guid.NewGuid():N}".Substring(0, 12),
            CompanyName = "Acme Facilities",
            Email = "ops@acme.test",
            Country = "Kenya",
            Status = "Active",
            LicenseStatus = "Active",
            PrimaryColor = "#0F4C81",
            SecondaryColor = "#F4B942",
            IsActive = true
        };

        var user = new User
        {
            TenantId = tenant.Id,
            FullName = "Contract Manager",
            Email = $"contracts-{Guid.NewGuid():N}@acme.test",
            PasswordHash = "hash",
            Role = AppRoles.Admin,
            JobTitle = "Operations Manager",
            IsActive = true,
            HasAllBranchAccess = true
        };

        var client = new Client
        {
            TenantId = tenant.Id,
            ClientName = "Acme Facilities",
            Email = "client@acme.test",
            IsActive = true
        };

        var site = new Site
        {
            TenantId = tenant.Id,
            ClientId = client.Id,
            SiteCode = "ACME-HQ",
            SiteName = "Acme HQ",
            SiteType = "Main",
            Status = "Active",
            Country = "Kenya"
        };

        var asset = new Asset
        {
            TenantId = tenant.Id,
            ClientId = client.Id,
            SiteId = site.Id,
            AssetName = "Generator 1",
            AssetCode = "GEN-01",
            AssetType = "Generator",
            Status = "Active"
        };

        dbContext.AddRange(tenant, user, client, site, asset);
        await dbContext.SaveChangesAsync();
        return new FixtureData(tenant.Id, user.Id, client.Id, site.Id, asset.Id);
    }

    private static async Task<Contract> SeedActiveContractAsync(
        AppDbContext dbContext,
        FixtureData fixture,
        ContractStatus status,
        BillingModel billingModel,
        bool allowGraceRates,
        bool allowGraceSla,
        DateTime? endDate = null)
    {
        var contract = new Contract
        {
            TenantId = fixture.TenantId,
            ClientId = fixture.ClientId,
            ContractNumber = $"CNT-{Guid.NewGuid():N}".Substring(0, 12),
            Name = "Test contract",
            ContractType = ContractType.ComprehensiveMaintenance,
            Status = status,
            StartDate = DateTime.UtcNow.Date.AddDays(-30),
            EndDate = endDate ?? DateTime.UtcNow.Date.AddDays(60),
            BillingModel = billingModel,
            Currency = "KES",
            RenewalNoticeDays = 30,
            AllowPreferentialRatesDuringGracePeriod = allowGraceRates,
            AllowSlaDuringGracePeriod = allowGraceSla,
            ResponseTimeHours = 4m,
            ResolutionTimeHours = 24m,
            ActivatedAt = DateTime.UtcNow.AddDays(-10),
            RenewalRisk = RenewalRisk.Low
        };

        dbContext.Contracts.Add(contract);
        await dbContext.SaveChangesAsync();

        dbContext.ContractSites.Add(new ContractSite { TenantId = fixture.TenantId, ContractId = contract.Id, SiteId = fixture.SiteId });
        dbContext.ContractAssets.Add(new ContractAsset { TenantId = fixture.TenantId, ContractId = contract.Id, AssetId = fixture.AssetId });
        dbContext.ContractAssetCategories.Add(new ContractAssetCategory { TenantId = fixture.TenantId, ContractId = contract.Id, AssetCategoryName = "Generator" });
        dbContext.ContractWorkOrderTypes.Add(new ContractWorkOrderType { TenantId = fixture.TenantId, ContractId = contract.Id, WorkOrderTypeName = WorkOrderTypes.CorrectiveMaintenance });
        await dbContext.SaveChangesAsync();

        return contract;
    }

    private sealed record FixtureData(Guid TenantId, Guid UserId, Guid ClientId, Guid SiteId, Guid AssetId);

    private sealed class FakeTenantContext(Guid tenantId, Guid userId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
        public Guid? SessionId => Guid.NewGuid();
        public string? Email => "contracts@acme.test";
        public string? Role => AppRoles.Admin;
        public string? JobTitle => "Operations Manager";
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public bool IsAdmin => true;
        public bool HasRole(string role) => string.Equals(role, Role, StringComparison.OrdinalIgnoreCase);
        public bool HasPermission(string permissionName) => true;
        public Guid GetRequiredTenantId() => tenantId;
        public Guid GetRequiredUserId() => userId;
        public Guid GetRequiredSessionId() => SessionId!.Value;
    }

    private sealed class AllowAllContractAccessService : IContractAccessService
    {
        public bool CanManageContracts() => true;
        public bool CanManageLifecycle() => true;
        public bool CanManageRates() => true;
        public bool CanManageRenewals() => true;
        public bool CanViewContracts() => true;
        public bool CanViewPricing() => true;
        public void EnsureCanManageContracts() { }
        public void EnsureCanManageLifecycle() { }
        public void EnsureCanManageRates() { }
        public void EnsureCanManageRenewals() { }
        public void EnsureCanViewContracts() { }
        public void EnsureCanViewPricing() { }
    }

    private sealed class StubContractPricingService : IContractPricingService
    {
        public Task<ContractPricingEvaluation> EvaluateAsync(Guid tenantId, Guid clientId, Guid? siteId, Guid? assetId, string? workOrderType, string? priority, DateTime? asOfUtc = null, decimal? estimatedLabourHours = null, decimal? estimatedMaterialCost = null, bool allowGracePeriod = true, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ContractPricingEvaluation(null, null, null, null, BillingModel.ChargeableCallout, CoverageStatus.NotCovered, "StandardChargeableFallback", 0m, 0m, 0m, 0m, 0m, 0m, 1m, 0m, 0m, 0m, 0m, 0m, true, 0m, []));
    }

    private sealed class FakeContractLifecycleService(AppDbContext dbContext) : IContractLifecycleService
    {
        public int CalculateDaysToExpiry(DateTime endDateUtc, DateTime? asOfUtc = null)
        {
            var asOf = (asOfUtc ?? DateTime.UtcNow).Date;
            return (endDateUtc.Date - asOf).Days;
        }

        public RenewalRisk CalculateRenewalRisk(DateTime endDateUtc, DateTime? asOfUtc = null)
        {
            var days = CalculateDaysToExpiry(endDateUtc, asOfUtc);
            return days switch
            {
                < 0 => RenewalRisk.Expired,
                <= 7 => RenewalRisk.Critical,
                <= 30 => RenewalRisk.High,
                <= 60 => RenewalRisk.Medium,
                _ => RenewalRisk.Low
            };
        }

        public async Task<Contract> SubmitForApprovalAsync(Guid tenantId, Guid contractId, Guid? actorUserId, string? note, CancellationToken cancellationToken = default)
        {
            var contract = await LoadAsync(tenantId, contractId, cancellationToken);
            contract.Status = ContractStatus.PendingApproval;
            contract.SubmittedForApprovalAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return contract;
        }

        public async Task<Contract> ApproveAsync(Guid tenantId, Guid contractId, Guid? actorUserId, string? note, CancellationToken cancellationToken = default)
        {
            var contract = await LoadAsync(tenantId, contractId, cancellationToken);
            contract.ApprovedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return contract;
        }

        public async Task<Contract> ActivateAsync(Guid tenantId, Guid contractId, Guid? actorUserId, string? note, CancellationToken cancellationToken = default)
        {
            var contract = await LoadAsync(tenantId, contractId, cancellationToken);
            contract.Status = ContractStatus.Active;
            contract.ActivatedAt = DateTime.UtcNow;
            contract.RenewalRisk = CalculateRenewalRisk(contract.EndDate);
            await dbContext.SaveChangesAsync(cancellationToken);
            return contract;
        }

        public Task<Contract> StartRenewalAsync(Guid tenantId, Guid contractId, Guid? actorUserId, string? note, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Contract> SuspendAsync(Guid tenantId, Guid contractId, Guid? actorUserId, string? reason, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Contract> ResumeAsync(Guid tenantId, Guid contractId, Guid? actorUserId, string? note, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Contract> TerminateAsync(Guid tenantId, Guid contractId, Guid? actorUserId, DateTime? terminationDate, string? reason, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Contract> CancelAsync(Guid tenantId, Guid contractId, Guid? actorUserId, string? reason, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Contract> RecalculateLifecycleAsync(Guid tenantId, Guid contractId, Guid? actorUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<(Contract OldContract, Contract NewContract)> RenewAsync(Guid tenantId, Guid contractId, Guid? actorUserId, ContractRenewalOptions options, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<ContractLifecycleEvent>> GetLifecycleAsync(Guid tenantId, Guid contractId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<ContractLifecycleEvent>>([]);

        private async Task<Contract> LoadAsync(Guid tenantId, Guid contractId, CancellationToken cancellationToken) =>
            await dbContext.Contracts.SingleAsync(x => x.TenantId == tenantId && x.Id == contractId, cancellationToken);
    }

    private sealed class NoOpSlaService : ISlaService
    {
        public Task ApplyDefinitionAsync(WorkOrder workOrder, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> RefreshWorkOrderAsync(Guid workOrderId, DateTime utcNow, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
