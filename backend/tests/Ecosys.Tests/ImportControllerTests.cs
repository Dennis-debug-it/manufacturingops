using System.Text;
using System.Text.Json;
using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

public sealed class ImportControllerTests
{
    [Fact]
    public async Task CommitUsers_AssignsUniqueTemporaryPasswords_AndRequiresPasswordChange()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var deliveryService = new RecordingCredentialDeliveryService();
        var passwordService = new QueueTemporaryPasswordService("TempPass!234", "TempPass!567");
        var controller = CreateController(dbContext, fixture, deliveryService, passwordService);

        var result = await controller.CommitUsers(
            new CsvUploadRequest(CreateCsvFile(
                """
                FullName,Email,Phone,Role,JobTitle,BranchName,IsTechnician,Status
                Alice Njeri,alice@example.com,+254700000001,TenantAdmin,Operations Manager,Head Office,false,Active
                Moses Otieno,moses@example.com,+254700000002,Technician,Technician,Head Office,true,Active
                """)),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ImportCommitResponse>(ok.Value);
        Assert.Equal(2, response.SuccessfulRows);
        Assert.Equal(0, response.FailedRows);

        var users = await dbContext.Users
            .Where(x => x.Id != fixture.Actor.Id)
            .OrderBy(x => x.Email)
            .ToListAsync();
        Assert.Equal(2, users.Count);
        Assert.All(users, user => Assert.True(user.MustChangePassword));
        Assert.All(users, user => Assert.NotNull(user.LastCredentialSentAt));
        Assert.NotEqual(users[0].PasswordHash, users[1].PasswordHash);
        Assert.DoesNotContain(users, user => string.Equals(user.PasswordHash, "ChangeMe123!", StringComparison.Ordinal));

        Assert.Equal(2, deliveryService.Requests.Count);
        Assert.Equal(["TempPass!234", "TempPass!567"], deliveryService.Requests.Select(x => x.TemporaryPassword ?? string.Empty).ToArray());
        Assert.All(deliveryService.Requests, request => Assert.True(request.RequirePasswordChange));

        var technician = await dbContext.Technicians.SingleAsync();
        Assert.Equal("moses@example.com", technician.Email);
    }

    [Fact]
    public async Task CommitUsers_Response_DoesNotExposePasswords()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var deliveryService = new RecordingCredentialDeliveryService();
        var passwordService = new QueueTemporaryPasswordService("SecretOne!234");
        var controller = CreateController(dbContext, fixture, deliveryService, passwordService);

