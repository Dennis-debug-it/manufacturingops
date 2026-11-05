using System.ComponentModel.DataAnnotations;
using Ecosys.Api.Common;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>Statutory inspections, calibrations, fire safety and certificates: due dates, owners, evidence and escalation.</summary>
[ApiController]
[Authorize]
[Route("api/factory/compliance")]
public sealed class ComplianceController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IOrgScopeService orgScopeService,
    IComplianceService complianceService,
    IFileStorageService fileStorageService) : TenantAwareControllerBase(tenantContext)
{
    private const long MaxCertificateBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> CertificateTypes = new(StringComparer.OrdinalIgnoreCase) { "application/pdf", "image/jpeg", "image/png", "image/webp" };

    private FactoryActor Actor => new(UserId, UserDisplayName ?? "Unknown");

    [HttpGet("items")]
    public async Task<ActionResult<IReadOnlyCollection<ComplianceItemDto>>> Items(
        [FromQuery] Guid? orgNodeId, [FromQuery] Guid? assetId, [FromQuery] string? status, [FromQuery] string? kind, [FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var query = await ScopedAsync(orgNodeId, cancellationToken);
        if (!includeInactive) query = query.Where(x => x.IsActive);
        if (assetId.HasValue) query = query.Where(x => x.AssetId == assetId);
        if (!string.IsNullOrWhiteSpace(kind)) query = query.Where(x => x.Kind == kind);
        var today = DateTime.UtcNow.Date;
        var items = (await query.Select(Projection).ToListAsync(cancellationToken)).Select(x => WithStatus(x, today));
        if (!string.IsNullOrWhiteSpace(status)) items = items.Where(x => string.Equals(x.Status, status, StringComparison.OrdinalIgnoreCase));
        // Overdue first, then by due date; items with no date last.
        return Ok(items.OrderBy(x => x.Status == ComplianceStatuses.Overdue ? 0 : x.Status == ComplianceStatuses.NoDate ? 2 : 1).ThenBy(x => x.DueDate).ThenBy(x => x.Name).ToList());
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ComplianceSummary>> Summary([FromQuery] Guid? orgNodeId, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var today = DateTime.UtcNow.Date;
        var items = await (await ScopedAsync(orgNodeId, cancellationToken)).Where(x => x.IsActive).Select(x => new { x.Kind, x.DueDate }).ToListAsync(cancellationToken);
        var statuses = items.Select(x => (x.Kind, Status: ComplianceStatuses.Of(x.DueDate, today))).ToList();
        return Ok(new ComplianceSummary(
            statuses.Count, statuses.Count(x => x.Status == ComplianceStatuses.Ok), statuses.Count(x => x.Status == ComplianceStatuses.DueSoon),
            statuses.Count(x => x.Status == ComplianceStatuses.Overdue), statuses.Count(x => x.Status == ComplianceStatuses.NoDate),
            statuses.GroupBy(x => x.Kind).Select(g => new ComplianceKindCount(g.Key, g.Count(), g.Count(x => x.Status == ComplianceStatuses.Overdue), g.Count(x => x.Status == ComplianceStatuses.DueSoon))).OrderBy(x => x.Kind).ToList()));
    }

    [HttpGet("items/{id:guid}")]
    public async Task<ActionResult<ComplianceItemDetail>> Get(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanView();
        await LoadAsync(id, cancellationToken);
        return Ok(await DetailAsync(id, cancellationToken));
    }

    [HttpPost("items")]
    public async Task<ActionResult<ComplianceItemDetail>> Create([FromBody] SaveComplianceItemRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var item = new ComplianceItem { TenantId = TenantId };
        await ApplyAsync(item, request, cancellationToken);
        dbContext.ComplianceItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await DetailAsync(item.Id, cancellationToken));
    }

    [HttpPut("items/{id:guid}")]
    public async Task<ActionResult<ComplianceItemDetail>> Update(Guid id, [FromBody] SaveComplianceItemRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var item = await LoadAsync(id, cancellationToken);
        var dueBefore = item.DueDate;
        await ApplyAsync(item, request, cancellationToken);
        if (item.DueDate != dueBefore)
        {
            item.LastDueAlertDays = null;
            item.LastOverdueAlertAt = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await DetailAsync(id, cancellationToken));
    }

    /// <summary>Records an inspection, calibration or renewal, with the certificate (PDF or photo).</summary>
    [HttpPost("items/{id:guid}/records")]
    [RequestSizeLimit(MaxCertificateBytes + 200_000)]
    public async Task<ActionResult<ComplianceItemDetail>> Record(Guid id, [FromForm] ComplianceRecordForm form, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageSettings, PermissionNames.CanAssignWorkOrders, PermissionNames.CanCompleteWorkOrders);
        var item = await LoadAsync(id, cancellationToken);
        Guid? attachmentId = null;
        if (form.Certificate is { Length: > 0 } file)
        {
            if (!CertificateTypes.Contains(file.ContentType ?? string.Empty) || await UploadedFileRules.CheckAsync(file, MaxCertificateBytes, cancellationToken) is not null)
            {
                throw new BusinessRuleException("The certificate must be a PDF or a photo under 10 MB.");
            }

            await using var stream = file.OpenReadStream();
            var stored = await fileStorageService.UploadAsync(stream, Path.GetFileName(file.FileName), file.ContentType!, TenantId, cancellationToken);
            var attachment = new Attachment
            {
                TenantId = TenantId, EntityType = nameof(ComplianceItem), EntityId = item.Id, FileName = Path.GetFileName(file.FileName), FileSize = stored.FileSize,
                MimeType = file.ContentType!, StoragePath = stored.StoragePath, UploadedByUserId = UserId,
            };
            attachment.PublicUrl = $"/api/factory/compliance/certificates/{attachment.Id}";
            dbContext.Attachments.Add(attachment);
            attachmentId = attachment.Id;
        }

        await complianceService.RecordAsync(item, new ComplianceRecordInput(
            form.DoneAt ?? throw new BusinessRuleException("Enter the date it was done."), form.Result ?? ComplianceResults.Pass, form.ValidUntil,
            form.CertificateNumber, form.Examiner, form.Notes, attachmentId), Actor, cancellationToken);
        return Ok(await DetailAsync(id, cancellationToken));
    }

    [HttpGet("certificates/{attachmentId:guid}")]
    public async Task<IActionResult> Certificate(Guid attachmentId, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var attachment = await dbContext.Attachments.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == attachmentId && x.EntityType == nameof(ComplianceItem), cancellationToken)
            ?? throw new NotFoundException("The certificate was not found.");
        await LoadAsync(attachment.EntityId, cancellationToken);
        var stream = await fileStorageService.DownloadAsync(attachment.StoragePath, cancellationToken);
        return File(stream, attachment.MimeType, attachment.FileName, enableRangeProcessing: true);
    }

    [HttpGet("templates")]
    public ActionResult<IReadOnlyCollection<ComplianceTemplate>> Templates()
    {
        EnsureCanView();
        return Ok(ComplianceTemplates.TeaKenya);
    }

    /// <summary>Adds the factory's standard statutory items (boilers, air receivers, lifting, weighing, fire, audits).</summary>
    [HttpPost("install")]
    public async Task<ActionResult<ComplianceInstalled>> Install([FromBody] InstallComplianceRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, request.FactoryId, cancellationToken);
        return Ok(new ComplianceInstalled(await complianceService.InstallAsync(TenantId, request.FactoryId, cancellationToken)));
    }

    private async Task ApplyAsync(ComplianceItem item, SaveComplianceItemRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new BusinessRuleException("Name the item, e.g. \"Boiler examination – BLR-01\".");
        var kind = ComplianceKinds.All.FirstOrDefault(x => string.Equals(x, request.Kind, StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessRuleException($"Kind must be one of: {string.Join(", ", ComplianceKinds.All)}.");
        if (request.IntervalMonths is < 1 or > 120) throw new BusinessRuleException("The interval must be between 1 and 120 months.");

        var orgNodeId = request.OrgNodeId;
        if (request.AssetId is { } assetId)
        {
            orgNodeId = await dbContext.Assets.Where(x => x.TenantId == TenantId && x.Id == assetId && x.ClientId == null).Select(x => x.OrgNodeId).SingleOrDefaultAsync(cancellationToken)
                ?? throw new BusinessRuleException("The asset was not found.");
        }

        if (orgNodeId is not { } node || !await dbContext.OrgNodes.AnyAsync(x => x.TenantId == TenantId && x.Id == node, cancellationToken))
        {
            throw new BusinessRuleException("Choose the asset, or where it applies.");
        }

        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, node, cancellationToken);
        if (request.OwnerUserId is { } owner && !await dbContext.Users.AnyAsync(x => x.TenantId == TenantId && x.Id == owner && x.IsActive, cancellationToken))
        {
            throw new BusinessRuleException("The owner was not found.");
        }

        item.Name = request.Name.Trim();
        item.Kind = kind;
        item.Regulation = Clean(request.Regulation);
        item.IntervalMonths = request.IntervalMonths;
        item.OrgNodeId = node;
        item.AssetId = request.AssetId;
        item.OwnerUserId = request.OwnerUserId;
        item.Provider = Clean(request.Provider);
        item.RaiseJobDaysBefore = request.RaiseJobDaysBefore is >= 0 and <= 180 ? request.RaiseJobDaysBefore : null;
        item.Notes = Clean(request.Notes);
        item.IsActive = request.IsActive ?? true;
        item.LastDoneAt = request.LastDoneAt is { } last ? DateTime.SpecifyKind(last.Date, DateTimeKind.Utc) : item.LastDoneAt;
        item.DueDate = request.DueDate is { } due
            ? DateTime.SpecifyKind(due.Date, DateTimeKind.Utc)
            : item.DueDate ?? (item.LastDoneAt is { } done ? done.AddMonths(item.IntervalMonths) : null);
    }

    private async Task<ComplianceItemDetail> DetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = WithStatus(await dbContext.ComplianceItems.Where(x => x.Id == id).Select(Projection).SingleAsync(cancellationToken), DateTime.UtcNow.Date);
        var records = await dbContext.ComplianceRecords.Where(x => x.ItemId == id).OrderByDescending(x => x.DoneAt)
            .Select(x => new ComplianceRecordDto(x.Id, x.DoneAt, x.Result, x.ValidUntil, x.CertificateNumber, x.Examiner, x.Notes, x.CertificateAttachmentId,
                x.CertificateAttachment != null ? x.CertificateAttachment.FileName : null, x.WorkOrderId, x.RecordedByName))
            .ToListAsync(cancellationToken);
        return new ComplianceItemDetail(item, records);
    }

    private static ComplianceItemDto WithStatus(ComplianceItemDto item, DateTime today) => item with
    {
        Status = ComplianceStatuses.Of(item.DueDate, today),
        DaysLeft = item.DueDate is { } due ? (int)(due.Date - today).TotalDays : null,
    };

    private static readonly System.Linq.Expressions.Expression<Func<ComplianceItem, ComplianceItemDto>> Projection = x => new ComplianceItemDto(
        x.Id, x.Name, x.Kind, x.Regulation, x.IntervalMonths, x.OrgNodeId, x.OrgNode!.Name, x.AssetId, x.Asset != null ? x.Asset.AssetCode : null, x.Asset != null ? x.Asset.AssetName : null,
        x.LastDoneAt, x.DueDate, x.OwnerUserId, x.Owner != null ? x.Owner.FullName : null, x.Provider, x.RaiseJobDaysBefore,
        x.OpenWorkOrderId, x.OpenWorkOrder != null ? x.OpenWorkOrder.WorkOrderNumber : null, x.Notes, x.IsActive,
        x.Records.OrderByDescending(r => r.DoneAt).Select(r => r.CertificateNumber).FirstOrDefault(),
        x.Records.OrderByDescending(r => r.DoneAt).Select(r => r.CertificateAttachmentId).FirstOrDefault(),
        string.Empty, null);

    private async Task<ComplianceItem> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await dbContext.ComplianceItems.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("The compliance item was not found.");
        var scope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        if (scope.Nodes is not null && !scope.Nodes.Contains(item.OrgNodeId)) throw new NotFoundException("The compliance item was not found.");
        return item;
    }

    private async Task<IQueryable<ComplianceItem>> ScopedAsync(Guid? orgNodeId, CancellationToken cancellationToken)
    {
        var query = dbContext.ComplianceItems.Where(x => x.TenantId == TenantId);
        var scope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        if (scope.Nodes is not null)
        {
            var nodes = scope.Nodes.ToList();
            query = query.Where(x => nodes.Contains(x.OrgNodeId));
        }

        if (orgNodeId.HasValue)
        {
            var subtree = (await orgScopeService.GetSubtreeAsync(TenantId, orgNodeId.Value, cancellationToken)).ToList();
            query = query.Where(x => subtree.Contains(x.OrgNodeId));
        }

        return query;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void EnsureCanView() => userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanViewWorkOrders);

    private void EnsureCanManage() => userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageSettings, PermissionNames.CanAssignWorkOrders);
}

