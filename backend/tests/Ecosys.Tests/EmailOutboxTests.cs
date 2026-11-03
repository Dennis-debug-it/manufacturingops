using System.Text.Json;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Options;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

public sealed class EmailOutboxTests
{
    [Fact]
    public void MergeTemplateVariables_DuplicateFullName_DoesNotThrow_AndLatestValueWins()
    {
        var variables = EmailTemplateVariables.WithRecipientAndActorAliases(
            "Recipient Name",
            "Actor Name",
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["FullName"] = "Recipient Name",
                ["fullName"] = "Overridden Recipient",
                ["recipientFullName"] = "Recipient Alias",
                ["actorFullName"] = "Actor Alias",
            });

        Assert.Equal("Overridden Recipient", variables["fullName"]);
        Assert.Equal("Recipient Alias", variables["recipientFullName"]);
        Assert.Equal("Actor Alias", variables["actorFullName"]);
    }

    [Fact]
    public async Task SubjectRules_ApplyPrefixTagsEnvironmentAndTenant_WithoutDuplicatingPrefix()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();

        dbContext.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Acme Workspace",
            Slug = "acme-workspace",
            CompanyName = "Acme Workspace",
            Email = "ops@acme.test",
            ContactEmail = "ops@acme.test",
            Country = "Kenya",
            Status = "Active",
            LicenseStatus = "Active",
            PrimaryColor = "#0F4C81",
            SecondaryColor = "#F4B942",
            IsActive = true
        });
        dbContext.PlatformSettings.Add(new PlatformSetting
        {
            Category = "platform-email-subject-rules",
            JsonValue = JsonSerializer.Serialize(new EmailSubjectRuleOptions(
                "[Ecosys]",
                "[Priority]",
                true,
                "Production",
                true,
                true))
        });
        await dbContext.SaveChangesAsync();

        var service = new EmailSubjectRuleService(dbContext);

        var finalSubject = await service.BuildFinalSubjectAsync(
            tenantId,
            "auth.password-reset.requested",
            "[Ecosys] Reset your password",
            cancellationToken: CancellationToken.None);

        Assert.Equal("[Security] [Production] [Acme Workspace] [Ecosys] Reset your password [Priority]", finalSubject);
    }

    [Fact]
    public async Task QueueEmailAsync_CreatesPendingOutboxMessage_AndPendingDeliveryLog()
    {
        await using var dbContext = CreateDbContext();
        var logService = new EmailDeliveryLogService(dbContext);
        var outboxService = new EmailOutboxService(dbContext, logService);

        var message = await outboxService.QueueEmailAsync(
            new QueueEmailRequest(
                Guid.NewGuid(),
                "email.test",
                "test-email",
                "recipient@example.com",
                "Recipient User",
                "Ecosys",
                "noreply@example.com",
                "support@example.com",
                "[Ecosys] [Test] SMTP delivery test",
                "<p>Hello</p>",
                "Hello",
                Guid.NewGuid()),
            CancellationToken.None);

        var storedMessage = await dbContext.EmailOutboxMessages.SingleAsync(x => x.Id == message.Id);
        var storedLog = await dbContext.EmailDeliveryLogs.SingleAsync(x => x.OutboxMessageId == message.Id);

        Assert.Equal(EmailOutboxStatuses.Pending, storedMessage.Status);
        Assert.Equal("recipient@example.com", storedMessage.RecipientEmail);
        Assert.Equal("Recipient User", storedMessage.RecipientName);
        Assert.Equal("support@example.com", storedMessage.ReplyToEmail);
        Assert.Equal(EmailOutboxStatuses.Pending, storedLog.Status);
        Assert.Equal(0, storedLog.AttemptCount);
        Assert.NotNull(storedLog.NextAttemptAt);
    }

    [Fact]
    public async Task QueueEmailAsync_WithIdempotencyKey_IsSingleOutboxMessage_AndPreservesDeliveryMetadata()
    {
        await using var dbContext = CreateDbContext();
        var logService = new EmailDeliveryLogService(dbContext);
        var outboxService = new EmailOutboxService(dbContext, logService);
        var idempotencyKey = $"invoice.sent:{Guid.NewGuid():N}";
        var attachment = new EmailAttachment("invoice.pdf", [1, 2, 3], "application/pdf");

        var request = new QueueEmailRequest(
            Guid.NewGuid(), "invoice.sent", "invoice.sent", "billing@example.com", null,
            "Ecosys", "noreply@example.com", null, "Invoice", "<p>Invoice</p>", "Invoice",
            Guid.NewGuid(), Attachments: [attachment], CcAddresses: ["finance@example.com"],
            DocumentType: "Invoice", DocumentId: Guid.NewGuid(), TriggeredAutomatically: true,
            IdempotencyKey: idempotencyKey);

        var first = await outboxService.QueueEmailAsync(request);
        var second = await outboxService.QueueEmailAsync(request with { Subject = "Changed subject" });

        Assert.Equal(first.Id, second.Id);
        Assert.Single(await dbContext.EmailOutboxMessages.ToListAsync());
        Assert.Single(await dbContext.EmailDeliveryLogs.ToListAsync());
        var stored = await dbContext.EmailOutboxMessages.SingleAsync();
        var log = await dbContext.EmailDeliveryLogs.SingleAsync();
        Assert.Equal(idempotencyKey, stored.IdempotencyKey);
        Assert.Equal("Invoice", stored.DocumentType);
        Assert.Equal(first.DocumentId, stored.DocumentId);
        Assert.Contains("finance@example.com", stored.CcAddressesJson);
        Assert.Contains("invoice.pdf", stored.AttachmentsJson);
        Assert.True(stored.TriggeredAutomatically);
        Assert.Equal(stored.DocumentType, log.DocumentType);
        Assert.Equal(stored.DocumentId, log.DocumentId);
        Assert.True(log.TriggeredAutomatically);
    }

    [Fact]
    public async Task MarkFailedAsync_SchedulesRetry_ThenFinalFailure()
    {
        await using var dbContext = CreateDbContext();
        var logService = new EmailDeliveryLogService(dbContext);
        var outboxService = new EmailOutboxService(dbContext, logService);

        var message = await outboxService.QueueEmailAsync(
            new QueueEmailRequest(
                null,
                "email.test",
                "test-email",
                "recipient@example.com",
                null,
                "Ecosys",
                "noreply@example.com",
                null,
                "[Ecosys] [Test] SMTP delivery test",
                "<p>Hello</p>",
                "Hello",
                null),
            CancellationToken.None);

        var now = DateTime.UtcNow;
        await outboxService.MarkFailedAsync(message.Id, "Connection failed", "SMTP timed out", now, CancellationToken.None);

        var retryingMessage = await dbContext.EmailOutboxMessages.SingleAsync(x => x.Id == message.Id);
        var retryingLog = await dbContext.EmailDeliveryLogs.SingleAsync(x => x.OutboxMessageId == message.Id);

        Assert.Equal(EmailOutboxStatuses.Pending, retryingMessage.Status);
        Assert.Equal(1, retryingMessage.AttemptCount);
        Assert.NotNull(retryingMessage.NextAttemptAt);
        Assert.Equal("Retrying", retryingLog.Status);

        await outboxService.MarkFailedAsync(message.Id, "Connection failed", "SMTP still timed out", now.AddMinutes(2), CancellationToken.None);
        await outboxService.MarkFailedAsync(message.Id, "Connection failed", "SMTP permanently timed out", now.AddMinutes(12), CancellationToken.None);

        var failedMessage = await dbContext.EmailOutboxMessages.SingleAsync(x => x.Id == message.Id);
        var failedLog = await dbContext.EmailDeliveryLogs.SingleAsync(x => x.OutboxMessageId == message.Id);

        Assert.Equal(EmailOutboxStatuses.Failed, failedMessage.Status);
        Assert.Equal(3, failedMessage.AttemptCount);
        Assert.Null(failedMessage.NextAttemptAt);
        Assert.Equal(EmailOutboxStatuses.Failed, failedLog.Status);
        Assert.Equal("Connection failed", failedLog.ErrorCategory);
    }

    [Fact]
    public async Task ReleaseAbandonedClaimsAsync_ReturnsStrandedSendingMessagesToTheQueue()
    {
        // EC-P1-06d: MarkSendingAsync moves a row to Sending and the batch query only ever selects
        // Pending, so a claim left behind by an instance that stopped mid-send was unreachable for
        // ever. Verified live with three messages stuck in Sending.
        await using var dbContext = CreateDbContext();
        var logService = new EmailDeliveryLogService(dbContext);
        var outboxService = new EmailOutboxService(dbContext, logService);

        var message = await QueueTestMessageAsync(outboxService);
        await ClaimAsync(dbContext, message.Id, DateTime.UtcNow.AddHours(-1));

        var now = DateTime.UtcNow;
        Assert.Empty(await outboxService.GetPendingBatchAsync(10, now, CancellationToken.None));

        var released = await outboxService.ReleaseAbandonedClaimsAsync(TimeSpan.FromMinutes(15), now, CancellationToken.None);

        var rescued = await dbContext.EmailOutboxMessages.SingleAsync(x => x.Id == message.Id);
        Assert.Equal(1, released);
        Assert.Equal(EmailOutboxStatuses.Pending, rescued.Status);
        Assert.Equal(1, rescued.AttemptCount);
        Assert.NotNull(rescued.NextAttemptAt);
        Assert.Equal("Abandoned", rescued.ErrorCategory);
    }

    [Fact]
    public async Task ReleaseAbandonedClaimsAsync_RetiresAMessageThatHasUsedUpItsAttempts()
    {
        // A message that kills whichever worker picks it up must reach the terminal Failed state
        // rather than being requeued for ever.
        await using var dbContext = CreateDbContext();
        var logService = new EmailDeliveryLogService(dbContext);
        var outboxService = new EmailOutboxService(dbContext, logService);

        var message = await QueueTestMessageAsync(outboxService, maxAttempts: 1);
        await ClaimAsync(dbContext, message.Id, DateTime.UtcNow.AddHours(-1));

        await outboxService.ReleaseAbandonedClaimsAsync(TimeSpan.FromMinutes(15), DateTime.UtcNow, CancellationToken.None);

        var retired = await dbContext.EmailOutboxMessages.SingleAsync(x => x.Id == message.Id);
        Assert.Equal(EmailOutboxStatuses.Failed, retired.Status);
        Assert.Null(retired.NextAttemptAt);
    }

    [Fact]
    public async Task ReleaseAbandonedClaimsAsync_LeavesAClaimThatIsStillWithinTheTimeout()
    {
        await using var dbContext = CreateDbContext();
        var logService = new EmailDeliveryLogService(dbContext);
        var outboxService = new EmailOutboxService(dbContext, logService);

        var message = await QueueTestMessageAsync(outboxService);
        await ClaimAsync(dbContext, message.Id, DateTime.UtcNow);

        var released = await outboxService.ReleaseAbandonedClaimsAsync(TimeSpan.FromMinutes(15), DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(0, released);
        Assert.Equal(EmailOutboxStatuses.Sending, (await dbContext.EmailOutboxMessages.SingleAsync(x => x.Id == message.Id)).Status);
    }

    /// <summary>
    /// Reproduces the row shape <see cref="EmailOutboxService.MarkSendingAsync"/> leaves behind. That
    /// method claims with ExecuteUpdate, which the in-memory provider does not support, so the state
    /// is written directly rather than through it.
    /// </summary>
    private static async Task ClaimAsync(AppDbContext dbContext, Guid outboxMessageId, DateTime claimedAt)
    {
        var message = await dbContext.EmailOutboxMessages.SingleAsync(x => x.Id == outboxMessageId);
        message.Status = EmailOutboxStatuses.Sending;
        message.LastAttemptAt = claimedAt;
        message.NextAttemptAt = null;
        await dbContext.SaveChangesAsync();
    }

    private static Task<EmailOutboxMessage> QueueTestMessageAsync(EmailOutboxService outboxService, int maxAttempts = 3) =>
        outboxService.QueueEmailAsync(
            new QueueEmailRequest(
                null,
                "email.test",
                "test-email",
                "recipient@example.com",
                null,
                "Ecosys",
                "noreply@example.com",
                null,
                "[Ecosys] [Test] SMTP delivery test",
                "<p>Hello</p>",
                "Hello",
                null,
                maxAttempts),
            CancellationToken.None);

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-outbox-tests-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }
}
