// DEAD CODE — DO NOT USE.
// This file targets the OLD domain model (EcosysDbContext / Ecosys.ServiceOps.Entities)
// and is NOT registered in DI.
// The active SLA enforcer is: Ecosys.Infrastructure.Services.SlaEnforcementWorker
// Delete this file on Windows: del /f backend\src\Ecosys.Infrastructure\Hosted\SlaMonitorHostedService.cs
#if NEVER  // prevents compilation of obsolete code
using Ecosys.Infrastructure.Persistence;
using Ecosys.Platform.Contracts.Notifications;
using Ecosys.ServiceOps.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Hosted;

internal sealed class SlaMonitorHostedService(IServiceScopeFactory scopeFactory, ILogger<SlaMonitorHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<EcosysDbContext>();
                var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

                var now = DateTime.UtcNow;
                var workOrders = await dbContext.WorkOrders
                    .IgnoreQueryFilters()
                    .Where(x => x.Status != WorkOrderStatus.Closed && x.Status != WorkOrderStatus.Cancelled)
                    .ToListAsync(stoppingToken);

                foreach (var workOrder in workOrders)
                {
                    var previousStatus = workOrder.SlaStatus;
                    if (workOrder.ResolutionDueUtc.HasValue && workOrder.ResolutionDueUtc.Value < now)
                    {
                        workOrder.SlaStatus = SlaStatus.ResolutionBreached;
                    }
                    else if (workOrder.ResponseDueUtc.HasValue && workOrder.ResponseDueUtc.Value < now)
                    {
                        workOrder.SlaStatus = SlaStatus.ResponseBreached;
                    }
                    else
                    {
                        workOrder.SlaStatus = SlaStatus.WithinTarget;
                    }

                    if (previousStatus != workOrder.SlaStatus && workOrder.AssignedTechnicianId.HasValue)
                    {
                        await notifications.QueueAsync(
                            workOrder.TenantId,
                            workOrder.AssignedTechnicianId,
                            "SLA status changed",
                            $"Work order {workOrder.Number} is now {workOrder.SlaStatus}.",
                            stoppingToken);
                    }
                }

                await dbContext.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SLA monitor run failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
#endif // NEVER
