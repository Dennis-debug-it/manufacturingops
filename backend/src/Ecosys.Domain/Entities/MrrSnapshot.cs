using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// One daily snapshot of platform MRR and tenant mix, written by the background snapshot job. Point-
/// in-time MRR isn't stored anywhere else, so these rows are what make MRR/revenue trend charts
/// possible. One row per UTC day (unique <see cref="SnapshotDate"/>).
/// </summary>
public sealed class MrrSnapshot : AuditableEntity
{
    public DateTime SnapshotDate { get; set; }
    public decimal Mrr { get; set; }
    public int ActiveTenants { get; set; }
    public int TrialTenants { get; set; }
    public int PaidTenants { get; set; }
}
