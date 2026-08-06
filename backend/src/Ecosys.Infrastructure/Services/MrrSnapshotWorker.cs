using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services;

/// <summary>
/// Writes one MRR snapshot per UTC day (idempotent upsert) so the platform dashboard can chart MRR
/// and tenant-mix over time. Captures once shortly after startup, then every 24 hours.
/// </summary>
public sealed class MrrSnapshotWorker(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<MrrSnapshotWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        do
        {
            try
            {
                await CaptureAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "MRR snapshot capture failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task CaptureAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var today = DateTime.UtcNow.Date;

        var mrr = await db.TenantLicenses
            .Include(x => x.LicensePlan)
            .Where(x => x.Status == "Active" || x.Status == "Trial")
            .SumAsync(x => (decimal?)x.LicensePlan!.MonthlyPrice, cancellationToken) ?? 0m;
        var paidTenants = await db.TenantLicenses.CountAsync(x => x.Status == "Active", cancellationToken);
        var trialTenants = await db.TenantLicenses.CountAsync(x => x.Status == "Trial", cancellationToken);
        var activeTenants = await db.Tenants.CountAsync(x => x.Id != PlatformConstants.RootTenantId && x.IsActive, cancellationToken);

        var existing = await db.MrrSnapshots.FirstOrDefaultAsync(x => x.SnapshotDate == today, cancellationToken);
        if (existing is null)
        {
            db.MrrSnapshots.Add(new MrrSnapshot
            {
                SnapshotDate = today,
                Mrr = mrr,
                ActiveTenants = activeTenants,
                TrialTenants = trialTenants,
                PaidTenants = paidTenants,
            });
        }
        else
        {
            existing.Mrr = mrr;
            existing.ActiveTenants = activeTenants;
            existing.TrialTenants = trialTenants;
            existing.PaidTenants = paidTenants;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
