using Ecosys.Domain.Entities;
using Ecosys.Shared.Errors;

namespace Ecosys.Infrastructure.Services;

/// <summary>
/// The work-order list's <c>state</c> filter (<c>open</c> or <c>overdue</c>), so a dashboard figure can
/// link to exactly the rows it counts. Open means not completed, acknowledged, closed or cancelled, as
/// the dashboards have always counted it; overdue is open and past its due date.
/// </summary>
public static class WorkOrderListStates
{
    public const string Open = "open";
    public const string Overdue = "overdue";

    public static readonly string[] NonOpenStatuses = ["Completed", "Acknowledged", "Closed", "Cancelled"];

    public static IQueryable<WorkOrder> Apply(IQueryable<WorkOrder> query, string state, DateTime utcNow) =>
        state.Trim().ToLowerInvariant() switch
        {
            Open => query.Where(x => !NonOpenStatuses.Contains(x.Status)),
            Overdue => query.Where(x => !NonOpenStatuses.Contains(x.Status) && x.DueDate != null && x.DueDate < utcNow),
            _ => throw new BusinessRuleException("State filter must be open or overdue."),
        };
}
