using System.ComponentModel.DataAnnotations;
using Ecosys.Api.Common;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ecosys.Api.Controllers;

/// <summary>
/// The operator fault-report portal: an operator scans an asset's label and reports a fault
/// without logging in. The label link's key authorises it; requests are rate-limited per address
/// and per asset.
/// </summary>
[ApiController]
[AllowAnonymous]
[EnableRateLimiting("PublicEndpoints")]
[Route("api/public/fault-reports/{key}/{tag}")]
public sealed class OperatorFaultReportController(IOperatorReportService operatorReportService) : ControllerBase
{
    private const long MaxPhotoBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> PhotoTypes = new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };

    [HttpGet]
    public async Task<ActionResult<OperatorAssetResponse>> Get(string key, string tag, CancellationToken cancellationToken)
    {
        var view = await operatorReportService.GetAssetAsync(key, tag, cancellationToken);
        return Ok(new OperatorAssetResponse(view.CompanyName, view.AssetCode, view.AssetName, view.Location, view.OpenJobNumber, view.OpenJobStatus));
    }

    [HttpPost]
    [RequestSizeLimit(MaxPhotoBytes + 200_000)]
    public async Task<ActionResult<OperatorReportResult>> Submit(string key, string tag, [FromForm] OperatorReportForm form, CancellationToken cancellationToken)
    {
        OperatorPhoto? photo = null;
        if (form.Photo is { Length: > 0 } file)
        {
            if (!PhotoTypes.Contains(file.ContentType ?? string.Empty) || await UploadedFileRules.CheckAsync(file, MaxPhotoBytes, cancellationToken) is { } problem)
            {
                throw new BusinessRuleException("The photo must be a JPEG, PNG or WebP image under 5 MB.");
            }

            photo = new OperatorPhoto(file.OpenReadStream(), Path.GetFileName(file.FileName), file.ContentType!);
        }

        try
        {
            var result = await operatorReportService.SubmitAsync(key, tag,
                new OperatorReport(form.Description ?? string.Empty, form.ReporterName ?? string.Empty, form.MachineStopped, form.Phone), photo, cancellationToken);
            return Ok(result);
        }
        finally
        {
            photo?.Content.Dispose();
        }
    }
}

public sealed record OperatorAssetResponse(string CompanyName, string AssetCode, string AssetName, string? Location, string? OpenJobNumber, string? OpenJobStatus);

public sealed class OperatorReportForm
{
    [StringLength(1000)] public string? Description { get; set; }
    [StringLength(100)] public string? ReporterName { get; set; }
    [StringLength(30)] public string? Phone { get; set; }
    public bool MachineStopped { get; set; }
    public IFormFile? Photo { get; set; }
}
