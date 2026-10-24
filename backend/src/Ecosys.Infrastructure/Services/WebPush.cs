using System.Buffers.Binary;
using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecosys.Infrastructure.Services;

/// <summary>
/// Optional fixed VAPID keys (base64url, as printed by any web-push key generator). When they are
/// not configured, a key pair is generated on first use and kept in platform_settings, so push works
/// with no setup. Configure them explicitly to share keys across environments.
/// </summary>
public sealed class WebPushOptions
{
    public const string SectionName = "WebPush";

    public string? PublicKey { get; set; }
    public string? PrivateKey { get; set; }
    /// <summary>Contact the push services can reach (mailto: or https:). Defaults to App:PublicUrl.</summary>
    public string? Subject { get; set; }
}

public sealed record VapidKeys(string PublicKey, string PrivateKey, string Subject);

public sealed record PushMessage(string Title, string Body, string Url, string Tag);

public interface IVapidKeyProvider
{
    Task<VapidKeys> GetAsync(CancellationToken cancellationToken = default);
}

internal sealed class VapidKeyProvider(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<WebPushOptions> options,
    IConfiguration configuration) : IVapidKeyProvider
{
    private const string SettingCategory = "web-push-vapid";
    private readonly SemaphoreSlim gate = new(1, 1);
    private VapidKeys? cached;

    public async Task<VapidKeys> GetAsync(CancellationToken cancellationToken = default)
    {
        if (cached is not null) return cached;

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (cached is not null) return cached;

            var subject = options.Value.Subject;
            if (string.IsNullOrWhiteSpace(subject))
            {
                subject = configuration["App:PublicUrl"];
            }
            if (string.IsNullOrWhiteSpace(subject))
            {
                subject = "https://app.ecosysdigital.co.ke";
            }

            if (!string.IsNullOrWhiteSpace(options.Value.PublicKey) && !string.IsNullOrWhiteSpace(options.Value.PrivateKey))
            {
                return cached = new VapidKeys(options.Value.PublicKey.Trim(), options.Value.PrivateKey.Trim(), subject);
            }

            using var scope = serviceScopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await LoadAsync(db, cancellationToken);
            if (stored is null)
            {
                var (publicKey, privateKey) = WebPushCrypto.GenerateVapidKeys();
                db.PlatformSettings.Add(new PlatformSetting
                {
                    Category = SettingCategory,
                    JsonValue = JsonSerializer.Serialize(new StoredKeys(publicKey, privateKey)),
                });
                try
                {
                    await db.SaveChangesAsync(cancellationToken);
                    stored = new StoredKeys(publicKey, privateKey);
                }
                catch (DbUpdateException)
                {
                    // Another instance stored its pair first; use that one so every instance signs alike.
                    db.ChangeTracker.Clear();
                    stored = await LoadAsync(db, cancellationToken)
                        ?? throw new InvalidOperationException("Could not store or load the Web Push keys.");
                }
            }

            return cached = new VapidKeys(stored.PublicKey, stored.PrivateKey, subject);
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task<StoredKeys?> LoadAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var row = await db.PlatformSettings
            .AsNoTracking()
            .Where(x => x.Category == SettingCategory)
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : JsonSerializer.Deserialize<StoredKeys>(row.JsonValue);
    }

    private sealed record StoredKeys(string PublicKey, string PrivateKey);
}

/// <summary>
/// Web Push message encryption (RFC 8291, aes128gcm) and VAPID authorization (RFC 8292), using only
/// the framework's crypto. Pure functions so they can be tested by decrypting with the browser's key.
/// </summary>
public static class WebPushCrypto
{
    private const int RecordSize = 4096;

    public static (string PublicKey, string PrivateKey) GenerateVapidKeys()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = key.ExportParameters(true);
        return (Base64Url.EncodeToString(UncompressedPoint(parameters.Q)), Base64Url.EncodeToString(parameters.D!));
    }

    /// <summary>Encrypts <paramref name="plaintext"/> for a subscription's p256dh/auth keys.</summary>
    public static byte[] Encrypt(byte[] plaintext, string p256dh, string auth, byte[]? salt = null, ECDiffieHellman? senderKey = null)
    {
        var userAgentPublic = Base64Url.DecodeFromChars(p256dh);
        var authSecret = Base64Url.DecodeFromChars(auth);
        salt ??= RandomNumberGenerator.GetBytes(16);

        using var ownedKey = senderKey is null ? ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256) : null;
        var sender = senderKey ?? ownedKey!;
        var senderPublic = UncompressedPoint(sender.ExportParameters(false).Q);

        using var receiver = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = PointFromUncompressed(userAgentPublic),
        });
        var sharedSecret = sender.DeriveRawSecretAgreement(receiver.PublicKey);

        var keyInfo = Concat(Encoding.ASCII.GetBytes("WebPush: info\0"), userAgentPublic, senderPublic);
        var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, sharedSecret, 32, authSecret, keyInfo);
        var contentKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, salt, Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"));
        var nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 12, salt, Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"));

        // One record: the payload followed by the 0x02 "last record" delimiter.
        var padded = Concat(plaintext, [0x02]);
        var cipher = new byte[padded.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(contentKey, 16))
        {
            aes.Encrypt(nonce, padded, cipher, tag);
        }

        var header = new byte[16 + 4 + 1 + senderPublic.Length];
        salt.CopyTo(header, 0);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(16), RecordSize);
        header[20] = (byte)senderPublic.Length;
        senderPublic.CopyTo(header, 21);
        return Concat(header, cipher, tag);
    }

    /// <summary>The <c>Authorization: vapid t=…, k=…</c> header value for a push endpoint.</summary>
    public static string CreateVapidAuthorization(string endpoint, VapidKeys keys, DateTimeOffset now)
    {
        var audience = new Uri(endpoint).GetLeftPart(UriPartial.Authority);
        var header = Base64Url.EncodeToString(Encoding.UTF8.GetBytes("{\"typ\":\"JWT\",\"alg\":\"ES256\"}"));
        var claims = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["aud"] = audience,
            ["exp"] = now.AddHours(12).ToUnixTimeSeconds(),
            ["sub"] = keys.Subject,
        }));
        var signingInput = $"{header}.{claims}";

        var publicKey = Base64Url.DecodeFromChars(keys.PublicKey);
        using var ecdsa = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = PointFromUncompressed(publicKey),
            D = Base64Url.DecodeFromChars(keys.PrivateKey),
        });
        var signature = ecdsa.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"vapid t={signingInput}.{Base64Url.EncodeToString(signature)}, k={keys.PublicKey}";
    }

    public static byte[] UncompressedPoint(ECPoint point) => Concat([0x04], point.X!, point.Y!);

    public static ECPoint PointFromUncompressed(byte[] bytes)
    {
        if (bytes.Length != 65 || bytes[0] != 0x04)
        {
            throw new ArgumentException("Expected an uncompressed P-256 public key.");
        }
        return new ECPoint { X = bytes[1..33], Y = bytes[33..65] };
    }

    private static byte[] Concat(params byte[][] parts)
    {
        var result = new byte[parts.Sum(x => x.Length)];
        var offset = 0;
        foreach (var part in parts)
        {
            part.CopyTo(result, offset);
            offset += part.Length;
        }
        return result;
    }
}

