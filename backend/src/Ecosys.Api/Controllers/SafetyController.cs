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

/// <summary>Safety: permits to work, isolation (LOTO), incidents and near misses, training and PPE records.</summary>
[ApiController]
[Authorize]
[Route("api/factory/safety")]
public sealed class SafetyController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IOrgScopeService orgScopeService,
    IFactoryWorkOrderService workOrderService,
    INotificationDispatcher? notificationDispatcher = null) : TenantAwareControllerBase(tenantContext)
{
    public const string IncidentKey = "safety.incident-reported";
    public const string PermitKey = "safety.permit-requested";
    private static readonly string[] Finished = [WorkOrderStatuses.Completed, WorkOrderStatuses.Verified, WorkOrderStatuses.Closed, WorkOrderStatuses.Cancelled, WorkOrderStatuses.Rejected];

    /// <summary>The precautions on each kind of permit, ticked before it is authorised.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> PermitChecks = new Dictionary<string, string[]>
    {
        [PermitTypes.HotWork] =
        [
            "Area cleared of tea dust, fluff and combustibles within 10 m",
            "Fire extinguisher and fire watch at the work",
            "Welding screens up; nearby openings covered",
            "Ducts, cyclones and dryer chambers checked free of dust",
            "Fire watch kept for 30 minutes after the work ends",
        ],
        [PermitTypes.ConfinedSpace] =
        [
            "Space isolated: drives locked out, inlets blanked",
            "Space ventilated and gas tested (O₂, LEL, CO, H₂S) before entry",
            "Attendant at the entry for the whole time",
            "Rescue plan and harness with lifeline ready",
            "Entrants signed in and out",
        ],
        [PermitTypes.Electrical] =
        [
            "Circuit isolated, locked and tagged at the source",
            "Proved dead with a tested voltage detector",
            "Earths applied where needed",
            "Insulated tools and rubber mat in use",
            "Work done by a licensed electrician",
        ],
        [PermitTypes.WorkingAtHeights] =
        [
            "Harness and lanyard inspected and anchored",
            "Ladder or scaffold inspected and tagged",
            "Area below barricaded",
            "No work in high wind or rain",
            "Rescue plan in place",
        ],
        [PermitTypes.General] =
        [
            "Hazards discussed with the team",
            "Equipment isolated where needed",
            "Right PPE worn",
        ],
    };

    private FactoryActor Actor => new(UserId, UserDisplayName ?? "Unknown");

    /* ── Permits ─────────────────────────────────────────────────────────── */

    [HttpGet("permits")]
    public async Task<ActionResult<IReadOnlyCollection<PermitSummary>>> Permits([FromQuery] string? status, [FromQuery] Guid? workOrderId, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var query = await ScopedPermitsAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (workOrderId.HasValue) query = query.Where(x => x.WorkOrderId == workOrderId);
        return Ok(await query.OrderByDescending(x => x.CreatedAt).Take(200).Select(PermitProjection).ToListAsync(cancellationToken));
    }

    [HttpGet("permits/{id:guid}")]
    public async Task<ActionResult<PermitDetail>> Permit(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanView();
        return Ok(await PermitDetailAsync(id, cancellationToken));
    }

    /// <summary>Requests a permit for a job; it carries the standard precautions for its type.</summary>
    [HttpPost("permits")]
    public async Task<ActionResult<PermitDetail>> RequestPermit([FromBody] RequestPermitRequest request, CancellationToken cancellationToken)
    {
        EnsureCanWork();
        var job = await LoadJobAsync(request.WorkOrderId, cancellationToken);
        if (Finished.Contains(job.Status)) throw new BusinessRuleException("The job is already finished.");
        var type = PermitTypes.Find(request.PermitType) ?? throw new BusinessRuleException($"Permit type must be one of: {string.Join(", ", PermitTypes.All)}.");
        var from = request.ValidFrom ?? DateTime.UtcNow;
        var to = request.ValidTo ?? from.AddHours(12);
        if (to <= from || (to - from).TotalHours > 24) throw new BusinessRuleException("A permit runs for at most 24 hours (normally one shift). Issue a new one for the next day.");
        if (await dbContext.WorkPermits.AnyAsync(x => x.WorkOrderId == job.Id && x.PermitType == type && (x.Status == PermitStatuses.Requested || x.Status == PermitStatuses.Approved), cancellationToken))
        {
            throw new BusinessRuleException("This job already has an open permit of that type.");
        }

        var count = await dbContext.WorkPermits.IgnoreQueryFilters().CountAsync(x => x.TenantId == TenantId, cancellationToken);
        var permit = new WorkPermit
        {
            TenantId = TenantId, PermitNumber = $"PTW-{count + 1:000000}", WorkOrderId = job.Id, PermitType = type,
            Hazards = Clean(request.Hazards), WorkDescription = Clean(request.WorkDescription) ?? job.Title,
            ValidFrom = DateTime.SpecifyKind(from, DateTimeKind.Utc), ValidTo = DateTime.SpecifyKind(to, DateTimeKind.Utc),
            IssuedToName = Clean(request.IssuedToName), RequestedByUserId = UserId, RequestedByName = Actor.Name,
        };
        foreach (var (prompt, index) in PermitChecks[type].Select((p, i) => (p, i)))
        {
            permit.Checks.Add(new WorkPermitCheck { TenantId = TenantId, PermitId = permit.Id, Prompt = prompt, Sequence = index });
        }

        dbContext.WorkPermits.Add(permit);
        var needed = FactoryJobGuards.PermitTypesOf(job.RequiredPermitTypes);
        if (!needed.Contains(type))
        {
            // A permit raised for a job makes it a requirement: work can't start without it being authorised.
            job.RequiredPermitTypes = FactoryJobGuards.NormalisePermitTypes(needed.Append(type));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await NotifyPermitAsync(permit, job, cancellationToken);
        return Ok(await PermitDetailAsync(permit.Id, cancellationToken));
    }

    [HttpPost("permits/{id:guid}/checks/{checkId:guid}")]
    public async Task<ActionResult<PermitDetail>> TickCheck(Guid id, Guid checkId, [FromBody] TickRequest request, CancellationToken cancellationToken)
    {
        EnsureCanWork();
        var permit = await LoadPermitAsync(id, cancellationToken);
        if (permit.Status != PermitStatuses.Requested) throw new BusinessRuleException("Precautions are ticked before the permit is authorised.");
        var check = await dbContext.WorkPermitChecks.SingleOrDefaultAsync(x => x.PermitId == id && x.Id == checkId, cancellationToken) ?? throw new NotFoundException("The check was not found.");
        check.Done = request.Done;
        check.DoneByName = request.Done ? Actor.Name : null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await PermitDetailAsync(id, cancellationToken));
    }

    /// <summary>Authorises (or refuses) a permit. Someone other than the requester does this, once every precaution is ticked.</summary>
    [HttpPost("permits/{id:guid}/decide")]
    public async Task<ActionResult<PermitDetail>> Decide(Guid id, [FromBody] PermitDecisionRequest request, CancellationToken cancellationToken)
    {
        EnsureCanAuthorise();
        var permit = await LoadPermitAsync(id, cancellationToken);
        if (permit.Status != PermitStatuses.Requested) throw new BusinessRuleException("This permit has already been decided.");
        if (permit.RequestedByUserId == UserId) throw new BusinessRuleException("Someone other than the requester must authorise the permit.");
        if (request.Approve)
        {
            var left = await dbContext.WorkPermitChecks.CountAsync(x => x.PermitId == id && !x.Done, cancellationToken);
            if (left > 0) throw new BusinessRuleException($"Tick the precautions first ({left} left).");
            if (permit.PermitType == PermitTypes.ConfinedSpace && string.IsNullOrWhiteSpace(request.GasTest ?? permit.GasTest))
                throw new BusinessRuleException("Record the gas test before authorising confined space entry.");
            if (permit.ValidTo <= DateTime.UtcNow) throw new BusinessRuleException("The permit's time has passed. Request a new one.");
            permit.Status = PermitStatuses.Approved;
            permit.GasTest = Clean(request.GasTest) ?? permit.GasTest;
        }
        else
        {
            permit.Status = PermitStatuses.Rejected;
            permit.DecisionNote = Clean(request.Note) ?? throw new BusinessRuleException("Say why the permit is refused.");
        }

        permit.ApprovedByUserId = UserId;
        permit.ApprovedByName = Actor.Name;
        permit.ApprovedAt = DateTime.UtcNow;
        permit.DecisionNote = Clean(request.Note) ?? permit.DecisionNote;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await PermitDetailAsync(id, cancellationToken));
    }

    /// <summary>Hands the permit back: work finished or stopped, area safe, locks off.</summary>
    [HttpPost("permits/{id:guid}/close")]
    public async Task<ActionResult<PermitDetail>> Close(Guid id, [FromBody] PermitCloseRequest request, CancellationToken cancellationToken)
    {
        EnsureCanWork();
        var permit = await LoadPermitAsync(id, cancellationToken);
        if (permit.Status != PermitStatuses.Approved) throw new BusinessRuleException("Only an authorised permit is closed out.");
        var locks = await dbContext.LotoLocks.CountAsync(x => x.WorkOrderId == permit.WorkOrderId && x.RemovedAt == null, cancellationToken);
        if (locks > 0) throw new BusinessRuleException($"Remove the {locks} isolation lock{(locks == 1 ? "" : "s")} before closing the permit.");
        permit.Status = PermitStatuses.Closed;
        permit.ClosedAt = DateTime.UtcNow;
        permit.ClosedByName = Actor.Name;
        permit.CloseOutNotes = Clean(request.Notes) ?? throw new BusinessRuleException("Say how the area was left (work finished, guards back, area clean).");
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await PermitDetailAsync(id, cancellationToken));
    }

    [HttpPost("permits/{id:guid}/cancel")]
    public async Task<ActionResult<PermitDetail>> Cancel(Guid id, [FromBody] PermitCloseRequest request, CancellationToken cancellationToken)
    {
        EnsureCanWork();
        var permit = await LoadPermitAsync(id, cancellationToken);
        if (permit.Status != PermitStatuses.Requested) throw new BusinessRuleException("Only a permit not yet authorised can be cancelled; close out an authorised one.");
        permit.Status = PermitStatuses.Cancelled;
        permit.CloseOutNotes = Clean(request.Notes);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await PermitDetailAsync(id, cancellationToken));
    }

    /* ── Isolation points and locks ──────────────────────────────────────── */

    [HttpGet("assets/{assetId:guid}/isolation-points")]
    public async Task<ActionResult<IReadOnlyCollection<IsolationPointDto>>> IsolationPoints(Guid assetId, CancellationToken cancellationToken)
    {
        EnsureCanView();
        await LoadAssetAsync(assetId, cancellationToken);
        return Ok(await dbContext.IsolationPoints.Where(x => x.AssetId == assetId && x.IsActive).OrderBy(x => x.Sequence).ThenBy(x => x.Code)
            .Select(x => new IsolationPointDto(x.Id, x.AssetId, x.Code, x.EnergyType, x.Description, x.Location, x.Method)).ToListAsync(cancellationToken));
    }

    [HttpPost("assets/{assetId:guid}/isolation-points")]
    public async Task<ActionResult<IsolationPointDto>> AddIsolationPoint(Guid assetId, [FromBody] SaveIsolationPointRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        await LoadAssetAsync(assetId, cancellationToken);
        var point = new IsolationPoint { TenantId = TenantId, AssetId = assetId, Sequence = await dbContext.IsolationPoints.CountAsync(x => x.AssetId == assetId, cancellationToken) };
        Apply(point, request);
        if (await dbContext.IsolationPoints.AnyAsync(x => x.AssetId == assetId && x.Code == point.Code, cancellationToken)) throw new BusinessRuleException("This asset already has a point with that code.");
        dbContext.IsolationPoints.Add(point);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new IsolationPointDto(point.Id, point.AssetId, point.Code, point.EnergyType, point.Description, point.Location, point.Method));
    }

    [HttpPut("isolation-points/{id:guid}")]
    public async Task<ActionResult<IsolationPointDto>> UpdateIsolationPoint(Guid id, [FromBody] SaveIsolationPointRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var point = await dbContext.IsolationPoints.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("The isolation point was not found.");
        await LoadAssetAsync(point.AssetId, cancellationToken);
        Apply(point, request);
        point.IsActive = request.IsActive ?? point.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new IsolationPointDto(point.Id, point.AssetId, point.Code, point.EnergyType, point.Description, point.Location, point.Method));
    }

    /// <summary>Records a lock put on an isolation point for a job, after proving zero energy.</summary>
    [HttpPost("locks")]
    public async Task<ActionResult<LotoLockDto>> ApplyLock([FromBody] ApplyLockRequest request, CancellationToken cancellationToken)
    {
        EnsureCanWork();
        var job = await LoadJobAsync(request.WorkOrderId, cancellationToken);
        if (Finished.Contains(job.Status)) throw new BusinessRuleException("The job is already finished.");
        var point = await dbContext.IsolationPoints.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == request.IsolationPointId && x.IsActive, cancellationToken)
            ?? throw new BusinessRuleException("The isolation point was not found.");
        if (job.AssetId is { } assetId && point.AssetId != assetId && !await IsComponentOfAsync(point.AssetId, assetId, cancellationToken))
            throw new BusinessRuleException("That isolation point belongs to another asset.");
        if (string.IsNullOrWhiteSpace(request.LockNumber)) throw new BusinessRuleException("Enter the number on the lock.");
        if (!request.ZeroEnergyVerified) throw new BusinessRuleException("Prove zero energy (try to start it, test dead) before recording the lock.");
        if (await dbContext.LotoLocks.AnyAsync(x => x.WorkOrderId == job.Id && x.IsolationPointId == point.Id && x.RemovedAt == null, cancellationToken))
            throw new BusinessRuleException("That point is already locked for this job.");
        var permitId = await dbContext.WorkPermits.Where(x => x.WorkOrderId == job.Id && x.Status == PermitStatuses.Approved).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken);
        var entry = new LotoLock
        {
            TenantId = TenantId, WorkOrderId = job.Id, PermitId = permitId, IsolationPointId = point.Id, LockNumber = request.LockNumber.Trim(),
            AppliedByName = Clean(request.AppliedByName) ?? Actor.Name, AppliedAt = DateTime.UtcNow, ZeroEnergyVerified = true,
        };
        dbContext.LotoLocks.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await LockAsync(entry.Id, cancellationToken));
    }

    [HttpPost("locks/{id:guid}/remove")]
    public async Task<ActionResult<LotoLockDto>> RemoveLock(Guid id, [FromBody] RemoveLockRequest request, CancellationToken cancellationToken)
    {
        EnsureCanWork();
        var entry = await dbContext.LotoLocks.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("The lock was not found.");
        await LoadJobAsync(entry.WorkOrderId, cancellationToken);
        if (entry.RemovedAt is not null) throw new BusinessRuleException("The lock is already off.");
        entry.RemovedAt = DateTime.UtcNow;
        entry.RemovedByName = Clean(request.RemovedByName) ?? Actor.Name;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await LockAsync(id, cancellationToken));
    }

    /// <summary>Every lock on right now, across the factory: what is isolated, for which job, by whom.</summary>
    [HttpGet("locks")]
    public async Task<ActionResult<IReadOnlyCollection<LotoLockDto>>> Locks([FromQuery] bool includeRemoved, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var nodes = (await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken)).Nodes?.ToList();
        var query = dbContext.LotoLocks.Where(x => x.TenantId == TenantId);
        if (!includeRemoved) query = query.Where(x => x.RemovedAt == null);
        if (nodes is not null) query = query.Where(x => x.WorkOrder!.OrgNodeId != null && nodes.Contains(x.WorkOrder.OrgNodeId.Value));
        return Ok(await query.OrderByDescending(x => x.AppliedAt).Take(300).Select(LockProjection).ToListAsync(cancellationToken));
    }

    /* ── Incidents ───────────────────────────────────────────────────────── */

    [HttpGet("incidents")]
    public async Task<ActionResult<IReadOnlyCollection<IncidentDto>>> Incidents([FromQuery] string? kind, [FromQuery] string? status, [FromQuery] Guid? orgNodeId, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var query = await ScopedIncidentsAsync(orgNodeId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(kind)) query = query.Where(x => x.Kind == kind);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        return Ok(await query.OrderByDescending(x => x.OccurredAt).Take(300).Select(IncidentProjection).ToListAsync(cancellationToken));
    }

    [HttpGet("incidents/summary")]
    public async Task<ActionResult<IncidentSummary>> IncidentSummary([FromQuery] Guid? orgNodeId, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var since = DateTime.UtcNow.AddMonths(-12);
        var rows = await (await ScopedIncidentsAsync(orgNodeId, cancellationToken)).Where(x => x.OccurredAt >= since)
            .Select(x => new { x.Kind, x.Status, x.DaysLost, x.OccurredAt }).ToListAsync(cancellationToken);
        var lastLostTime = await (await ScopedIncidentsAsync(orgNodeId, cancellationToken)).Where(x => x.Kind == IncidentKinds.Injury && x.DaysLost > 0)
            .MaxAsync(x => (DateTime?)x.OccurredAt, cancellationToken);
        return Ok(new IncidentSummary(
            rows.Count, rows.Count(x => x.Kind == IncidentKinds.Injury), rows.Count(x => x.Kind == IncidentKinds.NearMiss), rows.Count(x => x.Status != IncidentStatuses.Closed),
            rows.Sum(x => x.DaysLost ?? 0), lastLostTime is { } last ? (int)(DateTime.UtcNow.Date - last.Date).TotalDays : null));
    }

    [HttpPost("incidents")]
    public async Task<ActionResult<IncidentDto>> ReportIncident([FromBody] SaveIncidentRequest request, CancellationToken cancellationToken)
    {
        // Anyone at work may report: near misses are only caught if reporting is easy.
        userAccessService.EnsureTenantOperationalAccess();
        var incident = new Incident { TenantId = TenantId, ReportedByName = Actor.Name };
        await ApplyAsync(incident, request, cancellationToken);
        var count = await dbContext.Incidents.IgnoreQueryFilters().CountAsync(x => x.TenantId == TenantId, cancellationToken);
        incident.IncidentNumber = $"INC-{count + 1:000000}";
        dbContext.Incidents.Add(incident);
        await dbContext.SaveChangesAsync(cancellationToken);
        await NotifyIncidentAsync(incident, cancellationToken);
        return Ok(await IncidentAsync(incident.Id, cancellationToken));
    }

    [HttpPut("incidents/{id:guid}")]
    public async Task<ActionResult<IncidentDto>> UpdateIncident(Guid id, [FromBody] SaveIncidentRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var incident = await LoadIncidentAsync(id, cancellationToken);
        if (incident.Status == IncidentStatuses.Closed) throw new BusinessRuleException("A closed incident can't be changed.");
        await ApplyAsync(incident, request, cancellationToken);
        if (incident.Status == IncidentStatuses.Open && !string.IsNullOrWhiteSpace(incident.RootCause)) incident.Status = IncidentStatuses.Investigating;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await IncidentAsync(id, cancellationToken));
    }

    /// <summary>Raises the corrective job that puts the cause right (a safety job on the asset).</summary>
    [HttpPost("incidents/{id:guid}/corrective-job")]
    public async Task<ActionResult<IncidentDto>> RaiseCorrectiveJob(Guid id, [FromBody] CorrectiveJobRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var incident = await LoadIncidentAsync(id, cancellationToken);
        if (incident.CorrectiveWorkOrderId is not null) throw new BusinessRuleException("A corrective job is already raised.");
        var job = await workOrderService.CreateAsync(TenantId, Actor, new CreateFactoryWorkOrder(
            Clean(request.Title) ?? $"Make safe: {incident.Title}", FactoryWorkTypes.Safety, request.Priority ?? (incident.Severity is "Serious" or "Fatal" ? "Critical" : "High"),
            AssetId: incident.AssetId, OrgNodeId: incident.AssetId is null ? incident.OrgNodeId : null,
            Description: $"From incident {incident.IncidentNumber}: {incident.Description}", RequestedByName: Actor.Name), cancellationToken);
        incident.CorrectiveWorkOrderId = job.Id;
        if (incident.Status == IncidentStatuses.Open) incident.Status = IncidentStatuses.Investigating;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await IncidentAsync(id, cancellationToken));
    }

    [HttpPost("incidents/{id:guid}/close")]
    public async Task<ActionResult<IncidentDto>> CloseIncident(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var incident = await LoadIncidentAsync(id, cancellationToken);
        if (incident.Status == IncidentStatuses.Closed) throw new BusinessRuleException("Already closed.");
        if (string.IsNullOrWhiteSpace(incident.RootCause)) throw new BusinessRuleException("Record the root cause before closing.");
        if (incident.CorrectiveWorkOrderId is { } jobId
            && await dbContext.WorkOrders.AnyAsync(x => x.Id == jobId && !Finished.Contains(x.Status), cancellationToken))
            throw new BusinessRuleException("Finish the corrective job first.");
        incident.Status = IncidentStatuses.Closed;
        incident.ClosedAt = DateTime.UtcNow;
        incident.ClosedByName = Actor.Name;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await IncidentAsync(id, cancellationToken));
    }

    /* ── Training and PPE ────────────────────────────────────────────────── */

    [HttpGet("training")]
    public async Task<ActionResult<IReadOnlyCollection<TrainingDto>>> Training([FromQuery] Guid? technicianId, [FromQuery] bool contractors, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var query = dbContext.TrainingRecords.Where(x => x.TenantId == TenantId);
        if (technicianId.HasValue) query = query.Where(x => x.TechnicianId == technicianId);
        if (contractors) query = query.Where(x => x.IsContractor);
        return Ok(await query.OrderByDescending(x => x.CompletedOn).Take(500)
            .Select(x => new TrainingDto(x.Id, x.TechnicianId, x.PersonName, x.Company, x.IsContractor, x.Course, x.Category, x.CompletedOn, x.ExpiresOn, x.Trainer, x.Notes)).ToListAsync(cancellationToken));
    }

    [HttpPost("training")]
    public async Task<ActionResult<TrainingDto>> AddTraining([FromBody] SaveTrainingRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var person = await PersonAsync(request.TechnicianId, request.PersonName, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Course)) throw new BusinessRuleException("Name the course.");
        if (request.ExpiresOn is { } e && e.Date <= request.CompletedOn.Date) throw new BusinessRuleException("The expiry must be after the course date.");
        var record = new TrainingRecord
        {
            TenantId = TenantId, TechnicianId = request.TechnicianId, PersonName = person, Company = Clean(request.Company), IsContractor = request.IsContractor,
            Course = request.Course.Trim(), Category = Clean(request.Category) ?? "Induction",
            CompletedOn = DateTime.SpecifyKind(request.CompletedOn.Date, DateTimeKind.Utc),
            ExpiresOn = request.ExpiresOn is { } exp ? DateTime.SpecifyKind(exp.Date, DateTimeKind.Utc) : null,
            Trainer = Clean(request.Trainer), Notes = Clean(request.Notes),
        };
        dbContext.TrainingRecords.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new TrainingDto(record.Id, record.TechnicianId, record.PersonName, record.Company, record.IsContractor, record.Course, record.Category, record.CompletedOn, record.ExpiresOn, record.Trainer, record.Notes));
    }

    [HttpGet("ppe")]
    public async Task<ActionResult<IReadOnlyCollection<PpeDto>>> Ppe([FromQuery] Guid? technicianId, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var query = dbContext.PpeIssues.Where(x => x.TenantId == TenantId);
        if (technicianId.HasValue) query = query.Where(x => x.TechnicianId == technicianId);
        return Ok(await query.OrderByDescending(x => x.IssuedOn).Take(500)
            .Select(x => new PpeDto(x.Id, x.TechnicianId, x.PersonName, x.Item, x.Size, x.Quantity, x.IssuedOn, x.IssuedByName, x.ReplaceBy, x.Notes)).ToListAsync(cancellationToken));
    }

    [HttpPost("ppe")]
    public async Task<ActionResult<PpeDto>> IssuePpe([FromBody] SavePpeRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var person = await PersonAsync(request.TechnicianId, request.PersonName, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Item)) throw new BusinessRuleException("Say what was issued.");
        if (request.Quantity is < 1 or > 100) throw new BusinessRuleException("Quantity is between 1 and 100.");
        var issue = new PpeIssue
        {
            TenantId = TenantId, TechnicianId = request.TechnicianId, PersonName = person, Item = request.Item.Trim(), Size = Clean(request.Size), Quantity = request.Quantity,
            IssuedOn = DateTime.SpecifyKind((request.IssuedOn ?? DateTime.UtcNow).Date, DateTimeKind.Utc), IssuedByName = Actor.Name,
            ReplaceBy = request.ReplaceBy is { } r ? DateTime.SpecifyKind(r.Date, DateTimeKind.Utc) : null, Notes = Clean(request.Notes),
        };
        dbContext.PpeIssues.Add(issue);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new PpeDto(issue.Id, issue.TechnicianId, issue.PersonName, issue.Item, issue.Size, issue.Quantity, issue.IssuedOn, issue.IssuedByName, issue.ReplaceBy, issue.Notes));
    }

    /* ── helpers ─────────────────────────────────────────────────────────── */

    private async Task<string> PersonAsync(Guid? technicianId, string? name, CancellationToken cancellationToken)
    {
        if (technicianId is { } id)
        {
            return await dbContext.Technicians.Where(x => x.TenantId == TenantId && x.Id == id).Select(x => x.FullName).SingleOrDefaultAsync(cancellationToken)
                ?? throw new BusinessRuleException("The technician was not found.");
        }

        return Clean(name) ?? throw new BusinessRuleException("Choose the technician, or enter the person's name.");
    }

    private async Task ApplyAsync(Incident incident, SaveIncidentRequest request, CancellationToken cancellationToken)
    {
        incident.Kind = IncidentKinds.All.FirstOrDefault(x => string.Equals(x, request.Kind, StringComparison.OrdinalIgnoreCase)) ?? throw new BusinessRuleException("Choose what happened.");
        incident.Severity = IncidentSeverities.All.FirstOrDefault(x => string.Equals(x, request.Severity, StringComparison.OrdinalIgnoreCase)) ?? "Minor";
        incident.Title = Clean(request.Title) ?? throw new BusinessRuleException("Say briefly what happened.");
        incident.Description = Clean(request.Description) ?? incident.Title;
        incident.OccurredAt = DateTime.SpecifyKind(request.OccurredAt ?? DateTime.UtcNow, DateTimeKind.Utc);
        if (incident.OccurredAt > DateTime.UtcNow.AddMinutes(5)) throw new BusinessRuleException("The time can't be in the future.");
        Guid? node = request.OrgNodeId;
        if (request.AssetId is { } assetId)
        {
            node = (await LoadAssetAsync(assetId, cancellationToken)).OrgNodeId;
        }

        incident.OrgNodeId = node ?? throw new BusinessRuleException("Choose the asset, or where it happened.");
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, incident.OrgNodeId, cancellationToken);
        incident.AssetId = request.AssetId;
        if (request.WorkOrderId is { } jobId) await LoadJobAsync(jobId, cancellationToken);
        incident.WorkOrderId = request.WorkOrderId;
        incident.PersonsInvolved = Clean(request.PersonsInvolved);
        incident.ImmediateAction = Clean(request.ImmediateAction);
        incident.RootCause = Clean(request.RootCause) ?? incident.RootCause;
        incident.Lessons = Clean(request.Lessons) ?? incident.Lessons;
        incident.DaysLost = request.DaysLost is >= 0 ? request.DaysLost : null;
        incident.ReportedToAuthority = request.ReportedToAuthority;
    }

    private static void Apply(IsolationPoint point, SaveIsolationPointRequest request)
    {
        point.Code = Clean(request.Code) ?? throw new BusinessRuleException("Give the point a code, e.g. E1 or V2.");
        point.EnergyType = EnergyTypes.Find(request.EnergyType) ?? throw new BusinessRuleException($"Energy must be one of: {string.Join(", ", EnergyTypes.All)}.");
        point.Description = Clean(request.Description) ?? throw new BusinessRuleException("Describe the point, e.g. \"Main isolator MCC-2 panel 4\".");
        point.Location = Clean(request.Location);
        point.Method = Clean(request.Method);
    }

    private async Task<bool> IsComponentOfAsync(Guid childId, Guid parentId, CancellationToken cancellationToken)
    {
        Guid? current = childId;
        for (var i = 0; i < 10 && current is { } id; i++)
        {
            current = await dbContext.Assets.Where(x => x.Id == id).Select(x => x.ParentAssetId).SingleOrDefaultAsync(cancellationToken);
            if (current == parentId) return true;
        }

        return false;
    }

    private async Task NotifyPermitAsync(WorkPermit permit, WorkOrder job, CancellationToken cancellationToken)
    {
        if (notificationDispatcher is null || job.OrgNodeId is not { } node) return;
        var recipients = (await FactoryRecipients.SupervisorsAsync(dbContext, TenantId, node, cancellationToken)).Where(x => x != UserId).ToList();
        if (recipients.Count == 0) return;
        await notificationDispatcher.NotifyAsync(new NotificationRequest(TenantId, PermitKey, recipients,
            $"Permit {permit.PermitNumber} waiting for authorisation", $"{FactoryJobGuards.Describe(permit.PermitType)} permit for {job.WorkOrderNumber}: {job.Title}",
            $"/maintenance/safety?tab=permits&permit={permit.Id}", nameof(WorkPermit), permit.Id,
            new Dictionary<string, string?> { ["PermitNumber"] = permit.PermitNumber, ["RequestedBy"] = permit.RequestedByName, ["PermitType"] = FactoryJobGuards.Describe(permit.PermitType), ["WorkOrderNumber"] = job.WorkOrderNumber, ["Title"] = job.Title }),
            cancellationToken);
    }

    private async Task NotifyIncidentAsync(Incident incident, CancellationToken cancellationToken)
    {
        if (notificationDispatcher is null) return;
        var recipients = (await FactoryRecipients.SupervisorsAsync(dbContext, TenantId, incident.OrgNodeId, cancellationToken)).ToList();
        if (recipients.Count == 0) return;
        var place = await dbContext.OrgNodes.Where(x => x.Id == incident.OrgNodeId).Select(x => x.Name).SingleAsync(cancellationToken);
        var kind = incident.Kind == IncidentKinds.NearMiss ? "near miss" : incident.Kind.ToLowerInvariant();
        await notificationDispatcher.NotifyAsync(new NotificationRequest(TenantId, IncidentKey, recipients,
            $"{incident.IncidentNumber}: {kind} in {place}", incident.Title, $"/maintenance/safety?tab=incidents&incident={incident.Id}", nameof(Incident), incident.Id,
            new Dictionary<string, string?> { ["Kind"] = kind, ["Severity"] = incident.Severity, ["Title"] = incident.Title, ["IncidentNumber"] = incident.IncidentNumber, ["ReportedBy"] = incident.ReportedByName, ["Place"] = place, ["Description"] = incident.Description },
            incident.Kind == IncidentKinds.NearMiss ? NotificationSeverities.Warning : NotificationSeverities.Critical), cancellationToken);
    }

    private async Task<WorkOrder> LoadJobAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await dbContext.WorkOrders.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id && x.ClientId == null, cancellationToken)
            ?? throw new NotFoundException("The job was not found.");
        if (job.OrgNodeId is { } node) await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, node, cancellationToken);
        return job;
    }

    private async Task<Asset> LoadAssetAsync(Guid id, CancellationToken cancellationToken)
    {
        var asset = await dbContext.Assets.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id && x.ClientId == null, cancellationToken)
            ?? throw new NotFoundException("The asset was not found.");
        if (asset.OrgNodeId is { } node) await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, node, cancellationToken);
        return asset;
    }

    private async Task<WorkPermit> LoadPermitAsync(Guid id, CancellationToken cancellationToken)
    {
        var permit = await dbContext.WorkPermits.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("The permit was not found.");
        await LoadJobAsync(permit.WorkOrderId, cancellationToken);
        return permit;
    }

    private async Task<Incident> LoadIncidentAsync(Guid id, CancellationToken cancellationToken)
    {
        var incident = await dbContext.Incidents.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("The incident was not found.");
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, incident.OrgNodeId, cancellationToken);
        return incident;
    }

    private async Task<IQueryable<WorkPermit>> ScopedPermitsAsync(CancellationToken cancellationToken)
    {
        var nodes = (await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken)).Nodes?.ToList();
        var query = dbContext.WorkPermits.Where(x => x.TenantId == TenantId);
        return nodes is null ? query : query.Where(x => x.WorkOrder!.OrgNodeId != null && nodes.Contains(x.WorkOrder.OrgNodeId.Value));
    }

    private async Task<IQueryable<Incident>> ScopedIncidentsAsync(Guid? orgNodeId, CancellationToken cancellationToken)
    {
        var query = dbContext.Incidents.Where(x => x.TenantId == TenantId);
        var nodes = (await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken)).Nodes?.ToList();
        if (nodes is not null) query = query.Where(x => nodes.Contains(x.OrgNodeId));
        if (orgNodeId.HasValue)
        {
            var subtree = (await orgScopeService.GetSubtreeAsync(TenantId, orgNodeId.Value, cancellationToken)).ToList();
            query = query.Where(x => subtree.Contains(x.OrgNodeId));
        }

        return query;
    }

    private async Task<PermitDetail> PermitDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        await LoadPermitAsync(id, cancellationToken);
        var permit = await dbContext.WorkPermits.Where(x => x.Id == id).Select(PermitProjection).SingleAsync(cancellationToken);
        var extra = await dbContext.WorkPermits.Where(x => x.Id == id)
            .Select(x => new { x.Hazards, x.WorkDescription, x.IssuedToName, x.DecisionNote, x.GasTest, x.CloseOutNotes, x.ClosedByName, x.ClosedAt }).SingleAsync(cancellationToken);
        var checks = await dbContext.WorkPermitChecks.Where(x => x.PermitId == id).OrderBy(x => x.Sequence)
            .Select(x => new PermitCheckDto(x.Id, x.Prompt, x.Done, x.DoneByName)).ToListAsync(cancellationToken);
        var locks = await dbContext.LotoLocks.Where(x => x.WorkOrderId == permit.WorkOrderId).OrderBy(x => x.AppliedAt).Select(LockProjection).ToListAsync(cancellationToken);
        return new PermitDetail(permit, extra.Hazards, extra.WorkDescription, extra.IssuedToName, extra.DecisionNote, extra.GasTest, extra.CloseOutNotes, extra.ClosedByName, extra.ClosedAt, checks, locks);
    }

    private Task<LotoLockDto> LockAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.LotoLocks.Where(x => x.Id == id).Select(LockProjection).SingleAsync(cancellationToken);

    private Task<IncidentDto> IncidentAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Incidents.Where(x => x.Id == id).Select(IncidentProjection).SingleAsync(cancellationToken);

    private static readonly System.Linq.Expressions.Expression<Func<WorkPermit, PermitSummary>> PermitProjection = x => new PermitSummary(
        x.Id, x.PermitNumber, x.PermitType, x.Status, x.WorkOrderId, x.WorkOrder!.WorkOrderNumber, x.WorkOrder.Title,
        x.WorkOrder.Asset != null ? x.WorkOrder.Asset.AssetCode : null, x.ValidFrom, x.ValidTo, x.RequestedByName, x.ApprovedByName, x.ApprovedAt,
        x.Checks.Count, x.Checks.Count(c => c.Done));

    private static readonly System.Linq.Expressions.Expression<Func<LotoLock, LotoLockDto>> LockProjection = x => new LotoLockDto(
        x.Id, x.WorkOrderId, x.WorkOrder!.WorkOrderNumber, x.WorkOrder.Title, x.IsolationPointId, x.IsolationPoint!.Code, x.IsolationPoint.Description, x.IsolationPoint.EnergyType,
        x.IsolationPoint.Asset!.AssetCode, x.LockNumber, x.AppliedByName, x.AppliedAt, x.ZeroEnergyVerified, x.RemovedByName, x.RemovedAt);

    private static readonly System.Linq.Expressions.Expression<Func<Incident, IncidentDto>> IncidentProjection = x => new IncidentDto(
        x.Id, x.IncidentNumber, x.Kind, x.Severity, x.OccurredAt, x.OrgNodeId, x.OrgNode!.Name, x.AssetId, x.Asset != null ? x.Asset.AssetCode : null, x.Asset != null ? x.Asset.AssetName : null,
        x.WorkOrderId, x.Title, x.Description, x.PersonsInvolved, x.ImmediateAction, x.RootCause, x.Lessons, x.DaysLost, x.ReportedToAuthority, x.Status, x.ReportedByName,
        x.CorrectiveWorkOrderId, x.CorrectiveWorkOrder != null ? x.CorrectiveWorkOrder.WorkOrderNumber : null, x.CorrectiveWorkOrder != null ? x.CorrectiveWorkOrder.Status : null,
        x.ClosedAt, x.ClosedByName, x.CreatedAt);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void EnsureCanView() => userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanViewWorkOrders);

    private void EnsureCanManage() => userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageSettings, PermissionNames.CanAssignWorkOrders);

    /// <summary>Supervisors authorise permits.</summary>
    private void EnsureCanAuthorise() => userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanAssignWorkOrders);

    /// <summary>The people doing the work request permits and apply locks.</summary>
    private void EnsureCanWork() => userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanAssignWorkOrders, PermissionNames.CanCompleteWorkOrders);
}

