using System.Text.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ecosys.Domain.Entities;
using Ecosys.Domain.Enums;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services;

public sealed record ContractCoverageEvaluation(
    Guid? ContractId,
    string? ContractNumber,
    string? ContractName,
    ContractStatus? ContractStatus,
    RenewalRisk? RenewalRisk,
    CoverageStatus CoverageStatus,
    bool SiteCovered,
    bool AssetCovered,
    bool AssetCategoryCovered,
    bool WorkOrderTypeCovered,
    bool ExcludedServiceMatched,
    BillingModel? BillingModel,
    decimal? ResponseTimeHours,
    decimal? ResolutionTimeHours,
    string? WorkingHoursPolicy,
    bool PauseSlaOnHold,
    bool GracePeriodApplied,
    IReadOnlyCollection<string> WarningMessages);

public sealed record ContractPricingEvaluation(
    Guid? ContractId,
    Guid? ContractRateCardId,
    string? ContractRateCardName,
    Guid? ContractRateRuleId,
    BillingModel BillingModel,
    CoverageStatus CoverageStatus,
    string AppliedRateSource,
    decimal CalloutFee,
    decimal FixedVisitFee,
    decimal LabourHourlyRate,
    decimal OvertimeHourlyRate,
    decimal WeekendHourlyRate,
    decimal PublicHolidayHourlyRate,
    decimal EmergencyMultiplier,
    decimal TravelCharge,
    decimal MaterialMarkupPercent,
    decimal DiscountPercent,
    decimal MinimumBillableHours,
    decimal IncludedLabourHours,
    bool VatApplicable,
    decimal EstimatedCharge,
    IReadOnlyCollection<string> Warnings);

public sealed record ContractOperationalSnapshot(
    ContractCoverageEvaluation Coverage,
    ContractPricingEvaluation Pricing,
    bool AppliedContractSla,
    decimal? AppliedResponseHours,
    decimal? AppliedResolutionHours);

public sealed record ContractRenewalOptions(
    DateTime? StartDate,
    DateTime? EndDate,
    int? DurationMonths,
    bool ActivateNewContract,
    bool SubmitForApproval,
    string? Name,
    decimal? ContractValue,
    string? Notes);

public interface IContractAccessService
{
    bool CanViewContracts();
    bool CanManageContracts();
    bool CanManageLifecycle();
    bool CanManageRates();
    bool CanViewPricing();
    bool CanManageRenewals();
    void EnsureCanViewContracts();
    void EnsureCanManageContracts();
    void EnsureCanManageLifecycle();
    void EnsureCanManageRates();
    void EnsureCanViewPricing();
    void EnsureCanManageRenewals();
}

public interface IContractCoverageService
{
    Task<ContractCoverageEvaluation> EvaluateAsync(
        Guid tenantId,
        Guid clientId,
        Guid? siteId,
        Guid? assetId,
        string? workOrderType,
        string? serviceType,
        DateTime? asOfUtc = null,
        bool allowGracePeriod = true,
        CancellationToken cancellationToken = default);
}

public interface IContractPricingService
{
    Task<ContractPricingEvaluation> EvaluateAsync(
        Guid tenantId,
        Guid clientId,
        Guid? siteId,
        Guid? assetId,
        string? workOrderType,
        string? priority,
        DateTime? asOfUtc = null,
        decimal? estimatedLabourHours = null,
        decimal? estimatedMaterialCost = null,
        bool allowGracePeriod = true,
        CancellationToken cancellationToken = default);
}

public interface IContractLifecycleService
{
    Task<Contract> SubmitForApprovalAsync(Guid tenantId, Guid contractId, Guid? actorUserId, string? note, CancellationToken cancellationToken = default);
    Task<Contract> ApproveAsync(Guid tenantId, Guid contractId, Guid? actorUserId, string? note, CancellationToken cancellationToken = default);
    Task<Contract> ActivateAsync(Guid tenantId, Guid contractId, Guid? actorUserId, string? note, CancellationToken cancellationToken = default);
    Task<Contract> StartRenewalAsync(Guid tenantId, Guid contractId, Guid? actorUserId, string? note, CancellationToken cancellationToken = default);
    Task<Contract> SuspendAsync(Guid tenantId, Guid contractId, Guid? actorUserId, string? reason, CancellationToken cancellationToken = default);
    Task<Contract> ResumeAsync(Guid tenantId, Guid contractId, Guid? actorUserId, string? note, CancellationToken cancellationToken = default);
    Task<Contract> TerminateAsync(Guid tenantId, Guid contractId, Guid? actorUserId, DateTime? terminationDate, string? reason, CancellationToken cancellationToken = default);
    Task<Contract> CancelAsync(Guid tenantId, Guid contractId, Guid? actorUserId, string? reason, CancellationToken cancellationToken = default);
    Task<Contract> RecalculateLifecycleAsync(Guid tenantId, Guid contractId, Guid? actorUserId, CancellationToken cancellationToken = default);
    Task<(Contract OldContract, Contract NewContract)> RenewAsync(Guid tenantId, Guid contractId, Guid? actorUserId, ContractRenewalOptions options, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ContractLifecycleEvent>> GetLifecycleAsync(Guid tenantId, Guid contractId, CancellationToken cancellationToken = default);
    RenewalRisk CalculateRenewalRisk(DateTime endDateUtc, DateTime? asOfUtc = null);
    int CalculateDaysToExpiry(DateTime endDateUtc, DateTime? asOfUtc = null);
}

public interface IContractOperationalService
{
    Task<ContractOperationalSnapshot> ApplyOperationalSnapshotAsync(WorkOrder workOrder, CancellationToken cancellationToken = default);
    Task<ContractOperationalSnapshot> RecalculateWorkOrderAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default);
    Task IncrementPmEntitlementUsageIfNeededAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default);
}

internal sealed class ContractAccessService(ITenantContext tenantContext) : IContractAccessService
{
    public bool CanViewContracts() =>
        !tenantContext.IsSuperAdmin;

    public bool CanManageContracts() =>
        !tenantContext.IsSuperAdmin
        && (tenantContext.IsAdmin || tenantContext.HasPermission(PermissionNames.CanManageSettings));

    public bool CanManageLifecycle() =>
        CanManageContracts() || IsSupervisorLike();

    public bool CanManageRates() =>
        !tenantContext.IsSuperAdmin
        && (tenantContext.IsAdmin
            || tenantContext.HasPermission(PermissionNames.FinanceManageRates)
            || tenantContext.HasPermission(PermissionNames.FinanceManageBillableWork)
            || IsFinanceLike());

    public bool CanViewPricing() =>
        !tenantContext.IsSuperAdmin
        && (tenantContext.IsAdmin
            || tenantContext.HasPermission(PermissionNames.FinanceViewRates)
            || tenantContext.HasPermission(PermissionNames.FinanceViewBillableWork)
            || tenantContext.HasPermission(PermissionNames.FinanceView)
            || tenantContext.HasPermission(PermissionNames.FinanceManageRates)
            || IsFinanceLike());

    public bool CanManageRenewals() =>
        CanManageContracts() || IsSupervisorLike() || IsFinanceLike();

    public void EnsureCanViewContracts()
    {
        if (!CanViewContracts())
        {
            throw new ForbiddenException("You do not have access to contracts.");
        }
    }

    public void EnsureCanManageContracts()
    {
        if (!CanManageContracts())
        {
            throw new ForbiddenException("You do not have permission to manage contracts.");
        }
    }

    public void EnsureCanManageLifecycle()
    {
        if (!CanManageLifecycle())
        {
            throw new ForbiddenException("You do not have permission to manage contract lifecycle.");
        }
    }

    public void EnsureCanManageRates()
    {
        if (!CanManageRates())
        {
            throw new ForbiddenException("You do not have permission to manage contract rates.");
        }
    }

    public void EnsureCanViewPricing()
    {
        if (!CanViewPricing())
        {
            throw new ForbiddenException("You do not have access to contract pricing.");
        }
    }

    public void EnsureCanManageRenewals()
    {
        if (!CanManageRenewals())
        {
            throw new ForbiddenException("You do not have permission to manage contract renewals.");
        }
    }

