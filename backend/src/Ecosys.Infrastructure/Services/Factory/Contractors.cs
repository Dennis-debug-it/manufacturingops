using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services.Factory;

public sealed record ContractorAlertRun(int DocumentAlerts, int RenewalAlerts, int VisitsRaised, int AgreementsExpired);

public interface IContractorService
{
    /// <summary>Why the contractor can't be given work now (not approved, required documents missing or expired); empty when they can.</summary>
    Task<IReadOnlyList<string>> WorkBlockersAsync(Contractor contractor, DateTime utcNow, CancellationToken cancellationToken = default);

    /// <summary>Gives a job to a contractor, under an agreement if one covers it; its response and repair times set the job's SLA.</summary>
    Task AssignAsync(WorkOrder workOrder, Contractor contractor, MaintenanceAgreement? agreement, FactoryActor actor, CancellationToken cancellationToken = default);

    /// <summary>Checks each invoice line against the contractor's completed jobs and sets Matched or Query with the issues.</summary>
    Task MatchAsync(ContractorInvoice invoice, CancellationToken cancellationToken = default);

    /// <summary>Document expiry alerts, renewal notices, expiring agreements and raising scheduled contractor visits.</summary>
    Task<ContractorAlertRun> RunAsync(Guid tenantId, DateTime utcNow, CancellationToken cancellationToken = default);
}

internal sealed class ContractorService(AppDbContext dbContext, IFactoryWorkOrderService workOrderService, INotificationDispatcher? notificationDispatcher = null) : IContractorService
{
    public const string DocumentKey = "contractor.document-expiring";
    public const string RenewalKey = "contractor.agreement-renewal";

    private static readonly string[] Done = [WorkOrderStatuses.Completed, WorkOrderStatuses.Verified, WorkOrderStatuses.Closed];

    public async Task<IReadOnlyList<string>> WorkBlockersAsync(Contractor contractor, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var blockers = new List<string>();
        if (!contractor.IsActive) blockers.Add($"{contractor.Name} is inactive.");
        if (contractor.Status != ContractorStatuses.Approved) blockers.Add($"{contractor.Name} is {contractor.Status.ToLowerInvariant()}, not approved.");
        var documents = await dbContext.ContractorDocuments.AsNoTracking().Where(x => x.ContractorId == contractor.Id).ToListAsync(cancellationToken);
        foreach (var type in ContractorDocumentTypes.Required)
        {
            var current = documents.Where(x => x.DocumentType == type).Any(x => x.ExpiresOn is null || x.ExpiresOn.Value.Date >= utcNow.Date);
            if (!current) blockers.Add(documents.Any(x => x.DocumentType == type) ? $"{contractor.Name}'s {type.ToLowerInvariant()} has expired." : $"{contractor.Name} has no {type.ToLowerInvariant()} on file.");
        }

        return blockers;
    }

