using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MimeKit;
using Npgsql;

namespace Ecosys.Tests;

public sealed class EmailIntakeParsingTests
{
    [Theory]
    [InlineData("Re: Fwd: RE: Lift stuck", "Lift stuck")]
    [InlineData("AW: Re[2]: Chiller", "Chiller")]
    [InlineData("   ", "(no subject)")]
    public void CleanSubject_RemovesReplyPrefixes(string subject, string expected) =>
        Assert.Equal(expected, EmailIntakeService.CleanSubject(subject));

    [Fact]
    public void StripQuotedReply_KeepsOnlyWhatTheSenderWrote()
    {
        var body = "The technician arrived, thanks.\n\nOn Mon, 5 Oct 2026, Acme wrote:\n> We received your request\n> WO-000002";
        Assert.Equal("The technician arrived, thanks.", EmailIntakeService.StripQuotedReply(body));
    }

    [Fact]
    public void PriorityRules_AcceptSeveralSeparators_AndIgnoreUnknownPriorities()
    {
        var rules = EmailIntakeService.ParsePriorityRules("chiller down => Critical\nurgent = High, leak: medium\nfoo = Whenever");
        Assert.Equal([("chiller down", "Critical"), ("urgent", "High"), ("leak", "Medium")], rules);
    }

    [Fact]
    public void BodyText_FallsBackToTheHtmlPart()
    {
        var message = new MimeMessage { Body = new TextPart("html") { Text = "<p>Line one</p><p>Line &amp; two</p><style>p{}</style>" } };
        Assert.Equal("Line one\nLine & two", EmailIntakeService.BodyText(message));
    }
}

