using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

/// <summary>Email intake comes with the Operations module; the old separate "EmailIngestion" switch is ignored.</summary>
public sealed class EmailIntakeLicensingTests
{
    [Theory]
    [InlineData(ServiceOpsPlans.Operations)]
    [InlineData(ServiceOpsPlans.Suite)]
    public async Task PlansWithOperations_IncludeEmailIntake_EvenWithTheOldSwitchOff(string planCode)
    {
        await using var db = CreateDbContext();
        var tenantId = await SeedAsync(db, planCode);
        var service = new LicenseGuardService(db);

        await service.EnsureFeatureEnabledAsync(tenantId, LicenseFeatures.EmailIngestion);
        Assert.True((await service.GetSnapshotAsync(tenantId)).EmailIngestion);
    }

    [Fact]
    public async Task FinanceOnlyPlan_DoesNotIncludeEmailIntake()
    {
        await using var db = CreateDbContext();
        var tenantId = await SeedAsync(db, ServiceOpsPlans.Finance);
        var service = new LicenseGuardService(db);

        var error = await Assert.ThrowsAsync<LicenseException>(() => service.EnsureFeatureEnabledAsync(tenantId, LicenseFeatures.EmailIngestion));
        Assert.Equal("license_module_operations", error.ErrorCode);
        Assert.False((await service.GetSnapshotAsync(tenantId)).EmailIngestion);
    }

    private static async Task<Guid> SeedAsync(AppDbContext db, string planCode)
    {
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId, Name = "Intake Co", Slug = $"intake-{tenantId:N}", CompanyName = "Intake Co",
            Email = "ops@intake.test", ContactEmail = "ops@intake.test", Country = "Kenya", Status = "Active",
            LicenseStatus = LicenseStatuses.Active, IsActive = true,
        });
        // The old switch is off on both the plan and the workspace; it must make no difference.
        var plan = new LicensePlan { PlanCode = planCode, DisplayName = planCode, IsActive = true, EmailIngestion = false };
        db.LicensePlans.Add(plan);
        db.TenantLicenses.Add(new TenantLicense
        {
            TenantId = tenantId, LicensePlan = plan, Status = LicenseStatuses.Active,
            StartsAt = DateTime.UtcNow.AddDays(-10), ExpiresAt = DateTime.UtcNow.AddDays(30), EmailIngestionOverride = false,
        });
        await db.SaveChangesAsync();
        return tenantId;
    }

    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"ecosys-intake-licensing-{Guid.NewGuid():N}").Options);
}
