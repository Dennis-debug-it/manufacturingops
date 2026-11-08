using System.Net;
using System.Net.Http.Json;
using System.Text;
using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Options;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ecosys.Tests.Factory;

public sealed class SensorFeedTests
{
    [Fact]
    public async Task Readings_LandOnTheTaggedMeter_SteadyOnesAreSampled_AndAlarmsAlwaysGetThrough()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var gateway = await AddGatewayAsync(db, site, sampleSeconds: 300);
        var feed = Feed(db, site);
        var start = DateTime.UtcNow.AddHours(-1);

        var result = await feed.IngestAsync(gateway, [
            new SensorSample("BLR1.PRESSURE", 8.0m, start),
            new SensorSample("BLR1.PRESSURE", 8.1m, start.AddSeconds(30)),   // steady, inside 5 min: not stored
            new SensorSample("BLR1.PRESSURE", 8.2m, start.AddMinutes(6)),    // steady, after 5 min: stored
            new SensorSample("BLR1.PRESSURE", 10.6m, start.AddMinutes(7)),   // alarm: stored at once
            new SensorSample("BLR1.PRESSURE", 10.7m, start.AddMinutes(7.5)), // still in alarm, inside 5 min: not stored
            new SensorSample("BLR1.PRESSURE", 8.0m, start.AddMinutes(8)),    // back to normal: stored at once
        ]);

        Assert.Equal((4, 2, 0), (result.Accepted, result.SampledOut, result.Skipped));
        Assert.Empty(result.Rejected);
        var readings = await db.MeterReadings.OrderBy(x => x.ReadAt).ToListAsync();
        Assert.Equal([8.0m, 8.2m, 10.6m, 8.0m], readings.Select(x => x.Value));
        Assert.All(readings, x => Assert.Equal((MeterReadingSources.Sensor, "Boiler PLC"), (x.Source, x.RecordedByName)));
        var excursion = await db.ConditionEvents.SingleAsync();
        Assert.Equal((ConditionLevels.Alarm, ConditionEventStatuses.Cleared), (excursion.Level, excursion.Status));
        Assert.NotNull(excursion.WorkOrderId);

