using System.Diagnostics;

namespace Ecosys.Api.Observability;

/// <summary>
/// Lightweight in-process runtime metrics for the platform health page: process uptime, a sampled
/// CPU%, and a rolling 5-minute window of request latency / 5xx error rate. Registered as a
/// singleton; requests are recorded by <see cref="RequestMetricsMiddleware"/>.
/// </summary>
public interface IRuntimeMetrics
{
    DateTime StartedUtc { get; }
    double CpuPercent { get; }
    void Record(int statusCode, double elapsedMs);
    RequestWindowStats GetWindowStats();
}

public sealed record RequestWindowStats(int WindowSeconds, long RequestCount, double AvgLatencyMs, double ErrorRatePercent);

public sealed class RuntimeMetrics : IRuntimeMetrics, IDisposable
{
    private const int WindowSeconds = 300;

    public DateTime StartedUtc { get; } = DateTime.UtcNow;

    // One counter bucket per second over the trailing window — bounded memory regardless of load.
    private sealed class Bucket
    {
        public long Second;
        public long Count;
        public double SumMs;
        public long Errors;
    }

    private readonly Bucket[] _buckets = new Bucket[WindowSeconds];
    private readonly object _lock = new();

    private double _cpuPercent;
    private readonly Timer _cpuTimer;
    private TimeSpan _lastCpuTime;
    private DateTime _lastCpuSampleUtc;

    public RuntimeMetrics()
    {
        using var proc = Process.GetCurrentProcess();
        _lastCpuTime = proc.TotalProcessorTime;
        _lastCpuSampleUtc = DateTime.UtcNow;
        _cpuTimer = new Timer(SampleCpu, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public double CpuPercent => Volatile.Read(ref _cpuPercent);

    private void SampleCpu(object? _)
    {
        try
        {
            using var proc = Process.GetCurrentProcess();
            var now = DateTime.UtcNow;
            var cpuNow = proc.TotalProcessorTime;
            var wallMs = (now - _lastCpuSampleUtc).TotalMilliseconds;
            if (wallMs > 0)
            {
                var cpuMs = (cpuNow - _lastCpuTime).TotalMilliseconds;
                var cores = Math.Max(1, Environment.ProcessorCount);
                Volatile.Write(ref _cpuPercent, Math.Clamp(cpuMs / (wallMs * cores) * 100.0, 0, 100));
            }

            _lastCpuTime = cpuNow;
            _lastCpuSampleUtc = now;
        }
        catch
        {
            // Best-effort sampling; never let it throw on the timer thread.
        }
    }

    public void Record(int statusCode, double elapsedMs)
    {
        var second = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var idx = (int)(second % WindowSeconds);
        lock (_lock)
        {
            var b = _buckets[idx] ??= new Bucket();
            if (b.Second != second)
            {
                b.Second = second;
                b.Count = 0;
                b.SumMs = 0;
                b.Errors = 0;
            }

            b.Count++;
            b.SumMs += elapsedMs;
            if (statusCode >= 500)
            {
                b.Errors++;
            }
        }
    }

    public RequestWindowStats GetWindowStats()
    {
        var cutoff = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - WindowSeconds;
        long count = 0;
        long errors = 0;
        double sumMs = 0;

        lock (_lock)
        {
            foreach (var b in _buckets)
            {
                if (b is not null && b.Second > cutoff)
                {
                    count += b.Count;
                    sumMs += b.SumMs;
                    errors += b.Errors;
                }
            }
        }

        if (count == 0)
        {
            return new RequestWindowStats(WindowSeconds, 0, 0, 0);
        }

        return new RequestWindowStats(
            WindowSeconds,
            count,
            Math.Round(sumMs / count, 1),
            Math.Round((double)errors / count * 100.0, 2));
    }

    public void Dispose() => _cpuTimer.Dispose();
}
