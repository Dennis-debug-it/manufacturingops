using System.Security.Claims;
using System.Text.Json;
using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ecosys.Tests;

public sealed class WorkOrderPhase3Tests
{
    [Fact]
    public async Task ApplyAssignments_MultiTechnicianLead_UsesRelationalAssignmentsAndClearsLegacyJson()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedWorkflowFixtureAsync(dbContext);
        var service = CreateAssignmentWorkflowService(dbContext);

        await service.ApplyAssignmentsAsync(
            fixture.Tenant.Id,
            fixture.WorkOrder.Id,
            fixture.Actor.Id,
            new WorkOrderAssignmentUpdateRequest(
                fixture.AssignmentGroup.Id,
                [fixture.TechnicianOne.Id, fixture.TechnicianTwo.Id],
                fixture.TechnicianTwo.Id,
                "Assign generator team"),
            false,
            CancellationToken.None);

        dbContext.ChangeTracker.Clear();

        var persisted = await dbContext.WorkOrders
            .Include(x => x.TechnicianAssignments)
            .SingleAsync(x => x.Id == fixture.WorkOrder.Id);

        Assert.Equal(2, persisted.TechnicianAssignments.Count);
        Assert.Equal(WorkOrderAssignmentTypes.MultipleTechnicians, persisted.AssignmentType);
        Assert.Equal(fixture.TechnicianTwo.Id, persisted.LeadTechnicianId);
        Assert.Equal(fixture.TechnicianTwo.Id, persisted.AssignedTechnicianId);
        Assert.Null(persisted.AssignedTechnicianIdsJson);

