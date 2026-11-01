using Ecosys.Domain.Entities;
using Ecosys.Domain.Enums;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ecosys.Tests;

/// <summary>Contract PM stops when the contract does, and picks up again when cover returns.</summary>
public sealed class PmContractLapseTests
{
    private readonly Guid tenantId = Guid.NewGuid();
    private static readonly DateTime Today = DateTime.UtcNow.Date;

    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static PmSchedulerWorker Worker() => new(null!, NullLogger<PmSchedulerWorker>.Instance);

    private sealed record Fixture(Client Client, Asset Asset, PreventiveMaintenancePlan Plan, User Admin);

    private async Task<Fixture> Seed(AppDbContext db, Action<Contract>? contract = null, Action<PreventiveMaintenancePlan>? plan = null)
    {
        var client = new Client { TenantId = tenantId, ClientName = "Nairobi Hospital", IsActive = true };
        var asset = new Asset { TenantId = tenantId, ClientId = client.Id, AssetName = "Chiller 2", AssetCode = "CH-2", AssetType = "HVAC", Status = "Active" };
        var admin = new User { TenantId = tenantId, FullName = "Admin", Email = "admin@test.local", Role = AppRoles.Admin, IsActive = true };
        var pm = new PreventiveMaintenancePlan
        {
            TenantId = tenantId, AssetId = asset.Id, Frequency = "Quarterly", Status = "Active", AutoSchedule = true,
            NextPmDate = Today.AddDays(2), DaysBeforeDue = 3,
        };
        plan?.Invoke(pm);
        db.AddRange(client, asset, admin, pm);
        if (contract is not null)
        {
            var c = new Contract
            {
                TenantId = tenantId, ClientId = client.Id, ContractNumber = "CT-100", Name = "Chiller cover", Status = ContractStatus.Expired,
                StartDate = Today.AddYears(-1), EndDate = Today.AddDays(-20), AccountManagerUserId = Guid.NewGuid(),
            };
            c.Assets.Add(new ContractAsset { TenantId = tenantId, AssetId = asset.Id });
            contract(c);
            db.Add(c);
        }
        await db.SaveChangesAsync();
        return new Fixture(client, asset, pm, admin);
    }

    private Contract Renewal(Guid clientId, Guid assetId)
    {
        var c = new Contract
        {
            TenantId = tenantId, ClientId = clientId, ContractNumber = "CT-101", Name = "Chiller cover (renewed)", Status = ContractStatus.Active,
            StartDate = Today.AddDays(-1), EndDate = Today.AddYears(1),
        };
        c.Assets.Add(new ContractAsset { TenantId = tenantId, AssetId = assetId });
        return c;
    }

    [Fact]
    public async Task LapsedContract_HoldsTheSchedule_RaisesNothing_AndTellsAdminsAndTheAccountManager()
    {
        await using var db = Db();
        Contract? lapsed = null;
        var f = await Seed(db, c => lapsed = c);
        var dispatcher = new RecordingDispatcher();

        var result = await Worker().RunPlansAsync(db, dispatcher, DateTime.UtcNow, default);

        Assert.Equal((0, 1, 0), result);
        Assert.Empty(db.WorkOrders);
        var plan = await db.PreventiveMaintenancePlans.SingleAsync();
        Assert.Equal(PmContractGuard.HeldStatus, plan.Status);
        Assert.Equal(lapsed!.Id, plan.LapsedContractId);
        Assert.Contains("CT-100 ended on", plan.HoldReason);
        var sent = Assert.Single(dispatcher.Sent);
        Assert.Equal(PmContractGuard.NotificationKey, sent.Key);
        Assert.Contains(f.Admin.Id, sent.UserIds);
        Assert.Contains(lapsed.AccountManagerUserId!.Value, sent.UserIds);

        // Still lapsed on the next run: no second notice, still nothing raised.
        Assert.Equal((0, 0, 0), await Worker().RunPlansAsync(db, dispatcher, DateTime.UtcNow, default));
        Assert.Single(dispatcher.Sent);
        Assert.Empty(db.WorkOrders);
    }

