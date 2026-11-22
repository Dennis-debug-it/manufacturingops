using Ecosys.Domain.Entities;
using Ecosys.Domain.Enums;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests.Factory;

/// <summary>
/// Factory jobs carry no branch and their own work types. Users limited to a branch (technicians, in the demo and in
/// any workspace with a branch) must still reach them, and the older screens must be able to read their types.
/// </summary>
public sealed class BranchlessFactoryRecordsTests
{
    [Theory]
    [InlineData("Corrective", WorkOrderType.CorrectiveMaintenance)]
    [InlineData("Safety", WorkOrderType.CorrectiveMaintenance)]
    [InlineData("Preventive", WorkOrderType.PreventiveMaintenance)]
    [InlineData("Inspection", WorkOrderType.InspectionAssessment)]
    [InlineData("Statutory", WorkOrderType.InspectionAssessment)]
    [InlineData("Modification", WorkOrderType.NewInstallation)]
    public void FactoryWorkTypes_ReadAsTheOlderTypes(string factoryType, WorkOrderType expected)
    {
        Assert.Equal(expected, WorkOrderTypes.Parse(factoryType));
    }

    [Fact]
    public async Task BranchLimitedUser_SeesRecordsWithNoBranch_ButNotOtherBranches()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"branchless-{Guid.NewGuid():N}").Options);
        var tenantId = Guid.NewGuid();
        var mine = new Branch { TenantId = tenantId, Name = "Kapkoros", Code = "KPK", IsActive = true };
        var other = new Branch { TenantId = tenantId, Name = "Litein", Code = "LTN", IsActive = true };
        var user = new User
        {
            TenantId = tenantId, FullName = "Kevin", Email = "kevin@kapkoros.test", PasswordHash = "x",
            Role = "User", IsActive = true, HasAllBranchAccess = false,
        };
        db.AddRange(mine, other, user);
        db.UserBranchAssignments.Add(new UserBranchAssignment { TenantId = tenantId, UserId = user.Id, BranchId = mine.Id, IsDefault = true });
        db.WorkOrders.AddRange(
            Job(tenantId, "WO-1", null),
            Job(tenantId, "WO-2", mine.Id),
            Job(tenantId, "WO-3", other.Id));
        await db.SaveChangesAsync();

        var access = new BranchAccessService(db, new FactoryTenantContext(tenantId, user.Id, isAdmin: false));
        var scope = await access.GetQueryScopeAsync(tenantId, null);
        var visible = await db.WorkOrders.Where(x => x.TenantId == tenantId).WhereAccessible(scope, x => x.BranchId)
            .Select(x => x.WorkOrderNumber).OrderBy(x => x).ToListAsync();

        Assert.Equal(["WO-1", "WO-2"], visible);

        // Picking a branch still narrows to that branch alone.
        var narrowed = await access.GetQueryScopeAsync(tenantId, mine.Id);
        Assert.Equal(["WO-2"], await db.WorkOrders.Where(x => x.TenantId == tenantId).WhereAccessible(narrowed, x => x.BranchId)
            .Select(x => x.WorkOrderNumber).ToListAsync());
    }

    private static WorkOrder Job(Guid tenantId, string number, Guid? branchId) => new()
    {
        TenantId = tenantId, WorkOrderNumber = number, Title = number, Priority = "Medium", Status = "Assigned",
        WorkType = "Corrective", BranchId = branchId,
    };
}
