using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services.Factory;

public sealed record FailureCodeTemplate(string Kind, string Code, string Name, string? AssetClassCode = null);

/// <summary>
/// The default failure codes: problems (failure modes), causes (failure mechanisms) and remedies
/// (maintenance activities) following ISO 14224's codes, plus tea-machinery problems tied to their
/// asset class. Tenants edit their own copy.
/// </summary>
public static class FailureCodeTemplates
{
    private const string P = FailureCodeKinds.Problem;
    private const string C = FailureCodeKinds.Cause;
    private const string R = FailureCodeKinds.Remedy;

    public static readonly IReadOnlyList<FailureCodeTemplate> Default =
    [
        // Failure modes (ISO 14224 Annex B)
        new(P, "BRD", "Breakdown"),
        new(P, "FTS", "Fails to start"),
        new(P, "STP", "Fails to stop"),
        new(P, "SPS", "Spurious stop"),
        new(P, "ERO", "Erratic output"),
        new(P, "LOO", "Low output"),
        new(P, "HIO", "High output"),
        new(P, "OHE", "Overheating"),
        new(P, "VIB", "Vibration"),
        new(P, "NOI", "Noise"),
        new(P, "ELP", "External leak (process medium)"),
        new(P, "ELU", "External leak (utility: oil, water, steam)"),
        new(P, "PLU", "Plugged or choked"),
        new(P, "STD", "Structural deficiency"),
        new(P, "AIR", "Abnormal instrument reading"),
        new(P, "PDE", "Parameter deviation"),
        new(P, "SER", "Minor in-service problem"),
        new(P, "OTH", "Other"),
        // Tea machinery
        new(P, "CTC-TTH", "Roller teeth blunt or worn", "TEA-CTC"),
        new(P, "CTC-GAP", "Roller gap out of setting", "TEA-CTC"),
        new(P, "RV-CHK", "Rotorvane choking", "TEA-RV"),
        new(P, "FBD-FIRE", "Fire in dryer chamber", "TEA-FBD"),
        new(P, "FBD-BED", "Uneven or choked fluid bed", "TEA-FBD"),
        new(P, "WTR-MESH", "Trough mesh torn", "TEA-WTR"),
        new(P, "WF-IMP", "Fan impeller damaged or unbalanced", "TEA-WFN"),
        new(P, "BLR-LOW", "Low steam pressure", "TEA-BLR"),
        new(P, "BLR-WTR", "Low water level trip", "TEA-BLR"),
        new(P, "WB-CAL", "Weighbridge reading out of calibration", "TEA-WB"),

        // Failure mechanisms (ISO 14224 Table B.2)
        new(C, "1.1", "Leakage"),
        new(C, "1.2", "Vibration"),
        new(C, "1.3", "Clearance or alignment failure"),
        new(C, "1.4", "Deformation"),
        new(C, "1.5", "Looseness"),
        new(C, "1.6", "Sticking"),
        new(C, "2.1", "Cavitation"),
        new(C, "2.2", "Corrosion"),
        new(C, "2.3", "Erosion"),
        new(C, "2.4", "Wear"),
        new(C, "2.5", "Breakage"),
        new(C, "2.6", "Fatigue"),
        new(C, "2.7", "Overheating"),
        new(C, "2.8", "Burst"),
        new(C, "3.1", "Control failure"),
        new(C, "3.4", "Out of adjustment"),
        new(C, "4.1", "Short circuit"),
        new(C, "4.2", "Open circuit"),
        new(C, "4.3", "No power or voltage"),
        new(C, "4.4", "Faulty power or voltage"),
        new(C, "4.5", "Earth or isolation fault"),
        new(C, "5.1", "Blockage or plugged"),
        new(C, "5.2", "Contamination"),
        new(C, "5.3", "Miscellaneous external influence"),
        new(C, "6.1", "No cause found"),
        new(C, "6.2", "Combined causes"),
        new(C, "6.3", "Other"),
        new(C, "LUB", "Lubrication failure"),
        new(C, "OPR", "Operating error"),

        // Maintenance activities (ISO 14224 Table B.5)
        new(R, "REP", "Replace"),
        new(R, "RPR", "Repair"),
        new(R, "MOD", "Modify"),
        new(R, "ADJ", "Adjust"),
        new(R, "RFT", "Refit"),
        new(R, "CHK", "Check"),
        new(R, "SVC", "Service (clean, lubricate)"),
        new(R, "TST", "Test"),
        new(R, "INS", "Inspect"),
        new(R, "OVH", "Overhaul"),
        new(R, "CMB", "Combination of activities"),
        new(R, "OTH", "Other"),
    ];
}

public interface IFailureCodeService
{
    /// <summary>Gives a tenant the default codes the first time it needs them.</summary>
    Task EnsureSeededAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

internal sealed class FailureCodeService(AppDbContext dbContext) : IFailureCodeService
{
    public async Task EnsureSeededAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (await dbContext.FailureCodes.AnyAsync(x => x.TenantId == tenantId, cancellationToken))
        {
            return;
        }

        var classes = await dbContext.AssetCategories
            .Where(x => x.TenantId == tenantId && x.Code != null)
            .ToDictionaryAsync(x => x.Code!, x => x.Id, cancellationToken);

        var order = 0;
        foreach (var template in FailureCodeTemplates.Default)
        {
            dbContext.FailureCodes.Add(new FailureCode
            {
                TenantId = tenantId,
                Kind = template.Kind,
                Code = template.Code,
                Name = template.Name,
                AssetCategoryId = template.AssetClassCode is { } code && classes.TryGetValue(code, out var id) ? id : null,
                DisplayOrder = order++
            });
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another request seeded them first.
            foreach (var entry in dbContext.ChangeTracker.Entries<FailureCode>().Where(x => x.State == EntityState.Added).ToList())
            {
                entry.State = EntityState.Detached;
            }
        }
    }
}