public sealed record PermitSummary(
    Guid Id, string PermitNumber, string PermitType, string Status, Guid WorkOrderId, string WorkOrderNumber, string WorkOrderTitle, string? AssetCode,
    DateTime ValidFrom, DateTime ValidTo, string? RequestedByName, string? ApprovedByName, DateTime? ApprovedAt, int Checks, int ChecksDone);

public sealed record PermitCheckDto(Guid Id, string Prompt, bool Done, string? DoneByName);

public sealed record PermitDetail(
    PermitSummary Permit, string? Hazards, string? WorkDescription, string? IssuedToName, string? DecisionNote, string? GasTest,
    string? CloseOutNotes, string? ClosedByName, DateTime? ClosedAt, IReadOnlyCollection<PermitCheckDto> Checks, IReadOnlyCollection<LotoLockDto> Locks);

public sealed record RequestPermitRequest(Guid WorkOrderId, string? PermitType, [StringLength(2000)] string? Hazards, [StringLength(2000)] string? WorkDescription,
    DateTime? ValidFrom, DateTime? ValidTo, [StringLength(200)] string? IssuedToName);

public sealed record TickRequest(bool Done);

public sealed record PermitDecisionRequest(bool Approve, [StringLength(1000)] string? Note, [StringLength(500)] string? GasTest);