    [Fact]
    public async Task ContractEndingBeforeTheDueDate_HoldsEvenThoughItIsStillRunningToday()
    {
        await using var db = Db();
        await Seed(db, c => { c.Status = ContractStatus.ExpiringSoon; c.EndDate = Today.AddDays(1); });

        await Worker().RunPlansAsync(db, null, DateTime.UtcNow, default);

        Assert.Equal(PmContractGuard.HeldStatus, (await db.PreventiveMaintenancePlans.SingleAsync()).Status);
        Assert.Empty(db.WorkOrders);
    }

    [Fact]
    public async Task HeldSchedule_ResumesOnRenewal_AndRaisesTheMissedVisitToday()
    {
        await using var db = Db();
        var f = await Seed(db, _ => { }, p => p.NextPmDate = Today.AddDays(-10));
        await Worker().RunPlansAsync(db, null, DateTime.UtcNow, default);
        Assert.Equal(PmContractGuard.HeldStatus, (await db.PreventiveMaintenancePlans.SingleAsync()).Status);

        db.Add(Renewal(f.Client.Id, f.Asset.Id));
        await db.SaveChangesAsync();
        var result = await Worker().RunPlansAsync(db, null, DateTime.UtcNow, default);

        Assert.Equal((1, 0, 1), result);
        var plan = await db.PreventiveMaintenancePlans.SingleAsync();
        Assert.Equal("Active", plan.Status);
        Assert.Null(plan.HoldReason);
        Assert.Equal(Today, plan.NextPmDate!.Value.Date);
        var job = Assert.Single(db.WorkOrders);
        Assert.Equal(Today, job.DueDate!.Value.Date);
    }

    [Fact]
    public async Task AssetNeverUnderContract_IsScheduledAsBefore()
    {
        await using var db = Db();
        await Seed(db);

        Assert.Equal((1, 0, 0), await Worker().RunPlansAsync(db, null, DateTime.UtcNow, default));
        Assert.Equal("Active", (await db.PreventiveMaintenancePlans.SingleAsync()).Status);
    }

    [Fact]
    public async Task ActiveContract_AndGracePeriod_KeepVisitsGoing()
    {
        await using var active = Db();
        await Seed(active, c => { c.Status = ContractStatus.Active; c.EndDate = Today.AddMonths(6); });
        Assert.Equal((1, 0, 0), await Worker().RunPlansAsync(active, null, DateTime.UtcNow, default));

        await using var grace = Db();
        await Seed(grace, c => { c.EndDate = Today.AddDays(-5); c.GracePeriodDays = 14; });
        Assert.Equal((1, 0, 0), await Worker().RunPlansAsync(grace, null, DateTime.UtcNow, default));
    }

    [Fact]
    public async Task ContinueWithoutContract_RaisesChargeableVisitsInsteadOfHolding()
    {
        await using var db = Db();
        await Seed(db, _ => { }, p => p.ContinueWithoutContract = true);

        Assert.Equal((1, 0, 0), await Worker().RunPlansAsync(db, null, DateTime.UtcNow, default));
        var job = Assert.Single(db.WorkOrders);
        Assert.Contains("chargeable", job.Title);
        Assert.Contains("CT-100", job.Description);
    }

    [Fact]
    public async Task ADraftOrCancelledContract_NeverGaveCover_SoItDoesNotHold()
    {
        await using var db = Db();
        await Seed(db, c => c.Status = ContractStatus.Cancelled);

        Assert.Equal((1, 0, 0), await Worker().RunPlansAsync(db, null, DateTime.UtcNow, default));
    }

    private sealed class RecordingDispatcher : INotificationDispatcher
    {
        public List<NotificationRequest> Sent { get; } = [];
        public Task NotifyAsync(NotificationRequest request, CancellationToken cancellationToken = default)
        {
            Sent.Add(request);
            return Task.CompletedTask;
        }
    }
}
