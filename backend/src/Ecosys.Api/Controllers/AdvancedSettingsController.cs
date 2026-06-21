using System.Text.Json;
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
[Route("api/settings")]
public sealed class AdvancedSettingsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    ILicenseGuardService licenseGuardService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet("branding")]
    public async Task<ActionResult<BrandingSettingsResponse>> GetBranding(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == TenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant not found.");

        return Ok(MapBrandingResponse(tenant));
    }

    [HttpPut("branding")]
    public async Task<ActionResult<BrandingSettingsResponse>> UpdateBranding([FromBody] BrandingSettingsRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var tenant = await dbContext.Tenants
            .SingleOrDefaultAsync(x => x.Id == TenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant not found.");

        tenant.LogoUrl = string.IsNullOrWhiteSpace(request.LogoUrl) ? null : request.LogoUrl.Trim();

        if (!string.IsNullOrWhiteSpace(request.PrimaryColor) && IsValidHexColor(request.PrimaryColor))
            tenant.PrimaryColor = request.PrimaryColor.Trim();

        if (!string.IsNullOrWhiteSpace(request.SecondaryColor) && IsValidHexColor(request.SecondaryColor))
            tenant.SecondaryColor = request.SecondaryColor.Trim();

        tenant.ShowPoweredByEcosys = request.ShowPoweredByEcosys;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(MapBrandingResponse(tenant));
    }

    private static bool IsValidHexColor(string value) =>
        value.StartsWith('#') && (value.Length == 7 || value.Length == 4) &&
        value[1..].All(c => "0123456789abcdefABCDEF".Contains(c));

    private static BrandingSettingsResponse MapBrandingResponse(Tenant tenant)
    {
        var primaryColor = IsValidHexColor(tenant.PrimaryColor) ? tenant.PrimaryColor : "#0F4C81";
        var secondaryColor = IsValidHexColor(tenant.SecondaryColor) ? tenant.SecondaryColor : "#F4B942";

        return new BrandingSettingsResponse(
            string.IsNullOrWhiteSpace(tenant.LogoUrl) ? null : tenant.LogoUrl.Trim(),
            primaryColor,
            secondaryColor,
            tenant.ShowPoweredByEcosys);
    }

    [HttpGet("assignment-groups")]
    public async Task<ActionResult<IReadOnlyCollection<AssignmentGroupResponse>>> GetAssignmentGroups(CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var groups = await dbContext.AssignmentGroups
            .Include(x => x.Branch)
            .Include(x => x.Members)
            .Where(x => x.TenantId == TenantId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return Ok(groups.Select(x => new AssignmentGroupResponse(x.Id, x.BranchId, x.Branch?.Name, x.Name, x.Description, x.IsActive, x.Members.Where(member => member.IsActive).Select(member => member.TechnicianId).ToList())).ToList());
    }

    [HttpPost("assignment-groups")]
    public async Task<ActionResult<AssignmentGroupResponse>> CreateAssignmentGroup([FromBody] UpsertAssignmentGroupRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var group = new AssignmentGroup
        {
            TenantId = TenantId,
            BranchId = request.BranchId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            IsActive = request.IsActive
        };

        dbContext.AssignmentGroups.Add(group);
        await dbContext.SaveChangesAsync(cancellationToken);
        await SyncMembersAsync(group, request.TechnicianIds, cancellationToken);
        await dbContext.Entry(group).Reference(x => x.Branch).LoadAsync(cancellationToken);

        return Ok(new AssignmentGroupResponse(group.Id, group.BranchId, group.Branch?.Name, group.Name, group.Description, group.IsActive, request.TechnicianIds));
    }

    [HttpPut("assignment-groups/{id:guid}")]
    public async Task<ActionResult<AssignmentGroupResponse>> UpdateAssignmentGroup(Guid id, [FromBody] UpsertAssignmentGroupRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var group = await dbContext.AssignmentGroups.Include(x => x.Branch).SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Assignment group was not found.");

        group.BranchId = request.BranchId;
        group.Name = request.Name.Trim();
        group.Description = request.Description?.Trim();
        group.IsActive = request.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);
        await SyncMembersAsync(group, request.TechnicianIds, cancellationToken);
        await dbContext.Entry(group).Reference(x => x.Branch).LoadAsync(cancellationToken);

        return Ok(new AssignmentGroupResponse(group.Id, group.BranchId, group.Branch?.Name, group.Name, group.Description, group.IsActive, request.TechnicianIds));
    }

    [HttpGet("operations-policy")]
    public async Task<ActionResult<TenantOperationsPolicy>> GetOperationsPolicy(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var policy = await dbContext.TenantOperationsPolicies.SingleOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken)
            ?? new TenantOperationsPolicy { TenantId = TenantId };
        return Ok(policy);
    }

    [HttpPut("operations-policy")]
    public async Task<ActionResult<TenantOperationsPolicy>> UpdateOperationsPolicy([FromBody] TenantOperationsPolicyRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var policy = await dbContext.TenantOperationsPolicies.SingleOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken);
        if (policy is null)
        {
            policy = new TenantOperationsPolicy { TenantId = TenantId };
            dbContext.TenantOperationsPolicies.Add(policy);
        }

        policy.RequireTechnicianSignature = request.RequireTechnicianSignature;
        policy.RequireClientSignature = request.RequireClientSignature;
        policy.AllowRemoteCompletion = request.AllowRemoteCompletion;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(policy);
    }

    [HttpGet("security")]
    public async Task<ActionResult<TenantSecurityPolicy>> GetSecurity(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var policy = await dbContext.TenantSecurityPolicies.SingleOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken)
            ?? new TenantSecurityPolicy { TenantId = TenantId };
        return Ok(policy);
    }

    [HttpPut("security")]
    public async Task<ActionResult<TenantSecurityPolicy>> UpdateSecurity([FromBody] TenantSecurityPolicyRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var policy = await dbContext.TenantSecurityPolicies.SingleOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken);
        if (policy is null)
        {
            policy = new TenantSecurityPolicy { TenantId = TenantId };
            dbContext.TenantSecurityPolicies.Add(policy);
        }

        policy.MinPasswordLength = request.MinPasswordLength;
        policy.RequireUppercase = request.RequireUppercase;
        policy.RequireLowercase = request.RequireLowercase;
        policy.RequireDigit = request.RequireDigit;
        policy.RequireSpecialCharacter = request.RequireSpecialCharacter;
        policy.PasswordRotationDays = request.PasswordRotationDays;
        policy.SessionTimeoutMinutes = request.SessionTimeoutMinutes;
        policy.RequireMfa = request.RequireMfa;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(policy);
    }

    [HttpGet("notifications")]
    public async Task<ActionResult<NotificationSetting>> GetNotifications(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var settings = await dbContext.NotificationSettings.SingleOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken)
            ?? new NotificationSetting { TenantId = TenantId };
        return Ok(settings);
    }

    [HttpPut("notifications")]
    public async Task<ActionResult<NotificationSetting>> UpdateNotifications([FromBody] NotificationSettingsRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var settings = await dbContext.NotificationSettings.SingleOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken);
        if (settings is null)
        {
            settings = new NotificationSetting { TenantId = TenantId };
            dbContext.NotificationSettings.Add(settings);
        }

        settings.EmailAlertsEnabled = request.EmailAlertsEnabled;
        settings.SmsAlertsEnabled = request.SmsAlertsEnabled;
        settings.WorkOrderAssignmentAlerts = request.WorkOrderAssignmentAlerts;
        settings.LicenseExpiryAlerts = request.LicenseExpiryAlerts;
        settings.DailyDigestEnabled = request.DailyDigestEnabled;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(settings);
    }

    [HttpGet("monitoring")]
    public async Task<ActionResult<MonitoringSetting>> GetMonitoring(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var settings = await dbContext.MonitoringSettings.SingleOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken)
            ?? new MonitoringSetting { TenantId = TenantId };
        return Ok(settings);
    }

    [HttpPut("monitoring")]
    public async Task<ActionResult<MonitoringSetting>> UpdateMonitoring([FromBody] MonitoringSettingsRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var settings = await dbContext.MonitoringSettings.SingleOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken);
        if (settings is null)
        {
            settings = new MonitoringSetting { TenantId = TenantId };
            dbContext.MonitoringSettings.Add(settings);
        }

        settings.ProviderName = request.ProviderName.Trim();
        settings.EndpointLabel = request.EndpointLabel?.Trim();
        settings.WebhookSecret = request.WebhookSecret?.Trim();
        settings.DefaultBranchId = request.DefaultBranchId;
        settings.DefaultPriority = request.DefaultPriority;
        settings.AutoCreateWorkOrders = request.AutoCreateWorkOrders;
        settings.IsEnabled = request.IsEnabled;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(settings);
    }

    // ── Report Template ───────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    [HttpGet("report-template")]
    public async Task<ActionResult<ReportTemplateConfig>> GetReportTemplate(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var tenant = await dbContext.Tenants.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == TenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant was not found.");

        if (string.IsNullOrWhiteSpace(tenant.ReportTemplateConfigJson))
            return Ok(new ReportTemplateConfig());

        var config = JsonSerializer.Deserialize<ReportTemplateConfig>(tenant.ReportTemplateConfigJson, _jsonOpts)
                     ?? new ReportTemplateConfig();
        return Ok(config);
    }

    [HttpPut("report-template")]
    public async Task<ActionResult<ReportTemplateConfig>> UpdateReportTemplate(
        [FromBody] ReportTemplateConfig request,
        CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var tenant = await dbContext.Tenants
            .SingleOrDefaultAsync(x => x.Id == TenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant was not found.");

        // Normalise section order so it's always 0-based and gap-free
        for (var i = 0; i < request.Sections.Count; i++)
            request.Sections[i].Order = i;

        tenant.ReportTemplateConfigJson = JsonSerializer.Serialize(request);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(request);
    }

    // ── License ───────────────────────────────────────────────────────────────

    [HttpGet("license")]
    public async Task<ActionResult<TenantLicenseSnapshot>> GetLicense(CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        return Ok(await licenseGuardService.GetSnapshotAsync(TenantId, cancellationToken));
    }

    private async Task<Tenant> GetTenantAsync(CancellationToken cancellationToken) =>
        await dbContext.Tenants.SingleOrDefaultAsync(x => x.Id == TenantId, cancellationToken)
        ?? throw new NotFoundException("Tenant was not found.");

    private async Task SyncMembersAsync(AssignmentGroup group, IReadOnlyCollection<Guid> technicianIds, CancellationToken cancellationToken)
    {
        var members = await dbContext.AssignmentGroupMembers.Where(x => x.TenantId == TenantId && x.AssignmentGroupId == group.Id).ToListAsync(cancellationToken);
        dbContext.AssignmentGroupMembers.RemoveRange(members);
        dbContext.AssignmentGroupMembers.AddRange(technicianIds.Distinct().Select(id => new AssignmentGroupMember
        {
            TenantId = TenantId,
            AssignmentGroupId = group.Id,
            TechnicianId = id,
            IsActive = true,
            AddedAt = DateTime.UtcNow
        }));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed record BrandingSettingsRequest(string? LogoUrl, string? PrimaryColor, string? SecondaryColor, bool ShowPoweredByEcosys);
public sealed record BrandingSettingsResponse(string? LogoUrl, string PrimaryColor, string SecondaryColor, bool ShowPoweredByEcosys);
public sealed record UpsertAssignmentGroupRequest(Guid? BranchId, string Name, string? Description, bool IsActive, IReadOnlyCollection<Guid> TechnicianIds);
public sealed record AssignmentGroupResponse(Guid Id, Guid? BranchId, string? BranchName, string Name, string? Description, bool IsActive, IReadOnlyCollection<Guid> TechnicianIds);
public sealed record TenantSecurityPolicyRequest(int MinPasswordLength, bool RequireUppercase, bool RequireLowercase, bool RequireDigit, bool RequireSpecialCharacter, int PasswordRotationDays, int SessionTimeoutMinutes, bool RequireMfa);
public sealed record NotificationSettingsRequest(bool EmailAlertsEnabled, bool SmsAlertsEnabled, bool WorkOrderAssignmentAlerts, bool LicenseExpiryAlerts, bool DailyDigestEnabled);
public sealed record MonitoringSettingsRequest(string ProviderName, string? EndpointLabel, string? WebhookSecret, Guid? DefaultBranchId, string DefaultPriority, bool AutoCreateWorkOrders, bool IsEnabled);
public sealed record TenantOperationsPolicyRequest(bool RequireTechnicianSignature, bool RequireClientSignature, bool AllowRemoteCompletion);
