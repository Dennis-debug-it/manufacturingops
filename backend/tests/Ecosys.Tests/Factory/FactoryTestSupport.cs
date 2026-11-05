using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests.Factory;

/// <summary>Shared fixtures for the factory maintenance modules.</summary>
internal static class FactoryTestSupport
{
    public static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-factory-tests-{Guid.NewGuid():N}")
            .Options);

    public static async Task<(Tenant Tenant, User Admin)> SeedTenantAsync(AppDbContext dbContext)
    {
        var tenant = new Tenant
        {
            Name = "Kericho Tea",
            Slug = $"t-{Guid.NewGuid():N}"[..12],
            CompanyName = "Kericho Tea Factories",
            Email = "ops@kericho.test",
            Country = "Kenya",
            Status = "Active",
            LicenseStatus = "Active",
            PrimaryColor = "#0F4C81",
            SecondaryColor = "#F4B942",
            IsActive = true
        };
        var admin = NewUser(tenant.Id, "admin@kericho.test", AppRoles.Admin);
        dbContext.Tenants.Add(tenant);
        dbContext.Users.Add(admin);
        await dbContext.SaveChangesAsync();
        return (tenant, admin);
    }

    public static User NewUser(Guid tenantId, string email, string role = AppRoles.User) => new()
    {
        TenantId = tenantId,
        FullName = email.Split('@')[0],
        Email = email,
        PasswordHash = "hash",
        Role = role,
        IsActive = true
    };

    public static OrgScopeService Scope(AppDbContext dbContext, ITenantContext context) => new(dbContext, context);

    public static FactorySettingsService Settings(AppDbContext dbContext) => new(dbContext);

    public static AuditLogService Audit(AppDbContext dbContext) => new(dbContext, new HttpContextAccessor());

    public static UserAccessService Access(ITenantContext context) => new(context);
}

internal sealed class FactoryTenantContext(Guid tenantId, Guid userId, bool isAdmin = true, params string[] permissions) : ITenantContext
{
    public Guid? TenantId => tenantId;
    public Guid? UserId => userId;
    public Guid? SessionId { get; } = Guid.NewGuid();
    public string? Email => "user@kericho.test";
    public string? Role => isAdmin ? AppRoles.Admin : AppRoles.User;
    public string? JobTitle => null;
    public bool IsAuthenticated => true;
    public bool IsSuperAdmin => false;
    public bool IsAdmin => isAdmin;
    public bool HasRole(string role) => string.Equals(role, Role, StringComparison.OrdinalIgnoreCase);
    public bool HasPermission(string permissionName) => isAdmin || permissions.Contains(permissionName);
    public Guid GetRequiredTenantId() => tenantId;
    public Guid GetRequiredUserId() => userId;
    public Guid GetRequiredSessionId() => SessionId!.Value;
}
