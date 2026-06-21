using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

public interface IPreventiveMaintenancePlanService
{
    Task SyncForAssetAsync(Asset asset, CancellationToken cancellationToken = default);
}

internal sealed class PreventiveMaintenancePlanService(AppDbContext dbContext) : IPreventiveMaintenancePlanService
{
    public async Task SyncForAssetAsync(Asset asset, CancellationToken cancellationToken = default)
    {
        var existingAutoPlan = await dbContext.PreventiveMaintenancePlans
            .SingleOrDefaultAsync(x => x.AssetId == asset.Id && x.AutoSchedule, cancellationToken);

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

        existingAutoPlan.Frequency = asset.RecommendedPmFrequency;
        existingAutoPlan.BranchId = asset.BranchId;
        existingAutoPlan.LastPmDate = asset.LastPmDate;
        existingAutoPlan.NextPmDate = asset.NextPmDate;
        existingAutoPlan.Status = "Active";

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
