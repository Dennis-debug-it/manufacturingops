using Ecosys.Api.Controllers;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;

namespace Ecosys.Tests;

public sealed class SecurityHardeningTests
{
    [Fact]
    public async Task Swagger_IsNotExposedOutsideDevelopment()
    {
        await using var factory = CreateFactory();

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/swagger/index.html");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Startup_Fails_WhenJwtSigningKeyIsMissing()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var factory = CreateFactory(builder => builder.UseSetting("Jwt:SigningKey", string.Empty));
            using var _ = factory.CreateClient();
        });

        Assert.Contains("Jwt:SigningKey", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Startup_Fails_WhenPlatformAdminPasswordIsDefault()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var factory = CreateFactory(builder => builder.UseSetting("PlatformAdmin:Password", "SuperAdmin123!"));
            using var _ = factory.CreateClient();
        });

        Assert.Contains("PlatformAdmin:Password", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthController_HasRateLimitingPolicy()
    {
        var attribute = typeof(AuthController).GetCustomAttributes(typeof(EnableRateLimitingAttribute), inherit: false)
            .OfType<EnableRateLimitingAttribute>()
            .SingleOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal("AuthEndpoints", attribute!.PolicyName);
    }

    [Fact]
    public async Task PlatformBootstrap_DoesNotOverwriteExistingAdminPassword()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Tenants.Add(new Domain.Entities.Tenant
        {
            Id = PlatformConstants.RootTenantId,
            Name = "Ecosys Platform",
            Slug = "ecosys-platform",
            CompanyName = "Ecosys Platform",
            Email = "existing.owner@ecosys.test",
            ContactEmail = "existing.owner@ecosys.test",
            Country = "Kenya",
            Status = "Active",
            LicenseStatus = "Active",
            IsActive = true
        });

        var passwordHasher = new PasswordHasher<Domain.Entities.User>();
        var existingUser = new Domain.Entities.User
        {
            Id = PlatformConstants.SuperAdminUserId,
            TenantId = PlatformConstants.RootTenantId,
            FullName = "Existing Owner",
            Email = "existing.owner@ecosys.test",
            Role = AppRoles.SuperAdmin,
            IsActive = true,
            HasAllBranchAccess = true
        };
        existingUser.PasswordHash = passwordHasher.HashPassword(existingUser, "OriginalPass123!");
        existingUser.Permission = new Domain.Entities.UserPermission();
        dbContext.Users.Add(existingUser);
        await dbContext.SaveChangesAsync();

        var originalHash = existingUser.PasswordHash;
        var service = new PlatformBootstrapService(
            dbContext,
            passwordHasher,
            Options.Create(new PlatformAdminOptions
            {
                Email = "new.owner@ecosys.test",
                Password = "ValidPlatformAdmin123!",
                FullName = "New Owner"
            }));

        await service.EnsurePlatformAdminAsync();

        var refreshedUser = await dbContext.Users.SingleAsync(x => x.Id == PlatformConstants.SuperAdminUserId);
        Assert.Equal(originalHash, refreshedUser.PasswordHash);
        Assert.Equal("existing.owner@ecosys.test", refreshedUser.Email);
    }

    [Fact]
    public async Task DevSeed_ReturnsNotFoundOutsideDevelopment()
    {
        await using var dbContext = CreateDbContext();
        var tenant = new Domain.Entities.Tenant
        {
            Name = "Acme",
            Slug = $"acme-{Guid.NewGuid():N}".Substring(0, 12),
            CompanyName = "Acme Facilities",
            Email = "ops@acme.test",
            Country = "Kenya",
            Status = "Active",
            LicenseStatus = "Active"
        };
        var user = new Domain.Entities.User
        {
            TenantId = tenant.Id,
            FullName = "Alice Admin",
            Email = $"alice-{Guid.NewGuid():N}@acme.test",
            PasswordHash = "hash",
            Role = AppRoles.Admin,
            IsActive = true,
            HasAllBranchAccess = true
        };
        dbContext.AddRange(tenant, user);
        await dbContext.SaveChangesAsync();

        var controller = new DevController(
            dbContext,
            new FakeTenantContext(tenant.Id, user.Id),
            new FakeWebHostEnvironment("Production"),
            new NoOpDocumentNumberingService(),
            new NoOpPreventiveMaintenancePlanService(),
            new NoOpStockLedgerService(),
            new NoOpUserAccessService());

        var result = await controller.Seed(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-security-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static WebApplicationFactory<Program> CreateFactory(Action<IWebHostBuilder>? configureBuilder = null)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.ConfigureLogging(logging => logging.ClearProviders().AddConsole());
                builder.UseSetting("ConnectionStrings:DefaultConnection", Environment.GetEnvironmentVariable("ECOSYS_TEST_DB_CONNECTION")
                    ?? "Host=localhost;Port=5432;Database=ecosys_test;Username=postgres;Password=postgres");
                builder.UseSetting("Jwt:Issuer", "ecosys-tests");
                builder.UseSetting("Jwt:Audience", "ecosys-tests");
                builder.UseSetting("Jwt:SigningKey", "12345678901234567890123456789012");
                builder.UseSetting("PlatformAdmin:Email", "platform.owner@ecosys.test");
                builder.UseSetting("PlatformAdmin:Password", "ValidPlatformAdmin123!");
                builder.UseSetting("PlatformAdmin:FullName", "Platform Owner");
                builder.UseSetting("App:PublicUrl", "https://ecosys-tests.example.com");
                builder.UseSetting($"{AuthSecurityOptions.SectionName}:FailedLoginThreshold", "5");
                builder.UseSetting($"{AuthSecurityOptions.SectionName}:LockoutMinutes", "15");
                builder.UseSetting($"{AuthSecurityOptions.SectionName}:RateLimitPermitLimit", "20");
                builder.UseSetting($"{AuthSecurityOptions.SectionName}:RateLimitWindowSeconds", "60");
                builder.UseSetting($"{AuthSecurityOptions.SectionName}:RateLimitQueueLimit", "0");
                configureBuilder?.Invoke(builder);
            });
    }

    private sealed class FakeTenantContext(Guid tenantId, Guid userId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
        public Guid? SessionId => Guid.NewGuid();
        public string? Email => "admin@acme.test";
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

    private sealed class FakeWebHostEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Ecosys.Api";
        public IFileProvider WebRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = environmentName;
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private sealed class NoOpUserAccessService : IUserAccessService
    {
        public void EnsureAdmin() { }
        public void EnsureAdminOrPermission(string permissionName) { }
        public void EnsureAdminOrAnyPermission(params string[] permissionNames) { }
        public void EnsureTenantOperationalAccess() { }
    }

    private sealed class NoOpDocumentNumberingService : IDocumentNumberingService
    {
        public Task<string> GenerateAsync(Guid tenantId, Guid? branchId, string documentType, CancellationToken cancellationToken = default) =>
            Task.FromResult("DEV-0001");

        public Task<Domain.Entities.NumberingSetting> UpsertAsync(Guid tenantId, Guid? branchId, string documentType, string prefix, long nextNumber, int paddingLength, string resetFrequency, bool includeYear, bool includeMonth, bool isActive, CancellationToken cancellationToken = default, string? suffix = null, string yearFormat = "YYYY", string separator = "-", bool isLocked = false) =>
            Task.FromException<Domain.Entities.NumberingSetting>(new NotSupportedException());
    }

    private sealed class NoOpPreventiveMaintenancePlanService : IPreventiveMaintenancePlanService
    {
        public Task SyncForAssetAsync(Domain.Entities.Asset asset, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class NoOpStockLedgerService : IStockLedgerService
    {
        public Task<Domain.Entities.StockMovement> RecordReceiptAsync(Domain.Entities.MaterialItem material, Guid? branchId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber, Guid? workOrderId = null, Guid? materialRequestId = null, CancellationToken cancellationToken = default) =>
            Task.FromException<Domain.Entities.StockMovement>(new NotSupportedException());

        public Task<Domain.Entities.StockMovement> RecordAdjustmentAsync(Domain.Entities.MaterialItem material, Guid? branchId, decimal quantityChange, Guid? createdByUserId, string? reason, string? referenceNumber = null, Guid? workOrderId = null, Guid? materialRequestId = null, CancellationToken cancellationToken = default) =>
            Task.FromException<Domain.Entities.StockMovement>(new NotSupportedException());

        public Task<Domain.Entities.StockMovement> RecordIssueAsync(Domain.Entities.MaterialItem material, Guid? branchId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber = null, Guid? workOrderId = null, Guid? materialRequestId = null, bool allowNegativeStock = false, CancellationToken cancellationToken = default) =>
            Task.FromException<Domain.Entities.StockMovement>(new NotSupportedException());

        public Task<Domain.Entities.StockMovement> RecordReturnAsync(Domain.Entities.MaterialItem material, Guid? branchId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber = null, Guid? workOrderId = null, Guid? materialRequestId = null, CancellationToken cancellationToken = default) =>
            Task.FromException<Domain.Entities.StockMovement>(new NotSupportedException());

        public Task<Domain.Entities.StockMovement> RecordTransferOutAsync(Domain.Entities.MaterialItem material, Guid fromBranchId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber = null, CancellationToken cancellationToken = default) =>
            Task.FromException<Domain.Entities.StockMovement>(new NotSupportedException());

        public Task<Domain.Entities.StockMovement> RecordTransferInAsync(Domain.Entities.MaterialItem material, Guid toBranchId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber = null, CancellationToken cancellationToken = default) =>
            Task.FromException<Domain.Entities.StockMovement>(new NotSupportedException());

        public Task RecordVanLoadAsync(Domain.Entities.MaterialItem material, Guid fromBranchId, Guid vanId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber = null, CancellationToken cancellationToken = default) =>
            Task.FromException(new NotSupportedException());

        public Task RecordVanReturnAsync(Domain.Entities.MaterialItem material, Guid vanId, Guid toBranchId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber = null, CancellationToken cancellationToken = default) =>
            Task.FromException(new NotSupportedException());

        public Task<Domain.Entities.StockMovement> RecordVanConsumeAsync(Domain.Entities.MaterialItem material, Guid vanId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber = null, Guid? workOrderId = null, CancellationToken cancellationToken = default) =>
            Task.FromException<Domain.Entities.StockMovement>(new NotSupportedException());

        public Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default) =>
            operation(cancellationToken);
    }
}
