using System.Reflection;
using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

/// <summary>
/// Locks in the settings-ownership cleanup: SMTP transport is global platform infrastructure with no
/// per-tenant write surface anywhere (not the tenant's own settings, not even Platform Admin editing a
/// specific tenant directly), and numbering resets are tenant-scoped, reason-audited, and refuse a
/// locked sequence.
/// </summary>
public sealed class SettingsOwnershipTests
{
    [Fact]
    public void SettingsController_NoLongerExposesAnyTenantFacingSmtpTransportEndpoint()
    {
        // Regression guard for the architecture correction: a tenant admin (even one impersonated by a
        // platform admin) must never be able to write SMTP host/port/credentials again.
        var actionNames = typeof(SettingsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(x => x.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("GetEmail", actionNames);
        Assert.DoesNotContain("UpdateEmail", actionNames);
        Assert.DoesNotContain("GetEmailNotifications", actionNames);
        Assert.DoesNotContain("UpdateEmailNotifications", actionNames);
        Assert.DoesNotContain("TestEmailNotifications", actionNames);
    }

    [Fact]
    public void PlatformTenantCommunicationSettingsController_CannotWriteATenantsSmtpTransport()
    {
        // Platform Admin's per-tenant view is read-only-plus-diagnostic: no action may persist SMTP
        // transport fields (host/port/credentials) for a specific tenant.
        var actionNames = typeof(PlatformTenantCommunicationSettingsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(x => x.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("UpdateEmailSettings", actionNames);
        Assert.DoesNotContain("UpdateCommunicationSettings", actionNames);
        Assert.DoesNotContain("ResetEmailSettingsToDefaults", actionNames);
    }

    [Fact]
    public async Task ResetNumbering_RejectsALockedSequence()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId);
        db.NumberingSettings.Add(new NumberingSetting { TenantId = tenantId, DocumentType = "WorkOrder", NextNumber = 42, IsLocked = true });
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            controller.ResetNumbering(tenantId, new ResetNumberingRequest("WorkOrder", 1, "Fat-fingered by support"), CancellationToken.None));

        var setting = await db.NumberingSettings.SingleAsync(x => x.TenantId == tenantId);
        Assert.Equal(42, setting.NextNumber);
    }

    [Fact]
    public async Task ResetNumbering_RecordsTheReasonInTheAuditLog()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId);
        db.NumberingSettings.Add(new NumberingSetting { TenantId = tenantId, DocumentType = "Invoice", NextNumber = 1 });
        await db.SaveChangesAsync();

        var audit = new RecordingAuditLogService();
        var controller = CreateController(db, audit);

        await controller.ResetNumbering(tenantId, new ResetNumberingRequest("Invoice", 1, "Duplicate invoice numbers reported by finance"), CancellationToken.None);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal("platform.support.numbering-reset", entry.Action);
        Assert.Contains("Duplicate invoice numbers reported by finance", entry.Details);
    }

    [Fact]
    public async Task ResetNumbering_OnlyAffectsTheTargetTenantsSequence()
    {
        // Two tenants can both have a "WorkOrder" sequence; resetting one must never touch the other's.
        await using var db = CreateDbContext();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await SeedTenantAsync(db, tenantA);
        await SeedTenantAsync(db, tenantB);
        db.NumberingSettings.Add(new NumberingSetting { TenantId = tenantA, DocumentType = "WorkOrder", NextNumber = 100 });
        db.NumberingSettings.Add(new NumberingSetting { TenantId = tenantB, DocumentType = "WorkOrder", NextNumber = 200 });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        await controller.ResetNumbering(tenantA, new ResetNumberingRequest("WorkOrder", 1, "Reset for tenant A only"), CancellationToken.None);

        Assert.Equal(1, (await db.NumberingSettings.SingleAsync(x => x.TenantId == tenantA)).NextNumber);
        Assert.Equal(200, (await db.NumberingSettings.SingleAsync(x => x.TenantId == tenantB)).NextNumber);
    }

    private static PlatformSupportToolsController CreateController(AppDbContext db, IAuditLogService? auditLogService = null) =>
        new(
            db,
            new EmailOutboxService(db, new EmailDeliveryLogService(db)),
            auditLogService ?? new RecordingAuditLogService(),
            new FakeTenantContext(PlatformConstants.RootTenantId, Guid.NewGuid(), isAdmin: true, isSuperAdmin: true));

    private static async Task SeedTenantAsync(AppDbContext db, Guid tenantId)
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Acme Co",
            Slug = $"acme-{tenantId:N}",
            CompanyName = "Acme Co",
            Email = "ops@acme.test",
            ContactEmail = "ops@acme.test",
            Country = "Kenya",
            Status = "Active",
            LicenseStatus = "Active",
            IsActive = true,
        });
        await db.SaveChangesAsync();
    }

    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-settings-ownership-{Guid.NewGuid():N}")
            .Options);

    private sealed class FakeTenantContext(Guid tenantId, Guid userId, bool isAdmin, bool isSuperAdmin) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
        public Guid? SessionId => Guid.NewGuid();
        public string? Email => "platform.admin@ecosys.test";
        public string? Role => isSuperAdmin ? AppRoles.SuperAdmin : AppRoles.Admin;
        public string? JobTitle => null;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => isSuperAdmin;
        public bool IsAdmin => isAdmin;
        public bool HasRole(string role) => string.Equals(Role, role, StringComparison.OrdinalIgnoreCase);
        public bool HasPermission(string permissionName) => true;
        public Guid GetRequiredTenantId() => tenantId;
        public Guid GetRequiredUserId() => userId;
        public Guid GetRequiredSessionId() => Guid.NewGuid();
    }

    private sealed class RecordingAuditLogService : IAuditLogService
    {
        public List<(string Action, string? Details)> Entries { get; } = [];

        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, CancellationToken cancellationToken = default)
        {
            Entries.Add((action, details));
            return Task.CompletedTask;
        }

        public Task LogAsync(
            Guid? tenantId,
            Guid? userId,
            string action,
            string entityName,
            string entityId,
            string? details,
            string severity = "Info",
            string? actorName = null,
            string? ipAddress = null,
            string? userAgent = null,
            CancellationToken cancellationToken = default)
        {
            Entries.Add((action, details));
            return Task.CompletedTask;
        }
    }
}
