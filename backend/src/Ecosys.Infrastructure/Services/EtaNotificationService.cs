using System.Security.Cryptography;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecosys.Infrastructure.Services;

public static class TrackingTokenGenerator
{
    public static string Generate() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(9))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
}

public interface IEtaNotificationService
{
    Task NotifyAsync(Guid tenantId, WorkOrder workOrder, string eventType, string? technicianName, CancellationToken cancellationToken = default);
}

public static class EtaNotificationTemplateDefaults
{
    public static readonly IReadOnlyDictionary<string, (string Subject, string Body)> Templates =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            [EtaNotificationEventTypes.Assigned] = (
                "Your service request {{woNumber}} has been assigned",
                "Hi {{clientName}},\n\n{{technicianName}} has been assigned to your job {{woNumber}}.\n\nTrack progress here: {{trackingLink}}"),
            [EtaNotificationEventTypes.EnRoute] = (
                "Your technician is on the way — {{woNumber}}",
                "Hi {{clientName}},\n\n{{technicianName}} is now en route for job {{woNumber}}.\n\nTrack live status here: {{trackingLink}}"),
            [EtaNotificationEventTypes.Arrived] = (
                "{{technicianName}} has arrived — {{woNumber}}",
                "Hi {{clientName}},\n\n{{technicianName}} has arrived on site for job {{woNumber}}.\n\nTrack status here: {{trackingLink}}"),
            [EtaNotificationEventTypes.Completed] = (
                "Job {{woNumber}} completed",
                "Hi {{clientName}},\n\nYour job {{woNumber}} has been completed by {{technicianName}}.\n\nView details here: {{trackingLink}}"),
        };

    public static (string Subject, string Body) Resolve(string eventType) =>
        Templates.TryGetValue(eventType, out var template)
            ? template
            : ("Update on {{woNumber}}", "There is a status update on job {{woNumber}}.");
}

internal sealed class EtaNotificationService(
    AppDbContext dbContext,
    IEmailOutboxService emailOutboxService,
    IOptions<PublicAppOptions> publicAppOptions,
    ILogger<EtaNotificationService> logger,
    IClientWhatsAppNotifier? clientWhatsAppNotifier = null) : IEtaNotificationService
{
    public async Task NotifyAsync(Guid tenantId, WorkOrder workOrder, string eventType, string? technicianName, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!workOrder.NotifyClient) return;

            await TryNotifyWhatsAppAsync(tenantId, workOrder, eventType, technicianName, cancellationToken);

            var settings = await dbContext.EtaNotificationSettings
                .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.EventType == eventType, cancellationToken);

            if (settings is not null && !settings.Enabled) return;

            var client = workOrder.ClientId != Guid.Empty
                ? await dbContext.Clients
                    .Where(x => x.Id == workOrder.ClientId)
                    .Select(x => new { x.Email, x.ClientName })
                    .SingleOrDefaultAsync(cancellationToken)
                : null;

            var recipientEmail = workOrder.ClientContactEmail ?? client?.Email;
            if (string.IsNullOrWhiteSpace(recipientEmail)) return;

            await EnsureTrackingTokenAsync(workOrder, cancellationToken);

            var defaults = EtaNotificationTemplateDefaults.Resolve(eventType);

            var subjectTemplate = !string.IsNullOrWhiteSpace(settings?.SubjectTemplate) ? settings!.SubjectTemplate : defaults.Subject;
            var bodyTemplate = !string.IsNullOrWhiteSpace(settings?.BodyTemplate) ? settings!.BodyTemplate : defaults.Body;

            var trackingLink = $"{publicAppOptions.Value.PublicUrl.TrimEnd('/')}/track/{workOrder.TrackingToken}";
            var placeholders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["clientName"] = client?.ClientName ?? "Valued Client",
                ["technicianName"] = string.IsNullOrWhiteSpace(technicianName) ? "your technician" : technicianName,
                ["woNumber"] = workOrder.WorkOrderNumber,
                ["trackingLink"] = trackingLink
            };

            var subject = ApplyPlaceholders(subjectTemplate, placeholders);
            var body = ApplyPlaceholders(bodyTemplate, placeholders);
            var eventKey = $"eta.{eventType.ToLowerInvariant()}";
            var htmlBody = $"<p>{System.Net.WebUtility.HtmlEncode(body).Replace("\n", "<br/>")}</p>";

            await emailOutboxService.QueueEmailAsync(
                new QueueEmailRequest(
                    tenantId, eventKey, eventKey,
                    recipientEmail, client?.ClientName,
                    null, null, null,
                    subject, htmlBody, body, null),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to queue ETA notification ({EventType}) for work order {WorkOrderId}.", eventType, workOrder.Id);
        }
    }

    /// <summary>
    /// The client WhatsApp messages for "on the way" and "completed" (Release 3 design 4.2). Independent of
    /// the email settings below; the client notifier checks the tenant's allowed keys and contact consent.
    /// </summary>
    private async Task TryNotifyWhatsAppAsync(Guid tenantId, WorkOrder workOrder, string eventType, string? technicianName, CancellationToken cancellationToken)
    {
        var key = eventType switch
        {
            EtaNotificationEventTypes.EnRoute => ClientWhatsAppKeys.TechnicianOnTheWay,
            EtaNotificationEventTypes.Completed => ClientWhatsAppKeys.WorkOrderCompleted,
            _ => null,
        };
        if (key is null || clientWhatsAppNotifier is null || workOrder.ClientId == Guid.Empty)
            return;

        try
        {
            await EnsureTrackingTokenAsync(workOrder, cancellationToken);
            await clientWhatsAppNotifier.NotifyClientAsync(tenantId, workOrder.ClientId, key, new Dictionary<string, string?>
            {
                ["TechnicianName"] = string.IsNullOrWhiteSpace(technicianName) ? "Your technician" : technicianName,
                ["WorkOrderNumber"] = workOrder.WorkOrderNumber,
                ["TrackingLink"] = $"{publicAppOptions.Value.PublicUrl.TrimEnd('/')}/track/{workOrder.TrackingToken}",
            }, "WorkOrder", workOrder.Id,
                onceScope: key == ClientWhatsAppKeys.WorkOrderCompleted ? workOrder.Id.ToString("N") : null,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to queue WhatsApp {EventType} for work order {WorkOrderId}.", eventType, workOrder.Id);
        }
    }

    private async Task EnsureTrackingTokenAsync(WorkOrder workOrder, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(workOrder.TrackingToken)) return;

        workOrder.TrackingToken = TrackingTokenGenerator.Generate();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string ApplyPlaceholders(string template, IReadOnlyDictionary<string, string> values)
    {
        var result = template;
        foreach (var (key, value) in values)
        {
            result = result.Replace("{{" + key + "}}", value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }
}
