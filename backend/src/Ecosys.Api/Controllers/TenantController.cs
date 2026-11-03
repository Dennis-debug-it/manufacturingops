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
[Authorize]
[Route("api/tenant")]
public sealed class TenantController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IAuditLogService auditLogService,
    IWebHostEnvironment environment,
    IUserAccessService userAccessService) : TenantAwareControllerBase(tenantContext)
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp"
    };

    [HttpGet("profile")]
    public async Task<ActionResult<TenantProfileResponse>> GetProfile(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var tenant = await GetTenantAsync(cancellationToken);
        return Ok(Map(tenant));
    }

    [HttpPut("profile")]
    public async Task<ActionResult<TenantProfileResponse>> UpdateProfile([FromBody] UpdateTenantProfileRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var tenant = await GetTenantAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(request.CompanyName) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Country))
        {
            throw new BusinessRuleException("Company name, email, and country are required.");
        }

        tenant.CompanyName = request.CompanyName.Trim();
        tenant.LegalName = request.LegalName is null ? tenant.LegalName : NormalizeOptional(request.LegalName);
        tenant.TradingName = request.TradingName is null ? tenant.TradingName : NormalizeOptional(request.TradingName);
        tenant.PostalAddress = request.PostalAddress is null ? tenant.PostalAddress : NormalizeOptional(request.PostalAddress);
        tenant.PhysicalAddress = request.PhysicalAddress is null ? tenant.PhysicalAddress : NormalizeOptional(request.PhysicalAddress);
        tenant.Website = request.Website is null ? tenant.Website : NormalizeOptional(request.Website);
        tenant.VatNumber = request.VatNumber is null ? tenant.VatNumber : NormalizeOptional(request.VatNumber);
        tenant.RegistrationNumber = request.RegistrationNumber is null ? tenant.RegistrationNumber : NormalizeOptional(request.RegistrationNumber);
        tenant.Email = request.Email.Trim().ToLowerInvariant();
        tenant.Phone = request.Phone?.Trim();
        tenant.Country = request.Country.Trim();
        if (request.TimeZoneId is not null) tenant.TimeZoneId = TenantTimeService.NormalizeTimeZone(request.TimeZoneId, tenant.Country);
        tenant.Industry = request.Industry?.Trim();
        tenant.Address = request.Address is null ? tenant.Address : NormalizeOptional(request.Address);
        tenant.LogoUrl = request.LogoUrl?.Trim();

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Tenant profile updated",
            nameof(Tenant),
            tenant.Id.ToString(),
            $"Updated tenant profile for '{tenant.CompanyName}'.",
            cancellationToken);

        return Ok(Map(tenant));
    }

    [HttpGet("email-identity")]
    public async Task<ActionResult<TenantEmailIdentityResponse>> GetEmailIdentity(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var identity = await dbContext.EmailSettings.SingleOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken)
            ?? throw new NotFoundException("Email identity was not found.");
        return Ok(MapIdentity(identity));
    }

    [HttpPut("email-identity")]
    public async Task<ActionResult<TenantEmailIdentityResponse>> UpdateEmailIdentity(UpdateTenantEmailIdentityRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var identity = await dbContext.EmailSettings.SingleOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken)
            ?? throw new NotFoundException("Email identity was not found.");
        if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Length > 150)
            throw new BusinessRuleException("Display name is required and must be at most 150 characters.");
        var replyTo = ValidateReplyTo(request.ReplyToAddress, "Reply-to address");
        var financeReplyTo = ValidateReplyTo(request.FinanceReplyToAddress, "Finance reply-to address");
        var operationsReplyTo = ValidateReplyTo(request.OperationsReplyToAddress, "Operations reply-to address");
        if (request.EmailSignature?.Length > 4000) throw new BusinessRuleException("Email signature must be at most 4000 characters.");
        identity.SenderName = request.DisplayName.Trim();
        identity.ReplyToEmail = replyTo;
        identity.FinanceReplyToEmail = financeReplyTo;
        identity.OperationsReplyToEmail = operationsReplyTo;
        identity.InboundEnabled = request.InboundEnabled;
        identity.IsEnabled = request.OutboundEnabled;
        identity.EmailSignature = NormalizeOptional(request.EmailSignature);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "Tenant email identity updated", nameof(EmailSetting), identity.Id.ToString(), "Updated tenant-facing email preferences.", cancellationToken);
        return Ok(MapIdentity(identity));
    }

    private static TenantEmailIdentityResponse MapIdentity(EmailSetting value) => new(value.ManagedEmailAddress,
        value.SenderName, value.ReplyToEmail, value.InboundEnabled, value.IsEnabled, value.EmailSignature,
        value.ManagedEmailStatus, value.ManagedEmailStatusCheckedAt, value.FinanceReplyToEmail, value.OperationsReplyToEmail);

    private static string? ValidateReplyTo(string? value, string label)
    {
        var normalized = NormalizeOptional(value);
        if (normalized is not null && (normalized.Length > 256 || !System.Net.Mail.MailAddress.TryCreate(normalized, out _)))
            throw new BusinessRuleException($"{label} must be a valid email address.");
        return normalized;
    }

    [HttpPost("branding/logo")]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<LogoUploadResponse>> UploadLogo([FromForm] LogoUploadRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        if (request.File is null || request.File.Length == 0)
        {
            throw new BusinessRuleException("Logo file is required.");
        }

        var extension = Path.GetExtension(request.File.FileName);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new BusinessRuleException("Supported logo formats are png, jpg, jpeg, and webp.");
        }

        var tenant = await GetTenantAsync(cancellationToken);
        var webRoot = environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(environment.ContentRootPath, "wwwroot");
        }

        var logoFolder = Path.Combine(webRoot, "uploads", "logos");
        Directory.CreateDirectory(logoFolder);

        var safeFileName = $"{tenant.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}{extension.ToLowerInvariant()}";
        var filePath = Path.Combine(logoFolder, safeFileName);

        await using (var stream = System.IO.File.Create(filePath))
        {
            await request.File.CopyToAsync(stream, cancellationToken);
        }

        tenant.LogoUrl = $"{Request.Scheme}://{Request.Host}/uploads/logos/{safeFileName}";
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Logo uploaded",
            nameof(Tenant),
            tenant.Id.ToString(),
            $"Uploaded tenant logo '{safeFileName}'.",
            cancellationToken);

        return Ok(new LogoUploadResponse(tenant.LogoUrl));
    }

    private async Task<Tenant> GetTenantAsync(CancellationToken cancellationToken) =>
        await dbContext.Tenants.SingleOrDefaultAsync(x => x.Id == TenantId, cancellationToken)
        ?? throw new NotFoundException("Tenant profile was not found.");

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static TenantProfileResponse Map(Tenant tenant) =>
        new(
            tenant.CompanyName,
            tenant.LegalName,
            tenant.TradingName,
            tenant.PostalAddress,
            tenant.PhysicalAddress,
            tenant.Website,
            tenant.VatNumber,
            tenant.RegistrationNumber,
            tenant.Email,
            tenant.Phone,
            tenant.Country,
            tenant.Industry,
            tenant.Address,
            tenant.LogoUrl,
            tenant.PrimaryColor,
            tenant.SecondaryColor,
            tenant.ShowPoweredByEcosys, tenant.Slug, tenant.TimeZoneId, tenant.Currency,
            $"{tenant.Slug}@{TenantEmailIdentity.Domain}");
}

