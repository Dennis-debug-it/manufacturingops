using System.Text.Json;
using System.Text.RegularExpressions;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/workorders/{workOrderId:guid}")]
public sealed partial class WorkOrderExecutionController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IBranchAccessService branchAccessService,
    IFileStorageService fileStorageService,
    IAuditLogService auditLogService,
    IStockLedgerService stockLedgerService,
    IWorkOrderAssignmentWorkflowService assignmentWorkflowService,
    IWorkOrderLifecycleService workOrderLifecycleService,
    IPdfRenderer pdfRenderer,
    IDocumentNumberingService documentNumberingService,
    IHttpClientFactory httpClientFactory) : TenantAwareControllerBase(tenantContext)
{
    private const string ProgressUpdateEventType = "ProgressUpdate";

    private static readonly HashSet<string> AllowedPhotoCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Before", "During", "After", "Defect", "Completion", "Spare Part", "Client Sign-off", "Checklist", "Other"
    };

    private static readonly HashSet<string> AllowedPhotoMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    private static readonly HashSet<string> AllowedSignatureTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Technician", "Client"
    };

    private const long MaxPhotoSizeBytes = 5 * 1024 * 1024;

    [HttpGet("execution")]
    public async Task<ActionResult<WorkOrderExecutionBundleResponse>> GetExecutionBundle(Guid workOrderId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        await EnsureAccessibleAsync(workOrderId, cancellationToken);
        return Ok(await BuildExecutionBundleAsync(workOrderId, cancellationToken));
    }

    [HttpPost("execution-notes")]
    public async Task<ActionResult<WorkOrderExecutionBundleResponse>> SaveExecutionNotes(Guid workOrderId, [FromBody] SaveWorkOrderExecutionNotesRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var workOrder = await LoadWorkOrderAsync(workOrderId, cancellationToken);

        if (request.Findings is not null)
        {
            workOrder.JobCardNotes = string.IsNullOrWhiteSpace(request.Findings) ? null : request.Findings.Trim();
        }

        if (request.WorkDone is not null)
        {
            workOrder.WorkDoneNotes = string.IsNullOrWhiteSpace(request.WorkDone) ? null : request.WorkDone.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.ProgressComment))
        {
            dbContext.WorkOrderEvents.Add(new WorkOrderEvent
            {
                TenantId = TenantId,
                WorkOrderId = workOrder.Id,
                ActorUserId = UserId,
                EventType = ProgressUpdateEventType,
                Status = WorkOrderStatuses.Normalize(workOrder.Status),
                Message = request.ProgressComment.Trim(),
                OccurredAt = DateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Work order execution notes updated",
            nameof(WorkOrder),
            workOrder.Id.ToString(),
            string.IsNullOrWhiteSpace(request.ProgressComment)
                ? $"Updated findings and work done notes for '{workOrder.WorkOrderNumber}'."
                : $"Updated execution notes and saved a progress update for '{workOrder.WorkOrderNumber}'.",
            cancellationToken);

        return Ok(await BuildExecutionBundleAsync(workOrderId, cancellationToken));
    }

    [HttpPost("in-transit")]
    public async Task<ActionResult<WorkOrderResponse>> MarkInTransit(Guid workOrderId, [FromBody] MarkInTransitRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var workOrder = await LoadWorkOrderAsync(workOrderId, cancellationToken);

        await assignmentWorkflowService.RecordInTransitAsync(
            TenantId,
            workOrderId,
            UserId,
            new TechnicianInTransitRequest(request.TechnicianId, request.Latitude, request.Longitude, request.InTransitAt, request.Notes),
            cancellationToken);

        var refreshed = await dbContext.WorkOrders
            .Include(x => x.Client)
            .Include(x => x.Asset)
            .Include(x => x.Site)
            .Include(x => x.System)
            .Include(x => x.AssignedTechnician)
            .Include(x => x.LeadTechnician)
            .Include(x => x.AssignmentGroup)
            .Include(x => x.Branch)
            .Include(x => x.Assignments)
            .Include(x => x.TechnicianAssignments)
            .ThenInclude(x => x.Technician)
            .Include(x => x.PmTemplate)
            .Include(x => x.ChecklistItems)
            .SingleAsync(x => x.TenantId == TenantId && x.Id == workOrder.Id, cancellationToken);

        return Ok(MapWorkOrderResponse(refreshed));
    }

    [HttpPost("materials-used")]
    public async Task<ActionResult<WorkOrderExecutionBundleResponse>> AddMaterialUsage(Guid workOrderId, [FromBody] AddWorkOrderMaterialUsageRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var workOrder = await LoadWorkOrderAsync(workOrderId, cancellationToken);

        if (request.QuantityUsed <= 0)
        {
            throw new BusinessRuleException("Quantity used must be greater than zero.");
        }

        var material = await dbContext.MaterialItems
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.IsActive && x.Id == request.MaterialItemId, cancellationToken)
            ?? throw new BusinessRuleException("Material or spare item was not found.");

        Asset? asset = null;
        if (request.AssetId.HasValue)
        {
            asset = await dbContext.Assets.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == request.AssetId.Value, cancellationToken)
                ?? throw new BusinessRuleException("Asset was not found.");
        }

        await stockLedgerService.ExecuteAsync(async ct =>
        {
            await stockLedgerService.RecordIssueAsync(
                material,
                workOrder.BranchId,
                request.QuantityUsed,
                UserId,
                $"Used on work order {workOrder.WorkOrderNumber}",
                workOrder.WorkOrderNumber,
                workOrder.Id,
                null,
                false,
                ct);

            dbContext.WorkOrderMaterialUsages.Add(new WorkOrderMaterialUsage
            {
                TenantId = TenantId,
                WorkOrderId = workOrderId,
                MaterialItemId = material.Id,
                AssetId = asset?.Id ?? workOrder.AssetId,
                QuantityUsed = request.QuantityUsed,
                UnitCost = request.UnitCost ?? material.UnitCost,
                Chargeable = request.Chargeable,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                UsedByUserId = UserId,
                UsedAt = request.UsedAt ?? DateTime.UtcNow
            });

            dbContext.WorkOrderEvents.Add(new WorkOrderEvent
            {
                TenantId = TenantId,
                WorkOrderId = workOrderId,
                ActorUserId = UserId,
                EventType = "PartUsed",
                Status = WorkOrderStatuses.Normalize(workOrder.Status),
                Message = $"Used {request.QuantityUsed} x {material.ItemName}.",
                OccurredAt = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync(ct);
        }, cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Work order material usage added",
            nameof(WorkOrder),
            workOrder.Id.ToString(),
            $"Added material usage for '{material.ItemName}' on '{workOrder.WorkOrderNumber}'.",
            cancellationToken);

        return Ok(await BuildExecutionBundleAsync(workOrderId, cancellationToken));
    }

    [HttpPost("photos")]
    [RequestSizeLimit(6_000_000)]
    public async Task<ActionResult<WorkOrderExecutionBundleResponse>> UploadPhoto(
        Guid workOrderId,
        [FromForm] string? caption,
        [FromForm] string? category,
        [FromForm] bool? includeInReport,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var workOrder = await LoadWorkOrderAsync(workOrderId, cancellationToken);

        if (file is null || file.Length == 0)
        {
            throw new BusinessRuleException("Select a photo to upload.");
        }

        if (file.Length > MaxPhotoSizeBytes)
        {
            throw new BusinessRuleException("Photos must be 5 MB or smaller.");
        }

        var mimeType = file.ContentType ?? "application/octet-stream";
        if (!AllowedPhotoMimeTypes.Contains(mimeType))
        {
            throw new BusinessRuleException("Photos must be JPG, PNG, or WebP.");
        }

        var normalizedCategory = NormalizePhotoCategory(category);
        var currentPhotoCount = await dbContext.WorkOrderPhotoEvidence.CountAsync(x => x.TenantId == TenantId && x.WorkOrderId == workOrderId, cancellationToken);
        if (currentPhotoCount >= 20)
        {
            throw new BusinessRuleException("A work order can only store up to 20 photos.");
        }

        await using var stream = file.OpenReadStream();
        var uploadResult = await fileStorageService.UploadAsync(stream, file.FileName, mimeType, TenantId, cancellationToken);

        var attachment = new Attachment
        {
            TenantId = TenantId,
            EntityType = "WorkOrderPhotoEvidence",
            EntityId = workOrderId,
            FileName = file.FileName,
            FileSize = uploadResult.FileSize,
            MimeType = mimeType,
            StoragePath = uploadResult.StoragePath,
            PublicUrl = $"/api/attachments/{Guid.Empty}/download",
            UploadedByUserId = UserId
        };
        attachment.PublicUrl = $"/api/attachments/{attachment.Id}/download";
        dbContext.Attachments.Add(attachment);

        dbContext.WorkOrderPhotoEvidence.Add(new WorkOrderPhotoEvidence
        {
            TenantId = TenantId,
            WorkOrderId = workOrderId,
            Attachment = attachment,
            Caption = string.IsNullOrWhiteSpace(caption) ? file.FileName : caption.Trim(),
            Category = normalizedCategory,
            IncludeInReport = includeInReport ?? true,
            UploadedByUserId = UserId,
            UploadedAt = DateTime.UtcNow
        });

        dbContext.WorkOrderEvents.Add(new WorkOrderEvent
        {
            TenantId = TenantId,
            WorkOrderId = workOrderId,
            ActorUserId = UserId,
            EventType = "PhotoUploaded",
            Status = WorkOrderStatuses.Normalize(workOrder.Status),
            Message = $"Uploaded a {normalizedCategory} photo.",
            OccurredAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Work order photo uploaded",
            nameof(WorkOrder),
            workOrder.Id.ToString(),
            $"Uploaded evidence photo to '{workOrder.WorkOrderNumber}'.",
            cancellationToken);

        return Ok(await BuildExecutionBundleAsync(workOrderId, cancellationToken));
    }

    [HttpPut("photos/{photoId:guid}")]
    public async Task<ActionResult<WorkOrderExecutionBundleResponse>> UpdatePhoto(Guid workOrderId, Guid photoId, [FromBody] UpdateWorkOrderPhotoRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        await EnsureAccessibleAsync(workOrderId, cancellationToken);

        var photo = await dbContext.WorkOrderPhotoEvidence
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.WorkOrderId == workOrderId && x.Id == photoId, cancellationToken)
            ?? throw new NotFoundException("Photo evidence was not found.");

        photo.Caption = string.IsNullOrWhiteSpace(request.Caption) ? photo.Caption : request.Caption.Trim();
        photo.Category = NormalizePhotoCategory(request.Category);
        photo.IncludeInReport = request.IncludeInReport;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(await BuildExecutionBundleAsync(workOrderId, cancellationToken));
    }

    [HttpPost("checklist/{itemId:guid}/evidence")]
    [RequestSizeLimit(6_000_000)]
    public async Task<ActionResult<WorkOrderChecklistItemResponse>> UploadChecklistEvidence(
        Guid workOrderId,
        Guid itemId,
        [FromForm] string? caption,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        await EnsureAccessibleAsync(workOrderId, cancellationToken);

        var item = await dbContext.WorkOrderChecklistItems
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.WorkOrderId == workOrderId && x.Id == itemId, cancellationToken)
            ?? throw new NotFoundException("Checklist item was not found.");

        if (file is null || file.Length == 0)
        {
            throw new BusinessRuleException("Select a photo to upload.");
        }

        if (file.Length > MaxPhotoSizeBytes)
        {
            throw new BusinessRuleException("Photos must be 5 MB or smaller.");
        }

        var mimeType = file.ContentType ?? "application/octet-stream";
        if (!AllowedPhotoMimeTypes.Contains(mimeType))
        {
            throw new BusinessRuleException("Photos must be JPG, PNG, or WebP.");
        }

        await using var stream = file.OpenReadStream();
        var uploadResult = await fileStorageService.UploadAsync(stream, file.FileName, mimeType, TenantId, cancellationToken);

        var attachment = new Attachment
        {
            TenantId = TenantId,
            EntityType = "WorkOrderChecklistItem",
            EntityId = item.Id,
            FileName = file.FileName,
            FileSize = uploadResult.FileSize,
            MimeType = mimeType,
            StoragePath = uploadResult.StoragePath,
            PublicUrl = $"/api/attachments/{Guid.Empty}/download",
            UploadedByUserId = UserId
        };
        attachment.PublicUrl = $"/api/attachments/{attachment.Id}/download";
        dbContext.Attachments.Add(attachment);
        item.Attachment = attachment;
        item.AttachmentId = attachment.Id;
        if (string.Equals(item.InputType, "photo", StringComparison.OrdinalIgnoreCase))
        {
            item.IsCompleted = true;
            item.CompletedByUserId = UserId;
            item.CompletedAt = DateTime.UtcNow;
        }

        dbContext.WorkOrderPhotoEvidence.Add(new WorkOrderPhotoEvidence
        {
            TenantId = TenantId,
            WorkOrderId = workOrderId,
            Attachment = attachment,
            Caption = string.IsNullOrWhiteSpace(caption) ? item.QuestionText : caption.Trim(),
            Category = "Checklist",
            IncludeInReport = true,
            UploadedByUserId = UserId,
            UploadedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new WorkOrderChecklistItemResponse(
            item.Id,
            item.PmTemplateQuestionId,
            item.SectionName,
            item.QuestionText,
            item.InputType,
            item.IsRequired,
            item.SortOrder,
            item.ResponseValue,
            item.Remarks,
            item.IsCompleted,
            item.CompletedByUserId,
            item.CompletedAt,
            ParseOptions(item.OptionsJson),
            item.SectionOrder,
            item.HelperText,
            item.Unit,
            item.MinValue,
            item.MaxValue,
            item.DefaultValue,
            item.EvidenceRequired,
            item.AttachmentId,
            item.TechnicianRemarksAllowed,
            item.RequiresNoteOnFail));
    }

    [HttpPost("signatures")]
    public async Task<ActionResult<WorkOrderExecutionBundleResponse>> CaptureSignature(Guid workOrderId, [FromBody] CaptureWorkOrderSignatureRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var workOrder = await LoadWorkOrderAsync(workOrderId, cancellationToken);

        if (!AllowedSignatureTypes.Contains(request.SignatureType))
        {
            throw new BusinessRuleException("Signature type must be Technician or Client.");
        }

        if (string.IsNullOrWhiteSpace(request.SignerName))
        {
            throw new BusinessRuleException("Signer name is required.");
        }

        if (!TryDecodeDataUrl(request.SignatureDataUrl, out var imageBytes))
        {
            throw new BusinessRuleException("Signature image is invalid.");
        }

        if (imageBytes.Length == 0)
        {
            throw new BusinessRuleException("Signature image is empty.");
        }

        var existing = await dbContext.WorkOrderSignatures
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.WorkOrderId == workOrderId && x.SignatureType == request.SignatureType, cancellationToken);

        if (existing is null)
        {
            dbContext.WorkOrderSignatures.Add(new WorkOrderSignature
            {
                TenantId = TenantId,
                WorkOrderId = workOrderId,
                SignatureType = request.SignatureType.Trim(),
                SignerName = request.SignerName.Trim(),
                SignerRole = string.IsNullOrWhiteSpace(request.SignerRole) ? null : request.SignerRole.Trim(),
                SignatureDataUrl = request.SignatureDataUrl.Trim(),
                Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
                CapturedByUserId = UserId,
                CapturedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.SignerName = request.SignerName.Trim();
            existing.SignerRole = string.IsNullOrWhiteSpace(request.SignerRole) ? null : request.SignerRole.Trim();
            existing.SignatureDataUrl = request.SignatureDataUrl.Trim();
            existing.Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
            existing.CapturedByUserId = UserId;
            existing.CapturedAt = DateTime.UtcNow;
        }

        var matchingChecklistItems = await dbContext.WorkOrderChecklistItems
            .Where(x => x.TenantId == TenantId
                && x.WorkOrderId == workOrderId
                && x.InputType == "signature")
            .ToListAsync(cancellationToken);

        foreach (var checklistItem in matchingChecklistItems)
        {
            var needsClientSignature = checklistItem.QuestionText.Contains("client", StringComparison.OrdinalIgnoreCase);
            var needsTechnicianSignature = checklistItem.QuestionText.Contains("technician", StringComparison.OrdinalIgnoreCase);
            var signatureMatches = request.SignatureType.Equals("Client", StringComparison.OrdinalIgnoreCase)
                ? !needsTechnicianSignature
                : !needsClientSignature;

            if (!signatureMatches)
            {
                continue;
            }

            checklistItem.IsCompleted = true;
            checklistItem.CompletedByUserId = UserId;
            checklistItem.CompletedAt = DateTime.UtcNow;
            checklistItem.ResponseValue = request.SignerName.Trim();
        }

        dbContext.WorkOrderEvents.Add(new WorkOrderEvent
        {
            TenantId = TenantId,
            WorkOrderId = workOrderId,
            ActorUserId = UserId,
            EventType = "SignatureCollected",
            Status = WorkOrderStatuses.Normalize(workOrder.Status),
            Message = $"Captured {request.SignatureType.ToLowerInvariant()} signature from {request.SignerName.Trim()}.",
            OccurredAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Work order signature captured",
            nameof(WorkOrder),
            workOrder.Id.ToString(),
            $"Captured {request.SignatureType.ToLowerInvariant()} signature on '{workOrder.WorkOrderNumber}'.",
            cancellationToken);

        return Ok(await BuildExecutionBundleAsync(workOrderId, cancellationToken));
    }

    [HttpGet("signatures")]
    public async Task<ActionResult<IReadOnlyCollection<WorkOrderSignatureResponse>>> GetSignatures(Guid workOrderId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        await EnsureAccessibleAsync(workOrderId, cancellationToken);

        var signatures = await dbContext.WorkOrderSignatures
            .Where(x => x.TenantId == TenantId && x.WorkOrderId == workOrderId)
            .OrderBy(x => x.SignatureType)
            .ThenBy(x => x.CapturedAt)
            .Select(x => new WorkOrderSignatureResponse(
                x.Id,
                x.SignatureType,
                x.SignerName,
                x.SignerRole,
                x.SignatureDataUrl,
                x.Comment,
                x.CapturedByUserId,
                x.CapturedAt))
            .ToListAsync(cancellationToken);

        return Ok(signatures);
    }

    [HttpDelete("signatures/{signatureId:guid}")]
    public async Task<IActionResult> DeleteSignature(Guid workOrderId, Guid signatureId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        await EnsureAccessibleAsync(workOrderId, cancellationToken);

        var signature = await dbContext.WorkOrderSignatures
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.WorkOrderId == workOrderId && x.Id == signatureId, cancellationToken)
            ?? throw new NotFoundException("Signature was not found.");

        dbContext.WorkOrderSignatures.Remove(signature);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Work order signature deleted",
            nameof(WorkOrder),
            workOrderId.ToString(),
            $"Deleted {signature.SignatureType.ToLowerInvariant()} signature from work order.",
            cancellationToken);

        return NoContent();
    }

    [HttpGet("service-report")]
    public async Task<ActionResult<WorkOrderServiceReportPreviewResponse>> GetServiceReport(Guid workOrderId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        await EnsureAccessibleAsync(workOrderId, cancellationToken);
        return Ok(await BuildServiceReportPreviewAsync(workOrderId, cancellationToken));
    }

    [HttpPost("service-report/generate")]
    public async Task<ActionResult<WorkOrderServiceReportPreviewResponse>> GenerateServiceReport(Guid workOrderId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var workOrder = await LoadWorkOrderAsync(workOrderId, cancellationToken);
        await EnsureReportGeneratedAsync(workOrder, cancellationToken);
        var preview = await BuildServiceReportPreviewAsync(workOrderId, cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Work order service report generated",
            nameof(WorkOrder),
            workOrder.Id.ToString(),
            $"Generated service report preview for '{workOrder.WorkOrderNumber}'.",
            cancellationToken);

        return Ok(preview);
    }

    [HttpGet("service-report/pdf")]
    public async Task<IActionResult> DownloadServiceReportPdf(Guid workOrderId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var report = await BuildPdfModelAsync(workOrderId, cancellationToken);
        var pdf = pdfRenderer.RenderWorkOrderReportPdf(report);
        return File(pdf, "application/pdf", $"{report.WorkOrderNumber}-service-report.pdf");
    }

    [HttpPost("pause")]
    public Task<ActionResult<WorkOrderResponse>> Pause(Guid workOrderId, [FromBody] PauseWorkOrderRequest? request, CancellationToken cancellationToken)
    {
        var holdReason = WorkOrderHoldReasons.Normalize(request?.HoldReason);
        return ChangeLifecycleStatusAsync(
            workOrderId,
            new WorkOrderStatusChangeRequest(WorkOrderStatuses.OnHold, holdReason, request?.Note),
            cancellationToken);
    }

    [HttpPost("resume")]
    public Task<ActionResult<WorkOrderResponse>> Resume(Guid workOrderId, CancellationToken cancellationToken) =>
        ChangeLifecycleStatusAsync(
            workOrderId,
            new WorkOrderStatusChangeRequest(WorkOrderStatuses.InProgress, "Work resumed"),
            cancellationToken);

    [HttpPost("close")]
    public Task<ActionResult<WorkOrderResponse>> Close(Guid workOrderId, CancellationToken cancellationToken) =>
        ChangeLifecycleStatusAsync(
            workOrderId,
            new WorkOrderStatusChangeRequest(WorkOrderStatuses.Closed, "Work order closed"),
            cancellationToken);

    [HttpPost("cancel")]
    public Task<ActionResult<WorkOrderResponse>> Cancel(Guid workOrderId, CancellationToken cancellationToken) =>
        ChangeLifecycleStatusAsync(
            workOrderId,
            new WorkOrderStatusChangeRequest(WorkOrderStatuses.Cancelled, "Work order cancelled"),
            cancellationToken);

    private async Task EnsureAccessibleAsync(Guid workOrderId, CancellationToken cancellationToken)
    {
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var query = dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId && x.Id == workOrderId)
            .WhereAccessible(scope, x => x.BranchId);

        if (IsTechnicianOnlyWorkspaceUser())
        {
            var technicianId = await ResolveCurrentTechnicianIdAsync(cancellationToken);
            if (!technicianId.HasValue)
            {
                throw new ForbiddenException("Technicians can only access jobs assigned to them.");
            }

            query = query.Where(x =>
                x.AssignedTechnicianId == technicianId.Value
                || x.LeadTechnicianId == technicianId.Value
                || x.TechnicianAssignments.Any(assignment => assignment.TechnicianId == technicianId.Value));
        }

        var exists = await query.AnyAsync(cancellationToken);

        if (!exists)
        {
            throw new ForbiddenException("Technicians can only access jobs assigned to them.");
        }
    }

    private async Task<WorkOrder> LoadWorkOrderAsync(Guid workOrderId, CancellationToken cancellationToken)
    {
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var query = dbContext.WorkOrders
            .Include(x => x.Client)
            .Include(x => x.Site)
            .Include(x => x.Asset)
            .ThenInclude(x => x!.AssetCategory)
            .Include(x => x.System)
            .Include(x => x.Branch)
            .Include(x => x.AssignmentGroup)
            .Include(x => x.AssignedTechnician)
            .Include(x => x.LeadTechnician)
            .Include(x => x.Events)
            .Include(x => x.TechnicianAssignments)
            .ThenInclude(x => x.Technician)
            .Include(x => x.ChecklistItems)
            .Where(x => x.TenantId == TenantId && x.Id == workOrderId)
            .WhereAccessible(scope, x => x.BranchId);

        if (IsTechnicianOnlyWorkspaceUser())
        {
            var technicianId = await ResolveCurrentTechnicianIdAsync(cancellationToken);
            if (!technicianId.HasValue)
            {
                throw new ForbiddenException("Technicians can only access jobs assigned to them.");
            }

            query = query.Where(x =>
                x.AssignedTechnicianId == technicianId.Value
                || x.LeadTechnicianId == technicianId.Value
                || x.TechnicianAssignments.Any(assignment => assignment.TechnicianId == technicianId.Value));
        }

        return await query.SingleOrDefaultAsync(cancellationToken)
            ?? throw new ForbiddenException("Technicians can only access jobs assigned to them.");
    }

    private async Task<ActionResult<WorkOrderResponse>> ChangeLifecycleStatusAsync(
        Guid workOrderId,
        WorkOrderStatusChangeRequest request,
        CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var workOrder = await LoadWorkOrderAsync(workOrderId, cancellationToken);
        await dbContext.Entry(workOrder).Collection(x => x.ChecklistItems).LoadAsync(cancellationToken);
        await dbContext.Entry(workOrder).Collection(x => x.Assignments).LoadAsync(cancellationToken);
        await workOrderLifecycleService.ChangeStatusAsync(TenantId, workOrderId, UserId, request, cancellationToken);

        var refreshed = await dbContext.WorkOrders
            .Include(x => x.Client)
            .Include(x => x.Asset)
            .ThenInclude(x => x!.AssetCategory)
            .Include(x => x.Site)
            .Include(x => x.System)
            .Include(x => x.AssignedTechnician)
            .Include(x => x.LeadTechnician)
            .Include(x => x.AssignmentGroup)
            .Include(x => x.Branch)
            .Include(x => x.Assignments)
            .Include(x => x.Events)
            .Include(x => x.TechnicianAssignments)
            .ThenInclude(x => x.Technician)
            .Include(x => x.PmTemplate)
            .Include(x => x.ChecklistItems)
            .SingleAsync(x => x.TenantId == TenantId && x.Id == workOrderId, cancellationToken);

        return Ok(MapWorkOrderResponse(refreshed));
    }

    private async Task<WorkOrderExecutionBundleResponse> BuildExecutionBundleAsync(Guid workOrderId, CancellationToken cancellationToken)
    {
        var workOrder = await LoadWorkOrderAsync(workOrderId, cancellationToken);

        var photos = await dbContext.WorkOrderPhotoEvidence
            .Include(x => x.Attachment)
            .Include(x => x.UploadedByUser)
            .Where(x => x.TenantId == TenantId && x.WorkOrderId == workOrderId)
            .OrderBy(x => x.UploadedAt)
            .ToListAsync(cancellationToken);

        var signatures = await dbContext.WorkOrderSignatures
            .Where(x => x.TenantId == TenantId && x.WorkOrderId == workOrderId)
            .OrderBy(x => x.SignatureType)
            .ToListAsync(cancellationToken);

        var materialUsages = await dbContext.WorkOrderMaterialUsages
            .Include(x => x.MaterialItem)
            .Include(x => x.Asset)
            .Include(x => x.UsedByUser)
            .Where(x => x.TenantId == TenantId && x.WorkOrderId == workOrderId)
            .OrderBy(x => x.UsedAt)
            .ToListAsync(cancellationToken);

        var progressUpdates = await dbContext.WorkOrderEvents
            .Include(x => x.ActorUser)
            .Where(x => x.TenantId == TenantId && x.WorkOrderId == workOrderId && x.EventType == ProgressUpdateEventType)
            .OrderByDescending(x => x.OccurredAt)
            .ToListAsync(cancellationToken);

        return new WorkOrderExecutionBundleResponse(
            new WorkOrderExecutionNotesResponse(workOrder.JobCardNotes, workOrder.WorkDoneNotes),
            photos.Select(x => new WorkOrderPhotoResponse(
                x.Id,
                x.AttachmentId,
                x.Attachment?.FileName ?? "photo",
                x.AttachmentId == Guid.Empty ? x.Attachment?.PublicUrl : $"/api/attachments/{x.AttachmentId}/download",
                x.Caption,
                x.Category,
                x.IncludeInReport,
                x.UploadedByUserId,
                x.UploadedByUser?.FullName,
                x.UploadedAt)).ToList(),
            materialUsages.Select(x => new WorkOrderMaterialUsageResponse(
                x.Id,
                x.MaterialItemId,
                x.MaterialItem?.ItemName,
                x.MaterialItem?.UnitOfMeasure,
                x.AssetId,
                x.Asset?.AssetName,
                x.QuantityUsed,
                x.UnitCost,
                x.Chargeable,
                x.Notes,
                x.UsedByUserId,
                x.UsedByUser?.FullName,
                x.UsedAt)).ToList(),
            signatures.Select(x => new WorkOrderSignatureResponse(
                x.Id,
                x.SignatureType,
                x.SignerName,
                x.SignerRole,
                x.SignatureDataUrl,
                x.Comment,
                x.CapturedByUserId,
                x.CapturedAt)).ToList(),
            progressUpdates.Select(x => new WorkOrderExecutionProgressUpdateResponse(
                x.Id,
                x.Message,
                x.ActorUserId,
                x.ActorUser?.FullName,
                x.OccurredAt)).ToList(),
            await BuildServiceReportPreviewAsync(workOrderId, cancellationToken));
    }

    private async Task<WorkOrderServiceReportPreviewResponse> BuildServiceReportPreviewAsync(Guid workOrderId, CancellationToken cancellationToken)
    {
        var pdfModel = await BuildPdfModelAsync(workOrderId, cancellationToken);
        return new WorkOrderServiceReportPreviewResponse(
            pdfModel.CompanyName,
            pdfModel.WorkOrderNumber,
            pdfModel.Title,
            pdfModel.ClientName,
            pdfModel.SiteLabel,
            pdfModel.AssetName,
            pdfModel.AssetDetails,
            pdfModel.TechnicianTeam,
            pdfModel.TemplateName,
            pdfModel.TemplateVersion,
            pdfModel.ReportedProblem,
            pdfModel.Findings,
            pdfModel.WorkDone,
            pdfModel.GeneratedAtLabel,
            pdfModel.Timestamps.Select(x => new WorkOrderReportTimestampResponse(x.Label, x.Value)).ToList(),
            pdfModel.ChecklistSections.Select(section => new WorkOrderReportChecklistSectionResponse(
                section.SectionName,
                section.Items.Select(item => new WorkOrderReportChecklistItemResponse(
                    item.Question,
                    item.InputType,
                    item.ResponseValue,
                    item.Unit,
                    item.Remarks,
                    item.IsRequired,
                    item.IsCompleted,
                    item.EvidenceRequired,
                    item.EvidenceUrl)).ToList())).ToList(),
            pdfModel.Materials.Select(x => new WorkOrderReportMaterialResponse(x.Name, x.QuantityUsed, x.UnitOfMeasure, x.UnitCost, x.Chargeable, x.Notes)).ToList(),
            pdfModel.PhotoGroups.Select(x => new WorkOrderReportPhotoGroupResponse(
                x.Category,
                x.Photos.Select(p => new WorkOrderReportPhotoResponse(p.Caption, p.PublicUrl)).ToList())).ToList(),
            pdfModel.Signatures.Select(x => new WorkOrderReportSignatureResponse(x.SignatureType, x.SignerName, x.SignerRole, x.Comment, x.CapturedAtLabel)).ToList(),
            pdfModel.ShowPoweredByEcosys);
    }

    private async Task<WorkOrderReportPdfModel> BuildPdfModelAsync(Guid workOrderId, CancellationToken cancellationToken)
    {
        var workOrder = await LoadWorkOrderAsync(workOrderId, cancellationToken);
        var tenant = await dbContext.Tenants.SingleAsync(x => x.Id == TenantId, cancellationToken);

        var photos = await dbContext.WorkOrderPhotoEvidence
            .Include(x => x.Attachment)
            .Where(x => x.TenantId == TenantId && x.WorkOrderId == workOrderId && x.IncludeInReport)
            .OrderBy(x => x.UploadedAt)
            .ToListAsync(cancellationToken);

        var signatures = await dbContext.WorkOrderSignatures
            .Where(x => x.TenantId == TenantId && x.WorkOrderId == workOrderId)
            .OrderBy(x => x.SignatureType)
            .ToListAsync(cancellationToken);

        var materialUsages = await dbContext.WorkOrderMaterialUsages
            .Include(x => x.MaterialItem)
            .Where(x => x.TenantId == TenantId && x.WorkOrderId == workOrderId)
            .OrderBy(x => x.UsedAt)
            .ToListAsync(cancellationToken);

        var technicianTeam = workOrder.TechnicianAssignments.Count == 0
            ? workOrder.LeadTechnician?.FullName ?? workOrder.AssignedTechnician?.FullName
            : string.Join(", ", workOrder.TechnicianAssignments
                .OrderByDescending(x => x.IsLead)
                .ThenBy(x => x.AssignedAt)
                .Select(x => x.Technician?.FullName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct());

        var groupedPhotos = photos
            .GroupBy(x => x.Category)
            .OrderBy(group => GetCategoryOrder(group.Key))
            .Select(group => new WorkOrderReportPdfPhotoGroup(
                group.Key,
                group.Select(photo => new WorkOrderReportPdfPhoto(
                    photo.Caption,
                    photo.AttachmentId == Guid.Empty ? photo.Attachment?.PublicUrl : $"/api/attachments/{photo.AttachmentId}/download",
                    TryLoadImageBytes(photo.Attachment?.StoragePath, cancellationToken))).ToList()))
            .ToList();

        var signatureModels = signatures
            .Select(signature => new WorkOrderReportPdfSignature(
                signature.SignatureType,
                signature.SignerName,
                signature.SignerRole,
                signature.Comment,
                signature.CapturedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                TryDecodeDataUrl(signature.SignatureDataUrl, out var imageBytes) ? imageBytes : null))
            .ToList();

        var assetDetails = workOrder.Asset is null
            ? null
            : string.Join(" | ", new[]
            {
                workOrder.Asset.AssetCode,
                workOrder.Asset.AssetType,
                workOrder.Asset.Location,
                workOrder.Asset.Manufacturer,
                workOrder.Asset.Model
            }.Where(x => !string.IsNullOrWhiteSpace(x)));

        var siteLabel = workOrder.Site?.SiteName
            ?? workOrder.Branch?.Name
            ?? workOrder.Asset?.Location
            ?? workOrder.Client?.Location;

        var checklistSections = workOrder.ChecklistItems
            .OrderBy(x => x.SectionOrder)
            .ThenBy(x => x.SectionName ?? string.Empty)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.CreatedAt)
            .GroupBy(x => new { x.SectionOrder, SectionName = string.IsNullOrWhiteSpace(x.SectionName) ? "General" : x.SectionName! })
            .OrderBy(x => x.Key.SectionOrder)
            .ThenBy(x => x.Key.SectionName)
            .Select(group => new WorkOrderReportPdfChecklistSection(
                group.Key.SectionName,
                group.Select(item => new WorkOrderReportPdfChecklistItem(
                    item.QuestionText,
                    item.InputType,
                    item.ResponseValue,
                    item.Unit,
                    item.Remarks,
                    item.IsRequired,
                    item.IsCompleted,
                    item.EvidenceRequired,
                    item.AttachmentId.HasValue ? $"/api/attachments/{item.AttachmentId}/download" : null)).ToList()))
            .ToList();

        // Fetch logo bytes so the renderer can embed the image without making outbound HTTP calls
        var logoBytes = await TryFetchLogoBytesAsync(tenant.LogoUrl, cancellationToken);

        // Resolve the service-report design config: the per-document-type override if present,
        // otherwise the shared base config (null = use defaults in the renderer).
        var templateConfig = PdfTemplateHelper.ResolveForDocument(
            tenant.ReportTemplateConfigJson, tenant.DocumentDesignsJson, "service-report");

        return new WorkOrderReportPdfModel(
            string.IsNullOrWhiteSpace(tenant.CompanyName) ? tenant.Name : tenant.CompanyName,
            tenant.LogoUrl,
            logoBytes,
            tenant.PrimaryColor,
            tenant.SecondaryColor,
            tenant.ShowPoweredByEcosys,
            tenant.ContactPhone ?? tenant.Phone,
            tenant.ContactEmail ?? tenant.Email,
            tenant.Address,
            workOrder.WorkOrderNumber,
            workOrder.Title,
            workOrder.Client?.ClientName,
            siteLabel,
            workOrder.Asset?.AssetName,
            assetDetails,
            technicianTeam,
            workOrder.PmTemplateNameSnapshot ?? workOrder.PmTemplate?.Name,
            workOrder.PmTemplateVersionSnapshot,
            workOrder.Description,
            workOrder.JobCardNotes,
            workOrder.WorkDoneNotes,
            DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'"),
            new List<WorkOrderReportPdfTimestamp>
            {
                new("Created", workOrder.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")),
                new("Arrived", workOrder.ArrivalAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm")),
                new("Started", workOrder.WorkStartedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm")),
                new("Completed", workOrder.CompletedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm")),
                new("Departed", workOrder.DepartureAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm"))
            },
            checklistSections,
            materialUsages.Select(x => new WorkOrderReportPdfMaterial(
                x.MaterialItem?.ItemName ?? "Material",
                x.QuantityUsed,
                x.MaterialItem?.UnitOfMeasure ?? "unit",
                x.UnitCost,
                x.Chargeable,
                x.Notes)).ToList(),
            groupedPhotos,
            signatureModels,
            templateConfig);
    }

    private byte[]? TryLoadImageBytes(string? storagePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return null;
        }

        try
        {
            using var stream = fileStorageService.DownloadAsync(storagePath, cancellationToken).GetAwaiter().GetResult();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizePhotoCategory(string? category)
    {
        var value = string.IsNullOrWhiteSpace(category) ? "Other" : category.Trim();
        if (!AllowedPhotoCategories.Contains(value))
        {
            throw new BusinessRuleException("Invalid photo category.");
        }

        return value;
    }

    private static int GetCategoryOrder(string category) => category switch
    {
        "Before" => 1,
        "Defect" => 2,
        "During" => 3,
        "Spare Part" => 4,
        "After" => 5,
        "Completion" => 6,
        "Client Sign-off" => 7,
        _ => 8
    };

    private async Task<byte[]?> TryFetchLogoBytesAsync(string? logoUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(logoUrl))
            return null;

        try
        {
            if (!Uri.TryCreate(logoUrl, UriKind.Absolute, out var uri))
                return null;

            using var client = httpClientFactory.CreateClient("LogoFetch");
            client.Timeout = TimeSpan.FromSeconds(5);
            var response = await client.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch
        {
            // Logo fetch is best-effort; never block PDF generation on a network failure
            return null;
        }
    }

    private static bool TryDecodeDataUrl(string? dataUrl, out byte[] imageBytes)
    {
        imageBytes = [];
        if (string.IsNullOrWhiteSpace(dataUrl))
        {
            return false;
        }

        var match = SignatureDataUrlRegex().Match(dataUrl.Trim());
        if (!match.Success)
        {
            return false;
        }

        try
        {
            imageBytes = Convert.FromBase64String(match.Groups["data"].Value);
            return true;
        }
        catch
        {
            imageBytes = [];
            return false;
        }
    }

    [GeneratedRegex(@"^data:image\/(?:png|jpeg|jpg|webp);base64,(?<data>[a-zA-Z0-9+/=]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex SignatureDataUrlRegex();

    private WorkOrderResponse MapWorkOrderResponse(WorkOrder workOrder)
    {
        var holdDetails = ResolveActiveHoldDetails(workOrder);
        var technicianAssignments = workOrder.TechnicianAssignments
            .OrderByDescending(x => x.IsLead)
            .ThenBy(x => x.AssignedAt)
            .Select(x => new WorkOrderTechnicianAssignmentResponse(
                x.Id,
                x.TechnicianId,
                x.Technician?.FullName,
                x.IsLead,
                x.Status,
                x.AssignedAt,
                x.AcceptedAt,
                x.ArrivalAt,
                x.DepartureAt,
                x.Notes))
            .ToList();
        var availableTransitions = WorkOrderStatusTransitions
            .GetAllowedTargets(workOrder.Status, CanUseAdminOnlyLifecycleTransitions())
            .ToList();

        return new WorkOrderResponse(
            workOrder.Id,
            workOrder.BranchId,
            workOrder.Branch?.Name,
            workOrder.ClientId,
            workOrder.Client?.ClientName,
            workOrder.SiteId,
            workOrder.Site?.SiteName,
            workOrder.AssetId,
            workOrder.Asset?.AssetName,
            workOrder.AssignmentGroupId,
            workOrder.AssignmentGroup?.Name,
            workOrder.WorkOrderNumber,
            workOrder.Title,
            workOrder.Description,
            workOrder.Priority,
            WorkOrderStatuses.Normalize(workOrder.Status),
            WorkOrderTypes.Normalize(workOrder.WorkType, WorkOrderTypes.CorrectiveMaintenance),
            workOrder.SlaResolutionBreached ? "Resolution Breached" : workOrder.SlaResponseBreached ? "Response Breached" : workOrder.SlaResolutionDeadline.HasValue ? "On Track" : "Not Configured",
            workOrder.AssignmentType,
            workOrder.AssignedTechnicianId,
            workOrder.AssignedTechnician?.FullName,
            ResolveAssignedTechnicianIds(workOrder),
            workOrder.LeadTechnicianId,
            workOrder.LeadTechnician?.FullName,
            workOrder.DueDate,
            workOrder.CreatedAt,
            workOrder.WorkStartedAt,
            workOrder.ArrivalAt,
            workOrder.DepartureAt,
            workOrder.CompletedAt,
            workOrder.WorkDoneNotes,
            workOrder.JobCardNotes,
            workOrder.SlaResponseDeadline,
            workOrder.SlaResolutionDeadline,
            workOrder.SlaResponseBreached,
            workOrder.SlaResolutionBreached,
            workOrder.SlaResponseBreachedAt,
            workOrder.SlaResolutionBreachedAt,
            workOrder.AcknowledgedByName,
            workOrder.AcknowledgementComments,
            workOrder.AcknowledgementDate,
            workOrder.IsPreventiveMaintenance,
            holdDetails.Reason,
            holdDetails.Note,
            workOrder.Assignments.OrderByDescending(x => x.AssignedAt).Select(x => x.AssignmentStatus).FirstOrDefault() ?? "Unassigned",
            workOrder.Assignments.OrderByDescending(x => x.AssignedAt).Select(x => x.Notes).FirstOrDefault(),
            technicianAssignments,
            !string.IsNullOrWhiteSpace(workOrder.AssignmentGroup?.Name) && technicianAssignments.Count == 1
                ? $"{workOrder.AssignmentGroup.Name} -> {technicianAssignments[0].TechnicianName}"
                : technicianAssignments.Count switch
                {
                    0 => workOrder.AssignmentGroup?.Name ?? "Unassigned",
                    1 => technicianAssignments[0].TechnicianName,
                    _ => $"{technicianAssignments[0].TechnicianName} + {technicianAssignments.Count - 1} others"
                },
            !workOrder.AssignmentGroupId.HasValue && technicianAssignments.Count == 0,
            workOrder.PmTemplateId,
            workOrder.PmTemplateNameSnapshot ?? workOrder.PmTemplate?.Name,
            workOrder.PreventiveMaintenancePlanId,
            workOrder.SystemId,
            workOrder.System?.SystemName,
            workOrder.ChecklistItems
                .OrderBy(x => x.SectionOrder)
                .ThenBy(x => x.SectionName ?? string.Empty)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.CreatedAt)
                .Select(x => new WorkOrderChecklistItemResponse(
                    x.Id,
                    x.PmTemplateQuestionId,
                    x.SectionName,
                    x.QuestionText,
                    x.InputType,
                    x.IsRequired,
                    x.SortOrder,
                    x.ResponseValue,
                    x.Remarks,
                    x.IsCompleted,
                    x.CompletedByUserId,
                    x.CompletedAt,
                    ParseOptions(x.OptionsJson),
                    x.SectionOrder,
                    x.HelperText,
                    x.Unit,
                    x.MinValue,
                    x.MaxValue,
                    x.DefaultValue,
                    x.EvidenceRequired,
                    x.AttachmentId,
                    x.TechnicianRemarksAllowed,
                    x.RequiresNoteOnFail))
                .ToList(),
            workOrder.PmTemplateVersionSnapshot,
            workOrder.ScheduledStartAt,
            workOrder.ScheduledEndAt,
            workOrder.NotifyClient,
            workOrder.ClientContactEmail,
            workOrder.BillingStatus,
            workOrder.IsBillable,
            workOrder.Client?.ContactPerson,
            workOrder.Client?.ContactPhone,
            workOrder.Client?.SlaPlan,
            workOrder.Site?.SiteCode,
            workOrder.Site?.ContactPerson,
            workOrder.Site?.ContactPhone,
            workOrder.Site?.OperatingHours,
            workOrder.Site?.AccessNotes,
            BuildSiteLocation(workOrder.Site),
            workOrder.Site?.Latitude,
            workOrder.Site?.Longitude,
            workOrder.EstimatedLabourHours,
            workOrder.Asset?.AssetCode,
            workOrder.Asset?.AssetType,
            workOrder.Asset?.AssetCategory?.Name,
            workOrder.Asset?.SerialNumber,
            workOrder.Asset?.Manufacturer,
            workOrder.Asset?.Model,
            workOrder.Asset?.Location,
            workOrder.Asset?.WarrantyExpiryDate,
            workOrder.Asset?.LastPmDate,
            workOrder.Asset?.NextPmDate,
            ResolveAssetWarrantyStatus(workOrder.Asset),
            null,
            availableTransitions);
    }

    private bool CanUseAdminOnlyLifecycleTransitions() =>
        IsAdmin
        || tenantContext.IsSuperAdmin
        || tenantContext.HasRole(AppRoles.PlatformAdmin)
        || tenantContext.HasRole(AppRoles.PlatformAdminRole)
        || tenantContext.HasRole(AppRoles.PlatformOwner)
        || tenantContext.HasRole(AppRoles.PlatformSuperAdmin)
        || tenantContext.HasRole(AppRoles.SuperAdmin);

    private static string? BuildSiteLocation(Site? site)
    {
        if (site is null)
        {
            return null;
        }

        var parts = new[] { site.StreetAddress, site.TownCity, site.County, site.Country }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part!.Trim())
            .ToList();

        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static string? ResolveAssetWarrantyStatus(Asset? asset)
    {
        if (asset?.WarrantyExpiryDate is null)
        {
            return null;
        }

        return asset.WarrantyExpiryDate.Value.Date >= DateTime.UtcNow.Date
            ? "Under Warranty"
            : "Out of Warranty";
    }

    private async Task<Guid?> ResolveCurrentTechnicianIdAsync(CancellationToken cancellationToken)
    {
        var query = dbContext.Technicians
            .Where(x => x.TenantId == TenantId && x.IsActive);

        if (UserId != Guid.Empty)
        {
            var byUserId = await query
                .Where(x => x.UserId == UserId)
                .Select(x => (Guid?)x.Id)
                .SingleOrDefaultAsync(cancellationToken);

            if (byUserId.HasValue)
            {
                return byUserId.Value;
            }
        }

        var userEmail = await dbContext.Users
            .Where(x => x.TenantId == TenantId && x.Id == UserId)
            .Select(x => x.Email)
            .SingleOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(userEmail))
        {
            return null;
        }

        return await query
            .Where(x => x.Email == userEmail)
            .Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static IReadOnlyCollection<Guid> ParseAssignedTechnicianIds(string? assignedTechnicianIdsJson)
    {
        if (string.IsNullOrWhiteSpace(assignedTechnicianIdsJson))
        {
            return [];
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(assignedTechnicianIdsJson) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyCollection<Guid> ResolveAssignedTechnicianIds(WorkOrder workOrder)
    {
        var relationalIds = workOrder.TechnicianAssignments
            .OrderByDescending(x => x.IsLead)
            .ThenBy(x => x.AssignedAt)
            .Select(x => x.TechnicianId)
            .Distinct()
            .ToList();

        if (relationalIds.Count > 0)
        {
            return relationalIds;
        }

        var legacyIds = ParseAssignedTechnicianIds(workOrder.AssignedTechnicianIdsJson);
        if (legacyIds.Count > 0)
        {
            return legacyIds;
        }

        return workOrder.AssignedTechnicianId.HasValue ? [workOrder.AssignedTechnicianId.Value] : [];
    }

    private static (string? Reason, string? Note) ResolveActiveHoldDetails(WorkOrder workOrder)
        => WorkOrderHoldReasons.ResolveCurrentDetails(workOrder);

    private static IReadOnlyCollection<string> ParseOptions(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson))
        {
            return [];
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(optionsJson) ?? [];
        }
        catch
        {
            return [];
        }
    }
}

public sealed record SaveWorkOrderExecutionNotesRequest(string? Findings, string? WorkDone, string? ProgressComment);
public sealed record PauseWorkOrderRequest(string? HoldReason, string? Note);
public sealed record MarkInTransitRequest(Guid TechnicianId, decimal? Latitude, decimal? Longitude, DateTime? InTransitAt, string? Notes);
public sealed record AddWorkOrderMaterialUsageRequest(Guid MaterialItemId, Guid? AssetId, decimal QuantityUsed, decimal? UnitCost, bool Chargeable, string? Notes, DateTime? UsedAt);
public sealed record UpdateWorkOrderPhotoRequest(string? Caption, string? Category, bool IncludeInReport);
public sealed record CaptureWorkOrderSignatureRequest(string SignatureType, string SignerName, string? SignerRole, string SignatureDataUrl, string? Comment);

public sealed record WorkOrderExecutionBundleResponse(
    WorkOrderExecutionNotesResponse Notes,
    IReadOnlyCollection<WorkOrderPhotoResponse> Photos,
    IReadOnlyCollection<WorkOrderMaterialUsageResponse> MaterialUsages,
    IReadOnlyCollection<WorkOrderSignatureResponse> Signatures,
    IReadOnlyCollection<WorkOrderExecutionProgressUpdateResponse> ProgressUpdates,
    WorkOrderServiceReportPreviewResponse ReportPreview);

public sealed record WorkOrderExecutionNotesResponse(string? Findings, string? WorkDone);
public sealed record WorkOrderPhotoResponse(Guid Id, Guid AttachmentId, string FileName, string? PublicUrl, string Caption, string Category, bool IncludeInReport, Guid UploadedByUserId, string? UploadedByName, DateTime UploadedAt);
public sealed record WorkOrderMaterialUsageResponse(Guid Id, Guid MaterialItemId, string? MaterialName, string? UnitOfMeasure, Guid? AssetId, string? AssetName, decimal QuantityUsed, decimal? UnitCost, bool Chargeable, string? Notes, Guid UsedByUserId, string? UsedByName, DateTime UsedAt);
public sealed record WorkOrderSignatureResponse(Guid Id, string SignatureType, string SignerName, string? SignerRole, string SignatureDataUrl, string? Comment, Guid CapturedByUserId, DateTime CapturedAt);
public sealed record WorkOrderExecutionProgressUpdateResponse(Guid Id, string Message, Guid? ActorUserId, string? ActorUserName, DateTime OccurredAt);
public sealed record WorkOrderServiceReportPreviewResponse(
    string CompanyName,
    string WorkOrderNumber,
    string Title,
    string? ClientName,
    string? SiteLabel,
    string? AssetName,
    string? AssetDetails,
    string? TechnicianTeam,
    string? TemplateName,
    int? TemplateVersion,
    string? ReportedProblem,
    string? Findings,
    string? WorkDone,
    string GeneratedAtLabel,
    IReadOnlyCollection<WorkOrderReportTimestampResponse> Timestamps,
    IReadOnlyCollection<WorkOrderReportChecklistSectionResponse> ChecklistSections,
    IReadOnlyCollection<WorkOrderReportMaterialResponse> Materials,
    IReadOnlyCollection<WorkOrderReportPhotoGroupResponse> PhotoGroups,
    IReadOnlyCollection<WorkOrderReportSignatureResponse> Signatures,
    bool ShowPoweredByEcosys);
public sealed record WorkOrderReportTimestampResponse(string Label, string? Value);
public sealed record WorkOrderReportChecklistSectionResponse(string SectionName, IReadOnlyCollection<WorkOrderReportChecklistItemResponse> Items);
public sealed record WorkOrderReportChecklistItemResponse(string Question, string InputType, string? ResponseValue, string? Unit, string? Remarks, bool IsRequired, bool IsCompleted, bool EvidenceRequired, string? EvidenceUrl);
public sealed record WorkOrderReportMaterialResponse(string Name, decimal QuantityUsed, string UnitOfMeasure, decimal? UnitCost, bool Chargeable, string? Notes);
public sealed record WorkOrderReportPhotoGroupResponse(string Category, IReadOnlyCollection<WorkOrderReportPhotoResponse> Photos);
public sealed record WorkOrderReportPhotoResponse(string Caption, string? PublicUrl);
public sealed record WorkOrderReportSignatureResponse(string SignatureType, string SignerName, string? SignerRole, string? Comment, string CapturedAtLabel);
