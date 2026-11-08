using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Domain.Enums;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

public sealed class ClientDeletionTests
{
    [Fact]
    public async Task DeletePermanent_RemovesClientWithNoBlockingRecords()
    {
        var databaseName = $"ecosys-client-delete-{Guid.NewGuid():N}";
        var tenantId = Guid.NewGuid();
        var clientId = await SeedClientAsync(databaseName, tenantId);

        var tenantContext = new FakeTenantContext(tenantId, Guid.NewGuid());
        await using var dbContext = CreateDbContext(databaseName, tenantContext);
        var controller = new ClientsController(dbContext, tenantContext, new AllowAllUserAccessService(), new NoOpAuditLogService());

        var result = await controller.DeletePermanent(clientId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.False(await dbContext.Clients.IgnoreQueryFilters().AnyAsync(x => x.Id == clientId));
    }

    [Fact]
    public async Task DeletePermanent_BlockedByAnyWorkOrderIncludingCompletedOnes()
    {
        var databaseName = $"ecosys-client-delete-{Guid.NewGuid():N}";
        var tenantId = Guid.NewGuid();
        var clientId = await SeedClientAsync(databaseName, tenantId, seed: (db, client) =>
        {
            db.WorkOrders.Add(new WorkOrder
            {
                TenantId = tenantId,
                ClientId = client.Id,
                WorkOrderNumber = "WO-001",
                Title = "Finished job",
                Priority = "High",
                Status = "Completed",
                WorkType = WorkOrderTypes.CorrectiveMaintenance,
                AssignmentType = "Unassigned"
            });
        });

        var tenantContext = new FakeTenantContext(tenantId, Guid.NewGuid());
        await using var dbContext = CreateDbContext(databaseName, tenantContext);
        var controller = new ClientsController(dbContext, tenantContext, new AllowAllUserAccessService(), new NoOpAuditLogService());

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => controller.DeletePermanent(clientId, CancellationToken.None));

        Assert.Contains("1 work order", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(await dbContext.Clients.AnyAsync(x => x.Id == clientId));
    }

    [Fact]
    public async Task DeletePermanent_AllowedWhenOnlyCascadingSitesExist()
    {
        var databaseName = $"ecosys-client-delete-{Guid.NewGuid():N}";
        var tenantId = Guid.NewGuid();
        var clientId = await SeedClientAsync(databaseName, tenantId, seed: (db, client) =>
        {
            db.Sites.Add(new Site
            {
                TenantId = tenantId,
                ClientId = client.Id,
                SiteCode = "SITE-1",
                SiteName = "Head Office",
                SiteType = "Office",
                Status = "Active"
            });
        });

        var tenantContext = new FakeTenantContext(tenantId, Guid.NewGuid());
        await using var dbContext = CreateDbContext(databaseName, tenantContext);
        var controller = new ClientsController(dbContext, tenantContext, new AllowAllUserAccessService(), new NoOpAuditLogService());

        var result = await controller.DeletePermanent(clientId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task ValidateDeletion_ReportsBlockingCounts()
    {
        var databaseName = $"ecosys-client-delete-{Guid.NewGuid():N}";
        var tenantId = Guid.NewGuid();
        var clientId = await SeedClientAsync(databaseName, tenantId, seed: (db, client) =>
        {
            db.WorkOrders.Add(new WorkOrder
            {
                TenantId = tenantId,
                ClientId = client.Id,
                WorkOrderNumber = "WO-001",
                Title = "Open job",
                Priority = "High",
                Status = "Open",
                WorkType = WorkOrderTypes.CorrectiveMaintenance,
                AssignmentType = "Unassigned"
            });
            db.WorkOrders.Add(new WorkOrder
            {
                TenantId = tenantId,
                ClientId = client.Id,
                WorkOrderNumber = "WO-002",
                Title = "Done job",
                Priority = "Low",
                Status = "Completed",
                WorkType = WorkOrderTypes.CorrectiveMaintenance,
                AssignmentType = "Unassigned"
            });
        });

        var tenantContext = new FakeTenantContext(tenantId, Guid.NewGuid());
        await using var dbContext = CreateDbContext(databaseName, tenantContext);
        var controller = new ClientsController(dbContext, tenantContext, new AllowAllUserAccessService(), new NoOpAuditLogService());

        var result = await controller.ValidateDeletion(clientId, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var check = Assert.IsType<ClientDeletionCheckResponse>(ok.Value);

        Assert.False(check.CanDelete);
        Assert.Equal(2, check.WorkOrderCount);
        Assert.Equal(0, check.InvoiceCount);
        Assert.Contains("2 work orders", check.BlockingReason);
    }

    [Fact]
    public async Task DeletePermanent_OtherTenantClientIsNotFound()
    {
        var databaseName = $"ecosys-client-delete-{Guid.NewGuid():N}";
        var otherTenantId = Guid.NewGuid();
        var clientId = await SeedClientAsync(databaseName, otherTenantId);

        var tenantContext = new FakeTenantContext(Guid.NewGuid(), Guid.NewGuid());
        await using var dbContext = CreateDbContext(databaseName, tenantContext);
        var controller = new ClientsController(dbContext, tenantContext, new AllowAllUserAccessService(), new NoOpAuditLogService());

        await Assert.ThrowsAsync<NotFoundException>(
            () => controller.DeletePermanent(clientId, CancellationToken.None));
    }

    private static async Task<Guid> SeedClientAsync(
        string databaseName,
        Guid tenantId,
        Action<AppDbContext, Client>? seed = null)
    {
        await using var dbContext = CreateDbContext(databaseName);

        var client = new Client { TenantId = tenantId, ClientName = "Acme Ltd", IsActive = true };
        dbContext.Clients.Add(client);
        seed?.Invoke(dbContext, client);

        await dbContext.SaveChangesAsync();
        return client.Id;
    }

    private static AppDbContext CreateDbContext(string databaseName, ITenantContext? tenantContext = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new AppDbContext(options, tenantContext);
    }

    private sealed class FakeTenantContext(Guid tenantId, Guid userId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
        public Guid? SessionId => Guid.NewGuid();
        public string? Email => "test@acme.test";
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

    private sealed class AllowAllUserAccessService : IUserAccessService
    {
        public void EnsureAdmin() { }
        public void EnsureAdminOrPermission(string permissionName) { }
        public void EnsureAdminOrAnyPermission(params string[] permissionNames) { }
        public void EnsureTenantOperationalAccess() { }
    }

    private sealed class NoOpAuditLogService : IAuditLogService
    {
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, string severity = "Info", string? actorName = null, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
