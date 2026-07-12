using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ecosys.Tests;

public sealed class WorkOrderLifecycleStateTests
{
    [Fact]
    public async Task CompleteAsync_RejectsCompletionBeforeArrival()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedLifecycleFixtureAsync(dbContext, WorkOrderStatuses.InProgress);

        var service = CreateLifecycleService(dbContext);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CompleteAsync(
            fixture.TenantId,
            fixture.WorkOrderId,
            fixture.ActorUserId,
            new WorkOrderCompletionRequest("Resolved on site."),
            CancellationToken.None));

        Assert.Contains("arrival", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshWorkOrderAsync_DoesNotBreachWhileActiveHoldExtendsDeadline()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedSlaFixtureAsync(dbContext);
        var service = CreateSlaService(dbContext);

        var changed = await service.RefreshWorkOrderAsync(fixture.WorkOrderId, fixture.CreatedAt.AddHours(1.5), CancellationToken.None);

        Assert.False(changed);

        var workOrder = await dbContext.WorkOrders.SingleAsync(x => x.Id == fixture.WorkOrderId);
        Assert.False(workOrder.SlaResolutionBreached);
        Assert.False(workOrder.SlaResponseBreached);
    }

    [Fact]
    public async Task ChangeStatusAsync_ResumeFromHoldShiftsOpenSlaDeadlines()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedSlaFixtureAsync(dbContext);
        var lifecycleService = CreateLifecycleService(dbContext, CreateSlaService(dbContext));

        var beforeResume = await dbContext.WorkOrders
            .AsNoTracking()
            .SingleAsync(x => x.Id == fixture.WorkOrderId);

        await lifecycleService.ChangeStatusAsync(
            fixture.TenantId,
            fixture.WorkOrderId,
            fixture.ActorUserId,
            new WorkOrderStatusChangeRequest(WorkOrderStatuses.InProgress, "Work resumed"),
            CancellationToken.None);

        var resumed = await dbContext.WorkOrders.SingleAsync(x => x.Id == fixture.WorkOrderId);

        Assert.Equal(WorkOrderStatuses.InProgress, resumed.Status);
        Assert.NotNull(resumed.HoldEndedAt);
        Assert.True(resumed.SlaResolutionDeadline > beforeResume.SlaResolutionDeadline);
        Assert.True(resumed.SlaResponseDeadline > beforeResume.SlaResponseDeadline);
    }

    [Fact]
    public async Task ChangeStatusAsync_OnHoldUsesCanonicalStatusAndReason()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedLifecycleFixtureAsync(dbContext, WorkOrderStatuses.InProgress);
        var lifecycleService = CreateLifecycleService(dbContext);

        await lifecycleService.ChangeStatusAsync(
            fixture.TenantId,
            fixture.WorkOrderId,
            fixture.ActorUserId,
            new WorkOrderStatusChangeRequest(
                WorkOrderStatuses.OnHold,
                WorkOrderHoldReasons.AwaitingMaterials,
                "Waiting for replacement belt."),
            CancellationToken.None);

        var updated = await dbContext.WorkOrders.SingleAsync(x => x.Id == fixture.WorkOrderId);

        Assert.Equal(WorkOrderStatuses.OnHold, updated.Status);
        Assert.Equal(WorkOrderHoldReasons.AwaitingMaterials, updated.HoldReason);
        Assert.Equal("Waiting for replacement belt.", updated.HoldNote);
        Assert.NotNull(updated.HoldStartedAt);
    }

    private static WorkOrderLifecycleService CreateLifecycleService(AppDbContext dbContext, ISlaService? slaService = null) =>
        new(
            dbContext,
            new TestTenantContext(),
            new AuditLogService(dbContext, new HttpContextAccessor()),
            new NoOpEmailOutboxService(),
            new StubEmailTemplateService(),
            new StubEmailSubjectRuleService(),
            slaService ?? new NoOpSlaService(),
            new NoOpServiceOpsNotifier(),
            Microsoft.Extensions.Options.Options.Create(new Ecosys.Shared.Options.PublicAppOptions()),
            NullLogger<WorkOrderLifecycleService>.Instance);

    private static SlaService CreateSlaService(AppDbContext dbContext) =>
        new(dbContext, new NoOpEmailOutboxService(), NullLogger<SlaService>.Instance);

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-work-order-state-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<(Guid TenantId, Guid ActorUserId, Guid WorkOrderId)> SeedLifecycleFixtureAsync(AppDbContext dbContext, string status)
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

        var user = new User
        {
            TenantId = tenant.Id,
            FullName = "Planner",
            Email = $"planner-{Guid.NewGuid():N}@acme.test",
            PasswordHash = "hash",
            Role = "Admin",
            IsActive = true,
            HasAllBranchAccess = true
        };

        var client = new Client
        {
            TenantId = tenant.Id,
            ClientName = "Acme Manufacturing",
            Email = "client@acme.test",
            IsActive = true
        };

        var workOrder = new WorkOrder
        {
            TenantId = tenant.Id,
            ClientId = client.Id,
            WorkOrderNumber = "WO-LIFECYCLE-001",
            Title = "Generator inspection",
            Priority = "High",
            Status = status,
            WorkStartedAt = DateTime.UtcNow.AddMinutes(-20)
        };

        dbContext.AddRange(tenant, user, client, workOrder);
        await dbContext.SaveChangesAsync();

        return (tenant.Id, user.Id, workOrder.Id);
    }

    private static async Task<(Guid TenantId, Guid ActorUserId, Guid WorkOrderId, DateTime CreatedAt)> SeedSlaFixtureAsync(AppDbContext dbContext)
    {
        var createdAt = new DateTime(2026, 5, 24, 8, 0, 0, DateTimeKind.Utc);
        var holdStartedAt = createdAt.AddMinutes(20);

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

        var user = new User
        {
            TenantId = tenant.Id,
            FullName = "Planner",
            Email = $"planner-{Guid.NewGuid():N}@acme.test",
            PasswordHash = "hash",
            Role = "Admin",
            IsActive = true,
            HasAllBranchAccess = true
        };

        var slaDefinition = new SlaDefinition
        {
            TenantId = tenant.Id,
            PlanName = "Priority plan",
            IsActive = true
        };

        var slaRule = new SlaRule
        {
            TenantId = tenant.Id,
            SlaDefinitionId = slaDefinition.Id,
            Priority = "High",
            ResponseTargetHours = 1,
            ResolutionTargetHours = 1,
            BusinessHoursOnly = false
        };

        var client = new Client
        {
            TenantId = tenant.Id,
            ClientName = "Acme Manufacturing",
            Email = "client@acme.test",
            IsActive = true,
            SlaDefinitionId = slaDefinition.Id
        };

        var workOrder = new WorkOrder
        {
            TenantId = tenant.Id,
            ClientId = client.Id,
            WorkOrderNumber = "WO-SLA-001",
            Title = "Generator inspection",
            Priority = "High",
            Status = WorkOrderStatuses.OnHold,
            CreatedAt = createdAt,
            WorkStartedAt = createdAt.AddMinutes(5),
            ArrivalAt = createdAt.AddMinutes(10),
            HoldReason = WorkOrderHoldReasons.PendingClient,
            HoldStartedAt = holdStartedAt
        };

        slaDefinition.Rules.Add(slaRule);
        dbContext.AddRange(tenant, user, slaDefinition, client, workOrder);
        await dbContext.SaveChangesAsync();

        var slaService = CreateSlaService(dbContext);
        await slaService.ApplyDefinitionAsync(workOrder, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        return (tenant.Id, user.Id, workOrder.Id, createdAt);
    }

    private sealed class NoOpEmailOutboxService : IEmailOutboxService
    {
        public Task CancelAsync(Guid outboxMessageId, string? errorMessage, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyCollection<EmailOutboxMessage>> GetPendingBatchAsync(int batchSize, DateTime utcNow, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<EmailOutboxMessage>>([]);
        public Task<bool> MarkSendingAsync(Guid outboxMessageId, DateTime utcNow, CancellationToken cancellationToken = default) => Task.FromResult(true);
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
            Task.FromResult(new RenderedEmailTemplate(eventKey, "Subject", "Subject", "<p>Body</p>", "Body", null, null, true));
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
