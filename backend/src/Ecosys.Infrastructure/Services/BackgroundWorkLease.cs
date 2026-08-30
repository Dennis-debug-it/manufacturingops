using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Ecosys.Infrastructure.Services;

/// <summary>
/// A cluster-wide "only one instance does this work" lease. Nine hosted services generate work
/// orders, send customer email and write snapshots on a timer; at one replica that is fine, but
/// <c>--scale api=2</c> produced duplicate preventive-maintenance work orders and duplicate customer
/// emails because every replica ran every worker (EC-P2-10).
/// </summary>
public interface IBackgroundWorkLease
{
    /// <summary>Tries to take the lease for <paramref name="workName"/> without waiting.</summary>
    Task<bool> TryAcquireAsync(string workName, CancellationToken cancellationToken = default);

    /// <summary>
    /// True while this process still demonstrably holds the lease. A holder that has lost its
    /// database session has lost the lease with it — another instance may already have taken over —
    /// so the caller must stop acting on it.
    /// </summary>
    Task<bool> IsHeldAsync(string workName, CancellationToken cancellationToken = default);

    Task ReleaseAsync(string workName, CancellationToken cancellationToken = default);
}

/// <summary>
/// PostgreSQL session-level advisory locks. No new infrastructure — the database the application
/// already depends on is the arbiter — and the lock is tied to the session that took it, so an
/// instance that is killed, partitioned or OOM-ed releases everything it held the moment its
/// connection drops. No lease table, no expiry to tune, no stale row to clean up.
/// <para>
/// One dedicated connection holds every lock this process owns: session advisory locks are held per
/// session and a session may hold many keys, so nine workers cost one connection, not nine. That
/// connection is deliberately outside the EF pool — a pooled connection is handed back at the end of
/// the scope, which would drop the locks with it.
/// </para>
/// </summary>
public sealed class PostgresAdvisoryLockLease(
    string connectionString,
    ILogger<PostgresAdvisoryLockLease> logger) : IBackgroundWorkLease, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HashSet<string> _held = new(StringComparer.Ordinal);
    private NpgsqlConnection? _connection;

    public async Task<bool> TryAcquireAsync(string workName, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var connection = await GetOpenConnectionAsync(cancellationToken);
            if (connection is null)
            {
                return false;
            }

            await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(@key)", connection);
            command.Parameters.AddWithValue("key", KeyFor(workName));
            var acquired = await command.ExecuteScalarAsync(cancellationToken) is true;

            if (acquired)
            {
                _held.Add(workName);
            }

            return acquired;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Failing closed is the right default for a lock: better that no instance runs this
            // cycle than that every instance does. The caller polls, so work resumes on its own.
            logger.LogWarning(ex, "Could not acquire the background work lease for {WorkName}.", workName);
            await DiscardConnectionAsync();
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> IsHeldAsync(string workName, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!_held.Contains(workName) || _connection is null)
            {
                return false;
            }

            // The lock lives on this session, so proving the session is still there proves the lock
            // is still ours. A dropped connection has already released it server-side.
            await using var command = new NpgsqlCommand("SELECT 1", _connection);
            await command.ExecuteScalarAsync(cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Lost the database session holding the background work lease for {WorkName}.", workName);
            await DiscardConnectionAsync();
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ReleaseAsync(string workName, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!_held.Remove(workName) || _connection is null)
            {
                return;
            }

            await using var command = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", _connection);
            command.Parameters.AddWithValue("key", KeyFor(workName));
            await command.ExecuteScalarAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Nothing to repair: closing the connection releases the lock anyway.
            logger.LogWarning(ex, "Could not release the background work lease for {WorkName}.", workName);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DiscardConnectionAsync();
        _gate.Dispose();
    }

    private async Task<NpgsqlConnection?> GetOpenConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { State: System.Data.ConnectionState.Open })
        {
            return _connection;
        }

        await DiscardConnectionAsync();

        // Pooling off: a pooled connection can be reset and reused underneath us, and a reset drops
        // every advisory lock the session holds.
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Pooling = false,
            ApplicationName = "ecosys-background-work-lease",
        };

        var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        _connection = connection;
        return _connection;
    }

    private async Task DiscardConnectionAsync()
    {
        _held.Clear();

        if (_connection is null)
        {
            return;
        }

        var connection = _connection;
        _connection = null;

        try
        {
            await connection.DisposeAsync();
        }
        catch
        {
            // Closing a connection that is already gone is not a failure worth reporting.
        }
    }

    /// <summary>
    /// A stable 64-bit key for the advisory lock. <c>string.GetHashCode</c> is randomised per
    /// process, so two replicas would hash the same worker name to different keys and both would
    /// "win" — exactly the bug this class exists to prevent. FNV-1a is deterministic across
    /// processes and releases.
    /// </summary>
    internal static long KeyFor(string workName)
    {
        const ulong offsetBasis = 14695981039346656037;
        const ulong prime = 1099511628211;

        var hash = offsetBasis;
        foreach (var b in Encoding.UTF8.GetBytes($"ecosys:background-work:{workName}"))
        {
            hash ^= b;
            hash *= prime;
        }

        return unchecked((long)hash);
    }
}

