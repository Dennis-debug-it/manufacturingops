using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

public interface IPreventiveMaintenancePlanService
{
    /// <summary>
    /// Keeps the asset's own PM schedule in line with its maintenance fields: with auto-schedule on
    /// (and a frequency and next due date), one auto-scheduled plan; with it off, that plan stops.
    /// </summary>
    Task SyncForAssetAsync(Asset asset, CancellationToken cancellationToken = default);
}

internal sealed class PreventiveMaintenancePlanService(AppDbContext dbContext) : IPreventiveMaintenancePlanService
{
    public async Task SyncForAssetAsync(Asset asset, CancellationToken cancellationToken = default)
    {
        // An asset can have more than one auto-scheduled plan (e.g. one from the schedule wizard);
        // the asset's own is the oldest. SingleOrDefault threw here once there were two.
        var existingAutoPlan = await dbContext.PreventiveMaintenancePlans
            .Where(x => x.AssetId == asset.Id && x.AutoSchedule)
            .OrderBy(x => x.PmTemplateId.HasValue)
            .ThenBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (!asset.AutoSchedulePm || string.IsNullOrWhiteSpace(asset.RecommendedPmFrequency) || !asset.NextPmDate.HasValue)
        {
            if (existingAutoPlan is not null)
            {
                existingAutoPlan.Status = "Inactive";
                existingAutoPlan.AutoSchedule = false;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        // Turning auto-schedule back on picks up the plan it switched off, keeping its history,
        // rather than starting a second one.
        existingAutoPlan ??= await dbContext.PreventiveMaintenancePlans
            .Where(x => x.AssetId == asset.Id && !x.AutoSchedule && x.PmTemplateId == null)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingAutoPlan is null)
        {
            existingAutoPlan = new PreventiveMaintenancePlan
            {
                TenantId = asset.TenantId,
                BranchId = asset.BranchId,
                AssetId = asset.Id,
                AutoSchedule = true
            };

            dbContext.PreventiveMaintenancePlans.Add(existingAutoPlan);
        }

        existingAutoPlan.AutoSchedule = true;
        existingAutoPlan.Frequency = asset.RecommendedPmFrequency;
        existingAutoPlan.BranchId = asset.BranchId;
        existingAutoPlan.LastPmDate = asset.LastPmDate;
        existingAutoPlan.NextPmDate = asset.NextPmDate;
        // A plan on hold for a lapsed contract stays on hold; the scheduler resumes it when cover returns.
        if (existingAutoPlan.Status != PmContractGuard.HeldStatus)
            existingAutoPlan.Status = "Active";

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
