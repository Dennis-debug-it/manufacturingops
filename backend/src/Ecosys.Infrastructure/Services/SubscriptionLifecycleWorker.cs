using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services;

/// <summary>
/// Daily sweep over paid (Active) tenant subscriptions: warns the tenant once an unpaid invoice
/// pushes them into their grace period, then suspends the subscription once the grace period is
/// exceeded. Trial transitions are handled separately by <see cref="TrialLifecycleWorker"/>;
/// reactivation and extension happen immediately when a payment is recorded, via
/// <see cref="IPlatformBillingLifecycleService.ApplyPaymentReceivedAsync"/>.
/// </summary>
public sealed class SubscriptionLifecycleWorker(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<SubscriptionLifecycleWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Subscription lifecycle worker started.");

        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));

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
            var billingLifecycle = scope.ServiceProvider.GetRequiredService<IPlatformBillingLifecycleService>();
            var now = DateTime.UtcNow;

            var activeLicenses = await dbContext.TenantLicenses
                .Include(x => x.Tenant)
                .Where(x => x.Status == LicenseStatuses.Active
                    && x.TenantId != PlatformConstants.RootTenantId
                    && x.ExpiresAt != null
                    && x.ExpiresAt <= now)
                .ToListAsync(cancellationToken);

            var warnedCount = 0;
            var suspendedCount = 0;

            foreach (var license in activeLicenses)
            {
                var graceEndsAt = license.ExpiresAt!.Value.AddDays(license.GracePeriodDays);

                if (now > graceEndsAt)
                {
                    license.Status = LicenseStatuses.Suspended;
                    license.SuspendedAt = now;
                    if (license.Tenant is not null)
                    {
                        license.Tenant.LicenseStatus = LicenseStatuses.Suspended;
                    }

                    suspendedCount++;
                    logger.LogInformation(
                        "Subscription suspended for tenant {TenantId}: grace period ended {GraceEndsAt}.",
                        license.TenantId, graceEndsAt);
                    await billingLifecycle.QueueSubscriptionSuspendedEmailAsync(license, cancellationToken);
                }
                else if (license.OverdueWarningSentAt is null)
                {
                    license.OverdueWarningSentAt = now;
                    warnedCount++;
                    logger.LogInformation(
                        "Grace-period warning sent for tenant {TenantId}: grace period ends {GraceEndsAt}.",
                        license.TenantId, graceEndsAt);
                    await billingLifecycle.QueueGracePeriodWarningEmailAsync(license, graceEndsAt, cancellationToken);
                }
            }

            if (warnedCount > 0 || suspendedCount > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation(
                    "Subscription lifecycle cycle complete. Warned: {WarnedCount}, Suspended: {SuspendedCount}.",
                    warnedCount, suspendedCount);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Subscription lifecycle worker cycle failed.");
        }
    }
}
