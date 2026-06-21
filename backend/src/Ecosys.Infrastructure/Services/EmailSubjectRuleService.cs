using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Options;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

public interface IEmailSubjectRuleService
{
    Task<EmailSubjectRuleOptions> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<string> BuildFinalSubjectAsync(
        Guid? tenantId,
        string eventKey,
        string templateSubject,
        string? tenantNameOverride = null,
        CancellationToken cancellationToken = default);
}

public sealed class EmailSubjectRuleService(AppDbContext dbContext) : IEmailSubjectRuleService
{
    private const string Category = "platform-email-subject-rules";

    private static readonly IReadOnlyDictionary<string, string> EventTags =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["email.test"] = "[Test]",
            ["platform.user.created"] = "[Account]",
            ["platform.user.credentials.resent"] = "[Account]",
            ["tenant.user.created"] = "[Account]",
            ["tenant.user.credentials.resent"] = "[Account]",
            ["tenant.onboarding"] = "[Onboarding]",
            ["platform.lead.received"] = "[Lead]",
            ["auth.password-reset.requested"] = "[Security]",
            ["user.password-reset.admin"] = "[Security]",
            ["work-order.assigned"] = "[Work Order]",
            ["work-order.overdue"] = "[Work Order]",
            ["pm.due"] = "[Maintenance]",
            ["material.request.submitted"] = "[Materials]",
            ["smtp.failure"] = "[System]",
        };

    public async Task<EmailSubjectRuleOptions> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var row = await dbContext.PlatformSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Category == Category, cancellationToken);

        if (row is null || string.IsNullOrWhiteSpace(row.JsonValue))
        {
            return EmailSubjectRuleOptions.Default;
        }

        return System.Text.Json.JsonSerializer.Deserialize<EmailSubjectRuleOptions>(row.JsonValue)
            ?? EmailSubjectRuleOptions.Default;
    }

    public async Task<string> BuildFinalSubjectAsync(
        Guid? tenantId,
        string eventKey,
        string templateSubject,
        string? tenantNameOverride = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return await BuildFinalSubjectAsync(settings, tenantId, eventKey, templateSubject, tenantNameOverride, cancellationToken);
    }

    public async Task<string> BuildFinalSubjectAsync(
        EmailSubjectRuleOptions settings,
        Guid? tenantId,
        string eventKey,
        string templateSubject,
        string? tenantNameOverride = null,
        CancellationToken cancellationToken = default)
    {
        var template = (templateSubject ?? string.Empty).Trim();
        var prefix = NormalizeToken(settings.SubjectPrefix);
        var suffix = NormalizeToken(settings.SubjectSuffix);
        var environmentTag = settings.IncludeEnvironmentInSubject ? NormalizeTag(settings.EnvironmentLabel) : null;
        var eventTag = settings.EnableEventSubjectTags && EventTags.TryGetValue(eventKey, out var tag) ? tag : null;
        var tenantTag = settings.IncludeTenantNameInSubject
            ? NormalizeTag(await ResolveTenantNameAsync(tenantId, tenantNameOverride, cancellationToken))
            : null;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(prefix) && !template.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add(prefix);
        }

        if (!string.IsNullOrWhiteSpace(eventTag))
        {
            parts.Add(eventTag);
        }

        if (!string.IsNullOrWhiteSpace(environmentTag))
        {
            parts.Add(environmentTag);
        }

        if (!string.IsNullOrWhiteSpace(tenantTag))
        {
            parts.Add(tenantTag);
        }

        if (!string.IsNullOrWhiteSpace(template))
        {
            parts.Add(template);
        }

        if (!string.IsNullOrWhiteSpace(suffix))
        {
            parts.Add(suffix);
        }

        return string.Join(" ", parts.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
    }

    private async Task<string?> ResolveTenantNameAsync(Guid? tenantId, string? tenantNameOverride, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(tenantNameOverride))
        {
            return tenantNameOverride.Trim();
        }

        if (!tenantId.HasValue || tenantId.Value == PlatformConstants.RootTenantId)
        {
            return null;
        }

        return await dbContext.Tenants
            .Where(x => x.Id == tenantId.Value)
            .Select(x => x.CompanyName)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static string? NormalizeToken(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeTag(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.StartsWith('[') && trimmed.EndsWith(']')
            ? trimmed
            : $"[{trimmed}]";
    }
}
