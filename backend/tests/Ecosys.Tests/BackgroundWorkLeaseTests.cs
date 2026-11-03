using Ecosys.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

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
            "ProjectReminderWorker",
            "ActivityReminderWorker",
        ];

        var keys = workers.Select(PostgresAdvisoryLockLease.KeyFor).ToList();

        Assert.Equal(workers.Length, keys.Distinct().Count());
    }

    /// <summary>
    /// Host shutdown disposes the lease while a worker's StopAsync is still releasing its lock. That
    /// used to dispose the gate under the releasing caller, whose Release() then threw and failed the
    /// shutdown (seen as random failures in the WebApplicationFactory tests).
    /// </summary>
    [Fact]
    public async Task ReleaseRacingDispose_DoesNotThrow_AndDisposedLeaseAcquiresNothing()
    {
        var connectionString = Environment.GetEnvironmentVariable("ECOSYS_TEST_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres";

        for (var attempt = 0; attempt < 50; attempt++)
        {
            var lease = new PostgresAdvisoryLockLease(connectionString, NullLogger<PostgresAdvisoryLockLease>.Instance);
            var workName = $"lease-race-test-{Guid.NewGuid():N}";
            Assert.True(await lease.TryAcquireAsync(workName));

            var release = Task.Run(() => lease.ReleaseAsync(workName));
            var dispose = Task.Run(async () => await lease.DisposeAsync());
            await Task.WhenAll(release, dispose);

            Assert.False(await lease.TryAcquireAsync(workName));
            Assert.False(await lease.IsHeldAsync(workName));
            await lease.ReleaseAsync(workName);
            await lease.DisposeAsync();
        }
    }
}
