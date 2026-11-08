using System.Text;
using System.Text.Json;
using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
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

    /// <summary>Audit TM-P1-07: CSV import used to bypass licensed seats entirely.</summary>
    [Fact]
    public async Task CommitUsers_RejectsTheBatch_WhenActiveRowsExceedTheRemainingSeats()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        (await dbContext.TenantLicenses.SingleAsync(x => x.TenantId == fixture.Tenant.Id)).OfficeSeats = 1;
        await dbContext.SaveChangesAsync();
        var controller = CreateController(dbContext, fixture, new RecordingCredentialDeliveryService(), new QueueTemporaryPasswordService("TempPass!234", "TempPass!567"));

        var error = await Assert.ThrowsAsync<LicenseException>(() => controller.CommitUsers(
            new CsvUploadRequest(CreateCsvFile(
                """
                FullName,Email,Phone,Role,JobTitle,BranchName,IsTechnician,Status
                Alice Njeri,alice@example.com,+254700000001,TenantAdmin,Operations Manager,Head Office,false,Active
                Moses Otieno,moses@example.com,+254700000002,Technician,Technician,Head Office,true,Active
                """)),
            CancellationToken.None));

        Assert.Equal("license_office_seats_reached", error.ErrorCode);
        Assert.Equal(1, await dbContext.Users.CountAsync(x => x.TenantId == fixture.Tenant.Id));
    }

    /// <summary>Inactive rows don't consume seats, so they import even when the tenant is full.</summary>
    [Fact]
    public async Task CommitUsers_AllowsInactiveRows_WhenNoSeatsRemain()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var license = await dbContext.TenantLicenses.SingleAsync(x => x.TenantId == fixture.Tenant.Id);
        (license.OfficeSeats, license.FieldSeats) = (1, 0);
        await dbContext.SaveChangesAsync();
        var controller = CreateController(dbContext, fixture, new RecordingCredentialDeliveryService(), new QueueTemporaryPasswordService("TempPass!234"));

        var result = await controller.CommitUsers(
            new CsvUploadRequest(CreateCsvFile(
                """
                FullName,Email,Phone,Role,JobTitle,BranchName,IsTechnician,Status
                Alice Njeri,alice@example.com,+254700000001,TenantAdmin,Operations Manager,Head Office,false,Inactive
                """)),
            CancellationToken.None);

        var response = Assert.IsType<ImportCommitResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(1, response.SuccessfulRows);
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

    [Fact]
    public async Task CommitUsers_WithoutAdminAccess_IsRejected_AndCreatesNoUsers()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var deliveryService = new RecordingCredentialDeliveryService();
        var passwordService = new QueueTemporaryPasswordService("TempPass!234");
        var controller = CreateController(dbContext, fixture, deliveryService, passwordService, new DenyAdminUserAccessService());

        await Assert.ThrowsAsync<ForbiddenException>(() => controller.CommitUsers(
            new CsvUploadRequest(CreateCsvFile(
                """
                FullName,Email,Phone,Role,JobTitle,BranchName,IsTechnician,Status
                Mallory Kip,mallory@example.com,+254700000003,TenantAdmin,Operations Manager,Head Office,false,Active
                """)),
            CancellationToken.None));

        Assert.Empty(await dbContext.Users.Where(x => x.Id != fixture.Actor.Id).ToListAsync());
        Assert.Empty(deliveryService.Requests);
    }

    /// <summary>
    /// A bulk-imported opening quantity used to be written straight onto the part's total, so it had
    /// no branch balance and no ledger entry; it must land on the named branch as an initial receipt.
    /// </summary>
    [Fact]
    public async Task CommitMaterials_PostsOpeningQuantityToTheNamedBranchThroughTheLedger()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var controller = CreateController(dbContext, fixture, new RecordingCredentialDeliveryService(), new QueueTemporaryPasswordService());

        var result = await controller.CommitMaterials(
            new CsvUploadRequest(CreateCsvFile(
                """
                Material Name,Material Code,Category,Unit,Opening Quantity,Reorder Level,Unit Cost,Branch
                Generator Oil,MAT-001,Consumables,Litres,20,5,1200,Head Office
                Air Filter,MAT-002,Filters,Pieces,0,2,800,
                """)),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ImportCommitResponse>(ok.Value);
        Assert.Equal(2, response.SuccessfulRows);
        Assert.Equal(0, response.FailedRows);

        var oil = await dbContext.MaterialItems.Include(x => x.BranchStocks).SingleAsync(x => x.ItemCode == "MAT-001");
        Assert.Equal(20m, oil.QuantityOnHand);
        var branchStock = Assert.Single(oil.BranchStocks);
        Assert.Equal(fixture.Branch.Id, branchStock.BranchId);
        Assert.Equal(20m, branchStock.QuantityOnHand);

        var movement = await dbContext.StockMovements.SingleAsync(x => x.MaterialId == oil.Id);
        Assert.Equal(fixture.Branch.Id, movement.BranchId);
        Assert.Equal(20m, movement.Quantity);

        var filter = await dbContext.MaterialItems.SingleAsync(x => x.ItemCode == "MAT-002");
        Assert.Equal(0m, filter.QuantityOnHand);
        Assert.False(await dbContext.StockMovements.AnyAsync(x => x.MaterialId == filter.Id));
    }

    /// <summary>Re-uploading a stock list updates parts already in the catalogue instead of rejecting them.</summary>
    [Fact]
    public async Task CommitMaterials_UpdatesExistingPartsByCode_AndSetsTheirBranchStock()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var controller = CreateController(dbContext, fixture, new RecordingCredentialDeliveryService(), new QueueTemporaryPasswordService());
        const string header = "Material Name,Material Code,Category,Unit,Opening Quantity,Reorder Level,Unit Cost,Branch\n";
        await controller.CommitMaterials(new CsvUploadRequest(CreateCsvFile(header + "Generator Oil,MAT-001,Consumables,Litres,20,5,1200,Head Office\nBelt,MAT-002,Belts,Pieces,4,1,300,Head Office\n")), CancellationToken.None);

        var preview = Assert.IsType<ImportPreviewResponse>(Assert.IsType<OkObjectResult>((await controller.PreviewMaterials(
            new CsvUploadRequest(CreateCsvFile(header + "Generator Oil 15W-40,mat-001,Consumables,Litres,12,8,1350,Head Office\nBelt B-52,MAT-002,Belts,Pieces,,2,,Head Office\nNew Filter,MAT-003,Filters,Pieces,,,,\n")), CancellationToken.None)).Result).Value);
        Assert.Equal(["Update", "Update", "Create"], preview.Rows.Select(x => x.Action));
        Assert.False(preview.Rows.Last().IsValid); // a new part still needs its quantity

        var result = await controller.CommitMaterials(new CsvUploadRequest(CreateCsvFile(header + "Generator Oil 15W-40,mat-001,Consumables,Litres,12,8,1350,Head Office\nBelt B-52,MAT-002,Belts,Pieces,,2,,Head Office\n")), CancellationToken.None);
        var response = Assert.IsType<ImportCommitResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(2, response.SuccessfulRows);

        var oil = await dbContext.MaterialItems.Include(x => x.BranchStocks).SingleAsync(x => x.ItemCode == "MAT-001");
        Assert.Equal("Generator Oil 15W-40", oil.ItemName);
        Assert.Equal(1350m, oil.UnitCost);
        Assert.Equal(12m, Assert.Single(oil.BranchStocks).QuantityOnHand);
        var adjustment = await dbContext.StockMovements.SingleAsync(x => x.MaterialId == oil.Id && x.MovementType == "Adjustment");
        Assert.Equal(-8m, adjustment.QuantityChange);

        // A blank quantity leaves stock alone; a blank cost leaves the cost.
        var belt = await dbContext.MaterialItems.Include(x => x.BranchStocks).SingleAsync(x => x.ItemCode == "MAT-002");
        Assert.Equal("Belt B-52", belt.ItemName);
        Assert.Equal(4m, Assert.Single(belt.BranchStocks).QuantityOnHand);
        Assert.Equal(300m, belt.UnitCost);
        Assert.Equal(2, await dbContext.MaterialItems.CountAsync());
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

        var plan = new LicensePlan { PlanCode = $"suite-{Guid.NewGuid():N}", DisplayName = "Suite", IsActive = true };
        var license = new TenantLicense { TenantId = tenant.Id, LicensePlan = plan, Status = "Active", StartsAt = DateTime.UtcNow.AddMonths(-1), ExpiresAt = DateTime.UtcNow.AddYears(1) };

        dbContext.AddRange(tenant, branch, actor, plan, license);
        await dbContext.SaveChangesAsync();
        return new FixtureData(tenant, branch, actor);
    }

    private static ImportController CreateController(
        AppDbContext dbContext,
        FixtureData fixture,
        RecordingCredentialDeliveryService deliveryService,
        QueueTemporaryPasswordService passwordService,
        IUserAccessService? userAccessService = null) =>
        new(
            dbContext,
            new FakeTenantContext(fixture.Tenant.Id, fixture.Actor.Id, fixture.Actor.Email),
            userAccessService ?? new NoOpUserAccessService(),
            new NoOpPreventiveMaintenancePlanService(),
            new AuditLogService(dbContext, new HttpContextAccessor()),
            new FakePermissionTemplateService(),
            new PasswordHasher<User>(),
            deliveryService,
            passwordService,
            new FakeDocumentNumberingService(),
            new LicenseGuardService(dbContext),
            new StockLedgerService(dbContext),
            new PassThroughBranchAccessService());

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

    private sealed class PassThroughBranchAccessService : IBranchAccessService
    {
        public Task<BranchQueryScope> GetQueryScopeAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new BranchQueryScope(false, requestedBranchId, [], true));

        public Task<Guid?> ResolveBranchIdForWriteAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(requestedBranchId);

        public Task EnsureCanAccessBranchAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyCollection<Guid>> GetAccessibleBranchIdsAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Guid>>([]);
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

    private sealed class NoOpUserAccessService : IUserAccessService
    {
        public void EnsureAdmin() { }
        public void EnsureAdminOrPermission(string permissionName) { }
        public void EnsureAdminOrAnyPermission(params string[] permissionNames) { }
        public void EnsureTenantOperationalAccess() { }
    }

    // Stands in for a non-admin caller: the import endpoints are the only thing between a Viewer and
    // a self-minted TenantAdmin, so the guard itself is what the test below asserts.
    private sealed class DenyAdminUserAccessService : IUserAccessService
    {
        public void EnsureAdmin() => throw new ForbiddenException("Admin access is required for this action.");
        public void EnsureAdminOrPermission(string permissionName) => throw new ForbiddenException("You do not have permission to perform this action.");
        public void EnsureAdminOrAnyPermission(params string[] permissionNames) => throw new ForbiddenException("You do not have permission to perform this action.");
        public void EnsureTenantOperationalAccess() { }
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
                CanOverrideMaterialStock = true,
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
