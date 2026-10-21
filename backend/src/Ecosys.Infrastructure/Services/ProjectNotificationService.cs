using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services;

public static class ProjectNotificationKeys
{
    public const string MemberAdded = "project.member-added";
    public const string TaskAssigned = "project.task-assigned";
    public const string VariationSubmitted = "project.variation-submitted";
    public const string VariationDecided = "project.variation-decided";
    public const string TaskDue = "project.task-due";
    public const string MilestoneDue = "project.milestone-due";
    public const string MilestoneOverdue = "project.milestone-overdue";
    public const string EndDateApproaching = "project.end-date-approaching";
    public const string MusterMissing = "project.muster-missing";
    public const string ValuationCertified = "project.valuation-certified";
    public const string RetentionDue = "project.retention-due";
}

/// <summary>
/// Email (through the outbox) and web push for project events. Each channel honours the
/// recipient's <see cref="UserNotificationPreference"/>; a missing row means "on". Failures are
/// logged and swallowed: a notification must never undo the change that triggered it.
/// </summary>
public interface IProjectNotificationService
{
    Task MemberAddedAsync(Guid tenantId, Project project, Guid userId, string role, CancellationToken cancellationToken = default);
    Task TaskAssignedAsync(Guid tenantId, Project project, ProjectTask task, CancellationToken cancellationToken = default);
    Task VariationSubmittedAsync(Guid tenantId, Project project, ProjectVariation variation, CancellationToken cancellationToken = default);
    Task VariationDecidedAsync(Guid tenantId, Project project, ProjectVariation variation, CancellationToken cancellationToken = default);
    Task ValuationCertifiedAsync(Guid tenantId, Project project, ProjectValuation valuation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a project notification to the given users. <paramref name="values"/> are the template
    /// placeholders; RecipientName, ProjectNumber and ProjectName are added for each recipient.
    /// </summary>
    Task NotifyAsync(Guid tenantId, string key, Project project, IReadOnlyCollection<Guid> userIds, IReadOnlyDictionary<string, string?> values,
        string pushTitle, string pushBody, string pushTag, CancellationToken cancellationToken = default);

    /// <summary>The project manager plus active users in the given tenant roles.</summary>
    Task<IReadOnlyList<Guid>> ManagerAndRolesAsync(Guid tenantId, Project project, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);
}

internal sealed class ProjectNotificationService(
    AppDbContext dbContext,
    IEmailTemplateService emailTemplateService,
    IEmailSubjectRuleService emailSubjectRuleService,
    IEmailOutboxService emailOutboxService,
    IPushNotificationQueue pushNotificationQueue,
    ILogger<ProjectNotificationService> logger) : IProjectNotificationService
{
    public Task MemberAddedAsync(Guid tenantId, Project project, Guid userId, string role, CancellationToken cancellationToken = default) =>
        SendAsync(tenantId, ProjectNotificationKeys.MemberAdded, project, [userId],
            recipient => new Dictionary<string, string?>
            {
                ["RecipientName"] = recipient.FullName,
                ["ProjectNumber"] = project.ProjectNumber,
                ["ProjectName"] = project.Name,
                ["Role"] = HumanizeRole(role),
            },
            new PushMessage($"Added to {project.ProjectNumber}", $"You are on the {project.Name} team as {HumanizeRole(role)}.", ProjectUrl(project), $"project-member-{project.Id}"),
            cancellationToken);

    public Task TaskAssignedAsync(Guid tenantId, Project project, ProjectTask task, CancellationToken cancellationToken = default) =>
        task.AssigneeUserId is not Guid assignee
            ? Task.CompletedTask
            : SendAsync(tenantId, ProjectNotificationKeys.TaskAssigned, project, [assignee],
                recipient => new Dictionary<string, string?>
                {
                    ["RecipientName"] = recipient.FullName,
                    ["ProjectNumber"] = project.ProjectNumber,
                    ["ProjectName"] = project.Name,
                    ["TaskTitle"] = task.Title,
                    ["DueDate"] = task.DueDate?.ToString("yyyy-MM-dd") ?? "Not set",
                },
                new PushMessage($"Task on {project.ProjectNumber}", task.Title, ProjectUrl(project), $"project-task-{task.Id}"),
                cancellationToken);

    public async Task VariationSubmittedAsync(Guid tenantId, Project project, ProjectVariation variation, CancellationToken cancellationToken = default)
    {
        // Approvers: tenant admins and managers (the role that holds projects.variations.approve by
        // default), plus the project manager. An approval matrix sends its own approver reminders.
        var approvers = await dbContext.Users
            .Where(x => x.TenantId == tenantId && x.IsActive && (x.Role == AppRoles.Admin || x.Role == AppRoles.Manager))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (project.ProjectManagerUserId.HasValue)
            approvers.Add(project.ProjectManagerUserId.Value);
        approvers.Remove(variation.SubmittedByUserId ?? Guid.Empty);

        await SendAsync(tenantId, ProjectNotificationKeys.VariationSubmitted, project, approvers.Distinct().ToList(),
            recipient => VariationValues(recipient, project, variation),
            new PushMessage($"Variation {variation.VariationNumber} to review", $"{variation.Kind} of {project.Currency} {variation.Amount:N2} on {project.ProjectNumber}.", ProjectUrl(project), $"project-variation-{variation.Id}"),
            cancellationToken);
    }

    public async Task VariationDecidedAsync(Guid tenantId, Project project, ProjectVariation variation, CancellationToken cancellationToken = default)
    {
        var recipients = new List<Guid>();
        if (variation.SubmittedByUserId.HasValue)
            recipients.Add(variation.SubmittedByUserId.Value);
        if (project.ProjectManagerUserId.HasValue)
            recipients.Add(project.ProjectManagerUserId.Value);
        recipients.Remove(variation.DecidedByUserId ?? Guid.Empty);

        await SendAsync(tenantId, ProjectNotificationKeys.VariationDecided, project, recipients.Distinct().ToList(),
            recipient => VariationValues(recipient, project, variation),
            new PushMessage($"Variation {variation.VariationNumber} {variation.Status.ToLowerInvariant()}", $"{project.ProjectNumber}: {variation.Title}", ProjectUrl(project), $"project-variation-{variation.Id}"),
            cancellationToken);
    }

    public async Task ValuationCertifiedAsync(Guid tenantId, Project project, ProjectValuation valuation, CancellationToken cancellationToken = default)
    {
        var recipients = await ManagerAndRolesAsync(tenantId, project, [AppRoles.Admin, AppRoles.FinanceUser], cancellationToken);
        var amount = $"{project.Currency} {valuation.CertifiedAmount ?? valuation.AmountDue:N2}";
        await NotifyAsync(tenantId, ProjectNotificationKeys.ValuationCertified, project, recipients,
            new Dictionary<string, string?>
            {
                ["ValuationNumber"] = valuation.ValuationNumber,
                ["CertificateReference"] = valuation.CertificateReference,
                ["CertifiedAmount"] = amount,
            },
            $"Valuation {valuation.ValuationNumber} certified", $"{project.ProjectNumber}: {amount} certified. Raise the invoice.", $"project-valuation-{valuation.Id}",
            cancellationToken);
    }

    public Task NotifyAsync(Guid tenantId, string key, Project project, IReadOnlyCollection<Guid> userIds, IReadOnlyDictionary<string, string?> values,
        string pushTitle, string pushBody, string pushTag, CancellationToken cancellationToken = default) =>
        SendAsync(tenantId, key, project, userIds.Distinct().ToList(),
            recipient => new Dictionary<string, string?>(values)
            {
                ["RecipientName"] = recipient.FullName,
                ["ProjectNumber"] = project.ProjectNumber,
                ["ProjectName"] = project.Name,
            },
            new PushMessage(pushTitle, pushBody, ProjectUrl(project), pushTag),
            cancellationToken);

    public async Task<IReadOnlyList<Guid>> ManagerAndRolesAsync(Guid tenantId, Project project, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default)
    {
        var users = await dbContext.Users.IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.IsActive && roles.Contains(x.Role))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (project.ProjectManagerUserId.HasValue)
            users.Add(project.ProjectManagerUserId.Value);
        return users.Distinct().ToList();
    }

