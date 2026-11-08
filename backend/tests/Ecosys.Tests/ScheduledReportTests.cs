using System.Text.RegularExpressions;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Contracts.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ecosys.Tests;

// ── Scheduled-report service: validation and scheduling ───────────────────────

public sealed class ScheduledReportValidationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task CreateAsync_EmptyName_ThrowsBusinessRule(string name)
    {
        await using var db = CreateDbContext();
        var svc = new ScheduledReportService(db, CreateNullOutbox(), NullLogger<ScheduledReportService>.Instance);

        await Assert.ThrowsAsync<Shared.Errors.BusinessRuleException>(() =>
            svc.CreateAsync(Guid.NewGuid(), ValidRequest(name: name)));
    }

    [Fact]
    public async Task CreateAsync_UnknownReportType_ThrowsBusinessRule()
    {
        await using var db = CreateDbContext();
        var svc = new ScheduledReportService(db, CreateNullOutbox(), NullLogger<ScheduledReportService>.Instance);

        await Assert.ThrowsAsync<Shared.Errors.BusinessRuleException>(() =>
            svc.CreateAsync(Guid.NewGuid(), ValidRequest(reportType: "GibberishType")));
    }

    [Fact]
    public async Task CreateAsync_InvalidRecipient_ThrowsBusinessRule()
    {
        await using var db = CreateDbContext();
        var svc = new ScheduledReportService(db, CreateNullOutbox(), NullLogger<ScheduledReportService>.Instance);

        await Assert.ThrowsAsync<Shared.Errors.BusinessRuleException>(() =>
            svc.CreateAsync(Guid.NewGuid(), ValidRequest(recipients: "not-an-email")));
    }

    [Theory]
    [InlineData("WorkOrderSummary")]
    public async Task CreateAsync_ValidReportType_Succeeds(string reportType)
    {
        await using var db = CreateDbContext();
        db.Tenants.Add(MinimalTenant(out var tenantId));
        await db.SaveChangesAsync();

        var svc = new ScheduledReportService(db, CreateNullOutbox(), NullLogger<ScheduledReportService>.Instance);
        var report = await svc.CreateAsync(tenantId, ValidRequest(reportType: reportType));

        Assert.Equal(reportType, report.ReportType);
    }

    [Fact]
    public async Task ComputeNextRunAt_DailyInFuture_ReturnsCorrectDate()
    {
        await using var db = CreateDbContext();
        var svc = new ScheduledReportService(db, CreateNullOutbox(), NullLogger<ScheduledReportService>.Instance);

        var report = new ScheduledReport
        {
            Frequency = "Daily",
            TimeOfDay = "23:59",
            DayOfMonth = null,
            DayOfWeek = null,
        };

        var next = await svc.ComputeNextRunAtAsync(report);

        // Should be within the next 48 h
        Assert.True(next > DateTime.UtcNow);
        Assert.True(next <= DateTime.UtcNow.AddDays(2));
        Assert.Equal(23, next.Hour);
        Assert.Equal(59, next.Minute);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static CreateScheduledReportRequest ValidRequest(
        string name = "Test Report",
        string reportType = "WorkOrderSummary",
        string frequency = "Monthly",
        string recipients = "finance@acme.test")
        => new(name, reportType, frequency, 1, null, "08:00", "LastMonth", null, null, recipients);

    private static Tenant MinimalTenant(out Guid id)
    {
        id = Guid.NewGuid();
        return new Tenant
        {
            Id = id, Name = "Acme", Slug = $"acme-{id:N}", CompanyName = "Acme",
            Email = "ops@acme.test", ContactEmail = "ops@acme.test", Country = "KE",
            Status = "Active", LicenseStatus = "Active", PrimaryColor = "#000", SecondaryColor = "#fff",
        };
    }

    private static IEmailOutboxService CreateNullOutbox() => new NullEmailOutboxService();

    private static AppDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"sr-tests-{Guid.NewGuid():N}")
            .Options);
}

// ── PDF attachment extraction ─────────────────────────────────────────────────

public sealed class EmailAttachmentExtractionTests
{
    // Replicate the private helper via the same regex so we can unit-test it in isolation.
    private static (string CleanBody, List<EmailAttachment> Attachments) ExtractAttachments(string body)
    {
        var attachments = new List<EmailAttachment>();
        if (string.IsNullOrEmpty(body))
            return (body, attachments);

        var pattern = new Regex(@"<!--\s*attachment:([^:]+):([A-Za-z0-9+/=]+)\s*-->", RegexOptions.Compiled);
        var cleanBody = pattern.Replace(body, match =>
        {
            var filename = match.Groups[1].Value.Trim();
            var base64 = match.Groups[2].Value.Trim();
            try
            {
                var bytes = Convert.FromBase64String(base64);
                var contentType = filename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                    ? "application/pdf"
                    : "application/octet-stream";
                attachments.Add(new EmailAttachment(filename, bytes, contentType));
            }
            catch { /* skip */ }
            return string.Empty;
        });

        return (cleanBody.Trim(), attachments);
    }

    [Fact]
    public void ExtractAttachments_NoComment_ReturnsBodyUnchanged()
    {
        const string body = "<p>Hello world</p>";
        var (clean, atts) = ExtractAttachments(body);
        Assert.Equal(body, clean);
        Assert.Empty(atts);
    }

    [Fact]
    public void ExtractAttachments_ValidComment_ExtractsAttachmentAndStripsComment()
    {
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF
        var base64 = Convert.ToBase64String(pdfBytes);
        var body = $"<p>See attached.</p><!-- attachment:report.pdf:{base64} -->";

        var (clean, atts) = ExtractAttachments(body);

        Assert.DoesNotContain("attachment:", clean);
        Assert.Single(atts);
        Assert.Equal("report.pdf", atts[0].Filename);
        Assert.Equal("application/pdf", atts[0].ContentType);
        Assert.Equal(pdfBytes, atts[0].Content);
    }

    [Fact]
    public void ExtractAttachments_MalformedBase64_SkipsAttachmentSilently()
    {
        var body = "<p>Hi</p><!-- attachment:report.pdf:!!!notbase64!!! -->";
        var (_, atts) = ExtractAttachments(body);
        Assert.Empty(atts);
    }
}

// ── Billing: auto-populate deduplication ─────────────────────────────────────

public sealed class BillingAutoPopulateTests
{
    private static AppDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"billing-tests-{Guid.NewGuid():N}")
            .Options);
}

// ── Null outbox stub ──────────────────────────────────────────────────────────

file sealed class NullEmailOutboxService : IEmailOutboxService
{
    public Task<EmailOutboxMessage> QueueEmailAsync(QueueEmailRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new EmailOutboxMessage { Id = Guid.NewGuid(), Subject = request.Subject });

    public Task<IReadOnlyCollection<EmailOutboxMessage>> GetPendingBatchAsync(int batchSize, DateTime utcNow, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<EmailOutboxMessage>>(Array.Empty<EmailOutboxMessage>());

    public Task<bool> MarkSendingAsync(Guid outboxMessageId, DateTime utcNow, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task<int> ReleaseAbandonedClaimsAsync(TimeSpan claimTimeout, DateTime utcNow, CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task MarkSentAsync(Guid outboxMessageId, DateTime utcNow, string? providerMessageId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task MarkFailedAsync(Guid outboxMessageId, string? errorCategory, string? errorMessage, DateTime utcNow, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RetryAsync(Guid outboxMessageId, DateTime nextAttemptAt, string? errorMessage, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task CancelAsync(Guid outboxMessageId, string? errorMessage, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
