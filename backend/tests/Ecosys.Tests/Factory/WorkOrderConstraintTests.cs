using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Ecosys.Tests.Factory;

/// <summary>
/// The PostgreSQL check constraints on work orders list the allowed values. The in-memory test
/// database doesn't enforce them, so this checks every factory value is in the list.
/// </summary>
public sealed class WorkOrderConstraintTests
{
    private static string Constraint(string name)
    {
        using var db = FactoryTestSupport.CreateDbContext();
        return db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(WorkOrder))!.GetCheckConstraints().Single(x => x.Name == name).Sql;
    }

    [Fact]
    public void Every_factory_status_is_allowed()
    {
        var sql = Constraint("CK_work_orders_status");
        string[] statuses = [WorkOrderStatuses.Requested, WorkOrderStatuses.Approved, WorkOrderStatuses.Planned, WorkOrderStatuses.Assigned,
            WorkOrderStatuses.InProgress, WorkOrderStatuses.OnHold, WorkOrderStatuses.Completed, WorkOrderStatuses.Verified, WorkOrderStatuses.Closed,
            WorkOrderStatuses.Cancelled, WorkOrderStatuses.Rejected, WorkOrderStatuses.Reopened];
        Assert.All(statuses, status => Assert.Contains($"'{status}'", sql));
    }

    [Fact]
    public void Every_factory_work_type_is_allowed()
    {
        var sql = Constraint("CK_work_orders_work_type");
        Assert.All(FactoryWorkTypes.All, type => Assert.Contains($"'{type}'", sql));
    }

    [Fact]
    public void Every_factory_hold_reason_is_allowed()
    {
        var sql = Constraint("CK_work_orders_hold_reason");
        Assert.All(FactoryHoldReasons.All, reason => Assert.Contains($"'{reason}'", sql));
    }

    [Fact]
    public void Every_factory_priority_is_allowed()
    {
        var sql = Constraint("CK_work_orders_priority");
        Assert.All(FactoryPriorities.All, priority => Assert.Contains($"'{priority}'", sql));
    }
}
