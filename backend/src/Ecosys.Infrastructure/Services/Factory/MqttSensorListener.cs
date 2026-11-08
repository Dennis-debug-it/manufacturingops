using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;

namespace Ecosys.Infrastructure.Services.Factory;

/// <summary>The MQTT broker the sensor listener subscribes to (section "Mqtt"). Off unless enabled.</summary>
public sealed class MqttSensorOptions
{
    public const string SectionName = "Mqtt";

    public bool Enabled { get; set; }
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1883;
    public bool UseTls { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string ClientId { get; set; } = "ecosys-sensors";
    /// <summary>Readings arrive on "{TopicPrefix}/{gatewayId}/{tag}".</summary>
    public string TopicPrefix { get; set; } = "ecosys/sensors";
}

/// <summary>
/// Subscribes to the broker and feeds each message to <see cref="ISensorFeed"/> as its gateway.
/// Runs on one instance only (the single-instance lease), so readings are stored once. A bad
/// message is logged on its gateway and dropped; a lost connection is retried with back-off.
/// </summary>
public sealed class MqttSensorListener(
    IServiceScopeFactory scopeFactory,
    IOptions<MqttSensorOptions> options,
    ILogger<MqttSensorListener> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return;
        }

        var factory = new MqttFactory();
        using var client = factory.CreateMqttClient();
        client.ApplicationMessageReceivedAsync += async args =>
        {
            args.AutoAcknowledge = true;
            await HandleAsync(settings.TopicPrefix, args.ApplicationMessage.Topic, args.ApplicationMessage.PayloadSegment, stoppingToken);
        };

        var delay = TimeSpan.FromSeconds(2);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!client.IsConnected)
                {
                    var builder = new MqttClientOptionsBuilder()
                        .WithTcpServer(settings.Host, settings.Port)
                        .WithClientId(settings.ClientId)
                        .WithCleanSession(false);
                    if (!string.IsNullOrEmpty(settings.Username))
                        builder = builder.WithCredentials(settings.Username, settings.Password);
                    if (settings.UseTls)
                        builder = builder.WithTlsOptions(o => o.UseTls());

                    await client.ConnectAsync(builder.Build(), stoppingToken);
                    await client.SubscribeAsync(factory.CreateSubscribeOptionsBuilder()
                        .WithTopicFilter(f => f.WithTopic($"{settings.TopicPrefix.Trim('/')}/#").WithAtLeastOnceQoS())
                        .Build(), stoppingToken);
                    logger.LogInformation("Sensor listener connected to {Host}:{Port} on {Prefix}/#.", settings.Host, settings.Port, settings.TopicPrefix);
                    delay = TimeSpan.FromSeconds(2);
                }

                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Sensor listener could not reach the MQTT broker; retrying in {Delay}.", delay);
                await Task.Delay(delay, stoppingToken).ContinueWith(_ => { }, CancellationToken.None);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 300));
            }
        }

        if (client.IsConnected)
        {
            await client.DisconnectAsync(cancellationToken: CancellationToken.None);
        }
    }

    internal async Task HandleAsync(string prefix, string topic, ArraySegment<byte> payload, CancellationToken cancellationToken)
    {
        try
        {
            if (!MqttSensorMessages.TryParse(prefix, topic, payload, out var message, out var error))
            {
                logger.LogDebug("Ignored MQTT message on {Topic}: {Error}", topic, error);
                return;
            }

            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var feed = scope.ServiceProvider.GetRequiredService<ISensorFeed>();
            var gateway = await dbContext.SensorGateways.IgnoreQueryFilters()
                .SingleOrDefaultAsync(x => x.Id == message!.GatewayId && x.IsActive && x.Tenant!.IsActive, cancellationToken);
            if (gateway is null)
            {
                logger.LogDebug("Ignored MQTT message on {Topic}: no active gateway.", topic);
                return;
            }

            if (!gateway.MqttTrustBroker && (message!.Key is null || !SensorKeys.Matches(message.Key, gateway.KeyHash)))
            {
                gateway.RejectedCount++;
                gateway.LastError = $"{message!.Tag}: MQTT message without this gateway's key.";
                gateway.LastErrorAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            await feed.IngestAsync(gateway, [new SensorSample(message!.Tag, message.Value, message.At)], cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not store the MQTT reading on {Topic}.", topic);
        }
    }
}
