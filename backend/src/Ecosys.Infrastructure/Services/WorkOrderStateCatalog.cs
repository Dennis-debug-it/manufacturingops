using Ecosys.Domain.Entities;
using Ecosys.Domain.Enums;
using Ecosys.Shared.Errors;

namespace Ecosys.Infrastructure.Services;

public static class WorkOrderStatuses
{
    public const string New = "New";
    public const string Open = "Open";
    public const string Assigned = "Assigned";
    public const string Scheduled = "Scheduled";
    public const string InProgress = "In Progress";
    public const string OnHold = "On Hold";
    public const string WaitingOnParts = "Waiting on Parts";
    public const string WaitingOnClient = "Waiting on Client";
    public const string Completed = "Completed";
    public const string ClientAccepted = "Client Accepted";
    public const string Closed = "Closed";
    public const string Cancelled = "Cancelled";
    public const string Rejected = "Rejected";
    public const string Reopened = "Reopened";

    public static readonly IReadOnlyCollection<string> All =
    [
        New,
        Open,
        Assigned,
        Scheduled,
        InProgress,
        OnHold,
        WaitingOnParts,
        WaitingOnClient,
        Completed,
        ClientAccepted,
        Closed,
        Cancelled,
        Rejected,
        Reopened
    ];

    public static readonly string[] ClosedStatuses = [Closed, Cancelled, Rejected];
    public static readonly string[] TerminalStatuses = [Closed, Cancelled, Rejected];
    public static readonly string[] HoldStatuses = [OnHold, WaitingOnParts, WaitingOnClient];

    public static WorkOrderStatus Parse(string? status)
    {
        var normalized = (status ?? string.Empty).Trim();
        return normalized switch
        {
            "Draft" => WorkOrderStatus.New,
            "Pending Materials" or "Awaiting Materials" or "Awaiting Parts" or "Waiting for Spares" or "Awaiting Spares" => WorkOrderStatus.OnHold,
            "Pending Client" or "Awaiting User" or "Awaiting Client" or "Awaiting Approval" or "Awaiting Vendor" or "Waiting for Access" or "Awaiting Site Access" => WorkOrderStatus.OnHold,
            "Paused" or OnHold => WorkOrderStatus.OnHold,
            WaitingOnParts => WorkOrderStatus.WaitingOnParts,
            WaitingOnClient => WorkOrderStatus.WaitingOnClient,
            "Acknowledged" => WorkOrderStatus.ClientAccepted,
            "Ready to Close" => WorkOrderStatus.Completed,
            New => WorkOrderStatus.New,
            Open => WorkOrderStatus.Open,
            Assigned => WorkOrderStatus.Assigned,
            Scheduled => WorkOrderStatus.Scheduled,
            InProgress => WorkOrderStatus.InProgress,
            Completed => WorkOrderStatus.Completed,
            ClientAccepted => WorkOrderStatus.ClientAccepted,
            Closed => WorkOrderStatus.Closed,
            Cancelled => WorkOrderStatus.Cancelled,
            Rejected => WorkOrderStatus.Rejected,
            Reopened => WorkOrderStatus.Reopened,
            _ => throw new BusinessRuleException("Invalid work order status.")
        };
    }

    public static string Normalize(string? status)
        => ToValue(Parse(status));

    // Every status string the app may have persisted: the canonical set plus the legacy synonyms
    // that Parse() accepts. Keep in sync with Parse when adding new legacy inputs. Used to expand a
    // status filter into the full set of stored values that normalize to the same canonical status,
    // so the filter can run as a SQL IN-list (DB-level) yet still match legacy rows.
    private static readonly string[] KnownRawStatuses =
    [
        .. All,
        "Draft",
        "Pending Materials", "Awaiting Materials", "Awaiting Parts", "Waiting for Spares", "Awaiting Spares",
        "Pending Client", "Awaiting User", "Awaiting Client", "Awaiting Approval", "Awaiting Vendor",
        "Waiting for Access", "Awaiting Site Access",
        "Paused",
        "Acknowledged",
        "Ready to Close",
    ];

