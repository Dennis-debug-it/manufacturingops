using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Reports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

public sealed record ServiceReportLayoutResponse(string? Layout, bool WordAvailable, IReadOnlyCollection<string> PhotoCaptions);

/// <summary>
/// Client-specific service report layouts, chosen by the checklist template on the job (its Code).
/// Every other job keeps the standard service report.
/// </summary>
public sealed partial class WorkOrderExecutionController
{
    private const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    /// <summary>Which report this job produces, so the apps can offer the Word download and suggest photo captions.</summary>
    [HttpGet("service-report/layout")]
    public async Task<ActionResult<ServiceReportLayoutResponse>> GetServiceReportLayout(Guid workOrderId, CancellationToken cancellationToken)
    {
        var workOrder = await LoadWorkOrderAsync(workOrderId, cancellationToken);
        return await GetReportLayoutAsync(workOrder, cancellationToken) == SolarInstallationTemplate.LayoutKey
            ? Ok(new ServiceReportLayoutResponse(SolarInstallationTemplate.LayoutKey, true, SolarInstallationTemplate.PhotoSlots.Select(x => x.Caption).ToList()))
            : Ok(new ServiceReportLayoutResponse(null, false, []));
    }

    /// <summary>The report as an editable Word document, for layouts built on the client's own template.</summary>
    [HttpGet("service-report/docx")]
    public async Task<IActionResult> DownloadServiceReportDocx(Guid workOrderId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var workOrder = await LoadWorkOrderAsync(workOrderId, cancellationToken);
        if (await GetReportLayoutAsync(workOrder, cancellationToken) != SolarInstallationTemplate.LayoutKey)
        {
            return NotFound(new { message = "This job's report isn't available as a Word document." });
        }

        var data = await BuildSolarReportDataAsync(workOrder, cancellationToken);
        return File(SolarInstallationDocxRenderer.Render(data), DocxContentType, $"{workOrder.WorkOrderNumber}-installation-report.docx");
    }

    /// <summary>The PDF for download, the bulk zip and the copy emailed to the client: the job's own layout when it has one.</summary>
    private async Task<(byte[] Pdf, string FileName)> RenderServiceReportPdfAsync(Guid workOrderId, CancellationToken cancellationToken, bool skipTechnicianScope = false)
    {
        var workOrder = await LoadWorkOrderAsync(workOrderId, cancellationToken, skipTechnicianScope);
        if (await GetReportLayoutAsync(workOrder, cancellationToken) == SolarInstallationTemplate.LayoutKey)
        {
            var data = await BuildSolarReportDataAsync(workOrder, cancellationToken);
            return (SolarInstallationPdfRenderer.Render(data), $"{workOrder.WorkOrderNumber}-installation-report.pdf");
        }

        var model = await BuildPdfModelAsync(workOrderId, cancellationToken, skipTechnicianScope);
        return (pdfRenderer.RenderWorkOrderReportPdf(model), $"{model.WorkOrderNumber}-service-report.pdf");
    }

    private async Task<string?> GetReportLayoutAsync(WorkOrder workOrder, CancellationToken cancellationToken)
    {
        if (!workOrder.PmTemplateId.HasValue) return null;
        var code = await dbContext.PmTemplates
            .Where(x => x.Id == workOrder.PmTemplateId.Value && x.TenantId == TenantId)
            .Select(x => x.Code)
            .SingleOrDefaultAsync(cancellationToken);
        return string.Equals(code, SolarInstallationTemplate.Code, StringComparison.OrdinalIgnoreCase) ? SolarInstallationTemplate.LayoutKey : null;
    }

    private async Task<SolarInstallationReportData> BuildSolarReportDataAsync(WorkOrder workOrder, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants.SingleAsync(x => x.Id == TenantId, cancellationToken);

        var photos = await dbContext.WorkOrderPhotoEvidence
            .Include(x => x.Attachment)
            .Where(x => x.TenantId == TenantId && x.WorkOrderId == workOrder.Id && x.IncludeInReport)
            .OrderBy(x => x.UploadedAt)
            .ToListAsync(cancellationToken);
        var signatures = await dbContext.WorkOrderSignatures
            .Where(x => x.TenantId == TenantId && x.WorkOrderId == workOrder.Id)
            .ToListAsync(cancellationToken);

        SolarReportSignature? Signature(string type) => signatures
            .Where(x => string.Equals(x.SignatureType, type, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.CapturedAt)
            .Select(x => new SolarReportSignature(
                x.SignerName,
                x.SignerRole,
                x.CapturedAt.ToLocalTime(),
                TryDecodeDataUrl(x.SignatureDataUrl, out var bytes) ? bytes : null))
            .FirstOrDefault();

        var lead = workOrder.TechnicianAssignments.OrderByDescending(x => x.IsLead).ThenBy(x => x.AssignedAt).FirstOrDefault()?.Technician?.FullName
            ?? workOrder.LeadTechnician?.FullName
            ?? workOrder.AssignedTechnician?.FullName;

        return SolarInstallationReportData.Create(new SolarReportSource(
            string.IsNullOrWhiteSpace(tenant.CompanyName) ? tenant.Name : tenant.CompanyName,
            tenant.ContactPhone ?? tenant.Phone,
            tenant.ContactEmail ?? tenant.Email,
            await TryFetchLogoBytesAsync(tenant.LogoUrl, cancellationToken),
            workOrder.WorkOrderNumber,
            workOrder.Client?.ClientName,
            workOrder.Site?.SiteName ?? workOrder.Branch?.Name ?? workOrder.Client?.Location,
            workOrder.Site?.County ?? workOrder.Site?.Region ?? workOrder.Site?.TownCity,
            lead,
            workOrder.CompletedAt?.ToLocalTime(),
            DateTime.UtcNow.ToLocalTime(),
            workOrder.ChecklistItems.Select(x => new SolarReportAnswer(x.QuestionText, x.ResponseValue, x.Remarks)).ToList(),
            photos.Select(x => new SolarReportPhoto(x.Caption, TryLoadImageBytes(x.Attachment?.StoragePath, cancellationToken))).ToList(),
            Signature("Technician"),
            Signature("Client")));
    }
}
