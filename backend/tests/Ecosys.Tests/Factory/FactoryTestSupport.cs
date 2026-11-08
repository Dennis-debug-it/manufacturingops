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

    public static FactoryWorkOrderService WorkOrders(AppDbContext dbContext, ITenantContext context)
    {
        var scope = Scope(dbContext, context);
        return new FactoryWorkOrderService(dbContext, new DocumentNumberingService(dbContext), Settings(dbContext), new AssetLifecycleService(dbContext, scope),
            scope, new FactorySlaService(dbContext), new FactoryPmProgress(dbContext), new FactoryJobGuards(dbContext));
    }

    public static ConditionMonitor Monitor(AppDbContext dbContext, ITenantContext context, INotificationDispatcher? dispatcher = null) =>
        new(dbContext, WorkOrders(dbContext, context), Microsoft.Extensions.Logging.Abstractions.NullLogger<ConditionMonitor>.Instance, dispatcher);
}

/// <summary>License checks always pass; licensing is not what these tests are about.</summary>
internal sealed class AllowAllLicenseGuard : ILicenseGuardService
{
    public Task<TenantLicenseSnapshot> GetSnapshotAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<LicenseUsageSnapshot> GetUsageAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyCollection<PlatformLicenseUsageSnapshot>> GetPlatformUsageAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<TenantLicense> GetOrCreateTenantLicenseAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task EnsureTenantCanMutateAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task EnsureCanCreateUserAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task EnsureCanCreateBranchAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task EnsureFeatureEnabledAsync(Guid tenantId, string featureName, CancellationToken cancellationToken = default) => Task.CompletedTask;
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

/// <summary>File storage kept in memory.</summary>
internal sealed class MemoryStorage : IFileStorageService
{
    private readonly Dictionary<string, byte[]> files = [];

    public async Task<UploadResult> UploadAsync(Stream stream, string fileName, string mimeType, Guid tenantId, CancellationToken ct)
    {
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy, ct);
        var path = $"{tenantId}/{Guid.NewGuid():N}/{fileName}";
        files[path] = copy.ToArray();
        return new UploadResult(path, path, copy.Length);
    }

    public Task<Stream> DownloadAsync(string storagePath, CancellationToken ct) => Task.FromResult<Stream>(new MemoryStream(files[storagePath]));
    public Task DeleteAsync(string storagePath, CancellationToken ct) => Task.FromResult(files.Remove(storagePath));
    public string GetPublicUrl(string storagePath) => storagePath;
}
