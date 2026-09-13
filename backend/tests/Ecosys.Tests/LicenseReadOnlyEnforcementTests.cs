using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

/// <summary>
/// A tenant whose license has expired must not be able to write anything — only read. This is
/// enforced centrally by <see cref="TenantLicenseEnforcementMiddleware"/> calling
/// <see cref="ILicenseGuardService.EnsureTenantCanMutateAsync"/> on every non-GET/HEAD/OPTIONS
/// request, which in turn depends on <see cref="LicenseGuardService"/> correctly classifying a
/// license as read-only. These tests exercise that classification directly (the HTTP pipeline needs
/// a real database this sandbox doesn't have), since nothing previously asserted this behavior —
/// every other test file stubs <c>EnsureTenantCanMutateAsync</c> out as a no-op.
/// </summary>
public sealed class LicenseReadOnlyEnforcementTests
{
    [Fact]
    public async Task ActiveLicense_AllowsMutation()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = await SeedLicenseAsync(dbContext, status: LicenseStatuses.Active, expiresAt: DateTime.UtcNow.AddDays(30));
        var service = new LicenseGuardService(dbContext);

        var snapshot = await service.GetSnapshotAsync(tenantId);
        Assert.False(snapshot.IsReadOnly);

        await service.EnsureTenantCanMutateAsync(tenantId);
    }

    [Fact]
    public async Task ExpiredLicense_PastGracePeriod_BlocksMutationAsReadOnly()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = await SeedLicenseAsync(
            dbContext,
            status: LicenseStatuses.Active,
            expiresAt: DateTime.UtcNow.AddDays(-30),
            gracePeriodDays: 7);
        var service = new LicenseGuardService(dbContext);

        var snapshot = await service.GetSnapshotAsync(tenantId);
        Assert.True(snapshot.IsReadOnly);
        Assert.False(snapshot.IsSuspended);
        Assert.Equal(LicenseStatuses.Expired, snapshot.Status);

        var error = await Assert.ThrowsAsync<LicenseException>(() => service.EnsureTenantCanMutateAsync(tenantId));
        Assert.Equal("license_read_only", error.ErrorCode);
        Assert.Equal(403, error.StatusCode);
    }

    [Fact]
    public async Task ExplicitlyExpiredStatus_BlocksMutation_EvenBeforeExpiresAtDate()
    {
        // The Status column itself is authoritative, independent of ExpiresAt — an operator who
        // manually marks a license Expired shouldn't need to also backdate the expiry date.
        await using var dbContext = CreateDbContext();
        var tenantId = await SeedLicenseAsync(
            dbContext,
            status: LicenseStatuses.Expired,
            expiresAt: DateTime.UtcNow.AddDays(30),
            gracePeriodDays: 0);
        var service = new LicenseGuardService(dbContext);

        var snapshot = await service.GetSnapshotAsync(tenantId);
        Assert.True(snapshot.IsReadOnly);

        await Assert.ThrowsAsync<LicenseException>(() => service.EnsureTenantCanMutateAsync(tenantId));
    }

    [Fact]
    public async Task TrialExpiredStatus_BlocksMutation()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = await SeedLicenseAsync(
            dbContext,
            status: LicenseStatuses.TrialExpired,
            trialEndsAt: DateTime.UtcNow.AddDays(-1),
            gracePeriodDays: 0);
        var service = new LicenseGuardService(dbContext);

        var snapshot = await service.GetSnapshotAsync(tenantId);
        Assert.True(snapshot.IsReadOnly);

        await Assert.ThrowsAsync<LicenseException>(() => service.EnsureTenantCanMutateAsync(tenantId));
    }

    [Fact]
    public async Task ExpiredButWithinGracePeriod_StillAllowsMutation()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = await SeedLicenseAsync(
            dbContext,
            status: LicenseStatuses.Active,
            expiresAt: DateTime.UtcNow.AddDays(-1),
            gracePeriodDays: 7);
        var service = new LicenseGuardService(dbContext);

        var snapshot = await service.GetSnapshotAsync(tenantId);
        Assert.False(snapshot.IsReadOnly);
        Assert.True(snapshot.IsGracePeriod);

        await service.EnsureTenantCanMutateAsync(tenantId);
    }

    [Fact]
    public async Task SuspendedLicense_BlocksMutation_WithSuspendedNotReadOnlyError()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = await SeedLicenseAsync(dbContext, status: LicenseStatuses.Suspended, expiresAt: DateTime.UtcNow.AddDays(30));
        var service = new LicenseGuardService(dbContext);

        var snapshot = await service.GetSnapshotAsync(tenantId);
        Assert.True(snapshot.IsSuspended);

        var error = await Assert.ThrowsAsync<LicenseException>(() => service.EnsureTenantCanMutateAsync(tenantId));
        Assert.Equal("license_suspended", error.ErrorCode);
    }

    [Fact]
    public async Task CancelledLicense_IsAlwaysReadOnly_RegardlessOfExpiryDate()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = await SeedLicenseAsync(dbContext, status: LicenseStatuses.Cancelled, expiresAt: DateTime.UtcNow.AddDays(30));
        var service = new LicenseGuardService(dbContext);

        var snapshot = await service.GetSnapshotAsync(tenantId);
        Assert.True(snapshot.IsReadOnly);

        await Assert.ThrowsAsync<LicenseException>(() => service.EnsureTenantCanMutateAsync(tenantId));
    }

    [Fact]
    public async Task ExpiredLicense_AlsoBlocksLimitedCreateOperations()
    {
        // EnsureCanCreateUserAsync (and the other create-limit checks) route through
        // EnsureTenantCanMutateAsync first, so an expired tenant can't create a user even if it
        // hasn't hit its seat limit.
        await using var dbContext = CreateDbContext();
        var tenantId = await SeedLicenseAsync(
            dbContext,
            status: LicenseStatuses.Active,
            expiresAt: DateTime.UtcNow.AddDays(-30),
            gracePeriodDays: 0);
        var service = new LicenseGuardService(dbContext);

        var error = await Assert.ThrowsAsync<LicenseException>(() => service.EnsureCanCreateUserAsync(tenantId));
        Assert.Equal("license_read_only", error.ErrorCode);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-license-readonly-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<Guid> SeedLicenseAsync(
        AppDbContext dbContext,
        string status,
        DateTime? expiresAt = null,
        DateTime? trialEndsAt = null,
        int gracePeriodDays = 7)
    {
        var tenantId = Guid.NewGuid();
        dbContext.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "License Test Co",
            Slug = $"license-test-{tenantId:N}",
            CompanyName = "License Test Co",
            Email = "ops@licensetest.test",
            ContactEmail = "ops@licensetest.test",
            Country = "Kenya",
            Status = "Active",
            LicenseStatus = status,
            PrimaryColor = "#0F4C81",
            SecondaryColor = "#F4B942",
            IsActive = true
        });

        var plan = new LicensePlan
        {
            PlanCode = "Suite",
            DisplayName = "Suite",
            MaxUsers = 50,
            MaxBranches = 10,
            MaxAssets = 500,
            MonthlyWorkOrders = 1000,
            IsActive = true
        };
        dbContext.LicensePlans.Add(plan);

        dbContext.TenantLicenses.Add(new TenantLicense
        {
            TenantId = tenantId,
            LicensePlan = plan,
            Status = status,
            StartsAt = DateTime.UtcNow.AddDays(-60),
            ExpiresAt = expiresAt,
            TrialEndsAt = trialEndsAt,
            GracePeriodDays = gracePeriodDays
        });

        await dbContext.SaveChangesAsync();
        return tenantId;
    }
}
