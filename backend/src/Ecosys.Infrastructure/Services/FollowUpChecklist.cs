using System.Text.RegularExpressions;
using Ecosys.Domain.Entities;

namespace Ecosys.Infrastructure.Services;

/// <summary>Rules for follow-up (return visit) work orders.</summary>
public static partial class FollowUpChecklist
{
    private static readonly string[] YesValues = ["yes", "y", "true", "checked", "1", "required"];

    /// <summary>A follow-up is for finished work: completed, client-accepted or closed jobs.</summary>
    public static bool CanBeFollowedUp(string? status)
    {
        var normalized = WorkOrderStatuses.Normalize(status);
        return string.Equals(normalized, WorkOrderStatuses.Completed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, WorkOrderStatuses.ClientAccepted, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, WorkOrderStatuses.Closed, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True when the technician answered a "Follow-up required" checklist question (as seeded by the
    /// starter packs, or any question mentioning a follow-up) with yes. Lets the office see that another
    /// visit is needed without reading every checklist.
    /// </summary>
    public static bool IsRequested(IEnumerable<WorkOrderChecklistItem>? items) =>
        items is not null && items.Any(item =>
            FollowUpQuestion().IsMatch(item.QuestionText ?? string.Empty)
            && YesValues.Contains((item.ResponseValue ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase));

    [GeneratedRegex(@"follow[\s-]?up", RegexOptions.IgnoreCase)]
    private static partial Regex FollowUpQuestion();
}
