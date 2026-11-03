using System.Net;
using System.Net.Sockets;
using System.Text;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Integrations;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ecosys.Tests;

/// <summary>
/// One tenant, one system-generated communication address, and every tenant-scoped message sent as it —
/// through the platform's authenticated SMTP account, which is a separate (transport) identity.
/// </summary>
public sealed class TenantEmailIdentityTests
{
    private const string Melop = "melop@serviceops.ecosysdigital.co.ke";
    private const string Acme = "acme@serviceops.ecosysdigital.co.ke";
    private const string PlatformSender = "support@ecosysdigital.co.ke";

    [Fact]
    public void TenantSender_IsTheManagedAddress_WithCompanyNameAndReplyTo()
    {
        var tenant = NewTenant("melop", "Melop Ltd");
        var identity = TenantEmailIdentity.Create(tenant);

        var sender = TenantEmailIdentity.ResolveSender(tenant, identity);

        Assert.Equal(Melop, identity.ManagedEmailAddress);
        Assert.NotNull(sender);
        Assert.Equal(Melop, sender.FromAddress);
        Assert.Equal("Melop Ltd", sender.FromName);
        Assert.Equal(Melop, sender.ReplyTo);
    }

    [Fact]
    public void TenantSender_NeverFallsBackToALegacySenderAddress()
    {
        var tenant = NewTenant("melop", "Melop Ltd");
        var identity = TenantEmailIdentity.Create(tenant);
        identity.SenderAddress = PlatformSender;

        Assert.Equal(Melop, TenantEmailIdentity.ResolveSender(tenant, identity)!.FromAddress);
        // A legacy tenant with no identity row still gets its own slug address, not a platform one.
        Assert.Equal(Melop, TenantEmailIdentity.ResolveSender(tenant, null)!.FromAddress);
    }

    [Fact]
    public void NewIdentity_IsPending_UntilTheMailServerAcceptsIt()
    {
        var identity = TenantEmailIdentity.Create(NewTenant("melop", "Melop Ltd"));
        Assert.Equal(ManagedEmailStatuses.Pending, identity.ManagedEmailStatus);

        Assert.False(TenantEmailIdentity.RecordSenderOutcome(identity, false, EmailErrorCategories.Timeout, "timeout", DateTime.UtcNow));
        Assert.Equal(ManagedEmailStatuses.Pending, identity.ManagedEmailStatus);

        Assert.True(TenantEmailIdentity.RecordSenderOutcome(identity, false, EmailErrorCategories.SenderRejected, "Sender verify failed", DateTime.UtcNow));
        Assert.Equal(ManagedEmailStatuses.Rejected, identity.ManagedEmailStatus);
        Assert.Contains("Sender verify failed", identity.ManagedEmailStatusDetail);

        Assert.True(TenantEmailIdentity.RecordSenderOutcome(identity, true, null, null, DateTime.UtcNow));
        Assert.Equal(ManagedEmailStatuses.Verified, identity.ManagedEmailStatus);
        Assert.Null(identity.ManagedEmailStatusDetail);

        TenantEmailIdentity.MarkPending(identity);
        Assert.Equal(ManagedEmailStatuses.Pending, identity.ManagedEmailStatus);
    }

    [Fact]
    public async Task Smtp_AuthenticatesAsPlatformAccount_ButSendsAsTenantAddress()
    {
        await using var server = new FakeSmtpServer();
        var sender = new SmtpEmailSender(Options.Create(new SmtpOptions()), NullLogger<SmtpEmailSender>.Instance);

        await sender.SendAsync(
            new EmailMessage("client@example.test", "Invoice", "<p>hi</p>", "Melop Ltd", Melop, Melop, IsHtml: true),
            server.Settings(PlatformSender, "secret"));

        Assert.Equal(PlatformSender, server.AuthenticatedUser);
        Assert.Equal(Melop, server.MailFrom);
        Assert.Contains($"From: Melop Ltd <{Melop}>", server.Data);
        Assert.Contains($"Reply-To: {Melop}", server.Data);
        Assert.DoesNotContain($"From: {PlatformSender}", server.Data);
    }

