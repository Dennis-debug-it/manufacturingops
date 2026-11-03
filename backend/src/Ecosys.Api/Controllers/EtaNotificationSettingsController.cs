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
[Route("api/settings/notifications/eta")]
public sealed class EtaNotificationSettingsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<EtaNotificationSettingsResponse>>> GetAll(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);

        var existing = await dbContext.EtaNotificationSettings
            .Where(x => x.TenantId == TenantId)
            .ToListAsync(cancellationToken);

        var byEventType = existing.ToDictionary(x => x.EventType, StringComparer.OrdinalIgnoreCase);

        var results = EtaNotificationEventTypes.All.Select(eventType =>
        {
            if (byEventType.TryGetValue(eventType, out var row))
            {
                return Map(row);
            }

            var defaults = EtaNotificationTemplateDefaults.Resolve(eventType);

            return new EtaNotificationSettingsResponse(null, eventType, true, defaults.Subject, defaults.Body);
        }).ToList();

        return Ok(results);
    }

    [HttpPut("{eventType}")]
    public async Task<ActionResult<EtaNotificationSettingsResponse>> Upsert(string eventType, [FromBody] UpsertEtaNotificationSettingsRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);

        var normalizedEventType = NormalizeEventType(eventType);
        Validate(request);

        var row = await dbContext.EtaNotificationSettings
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.EventType == normalizedEventType, cancellationToken);

        if (row is null)
        {
            row = new EtaNotificationSettings { TenantId = TenantId, EventType = normalizedEventType };
            dbContext.EtaNotificationSettings.Add(row);
        }

        row.Enabled = request.Enabled;
        row.SubjectTemplate = request.SubjectTemplate.Trim();
        row.BodyTemplate = request.BodyTemplate.Trim();

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "ETA notification template updated",
            nameof(EtaNotificationSettings),
            row.Id.ToString(),
            $"Updated ETA notification template for '{normalizedEventType}'.",
            cancellationToken);

        return Ok(Map(row));
    }

    private static void Validate(UpsertEtaNotificationSettingsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SubjectTemplate))
        {
            throw new BusinessRuleException("Subject template is required.");
        }

        if (string.IsNullOrWhiteSpace(request.BodyTemplate))
        {
            throw new BusinessRuleException("Body template is required.");
        }
    }

    private static string NormalizeEventType(string value)
    {
        var match = EtaNotificationEventTypes.All.FirstOrDefault(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));
        return match ?? throw new BusinessRuleException("Event type must be one of: Assigned, EnRoute, Arrived, Completed.");
    }

    private static EtaNotificationSettingsResponse Map(EtaNotificationSettings settings) =>
        new(settings.Id, settings.EventType, settings.Enabled, settings.SubjectTemplate, settings.BodyTemplate);
}

public sealed record UpsertEtaNotificationSettingsRequest(bool Enabled, string SubjectTemplate, string BodyTemplate);

public sealed record EtaNotificationSettingsResponse(Guid? Id, string EventType, bool Enabled, string SubjectTemplate, string BodyTemplate);
