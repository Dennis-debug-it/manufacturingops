using System.ComponentModel.DataAnnotations;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// Contractors: the register with compliance documents and expiry, maintenance agreements (AMCs)
/// with SLAs, visit schedules and renewal, giving jobs to contractors, and matching their invoices
/// to the jobs they completed.
/// </summary>
[ApiController]
[Authorize]
[Route("api/factory/contractors")]
public sealed class ContractorsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IOrgScopeService orgScopeService,
    IContractorService contractorService) : TenantAwareControllerBase(tenantContext)
{
    private static readonly string[] Finished = [WorkOrderStatuses.Completed, WorkOrderStatuses.Verified, WorkOrderStatuses.Closed, WorkOrderStatuses.Cancelled, WorkOrderStatuses.Rejected];
    private static readonly string[] Done = [WorkOrderStatuses.Completed, WorkOrderStatuses.Verified, WorkOrderStatuses.Closed];

    private FactoryActor Actor => new(UserId, UserDisplayName ?? "Unknown");

    /* ── Register ────────────────────────────────────────────────────────── */

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ContractorSummary>>> List([FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var contractors = await dbContext.Contractors.Include(x => x.Documents)
            .Where(x => x.TenantId == TenantId && (includeInactive || x.IsActive)).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var ids = contractors.Select(x => x.Id).ToList();
        var agreements = await dbContext.MaintenanceAgreements.Where(x => ids.Contains(x.ContractorId) && x.Status == AgreementStatuses.Active)
            .GroupBy(x => x.ContractorId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        var open = await dbContext.WorkOrders.Where(x => x.ContractorId != null && ids.Contains(x.ContractorId.Value) && !Finished.Contains(x.Status))
            .GroupBy(x => x.ContractorId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        var today = DateTime.UtcNow.Date;
        var result = new List<ContractorSummary>();
        foreach (var c in contractors)
        {
            var blockers = await contractorService.WorkBlockersAsync(c, DateTime.UtcNow, cancellationToken);
            result.Add(new ContractorSummary(c.Id, c.Name, c.Services, c.ContactPerson, c.Phone, c.Email, c.Status, c.IsActive,
                c.Documents.Count(d => d.ExpiresOn is { } e && e.Date < today), c.Documents.Count(d => d.ExpiresOn is { } e && e.Date >= today && e.Date <= today.AddDays(30)),
                agreements.GetValueOrDefault(c.Id), open.GetValueOrDefault(c.Id), blockers.Count == 0, blockers));
        }

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ContractorDetail>> Get(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanView();
        return Ok(await DetailAsync(await LoadAsync(id, cancellationToken), cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<ContractorDetail>> Create([FromBody] SaveContractorRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var contractor = new Contractor { TenantId = TenantId };
        await ApplyAsync(contractor, request, cancellationToken);
        dbContext.Contractors.Add(contractor);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await DetailAsync(contractor, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ContractorDetail>> Update(Guid id, [FromBody] SaveContractorRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var contractor = await LoadAsync(id, cancellationToken);
        await ApplyAsync(contractor, request, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await DetailAsync(contractor, cancellationToken));
    }

    [HttpPost("{id:guid}/documents")]
    public async Task<ActionResult<ContractorDetail>> AddDocument(Guid id, [FromBody] SaveContractorDocumentRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var contractor = await LoadAsync(id, cancellationToken);
        var document = new ContractorDocument { TenantId = TenantId, ContractorId = id };
        await ApplyDocumentAsync(document, request, cancellationToken);
        dbContext.ContractorDocuments.Add(document);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await DetailAsync(contractor, cancellationToken));
    }

    [HttpPut("documents/{documentId:guid}")]
    public async Task<ActionResult<ContractorDetail>> UpdateDocument(Guid documentId, [FromBody] SaveContractorDocumentRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var document = await dbContext.ContractorDocuments.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == documentId, cancellationToken) ?? throw new NotFoundException("Document was not found.");
        var expiry = document.ExpiresOn;
        await ApplyDocumentAsync(document, request, cancellationToken);
        // A renewed document starts its alerts again.
        if (document.ExpiresOn != expiry) document.LastAlertDays = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await DetailAsync(await LoadAsync(document.ContractorId, cancellationToken), cancellationToken));
    }

    [HttpDelete("documents/{documentId:guid}")]
    public async Task<ActionResult<ContractorDetail>> DeleteDocument(Guid documentId, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var document = await dbContext.ContractorDocuments.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == documentId, cancellationToken) ?? throw new NotFoundException("Document was not found.");
        dbContext.ContractorDocuments.Remove(document);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await DetailAsync(await LoadAsync(document.ContractorId, cancellationToken), cancellationToken));
    }

    /// <summary>Completed jobs of the contractor not yet on an invoice, for building one.</summary>
    [HttpGet("{id:guid}/unbilled-jobs")]
    public async Task<ActionResult<IReadOnlyCollection<ContractorJob>>> Unbilled(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanView();
        await LoadAsync(id, cancellationToken);
        var billed = dbContext.ContractorInvoiceLines.Where(l => l.WorkOrderId != null && l.Invoice!.Status != ContractorInvoiceStatuses.Rejected).Select(l => l.WorkOrderId!.Value);
        return Ok(await JobRows(dbContext.WorkOrders.Where(x => x.TenantId == TenantId && x.ContractorId == id && Done.Contains(x.Status) && !billed.Contains(x.Id)))
            .ToListAsync(cancellationToken));
    }

    /* ── Jobs ────────────────────────────────────────────────────────────── */

    [HttpPost("/api/factory/work-orders/{workOrderId:guid}/contractor")]
    public async Task<ActionResult<ContractorJob>> Assign(Guid workOrderId, [FromBody] AssignContractorRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanAssignWorkOrders);
        var job = await LoadJobAsync(workOrderId, cancellationToken);
        if (Finished.Contains(job.Status)) throw new BusinessRuleException("The job is finished.");
        var contractor = await LoadAsync(request.ContractorId, cancellationToken);
        var agreement = request.AgreementId is { } agreementId
            ? await dbContext.MaintenanceAgreements.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == agreementId, cancellationToken) ?? throw new BusinessRuleException("Agreement was not found.")
            : await CoveringAgreementAsync(contractor.Id, job, cancellationToken);
        await contractorService.AssignAsync(job, contractor, agreement, Actor, cancellationToken);
        return Ok(await JobRows(dbContext.WorkOrders.Where(x => x.Id == job.Id)).SingleAsync(cancellationToken));
    }

    [HttpDelete("/api/factory/work-orders/{workOrderId:guid}/contractor")]
    public async Task<IActionResult> Unassign(Guid workOrderId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanAssignWorkOrders);
        var job = await LoadJobAsync(workOrderId, cancellationToken);
        if (await dbContext.ContractorInvoiceLines.AnyAsync(x => x.WorkOrderId == job.Id && x.Invoice!.Status != ContractorInvoiceStatuses.Rejected, cancellationToken))
            throw new BusinessRuleException("The job is on a contractor invoice; it can't be taken off the contractor.");
        job.ContractorId = null;
        job.MaintenanceAgreementId = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("jobs")]
    public async Task<ActionResult<IReadOnlyCollection<ContractorJob>>> Jobs([FromQuery] Guid? contractorId, [FromQuery] bool openOnly, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var query = dbContext.WorkOrders.Where(x => x.TenantId == TenantId && x.ContractorId != null);
        if (contractorId.HasValue) query = query.Where(x => x.ContractorId == contractorId);
        if (openOnly) query = query.Where(x => !Finished.Contains(x.Status));
        var scope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        if (scope.Nodes is not null)
        {
            var nodes = scope.Nodes.ToList();
            query = query.Where(x => x.OrgNodeId != null && nodes.Contains(x.OrgNodeId.Value));
        }

        return Ok(await JobRows(query).Take(300).ToListAsync(cancellationToken));
    }

    /* ── Agreements ──────────────────────────────────────────────────────── */

    [HttpGet("agreements")]
    public async Task<ActionResult<IReadOnlyCollection<AgreementSummary>>> Agreements([FromQuery] Guid? contractorId, [FromQuery] string? status, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var query = dbContext.MaintenanceAgreements.Where(x => x.TenantId == TenantId);
        if (contractorId.HasValue) query = query.Where(x => x.ContractorId == contractorId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        return Ok(await AgreementRows(query).ToListAsync(cancellationToken));
    }

    [HttpGet("agreements/{id:guid}")]
    public async Task<ActionResult<AgreementDetail>> Agreement(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanView();
        await LoadAgreementAsync(id, cancellationToken);
        return Ok(await AgreementDetailAsync(id, cancellationToken));
    }

    [HttpPost("agreements")]
    public async Task<ActionResult<AgreementDetail>> CreateAgreement([FromBody] SaveAgreementRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var count = await dbContext.MaintenanceAgreements.IgnoreQueryFilters().CountAsync(x => x.TenantId == TenantId, cancellationToken);
        var agreement = new MaintenanceAgreement { TenantId = TenantId, AgreementNumber = $"AMC-{count + 1:00000}" };
        await ApplyAgreementAsync(agreement, request, cancellationToken);
        dbContext.MaintenanceAgreements.Add(agreement);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await AgreementDetailAsync(agreement.Id, cancellationToken));
    }

    [HttpPut("agreements/{id:guid}")]
    public async Task<ActionResult<AgreementDetail>> UpdateAgreement(Guid id, [FromBody] SaveAgreementRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var agreement = await LoadAgreementAsync(id, cancellationToken);
        var end = agreement.EndDate;
        await ApplyAgreementAsync(agreement, request, cancellationToken);
        if (agreement.EndDate != end) agreement.RenewalAlertSentAt = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await AgreementDetailAsync(id, cancellationToken));
    }

    /// <summary>Renews an agreement for a new term: it stays the same agreement, with a new end date (and value).</summary>
    [HttpPost("agreements/{id:guid}/renew")]
    public async Task<ActionResult<AgreementDetail>> Renew(Guid id, [FromBody] RenewAgreementRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var agreement = await LoadAgreementAsync(id, cancellationToken);
        if (agreement.Status == AgreementStatuses.Cancelled) throw new BusinessRuleException("A cancelled agreement can't be renewed.");
        if (request.EndDate.Date <= agreement.EndDate.Date) throw new BusinessRuleException("The new end date must be after the current one.");
        var restartFrom = agreement.EndDate.Date.AddDays(1);
        agreement.EndDate = DateTime.SpecifyKind(request.EndDate.Date, DateTimeKind.Utc);
        if (request.Value is { } value) agreement.Value = value;
        agreement.Status = AgreementStatuses.Active;
        agreement.RenewalAlertSentAt = null;
        if (agreement.VisitEveryMonths is > 0 && agreement.NextVisitDue is null) agreement.NextVisitDue = DateTime.SpecifyKind(restartFrom < DateTime.UtcNow.Date ? DateTime.UtcNow.Date : restartFrom, DateTimeKind.Utc);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await AgreementDetailAsync(id, cancellationToken));
    }

    [HttpPost("agreements/{id:guid}/cancel")]
    public async Task<ActionResult<AgreementDetail>> CancelAgreement(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var agreement = await LoadAgreementAsync(id, cancellationToken);
        agreement.Status = AgreementStatuses.Cancelled;
        agreement.NextVisitDue = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await AgreementDetailAsync(id, cancellationToken));
    }

    /// <summary>Runs the alerts and raises due visits now, instead of waiting for the worker.</summary>
    [HttpPost("run")]
    public async Task<ActionResult<ContractorAlertRun>> Run(CancellationToken cancellationToken)
    {
        EnsureCanManage();
        return Ok(await contractorService.RunAsync(TenantId, DateTime.UtcNow, cancellationToken));
    }

    /* ── Invoices ────────────────────────────────────────────────────────── */

    [HttpGet("invoices")]
    public async Task<ActionResult<IReadOnlyCollection<ContractorInvoiceSummary>>> Invoices([FromQuery] Guid? contractorId, [FromQuery] string? status, CancellationToken cancellationToken)
    {
        EnsureCanViewInvoices();
        var query = dbContext.ContractorInvoices.Where(x => x.TenantId == TenantId);
        if (contractorId.HasValue) query = query.Where(x => x.ContractorId == contractorId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        return Ok(await InvoiceRows(query).Take(300).ToListAsync(cancellationToken));
    }

    [HttpGet("invoices/{id:guid}")]
    public async Task<ActionResult<ContractorInvoiceDetail>> Invoice(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanViewInvoices();
        await LoadInvoiceAsync(id, cancellationToken);
        return Ok(await InvoiceDetailAsync(id, cancellationToken));
    }

    [HttpPost("invoices")]
    public async Task<ActionResult<ContractorInvoiceDetail>> RecordInvoice([FromBody] SaveContractorInvoiceRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManageInvoices();
        var contractor = await LoadAsync(request.ContractorId, cancellationToken);
        var number = Required(request.InvoiceNumber, "Invoice number");
        if (await dbContext.ContractorInvoices.AnyAsync(x => x.TenantId == TenantId && x.ContractorId == contractor.Id && x.InvoiceNumber == number, cancellationToken))
            throw new ConflictException($"Invoice {number} from {contractor.Name} is already recorded.");
        if (request.Lines.Count == 0) throw new BusinessRuleException("Add the invoice lines, one per job.");
        var currency = (request.Currency ?? await dbContext.Tenants.Where(x => x.Id == TenantId).Select(x => x.Currency).SingleAsync(cancellationToken)).Trim().ToUpperInvariant();
        var invoice = new ContractorInvoice
        {
            TenantId = TenantId, ContractorId = contractor.Id, InvoiceNumber = number, InvoiceDate = DateTime.SpecifyKind(request.InvoiceDate.Date, DateTimeKind.Utc),
            Currency = currency, Amount = request.Amount ?? request.Lines.Sum(x => x.Amount), AttachmentId = request.AttachmentId
        };
        foreach (var line in request.Lines)
        {
            var description = Clean(line.Description);
            if (description is null && line.WorkOrderId is { } jobId)
                description = await dbContext.WorkOrders.Where(x => x.TenantId == TenantId && x.Id == jobId).Select(x => x.WorkOrderNumber + " " + x.Title).SingleOrDefaultAsync(cancellationToken);
            invoice.Lines.Add(new ContractorInvoiceLine { TenantId = TenantId, WorkOrderId = line.WorkOrderId, Description = description ?? "Unspecified work", Amount = line.Amount });
        }

        dbContext.ContractorInvoices.Add(invoice);
        await dbContext.SaveChangesAsync(cancellationToken);
        await contractorService.MatchAsync(invoice, cancellationToken);
        return Ok(await InvoiceDetailAsync(invoice.Id, cancellationToken));
    }

    [HttpPost("invoices/{id:guid}/match")]
    public async Task<ActionResult<ContractorInvoiceDetail>> Rematch(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanManageInvoices();
        var invoice = await LoadInvoiceAsync(id, cancellationToken);
        if (invoice.Status is ContractorInvoiceStatuses.Approved or ContractorInvoiceStatuses.Rejected) throw new BusinessRuleException("The invoice is already decided.");
        await contractorService.MatchAsync(invoice, cancellationToken);
        return Ok(await InvoiceDetailAsync(id, cancellationToken));
    }

    /// <summary>Approves a matched invoice for payment. One that did not match needs a note saying why it is approved anyway.</summary>
    [HttpPost("invoices/{id:guid}/approve")]
    public async Task<ActionResult<ContractorInvoiceDetail>> Approve(Guid id, [FromBody] ReasonRequest? request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanApproveMaterials);
        var invoice = await LoadInvoiceAsync(id, cancellationToken);
        if (invoice.Status is ContractorInvoiceStatuses.Approved or ContractorInvoiceStatuses.Rejected) throw new BusinessRuleException("The invoice is already decided.");
        await contractorService.MatchAsync(invoice, cancellationToken);
        var note = Clean(request?.Reason);
        if (invoice.Status != ContractorInvoiceStatuses.Matched && note is null)
            throw new BusinessRuleException("The invoice does not match the completed jobs. Resolve the issues, or give a reason to approve it anyway.");
        invoice.Status = ContractorInvoiceStatuses.Approved;
        invoice.DecidedAt = OccurredAt.UtcNow;
        invoice.DecidedByName = Actor.Name;
        invoice.DecisionNote = note;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await InvoiceDetailAsync(id, cancellationToken));
    }

    [HttpPost("invoices/{id:guid}/reject")]
    public async Task<ActionResult<ContractorInvoiceDetail>> Reject(Guid id, [FromBody] ReasonRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManageInvoices();
        var invoice = await LoadInvoiceAsync(id, cancellationToken);
        if (invoice.Status is ContractorInvoiceStatuses.Approved or ContractorInvoiceStatuses.Rejected) throw new BusinessRuleException("The invoice is already decided.");
        invoice.Status = ContractorInvoiceStatuses.Rejected;
        invoice.DecidedAt = OccurredAt.UtcNow;
        invoice.DecidedByName = Actor.Name;
        invoice.DecisionNote = Clean(request.Reason) ?? throw new BusinessRuleException("Say why it is rejected.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await InvoiceDetailAsync(id, cancellationToken));
    }

    /* ── Helpers ─────────────────────────────────────────────────────────── */

    private async Task<MaintenanceAgreement?> CoveringAgreementAsync(Guid contractorId, WorkOrder job, CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var active = dbContext.MaintenanceAgreements.Where(x => x.ContractorId == contractorId && x.Status == AgreementStatuses.Active && x.StartDate <= DateTime.UtcNow && x.EndDate >= today);
        if (job.AssetId is { } assetId && await active.Where(x => x.Assets.Any(a => a.AssetId == assetId)).OrderBy(x => x.EndDate).FirstOrDefaultAsync(cancellationToken) is { } byAsset)
            return byAsset;
        return null;
    }

    private async Task<ContractorDetail> DetailAsync(Contractor c, CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var documents = await dbContext.ContractorDocuments.Where(x => x.ContractorId == c.Id).OrderBy(x => x.DocumentType).ThenByDescending(x => x.ExpiresOn).ToListAsync(cancellationToken);
        var agreements = await AgreementRows(dbContext.MaintenanceAgreements.Where(x => x.ContractorId == c.Id)).ToListAsync(cancellationToken);
        var jobs = await JobRows(dbContext.WorkOrders.Where(x => x.ContractorId == c.Id)).Take(50).ToListAsync(cancellationToken);
        var invoices = await InvoiceRows(dbContext.ContractorInvoices.Where(x => x.ContractorId == c.Id)).Take(50).ToListAsync(cancellationToken);
        var supplier = c.SupplierId is { } s ? await dbContext.Suppliers.Where(x => x.Id == s).Select(x => x.SupplierName).SingleOrDefaultAsync(cancellationToken) : null;
        return new ContractorDetail(c.Id, c.Name, c.Services, c.ContactPerson, c.Phone, c.Email, c.RegistrationNumber, c.TaxPin, c.Status, c.SupplierId, supplier, c.Notes, c.IsActive,
            c.ExternalId, c.ExternalSystem, await contractorService.WorkBlockersAsync(c, DateTime.UtcNow, cancellationToken),
            documents.Select(d => new ContractorDocumentDto(d.Id, d.DocumentType, d.Reference, d.IssuedOn, d.ExpiresOn, d.AttachmentId, d.Notes, CertificateStatuses.Of(d.ExpiresOn, today),
                ContractorDocumentTypes.Required.Contains(d.DocumentType))).ToList(),
            agreements, jobs, invoices);
    }

    private async Task<AgreementDetail> AgreementDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var summary = await AgreementRows(dbContext.MaintenanceAgreements.Where(x => x.Id == id)).SingleAsync(cancellationToken);
        var agreement = await dbContext.MaintenanceAgreements.SingleAsync(x => x.Id == id, cancellationToken);
        var assets = await dbContext.MaintenanceAgreementAssets.Where(x => x.AgreementId == id).OrderBy(x => x.Asset!.AssetCode)
            .Select(x => new AgreementAsset(x.AssetId, x.Asset!.AssetCode, x.Asset.AssetName)).ToListAsync(cancellationToken);
        var jobs = await JobRows(dbContext.WorkOrders.Where(x => x.MaintenanceAgreementId == id)).Take(100).ToListAsync(cancellationToken);
        // SLA performance on the agreement's jobs: how many met the response and repair times.
        var finished = await dbContext.WorkOrders.Where(x => x.MaintenanceAgreementId == id && x.CompletedAt != null)
            .Select(x => new { x.SlaResolutionBreached, x.SlaResponseBreached }).ToListAsync(cancellationToken);
        return new AgreementDetail(summary, agreement.Scope, agreement.ResponseHours, agreement.RepairHours, agreement.RenewalNoticeDays, assets, jobs,
            finished.Count, finished.Count(x => !x.SlaResponseBreached && !x.SlaResolutionBreached));
    }

    private async Task<ContractorInvoiceDetail> InvoiceDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var summary = await InvoiceRows(dbContext.ContractorInvoices.Where(x => x.Id == id)).SingleAsync(cancellationToken);
        var invoice = await dbContext.ContractorInvoices.SingleAsync(x => x.Id == id, cancellationToken);
        var lines = await dbContext.ContractorInvoiceLines.Where(x => x.InvoiceId == id).OrderBy(x => x.CreatedAt)
            .Select(x => new ContractorInvoiceLineDto(x.Id, x.WorkOrderId, x.WorkOrder != null ? x.WorkOrder.WorkOrderNumber : null, x.WorkOrder != null ? x.WorkOrder.Title : null,
                x.WorkOrder != null ? x.WorkOrder.Status : null, x.Description, x.Amount)).ToListAsync(cancellationToken);
        // A copy uploaded after the invoice was recorded is attached to it by id.
        var attachmentId = invoice.AttachmentId ?? await dbContext.Attachments
            .Where(x => x.TenantId == TenantId && x.EntityType == "ContractorInvoice" && x.EntityId == id)
            .OrderByDescending(x => x.CreatedAt).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken);
        return new ContractorInvoiceDetail(summary, invoice.MatchIssues?.Split('\n', StringSplitOptions.RemoveEmptyEntries) ?? [], invoice.DecisionNote, attachmentId, lines);
    }

    private IQueryable<AgreementSummary> AgreementRows(IQueryable<MaintenanceAgreement> query) =>
        query.OrderBy(x => x.Status).ThenBy(x => x.EndDate).Select(x => new AgreementSummary(
            x.Id, x.AgreementNumber, x.ContractorId, x.Contractor!.Name, x.Title, x.FactoryId, x.Factory != null ? x.Factory.Name : null, x.StartDate, x.EndDate,
            x.Value, x.Currency, x.VisitEveryMonths, x.NextVisitDue, x.Status, x.Assets.Count,
            dbContext.WorkOrders.Count(w => w.MaintenanceAgreementId == x.Id && !Finished.Contains(w.Status))));

    private IQueryable<ContractorJob> JobRows(IQueryable<WorkOrder> query) =>
        query.OrderByDescending(x => x.CreatedAt).Select(x => new ContractorJob(
            x.Id, x.WorkOrderNumber, x.Title, x.Status, x.WorkType, x.AssetId, x.Asset != null ? x.Asset.AssetCode : null, x.ContractorId,
            dbContext.Contractors.Where(c => c.Id == x.ContractorId).Select(c => c.Name).FirstOrDefault(),
            x.MaintenanceAgreementId, dbContext.MaintenanceAgreements.Where(a => a.Id == x.MaintenanceAgreementId).Select(a => a.AgreementNumber).FirstOrDefault(),
            x.DueDate, x.CompletedAt, x.SlaResolutionDeadline, x.SlaResolutionBreached));

    private IQueryable<ContractorInvoiceSummary> InvoiceRows(IQueryable<ContractorInvoice> query) =>
        query.OrderByDescending(x => x.InvoiceDate).ThenByDescending(x => x.CreatedAt).Select(x => new ContractorInvoiceSummary(
            x.Id, x.ContractorId, x.Contractor!.Name, x.InvoiceNumber, x.InvoiceDate, x.Currency, x.Amount, x.Status, x.Lines.Count, x.DecidedByName, x.DecidedAt));

    private async Task ApplyAsync(Contractor contractor, SaveContractorRequest request, CancellationToken cancellationToken)
    {
        var name = Required(request.Name, "Name");
        if (await dbContext.Contractors.AnyAsync(x => x.TenantId == TenantId && x.Name == name && x.Id != contractor.Id, cancellationToken)) throw new ConflictException($"{name} is already registered.");
        if (request.SupplierId is { } supplierId && !await dbContext.Suppliers.AnyAsync(x => x.TenantId == TenantId && x.Id == supplierId, cancellationToken)) throw new BusinessRuleException("Supplier was not found.");
        contractor.Name = name;
        contractor.Services = Clean(request.Services);
        contractor.ContactPerson = Clean(request.ContactPerson);
        contractor.Phone = Clean(request.Phone);
        contractor.Email = Clean(request.Email);
        contractor.RegistrationNumber = Clean(request.RegistrationNumber);
        contractor.TaxPin = Clean(request.TaxPin);
        contractor.Status = ContractorStatuses.Find(request.Status) ?? ContractorStatuses.Approved;
        contractor.SupplierId = request.SupplierId;
        contractor.Notes = Clean(request.Notes);
        contractor.IsActive = request.IsActive ?? true;
        contractor.ExternalId = Clean(request.ExternalId);
        contractor.ExternalSystem = Clean(request.ExternalSystem);
    }

    private async Task ApplyDocumentAsync(ContractorDocument document, SaveContractorDocumentRequest request, CancellationToken cancellationToken)
    {
        document.DocumentType = ContractorDocumentTypes.Find(request.DocumentType) ?? throw new BusinessRuleException($"Document type must be one of: {string.Join(", ", ContractorDocumentTypes.All)}.");
        if (request.IssuedOn is { } issued && request.ExpiresOn is { } expires && expires < issued) throw new BusinessRuleException("It can't expire before it was issued.");
        if (request.AttachmentId is { } attachmentId && !await dbContext.Attachments.AnyAsync(x => x.TenantId == TenantId && x.Id == attachmentId, cancellationToken)) throw new BusinessRuleException("File was not found.");
        document.Reference = Clean(request.Reference);
        document.IssuedOn = Utc(request.IssuedOn);
        document.ExpiresOn = Utc(request.ExpiresOn);
        document.AttachmentId = request.AttachmentId;
        document.Notes = Clean(request.Notes);
    }

    private async Task ApplyAgreementAsync(MaintenanceAgreement agreement, SaveAgreementRequest request, CancellationToken cancellationToken)
    {
        var contractor = await LoadAsync(request.ContractorId, cancellationToken);
        if (request.EndDate.Date < request.StartDate.Date) throw new BusinessRuleException("The agreement can't end before it starts.");
        if (request.FactoryId is { } factoryId)
        {
            if (!await dbContext.OrgNodes.AnyAsync(x => x.TenantId == TenantId && x.Id == factoryId && x.Level == OrgNodeLevels.Factory, cancellationToken)) throw new BusinessRuleException("Factory was not found.");
            await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, factoryId, cancellationToken);
        }

        if (request.VisitEveryMonths is < 1 or > 24) throw new BusinessRuleException("Visits are every 1 to 24 months.");
        var assetIds = (request.AssetIds ?? []).Distinct().ToList();
        if (await dbContext.Assets.CountAsync(x => x.TenantId == TenantId && assetIds.Contains(x.Id), cancellationToken) != assetIds.Count) throw new BusinessRuleException("An asset was not found.");
        if (request.VisitEveryMonths is not null && assetIds.Count == 0 && request.FactoryId is null) throw new BusinessRuleException("Scheduled visits need the assets or factory they cover.");

        agreement.ContractorId = contractor.Id;
        agreement.Title = Required(request.Title, "Title");
        agreement.Scope = Clean(request.Scope);
        agreement.FactoryId = request.FactoryId;
        agreement.StartDate = DateTime.SpecifyKind(request.StartDate.Date, DateTimeKind.Utc);
        agreement.EndDate = DateTime.SpecifyKind(request.EndDate.Date, DateTimeKind.Utc);
        agreement.Value = request.Value;
        agreement.Currency = (request.Currency ?? await dbContext.Tenants.Where(x => x.Id == TenantId).Select(x => x.Currency).SingleAsync(cancellationToken)).Trim().ToUpperInvariant();
        agreement.ResponseHours = request.ResponseHours;
        agreement.RepairHours = request.RepairHours;
        if (agreement.VisitEveryMonths != request.VisitEveryMonths || request.FirstVisitDue is not null)
        {
            // With no first visit given it is the start date, or today for an agreement already running.
            var first = request.FirstVisitDue?.Date ?? (request.StartDate.Date < DateTime.UtcNow.Date ? DateTime.UtcNow.Date : request.StartDate.Date);
            agreement.NextVisitDue = request.VisitEveryMonths is null ? null : DateTime.SpecifyKind(first, DateTimeKind.Utc);
        }

        agreement.VisitEveryMonths = request.VisitEveryMonths;
        agreement.RenewalNoticeDays = request.RenewalNoticeDays is > 0 ? request.RenewalNoticeDays.Value : 60;
        if (agreement.Status == AgreementStatuses.Expired && agreement.EndDate.Date >= DateTime.UtcNow.Date) agreement.Status = AgreementStatuses.Active;

        var existing = await dbContext.MaintenanceAgreementAssets.Where(x => x.AgreementId == agreement.Id).ToListAsync(cancellationToken);
        dbContext.MaintenanceAgreementAssets.RemoveRange(existing.Where(x => !assetIds.Contains(x.AssetId)));
        foreach (var assetId in assetIds.Where(id => existing.All(x => x.AssetId != id)))
        {
            dbContext.MaintenanceAgreementAssets.Add(new MaintenanceAgreementAsset { TenantId = TenantId, AgreementId = agreement.Id, AssetId = assetId });
        }
    }

    private async Task<Contractor> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.Contractors.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("Contractor was not found.");

    private async Task<MaintenanceAgreement> LoadAgreementAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.MaintenanceAgreements.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("Agreement was not found.");

    private async Task<ContractorInvoice> LoadInvoiceAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.ContractorInvoices.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("Invoice was not found.");

    private async Task<WorkOrder> LoadJobAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await dbContext.WorkOrders.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id && x.ClientId == null, cancellationToken) ?? throw new NotFoundException("Job was not found.");
        if (job.OrgNodeId is { } node) await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, node, cancellationToken);
        return job;
    }

    private void EnsureCanView() => userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanViewWorkOrders);

    private void EnsureCanManage() => userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageSettings, PermissionNames.CanAssignWorkOrders);

    private void EnsureCanViewInvoices() => userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanAssignWorkOrders, PermissionNames.CanApproveMaterials, PermissionNames.CanViewReports);

    private void EnsureCanManageInvoices() => userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanAssignWorkOrders, PermissionNames.CanApproveMaterials);

    private static string Required(string? value, string label) => string.IsNullOrWhiteSpace(value) ? throw new BusinessRuleException($"{label} is required.") : value.Trim();

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Dates arrive without a kind; PostgreSQL stores UTC.</summary>
    private static DateTime? Utc(DateTime? value) => value is { } v ? DateTime.SpecifyKind(v.Date, DateTimeKind.Utc) : null;
}

