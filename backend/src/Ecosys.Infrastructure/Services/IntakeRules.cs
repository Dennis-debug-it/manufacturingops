using System.Text.Json;
using System.Text.Json.Serialization;
using Ecosys.Domain.Entities;

namespace Ecosys.Infrastructure.Services;

/// <summary>An incoming request as the intake rules see it.</summary>
public sealed record IntakeRuleInput(string Subject, string Body, string FromAddress, string? FromName);

/// <summary>What a matched rule does: drop the email, or shape the work order it creates.</summary>
public sealed record IntakeRuleOutcome(
    IntakeProtocol Rule,
    bool Ignore,
    string? Priority,
    string? WorkOrderType,
    Guid? ClientId,
    Guid? AssignmentGroupId,
    string? TitleTemplate);

/// <summary>
/// Intake rules ("intake protocols"): conditions on an incoming email (subject, body, sender) and what
/// to do when they match. Rules are tried oldest first and the first match wins.
///
/// Stored as JSON on <see cref="IntakeProtocol"/>: <c>CriteriaJson</c> holds condition groups (a rule
/// matches when any group matches; a group matches when all — or any, with logic "OR" — of its
/// conditions do), <c>ActionsJson</c> holds <c>createWorkOrder</c> settings or <c>ignore</c>.
/// </summary>
public static class IntakeRules
{
    public static readonly string[] Fields = ["Subject", "Body", "Subject or Body", "Sender Email", "Sender Domain", "Sender Name"];
    public static readonly string[] Operators = ["Contains", "Contains Any Of", "Does Not Contain", "Equals", "Starts With", "Ends With", "Is Empty", "Is Not Empty"];

    /// <summary>The auto-created rule older versions made from the mailbox settings; it matched everything.</summary>
    public const string LegacyMarker = "legacy-email-subject";

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static IntakeRuleOutcome? FirstMatch(IEnumerable<IntakeProtocol> rules, IntakeRuleInput input)
    {
        foreach (var rule in rules.Where(x => x.IsActive && !x.CriteriaJson.Contains(LegacyMarker, StringComparison.Ordinal)).OrderBy(x => x.CreatedAt))
        {
            if (Matches(rule.CriteriaJson, input))
            {
                return ToOutcome(rule);
            }
        }

        return null;
    }

    public static bool Matches(string criteriaJson, IntakeRuleInput input)
    {
        var groups = Parse<List<Group>>(criteriaJson) ?? [];
        var usable = groups.Where(group => group.Criteria is { Count: > 0 }).ToList();
        if (usable.Count == 0) return false;

        return usable.Any(group =>
            string.Equals(group.Logic, "OR", StringComparison.OrdinalIgnoreCase)
                ? group.Criteria!.Any(criterion => Test(criterion, input))
                : group.Criteria!.All(criterion => Test(criterion, input)));
    }

