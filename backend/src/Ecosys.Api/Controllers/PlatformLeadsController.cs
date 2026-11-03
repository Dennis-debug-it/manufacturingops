using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using Ecosys.Api.Common;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
public sealed class PlatformLeadsController(
    AppDbContext dbContext,
    IAuditLogService auditLogService,
    IPlatformLeadNotifier platformLeadNotifier,
    ITenantContext tenantContext) : ControllerBase
{
    private static readonly string[] AllowedContactMethods =
    [
        "Phone",
        "Email",
        "WhatsApp"
    ];

    private static readonly string[] AllowedStatuses =
    [
        "New",
        "Contacted",
        "Qualified",
        "Demo Scheduled",
        "Converted to Workspace",
        "Not a Fit"
    ];

    [AllowAnonymous]
    [EnableRateLimiting("PublicEndpoints")]
    [HttpPost("api/public/leads")]
    public async Task<ActionResult<PublicLeadSubmissionResponse>> CreatePublicLead([FromBody] CreatePlatformLeadRequest request, CancellationToken cancellationToken)
    {
        var lead = new PlatformLead
        {
            CompanyName = NormalizeRequired(request.CompanyName, "Company name is required."),
            ContactPersonName = NormalizeRequired(request.ContactPersonName, "Contact person name is required."),
            Email = NormalizeEmail(request.Email),
            Phone = NormalizeRequired(request.Phone, "Phone number is required."),
            Country = NormalizeOptional(request.Country),
            Industry = NormalizeOptional(request.Industry),
            CompanySize = NormalizeOptional(request.CompanySize),
            Message = NormalizeOptional(request.Message),
            PreferredContactMethod = NormalizeContactMethod(request.PreferredContactMethod),
            Status = "New",
            Notes = null
        };

        dbContext.PlatformLeads.Add(lead);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            null,
            null,
            "platform.lead.submitted",
            nameof(PlatformLead),
            lead.Id.ToString(),
            $"Lead submitted by {lead.CompanyName}.",
            actorName: lead.ContactPersonName,
            cancellationToken: cancellationToken);

        await platformLeadNotifier.TryNotifyAsync(lead, tenantContext.UserId, cancellationToken);

        return Ok(new PublicLeadSubmissionResponse(
            true,
            "Thank you. We have received your request.\n\nThe Ecosys team will contact you shortly to understand your needs and guide you through the next steps."));
    }

    [Authorize(Policy = "PlatformOwnerOnly")]
    [HttpGet("api/platform/leads")]
    public async Task<IActionResult> GetLeads(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.PlatformLeads.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLower();
            query = query.Where(x =>
                x.CompanyName.ToLower().Contains(q)
                || (x.ContactPersonName != null && x.ContactPersonName.ToLower().Contains(q))
                || (x.Email != null && x.Email.ToLower().Contains(q)));
        }

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.Status == status);
        }

        var projected = query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new PlatformLeadListItemResponse(
                x.Id,
                x.CompanyName,
                x.ContactPersonName,
                x.Email,
                x.Phone,
                x.Status,
                x.CreatedAt,
                x.ContactedAt,
                x.ConvertedTenantId));

        if (QueryablePagingExtensions.IsPagingRequested(page, pageSize))
        {
            return Ok(await projected.ToPagedResultAsync(page, pageSize, cancellationToken));
        }

        return Ok(await projected.ToListAsync(cancellationToken));
    }

    [Authorize(Policy = "PlatformOwnerOnly")]
    [HttpGet("api/platform/leads/{id:guid}")]
    public async Task<ActionResult<PlatformLeadDetailResponse>> GetLead(Guid id, CancellationToken cancellationToken)
    {
        var lead = await LoadLeadAsync(id, cancellationToken);
        return Ok(MapDetail(lead));
    }

    [Authorize(Policy = "PlatformOwnerOnly")]
    [HttpPut("api/platform/leads/{id:guid}/status")]
    public async Task<ActionResult<PlatformLeadDetailResponse>> UpdateStatus(Guid id, [FromBody] UpdatePlatformLeadStatusRequest request, CancellationToken cancellationToken)
    {
        var lead = await LoadLeadAsync(id, cancellationToken);
        var nextStatus = NormalizeStatus(request.Status);

        lead.Status = nextStatus;
        if (nextStatus != "New" && !lead.ContactedAt.HasValue)
        {
            lead.ContactedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(
            null,
            tenantContext.UserId,
            "platform.lead.status.updated",
            nameof(PlatformLead),
            lead.Id.ToString(),
            $"Lead '{lead.CompanyName}' marked as {nextStatus}.",
            cancellationToken);

        return Ok(MapDetail(lead));
    }

    [Authorize(Policy = "PlatformOwnerOnly")]
    [HttpPut("api/platform/leads/{id:guid}/notes")]
    public async Task<ActionResult<PlatformLeadDetailResponse>> UpdateNotes(Guid id, [FromBody] UpdatePlatformLeadNotesRequest request, CancellationToken cancellationToken)
    {
        var lead = await LoadLeadAsync(id, cancellationToken);
        lead.Notes = NormalizeOptional(request.Notes);

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(
            null,
            tenantContext.UserId,
            "platform.lead.notes.updated",
            nameof(PlatformLead),
            lead.Id.ToString(),
            $"Internal notes updated for lead '{lead.CompanyName}'.",
            cancellationToken);

        return Ok(MapDetail(lead));
    }

    private async Task<PlatformLead> LoadLeadAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.PlatformLeads
            .AsTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
        ?? throw new NotFoundException("Lead was not found.");

    private static PlatformLeadDetailResponse MapDetail(PlatformLead lead) =>
        new(
            lead.Id,
            lead.CompanyName,
            lead.ContactPersonName,
            lead.Email,
            lead.Phone,
            lead.Country,
            lead.Industry,
            lead.CompanySize,
            lead.Message,
            lead.PreferredContactMethod,
            lead.Status,
            lead.CreatedAt,
            lead.UpdatedAt,
            lead.ContactedAt,
            lead.ConvertedTenantId,
            lead.Notes);

    private static string NormalizeRequired(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessRuleException(message);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeEmail(string? value)
    {
        var trimmed = NormalizeRequired(value, "Email address is required.");
        try
        {
            return new MailAddress(trimmed).Address.ToLowerInvariant();
        }
        catch (FormatException)
        {
            throw new BusinessRuleException("Email address must be valid.");
        }
    }

    private static string? NormalizeContactMethod(string? value)
    {
        var normalized = NormalizeOptional(value);
        if (normalized is null)
        {
            return null;
        }

        var match = AllowedContactMethods.SingleOrDefault(item => string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase));
        return match ?? throw new BusinessRuleException("Preferred contact method is invalid.");
    }

    private static string NormalizeStatus(string? value)
    {
        var normalized = NormalizeRequired(value, "Status is required.");
        var match = AllowedStatuses.SingleOrDefault(item => string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase));
        return match ?? throw new BusinessRuleException("Lead status is invalid.");
    }
}

public sealed record CreatePlatformLeadRequest(
    [Required] string CompanyName,
    [Required] string ContactPersonName,
    [Required, EmailAddress] string Email,
    [Required] string Phone,
    string? Country,
    string? Industry,
    string? CompanySize,
    string? Message,
    string? PreferredContactMethod);

public sealed record UpdatePlatformLeadStatusRequest([Required] string Status);
public sealed record UpdatePlatformLeadNotesRequest(string? Notes);

public sealed record PublicLeadSubmissionResponse(bool Success, string Message);

public sealed record PlatformLeadListItemResponse(
    Guid Id,
    string CompanyName,
    string ContactPersonName,
    string Email,
    string Phone,
    string Status,
    DateTime CreatedAt,
    DateTime? ContactedAt,
    Guid? ConvertedTenantId);

public sealed record PlatformLeadDetailResponse(
    Guid Id,
    string CompanyName,
    string ContactPersonName,
    string Email,
    string Phone,
    string? Country,
    string? Industry,
    string? CompanySize,
    string? Message,
    string? PreferredContactMethod,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ContactedAt,
    Guid? ConvertedTenantId,
    string? Notes);
