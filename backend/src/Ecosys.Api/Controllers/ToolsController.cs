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
/// The tool register: tools and test equipment checked out to technicians and returned, with
/// calibration and inspection records. Stores staff manage it; technicians see and confirm their own.
/// </summary>
[ApiController]
[Authorize]
[Route("api/tools")]
public sealed class ToolsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IBranchAccessService branchAccessService,
    ITenantTimeZoneService tenantTimeZoneService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    /// <summary>Calibration falling due within this many days is flagged as due soon.</summary>
    public const int CalibrationDueSoonDays = 14;

    /// <param name="status">InStore, CheckedOut, UnderRepair, Lost, Retired; omitted lists everything but retired tools.</param>
    /// <param name="attention">calibration (overdue or due soon), overdue-return, or unacknowledged.</param>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ToolListItemResponse>>> List(
        [FromQuery] string? status, [FromQuery] Guid? branchId, [FromQuery] Guid? technicianId,
        [FromQuery] string? category, [FromQuery] string? search, [FromQuery] string? attention,
        CancellationToken cancellationToken)
    {
        EnsureViewAccess();
        var today = await TodayAsync(cancellationToken);
        var query = await ScopedAsync(branchId, cancellationToken);

        if (string.Equals(status, "All", StringComparison.OrdinalIgnoreCase)) { }
        else if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status.Trim());
        else
            query = query.Where(x => x.Status != ToolStatuses.Retired);
        if (technicianId.HasValue)
            query = query.Where(x => x.CurrentAssignment != null && x.CurrentAssignment.TechnicianId == technicianId);
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(x => x.Category == category.Trim());
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(term) || x.ToolCode.ToLower().Contains(term)
                || (x.SerialNumber != null && x.SerialNumber.ToLower().Contains(term))
                || (x.CurrentAssignment != null && x.CurrentAssignment.Technician!.FullName.ToLower().Contains(term)));
        }

        var dueSoon = today.AddDays(CalibrationDueSoonDays);
        query = attention?.Trim().ToLowerInvariant() switch
        {
            null or "" => query,
            "calibration" => query.Where(x => x.Status != ToolStatuses.Retired && x.Status != ToolStatuses.Lost
                && x.NextCalibrationDue != null && x.NextCalibrationDue <= dueSoon),
            "overdue-return" => query.Where(x => x.CurrentAssignment != null && x.CurrentAssignment.ExpectedReturnAt != null
                && x.CurrentAssignment.ExpectedReturnAt < today),
            "unacknowledged" => query.Where(x => x.CurrentAssignment != null && x.CurrentAssignment.AcknowledgedAt == null),
            _ => throw new BusinessRuleException("Attention must be calibration, overdue-return or unacknowledged."),
        };

        var tools = await query.OrderBy(x => x.Name).ThenBy(x => x.ToolCode).Take(1000).ToListAsync(cancellationToken);
        return Ok(tools.Select(x => MapListItem(x, today)).ToList());
    }

    /// <summary>Counts behind the Tools page's attention banner.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<ToolSummaryResponse>> Summary([FromQuery] Guid? branchId, CancellationToken cancellationToken)
    {
        EnsureViewAccess();
        var today = await TodayAsync(cancellationToken);
        var dueSoon = today.AddDays(CalibrationDueSoonDays);
        var query = (await ScopedAsync(branchId, cancellationToken)).Where(x => x.Status != ToolStatuses.Retired);
        var inService = query.Where(x => x.Status != ToolStatuses.Lost);

        return Ok(new ToolSummaryResponse(
            await query.CountAsync(cancellationToken),
            await query.CountAsync(x => x.Status == ToolStatuses.InStore, cancellationToken),
            await query.CountAsync(x => x.Status == ToolStatuses.CheckedOut, cancellationToken),
            await query.CountAsync(x => x.Status == ToolStatuses.UnderRepair, cancellationToken),
            await query.CountAsync(x => x.Status == ToolStatuses.Lost, cancellationToken),
            await inService.CountAsync(x => x.NextCalibrationDue != null && x.NextCalibrationDue < today, cancellationToken),
            await inService.CountAsync(x => x.NextCalibrationDue != null && x.NextCalibrationDue >= today && x.NextCalibrationDue <= dueSoon, cancellationToken),
            await query.CountAsync(x => x.CurrentAssignment != null && x.CurrentAssignment.ExpectedReturnAt != null && x.CurrentAssignment.ExpectedReturnAt < today, cancellationToken),
            await query.CountAsync(x => x.CurrentAssignment != null && !x.CurrentAssignment.Technician!.IsActive, cancellationToken),
            await query.Where(x => x.Status == ToolStatuses.CheckedOut).SumAsync(x => x.PurchaseCost ?? 0, cancellationToken),
            await query.CountAsync(x => x.CurrentAssignment != null && x.CurrentAssignment.AcknowledgedAt == null, cancellationToken)));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ToolDetailResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        EnsureViewAccess();
        var tool = await LoadAsync(id, cancellationToken);
        return Ok(await MapDetailAsync(tool, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<ToolDetailResponse>> Create([FromBody] UpsertToolRequest request, CancellationToken cancellationToken)
    {
        EnsureManageAccess();
        var tool = new Tool { TenantId = TenantId };
        await ApplyAsync(tool, request, cancellationToken);
        dbContext.Tools.Add(tool);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "inventory.tool.created", nameof(Tool), tool.Id.ToString(), $"Tool {tool.ToolCode} {tool.Name} added", cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = tool.Id }, await MapDetailAsync(await LoadAsync(tool.Id, cancellationToken), cancellationToken));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ToolDetailResponse>> Update(Guid id, [FromBody] UpsertToolRequest request, CancellationToken cancellationToken)
    {
        EnsureManageAccess();
        var tool = await LoadAsync(id, cancellationToken);
        await ApplyAsync(tool, request, cancellationToken);
        tool.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await MapDetailAsync(tool, cancellationToken));
    }

    /// <summary>
    /// Hands a tool in store to a technician. A tool whose calibration is overdue is refused unless
    /// the caller explicitly overrides it, so an out-of-calibration meter is not used by accident.
    /// </summary>
    [HttpPost("{id:guid}/check-out")]
    public async Task<ActionResult<ToolDetailResponse>> CheckOut(Guid id, [FromBody] CheckOutToolRequest request, CancellationToken cancellationToken)
    {
        EnsureManageAccess();
        var tool = await LoadAsync(id, cancellationToken);
        if (tool.Status != ToolStatuses.InStore)
            throw new BusinessRuleException(tool.Status == ToolStatuses.CheckedOut
                ? $"{tool.Name} is already with {tool.CurrentAssignment?.Technician?.FullName ?? "a technician"}. Check it in first."
                : $"{tool.Name} is {Describe(tool.Status)} and cannot be checked out.");

        var today = await TodayAsync(cancellationToken);
        if (tool.NextCalibrationDue is { } due && due < today && !request.OverrideCalibration)
            throw new BusinessRuleException($"{tool.Name} was due for calibration on {due:dd MMM yyyy}. Record a calibration, or override to check it out anyway.");

        var technician = await dbContext.Technicians
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == request.TechnicianId, cancellationToken)
            ?? throw new BusinessRuleException("Technician was not found.");
        if (!technician.IsActive)
            throw new BusinessRuleException($"{technician.FullName} is inactive.");
        if (request.WorkOrderId is { } workOrderId
            && !await dbContext.WorkOrders.AnyAsync(x => x.TenantId == TenantId && x.Id == workOrderId, cancellationToken))
            throw new BusinessRuleException("Work order was not found.");
        if (request.ExpectedReturnAt is { } expected && expected.Date < today)
            throw new BusinessRuleException("The expected return date cannot be in the past.");

        var assignment = new ToolAssignment
        {
            TenantId = TenantId,
            ToolId = tool.Id,
            TechnicianId = technician.Id,
            WorkOrderId = request.WorkOrderId,
            CheckedOutAt = DateTime.UtcNow,
            CheckedOutByUserId = UserId,
            ExpectedReturnAt = request.ExpectedReturnAt is { } e ? DateTime.SpecifyKind(e.Date, DateTimeKind.Utc) : null,
            ConditionOut = NormalizeCondition(request.Condition) ?? tool.Condition,
            Notes = Blank(request.Notes),
        };
        dbContext.ToolAssignments.Add(assignment);
        tool.Status = ToolStatuses.CheckedOut;
        tool.Condition = assignment.ConditionOut;
        tool.CurrentAssignmentId = assignment.Id;
        tool.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "inventory.tool.checked_out", nameof(Tool), tool.Id.ToString(),
            $"{tool.ToolCode} {tool.Name} checked out to {technician.FullName}" + (request.OverrideCalibration && tool.NextCalibrationDue < today ? " (calibration overdue, overridden)" : string.Empty), cancellationToken);

        return Ok(await MapDetailAsync(await LoadAsync(tool.Id, cancellationToken), cancellationToken));
    }

    /// <summary>Takes a tool back. It returns to store, or straight to repair when it came back damaged and needs it.</summary>
    [HttpPost("{id:guid}/check-in")]
    public async Task<ActionResult<ToolDetailResponse>> CheckIn(Guid id, [FromBody] CheckInToolRequest request, CancellationToken cancellationToken)
    {
        EnsureManageAccess();
        var tool = await LoadAsync(id, cancellationToken);
        var assignment = tool.CurrentAssignment;
        if (tool.Status != ToolStatuses.CheckedOut || assignment is null)
            throw new BusinessRuleException($"{tool.Name} is not checked out.");

        var condition = NormalizeCondition(request.Condition) ?? throw new BusinessRuleException("Record the condition the tool came back in.");
        CloseAssignment(tool, condition, request.Notes);
        tool.Status = request.SendToRepair ? ToolStatuses.UnderRepair : ToolStatuses.InStore;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "inventory.tool.checked_in", nameof(Tool), tool.Id.ToString(),
            $"{tool.ToolCode} {tool.Name} returned by {assignment.Technician?.FullName} in {condition.ToLowerInvariant()} condition", cancellationToken);

        return Ok(await MapDetailAsync(await LoadAsync(tool.Id, cancellationToken), cancellationToken));
    }

    /// <summary>
    /// Moves a tool between in store, under repair, lost and retired. Marking a checked-out tool lost
    /// closes its check-out against the technician who had it, so the record shows where it went missing.
    /// </summary>
    [HttpPost("{id:guid}/status")]
    public async Task<ActionResult<ToolDetailResponse>> SetStatus(Guid id, [FromBody] SetToolStatusRequest request, CancellationToken cancellationToken)
    {
        EnsureManageAccess();
        var tool = await LoadAsync(id, cancellationToken);
        var target = new[] { ToolStatuses.InStore, ToolStatuses.UnderRepair, ToolStatuses.Lost, ToolStatuses.Retired }
            .FirstOrDefault(x => string.Equals(x, request.Status?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessRuleException("Status must be InStore, UnderRepair, Lost or Retired.");
        if (tool.Status == target)
            return Ok(await MapDetailAsync(tool, cancellationToken));
        if (tool.Status == ToolStatuses.CheckedOut && target != ToolStatuses.Lost)
            throw new BusinessRuleException($"{tool.Name} is checked out. Check it in first.");
        if (tool.Status == ToolStatuses.Retired)
            throw new BusinessRuleException($"{tool.Name} is retired.");
        var notes = Blank(request.Notes);
        if (target is ToolStatuses.Lost or ToolStatuses.Retired && notes is null)
            throw new BusinessRuleException($"Give a reason for marking the tool {Describe(target)}.");

        var holder = tool.CurrentAssignment?.Technician?.FullName;
        if (tool.Status == ToolStatuses.CheckedOut)
            CloseAssignment(tool, tool.Condition, $"Reported lost: {notes}");
        tool.Status = target;
        tool.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "inventory.tool.status", nameof(Tool), tool.Id.ToString(),
            $"{tool.ToolCode} {tool.Name} marked {Describe(target)}{(holder is null ? string.Empty : $" (was with {holder})")}{(notes is null ? string.Empty : $": {notes}")}", cancellationToken);

        return Ok(await MapDetailAsync(await LoadAsync(tool.Id, cancellationToken), cancellationToken));
    }

    /// <summary>Records a calibration or inspection and moves the next due date on.</summary>
    [HttpPost("{id:guid}/calibrations")]
    public async Task<ActionResult<ToolDetailResponse>> RecordCalibration(Guid id, [FromBody] RecordToolCalibrationRequest request, CancellationToken cancellationToken)
    {
        EnsureManageAccess();
        var tool = await LoadAsync(id, cancellationToken);
        var today = await TodayAsync(cancellationToken);
        var calibratedOn = DateTime.SpecifyKind(request.CalibratedOn.Date, DateTimeKind.Utc);
        if (calibratedOn > today)
            throw new BusinessRuleException("The calibration date cannot be in the future.");
        var result = string.Equals(request.Result, "Fail", StringComparison.OrdinalIgnoreCase) ? "Fail" : "Pass";
        DateTime? nextDue = request.NextDueOn is { } next
            ? DateTime.SpecifyKind(next.Date, DateTimeKind.Utc)
            : tool.CalibrationIntervalDays is { } days ? calibratedOn.AddDays(days) : null;
        if (nextDue <= calibratedOn)
            throw new BusinessRuleException("The next due date must be after the calibration date.");

        dbContext.ToolCalibrations.Add(new ToolCalibration
        {
            TenantId = TenantId,
            ToolId = tool.Id,
            CalibratedOn = calibratedOn,
            NextDueOn = result == "Pass" ? nextDue : null,
            CertificateNumber = Blank(request.CertificateNumber),
            PerformedBy = Blank(request.PerformedBy),
            Result = result,
            Notes = Blank(request.Notes),
            RecordedByUserId = UserId,
        });

        // Only a later record moves the dates: back-filling an old certificate must not reset them.
        if (tool.LastCalibratedOn is null || calibratedOn >= tool.LastCalibratedOn)
        {
            tool.LastCalibratedOn = calibratedOn;
            // A failed calibration leaves the tool already overdue, so it cannot go out until it passes.
            tool.NextCalibrationDue = result == "Pass" ? nextDue : calibratedOn.AddDays(-1);
            if (result == "Fail" && tool.Status == ToolStatuses.InStore)
                tool.Status = ToolStatuses.UnderRepair;
        }
        tool.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "inventory.tool.calibrated", nameof(Tool), tool.Id.ToString(),
            $"{tool.ToolCode} {tool.Name} calibration {result.ToLowerInvariant()} on {calibratedOn:dd MMM yyyy}", cancellationToken);

        return Ok(await MapDetailAsync(await LoadAsync(tool.Id, cancellationToken), cancellationToken));
    }

    /// <summary>The signed-in technician's tools: what they hold now, with their acknowledgement state.</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyCollection<ToolListItemResponse>>> Mine(CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var technicianId = await MyTechnicianIdAsync(cancellationToken);
        if (technicianId is null)
            return Ok(Array.Empty<ToolListItemResponse>());
        var today = await TodayAsync(cancellationToken);
        var tools = await WithIncludes(dbContext.Tools)
            .Where(x => x.TenantId == TenantId && x.CurrentAssignment != null && x.CurrentAssignment.TechnicianId == technicianId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return Ok(tools.Select(x => MapListItem(x, today)).ToList());
    }

    /// <summary>The technician holding a tool confirms they have it.</summary>
    [HttpPost("{id:guid}/acknowledge")]
    public async Task<IActionResult> Acknowledge(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var technicianId = await MyTechnicianIdAsync(cancellationToken);
        var assignment = await dbContext.ToolAssignments
            .Include(x => x.Tool)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ToolId == id && x.ReturnedAt == null, cancellationToken);
        if (assignment is null || technicianId is null || assignment.TechnicianId != technicianId)
            throw new NotFoundException("That tool is not checked out to you.");

        if (assignment.AcknowledgedAt is null)
        {
            assignment.AcknowledgedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditLogService.LogAsync(TenantId, UserId, "inventory.tool.acknowledged", nameof(Tool), id.ToString(),
                $"Receipt of {assignment.Tool?.ToolCode} {assignment.Tool?.Name} confirmed by the technician", cancellationToken);
        }

        return NoContent();
    }

    private void CloseAssignment(Tool tool, string condition, string? notes)
    {
        var assignment = tool.CurrentAssignment!;
        assignment.ReturnedAt = DateTime.UtcNow;
        assignment.ReturnedByUserId = UserId;
        assignment.ConditionIn = condition;
        assignment.ReturnNotes = Blank(notes);
        tool.Condition = condition;
        tool.CurrentAssignmentId = null;
        tool.CurrentAssignment = null;
        tool.UpdatedAt = DateTime.UtcNow;
    }

    private async Task ApplyAsync(Tool tool, UpsertToolRequest request, CancellationToken cancellationToken)
    {
        var code = Blank(request.ToolCode) ?? throw new BusinessRuleException("Tool code (asset tag) is required.");
        var name = Blank(request.Name) ?? throw new BusinessRuleException("Tool name is required.");
        if (request.PurchaseCost is < 0)
            throw new BusinessRuleException("Purchase cost cannot be negative.");
        if (request.CalibrationIntervalDays is <= 0 or > 3650)
            throw new BusinessRuleException("Calibration interval must be between 1 and 3650 days, or left blank.");
        var normalized = code.ToLower();
        if (await dbContext.Tools.AnyAsync(x => x.TenantId == TenantId && x.Id != tool.Id && x.ToolCode.ToLower() == normalized, cancellationToken))
            throw new BusinessRuleException($"Another tool already uses the code {code}.");
        if (request.HomeBranchId is { } branchId)
            await branchAccessService.EnsureCanAccessBranchAsync(TenantId, branchId, cancellationToken);

        tool.ToolCode = code;
        tool.Name = name;
        tool.Category = Blank(request.Category);
        tool.SerialNumber = Blank(request.SerialNumber);
        tool.Manufacturer = Blank(request.Manufacturer);
        tool.Model = Blank(request.Model);
        tool.PurchaseDate = request.PurchaseDate is { } bought ? DateTime.SpecifyKind(bought.Date, DateTimeKind.Utc) : null;
        tool.PurchaseCost = request.PurchaseCost;
        tool.HomeBranchId = request.HomeBranchId;
        tool.Notes = Blank(request.Notes);
        tool.Condition = NormalizeCondition(request.Condition) ?? tool.Condition;

        var intervalChanged = tool.CalibrationIntervalDays != request.CalibrationIntervalDays;
        tool.CalibrationIntervalDays = request.CalibrationIntervalDays;
        if (request.NextCalibrationDue is { } due)
            tool.NextCalibrationDue = DateTime.SpecifyKind(due.Date, DateTimeKind.Utc);
        else if (request.CalibrationIntervalDays is null)
            tool.NextCalibrationDue = null;
        else if (intervalChanged && tool.LastCalibratedOn is { } last)
            tool.NextCalibrationDue = last.AddDays(request.CalibrationIntervalDays.Value);
    }

    private async Task<IQueryable<Tool>> ScopedAsync(Guid? branchId, CancellationToken cancellationToken)
    {
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, branchId, cancellationToken);
        var query = WithIncludes(dbContext.Tools.AsNoTracking()).Where(x => x.TenantId == TenantId);
        // Tools with no home branch belong to the whole workspace and stay visible.
        if (!scope.HasConfiguredBranches)
            return query;
        if (scope.RequestedBranchId is { } requested)
            return query.Where(x => x.HomeBranchId == requested);
        var ids = scope.AccessibleBranchIds.ToList();
        return query.Where(x => x.HomeBranchId == null || ids.Contains(x.HomeBranchId.Value));
    }

    private static IQueryable<Tool> WithIncludes(IQueryable<Tool> query) =>
        query
            .Include(x => x.HomeBranch)
            .Include(x => x.CurrentAssignment)
                .ThenInclude(x => x!.Technician)
            .Include(x => x.CurrentAssignment)
                .ThenInclude(x => x!.WorkOrder);

    private async Task<Tool> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await WithIncludes(dbContext.Tools).SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
        ?? throw new NotFoundException("Tool was not found.");

    private async Task<Guid?> MyTechnicianIdAsync(CancellationToken cancellationToken) =>
        await dbContext.Technicians
            .Where(x => x.TenantId == TenantId && x.UserId == UserId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<DateTime> TodayAsync(CancellationToken cancellationToken) =>
        DateTime.SpecifyKind(await tenantTimeZoneService.TenantTodayAsync(TenantId, cancellationToken), DateTimeKind.Utc);

    private async Task<ToolDetailResponse> MapDetailAsync(Tool tool, CancellationToken cancellationToken)
    {
        var today = await TodayAsync(cancellationToken);
        var history = await dbContext.ToolAssignments.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.ToolId == tool.Id)
            .OrderByDescending(x => x.CheckedOutAt)
            .Take(100)
            .Select(x => new ToolAssignmentResponse(
                x.Id, x.TechnicianId, x.Technician != null ? x.Technician.FullName : null,
                x.WorkOrderId, x.WorkOrder != null ? x.WorkOrder.WorkOrderNumber : null,
                x.CheckedOutAt, x.CheckedOutByUser != null ? x.CheckedOutByUser.FullName : null, x.ExpectedReturnAt,
                x.ConditionOut, x.Notes, x.AcknowledgedAt, x.ReturnedAt,
                x.ReturnedByUser != null ? x.ReturnedByUser.FullName : null, x.ConditionIn, x.ReturnNotes))
            .ToListAsync(cancellationToken);
        var calibrations = await dbContext.ToolCalibrations.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.ToolId == tool.Id)
            .OrderByDescending(x => x.CalibratedOn).ThenByDescending(x => x.CreatedAt)
            .Select(x => new ToolCalibrationResponse(x.Id, x.CalibratedOn, x.NextDueOn, x.Result, x.CertificateNumber, x.PerformedBy, x.Notes))
            .ToListAsync(cancellationToken);
        return new ToolDetailResponse(MapListItem(tool, today), tool.Manufacturer, tool.Model, tool.PurchaseDate,
            tool.CalibrationIntervalDays, tool.LastCalibratedOn, tool.Notes, history, calibrations);
    }

    private static ToolListItemResponse MapListItem(Tool tool, DateTime today)
    {
        var assignment = tool.CurrentAssignment;
        return new ToolListItemResponse(
            tool.Id,
            tool.ToolCode,
            tool.Name,
            tool.Category,
            tool.SerialNumber,
            tool.Status,
            tool.Condition,
            tool.HomeBranchId,
            tool.HomeBranch?.Name,
            tool.PurchaseCost,
            tool.NextCalibrationDue,
            CalibrationState(tool, today),
            assignment?.TechnicianId,
            assignment?.Technician?.FullName,
            assignment?.CheckedOutAt,
            assignment?.ExpectedReturnAt,
            assignment?.ExpectedReturnAt is { } expected && expected < today,
            assignment is not null && assignment.AcknowledgedAt is null,
            assignment?.WorkOrderId,
            assignment?.WorkOrder?.WorkOrderNumber);
    }

    /// <summary>Overdue, DueSoon, Ok, or null when the tool needs no calibration.</summary>
    public static string? CalibrationState(Tool tool, DateTime today) =>
        tool.NextCalibrationDue is not { } due ? null
        : due < today ? "Overdue"
        : due <= today.AddDays(CalibrationDueSoonDays) ? "DueSoon"
        : "Ok";

    private static string? NormalizeCondition(string? condition) =>
        string.IsNullOrWhiteSpace(condition)
            ? null
            : ToolConditions.All.FirstOrDefault(x => string.Equals(x, condition.Trim(), StringComparison.OrdinalIgnoreCase))
              ?? throw new BusinessRuleException("Condition must be Good, Fair or Damaged.");

    private static string Describe(string status) => status switch
    {
        ToolStatuses.InStore => "in store",
        ToolStatuses.CheckedOut => "checked out",
        ToolStatuses.UnderRepair => "under repair",
        ToolStatuses.Lost => "lost",
        ToolStatuses.Retired => "retired",
        _ => status.ToLowerInvariant(),
    };

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void EnsureViewAccess() =>
        userAccessService.EnsureAdminOrAnyPermission(
            PermissionNames.CanViewMaterials, PermissionNames.CanManageMaterials, PermissionNames.CanApproveMaterials, PermissionNames.CanIssueMaterials);

    private void EnsureManageAccess() =>
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageMaterials, PermissionNames.CanIssueMaterials);
}

