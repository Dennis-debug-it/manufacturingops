using System.Net;
using System.Net.Sockets;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services.Factory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Server;

namespace Ecosys.Tests.Factory;

/// <summary>The MQTT listener against a real (embedded) broker: connect, subscribe, store, refuse.</summary>
public sealed class MqttSensorListenerTests
{
    [Fact]
    public async Task PublishedReadings_AreStored_AndMessagesWithoutTheKeyAreRefused()
    {
        var port = FreePort();
        var factory = new MqttFactory();
        using var broker = factory.CreateMqttServer(new MqttServerOptionsBuilder().WithDefaultEndpoint().WithDefaultEndpointPort(port)
            .WithDefaultEndpointBoundIPAddress(IPAddress.Loopback).WithDefaultEndpointBoundIPV6Address(IPAddress.None).Build());
        await broker.StartAsync();

        var databaseName = $"mqtt-{Guid.NewGuid():N}";
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddScoped<ISensorFeed>(provider =>
        {
            var db = provider.GetRequiredService<AppDbContext>();
            var context = new FactoryTenantContext(Guid.Empty, Guid.Empty);
            return new SensorFeed(db, new MeterReadingService(db), FactoryTestSupport.Monitor(db, context), FactoryTestSupport.Scope(db, context));
        });
        await using var provider = services.BuildServiceProvider();

        Guid gatewayId;
        string key;
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var (tenant, _) = await FactoryTestSupport.SeedTenantAsync(db);
            var asset = new Asset { TenantId = tenant.Id, AssetName = "Withering trough 3", AssetCode = "WT-3", Status = "Active" };
            (key, var prefix, var hash) = SensorKeys.Generate();
            var gateway = new SensorGateway { TenantId = tenant.Id, Name = "Node-RED", KeyPrefix = prefix, KeyHash = hash, SampleSeconds = 0 };
            db.AddRange(asset, gateway, new Meter { TenantId = tenant.Id, AssetId = asset.Id, Name = "Air temperature", MeterType = MeterTypes.Temperature, Unit = "°C", IsCumulative = false, ExternalId = "wt3/air" });
            await db.SaveChangesAsync();
            gatewayId = gateway.Id;
        }

        var options = Options.Create(new MqttSensorOptions { Enabled = true, Host = "127.0.0.1", Port = port, ClientId = "ecosys-test", TopicPrefix = "ecosys/sensors" });
        var log = new ListLogger();
        using var listener = new MqttSensorListener(provider.GetRequiredService<IServiceScopeFactory>(), options, log);
        await listener.StartAsync(CancellationToken.None);
        try
        {
            using var publisher = factory.CreateMqttClient();
            await publisher.ConnectAsync(new MqttClientOptionsBuilder().WithTcpServer("127.0.0.1", port).WithClientId("node-red").Build());
            // The listener logs "connected" once its subscription is in place.
            await WaitForAsync(() => { lock (log.Lines) return Task.FromResult(log.Lines.Any(x => x.Contains("connected", StringComparison.Ordinal))); });

            var topic = $"ecosys/sensors/{gatewayId}/wt3/air";
            await publisher.PublishStringAsync(topic, "31.5");                                                          // no key: refused
            await publisher.PublishStringAsync(topic, $"{{\"value\": 32.5, \"key\": \"{key}\"}}");                    // stored
            await publisher.PublishStringAsync($"ecosys/sensors/{Guid.NewGuid()}/wt3/air", $"{{\"value\": 1, \"key\": \"{key}\"}}"); // unknown gateway

            await WaitForAsync(async () =>
            {
                await using var scope = provider.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                return await db.MeterReadings.AnyAsync() && (await db.SensorGateways.SingleAsync()).RejectedCount == 1;
            });

            await using var check = provider.CreateAsyncScope();
            var checkDb = check.ServiceProvider.GetRequiredService<AppDbContext>();
            var reading = await checkDb.MeterReadings.SingleAsync();
            Assert.Equal((32.5m, MeterReadingSources.Sensor), (reading.Value, reading.Source));
            var gatewayAfter = await checkDb.SensorGateways.SingleAsync();
            Assert.Equal((1L, 1L), (gatewayAfter.AcceptedCount, gatewayAfter.RejectedCount));
            Assert.Contains("without this gateway's key", gatewayAfter.LastError);
        }
        finally
        {
            await listener.StopAsync(CancellationToken.None);
            await broker.StopAsync();
        }
    }

    private static int FreePort()
    {
        using var socket = new TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        return ((IPEndPoint)socket.LocalEndpoint).Port;
    }

    private static async Task WaitForAsync(Func<Task<bool>> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(15);
        while (!await condition())
        {
            if (DateTime.UtcNow > until)
                throw new TimeoutException("The condition was not met in time.");
            await Task.Delay(100);
        }
    }

    private sealed class ListLogger : Microsoft.Extensions.Logging.ILogger<MqttSensorListener>
    {
        public List<string> Lines { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (Lines) Lines.Add($"{logLevel}: {formatter(state, exception)} {exception}");
        }
    }
}