    public async Task AssignAsync(WorkOrder workOrder, Contractor contractor, MaintenanceAgreement? agreement, FactoryActor actor, CancellationToken cancellationToken = default)
    {
        var blockers = await WorkBlockersAsync(contractor, DateTime.UtcNow, cancellationToken);
        if (blockers.Count > 0) throw new BusinessRuleException(string.Join(" ", blockers));
        if (agreement is not null)
        {
            if (agreement.ContractorId != contractor.Id) throw new BusinessRuleException("That agreement is with another contractor.");
            if (agreement.Status != AgreementStatuses.Active || agreement.EndDate.Date < DateTime.UtcNow.Date) throw new BusinessRuleException($"Agreement {agreement.AgreementNumber} is not in force.");
            var anchor = workOrder.ApprovedAt ?? workOrder.CreatedAt;
            if (agreement.ResponseHours is { } response) workOrder.SlaResponseDeadline = anchor.AddHours(response);
            if (agreement.RepairHours is { } repair) workOrder.SlaResolutionDeadline = anchor.AddHours(repair);
        }

        workOrder.ContractorId = contractor.Id;
        workOrder.MaintenanceAgreementId = agreement?.Id;
        dbContext.WorkOrderEvents.Add(new WorkOrderEvent
        {
            TenantId = workOrder.TenantId, WorkOrderId = workOrder.Id, EventType = "Contractor", OccurredAt = OccurredAt.UtcNow, ActorUserId = actor.UserId,
            Message = $"Given to {contractor.Name}" + (agreement is null ? "." : $" under {agreement.AgreementNumber}."),
            Status = workOrder.Status
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MatchAsync(ContractorInvoice invoice, CancellationToken cancellationToken = default)
    {
        var lines = await dbContext.ContractorInvoiceLines.Where(x => x.InvoiceId == invoice.Id).ToListAsync(cancellationToken);
        var jobIds = lines.Where(x => x.WorkOrderId != null).Select(x => x.WorkOrderId!.Value).ToList();
        var jobs = await dbContext.WorkOrders.AsNoTracking().Where(x => jobIds.Contains(x.Id))
            .Select(x => new { x.Id, x.WorkOrderNumber, x.ContractorId, x.Status }).ToDictionaryAsync(x => x.Id, cancellationToken);
        var billedElsewhere = await dbContext.ContractorInvoiceLines.AsNoTracking()
            .Where(x => x.InvoiceId != invoice.Id && x.WorkOrderId != null && jobIds.Contains(x.WorkOrderId.Value) && x.Invoice!.Status != ContractorInvoiceStatuses.Rejected)
            .Select(x => new { WorkOrderId = x.WorkOrderId!.Value, x.Invoice!.InvoiceNumber }).ToListAsync(cancellationToken);

        var issues = new List<string>();
        foreach (var line in lines)
        {
            if (line.WorkOrderId is not { } id)
            {
                issues.Add($"\"{line.Description}\" is not linked to a job.");
                continue;
            }

            if (!jobs.TryGetValue(id, out var job))
            {
                issues.Add($"\"{line.Description}\": job not found.");
                continue;
            }

            if (job.ContractorId != invoice.ContractorId) issues.Add($"{job.WorkOrderNumber} was not given to this contractor.");
            if (!Done.Contains(job.Status)) issues.Add($"{job.WorkOrderNumber} is not complete ({job.Status}).");
            if (billedElsewhere.FirstOrDefault(x => x.WorkOrderId == id) is { } other) issues.Add($"{job.WorkOrderNumber} is already billed on invoice {other.InvoiceNumber}.");
        }

        if (lines.Count == 0) issues.Add("The invoice has no lines.");
        var total = lines.Sum(x => x.Amount);
        if (total != invoice.Amount) issues.Add($"Lines add up to {total:N2}, not the invoice total {invoice.Amount:N2}.");
        if (lines.Where(x => x.WorkOrderId != null).GroupBy(x => x.WorkOrderId).FirstOrDefault(g => g.Count() > 1) is { } twice && jobs.TryGetValue(twice.Key!.Value, out var dup))
            issues.Add($"{dup.WorkOrderNumber} appears on more than one line.");

        invoice.Status = issues.Count == 0 ? ContractorInvoiceStatuses.Matched : ContractorInvoiceStatuses.Query;
        invoice.MatchIssues = issues.Count == 0 ? null : string.Join("\n", issues);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ContractorAlertRun> RunAsync(Guid tenantId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var today = utcNow.Date;
        var admins = await RecipientsAsync(tenantId, cancellationToken);

        // Documents: 30 and 7 days ahead, then once expired.
        var documents = await dbContext.ContractorDocuments.Include(x => x.Contractor)
            .Where(x => x.TenantId == tenantId && x.ExpiresOn != null && x.ExpiresOn <= today.AddDays(30) && x.Contractor!.IsActive).ToListAsync(cancellationToken);
        var dueDocuments = new List<ContractorDocument>();
        foreach (var document in documents)
        {
            var days = (int)(document.ExpiresOn!.Value.Date - today).TotalDays;
            var step = days < 0 ? 0 : days <= 7 ? 7 : 30;
            if (document.LastAlertDays is null || step < document.LastAlertDays)
            {
                document.LastAlertDays = step;
                dueDocuments.Add(document);
            }
        }

        // Agreements: notice before the end, then marked expired.
        var agreements = await dbContext.MaintenanceAgreements.Include(x => x.Contractor)
            .Where(x => x.TenantId == tenantId && x.Status == AgreementStatuses.Active).ToListAsync(cancellationToken);
        var renewals = agreements.Where(x => x.RenewalAlertSentAt is null && x.EndDate.Date <= today.AddDays(x.RenewalNoticeDays) && x.EndDate.Date >= today).ToList();
        foreach (var agreement in renewals) agreement.RenewalAlertSentAt = utcNow;
        var expired = agreements.Where(x => x.EndDate.Date < today).ToList();
        foreach (var agreement in expired) agreement.Status = AgreementStatuses.Expired;
        await dbContext.SaveChangesAsync(cancellationToken);

        // Scheduled visits: raise the job a week ahead, then move the next visit on.
        var visits = 0;
        foreach (var agreement in agreements.Where(x => x.Status == AgreementStatuses.Active && x.VisitEveryMonths is > 0 && x.NextVisitDue is { } due && due.Date <= today.AddDays(7)))
        {
            var due = agreement.NextVisitDue!.Value;
            if (due.Date > agreement.EndDate.Date)
            {
                agreement.NextVisitDue = null;
                continue;
            }

            var assetId = await dbContext.MaintenanceAgreementAssets.Where(x => x.AgreementId == agreement.Id).OrderBy(x => x.CreatedAt).Select(x => (Guid?)x.AssetId).FirstOrDefaultAsync(cancellationToken);
            if (assetId is null && agreement.FactoryId is null)
            {
                agreement.NextVisitDue = null;
                continue;
            }

            var job = await workOrderService.CreateAsync(tenantId, new FactoryActor(null, "Contractor agreements"), new CreateFactoryWorkOrder(
                $"{agreement.Contractor!.Name} scheduled visit: {agreement.Title}", FactoryWorkTypes.Preventive, "Medium",
                AssetId: assetId, OrgNodeId: assetId is null ? agreement.FactoryId : null,
                Description: $"Scheduled visit under {agreement.AgreementNumber}.{(string.IsNullOrWhiteSpace(agreement.Scope) ? "" : "\n\nScope: " + agreement.Scope)}",
                DueDate: due, Source: FactoryRequestSources.Preventive, RequestedByName: agreement.Contractor.Name), cancellationToken);
            job.ContractorId = agreement.ContractorId;
            job.MaintenanceAgreementId = agreement.Id;
            agreement.NextVisitDue = due.AddMonths(agreement.VisitEveryMonths!.Value);
            await dbContext.SaveChangesAsync(cancellationToken);
            visits++;
        }

        if (notificationDispatcher is not null && admins.Count > 0)
        {
            if (dueDocuments.Count > 0)
            {
                var lines = dueDocuments.OrderBy(x => x.ExpiresOn).Select(x => $"{x.Contractor!.Name}: {x.DocumentType} {(x.ExpiresOn!.Value.Date < today ? "expired" : "expires")} {x.ExpiresOn:dd MMM yyyy}").ToList();
                await notificationDispatcher.NotifyAsync(new NotificationRequest(
                    tenantId, DocumentKey, admins, $"{lines.Count} contractor document{(lines.Count == 1 ? "" : "s")} expiring",
                    string.Join("; ", lines.Take(5)) + (lines.Count > 5 ? $" and {lines.Count - 5} more" : string.Empty),
                    "/maintenance/contractors", nameof(ContractorDocument), null,
                    new Dictionary<string, string?> { ["Count"] = lines.Count.ToString(), ["Documents"] = string.Join("\n", lines) },
                    NotificationSeverities.Warning), cancellationToken);
            }

            if (renewals.Count > 0 || expired.Count > 0)
            {
                var lines = renewals.Select(x => $"{x.AgreementNumber} {x.Title} ({x.Contractor!.Name}) ends {x.EndDate:dd MMM yyyy}")
                    .Concat(expired.Select(x => $"{x.AgreementNumber} {x.Title} ({x.Contractor!.Name}) ended {x.EndDate:dd MMM yyyy}")).ToList();
                await notificationDispatcher.NotifyAsync(new NotificationRequest(
                    tenantId, RenewalKey, admins, $"{lines.Count} maintenance agreement{(lines.Count == 1 ? "" : "s")} to renew",
                    string.Join("; ", lines.Take(5)) + (lines.Count > 5 ? $" and {lines.Count - 5} more" : string.Empty),
                    "/maintenance/contractors?tab=agreements", nameof(MaintenanceAgreement), null,
                    new Dictionary<string, string?> { ["Count"] = lines.Count.ToString(), ["Agreements"] = string.Join("\n", lines) },
                    NotificationSeverities.Warning), cancellationToken);
            }
        }

        return new ContractorAlertRun(dueDocuments.Count, renewals.Count, visits, expired.Count);
    }

    private async Task<List<Guid>> RecipientsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var roles = (await dbContext.RolePermissions.AsNoTracking().Where(x => x.TenantId == tenantId && (x.CanAssignWorkOrders || x.CanManageSettings)).Select(x => x.Role).ToListAsync(cancellationToken))
            .Append(AppRoles.Admin).Distinct().ToList();
        return await dbContext.Users.AsNoTracking().Where(x => x.TenantId == tenantId && x.IsActive && roles.Contains(x.Role)).Select(x => x.Id).ToListAsync(cancellationToken);
    }
}

/// <summary>Runs contractor alerts and scheduled visits (checked every 6 hours).</summary>
public sealed class ContractorWorker(IServiceScopeFactory scopeFactory, ILogger<ContractorWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var service = scope.ServiceProvider.GetRequiredService<IContractorService>();
                var tenants = await db.Contractors.AsNoTracking().IgnoreQueryFilters().Where(x => x.IsActive && x.Tenant!.IsActive)
                    .Select(x => x.TenantId).Distinct().ToListAsync(stoppingToken);
                foreach (var tenantId in tenants)
                {
                    await service.RunAsync(tenantId, DateTime.UtcNow, stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Contractor alert cycle failed.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
