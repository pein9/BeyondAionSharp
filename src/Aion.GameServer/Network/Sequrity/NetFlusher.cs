using System;
using System.Collections.Generic;
using System.Threading;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Network.Sequrity;

/// <summary>
/// Java parity: network/sequrity/NetFlusher (NB4L1). Java single daemon java.util.Timer + TimerTask scheduleAtFixedRate →
/// C# System.Threading.Timer per task (retained in a static list to prevent GC, mirroring the JVM Timer keeping tasks alive).
/// Runnable→Action; RuntimeException→Exception.
/// </summary>
public static class NetFlusher
{
    private static readonly ILogger log = AionLog.For(nameof(NetFlusher));
    private static readonly List<Timer> _timers = new();

    public static void Add(Action runnable, long interval)
    {
        if (ThreadPoolManager.IsDeterministicMode)
        {
            // SIM-only infrastructure deviation: make fixed-rate network maintenance advance with virtual time.
            ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(
                _ => RunSafely(runnable),
                TimeSpan.FromMilliseconds(interval),
                TimeSpan.FromMilliseconds(interval));
            return;
        }

        Timer timer = new Timer(_ =>
        {
            _ = RunSafely(runnable);
        }, null, interval, interval);
        lock (_timers)
        {
            _timers.Add(timer);
        }
    }

    private static ValueTask RunSafely(Action runnable)
    {
        try
        {
            runnable();
        }
        catch (Exception e)
        {
            log.LogError(e, "Net flusher task failed");
        }
        return ValueTask.CompletedTask;
    }
}
