using System.Text.RegularExpressions;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize(Policy = "PlatformSettingsAccess")]
[Route("api/platform/settings/templates")]
public sealed class PlatformSettingsTemplatesController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IAuditLogService auditLogService) : ControllerBase
{
    private static readonly Regex PlaceholderRegex = new(@"\{\{\s*([a-zA-Z0-9\._]+)\s*\}\}", RegexOptions.Compiled);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<PlatformSettingsTemplateResponse>>> List(CancellationToken cancellationToken)
    {
        var rows = await dbContext.PlatformDocumentTemplates
            .OrderBy(x => x.Type)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(MapTemplate).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PlatformSettingsTemplateResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var row = await LoadTemplateAsync(id, cancellationToken);
        return Ok(MapTemplate(row));
    }

    [HttpPost]
    public async Task<ActionResult<PlatformSettingsTemplateResponse>> Create([FromBody] UpsertPlatformSettingsTemplateRequest request, CancellationToken cancellationToken)
    {
        var row = BuildTemplate(new PlatformDocumentTemplate(), request, true);
        if (row.IsDefault)
        {
            await ResetDefaultTemplatesAsync(row.Type, cancellationToken);
        }

        dbContext.PlatformDocumentTemplates.Add(row);
        await dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("platform.settings.template.created", row.Id, $"Template {row.Name} created.", cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = row.Id }, MapTemplate(row));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PlatformSettingsTemplateResponse>> Update(Guid id, [FromBody] UpsertPlatformSettingsTemplateRequest request, CancellationToken cancellationToken)
    {
        var row = await LoadTemplateAsync(id, cancellationToken);
        BuildTemplate(row, request, false);
        if (row.IsDefault)
        {
            await ResetDefaultTemplatesAsync(row.Type, cancellationToken);
            row.IsDefault = true;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("platform.settings.template.updated", row.Id, $"Template {row.Name} updated.", cancellationToken);
        return Ok(MapTemplate(row));
    }

    [HttpPost("{id:guid}/duplicate")]
    public async Task<ActionResult<PlatformSettingsTemplateResponse>> Duplicate(Guid id, CancellationToken cancellationToken)
    {
        var source = await LoadTemplateAsync(id, cancellationToken);
        var copy = new PlatformDocumentTemplate
        {
            Name = $"{source.Name} Copy",
            Type = source.Type,
            PreviewText = source.PreviewText,
            Subject = source.Subject,
            HeaderHtml = source.HeaderHtml,
            BodyHtml = source.BodyHtml,
            FooterHtml = source.FooterHtml,
            TermsHtml = source.TermsHtml,
            SignatureHtml = source.SignatureHtml,
            IsDefault = false,
            IsActive = source.IsActive,
            PageSize = source.PageSize,
            Orientation = source.Orientation,
            ShowLogo = source.ShowLogo,
            ShowTenantBranding = source.ShowTenantBranding,
            ShowPoweredByEcosys = source.ShowPoweredByEcosys,
            CreatedByUserId = tenantContext.GetRequiredUserId(),
            UpdatedByUserId = tenantContext.GetRequiredUserId()
        };

        dbContext.PlatformDocumentTemplates.Add(copy);
        await dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("platform.settings.template.duplicated", copy.Id, $"Template {source.Name} duplicated.", cancellationToken);
        return Ok(MapTemplate(copy));
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<PlatformSettingsTemplateResponse>> Activate(Guid id, CancellationToken cancellationToken)
    {
        var row = await LoadTemplateAsync(id, cancellationToken);
        row.IsActive = true;
        row.UpdatedByUserId = tenantContext.GetRequiredUserId();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapTemplate(row));
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<ActionResult<PlatformSettingsTemplateResponse>> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var row = await LoadTemplateAsync(id, cancellationToken);
        row.IsActive = false;
        row.UpdatedByUserId = tenantContext.GetRequiredUserId();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapTemplate(row));
    }

