using System.Security.Cryptography;
using System.Text;
using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Errors;
using Ecosys.Shared.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ecosys.Tests;

public sealed class PortalAuthSecurityTests
{
    [PortalPostgresFact]
    public async Task Verify_ConcurrentRelationalRequestsIssueExactlyOneSession()
    {
        var connectionString = Environment.GetEnvironmentVariable("ECOSYS_TEST_DB_CONNECTION")!;
        var schema = "portal_auth_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA {schema}", admin))
            await create.ExecuteNonQueryAsync();
        try
        {
            var scopedConnection = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema }.ConnectionString;
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(scopedConnection).Options;
            await using (var seed = new AppDbContext(options))
            {
                await seed.Database.ExecuteSqlRawAsync(seed.Database.GenerateCreateScript());
                var tenant = new Tenant { IsActive = true };
                var client = new Client { TenantId = tenant.Id, IsActive = true };
                var contact = new ClientPortalUser { TenantId = tenant.Id, Tenant = tenant,
                    ClientId = client.Id, Client = client, FullName = "Portal test", Email = "portal@example.test" };
                seed.ClientPortalLoginTokens.Add(new ClientPortalLoginToken
                {
                    ClientPortalUser = contact, ClientPortalUserId = contact.Id,
                    TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("race-token"))),
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30)
                });
                await seed.SaveChangesAsync();
            }
            var barrier = new TokenReadBarrier();
            var raceOptions = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(scopedConnection)
                .AddInterceptors(barrier).Options;
            async Task<bool> Verify()
            {
                await using var db = new AppDbContext(raceOptions);
                var service = new PortalAuthService(db, Options.Create(new JwtOptions
                { SigningKey = new string('x', 64) }), null!, new NoOpAudit());
                try { return !string.IsNullOrWhiteSpace((await service.VerifyMagicLinkAsync("race-token")).Token); }
                catch (ForbiddenException) { return false; }
            }
            var results = await Task.WhenAll(Verify(), Verify());
            Assert.Single(results.Where(result => result));
            await using var verify = new AppDbContext(options);
            Assert.NotNull((await verify.ClientPortalLoginTokens.SingleAsync()).ConsumedAt);
        }
        finally
        {
            // schema is a fixed prefix plus a locally generated GUID; never user input.
            await using var drop = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Theory]
    [InlineData("https://attacker.test")]
    [InlineData("")]
    public async Task RequestLink_UsesConfiguredPublicUrlRegardlessOfRequestHeaders(string origin)
    {
        var service = new RecordingPortalAuthService();
        var controller = new PortalAuthController(service, null!, null!,
            Options.Create(new PublicAppOptions { PublicUrl = "https://portal.example.test/" }))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.Request.Headers.Origin = origin;
        controller.Request.Host = new HostString("attacker.test");
        await controller.RequestLink(new PortalRequestLinkRequest("contact@example.test"), CancellationToken.None);
        Assert.Equal("https://portal.example.test", service.BaseUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://user:password@example.test")]
    [InlineData("https://example.test?redirect=other")]
    [InlineData("https://example.test#fragment")]
    public async Task RequestLink_InvalidPublicUrlFailsBeforeQueuing(string publicUrl)
    {
        var service = new RecordingPortalAuthService();
        var controller = new PortalAuthController(service, null!, null!,
            Options.Create(new PublicAppOptions { PublicUrl = publicUrl }));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.RequestLink(new PortalRequestLinkRequest("contact@example.test"), CancellationToken.None));
        Assert.Null(service.BaseUrl);
    }

    // These tests verify rejection before the relational claim. PostgreSQL tests
    // separately exercise the atomic update; InMemory cannot prove concurrency.
    [Theory]
    [InlineData("expired")]
    [InlineData("consumed")]
    [InlineData("disabled-contact")]
    [InlineData("disabled-client")]
    [InlineData("disabled-tenant")]
    [InlineData("foreign-client")]
    [InlineData("unknown-token")]
    public async Task Verify_RejectsInvalidOrInactiveIdentity(string condition)
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var tenant = new Tenant { IsActive = condition != "disabled-tenant" };
        var client = new Client { TenantId = condition == "foreign-client" ? Guid.NewGuid() : tenant.Id,
            IsActive = condition != "disabled-client" };
        var contact = new ClientPortalUser { TenantId = tenant.Id, Tenant = tenant,
            ClientId = client.Id, Client = client, IsActive = condition != "disabled-contact" };
        db.ClientPortalLoginTokens.Add(new ClientPortalLoginToken
        {
            ClientPortalUser = contact, ClientPortalUserId = contact.Id,
            TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("test-token"))),
            ExpiresAt = DateTime.UtcNow.AddMinutes(condition == "expired" ? -1 : 30),
            ConsumedAt = condition == "consumed" ? DateTime.UtcNow : null
        });
        await db.SaveChangesAsync();
        var service = new PortalAuthService(db, Options.Create(new JwtOptions()), null!, null!);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.VerifyMagicLinkAsync(
            condition == "unknown-token" ? "other-token" : "test-token"));
    }

    [Fact]
    public async Task PermanentLink_KeepsWorking_UntilResetOrTheContactIsDeactivated()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var tenant = new Tenant { IsActive = true, CompanyName = "Acme Services" };
        var client = new Client { TenantId = tenant.Id, IsActive = true, ClientName = "Client Co" };
        var contact = new ClientPortalUser { TenantId = tenant.Id, Tenant = tenant, ClientId = client.Id, Client = client,
            IsActive = true, FullName = "Jane", Email = "jane@client.test" };
        db.AddRange(tenant, client, contact);
        await db.SaveChangesAsync();
        var outbox = new RecordingOutbox();
        var service = new PortalAuthService(db, Options.Create(new JwtOptions { SigningKey = new string('k', 64) }), outbox, new NoOpAudit());
        static string Fragment(string link) => link[(link.IndexOf('#') + 1)..];

        // The invitation email carries the same permanent link staff can copy.
        await service.SendInvitationAsync(contact.Id, "https://serviceops.test", null, null, null);
        var link = await service.GetAccessLinkAsync(contact.Id, "https://serviceops.test", Guid.NewGuid(), reset: false);
        Assert.StartsWith("https://serviceops.test/portal/access#", link);
        Assert.Contains(link, Assert.Single(outbox.Queued).HtmlBody);
        Assert.Empty(await db.ClientPortalLoginTokens.ToListAsync());

        // Reusable: no expiry, not consumed by use.
        Assert.Equal(contact.Id, (await service.OpenAccessLinkAsync(Fragment(link))).PortalUserId);
        Assert.Equal(contact.Id, (await service.OpenAccessLinkAsync(Fragment(link))).PortalUserId);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.OpenAccessLinkAsync(Fragment(link)[..^2] + "xx"));

        // Reset kills the old link; the new one works.
        var newLink = await service.GetAccessLinkAsync(contact.Id, "https://serviceops.test", Guid.NewGuid(), reset: true);
        Assert.NotEqual(link, newLink);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.OpenAccessLinkAsync(Fragment(link)));
        Assert.Equal(contact.Id, (await service.OpenAccessLinkAsync(Fragment(newLink))).PortalUserId);

        // Deactivating the contact stops it too.
        contact.IsActive = false;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => service.OpenAccessLinkAsync(Fragment(newLink)));
    }

    private sealed class RecordingPortalAuthService : IPortalAuthService
    {
        public string? BaseUrl { get; private set; }
        public Task RequestMagicLinkAsync(string email, string magicLinkBaseUrl, string? ipAddress,
            string? userAgent, CancellationToken cancellationToken = default)
        {
            BaseUrl = magicLinkBaseUrl;
            return Task.CompletedTask;
        }
        public Task<PortalMagicLinkResult> VerifyMagicLinkAsync(string token,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> IssueSignInLinkAsync(Guid portalUserId, string magicLinkBaseUrl, string? ipAddress,
            string? userAgent, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SendInvitationAsync(Guid portalUserId, string portalBaseUrl, Guid? invitedByUserId, string? ipAddress,
            string? userAgent, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> GetAccessLinkAsync(Guid portalUserId, string portalBaseUrl, Guid? actorUserId, bool reset, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PortalMagicLinkResult> OpenAccessLinkAsync(string link, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingOutbox : IEmailOutboxService
    {
        public List<QueueEmailRequest> Queued { get; } = [];
        public Task<EmailOutboxMessage> QueueEmailAsync(QueueEmailRequest request, CancellationToken cancellationToken = default)
        {
            Queued.Add(request);
            return Task.FromResult(new EmailOutboxMessage());
        }
        public Task<IReadOnlyCollection<EmailOutboxMessage>> GetPendingBatchAsync(int batchSize, DateTime utcNow, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> MarkSendingAsync(Guid outboxMessageId, DateTime utcNow, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> ReleaseAbandonedClaimsAsync(TimeSpan claimTimeout, DateTime utcNow, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task MarkSentAsync(Guid outboxMessageId, DateTime utcNow, string? providerMessageId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task MarkFailedAsync(Guid outboxMessageId, string? errorCategory, string? errorMessage, DateTime utcNow, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RetryAsync(Guid outboxMessageId, DateTime nextAttemptAt, string? errorMessage, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CancelAsync(Guid outboxMessageId, string? errorMessage, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class TokenReadBarrier : DbCommandInterceptor
    {
        private int readers;
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,
            CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("client_portal_login_tokens", StringComparison.Ordinal))
            {
                if (Interlocked.Increment(ref readers) == 2) ready.TrySetResult();
                await ready.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }

    private sealed class NoOpAudit : IAuditLogService
    {
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName,
            string entityId, string? details, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName,
            string entityId, string? details, string severity = "Info", string? actorName = null,
            string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

public sealed class PortalPostgresFactAttribute : FactAttribute
{
    public PortalPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ECOSYS_TEST_DB_CONNECTION")))
            Skip = "Requires an isolated PostgreSQL test connection; InMemory does not verify atomic token consumption.";
    }
}
