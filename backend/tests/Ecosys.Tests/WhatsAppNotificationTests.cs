using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Ecosys.Tests;

/// <summary>Release 3 step 3d: WhatsApp reminders from the shared Ecosys number. Never calls Meta.</summary>
public sealed class WhatsAppNotificationTests
{
    private const string AppSecret = "test-app-secret";
    private const string VerifyToken = "test-verify-token";
    private const string AliceNumber = "+254712345678";

    // ── Phone numbers ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("0712 345 678", null, "+254712345678")]
    [InlineData("0712-345-678", "Kenya", "+254712345678")]
    [InlineData("+254 712 345 678", null, "+254712345678")]
    [InlineData("254712345678", "Kenya", "+254712345678")]
    [InlineData("712345678", "Kenya", "+254712345678")]
    [InlineData("0110 123 456", "Kenya", "+254110123456")]
    [InlineData("00254712345678", null, "+254712345678")]
    [InlineData("0772 123 456", "Uganda", "+256772123456")]
    [InlineData("+44 7911 123456", "Kenya", "+447911123456")]
    [InlineData("0612 345 678", "Kenya", null)]   // not a Kenyan mobile prefix
    [InlineData("07123", null, null)]
    [InlineData("0712 345 678 9", null, null)]
    [InlineData("call me", null, null)]
    [InlineData("", null, null)]
    public void PhoneNumbers_NormaliseToE164(string input, string? country, string? expected) =>
        Assert.Equal(expected, WhatsAppPhoneNumbers.Normalize(input, country));

    // ── Sender ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sender_PostsTheMetaTemplatePayload()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, """{"messaging_product":"whatsapp","messages":[{"id":"wamid.ABC"}]}""");
        var sender = new MetaWhatsAppSender(new HandlerFactory(handler), NullLogger<MetaWhatsAppSender>.Instance);
        var account = new WhatsAppSendingAccount("1234567890", "token-xyz", "v21.0", "https://graph.example.test", null);

        var result = await sender.SendTemplateAsync(account, AliceNumber, "muster_missing", "en", ["Acme Builders", "Westlands Mall", "2026-10-21"], null, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("wamid.ABC", result.MessageId);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://graph.example.test/v21.0/1234567890/messages", handler.Request.RequestUri!.ToString());
        Assert.Equal("Bearer token-xyz", handler.Request.Headers.Authorization!.ToString());
        using var json = JsonDocument.Parse(handler.Body!);
        var root = json.RootElement;
        Assert.Equal("whatsapp", root.GetProperty("messaging_product").GetString());
        Assert.Equal("254712345678", root.GetProperty("to").GetString());
        Assert.Equal("template", root.GetProperty("type").GetString());
        var template = root.GetProperty("template");
        Assert.Equal("muster_missing", template.GetProperty("name").GetString());
        Assert.Equal("en", template.GetProperty("language").GetProperty("code").GetString());
        var body = Assert.Single(template.GetProperty("components").EnumerateArray());
        Assert.Equal("body", body.GetProperty("type").GetString());
        Assert.Equal(["Acme Builders", "Westlands Mall", "2026-10-21"],
            body.GetProperty("parameters").EnumerateArray().Select(p => p.GetProperty("text").GetString()));
    }

    [Fact]
    public async Task Sender_AddsTheCopyCodeButton_AndClassifiesFailures()
    {
        var payload = MetaWhatsAppSender.BuildPayload(AliceNumber, "ecosys_verification_code", "en", ["123456"], ["123456"]);
        using (var json = JsonDocument.Parse(payload))
        {
            var button = json.RootElement.GetProperty("template").GetProperty("components")[1];
            Assert.Equal(("button", "url", "0"), (button.GetProperty("type").GetString(), button.GetProperty("sub_type").GetString(), button.GetProperty("index").GetString()));
            Assert.Equal("123456", button.GetProperty("parameters")[0].GetProperty("text").GetString());
        }

        var account = new WhatsAppSendingAccount("1", "t", "v21.0", "https://graph.example.test", null);
        var refused = new MetaWhatsAppSender(new HandlerFactory(new RecordingHandler(HttpStatusCode.BadRequest, """{"error":{"message":"Template name does not exist","code":132001}}""")), NullLogger<MetaWhatsAppSender>.Instance);
        var result = await refused.SendTemplateAsync(account, AliceNumber, "x", "en", [], null, CancellationToken.None);
        Assert.False(result.Retryable);
        Assert.Contains("132001", result.Error);

        var busy = new MetaWhatsAppSender(new HandlerFactory(new RecordingHandler(HttpStatusCode.ServiceUnavailable, "")), NullLogger<MetaWhatsAppSender>.Instance);
        Assert.True((await busy.SendTemplateAsync(account, AliceNumber, "x", "en", [], null, CancellationToken.None)).Retryable);
    }

    [Fact]
    public void Catalogue_FirstParameterIsTheTenantName_AndEveryKeyHasATemplate()
    {
        var muster = WhatsAppTemplateCatalogue.Find("project.muster-missing")!;
        var parameters = WhatsAppTemplateCatalogue.BuildParameters(muster, "Acme Builders",
            new Dictionary<string, string?> { ["projectname"] = "Westlands\nMall", ["WorkDate"] = null });
        Assert.Equal(["Acme Builders", "Westlands Mall", "-"], parameters);

        foreach (var descriptor in NotificationKeyDescriptor.List().Where(d => d.Channels.HasFlag(NotificationChannels.WhatsApp)))
        {
            Assert.NotNull(WhatsAppTemplateCatalogue.Find(descriptor.Key));
            Assert.False(descriptor.DefaultChannels.HasFlag(NotificationChannels.WhatsApp)); // opt-in only
        }
        Assert.All(WhatsAppTemplateCatalogue.DefaultAllowedKeys, key => Assert.NotNull(WhatsAppTemplateCatalogue.Find(key)));
    }

