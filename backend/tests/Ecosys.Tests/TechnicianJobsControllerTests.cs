using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

public sealed class TechnicianJobsControllerTests
{
    [Fact]
    public async Task List_ForTechnician_ReturnsOnlyAssignedJobs()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);

        var controller = CreateController(dbContext, new FakeTenantContext(fixture.Tenant.Id, fixture.User.Id, fixture.User.Email, isAdmin: false));

        var result = await controller.List(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var jobs = Assert.IsAssignableFrom<IReadOnlyCollection<WorkOrderResponse>>(ok.Value);
        var job = Assert.Single(jobs);
        Assert.Equal(fixture.AssignedWorkOrder.Id, job.Id);
        Assert.Equal("WO-TECH-001", job.WorkOrderNumber);
        Assert.Equal(WorkOrderTypes.CorrectiveMaintenance, job.WorkType);
    }

    [Fact]
    public async Task Get_ForDifferentTechnicianJob_ReturnsForbidden()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);

        var controller = CreateController(dbContext, new FakeTenantContext(fixture.Tenant.Id, fixture.User.Id, fixture.User.Email, isAdmin: false));

        await Assert.ThrowsAsync<ForbiddenException>(() => controller.Get(fixture.OtherWorkOrder.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Get_ForAssignedTechnicianJob_ReturnsAssignedJob()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);

        var controller = CreateController(dbContext, new FakeTenantContext(fixture.Tenant.Id, fixture.User.Id, fixture.User.Email, isAdmin: false));

        var result = await controller.Get(fixture.AssignedWorkOrder.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var job = Assert.IsType<WorkOrderResponse>(ok.Value);
        Assert.Equal(fixture.AssignedWorkOrder.Id, job.Id);
        Assert.Equal("WO-TECH-001", job.WorkOrderNumber);
        Assert.Equal(WorkOrderTypes.CorrectiveMaintenance, job.WorkType);
        Assert.Equal("CNT-TECH-001", job.ContractNumber);
        Assert.Equal("Covered", job.CoverageStatus);
        Assert.Equal("Included", job.BillingModel);
        Assert.Null(job.EstimatedCharge);
        Assert.Null(job.FinalCharge);
        Assert.Null(job.RateCardName);
    }

    [Fact]
    public async Task List_ForAdmin_ReturnsAllAccessibleJobs()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);

        var controller = CreateController(dbContext, new FakeTenantContext(fixture.Tenant.Id, fixture.User.Id, fixture.User.Email, isAdmin: true));

        var result = await controller.List(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var jobs = Assert.IsAssignableFrom<IReadOnlyCollection<WorkOrderResponse>>(ok.Value);
        Assert.Equal(2, jobs.Count);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-technician-jobs-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static TechnicianJobsController CreateController(AppDbContext dbContext, FakeTenantContext tenantContext) =>
        new(
            dbContext,
            tenantContext,
            new NoOpUserAccessService(),
            new NoOpBranchAccessService());

    private static async Task<FixtureData> SeedFixtureAsync(AppDbContext dbContext)
    {
        var tenant = new Tenant
        {
            Name = "Acme",
            Slug = $"acme-{Guid.NewGuid():N}".Substring(0, 12),
            CompanyName = "Acme Facilities",
            Email = "ops@acme.test",
            Country = "Kenya",
            Status = "Active",
            LicenseStatus = "Active",
            PrimaryColor = "#0F4C81",
            SecondaryColor = "#F4B942",
            IsActive = true
        };

        var user = new User
        {
            TenantId = tenant.Id,
            FullName = "Field Technician",
            Email = $"tech-{Guid.NewGuid():N}@acme.test",
            PasswordHash = "hash",
            Role = AppRoles.User,
            JobTitle = "Technician",
            IsActive = true,
            HasAllBranchAccess = false
        };

        var client = new Client
        {
            TenantId = tenant.Id,
            ClientName = "Acme Facilities",
            Email = "client@acme.test",
            IsActive = true
        };

        var technician = new Technician
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            FullName = "Field Technician",
            Email = user.Email,
            IsActive = true
        };

        var otherTechnician = new Technician
        {
            TenantId = tenant.Id,
            FullName = "Other Technician",
            Email = $"other-{Guid.NewGuid():N}@acme.test",
            IsActive = true
        };

        var assignedWorkOrder = new WorkOrder
        {
            TenantId = tenant.Id,
            ClientId = client.Id,
            WorkOrderNumber = "WO-TECH-001",
            Title = "Assigned job",
            Priority = "High",
            Status = "Open",
            WorkType = WorkOrderTypes.CorrectiveMaintenance,
            AssignedTechnicianId = technician.Id,
            LeadTechnicianId = technician.Id,
            AssignmentType = "IndividualTechnician",
            CoverageStatus = "Covered",
            BillingModel = "Included",
            PricingNotes = "Follow shutdown procedure.",
            Contract = new Contract
            {
                TenantId = tenant.Id,
                ClientId = client.Id,
                ContractNumber = "CNT-TECH-001",
                Name = "Technician visibility contract",
                ContractType = Ecosys.Domain.Enums.ContractType.ComprehensiveMaintenance,
                Status = Ecosys.Domain.Enums.ContractStatus.Active,
                StartDate = DateTime.UtcNow.Date.AddDays(-30),
                EndDate = DateTime.UtcNow.Date.AddDays(30),
                BillingModel = Ecosys.Domain.Enums.BillingModel.Included,
                Currency = "KES",
                RenewalNoticeDays = 30,
                RenewalRisk = Ecosys.Domain.Enums.RenewalRisk.Medium
            }
        };

        var otherWorkOrder = new WorkOrder
        {
            TenantId = tenant.Id,
            ClientId = client.Id,
            WorkOrderNumber = "WO-TECH-002",
            Title = "Another job",
            Priority = "Medium",
            Status = "Open",
            WorkType = WorkOrderTypes.InspectionAssessment,
            AssignedTechnicianId = otherTechnician.Id,
            LeadTechnicianId = otherTechnician.Id,
            AssignmentType = "IndividualTechnician"
        };

        dbContext.AddRange(tenant, user, client, technician, otherTechnician, assignedWorkOrder, otherWorkOrder);
        dbContext.WorkOrderTechnicianAssignments.AddRange(
            new WorkOrderTechnicianAssignment
            {
                TenantId = tenant.Id,
                WorkOrderId = assignedWorkOrder.Id,
                TechnicianId = technician.Id,
                IsLead = true,
                Status = "Accepted"
            },
            new WorkOrderTechnicianAssignment
            {
                TenantId = tenant.Id,
                WorkOrderId = otherWorkOrder.Id,
                TechnicianId = otherTechnician.Id,
                IsLead = true,
                Status = "Accepted"
            });

        await dbContext.SaveChangesAsync();
        return new FixtureData(tenant, user, assignedWorkOrder, otherWorkOrder);
    }

    private sealed record FixtureData(Tenant Tenant, User User, WorkOrder AssignedWorkOrder, WorkOrder OtherWorkOrder);

    private sealed class FakeTenantContext(Guid tenantId, Guid userId, string email, bool isAdmin) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
        public Guid? SessionId => Guid.NewGuid();
        public string? Email => email;
        public string? Role => isAdmin ? AppRoles.Admin : AppRoles.User;
        public string? JobTitle => isAdmin ? "Admin" : "Technician";
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public bool IsAdmin => isAdmin;
        public bool HasRole(string role) => string.Equals(role, Role, StringComparison.OrdinalIgnoreCase);
        public bool HasPermission(string permissionName) => true;
        public Guid GetRequiredTenantId() => tenantId;
        public Guid GetRequiredUserId() => userId;
        public Guid GetRequiredSessionId() => SessionId!.Value;
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
        public Task<BranchQueryScope> GetQueryScopeAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new BranchQueryScope(false, requestedBranchId, [], true));

        public Task<Guid?> ResolveBranchIdForWriteAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(requestedBranchId);

        public Task EnsureCanAccessBranchAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyCollection<Guid>> GetAccessibleBranchIdsAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Guid>>([]);
    }
}
