using System.Text.Json;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
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

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-outbox-tests-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }
}
