using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ecosys.Tests;

/// <summary>
/// En route / arrival / departure write one <see cref="WorkOrderVisit"/> per trip, so a job that runs
/// over several days records every day's travel and on-site time instead of only the first arrival.
/// </summary>
public sealed class WorkOrderVisitTests
{
    private static readonly DateTime Day1 = new(2026, 10, 5, 7, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day2 = Day1.AddDays(1);

    [Fact]
    public async Task MultiDayJob_RecordsOneVisitPerTrip_WithTravelAndOnSiteMinutes()
    {
        await using var db = CreateDbContext();
        var (tenantId, workOrderId, technicianId) = await SeedAsync(db);
        var workflow = CreateWorkflow(db);

        await workflow.RecordInTransitAsync(tenantId, workOrderId, null, new(technicianId, null, null, Day1, null));
        await workflow.RecordArrivalAsync(tenantId, workOrderId, null, new(technicianId, null, null, Day1.AddMinutes(45), null));
        await workflow.RecordDepartureAsync(tenantId, workOrderId, null, new(technicianId, null, null, Day1.AddHours(9), null));
        await workflow.RecordInTransitAsync(tenantId, workOrderId, null, new(technicianId, null, null, Day2, null));
        await workflow.RecordArrivalAsync(tenantId, workOrderId, null, new(technicianId, null, null, Day2.AddMinutes(30), null));
        await workflow.RecordDepartureAsync(tenantId, workOrderId, null, new(technicianId, null, null, Day2.AddHours(4), null));

        var visits = await db.WorkOrderVisits.OrderBy(x => x.TravelStartedAt).ToListAsync();
        Assert.Equal(2, visits.Count);
        Assert.Equal([45, 30], visits.Select(x => x.TravelMinutes));
        Assert.Equal([495, 210], visits.Select(x => x.OnSiteMinutes));

        // The first arrival stays the response milestone; departure tracks the latest one.
        var assignment = await db.WorkOrderTechnicianAssignments.SingleAsync();
        Assert.Equal(Day1.AddMinutes(45), assignment.ArrivalAt);
        Assert.Equal(Day2.AddHours(4), assignment.DepartureAt);
    }

    [Fact]
    public async Task OnDayTwo_TheLatestArrivalIsKept_SoTheTechnicianShowsOnSiteDespiteYesterdaysDeparture()
    {
        await using var db = CreateDbContext();
        var (tenantId, workOrderId, technicianId) = await SeedAsync(db);
        var workflow = CreateWorkflow(db);

        await workflow.RecordArrivalAsync(tenantId, workOrderId, null, new(technicianId, null, null, Day1, null));
        await workflow.RecordDepartureAsync(tenantId, workOrderId, null, new(technicianId, null, null, Day1.AddHours(8), null));
        await workflow.RecordArrivalAsync(tenantId, workOrderId, null, new(technicianId, null, null, Day2, null));

        var assignment = await db.WorkOrderTechnicianAssignments.SingleAsync();
        Assert.Equal(Day1, assignment.ArrivalAt);
        Assert.Equal(Day2, assignment.LastArrivalAt);
        // On site: the latest arrival is after the last departure, which is still yesterday's.
        Assert.True(assignment.LastArrivalAt > assignment.DepartureAt);

        await workflow.RecordDepartureAsync(tenantId, workOrderId, null, new(technicianId, null, null, Day2.AddHours(6), null));
        Assert.True(assignment.DepartureAt > assignment.LastArrivalAt);
    }

    [Fact]
    public async Task StartingTheNextTrip_WhileStillOnSite_IsRejected()
    {
        await using var db = CreateDbContext();
        var (tenantId, workOrderId, technicianId) = await SeedAsync(db);
        var workflow = CreateWorkflow(db);
        await workflow.RecordArrivalAsync(tenantId, workOrderId, null, new(technicianId, null, null, Day1, null));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            workflow.RecordInTransitAsync(tenantId, workOrderId, null, new(technicianId, null, null, Day1.AddHours(1), null)));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            workflow.RecordArrivalAsync(tenantId, workOrderId, null, new(technicianId, null, null, Day1.AddHours(1), null)));
    }

    [Fact]
    public async Task Departure_WithoutAnArrival_IsRejected()
    {
        await using var db = CreateDbContext();
        var (tenantId, workOrderId, technicianId) = await SeedAsync(db);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateWorkflow(db).RecordDepartureAsync(tenantId, workOrderId, null, new(technicianId, null, null, Day1, null)));
    }

    [Fact]
    public async Task Departure_ForATechnicianWhoArrivedBeforeVisitsExisted_BuildsTheVisitFromThatArrival()
    {
        await using var db = CreateDbContext();
        var (tenantId, workOrderId, technicianId) = await SeedAsync(db);
        var assignment = await db.WorkOrderTechnicianAssignments.SingleAsync();
        assignment.ArrivalAt = Day1;
        assignment.Status = "Arrived";
        await db.SaveChangesAsync();

        await CreateWorkflow(db).RecordDepartureAsync(tenantId, workOrderId, null, new(technicianId, null, null, Day1.AddHours(2), null));

        var visit = await db.WorkOrderVisits.SingleAsync();
        Assert.Equal(120, visit.OnSiteMinutes);
    }

    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"ecosys-visits-{Guid.NewGuid():N}").Options);

    private static async Task<(Guid TenantId, Guid WorkOrderId, Guid TechnicianId)> SeedAsync(AppDbContext db)
    {
        var tenantId = Guid.NewGuid();
        var client = new Client { TenantId = tenantId, ClientName = "Acme Hospital", IsActive = true };
        var technician = new Technician { TenantId = tenantId, FullName = "Field Tech", Email = $"tech-{Guid.NewGuid():N}@acme.test", IsActive = true };
        db.AddRange(client, technician);
        await db.SaveChangesAsync();

        var workOrder = new WorkOrder
        {
            TenantId = tenantId, ClientId = client.Id, WorkOrderNumber = "WO-VISIT-1", Title = "Chiller overhaul",
            Status = WorkOrderStatuses.InProgress, Priority = "Medium", WorkType = "CorrectiveMaintenance",
        };
        db.WorkOrders.Add(workOrder);
        db.WorkOrderTechnicianAssignments.Add(new WorkOrderTechnicianAssignment
        {
            TenantId = tenantId, WorkOrderId = workOrder.Id, TechnicianId = technician.Id, IsLead = true, Status = "Accepted",
        });
        await db.SaveChangesAsync();
        return (tenantId, workOrder.Id, technician.Id);
    }

    private static WorkOrderAssignmentWorkflowService CreateWorkflow(AppDbContext db) =>
        new(db, new AuditLogService(db, new HttpContextAccessor()), null!, null!, null!, null!, new NoOpEtaNotificationService(),
            null!, NullLogger<WorkOrderAssignmentWorkflowService>.Instance);

    private sealed class NoOpEtaNotificationService : IEtaNotificationService
    {
        public Task NotifyAsync(Guid tenantId, WorkOrder workOrder, string eventType, string? technicianName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