public sealed record UpsertToolRequest(
    string ToolCode, string Name, string? Category = null, string? SerialNumber = null, string? Manufacturer = null, string? Model = null,
    DateTime? PurchaseDate = null, decimal? PurchaseCost = null, Guid? HomeBranchId = null, int? CalibrationIntervalDays = null,
    DateTime? NextCalibrationDue = null, string? Condition = null, string? Notes = null);

/// <param name="OverrideCalibration">Check out a tool whose calibration is overdue anyway; the audit log records it.</param>
public sealed record CheckOutToolRequest(
    Guid TechnicianId, Guid? WorkOrderId = null, DateTime? ExpectedReturnAt = null, string? Condition = null, string? Notes = null,
    bool OverrideCalibration = false);

public sealed record CheckInToolRequest(string Condition, string? Notes = null, bool SendToRepair = false);

public sealed record SetToolStatusRequest(string Status, string? Notes = null);

/// <param name="NextDueOn">Defaults to the calibration date plus the tool's interval.</param>
public sealed record RecordToolCalibrationRequest(
    DateTime CalibratedOn, DateTime? NextDueOn = null, string? Result = "Pass", string? CertificateNumber = null, string? PerformedBy = null, string? Notes = null);

/// <param name="CalibrationState">Overdue, DueSoon (within 14 days), Ok, or null when not calibrated.</param>
public sealed record ToolListItemResponse(
    Guid Id, string ToolCode, string Name, string? Category, string? SerialNumber, string Status, string Condition,
    Guid? HomeBranchId, string? HomeBranchName, decimal? PurchaseCost, DateTime? NextCalibrationDue, string? CalibrationState,
    Guid? HolderTechnicianId, string? HolderName, DateTime? CheckedOutAt, DateTime? ExpectedReturnAt, bool ReturnOverdue,
    bool AwaitingAcknowledgement, Guid? WorkOrderId, string? WorkOrderNumber);

