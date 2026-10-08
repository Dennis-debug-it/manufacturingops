using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ecosys.Tests;

public sealed class PreventiveMaintenanceFlowTests
{
    [Fact]
    public async Task Close_WithOpenSubtask_IsRefused_UntilTheSubtaskIsDone()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var parent = await dbContext.WorkOrders.SingleAsync(x => x.Id == fixture.WorkOrderId);
        parent.Status = WorkOrderStatuses.Completed;
        var subtask = new WorkOrder
        {
            TenantId = fixture.TenantId, ClientId = parent.ClientId, ParentWorkOrderId = parent.Id,
            WorkOrderNumber = "WO-SUB-1", Title = "Replace filter", Priority = "Low", Status = WorkOrderStatuses.Assigned,
        };
        dbContext.WorkOrders.Add(subtask);
        await dbContext.SaveChangesAsync();
        var service = CreateLifecycleService(dbContext);

        var error = await Assert.ThrowsAsync<Ecosys.Shared.Errors.BusinessRuleException>(() => service.ChangeStatusAsync(
            fixture.TenantId, parent.Id, fixture.ActorUserId, new WorkOrderStatusChangeRequest(WorkOrderStatuses.Closed)));
        Assert.Contains("WO-SUB-1", error.Message);
        Assert.Equal(WorkOrderStatuses.Completed, (await dbContext.WorkOrders.AsNoTracking().SingleAsync(x => x.Id == parent.Id)).Status);

