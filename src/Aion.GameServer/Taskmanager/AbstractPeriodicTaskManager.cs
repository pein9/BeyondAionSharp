using System;
using System.Threading.Tasks;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Taskmanager;

/// <summary>
/// Java parity: taskmanager/AbstractPeriodicTaskManager (lord_rex, MrPoke based on l2j-free engines).
/// This can be used for periodic calls.
/// </summary>
public abstract class AbstractPeriodicTaskManager
{
    protected static readonly ILogger log = AionLog.For(nameof(AbstractPeriodicTaskManager));
    private readonly object scheduleLock = new();
    private readonly int period;
    private ScheduledTask scheduledTask;

    public AbstractPeriodicTaskManager(int period)
    {
        this.period = period;
        log.LogInformation(GetType().Name + " initialized.");
        scheduledTask = Arm(ThreadPoolManager.GetInstance());
    }

    /// <summary>
    /// Rebinds an already-created Java-style singleton to the currently registered deterministic pool.
    /// Production never calls this; SIM calls it after installing its virtual scheduler.
    /// </summary>
    public void RearmForDeterministicSimulation()
    {
        ThreadPoolManager pool = ThreadPoolManager.GetInstance();
        if (!pool.IsDeterministic)
            throw new InvalidOperationException("Periodic managers can only be rebound to a deterministic ThreadPoolManager.");

        lock (scheduleLock)
        {
            scheduledTask.Cancel(false);
            scheduledTask = Arm(pool);
        }
    }

    private ScheduledTask Arm(ThreadPoolManager pool)
    {
        int initialDelay = Aion.GameServer.Commons.Utils.Rnd.Get(500, 550);
        return pool.ScheduleAtFixedRateTask(ct =>
        {
            Run();
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(initialDelay), TimeSpan.FromMilliseconds(period));
    }

    protected abstract void Run();
}