public sealed record PermitCloseRequest([StringLength(2000)] string? Notes);

public sealed record IsolationPointDto(Guid Id, Guid AssetId, string Code, string EnergyType, string Description, string? Location, string? Method);

public sealed record SaveIsolationPointRequest([StringLength(50)] string? Code, string? EnergyType, [StringLength(500)] string? Description, [StringLength(200)] string? Location, [StringLength(1000)] string? Method, bool? IsActive = null);

public sealed record LotoLockDto(
    Guid Id, Guid WorkOrderId, string WorkOrderNumber, string WorkOrderTitle, Guid IsolationPointId, string PointCode, string PointDescription, string EnergyType,
    string AssetCode, string LockNumber, string AppliedByName, DateTime AppliedAt, bool ZeroEnergyVerified, string? RemovedByName, DateTime? RemovedAt);

public sealed record ApplyLockRequest(Guid WorkOrderId, Guid IsolationPointId, [StringLength(50)] string? LockNumber, bool ZeroEnergyVerified, [StringLength(200)] string? AppliedByName = null);

public sealed record RemoveLockRequest([StringLength(200)] string? RemovedByName = null);

public sealed record IncidentDto(
    Guid Id, string IncidentNumber, string Kind, string Severity, DateTime OccurredAt, Guid OrgNodeId, string? OrgNodeName, Guid? AssetId, string? AssetCode, string? AssetName,
    Guid? WorkOrderId, string Title, string Description, string? PersonsInvolved, string? ImmediateAction, string? RootCause, string? Lessons, int? DaysLost,
    bool ReportedToAuthority, string Status, string ReportedByName, Guid? CorrectiveWorkOrderId, string? CorrectiveWorkOrderNumber, string? CorrectiveWorkOrderStatus,
    DateTime? ClosedAt, string? ClosedByName, DateTime CreatedAt);

