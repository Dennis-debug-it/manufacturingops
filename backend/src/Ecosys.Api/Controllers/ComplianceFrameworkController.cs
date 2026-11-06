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
/// The compliance framework: standards as lists of requirements mapped to the records that prove
/// them, one-click evidence packs, non-conformities as corrective actions, the audit calendar, and
/// the waste, food safety and glass registers that feed it.
/// </summary>
[ApiController]
[Authorize]
[Route("api/factory/compliance-framework")]
public sealed class ComplianceFrameworkController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IOrgScopeService orgScopeService,
    IComplianceFrameworkService frameworkService,
    IFactoryWorkOrderService workOrderService) : FactoryReportingControllerBase(dbContext, tenantContext, orgScopeService)
{
    private FactoryActor Actor => new(UserId, UserDisplayName ?? "Unknown");

    /* ── Standards and requirements ──────────────────────────────────────── */

    [HttpGet("templates")]
    public ActionResult<IReadOnlyCollection<StandardTemplateDto>> Templates()
    {
        EnsureCanView();
        return Ok(frameworkService.Templates.Select(x => new StandardTemplateDto(x.Key, x.Code, x.Name, x.Description, x.Requirements.Count)).ToList());
    }

    [HttpGet("evidence-sources")]
    public ActionResult<IReadOnlyDictionary<string, string>> Sources()
    {
        EnsureCanView();
        return Ok(EvidenceSources.Labels);
    }

    [HttpGet("standards")]
    public async Task<ActionResult<IReadOnlyCollection<StandardDto>>> Standards(CancellationToken cancellationToken)
    {
        EnsureCanView();
        return Ok(await Db.ComplianceStandards.AsNoTracking().Where(x => x.TenantId == TenantId).OrderBy(x => x.Name)
            .Select(x => new StandardDto(x.Id, x.Code, x.Name, x.Description, x.TemplateKey, x.IsActive,
                x.Requirements.OrderBy(r => r.Sequence).Select(r => new RequirementDto(r.Id, r.Clause, r.Title, r.Guidance, r.EvidenceSources, r.Sequence)).ToList()))
            .ToListAsync(cancellationToken));
    }

    [HttpPost("standards/install")]
    public async Task<ActionResult<IReadOnlyCollection<StandardDto>>> Install([FromBody] InstallStandardRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        await frameworkService.InstallAsync(TenantId, request.TemplateKey, cancellationToken);
        return await Standards(cancellationToken);
    }

    [HttpPost("standards")]
    public async Task<ActionResult<IReadOnlyCollection<StandardDto>>> CreateStandard([FromBody] SaveStandardRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var standard = new ComplianceStandard { TenantId = TenantId };
        await ApplyAsync(standard, request, cancellationToken);
        Db.ComplianceStandards.Add(standard);
        await Db.SaveChangesAsync(cancellationToken);
        return await Standards(cancellationToken);
    }

    [HttpPut("standards/{id:guid}")]
    public async Task<ActionResult<IReadOnlyCollection<StandardDto>>> UpdateStandard(Guid id, [FromBody] SaveStandardRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var standard = await LoadStandardAsync(id, cancellationToken);
        await ApplyAsync(standard, request, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
        return await Standards(cancellationToken);
    }

    [HttpPost("standards/{id:guid}/requirements")]
    public async Task<ActionResult<IReadOnlyCollection<StandardDto>>> AddRequirement(Guid id, [FromBody] SaveRequirementRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        await LoadStandardAsync(id, cancellationToken);
        var requirement = new StandardRequirement
        {
            TenantId = TenantId, StandardId = id,
            Sequence = (await Db.StandardRequirements.Where(x => x.StandardId == id).MaxAsync(x => (int?)x.Sequence, cancellationToken) ?? 0) + 1,
        };
        Apply(requirement, request);
        Db.StandardRequirements.Add(requirement);
        await Db.SaveChangesAsync(cancellationToken);
        return await Standards(cancellationToken);
    }

    [HttpPut("requirements/{id:guid}")]
    public async Task<ActionResult<IReadOnlyCollection<StandardDto>>> UpdateRequirement(Guid id, [FromBody] SaveRequirementRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var requirement = await Db.StandardRequirements.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("Requirement was not found.");
        Apply(requirement, request);
        await Db.SaveChangesAsync(cancellationToken);
        return await Standards(cancellationToken);
    }

    [HttpDelete("requirements/{id:guid}")]
    public async Task<ActionResult<IReadOnlyCollection<StandardDto>>> DeleteRequirement(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var requirement = await Db.StandardRequirements.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("Requirement was not found.");
        Db.StandardRequirements.Remove(requirement);
        await Db.SaveChangesAsync(cancellationToken);
        return await Standards(cancellationToken);
    }

    /// <summary>What the evidence pack will contain: per requirement, the records found and the gaps.</summary>
    [HttpGet("standards/{id:guid}/evidence")]
    public async Task<ActionResult<EvidencePack>> Evidence(Guid id, [FromQuery] Guid factoryId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        EnsureCanView();
        await FactoryAsync(factoryId, cancellationToken);
        var (start, end) = PackPeriod(from, to);
        return Ok(await frameworkService.EvidencePackAsync(TenantId, id, factoryId, start, end, cancellationToken));
    }

    /// <summary>The audit evidence pack as one PDF: summary of coverage, then each requirement with its records.</summary>
    [HttpGet("standards/{id:guid}/evidence-pack")]
    public async Task<IActionResult> EvidencePackPdf(Guid id, [FromQuery] Guid factoryId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var factory = await FactoryAsync(factoryId, cancellationToken);
        var (start, end) = PackPeriod(from, to);
        var pack = await frameworkService.EvidencePackAsync(TenantId, id, factoryId, start, end, cancellationToken);
        var name = $"evidence-{pack.StandardName.Split(' ')[0].ToLowerInvariant()}-{factory.Code.ToLowerInvariant()}-{start:yyyyMMdd}-{end.AddDays(-1):yyyyMMdd}.pdf";
        return File(FactoryPdfs.EvidencePack(await CompanyAsync(cancellationToken), pack), "application/pdf", name);
    }

    /* ── Non-conformities ────────────────────────────────────────────────── */

    [HttpGet("non-conformities")]
    public async Task<ActionResult<IReadOnlyCollection<NonConformityDto>>> NonConformities([FromQuery] string? status, [FromQuery] Guid? factoryId, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var factories = (await FactoriesInScopeAsync(cancellationToken)).Select(x => x.Id).ToList();
        var query = Db.NonConformities.AsNoTracking().Where(x => x.TenantId == TenantId && factories.Contains(x.FactoryId));
        if (factoryId.HasValue) query = query.Where(x => x.FactoryId == factoryId);
        if (string.Equals(status, "open", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.Status == NonConformityStatuses.Open || x.Status == NonConformityStatuses.InProgress || x.Status == NonConformityStatuses.Resolved);
        else if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        return Ok(await NcRows(query).ToListAsync(cancellationToken));
    }

    [HttpPost("non-conformities")]
    public async Task<ActionResult<NonConformityDto>> RaiseNonConformity([FromBody] SaveNonConformityRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var count = await Db.NonConformities.IgnoreQueryFilters().CountAsync(x => x.TenantId == TenantId, cancellationToken);
        var nc = new NonConformity { TenantId = TenantId, Number = $"NC-{count + 1:00000}", RaisedByName = Actor.Name };
        await ApplyAsync(nc, request, cancellationToken);
        Db.NonConformities.Add(nc);
        await Db.SaveChangesAsync(cancellationToken);
        return Ok(await NcRows(Db.NonConformities.Where(x => x.Id == nc.Id)).SingleAsync(cancellationToken));
    }

    [HttpPut("non-conformities/{id:guid}")]
    public async Task<ActionResult<NonConformityDto>> UpdateNonConformity(Guid id, [FromBody] SaveNonConformityRequest request, CancellationToken cancellationToken)
    {
        EnsureCanWork();
        var nc = await LoadNcAsync(id, cancellationToken);
        if (nc.Status == NonConformityStatuses.Closed) throw new BusinessRuleException("A closed finding can't be changed; reopen it first.");
        await ApplyAsync(nc, request, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
        return Ok(await NcRows(Db.NonConformities.Where(x => x.Id == nc.Id)).SingleAsync(cancellationToken));
    }

    /// <summary>start, resolve (corrective action done, with evidence), close (verified), reopen, or job (raise a corrective job).</summary>
    [HttpPost("non-conformities/{id:guid}/{action}")]
    public async Task<ActionResult<NonConformityDto>> NonConformityAction(Guid id, string action, [FromBody] NonConformityActionRequest? request, CancellationToken cancellationToken)
    {
        var nc = await LoadNcAsync(id, cancellationToken);
        var now = OccurredAt.UtcNow;
        switch (action.ToLowerInvariant())
        {
            case "start":
                EnsureCanWork();
                if (nc.Status != NonConformityStatuses.Open) throw new BusinessRuleException("Only an open finding can be started.");
                nc.Status = NonConformityStatuses.InProgress;
                break;
            case "resolve":
                EnsureCanWork();
                if (nc.Status is not (NonConformityStatuses.Open or NonConformityStatuses.InProgress)) throw new BusinessRuleException("This finding is not open.");
                nc.CorrectiveAction = Clean(request?.CorrectiveAction) ?? nc.CorrectiveAction ?? throw new BusinessRuleException("Say what was done to correct it.");
                nc.ClosureEvidence = Clean(request?.Evidence) ?? throw new BusinessRuleException("Describe the evidence that it is corrected.");
                nc.Status = NonConformityStatuses.Resolved;
                nc.ResolvedAt = now;
                break;
            case "close":
                EnsureCanManage();
                if (nc.Status != NonConformityStatuses.Resolved) throw new BusinessRuleException("Resolve the finding, with evidence, before closing it.");
                if (nc.WorkOrderId is { } openJob && await Db.WorkOrders.AnyAsync(x => x.Id == openJob && x.Status != WorkOrderStatuses.Completed && x.Status != WorkOrderStatuses.Verified && x.Status != WorkOrderStatuses.Closed && x.Status != WorkOrderStatuses.Cancelled, cancellationToken))
                    throw new BusinessRuleException("Its corrective job is still open.");
                nc.Status = NonConformityStatuses.Closed;
                nc.ClosedAt = now;
                nc.ClosedByName = Actor.Name;
                break;
            case "reopen":
                EnsureCanManage();
                if (nc.Status is not (NonConformityStatuses.Resolved or NonConformityStatuses.Closed)) throw new BusinessRuleException("Only a resolved or closed finding can be reopened.");
                nc.Status = NonConformityStatuses.InProgress;
                nc.ClosedAt = null;
                nc.ClosedByName = null;
                break;
            case "job":
                EnsureCanManage();
                if (nc.WorkOrderId is not null) throw new BusinessRuleException("A corrective job is already raised.");
                var job = await workOrderService.CreateAsync(TenantId, Actor, new CreateFactoryWorkOrder(
                    $"Corrective action {nc.Number}", FactoryWorkTypes.Safety, nc.Severity == "Major" ? "High" : "Medium", AssetId: request?.AssetId,
                    OrgNodeId: request?.AssetId is null ? nc.FactoryId : null, Description: nc.Finding, DueDate: nc.DueDate, RequestedByName: Actor.Name), cancellationToken);
                nc.WorkOrderId = job.Id;
                if (nc.Status == NonConformityStatuses.Open) nc.Status = NonConformityStatuses.InProgress;
                break;
            default:
                throw new BusinessRuleException("Action must be start, resolve, close, reopen or job.");
        }

        await Db.SaveChangesAsync(cancellationToken);
        return Ok(await NcRows(Db.NonConformities.Where(x => x.Id == nc.Id)).SingleAsync(cancellationToken));
    }

    /* ── Audit calendar ──────────────────────────────────────────────────── */

    [HttpGet("audits")]
    public async Task<ActionResult<IReadOnlyCollection<AuditEventDto>>> Audits([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var factories = (await FactoriesInScopeAsync(cancellationToken)).Select(x => x.Id).ToList();
        var start = DateTime.SpecifyKind((from ?? DateTime.UtcNow.AddMonths(-3)).Date, DateTimeKind.Utc);
        var end = DateTime.SpecifyKind((to ?? DateTime.UtcNow.AddMonths(15)).Date, DateTimeKind.Utc);
        return Ok(await AuditRows(Db.AuditEvents.AsNoTracking().Where(x => x.TenantId == TenantId && factories.Contains(x.FactoryId) && x.ScheduledFor >= start && x.ScheduledFor <= end))
            .ToListAsync(cancellationToken));
    }

    [HttpPost("audits")]
    public async Task<ActionResult<AuditEventDto>> PlanAudit([FromBody] SaveAuditRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var audit = new AuditEvent { TenantId = TenantId };
        await ApplyAsync(audit, request, cancellationToken);
        Db.AuditEvents.Add(audit);
        await Db.SaveChangesAsync(cancellationToken);
        return Ok(await AuditRows(Db.AuditEvents.Where(x => x.Id == audit.Id)).SingleAsync(cancellationToken));
    }

    [HttpPut("audits/{id:guid}")]
    public async Task<ActionResult<AuditEventDto>> UpdateAudit(Guid id, [FromBody] SaveAuditRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var audit = await Db.AuditEvents.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("Audit was not found.");
        await FactoryAsync(audit.FactoryId, cancellationToken);
        var date = audit.ScheduledFor;
        await ApplyAsync(audit, request, cancellationToken);
        if (audit.ScheduledFor != date) audit.LastReminderDays = null;
        await Db.SaveChangesAsync(cancellationToken);
        return Ok(await AuditRows(Db.AuditEvents.Where(x => x.Id == audit.Id)).SingleAsync(cancellationToken));
    }

    /* ── Waste ───────────────────────────────────────────────────────────── */

    [HttpGet("waste")]
    public async Task<ActionResult<IReadOnlyCollection<WasteDto>>> Waste([FromQuery] Guid? factoryId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var factories = (await FactoriesInScopeAsync(cancellationToken)).Select(x => x.Id).ToList();
        var (start, end) = PackPeriod(from, to);
        var query = Db.WasteDisposals.AsNoTracking().Where(x => x.TenantId == TenantId && factories.Contains(x.FactoryId) && x.DisposedOn >= start && x.DisposedOn < end);
        if (factoryId.HasValue) query = query.Where(x => x.FactoryId == factoryId);
        return Ok(await query.OrderByDescending(x => x.DisposedOn)
            .Select(x => new WasteDto(x.Id, x.FactoryId, x.Factory!.Name, x.DisposedOn, x.WasteType, x.Quantity, x.Unit, x.ContractorId, x.HandlerName, x.LicenceNumber, x.ConsignmentNote, x.AttachmentId, x.Notes, x.RecordedByName))
            .ToListAsync(cancellationToken));
    }

    [HttpPost("waste")]
    public async Task<ActionResult<WasteDto>> RecordWaste([FromBody] SaveWasteRequest request, CancellationToken cancellationToken)
    {
        EnsureCanWork();
        await FactoryAsync(request.FactoryId, cancellationToken);
        var type = WasteTypes.Find(request.WasteType) ?? throw new BusinessRuleException($"Waste type must be one of: {string.Join(", ", WasteTypes.All)}.");
        if (request.Quantity <= 0) throw new BusinessRuleException("Quantity must be more than zero.");
        string handler;
        if (request.ContractorId is { } contractorId)
        {
            handler = await Db.Contractors.Where(x => x.TenantId == TenantId && x.Id == contractorId).Select(x => x.Name).SingleOrDefaultAsync(cancellationToken) ?? throw new BusinessRuleException("Contractor was not found.");
        }
        else
        {
            handler = Clean(request.HandlerName) ?? throw new BusinessRuleException("Say who took the waste.");
        }

        var record = new WasteDisposal
        {
            TenantId = TenantId, FactoryId = request.FactoryId, DisposedOn = Utc(request.DisposedOn), WasteType = type, Quantity = request.Quantity,
            Unit = Clean(request.Unit) ?? "kg", ContractorId = request.ContractorId, HandlerName = handler, LicenceNumber = Clean(request.LicenceNumber),
            ConsignmentNote = Clean(request.ConsignmentNote), Notes = Clean(request.Notes), RecordedByName = Actor.Name,
        };
        Db.WasteDisposals.Add(record);
        await Db.SaveChangesAsync(cancellationToken);
        return Ok(Unwrap(await Waste(record.FactoryId, record.DisposedOn, record.DisposedOn, cancellationToken)).First(x => x.Id == record.Id));
    }

    /* ── Food safety ─────────────────────────────────────────────────────── */

    [HttpGet("food-safety")]
    public async Task<ActionResult<IReadOnlyCollection<FoodSafetyDto>>> FoodSafety([FromQuery] string? kind, [FromQuery] Guid? factoryId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var factories = (await FactoriesInScopeAsync(cancellationToken)).Select(x => x.Id).ToList();
        var (start, end) = PackPeriod(from, to);
        var query = Db.FoodSafetyRecords.AsNoTracking().Where(x => x.TenantId == TenantId && factories.Contains(x.FactoryId) && x.DoneAt >= start && x.DoneAt < end);
        if (FoodSafetyKinds.Find(kind) is { } k) query = query.Where(x => x.Kind == k);
        if (factoryId.HasValue) query = query.Where(x => x.FactoryId == factoryId);
        return Ok(await FoodRows(query).ToListAsync(cancellationToken));
    }

    /// <summary>Records a cleaning, metal detector test or glass check. A failure raises a high-priority corrective job.</summary>
    [HttpPost("food-safety")]
    public async Task<ActionResult<FoodSafetyDto>> RecordFoodSafety([FromBody] SaveFoodSafetyRequest request, CancellationToken cancellationToken)
    {
        EnsureCanWork();
        await FactoryAsync(request.FactoryId, cancellationToken);
        var kind = FoodSafetyKinds.Find(request.Kind) ?? throw new BusinessRuleException("Kind must be Cleaning, MetalDetector or GlassCheck.");
        Asset? asset = null;
        if (request.AssetId is { } assetId)
        {
            asset = await Db.Assets.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == assetId, cancellationToken) ?? throw new BusinessRuleException("Asset was not found.");
            var subtree = await orgScopeService.GetSubtreeAsync(TenantId, request.FactoryId, cancellationToken);
            if (asset.OrgNodeId is not { } node || !subtree.Contains(node)) throw new BusinessRuleException("The asset is not in that factory.");
        }

        if (asset is null && string.IsNullOrWhiteSpace(request.Area)) throw new BusinessRuleException("Choose the asset or name the area.");
        var passed = kind switch
        {
            FoodSafetyKinds.MetalDetector => (request.FerrousRejected ?? throw new BusinessRuleException("Record each test piece.")) && (request.NonFerrousRejected ?? false) && (request.StainlessRejected ?? false),
            FoodSafetyKinds.GlassCheck => (request.ItemsDamaged ?? 0) == 0,
            _ => request.Passed ?? true,
        };
        if (kind == FoodSafetyKinds.MetalDetector && (request.NonFerrousRejected is null || request.StainlessRejected is null)) throw new BusinessRuleException("Record each test piece.");
        var record = new FoodSafetyRecord
        {
            TenantId = TenantId, FactoryId = request.FactoryId, Kind = kind, DoneAt = OccurredAt.UtcNow, AssetId = asset?.Id, Area = Clean(request.Area), Passed = passed,
            Method = Clean(request.Method), FerrousRejected = request.FerrousRejected, NonFerrousRejected = request.NonFerrousRejected, StainlessRejected = request.StainlessRejected,
            ItemsChecked = request.ItemsChecked, ItemsDamaged = request.ItemsDamaged, Notes = Clean(request.Notes), DoneByName = Actor.Name, VerifiedByName = Clean(request.VerifiedByName),
        };
        if (!passed)
        {
            var what = kind switch
            {
                FoodSafetyKinds.MetalDetector => "Metal detector failed its test",
                FoodSafetyKinds.GlassCheck => "Glass or hard plastic damage found",
                _ => "Cleaning failed verification",
            };
            var job = await workOrderService.CreateAsync(TenantId, Actor, new CreateFactoryWorkOrder(
                $"{what}: {asset?.AssetName ?? record.Area}", FactoryWorkTypes.Safety, "High", AssetId: asset?.Id, OrgNodeId: asset is null ? request.FactoryId : null,
                Description: $"{what}. {record.Notes}".Trim() + (kind == FoodSafetyKinds.MetalDetector ? " Hold product since the last good test until it is re-checked." : ""),
                RequestedByName: Actor.Name), cancellationToken);
            record.CorrectiveWorkOrderId = job.Id;
        }

        Db.FoodSafetyRecords.Add(record);
        await Db.SaveChangesAsync(cancellationToken);
        return Ok(await FoodRows(Db.FoodSafetyRecords.Where(x => x.Id == record.Id)).SingleAsync(cancellationToken));
    }

    [HttpGet("glass-register")]
    public async Task<ActionResult<IReadOnlyCollection<GlassItemDto>>> GlassRegister([FromQuery] Guid factoryId, CancellationToken cancellationToken)
    {
        EnsureCanView();
        await FactoryAsync(factoryId, cancellationToken);
        return Ok(await Db.GlassRegisterItems.AsNoTracking().Where(x => x.TenantId == TenantId && x.FactoryId == factoryId).OrderBy(x => x.Location).ThenBy(x => x.Description)
            .Select(x => new GlassItemDto(x.Id, x.FactoryId, x.Location, x.Description, x.Material, x.Quantity, x.IsActive)).ToListAsync(cancellationToken));
    }

    [HttpPost("glass-register")]
    public async Task<ActionResult<IReadOnlyCollection<GlassItemDto>>> AddGlassItem([FromBody] SaveGlassItemRequest request, CancellationToken cancellationToken)
    {
        EnsureCanWork();
        await FactoryAsync(request.FactoryId, cancellationToken);
        var item = new GlassRegisterItem { TenantId = TenantId, FactoryId = request.FactoryId };
        Apply(item, request);
        Db.GlassRegisterItems.Add(item);
        await Db.SaveChangesAsync(cancellationToken);
        return await GlassRegister(request.FactoryId, cancellationToken);
    }

    [HttpPut("glass-register/{id:guid}")]
    public async Task<ActionResult<IReadOnlyCollection<GlassItemDto>>> UpdateGlassItem(Guid id, [FromBody] SaveGlassItemRequest request, CancellationToken cancellationToken)
    {
        EnsureCanWork();
        var item = await Db.GlassRegisterItems.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("Item was not found.");
        await FactoryAsync(item.FactoryId, cancellationToken);
        Apply(item, request);
        await Db.SaveChangesAsync(cancellationToken);
        return await GlassRegister(item.FactoryId, cancellationToken);
    }

    /* ── Helpers ─────────────────────────────────────────────────────────── */

    /// <summary>Evidence packs look back a year by default.</summary>
    private static (DateTime From, DateTime To) PackPeriod(DateTime? from, DateTime? to) =>
        Period(from ?? DateTime.UtcNow.Date.AddYears(-1).AddDays(1), to);

    private IQueryable<NonConformityDto> NcRows(IQueryable<NonConformity> query) =>
        query.OrderBy(x => x.Status == NonConformityStatuses.Closed).ThenBy(x => x.DueDate).Select(x => new NonConformityDto(
            x.Id, x.Number, x.FactoryId, x.Factory!.Name, x.StandardId, x.Standard != null ? x.Standard.Name : null, x.RequirementId, x.Requirement != null ? x.Requirement.Clause : null,
            x.AuditEventId, x.Source, x.Severity, x.Finding, x.RootCause, x.CorrectiveAction, x.OwnerUserId, x.Owner != null ? x.Owner.FullName : null, x.DueDate, x.Status,
            x.WorkOrderId, Db.WorkOrders.Where(w => w.Id == x.WorkOrderId).Select(w => w.WorkOrderNumber).FirstOrDefault(),
            x.ClosureEvidence, x.ResolvedAt, x.ClosedAt, x.ClosedByName, x.RaisedByName, x.CreatedAt));

    private IQueryable<AuditEventDto> AuditRows(IQueryable<AuditEvent> query) =>
        query.OrderBy(x => x.ScheduledFor).Select(x => new AuditEventDto(x.Id, x.FactoryId, x.Factory!.Name, x.StandardId, x.Standard != null ? x.Standard.Name : null, x.Kind, x.ScheduledFor,
            x.Auditor, x.Status, x.Outcome, x.Notes, x.CompletedAt, Db.NonConformities.Count(n => n.AuditEventId == x.Id)));

    private IQueryable<FoodSafetyDto> FoodRows(IQueryable<FoodSafetyRecord> query) =>
        query.OrderByDescending(x => x.DoneAt).Select(x => new FoodSafetyDto(x.Id, x.FactoryId, x.Factory!.Name, x.Kind, x.DoneAt, x.AssetId, x.Asset != null ? x.Asset.AssetCode : null,
            x.Asset != null ? x.Asset.AssetName : null, x.Area, x.Passed, x.Method, x.FerrousRejected, x.NonFerrousRejected, x.StainlessRejected, x.ItemsChecked, x.ItemsDamaged, x.Notes,
            x.DoneByName, x.VerifiedByName, x.CorrectiveWorkOrderId, Db.WorkOrders.Where(w => w.Id == x.CorrectiveWorkOrderId).Select(w => w.WorkOrderNumber).FirstOrDefault()));

    private async Task ApplyAsync(ComplianceStandard standard, SaveStandardRequest request, CancellationToken cancellationToken)
    {
        var code = string.IsNullOrWhiteSpace(request.Code) ? throw new BusinessRuleException("Code is required.") : request.Code.Trim().ToUpperInvariant();
        if (await Db.ComplianceStandards.AnyAsync(x => x.TenantId == TenantId && x.Code == code && x.Id != standard.Id, cancellationToken)) throw new ConflictException($"Standard {code} already exists.");
        standard.Code = code;
        standard.Name = string.IsNullOrWhiteSpace(request.Name) ? throw new BusinessRuleException("Name is required.") : request.Name.Trim();
        standard.Description = Clean(request.Description);
        standard.IsActive = request.IsActive ?? true;
    }

    private static void Apply(StandardRequirement requirement, SaveRequirementRequest request)
    {
        requirement.Clause = string.IsNullOrWhiteSpace(request.Clause) ? throw new BusinessRuleException("Clause is required.") : request.Clause.Trim();
        requirement.Title = string.IsNullOrWhiteSpace(request.Title) ? throw new BusinessRuleException("Title is required.") : request.Title.Trim();
        requirement.Guidance = Clean(request.Guidance);
        var sources = EvidenceSources.Parse(string.Join(",", request.EvidenceSources ?? []));
        if (sources.Count == 0) throw new BusinessRuleException("Choose at least one kind of record that proves it (attached documents count).");
        requirement.EvidenceSources = string.Join(",", sources);
    }

    private async Task ApplyAsync(NonConformity nc, SaveNonConformityRequest request, CancellationToken cancellationToken)
    {
        await FactoryAsync(request.FactoryId, cancellationToken);
        nc.FactoryId = request.FactoryId;
        if (request.RequirementId is { } requirementId)
        {
            var requirement = await Db.StandardRequirements.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == requirementId, cancellationToken) ?? throw new BusinessRuleException("Requirement was not found.");
            nc.StandardId = requirement.StandardId;
        }
        else
        {
            nc.StandardId = request.StandardId;
        }

        if (nc.StandardId is { } sid && !await Db.ComplianceStandards.AnyAsync(x => x.TenantId == TenantId && x.Id == sid, cancellationToken)) throw new BusinessRuleException("Standard was not found.");
        if (request.AuditEventId is { } aid && !await Db.AuditEvents.AnyAsync(x => x.TenantId == TenantId && x.Id == aid, cancellationToken)) throw new BusinessRuleException("Audit was not found.");
        if (request.OwnerUserId is { } owner && !await Db.Users.AnyAsync(x => x.TenantId == TenantId && x.Id == owner && x.IsActive, cancellationToken)) throw new BusinessRuleException("Owner was not found.");
        nc.RequirementId = request.RequirementId;
        nc.AuditEventId = request.AuditEventId;
        nc.Source = Clean(request.Source) ?? "Audit";
        nc.Severity = request.Severity is "Major" or "Minor" or "Observation" ? request.Severity : throw new BusinessRuleException("Severity must be Major, Minor or Observation.");
        nc.Finding = Clean(request.Finding) ?? throw new BusinessRuleException("Describe the finding.");
        nc.RootCause = Clean(request.RootCause);
        nc.CorrectiveAction = Clean(request.CorrectiveAction);
        nc.OwnerUserId = request.OwnerUserId;
        nc.DueDate = request.DueDate is { } d ? Utc(d) : null;
    }

    private async Task ApplyAsync(AuditEvent audit, SaveAuditRequest request, CancellationToken cancellationToken)
    {
        await FactoryAsync(request.FactoryId, cancellationToken);
        if (request.StandardId is { } sid && !await Db.ComplianceStandards.AnyAsync(x => x.TenantId == TenantId && x.Id == sid, cancellationToken)) throw new BusinessRuleException("Standard was not found.");
        audit.FactoryId = request.FactoryId;
        audit.StandardId = request.StandardId;
        audit.Kind = AuditEventKinds.Find(request.Kind) ?? throw new BusinessRuleException($"Kind must be one of: {string.Join(", ", AuditEventKinds.All)}.");
        audit.ScheduledFor = Utc(request.ScheduledFor);
        audit.Auditor = Clean(request.Auditor);
        audit.Notes = Clean(request.Notes);
        audit.Outcome = Clean(request.Outcome);
        var status = request.Status ?? "Planned";
        if (status is not ("Planned" or "Done" or "Cancelled")) throw new BusinessRuleException("Status must be Planned, Done or Cancelled.");
        if (status == "Done" && audit.Status != "Done") audit.CompletedAt = OccurredAt.UtcNow;
        audit.Status = status;
    }

    private static void Apply(GlassRegisterItem item, SaveGlassItemRequest request)
    {
        item.Location = Clean(request.Location) ?? throw new BusinessRuleException("Say where it is.");
        item.Description = Clean(request.Description) ?? throw new BusinessRuleException("Describe the item.");
        item.Material = request.Material is "Glass" or "Hard plastic" or "Perspex" ? request.Material : "Glass";
        item.Quantity = request.Quantity is > 0 ? request.Quantity.Value : 1;
        item.IsActive = request.IsActive ?? true;
    }

    private async Task<ComplianceStandard> LoadStandardAsync(Guid id, CancellationToken cancellationToken) =>
        await Db.ComplianceStandards.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("Standard was not found.");

    private async Task<NonConformity> LoadNcAsync(Guid id, CancellationToken cancellationToken)
    {
        var nc = await Db.NonConformities.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("Finding was not found.");
        await FactoryAsync(nc.FactoryId, cancellationToken);
        return nc;
    }

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);

    private static T Unwrap<T>(ActionResult<T> result) => result.Value ?? (T)((ObjectResult)result.Result!).Value!;

    private void EnsureCanView() => userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanViewWorkOrders, PermissionNames.CanViewReports);

    private void EnsureCanManage() => userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageSettings, PermissionNames.CanAssignWorkOrders);

    /// <summary>Recording checks and registers, and working corrective actions: supervisors and technicians.</summary>
    private void EnsureCanWork() => userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanAssignWorkOrders, PermissionNames.CanCompleteWorkOrders);

}

