using Aion.Commons.Diagnostics;

namespace Aion.Commons.Tests;

public sealed class DispatchLatencyMetricsTests
{
    [Fact]
    public void CoalescesRequestsAndKeepsPendingAgeAcrossDisjointWindows()
    {
        var clock = new ManualClock();
        var metrics = new DispatchLatencyMetrics();
        var probe = new PendingDispatchProbe(metrics, clock);
        probe.Queued();
        clock.Now = 2;
        probe.Queued();
        Assert.Equal(2, probe.PendingMilliseconds);
        Assert.Equal(0, metrics.TakeSnapshot().Count);
        clock.Now = 10;
        probe.Dispatched();
        probe.Dispatched();
        var window = metrics.TakeSnapshot();
        Assert.Null(probe.PendingMilliseconds);
        Assert.Equal(1, window.Count);
        Assert.Equal(10, window.MaximumMilliseconds);
        Assert.Equal(10, window.MeanMilliseconds);
        Assert.Equal(1, window.Buckets[5]);
        Assert.Equal(1, window.Buckets.Sum());
        var next = metrics.TakeSnapshot();
        Assert.Equal(0, next.Count);
        Assert.Equal(0, next.MaximumMilliseconds);
        Assert.All(next.Buckets, count => Assert.Equal(0, count));
        Assert.Equal(1, window.Buckets.Sum()); // Snapshots cannot change with the next window.
    }

    [Fact]
    public void DiscardIsNotAZeroLatencySuccessAndConcurrentRequestsStayBounded()
    {
        var clock = new ManualClock();
        var metrics = new DispatchLatencyMetrics();
        var probe = new PendingDispatchProbe(metrics, clock);
        Parallel.For(0, 1000, _ => probe.Queued());
        probe.Discard(); probe.Discard(); probe.Dispatched();
        var discarded = metrics.TakeSnapshot();
        Assert.Equal(1, discarded.Abandoned);
        Assert.Equal(0, discarded.Count);
        Assert.Null(probe.PendingMilliseconds);
        probe.Queued(); clock.Now = 6000; probe.Dispatched();
        var slow = metrics.TakeSnapshot();
        Assert.Equal(6000, slow.MaximumMilliseconds);
        Assert.Equal(1, slow.Buckets[^1]);
        Assert.Equal(14, slow.Buckets.Length);
    }

    private sealed class ManualClock : TimeProvider
    {
        public long Now { get; set; }
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => Now;
    }
}