        var stats = await db.SensorGateways.SingleAsync();
        Assert.Equal((4L, 0L), (stats.AcceptedCount, stats.RejectedCount));
        Assert.NotNull(stats.LastSeenAt);
    }

    [Fact]
    public async Task RepeatsAreSkipped_AndBadReadingsAreReportedWithoutStoppingTheRest()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var gateway = await AddGatewayAsync(db, site, sampleSeconds: 0);
        var feed = Feed(db, site);
        var at = DateTime.UtcNow.AddMinutes(-5);
        await feed.IngestAsync(gateway, [new SensorSample("BLR1.PRESSURE", 8m, at)]);

        var result = await feed.IngestAsync(gateway, [
            new SensorSample("BLR1.PRESSURE", 8m, at),                     // a repeat
            new SensorSample("NOPE", 1m, at.AddMinutes(1)),
            new SensorSample("BLR1.PRESSURE", 9m, DateTime.UtcNow.AddHours(2)),
            new SensorSample("", 1m),
            new SensorSample("BLR1.PRESSURE", 8.5m, at.AddMinutes(2)),
        ]);

        Assert.Equal((1, 0, 1), (result.Accepted, result.SampledOut, result.Skipped));
        // Oldest first: an undated reading counts as "now", before one dated two hours ahead.
        Assert.Equal(["NOPE", "", "BLR1.PRESSURE"], result.Rejected.Select(x => x.Tag));
        Assert.Contains("future", result.Rejected[2].Reason);
        var stats = await db.SensorGateways.SingleAsync();
        Assert.Equal((2L, 3L), (stats.AcceptedCount, stats.RejectedCount));
        Assert.StartsWith("NOPE:", stats.LastError);

        await Assert.ThrowsAsync<BusinessRuleException>(() => feed.IngestAsync(gateway,
            Enumerable.Range(0, 501).Select(i => new SensorSample("BLR1.PRESSURE", 1m)).ToList()));
    }

    [Fact]
    public async Task AGatewayAtOneFactory_CannotFeedMetersElsewhere_AndSharedTagsAreRefused()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var other = new OrgNode { TenantId = site.TenantId, Level = OrgNodeLevels.Factory, Name = "Kapset", Code = "KPS" };
        var otherAsset = new Asset { TenantId = site.TenantId, AssetName = "Kapset boiler", AssetCode = "KPS-BLR", OrgNodeId = other.Id };
        db.AddRange(other, otherAsset,
            new Meter { TenantId = site.TenantId, AssetId = otherAsset.Id, Name = "Pressure", MeterType = MeterTypes.Pressure, Unit = "bar", IsCumulative = false, ExternalId = "KPS.PRESSURE" },
            new Meter { TenantId = site.TenantId, AssetId = site.Asset.Id, Name = "Temp A", MeterType = MeterTypes.Temperature, Unit = "°C", IsCumulative = false, ExternalId = "DUP" },
            new Meter { TenantId = site.TenantId, AssetId = site.Asset.Id, Name = "Temp B", MeterType = MeterTypes.Temperature, Unit = "°C", IsCumulative = false, ExternalId = "DUP" });
        await db.SaveChangesAsync();
        var gateway = await AddGatewayAsync(db, site, sampleSeconds: 0, orgNodeId: site.FactoryId);

        var result = await Feed(db, site).IngestAsync(gateway, [new SensorSample("KPS.PRESSURE", 5m), new SensorSample("DUP", 50m)]);

        Assert.Equal(0, result.Accepted);
        Assert.Contains("location", result.Rejected[0].Reason);
        Assert.Contains("2 meters share this tag", result.Rejected[1].Reason);
    }

    [Fact]
    public async Task ACounterThatGoesDown_IsTakenAsAReset()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        db.Meters.Add(new Meter { TenantId = site.TenantId, AssetId = site.Asset.Id, Name = "Odometer", MeterType = MeterTypes.Odometer, Unit = "km", IsCumulative = true, ExternalId = "KCA123A.ODO" });
        await db.SaveChangesAsync();
        var gateway = await AddGatewayAsync(db, site, sampleSeconds: 0);
        var start = DateTime.UtcNow.AddHours(-2);

        await Feed(db, site).IngestAsync(gateway, [new SensorSample("KCA123A.ODO", 152_000m, start), new SensorSample("KCA123A.ODO", 120m, start.AddHours(1))]);

        var reset = await db.MeterReadings.OrderBy(x => x.ReadAt).LastAsync();
        Assert.True(reset.IsReset);
        Assert.Equal(0m, reset.Delta);
    }

    [Fact]
    public async Task Keys_AreHashed_AndOnlyAnActiveGatewaysKeyAuthenticates()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var gateways = FactoryAssetRegisterTests.WithHttp(new SensorGatewaysController(db, site.Context, FactoryTestSupport.Access(site.Context), FactoryTestSupport.Audit(db)));
        var created = Assert.IsType<SensorGatewayCreatedResponse>(Assert.IsType<OkObjectResult>((await gateways.Create(new SaveSensorGatewayRequest("Boiler PLC"), default)).Result).Value);
        var feed = Feed(db, site);

        Assert.StartsWith("sg_", created.Key);
        Assert.Equal(created.Key[..SensorKeys.PrefixLength], created.Gateway.KeyPrefix);
        Assert.DoesNotContain(created.Key, (await db.SensorGateways.SingleAsync()).KeyHash);
        Assert.NotNull(await feed.AuthenticateAsync(created.Key));
        Assert.Null(await feed.AuthenticateAsync(created.Key[..^1] + (created.Key[^1] == '0' ? '1' : '0')));
        Assert.Null(await feed.AuthenticateAsync(null));

        var rotated = Assert.IsType<SensorGatewayCreatedResponse>(Assert.IsType<OkObjectResult>((await gateways.RotateKey(created.Gateway.Id, default)).Result).Value);
        Assert.Null(await feed.AuthenticateAsync(created.Key));
        Assert.NotNull(await feed.AuthenticateAsync(rotated.Key));

        await gateways.Update(created.Gateway.Id, new SaveSensorGatewayRequest("Boiler PLC", IsActive: false), default);
        Assert.Null(await feed.AuthenticateAsync(rotated.Key));
        await Assert.ThrowsAsync<ConflictException>(() => gateways.Create(new SaveSensorGatewayRequest("boiler plc"), default));
    }

    [Fact]
    public async Task Controller_RefusesAWrongKey_AndStoresReadingsForTheRightOne()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var (key, prefix, hash) = SensorKeys.Generate();
        db.SensorGateways.Add(new SensorGateway { TenantId = site.TenantId, Name = "Boiler PLC", KeyPrefix = prefix, KeyHash = hash, SampleSeconds = 0 });
        await db.SaveChangesAsync();
        var controller = new SensorsController(Feed(db, site)) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var request = new SensorReadingsRequest([new SensorReadingInput("BLR1.PRESSURE", 8.4m, null)]);

        Assert.IsType<UnauthorizedObjectResult>((await controller.Post(request, default)).Result);

        controller.ControllerContext.HttpContext.Request.Headers[SensorsController.KeyHeader] = key;
        var result = Assert.IsType<SensorIngestResult>(Assert.IsType<OkObjectResult>((await controller.Post(request, default)).Result).Value);
        Assert.Equal(1, result.Accepted);

        var bearer = new SensorsController(Feed(db, site)) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        bearer.ControllerContext.HttpContext.Request.Headers.Authorization = $"Bearer {key}";
        Assert.IsType<OkObjectResult>((await bearer.Post(new SensorReadingsRequest([]), default)).Result);
    }

    [Theory]
    [InlineData("12.5", 12.5, null, null)]
    [InlineData("{\"value\": 7.25, \"at\": \"2026-11-08T06:00:00Z\", \"key\": \"sg_abc\"}", 7.25, "2026-11-08T06:00:00Z", "sg_abc")]
    [InlineData("{\"value\": \"3.5\"}", 3.5, null, null)]
    public void MqttPayloads_AreRead(string payload, double value, string? at, string? key)
    {
        var gateway = Guid.NewGuid();
        Assert.True(MqttSensorMessages.TryParse("ecosys/sensors", $"ecosys/sensors/{gateway}/line1/dryer/temp", Encoding.UTF8.GetBytes(payload), out var message, out _));
        Assert.Equal((gateway, "line1/dryer/temp", (decimal)value, key), (message!.GatewayId, message.Tag, message.Value, message.Key));
        Assert.Equal(at is null ? null : DateTime.Parse(at, null, System.Globalization.DateTimeStyles.AdjustToUniversal), message.At);
    }

    [Theory]
    [InlineData("other/sensors/{0}/tag", "12")]
    [InlineData("ecosys/sensors/not-a-guid/tag", "12")]
    [InlineData("ecosys/sensors/{0}", "12")]
    [InlineData("ecosys/sensors/{0}/tag", "hot")]
    [InlineData("ecosys/sensors/{0}/tag", "{\"temp\": 12}")]
    [InlineData("ecosys/sensors/{0}/tag", "{\"value\": 12, \"at\": \"yesterday\"}")]
    public void BadMqttMessages_AreRefused(string topic, string payload)
    {
        Assert.False(MqttSensorMessages.TryParse("ecosys/sensors/", string.Format(topic, Guid.NewGuid()), Encoding.UTF8.GetBytes(payload), out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    [Trait("Category", "PostgreSql")]
    public async Task HttpFeed_IsReachableWithoutASession_AndRefusesAWrongKey()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.UseSetting("ConnectionStrings:DefaultConnection", Environment.GetEnvironmentVariable("ECOSYS_TEST_DB_CONNECTION")
                ?? "Host=localhost;Port=5432;Database=ecosys_test;Username=postgres;Password=postgres");
            builder.UseSetting("Jwt:Issuer", "ecosys-tests");
            builder.UseSetting("Jwt:Audience", "ecosys-tests");
            builder.UseSetting("Jwt:SigningKey", "12345678901234567890123456789012");
            builder.UseSetting("PlatformAdmin:Email", "platform.owner@ecosys.test");
            builder.UseSetting("PlatformAdmin:Password", "ValidPlatformAdmin123!");
            builder.UseSetting("PlatformAdmin:FullName", "Platform Owner");
            builder.UseSetting("App:PublicUrl", "https://ecosys-tests.example.com");
            builder.UseSetting($"{AuthSecurityOptions.SectionName}:FailedLoginThreshold", "5");
            builder.UseSetting($"{AuthSecurityOptions.SectionName}:LockoutMinutes", "15");
        });
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/sensors/readings")
        {
            Content = JsonContent.Create(new { readings = new[] { new { tag = "X", value = 1 } } }),
        };
        request.Headers.Add(SensorsController.KeyHeader, "sg_0000000000000000000000000000000000000000");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("SENSOR_KEY_INVALID", await response.Content.ReadAsStringAsync());
    }

    private sealed record Site(Guid TenantId, Guid FactoryId, Asset Asset, FactoryTenantContext Context);

    private static async Task<Site> SeedAsync(AppDbContext db)
    {
        var (tenant, admin) = await FactoryTestSupport.SeedTenantAsync(db);
        var factory = new OrgNode { TenantId = tenant.Id, Level = OrgNodeLevels.Factory, Name = "Chelal", Code = "CHL" };
        var asset = new Asset { TenantId = tenant.Id, AssetName = "Boiler 1", AssetCode = "CHL-BLR1", OrgNodeId = factory.Id, Status = "Active" };
        db.AddRange(factory, asset, new Meter
        {
            TenantId = tenant.Id, AssetId = asset.Id, Name = "Steam pressure", MeterType = MeterTypes.Pressure, Unit = "bar", IsCumulative = false,
            ExternalId = "BLR1.PRESSURE", MaxThreshold = 10m, ConditionAction = ConditionActions.WorkOrder,
        });
        await db.SaveChangesAsync();
        return new Site(tenant.Id, factory.Id, asset, new FactoryTenantContext(tenant.Id, admin.Id));
    }

    private static async Task<SensorGateway> AddGatewayAsync(AppDbContext db, Site site, int sampleSeconds, Guid? orgNodeId = null)
    {
        var (_, prefix, hash) = SensorKeys.Generate();
        var gateway = new SensorGateway { TenantId = site.TenantId, Name = "Boiler PLC", KeyPrefix = prefix, KeyHash = hash, SampleSeconds = sampleSeconds, OrgNodeId = orgNodeId };
        db.SensorGateways.Add(gateway);
        await db.SaveChangesAsync();
        return gateway;
    }

    private static SensorFeed Feed(AppDbContext db, Site site) =>
        new(db, new MeterReadingService(db), FactoryTestSupport.Monitor(db, site.Context), FactoryTestSupport.Scope(db, site.Context));
}