public sealed record ToolAssignmentResponse(
    Guid Id, Guid TechnicianId, string? TechnicianName, Guid? WorkOrderId, string? WorkOrderNumber,
    DateTime CheckedOutAt, string? CheckedOutByName, DateTime? ExpectedReturnAt, string ConditionOut, string? Notes,
    DateTime? AcknowledgedAt, DateTime? ReturnedAt, string? ReturnedByName, string? ConditionIn, string? ReturnNotes);

public sealed record ToolCalibrationResponse(
    Guid Id, DateTime CalibratedOn, DateTime? NextDueOn, string Result, string? CertificateNumber, string? PerformedBy, string? Notes);

public sealed record ToolDetailResponse(
    ToolListItemResponse Tool, string? Manufacturer, string? Model, DateTime? PurchaseDate, int? CalibrationIntervalDays,
    DateTime? LastCalibratedOn, string? Notes, IReadOnlyList<ToolAssignmentResponse> History, IReadOnlyList<ToolCalibrationResponse> Calibrations);

/// <param name="HeldByInactiveTechnicians">Tools still checked out to technicians who have been deactivated (leavers).</param>
/// <param name="ValueCheckedOut">Purchase value of the tools currently out with technicians.</param>
public sealed record ToolSummaryResponse(
    int Total, int InStore, int CheckedOut, int UnderRepair, int Lost,
    int CalibrationOverdue, int CalibrationDueSoon, int ReturnOverdue, int HeldByInactiveTechnicians, decimal ValueCheckedOut,
    int AwaitingAcknowledgement = 0);
