using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecosys.Api.Controllers;

/// <summary>
/// Reliability groundwork for predictive maintenance: MTBF per asset, PM changes the breakdown
/// history argues for, and faults that keep coming back.
/// </summary>
[ApiController]
[Authorize]
public sealed class ReliabilityController(
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IOrgScopeService orgScopeService,
    IReliabilityService reliabilityService,
    IFactorySettingsService factorySettingsService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    /// <summary>A year by default: enough breakdowns to see an interval, recent enough to still be true.</summary>
    private const int DefaultDays = 365;

    [HttpGet("api/reliability/assets")]
    public async Task<ActionResult<IReadOnlyList<AssetReliability>>> Assets([FromQuery] Guid? orgNodeId, [FromQuery] int? days, CancellationToken cancellationToken)
    {
        var (nodes, from, to) = await ScopeAsync(orgNodeId, days, cancellationToken);
        return Ok(await reliabilityService.AssetsAsync(TenantId, nodes, from, to, cancellationToken));
    }

    [HttpGet("api/reliability/suggestions")]
    public async Task<ActionResult<IReadOnlyList<PmSuggestion>>> Suggestions([FromQuery] Guid? orgNodeId, [FromQuery] int? days, CancellationToken cancellationToken)
    {
        var (nodes, from, to) = await ScopeAsync(orgNodeId, days, cancellationToken);
        return Ok(await reliabilityService.SuggestionsAsync(TenantId, nodes, from, to, cancellationToken));
    }

    /// <summary>Creates the suggested PM plan for the asset, or shortens its existing one.</summary>
    [HttpPost("api/reliability/suggestions/{assetId:guid}/apply")]
    public async Task<ActionResult<AppliedSuggestionResponse>> Apply(Guid assetId, [FromQuery] int? days, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageSettings, PermissionNames.CanCreateWorkOrders);
        var (_, from, to) = await ScopeAsync(null, days, cancellationToken);
        var plan = await reliabilityService.ApplyAsync(TenantId, assetId, from, to, cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "reliability.suggestion.applied", "PreventiveMaintenancePlan", plan.Id.ToString(),
            $"Applied the reliability suggestion: {plan.Name} ({(plan.TriggerType == "Meter" ? $"every {plan.MeterInterval:0.##}" : plan.Frequency)}).", cancellationToken);
        return Ok(new AppliedSuggestionResponse(plan.Id, plan.Name ?? plan.Frequency, plan.TriggerType, plan.Frequency, plan.MeterInterval));
    }

    [HttpGet("api/reliability/repeat-failures")]
    public async Task<ActionResult<RepeatFailureAnalysis>> RepeatFailures([FromQuery] Guid? orgNodeId, [FromQuery] int? days, [FromQuery] int? repeatWithinDays, CancellationToken cancellationToken)
    {
        var (nodes, from, to) = await ScopeAsync(orgNodeId, days, cancellationToken);
        var window = repeatWithinDays ?? (await factorySettingsService.GetOrCreateAsync(TenantId, cancellationToken)).ReworkWindowDays;
        return Ok(await reliabilityService.RepeatFailuresAsync(TenantId, nodes, from, to, window, cancellationToken));
    }

    private async Task<(IReadOnlySet<Guid>? Nodes, DateTime From, DateTime To)> ScopeAsync(Guid? orgNodeId, int? days, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanViewWorkOrders, PermissionNames.CanViewReports);
        var nodes = await orgScopeService.VisibleNodesAsync(TenantId, UserId, orgNodeId, cancellationToken);
        var to = DateTime.UtcNow;
        return (nodes, to.AddDays(-Math.Clamp(days ?? DefaultDays, 30, 3 * 365)), to);
    }
}

public sealed record AppliedSuggestionResponse(Guid PlanId, string Name, string TriggerType, string Frequency, decimal? MeterInterval);