public sealed record ComplianceItemDto(
    Guid Id, string Name, string Kind, string? Regulation, int IntervalMonths, Guid OrgNodeId, string? OrgNodeName, Guid? AssetId, string? AssetCode, string? AssetName,
    DateTime? LastDoneAt, DateTime? DueDate, Guid? OwnerUserId, string? OwnerName, string? Provider, int? RaiseJobDaysBefore,
    Guid? OpenWorkOrderId, string? OpenWorkOrderNumber, string? Notes, bool IsActive, string? LastCertificateNumber, Guid? LastCertificateId,
    string Status, int? DaysLeft);

public sealed record ComplianceRecordDto(
    Guid Id, DateTime DoneAt, string Result, DateTime? ValidUntil, string? CertificateNumber, string? Examiner, string? Notes,
    Guid? CertificateId, string? CertificateFileName, Guid? WorkOrderId, string? RecordedByName);

public sealed record ComplianceItemDetail(ComplianceItemDto Item, IReadOnlyCollection<ComplianceRecordDto> Records);

public sealed record ComplianceKindCount(string Kind, int Total, int Overdue, int DueSoon);

public sealed record ComplianceSummary(int Total, int Ok, int DueSoon, int Overdue, int NoDate, IReadOnlyCollection<ComplianceKindCount> ByKind);

public sealed record SaveComplianceItemRequest(
    [StringLength(200)] string? Name, string? Kind, [StringLength(200)] string? Regulation, int IntervalMonths, Guid? OrgNodeId, Guid? AssetId,
    DateTime? LastDoneAt, DateTime? DueDate, Guid? OwnerUserId, [StringLength(200)] string? Provider, int? RaiseJobDaysBefore, [StringLength(2000)] string? Notes, bool? IsActive);

public sealed class ComplianceRecordForm
{
    public DateTime? DoneAt { get; set; }
    public string? Result { get; set; }
    public DateTime? ValidUntil { get; set; }
    [StringLength(100)] public string? CertificateNumber { get; set; }
    [StringLength(200)] public string? Examiner { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
    public IFormFile? Certificate { get; set; }
}

public sealed record InstallComplianceRequest(Guid FactoryId);

public sealed record ComplianceInstalled(int ItemsAdded);