    [HttpPost("{id:guid}/make-default")]
    public async Task<ActionResult<PlatformSettingsTemplateResponse>> MakeDefault(Guid id, CancellationToken cancellationToken)
    {
        var row = await LoadTemplateAsync(id, cancellationToken);
        await ResetDefaultTemplatesAsync(row.Type, cancellationToken);
        row.IsDefault = true;
        row.IsActive = true;
        row.UpdatedByUserId = tenantContext.GetRequiredUserId();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapTemplate(row));
    }

    [HttpPost("{id:guid}/preview")]
    public async Task<ActionResult<PlatformTemplatePreviewResponse>> Preview(Guid id, [FromBody] PlatformTemplatePreviewRequest? request, CancellationToken cancellationToken)
    {
        var row = await LoadTemplateAsync(id, cancellationToken);
        var sample = BuildSampleValues();
        if (request?.SampleData is not null)
        {
            foreach (var item in request.SampleData)
            {
                if (!string.IsNullOrWhiteSpace(item.Key) && item.Value is not null)
                {
                    sample[item.Key.Trim()] = item.Value;
                }
            }
        }

        var body = RenderTemplate(row.BodyHtml ?? row.PreviewText, sample);
        var subject = RenderTemplate(row.Subject ?? $"{row.Type} Template", sample);
        var header = RenderTemplate(row.HeaderHtml ?? string.Empty, sample);
        var footer = RenderTemplate(row.FooterHtml ?? string.Empty, sample);
        var terms = RenderTemplate(row.TermsHtml ?? string.Empty, sample);
        var signature = RenderTemplate(row.SignatureHtml ?? string.Empty, sample);

        return Ok(new PlatformTemplatePreviewResponse(
            row.Id,
            row.Name,
            row.Type,
            subject,
            header,
            body,
            footer,
            terms,
            signature,
            BuildHtmlPreview(row, header, body, footer, terms, signature)));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var row = await LoadTemplateAsync(id, cancellationToken);
        if (await IsTemplateUsedAsync(row, cancellationToken))
        {
            row.IsActive = false;
            row.IsDefault = false;
            row.UpdatedByUserId = tenantContext.GetRequiredUserId();
            await dbContext.SaveChangesAsync(cancellationToken);
            return Ok(MapTemplate(row));
        }

        dbContext.PlatformDocumentTemplates.Remove(row);
        await dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("platform.settings.template.deleted", row.Id, $"Template {row.Name} deleted.", cancellationToken);
        return NoContent();
    }

    private PlatformDocumentTemplate BuildTemplate(PlatformDocumentTemplate row, UpsertPlatformSettingsTemplateRequest request, bool isCreate)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessRuleException("Template name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Type))
        {
            throw new BusinessRuleException("Template type is required.");
        }

        row.Name = request.Name.Trim();
        row.Type = request.Type.Trim();
        row.Subject = NormalizeOptional(request.Subject);
        row.HeaderHtml = NormalizeOptional(request.HeaderHtml);
        row.BodyHtml = NormalizeOptional(request.BodyHtml);
        row.FooterHtml = NormalizeOptional(request.FooterHtml);
        row.TermsHtml = NormalizeOptional(request.TermsHtml);
        row.SignatureHtml = NormalizeOptional(request.SignatureHtml);
        row.PreviewText = BuildPreviewText(request.PreviewText, row.BodyHtml);
        row.IsDefault = request.IsDefault;
        row.IsActive = request.IsActive;
        row.PageSize = NormalizePageSize(request.PageSize);
        row.Orientation = NormalizeOrientation(request.Orientation);
        row.ShowLogo = request.ShowLogo;
        row.ShowTenantBranding = request.ShowTenantBranding;
        row.ShowPoweredByEcosys = request.ShowPoweredByEcosys;
        row.UpdatedByUserId = tenantContext.GetRequiredUserId();
        if (isCreate)
        {
            row.CreatedByUserId = tenantContext.GetRequiredUserId();
        }

        return row;
    }

    private async Task<PlatformDocumentTemplate> LoadTemplateAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.PlatformDocumentTemplates.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
        ?? throw new NotFoundException("Template was not found.");

    private async Task ResetDefaultTemplatesAsync(string type, CancellationToken cancellationToken)
    {
        await dbContext.PlatformDocumentTemplates
            .Where(x => x.Type == type)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsDefault, false), cancellationToken);
    }

    private async Task<bool> IsTemplateUsedAsync(PlatformDocumentTemplate template, CancellationToken cancellationToken)
    {
        var type = template.Type.Trim().ToLowerInvariant();
        if (type.Contains("invoice"))
        {
            return await dbContext.PlatformInvoices.AnyAsync(cancellationToken);
        }

        if (type.Contains("quotation"))
        {
            return await dbContext.PlatformQuotations.AnyAsync(cancellationToken);
        }

        if (type.Contains("receipt") || type.Contains("payment"))
        {
            return await dbContext.PlatformPayments.AnyAsync(cancellationToken);
        }

        if (type.Contains("expense"))
        {
            return await dbContext.PlatformExpenses.AnyAsync(cancellationToken);
        }

        return false;
    }

    private async Task AuditAsync(string action, Guid templateId, string description, CancellationToken cancellationToken)
    {
        await auditLogService.LogAsync(
            PlatformConstants.RootTenantId,
            tenantContext.GetRequiredUserId(),
            action,
            nameof(PlatformDocumentTemplate),
            templateId.ToString(),
            description,
            cancellationToken: cancellationToken);
    }

    private static PlatformSettingsTemplateResponse MapTemplate(PlatformDocumentTemplate x) =>
        new(
            x.Id,
            x.Name,
            x.Type,
            x.PreviewText,
            x.IsDefault,
            x.IsActive,
            x.Subject,
            x.HeaderHtml,
            x.BodyHtml,
            x.FooterHtml,
            x.TermsHtml,
            x.SignatureHtml,
            x.PageSize,
            x.Orientation,
            x.ShowLogo,
            x.ShowTenantBranding,
            x.ShowPoweredByEcosys,
            x.CreatedByUserId,
            x.UpdatedByUserId,
            x.CreatedAt,
            x.UpdatedAt);

    private static string BuildPreviewText(string? requestedPreview, string? bodyHtml)
    {
        if (!string.IsNullOrWhiteSpace(requestedPreview))
        {
            return requestedPreview.Trim();
        }

        if (string.IsNullOrWhiteSpace(bodyHtml))
        {
            return "Template";
        }

        var text = bodyHtml.Replace("\r", string.Empty).Replace("\n", " ").Trim();
        return text.Length <= 240 ? text : text[..240];
    }

    private static string NormalizePageSize(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "A4" : value.Trim();
        return normalized switch
        {
            "A4" => "A4",
            "Letter" => "Letter",
            "Receipt" => "Receipt",
            _ => "A4"
        };
    }

    private static string NormalizeOrientation(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "Portrait" : value.Trim();
        return normalized switch
        {
            "Portrait" => "Portrait",
            "Landscape" => "Landscape",
            _ => "Portrait"
        };
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Dictionary<string, string> BuildSampleValues() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["platform.name"] = "Ecosys Command Centre",
        ["platform.logo"] = "https://example.com/logo.png",
        ["tenant.name"] = "Acme Facilities Ltd",
        ["tenant.logo"] = "https://example.com/tenant-logo.png",
        ["tenant.email"] = "ops@acme.test",
        ["tenant.phone"] = "+254700000001",
        ["tenant.address"] = "Nairobi, Kenya",
        ["customer.name"] = "Jane Doe",
        ["customer.email"] = "jane.doe@example.com",
        ["customer.phone"] = "+254700000123",
        ["document.number"] = "INV-000001",
        ["document.date"] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
        ["document.dueDate"] = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd"),
        ["document.subtotal"] = "100,000.00",
        ["document.tax"] = "16,000.00",
        ["document.discount"] = "0.00",
        ["document.total"] = "116,000.00",
        ["document.balance"] = "116,000.00",
        ["invoice.number"] = "INV-000001",
        ["quotation.number"] = "QTN-000001",
        ["payment.amount"] = "50,000.00",
        ["payment.method"] = "M-Pesa",
        ["workOrder.number"] = "WO-000001",
        ["workOrder.title"] = "Replace ATS relay",
        ["workOrder.status"] = "Open",
        ["asset.name"] = "Generator 250kVA",
        ["technician.name"] = "Alex Kimani",
        ["currentUser.name"] = "Platform Admin"
    };

    private static string RenderTemplate(string source, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return string.Empty;
        }

        return PlaceholderRegex.Replace(source, match =>
        {
            var key = match.Groups[1].Value.Trim();
            return values.TryGetValue(key, out var value) ? value : match.Value;
        });
    }

    private static string BuildHtmlPreview(PlatformDocumentTemplate template, string header, string body, string footer, string terms, string signature)
    {
        var logoText = template.ShowLogo ? "<div style=\"font-size:12px;color:#6b7280;\">[Platform Logo]</div>" : string.Empty;
        var tenantBranding = template.ShowTenantBranding ? "<div style=\"font-size:12px;color:#6b7280;\">Tenant branding enabled</div>" : string.Empty;
        var poweredBy = template.ShowPoweredByEcosys ? "<div style=\"font-size:11px;color:#9ca3af;margin-top:16px;\">Powered by Ecosys</div>" : string.Empty;
        return $"""
<div style="font-family:Segoe UI,Arial,sans-serif;padding:24px;max-width:900px;margin:auto;border:1px solid #e5e7eb;border-radius:12px;background:#fff;">
  <div style="display:flex;justify-content:space-between;align-items:flex-start;gap:12px;">
    <div>
      {logoText}
      {tenantBranding}
    </div>
    <div style="font-size:12px;color:#6b7280;">{template.PageSize} • {template.Orientation}</div>
  </div>
  <div style="margin-top:16px;">{header}</div>
  <div style="margin-top:16px;line-height:1.6;">{body}</div>
  <div style="margin-top:16px;">{footer}</div>
  <div style="margin-top:16px;">{terms}</div>
  <div style="margin-top:16px;">{signature}</div>
  {poweredBy}
</div>
""";
    }
}

