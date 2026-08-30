using Ecosys.Infrastructure.Services;

namespace Ecosys.Tests;

/// <summary>
/// EC-P2-10. The advisory lock itself needs a live PostgreSQL session to exercise, but the part that
/// silently breaks the guarantee is the key: two replicas must hash the same worker name to the same
/// bigint, or both take "the lock" and duplicate work orders and customer emails come back.
/// </summary>
public sealed class BackgroundWorkLeaseTests
{
    [Fact]
    public void KeyFor_IsStableForAWorkerName()
    {
        // string.GetHashCode is randomised per process and would have passed a same-process
        // assertion while failing across replicas — the literal pins the value across processes.
        Assert.Equal(PostgresAdvisoryLockLease.KeyFor("PmSchedulerWorker"), PostgresAdvisoryLockLease.KeyFor("PmSchedulerWorker"));
        Assert.Equal(-2444616645158832697L, PostgresAdvisoryLockLease.KeyFor("PmSchedulerWorker"));
    }

    [Fact]
    public void KeyFor_SeparatesTheWorkers()
    {
        string[] workers =
        [
            "EmailOutboxWorker",
            "TrialLifecycleWorker",
            "PmSchedulerWorker",
            "MrrSnapshotWorker",
            "SlaEnforcementWorker",
            "ContractReminderWorker",
            "ScheduledReportWorker",
            "ApprovalReminderWorker",
            "EscalationRuleWorker",
        ];

        var keys = workers.Select(PostgresAdvisoryLockLease.KeyFor).ToList();

        Assert.Equal(workers.Length, keys.Distinct().Count());
    }
}