    private bool IsSupervisorLike()
    {
        var jobTitle = tenantContext.JobTitle?.Trim() ?? string.Empty;
        return jobTitle.Contains("supervisor", StringComparison.OrdinalIgnoreCase)
            || jobTitle.Contains("dispatcher", StringComparison.OrdinalIgnoreCase)
            || jobTitle.Contains("manager", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsFinanceLike()
    {
        var jobTitle = tenantContext.JobTitle?.Trim() ?? string.Empty;
        return jobTitle.Contains("finance", StringComparison.OrdinalIgnoreCase)
            || jobTitle.Contains("account", StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class ContractCoverageService(AppDbContext dbContext) : IContractCoverageService
{
    public async Task<ContractCoverageEvaluation> EvaluateAsync(
        Guid tenantId,
        Guid clientId,
        Guid? siteId,
        Guid? assetId,
        string? workOrderType,
        string? serviceType,
        DateTime? asOfUtc = null,
        bool allowGracePeriod = true,
        CancellationToken cancellationToken = default)
    {
        var asOf = NormalizeDateOnly(asOfUtc ?? DateTime.UtcNow);
        var assetCategory = await ResolveAssetCategoryAsync(tenantId, assetId, cancellationToken);

        var candidates = await dbContext.Contracts
            .Include(x => x.Sites)
            .Include(x => x.Assets)
            .Include(x => x.AssetCategories)
            .Include(x => x.WorkOrderTypes)
            .Where(x => x.TenantId == tenantId && x.ClientId == clientId && !x.IsArchived)
            .OrderBy(x => x.EndDate)
            .ToListAsync(cancellationToken);

        Contract? bestContract = null;
        CoverageStatus bestStatus = CoverageStatus.NotCovered;
        var bestWarnings = new List<string>();
        var bestFlags = CoverageFlags.None;
        var bestGraceApplied = false;

        foreach (var contract in candidates)
        {
            var warnings = new List<string>();
            var flags = EvaluateCoverageFlags(contract, siteId, assetId, assetCategory, workOrderType, serviceType, asOf, allowGracePeriod, warnings, out var graceApplied);
            if (!flags.IsOperationallyEligible)
            {
                continue;
            }

            var status = flags.FullyMatched
                ? (flags.ExcludedServiceMatched ? CoverageStatus.PartiallyCovered : CoverageStatus.Covered)
                : flags.AnyCoverageMatched
                    ? CoverageStatus.PartiallyCovered
                    : CoverageStatus.NotCovered;

            if (status == CoverageStatus.NotCovered)
            {
                continue;
            }

            if (bestContract is null || CompareContractMatch(flags, status, contract, bestFlags, bestStatus, bestContract) > 0)
            {
                bestContract = contract;
                bestStatus = status;
                bestWarnings = warnings;
                bestFlags = flags;
                bestGraceApplied = graceApplied;
            }
        }

        if (bestContract is null)
        {
            return new ContractCoverageEvaluation(
                null,
                null,
                null,
                null,
                null,
                CoverageStatus.NotCovered,
                false,
                false,
                false,
                false,
                false,
                null,
                null,
                null,
                null,
                false,
                false,
                ["No active contract coverage matched this work order context."]);
        }

        return new ContractCoverageEvaluation(
            bestContract.Id,
            bestContract.ContractNumber,
            bestContract.Name,
            bestContract.Status,
            bestContract.RenewalRisk,
            bestStatus,
            bestFlags.SiteMatched,
            bestFlags.AssetMatched,
            bestFlags.AssetCategoryMatched,
            bestFlags.WorkOrderTypeMatched,
            bestFlags.ExcludedServiceMatched,
            bestContract.BillingModel,
            ShouldApplyContractSla(bestContract, bestGraceApplied) ? bestContract.ResponseTimeHours : null,
            ShouldApplyContractSla(bestContract, bestGraceApplied) ? bestContract.ResolutionTimeHours : null,
            ShouldApplyContractSla(bestContract, bestGraceApplied) ? bestContract.WorkingHoursPolicy : null,
            bestContract.PauseSlaOnHold,
            bestGraceApplied,
            bestWarnings);
    }

    private async Task<string?> ResolveAssetCategoryAsync(Guid tenantId, Guid? assetId, CancellationToken cancellationToken)
    {
        if (!assetId.HasValue)
        {
            return null;
        }

        return await dbContext.Assets
            .Where(x => x.TenantId == tenantId && x.Id == assetId.Value)
            .Select(x => x.AssetCategory != null ? x.AssetCategory.Name : x.AssetType)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private CoverageFlags EvaluateCoverageFlags(
        Contract contract,
        Guid? siteId,
        Guid? assetId,
        string? assetCategory,
        string? workOrderType,
        string? serviceType,
        DateTime asOf,
        bool allowGracePeriod,
        List<string> warnings,
        out bool graceApplied)
    {
        graceApplied = false;

        var withinBaseTerm = contract.StartDate.Date <= asOf && contract.EndDate.Date >= asOf;
        var withinGrace = allowGracePeriod
            && contract.EndDate.Date < asOf
            && contract.GracePeriodDays > 0
            && contract.EndDate.Date.AddDays(contract.GracePeriodDays) >= asOf;

        if (!withinBaseTerm && !withinGrace)
        {
            return CoverageFlags.None;
        }

        if (withinGrace)
        {
            graceApplied = true;
            warnings.Add($"Contract {contract.ContractNumber} is operating in grace period.");
        }

        var lifecycleEligible = contract.Status is ContractStatus.Active or ContractStatus.ExpiringSoon or ContractStatus.RenewalInProgress
            || (withinGrace && contract.Status == ContractStatus.Expired);

        if (!lifecycleEligible)
        {
            return CoverageFlags.None;
        }

        var siteMatched = siteId.HasValue && contract.Sites.Any(x => x.SiteId == siteId.Value);

        var assetMatched = assetId.HasValue && contract.Assets.Any(x => x.AssetId == assetId.Value);
        var categoryMatched = MatchesAssetCategory(contract, assetCategory);
        var coverageMatched = MatchesCoverageScope(contract, siteMatched, assetMatched, categoryMatched, warnings);

        var workTypeMatched = MatchesWorkOrderType(contract, workOrderType);
        if (!workTypeMatched && contract.WorkOrderTypes.Count > 0)
        {
            warnings.Add("Work order type is outside contract coverage.");
        }

        var excluded = MatchesServiceList(contract.ExcludedServices, serviceType ?? workOrderType);
        if (excluded)
        {
            warnings.Add("Excluded service rules override normal contract coverage.");
        }

        var includedMatched = string.IsNullOrWhiteSpace(contract.IncludedServices) || MatchesServiceList(contract.IncludedServices, serviceType ?? workOrderType);
        if (!includedMatched && !string.IsNullOrWhiteSpace(contract.IncludedServices))
        {
            warnings.Add("Requested service is not explicitly listed in included services.");
        }

        return new CoverageFlags(
            lifecycleEligible,
            siteMatched,
            assetMatched,
            categoryMatched,
            workTypeMatched,
            excluded,
            coverageMatched && workTypeMatched && !excluded && includedMatched,
            coverageMatched && workTypeMatched);
    }

    private static bool MatchesCoverageScope(
        Contract contract,
        bool siteMatched,
        bool assetMatched,
        bool categoryMatched,
        List<string> warnings)
    {
        if (contract.Assets.Count > 0)
        {
            if (!assetMatched)
            {
                warnings.Add("Selected asset is outside contract coverage.");
            }

            return assetMatched;
        }

        if (contract.AssetCategories.Count > 0)
        {
            if (!categoryMatched)
            {
                warnings.Add("Selected asset category is outside contract coverage.");
            }

            return categoryMatched;
        }

        if (contract.Sites.Count > 0)
        {
            if (!siteMatched)
            {
                warnings.Add("Selected site is outside contract coverage.");
            }

            return siteMatched;
        }

        return true;
    }

    private static bool MatchesAssetCategory(Contract contract, string? assetCategory)
    {
        if (contract.AssetCategories.Count == 0)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(assetCategory))
        {
            return false;
        }

        return contract.AssetCategories.Any(x =>
            string.Equals(x.AssetCategory?.Name, assetCategory, StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.AssetCategoryName, assetCategory, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesWorkOrderType(Contract contract, string? workOrderType)
    {
        if (contract.WorkOrderTypes.Count == 0)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(workOrderType))
        {
            return false;
        }

        return contract.WorkOrderTypes.Any(x =>
            string.Equals(x.WorkOrderTypeName, workOrderType, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesServiceList(string? configured, string? requested)
    {
        if (string.IsNullOrWhiteSpace(configured) || string.IsNullOrWhiteSpace(requested))
        {
            return false;
        }

        var requestedValue = requested.Trim();
        return configured
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(value => string.Equals(value, requestedValue, StringComparison.OrdinalIgnoreCase));
    }

    private static int CompareContractMatch(
        CoverageFlags candidateFlags,
        CoverageStatus candidateStatus,
        Contract candidate,
        CoverageFlags currentFlags,
        CoverageStatus currentStatus,
        Contract current)
    {
        var candidateScore = GetMatchScore(candidateFlags, candidateStatus, candidate);
        var currentScore = GetMatchScore(currentFlags, currentStatus, current);
        return candidateScore.CompareTo(currentScore);
    }

    private static int GetMatchScore(CoverageFlags flags, CoverageStatus status, Contract contract)
    {
        var score = status switch
        {
            CoverageStatus.Covered => 500,
            CoverageStatus.PartiallyCovered => 200,
            _ => 0
        };

        if (flags.AssetMatched) score += 80;
        if (flags.AssetCategoryMatched) score += 40;
        if (flags.SiteMatched) score += 20;
        if (flags.WorkOrderTypeMatched) score += 10;

        score += (int)(contract.EndDate.Date - contract.StartDate.Date).TotalDays / 30;
        return score;
    }

    private static bool ShouldApplyContractSla(Contract contract, bool graceApplied)
    {
        if (graceApplied)
        {
            return IsGraceSlaEnabled(contract);
        }

        return contract.Status is ContractStatus.Active or ContractStatus.ExpiringSoon or ContractStatus.RenewalInProgress;
    }

    private static bool IsGraceSlaEnabled(Contract contract) =>
        string.Equals(contract.GracePeriodPolicy, "Sla", StringComparison.OrdinalIgnoreCase)
        || string.Equals(contract.GracePeriodPolicy, "Both", StringComparison.OrdinalIgnoreCase)
        || contract.AllowSlaDuringGracePeriod
        || contract.GracePeriodAllowsSla;

    private static DateTime NormalizeDateOnly(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value.Date
            : DateTime.SpecifyKind(value, DateTimeKind.Utc).Date;

    private sealed record CoverageFlags(
        bool IsOperationallyEligible,
        bool SiteMatched,
        bool AssetMatched,
        bool AssetCategoryMatched,
        bool WorkOrderTypeMatched,
        bool ExcludedServiceMatched,
        bool FullyMatched,
        bool AnyCoverageMatched)
    {
        public static CoverageFlags None { get; } = new(false, false, false, false, false, false, false, false);
    }
}

internal sealed class ContractPricingService(
    AppDbContext dbContext,
    IContractCoverageService coverageService) : IContractPricingService
{
    public async Task<ContractPricingEvaluation> EvaluateAsync(
        Guid tenantId,
        Guid clientId,
        Guid? siteId,
        Guid? assetId,
        string? workOrderType,
        string? priority,
        DateTime? asOfUtc = null,
        decimal? estimatedLabourHours = null,
        decimal? estimatedMaterialCost = null,
        bool allowGracePeriod = true,
        CancellationToken cancellationToken = default)
    {
        var asOf = asOfUtc ?? DateTime.UtcNow;
        var coverage = await coverageService.EvaluateAsync(
            tenantId,
            clientId,
            siteId,
            assetId,
            workOrderType,
            workOrderType,
            asOf,
            allowGracePeriod,
            cancellationToken);

        var warnings = new List<string>(coverage.WarningMessages);
        var materialCost = estimatedMaterialCost ?? 0m;
        var requestedHours = Math.Max(estimatedLabourHours ?? 1m, 0m);

        ContractRateCard? contractRateCard = null;
        ContractRateRule? contractRule = null;
        if (coverage.ContractId.HasValue
            && coverage.CoverageStatus != CoverageStatus.NotCovered
            && coverage.BillingModel.HasValue)
        {
            var assetCategory = await dbContext.Assets
                .Where(x => x.TenantId == tenantId && x.Id == assetId)
                .Select(x => x.AssetCategory != null ? x.AssetCategory.Name : x.AssetType)
                .SingleOrDefaultAsync(cancellationToken);

            var activeCards = await dbContext.ContractRateCards
                .Include(x => x.Rules)
                .Where(x => x.TenantId == tenantId
                    && x.ContractId == coverage.ContractId.Value
                    && x.IsActive
                    && (!x.EffectiveFrom.HasValue || x.EffectiveFrom.Value <= asOf)
                    && (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= asOf))
                .OrderBy(x => x.IsDefault ? 0 : 1)
                .ThenBy(x => x.Name)
                .ToListAsync(cancellationToken);

            contractRule = SelectBestRule(activeCards, siteId, assetCategory, workOrderType, priority);
            contractRateCard = contractRule is null
                ? activeCards.FirstOrDefault(x => x.IsDefault) ?? activeCards.FirstOrDefault()
                : activeCards.First(x => x.Id == contractRule.RateCardId);
        }

        var defaultLabourCard = await dbContext.RateCards
            .Where(x => x.TenantId == tenantId && x.IsActive && x.Category == "Labour" && x.TimeType == "Standard")
            .OrderBy(x => x.IsDefault ? 0 : 1)
            .ThenBy(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken);
        var defaultCalloutCard = await dbContext.RateCards
            .Where(x => x.TenantId == tenantId && x.IsActive && x.Category == "Callout")
            .OrderBy(x => x.IsDefault ? 0 : 1)
            .ThenBy(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken);

        var billingModel = contractRule?.BillingModel
            ?? coverage.BillingModel
            ?? BillingModel.ChargeableCallout;

        var calloutFee = contractRule?.CalloutFee ?? (billingModel == BillingModel.ChargeableCallout ? defaultCalloutCard?.UnitRate ?? 0m : 0m);
        var fixedVisitFee = contractRule?.FixedVisitFee ?? 0m;
        var labourRate = contractRule?.LabourHourlyRate ?? defaultLabourCard?.UnitRate ?? 0m;
        var overtimeRate = contractRule?.OvertimeHourlyRate ?? labourRate;
        var weekendRate = contractRule?.WeekendHourlyRate ?? labourRate;
        var holidayRate = contractRule?.PublicHolidayHourlyRate ?? labourRate;
        var emergencyMultiplier = contractRule?.EmergencyMultiplier ?? 1m;
        var travelCharge = contractRule?.TravelCharge ?? 0m;
        var markupPercent = contractRule?.MaterialMarkupPercent ?? 0m;
        var discountPercent = contractRule?.DiscountPercent ?? 0m;
        var minimumBillableHours = contractRule?.MinimumBillableHours ?? 0m;
        var includedLabourHours = contractRule?.IncludedLabourHours ?? 0m;
        var vatApplicable = contractRule?.VatApplicable ?? true;

        var billableHours = Math.Max(requestedHours, minimumBillableHours);
        billableHours = Math.Max(0m, billableHours - includedLabourHours);
        var adjustedMaterialCost = materialCost + (materialCost * (markupPercent / 100m));

        var estimatedCharge = billingModel switch
        {
            BillingModel.Included or BillingModel.Warranty or BillingModel.Retainer => 0m,
            BillingModel.FixedPrice => fixedVisitFee > 0m ? fixedVisitFee : calloutFee,
            BillingModel.PerVisit => fixedVisitFee > 0m ? fixedVisitFee : calloutFee,
            _ => calloutFee + fixedVisitFee + travelCharge + (billableHours * labourRate) + adjustedMaterialCost
        };

        if (discountPercent > 0m)
        {
            estimatedCharge -= estimatedCharge * (discountPercent / 100m);
        }

        var appliedRateSource = contractRule is not null
            ? "ContractRateRule"
            : contractRateCard is not null
                ? "ContractRateCardDefault"
                : defaultLabourCard is not null || defaultCalloutCard is not null
                    ? "TenantDefaultRateCard"
                    : "StandardChargeableFallback";

        if (coverage.ContractId.HasValue && billingModel == BillingModel.PreferentialRates)
        {
            warnings.Add("Preferential contract rates will apply to this work order.");
        }

        if (coverage.ContractId.HasValue && coverage.CoverageStatus == CoverageStatus.NotCovered)
        {
            warnings.Add("A client contract exists, but this work order is outside contract coverage.");
        }

        return new ContractPricingEvaluation(
            coverage.ContractId,
            contractRateCard?.Id,
            contractRateCard?.Name,
            contractRule?.Id,
            billingModel,
            coverage.CoverageStatus,
            appliedRateSource,
            calloutFee,
            fixedVisitFee,
            labourRate,
            overtimeRate,
            weekendRate,
            holidayRate,
            emergencyMultiplier,
            travelCharge,
            markupPercent,
            discountPercent,
            minimumBillableHours,
            includedLabourHours,
            vatApplicable,
            Math.Max(estimatedCharge, 0m),
            warnings);
    }

    private static ContractRateRule? SelectBestRule(
        IReadOnlyCollection<ContractRateCard> cards,
        Guid? siteId,
        string? assetCategory,
        string? workOrderType,
        string? priority)
    {
        var rules = cards
            .SelectMany(x => x.Rules.Where(rule => rule.IsActive))
            .ToList();

        return rules
            .OrderByDescending(rule => GetRuleScore(rule, siteId, assetCategory, workOrderType, priority))
            .ThenBy(rule => rule.CreatedAt)
            .FirstOrDefault(rule => GetRuleScore(rule, siteId, assetCategory, workOrderType, priority) > 0);
    }

    private static int GetRuleScore(
        ContractRateRule rule,
        Guid? siteId,
        string? assetCategory,
        string? workOrderType,
        string? priority)
    {
        if (rule.SiteId.HasValue && rule.SiteId != siteId) return 0;
        if (!string.IsNullOrWhiteSpace(rule.AssetCategoryName)
            && !string.Equals(rule.AssetCategoryName, assetCategory, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }
        if (!string.IsNullOrWhiteSpace(rule.WorkOrderTypeName)
            && !string.Equals(rule.WorkOrderTypeName, workOrderType, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }
        if (!string.IsNullOrWhiteSpace(rule.Priority)
            && !string.Equals(rule.Priority, priority, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var score = 1;
        if (rule.SiteId.HasValue) score += 8;
        if (!string.IsNullOrWhiteSpace(rule.AssetCategoryName) || rule.AssetCategoryId.HasValue) score += 4;
        if (!string.IsNullOrWhiteSpace(rule.WorkOrderTypeName) || rule.WorkOrderTypeId.HasValue) score += 2;
        if (!string.IsNullOrWhiteSpace(rule.Priority)) score += 1;
        return score;
    }
}

internal sealed class ContractLifecycleService(
    AppDbContext dbContext,
    ILogger<ContractLifecycleService> logger) : IContractLifecycleService
{
    public async Task<IReadOnlyCollection<ContractLifecycleEvent>> GetLifecycleAsync(Guid tenantId, Guid contractId, CancellationToken cancellationToken = default) =>
        await dbContext.ContractLifecycleEvents
            .Include(x => x.CreatedByUser)
            .Where(x => x.TenantId == tenantId && x.ContractId == contractId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

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

    public int CalculateDaysToExpiry(DateTime endDateUtc, DateTime? asOfUtc = null)
    {
        var asOf = (asOfUtc ?? DateTime.UtcNow).Date;
        return (endDateUtc.Date - asOf).Days;
    }

    public Task<Contract> SubmitForApprovalAsync(Guid tenantId, Guid contractId, Guid? actorUserId, string? note, CancellationToken cancellationToken = default) =>
        TransitionAsync(tenantId, contractId, actorUserId, ContractLifecycleEventType.SubmittedForApproval, ContractStatus.PendingApproval, note, null, contract =>
        {
            EnsureStatus(contract, ContractStatus.Draft, "Only draft contracts can be submitted for approval.");
            contract.SubmittedForApprovalAt = DateTime.UtcNow;
        }, cancellationToken);

    public async Task<Contract> ApproveAsync(Guid tenantId, Guid contractId, Guid? actorUserId, string? note, CancellationToken cancellationToken = default)
    {
        var contract = await LoadContractAsync(tenantId, contractId, cancellationToken);
        EnsureStatus(contract, ContractStatus.PendingApproval, "Only contracts pending approval can be approved.");
        contract.ApprovedAt = DateTime.UtcNow;
        await AddLifecycleEventAsync(contract, actorUserId, ContractLifecycleEventType.Approved, contract.Status, contract.Status, note, null, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return contract;
    }

    public Task<Contract> ActivateAsync(Guid tenantId, Guid contractId, Guid? actorUserId, string? note, CancellationToken cancellationToken = default) =>
        TransitionAsync(tenantId, contractId, actorUserId, ContractLifecycleEventType.Activated, ContractStatus.Active, note, null, contract =>
        {
            EnsureStatus(contract, ContractStatus.PendingApproval, "Only contracts pending approval can be activated.");
            if (!contract.ApprovedAt.HasValue)
            {
                throw new BusinessRuleException("Contract must be approved before activation.");
            }
            contract.ActivatedAt = DateTime.UtcNow;
        }, cancellationToken);

    public Task<Contract> StartRenewalAsync(Guid tenantId, Guid contractId, Guid? actorUserId, string? note, CancellationToken cancellationToken = default) =>
        TransitionAsync(tenantId, contractId, actorUserId, ContractLifecycleEventType.RenewalStarted, ContractStatus.RenewalInProgress, note, null, contract =>
        {
            if (contract.Status is not ContractStatus.Active and not ContractStatus.ExpiringSoon and not ContractStatus.Expired)
            {
                throw new BusinessRuleException("Renewal can only start from active, expiring soon, or expired contracts.");
            }
            contract.RenewalStartedAt = DateTime.UtcNow;
            contract.RenewalRisk = CalculateRenewalRisk(contract.EndDate);
        }, cancellationToken, createOrUpdateRenewal: true);

    public Task<Contract> SuspendAsync(Guid tenantId, Guid contractId, Guid? actorUserId, string? reason, CancellationToken cancellationToken = default) =>
        TransitionAsync(tenantId, contractId, actorUserId, ContractLifecycleEventType.Suspended, ContractStatus.Suspended, reason, reason, contract =>
        {
            if (contract.Status is ContractStatus.Terminated or ContractStatus.Cancelled or ContractStatus.Renewed)
            {
                throw new BusinessRuleException("This contract can no longer be suspended.");
            }
            contract.SuspensionReason = reason?.Trim();
        }, cancellationToken);

    public Task<Contract> ResumeAsync(Guid tenantId, Guid contractId, Guid? actorUserId, string? note, CancellationToken cancellationToken = default) =>
        TransitionAsync(tenantId, contractId, actorUserId, ContractLifecycleEventType.Resumed, ContractStatus.Active, note, null, contract =>
        {
            EnsureStatus(contract, ContractStatus.Suspended, "Only suspended contracts can be resumed.");
            contract.SuspensionReason = null;
        }, cancellationToken);

    public Task<Contract> TerminateAsync(Guid tenantId, Guid contractId, Guid? actorUserId, DateTime? terminationDate, string? reason, CancellationToken cancellationToken = default) =>
        TransitionAsync(tenantId, contractId, actorUserId, ContractLifecycleEventType.Terminated, ContractStatus.Terminated, reason, reason, contract =>
        {
            if (contract.Status is ContractStatus.Renewed or ContractStatus.Cancelled or ContractStatus.Terminated)
            {
                throw new BusinessRuleException("This contract can no longer be terminated.");
            }
            contract.TerminationDate = terminationDate ?? DateTime.UtcNow;
            contract.TerminationReason = reason?.Trim();
        }, cancellationToken);

    public Task<Contract> CancelAsync(Guid tenantId, Guid contractId, Guid? actorUserId, string? reason, CancellationToken cancellationToken = default) =>
        TransitionAsync(tenantId, contractId, actorUserId, ContractLifecycleEventType.Cancelled, ContractStatus.Cancelled, reason, reason, contract =>
        {
            if (contract.Status is ContractStatus.Renewed or ContractStatus.Terminated)
            {
                throw new BusinessRuleException("This contract can no longer be cancelled.");
            }
            contract.CancellationReason = reason?.Trim();
        }, cancellationToken);

    public async Task<Contract> RecalculateLifecycleAsync(Guid tenantId, Guid contractId, Guid? actorUserId, CancellationToken cancellationToken = default)
    {
        var contract = await LoadContractAsync(tenantId, contractId, cancellationToken);
        var previousStatus = contract.Status;
        var previousRisk = contract.RenewalRisk;
        RecalculateState(contract);
        if (contract.Status != previousStatus)
        {
            var eventType = contract.Status switch
            {
                ContractStatus.ExpiringSoon => ContractLifecycleEventType.MarkedExpiringSoon,
                ContractStatus.Expired => ContractLifecycleEventType.Expired,
                _ => ContractLifecycleEventType.Reopened
            };
            await AddLifecycleEventAsync(contract, actorUserId, eventType, previousStatus, contract.Status, null, "Lifecycle recalculated", cancellationToken);
        }
        else if (contract.RenewalRisk != previousRisk)
        {
            await AddLifecycleEventAsync(contract, actorUserId, ContractLifecycleEventType.Reopened, contract.Status, contract.Status, null, "Renewal risk recalculated", cancellationToken);
        }

        contract.ExpiryCheckedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return contract;
    }

    public async Task<(Contract OldContract, Contract NewContract)> RenewAsync(Guid tenantId, Guid contractId, Guid? actorUserId, ContractRenewalOptions options, CancellationToken cancellationToken = default)
    {
        var oldContract = await dbContext.Contracts
            .Include(x => x.Sites)
            .Include(x => x.Assets)
            .Include(x => x.AssetCategories)
            .Include(x => x.WorkOrderTypes)
            .Include(x => x.ReminderRules)
            .Include(x => x.PmEntitlements)
            .Include(x => x.RateCards)
            .ThenInclude(x => x.Rules)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == contractId, cancellationToken)
            ?? throw new NotFoundException("Contract was not found.");

        if (oldContract.Status is not ContractStatus.ExpiringSoon and not ContractStatus.RenewalInProgress and not ContractStatus.Expired and not ContractStatus.Active)
        {
            throw new BusinessRuleException("Only active, expiring, renewal-in-progress, or expired contracts can be renewed.");
        }

        var newContract = new Contract
        {
            TenantId = tenantId,
            ClientId = oldContract.ClientId,
            ContractNumber = $"{oldContract.ContractNumber}-R{DateTime.UtcNow:yyyyMMddHHmm}",
            Name = options.Name?.Trim() ?? $"{oldContract.Name} Renewal",
            ContractType = oldContract.ContractType,
            Status = options.ActivateNewContract ? ContractStatus.Active : options.SubmitForApproval ? ContractStatus.PendingApproval : ContractStatus.Draft,
            StartDate = options.StartDate?.Date ?? oldContract.EndDate.Date.AddDays(1),
            EndDate = options.EndDate?.Date ?? oldContract.EndDate.Date.AddMonths(options.DurationMonths ?? oldContract.DurationMonths ?? 12),
            DurationMonths = options.DurationMonths ?? oldContract.DurationMonths,
            BillingModel = oldContract.BillingModel,
            Currency = oldContract.Currency,
            ContractValue = options.ContractValue ?? oldContract.ContractValue,
            RenewalNoticeDays = oldContract.RenewalNoticeDays,
            Description = oldContract.Description,
            IncludedServices = oldContract.IncludedServices,
            ExcludedServices = oldContract.ExcludedServices,
            Notes = options.Notes?.Trim() ?? oldContract.Notes,
            RenewalOwnerUserId = oldContract.RenewalOwnerUserId,
            AccountManagerUserId = oldContract.AccountManagerUserId,
            InternalApproverUserId = oldContract.InternalApproverUserId,
            AutoExpireEnabled = oldContract.AutoExpireEnabled,
            GracePeriodDays = oldContract.GracePeriodDays,
            AllowPreferentialRatesDuringGracePeriod = oldContract.AllowPreferentialRatesDuringGracePeriod,
            AllowSlaDuringGracePeriod = oldContract.AllowSlaDuringGracePeriod,
            ResponseTimeHours = oldContract.ResponseTimeHours,
            ResolutionTimeHours = oldContract.ResolutionTimeHours,
            WorkingHoursPolicy = oldContract.WorkingHoursPolicy,
            PauseSlaOnHold = oldContract.PauseSlaOnHold,
            EscalationEmail = oldContract.EscalationEmail,
            PriorityLevel = oldContract.PriorityLevel,
            GracePeriodAllowsSla = oldContract.GracePeriodAllowsSla,
            RenewedFromContractId = oldContract.Id,
            ActivatedAt = options.ActivateNewContract ? DateTime.UtcNow : null,
            ApprovedAt = options.ActivateNewContract || options.SubmitForApproval ? DateTime.UtcNow : null,
            SubmittedForApprovalAt = options.SubmitForApproval ? DateTime.UtcNow : null
        };
        RecalculateState(newContract);

        dbContext.Contracts.Add(newContract);
        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.ContractSites.AddRange(oldContract.Sites.Select(x => new ContractSite
        {
            TenantId = tenantId,
            ContractId = newContract.Id,
            SiteId = x.SiteId
        }));
        dbContext.ContractAssets.AddRange(oldContract.Assets.Select(x => new ContractAsset
        {
            TenantId = tenantId,
            ContractId = newContract.Id,
            AssetId = x.AssetId
        }));
        dbContext.ContractAssetCategories.AddRange(oldContract.AssetCategories.Select(x => new ContractAssetCategory
        {
            TenantId = tenantId,
            ContractId = newContract.Id,
            AssetCategoryId = x.AssetCategoryId,
            AssetCategoryName = x.AssetCategoryName
        }));
        dbContext.ContractWorkOrderTypes.AddRange(oldContract.WorkOrderTypes.Select(x => new ContractWorkOrderType
        {
            TenantId = tenantId,
            ContractId = newContract.Id,
            WorkOrderTypeId = x.WorkOrderTypeId,
            WorkOrderTypeName = x.WorkOrderTypeName
        }));
        dbContext.ContractReminderRules.AddRange(oldContract.ReminderRules.Select(x => new ContractReminderRule
        {
            TenantId = tenantId,
            ContractId = newContract.Id,
            DaysBeforeExpiry = x.DaysBeforeExpiry,
            NotifyAccountManager = x.NotifyAccountManager,
            NotifyRenewalOwner = x.NotifyRenewalOwner,
            NotifySupervisor = x.NotifySupervisor,
            NotifyFinance = x.NotifyFinance,
            NotifyTenantAdmin = x.NotifyTenantAdmin,
            EmailEnabled = x.EmailEnabled,
            InAppEnabled = x.InAppEnabled,
            EscalationEnabled = x.EscalationEnabled,
            EscalationAfterDays = x.EscalationAfterDays,
            IsActive = x.IsActive
        }));
        dbContext.ContractPmEntitlements.AddRange(oldContract.PmEntitlements.Select(x => new ContractPmEntitlement
        {
            TenantId = tenantId,
            ContractId = newContract.Id,
            AssetId = x.AssetId,
            AssetCategory = x.AssetCategory,
            WorkOrderType = x.WorkOrderType,
            VisitsPerPeriod = x.VisitsPerPeriod,
            PeriodType = x.PeriodType,
            IncludedVisits = x.IncludedVisits,
            UsedVisits = 0,
            RemainingVisits = x.IncludedVisits,
            EffectiveFrom = newContract.StartDate,
            EffectiveTo = newContract.EndDate,
            IsActive = x.IsActive,
            Notes = x.Notes
        }));
        foreach (var sourceCard in oldContract.RateCards)
        {
            var targetCard = new ContractRateCard
            {
                TenantId = tenantId,
                ContractId = newContract.Id,
                Name = sourceCard.Name,
                Description = sourceCard.Description,
                Currency = sourceCard.Currency,
                IsDefault = sourceCard.IsDefault,
                EffectiveFrom = newContract.StartDate,
                EffectiveTo = newContract.EndDate,
                IsActive = sourceCard.IsActive
            };
            dbContext.ContractRateCards.Add(targetCard);
            await dbContext.SaveChangesAsync(cancellationToken);

            dbContext.ContractRateRules.AddRange(sourceCard.Rules.Select(rule => new ContractRateRule
            {
                TenantId = tenantId,
                RateCardId = targetCard.Id,
                WorkOrderTypeId = rule.WorkOrderTypeId,
                WorkOrderTypeName = rule.WorkOrderTypeName,
                AssetCategoryId = rule.AssetCategoryId,
                AssetCategoryName = rule.AssetCategoryName,
                SiteId = rule.SiteId,
                Priority = rule.Priority,
                BillingModel = rule.BillingModel,
                CalloutFee = rule.CalloutFee,
                FixedVisitFee = rule.FixedVisitFee,
                LabourHourlyRate = rule.LabourHourlyRate,
                OvertimeHourlyRate = rule.OvertimeHourlyRate,
                WeekendHourlyRate = rule.WeekendHourlyRate,
                PublicHolidayHourlyRate = rule.PublicHolidayHourlyRate,
                EmergencyMultiplier = rule.EmergencyMultiplier,
                TravelCharge = rule.TravelCharge,
                MaterialMarkupPercent = rule.MaterialMarkupPercent,
                DiscountPercent = rule.DiscountPercent,
                MinimumBillableHours = rule.MinimumBillableHours,
                IncludedLabourHours = rule.IncludedLabourHours,
                VatApplicable = rule.VatApplicable,
                Notes = rule.Notes,
                IsActive = rule.IsActive
            }));
        }

        oldContract.Status = ContractStatus.Renewed;
        oldContract.RenewedToContractId = newContract.Id;
        oldContract.RenewalStartedAt ??= DateTime.UtcNow;
        oldContract.RenewalRisk = CalculateRenewalRisk(oldContract.EndDate);

        var renewal = await dbContext.ContractRenewals
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.OriginalContractId == oldContract.Id, cancellationToken);
        if (renewal is null)
        {
            renewal = new ContractRenewal
            {
                TenantId = tenantId,
                OriginalContractId = oldContract.Id,
                OwnerUserId = oldContract.RenewalOwnerUserId,
                RenewalStartedAt = oldContract.RenewalStartedAt ?? DateTime.UtcNow
            };
            dbContext.ContractRenewals.Add(renewal);
        }
        renewal.RenewedContractId = newContract.Id;
        renewal.RenewalStatus = ContractRenewalStatus.Completed;
        renewal.RenewalCompletedAt = DateTime.UtcNow;
        renewal.UpdatedAt = DateTime.UtcNow;

        await AddLifecycleEventAsync(oldContract, actorUserId, ContractLifecycleEventType.Renewed, oldContract.Status, ContractStatus.Renewed, options.Notes, null, cancellationToken);
        await AddLifecycleEventAsync(newContract, actorUserId, ContractLifecycleEventType.Created, null, newContract.Status, options.Notes, "Renewal contract created", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (oldContract, newContract);
    }

    private async Task<Contract> TransitionAsync(
        Guid tenantId,
        Guid contractId,
        Guid? actorUserId,
        ContractLifecycleEventType eventType,
        ContractStatus newStatus,
        string? note,
        string? reason,
        Action<Contract> mutator,
        CancellationToken cancellationToken,
        bool createOrUpdateRenewal = false)
    {
        var contract = await LoadContractAsync(tenantId, contractId, cancellationToken);
        var oldStatus = contract.Status;
        mutator(contract);
        contract.Status = newStatus;
        RecalculateState(contract, preserveManualStatus: true);

        if (createOrUpdateRenewal)
        {
            var renewal = await dbContext.ContractRenewals.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.OriginalContractId == contract.Id, cancellationToken);
            if (renewal is null)
            {
                renewal = new ContractRenewal
                {
                    TenantId = tenantId,
                    OriginalContractId = contract.Id,
                    OwnerUserId = contract.RenewalOwnerUserId,
                    RenewalStartedAt = DateTime.UtcNow,
                    RenewalStatus = ContractRenewalStatus.InProgress
                };
                dbContext.ContractRenewals.Add(renewal);
            }
            else
            {
                renewal.RenewalStatus = ContractRenewalStatus.InProgress;
                renewal.RenewalStartedAt ??= DateTime.UtcNow;
            }
        }

        await AddLifecycleEventAsync(contract, actorUserId, eventType, oldStatus, contract.Status, note, reason, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return contract;
    }

    private async Task<Contract> LoadContractAsync(Guid tenantId, Guid contractId, CancellationToken cancellationToken) =>
        await dbContext.Contracts
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == contractId, cancellationToken)
        ?? throw new NotFoundException("Contract was not found.");

    private async Task AddLifecycleEventAsync(
        Contract contract,
        Guid? actorUserId,
        ContractLifecycleEventType eventType,
        ContractStatus? oldStatus,
        ContractStatus? newStatus,
        string? note,
        string? reason,
        CancellationToken cancellationToken)
    {
        dbContext.ContractLifecycleEvents.Add(new ContractLifecycleEvent
        {
            TenantId = contract.TenantId,
            ContractId = contract.Id,
            EventType = eventType,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            Note = note?.Trim(),
            Reason = reason?.Trim(),
            CreatedByUserId = actorUserId
        });
        await Task.CompletedTask;
    }

    private void RecalculateState(Contract contract, bool preserveManualStatus = false)
    {
        contract.RenewalRisk = CalculateRenewalRisk(contract.EndDate);
        var daysToExpiry = CalculateDaysToExpiry(contract.EndDate);
        contract.NextReminderAt = daysToExpiry >= 0 ? contract.EndDate.Date.AddDays(-Math.Min(contract.RenewalNoticeDays, Math.Max(daysToExpiry, 0))) : null;

        if (preserveManualStatus && contract.Status is ContractStatus.Suspended or ContractStatus.Terminated or ContractStatus.Cancelled or ContractStatus.Renewed or ContractStatus.RenewalInProgress)
        {
            return;
        }

        if (IsAutoExpireEnabled(contract) && contract.EndDate.Date < DateTime.UtcNow.Date)
        {
            contract.Status = ContractStatus.Expired;
            return;
        }

        if (daysToExpiry <= contract.RenewalNoticeDays && contract.Status == ContractStatus.Active)
        {
            contract.Status = ContractStatus.ExpiringSoon;
        }
    }

    private static void EnsureStatus(Contract contract, ContractStatus expected, string message)
    {
        if (contract.Status != expected)
        {
            throw new BusinessRuleException(message);
        }
    }

    private static bool IsAutoExpireEnabled(Contract contract) =>
        string.Equals(contract.RenewalBehavior, "AutoExpire", StringComparison.OrdinalIgnoreCase)
        || contract.AutoExpireEnabled;
}

internal sealed class ContractOperationalService(
    AppDbContext dbContext,
    IContractCoverageService coverageService,
    IContractPricingService pricingService,
    ISlaService slaService) : IContractOperationalService
{
    public async Task<ContractOperationalSnapshot> ApplyOperationalSnapshotAsync(WorkOrder workOrder, CancellationToken cancellationToken = default)
    {
        var coverage = await coverageService.EvaluateAsync(
            workOrder.TenantId,
            workOrder.ClientId,
            workOrder.SiteId,
            workOrder.AssetId,
            workOrder.WorkType,
            workOrder.ServiceType ?? workOrder.WorkType,
            DateTime.UtcNow,
            true,
            cancellationToken);

        var pricing = await pricingService.EvaluateAsync(
            workOrder.TenantId,
            workOrder.ClientId,
            workOrder.SiteId,
            workOrder.AssetId,
            workOrder.WorkType,
            workOrder.Priority,
            DateTime.UtcNow,
            workOrder.EstimatedDuration,
            workOrder.EstimatedMaterialCost,
            true,
            cancellationToken);

        var slaTarget = await ResolveContractSlaAsync(workOrder, coverage.ContractId, cancellationToken);
        workOrder.ContractId = coverage.ContractId;
        workOrder.RateCardId = pricing.ContractRateCardId;
        workOrder.CoverageStatus = coverage.CoverageStatus.ToString();
        workOrder.BillingModel = pricing.BillingModel.ToString();
        workOrder.PricingStatus = "Estimated";
        workOrder.AppliedRateSource = pricing.AppliedRateSource;
        workOrder.EstimatedLabourHours = Math.Max(workOrder.EstimatedDuration ?? 1m, pricing.MinimumBillableHours);
        workOrder.EstimatedCharge = pricing.EstimatedCharge;
        workOrder.PricingNotes = pricing.Warnings.Count > 0 ? string.Join(" | ", pricing.Warnings) : null;

        var appliedContractSla = coverage.ContractId.HasValue
            && slaTarget.ResponseTimeHours.HasValue
            && slaTarget.ResolutionTimeHours.HasValue;

        if (appliedContractSla)
        {
            workOrder.AppliedSlaSource = slaTarget.Source;
            workOrder.AppliedResponseHours = slaTarget.ResponseTimeHours;
            workOrder.AppliedResolutionHours = slaTarget.ResolutionTimeHours;
            workOrder.SlaResponseDeadline = DateTime.UtcNow.AddHours((double)slaTarget.ResponseTimeHours.Value);
            workOrder.SlaResolutionDeadline = DateTime.UtcNow.AddHours((double)slaTarget.ResolutionTimeHours.Value);
            workOrder.SlaResponseBreached = false;
            workOrder.SlaResolutionBreached = false;
            workOrder.SlaResponseBreachedAt = null;
            workOrder.SlaResolutionBreachedAt = null;
        }
        else
        {
            workOrder.AppliedSlaSource = "TenantDefault";
            workOrder.AppliedResponseHours = null;
            workOrder.AppliedResolutionHours = null;
            await slaService.ApplyDefinitionAsync(workOrder, cancellationToken);
        }

        workOrder.ContractSnapshotJson = JsonSerializer.Serialize(new
        {
            coverage.ContractId,
            coverage.ContractNumber,
            coverage.ContractName,
            coverage.ContractStatus,
            coverage.RenewalRisk,
            coverage.CoverageStatus,
            coverage.SiteCovered,
            coverage.AssetCovered,
            coverage.AssetCategoryCovered,
            coverage.WorkOrderTypeCovered,
            coverage.ExcludedServiceMatched,
            coverage.BillingModel,
            coverage.WarningMessages,
            CapturedAtUtc = DateTime.UtcNow
        });
        workOrder.SlaSnapshotJson = JsonSerializer.Serialize(new
        {
            Source = workOrder.AppliedSlaSource,
            workOrder.AppliedResponseHours,
            workOrder.AppliedResolutionHours,
            workOrder.SlaResponseDeadline,
            workOrder.SlaResolutionDeadline,
            CapturedAtUtc = DateTime.UtcNow
        });
        workOrder.PricingSnapshotJson = JsonSerializer.Serialize(new
        {
            pricing.ContractRateCardId,
            pricing.ContractRateCardName,
            pricing.ContractRateRuleId,
            pricing.BillingModel,
            pricing.CoverageStatus,
            pricing.AppliedRateSource,
            pricing.CalloutFee,
            pricing.FixedVisitFee,
            pricing.LabourHourlyRate,
            pricing.OvertimeHourlyRate,
            pricing.WeekendHourlyRate,
            pricing.PublicHolidayHourlyRate,
            pricing.EmergencyMultiplier,
            pricing.TravelCharge,
            pricing.MaterialMarkupPercent,
            pricing.DiscountPercent,
            pricing.MinimumBillableHours,
            pricing.IncludedLabourHours,
            pricing.VatApplicable,
            pricing.EstimatedCharge,
            pricing.Warnings,
            PmEntitlementCounted = GetPmEntitlementCounted(workOrder.PricingSnapshotJson),
            CapturedAtUtc = DateTime.UtcNow
        });

        return new ContractOperationalSnapshot(
            coverage,
            pricing,
            appliedContractSla,
            workOrder.AppliedResponseHours,
            workOrder.AppliedResolutionHours);
    }

    private async Task<(decimal? ResponseTimeHours, decimal? ResolutionTimeHours, string Source)> ResolveContractSlaAsync(
        WorkOrder workOrder,
        Guid? contractId,
        CancellationToken cancellationToken)
    {
        if (!contractId.HasValue)
        {
            return (null, null, "TenantDefault");
        }

        var contract = await dbContext.Contracts
            .Where(x => x.TenantId == workOrder.TenantId && x.Id == contractId.Value)
            .Select(x => new { x.SlaMatrixJson, x.ResponseTimeHours, x.ResolutionTimeHours })
            .SingleOrDefaultAsync(cancellationToken);

        if (contract is null)
        {
            return (null, null, "TenantDefault");
        }

        var slaRow = SelectSlaRow(contract.SlaMatrixJson, workOrder.Priority)
            ?? new SlaRow(
                "Standard",
                contract.ResponseTimeHours,
                contract.ResolutionTimeHours);

        return (slaRow.ResponseTimeHours, slaRow.ResolutionTimeHours, "Contract");
    }

    private static SlaRow? SelectSlaRow(string? json, string? priority)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var rows = JsonSerializer.Deserialize<List<SlaRow>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (rows is null || rows.Count == 0)
            {
                return null;
            }

            var normalizedPriority = priority?.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedPriority))
            {
                var exact = rows.FirstOrDefault(x => string.Equals(x.Priority, normalizedPriority, StringComparison.OrdinalIgnoreCase));
                if (exact is not null)
                {
                    return exact;
                }
            }

            return rows.FirstOrDefault(x => string.Equals(x.Priority, "Standard", StringComparison.OrdinalIgnoreCase))
                ?? rows[0];
        }
        catch
        {
            return null;
        }
    }

    private sealed record SlaRow(string Priority, decimal? ResponseTimeHours, decimal? ResolutionTimeHours);

    public async Task<ContractOperationalSnapshot> RecalculateWorkOrderAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default)
    {
        var workOrder = await dbContext.WorkOrders
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == workOrderId, cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");

        var snapshot = await ApplyOperationalSnapshotAsync(workOrder, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return snapshot;
    }

    public async Task IncrementPmEntitlementUsageIfNeededAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default)
    {
        var workOrder = await dbContext.WorkOrders
            .Include(x => x.Asset)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == workOrderId, cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");

        if (!workOrder.IsPreventiveMaintenance || !workOrder.ContractId.HasValue)
        {
            return;
        }

        if (GetPmEntitlementCounted(workOrder.PricingSnapshotJson))
        {
            return;
        }

        var assetCategory = workOrder.Asset?.AssetCategory?.Name ?? workOrder.Asset?.AssetType;
        var entitlement = await dbContext.ContractPmEntitlements
            .Where(x => x.TenantId == tenantId
                && x.ContractId == workOrder.ContractId.Value
                && x.IsActive
                && x.EffectiveFrom <= DateTime.UtcNow
                && x.EffectiveTo >= DateTime.UtcNow
                && x.WorkOrderType == workOrder.WorkType)
            .OrderByDescending(x => x.AssetId.HasValue && x.AssetId == workOrder.AssetId)
            .ThenByDescending(x => !string.IsNullOrWhiteSpace(x.AssetCategory) && x.AssetCategory == assetCategory)
            .FirstOrDefaultAsync(cancellationToken);

        if (entitlement is null)
        {
            return;
        }

        entitlement.UsedVisits += 1;
        entitlement.RemainingVisits = Math.Max(0, entitlement.IncludedVisits - entitlement.UsedVisits);

        var snapshot = ParseSnapshot(workOrder.PricingSnapshotJson);
        snapshot["PmEntitlementCounted"] = JsonSerializer.SerializeToNode(true);
        workOrder.PricingSnapshotJson = snapshot.ToJsonString();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool GetPmEntitlementCounted(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("PmEntitlementCounted", out var property)
                && property.ValueKind == JsonValueKind.True;
        }
        catch
        {
            return false;
        }
    }

    private static JsonObject ParseSnapshot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<JsonObject>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}

internal sealed class ContractReminderWorker(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ContractReminderWorker> logger) : BackgroundService
{
    private static readonly int[] DefaultThresholds = [90, 60, 30, 14, 7, 0];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Contract reminder worker started.");
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));

        await RunCycleAsync(stoppingToken);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCycleAsync(stoppingToken);
        }
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var lifecycleService = scope.ServiceProvider.GetRequiredService<IContractLifecycleService>();
            var emailOutboxService = scope.ServiceProvider.GetRequiredService<IEmailOutboxService>();

            var contracts = await dbContext.Contracts
                .Include(x => x.Client)
                .Where(x => !x.IsArchived
                    && x.Status != ContractStatus.Cancelled
                    && x.Status != ContractStatus.Terminated
                    && x.Status != ContractStatus.Renewed)
                .ToListAsync(cancellationToken);

            foreach (var contract in contracts)
            {
                var previousStatus = contract.Status;
                var previousRisk = contract.RenewalRisk;
                var daysToExpiry = lifecycleService.CalculateDaysToExpiry(contract.EndDate);
                contract.RenewalRisk = lifecycleService.CalculateRenewalRisk(contract.EndDate);
                contract.ExpiryCheckedAt = DateTime.UtcNow;

                if ((string.Equals(contract.RenewalBehavior, "AutoExpire", StringComparison.OrdinalIgnoreCase) || contract.AutoExpireEnabled)
                    && daysToExpiry < 0
                    && contract.Status != ContractStatus.Expired)
                {
                    contract.Status = ContractStatus.Expired;
                    dbContext.ContractLifecycleEvents.Add(new ContractLifecycleEvent
                    {
                        TenantId = contract.TenantId,
                        ContractId = contract.Id,
                        EventType = ContractLifecycleEventType.Expired,
                        OldStatus = previousStatus,
                        NewStatus = ContractStatus.Expired,
                        Note = "Automatically expired after contract end date."
                    });
                }
                else if (daysToExpiry <= contract.RenewalNoticeDays
                    && contract.Status == ContractStatus.Active)
                {
                    contract.Status = ContractStatus.ExpiringSoon;
                    dbContext.ContractLifecycleEvents.Add(new ContractLifecycleEvent
                    {
                        TenantId = contract.TenantId,
                        ContractId = contract.Id,
                        EventType = ContractLifecycleEventType.MarkedExpiringSoon,
                        OldStatus = previousStatus,
                        NewStatus = ContractStatus.ExpiringSoon,
                        Note = "Contract entered renewal notice window."
                    });
                }

                if (previousRisk != contract.RenewalRisk && previousStatus == contract.Status)
                {
                    dbContext.ContractLifecycleEvents.Add(new ContractLifecycleEvent
                    {
                        TenantId = contract.TenantId,
                        ContractId = contract.Id,
                        EventType = ContractLifecycleEventType.Reopened,
                        OldStatus = contract.Status,
                        NewStatus = contract.Status,
                        Note = "Renewal risk was recalculated."
                    });
                }

                var rules = await dbContext.ContractReminderRules
                    .Where(x => x.TenantId == contract.TenantId && x.IsActive && (x.ContractId == contract.Id || x.ContractId == null))
                    .OrderBy(x => x.ContractId.HasValue ? 0 : 1)
                    .ToListAsync(cancellationToken);

                var thresholds = rules.Count == 0
                    ? DefaultThresholds.Select(days => new ContractReminderRule
                    {
                        TenantId = contract.TenantId,
                        ContractId = contract.Id,
                        DaysBeforeExpiry = days,
                        NotifyAccountManager = true,
                        NotifyRenewalOwner = true,
                        NotifyTenantAdmin = true,
                        EmailEnabled = true,
                        InAppEnabled = false,
                        IsActive = true
                    }).ToList()
                    : rules;

                foreach (var rule in thresholds.Where(x => x.DaysBeforeExpiry == daysToExpiry))
                {
                    var thresholdKey = $"Threshold:{rule.DaysBeforeExpiry}";
                    var alreadySent = await dbContext.ContractLifecycleEvents.AnyAsync(
                        x => x.TenantId == contract.TenantId
                            && x.ContractId == contract.Id
                            && x.EventType == ContractLifecycleEventType.ReminderSent
                            && x.Reason == thresholdKey,
                        cancellationToken);

                    if (alreadySent)
                    {
                        continue;
                    }

                    var recipients = await ResolveReminderRecipientsAsync(dbContext, contract, rule, cancellationToken);
                    foreach (var recipient in recipients)
                    {
                        if (!rule.EmailEnabled || string.IsNullOrWhiteSpace(recipient.Email))
                        {
                            continue;
                        }

                        var message = BuildReminderMessage(contract, daysToExpiry);
                        await emailOutboxService.QueueEmailAsync(
                            new QueueEmailRequest(
                                contract.TenantId,
                                "contract-reminder",
                                "contract-reminder",
                                recipient.Email!,
                                recipient.FullName,
                                "Ecosys ServiceOps",
                                null,
                                null,
                                message.subject,
                                $"<p>{message.body}</p>",
                                message.body,
                                null),
                            cancellationToken);
                    }

                    contract.LastReminderSentAt = DateTime.UtcNow;
                    dbContext.ContractLifecycleEvents.Add(new ContractLifecycleEvent
                    {
                        TenantId = contract.TenantId,
                        ContractId = contract.Id,
                        EventType = ContractLifecycleEventType.ReminderSent,
                        OldStatus = contract.Status,
                        NewStatus = contract.Status,
                        Note = BuildReminderMessage(contract, daysToExpiry).body,
                        Reason = thresholdKey
                    });
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Contract reminder cycle failed.");
        }
    }

    private static async Task<IReadOnlyCollection<(string? Email, string FullName)>> ResolveReminderRecipientsAsync(
        AppDbContext dbContext,
        Contract contract,
        ContractReminderRule rule,
        CancellationToken cancellationToken)
    {
        var userIds = new HashSet<Guid>();
        if (rule.NotifyAccountManager && contract.AccountManagerUserId.HasValue) userIds.Add(contract.AccountManagerUserId.Value);
        if (rule.NotifyRenewalOwner && contract.RenewalOwnerUserId.HasValue) userIds.Add(contract.RenewalOwnerUserId.Value);

        var query = dbContext.Users.Where(x => x.TenantId == contract.TenantId && x.IsActive);
        if (rule.NotifyTenantAdmin)
        {
            foreach (var id in await query.Where(x => x.Role == AppRoles.Admin).Select(x => x.Id).ToListAsync(cancellationToken))
            {
                userIds.Add(id);
            }
        }

        if (rule.NotifyFinance)
        {
            foreach (var id in await query.Where(x => x.JobTitle != null && x.JobTitle.Contains("Finance")).Select(x => x.Id).ToListAsync(cancellationToken))
            {
                userIds.Add(id);
            }
        }

        if (rule.NotifySupervisor)
        {
            foreach (var id in await query.Where(x => x.JobTitle != null && (x.JobTitle.Contains("Supervisor") || x.JobTitle.Contains("Manager"))).Select(x => x.Id).ToListAsync(cancellationToken))
            {
                userIds.Add(id);
            }
        }

        return await dbContext.Users
            .Where(x => x.TenantId == contract.TenantId && userIds.Contains(x.Id))
            .Select(x => new ValueTuple<string?, string>(x.Email, x.FullName))
            .ToListAsync(cancellationToken);
    }

    private static (string subject, string body) BuildReminderMessage(Contract contract, int daysToExpiry)
    {
        var clientName = contract.Client?.ClientName ?? "client";
        return daysToExpiry switch
        {
            90 => ($"Contract {contract.ContractNumber} expires in 90 days", $"Contract {contract.ContractNumber} for {clientName} expires in 90 days. Start renewal review."),
            30 => ($"Contract {contract.ContractNumber} expires in 30 days", $"Contract {contract.ContractNumber} expires in 30 days. Preferential rates and SLA coverage may lapse if not renewed."),
            7 => ($"Urgent: Contract {contract.ContractNumber} expires in 7 days", $"Urgent: Contract {contract.ContractNumber} expires in 7 days. Renewal action is required."),
            0 => ($"Contract {contract.ContractNumber} expires today", $"Contract {contract.ContractNumber} has expired. New work orders will use default chargeable rates unless a renewal or grace rule applies."),
            < 0 => ($"Contract {contract.ContractNumber} has expired", $"Contract {contract.ContractNumber} has expired. New work orders will use default chargeable rates unless a renewal or grace rule applies."),
            _ => ($"Contract {contract.ContractNumber} expires in {daysToExpiry} days", $"Contract {contract.ContractNumber} for {clientName} expires in {daysToExpiry} days. Review renewal progress.")
        };
    }
}
