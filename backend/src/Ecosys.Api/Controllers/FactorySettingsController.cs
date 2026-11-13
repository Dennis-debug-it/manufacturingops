using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/factory-settings")]
public sealed class FactorySettingsController(
    ITenantContext tenantContext,
    AppDbContext dbContext,
    IUserAccessService userAccessService,
    IFactorySettingsService factorySettingsService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<FactorySettingsResponse>> Get(CancellationToken cancellationToken) =>
        Ok(Map(await factorySettingsService.GetOrCreateAsync(TenantId, cancellationToken)));

    [HttpPut]
    public async Task<ActionResult<FactorySettingsResponse>> Update([FromBody] UpdateFactorySettingsRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);

        if (string.IsNullOrWhiteSpace(request.ProductionUnit) || string.IsNullOrWhiteSpace(request.ProductionUnitShort))
        {
            throw new BusinessRuleException("Production unit and its short form are required.");
        }

        if (IndustryTemplates.Find(request.IndustryTemplate) is null)
        {
            throw new BusinessRuleException("Unknown industry template.");
        }

        if (!TimeOnly.TryParse(request.WeeklyWindowStart, out var start) || !TimeOnly.TryParse(request.WeeklyWindowEnd, out var end))
        {
            throw new BusinessRuleException("Window start and end must be times such as 06:00.");
        }

        if (end <= start)
        {
            throw new BusinessRuleException("The window must end after it starts.");
        }

        if (!Enum.TryParse<DayOfWeek>(request.WeeklyWindowDay, true, out var day))
        {
            throw new BusinessRuleException("Window day must be a day of the week.");
        }

        if (request.LowCropStartMonth is < 1 or > 12 || request.LowCropEndMonth is < 1 or > 12)
        {
            throw new BusinessRuleException("Low-crop months must be between 1 and 12.");
        }

        var settings = await factorySettingsService.GetOrCreateAsync(TenantId, cancellationToken);

        if (!request.UseGroupLevel && settings.UseGroupLevel
            && await dbContext.OrgNodes.AnyAsync(x => x.TenantId == TenantId && x.Level == OrgNodeLevels.Group && x.IsActive, cancellationToken))
        {
            throw new BusinessRuleException("Deactivate the existing groups before switching the group level off.");
        }

        if (!request.UseSubSectionLevel && settings.UseSubSectionLevel
            && await dbContext.OrgNodes.AnyAsync(x => x.TenantId == TenantId && x.Level == OrgNodeLevels.SubSection && x.IsActive, cancellationToken))
        {
            throw new BusinessRuleException("Deactivate the existing sub-sections before switching the sub-section level off.");
        }

        settings.IndustryTemplate = request.IndustryTemplate.Trim().ToLowerInvariant();
        settings.ProductionUnit = request.ProductionUnit.Trim();
        settings.ProductionUnitShort = request.ProductionUnitShort.Trim();
        settings.UseGroupLevel = request.UseGroupLevel;
        settings.UseSubSectionLevel = request.UseSubSectionLevel;
        settings.WeeklyWindowEnabled = request.WeeklyWindowEnabled;
        settings.WeeklyWindowDay = day;
        settings.WeeklyWindowStart = start;
        settings.WeeklyWindowEnd = end;
        settings.LowCropStartMonth = request.LowCropStartMonth;
        settings.LowCropEndMonth = request.LowCropEndMonth;
        if (request.ReworkWindowDays is < 1 or > 365)
        {
            throw new BusinessRuleException("The rework window must be between 1 and 365 days.");
        }

        settings.RequireWorkOrderApproval = request.RequireWorkOrderApproval ?? settings.RequireWorkOrderApproval;
        settings.AutoApproveBreakdowns = request.AutoApproveBreakdowns ?? settings.AutoApproveBreakdowns;
        settings.CloseJobsOnVerify = request.CloseJobsOnVerify ?? settings.CloseJobsOnVerify;
        settings.ReworkWindowDays = request.ReworkWindowDays ?? settings.ReworkWindowDays;
        settings.OperatorReportingEnabled = request.OperatorReportingEnabled ?? settings.OperatorReportingEnabled;
        if (request.Shifts is { } shifts)
        {
            var parsed = ShiftCalendar.Parse(System.Text.Json.JsonSerializer.Serialize(shifts.Select(x => new { name = x.Name, start = x.Start, end = x.End })));
            settings.ShiftsJson = ShiftCalendar.Serialize(parsed);
        }
        if (!string.IsNullOrWhiteSpace(request.TagPrefix))
        {
            var prefix = request.TagPrefix.Trim().ToUpperInvariant();
            if (prefix.Length > 10 || !prefix.All(char.IsLetterOrDigit))
            {
                throw new BusinessRuleException("The tag prefix can be up to 10 letters or digits.");
            }

            settings.TagPrefix = prefix;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "factory-settings.updated", nameof(FactorySettings), settings.Id.ToString(),
            "Factory settings updated.", cancellationToken);

        return Ok(Map(settings));
    }

    /// <summary>
    /// Issues a new operator report key, for when a label link has leaked. Labels printed before
    /// stop accepting reports until they are reprinted; staff scanning still works.
    /// </summary>
    [HttpPost("operator-report-key")]
    public async Task<ActionResult<FactorySettingsResponse>> RegenerateReportKey(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var settings = await factorySettingsService.GetOrCreateAsync(TenantId, cancellationToken);
        settings.OperatorReportKey = FactorySettingsService.NewReportKey();
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "factory-settings.report-key-regenerated", nameof(FactorySettings), settings.Id.ToString(),
            "Operator report key regenerated; earlier labels no longer accept fault reports.", cancellationToken);
        return Ok(Map(settings));
    }

    /// <summary>The factories and sections a user is limited to. An empty list means the whole organisation.</summary>
    [HttpGet("/api/users/{userId:guid}/org-scope")]
    public async Task<ActionResult<UserOrgScopeResponse>> GetUserScope(Guid userId, CancellationToken cancellationToken)
    {
        if (userId != UserId)
        {
            userAccessService.EnsureAdmin();
        }

        await EnsureUserAsync(userId, cancellationToken);
        var nodeIds = await dbContext.UserOrgNodeAssignments
            .Where(x => x.TenantId == TenantId && x.UserId == userId)
            .Select(x => x.OrgNodeId)
            .ToListAsync(cancellationToken);

        return Ok(new UserOrgScopeResponse(userId, nodeIds));
    }

    [HttpPut("/api/users/{userId:guid}/org-scope")]
    public async Task<ActionResult<UserOrgScopeResponse>> SetUserScope(Guid userId, [FromBody] SetUserOrgScopeRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        await EnsureUserAsync(userId, cancellationToken);

        var requested = (request.OrgNodeIds ?? []).Distinct().ToList();
        var found = await dbContext.OrgNodes
            .Where(x => x.TenantId == TenantId && requested.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (found.Count != requested.Count)
        {
            throw new BusinessRuleException("One or more organisation nodes were not found.");
        }

        var existing = await dbContext.UserOrgNodeAssignments
            .Where(x => x.TenantId == TenantId && x.UserId == userId)
            .ToListAsync(cancellationToken);

        dbContext.UserOrgNodeAssignments.RemoveRange(existing.Where(x => !requested.Contains(x.OrgNodeId)));
        foreach (var nodeId in requested.Where(id => existing.All(x => x.OrgNodeId != id)))
        {
            dbContext.UserOrgNodeAssignments.Add(new UserOrgNodeAssignment { TenantId = TenantId, UserId = userId, OrgNodeId = nodeId });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "user.org-scope.updated", nameof(User), userId.ToString(),
            requested.Count == 0 ? "Access set to the whole organisation." : $"Access limited to {requested.Count} organisation node(s).", cancellationToken);

        return Ok(new UserOrgScopeResponse(userId, requested));
    }

    private async Task EnsureUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Users.AnyAsync(x => x.TenantId == TenantId && x.Id == userId, cancellationToken))
        {
            throw new NotFoundException("User was not found.");
        }
    }

    private FactorySettingsResponse Map(FactorySettings settings) => new(
        settings.IndustryTemplate,
        settings.ProductionUnit,
        settings.ProductionUnitShort,
        settings.UseGroupLevel,
        settings.UseSubSectionLevel,
        settings.WeeklyWindowEnabled,
        settings.WeeklyWindowDay.ToString(),
        settings.WeeklyWindowStart.ToString("HH:mm"),
        settings.WeeklyWindowEnd.ToString("HH:mm"),
        settings.LowCropStartMonth,
        settings.LowCropEndMonth,
        settings.TagPrefix,
        settings.NextTagNumber,
        settings.RequireWorkOrderApproval,
        settings.AutoApproveBreakdowns,
        settings.CloseJobsOnVerify,
        settings.ReworkWindowDays,
        settings.OperatorReportingEnabled,
        // The key authorises anonymous reports, so only admins see it.
        IsAdmin ? settings.OperatorReportKey : null,
        ShiftCalendar.Parse(settings.ShiftsJson).Select(x => new ShiftDto(x.Name, x.Start.ToString("HH:mm"), x.End.ToString("HH:mm"))).ToList())
    {
        // The industry's pack, so screens can use its words (low-crop vs off-season…).
        Industry = IndustryTemplateResponse.From(IndustryTemplates.Find(settings.IndustryTemplate) ?? IndustryTemplates.Default),
    };
}