public sealed record ContractorSummary(Guid Id, string Name, string? Services, string? ContactPerson, string? Phone, string? Email, string Status, bool IsActive,
    int DocumentsExpired, int DocumentsExpiring, int ActiveAgreements, int OpenJobs, bool CanTakeWork, IReadOnlyCollection<string> Blockers);
public sealed record ContractorDocumentDto(Guid Id, string DocumentType, string? Reference, DateTime? IssuedOn, DateTime? ExpiresOn, Guid? AttachmentId, string? Notes, string Status, bool Required);
public sealed record ContractorDetail(Guid Id, string Name, string? Services, string? ContactPerson, string? Phone, string? Email, string? RegistrationNumber, string? TaxPin, string Status,
    Guid? SupplierId, string? SupplierName, string? Notes, bool IsActive, string? ExternalId, string? ExternalSystem, IReadOnlyCollection<string> Blockers,
    IReadOnlyCollection<ContractorDocumentDto> Documents, IReadOnlyCollection<AgreementSummary> Agreements, IReadOnlyCollection<ContractorJob> Jobs, IReadOnlyCollection<ContractorInvoiceSummary> Invoices);
public sealed record SaveContractorRequest([StringLength(200)] string Name, [StringLength(500)] string? Services = null, [StringLength(200)] string? ContactPerson = null, [StringLength(50)] string? Phone = null,
    [StringLength(256)] string? Email = null, [StringLength(100)] string? RegistrationNumber = null, [StringLength(50)] string? TaxPin = null, [StringLength(20)] string? Status = null, Guid? SupplierId = null,
    [StringLength(2000)] string? Notes = null, bool? IsActive = null, [StringLength(100)] string? ExternalId = null, [StringLength(50)] string? ExternalSystem = null);