    private static Dictionary<string, string?> VariationValues(RecipientInfo recipient, Project project, ProjectVariation variation) => new()
    {
        ["RecipientName"] = recipient.FullName,
        ["ProjectNumber"] = project.ProjectNumber,
        ["ProjectName"] = project.Name,
        ["VariationNumber"] = variation.VariationNumber,
        ["VariationTitle"] = variation.Title,
        ["Kind"] = variation.Kind,
        ["Amount"] = $"{project.Currency} {variation.Amount:N2}",
        ["Status"] = variation.Status,
    };

    private async Task SendAsync(
        Guid tenantId,
        string eventKey,
        Project project,
        IReadOnlyCollection<Guid> userIds,
        Func<RecipientInfo, Dictionary<string, string?>> values,
        PushMessage push,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
            return;

        try
        {
            var recipients = await dbContext.Users
                .Where(x => x.TenantId == tenantId && x.IsActive && userIds.Contains(x.Id))
                .Select(x => new RecipientInfo(x.Id, x.FullName, x.Email))
                .ToListAsync(cancellationToken);
            var preferences = await dbContext.UserNotificationPreferences
                .Where(x => x.TenantId == tenantId && userIds.Contains(x.UserId) && x.NotificationKey == eventKey)
                .ToListAsync(cancellationToken);

            var pushUsers = recipients
                .Where(r => preferences.FirstOrDefault(p => p.UserId == r.Id)?.InAppEnabled ?? true)
                .Select(r => r.Id)
                .ToList();
            if (pushUsers.Count > 0)
                pushNotificationQueue.Enqueue(tenantId, pushUsers, push);

            foreach (var recipient in recipients.Where(r => !string.IsNullOrWhiteSpace(r.Email)))
            {
                if (!(preferences.FirstOrDefault(p => p.UserId == recipient.Id)?.EmailEnabled ?? true))
                    continue;

                var template = await emailTemplateService.RenderTenantTemplateAsync(tenantId, eventKey, values(recipient), cancellationToken);
                if (!template.Enabled)
                    continue;
                var subject = await emailSubjectRuleService.BuildFinalSubjectAsync(tenantId, eventKey, template.Subject, cancellationToken: cancellationToken);
                await emailOutboxService.QueueEmailAsync(new QueueEmailRequest(
                    tenantId, eventKey, eventKey, recipient.Email!, recipient.FullName, template.SenderNameOverride,
                    null, null, subject, template.HtmlBody, template.TextBody, null), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send {EventKey} notifications for project {ProjectId}.", eventKey, project.Id);
        }
    }

    private static string ProjectUrl(Project project) => $"/projects/{project.Id}";

    private static string HumanizeRole(string role) => role switch
    {
        ProjectMemberRoles.ProjectManager => "Project Manager",
        _ => role,
    };

    private sealed record RecipientInfo(Guid Id, string FullName, string? Email);
}
