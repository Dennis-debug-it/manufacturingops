using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Microsoft.EntityFrameworkCore;

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
/// Project events, sent through <see cref="INotificationDispatcher"/>: the bell, push and email, each
/// honouring the recipient's <see cref="UserNotificationPreference"/> (a missing row means the key's
/// defaults). Failures are logged and swallowed: a notification must never undo the change that triggered it.
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
    /// placeholders; ProjectNumber and ProjectName are added, and RecipientName for each recipient.
    /// </summary>
    Task NotifyAsync(Guid tenantId, string key, Project project, IReadOnlyCollection<Guid> userIds, IReadOnlyDictionary<string, string?> values,
        string title, string body, CancellationToken cancellationToken = default);

    /// <summary>The project manager plus active users in the given tenant roles.</summary>
    Task<IReadOnlyList<Guid>> ManagerAndRolesAsync(Guid tenantId, Project project, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);
}

internal sealed class ProjectNotificationService(
    AppDbContext dbContext,
    INotificationDispatcher dispatcher) : IProjectNotificationService
{
    public Task MemberAddedAsync(Guid tenantId, Project project, Guid userId, string role, CancellationToken cancellationToken = default) =>
        NotifyAsync(tenantId, ProjectNotificationKeys.MemberAdded, project, [userId],
            new Dictionary<string, string?> { ["Role"] = HumanizeRole(role) },
            $"Added to {project.ProjectNumber}", $"You are on the {project.Name} team as {HumanizeRole(role)}.",
            cancellationToken);

    public Task TaskAssignedAsync(Guid tenantId, Project project, ProjectTask task, CancellationToken cancellationToken = default) =>
        task.AssigneeUserId is not Guid assignee
            ? Task.CompletedTask
            : NotifyAsync(tenantId, ProjectNotificationKeys.TaskAssigned, project, [assignee],
                new Dictionary<string, string?>
                {
                    ["TaskTitle"] = task.Title,
                    ["DueDate"] = task.DueDate?.ToString("yyyy-MM-dd") ?? "Not set",
                },
                $"Task on {project.ProjectNumber}", task.Title,
                cancellationToken, url: $"{ProjectUrl(project)}?tab=tasks");

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

        await NotifyAsync(tenantId, ProjectNotificationKeys.VariationSubmitted, project, approvers,
            VariationValues(project, variation),
            $"Variation {variation.VariationNumber} to review", $"{variation.Kind} of {project.Currency} {variation.Amount:N2} on {project.ProjectNumber}.",
            cancellationToken, url: $"{ProjectUrl(project)}?tab=variations");
    }

    public async Task VariationDecidedAsync(Guid tenantId, Project project, ProjectVariation variation, CancellationToken cancellationToken = default)
    {
        var recipients = new List<Guid>();
        if (variation.SubmittedByUserId.HasValue)
            recipients.Add(variation.SubmittedByUserId.Value);
        if (project.ProjectManagerUserId.HasValue)
            recipients.Add(project.ProjectManagerUserId.Value);
        recipients.Remove(variation.DecidedByUserId ?? Guid.Empty);

        await NotifyAsync(tenantId, ProjectNotificationKeys.VariationDecided, project, recipients,
            VariationValues(project, variation),
            $"Variation {variation.VariationNumber} {variation.Status.ToLowerInvariant()}", $"{project.ProjectNumber}: {variation.Title}",
            cancellationToken, url: $"{ProjectUrl(project)}?tab=variations");
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
            $"Valuation {valuation.ValuationNumber} certified", $"{project.ProjectNumber}: {amount} certified. Raise the invoice.",
            cancellationToken, url: $"{ProjectUrl(project)}?tab=valuations");
    }

    public Task NotifyAsync(Guid tenantId, string key, Project project, IReadOnlyCollection<Guid> userIds, IReadOnlyDictionary<string, string?> values,
        string title, string body, CancellationToken cancellationToken = default) =>
        NotifyAsync(tenantId, key, project, userIds, values, title, body, cancellationToken, url: null);

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

    private Task NotifyAsync(Guid tenantId, string key, Project project, IReadOnlyCollection<Guid> userIds, IReadOnlyDictionary<string, string?> values,
        string title, string body, CancellationToken cancellationToken, string? url) =>
        dispatcher.NotifyAsync(new NotificationRequest(
            tenantId, key, userIds, title, body, url ?? ProjectUrl(project), "Project", project.Id,
            new Dictionary<string, string?>(values)
            {
                ["ProjectNumber"] = project.ProjectNumber,
                ["ProjectName"] = project.Name,
            }), cancellationToken);

    private static Dictionary<string, string?> VariationValues(Project project, ProjectVariation variation) => new()
    {
        ["VariationNumber"] = variation.VariationNumber,
        ["VariationTitle"] = variation.Title,
        ["Kind"] = variation.Kind,
        ["Amount"] = $"{project.Currency} {variation.Amount:N2}",
        ["Status"] = variation.Status,
    };

    private static string ProjectUrl(Project project) => $"/projects/{project.Id}";

    private static string HumanizeRole(string role) => role switch
    {
        ProjectMemberRoles.ProjectManager => "Project Manager",
        _ => role,
    };
}
