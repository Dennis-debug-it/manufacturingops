using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

/// <summary>The client portal shows which of the client's assets are still under warranty.</summary>
public sealed class PortalWarrantyTests
{
    private static readonly DateTime Today = new(2026, 10, 26);
    private readonly Guid tenantId = Guid.NewGuid();
    private readonly Guid clientId = Guid.NewGuid();

    [Theory]
    [InlineData(null, null, null)]
    [InlineData(-1, "Out of Warranty", null)]
    [InlineData(0, "Expiring Soon", 1)]          // cover runs to the end of the expiry date
    [InlineData(59, "Expiring Soon", 60)]
    [InlineData(60, "Under Warranty", 61)]
    [InlineData(400, "Under Warranty", 401)]
    public void StatusAndDaysLeftFollowTheExpiryDate(int? daysFromToday, string? status, int? daysLeft)
    {
        DateTime? expiry = daysFromToday is { } days ? Today.AddDays(days) : null;
        Assert.Equal(status, PortalWarranty.Status(expiry, Today));
        Assert.Equal(daysLeft, PortalWarranty.DaysLeft(expiry, Today));
    }

    [Fact]
    public async Task PortalListsOnlyThisClientsAssetsWithTheirWarrantyAndCountsTheCovered()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        Asset Asset(string name, int? expiresInDays, Guid? client = null) => new()
        {
            TenantId = tenantId, ClientId = client ?? clientId, AssetName = name, AssetCode = name, Status = "Active",
            WarrantyExpiryDate = expiresInDays is { } days ? DateTime.SpecifyKind(Today.AddDays(days), DateTimeKind.Utc) : null,
        };
        db.Assets.AddRange(
            Asset("Chiller", 300), Asset("Generator", 10), Asset("Pump", -5), Asset("Fan", null),
            Asset("Someone else's", 300, Guid.NewGuid()));
        await db.SaveChangesAsync();
        var controller = new PortalController(db, new Portal(tenantId, clientId), new NoNumbers(), new NoAudit());

        var assets = Assert.IsAssignableFrom<IReadOnlyCollection<PortalAssetListItem>>(
            Assert.IsType<OkObjectResult>((await controller.ListAssets(new FixedToday(), default)).Result).Value).ToDictionary(x => x.AssetName);

        Assert.Equal(4, assets.Count);
        Assert.Equal("Under Warranty", assets["Chiller"].WarrantyStatus);
        Assert.Equal(("Expiring Soon", 11), (assets["Generator"].WarrantyStatus, assets["Generator"].WarrantyDaysLeft));
        Assert.Equal("Out of Warranty", assets["Pump"].WarrantyStatus);
        Assert.Null(assets["Fan"].WarrantyStatus);

        var summary = Assert.IsType<PortalDashboardSummaryResponse>(
            Assert.IsType<OkObjectResult>((await controller.GetDashboardSummary(new FixedToday(), default)).Result).Value);
        Assert.Equal((4, 2), (summary.AssetCount, summary.AssetsUnderWarranty));
    }

    private sealed class FixedToday : ITenantTimeZoneService
    {
        public Task<string> GetPlatformTimeZoneIdAsync(CancellationToken cancellationToken = default) => Task.FromResult("Africa/Nairobi");
        public Task<string> GetTimeZoneIdAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult("Africa/Nairobi");
        public Task<TimeZoneInfo> GetTimeZoneAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(TimeZoneInfo.Utc);
        public Task<DateTime> ToTenantTimeAsync(DateTime utcValue, Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(utcValue);
        // Unspecified kind, as the real service returns it.
        public Task<DateTime> TenantTodayAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(Today);
        public Task<DateTime> TenantDayEndUtcAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> FormatForTenantAsync(DateTime utcValue, Guid tenantId, string format = "dd MMM yyyy 'at' HH:mm", CancellationToken cancellationToken = default) => Task.FromResult(utcValue.ToString(format));
        public string? NormalizeTimeZoneId(string? timeZoneId) => timeZoneId;
        public void Invalidate(Guid? tenantId = null) { }
    }
    private sealed class Portal(Guid tenantId, Guid clientId) : IPortalContext
    {
        public Guid? TenantId => tenantId;
        public Guid? PortalUserId => Guid.Empty;
        public Guid? ClientId => clientId;
        public string? Email => "client@test.local";
        public string? FullName => "Client";
        public bool IsAuthenticated => true;
        public Guid GetRequiredTenantId() => tenantId;
        public Guid GetRequiredPortalUserId() => Guid.Empty;
        public Guid GetRequiredClientId() => clientId;
    }
    private sealed class NoNumbers : IDocumentNumberingService
    {
        public Task<string> GenerateAsync(Guid tenantId, Guid? branchId, string documentType, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<NumberingSetting> UpsertAsync(Guid tenantId, Guid? branchId, string documentType, string prefix, long nextNumber, int paddingLength, string resetFrequency, bool includeYear, bool includeMonth, bool isActive, CancellationToken cancellationToken = default, string? suffix = null, string yearFormat = "YYYY", string separator = "-", bool isLocked = false) =>
            throw new NotSupportedException();
    }
    private sealed class NoAudit : IAuditLogService
    {
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, string severity = "Info", string? actorName = null, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