public sealed record SaveContractorDocumentRequest([StringLength(50)] string DocumentType, [StringLength(100)] string? Reference = null, DateTime? IssuedOn = null, DateTime? ExpiresOn = null,
    Guid? AttachmentId = null, [StringLength(1000)] string? Notes = null);
public sealed record AssignContractorRequest(Guid ContractorId, Guid? AgreementId = null);
public sealed record ContractorJob(Guid Id, string WorkOrderNumber, string Title, string Status, string WorkType, Guid? AssetId, string? AssetCode, Guid? ContractorId, string? ContractorName,
    Guid? AgreementId, string? AgreementNumber, DateTime? DueDate, DateTime? CompletedAt, DateTime? RepairDeadline, bool RepairBreached);
public sealed record AgreementSummary(Guid Id, string AgreementNumber, Guid ContractorId, string ContractorName, string Title, Guid? FactoryId, string? FactoryName, DateTime StartDate, DateTime EndDate,
    decimal? Value, string Currency, int? VisitEveryMonths, DateTime? NextVisitDue, string Status, int Assets, int OpenJobs);
public sealed record AgreementAsset(Guid AssetId, string AssetCode, string AssetName);
public sealed record AgreementDetail(AgreementSummary Agreement, string? Scope, int? ResponseHours, int? RepairHours, int RenewalNoticeDays, IReadOnlyCollection<AgreementAsset> Assets,
    IReadOnlyCollection<ContractorJob> Jobs, int JobsFinished, int JobsWithinSla);
