using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ecosys.Tests.Factory;

public sealed class AssetAlertTests
{
    [Fact]
    public async Task WarrantyWarning_GoesOnceAheadAndOnceOnTheDay_ToManagersWhoCanSeeTheAsset()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var today = new DateTime(2026, 11, 5, 3, 0, 0, DateTimeKind.Utc);
        site.Ours.WarrantyExpiryDate = today.Date.AddDays(20);
        site.Theirs.WarrantyExpiryDate = today.Date.AddDays(10);
        db.Assets.Add(new Asset { TenantId = site.TenantId, AssetName = "Far off", AssetCode = "FAR", OrgNodeId = site.Chelal, WarrantyExpiryDate = today.Date.AddDays(90) });
        await db.SaveChangesAsync();
        var dispatcher = new RecordingDispatcher();
        var runner = new AssetAlertRunner(db, dispatcher, NullLogger<AssetAlertRunner>.Instance);

        Assert.Equal(2, await runner.RunAsync(today));
        var adminMessage = Assert.Single(dispatcher.Sent, x => x.UserIds.Contains(site.AdminId));
        Assert.Equal("2 warranties ending soon", adminMessage.Title);
        var engineerMessage = Assert.Single(dispatcher.Sent, x => x.UserIds.Contains(site.EngineerId));
        Assert.Equal("Warranty ending: Our dryer", engineerMessage.Title);
        Assert.DoesNotContain(dispatcher.Sent, x => x.Body.Contains("Far off", StringComparison.Ordinal));

        // Running again the same day, or the next, sends nothing new.
        dispatcher.Sent.Clear();
        Assert.Equal(0, await runner.RunAsync(today.AddHours(5)));
        Assert.Equal(0, await runner.RunAsync(today.AddDays(1)));

        // On the day it ends, one more warning.
        Assert.Equal(1, await runner.RunAsync(today.AddDays(10)));
        Assert.Contains("ends today", Assert.Single(dispatcher.Sent).Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OutOfRangeReading_NotifiesAssetManagers()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var meter = new Meter { TenantId = site.TenantId, AssetId = site.Ours.Id, Name = "Bearing temperature", MeterType = MeterTypes.Temperature, Unit = "°C", IsCumulative = false, MaxThreshold = 80m };
        db.Meters.Add(meter);
        await db.SaveChangesAsync();
        var dispatcher = new RecordingDispatcher();
        var context = new FactoryTenantContext(site.TenantId, site.AdminId);
        var meters = FactoryAssetRegisterTests.WithHttp(new MetersController(db, context, FactoryTestSupport.Access(context), FactoryTestSupport.Scope(db, context),
            FactoryTestSupport.Settings(db), new MeterReadingService(db), FactoryTestSupport.Audit(db), dispatcher));

        await meters.Record(meter.Id, new RecordMeterReadingRequest(65m, null), CancellationToken.None);
        Assert.Empty(dispatcher.Sent);

        await meters.Record(meter.Id, new RecordMeterReadingRequest(92.5m, null), CancellationToken.None);
        var alert = Assert.Single(dispatcher.Sent);
        Assert.Equal(AssetAlerts.MeterOutOfRange, alert.Key);
        Assert.Equal("Our dryer: Bearing temperature 92.5 °C", alert.Title);
        Assert.Contains(site.AdminId, alert.UserIds);
        Assert.Contains(site.EngineerId, alert.UserIds);
        Assert.Equal("Drying", alert.TemplateValues["Location"]);
    }

    private sealed record Seeded(Guid TenantId, Guid AdminId, Guid EngineerId, Guid Chelal, Asset Ours, Asset Theirs);

    private static async Task<Seeded> SeedAsync(AppDbContext db)
    {
        var (tenant, admin) = await FactoryTestSupport.SeedTenantAsync(db);
        var chelal = new OrgNode { TenantId = tenant.Id, Level = OrgNodeLevels.Factory, Name = "Chelal", Code = "CHL" };
        var drying = new OrgNode { TenantId = tenant.Id, ParentId = chelal.Id, Level = OrgNodeLevels.Section, Name = "Drying", Code = "CHL-DRY" };
        var momul = new OrgNode { TenantId = tenant.Id, Level = OrgNodeLevels.Factory, Name = "Momul", Code = "MML" };
        var ours = new Asset { TenantId = tenant.Id, AssetName = "Our dryer", AssetCode = "CHL-FBD", OrgNodeId = drying.Id };
        var theirs = new Asset { TenantId = tenant.Id, AssetName = "Their dryer", AssetCode = "MML-FBD", OrgNodeId = momul.Id };

        // A factory engineer whose role has "manage assets", limited to Chelal; and a fitter without it.
        var engineer = FactoryTestSupport.NewUser(tenant.Id, "engineer@chelal.test", AppRoles.Manager);
        var fitter = FactoryTestSupport.NewUser(tenant.Id, "fitter@chelal.test", AppRoles.Technician);
        db.RolePermissions.Add(new RolePermission { TenantId = tenant.Id, Role = AppRoles.Manager, CanManageAssets = true });
        db.OrgNodes.AddRange(chelal, drying, momul);
        db.Assets.AddRange(ours, theirs);
        db.Users.AddRange(engineer, fitter);
        db.UserOrgNodeAssignments.Add(new UserOrgNodeAssignment { TenantId = tenant.Id, UserId = engineer.Id, OrgNodeId = chelal.Id });
        await db.SaveChangesAsync();
        return new Seeded(tenant.Id, admin.Id, engineer.Id, chelal.Id, ours, theirs);
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