    [Fact]
    public async Task Smtp_SenderVerifyFailure_IsReportedAsSenderRejected_WithoutRewritingFrom()
    {
        // The exact production refusal: Exim verifies the sender at RCPT time.
        await using var server = new FakeSmtpServer { RcptRefusal = $"550 Verification failed for <{Melop}> Sender verify failed" };
        var sender = new SmtpEmailSender(Options.Create(new SmtpOptions()), NullLogger<SmtpEmailSender>.Instance);

        var ex = await Assert.ThrowsAsync<EmailDeliveryException>(() => sender.SendAsync(
            new EmailMessage("admin@melop.test", "Invite", "hi", "Melop Ltd", Melop),
            server.Settings(PlatformSender, "secret")));

        Assert.Equal(EmailErrorCategories.SenderRejected, ex.ErrorCategory);
        Assert.Equal(Melop, server.MailFrom);
        Assert.Null(server.Data);
    }

    [Fact]
    public async Task SenderProbe_ReportsRefusal_AndNeverSendsAMessage()
    {
        await using var refusing = new FakeSmtpServer { RcptRefusal = "550 Sender verify failed" };
        var sender = new SmtpEmailSender(Options.Create(new SmtpOptions()), NullLogger<SmtpEmailSender>.Instance);

        var refused = await sender.VerifySenderAsync(refusing.Settings(PlatformSender, "secret"), Melop);

        Assert.False(refused.Success);
        Assert.Equal(EmailErrorCategories.SenderRejected, refused.ErrorCategory);
        Assert.Equal(Melop, refusing.MailFrom);
        Assert.Null(refusing.Data);

        await using var accepting = new FakeSmtpServer();
        var accepted = await sender.VerifySenderAsync(accepting.Settings(PlatformSender, "secret"), Melop);

        Assert.True(accepted.Success, accepted.TechnicalMessage);
        Assert.Equal(PlatformSender, accepting.AuthenticatedUser);
        Assert.Null(accepting.Data);
    }