public sealed record SaveAgreementRequest(Guid ContractorId, [StringLength(200)] string Title, DateTime StartDate, DateTime EndDate, [StringLength(4000)] string? Scope = null, Guid? FactoryId = null,
    decimal? Value = null, [StringLength(3)] string? Currency = null, int? ResponseHours = null, int? RepairHours = null, int? VisitEveryMonths = null, DateTime? FirstVisitDue = null,
    int? RenewalNoticeDays = null, IReadOnlyCollection<Guid>? AssetIds = null);
public sealed record RenewAgreementRequest(DateTime EndDate, decimal? Value = null);
public sealed record ContractorInvoiceSummary(Guid Id, Guid ContractorId, string ContractorName, string InvoiceNumber, DateTime InvoiceDate, string Currency, decimal Amount, string Status, int Lines,
    string? DecidedByName, DateTime? DecidedAt);
public sealed record ContractorInvoiceLineDto(Guid Id, Guid? WorkOrderId, string? WorkOrderNumber, string? WorkOrderTitle, string? WorkOrderStatus, string Description, decimal Amount);
public sealed record ContractorInvoiceDetail(ContractorInvoiceSummary Invoice, IReadOnlyCollection<string> Issues, string? DecisionNote, Guid? AttachmentId, IReadOnlyCollection<ContractorInvoiceLineDto> Lines);
public sealed record SaveContractorInvoiceLine(Guid? WorkOrderId, decimal Amount, [StringLength(300)] string? Description = null);
public sealed record SaveContractorInvoiceRequest(Guid ContractorId, [StringLength(60)] string InvoiceNumber, DateTime InvoiceDate, IReadOnlyCollection<SaveContractorInvoiceLine> Lines,
    decimal? Amount = null, [StringLength(3)] string? Currency = null, Guid? AttachmentId = null);