/// <summary>
/// Sends one message to every device the given users registered. Callers own the scope: the
/// subscriptions are read from, and dead ones removed through, the context they pass in.
/// </summary>
public interface IWebPushSender
{
    Task SendAsync(AppDbContext db, Guid tenantId, IReadOnlyCollection<Guid> userIds, PushMessage message, CancellationToken cancellationToken = default);
}

internal sealed class WebPushSender(
    IVapidKeyProvider vapidKeyProvider,
    IHttpClientFactory httpClientFactory,
    ILogger<WebPushSender> logger) : IWebPushSender
{
    public async Task SendAsync(AppDbContext db, Guid tenantId, IReadOnlyCollection<Guid> userIds, PushMessage message, CancellationToken cancellationToken = default)
    {
        var subscriptions = await db.PushSubscriptions
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && userIds.Contains(x.UserId))
            .ToListAsync(cancellationToken);
        if (subscriptions.Count == 0) return;

        var keys = await vapidKeyProvider.GetAsync(cancellationToken);
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            title = message.Title,
            body = message.Body,
            url = message.Url,
            tag = message.Tag,
        });

        var client = httpClientFactory.CreateClient(nameof(WebPushSender));
        client.Timeout = TimeSpan.FromSeconds(15);
        foreach (var subscription in subscriptions)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, subscription.Endpoint)
                {
                    Content = new ByteArrayContent(WebPushCrypto.Encrypt(payload, subscription.P256dh, subscription.Auth)),
                };
                request.Content.Headers.ContentType = new("application/octet-stream");
                request.Content.Headers.ContentEncoding.Add("aes128gcm");
                request.Headers.TryAddWithoutValidation("TTL", "86400");
                request.Headers.TryAddWithoutValidation("Urgency", "high");
                request.Headers.TryAddWithoutValidation("Authorization", WebPushCrypto.CreateVapidAuthorization(subscription.Endpoint, keys, DateTimeOffset.UtcNow));

                using var response = await client.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    subscription.LastSuccessAt = DateTime.UtcNow;
                }
                else if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
                {
                    // The browser unsubscribed or the app was uninstalled; this endpoint is dead for good.
                    db.PushSubscriptions.Remove(subscription);
                }
                else
                {
                    logger.LogWarning("Push service returned {StatusCode} for subscription {SubscriptionId}.", (int)response.StatusCode, subscription.Id);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or FormatException or ArgumentException or CryptographicException)
            {
                logger.LogWarning(ex, "Push delivery failed for subscription {SubscriptionId}.", subscription.Id);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// An in-memory hand-off for pushes that need no inbox row — today only the "send me a test push"
/// button. Every real notification goes through <see cref="INotificationDispatcher"/>, which records
/// the push on its <c>Notification</c> row so a restart cannot lose it (see <c>NotificationPushWorker</c>).
/// In-memory and per instance: a message still queued when the process stops is dropped.
/// </summary>
public interface IPushNotificationQueue
{
    void Enqueue(Guid tenantId, IReadOnlyCollection<Guid> userIds, PushMessage message);
}

internal sealed record PushNotificationJob(Guid TenantId, IReadOnlyCollection<Guid> UserIds, PushMessage Message);

internal sealed class PushNotificationQueue : IPushNotificationQueue
{
    private readonly Channel<PushNotificationJob> channel = Channel.CreateBounded<PushNotificationJob>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest });

    public ChannelReader<PushNotificationJob> Reader => channel.Reader;

    public void Enqueue(Guid tenantId, IReadOnlyCollection<Guid> userIds, PushMessage message)
    {
        if (userIds.Count > 0)
        {
            channel.Writer.TryWrite(new PushNotificationJob(tenantId, userIds, message));
        }
    }
}

internal sealed class PushNotificationWorker(
    PushNotificationQueue queue,
    IServiceScopeFactory serviceScopeFactory,
    IWebPushSender sender,
    ILogger<PushNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await sender.SendAsync(db, job.TenantId, job.UserIds, job.Message, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Failed to send push notification '{Tag}'.", job.Message.Tag);
            }
        }
    }
}
