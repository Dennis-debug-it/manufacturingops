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
        tenant.Email = request.Email.Trim().ToLowerInvariant();
        tenant.Phone = request.Phone?.Trim();
        tenant.Country = request.Country.Trim();
        tenant.Industry = request.Industry?.Trim();
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

    private static TenantProfileResponse Map(Tenant tenant) =>
        new(
            tenant.CompanyName,
            tenant.Email,
            tenant.Phone,
            tenant.Country,
            tenant.Industry,
            tenant.LogoUrl,
            tenant.PrimaryColor,
            tenant.SecondaryColor,
            tenant.ShowPoweredByEcosys);
}

public sealed record TenantProfileResponse(
    string CompanyName,
    string Email,
    string? Phone,
    string Country,
    string? Industry,
    string? LogoUrl,
    string PrimaryColor,
    string SecondaryColor,
    bool ShowPoweredByEcosys);

public sealed record UpdateTenantProfileRequest(
    string CompanyName,
    string Email,
    string? Phone,
    string Country,
    string? Industry,
    string? LogoUrl,
    string? PrimaryColor,
    string? SecondaryColor,
    bool ShowPoweredByEcosys);

public sealed record LogoUploadRequest(IFormFile File);

public sealed record LogoUploadResponse(string LogoUrl);