public sealed record UpsertPlatformSettingsTemplateRequest(
    string Name,
    string Type,
    string? PreviewText,
    bool IsDefault,
    bool IsActive,
    string? Subject,
    string? HeaderHtml,
    string? BodyHtml,
    string? FooterHtml,
    string? TermsHtml,
    string? SignatureHtml,
    bool ShowLogo,
    bool ShowTenantBranding,
    bool ShowPoweredByEcosys,
    string? PageSize,
    string? Orientation);

public sealed record PlatformSettingsTemplateResponse(
    Guid Id,
    string Name,
    string Type,
    string PreviewText,
    bool IsDefault,
    bool IsActive,
    string? Subject,
    string? HeaderHtml,
    string? BodyHtml,
    string? FooterHtml,
    string? TermsHtml,
    string? SignatureHtml,
    string PageSize,
    string Orientation,
    bool ShowLogo,
    bool ShowTenantBranding,
    bool ShowPoweredByEcosys,
    Guid? CreatedByUserId,
    Guid? UpdatedByUserId,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record PlatformTemplatePreviewRequest(Dictionary<string, string>? SampleData);

public sealed record PlatformTemplatePreviewResponse(
    Guid Id,
    string Name,
    string Type,
    string Subject,
    string HeaderHtml,
    string BodyHtml,
    string FooterHtml,
    string TermsHtml,
    string SignatureHtml,
    string RenderedHtml);
