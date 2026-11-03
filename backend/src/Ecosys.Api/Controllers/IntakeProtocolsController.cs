using System.Net.Sockets;
using System.Text.Json;
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
[Route("api/settings/intake-protocols")]
public sealed class IntakeProtocolsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<IntakeProtocolResponse>>> List(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();

        // Oldest first: that is the order rules are tried in. The old auto-made catch-all rule is hidden.
        var items = await dbContext.IntakeProtocols
            .Where(x => x.TenantId == TenantId && !x.CriteriaJson.Contains(IntakeRules.LegacyMarker))
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(items.Select(MapProtocol).ToList());
    }

    /// <summary>Ready-made rules a workspace can add with one click.</summary>
    [HttpGet("templates")]
    public ActionResult<IReadOnlyCollection<IntakeRuleTemplateResponse>> Templates()
    {
        userAccessService.EnsureAdmin();
        return Ok(IntakeRuleTemplates.All.Select(x => new IntakeRuleTemplateResponse(x.Key, x.Name, x.Description, x.CriteriaJson, x.ActionsJson)).ToList());
    }

    [HttpPost("templates/{key}")]
    public async Task<ActionResult<IntakeProtocolResponse>> AddFromTemplate(string key, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var template = IntakeRuleTemplates.All.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotFoundException("That ready-made rule was not found.");

        var protocol = new IntakeProtocol
        {
            TenantId = TenantId,
            Name = template.Name,
            Description = template.Description,
            SourceType = "Email",
            IsActive = true,
            CriteriaJson = template.CriteriaJson,
            ActionsJson = template.ActionsJson,
            SourceConfigJson = "{}",
        };
        dbContext.IntakeProtocols.Add(protocol);
        await dbContext.SaveChangesAsync(cancellationToken);
        await AuditSettingsChangeAsync("Intake protocol created", protocol.Id, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = protocol.Id }, MapProtocol(protocol));
    }

    /// <summary>Which rule a sample email would match, without saving anything.</summary>
    [HttpPost("preview")]
    public async Task<ActionResult<IntakeRulePreviewResponse>> Preview([FromBody] IntakeRulePreviewRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var rules = await dbContext.IntakeProtocols
            .Where(x => x.TenantId == TenantId && x.IsActive && x.SourceType == "Email")
            .ToListAsync(cancellationToken);
        var outcome = IntakeRules.FirstMatch(rules, new IntakeRuleInput(request.Subject ?? string.Empty, request.Body ?? string.Empty, request.FromAddress?.Trim() ?? string.Empty, request.FromName));
        if (outcome is null) return Ok(new IntakeRulePreviewResponse(null, null, "No rule matches; the workspace defaults apply.", null, null, null));
        var title = IntakeRules.ApplyTitle(outcome.TitleTemplate, request.Subject ?? string.Empty, request.FromAddress ?? string.Empty, request.FromName);
        return Ok(new IntakeRulePreviewResponse(
            outcome.Rule.Id,
            outcome.Rule.Name,
            outcome.Ignore ? "Ignored — no work order is created." : "Creates a work order.",
            outcome.Ignore ? null : title,
            outcome.Priority,
            outcome.WorkOrderType));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<IntakeProtocolResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();

        var item = await dbContext.IntakeProtocols
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Intake protocol was not found.");

        return Ok(MapProtocol(item));
    }

    [HttpPost]
    public async Task<ActionResult<IntakeProtocolResponse>> Create([FromBody] UpsertIntakeProtocolRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        ValidateProtocolRequest(request);

        var protocol = new IntakeProtocol
        {
            TenantId = TenantId
        };

        await ApplyProtocolAsync(protocol, request, cancellationToken);
        dbContext.IntakeProtocols.Add(protocol);
        await dbContext.SaveChangesAsync(cancellationToken);


        await AuditSettingsChangeAsync("Intake protocol created", protocol.Id, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = protocol.Id }, MapProtocol(protocol));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<IntakeProtocolResponse>> Update(Guid id, [FromBody] UpsertIntakeProtocolRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        ValidateProtocolRequest(request);

        var protocol = await dbContext.IntakeProtocols
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Intake protocol was not found.");

        await ApplyProtocolAsync(protocol, request, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);


        await AuditSettingsChangeAsync("Intake protocol updated", protocol.Id, cancellationToken);
        return Ok(MapProtocol(protocol));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();

        var protocol = await dbContext.IntakeProtocols
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Intake protocol was not found.");

        dbContext.IntakeProtocols.Remove(protocol);
        await dbContext.SaveChangesAsync(cancellationToken);

        await AuditSettingsChangeAsync("Intake protocol deleted", protocol.Id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/test")]
    public async Task<ActionResult<IntakeProtocolTestResponse>> Test(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();

        var protocol = await dbContext.IntakeProtocols
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Intake protocol was not found.");

        var sourceType = NormalizeSourceType(protocol.SourceType);

        try
        {
            if (sourceType == "Email")
            {
                var config = Deserialize<EmailProtocolSourceConfig>(protocol.SourceConfigJson) ?? new EmailProtocolSourceConfig();
                var host = NormalizeOptional(config.Host);
                var port = config.Port.GetValueOrDefault();

                if (string.IsNullOrWhiteSpace(host) || port <= 0)
                {
                    throw new BusinessRuleException("Email protocols require a host and port before testing.");
                }

                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(host, port, cancellationToken).AsTask();
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                if (completedTask != connectTask || !client.Connected)
                {
                    throw new TimeoutException("Connection timed out.");
                }

                protocol.LastTriggeredAt = DateTime.UtcNow;
                protocol.LastTriggerStatus = "Mailbox connection test passed";
                protocol.LastError = null;
            }
            else
            {
                var config = Deserialize<MonitoringProtocolSourceConfig>(protocol.SourceConfigJson) ?? new MonitoringProtocolSourceConfig();
                if (string.IsNullOrWhiteSpace(config.ToolType) && string.IsNullOrWhiteSpace(config.WebhookEndpoint))
                {
                    throw new BusinessRuleException("Monitoring protocols require a tool type or webhook endpoint before testing.");
                }

                protocol.LastTriggeredAt = DateTime.UtcNow;
                protocol.LastTriggerStatus = "Monitoring source configuration validated";
                protocol.LastError = null;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return Ok(new IntakeProtocolTestResponse(true, protocol.LastTriggeredAt, protocol.LastTriggerStatus, null));
        }
        catch (Exception ex)
        {
            protocol.LastTriggeredAt = DateTime.UtcNow;
            protocol.LastTriggerStatus = "Test failed";
            protocol.LastError = ex.Message;
            await dbContext.SaveChangesAsync(cancellationToken);
            return Ok(new IntakeProtocolTestResponse(false, protocol.LastTriggeredAt, protocol.LastTriggerStatus, protocol.LastError));
        }
    }

    private async Task ApplyProtocolAsync(IntakeProtocol protocol, UpsertIntakeProtocolRequest request, CancellationToken cancellationToken)
    {
        protocol.Name = request.Name.Trim();
        protocol.SourceType = NormalizeSourceType(request.SourceType);
        protocol.IsActive = request.IsActive;
        protocol.Description = NormalizeOptional(request.Description);
        protocol.CriteriaJson = NormalizeJsonPayload(request.CriteriaJson, "[]");
        protocol.ActionsJson = NormalizeJsonPayload(request.ActionsJson, "{}");
        protocol.SourceConfigJson = NormalizeJsonPayload(request.SourceConfigJson, "{}");

        await ValidateReferencesAsync(protocol.SourceType, protocol.ActionsJson, cancellationToken);
    }

    private async Task ValidateReferencesAsync(string sourceType, string actionsJson, CancellationToken cancellationToken)
    {
        var actions = Deserialize<IntakeActionsEnvelope>(actionsJson) ?? new IntakeActionsEnvelope();
        var workOrder = actions.CreateWorkOrder;

        if (workOrder is null)
        {
            return;
        }

        await ValidateClientIdAsync(workOrder.ClientId, cancellationToken);
        await ValidateBranchIdAsync(workOrder.BranchId, cancellationToken);
        await ValidateAssetIdAsync(workOrder.AssetId, cancellationToken);
        await ValidateAssignmentGroupIdAsync(workOrder.AssignmentGroupId, cancellationToken);
        await ValidateUserIdAsync(workOrder.AssignedUserId, cancellationToken);

        if (string.Equals(sourceType, "Monitoring", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
    }

    private async Task<Guid?> ValidateClientIdAsync(Guid? clientId, CancellationToken cancellationToken)
    {
        if (!clientId.HasValue)
        {
            return null;
        }

        var exists = await dbContext.Clients.AnyAsync(x => x.TenantId == TenantId && x.IsActive && x.Id == clientId.Value, cancellationToken);
        if (!exists)
        {
            throw new BusinessRuleException("Client was not found for this tenant.");
        }

        return clientId.Value;
    }

    private async Task<Guid?> ValidateAssetIdAsync(Guid? assetId, CancellationToken cancellationToken)
    {
        if (!assetId.HasValue)
        {
            return null;
        }

        var exists = await dbContext.Assets.AnyAsync(x => x.TenantId == TenantId && x.Id == assetId.Value, cancellationToken);
        if (!exists)
        {
            throw new BusinessRuleException("Asset was not found for this tenant.");
        }

        return assetId.Value;
    }

    private async Task<Guid?> ValidateBranchIdAsync(Guid? branchId, CancellationToken cancellationToken)
    {
        if (!branchId.HasValue)
        {
            return null;
        }

        var exists = await dbContext.Branches.AnyAsync(x => x.TenantId == TenantId && x.IsActive && x.Id == branchId.Value, cancellationToken);
        if (!exists)
        {
            throw new BusinessRuleException("Branch was not found for this tenant.");
        }

        return branchId.Value;
    }

    private async Task<Guid?> ValidateAssignmentGroupIdAsync(Guid? assignmentGroupId, CancellationToken cancellationToken)
    {
        if (!assignmentGroupId.HasValue)
        {
            return null;
        }

        var exists = await dbContext.AssignmentGroups.AnyAsync(x => x.TenantId == TenantId && x.IsActive && x.Id == assignmentGroupId.Value, cancellationToken);
        if (!exists)
        {
            throw new BusinessRuleException("Assignment group was not found for this tenant.");
        }

        return assignmentGroupId.Value;
    }

    private async Task<Guid?> ValidateUserIdAsync(Guid? userId, CancellationToken cancellationToken)
    {
        if (!userId.HasValue)
        {
            return null;
        }

        var exists = await dbContext.Users.AnyAsync(x => x.TenantId == TenantId && x.IsActive && x.Id == userId.Value, cancellationToken);
        if (!exists)
        {
            throw new BusinessRuleException("Assigned user was not found for this tenant.");
        }

        return userId.Value;
    }

    private async Task AuditSettingsChangeAsync(string action, Guid entityId, CancellationToken cancellationToken)
    {
        await auditLogService.LogAsync(TenantId, UserId, action, nameof(IntakeProtocol), entityId.ToString(), action, cancellationToken);
    }

    private static void ValidateProtocolRequest(UpsertIntakeProtocolRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessRuleException("Protocol name is required.");
        }

        _ = NormalizeSourceType(request.SourceType);
        ValidateJsonPayload(request.CriteriaJson, "CriteriaJson");
        ValidateJsonPayload(request.ActionsJson, "ActionsJson");
        ValidateJsonPayload(request.SourceConfigJson, "SourceConfigJson");
        if (string.Equals(request.SourceType, "Email", StringComparison.OrdinalIgnoreCase)
            && IntakeRules.Validate(request.CriteriaJson, request.ActionsJson) is { } problem)
        {
            throw new BusinessRuleException(problem);
        }
    }

    private static void ValidateJsonPayload(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessRuleException($"{fieldName} is required.");
        }

        try
        {
            using var _ = JsonDocument.Parse(value);
        }
        catch (JsonException)
        {
            throw new BusinessRuleException($"{fieldName} must be valid JSON.");
        }
    }

    private static string NormalizeJsonPayload(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        using var document = JsonDocument.Parse(value);
        return JsonSerializer.Serialize(document.RootElement);
    }

    private static string NormalizeSourceType(string? sourceType) =>
        sourceType?.Trim() switch
        {
            var value when string.Equals(value, "Email", StringComparison.OrdinalIgnoreCase) => "Email",
            var value when string.Equals(value, "Monitoring", StringComparison.OrdinalIgnoreCase) => "Monitoring",
            _ => throw new BusinessRuleException("Source type must be Email or Monitoring.")
        };

    private static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IntakeProtocolResponse MapProtocol(IntakeProtocol item) =>
        new(
            item.Id,
            item.TenantId,
            item.Name,
            item.SourceType,
            item.IsActive,
            item.Description,
            item.CriteriaJson,
            item.ActionsJson,
            item.SourceConfigJson,
            item.CreatedAt,
            item.UpdatedAt,
            item.LastTriggeredAt,
            item.LastTriggerStatus,
            item.LastError);

    private sealed record EmailProtocolSourceConfig
    {
        public string? IntakeEmailAddress { get; init; }
        public string? MonitoredMailbox { get; init; }
        public string? AllowedSenderDomains { get; init; }
        public bool? KnownSenderOnly { get; init; }
        public string? SubjectParsingMode { get; init; }
        public bool? MatchEmailBody { get; init; }
        public bool? AttachmentParsing { get; init; }
        public string? MailboxProvider { get; init; }
        public string? Host { get; init; }
        public int? Port { get; init; }
        public bool? UseSsl { get; init; }
        public string? Username { get; init; }
        public string? Password { get; init; }
        public string? PollingMode { get; init; }
    }

    private sealed record MonitoringProtocolSourceConfig
    {
        public string? ToolType { get; init; }
        public string? WebhookEndpoint { get; init; }
        public string? SecretStatus { get; init; }
        public string? EventSourceMapping { get; init; }
    }

    private sealed record IntakeActionsEnvelope
    {
        public CreateWorkOrderActionConfig? CreateWorkOrder { get; init; }
    }

    private sealed record CreateWorkOrderActionConfig
    {
        public bool Enabled { get; init; }
        public string? WorkOrderTitleTemplate { get; init; }
        public string? WorkOrderDescriptionTemplate { get; init; }
        public string? WorkOrderType { get; init; }
        public string? Priority { get; init; }
        public Guid? ClientId { get; init; }
        public Guid? BranchId { get; init; }
        public Guid? AssetId { get; init; }
        public Guid? AssignmentGroupId { get; init; }
        public Guid? AssignedUserId { get; init; }
        public string? DueDateRule { get; init; }
        public string? Tags { get; init; }
        public bool AutoCreateImmediately { get; init; }
    }
}