    /// <summary>Checks a rule is usable before it's saved; returns why not, or null.</summary>
    public static string? Validate(string criteriaJson, string actionsJson)
    {
        var groups = Parse<List<Group>>(criteriaJson);
        if (groups is null || !groups.Any(group => group.Criteria is { Count: > 0 }))
            return "Add at least one condition.";
        foreach (var criterion in groups.SelectMany(group => group.Criteria ?? []))
        {
            if (!Fields.Contains(criterion.Field ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                return $"Unknown field '{criterion.Field}'.";
            if (!Operators.Contains(criterion.Operator ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                return $"Unknown condition '{criterion.Operator}'.";
            var needsValue = !string.Equals(criterion.Operator, "Is Empty", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(criterion.Operator, "Is Not Empty", StringComparison.OrdinalIgnoreCase);
            if (needsValue && string.IsNullOrWhiteSpace(criterion.Value))
                return $"Enter a value for the '{criterion.Field} {criterion.Operator}' condition.";
        }

        var actions = Parse<Actions>(actionsJson) ?? new Actions();
        if (actions.Ignore?.Enabled != true && actions.CreateWorkOrder is null)
            return "Choose what the rule does.";
        if (actions.CreateWorkOrder?.Priority is { Length: > 0 } priority && !WorkOrderPriorities.All.Contains(priority))
            return $"Unknown priority '{priority}'.";
        return null;
    }

    private static IntakeRuleOutcome ToOutcome(IntakeProtocol rule)
    {
        var actions = Parse<Actions>(rule.ActionsJson) ?? new Actions();
        if (actions.Ignore?.Enabled == true)
        {
            return new IntakeRuleOutcome(rule, true, null, null, null, null, null);
        }

        var create = actions.CreateWorkOrder;
        var workType = string.IsNullOrWhiteSpace(create?.WorkOrderType) ? null : WorkOrderTypes.Normalize(create.WorkOrderType, WorkOrderTypes.CorrectiveMaintenance);
        var priority = create?.Priority is { Length: > 0 } value && WorkOrderPriorities.All.Contains(value) ? value : null;
        return new IntakeRuleOutcome(rule, false, priority, workType, create?.ClientId, create?.AssignmentGroupId, create?.WorkOrderTitleTemplate);
    }

    private static bool Test(Criterion criterion, IntakeRuleInput input)
    {
        var text = (criterion.Field ?? string.Empty).ToLowerInvariant() switch
        {
            "subject" => input.Subject,
            "body" => input.Body,
            "subject or body" => $"{input.Subject}\n{input.Body}",
            "sender email" => input.FromAddress,
            "sender domain" => input.FromAddress.Split('@').LastOrDefault() ?? string.Empty,
            "sender name" => input.FromName ?? string.Empty,
            _ => null,
        };
        if (text is null) return false;
        var value = (criterion.Value ?? string.Empty).Trim();
        const StringComparison ignoreCase = StringComparison.OrdinalIgnoreCase;

        return (criterion.Operator ?? string.Empty).ToLowerInvariant() switch
        {
            "contains" => value.Length > 0 && text.Contains(value, ignoreCase),
            "contains any of" => SplitList(value).Any(word => text.Contains(word, ignoreCase)),
            "does not contain" => value.Length > 0 && !text.Contains(value, ignoreCase),
            "equals" => string.Equals(text.Trim(), value, ignoreCase),
            "starts with" => value.Length > 0 && text.TrimStart().StartsWith(value, ignoreCase),
            "ends with" => value.Length > 0 && text.TrimEnd().EndsWith(value, ignoreCase),
            "is empty" => string.IsNullOrWhiteSpace(text),
            "is not empty" => !string.IsNullOrWhiteSpace(text),
            _ => false,
        };
    }

    /// <summary>Fills {{subject}}, {{sender}} and {{senderName}} in a title template.</summary>
    public static string ApplyTitle(string? template, string subject, string fromAddress, string? fromName)
    {
        if (string.IsNullOrWhiteSpace(template)) return subject;
        var title = template
            .Replace("{{subject}}", subject, StringComparison.OrdinalIgnoreCase)
            .Replace("{{senderName}}", string.IsNullOrWhiteSpace(fromName) ? fromAddress : fromName, StringComparison.OrdinalIgnoreCase)
            .Replace("{{sender}}", fromAddress, StringComparison.OrdinalIgnoreCase)
            .Trim();
        return title.Length == 0 ? subject : title;
    }

    private static IEnumerable<string> SplitList(string value) =>
        value.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(word => word.Length > 0);

    private static T? Parse<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<T>(json, Json); }
        catch (JsonException) { return null; }
    }

    private sealed record Group
    {
        public string? Logic { get; init; }
        public List<Criterion>? Criteria { get; init; }
    }

    private sealed record Criterion
    {
        public string? Field { get; init; }
        [JsonPropertyName("operator")]
        public string? Operator { get; init; }
        public string? Value { get; init; }
    }

    private sealed record Actions
    {
        public CreateAction? CreateWorkOrder { get; init; }
        public IgnoreAction? Ignore { get; init; }
    }

    private sealed record CreateAction
    {
        public string? WorkOrderTitleTemplate { get; init; }
        public string? WorkOrderType { get; init; }
        public string? Priority { get; init; }
        public Guid? ClientId { get; init; }
        public Guid? AssignmentGroupId { get; init; }
    }

    private sealed record IgnoreAction
    {
        public bool Enabled { get; init; }
    }
}

/// <summary>A ready-made intake rule a workspace can add with one click, then adjust.</summary>
public sealed record IntakeRuleTemplate(string Key, string Name, string Description, string CriteriaJson, string ActionsJson);

/// <summary>Common rules for field-service teams: urgency, trades, and mail that isn't a request.</summary>
public static class IntakeRuleTemplates
{
    private static string AnyOf(string field, string words) =>
        JsonSerializer.Serialize(new[] { new { logic = "AND", criteria = new[] { new { field, @operator = "Contains Any Of", value = words } } } });

    private static string Create(string? priority = null, string? workType = null, string? title = null) =>
        JsonSerializer.Serialize(new { createWorkOrder = new { priority, workOrderType = workType, workOrderTitleTemplate = title } });

    public static readonly IReadOnlyList<IntakeRuleTemplate> All =
    [
        new("ignore-automated", "Ignore newsletters and no-reply mail",
            "Mail from no-reply, newsletter and marketing addresses never becomes a work order.",
            AnyOf("Sender Email", "no-reply, noreply, donotreply, newsletter, marketing, mailer"),
            JsonSerializer.Serialize(new { ignore = new { enabled = true } })),
        new("urgent", "Urgent requests are Critical",
            "Emails that say urgent, emergency or ASAP get Critical priority.",
            AnyOf("Subject or Body", "urgent, emergency, asap, critical, immediately"),
            Create(WorkOrderPriorities.Critical, WorkOrderTypes.CorrectiveMaintenance)),
        new("power-outage", "Power outage or no power",
            "Loss of power is a High-priority breakdown.",
            AnyOf("Subject or Body", "no power, power outage, power failure, blackout, tripped, power cut"),
            Create(WorkOrderPriorities.High, WorkOrderTypes.CorrectiveMaintenance, "Power issue: {{subject}}")),
        new("generator-fault", "Generator faults",
            "Generator and genset problems are logged as breakdowns, tagged in the title.",
            AnyOf("Subject or Body", "generator, genset, gen set, standby power"),
            Create(WorkOrderPriorities.High, WorkOrderTypes.CorrectiveMaintenance, "[Generator] {{subject}}")),
        new("hvac-not-cooling", "Air conditioning not cooling",
            "AC, chiller and HVAC complaints are logged as breakdowns, tagged in the title.",
            AnyOf("Subject or Body", "not cooling, air con, aircon, air conditioner, hvac, chiller, too hot"),
            Create(WorkOrderPriorities.Medium, WorkOrderTypes.CorrectiveMaintenance, "[HVAC] {{subject}}")),
        new("water-leak", "Water leaks",
            "Leaks and flooding get High priority.",
            AnyOf("Subject or Body", "leak, leaking, flooding, flooded, burst pipe, water damage"),
            Create(WorkOrderPriorities.High, WorkOrderTypes.CorrectiveMaintenance, "Leak: {{subject}}")),
        new("planned-service", "Planned service requests",
            "Requests for routine servicing are logged as preventive maintenance at Low priority.",
            AnyOf("Subject or Body", "service due, routine service, scheduled maintenance, pm visit, maintenance request"),
            Create(WorkOrderPriorities.Low, WorkOrderTypes.PreventiveMaintenance)),
        new("installation", "New installation requests",
            "Quotes and requests for new equipment are logged as installations.",
            AnyOf("Subject or Body", "new installation, install a, installation of, supply and install"),
            Create(WorkOrderPriorities.Medium, WorkOrderTypes.NewInstallation)),
    ];
}