    [Fact]
    public async Task Invitation_AndResend_UseExactlyTheTenantManagedAddress()
    {
        await using var db = CreateDb();
        var (melop, _) = SeedTenants(db);
        var user = new User { TenantId = melop.Id, FullName = "Mary", Email = "mary@melop.test", Role = "Admin", IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var capture = new CaptureSender();
        var delivery = CreateCredentialDelivery(db, capture);

        var invite = await delivery.SendPasswordResetLinkAsync(melop.Id, user, new PasswordResetLinkDeliveryRequest(
            delivery.BuildInviteUrl("t1"), DateTime.UtcNow.AddDays(3), PasswordLinkDeliveryPurpose.AccountSetup));
        var resend = await delivery.SendAsync(melop.Id, user, new UserCredentialDeliveryRequest(
            "resend-credentials", delivery.BuildLoginUrl(), null, delivery.BuildInviteUrl("t2"), true));

        Assert.True(invite.Success, invite.Message);
        Assert.True(resend.Success, resend.Message);
        Assert.Equal(2, capture.Messages.Count);
        Assert.All(capture.Messages, m =>
        {
            Assert.Equal(Melop, m.Message.FromAddress);
            Assert.Equal("Melop Ltd", m.Message.FromName);
            Assert.Equal(Melop, m.Message.ReplyToEmail);
            // Transport stays the platform's authenticated account.
            Assert.Equal(PlatformSender, m.Settings.SmtpUsername);
        });
        // A successful send proves the address.
        Assert.Equal(ManagedEmailStatuses.Verified, (await db.EmailSettings.SingleAsync(x => x.TenantId == melop.Id)).ManagedEmailStatus);
    }

    [Fact]
    public async Task Invitation_SenderRejected_MarksIdentityRejected_AndKeepsTheTenantSender()
    {
        await using var db = CreateDb();
        var (melop, _) = SeedTenants(db);
        var user = new User { TenantId = melop.Id, FullName = "Mary", Email = "mary@melop.test", Role = "Admin", IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var capture = new CaptureSender { Failure = new EmailDeliveryException(EmailErrorCategories.SenderRejected, "Sender verify failed") };

        var result = await CreateCredentialDelivery(db, capture).SendPasswordResetLinkAsync(melop.Id, user,
            new PasswordResetLinkDeliveryRequest("https://app.test/reset?token=x", DateTime.UtcNow.AddDays(3), PasswordLinkDeliveryPurpose.AccountSetup));

        Assert.False(result.Success);
        Assert.Equal(Melop, capture.Messages.Single().Message.FromAddress);
        var identity = await db.EmailSettings.AsNoTracking().SingleAsync(x => x.TenantId == melop.Id);
        Assert.Equal(ManagedEmailStatuses.Rejected, identity.ManagedEmailStatus);
        Assert.Equal(Melop, identity.ManagedEmailAddress);
    }

    [Theory]
    [InlineData("work-order.assigned", "work-order-assigned", null)]
    [InlineData("invoice.sent", "invoice-email", "Invoice")]
    [InlineData("system.alerts", "notification", null)]
    public async Task EveryTenantMailFlow_ResolvesTheSameTenantSender(string eventKey, string templateKey, string? documentType)
    {
        await using var db = CreateDb();
        var (melop, _) = SeedTenants(db);
        await db.SaveChangesAsync();
        var capture = new CaptureSender();

        // The caller passes a platform sender — as older call sites did — and it must not win.
        var queued = await new EmailOutboxService(db, new EmailDeliveryLogService(db)).QueueEmailAsync(new QueueEmailRequest(
            melop.Id, eventKey, templateKey, "client@example.test", "Client", "Ecosys Support", PlatformSender, null,
            "Subject", "<p>Body</p>", null, null, DocumentType: documentType));
        await CreateProcessor(db, capture).ProcessPendingBatchAsync(10);

        Assert.Equal(Melop, queued.SenderEmail);
        Assert.Equal(Melop, queued.ReplyToEmail);
        var sent = Assert.Single(capture.Messages);
        Assert.Equal(Melop, sent.Message.FromAddress);
        Assert.Equal("Melop Ltd", sent.Message.FromName);
        Assert.Equal(Melop, sent.Message.ReplyToEmail);
    }

    [Fact]
    public async Task TenantSenders_AreIsolated_EvenWhenTheCallerNamesAnotherTenant()
    {
        await using var db = CreateDb();
        var (melop, acme) = SeedTenants(db);
        await db.SaveChangesAsync();
        var outbox = new EmailOutboxService(db, new EmailDeliveryLogService(db));
        var capture = new CaptureSender();

        await outbox.QueueEmailAsync(new QueueEmailRequest(melop.Id, "invoice.sent", "invoice-email", "a@x.test", null, "Acme", Acme, null, "M", "m", null, null));
        await outbox.QueueEmailAsync(new QueueEmailRequest(acme.Id, "invoice.sent", "invoice-email", "b@x.test", null, "Melop Ltd", Melop, null, "A", "a", null, null));
        await CreateProcessor(db, capture).ProcessPendingBatchAsync(10);

        Assert.Equal(Melop, capture.Messages.Single(x => x.Message.ToAddress == "a@x.test").Message.FromAddress);
        Assert.Equal(Acme, capture.Messages.Single(x => x.Message.ToAddress == "b@x.test").Message.FromAddress);
    }

    [Fact]
    public async Task QueuedRowWithStalePlatformSender_IsSentAsTheTenantAddress()
    {
        await using var db = CreateDb();
        var (melop, _) = SeedTenants(db);
        // Shape of a row queued before this rule: tenant-scoped, but carrying the platform sender.
        db.EmailOutboxMessages.Add(new EmailOutboxMessage
        {
            TenantId = melop.Id, EventKey = "work-order.assigned", TemplateKey = "wo", RecipientEmail = "tech@x.test",
            SenderName = "Ecosys", SenderEmail = PlatformSender, ReplyToEmail = PlatformSender, Subject = "WO",
            HtmlBody = "<p>x</p>", Status = EmailOutboxStatuses.Pending, MaxAttempts = 3, NextAttemptAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await db.SaveChangesAsync();
        var capture = new CaptureSender();

        await CreateProcessor(db, capture).ProcessPendingBatchAsync(10);

        Assert.Equal(Melop, Assert.Single(capture.Messages).Message.FromAddress);
    }

    [Fact]
    public async Task PlatformMail_KeepsThePlatformSender()
    {
        await using var db = CreateDb();
        SeedTenants(db);
        await db.SaveChangesAsync();
        var capture = new CaptureSender();

        var queued = await new EmailOutboxService(db, new EmailDeliveryLogService(db)).QueueEmailAsync(new QueueEmailRequest(
            PlatformConstants.RootTenantId, "tenant.onboarding", "tenant-onboarding", "owner@melop.test", null,
            "Ecosys", PlatformSender, PlatformSender, "Welcome", "<p>hi</p>", null, null));
        await CreateProcessor(db, capture).ProcessPendingBatchAsync(10);

        Assert.Equal(PlatformSender, queued.SenderEmail);
        Assert.Equal(PlatformSender, Assert.Single(capture.Messages).Message.FromAddress);
    }

    [Fact]
    public void NoReplyNotice_IsAddedOnce_ForManagedSender_InsideHtmlBody()
    {
        var html = TenantEmailIdentity.AppendNoReplyNotice("<html><body><p>Invoice</p></body></html>", true, Melop, Melop);

        Assert.Contains(TenantEmailIdentity.NoReplyNotice, html);
        Assert.True(html.IndexOf(TenantEmailIdentity.NoReplyNotice, StringComparison.Ordinal) < html.IndexOf("</body>", StringComparison.Ordinal));
        Assert.Equal(html, TenantEmailIdentity.AppendNoReplyNotice(html, true, Melop, Melop));

        var text = TenantEmailIdentity.AppendNoReplyNotice("Hello", false, Melop, null);
        Assert.EndsWith(TenantEmailIdentity.NoReplyNotice, text);
    }

    [Fact]
    public void NoReplyNotice_IsNotAdded_ForPlatformMail_OrWhenRepliesGoToARealMailbox()
    {
        Assert.Equal("<p>x</p>", TenantEmailIdentity.AppendNoReplyNotice("<p>x</p>", true, PlatformSender, PlatformSender));
        Assert.Equal("<p>x</p>", TenantEmailIdentity.AppendNoReplyNotice("<p>x</p>", true, Melop, "accounts@melop.co.ke"));
    }

    [Fact]
    public async Task TenantMail_CarriesNoReplyNotice_OnInvitationAndOutbox()
    {
        await using var db = CreateDb();
        var (melop, _) = SeedTenants(db);
        var user = new User { TenantId = melop.Id, FullName = "Mary", Email = "mary@melop.test", Role = "Admin", IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var capture = new CaptureSender();

        await CreateCredentialDelivery(db, capture).SendPasswordResetLinkAsync(melop.Id, user, new PasswordResetLinkDeliveryRequest(
            "https://app.test/reset?token=x", DateTime.UtcNow.AddDays(3), PasswordLinkDeliveryPurpose.AccountSetup));
        await new EmailOutboxService(db, new EmailDeliveryLogService(db)).QueueEmailAsync(new QueueEmailRequest(
            melop.Id, "work-order.assigned", "wo", "tech@x.test", null, null, null, null, "WO", "<p>Job</p>", null, null));
        await new EmailOutboxService(db, new EmailDeliveryLogService(db)).QueueEmailAsync(new QueueEmailRequest(
            PlatformConstants.RootTenantId, "tenant.onboarding", "tenant-onboarding", "owner@x.test", null,
            "Ecosys", PlatformSender, PlatformSender, "Welcome", "<p>hi</p>", null, null));
        await CreateProcessor(db, capture).ProcessPendingBatchAsync(10);

        Assert.All(capture.Messages.Where(m => m.Message.FromAddress == Melop),
            m => Assert.Contains(TenantEmailIdentity.NoReplyNotice, m.Message.Body));
        Assert.Equal(2, capture.Messages.Count(m => m.Message.FromAddress == Melop));
        Assert.DoesNotContain(TenantEmailIdentity.NoReplyNotice, capture.Messages.Single(m => m.Message.FromAddress == PlatformSender).Message.Body);
    }

    [Theory]
    [InlineData("invoice.sent", "Invoice", EmailCategory.Finance)]
    [InlineData("receipt.issued", "Receipt", EmailCategory.Finance)]
    [InlineData("finance.statement.issued", "Statement", EmailCategory.Finance)]
    [InlineData("quotation.sent", null, EmailCategory.Finance)]
    [InlineData("work-order.assigned", null, EmailCategory.Operations)]
    [InlineData("sla-breach", null, EmailCategory.Operations)]
    [InlineData("escalation-rule-fired", null, EmailCategory.Operations)]
    [InlineData("eta.en-route", null, EmailCategory.Operations)]
    [InlineData("contract-reminder", null, EmailCategory.Operations)]
    [InlineData("users.invited", null, EmailCategory.General)]
    [InlineData("scheduled-report.finance", null, EmailCategory.Finance)]
    [InlineData("scheduled-report.operations", null, EmailCategory.Operations)]
    [InlineData("scheduled-report", null, EmailCategory.General)]
    [InlineData("portal.magic-link", null, EmailCategory.General)]
    public void Categorize_RoutesFinanceAndOperationsMail(string eventKey, string? documentType, EmailCategory expected) =>
        Assert.Equal(expected, TenantEmailIdentity.Categorize(eventKey, documentType));

    [Fact]
    public async Task FinanceAndOperations_UseTheirOwnReplyTo_ButTheSameFrom()
    {
        await using var db = CreateDb();
        var (melop, _) = SeedTenants(db);
        await db.SaveChangesAsync();
        var identity = await db.EmailSettings.SingleAsync(x => x.TenantId == melop.Id);
        identity.ReplyToEmail = "info@melop.co.ke";
        identity.FinanceReplyToEmail = "accounts@melop.co.ke";
        identity.OperationsReplyToEmail = "service@melop.co.ke";
        await db.SaveChangesAsync();
        var outbox = new EmailOutboxService(db, new EmailDeliveryLogService(db));
        var capture = new CaptureSender();

        await outbox.QueueEmailAsync(new QueueEmailRequest(melop.Id, "invoice.sent", "invoice.sent", "fin@x.test", null, null, null, null, "Inv", "<p>i</p>", null, null, DocumentType: "Invoice"));
        await outbox.QueueEmailAsync(new QueueEmailRequest(melop.Id, "work-order.assigned", "wo", "ops@x.test", null, null, null, null, "WO", "<p>w</p>", null, null));
        await outbox.QueueEmailAsync(new QueueEmailRequest(melop.Id, "scheduled-report", "scheduled-report", "gen@x.test", null, null, null, null, "Rpt", "<p>r</p>", null, null));
        await CreateProcessor(db, capture).ProcessPendingBatchAsync(10);

        Assert.All(capture.Messages, m => Assert.Equal(Melop, m.Message.FromAddress));
        Assert.Equal("accounts@melop.co.ke", capture.Messages.Single(m => m.Message.ToAddress == "fin@x.test").Message.ReplyToEmail);
        Assert.Equal("service@melop.co.ke", capture.Messages.Single(m => m.Message.ToAddress == "ops@x.test").Message.ReplyToEmail);
        Assert.Equal("info@melop.co.ke", capture.Messages.Single(m => m.Message.ToAddress == "gen@x.test").Message.ReplyToEmail);
        // Replies go to real mailboxes, so no do-not-reply notice.
        Assert.All(capture.Messages, m => Assert.DoesNotContain(TenantEmailIdentity.NoReplyNotice, m.Message.Body));
    }

    [Fact]
    public void CategoryReplyTo_FallsBackToGeneralThenManagedAddress_AndExplicitOverrideWins()
    {
        var tenant = NewTenant("melop", "Melop Ltd");
        var identity = TenantEmailIdentity.Create(tenant);
        Assert.Equal(Melop, TenantEmailIdentity.ResolveSender(tenant, identity, null, EmailCategory.Finance)!.ReplyTo);

        identity.ReplyToEmail = "info@melop.co.ke";
        Assert.Equal("info@melop.co.ke", TenantEmailIdentity.ResolveSender(tenant, identity, null, EmailCategory.Operations)!.ReplyTo);

        identity.FinanceReplyToEmail = "accounts@melop.co.ke";
        Assert.Equal("billing@melop.co.ke", TenantEmailIdentity.ResolveSender(tenant, identity, "billing@melop.co.ke", EmailCategory.Finance)!.ReplyTo);
    }

    [Fact]
    public async Task QueuedMail_PicksUpAReplyToChangedAfterQueueing()
    {
        await using var db = CreateDb();
        var (melop, _) = SeedTenants(db);
        await db.SaveChangesAsync();
        await new EmailOutboxService(db, new EmailDeliveryLogService(db)).QueueEmailAsync(new QueueEmailRequest(
            melop.Id, "invoice.sent", "invoice.sent", "fin@x.test", null, null, null, null, "Inv", "<p>i</p>", null, null, DocumentType: "Invoice"));
        (await db.EmailSettings.SingleAsync(x => x.TenantId == melop.Id)).FinanceReplyToEmail = "accounts@melop.co.ke";
        await db.SaveChangesAsync();
        var capture = new CaptureSender();

        await CreateProcessor(db, capture).ProcessPendingBatchAsync(10);

        Assert.Equal("accounts@melop.co.ke", Assert.Single(capture.Messages).Message.ReplyToEmail);
    }

    [Theory]
    [InlineData("Revenue", "accounts@melop.co.ke")]
    [InlineData("Aging", "accounts@melop.co.ke")]
    [InlineData("ClientStatement", "accounts@melop.co.ke")]
    [InlineData("Invoices", "accounts@melop.co.ke")]
    [InlineData("WorkOrderSummary", "service@melop.co.ke")]
    public async Task ScheduledReports_TakeTheReplyToOfTheirArea_FromTheTenantAddress(string reportType, string expectedReplyTo)
    {
        await using var db = CreateDb();
        var (melop, _) = SeedTenants(db);
        await db.SaveChangesAsync();
        var identity = await db.EmailSettings.SingleAsync(x => x.TenantId == melop.Id);
        identity.ReplyToEmail = "info@melop.co.ke";
        identity.FinanceReplyToEmail = "accounts@melop.co.ke";
        identity.OperationsReplyToEmail = "service@melop.co.ke";
        await db.SaveChangesAsync();
        var outbox = new EmailOutboxService(db, new EmailDeliveryLogService(db));
        var report = new ScheduledReport
        {
            TenantId = melop.Id, Name = "Monthly", ReportType = reportType, Recipients = "manager@melop.co.ke", DateRangePreset = "LastMonth"
        };
        var capture = new CaptureSender();

        await new ScheduledReportService(db, outbox, NullLogger<ScheduledReportService>.Instance)
            .SendReportEmailAsync(report, [0x25, 0x50, 0x44, 0x46]);
        await CreateProcessor(db, capture).ProcessPendingBatchAsync(10);

        var sent = Assert.Single(capture.Messages).Message;
        Assert.Equal(Melop, sent.FromAddress);
        Assert.Equal(expectedReplyTo, sent.ReplyToEmail);
        Assert.Single(sent.Attachments!);
        Assert.Equal("scheduled-report", (await db.EmailOutboxMessages.SingleAsync()).TemplateKey);
    }

    private static Tenant NewTenant(string slug, string name) => new()
    {
        Id = Guid.NewGuid(), Slug = slug, Name = name, CompanyName = name, Country = "Kenya", TimeZoneId = "Africa/Nairobi", IsActive = true
    };

    private static (Tenant Melop, Tenant Acme) SeedTenants(AppDbContext db)
    {
        var melop = NewTenant("melop", "Melop Ltd");
        var acme = NewTenant("acme", "Acme");
        db.AddRange(melop, acme, TenantEmailIdentity.Create(melop), TenantEmailIdentity.Create(acme), new EmailSetting
        {
            TenantId = PlatformConstants.RootTenantId, Provider = "Smtp", Host = "mail.ecosysdigital.co.ke", Port = 465, UseSsl = true,
            Username = PlatformSender, Password = "secret", SenderName = "Ecosys", SenderAddress = PlatformSender, IsEnabled = true
        });
        return (melop, acme);
    }

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static UserCredentialDeliveryService CreateCredentialDelivery(AppDbContext db, IEmailSender sender) => new(
        db, new HttpContextAccessor { HttpContext = new DefaultHttpContext() }, sender,
        new EmailTemplateService(db), new EmailDeliveryLogService(db), new EmailSubjectRuleService(db), new NoSecrets(),
        new EmailSettingsResolver(db), Options.Create(new SmtpOptions()),
        Options.Create(new PublicAppOptions { PublicUrl = "https://serviceops.example.test" }),
        NullLogger<UserCredentialDeliveryService>.Instance);

    private static EmailOutboxProcessor CreateProcessor(AppDbContext db, IEmailSender sender) => new(
        db, new ClaimAlwaysOutbox(new EmailOutboxService(db, new EmailDeliveryLogService(db))), sender, new NoSecrets(),
        new EmailSettingsResolver(db), Options.Create(new SmtpOptions()), NullLogger<EmailOutboxProcessor>.Instance);

    /// <summary>The in-memory provider cannot run ExecuteUpdate, which only the claim step uses.</summary>
    private sealed class ClaimAlwaysOutbox(IEmailOutboxService inner) : IEmailOutboxService
    {
        public Task<EmailOutboxMessage> QueueEmailAsync(QueueEmailRequest request, CancellationToken cancellationToken = default) => inner.QueueEmailAsync(request, cancellationToken);
        public Task<IReadOnlyCollection<EmailOutboxMessage>> GetPendingBatchAsync(int batchSize, DateTime utcNow, CancellationToken cancellationToken = default) => inner.GetPendingBatchAsync(batchSize, utcNow, cancellationToken);
        public Task<bool> MarkSendingAsync(Guid outboxMessageId, DateTime utcNow, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<int> ReleaseAbandonedClaimsAsync(TimeSpan claimTimeout, DateTime utcNow, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task MarkSentAsync(Guid outboxMessageId, DateTime utcNow, string? providerMessageId, CancellationToken cancellationToken = default) => inner.MarkSentAsync(outboxMessageId, utcNow, providerMessageId, cancellationToken);
        public Task MarkFailedAsync(Guid outboxMessageId, string? errorCategory, string? errorMessage, DateTime utcNow, CancellationToken cancellationToken = default) => inner.MarkFailedAsync(outboxMessageId, errorCategory, errorMessage, utcNow, cancellationToken);
        public Task RetryAsync(Guid outboxMessageId, DateTime nextAttemptAt, string? errorMessage, CancellationToken cancellationToken = default) => inner.RetryAsync(outboxMessageId, nextAttemptAt, errorMessage, cancellationToken);
        public Task CancelAsync(Guid outboxMessageId, string? errorMessage, CancellationToken cancellationToken = default) => inner.CancelAsync(outboxMessageId, errorMessage, cancellationToken);
    }

    private sealed class NoSecrets : ISecretEncryptionService
    {
        public string? Encrypt(string? value) => value;
        public string? Decrypt(string? value) => value;
    }

    private sealed class CaptureSender : IEmailSender
    {
        public List<(EmailMessage Message, EmailDeliverySettings Settings)> Messages { get; } = [];
        public Exception? Failure { get; init; }

        public Task SendAsync(EmailMessage message, EmailDeliverySettings settings, CancellationToken cancellationToken = default)
        {
            Messages.Add((message, settings));
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }

        public Task<EmailVerificationResult> VerifyAsync(EmailDeliverySettings settings, CancellationToken cancellationToken = default) =>
            Task.FromResult(new EmailVerificationResult(true));

        public Task<EmailVerificationResult> VerifySenderAsync(EmailDeliverySettings settings, string senderAddress, CancellationToken cancellationToken = default) =>
            Task.FromResult(new EmailVerificationResult(true));
    }

    /// <summary>A minimal plaintext SMTP server: enough to observe AUTH, MAIL FROM, RCPT and DATA.</summary>
    private sealed class FakeSmtpServer : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly Task loop;

        public FakeSmtpServer()
        {
            listener.Start();
            loop = Task.Run(ServeAsync);
        }

        public string? RcptRefusal { get; init; }
        public string? AuthenticatedUser { get; private set; }
        public string? MailFrom { get; private set; }
        public string? Data { get; private set; }

        public EmailDeliverySettings Settings(string username, string password) => new(
            EmailDeliveryMode.Smtp, "127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, username, password,
            "Ecosys", PlatformSender, null, false, EmailSecureMode.None, TimeoutSeconds: 10);

        private async Task ServeAsync()
        {
            try
            {
                while (true)
                {
                    using var client = await listener.AcceptTcpClientAsync();
                    await using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.ASCII);
                    await using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
                    await writer.WriteLineAsync("220 fake ESMTP");
                    string? line;
                    while ((line = await reader.ReadLineAsync()) is not null)
                    {
                        var upper = line.ToUpperInvariant();
                        if (upper.StartsWith("EHLO"))
                        {
                            await writer.WriteLineAsync("250-fake");
                            await writer.WriteLineAsync("250 AUTH PLAIN");
                        }
                        else if (upper.StartsWith("AUTH PLAIN"))
                        {
                            var payload = line.Length > "AUTH PLAIN ".Length ? line["AUTH PLAIN ".Length..] : null;
                            if (payload is null)
                            {
                                await writer.WriteLineAsync("334 ");
                                payload = await reader.ReadLineAsync();
                            }

                            AuthenticatedUser = Encoding.UTF8.GetString(Convert.FromBase64String(payload!)).Split('\0')[1];
                            await writer.WriteLineAsync("235 ok");
                        }
                        else if (upper.StartsWith("MAIL FROM:"))
                        {
                            MailFrom = line[(line.IndexOf('<') + 1)..line.IndexOf('>')];
                            await writer.WriteLineAsync("250 ok");
                        }
                        else if (upper.StartsWith("RCPT TO:"))
                        {
                            await writer.WriteLineAsync(RcptRefusal ?? "250 ok");
                        }
                        else if (upper == "DATA")
                        {
                            await writer.WriteLineAsync("354 go");
                            var data = new StringBuilder();
                            while ((line = await reader.ReadLineAsync()) is not null && line != ".")
                            {
                                data.AppendLine(line);
                            }

                            Data = data.ToString();
                            await writer.WriteLineAsync("250 queued");
                        }
                        else if (upper == "QUIT")
                        {
                            await writer.WriteLineAsync("221 bye");
                            break;
                        }
                        else
                        {
                            await writer.WriteLineAsync("250 ok");
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Listener stopped.
            }
        }

        public async ValueTask DisposeAsync()
        {
            listener.Stop();
            await loop;
        }
    }
}