        var result = await controller.CommitUsers(
            new CsvUploadRequest(CreateCsvFile(
                """
                FullName,Email,Phone,Role,JobTitle,BranchName,IsTechnician,Status
                Alice Njeri,alice@example.com,+254700000001,TenantAdmin,Operations Manager,Head Office,false,Active
                """)),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ImportCommitResponse>(ok.Value);
        var json = JsonSerializer.Serialize(response);

        Assert.DoesNotContain("SecretOne!234", json, StringComparison.Ordinal);
        Assert.DoesNotContain("ChangeMe123!", json, StringComparison.Ordinal);
        Assert.Single(deliveryService.Requests);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-import-phase3-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<FixtureData> SeedFixtureAsync(AppDbContext dbContext)
    {
        var tenant = new Tenant
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
        var branch = new Branch
        {
            TenantId = tenant.Id,
            Name = "Head Office",
            Code = $"HO-{Guid.NewGuid():N}"[..8],
            IsActive = true
        };
        var actor = new User
        {
            TenantId = tenant.Id,
            FullName = "Import Admin",
            Email = $"import-admin-{Guid.NewGuid():N}@acme.test",
            PasswordHash = "hash",
            Role = AppRoles.Admin,
            IsActive = true,
            HasAllBranchAccess = true
        };

        dbContext.AddRange(tenant, branch, actor);
        await dbContext.SaveChangesAsync();
        return new FixtureData(tenant, branch, actor);
    }

    private static ImportController CreateController(
        AppDbContext dbContext,
        FixtureData fixture,
        RecordingCredentialDeliveryService deliveryService,
        QueueTemporaryPasswordService passwordService) =>
        new(
            dbContext,
            new FakeTenantContext(fixture.Tenant.Id, fixture.Actor.Id, fixture.Actor.Email),
            new NoOpPreventiveMaintenancePlanService(),
            new AuditLogService(dbContext, new HttpContextAccessor()),
            new FakePermissionTemplateService(),
            new PasswordHasher<User>(),
            deliveryService,
            passwordService,
            new FakeDocumentNumberingService());

    private static IFormFile CreateCsvFile(string csv)
    {
        var bytes = Encoding.UTF8.GetBytes(csv.Replace("\r", string.Empty));
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", "users.csv")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };
    }

    private sealed record FixtureData(Tenant Tenant, Branch Branch, User Actor);

    private sealed class FakeDocumentNumberingService : IDocumentNumberingService
    {
        private int _next;

        public Task<string> GenerateAsync(Guid tenantId, Guid? branchId, string documentType, CancellationToken cancellationToken = default) =>
            Task.FromResult($"{documentType.ToUpperInvariant()}-{Interlocked.Increment(ref _next):D6}");

        public Task<NumberingSetting> UpsertAsync(Guid tenantId, Guid? branchId, string documentType, string prefix, long nextNumber, int paddingLength, string resetFrequency, bool includeYear, bool includeMonth, bool isActive, CancellationToken cancellationToken = default, string? suffix = null, string yearFormat = "YYYY", string separator = "-", bool isLocked = false) =>
            Task.FromResult(new NumberingSetting { TenantId = tenantId, BranchId = branchId, DocumentType = documentType, Prefix = prefix, NextNumber = nextNumber, PaddingLength = paddingLength, ResetFrequency = resetFrequency, IncludeYear = includeYear, IncludeMonth = includeMonth, IsActive = isActive });
    }

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

    private sealed class NoOpPreventiveMaintenancePlanService : IPreventiveMaintenancePlanService
    {
        public Task SyncForAssetAsync(Asset asset, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakePermissionTemplateService : IUserPermissionTemplateService
    {
        public UserPermissionsModel GetDefaultPermissions(string role, string? jobTitle) =>
            string.Equals(role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase)
                ? GetFullTenantPermissions()
                : new UserPermissionsModel
                {
                    CanViewWorkOrders = true
                };

        public UserPermissionsModel GetFullTenantPermissions() =>
            new UserPermissionsModel
            {
                CanViewWorkOrders = true,
                CanCreateWorkOrders = true,
                CanAssignWorkOrders = true,
                CanCompleteWorkOrders = true,
                CanViewKnowledge = true,
                CanManageKnowledge = true,
                CanViewMaterials = true,
                CanRequestMaterials = true,
                CanManageMaterials = true,
                CanApproveMaterials = true,
                CanIssueMaterials = true,
                CanManageAssets = true,
                CanManageSettings = true,
                CanViewReports = true,
                CanViewFinance = true,
                CanManageFinance = true,
                CanViewFinanceRates = true,
                CanManageFinanceRates = true,
                CanViewFinanceApprovals = true,
                CanManageFinanceApprovals = true,
                CanViewBillableWork = true,
                CanManageBillableWork = true,
                CanOverrideMaterialStock = true
            };

        public UserPermissionsModel GetSuperAdminPermissions() =>
            new UserPermissionsModel();

        public UserPermissionsModel Merge(string role, string? jobTitle, UserPermissionsModel? customPermissions) =>
            customPermissions ?? GetDefaultPermissions(role, jobTitle);
    }

    private sealed class QueueTemporaryPasswordService(params string[] passwords) : ITemporaryPasswordService
    {
        private readonly Queue<string> _passwords = new(passwords);

        public string Generate(int minimumLength = 14) =>
            _passwords.Count > 0 ? _passwords.Dequeue() : $"Fallback!{minimumLength}234";
    }

    private sealed class RecordingCredentialDeliveryService : IUserCredentialDeliveryService
    {
        public List<UserCredentialDeliveryRequest> Requests { get; } = [];

        public string BuildLoginUrl() => "https://app.test/login";
        public string BuildInviteUrl(string token) => $"https://app.test/reset-password?token={token}&mode=invite";
        public string BuildResetPasswordUrl(string token) => $"https://app.test/reset-password?token={token}";

        public Task<UserCredentialDeliveryResult> SendAsync(Guid tenantId, User user, UserCredentialDeliveryRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(new UserCredentialDeliveryResult(true, "Sent", "User credentials sent."));
        }

        public Task<UserCredentialDeliveryResult> SendPasswordResetLinkAsync(Guid tenantId, User user, PasswordResetLinkDeliveryRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new UserCredentialDeliveryResult(true, "Sent", "Password reset email sent."));
    }
}
