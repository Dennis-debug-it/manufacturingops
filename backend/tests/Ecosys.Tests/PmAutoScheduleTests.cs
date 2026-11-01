using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ecosys.Tests;

/// <summary>Auto-schedule off means manual trigger only, and the asset's switch drives its own schedule.</summary>
public sealed class PmAutoScheduleTests
{
    private readonly Guid tenantId = Guid.NewGuid();
    private static readonly DateTime Today = DateTime.UtcNow.Date;

    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private Asset NewAsset(bool autoSchedule) => new()
    {
        TenantId = tenantId, ClientId = Guid.NewGuid(), AssetName = "Generator", AssetCode = "GEN-1", Status = "Active",
        RecommendedPmFrequency = "Quarterly", NextPmDate = Today.AddDays(1), AutoSchedulePm = autoSchedule,
    };

    [Fact]
    public async Task Scheduler_RaisesOnlyAutoScheduledPlans()
    {
        await using var db = Db();
        var asset = NewAsset(false);
        PreventiveMaintenancePlan Plan(bool auto) => new() { TenantId = tenantId, AssetId = asset.Id, Frequency = "Quarterly", Status = "Active", AutoSchedule = auto, NextPmDate = Today.AddDays(1), DaysBeforeDue = 3 };
        var manual = Plan(false);
        var automatic = Plan(true);
        db.AddRange(asset, manual, automatic);
        await db.SaveChangesAsync();

        var result = await new PmSchedulerWorker(null!, NullLogger<PmSchedulerWorker>.Instance).RunPlansAsync(db, null, DateTime.UtcNow, default);

        Assert.Equal(1, result.Generated);
        Assert.Equal(automatic.Id, Assert.Single(db.WorkOrders).PreventiveMaintenancePlanId);
    }

    [Fact]
    public async Task AssetSwitch_OffThenOn_ReusesTheSamePlan()
    {
        await using var db = Db();
        var asset = NewAsset(true);
        db.Add(asset);
        await db.SaveChangesAsync();
        var sync = new PreventiveMaintenancePlanService(db);

        await sync.SyncForAssetAsync(asset);
        var plan = Assert.Single(db.PreventiveMaintenancePlans);
        Assert.True(plan.AutoSchedule);

        asset.AutoSchedulePm = false;
        await sync.SyncForAssetAsync(asset);
        Assert.False(plan.AutoSchedule);

        asset.AutoSchedulePm = true;
        await sync.SyncForAssetAsync(asset);
        Assert.Equal(plan.Id, Assert.Single(db.PreventiveMaintenancePlans).Id);
        Assert.True(plan.AutoSchedule);
        Assert.Equal("Active", plan.Status);
    }

    [Fact]
    public async Task AssetSync_CopesWithASecondAutoScheduledPlan()
    {
        await using var db = Db();
        var asset = NewAsset(true);
        var own = new PreventiveMaintenancePlan { TenantId = tenantId, AssetId = asset.Id, Frequency = "Monthly", Status = "Active", AutoSchedule = true, CreatedAt = DateTime.UtcNow.AddDays(-5) };
        var wizard = new PreventiveMaintenancePlan { TenantId = tenantId, AssetId = asset.Id, Frequency = "Annual", Status = "Active", AutoSchedule = true, PmTemplateId = Guid.NewGuid() };
        db.AddRange(asset, own, wizard);
        await db.SaveChangesAsync();

        await new PreventiveMaintenancePlanService(db).SyncForAssetAsync(asset);

        Assert.Equal("Quarterly", own.Frequency);
        Assert.Equal("Annual", wizard.Frequency);
    }
}