public sealed record TenantProfileResponse(
    string CompanyName,
    string? LegalName,
    string? TradingName,
    string? PostalAddress,
    string? PhysicalAddress,
    string? Website,
    string? VatNumber,
    string? RegistrationNumber,
    string Email,
    string? Phone,
    string Country,
    string? Industry,
    string? Address,
    string? LogoUrl,
    string PrimaryColor,
    string SecondaryColor,
    bool ShowPoweredByEcosys,
    string? Slug = null,
    string TimeZoneId = "Africa/Nairobi",
    string Currency = "KES",
    string? ServiceOpsEmail = null);

public sealed record UpdateTenantProfileRequest(
    string CompanyName,
    string? LegalName,
    string? TradingName,
    string? PostalAddress,
    string? PhysicalAddress,
    string? Website,
    string? VatNumber,
    string? RegistrationNumber,
    string Email,
    string? Phone,
    string Country,
    string? Industry,
    string? Address,
    string? LogoUrl,
    string? PrimaryColor,
    string? SecondaryColor,
    bool ShowPoweredByEcosys,
    string? TimeZoneId = null);

public sealed record LogoUploadRequest(IFormFile File);

public sealed record LogoUploadResponse(string LogoUrl);

/// <summary><c>ManagedEmailStatus</c> says whether the mail server has actually accepted the address — see ManagedEmailStatuses.</summary>
public sealed record TenantEmailIdentityResponse(string? EmailAddress, string DisplayName, string? ReplyToAddress,
    bool InboundEnabled, bool OutboundEnabled, string? EmailSignature,
    string ManagedEmailStatus = ManagedEmailStatuses.Pending, DateTime? ManagedEmailStatusCheckedAt = null,
    string? FinanceReplyToAddress = null, string? OperationsReplyToAddress = null);
public sealed record UpdateTenantEmailIdentityRequest(string DisplayName, string? ReplyToAddress,
    bool InboundEnabled, bool OutboundEnabled, string? EmailSignature,
    string? FinanceReplyToAddress = null, string? OperationsReplyToAddress = null);
