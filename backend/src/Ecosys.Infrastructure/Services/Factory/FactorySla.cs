using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services.Factory;

public interface IFactorySlaService
{
    /// <summary>The tenant's matrix, created with the defaults the first time.</summary>
    Task<IReadOnlyList<SlaMatrixCell>> GetMatrixAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the job's response and repair deadlines from its priority and its asset's criticality
    /// (C when the asset has none, or there is no asset), counted from when the job was raised.
    /// </summary>
    Task ApplyAsync(WorkOrder workOrder, string? assetCriticality, CancellationToken cancellationToken = default);
}

internal sealed class FactorySlaService(AppDbContext dbContext) : IFactorySlaService
{
    /// <summary>Hours to respond / repair, by priority then criticality A, B, C. A starting point the factory tunes.</summary>
    internal static readonly IReadOnlyDictionary<string, (decimal Response, decimal Repair)[]> Defaults = new Dictionary<string, (decimal, decimal)[]>
    {
        ["Critical"] = [(0.5m, 4), (1, 8), (2, 24)],
        ["High"] = [(1, 8), (2, 24), (4, 48)],
        ["Medium"] = [(4, 24), (8, 48), (24, 72)],
        ["Low"] = [(24, 72), (48, 120), (72, 168)],
    };

    public async Task<IReadOnlyList<SlaMatrixCell>> GetMatrixAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var cells = await dbContext.SlaMatrixCells.Where(x => x.TenantId == tenantId).ToListAsync(cancellationToken);
        if (cells.Count > 0)
        {
            return cells;
        }

        foreach (var (priority, row) in Defaults)
        {
            for (var i = 0; i < AssetCriticality.All.Count; i++)
            {
                var cell = new SlaMatrixCell { TenantId = tenantId, Priority = priority, Criticality = AssetCriticality.All[i], ResponseHours = row[i].Response, RepairHours = row[i].Repair };
                dbContext.SlaMatrixCells.Add(cell);
                cells.Add(cell);
            }
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another request created it first.
            cells.ForEach(x => dbContext.Entry(x).State = EntityState.Detached);
            cells = await dbContext.SlaMatrixCells.Where(x => x.TenantId == tenantId).ToListAsync(cancellationToken);
        }

        return cells;
    }

    public async Task ApplyAsync(WorkOrder workOrder, string? assetCriticality, CancellationToken cancellationToken = default)
    {
        var matrix = await GetMatrixAsync(workOrder.TenantId, cancellationToken);
        var criticality = assetCriticality ?? AssetCriticality.C;
        var cell = matrix.FirstOrDefault(x => x.Priority == workOrder.Priority && x.Criticality == criticality);
        if (cell is null)
        {
            return;
        }

        var from = workOrder.CreatedAt;
        workOrder.AppliedSlaSource = "PriorityCriticalityMatrix";
        workOrder.AppliedResponseHours = cell.ResponseHours;
        workOrder.AppliedResolutionHours = cell.RepairHours;
        workOrder.SlaResponseDeadline = from.AddHours((double)cell.ResponseHours);
        workOrder.SlaResolutionDeadline = from.AddHours((double)cell.RepairHours);
        workOrder.DueDate ??= workOrder.SlaResolutionDeadline;
    }
}
