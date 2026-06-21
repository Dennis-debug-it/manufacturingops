using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services;

public sealed class TrialLifecycleWorker(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<TrialLifecycleWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Trial lifecycle worker started.");

        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));

        // Run once immediately on startup, then every 24 hours
        await RunCycleAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCycleAsync(stoppingToken);
        }
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTime.UtcNow;
            var soonThreshold = now.AddDays(7);

            var trialLicenses = await dbContext.TenantLicenses
                .Include(x => x.Tenant)
                .Where(x => x.Status == LicenseStatuses.Trial ||
                            x.Status == LicenseStatuses.TrialExpiringSoon)
                .ToListAsync(cancellationToken);

            var expiredCount = 0;
            var expiringSoonCount = 0;

            foreach (var license in trialLicenses)
            {
                if (license.TrialEndsAt is null)
                    continue;

                var trialEndsAt = license.TrialEndsAt.Value;

                if (trialEndsAt < now)
                {
                    // Trial has expired
                    license.Status = LicenseStatuses.TrialExpired;
                    if (license.Tenant is not null)
                        license.Tenant.LicenseStatus = LicenseStatuses.TrialExpired;
                    expiredCount++;
                    logger.LogInformation(
                        "Trial expired for tenant {TenantId}. Setting status to TrialExpired.", license.TenantId);
                }
                else if (trialEndsAt <= soonThreshold &&
                         string.Equals(license.Status, LicenseStatuses.Trial, StringComparison.OrdinalIgnoreCase))
                {
                    // Trial expires within 7 days — warn
                    license.Status = LicenseStatuses.TrialExpiringSoon;
                    if (license.Tenant is not null)
                        license.Tenant.LicenseStatus = LicenseStatuses.TrialExpiringSoon;
                    expiringSoonCount++;
                    logger.LogInformation(
                        "Trial expiring soon for tenant {TenantId} (ends {TrialEndsAt}). Setting status to TrialExpiringSoon.",
                        license.TenantId, trialEndsAt);
                }
            }

            if (expiredCount > 0 || expiringSoonCount > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation(
                    "Trial lifecycle cycle complete. Expired: {ExpiredCount}, ExpiringSoon: {ExpiringSoonCount}.",
                    expiredCount, expiringSoonCount);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Trial lifecycle worker cycle failed.");
        }
    }
}