public sealed record IncidentSummary(int Last12Months, int Injuries, int NearMisses, int Open, int DaysLost, int? DaysSinceLostTimeInjury);

public sealed record SaveIncidentRequest(
    string? Kind, string? Severity, DateTime? OccurredAt, Guid? OrgNodeId, Guid? AssetId, Guid? WorkOrderId, [StringLength(200)] string? Title, [StringLength(4000)] string? Description,
    [StringLength(1000)] string? PersonsInvolved, [StringLength(2000)] string? ImmediateAction, [StringLength(2000)] string? RootCause, [StringLength(2000)] string? Lessons,
    int? DaysLost, bool ReportedToAuthority = false);

public sealed record CorrectiveJobRequest([StringLength(250)] string? Title, string? Priority);

public sealed record TrainingDto(Guid Id, Guid? TechnicianId, string PersonName, string? Company, bool IsContractor, string Course, string Category, DateTime CompletedOn, DateTime? ExpiresOn, string? Trainer, string? Notes);

public sealed record SaveTrainingRequest(Guid? TechnicianId, [StringLength(200)] string? PersonName, [StringLength(200)] string? Company, bool IsContractor, [StringLength(200)] string? Course,
    [StringLength(50)] string? Category, DateTime CompletedOn, DateTime? ExpiresOn, [StringLength(200)] string? Trainer, [StringLength(1000)] string? Notes);

public sealed record PpeDto(Guid Id, Guid? TechnicianId, string PersonName, string Item, string? Size, int Quantity, DateTime IssuedOn, string? IssuedByName, DateTime? ReplaceBy, string? Notes);

public sealed record SavePpeRequest(Guid? TechnicianId, [StringLength(200)] string? PersonName, [StringLength(200)] string? Item, [StringLength(20)] string? Size, int Quantity, DateTime? IssuedOn, DateTime? ReplaceBy, [StringLength(1000)] string? Notes);