/// <summary>
/// Wraps a hosted service so it only runs on the instance that holds its <see cref="IBackgroundWorkLease"/>.
/// Applied uniformly to all nine workers in <c>DependencyInjection</c> rather than reimplemented in
/// each one, so no worker can be added later without the guard by simply forgetting it.
/// <para>
/// Standby instances keep polling, so the lease is not a one-shot election: when the holder dies its
/// database session goes with it, PostgreSQL releases the advisory lock, and the next poll on
/// another instance picks the work up — within one poll interval and with no operator involvement.
/// The holder polls too, and stands down if it can no longer prove it owns the lease.
/// </para>
/// </summary>
internal sealed class SingleInstanceHostedService<TWorker>(
    IServiceProvider serviceProvider,
    IBackgroundWorkLease lease,
    ILogger<SingleInstanceHostedService<TWorker>> logger) : IHostedService, IDisposable
    where TWorker : IHostedService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly string WorkName = typeof(TWorker).Name;

    private readonly CancellationTokenSource _stopping = new();
    private IHostedService? _running;
    private Task? _supervisor;
    private bool _standbyLogged;
    private bool _stopped;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Never block host startup on a lock the other replica currently owns.
        _supervisor = SuperviseAsync(_stopping.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // The host stops hosted services and then disposes them, and a WebApplicationFactory does
        // both — so this runs more than once and must be idempotent.
        if (_stopped)
        {
            return;
        }

        _stopped = true;
        await _stopping.CancelAsync();

        if (_supervisor is not null)
        {
            // Bounded: shutdown must not stall on a supervision cycle that is mid-connect to an
            // unreachable database. The advisory lock goes with the process either way.
            Task settled = Task.WhenAny(_supervisor, Task.Delay(TimeSpan.FromSeconds(5), CancellationToken.None));
            await settled.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        await StopRunningAsync(cancellationToken);
        await lease.ReleaseAsync(WorkName, CancellationToken.None);
    }

    public void Dispose() => _stopping.Dispose();

    private async Task SuperviseAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_running is null)
                {
                    await TryStartAsync(stoppingToken);
                }
                else if (!await lease.IsHeldAsync(WorkName, stoppingToken))
                {
                    logger.LogWarning("{WorkName} lost its single-instance lease and is standing down.", WorkName);
                    await StopRunningAsync(stoppingToken);
                    _standbyLogged = false;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Single-instance supervision cycle failed for {WorkName}.", WorkName);
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task TryStartAsync(CancellationToken stoppingToken)
    {
        if (!await lease.TryAcquireAsync(WorkName, stoppingToken))
        {
            if (!_standbyLogged)
            {
                logger.LogInformation("{WorkName} is running on another instance. Standing by.", WorkName);
                _standbyLogged = true;
            }

            return;
        }

        // Resolved per acquisition rather than held from startup: a BackgroundService runs its
        // ExecuteAsync once, so a stood-down worker has to be a fresh instance to run again.
        var worker = serviceProvider.GetRequiredService<TWorker>();
        await worker.StartAsync(stoppingToken);
        _running = worker;
        _standbyLogged = false;
        logger.LogInformation("{WorkName} acquired the single-instance lease and is now running on this instance.", WorkName);
    }

    private async Task StopRunningAsync(CancellationToken cancellationToken)
    {
        if (_running is null)
        {
            return;
        }

        var worker = _running;
        _running = null;

        try
        {
            await worker.StopAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{WorkName} did not stop cleanly.", WorkName);
        }
        finally
        {
            (worker as IDisposable)?.Dispose();
        }
    }
}

public static class SingleInstanceHostedServiceExtensions
{
    /// <summary>
    /// Registers a hosted service that only runs on the instance holding its background work lease.
    /// The worker itself is transient so a fresh instance is created each time the lease is taken.
    /// </summary>
    public static IServiceCollection AddSingleInstanceHostedService<TWorker>(this IServiceCollection services)
        where TWorker : class, IHostedService
    {
        services.AddTransient<TWorker>();
        services.AddSingleton<IHostedService, SingleInstanceHostedService<TWorker>>();
        return services;
    }
}