    /// <summary>
    /// Returns the raw status strings that normalize to the same canonical status as
    /// <paramref name="requested"/>. Throws <see cref="BusinessRuleException"/> if the requested
    /// status is not recognized (same behavior as normalizing an invalid filter value).
    /// </summary>
    public static string[] RawStatusesMatching(string requested)
    {
        var canonical = Normalize(requested);
        var matches = new List<string>();
        foreach (var raw in KnownRawStatuses)
        {
            try
            {
                if (string.Equals(Normalize(raw), canonical, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(raw);
                }
            }
            catch (BusinessRuleException)
            {
                // Not a recognized status literal; skip.
            }
        }

        return matches.ToArray();
    }

    public static bool IsClosed(string? status) =>
        TerminalStatuses.Contains(Normalize(status), StringComparer.OrdinalIgnoreCase);

    public static bool IsHold(string? status) =>
        HoldStatuses.Contains(Normalize(status), StringComparer.OrdinalIgnoreCase);

    public static bool IsOpen(string? status) =>
        !IsClosed(status)
        && !string.Equals(Normalize(status), Completed, StringComparison.OrdinalIgnoreCase)
        && !string.Equals(Normalize(status), ClientAccepted, StringComparison.OrdinalIgnoreCase);

    public static string ToValue(WorkOrderStatus status) => status switch
    {
        WorkOrderStatus.New => New,
        WorkOrderStatus.Open => Open,
        WorkOrderStatus.Assigned => Assigned,
        WorkOrderStatus.Scheduled => Scheduled,
        WorkOrderStatus.InProgress => InProgress,
        WorkOrderStatus.OnHold => OnHold,
        WorkOrderStatus.WaitingOnParts => WaitingOnParts,
        WorkOrderStatus.WaitingOnClient => WaitingOnClient,
        WorkOrderStatus.Completed => Completed,
        WorkOrderStatus.ClientAccepted => ClientAccepted,
        WorkOrderStatus.Closed => Closed,
        WorkOrderStatus.Cancelled => Cancelled,
        WorkOrderStatus.Rejected => Rejected,
        WorkOrderStatus.Reopened => Reopened,
        _ => throw new BusinessRuleException("Invalid work order status.")
    };
}

public static class WorkOrderStatusTransitions
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyCollection<string>> BaseTransitions =
        new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [WorkOrderStatuses.New] = [WorkOrderStatuses.Open, WorkOrderStatuses.Rejected, WorkOrderStatuses.Cancelled],
            [WorkOrderStatuses.Open] = [WorkOrderStatuses.Assigned, WorkOrderStatuses.Scheduled, WorkOrderStatuses.Cancelled],
            [WorkOrderStatuses.Assigned] = [WorkOrderStatuses.Scheduled, WorkOrderStatuses.InProgress, WorkOrderStatuses.OnHold, WorkOrderStatuses.WaitingOnParts, WorkOrderStatuses.WaitingOnClient, WorkOrderStatuses.Cancelled],
            [WorkOrderStatuses.Scheduled] = [WorkOrderStatuses.InProgress, WorkOrderStatuses.OnHold, WorkOrderStatuses.WaitingOnParts, WorkOrderStatuses.WaitingOnClient, WorkOrderStatuses.Cancelled],
            [WorkOrderStatuses.InProgress] = [WorkOrderStatuses.OnHold, WorkOrderStatuses.WaitingOnParts, WorkOrderStatuses.WaitingOnClient, WorkOrderStatuses.Completed],
            [WorkOrderStatuses.OnHold] = [WorkOrderStatuses.Assigned, WorkOrderStatuses.Scheduled, WorkOrderStatuses.InProgress, WorkOrderStatuses.Cancelled],
            [WorkOrderStatuses.WaitingOnParts] = [WorkOrderStatuses.Assigned, WorkOrderStatuses.Scheduled, WorkOrderStatuses.InProgress, WorkOrderStatuses.Cancelled],
            [WorkOrderStatuses.WaitingOnClient] = [WorkOrderStatuses.Assigned, WorkOrderStatuses.Scheduled, WorkOrderStatuses.InProgress, WorkOrderStatuses.Cancelled],
            [WorkOrderStatuses.Completed] = [WorkOrderStatuses.ClientAccepted, WorkOrderStatuses.Reopened, WorkOrderStatuses.Closed],
            [WorkOrderStatuses.ClientAccepted] = [WorkOrderStatuses.Closed, WorkOrderStatuses.Reopened],
            [WorkOrderStatuses.Closed] = [],
            [WorkOrderStatuses.Cancelled] = [],
            [WorkOrderStatuses.Rejected] = [],
            [WorkOrderStatuses.Reopened] = [WorkOrderStatuses.Assigned, WorkOrderStatuses.InProgress],
        };

    public static IReadOnlyCollection<string> GetAllowedTargets(string? currentStatus, bool allowAdminOnlyTransitions)
    {
        var normalizedCurrent = WorkOrderStatuses.Normalize(currentStatus);
        var allowed = BaseTransitions.TryGetValue(normalizedCurrent, out var targets)
            ? targets.ToList()
            : [];

        if (allowAdminOnlyTransitions && (string.Equals(normalizedCurrent, WorkOrderStatuses.Closed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedCurrent, WorkOrderStatuses.Cancelled, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedCurrent, WorkOrderStatuses.Rejected, StringComparison.OrdinalIgnoreCase)))
        {
            allowed.Add(WorkOrderStatuses.Reopened);
        }

        return allowed;
    }

    public static bool CanTransition(string? currentStatus, string? targetStatus, bool allowAdminOnlyTransitions) =>
        GetAllowedTargets(currentStatus, allowAdminOnlyTransitions)
            .Contains(WorkOrderStatuses.Normalize(targetStatus), StringComparer.OrdinalIgnoreCase);

    public static bool IsAdminOnlyTransition(string? currentStatus, string? targetStatus)
    {
        var normalizedCurrent = WorkOrderStatuses.Normalize(currentStatus);
        var normalizedTarget = WorkOrderStatuses.Normalize(targetStatus);

        if (string.Equals(normalizedTarget, WorkOrderStatuses.Closed, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(normalizedTarget, WorkOrderStatuses.Reopened, StringComparison.OrdinalIgnoreCase)
            && (string.Equals(normalizedCurrent, WorkOrderStatuses.Closed, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedCurrent, WorkOrderStatuses.Cancelled, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedCurrent, WorkOrderStatuses.Rejected, StringComparison.OrdinalIgnoreCase));
    }
}

public static class WorkOrderHoldReasons
{
    public const string AwaitingMaterials = "Awaiting Materials";
    public const string AwaitingClient = "Awaiting Client";
    public const string AwaitingVendor = "Awaiting Vendor";
    public const string AwaitingApproval = "Awaiting Approval";
    public const string AwaitingSiteAccess = "Awaiting Site Access";
    public const string AwaitingSpares = "Awaiting Spares";
    public const string Other = "Other";

    public const string PendingClient = AwaitingClient;
    public const string PendingMaterials = AwaitingMaterials;

    public static readonly IReadOnlyCollection<string> All =
    [
        AwaitingMaterials,
        AwaitingClient,
        AwaitingVendor,
        AwaitingApproval,
        AwaitingSiteAccess,
        AwaitingSpares,
        Other
    ];

    public static WorkOrderHoldReason Parse(string? holdReason)
    {
        var normalized = holdReason?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new BusinessRuleException("Hold reason is required.");
        }

        return normalized switch
        {
            PendingClient or AwaitingClient or "Awaiting User" => WorkOrderHoldReason.AwaitingClient,
            PendingMaterials or AwaitingMaterials or "Awaiting Parts" => WorkOrderHoldReason.AwaitingMaterials,
            AwaitingVendor => WorkOrderHoldReason.AwaitingVendor,
            AwaitingApproval => WorkOrderHoldReason.AwaitingApproval,
            AwaitingSiteAccess or "Waiting for Access" => WorkOrderHoldReason.AwaitingSiteAccess,
            AwaitingSpares or "Waiting for Spares" => WorkOrderHoldReason.AwaitingSpares,
            Other => WorkOrderHoldReason.Other,
            _ => throw new BusinessRuleException("Invalid hold reason.")
        };
    }

    public static string? Normalize(string? holdReason)
    {
        var normalized = holdReason?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return ToValue(Parse(normalized));
    }

    public static string ToValue(WorkOrderHoldReason holdReason) => holdReason switch
    {
        WorkOrderHoldReason.AwaitingMaterials => AwaitingMaterials,
        WorkOrderHoldReason.AwaitingClient => AwaitingClient,
        WorkOrderHoldReason.AwaitingVendor => AwaitingVendor,
        WorkOrderHoldReason.AwaitingApproval => AwaitingApproval,
        WorkOrderHoldReason.AwaitingSiteAccess => AwaitingSiteAccess,
        WorkOrderHoldReason.AwaitingSpares => AwaitingSpares,
        WorkOrderHoldReason.Other => Other,
        _ => throw new BusinessRuleException("Invalid hold reason.")
    };

    public static string? FromLegacyStatus(string? status)
    {
        var normalized = (status ?? string.Empty).Trim();
        return normalized switch
        {
            "Pending Client" or "Awaiting User" or "Awaiting Client" => AwaitingClient,
            "Pending Materials" or "Awaiting Materials" or "Awaiting Parts" => AwaitingMaterials,
            "Awaiting Vendor" => AwaitingVendor,
            "Awaiting Approval" => AwaitingApproval,
            "Waiting for Access" or "Awaiting Site Access" => AwaitingSiteAccess,
            "Waiting for Spares" or "Awaiting Spares" => AwaitingSpares,
            _ => null
        };
    }

    public static string BuildMessage(string? holdReason, string? note)
    {
        var normalizedReason = Normalize(holdReason);
        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        return normalizedReason switch
        {
            null => trimmedNote is null ? "Work order placed on hold." : $"Work order placed on hold. Note: {trimmedNote}",
            _ => trimmedNote is null ? $"On hold: {normalizedReason}" : $"On hold: {normalizedReason} | Note: {trimmedNote}",
        };
    }

    public static (string? Reason, string? Note) ResolveCurrentDetails(WorkOrder workOrder)
    {
        if (!WorkOrderStatuses.IsHold(workOrder.Status))
        {
            return (null, null);
        }

        if (!string.IsNullOrWhiteSpace(workOrder.HoldReason) || !string.IsNullOrWhiteSpace(workOrder.HoldNote))
        {
            return (Normalize(workOrder.HoldReason) ?? FromLegacyStatus(workOrder.Status), workOrder.HoldNote?.Trim());
        }

        var latestHoldEvent = workOrder.Events
            .Where(x => string.Equals(x.EventType, "StatusChanged", StringComparison.OrdinalIgnoreCase) && WorkOrderStatuses.IsHold(x.Status))
            .OrderByDescending(x => x.OccurredAt)
            .FirstOrDefault();

        return ParseLegacyMessage(latestHoldEvent?.Message, workOrder.Status);
    }

    public static (string? Reason, string? Note) ParseLegacyMessage(string? message, string? status)
    {
        var fallbackReason = FromLegacyStatus(status);
        if (string.IsNullOrWhiteSpace(message))
        {
            return (fallbackReason, null);
        }

        const string holdPrefix = "On hold: ";
        if (message.StartsWith(holdPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var payload = message[holdPrefix.Length..];
            var parts = payload.Split(" | Note: ", 2, StringSplitOptions.None);
            var reason = string.IsNullOrWhiteSpace(parts[0]) ? fallbackReason : Normalize(parts[0]) ?? fallbackReason;
            var note = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1].Trim() : null;
            return (reason, note);
        }

        const string pausePrefix = "Work paused. Note: ";
        if (message.StartsWith(pausePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return (fallbackReason, message[pausePrefix.Length..].Trim());
        }

        const string legacyHoldPrefix = "Work order placed on hold. Note: ";
        if (message.StartsWith(legacyHoldPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return (fallbackReason, message[legacyHoldPrefix.Length..].Trim());
        }

        return (fallbackReason, null);
    }
}

public static class WorkOrderHoldTypes
{
    public const string OnHold = "OnHold";
    public const string WaitingOnParts = "WaitingOnParts";
    public const string WaitingOnClient = "WaitingOnClient";

    public static readonly IReadOnlyCollection<string> All = [OnHold, WaitingOnParts, WaitingOnClient];

    public static string ToStatus(string? holdType)
    {
        var normalized = string.IsNullOrWhiteSpace(holdType) ? OnHold : holdType.Trim();
        return normalized switch
        {
            OnHold => WorkOrderStatuses.OnHold,
            WaitingOnParts => WorkOrderStatuses.WaitingOnParts,
            WaitingOnClient => WorkOrderStatuses.WaitingOnClient,
            _ => throw new BusinessRuleException("Invalid hold type. Use OnHold, WaitingOnParts, or WaitingOnClient.")
        };
    }

    public static string ToHoldReason(string? holdType)
    {
        var normalized = string.IsNullOrWhiteSpace(holdType) ? OnHold : holdType.Trim();
        return normalized switch
        {
            WaitingOnParts => WorkOrderHoldReasons.AwaitingMaterials,
            WaitingOnClient => WorkOrderHoldReasons.AwaitingClient,
            _ => WorkOrderHoldReasons.Other
        };
    }

    public static string FromStatus(string? status)
    {
        var normalized = WorkOrderStatuses.Normalize(status);
        return normalized switch
        {
            WorkOrderStatuses.WaitingOnParts => WaitingOnParts,
            WorkOrderStatuses.WaitingOnClient => WaitingOnClient,
            _ => OnHold
        };
    }
}

public static class WorkOrderPriorities
{
    public const string Low = nameof(WorkOrderPriority.Low);
    public const string Medium = nameof(WorkOrderPriority.Medium);
    public const string High = nameof(WorkOrderPriority.High);
    public const string Critical = nameof(WorkOrderPriority.Critical);

    public static readonly IReadOnlyCollection<string> All = [Low, Medium, High, Critical];

    public static WorkOrderPriority Parse(string? priority, WorkOrderPriority fallback = WorkOrderPriority.Medium)
    {
        var normalized = string.IsNullOrWhiteSpace(priority) ? ToValue(fallback) : priority.Trim();
        return normalized switch
        {
            Low => WorkOrderPriority.Low,
            Medium => WorkOrderPriority.Medium,
            High => WorkOrderPriority.High,
            Critical => WorkOrderPriority.Critical,
            _ => throw new BusinessRuleException("Priority must be Low, Medium, High, or Critical.")
        };
    }

    public static string Normalize(string? priority, string? fallback = null) =>
        ToValue(Parse(priority, string.IsNullOrWhiteSpace(fallback) ? WorkOrderPriority.Medium : Parse(fallback)));

    public static string ToValue(WorkOrderPriority priority) => priority switch
    {
        WorkOrderPriority.Low => Low,
        WorkOrderPriority.Medium => Medium,
        WorkOrderPriority.High => High,
        WorkOrderPriority.Critical => Critical,
        _ => throw new BusinessRuleException("Invalid priority.")
    };
}

public static class WorkOrderAssignmentTypes
{
    public const string Unassigned = nameof(WorkOrderAssignmentType.Unassigned);
    public const string IndividualTechnician = nameof(WorkOrderAssignmentType.IndividualTechnician);
    public const string MultipleTechnicians = nameof(WorkOrderAssignmentType.MultipleTechnicians);
    public const string AssignmentGroup = nameof(WorkOrderAssignmentType.AssignmentGroup);

    public static readonly IReadOnlyCollection<string> All =
    [
        Unassigned,
        IndividualTechnician,
        MultipleTechnicians,
        AssignmentGroup
    ];

    public static WorkOrderAssignmentType Parse(string? assignmentType)
    {
        var normalized = string.IsNullOrWhiteSpace(assignmentType) ? Unassigned : assignmentType.Trim();
        return normalized switch
        {
            Unassigned => WorkOrderAssignmentType.Unassigned,
            IndividualTechnician => WorkOrderAssignmentType.IndividualTechnician,
            MultipleTechnicians => WorkOrderAssignmentType.MultipleTechnicians,
            AssignmentGroup => WorkOrderAssignmentType.AssignmentGroup,
            _ => throw new BusinessRuleException("Invalid assignment type.")
        };
    }

    public static string Normalize(string? assignmentType) => ToValue(Parse(assignmentType));

    public static string ToValue(WorkOrderAssignmentType assignmentType) => assignmentType switch
    {
        WorkOrderAssignmentType.Unassigned => Unassigned,
        WorkOrderAssignmentType.IndividualTechnician => IndividualTechnician,
        WorkOrderAssignmentType.MultipleTechnicians => MultipleTechnicians,
        WorkOrderAssignmentType.AssignmentGroup => AssignmentGroup,
        _ => throw new BusinessRuleException("Invalid assignment type.")
    };
}