        var lead = Assert.Single(persisted.TechnicianAssignments, x => x.IsLead);
        Assert.Equal(fixture.TechnicianTwo.Id, lead.TechnicianId);
    }

    [Fact]
    public async Task ApplyAssignments_PushesNewJobOnlyToNewlyAssignedTechnicians()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedWorkflowFixtureAsync(dbContext);
        var userOne = Guid.NewGuid();
        var userTwo = Guid.NewGuid();
        fixture.TechnicianOne.UserId = userOne;
        fixture.TechnicianTwo.UserId = userTwo;
        await dbContext.SaveChangesAsync();
        var pushQueue = new RecordingPushQueue();
        var service = CreateAssignmentWorkflowService(dbContext, pushQueue);

        await service.ApplyAssignmentsAsync(
            fixture.Tenant.Id, fixture.WorkOrder.Id, fixture.Actor.Id,
            new WorkOrderAssignmentUpdateRequest(fixture.AssignmentGroup.Id, [fixture.TechnicianOne.Id], fixture.TechnicianOne.Id, null),
            false, CancellationToken.None);
        // Adding a second technician must not re-notify the first; changing only the lead notifies nobody.
        await service.ApplyAssignmentsAsync(
            fixture.Tenant.Id, fixture.WorkOrder.Id, fixture.Actor.Id,
            new WorkOrderAssignmentUpdateRequest(fixture.AssignmentGroup.Id, [fixture.TechnicianOne.Id, fixture.TechnicianTwo.Id], fixture.TechnicianOne.Id, null),
            true, CancellationToken.None);
        await service.ApplyAssignmentsAsync(
            fixture.Tenant.Id, fixture.WorkOrder.Id, fixture.Actor.Id,
            new WorkOrderAssignmentUpdateRequest(fixture.AssignmentGroup.Id, [fixture.TechnicianOne.Id, fixture.TechnicianTwo.Id], fixture.TechnicianTwo.Id, null),
            true, CancellationToken.None);

        Assert.Collection(
            pushQueue.Sent,
            first => Assert.Equal([userOne], first.UserIds),
            second => Assert.Equal([userTwo], second.UserIds));
        Assert.All(pushQueue.Sent, sent =>
        {
            Assert.Equal(fixture.Tenant.Id, sent.TenantId);
            Assert.Equal($"/tech/jobs/{fixture.WorkOrder.Id}", sent.Message.Url);
            Assert.Contains(fixture.WorkOrder.WorkOrderNumber, sent.Message.Title);
        });
    }

    [Fact]
    public async Task ApplyAssignments_ReassignAndUnassign_UpdatesCurrentAssignmentProjection()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedWorkflowFixtureAsync(dbContext);
        var service = CreateAssignmentWorkflowService(dbContext);

        await service.ApplyAssignmentsAsync(
            fixture.Tenant.Id,
            fixture.WorkOrder.Id,
            fixture.Actor.Id,
            new WorkOrderAssignmentUpdateRequest(
                fixture.AssignmentGroup.Id,
                [fixture.TechnicianOne.Id],
                fixture.TechnicianOne.Id,
                "Initial assignment"),
            false,
            CancellationToken.None);

        await service.ApplyAssignmentsAsync(
            fixture.Tenant.Id,
            fixture.WorkOrder.Id,
            fixture.Actor.Id,
            new WorkOrderAssignmentUpdateRequest(
                fixture.AssignmentGroup.Id,
                [fixture.TechnicianTwo.Id],
                fixture.TechnicianTwo.Id,
                "Reassign to second technician"),
            true,
            CancellationToken.None);

        await service.ApplyAssignmentsAsync(
            fixture.Tenant.Id,
            fixture.WorkOrder.Id,
            fixture.Actor.Id,
            new WorkOrderAssignmentUpdateRequest(null, [], null, "Clear assignment"),
            true,
            CancellationToken.None);

        dbContext.ChangeTracker.Clear();

        var persisted = await dbContext.WorkOrders
            .Include(x => x.TechnicianAssignments)
            .SingleAsync(x => x.Id == fixture.WorkOrder.Id);
        var history = await dbContext.WorkOrderAssignmentHistories
            .Where(x => x.WorkOrderId == fixture.WorkOrder.Id)
            .ToListAsync();

        Assert.Empty(persisted.TechnicianAssignments);
        Assert.Equal(WorkOrderAssignmentTypes.Unassigned, persisted.AssignmentType);
        Assert.Null(persisted.AssignedTechnicianId);
        Assert.Null(persisted.LeadTechnicianId);
        Assert.Null(persisted.AssignmentGroupId);
        Assert.Equal(WorkOrderStatuses.Open, persisted.Status);
        Assert.Contains(history, item => item.Action == "TechnicianReassigned");
    }

    [Fact]
    public async Task Create_NewInstallationFromInstalledSolarTemplate_AttachesTheChecklist()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedControllerFixtureAsync(dbContext);
        fixture.Tenant.Name = "Plexus Energy";
        await dbContext.SaveChangesAsync();
        var installed = await new Ecosys.Infrastructure.Reports.ClientTemplateInstaller(dbContext, new PmTemplateService(dbContext))
            .InstallAsync(fixture.Tenant.Id, Ecosys.Infrastructure.Reports.SolarInstallationTemplate.LayoutKey, CancellationToken.None);
        var jobTemplate = await dbContext.WorkOrderTemplates.SingleAsync(x => x.TenantId == fixture.Tenant.Id && x.ChecklistTemplateId == installed.TemplateId);

        var controller = new WorkOrdersController(
            dbContext,
            new FakeTenantContext(fixture.Tenant.Id, fixture.Actor.Id, fixture.Actor.Email),
            new NoOpDocumentNumberingService(),
            new AuditLogService(dbContext, new HttpContextAccessor()),
            new NoOpUserAccessService(),
            new NoOpBranchAccessService(),
            new NoOpLicenseGuardService(),
            new NoOpWorkOrderLifecycleService(),
            new NoOpAssignmentWorkflowService(),
            new PmWorkOrderChecklistService(dbContext, new AuditLogService(dbContext, new HttpContextAccessor())),
            new NoOpSlaService(),
            new NoOpWorkflowService());
        controller.ControllerContext = CreateWorkOrdersController(dbContext, fixture).ControllerContext;

        // What the New Work Order form sends after picking the "Solar PV Installation" template.
        var result = await controller.Create(
            new CreateWorkOrderRequest(
                fixture.Client.Id, null, null, null, null, null, null, null, null, null, null,
                "11 kWp hybrid install", null, WorkOrderPriorities.Medium, null,
                WorkOrderTypes.NewInstallation, false, null, TemplateId: jobTemplate.Id),
            CancellationToken.None);

        var created = Assert.IsType<WorkOrderResponse>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        dbContext.ChangeTracker.Clear();
        var persisted = await dbContext.WorkOrders.Include(x => x.ChecklistItems).SingleAsync(x => x.Id == created.Id);
        Assert.Equal(installed.TemplateId, persisted.PmTemplateId);
        Assert.Equal(Ecosys.Infrastructure.Reports.SolarInstallationTemplate.Fields.Count, persisted.ChecklistItems.Count);
    }

    [Fact]
    public async Task Create_WithTechnician_AssignsOnCreation()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedControllerFixtureAsync(dbContext);
        var technician = new Technician { TenantId = fixture.Tenant.Id, BranchId = fixture.Branch.Id, FullName = "John Kamau", Email = "john@acme.test", IsActive = true };
        dbContext.Technicians.Add(technician);
        await dbContext.SaveChangesAsync();
        var controller = CreateWorkOrdersController(dbContext, fixture, CreateAssignmentWorkflowService(dbContext));

        // Exactly what the New Work Order form sends when a technician is picked.
        var result = await controller.Create(
            new CreateWorkOrderRequest(
                fixture.Client.Id,
                null,
                null,
                null,
                fixture.Asset.Id,
                null,
                WorkOrderAssignmentTypes.IndividualTechnician,
                technician.Id,
                null,
                null,
                null,
                "Generator triage",
                null,
                WorkOrderPriorities.Medium,
                null,
                WorkOrderTypes.CorrectiveMaintenance,
                false,
                null),
            CancellationToken.None);

        var created = Assert.IsType<WorkOrderResponse>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        dbContext.ChangeTracker.Clear();
        var persisted = await dbContext.WorkOrders.Include(x => x.TechnicianAssignments).SingleAsync(x => x.Id == created.Id);
        Assert.Equal(technician.Id, persisted.AssignedTechnicianId);
        Assert.Single(persisted.TechnicianAssignments);
        Assert.Equal(WorkOrderStatuses.Assigned, persisted.Status);
        Assert.Equal(WorkOrderAssignmentTypes.IndividualTechnician, persisted.AssignmentType);
    }

    [Fact]
    public async Task Reassign_TechniciansWithoutTeam_AssignsThem()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedWorkflowFixtureAsync(dbContext);
        var controller = new WorkOrderAssignmentsController(
            dbContext,
            new FakeTenantContext(fixture.Tenant.Id, fixture.Actor.Id, fixture.Actor.Email),
            new NoOpUserAccessService(),
            new NoOpBranchAccessService(),
            CreateAssignmentWorkflowService(dbContext));

        var result = await controller.Reassign(
            fixture.WorkOrder.Id,
            new ReassignActionRequest(null, [fixture.TechnicianOne.Id, fixture.TechnicianTwo.Id], fixture.TechnicianTwo.Id, null),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        dbContext.ChangeTracker.Clear();
        var persisted = await dbContext.WorkOrders.Include(x => x.TechnicianAssignments).SingleAsync(x => x.Id == fixture.WorkOrder.Id);
        Assert.Null(persisted.AssignmentGroupId);
        Assert.Equal(2, persisted.TechnicianAssignments.Count);
        Assert.Equal(fixture.TechnicianTwo.Id, persisted.LeadTechnicianId);
        Assert.Equal(WorkOrderAssignmentTypes.MultipleTechnicians, persisted.AssignmentType);
    }

    [Fact]
    public async Task Create_RejectsInvalidPriority()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedControllerFixtureAsync(dbContext);
        var controller = CreateWorkOrdersController(dbContext, fixture);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Create(
            new CreateWorkOrderRequest(
                fixture.Client.Id,
                fixture.Branch.Id,
                null,
                null,
                fixture.Asset.Id,
                null,
                WorkOrderAssignmentTypes.Unassigned,
                null,
                null,
                null,
                null,
                "Generator triage",
                "Validate invalid priority handling.",
                "Urgent",
                null,
                WorkOrderTypes.CorrectiveMaintenance,
                false,
                null),
            CancellationToken.None));

        Assert.Contains("Priority", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_RejectsInvalidWorkType()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedControllerFixtureAsync(dbContext);
        var controller = CreateWorkOrdersController(dbContext, fixture);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Create(
            new CreateWorkOrderRequest(
                fixture.Client.Id,
                fixture.Branch.Id,
                null,
                null,
                fixture.Asset.Id,
                null,
                WorkOrderAssignmentTypes.Unassigned,
                null,
                null,
                null,
                null,
                "Generator triage",
                "Validate invalid work type handling.",
                WorkOrderPriorities.Medium,
                null,
                "ServiceRequest",
                false,
                null),
            CancellationToken.None));

        Assert.Contains("work type", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_RejectsInvalidStatus()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedControllerFixtureAsync(dbContext);
        var controller = CreateWorkOrdersController(dbContext, fixture);
        var workOrder = new WorkOrder
        {
            TenantId = fixture.Tenant.Id,
            BranchId = fixture.Branch.Id,
            ClientId = fixture.Client.Id,
            AssetId = fixture.Asset.Id,
            AssignmentType = WorkOrderAssignmentTypes.Unassigned,
            WorkOrderNumber = "WO-STATUS-001",
            Title = "Generator triage",
            Priority = WorkOrderPriorities.Medium,
            Status = WorkOrderStatuses.Open,
            WorkType = WorkOrderTypes.CorrectiveMaintenance
        };

        dbContext.WorkOrders.Add(workOrder);
        await dbContext.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Update(
            workOrder.Id,
            new UpdateWorkOrderRequest(
                fixture.Client.Id,
                fixture.Branch.Id,
                null,
                null,
                fixture.Asset.Id,
                null,
                WorkOrderAssignmentTypes.Unassigned,
                null,
                null,
                null,
                null,
                workOrder.Title,
                workOrder.Description,
                workOrder.Priority,
                "Dormant",
                null,
                workOrder.WorkType,
                null),
            CancellationToken.None));

        Assert.Contains("status", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_LoadsLegacyAssignedTechnicianIds_WhenRelationalAssignmentsAreMissing()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedControllerFixtureAsync(dbContext);
        var legacyIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var workOrder = new WorkOrder
        {
            TenantId = fixture.Tenant.Id,
            BranchId = fixture.Branch.Id,
            ClientId = fixture.Client.Id,
            AssetId = fixture.Asset.Id,
            AssignmentType = WorkOrderAssignmentTypes.MultipleTechnicians,
            WorkOrderNumber = "WO-LEGACY-001",
            Title = "Legacy assignment load",
            Priority = WorkOrderPriorities.Medium,
            Status = WorkOrderStatuses.Assigned,
            WorkType = WorkOrderTypes.CorrectiveMaintenance,
            AssignedTechnicianIdsJson = JsonSerializer.Serialize(legacyIds)
        };

        dbContext.WorkOrders.Add(workOrder);
        await dbContext.SaveChangesAsync();

        var controller = CreateWorkOrdersController(dbContext, fixture);
        var result = await controller.Get(workOrder.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<WorkOrderResponse>(ok.Value);
        Assert.Equal(legacyIds, response.AssignedTechnicianIds);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-work-order-phase3-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<WorkflowFixture> SeedWorkflowFixtureAsync(AppDbContext dbContext)
    {
        var tenant = CreateTenant();
        var branch = CreateBranch(tenant.Id);
        var actor = CreateUser(tenant.Id, "admin");
        var client = CreateClient(tenant.Id);
        var asset = CreateAsset(tenant.Id, branch.Id, client.Id);
        var workOrder = new WorkOrder
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            ClientId = client.Id,
            AssetId = asset.Id,
            AssignmentType = WorkOrderAssignmentTypes.Unassigned,
            WorkOrderNumber = "WO-WORKFLOW-001",
            Title = "Generator repair",
            Priority = WorkOrderPriorities.Medium,
            Status = WorkOrderStatuses.Open,
            WorkType = WorkOrderTypes.CorrectiveMaintenance
        };
        var assignmentGroup = new AssignmentGroup
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            Name = $"Generator Team {Guid.NewGuid():N}"[..20],
            IsActive = true
        };
        var technicianOne = CreateTechnician(tenant.Id, branch.Id, "tech-one");
        var technicianTwo = CreateTechnician(tenant.Id, branch.Id, "tech-two");
        var memberOne = new AssignmentGroupMember
        {
            TenantId = tenant.Id,
            AssignmentGroupId = assignmentGroup.Id,
            TechnicianId = technicianOne.Id,
            IsActive = true,
            AddedAt = DateTime.UtcNow
        };
        var memberTwo = new AssignmentGroupMember
        {
            TenantId = tenant.Id,
            AssignmentGroupId = assignmentGroup.Id,
            TechnicianId = technicianTwo.Id,
            IsActive = true,
            AddedAt = DateTime.UtcNow
        };

        dbContext.AddRange(tenant, branch, actor, client, asset, workOrder, assignmentGroup, technicianOne, technicianTwo, memberOne, memberTwo);
        await dbContext.SaveChangesAsync();
        return new WorkflowFixture(tenant, branch, actor, client, asset, workOrder, assignmentGroup, technicianOne, technicianTwo);
    }

    private static async Task<ControllerFixture> SeedControllerFixtureAsync(AppDbContext dbContext)
    {
        var tenant = CreateTenant();
        var branch = CreateBranch(tenant.Id);
        var actor = CreateUser(tenant.Id, "ops");
        var client = CreateClient(tenant.Id);
        var asset = CreateAsset(tenant.Id, branch.Id, client.Id);

        dbContext.AddRange(tenant, branch, actor, client, asset);
        await dbContext.SaveChangesAsync();
        return new ControllerFixture(tenant, branch, actor, client, asset);
    }

    private static Tenant CreateTenant() =>
        new()
        {
            Name = "Acme",
            Slug = $"acme-{Guid.NewGuid():N}"[..12],
            CompanyName = "Acme Facilities",
            Email = "ops@acme.test",
            Country = "Kenya",
            Status = "Active",
            LicenseStatus = "Active",
            IsActive = true
        };

    private static Branch CreateBranch(Guid tenantId) =>
        new()
        {
            TenantId = tenantId,
            Name = "Head Office",
            Code = $"HO-{Guid.NewGuid():N}"[..8],
            IsActive = true
        };

    private static User CreateUser(Guid tenantId, string seed) =>
        new()
        {
            TenantId = tenantId,
            FullName = "Ops Admin",
            Email = $"{seed}-{Guid.NewGuid():N}@acme.test",
            PasswordHash = "hash",
            Role = AppRoles.Admin,
            JobTitle = "Admin",
            IsActive = true,
            HasAllBranchAccess = true
        };

    private static Client CreateClient(Guid tenantId) =>
        new()
        {
            TenantId = tenantId,
            ClientName = "Acme Manufacturing",
            Email = "client@acme.test",
            IsActive = true
        };

    private static Asset CreateAsset(Guid tenantId, Guid branchId, Guid clientId) =>
        new()
        {
            TenantId = tenantId,
            BranchId = branchId,
            ClientId = clientId,
            AssetName = "Generator 01",
            AssetCode = $"GEN-{Guid.NewGuid():N}"[..10],
            Status = "Active"
        };

    private static Technician CreateTechnician(Guid tenantId, Guid branchId, string seed) =>
        new()
        {
            TenantId = tenantId,
            BranchId = branchId,
            FullName = $"{seed} technician",
            Email = $"{seed}-{Guid.NewGuid():N}@acme.test",
            IsActive = true
        };

    private static WorkOrderAssignmentWorkflowService CreateAssignmentWorkflowService(AppDbContext dbContext, IPushNotificationQueue? pushQueue = null) =>
        new(
            dbContext,
            new AuditLogService(dbContext, new HttpContextAccessor()),
            new NoOpEmailOutboxService(),
            new NoOpEmailTemplateService(),
            new NoOpEmailSubjectRuleService(),
            new NoOpSlaService(),
            new NoOpEtaNotificationService(),
            pushQueue ?? new RecordingPushQueue(),
            NullLogger<WorkOrderAssignmentWorkflowService>.Instance);

    private sealed class RecordingPushQueue : IPushNotificationQueue
    {
        public List<(Guid TenantId, IReadOnlyCollection<Guid> UserIds, PushMessage Message)> Sent { get; } = [];

        public void Enqueue(Guid tenantId, IReadOnlyCollection<Guid> userIds, PushMessage message) => Sent.Add((tenantId, userIds, message));
    }

    private static WorkOrdersController CreateWorkOrdersController(AppDbContext dbContext, ControllerFixture fixture, IWorkOrderAssignmentWorkflowService? assignmentWorkflowService = null)
    {
        var controller = new WorkOrdersController(
            dbContext,
            new FakeTenantContext(fixture.Tenant.Id, fixture.Actor.Id, fixture.Actor.Email),
            new NoOpDocumentNumberingService(),
            new AuditLogService(dbContext, new HttpContextAccessor()),
            new NoOpUserAccessService(),
            new NoOpBranchAccessService(),
            new NoOpLicenseGuardService(),
            new NoOpWorkOrderLifecycleService(),
            assignmentWorkflowService ?? new NoOpAssignmentWorkflowService(),
            new NoOpPmChecklistService(),
            new NoOpSlaService(),
            new NoOpWorkflowService());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, fixture.Actor.Id.ToString()),
                    new Claim(ClaimTypes.Name, fixture.Actor.FullName),
                    new Claim(ClaimTypes.Email, fixture.Actor.Email)
                ], "Test"))
            }
        };

        return controller;
    }

    private sealed record WorkflowFixture(
        Tenant Tenant,
        Branch Branch,
        User Actor,
        Client Client,
        Asset Asset,
        WorkOrder WorkOrder,
        AssignmentGroup AssignmentGroup,
        Technician TechnicianOne,
        Technician TechnicianTwo);

    private sealed record ControllerFixture(Tenant Tenant, Branch Branch, User Actor, Client Client, Asset Asset);

    private sealed class FakeTenantContext(Guid tenantId, Guid userId, string email) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
        public Guid? SessionId => Guid.NewGuid();
        public string? Email => email;
        public string? Role => AppRoles.Admin;
        public string? JobTitle => "Admin";
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public bool IsAdmin => true;
        public bool HasRole(string role) => string.Equals(role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase);
        public bool HasPermission(string permissionName) => true;
        public Guid GetRequiredTenantId() => tenantId;
        public Guid GetRequiredUserId() => userId;
        public Guid GetRequiredSessionId() => SessionId!.Value;
    }

    private sealed class NoOpEmailOutboxService : IEmailOutboxService
    {
        public Task CancelAsync(Guid outboxMessageId, string? errorMessage, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyCollection<EmailOutboxMessage>> GetPendingBatchAsync(int batchSize, DateTime utcNow, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<EmailOutboxMessage>>([]);
        public Task<bool> MarkSendingAsync(Guid outboxMessageId, DateTime utcNow, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<int> ReleaseAbandonedClaimsAsync(TimeSpan claimTimeout, DateTime utcNow, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task MarkSentAsync(Guid outboxMessageId, DateTime utcNow, string? providerMessageId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task MarkFailedAsync(Guid outboxMessageId, string? errorCategory, string? errorMessage, DateTime utcNow, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<EmailOutboxMessage> QueueEmailAsync(QueueEmailRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new EmailOutboxMessage { RecipientEmail = request.RecipientEmail, Subject = request.Subject, Status = EmailOutboxStatuses.Pending });
        public Task RetryAsync(Guid outboxMessageId, DateTime nextAttemptAt, string? errorMessage, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoOpEmailTemplateService : IEmailTemplateService
    {
        public Task<EmailTemplateDescriptor> GetPlatformTemplateAsync(string eventKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EmailTemplateDescriptor> GetTenantTemplateAsync(Guid tenantId, string eventKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<EmailTemplateDescriptor>> ListPlatformTemplatesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<EmailTemplateDescriptor>>([]);
        public Task<IReadOnlyCollection<EmailTemplateDescriptor>> ListTenantTemplatesAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<EmailTemplateDescriptor>>([]);
        public Task<RenderedEmailTemplate> RenderPlatformTemplateAsync(string eventKey, IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RenderedEmailTemplate> RenderTenantTemplateAsync(Guid tenantId, string eventKey, IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RenderedEmailTemplate(eventKey, eventKey, eventKey, "<p>ok</p>", "ok", null, null, false));
        public Task<EmailTemplateDescriptor> ResetPlatformTemplateAsync(string eventKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EmailTemplateDescriptor> ResetTenantTemplateAsync(Guid tenantId, string eventKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EmailTemplateDescriptor> SavePlatformTemplateAsync(string eventKey, EmailTemplateUpdateRequest request, Guid actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EmailTemplateDescriptor> SaveTenantTemplateAsync(Guid tenantId, string eventKey, EmailTemplateUpdateRequest request, Guid actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NoOpEmailSubjectRuleService : IEmailSubjectRuleService
    {
        public Task<string> BuildFinalSubjectAsync(Guid? tenantId, string eventKey, string templateSubject, string? tenantNameOverride = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(templateSubject);

        public Task<Ecosys.Shared.Options.EmailSubjectRuleOptions> GetSettingsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Ecosys.Shared.Options.EmailSubjectRuleOptions.Default);
    }

    private sealed class NoOpWorkflowService : IWorkflowService
    {
        public Task<WorkflowDefinition?> GetActiveWorkflowAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<WorkflowDefinition?>(null);
        public Task<IReadOnlyCollection<WorkflowStatus>> GetActiveWorkflowStatusesAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<WorkflowStatus>>([]);
        public Task EnsureDefaultWorkflowAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<WorkflowStatus> ApplyStatusTransitionAsync(WorkOrder workOrder, Guid targetWorkflowStatusId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
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

    private sealed class NoOpUserAccessService : IUserAccessService
    {
        public void EnsureAdmin() { }
        public void EnsureAdminOrPermission(string permissionName) { }
        public void EnsureAdminOrAnyPermission(params string[] permissionNames) { }
        public void EnsureTenantOperationalAccess() { }
    }

    private sealed class NoOpBranchAccessService : IBranchAccessService
    {
        public Task EnsureCanAccessBranchAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyCollection<Guid>> GetAccessibleBranchIdsAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<Guid>>([]);
        public Task<BranchQueryScope> GetQueryScopeAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new BranchQueryScope(false, requestedBranchId, [], true));
        public Task<Guid?> ResolveBranchIdForWriteAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(requestedBranchId);
    }

    private sealed class NoOpDocumentNumberingService : IDocumentNumberingService
    {
        public Task<string> GenerateAsync(Guid tenantId, Guid? branchId, string documentType, CancellationToken cancellationToken = default) =>
            Task.FromResult("WO-TEST-001");

        public Task<NumberingSetting> UpsertAsync(Guid tenantId, Guid? branchId, string documentType, string prefix, long nextNumber, int paddingLength, string resetFrequency, bool includeYear, bool includeMonth, bool isActive, CancellationToken cancellationToken = default, string? suffix = null, string yearFormat = "YYYY", string separator = "-", bool isLocked = false) =>
            throw new NotSupportedException();
    }

    private sealed class NoOpLicenseGuardService : ILicenseGuardService
    {
        public Task<TenantLicenseSnapshot> GetSnapshotAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LicenseUsageSnapshot> GetUsageAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PlatformLicenseUsageSnapshot>> GetPlatformUsageAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<PlatformLicenseUsageSnapshot>>([]);
        public Task EnsureTenantCanMutateAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureCanCreateBranchAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureCanCreateUserAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureFeatureEnabledAsync(Guid tenantId, string featureName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<TenantLicense> GetOrCreateTenantLicenseAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NoOpWorkOrderLifecycleService : IWorkOrderLifecycleService
    {
        public Task<WorkOrderEvent> AddCommentAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, WorkOrderCommentRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkOrder> ArriveAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, Ecosys.Infrastructure.Services.WorkOrderLocationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkOrder> AssignAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, Guid? technicianId, Guid? assignmentGroupId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkOrder> ChangeStatusAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, WorkOrderStatusChangeRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkOrder> CompleteAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, WorkOrderCompletionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkOrder> DepartAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, Ecosys.Infrastructure.Services.WorkOrderLocationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<WorkOrderEvent>> GetEventsAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<WorkOrderEvent>>([]);
        public string NormalizeStatus(string status) => WorkOrderStatuses.Normalize(status);
        public Task RecordCreatedAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, string workOrderNumber, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<WorkOrder> StartAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkOrder> HoldAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, string? holdType, string? reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkOrder> ResumeAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NoOpAssignmentWorkflowService : IWorkOrderAssignmentWorkflowService
    {
        public Task ApplyAssignmentsAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, WorkOrderAssignmentUpdateRequest request, bool isReassignment, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyCollection<WorkOrderAssignmentHistory>> GetHistoryAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<WorkOrderAssignmentHistory>>([]);
        public Task<bool> HasAcceptedTechnicianAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> HasArrivalAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task RecordArrivalAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, TechnicianArrivalRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RecordDepartureAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, TechnicianDepartureRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RecordInTransitAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, TechnicianInTransitRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RecordTechnicianResponseAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, TechnicianDispatchResponseRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoOpPmChecklistService : IPmWorkOrderChecklistService
    {
        public Task<int> AttachPmTemplateToWorkOrderAsync(Guid workOrderId, Guid pmTemplateId, Guid tenantId, Guid? actorUserId = null, bool replaceExisting = false, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task EnsureRequiredChecklistCompletedAsync(Guid workOrderId, Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<PmTemplate> GetActiveTemplateAsync(Guid tenantId, Guid pmTemplateId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkOrderChecklistItem> UpdateChecklistItemAsync(Guid workOrderId, Guid itemId, Guid tenantId, Guid actorUserId, string? responseValue, string? remarks, bool isCompleted, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