/// <summary>
/// Email intake through the real API on a throwaway PostgreSQL database; only the mailbox is faked.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class EmailIntakeFlowTests : IAsyncLifetime
{
    private readonly string databaseName = $"ecosys_intake_{Guid.NewGuid():N}";
    private readonly FakeMailbox mailbox = new();
    private string serverConnection = string.Empty;
    private string databaseConnection = string.Empty;

    public async Task InitializeAsync()
    {
        serverConnection = Environment.GetEnvironmentVariable("ECOSYS_TEST_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=ecosys_test;Username=postgres;Password=postgres";
        databaseConnection = new NpgsqlConnectionStringBuilder(serverConnection) { Database = databaseName }.ConnectionString;
        await ExecuteAdminAsync($"CREATE DATABASE \"{databaseName}\"");
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await ExecuteAdminAsync($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)");
    }

    [Fact]
    public async Task Emails_BecomeWorkOrders_RepliesJoinTheirJob_AndStrangersAreTurnedAway()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var token = await SignUpAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var created = await client.PostAsJsonAsync("/api/clients", new { clientName = "Acme Mall", email = "ops@acme.test", phone = "0700000000" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var saved = await client.PutAsJsonAsync("/api/settings/email-intake", new
        {
            isEnabled = true, intakeEmailAddress = "service@intake.test", mailboxProvider = "IMAP", host = "imap.intake.test", port = 993, useSsl = true,
            username = "service@intake.test", password = "secret", defaultClientId = (Guid?)null, defaultBranchId = (Guid?)null, defaultAssignmentGroupId = (Guid?)null,
            defaultPriority = "Medium", createWorkOrderFromUnknownSender = false, subjectParsingRules = (string?)null, allowedSenderDomains = (string?)null, autoReplyEnabled = true,
        });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        // First read only sets the starting point: older emails in the inbox are not imported.
        mailbox.Deliver("ops@acme.test", "Old email already in the inbox", "ignore me");
        Assert.Equal(0, await CheckNowAsync(client));

        mailbox.Deliver("ops@acme.test", "Generator not starting - URGENT", "Generator at Acme Mall won't start.");
        mailbox.Deliver("someone@stranger.test", "Quote please", "Can you quote?");
        mailbox.Deliver("ops@acme.test", "Automatic reply: Generator", "Out of office", ("Auto-Submitted", "auto-replied"));
        Assert.Equal(3, await CheckNowAsync(client));

        var messages = await MessagesAsync(client);
        var workOrderNumber = messages.Single(x => x.GetProperty("outcome").GetString() == EmailIntakeOutcomes.WorkOrderCreated).GetProperty("workOrderNumber").GetString()!;
        mailbox.Deliver("ops@acme.test", $"Re: [{workOrderNumber}] Generator not starting", "It's working now.\n\nOn Mon, Acme wrote:\n> earlier");
        Assert.Equal(1, await CheckNowAsync(client));
        // Read again: nothing is handled twice.
        Assert.Equal(0, await CheckNowAsync(client));

        await using var db = CreateContext();
        var workOrder = await db.WorkOrders.IgnoreQueryFilters().SingleAsync(x => x.WorkOrderNumber == workOrderNumber);
        Assert.Equal("Generator not starting - URGENT", workOrder.Title);
        Assert.Equal("High", workOrder.Priority);
        Assert.Equal("New", workOrder.Status);
        Assert.Contains("(Received by email from ops@acme.test.)", workOrder.Description);
        Assert.Equal(1, await db.WorkOrders.IgnoreQueryFilters().CountAsync());

        var note = await db.WorkOrderEvents.IgnoreQueryFilters().SingleAsync(x => x.WorkOrderId == workOrder.Id && x.EventType == "EmailReceived");
        Assert.Equal("It's working now.", note.Note);

        var outcomes = (await db.EmailIntakeMessages.IgnoreQueryFilters().OrderBy(x => x.CreatedAt).ToListAsync())
            .Select(x => (x.FromAddress, x.Outcome, x.AutoReplySent)).ToList();
        Assert.Equal(
            [
                ("ops@acme.test", EmailIntakeOutcomes.WorkOrderCreated, true),
                ("someone@stranger.test", EmailIntakeOutcomes.Rejected, true),
                ("ops@acme.test", EmailIntakeOutcomes.Ignored, false),
                ("ops@acme.test", EmailIntakeOutcomes.AddedToWorkOrder, true),
            ],
            outcomes);

        var replies = await db.EmailOutboxMessages.IgnoreQueryFilters().Where(x => x.EventKey.StartsWith("email-intake.")).OrderBy(x => x.CreatedAt).ToListAsync();
        Assert.Equal(
            [EmailIntakeTemplates.Received, EmailIntakeTemplates.Unrecognised, EmailIntakeTemplates.ReplyAdded],
            replies.Select(x => x.TemplateKey).ToList());
        Assert.Equal($"Re: Generator not starting - URGENT [{workOrderNumber}]", replies[0].Subject);
        Assert.Contains(workOrderNumber, replies[0].TextBody);
    }

    [Fact]
    public async Task UnknownSenders_AreLoggedUnderTheDefaultClient_WhenAllowed()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await SignUpAsync(client));
        var created = await (await client.PostAsJsonAsync("/api/clients", new { clientName = "Walk-in customers", email = "walkin@example.test", phone = "0700000000" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var defaultClientId = created.GetProperty("id").GetGuid();

        await client.PutAsJsonAsync("/api/settings/email-intake", new
        {
            isEnabled = true, intakeEmailAddress = (string?)null, mailboxProvider = "IMAP", host = "imap.intake.test", port = 993, useSsl = true,
            username = "service@intake.test", password = "secret", defaultClientId, defaultBranchId = (Guid?)null, defaultAssignmentGroupId = (Guid?)null,
            defaultPriority = "Low", createWorkOrderFromUnknownSender = true, subjectParsingRules = "leak = Critical", allowedSenderDomains = "newco.test", autoReplyEnabled = false,
        });
        await CheckNowAsync(client);

        mailbox.Deliver("pat@newco.test", "Roof leak in store room", "Water coming in.");
        mailbox.Deliver("spam@elsewhere.test", "Cheap offers", "...");
        Assert.Equal(2, await CheckNowAsync(client));

        await using var db = CreateContext();
        var workOrder = await db.WorkOrders.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(defaultClientId, workOrder.ClientId);
        Assert.Equal("Critical", workOrder.Priority);
        var rejected = await db.EmailIntakeMessages.IgnoreQueryFilters().SingleAsync(x => x.FromAddress == "spam@elsewhere.test");
        Assert.Equal(EmailIntakeOutcomes.Rejected, rejected.Outcome);
        // Auto-replies switched off: none queued.
        Assert.False(await db.EmailOutboxMessages.IgnoreQueryFilters().AnyAsync(x => x.EventKey.StartsWith("email-intake.")));
    }

    private static async Task<string> SignUpAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/signup", new
        {
            fullName = "Ivy Intake", email = $"ivy.{Guid.NewGuid():N}@intake-owner.test", password = "Intake-Owner-Pass-2026!",
            companyName = "Intake Co", industry = "Facilities", country = "Kenya",
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
    }

    private static async Task<int> CheckNowAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/settings/email-intake/check-now", new { });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.GetProperty("isConnectionHealthy").GetBoolean(), result.ToString());
        return result.GetProperty("handled").GetInt32();
    }

    private static async Task<List<JsonElement>> MessagesAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<JsonElement>>("/api/settings/email-intake/messages"))!;

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(databaseConnection).Options);

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.UseSetting("ConnectionStrings:DefaultConnection", databaseConnection);
            builder.UseSetting("Jwt:Issuer", "ecosys-tests");
            builder.UseSetting("Jwt:Audience", "ecosys-tests");
            builder.UseSetting("Jwt:SigningKey", "12345678901234567890123456789012");
            builder.UseSetting("PlatformAdmin:Email", "platform.owner@ecosys.test");
            builder.UseSetting("PlatformAdmin:Password", "ValidPlatformAdmin123!");
            builder.UseSetting("PlatformAdmin:FullName", "Platform Owner");
            builder.UseSetting("App:PublicUrl", "https://ecosys-tests.example.com");
            builder.UseSetting("Smtp:Host", "127.0.0.1");
            builder.UseSetting("Smtp:Port", "1");
            builder.UseSetting($"{AuthSecurityOptions.SectionName}:AllowSelfSignup", "true");
            builder.UseSetting($"{AuthSecurityOptions.SectionName}:RateLimitPermitLimit", "1000");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEmailIntakeMailbox>();
                services.AddSingleton<IEmailIntakeMailbox>(mailbox);
            });
        });

    private async Task ExecuteAdminAsync(string sql)
    {
        var admin = new NpgsqlConnectionStringBuilder(serverConnection) { Database = "postgres", Pooling = false }.ConnectionString;
        await using var connection = new NpgsqlConnection(admin);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>An inbox: delivered emails get increasing UIDs, like a real IMAP server.</summary>
    private sealed class FakeMailbox : IEmailIntakeMailbox
    {
        private readonly List<(long Uid, MimeMessage Message)> inbox = [];
        private readonly object gate = new();

        public void Deliver(string from, string subject, string body, params (string Name, string Value)[] headers)
        {
            var message = new MimeMessage { Subject = subject, Body = new TextPart("plain") { Text = body }, Date = DateTimeOffset.UtcNow, MessageId = MimeKit.Utils.MimeUtils.GenerateMessageId() };
            message.From.Add(MailboxAddress.Parse(from));
            message.To.Add(MailboxAddress.Parse("service@intake.test"));
            foreach (var (name, value) in headers) message.Headers.Add(name, value);
            lock (gate) inbox.Add((inbox.Count + 1, message));
        }

        public Task VerifyAsync(EmailIntakeMailboxSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<EmailIntakeFetchResult> FetchNewAsync(EmailIntakeMailboxSettings settings, long? uidValidity, long? lastUid, int maxMessages, CancellationToken cancellationToken = default)
        {
            lock (gate)
            {
                var newest = inbox.Count == 0 ? 0 : inbox[^1].Uid;
                if (lastUid is null || uidValidity != 1)
                {
                    return Task.FromResult(new EmailIntakeFetchResult(1, newest, []));
                }

                var fresh = inbox.Where(x => x.Uid > lastUid.Value).Take(maxMessages).ToList();
                return Task.FromResult(new EmailIntakeFetchResult(1, fresh.Count > 0 ? fresh[^1].Uid : lastUid.Value, fresh));
            }
        }
    }
}