public sealed record FactorySettingsResponse(
    string IndustryTemplate,
    string ProductionUnit,
    string ProductionUnitShort,
    bool UseGroupLevel,
    bool UseSubSectionLevel,
    bool WeeklyWindowEnabled,
    string WeeklyWindowDay,
    string WeeklyWindowStart,
    string WeeklyWindowEnd,
    int? LowCropStartMonth,
    int? LowCropEndMonth,
    string TagPrefix,
    int NextTagNumber,
    bool RequireWorkOrderApproval,
    bool AutoApproveBreakdowns,
    bool CloseJobsOnVerify,
    int ReworkWindowDays,
    bool OperatorReportingEnabled,
    string? OperatorReportKey,
    IReadOnlyCollection<ShiftDto> Shifts)
{
    public IndustryTemplateResponse? Industry { get; init; }
}

public sealed record ShiftDto(string Name, string Start, string End);

public sealed record UpdateFactorySettingsRequest(
    string IndustryTemplate,
    string ProductionUnit,
    string ProductionUnitShort,
    bool UseGroupLevel,
    bool UseSubSectionLevel,
    bool WeeklyWindowEnabled,
    string WeeklyWindowDay,
    string WeeklyWindowStart,
    string WeeklyWindowEnd,
    int? LowCropStartMonth,
    int? LowCropEndMonth,
    string? TagPrefix = null,
    bool? RequireWorkOrderApproval = null,
    bool? AutoApproveBreakdowns = null,
    bool? CloseJobsOnVerify = null,
    int? ReworkWindowDays = null,
    bool? OperatorReportingEnabled = null,
    IReadOnlyCollection<ShiftDto>? Shifts = null);

public sealed record SetUserOrgScopeRequest(IReadOnlyCollection<Guid>? OrgNodeIds);

public sealed record UserOrgScopeResponse(Guid UserId, IReadOnlyCollection<Guid> OrgNodeIds);
