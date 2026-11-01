using Ecosys.Domain.Entities;
using Ecosys.Domain.Enums;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

public enum PmContractOutcome
{
    /// <summary>A contract in force on the date covers the asset.</summary>
    Covered,
    /// <summary>No contract has ever covered the asset (in-house or pay-per-visit PM): schedule as before.</summary>
    NoContract,
    /// <summary>The asset was under contract, but that contract has ended, been terminated or suspended.</summary>
    Lapsed,
    /// <summary>The cover lapsed, but the schedule was set to continue without a contract.</summary>
    ContinuingWithoutContract,
}

/// <param name="Contract">For Covered, the covering contract; for Lapsed, the contract that lapsed.</param>
public sealed record PmContractCheck(PmContractOutcome Outcome, Contract? Contract)
{
    public bool MayGenerate => Outcome != PmContractOutcome.Lapsed;
}

/// <summary>
/// Preventive maintenance under a contract stops when the contract does. Before a PM work order is
/// raised for a due date, the asset's cover on that date is checked: if the contract that covered it
/// has lapsed and nothing else covers it, the schedule goes on hold (status ContractLapsed) instead
/// of raising a free visit. It resumes by itself once a contract covers the asset again — a renewal,
/// a new contract, or the asset added to one. Meanwhile a one-off chargeable visit can be raised, or
/// the schedule set to continue without a contract.
/// Assets that were never under contract are not affected.
/// </summary>
public static class PmContractGuard
{
    public const string HeldStatus = "ContractLapsed";
    public const string NotificationKey = "pm.contract-lapsed";

    /// <summary>Statuses of a contract that once gave cover; Draft, PendingApproval and Cancelled never did.</summary>
    private static readonly ContractStatus[] HadCoverStatuses =
    [
        ContractStatus.Active, ContractStatus.ExpiringSoon, ContractStatus.RenewalInProgress,
        ContractStatus.Expired, ContractStatus.Terminated, ContractStatus.Renewed, ContractStatus.Suspended,
    ];

    /// <summary>The asset's contract position on <paramref name="asOf"/>. The plan's Asset must be loaded.</summary>
    public static async Task<PmContractCheck> CheckAsync(AppDbContext dbContext, PreventiveMaintenancePlan plan, DateTime asOf, CancellationToken cancellationToken)
    {
        var asset = plan.Asset ?? await dbContext.Assets.SingleAsync(x => x.Id == plan.AssetId, cancellationToken);
        if (asset.AssetCategoryId.HasValue && asset.AssetCategory is null)
            asset.AssetCategory = await dbContext.AssetCategories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == asset.AssetCategoryId.Value, cancellationToken);

        var day = asOf.Date;
        var contracts = await dbContext.Contracts.AsNoTracking()
            .Include(x => x.Assets)
            .Include(x => x.Sites)
            .Include(x => x.AssetCategories).ThenInclude(x => x.AssetCategory)
            .Where(x => x.TenantId == plan.TenantId && x.ClientId == asset.ClientId && !x.IsArchived
                && HadCoverStatuses.Contains(x.Status) && x.StartDate <= day.AddDays(1))
            .ToListAsync(cancellationToken);

        var covering = ContractAssetScope.FindCovering(asset, contracts.Where(x => ContractAssetScope.IsInForce(x, day)));
        if (covering is { } cover)
            return new PmContractCheck(PmContractOutcome.Covered, cover.Contract);

        var lapsed = contracts
            .Where(x => x.StartDate.Date <= day && ContractAssetScope.ScopeFor(x, asset) is not null)
            .OrderByDescending(x => x.EndDate)
            .FirstOrDefault();
        if (lapsed is null)
            return new PmContractCheck(PmContractOutcome.NoContract, null);

        return new PmContractCheck(plan.ContinueWithoutContract ? PmContractOutcome.ContinuingWithoutContract : PmContractOutcome.Lapsed, lapsed);
    }

    /// <summary>"Contract CT-1 ended on 15 Oct 2026" (or was terminated, or is suspended).</summary>
    public static string LapseText(Contract lapsed)
    {
        var what = lapsed.Status switch
        {
            ContractStatus.Terminated => $"was terminated{(lapsed.TerminationDate is { } t ? $" on {t:dd MMM yyyy}" : string.Empty)}",
            ContractStatus.Suspended => "is suspended",
            _ => $"ended on {lapsed.EndDate:dd MMM yyyy}",
        };
        return $"Contract {lapsed.ContractNumber} {what}";
    }

    /// <summary>Plain words for why the schedule is held.</summary>
    public static string HoldReason(Contract lapsed, DateTime dueDate) =>
        $"{LapseText(lapsed)}, so the PM due {dueDate:dd MMM yyyy} was not raised. It resumes when a contract covers the asset again.";

    /// <summary>Puts the plan on hold. Returns false if it already was.</summary>
    public static bool Hold(PreventiveMaintenancePlan plan, Contract lapsed, DateTime dueDate, DateTime now)
    {
        if (plan.Status == HeldStatus && plan.LapsedContractId == lapsed.Id)
            return false;
        plan.Status = HeldStatus;
        plan.HoldReason = HoldReason(lapsed, dueDate);
        plan.HeldAt = now;
        plan.LapsedContractId = lapsed.Id;
        return true;
    }

    /// <summary>
    /// Takes the plan off hold. A due date missed while held moves to today, so the visit is raised
    /// now rather than back-dated; the cycle runs on from there.
    /// </summary>
    public static void Resume(PreventiveMaintenancePlan plan, DateTime now)
    {
        plan.Status = "Active";
        plan.HoldReason = null;
        plan.HeldAt = null;
        plan.LapsedContractId = null;
        if (plan.NextPmDate is { } next && next.Date < now.Date)
            plan.NextPmDate = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
    }

    /// <summary>
    /// Tells admins, and the lapsed contract's account manager and renewal owner, that a schedule is on
    /// hold and what they can do about it.
    /// </summary>
    public static async Task NotifyHeldAsync(INotificationDispatcher? dispatcher, AppDbContext dbContext, PreventiveMaintenancePlan plan, Contract lapsed, CancellationToken cancellationToken)
    {
        if (dispatcher is null)
            return;
        var admins = await dbContext.Users.AsNoTracking()
            .Where(x => x.TenantId == plan.TenantId && x.IsActive && x.Role == AppRoles.Admin)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var recipients = admins
            .Concat(new[] { lapsed.AccountManagerUserId, lapsed.RenewalOwnerUserId }.Where(x => x.HasValue).Select(x => x!.Value))
            .Distinct()
            .ToList();
        if (recipients.Count == 0)
            return;

        var assetName = plan.Asset?.AssetName ?? "an asset";
        await dispatcher.NotifyAsync(new NotificationRequest(
            plan.TenantId, NotificationKey, recipients,
            $"PM on hold: {assetName}",
            $"{plan.HoldReason} Renew the contract, raise a chargeable visit, or keep the schedule going without a contract.",
            $"/maintenance/schedules/{plan.Id}",
            "PreventiveMaintenancePlan", plan.Id,
            new Dictionary<string, string?>
            {
                ["AssetName"] = assetName,
                ["ContractNumber"] = lapsed.ContractNumber,
                ["Reason"] = plan.HoldReason,
            },
            NotificationSeverities.Warning), cancellationToken);
    }
}
