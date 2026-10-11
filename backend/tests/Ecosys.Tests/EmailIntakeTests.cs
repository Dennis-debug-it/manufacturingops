using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Ecosys.Domain.Entities;
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

    [Fact]
    public async Task IntakeRules_DropMailThatIsNotARequest_AndShapeTheWorkOrdersTheyCreate()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await SignUpAsync(client));
        await client.PostAsJsonAsync("/api/clients", new { clientName = "Acme Mall", email = "ops@acme.test", phone = "0700000000" });
        var bigCorp = (await (await client.PostAsJsonAsync("/api/clients", new { clientName = "BigCorp", email = "accounts@bigcorp.test", phone = "0700000001" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await client.PutAsJsonAsync("/api/settings/email-intake", new
        {
            isEnabled = true, intakeEmailAddress = "service@intake.test", mailboxProvider = "IMAP", host = "imap.intake.test", port = 993, useSsl = true,
            username = "service@intake.test", password = "secret", defaultClientId = (Guid?)null, defaultBranchId = (Guid?)null, defaultAssignmentGroupId = (Guid?)null,
            defaultPriority = "Medium", createWorkOrderFromUnknownSender = false, subjectParsingRules = (string?)null, allowedSenderDomains = (string?)null, autoReplyEnabled = false,
        });

        // Ready-made rules, tried in the order they were added.
        foreach (var key in new[] { "ignore-automated", "urgent", "generator-fault" })
        {
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsync($"/api/settings/intake-protocols/templates/{key}", null)).StatusCode);
        }

        // A custom rule: anything from bigcorp.test is that client's, even from people who aren't contacts.
        var custom = await client.PostAsJsonAsync("/api/settings/intake-protocols", new
        {
            name = "BigCorp facilities team", sourceType = "Email", isActive = true, description = (string?)null,
            criteriaJson = JsonSerializer.Serialize(new[] { new { logic = "AND", criteria = new[] { new { field = "Sender Domain", @operator = "Equals", value = "bigcorp.test" } } } }),
            actionsJson = JsonSerializer.Serialize(new { createWorkOrder = new { clientId = bigCorp, priority = "High", workOrderTitleTemplate = "BigCorp: {{subject}}" } }),
            sourceConfigJson = "{}",
        });
        Assert.Equal(HttpStatusCode.Created, custom.StatusCode);

        // A rule with no conditions is refused.
        var empty = await client.PostAsJsonAsync("/api/settings/intake-protocols", new
        {
            name = "Broken", sourceType = "Email", isActive = true, description = (string?)null,
            criteriaJson = "[]", actionsJson = JsonSerializer.Serialize(new { ignore = new { enabled = true } }), sourceConfigJson = "{}",
        });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

        // Saving rules leaves the mailbox settings alone.
        var settings = await client.GetFromJsonAsync<JsonElement>("/api/settings/email-intake");
        Assert.Equal("imap.intake.test", settings.GetProperty("host").GetString());
        Assert.True(settings.GetProperty("isEnabled").GetBoolean());

        var preview = await (await client.PostAsJsonAsync("/api/settings/intake-protocols/preview",
            new { subject = "Generator tripped", body = "", fromAddress = "ops@acme.test", fromName = "Acme" })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Generator faults", preview.GetProperty("ruleName").GetString());
        Assert.Equal("[Generator] Generator tripped", preview.GetProperty("title").GetString());

        await CheckNowAsync(client);
        mailbox.Deliver("newsletter@vendor.test", "October deals", "Buy now");
        mailbox.Deliver("ops@acme.test", "Chiller down - emergency", "No cooling in the food court.");
        mailbox.Deliver("ops@acme.test", "Generator making a noise", "Rattling at startup.");
        mailbox.Deliver("facilities@bigcorp.test", "Lift stuck", "Lift 2 stuck on floor 3.");
        Assert.Equal(4, await CheckNowAsync(client));

        await using var db = CreateContext();
        var ignored = await db.EmailIntakeMessages.IgnoreQueryFilters().SingleAsync(x => x.FromAddress == "newsletter@vendor.test");
        Assert.Equal(EmailIntakeOutcomes.Ignored, ignored.Outcome);
        Assert.Contains("Ignore newsletters and no-reply mail", ignored.Detail);

        var workOrders = await db.WorkOrders.IgnoreQueryFilters().ToDictionaryAsync(x => x.Title);
        Assert.Equal("Critical", workOrders["Chiller down - emergency"].Priority);
        Assert.Equal("High", workOrders["[Generator] Generator making a noise"].Priority);
        Assert.Equal(bigCorp, workOrders["BigCorp: Lift stuck"].ClientId);
        Assert.Equal(3, workOrders.Count);
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


public sealed class IntakeRuleMatchingTests
{
    private static string Rule(string logic, params (string Field, string Operator, string Value)[] criteria) =>
        JsonSerializer.Serialize(new[] { new { logic, criteria = criteria.Select(c => new { field = c.Field, @operator = c.Operator, value = c.Value }).ToArray() } });

    private static readonly IntakeRuleInput Email = new("Chiller down - URGENT", "No cooling since 8am.", "ops@acme.test", "Acme Ops");

    [Theory]
    [InlineData("Subject", "Contains", "urgent", true)]
    [InlineData("Subject", "Contains Any Of", "emergency, urgent", true)]
    [InlineData("Body", "Contains", "urgent", false)]
    [InlineData("Subject or Body", "Contains", "cooling", true)]
    [InlineData("Sender Domain", "Equals", "ACME.test", true)]
    [InlineData("Sender Email", "Starts With", "ops@", true)]
    [InlineData("Sender Name", "Ends With", "ops", true)]
    [InlineData("Subject", "Does Not Contain", "urgent", false)]
    [InlineData("Body", "Is Not Empty", "", true)]
    public void SingleConditions(string field, string op, string value, bool expected) =>
        Assert.Equal(expected, IntakeRules.Matches(Rule("AND", (field, op, value)), Email));

    [Fact]
    public void AllVersusAny()
    {
        Assert.False(IntakeRules.Matches(Rule("AND", ("Subject", "Contains", "urgent"), ("Body", "Contains", "generator")), Email));
        Assert.True(IntakeRules.Matches(Rule("OR", ("Subject", "Contains", "urgent"), ("Body", "Contains", "generator")), Email));
    }

    [Fact]
    public void TheFirstActiveRuleWins_AndTheOldCatchAllIsSkipped()
    {
        var create = JsonSerializer.Serialize(new { createWorkOrder = new { priority = "Critical" } });
        var rules = new[]
        {
            new IntakeProtocol { Name = "old", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-3), CriteriaJson = $"[{{\"id\":\"{IntakeRules.LegacyMarker}\",\"logic\":\"AND\",\"criteria\":[{{\"field\":\"Subject\",\"operator\":\"Is Not Empty\",\"value\":\"\"}}]}}]", ActionsJson = create },
            new IntakeProtocol { Name = "off", IsActive = false, CreatedAt = DateTime.UtcNow.AddDays(-2), CriteriaJson = Rule("AND", ("Subject", "Contains", "chiller")), ActionsJson = create },
            new IntakeProtocol { Name = "chiller", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-1), CriteriaJson = Rule("AND", ("Subject", "Contains", "chiller")), ActionsJson = create },
            new IntakeProtocol { Name = "urgent", IsActive = true, CreatedAt = DateTime.UtcNow, CriteriaJson = Rule("AND", ("Subject", "Contains", "urgent")), ActionsJson = create },
        };
        var outcome = IntakeRules.FirstMatch(rules, Email);
        Assert.Equal("chiller", outcome?.Rule.Name);
        Assert.Equal("Critical", outcome?.Priority);
    }

    [Fact]
    public void TemplatesAreValidRules()
    {
        foreach (var template in IntakeRuleTemplates.All)
        {
            Assert.Null(IntakeRules.Validate(template.CriteriaJson, template.ActionsJson));
        }
    }

    [Fact]
    public void TitleTemplates()
    {
        Assert.Equal("[Generator] Won't start", IntakeRules.ApplyTitle("[Generator] {{subject}}", "Won't start", "a@b.test", null));
        Assert.Equal("From Ann: Leak", IntakeRules.ApplyTitle("From {{senderName}}: {{subject}}", "Leak", "a@b.test", "Ann"));
        Assert.Equal("Leak", IntakeRules.ApplyTitle(null, "Leak", "a@b.test", null));
    }
}
