using System.IO.Compression;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// Service-report queue: completed work orders moving through the report lifecycle
/// (AwaitingGeneration → Draft → UnderReview → Approved → Sent → Acknowledged).
/// Absolute route templates are used so these actions escape the controller's
/// {workOrderId}-scoped prefix.
/// </summary>
public sealed partial class WorkOrderExecutionController
{
    internal static class ReportStatuses
    {
        public const string AwaitingGeneration = "AwaitingGeneration";
        public const string Draft = "Draft";
        public const string UnderReview = "UnderReview";
        public const string Approved = "Approved";
        public const string Sent = "Sent";
        public const string Acknowledged = "Acknowledged";
    }

    // ── Queue listing ────────────────────────────────────────────────────────

    [HttpGet("/api/workorders/report-queue")]
    public async Task<ActionResult<ReportQueueResponse>> GetReportQueue(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        userAccessService.EnsureTenantOperationalAccess();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var completed = await dbContext.WorkOrders
            .Include(x => x.Client)
            .Include(x => x.Site)
            .Include(x => x.Asset)
            .Include(x => x.AssignedTechnician)
            .Include(x => x.LeadTechnician)
            .Include(x => x.TechnicianAssignments).ThenInclude(x => x.Technician)
            .Where(x => x.TenantId == TenantId)
            .Where(x => x.Status == WorkOrderStatuses.Completed || x.Status == WorkOrderStatuses.Closed || x.CompletedAt != null)
            .WhereAccessible(scope, x => x.BranchId)
            .ToListAsync(cancellationToken);

        var rows = completed
            .Select(BuildQueueRow)
            .OrderByDescending(x => x.CompletedAt ?? DateTime.MinValue)
            .ToList();

        var counts = new ReportQueueCounts(
            AwaitingGeneration: rows.Count(x => x.ReportStatus == ReportStatuses.AwaitingGeneration),
            UnderReview: rows.Count(x => x.ReportStatus == ReportStatuses.UnderReview),
            ApprovedNotSent: rows.Count(x => x.ReportStatus == ReportStatuses.Approved),
            SentNotAcknowledged: rows.Count(x => x.ReportStatus == ReportStatuses.Sent));

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
            rows = rows.Where(x => string.Equals(x.ReportStatus, status.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

        var total = rows.Count;
        var pageItems = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Ok(new ReportQueueResponse(pageItems, counts, total, page, pageSize));
    }

    // ── Single-item transitions ──────────────────────────────────────────────

    [HttpPost("/api/workorders/{id:guid}/report/generate")]
    public async Task<ActionResult<ReportQueueRow>> GenerateQueuedReport(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var workOrder = await LoadReportWorkOrderAsync(id, cancellationToken);
        await EnsureReportGeneratedAsync(workOrder, cancellationToken);
        return Ok(BuildQueueRow(workOrder));
    }

    [HttpPost("/api/workorders/{id:guid}/report/approve")]
    public async Task<ActionResult<ReportQueueRow>> ApproveQueuedReport(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanCompleteWorkOrders);
        var workOrder = await LoadReportWorkOrderAsync(id, cancellationToken);

        var current = DeriveReportStatus(workOrder);
        if (current is not (ReportStatuses.Draft or ReportStatuses.UnderReview))
            throw new BusinessRuleException($"Only draft or under-review reports can be approved (current: {current}).");

        workOrder.ReportStatus = ReportStatuses.Approved;
        workOrder.ReportApprovedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "workorder.report.approved", nameof(WorkOrder), workOrder.Id.ToString(), $"Approved service report {workOrder.ReportNumber} for {workOrder.WorkOrderNumber}", cancellationToken);
        await TryAutoSendApprovedReportAsync(workOrder, cancellationToken);

        return Ok(BuildQueueRow(workOrder));
    }

    /// <summary>
    /// Settings → Operational Defaults can ask for approved reports to go straight to the client. A
    /// report that cannot be sent (no client contact email, template disabled) simply stays Approved
    /// for a manual send, and the reason is kept in the audit log.
    /// </summary>
    private async Task TryAutoSendApprovedReportAsync(WorkOrder workOrder, CancellationToken cancellationToken)
    {
        var autoSend = await dbContext.TenantOperationsPolicies
            .Where(x => x.TenantId == TenantId)
            .Select(x => x.AutoSendServiceReportOnApproval)
            .FirstOrDefaultAsync(cancellationToken);
        if (!autoSend) return;

        try
        {
            await SendReportAsync(workOrder, cancellationToken);
        }
        catch (BusinessRuleException ex)
        {
            await auditLogService.LogAsync(TenantId, UserId, "workorder.report.autosend-skipped", nameof(WorkOrder), workOrder.Id.ToString(), $"Service report {workOrder.ReportNumber} was approved but not sent automatically: {ex.Message}", cancellationToken);
        }
    }

