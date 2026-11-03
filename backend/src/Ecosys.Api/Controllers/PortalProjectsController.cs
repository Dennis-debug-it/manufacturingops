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
/// Read-only Projects page for client-portal users. A client sees only its own projects, and only
/// when the tenant has the portal page turned on and the project is marked visible. Costs, the
/// team, tasks, unapproved variations and unshared documents are never returned.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.PortalAccess)]
[Route("api/portal/projects")]
public sealed class PortalProjectsController(
    AppDbContext dbContext,
    IPortalContext portalContext,
    IProjectSettingsService projectSettingsService,
    IFileStorageService fileStorageService) : ControllerBase
{
    private Guid TenantId => portalContext.GetRequiredTenantId();
    private Guid ClientId => portalContext.GetRequiredClientId();
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<PortalProjectListItem>>> List(CancellationToken cancellationToken)
    {
        await EnsureEnabledAsync(cancellationToken);
        var projects = await VisibleProjects()
            .Include(x => x.Site)
            .Include(x => x.Stage)
            .Include(x => x.Milestones)
            .AsSplitQuery()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return Ok(projects.Select(MapListItem).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PortalProjectDetail>> Get(Guid id, CancellationToken cancellationToken)
    {
        await EnsureEnabledAsync(cancellationToken);
        var project = await VisibleProjects()
            .Include(x => x.Site)
            .Include(x => x.Stage)
            .Include(x => x.Milestones)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Project not found.");

        var variations = await dbContext.ProjectVariations
            .Where(x => x.TenantId == TenantId && x.ProjectId == id && x.Status == ProjectVariationStatuses.Approved)
            .OrderBy(x => x.DecidedAt)
            .Select(x => new PortalProjectVariation(x.VariationNumber, x.Kind, x.Title, x.Amount, x.ClientReference, x.DecidedAt))
            .ToListAsync(cancellationToken);
        var documents = await dbContext.Attachments
            .Where(x => x.TenantId == TenantId && x.EntityType == "Project" && x.EntityId == id && x.SharedWithClient)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new PortalProjectDocument(x.Id, x.FileName, x.FileSize, x.MimeType, x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(new PortalProjectDetail(
            MapListItem(project),
            project.Description,
            project.ActualStartDate,
            project.ActualEndDate,
            project.ApprovedAdditions,
            project.ApprovedOmissions,
            project.Milestones
                .OrderBy(x => x.SortOrder)
                .Select(x => new PortalProjectMilestone(x.Name, x.PlannedDate, x.Status,
                    x.Status is not (ProjectMilestoneStatuses.Completed or ProjectMilestoneStatuses.Verified) && x.PlannedDate.HasValue && x.PlannedDate.Value < Today))
                .ToList(),
            variations,
            documents));
    }

    /// <summary>
    /// Certified valuations, the project's invoices and retention held. Draft and submitted
    /// valuations stay internal, as do draft invoices. Needs the Finance module and the portal's
    /// invoices page switched on.
    /// </summary>
    [HttpGet("{id:guid}/valuations")]
    public async Task<ActionResult<PortalProjectBilling>> GetBilling(Guid id, CancellationToken cancellationToken)
    {
        await EnsureEnabledAsync(cancellationToken);
        if (!(await PortalModuleSettings.LoadAsync(dbContext, TenantId, cancellationToken)).ShowInvoices)
            throw new ForbiddenException("Invoices are not available in this portal.");
        var project = await VisibleProjects().SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Project not found.");

        var valuations = await dbContext.ProjectValuations
            .Where(x => x.TenantId == TenantId && x.ProjectId == id
                && (x.Status == ProjectValuationStatuses.Certified || x.Status == ProjectValuationStatuses.Invoiced))
            .ToListAsync(cancellationToken);
        var releases = await dbContext.ProjectRetentionReleases.Where(x => x.TenantId == TenantId && x.ProjectId == id).ToListAsync(cancellationToken);
        var invoices = await dbContext.TenantInvoices
            .Where(x => x.TenantId == TenantId && x.ClientId == ClientId && x.ProjectId == id && x.Status != "Draft" && x.Status != "Void")
            .OrderByDescending(x => x.InvoiceDate)
            .Select(x => new PortalInvoiceListItem(x.Id, x.InvoiceNumber, x.Status, x.InvoiceDate, x.DueDate, x.TotalAmount, x.AmountPaid, x.Balance, x.Currency))
            .ToListAsync(cancellationToken);

        return Ok(new PortalProjectBilling(
            project.Currency,
            valuations.OrderByDescending(x => x.PeriodEnd)
                .Select(x => new PortalProjectValuation(x.ValuationNumber, x.PeriodEnd, x.CertificateReference, x.CertifiedOn, x.GrossValueToDate,
                    x.RetentionToDate, x.CertifiedAmount ?? x.AmountDue))
                .ToList(),
            invoices,
            ProjectValuationCalculator.RetentionHeld(valuations, releases)));
    }

    [HttpGet("{id:guid}/documents/{documentId:guid}/download")]
    public async Task<IActionResult> DownloadDocument(Guid id, Guid documentId, CancellationToken cancellationToken)
    {
        await EnsureEnabledAsync(cancellationToken);
        if (!await VisibleProjects().AnyAsync(x => x.Id == id, cancellationToken))
            throw new NotFoundException("Project not found.");
        var attachment = await dbContext.Attachments.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == documentId
                && x.EntityType == "Project" && x.EntityId == id && x.SharedWithClient, cancellationToken)
            ?? throw new NotFoundException("Document not found.");
        var stream = await fileStorageService.DownloadAsync(attachment.StoragePath, cancellationToken);
        return File(stream, attachment.MimeType, attachment.FileName, enableRangeProcessing: true);
    }

    private IQueryable<Project> VisibleProjects() =>
        dbContext.Projects.Where(x => x.TenantId == TenantId && x.ClientId == ClientId && x.VisibleInClientPortal
            && x.Stage!.Category != ProjectStageCategories.Cancelled);

    private async Task EnsureEnabledAsync(CancellationToken cancellationToken)
    {
        var settings = await projectSettingsService.GetAsync(TenantId, cancellationToken);
        if (!settings.ClientPortal)
            throw new ForbiddenException("Projects are not available in this portal.");
    }

    private static PortalProjectListItem MapListItem(Project x)
    {
        var planned = ProjectCommercials.ComputePlannedProgress(x.Milestones, Today);
        return new PortalProjectListItem(
            x.Id, x.ProjectNumber, x.Name, x.Site?.SiteName, x.Stage?.Name ?? "", x.Stage?.Category ?? "",
            x.Currency, x.RevisedValue, x.EffectiveProgressPercent, planned, x.PlannedStartDate, x.PlannedEndDate);
    }
}

public sealed record PortalProjectListItem(
    Guid Id, string ProjectNumber, string Name, string? SiteName, string StageName, string StageCategory,
    string Currency, decimal ContractValue, decimal ProgressPercent, decimal PlannedProgressPercent,
    DateOnly? PlannedStartDate, DateOnly? PlannedEndDate);

public sealed record PortalProjectMilestone(string Name, DateOnly? PlannedDate, string Status, bool IsOverdue);

public sealed record PortalProjectVariation(string VariationNumber, string Kind, string Title, decimal Amount, string? ClientReference, DateTime? ApprovedAt);

public sealed record PortalProjectValuation(
    string ValuationNumber, DateOnly PeriodEnd, string? CertificateReference, DateOnly? CertifiedOn, decimal GrossValueToDate, decimal RetentionToDate, decimal CertifiedAmount);

public sealed record PortalProjectBilling(string Currency, IReadOnlyList<PortalProjectValuation> Valuations, IReadOnlyList<PortalInvoiceListItem> Invoices, decimal RetentionHeld);

public sealed record PortalProjectDocument(Guid Id, string FileName, long FileSize, string MimeType, DateTime CreatedAt);

public sealed record PortalProjectDetail(
    PortalProjectListItem Summary,
    string? Description,
    DateOnly? ActualStartDate,
    DateOnly? ActualEndDate,
    decimal ApprovedAdditions,
    decimal ApprovedOmissions,
    IReadOnlyList<PortalProjectMilestone> Milestones,
    IReadOnlyList<PortalProjectVariation> Variations,
    IReadOnlyList<PortalProjectDocument> Documents);