    // ── Webhook ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Webhook_RefusesAMissingOrWrongSignature()
    {
        var harness = await Harness.CreateAsync();
        var body = StatusPayload("wamid.1", "delivered");

        Assert.IsType<UnauthorizedResult>(await harness.Webhook(body, signature: null).Receive(CancellationToken.None));
        Assert.IsType<UnauthorizedResult>(await harness.Webhook(body, signature: "sha256=" + new string('0', 64)).Receive(CancellationToken.None));
        Assert.IsType<UnauthorizedResult>(await harness.Webhook(body, signature: Sign(body, "another-secret")).Receive(CancellationToken.None));
        // Signed over different bytes than were sent.
        Assert.IsType<UnauthorizedResult>(await harness.Webhook(body + " ", signature: Sign(body)).Receive(CancellationToken.None));
        Assert.IsType<OkResult>(await harness.Webhook(body, signature: Sign(body)).Receive(CancellationToken.None));

        // No app secret configured: nothing can be verified, so nothing is accepted.
        Assert.False(WhatsAppWebhookProcessor.ComputeIsValid("", Encoding.UTF8.GetBytes(body), Sign(body)));
    }

    [Fact]
    public async Task Webhook_Handshake_EchoesTheChallengeOnlyForTheVerifyToken()
    {
        var harness = await Harness.CreateAsync();
        var webhook = harness.Webhook("", null);

        Assert.Equal("1158201444", Assert.IsType<ContentResult>(webhook.Verify("subscribe", VerifyToken, "1158201444")).Content);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(webhook.Verify("subscribe", "wrong", "1")).StatusCode);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(webhook.Verify("unsubscribe", VerifyToken, "1")).StatusCode);
    }

    [Fact]
    public async Task Webhook_RecordsDeliveryStatusByMetaMessageId_WithoutGoingBackwards()
    {
        var harness = await Harness.CreateAsync();
        var row = await harness.SentRowAsync("wamid.42");

        await harness.PostWebhookAsync(StatusPayload("wamid.42", "read"));
        await harness.PostWebhookAsync(StatusPayload("wamid.42", "delivered")); // late, out of order

        var saved = await harness.NewContext().WhatsAppOutboxMessages.IgnoreQueryFilters().SingleAsync(x => x.Id == row.Id);
        Assert.Equal(WhatsAppOutboxStatuses.Read, saved.Status);
        Assert.NotNull(saved.ReadAt);
        Assert.NotNull(saved.DeliveredAt);

        await harness.PostWebhookAsync("""{"entry":[{"changes":[{"value":{"statuses":[{"id":"wamid.42","status":"failed","timestamp":"1760000000","errors":[{"code":131026,"title":"Message undeliverable"}]}]}}]}]}""");
        saved = await harness.NewContext().WhatsAppOutboxMessages.IgnoreQueryFilters().SingleAsync(x => x.Id == row.Id);
        Assert.Equal(WhatsAppOutboxStatuses.Failed, saved.Status);
        Assert.Contains("131026", saved.ErrorMessage);
    }

    [Fact]
    public async Task PlatformSwitchOff_QueuesNothing()
    {
        var harness = await Harness.CreateAsync();
        await harness.OptInUserAsync(harness.Alice, AliceNumber);
        var off = Microsoft.Extensions.Options.Options.Create(new PlatformFeatureOptions { WhatsApp = false });
        var result = await new WhatsAppOutbox(harness.NewContext(), harness.Options, NullLogger<WhatsAppOutbox>.Instance, off)
            .QueueAsync(new WhatsAppQueueRequest(harness.TenantId, "project.muster-missing", AliceNumber, "Alice",
                new Dictionary<string, string?> { ["ProjectName"] = "Westlands Mall", ["WorkDate"] = "2026-10-21" }));
        Assert.Equal(WhatsAppQueueOutcome.FeatureOff, result.Outcome);
        Assert.Empty(await harness.NewContext().WhatsAppOutboxMessages.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Stop_OptsTheNumberOutForEveryTenant_AndStartRestoresIt()
    {
        var harness = await Harness.CreateAsync();
        await harness.OptInUserAsync(harness.Alice, AliceNumber);
        var otherTenantUser = await harness.AddUserAsync(harness.OtherTenantId, "Alice Elsewhere");
        await harness.OptInUserAsync(otherTenantUser, AliceNumber);
        // A message queued before STOP arrives.
        var queuedEarlier = await harness.QueueAsync(harness.TenantId, "project.muster-missing");
        Assert.Equal(WhatsAppQueueOutcome.Queued, queuedEarlier.Outcome);

        await harness.PostWebhookAsync(MessagePayload("254712345678", "Stop "));

        Assert.Equal(WhatsAppQueueOutcome.OptedOut, (await harness.QueueAsync(harness.TenantId, "project.muster-missing")).Outcome);
        Assert.Equal(WhatsAppQueueOutcome.OptedOut, (await harness.QueueAsync(harness.OtherTenantId, "project.muster-missing")).Outcome);
        await harness.ProcessAsync();
        var skipped = await harness.NewContext().WhatsAppOutboxMessages.IgnoreQueryFilters().SingleAsync(x => x.Id == queuedEarlier.MessageId);
        Assert.Equal(WhatsAppOutboxStatuses.Skipped, skipped.Status);
        Assert.Empty(harness.Sender.Sent);
        // The user's own status shows it.
        Assert.True(Ok(await harness.MyWhatsApp(harness.Alice).Get(CancellationToken.None)).BlockedByStop);

        // Any other reply is only logged, never acted on.
        await harness.PostWebhookAsync(MessagePayload("254712345678", "When is the technician coming?"));
        Assert.Single(await harness.NewContext().WhatsAppOptOuts.ToListAsync());

        await harness.PostWebhookAsync(MessagePayload("254712345678", "START"));
        Assert.Equal(WhatsAppQueueOutcome.Queued, (await harness.QueueAsync(harness.OtherTenantId, "project.muster-missing")).Outcome);
    }

    // ── Opt-in, consent and allowed keys ───────────────────────────────────────

    [Fact]
    public async Task Staff_GetNothingWithoutOptInAllowedKeyAndTheFeature()
    {
        var harness = await Harness.CreateAsync();
        var channel = new WhatsAppNotificationChannel(harness.Db, harness.Outbox(), NullLogger<WhatsAppNotificationChannel>.Instance);
        var dispatcher = new NotificationDispatcher(harness.Db, null!, null!, null!, new NullInboxNotifier(), [channel], NullLogger<NotificationDispatcher>.Instance);
        NotificationRequest Request(string key) => new(harness.TenantId, key, [harness.Alice], "Muster", "Not marked", null, "Project", Guid.NewGuid(),
            new Dictionary<string, string?> { ["ProjectName"] = "Westlands Mall", ["WorkDate"] = "2026-10-21" }, Channels: NotificationChannels.WhatsApp);
        harness.Db.UserNotificationPreferences.AddRange(
            new UserNotificationPreference { TenantId = harness.TenantId, UserId = harness.Alice, NotificationKey = "project.muster-missing", WhatsAppEnabled = true },
            new UserNotificationPreference { TenantId = harness.TenantId, UserId = harness.Alice, NotificationKey = "project.task-due", WhatsAppEnabled = true });
        await harness.Db.SaveChangesAsync();

        // A number on the account is not an opt-in.
        var alice = await harness.Db.Users.SingleAsync(x => x.Id == harness.Alice);
        alice.WhatsAppNumber = AliceNumber;
        await harness.Db.SaveChangesAsync();
        await dispatcher.NotifyAsync(Request("project.muster-missing"));
        Assert.Equal(0, await harness.CountRowsAsync());

        await harness.OptInUserAsync(harness.Alice, AliceNumber);
        // project.task-due can use WhatsApp but isn't among this tenant's allowed keys (the defaults).
        await dispatcher.NotifyAsync(Request("project.task-due"));
        Assert.Equal(0, await harness.CountRowsAsync());

        await dispatcher.NotifyAsync(Request("project.muster-missing"));
        var row = await harness.Db.WhatsAppOutboxMessages.SingleAsync();
        Assert.Equal(("muster_missing", AliceNumber, harness.Alice), (row.TemplateName, row.ToPhone, row.RecipientUserId));
        Assert.Equal("[\"Acme Builders\",\"Westlands Mall\",\"2026-10-21\"]", row.ParametersJson);

        // The tenant's flag turned off stops everything.
        await harness.SetFeatureAsync(harness.TenantId, false);
        await dispatcher.NotifyAsync(Request("project.muster-missing"));
        Assert.Equal(1, await harness.CountRowsAsync());

        // Opting out stops it too.
        await harness.SetFeatureAsync(harness.TenantId, true);
        await harness.MyWhatsApp(harness.Alice).OptOut(CancellationToken.None);
        await dispatcher.NotifyAsync(Request("project.muster-missing"));
        Assert.Equal(1, await harness.CountRowsAsync());
    }

    [Fact]
    public async Task Clients_OnlyMarkedContactsWithConsent_ForAllowedKeys()
    {
        var harness = await Harness.CreateAsync();
        var client = new Client { TenantId = harness.TenantId, ClientName = "Westlands Mall Ltd" };
        var marked = new ClientPortalUser { TenantId = harness.TenantId, ClientId = client.Id, FullName = "Grace", Email = "grace@client.test" };
        var unmarked = new ClientPortalUser { TenantId = harness.TenantId, ClientId = client.Id, FullName = "Hassan", Email = "hassan@client.test", Phone = "0722 000 111" };
        harness.Db.AddRange(client, marked, unmarked);
        await harness.Db.SaveChangesAsync();
        var notifier = new ClientWhatsAppNotifier(harness.Db, harness.Outbox(), NullLogger<ClientWhatsAppNotifier>.Instance);
        var values = new Dictionary<string, string?> { ["TechnicianName"] = "Bob", ["WorkOrderNumber"] = "WO-7", ["TrackingLink"] = "https://x/track/abc" };

        // Nobody has agreed yet.
        await notifier.NotifyClientAsync(harness.TenantId, client.Id, ClientWhatsAppKeys.TechnicianOnTheWay, values, "WorkOrder", Guid.NewGuid());
        Assert.Equal(0, await harness.CountRowsAsync());

        // Recording consent needs a method.
        var consent = harness.Consent(harness.Bob);
        await Assert.ThrowsAsync<BusinessRuleException>(() => consent.Record(client.Id, marked.Id, new RecordWhatsAppConsentRequest("0733 444 555", true, null), CancellationToken.None));
        var recorded = Ok(await consent.Record(client.Id, marked.Id, new RecordWhatsAppConsentRequest("0733 444 555", true, "phone"), CancellationToken.None));
        Assert.Equal(("+254733444555", "Phone", "Bob"), (recorded.Number, recorded.ConsentMethod, recorded.ConsentRecordedBy));

        await notifier.NotifyClientAsync(harness.TenantId, client.Id, ClientWhatsAppKeys.TechnicianOnTheWay, values, "WorkOrder", Guid.NewGuid());
        var row = await harness.Db.WhatsAppOutboxMessages.SingleAsync();
        Assert.Equal((marked.Id, "technician_on_the_way", "+254733444555"), (row.RecipientPortalUserId!.Value, row.TemplateName, row.ToPhone));

        // invoice-issued isn't allowed by default, and another tenant's client id finds nobody.
        await notifier.NotifyClientAsync(harness.TenantId, client.Id, ClientWhatsAppKeys.InvoiceIssued, values, "TenantInvoice", Guid.NewGuid());
        await notifier.NotifyClientAsync(harness.OtherTenantId, client.Id, ClientWhatsAppKeys.TechnicianOnTheWay, values, "WorkOrder", Guid.NewGuid());
        Assert.Equal(1, await harness.CountRowsAsync());

        // Once allowed, an invoice goes once per contact however often the hook runs.
        await harness.Settings().SaveSettings(new SaveWhatsAppTenantSettingsRequest([ClientWhatsAppKeys.InvoiceIssued]), CancellationToken.None);
        var invoiceId = Guid.NewGuid();
        await notifier.NotifyClientAsync(harness.TenantId, client.Id, ClientWhatsAppKeys.InvoiceIssued, values, "TenantInvoice", invoiceId, onceScope: invoiceId.ToString("N"));
        await notifier.NotifyClientAsync(harness.TenantId, client.Id, ClientWhatsAppKeys.InvoiceIssued, values, "TenantInvoice", invoiceId, onceScope: invoiceId.ToString("N"));
        Assert.Equal(2, await harness.CountRowsAsync());

        // Turned off: no more messages, but consent history stays.
        var off = Ok(await consent.Record(client.Id, marked.Id, new RecordWhatsAppConsentRequest(null, false, null), CancellationToken.None));
        Assert.False(off.ReceivesWhatsApp);
        Assert.NotNull(off.ConsentAt);
        await notifier.NotifyClientAsync(harness.TenantId, client.Id, ClientWhatsAppKeys.InvoiceIssued, values, "TenantInvoice", Guid.NewGuid());
        Assert.Equal(2, await harness.CountRowsAsync());
    }

    [Fact]
    public async Task Settings_ListKeys_RejectUnknownOnes_AndPreferencesOfferWhatsAppOnlyWhereAllowed()
    {
        var harness = await Harness.CreateAsync();
        var settings = Ok(await harness.Settings().GetSettings(CancellationToken.None));
        Assert.True(settings.FeatureEnabled);
        Assert.Equal(1000, settings.MonthlyCap);
        Assert.Equal(WhatsAppTemplateCatalogue.DefaultAllowedKeys.OrderBy(x => x), settings.Keys.Where(k => k.Allowed).Select(k => k.Key).OrderBy(x => x));
        await Assert.ThrowsAsync<BusinessRuleException>(() => harness.Settings().SaveSettings(new SaveWhatsAppTenantSettingsRequest(["nope"]), CancellationToken.None));

        var preferences = new UserNotificationPreferencesController(harness.TenantDb(harness.Alice), harness.Context(harness.Alice));
        var list = Ok(await preferences.Get(CancellationToken.None));
        Assert.Contains("WhatsApp", list.Single(x => x.NotificationKey == "project.muster-missing").Channels);
        Assert.DoesNotContain("WhatsApp", list.Single(x => x.NotificationKey == "project.task-due").Channels);
        Assert.False(list.Single(x => x.NotificationKey == "project.muster-missing").WhatsAppEnabled);

        await harness.SetFeatureAsync(harness.TenantId, false);
        list = Ok(await preferences.Get(CancellationToken.None));
        Assert.DoesNotContain(list, x => x.Channels.Contains("WhatsApp"));
    }

    // ── Cap ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MonthlyCap_SkipsWhatsAppBeyondIt_PerTenant()
    {
        var harness = await Harness.CreateAsync(cap: 2);

        Assert.Equal(WhatsAppQueueOutcome.Queued, (await harness.QueueAsync(harness.TenantId, "project.muster-missing")).Outcome);
        Assert.Equal(WhatsAppQueueOutcome.Queued, (await harness.QueueAsync(harness.TenantId, "project.muster-missing")).Outcome);
        var over = await harness.QueueAsync(harness.TenantId, "project.muster-missing");
        Assert.Equal(WhatsAppQueueOutcome.OverCap, over.Outcome);
        var skipped = await harness.Db.WhatsAppOutboxMessages.SingleAsync(x => x.Id == over.MessageId);
        Assert.Equal(WhatsAppOutboxStatuses.Skipped, skipped.Status);
        Assert.Contains("limit", skipped.ErrorMessage);
        // Skipped rows don't count, and the other tenant has its own allowance.
        Assert.Equal(2, await harness.Outbox().CountThisMonthAsync(harness.TenantId, DateTime.UtcNow));
        Assert.Equal(WhatsAppQueueOutcome.Queued, (await harness.QueueAsync(harness.OtherTenantId, "project.muster-missing")).Outcome);

        await harness.ProcessAsync();
        Assert.Equal(3, harness.Sender.Sent.Count);
    }

    // ── Outbox worker ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Outbox_RetriesWithBackoff_ThenFailsForGood()
    {
        var harness = await Harness.CreateAsync();
        harness.Sender.Next = WhatsAppSendResult.Failed("Meta returned 503", retryable: true);
        var id = (await harness.QueueAsync(harness.TenantId, "project.muster-missing")).MessageId!.Value;
        var now = DateTime.UtcNow;

        await harness.ProcessAsync(now);
        var row = await harness.ReloadAsync(id);
        Assert.Equal((WhatsAppOutboxStatuses.Pending, 1), (row.Status, row.AttemptCount));
        Assert.Equal(now.AddMinutes(1), row.NextAttemptAt);

        await harness.ProcessAsync(now.AddSeconds(30)); // not due yet
        Assert.Equal(1, (await harness.ReloadAsync(id)).AttemptCount);

        var expectedDelays = new[] { 5, 15, 60 };
        var at = now.AddMinutes(1);
        foreach (var delay in expectedDelays)
        {
            await harness.ProcessAsync(at);
            row = await harness.ReloadAsync(id);
            Assert.Equal(WhatsAppOutboxStatuses.Pending, row.Status);
            Assert.Equal(at.AddMinutes(delay), row.NextAttemptAt);
            at = row.NextAttemptAt!.Value;
        }

        await harness.ProcessAsync(at);
        row = await harness.ReloadAsync(id);
        Assert.Equal((WhatsAppOutboxStatuses.Failed, 5), (row.Status, row.AttemptCount));
        Assert.NotNull(row.FailedAt);
        Assert.Equal("Meta returned 503", row.ErrorMessage);
        Assert.Equal(5, harness.Sender.Calls);

        // The tenant's log shows the failure, with the number masked.
        var log = Ok(await harness.Settings().Outbox(null, null, null, CancellationToken.None));
        var entry = Assert.Single(log.Items);
        Assert.Equal((WhatsAppOutboxStatuses.Failed, "Muster roll not marked"), (entry.Status, entry.Label));
        Assert.DoesNotContain("345", entry.To);
    }

    [Fact]
    public async Task Outbox_RefusedMessagesFailAtOnce_SuccessRecordsTheId_AndUnconfiguredSkips()
    {
        var harness = await Harness.CreateAsync();
        harness.Sender.Next = WhatsAppSendResult.Failed("Template not approved", retryable: false);
        var refused = (await harness.QueueAsync(harness.TenantId, "project.muster-missing")).MessageId!.Value;
        await harness.ProcessAsync();
        Assert.Equal((WhatsAppOutboxStatuses.Failed, 1), ((await harness.ReloadAsync(refused)).Status, (await harness.ReloadAsync(refused)).AttemptCount));

        harness.Sender.Next = null;
        var ok = (await harness.QueueAsync(harness.TenantId, "project.muster-missing")).MessageId!.Value;
        await harness.ProcessAsync();
        var sent = await harness.ReloadAsync(ok);
        Assert.Equal(WhatsAppOutboxStatuses.Sent, sent.Status);
        Assert.StartsWith("wamid.", sent.ProviderMessageId);
        var call = harness.Sender.Sent.Last();
        Assert.Equal(("muster_missing", AliceNumber), (call.Template, call.To));
        Assert.Equal("Acme Builders", call.Parameters[0]);

        // Abandoned mid-send by a stopped instance: back in the queue.
        var abandoned = (await harness.QueueAsync(harness.TenantId, "project.muster-missing")).MessageId!.Value;
        await using (var db = harness.NewContext())
        {
            var row = await db.WhatsAppOutboxMessages.SingleAsync(x => x.Id == abandoned);
            row.Status = WhatsAppOutboxStatuses.Sending;
            row.LastAttemptAt = DateTime.UtcNow.AddMinutes(-30);
            await db.SaveChangesAsync();
        }
        await harness.ProcessAsync(DateTime.UtcNow);
        Assert.Equal((WhatsAppOutboxStatuses.Pending, 1), ((await harness.ReloadAsync(abandoned)).Status, (await harness.ReloadAsync(abandoned)).AttemptCount));

        var unconfigured = await Harness.CreateAsync(configured: false);
        var skipped = (await unconfigured.QueueAsync(unconfigured.TenantId, "project.muster-missing")).MessageId!.Value;
        await unconfigured.ProcessAsync();
        Assert.Equal(WhatsAppOutboxStatuses.Skipped, (await unconfigured.ReloadAsync(skipped)).Status);
        Assert.Empty(unconfigured.Sender.Sent);
    }

    // ── Number confirmation ────────────────────────────────────────────────────

    [Fact]
    public async Task Verification_SendsAHashedCode_OptsInOnSuccess_AndEnablesAllowedKeys()
    {
        var harness = await Harness.CreateAsync();
        var me = harness.MyWhatsApp(harness.Alice);
        await harness.PostWebhookAsync(MessagePayload("254712345678", "STOP")); // an old STOP on this number

        var started = Ok(await me.Start(new WhatsAppStartRequest("0712 345 678"), CancellationToken.None));
        Assert.Equal(AliceNumber, started.PhoneNumber);
        await harness.ProcessAsync();
        var sent = Assert.Single(harness.Sender.Sent);
        Assert.Equal("ecosys_verification_code", sent.Template);
        var code = sent.Parameters[0];
        Assert.Matches("^[0-9]{6}$", code);
        Assert.Equal(new[] { code }, sent.ButtonParameters!);
        // Stored only as a hash, and dropped from the outbox once sent.
        var stored = await harness.NewContext().WhatsAppVerificationCodes.IgnoreQueryFilters().SingleAsync();
        Assert.DoesNotContain(code, stored.CodeHash);
        Assert.Null((await harness.NewContext().WhatsAppOutboxMessages.IgnoreQueryFilters().SingleAsync()).ParametersJson);

        var status = Ok(await me.Verify(new WhatsAppVerifyRequest(code), CancellationToken.None));
        Assert.True(status.OptedIn);
        Assert.False(status.BlockedByStop); // confirming again is fresh consent
        Assert.Equal(AliceNumber, status.Number);
        var prefs = await harness.NewContext().UserNotificationPreferences.IgnoreQueryFilters().Where(x => x.UserId == harness.Alice).ToListAsync();
        Assert.True(prefs.Single(x => x.NotificationKey == "project.muster-missing").WhatsAppEnabled);
        Assert.True(prefs.Single(x => x.NotificationKey == "work-order.assigned").WhatsAppEnabled);
        Assert.DoesNotContain(prefs, x => x.NotificationKey == "project.task-due"); // not allowed by the tenant

        // A used code can't be used again.
        await Assert.ThrowsAsync<BusinessRuleException>(() => me.Verify(new WhatsAppVerifyRequest(code), CancellationToken.None));
    }

    [Fact]
    public async Task Verification_ExpiresAfterTenMinutes_AndAllowsFiveAttempts()
    {
        var harness = await Harness.CreateAsync();
        var subject = WhatsAppSubject.ForUser(harness.TenantId, harness.Alice);
        var service = harness.Verification();

        await service.StartAsync(subject, "0712345678");
        await harness.ProcessAsync();
        var code = harness.Sender.Sent.Single().Parameters[0];
        var wrong = code == "000000" ? "111111" : "000000";

        // Asking again straight away is refused.
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.StartAsync(subject, "0712345678"));

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => service.VerifyAsync(subject, wrong));
            Assert.Contains($"{5 - attempt} attempt", error.Message);
        }
        Assert.Contains("Too many", (await Assert.ThrowsAsync<BusinessRuleException>(() => service.VerifyAsync(subject, wrong))).Message);
        // Locked: even the right code no longer works.
        Assert.Contains("Too many", (await Assert.ThrowsAsync<BusinessRuleException>(() => service.VerifyAsync(subject, code))).Message);

        // A new code, which then expires.
        harness.Clock.Advance(TimeSpan.FromMinutes(2));
        await service.StartAsync(subject, "0712345678");
        await harness.ProcessAsync();
        var second = harness.Sender.Sent.Last().Parameters[0];
        harness.Clock.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));
        Assert.Contains("expired", (await Assert.ThrowsAsync<BusinessRuleException>(() => service.VerifyAsync(subject, second))).Message);
        Assert.Null((await harness.NewContext().Users.IgnoreQueryFilters().SingleAsync(x => x.Id == harness.Alice)).WhatsAppVerifiedAt);
    }

    [Fact]
    public async Task Verification_PortalContactOptsInThemselves()
    {
        var harness = await Harness.CreateAsync();
        var client = new Client { TenantId = harness.TenantId, ClientName = "Westlands Mall Ltd" };
        var contact = new ClientPortalUser { TenantId = harness.TenantId, ClientId = client.Id, FullName = "Grace", Email = "grace@client.test" };
        harness.Db.AddRange(client, contact);
        await harness.Db.SaveChangesAsync();
        var subject = WhatsAppSubject.ForPortalUser(harness.TenantId, contact.Id);

        await harness.Verification().StartAsync(subject, "+254 733 444 555");
        await harness.ProcessAsync();
        await harness.Verification().VerifyAsync(subject, harness.Sender.Sent.Single().Parameters[0]);

        var saved = await harness.NewContext().ClientPortalUsers.IgnoreQueryFilters().SingleAsync(x => x.Id == contact.Id);
        Assert.True(saved.ReceivesWhatsApp);
        Assert.Equal((WhatsAppConsentMethods.Portal, "+254733444555"), (saved.WhatsAppConsentMethod, saved.WhatsAppNumber));
        Assert.NotNull(saved.WhatsAppConsentAt);
        Assert.Null(saved.WhatsAppConsentRecordedByUserId);
    }

    [Fact]
    public async Task Verification_RefusedWhenTheWorkspaceHasNoWhatsApp()
    {
        var harness = await Harness.CreateAsync();
        await harness.SetFeatureAsync(harness.TenantId, false);
        await Assert.ThrowsAsync<BusinessRuleException>(() => harness.Verification().StartAsync(WhatsAppSubject.ForUser(harness.TenantId, harness.Alice), "0712345678"));
        var unconfigured = await Harness.CreateAsync(configured: false);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => unconfigured.Verification()
            .StartAsync(WhatsAppSubject.ForUser(unconfigured.TenantId, unconfigured.Alice), "0712345678"));
        Assert.Contains("isn't available", error.Message);
        Assert.Equal(0, await harness.CountRowsAsync());
    }

    // ── Licensing ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("MyWhatsApp")]
    [InlineData("WhatsAppNotifications")]
    [InlineData("ClientWhatsAppConsent")]
    [InlineData("WhatsAppWebhook")]
    public void Controllers_AreSharedAcrossPlans(string controller)
    {
        Assert.True(ServiceOpsPlans.IsClassified(controller));
        Assert.Contains(controller, ServiceOpsPlans.SharedControllers);
    }

    // ── PostgreSQL ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "PostgreSql")]
    public async Task Postgres_OptInQueueSendWebhookAndStop_AllTranslate()
    {
        await WithPostgresAsync(async connection =>
        {
            var harness = await Harness.CreateAsync(context => PostgresContext(connection, context), seedTenants: true);

            // Confirm a number, which opts in and turns WhatsApp on for the allowed keys.
            var subject = WhatsAppSubject.ForUser(harness.TenantId, harness.Alice);
            await harness.Verification().StartAsync(subject, "0712 345 678");
            await harness.ProcessAsync();
            await harness.Verification().VerifyAsync(subject, harness.Sender.Sent.Single().Parameters[0]);
            Assert.True(Ok(await harness.MyWhatsApp(harness.Alice).Get(CancellationToken.None)).OptedIn);

            // A muster reminder through the real dispatcher and channel.
            await using (var db = harness.NewContext())
            {
                var channel = new WhatsAppNotificationChannel(db, harness.Outbox(db), NullLogger<WhatsAppNotificationChannel>.Instance);
                var dispatcher = new NotificationDispatcher(db, null!, null!, null!, new NullInboxNotifier(), [channel], NullLogger<NotificationDispatcher>.Instance);
                await dispatcher.NotifyAsync(new NotificationRequest(harness.TenantId, "project.muster-missing", [harness.Alice], "Muster", "Not marked", null, "Project", Guid.NewGuid(),
                    new Dictionary<string, string?> { ["ProjectName"] = "Westlands Mall", ["WorkDate"] = "2026-10-21" }, Channels: NotificationChannels.WhatsApp));
            }
            await harness.ProcessAsync();
            var muster = harness.Sender.Sent.Last();
            Assert.Equal(("muster_missing", AliceNumber), (muster.Template, muster.To));

            var row = await harness.NewContext().WhatsAppOutboxMessages.IgnoreQueryFilters().SingleAsync(x => x.TemplateName == "muster_missing");
            await harness.PostWebhookAsync(StatusPayload(row.ProviderMessageId!, "delivered"));
            Assert.Equal(WhatsAppOutboxStatuses.Delivered, (await harness.ReloadAsync(row.Id)).Status);

            var settings = Ok(await harness.Settings().GetSettings(CancellationToken.None));
            Assert.Equal(2, settings.UsedThisMonth);
            Assert.Equal(2, Ok(await harness.Settings().Outbox(WhatsAppOutboxStatuses.Delivered, null, null, CancellationToken.None)).Items.Count
                + Ok(await harness.Settings().Outbox(WhatsAppOutboxStatuses.Sent, null, null, CancellationToken.None)).Items.Count);

            await harness.PostWebhookAsync(MessagePayload("254712345678", "STOP"));
            await harness.PostWebhookAsync(MessagePayload("254712345678", "STOP")); // twice is fine
            Assert.Equal(WhatsAppQueueOutcome.OptedOut, (await harness.QueueAsync(harness.TenantId, "project.muster-missing")).Outcome);
            await harness.PostWebhookAsync(MessagePayload("254712345678", "START"));
            Assert.Equal(WhatsAppQueueOutcome.Queued, (await harness.QueueAsync(harness.TenantId, "project.muster-missing")).Outcome);
        });
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static T Ok<T>(ActionResult<T> result) =>
        Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private static string Sign(string body, string secret = AppSecret) =>
        "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();

    private static string StatusPayload(string messageId, string status) =>
        $$$"""{"object":"whatsapp_business_account","entry":[{"id":"1","changes":[{"field":"messages","value":{"messaging_product":"whatsapp","statuses":[{"id":"{{{messageId}}}","status":"{{{status}}}","timestamp":"1760000000","recipient_id":"254712345678"}]}}]}]}""";

    private static string MessagePayload(string from, string text) =>
        $$$"""{"object":"whatsapp_business_account","entry":[{"id":"1","changes":[{"field":"messages","value":{"messaging_product":"whatsapp","messages":[{"from":"{{{from}}}","id":"wamid.in","timestamp":"1760000000","type":"text","text":{"body":"{{{text}}}"}}]}}]}]}""";

    private static readonly Guid ProfessionalPlanId = Guid.Parse("55555555-5555-5555-5555-555555555562");

    private sealed class Harness
    {
        private Func<ITenantContext?, AppDbContext> createContext = null!;
        public AppDbContext Db { get; private set; } = null!;
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid OtherTenantId { get; } = Guid.NewGuid();
        public Guid Alice { get; private set; }
        public Guid Bob { get; private set; }
        public FakeSender Sender { get; } = new();
        public FakeClock Clock { get; } = new(DateTimeOffset.UtcNow);
        public IOptions<WhatsAppOptions> Options { get; private set; } = null!;

        public static Task<Harness> CreateAsync(int cap = 1000, bool configured = true)
        {
            var databaseName = $"ecosys-whatsapp-{Guid.NewGuid():N}";
            return CreateAsync(context => new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(databaseName).Options, context),
                seedTenants: true, cap, configured);
        }

        public static async Task<Harness> CreateAsync(Func<ITenantContext?, AppDbContext> createContext, bool seedTenants, int cap = 1000, bool configured = true)
        {
            var harness = new Harness { createContext = createContext };
            harness.Options = Microsoft.Extensions.Options.Options.Create(new WhatsAppOptions
            {
                Enabled = configured, PhoneNumberId = configured ? "1234567890" : null, AccessToken = configured ? "token" : null,
                AppSecret = AppSecret, WebhookVerifyToken = VerifyToken, MonthlyCapPerTenant = cap, DisplayPhoneNumber = "+254 700 000 000",
            });
            await using (var seed = createContext(null))
            {
                if (seedTenants)
                    seed.Tenants.AddRange(TestTenant(harness.TenantId, "Acme Builders"), TestTenant(harness.OtherTenantId, "Other Co"));
                seed.TenantLicenses.AddRange(
                    new TenantLicense { TenantId = harness.TenantId, LicensePlanId = ProfessionalPlanId, Status = "Active", IsPerpetual = true, WhatsAppAddOn = ServiceOpsPlans.WhatsAppNotificationsAddOn },
                    new TenantLicense { TenantId = harness.OtherTenantId, LicensePlanId = ProfessionalPlanId, Status = "Active", IsPerpetual = true, WhatsAppAddOn = ServiceOpsPlans.WhatsAppNotificationsAddOn });
                await seed.SaveChangesAsync();
            }

            harness.Db = createContext(null);
            harness.Alice = await harness.AddUserAsync(harness.TenantId, "Alice Wanjiru");
            harness.Bob = await harness.AddUserAsync(harness.TenantId, "Bob");
            return harness;
        }

        public async Task<Guid> AddUserAsync(Guid tenantId, string name)
        {
            var user = new User
            {
                TenantId = tenantId, FullName = name, Email = $"{Guid.NewGuid():N}@acme.test", Role = AppRoles.Admin, IsActive = true,
            };
            Db.Users.Add(user);
            await Db.SaveChangesAsync();
            return user.Id;
        }

        public async Task OptInUserAsync(Guid userId, string number)
        {
            var user = await Db.Users.IgnoreQueryFilters().SingleAsync(x => x.Id == userId);
            user.WhatsAppNumber = number;
            user.WhatsAppVerifiedAt = DateTime.UtcNow;
            user.WhatsAppOptedInAt = DateTime.UtcNow;
            user.WhatsAppOptedOutAt = null;
            await Db.SaveChangesAsync();
        }

        public async Task SetFeatureAsync(Guid tenantId, bool enabled)
        {
            await using var db = NewContext();
            (await db.TenantLicenses.SingleAsync(x => x.TenantId == tenantId)).WhatsAppAddOn = enabled ? ServiceOpsPlans.WhatsAppNotificationsAddOn : null;
            await db.SaveChangesAsync();
        }

        public AppDbContext NewContext() => createContext(null);

        public AppDbContext TenantDb(Guid userId) => createContext(Context(userId));

        public ITenantContext Context(Guid userId) => new FakeTenantContext(TenantId, userId);

        public IWhatsAppOutbox Outbox(AppDbContext? db = null) => new WhatsAppOutbox(db ?? Db, Options, NullLogger<WhatsAppOutbox>.Instance);

        public IWhatsAppAccountResolver Resolver() => new SharedWhatsAppAccountResolver(Options);

        public IWhatsAppVerificationService Verification()
        {
            var db = NewContext();
            return new WhatsAppVerificationService(db, Outbox(db), Resolver(), Clock);
        }

        public Task<WhatsAppQueueResult> QueueAsync(Guid tenantId, string key) =>
            Outbox(NewContext()).QueueAsync(new WhatsAppQueueRequest(tenantId, key, AliceNumber, "Alice",
                new Dictionary<string, string?> { ["ProjectName"] = "Westlands Mall", ["WorkDate"] = "2026-10-21" }));

        public async Task<int> ProcessAsync(DateTime? now = null)
        {
            await using var db = NewContext();
            return await new WhatsAppOutboxProcessor(db, Resolver(), Sender, NullLogger<WhatsAppOutboxProcessor>.Instance)
                .ProcessPendingBatchAsync(50, now ?? DateTime.UtcNow);
        }

        public async Task<WhatsAppOutboxMessage> ReloadAsync(Guid id)
        {
            await using var db = NewContext();
            return await db.WhatsAppOutboxMessages.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == id);
        }

        public async Task<int> CountRowsAsync()
        {
            await using var db = NewContext();
            return await db.WhatsAppOutboxMessages.IgnoreQueryFilters().CountAsync();
        }

        public async Task<WhatsAppOutboxMessage> SentRowAsync(string providerMessageId)
        {
            var row = new WhatsAppOutboxMessage
            {
                TenantId = TenantId, NotificationKey = "project.muster-missing", TemplateName = "muster_missing", ToPhone = AliceNumber,
                Status = WhatsAppOutboxStatuses.Sent, SentAt = DateTime.UtcNow, ProviderMessageId = providerMessageId, AttemptCount = 1,
            };
            await using var db = NewContext();
            db.WhatsAppOutboxMessages.Add(row);
            await db.SaveChangesAsync();
            return row;
        }

        public WhatsAppWebhookController Webhook(string body, string? signature)
        {
            var http = new DefaultHttpContext();
            http.Request.Method = "POST";
            http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
            http.Request.ContentLength = Encoding.UTF8.GetByteCount(body);
            if (signature is not null)
                http.Request.Headers["X-Hub-Signature-256"] = signature;
            var processor = new WhatsAppWebhookProcessor(NewContext(), Options, NullLogger<WhatsAppWebhookProcessor>.Instance);
            return new WhatsAppWebhookController(processor, NullLogger<WhatsAppWebhookController>.Instance)
            {
                ControllerContext = new ControllerContext { HttpContext = http },
            };
        }

        public async Task PostWebhookAsync(string body) =>
            Assert.IsType<OkResult>(await Webhook(body, Sign(body)).Receive(CancellationToken.None));

        public MyWhatsAppController MyWhatsApp(Guid userId)
        {
            var db = TenantDb(userId);
            return new MyWhatsAppController(db, Context(userId), new WhatsAppVerificationService(db, Outbox(db), Resolver(), Clock), Resolver(), Options);
        }

        public WhatsAppNotificationsController Settings()
        {
            var db = TenantDb(Alice);
            return new WhatsAppNotificationsController(db, Context(Alice), new AllowAllAccess(), new NoOpAuditLogService(), Outbox(db), Resolver(), Options);
        }

        public ClientWhatsAppConsentController Consent(Guid userId) =>
            new(TenantDb(userId), Context(userId), new AllowAllAccess(), new NoOpAuditLogService());
    }

    private static Tenant TestTenant(Guid id, string name) => new()
    {
        Id = id, Name = name, Slug = $"t-{id:N}"[..14], CompanyName = name, Email = $"{id:N}@tenant.test", Country = "Kenya",
        Status = "Active", LicenseStatus = "Active", PrimaryColor = "#0F4C81", SecondaryColor = "#F4B942", IsActive = true,
    };

    internal sealed class FakeSender : IWhatsAppSender
    {
        public WhatsAppSendResult? Next { get; set; }
        public int Calls { get; private set; }
        public List<(string To, string Template, IReadOnlyList<string> Parameters, IReadOnlyList<string>? ButtonParameters)> Sent { get; } = [];

        public Task<WhatsAppSendResult> SendTemplateAsync(WhatsAppSendingAccount account, string toE164, string templateName, string language,
            IReadOnlyList<string> bodyParameters, IReadOnlyList<string>? buttonParameters, CancellationToken cancellationToken)
        {
            Calls++;
            if (Next is { Succeeded: false } failure)
                return Task.FromResult(failure);
            Sent.Add((toE164, templateName, bodyParameters, buttonParameters));
            return Task.FromResult(WhatsAppSendResult.Sent($"wamid.{Guid.NewGuid():N}"));
        }
    }

    internal sealed class FakeClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset now = start;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan by) => now += by;
    }

    private sealed class RecordingHandler(HttpStatusCode status, string response) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class HandlerFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class FakeTenantContext(Guid tenantId, Guid userId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
        public Guid? SessionId => null;
        public string? Email => null;
        public string? Role => AppRoles.Admin;
        public string? JobTitle => null;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public bool IsAdmin => true;
        public bool HasRole(string role) => role == AppRoles.Admin;
        public bool HasPermission(string permissionName) => true;
        public Guid GetRequiredTenantId() => tenantId;
        public Guid GetRequiredUserId() => userId;
        public Guid GetRequiredSessionId() => throw new NotSupportedException();
    }

    private sealed class NoOpAuditLogService : IAuditLogService
    {
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, string severity = "Info", string? actorName = null, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class AllowAllAccess : IUserAccessService
    {
        public void EnsureAdmin() { }
        public void EnsureAdminOrPermission(string permissionName) { }
        public void EnsureAdminOrAnyPermission(params string[] permissionNames) { }
        public void EnsureTenantOperationalAccess() { }
    }

    private static AppDbContext PostgresContext(string connection, ITenantContext? tenantContext) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options, tenantContext);

    /// <summary>A freshly migrated database (ECOSYS_TEST_DB_CONNECTION, default localhost:5432 postgres/postgres).</summary>
    private static async Task WithPostgresAsync(Func<string, Task> body)
    {
        var server = Environment.GetEnvironmentVariable("ECOSYS_TEST_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=ecosys_test;Username=postgres;Password=postgres";
        var databaseName = $"ecosys_whatsapp_{Guid.NewGuid():N}";
        var database = new NpgsqlConnectionStringBuilder(server) { Database = databaseName, Pooling = false }.ConnectionString;
        var admin = new NpgsqlConnectionStringBuilder(server) { Database = "postgres", Pooling = false }.ConnectionString;

        await ExecuteAdminAsync(admin, $"CREATE DATABASE \"{databaseName}\"");
        try
        {
            await using (var setup = PostgresContext(database, null))
                await setup.Database.MigrateAsync();
            await body(database);
        }
        finally
        {
            await ExecuteAdminAsync(admin, $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)");
        }
    }

    private static async Task ExecuteAdminAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
