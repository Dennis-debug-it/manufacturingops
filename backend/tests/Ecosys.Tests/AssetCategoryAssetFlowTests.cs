using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Domain.Enums;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

public sealed class AssetCategoryAssetFlowTests
{
    [Fact]
    public async Task AssetCategories_List_SeedsDefaultCategories_ForNewTenant()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(dbContext, tenantId);

        var controller = CreateAssetCategoriesController(dbContext, tenantId, Guid.NewGuid());

        var result = await controller.List(false, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var categories = Assert.IsAssignableFrom<IEnumerable<object>>(ok.Value);
        Assert.True(categories.Count() >= 9);
        Assert.Contains(categories, item => item.ToString()!.Contains("Generator", StringComparison.Ordinal));
        Assert.Contains(categories, item => item.ToString()!.Contains("Other Equipment", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Asset_WithCategoryAndCustomFields_CanBeCreatedAndLoaded()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var category = new AssetCategory
        {
            TenantId = fixture.Tenant.Id,
            Name = "Generator",
            Icon = "tool",
            IsDefault = true,
            IsActive = true,
            DisplayOrder = 1,
            Fields =
            [
                new AssetCategoryField
                {
                    FieldName = "capacity_kva",
                    FieldLabel = "Capacity KVA",
                    FieldType = "Number",
                    IsRequired = true,
                    DisplayOrder = 1
                },
                new AssetCategoryField
                {
                    FieldName = "fuel_type",
                    FieldLabel = "Fuel Type",
                    FieldType = "Dropdown",
                    DropdownOptions = "Diesel,Petrol",
                    IsRequired = false,
                    DisplayOrder = 2
                }
            ]
        };
        dbContext.AssetCategories.Add(category);
        await dbContext.SaveChangesAsync();

        var assetsController = CreateAssetsController(dbContext, fixture.Tenant.Id, fixture.User.Id);
        var createResult = await assetsController.Create(
            new UpsertAssetRequest(
                fixture.Client.Id,
                null,
                fixture.Site.Id,
                null,
                category.Id,
                "Generator 500kVA",
                "GEN-500",
                "Generator",
                "Owned",
                "Generator room",
                "SER-500",
                "Caterpillar",
                "CAT500",
                DateTime.UtcNow.Date,
                null,
                "Monthly",
                1,
                "months",
                true,
                null,
                DateTime.UtcNow.Date.AddMonths(1),
                "Primary backup generator",
                "Active",
                [
                    new UpsertAssetCustomFieldValueRequest(category.Fields.First().Id, "500"),
                    new UpsertAssetCustomFieldValueRequest(category.Fields.Last().Id, "Diesel")
                ]),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(createResult.Result);
        var payload = Assert.IsType<AssetResponse>(created.Value);
        Assert.Equal(category.Id, payload.AssetCategoryId);
        Assert.Equal("Generator", payload.AssetCategoryName);
        Assert.Equal(2, payload.CustomFieldValues.Count);

        var getResult = await assetsController.Get(payload.Id, CancellationToken.None);
        var getOk = Assert.IsType<OkObjectResult>(getResult.Result);
        var loaded = Assert.IsType<AssetResponse>(getOk.Value);
        Assert.Equal("500", loaded.CustomFieldValues.Single(x => x.FieldName == "capacity_kva").Value);
        Assert.Equal("Diesel", loaded.CustomFieldValues.Single(x => x.FieldName == "fuel_type").Value);
    }

    [Fact]
    public async Task Assets_List_CanFilterByCategory()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);

        var generator = new AssetCategory
        {
            TenantId = fixture.Tenant.Id,
            Name = "Generator",
            Icon = "tool",
            IsDefault = true,
            IsActive = true,
            DisplayOrder = 1
        };
        var hvac = new AssetCategory
        {
            TenantId = fixture.Tenant.Id,
            Name = "HVAC",
            Icon = "tool",
            IsDefault = true,
            IsActive = true,
            DisplayOrder = 2
        };
        dbContext.AssetCategories.AddRange(generator, hvac);
        await dbContext.SaveChangesAsync();

        dbContext.Assets.AddRange(
            new Asset
            {
                TenantId = fixture.Tenant.Id,
                ClientId = fixture.Client.Id,
                SiteId = fixture.Site.Id,
                AssetCategoryId = generator.Id,
                AssetName = "Generator 01",
                AssetCode = "GEN-01",
                AssetType = "Generator",
                Status = "Active"
            },
            new Asset
            {
                TenantId = fixture.Tenant.Id,
                ClientId = fixture.Client.Id,
                SiteId = fixture.Site.Id,
                AssetCategoryId = hvac.Id,
                AssetName = "Chiller 01",
                AssetCode = "HVC-01",
                AssetType = "HVAC",
                Status = "Active"
            });
        await dbContext.SaveChangesAsync();

        var assetsController = CreateAssetsController(dbContext, fixture.Tenant.Id, fixture.User.Id);

        var result = await assetsController.GetAll(null, null, null, null, generator.Id, null, "all", null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var assets = Assert.IsAssignableFrom<IReadOnlyCollection<AssetResponse>>(ok.Value);
        var asset = Assert.Single(assets);
        Assert.Equal("Generator 01", asset.AssetName);
        Assert.Equal("Generator", asset.AssetCategoryName);
    }

    [Fact]
    public async Task Asset_WithoutCode_SkipsGeneratedCodesThatAreAlreadyTaken()
    {
        // The numbering counter does not know about codes typed by hand or imported, so its next
        // value can already exist; that used to fail the insert on the unique (tenant, code) index.
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        dbContext.Assets.Add(new Asset { TenantId = fixture.Tenant.Id, ClientId = fixture.Client.Id, AssetName = "Imported", AssetCode = "ast-000001", Status = "Active" });
        await dbContext.SaveChangesAsync();

        var assetsController = CreateAssetsController(dbContext, fixture.Tenant.Id, fixture.User.Id);
        var createResult = await assetsController.Create(
            new UpsertAssetRequest(
                fixture.Client.Id, null, fixture.Site.Id, null, null,
                "Chiller", null, "HVAC", "Owned", null, null, null, null,
                null, null, null, null, null, false, null, null, null, "Active", null),
            CancellationToken.None);

        var payload = Assert.IsType<AssetResponse>(Assert.IsType<CreatedAtActionResult>(createResult.Result).Value);
        Assert.Equal("AST-000002", payload.AssetCode);
    }

    [Fact]
    public async Task Asset_WithWarrantyPeriodAndPmFrequency_DerivesExpiryAndNextPmDate()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var installed = DateTime.UtcNow.Date.AddMonths(-3);
        var lastPm = DateTime.UtcNow.Date.AddDays(-10);

        var assetsController = CreateAssetsController(dbContext, fixture.Tenant.Id, fixture.User.Id);
        var createResult = await assetsController.Create(
            new UpsertAssetRequest(
                fixture.Client.Id, null, fixture.Site.Id, null, null,
                "Chiller", "CH-01", "HVAC", "Owned", null, null, null, null,
                installed, null, null, 3, "months", false, lastPm, null, null, "Active", null,
                WarrantyPeriodMonths: 24),
            CancellationToken.None);

        var payload = Assert.IsType<AssetResponse>(Assert.IsType<CreatedAtActionResult>(createResult.Result).Value);
        Assert.Equal(installed, payload.WarrantyStartDate);
        Assert.Equal(24, payload.WarrantyPeriodMonths);
        Assert.Equal(installed.AddMonths(24), payload.WarrantyExpiryDate);
        Assert.True(payload.IsUnderWarranty);
        Assert.Equal("Under Warranty", payload.WarrantyStatus);
        Assert.Equal("3 months", payload.RecommendedPmFrequency);
        Assert.Equal(lastPm.AddMonths(3), payload.NextPmDate);
    }

    [Fact]
    public async Task Asset_InstalledLongAgoWithNoPmHistory_NextPmIsNotOverdue()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var installed = DateTime.UtcNow.Date.AddYears(-5).AddDays(10);

        var assetsController = CreateAssetsController(dbContext, fixture.Tenant.Id, fixture.User.Id);
        var createResult = await assetsController.Create(
            new UpsertAssetRequest(
                fixture.Client.Id, null, fixture.Site.Id, null, null,
                "Old Chiller", "CH-OLD", null, null, null, null, null, null,
                installed, null, null, 3, "months", true, null, null, null, "Active", null),
            CancellationToken.None);

        var payload = Assert.IsType<AssetResponse>(Assert.IsType<CreatedAtActionResult>(createResult.Result).Value);
        Assert.NotNull(payload.NextPmDate);
        Assert.True(payload.NextPmDate!.Value.Date >= DateTime.UtcNow.Date);
        Assert.True(payload.NextPmDate.Value.Date < DateTime.UtcNow.Date.AddMonths(3).AddDays(1));
        // Still on the installation-date cadence: a whole number of quarters after installation.
        var months = (payload.NextPmDate.Value.Year - installed.Year) * 12 + payload.NextPmDate.Value.Month - installed.Month;
        Assert.Equal(0, months % 3);
    }

