using System.ComponentModel.DataAnnotations;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// Lets a technician on a job flag new work they spot (a failing unit, an upgrade the site needs)
/// as a sales lead, without any access to the sales CRM themselves. The lead is a deal at the Lead
/// stage for the job's client and site, owned by the best available sales owner
/// (<see cref="ISalesOwnerResolver"/>), with a follow-up due straight away so the owner is reminded.
/// Works from the offline outbox like any other job action; a replay is answered once.
/// </summary>
[ApiController]
[Authorize]
[Route("api/technician/jobs/{workOrderId:guid}/sales-lead")]
public sealed class TechnicianSalesLeadsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    ILicenseGuardService licenseGuardService,
    ISalesOwnerResolver salesOwnerResolver,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    public const string TechnicianSource = "Technician";

    [HttpPost]
    public async Task<ActionResult<TechnicianSalesLeadResponse>> Flag(Guid workOrderId, [FromBody] FlagSalesLeadRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var note = request.Note?.Trim();
        if (string.IsNullOrWhiteSpace(note))
            throw new BusinessRuleException("Say what the client could use, e.g. \"Chiller 2 compressor near end of life\".");

        var snapshot = await licenseGuardService.GetSnapshotAsync(TenantId, cancellationToken);
        if (!ServiceOpsPlans.RequiredModules("SalesCrm", "/api/crm").All(module => snapshot.Modules.Contains(module, StringComparer.OrdinalIgnoreCase)))
            throw new ForbiddenException("Sales leads are not part of this workspace's plan.");
        await licenseGuardService.EnsureTenantCanMutateAsync(TenantId, cancellationToken);

        var workOrder = await dbContext.WorkOrders
            .AsNoTracking()
            .Include(x => x.Client)
            .Include(x => x.Asset)
            .Include(x => x.Site)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == workOrderId, cancellationToken)
            ?? throw new NotFoundException("Work order not found.");

        var technician = await dbContext.Technicians.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.UserId == UserId && x.IsActive)
            .Select(x => new { x.Id, x.FullName })
            .FirstOrDefaultAsync(cancellationToken);
        if (!IsAdmin)
        {
            var onJob = technician is not null && (workOrder.AssignedTechnicianId == technician.Id || workOrder.LeadTechnicianId == technician.Id
                || await dbContext.WorkOrderTechnicianAssignments.AnyAsync(x => x.WorkOrderId == workOrder.Id && x.TechnicianId == technician.Id, cancellationToken));
            if (!onJob)
                throw new ForbiddenException("Technicians can only flag leads from jobs assigned to them.");
        }

        var flaggedBy = technician?.FullName
            ?? await dbContext.Users.Where(x => x.TenantId == TenantId && x.Id == UserId).Select(x => x.FullName).FirstOrDefaultAsync(cancellationToken)
            ?? "A technician";
        var owner = await salesOwnerResolver.ResolveAsync(TenantId, [], workOrder.ClientId,
            new SalesLeadFacts(TechnicianSource, workOrder.WorkType,
                string.Join(' ', new[] { workOrder.Site?.TownCity, workOrder.Site?.County, workOrder.Site?.Region, workOrder.Site?.SiteName }.Where(x => !string.IsNullOrWhiteSpace(x)))), cancellationToken);
        var subject = workOrder.Asset?.AssetName ?? workOrder.Title;
        var now = DateTime.UtcNow;
        var currency = await dbContext.Tenants.Where(x => x.Id == TenantId).Select(x => x.Currency).FirstOrDefaultAsync(cancellationToken);
        currency = string.IsNullOrWhiteSpace(currency) ? "KES" : currency.Trim().ToUpperInvariant();

        var deal = new SalesOpportunity
        {
            TenantId = TenantId,
            Title = Truncate($"{subject}: {note}", 200),
            Stage = SalesStages.Lead,
            Probability = SalesStages.DefaultProbability(SalesStages.Lead),
            ClientId = workOrder.ClientId,
            SiteId = workOrder.SiteId,
            Source = TechnicianSource,
            Description = $"Flagged by {flaggedBy} on work order {workOrder.WorkOrderNumber}.\n\n{note}",
            Currency = currency,
            OwnerUserId = owner?.Id,
            OwnerName = owner?.FullName,
            SourceWorkOrderId = workOrder.Id,
            LastActivityAt = now,
        };
        dbContext.SalesOpportunities.Add(deal);
        dbContext.SalesActivities.Add(new SalesActivity
        {
            TenantId = TenantId, OpportunityId = deal.Id, Type = SalesActivityTypes.System, OccurredAt = now,
            Summary = $"Lead flagged by {flaggedBy} on work order {workOrder.WorkOrderNumber}: {note}",
            LoggedByUserId = UserId, LoggedByName = flaggedBy,
        });
        dbContext.SalesActivities.Add(new SalesActivity
        {
            TenantId = TenantId, OpportunityId = deal.Id, Type = "FollowUp", OccurredAt = now, DueAt = now,
            Summary = Truncate($"Contact {workOrder.Client?.ClientName ?? "the client"} about the technician's lead: {note}", 2000),
            LoggedByUserId = UserId, LoggedByName = flaggedBy,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "crm.lead.flagged_by_technician", nameof(SalesOpportunity), deal.Id.ToString(),
            $"{flaggedBy} flagged a sales lead on {workOrder.WorkOrderNumber}.", cancellationToken);

        return Ok(new TechnicianSalesLeadResponse(deal.Id, owner?.FullName));
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";
}

public sealed record FlagSalesLeadRequest([StringLength(1000)] string? Note);

/// <summary>The technician only learns who will follow it up, not the deal itself.</summary>
public sealed record TechnicianSalesLeadResponse(Guid Id, string? OwnerName);
