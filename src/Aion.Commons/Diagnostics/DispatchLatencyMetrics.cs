namespace Aion.Commons.Diagnostics;

/// <summary>Bounded, real-time infrastructure observations, never gameplay time or packet scheduling.</summary>
public sealed class DispatchLatencyMetrics
{
    public static IReadOnlyList<double> BucketUpperBoundsMilliseconds { get; } =
        Array.AsReadOnly(new double[] { .1, .5, 1, 2, 5, 10, 20, 50, 100, 250, 500, 1000, 5000, double.PositiveInfinity });
    private readonly object gate = new();
    private readonly long[] buckets = new long[BucketUpperBoundsMilliseconds.Count];
    private long count, abandoned;
    private double total, maximum;

    internal void Observe(double milliseconds)
    {
        lock (gate)
        {
            int bucket = 0;
            while (milliseconds > BucketUpperBoundsMilliseconds[bucket]) bucket++;
            buckets[bucket]++;
            count++;
            total += milliseconds;
            maximum = Math.Max(maximum, milliseconds);
        }
    }

    internal void Abandon() { lock (gate) abandoned++; }

    /// <summary>One disjoint heartbeat window. No connections, packets or individual samples are retained.</summary>
    public DispatchLatencySnapshot TakeSnapshot()
    {
        lock (gate)
        {
            var result = new DispatchLatencySnapshot(count, abandoned, count == 0 ? 0 : total / count, maximum, (long[])buckets.Clone());
            count = abandoned = 0;
            total = maximum = 0;
            Array.Clear(buckets);
            return result;
        }
    }
}

public sealed record DispatchLatencySnapshot(long Count, long Abandoned, double MeanMilliseconds,
    double MaximumMilliseconds, long[] Buckets, int PendingConnections = 0, double OldestPendingMilliseconds = 0);

/// <summary>First pending write request to the next dispatcher buffer-preparation attempt.
/// Multiple queued packets coalesce; this is not per-packet latency or socket flush completion.</summary>
public sealed class PendingDispatchProbe(DispatchLatencyMetrics metrics, TimeProvider? clock = null)
{
    private readonly TimeProvider clock = clock ?? TimeProvider.System;
    private long since = long.MinValue;
    public void Queued() => Interlocked.CompareExchange(ref since, clock.GetTimestamp(), long.MinValue);
    public double? PendingMilliseconds
    {
        get
        {
            long start = Volatile.Read(ref since);
            return start == long.MinValue ? null : clock.GetElapsedTime(start).TotalMilliseconds;
        }
    }
    public void Dispatched()
    {
        long start = Interlocked.Exchange(ref since, long.MinValue);
        if (start != long.MinValue) metrics.Observe(clock.GetElapsedTime(start).TotalMilliseconds);
    }
    public void Discard()
    {
        if (Interlocked.Exchange(ref since, long.MinValue) != long.MinValue) metrics.Abandon();
    }
}