        // A finished sub-task no longer blocks closing.
        subtask.Status = WorkOrderStatuses.Completed;
        await dbContext.SaveChangesAsync();
        var closed = await service.ChangeStatusAsync(fixture.TenantId, parent.Id, fixture.ActorUserId, new WorkOrderStatusChangeRequest(WorkOrderStatuses.Closed));
        Assert.Equal(WorkOrderStatuses.Closed, closed.Status);
    }

    private static WorkOrderLifecycleService CreateLifecycleService(AppDbContext dbContext) =>
        new(
            dbContext,
            new TestTenantContext(),
            new AuditLogService(dbContext, new Microsoft.AspNetCore.Http.HttpContextAccessor()),
            new NoOpEmailOutboxService(),
            new StubEmailTemplateService(),
            new StubEmailSubjectRuleService(),
            new NoOpSlaService(),
            new NoOpServiceOpsNotifier(),
            new NoOpEtaNotificationService(),
            Microsoft.Extensions.Options.Options.Create(new Ecosys.Shared.Options.PublicAppOptions()),
            NullLogger<WorkOrderLifecycleService>.Instance);

    [Fact]
    public async Task CompleteAsync_ForPmWorkOrder_UpdatesAssetPlanAndMeterReading()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);

        var service = new WorkOrderLifecycleService(
            dbContext,
            new TestTenantContext(),
            new AuditLogService(dbContext, new Microsoft.AspNetCore.Http.HttpContextAccessor()),
            new NoOpEmailOutboxService(),
            new StubEmailTemplateService(),
            new StubEmailSubjectRuleService(),
            new NoOpSlaService(),
            new NoOpServiceOpsNotifier(),
            new NoOpEtaNotificationService(),
            Microsoft.Extensions.Options.Options.Create(new Ecosys.Shared.Options.PublicAppOptions()),
            NullLogger<WorkOrderLifecycleService>.Instance);

        var result = await service.CompleteAsync(
            fixture.TenantId,
            fixture.WorkOrderId,
            fixture.ActorUserId,
            new WorkOrderCompletionRequest("Completed the monthly PM service.", "PM completed successfully."),
            CancellationToken.None);

        Assert.Equal("Completed", result.Status);
        Assert.NotNull(result.CompletedAt);

        var asset = await dbContext.Assets.SingleAsync(x => x.Id == fixture.AssetId);
        var plan = await dbContext.PreventiveMaintenancePlans.SingleAsync(x => x.Id == fixture.PlanId);
        var report = await dbContext.PmReports.SingleAsync(x => x.WorkOrderId == fixture.WorkOrderId);

        Assert.Equal(result.CompletedAt, asset.LastPmDate);
        Assert.Equal(123.5m, asset.CurrentMeterReading);
        Assert.Equal(result.CompletedAt, plan.LastPmDate);
        Assert.Equal(fixture.OriginalNextPmDate.AddMonths(1), plan.NextPmDate);
        Assert.Equal(plan.NextPmDate, asset.NextPmDate);
        Assert.Equal(fixture.WorkOrderId, plan.LastPmWorkOrderId);
        Assert.Contains("Runtime Hours", report.AnswersJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompleteAsync_SyncedFromOffline_IsDatedWhenItHappened()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var service = CreateLifecycleService(dbContext);
        var completedOnSite = DateTime.UtcNow.AddHours(-3);

        Assert.True(OccurredAt.TrySet(completedOnSite));
        try
        {
            var result = await service.CompleteAsync(
                fixture.TenantId, fixture.WorkOrderId, fixture.ActorUserId,
                new WorkOrderCompletionRequest("Completed the monthly PM service.", "PM completed successfully."), CancellationToken.None);

            Assert.Equal(completedOnSite, result.CompletedAt);
            var statusEvent = await dbContext.WorkOrderEvents.Where(x => x.WorkOrderId == fixture.WorkOrderId && x.NewStatus == WorkOrderStatuses.Completed).SingleAsync();
            Assert.Equal(completedOnSite, statusEvent.OccurredAt);
        }
        finally
        {
            OccurredAt.Clear();
        }
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-pm-flow-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<Fixture> SeedFixtureAsync(AppDbContext dbContext)
    {
        var tenant = new Tenant
        {
            Name = "Acme",
            Slug = $"acme-{Guid.NewGuid():N}".Substring(0, 12),
            CompanyName = "Acme Facilities",
            Email = "ops@acme.test",
            Country = "Kenya",
            PrimaryColor = "#0F4C81",
            SecondaryColor = "#F4B942",
            ShowPoweredByEcosys = true
        };

        var asset = new Asset
        {
            TenantId = tenant.Id,
            ClientId = Guid.NewGuid(),
            AssetName = "Generator 01",
            AssetCode = $"GEN-{Guid.NewGuid():N}".Substring(0, 10),
            Status = "Active",
            LastPmDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            NextPmDate = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc)
        };

        var plan = new PreventiveMaintenancePlan
        {
            TenantId = tenant.Id,
            AssetId = asset.Id,
            Frequency = "Monthly",
            FrequencyUnit = "Monthly",
            FrequencyInterval = 1,
            LastPmDate = asset.LastPmDate,
            NextPmDate = asset.NextPmDate,
            Status = "Active"
        };

        var workOrder = new WorkOrder
        {
            TenantId = tenant.Id,
            ClientId = asset.ClientId,
            AssetId = asset.Id,
            WorkOrderNumber = "WO-PM-001",
            Title = "PM - Generator 01",
            Priority = "Medium",
            Status = WorkOrderStatuses.InProgress,
            WorkType = WorkOrderTypes.PreventiveMaintenance,
            IsPreventiveMaintenance = true,
            PreventiveMaintenancePlanId = plan.Id,
            WorkStartedAt = new DateTime(2026, 5, 15, 8, 30, 0, DateTimeKind.Utc),
            ArrivalAt = new DateTime(2026, 5, 15, 8, 45, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2026, 5, 14, 8, 0, 0, DateTimeKind.Utc)
        };

        dbContext.AddRange(tenant, asset, plan, workOrder);
        dbContext.WorkOrderChecklistItems.Add(new WorkOrderChecklistItem
        {
            TenantId = tenant.Id,
            WorkOrderId = workOrder.Id,
            SectionName = "Readings",
            QuestionText = "Runtime Hours",
            InputType = "number",
            IsRequired = true,
            SortOrder = 1,
            ResponseValue = "123.5",
            IsCompleted = true,
            CompletedAt = new DateTime(2026, 5, 15, 10, 0, 0, DateTimeKind.Utc)
        });

        await dbContext.SaveChangesAsync();

        return new Fixture(tenant.Id, Guid.NewGuid(), asset.Id, plan.Id, workOrder.Id, asset.NextPmDate!.Value);
    }

    private sealed record Fixture(
        Guid TenantId,
        Guid ActorUserId,
        Guid AssetId,
        Guid PlanId,
        Guid WorkOrderId,
        DateTime OriginalNextPmDate);

    private sealed class NoOpEmailOutboxService : IEmailOutboxService
    {
        public Task CancelAsync(Guid outboxMessageId, string? errorMessage, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyCollection<EmailOutboxMessage>> GetPendingBatchAsync(int batchSize, DateTime utcNow, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<EmailOutboxMessage>>([]);
        public Task<bool> MarkSendingAsync(Guid outboxMessageId, DateTime utcNow, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<int> ReleaseAbandonedClaimsAsync(TimeSpan claimTimeout, DateTime utcNow, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task MarkSentAsync(Guid outboxMessageId, DateTime utcNow, string? providerMessageId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task MarkFailedAsync(Guid outboxMessageId, string? errorCategory, string? errorMessage, DateTime utcNow, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<EmailOutboxMessage> QueueEmailAsync(QueueEmailRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new EmailOutboxMessage
            {
                TenantId = request.TenantId,
                EventKey = request.EventKey,
                TemplateKey = request.TemplateKey,
                RecipientEmail = request.RecipientEmail,
                Subject = request.Subject,
                Status = EmailOutboxStatuses.Pending
            });
        public Task RetryAsync(Guid outboxMessageId, DateTime nextAttemptAt, string? errorMessage, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubEmailTemplateService : IEmailTemplateService
    {
        public Task<IReadOnlyCollection<EmailTemplateDescriptor>> ListPlatformTemplatesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<EmailTemplateDescriptor>> ListTenantTemplatesAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EmailTemplateDescriptor> GetPlatformTemplateAsync(string eventKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EmailTemplateDescriptor> GetTenantTemplateAsync(Guid tenantId, string eventKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EmailTemplateDescriptor> SavePlatformTemplateAsync(string eventKey, EmailTemplateUpdateRequest request, Guid actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EmailTemplateDescriptor> SaveTenantTemplateAsync(Guid tenantId, string eventKey, EmailTemplateUpdateRequest request, Guid actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EmailTemplateDescriptor> ResetPlatformTemplateAsync(string eventKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EmailTemplateDescriptor> ResetTenantTemplateAsync(Guid tenantId, string eventKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RenderedEmailTemplate> RenderPlatformTemplateAsync(string eventKey, IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<RenderedEmailTemplate> RenderTenantTemplateAsync(Guid tenantId, string eventKey, IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RenderedEmailTemplate(
                eventKey,
                "Work order completed",
                "Work order completed",
                "<p>Completed</p>",
                "Completed",
                null,
                null,
                true));
    }

    private sealed class StubEmailSubjectRuleService : IEmailSubjectRuleService
    {
        public Task<Ecosys.Shared.Options.EmailSubjectRuleOptions> GetSettingsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Ecosys.Shared.Options.EmailSubjectRuleOptions.Default);

        public Task<string> BuildFinalSubjectAsync(Guid? tenantId, string eventKey, string templateSubject, string? tenantNameOverride = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(templateSubject);
    }

    private sealed class NoOpSlaService : ISlaService
    {
        public Task ApplyDefinitionAsync(WorkOrder workOrder, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> RefreshWorkOrderAsync(Guid workOrderId, DateTime nowUtc, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class NoOpEtaNotificationService : IEtaNotificationService
    {
        public Task NotifyAsync(Guid tenantId, WorkOrder workOrder, string eventType, string? technicianName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoOpServiceOpsNotifier : IServiceOpsNotifier
    {
        public Task NotifyWorkOrderUpdatedAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public Guid? TenantId => Guid.NewGuid();
        public Guid? UserId => Guid.NewGuid();
        public Guid? SessionId => Guid.NewGuid();
        public string? Email => "planner@acme.test";
        public string? Role => AppRoles.Admin;
        public string? JobTitle => null;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public bool IsAdmin => true;
        public bool HasRole(string role) => string.Equals(role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase);
        public bool HasPermission(string permissionName) => true;
        public Guid GetRequiredTenantId() => TenantId!.Value;
        public Guid GetRequiredUserId() => UserId!.Value;
        public Guid GetRequiredSessionId() => SessionId!.Value;
    }
}