    [Theory]
    [InlineData("Monthly", 1, "months")]
    [InlineData("Quarterly", 1, "quarters")]
    [InlineData("Semi-Annual", 6, "months")]
    [InlineData("annually", 1, "years")]
    [InlineData("Every 2 weeks", 2, "weeks")]
    [InlineData("3 months", 3, "months")]
    public async Task Asset_WithTextOnlyFrequency_IsConvertedToValueAndUnit(string label, int expectedValue, string expectedUnit)
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var lastPm = DateTime.UtcNow.Date.AddDays(-5);

        var assetsController = CreateAssetsController(dbContext, fixture.Tenant.Id, fixture.User.Id);
        var createResult = await assetsController.Create(
            new UpsertAssetRequest(
                fixture.Client.Id, null, fixture.Site.Id, null, null,
                "Pump", $"PMP-{Guid.NewGuid():N}"[..12], null, null, null, null, null, null,
                null, null, label, null, null, false, lastPm, null, null, "Active", null),
            CancellationToken.None);

        var payload = Assert.IsType<AssetResponse>(Assert.IsType<CreatedAtActionResult>(createResult.Result).Value);
        Assert.Equal(expectedValue, payload.PmFrequencyValue);
        Assert.Equal(expectedUnit, payload.PmFrequencyUnit);
        Assert.NotNull(payload.NextPmDate);
    }

    [Fact]
    public void PmFrequencyLabelParser_RejectsUnrecognisedText()
    {
        Assert.False(Ecosys.Api.Common.PmFrequencyLabelParser.TryParse("when it breaks", out _, out _));
        Assert.False(Ecosys.Api.Common.PmFrequencyLabelParser.TryParse("0 months", out _, out _));
        Assert.False(Ecosys.Api.Common.PmFrequencyLabelParser.TryParse(null, out _, out _));
    }

    [Fact]
    public async Task Asset_Retire_ThenReactivate_KeepsRecordAndStopsAutoPm()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var assetsController = CreateAssetsController(dbContext, fixture.Tenant.Id, fixture.User.Id);
        var created = Assert.IsType<AssetResponse>(Assert.IsType<CreatedAtActionResult>((await assetsController.Create(
            new UpsertAssetRequest(
                fixture.Client.Id, null, fixture.Site.Id, null, null,
                "Old UPS", "UPS-OLD", null, null, null, null, null, null,
                null, null, null, 6, "months", true, DateTime.UtcNow.Date.AddMonths(-1), null, null, "Active", null),
            CancellationToken.None)).Result).Value);

        var retireResult = await assetsController.Retire(created.Id, new RetireAssetRequest(null, "  End of life, replaced by UPS-NEW "), CancellationToken.None);
        var retired = Assert.IsType<AssetResponse>(Assert.IsType<OkObjectResult>(retireResult.Result).Value);
        Assert.Equal("Inactive", retired.Status);
        Assert.Equal("Retired", retired.SupportStatus);
        Assert.Equal("End of life, replaced by UPS-NEW", retired.DecommissionReason);
        Assert.Equal<DateTime?>(DateTime.UtcNow.Date, retired.DecommissionDate);
        Assert.False(retired.AutoSchedulePm);

        // Retired assets drop out of the default (active) list but can still be opened.
        var active = Assert.IsAssignableFrom<IReadOnlyCollection<AssetResponse>>(Assert.IsType<OkObjectResult>(
            await assetsController.GetAll(null, null, null, null, null, null, null, null, null, CancellationToken.None)).Value);
        Assert.DoesNotContain(active, x => x.Id == created.Id);
        Assert.IsType<OkObjectResult>((await assetsController.Get(created.Id, CancellationToken.None)).Result);

        await Assert.ThrowsAsync<Ecosys.Shared.Errors.BusinessRuleException>(() =>
            assetsController.Retire(created.Id, new RetireAssetRequest(null, "again"), CancellationToken.None));

        var reactivated = Assert.IsType<AssetResponse>(Assert.IsType<OkObjectResult>(
            (await assetsController.Reactivate(created.Id, CancellationToken.None)).Result).Value);
        Assert.Equal("Active", reactivated.Status);
        Assert.Null(reactivated.DecommissionDate);
        Assert.Null(reactivated.DecommissionReason);
    }

    [Fact]
    public async Task Asset_Retire_RequiresReason()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var asset = new Asset { TenantId = fixture.Tenant.Id, ClientId = fixture.Client.Id, AssetName = "Pump", AssetCode = "P-1", Status = "Active" };
        dbContext.Assets.Add(asset);
        await dbContext.SaveChangesAsync();

        var assetsController = CreateAssetsController(dbContext, fixture.Tenant.Id, fixture.User.Id);
        await Assert.ThrowsAsync<Ecosys.Shared.Errors.BusinessRuleException>(() =>
            assetsController.Retire(asset.Id, new RetireAssetRequest(null, " "), CancellationToken.None));
    }

    [Fact]
    public async Task Assets_OutOfSupportFilter_ExcludesWarrantyAndContractCover_AndBulkRetireWorks()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var today = DateTime.UtcNow.Date;
        Asset NewAsset(string code, DateTime? warrantyExpiry) => new()
        {
            TenantId = fixture.Tenant.Id, ClientId = fixture.Client.Id, SiteId = fixture.Site.Id,
            AssetName = code, AssetCode = code, Status = "Active", WarrantyExpiryDate = warrantyExpiry
        };
        var underWarranty = NewAsset("A-WARRANTY", today.AddMonths(6));
        var underContract = NewAsset("A-CONTRACT", today.AddMonths(-6));
        var unsupported = NewAsset("A-NONE", null);
        var expiredWarranty = NewAsset("A-EXPIRED", today.AddDays(-1));
        dbContext.Assets.AddRange(underWarranty, underContract, unsupported, expiredWarranty);

        var contract = new Contract
        {
            TenantId = fixture.Tenant.Id, ClientId = fixture.Client.Id, ContractNumber = "CT-1", Name = "Support",
            Status = ContractStatus.Active, StartDate = today.AddMonths(-1), EndDate = today.AddMonths(11)
        };
        contract.Assets.Add(new ContractAsset { TenantId = fixture.Tenant.Id, ContractId = contract.Id, AssetId = underContract.Id });
        // An expired contract (past grace) gives no cover.
        var oldContract = new Contract
        {
            TenantId = fixture.Tenant.Id, ClientId = fixture.Client.Id, ContractNumber = "CT-0", Name = "Old",
            Status = ContractStatus.Expired, StartDate = today.AddYears(-2), EndDate = today.AddYears(-1)
        };
        oldContract.Assets.Add(new ContractAsset { TenantId = fixture.Tenant.Id, ContractId = oldContract.Id, AssetId = unsupported.Id });
        dbContext.Contracts.AddRange(contract, oldContract);
        await dbContext.SaveChangesAsync();

        var assetsController = CreateAssetsController(dbContext, fixture.Tenant.Id, fixture.User.Id);
        var outOfSupport = Assert.IsAssignableFrom<IReadOnlyCollection<AssetResponse>>(Assert.IsType<OkObjectResult>(
            await assetsController.GetAll(null, null, null, null, null, null, null, null, null, CancellationToken.None, "out")).Value);
        Assert.Equal(new[] { "A-EXPIRED", "A-NONE" }, outOfSupport.Select(x => x.AssetCode).OrderBy(x => x).ToArray());
        Assert.All(outOfSupport, x => Assert.Equal("Out of Support", x.SupportStatus));

        var all = Assert.IsAssignableFrom<IReadOnlyCollection<AssetResponse>>(Assert.IsType<OkObjectResult>(
            await assetsController.GetAll(null, null, null, null, null, null, null, null, null, CancellationToken.None)).Value);
        Assert.Equal("Under Contract", all.Single(x => x.AssetCode == "A-CONTRACT").SupportStatus);
        Assert.Equal("Under Warranty", all.Single(x => x.AssetCode == "A-WARRANTY").SupportStatus);

        var bulk = Assert.IsType<BulkRetireAssetsResponse>(Assert.IsType<OkObjectResult>((await assetsController.RetireMany(
            new BulkRetireAssetsRequest(outOfSupport.Select(x => x.Id).Append(Guid.NewGuid()).ToList(), null, "No longer under support"),
            CancellationToken.None)).Result).Value);
        Assert.Equal(2, bulk.Retired);
        Assert.Equal(1, bulk.Skipped);
        Assert.Equal(2, await dbContext.Assets.CountAsync(x => x.Status == "Inactive" && x.DecommissionReason == "No longer under support"));
    }

    [Fact]
    public async Task Asset_WithoutWarranty_ReportsNoWarranty()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);

        var assetsController = CreateAssetsController(dbContext, fixture.Tenant.Id, fixture.User.Id);
        var createResult = await assetsController.Create(
            new UpsertAssetRequest(
                fixture.Client.Id, null, fixture.Site.Id, null, null,
                "Pump", "PMP-01", null, null, null, null, null, null,
                DateTime.UtcNow.Date, null, null, null, null, false, null, null, null, "Active", null),
            CancellationToken.None);

        var payload = Assert.IsType<AssetResponse>(Assert.IsType<CreatedAtActionResult>(createResult.Result).Value);
        Assert.Null(payload.WarrantyStartDate);
        Assert.Null(payload.WarrantyExpiryDate);
        Assert.False(payload.IsUnderWarranty);
        Assert.Equal("No Warranty", payload.WarrantyStatus);
    }

    [Fact]
    public void UpsertAssetRequest_AssetCodeIsOptional()
    {
        // With nullable reference types on, MVC treats a non-nullable string as implicitly [Required],
        // so the asset form (which leaves the code blank to auto-generate it) got a 400 every time.
        var parameter = typeof(UpsertAssetRequest).GetConstructors().Single().GetParameters().Single(x => x.Name == "AssetCode");
        Assert.Equal(System.Reflection.NullabilityState.Nullable, new System.Reflection.NullabilityInfoContext().Create(parameter).WriteState);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-asset-category-tests-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static async Task SeedTenantAsync(AppDbContext dbContext, Guid tenantId)
    {
        dbContext.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Tenant",
            Slug = $"tenant-{Guid.NewGuid():N}".Substring(0, 12),
            CompanyName = "Tenant Company",
            Email = "tenant@example.com",
            Country = "Kenya",
            Status = "Active",
            LicenseStatus = "Active",
            PrimaryColor = "#0F4C81",
            SecondaryColor = "#F4B942",
            IsActive = true
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task<FixtureData> SeedFixtureAsync(AppDbContext dbContext)
    {
        var tenant = new Tenant
        {
            Name = "Acme",
            Slug = $"acme-{Guid.NewGuid():N}".Substring(0, 12),
            CompanyName = "Acme Manufacturing",
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
            FullName = "Alice Admin",
            Email = $"alice-{Guid.NewGuid():N}@acme.test",
            PasswordHash = "hash",
            Role = AppRoles.Admin,
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

        var site = new Site
        {
            TenantId = tenant.Id,
            ClientId = client.Id,
            SiteCode = $"SITE-{Guid.NewGuid():N}".Substring(0, 10),
            SiteName = "Main Plant",
            SiteType = "Factory",
            Status = "Active"
        };

        dbContext.AddRange(tenant, user, client, site);
        await dbContext.SaveChangesAsync();
        return new FixtureData(tenant, user, client, site);
    }

    private static AssetCategoriesController CreateAssetCategoriesController(AppDbContext dbContext, Guid tenantId, Guid userId) =>
        new(
            dbContext,
            new FakeTenantContext(tenantId, userId),
            new NoOpUserAccessService());

    private static AssetsController CreateAssetsController(AppDbContext dbContext, Guid tenantId, Guid userId)
    {
        var tenantContext = new FakeTenantContext(tenantId, userId);
        var orgScope = new Ecosys.Infrastructure.Services.Factory.OrgScopeService(dbContext, tenantContext);
        return new(
            dbContext,
            tenantContext,
            new NoOpPreventiveMaintenancePlanService(),
            new NoOpAuditLogService(),
            new NoOpUserAccessService(),
            new NoOpBranchAccessService(),
            new FakeDocumentNumberingService(),
            new NoOpLicenseGuardService(dbContext),
            orgScope,
            new Ecosys.Infrastructure.Services.Factory.AssetLifecycleService(dbContext, orgScope));
    }

    private sealed record FixtureData(Tenant Tenant, User User, Client Client, Site Site);

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

    private sealed class NoOpUserAccessService : IUserAccessService
    {
        public void EnsureAdmin() { }
        public void EnsureAdminOrPermission(string permissionName) { }
        public void EnsureAdminOrAnyPermission(params string[] permissionNames) { }
        public void EnsureTenantOperationalAccess() { }
    }

    private sealed class NoOpPreventiveMaintenancePlanService : IPreventiveMaintenancePlanService
    {
        public Task SyncForAssetAsync(Asset asset, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoOpAuditLogService : IAuditLogService
    {
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, string severity = "Info", string? actorName = null, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
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

    private sealed class FakeDocumentNumberingService : IDocumentNumberingService
    {
        private int _next;

        public Task<string> GenerateAsync(Guid tenantId, Guid? branchId, string documentType, CancellationToken cancellationToken = default) =>
            Task.FromResult($"AST-{Interlocked.Increment(ref _next):D6}");

        public Task<NumberingSetting> UpsertAsync(Guid tenantId, Guid? branchId, string documentType, string prefix, long nextNumber, int paddingLength, string resetFrequency, bool includeYear, bool includeMonth, bool isActive, CancellationToken cancellationToken = default, string? suffix = null, string yearFormat = "YYYY", string separator = "-", bool isLocked = false) =>
            throw new NotSupportedException();
    }

    private sealed class NoOpLicenseGuardService(AppDbContext dbContext) : ILicenseGuardService
    {
        public Task<TenantLicenseSnapshot> GetSnapshotAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LicenseUsageSnapshot> GetUsageAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PlatformLicenseUsageSnapshot>> GetPlatformUsageAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task EnsureTenantCanMutateAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureCanCreateUserAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureCanCreateBranchAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureFeatureEnabledAsync(Guid tenantId, string featureName, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public async Task<TenantLicense> GetOrCreateTenantLicenseAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            var existing = await dbContext.TenantLicenses.SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
            if (existing is not null)
            {
                return existing;
            }

            var license = new TenantLicense
            {
                TenantId = tenantId,
                LicensePlanId = Guid.NewGuid(),
                Status = "Trial",
                StartsAt = DateTime.UtcNow
            };

            dbContext.TenantLicenses.Add(license);
            await dbContext.SaveChangesAsync(cancellationToken);
            return license;
        }
    }
}