    // ── Editing the report's wording ─────────────────────────────────────────

    /// <summary>The report's editable text: the job's own wording next to any office edit.</summary>
    [HttpGet("/api/workorders/{id:guid}/report/content")]
    public async Task<ActionResult<ReportContentResponse>> GetReportContent(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        await EnsureAccessibleAsync(id, cancellationToken);
        var workOrder = await LoadReportWorkOrderAsync(id, cancellationToken);
        return Ok(await BuildReportContentAsync(workOrder, cancellationToken));
    }

    /// <summary>
    /// Saves edits to the report's wording. The job's own description and the technician's notes are
    /// left as they were; a blank field (or one matching the original) goes back to the job's text.
    /// An approved report needs approving again after an edit; a sent or acknowledged one is locked.
    /// </summary>
    [HttpPut("/api/workorders/{id:guid}/report/content")]
    public async Task<ActionResult<ReportContentResponse>> UpdateReportContent(Guid id, [FromBody] UpdateReportContentRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanCompleteWorkOrders);
        var workOrder = await LoadReportWorkOrderAsync(id, cancellationToken);
        var status = DeriveReportStatus(workOrder);
        if (ReportLockedReason(status) is { } locked)
            throw new BusinessRuleException(locked);

        static string? Edit(string? value, string? original)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed == original?.Trim()) return null;
            if (trimmed.Length > 4000) throw new BusinessRuleException("Each report section can be at most 4000 characters.");
            return trimmed;
        }

        var problem = Edit(request.ReportedProblem, workOrder.Description);
        var findings = Edit(request.Findings, workOrder.JobCardNotes);
        var workDone = Edit(request.WorkDone, workOrder.WorkDoneNotes);
        if (problem == workOrder.ReportProblemText && findings == workOrder.ReportFindingsText && workDone == workOrder.ReportWorkDoneText)
            return Ok(await BuildReportContentAsync(workOrder, cancellationToken));

        workOrder.ReportProblemText = problem;
        workOrder.ReportFindingsText = findings;
        workOrder.ReportWorkDoneText = workDone;
        workOrder.ReportEditedAt = DateTime.UtcNow;
        workOrder.ReportEditedByUserId = UserId;

        var reopened = status == ReportStatuses.Approved;
        if (reopened)
        {
            workOrder.ReportStatus = ReportStatuses.Draft;
            workOrder.ReportApprovedAt = null;
        }

        dbContext.WorkOrderEvents.Add(new WorkOrderEvent
        {
            TenantId = TenantId,
            WorkOrderId = workOrder.Id,
            ActorUserId = UserId,
            EventType = "ReportEdited",
            Status = WorkOrderStatuses.Normalize(workOrder.Status),
            Message = reopened ? "Edited the service report; it needs approving again." : "Edited the service report.",
            OccurredAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "workorder.report.edited", nameof(WorkOrder), workOrder.Id.ToString(),
            $"Edited service report wording for {workOrder.WorkOrderNumber}{(reopened ? " (approval cleared)" : string.Empty)}", cancellationToken);

        return Ok(await BuildReportContentAsync(workOrder, cancellationToken));
    }

    private static string? ReportLockedReason(string status) => status switch
    {
        ReportStatuses.Sent => "This report has already been sent to the client and can no longer be edited.",
        ReportStatuses.Acknowledged => "This report has been acknowledged by the client and can no longer be edited.",
        _ => null
    };

    private async Task<ReportContentResponse> BuildReportContentAsync(WorkOrder workOrder, CancellationToken cancellationToken)
    {
        var status = DeriveReportStatus(workOrder);
        var editedBy = workOrder.ReportEditedByUserId is { } editorId
            ? await dbContext.Users.IgnoreQueryFilters().Where(x => x.Id == editorId).Select(x => x.FullName).FirstOrDefaultAsync(cancellationToken)
            : null;
        return new ReportContentResponse(
            status,
            ReportLockedReason(status),
            new ReportTextField(workOrder.Description, workOrder.ReportProblemText),
            new ReportTextField(workOrder.JobCardNotes, workOrder.ReportFindingsText),
            new ReportTextField(workOrder.WorkDoneNotes, workOrder.ReportWorkDoneText),
            workOrder.ReportEditedAt,
            editedBy);
    }

    [HttpPost("/api/workorders/{id:guid}/report/send")]
    public async Task<ActionResult<ReportQueueRow>> SendQueuedReport(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanCompleteWorkOrders);
        var workOrder = await LoadReportWorkOrderAsync(id, cancellationToken);
        await SendReportAsync(workOrder, cancellationToken);
        return Ok(BuildQueueRow(workOrder));
    }

    // ── Batch transitions ────────────────────────────────────────────────────

    [HttpPost("/api/workorders/report/generate-batch")]
    public async Task<ActionResult<ReportBatchResult>> GenerateBatch([FromBody] ReportBatchRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var workOrders = await LoadReportWorkOrdersAsync(request.Ids, cancellationToken);

        var processed = 0;
        foreach (var workOrder in workOrders)
        {
            if (DeriveReportStatus(workOrder) == ReportStatuses.AwaitingGeneration)
            {
                await EnsureReportGeneratedAsync(workOrder, cancellationToken);
                processed++;
            }
        }
        return Ok(new ReportBatchResult(processed, workOrders.Count));
    }

    [HttpPost("/api/workorders/report/send-batch")]
    public async Task<ActionResult<ReportBatchResult>> SendBatch([FromBody] ReportBatchRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanCompleteWorkOrders);
        var workOrders = await LoadReportWorkOrdersAsync(request.Ids, cancellationToken);

        var processed = 0;
        foreach (var workOrder in workOrders)
        {
            if (DeriveReportStatus(workOrder) == ReportStatuses.Approved)
            {
                // One client without a contact email must not block the rest of the batch; it stays
                // Approved and shows up as not processed.
                try
                {
                    await SendReportAsync(workOrder, cancellationToken);
                    processed++;
                }
                catch (BusinessRuleException)
                {
                }
            }
        }
        return Ok(new ReportBatchResult(processed, workOrders.Count));
    }

    // ── Bulk PDF download ────────────────────────────────────────────────────

    [HttpGet("/api/workorders/report/download-zip")]
    public async Task<IActionResult> DownloadReportsZip([FromQuery] string ids, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();

        var parsedIds = (ids ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => Guid.TryParse(x, out var g) ? g : (Guid?)null)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();

        if (parsedIds.Count == 0)
            throw new BusinessRuleException("No work orders selected.");

        var workOrders = await LoadReportWorkOrdersAsync(parsedIds, cancellationToken);
        if (workOrders.Count == 0)
            throw new NotFoundException("No matching work orders found.");

        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var workOrder in workOrders)
            {
                var model = await BuildPdfModelAsync(workOrder.Id, cancellationToken);
                var pdf = pdfRenderer.RenderWorkOrderReportPdf(model);
                var entry = archive.CreateEntry($"{workOrder.WorkOrderNumber}-service-report.pdf", CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(pdf, cancellationToken);
            }
        }

        return File(memory.ToArray(), "application/zip", $"service-reports-{DateTime.UtcNow:yyyyMMdd-HHmm}.zip");
    }

    // ── Shared helpers ───────────────────────────────────────────────────────

    /// <summary>Assigns an SR number and moves an awaiting report into Draft. Idempotent.</summary>
    private async Task EnsureReportGeneratedAsync(WorkOrder workOrder, CancellationToken cancellationToken)
    {
        var changed = false;
        if (string.IsNullOrWhiteSpace(workOrder.ReportNumber))
        {
            workOrder.ReportNumber = await documentNumberingService.GenerateAsync(TenantId, workOrder.BranchId, DocumentTypes.ServiceReport, cancellationToken);
            changed = true;
        }
        if (string.IsNullOrWhiteSpace(workOrder.ReportStatus) || workOrder.ReportStatus == ReportStatuses.AwaitingGeneration)
        {
            workOrder.ReportStatus = ReportStatuses.Draft;
            workOrder.ReportGeneratedAt = DateTime.UtcNow;
            changed = true;
        }
        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditLogService.LogAsync(TenantId, UserId, "workorder.report.generated", nameof(WorkOrder), workOrder.Id.ToString(), $"Generated service report {workOrder.ReportNumber} for {workOrder.WorkOrderNumber}", cancellationToken);
        }
    }

    private async Task SendReportAsync(WorkOrder workOrder, CancellationToken cancellationToken)
    {
        if (DeriveReportStatus(workOrder) != ReportStatuses.Approved)
            throw new BusinessRuleException("Only approved reports can be sent.");

        // "Sent" used to be a status flip only — nothing ever reached the client. Queue the PDF to the
        // client's saved contact first, so a missing contact or disabled template leaves it Approved.
        var pdf = pdfRenderer.RenderWorkOrderReportPdf(await BuildPdfModelAsync(workOrder.Id, cancellationToken));
        var recipient = await serviceReportDeliveryService.QueueAsync(TenantId, workOrder.Id, pdf, UserId, cancellationToken);

        workOrder.ReportStatus = ReportStatuses.Sent;
        workOrder.ReportSentAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "workorder.report.sent", nameof(WorkOrder), workOrder.Id.ToString(), $"Sent service report {workOrder.ReportNumber} for {workOrder.WorkOrderNumber} to {recipient}", cancellationToken);
    }

    /// <summary>Effective display status: acknowledgement wins, then stored status, else awaiting generation.</summary>
    private static string DeriveReportStatus(WorkOrder workOrder)
    {
        if (!string.IsNullOrWhiteSpace(workOrder.AcknowledgedByName) || workOrder.ReportStatus == ReportStatuses.Acknowledged)
            return ReportStatuses.Acknowledged;
        return string.IsNullOrWhiteSpace(workOrder.ReportStatus) ? ReportStatuses.AwaitingGeneration : workOrder.ReportStatus;
    }

    private ReportQueueRow BuildQueueRow(WorkOrder workOrder)
    {
        var technician = workOrder.TechnicianAssignments
            .OrderByDescending(x => x.IsLead).ThenBy(x => x.AssignedAt)
            .Select(x => x.Technician?.FullName)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
            ?? workOrder.LeadTechnician?.FullName
            ?? workOrder.AssignedTechnician?.FullName
            ?? "Unassigned";

        int? daysSince = workOrder.CompletedAt is { } completed
            ? Math.Max(0, (int)(DateTime.UtcNow.Date - completed.Date).TotalDays)
            : null;

        return new ReportQueueRow(
            WorkOrderId: workOrder.Id,
            SrNo: workOrder.ReportNumber,
            WoNo: workOrder.WorkOrderNumber,
            Client: workOrder.Client?.ClientName ?? "—",
            Site: workOrder.Site?.SiteName ?? "—",
            Asset: workOrder.Asset?.AssetName ?? "—",
            Technician: technician,
            CompletedAt: workOrder.CompletedAt,
            ReportStatus: DeriveReportStatus(workOrder),
            DaysSinceCompletion: daysSince);
    }

    private async Task<WorkOrder> LoadReportWorkOrderAsync(Guid id, CancellationToken cancellationToken)
    {
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        return await dbContext.WorkOrders
            .Include(x => x.Client)
            .Include(x => x.Site)
            .Include(x => x.Asset)
            .Include(x => x.AssignedTechnician)
            .Include(x => x.LeadTechnician)
            .Include(x => x.TechnicianAssignments).ThenInclude(x => x.Technician)
            .Where(x => x.TenantId == TenantId && x.Id == id)
            .WhereAccessible(scope, x => x.BranchId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Work order not found.");
    }

    private async Task<List<WorkOrder>> LoadReportWorkOrdersAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids is null || ids.Count == 0)
            throw new BusinessRuleException("No work orders selected.");

        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        return await dbContext.WorkOrders
            .Include(x => x.Client)
            .Include(x => x.Site)
            .Include(x => x.Asset)
            .Include(x => x.AssignedTechnician)
            .Include(x => x.LeadTechnician)
            .Include(x => x.TechnicianAssignments).ThenInclude(x => x.Technician)
            .Where(x => x.TenantId == TenantId && ids.Contains(x.Id))
            .WhereAccessible(scope, x => x.BranchId)
            .ToListAsync(cancellationToken);
    }
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public sealed record ReportQueueRow(
    Guid WorkOrderId, string? SrNo, string WoNo, string Client, string Site, string Asset,
    string Technician, DateTime? CompletedAt, string ReportStatus, int? DaysSinceCompletion);

public sealed record ReportQueueCounts(int AwaitingGeneration, int UnderReview, int ApprovedNotSent, int SentNotAcknowledged);

public sealed record ReportQueueResponse(
    IReadOnlyList<ReportQueueRow> Items, ReportQueueCounts Counts, int Total, int Page, int PageSize);

public sealed record ReportBatchRequest(IReadOnlyList<Guid> Ids);

public sealed record ReportBatchResult(int Processed, int Matched);

public sealed record UpdateReportContentRequest(string? ReportedProblem, string? Findings, string? WorkDone);

/// <summary>Original = the job's own text; Edited = the office's wording for the report (null = not edited).</summary>
public sealed record ReportTextField(string? Original, string? Edited);

public sealed record ReportContentResponse(
    string ReportStatus,
    string? LockedReason,
    ReportTextField ReportedProblem,
    ReportTextField Findings,
    ReportTextField WorkDone,
    DateTime? EditedAt,
    string? EditedByName);