/// <summary>Data protection (Kenya DPA 2019): consent records, subject access export, erasure of people who have left, and retention.</summary>
[ApiController]
[Authorize]
[Route("api/factory/data-protection")]
public sealed class DataProtectionController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IDataProtectionService dataProtectionService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    /// <summary>The people whose data is held: users and technicians, active and former.</summary>
    [HttpGet("people")]
    public async Task<ActionResult<IReadOnlyCollection<DataSubjectDto>>> People(CancellationToken cancellationToken)
    {
        EnsureAdmin();
        var technicians = await dbContext.Technicians.AsNoTracking().Where(x => x.TenantId == TenantId)
            .Select(x => new DataSubjectDto(x.UserId, x.Id, x.FullName, x.Email, x.IsActive, "Technician")).ToListAsync(cancellationToken);
        var linked = technicians.Where(x => x.UserId != null).Select(x => x.UserId!.Value).ToHashSet();
        var users = await dbContext.Users.AsNoTracking().Where(x => x.TenantId == TenantId)
            .Select(x => new DataSubjectDto(x.Id, null, x.FullName, x.Email, x.IsActive, x.Role)).ToListAsync(cancellationToken);
        return Ok(users.Where(x => !linked.Contains(x.UserId!.Value)).Concat(technicians).OrderBy(x => x.IsActive ? 1 : 0).ThenBy(x => x.Name).ToList());
    }

    [HttpGet("export")]
    public async Task<ActionResult<IReadOnlyDictionary<string, object?>>> Export([FromQuery] Guid? userId, [FromQuery] Guid? technicianId, CancellationToken cancellationToken)
    {
        EnsureAdmin();
        var data = await dataProtectionService.ExportAsync(TenantId, userId, technicianId, cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "DataSubjectExported", "Person", (technicianId ?? userId)!.Value.ToString(), "Personal data exported for a subject access request.", cancellationToken: cancellationToken);
        return Ok(data);
    }

    [HttpPost("erase")]
    public async Task<ActionResult<EraseResult>> Erase([FromBody] EraseRequest request, CancellationToken cancellationToken)
    {
        EnsureAdmin();
        if (!string.Equals(request.Confirm, "ERASE", StringComparison.Ordinal)) throw new BusinessRuleException("Type ERASE to confirm: this can't be undone.");
        var placeholder = await dataProtectionService.EraseAsync(TenantId, request.UserId, request.TechnicianId, cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "DataSubjectErased", "Person", (request.TechnicianId ?? request.UserId)!.Value.ToString(), $"Personal details replaced with \"{placeholder}\".", cancellationToken: cancellationToken);
        return Ok(new EraseResult(placeholder));
    }

    [HttpPost("retention")]
    public async Task<ActionResult<RetentionResult>> ApplyRetention(CancellationToken cancellationToken)
    {
        EnsureAdmin();
        var result = await dataProtectionService.ApplyRetentionAsync(TenantId, DateTime.UtcNow, cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "RetentionApplied", "PersonalData", TenantId.ToString(), $"{result.TrainingAnonymised} training, {result.PpeAnonymised} PPE records anonymised; {result.ConsentsRemoved} consents removed.", cancellationToken: cancellationToken);
        return Ok(result);
    }

    [HttpGet("consents")]
    public async Task<ActionResult<IReadOnlyCollection<ConsentDto>>> Consents(CancellationToken cancellationToken)
    {
        EnsureAdmin();
        return Ok(await dbContext.ConsentRecords.AsNoTracking().Where(x => x.TenantId == TenantId).OrderBy(x => x.SubjectName).ThenByDescending(x => x.GivenAt)
            .Select(x => new ConsentDto(x.Id, x.UserId, x.TechnicianId, x.SubjectName, x.Purpose, x.GivenAt, x.Method, x.WithdrawnAt, x.RecordedByName)).ToListAsync(cancellationToken));
    }

    [HttpPost("consents")]
    public async Task<ActionResult<IReadOnlyCollection<ConsentDto>>> RecordConsent([FromBody] SaveConsentRequest request, CancellationToken cancellationToken)
    {
        EnsureAdmin();
        string? name = null;
        if (request.TechnicianId is { } t) name = await dbContext.Technicians.Where(x => x.TenantId == TenantId && x.Id == t).Select(x => x.FullName).SingleOrDefaultAsync(cancellationToken) ?? throw new BusinessRuleException("Person was not found.");
        else if (request.UserId is { } u) name = await dbContext.Users.Where(x => x.TenantId == TenantId && x.Id == u).Select(x => x.FullName).SingleOrDefaultAsync(cancellationToken) ?? throw new BusinessRuleException("Person was not found.");
        name ??= string.IsNullOrWhiteSpace(request.SubjectName) ? throw new BusinessRuleException("Choose or name the person.") : request.SubjectName.Trim();
        dbContext.ConsentRecords.Add(new ConsentRecord
        {
            TenantId = TenantId, UserId = request.UserId, TechnicianId = request.TechnicianId, SubjectName = name,
            Purpose = string.IsNullOrWhiteSpace(request.Purpose) ? throw new BusinessRuleException("Say what the consent is for.") : request.Purpose.Trim(),
            GivenAt = DateTime.SpecifyKind((request.GivenAt ?? DateTime.UtcNow).Date, DateTimeKind.Utc), Method = string.IsNullOrWhiteSpace(request.Method) ? "Signed form" : request.Method.Trim(),
            RecordedByName = UserDisplayName,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return await Consents(cancellationToken);
    }

    [HttpPost("consents/{id:guid}/withdraw")]
    public async Task<ActionResult<IReadOnlyCollection<ConsentDto>>> Withdraw(Guid id, CancellationToken cancellationToken)
    {
        EnsureAdmin();
        var consent = await dbContext.ConsentRecords.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("Consent was not found.");
        consent.WithdrawnAt ??= DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await Consents(cancellationToken);
    }

    private void EnsureAdmin() => userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
}

public sealed record StandardTemplateDto(string Key, string Code, string Name, string Description, int Requirements);
public sealed record RequirementDto(Guid Id, string Clause, string Title, string? Guidance, string EvidenceSources, int Sequence);
public sealed record StandardDto(Guid Id, string Code, string Name, string? Description, string? TemplateKey, bool IsActive, IReadOnlyCollection<RequirementDto> Requirements);
public sealed record InstallStandardRequest(string TemplateKey);
public sealed record SaveStandardRequest([StringLength(40)] string Code, [StringLength(200)] string Name, [StringLength(2000)] string? Description = null, bool? IsActive = null);
public sealed record SaveRequirementRequest([StringLength(40)] string Clause, [StringLength(300)] string Title, IReadOnlyCollection<string>? EvidenceSources, [StringLength(2000)] string? Guidance = null);
public sealed record NonConformityDto(Guid Id, string Number, Guid FactoryId, string FactoryName, Guid? StandardId, string? StandardName, Guid? RequirementId, string? RequirementClause,
    Guid? AuditEventId, string Source, string Severity, string Finding, string? RootCause, string? CorrectiveAction, Guid? OwnerUserId, string? OwnerName, DateTime? DueDate, string Status,
    Guid? WorkOrderId, string? WorkOrderNumber, string? ClosureEvidence, DateTime? ResolvedAt, DateTime? ClosedAt, string? ClosedByName, string? RaisedByName, DateTime RaisedAt);
public sealed record SaveNonConformityRequest(Guid FactoryId, [StringLength(2000)] string Finding, string Severity, Guid? StandardId = null, Guid? RequirementId = null, Guid? AuditEventId = null,
    [StringLength(50)] string? Source = null, [StringLength(2000)] string? RootCause = null, [StringLength(2000)] string? CorrectiveAction = null, Guid? OwnerUserId = null, DateTime? DueDate = null);
public sealed record NonConformityActionRequest([StringLength(2000)] string? CorrectiveAction = null, [StringLength(2000)] string? Evidence = null, Guid? AssetId = null);
public sealed record AuditEventDto(Guid Id, Guid FactoryId, string FactoryName, Guid? StandardId, string? StandardName, string Kind, DateTime ScheduledFor, string? Auditor, string Status,
    string? Outcome, string? Notes, DateTime? CompletedAt, int Findings);
public sealed record SaveAuditRequest(Guid FactoryId, string Kind, DateTime ScheduledFor, Guid? StandardId = null, [StringLength(200)] string? Auditor = null, string? Status = null,
    [StringLength(500)] string? Outcome = null, [StringLength(2000)] string? Notes = null);
public sealed record WasteDto(Guid Id, Guid FactoryId, string FactoryName, DateTime DisposedOn, string WasteType, decimal Quantity, string Unit, Guid? ContractorId, string HandlerName,
    string? LicenceNumber, string? ConsignmentNote, Guid? AttachmentId, string? Notes, string? RecordedByName);
public sealed record SaveWasteRequest(Guid FactoryId, DateTime DisposedOn, string WasteType, decimal Quantity, [StringLength(20)] string? Unit = null, Guid? ContractorId = null,
    [StringLength(200)] string? HandlerName = null, [StringLength(100)] string? LicenceNumber = null, [StringLength(100)] string? ConsignmentNote = null, [StringLength(1000)] string? Notes = null);
public sealed record FoodSafetyDto(Guid Id, Guid FactoryId, string FactoryName, string Kind, DateTime DoneAt, Guid? AssetId, string? AssetCode, string? AssetName, string? Area, bool Passed,
    string? Method, bool? FerrousRejected, bool? NonFerrousRejected, bool? StainlessRejected, int? ItemsChecked, int? ItemsDamaged, string? Notes, string DoneByName, string? VerifiedByName,
    Guid? CorrectiveWorkOrderId, string? CorrectiveWorkOrderNumber);
public sealed record SaveFoodSafetyRequest(Guid FactoryId, string Kind, Guid? AssetId = null, [StringLength(200)] string? Area = null, bool? Passed = null, [StringLength(500)] string? Method = null,
    bool? FerrousRejected = null, bool? NonFerrousRejected = null, bool? StainlessRejected = null, int? ItemsChecked = null, int? ItemsDamaged = null, [StringLength(1000)] string? Notes = null,
    [StringLength(200)] string? VerifiedByName = null);
public sealed record GlassItemDto(Guid Id, Guid FactoryId, string Location, string Description, string Material, int Quantity, bool IsActive);
public sealed record SaveGlassItemRequest(Guid FactoryId, [StringLength(200)] string Location, [StringLength(300)] string Description, string? Material = null, int? Quantity = null, bool? IsActive = null);
public sealed record DataSubjectDto(Guid? UserId, Guid? TechnicianId, string Name, string Email, bool IsActive, string Kind);
public sealed record EraseRequest(Guid? UserId, Guid? TechnicianId, string Confirm);
public sealed record EraseResult(string Placeholder);
public sealed record ConsentDto(Guid Id, Guid? UserId, Guid? TechnicianId, string SubjectName, string Purpose, DateTime GivenAt, string Method, DateTime? WithdrawnAt, string? RecordedByName);
public sealed record SaveConsentRequest([StringLength(300)] string Purpose, Guid? UserId = null, Guid? TechnicianId = null, [StringLength(200)] string? SubjectName = null, DateTime? GivenAt = null,
    [StringLength(50)] string? Method = null);
