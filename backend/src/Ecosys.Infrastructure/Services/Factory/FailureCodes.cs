using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services.Factory;

public sealed record FailureCodeTemplate(string Kind, string Code, string Name, string? AssetClassCode = null);

/// <summary>
/// The failure codes every industry gets: problems (failure modes), causes (failure mechanisms) and
/// remedies (maintenance activities) following ISO 14224. Each industry pack adds its machinery's
/// problems tied to their asset classes (<see cref="IndustryTemplate.FailureCodes"/>). Tenants edit their own copy.
/// </summary>
public static class FailureCodeTemplates
{
    private const string P = FailureCodeKinds.Problem;
    private const string C = FailureCodeKinds.Cause;
    private const string R = FailureCodeKinds.Remedy;

    public static readonly IReadOnlyList<FailureCodeTemplate> Iso =
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
    /// <summary>Gives a tenant the ISO codes and its industries' codes: all of them the first time, and later any an added industry brings.</summary>
    Task EnsureSeededAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

internal sealed class FailureCodeService(AppDbContext dbContext) : IFailureCodeService
{
    public async Task EnsureSeededAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var industries = await IndustryTemplates.InUseAsync(dbContext, tenantId, cancellationToken);
        var existing = (await dbContext.FailureCodes.Where(x => x.TenantId == tenantId).Select(x => new { x.Kind, x.Code }).ToListAsync(cancellationToken))
            .Select(x => (x.Kind, x.Code)).ToHashSet();
        var wanted = (existing.Count == 0 ? FailureCodeTemplates.Iso : [])
            .Concat(industries.SelectMany(x => x.FailureCodes))
            .Where(x => !existing.Contains((x.Kind, x.Code)))
            .DistinctBy(x => (x.Kind, x.Code))
            .ToList();
        if (wanted.Count == 0)
        {
            return;
        }

        var classes = await dbContext.AssetCategories
            .Where(x => x.TenantId == tenantId && x.Code != null)
            .ToDictionaryAsync(x => x.Code!, x => x.Id, cancellationToken);

        var order = existing.Count;
        foreach (var template in wanted)
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
