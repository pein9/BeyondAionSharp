using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Taskmanager;

/// <summary>Java parity: taskmanager/AbstractFIFOPeriodicTaskManager (lord_rex, MrPoke based on l2j-free engines, Neon).</summary>
public abstract class AbstractFIFOPeriodicTaskManager<T> : AbstractPeriodicTaskManager
{
    private const int WARNING_PERIOD_SECONDS = 10;
    private readonly ConcurrentQueue<T> tasks = new();
    private readonly InsertionOrderedSet<T> processedTasks = new();
    private readonly int counterLimit;
    private int counter = 0;

    public AbstractFIFOPeriodicTaskManager(int periodMillis)
        : base(periodMillis)
    {
        counterLimit = Math.Max(5, WARNING_PERIOD_SECONDS * 1000 / periodMillis);
    }

    public void Add(T t)
    {
        tasks.Enqueue(t);
    }

    protected override void Run()
    {
        lock (this)
        {
            int previouslyProcessedTasksSize = processedTasks.Count;
            processedTasks.Clear();
            for (int i = tasks.Count; i > 0; --i)
            {
                if (!tasks.TryDequeue(out T task)) // no tasks left
                    break;
                processedTasks.Add(task);
            }
            foreach (T task in processedTasks)
            {
                try
                {
                    long begin = System.Diagnostics.Stopwatch.GetTimestamp();
                    CallTask(task);
                    if (Aion.Commons.Configs.CommonsConfig.RUNNABLESTATS_ENABLE)
                    {
                        long duration = (System.Diagnostics.Stopwatch.GetTimestamp() - begin) * 1_000_000_000L / System.Diagnostics.Stopwatch.Frequency;
                        Aion.GameServer.Commons.Utils.Concurrent.RunnableStatsManager.HandleStats(task.GetType(), GetCalledMethodName(), duration);
                    }
                }
                catch (Exception e)
                {
                    log.LogError(e, "Exception in " + GetType().Name + " processing " + task);
                }
            }
            if (processedTasks.Count <= previouslyProcessedTasksSize)
                counter = 0;
            else if (++counter % counterLimit == 0) // log warning if the task queue size continually increased over the last WARNING_PERIOD_SECONDS
                log.LogWarning("Tasks for " + GetType().Name + " are added faster than they can be executed (currently " + processedTasks.Count + " tasks).");
        }
    }

    protected abstract void CallTask(T task);

    protected abstract string GetCalledMethodName();
}

/// <summary>
/// Hash-based set membership with deterministic first-insertion iteration order, matching Java's
/// <c>LinkedHashSet</c> contract without relying on <see cref="HashSet{T}"/> iteration details.
/// </summary>
internal sealed class InsertionOrderedSet<T> : IReadOnlyCollection<T>
{
    private readonly HashSet<T> set = new();
    private readonly List<T> ordered = new();

    public int Count => ordered.Count;

    public bool Add(T item)
    {
        if (!set.Add(item))
            return false;
        ordered.Add(item);
        return true;
    }

    public void Clear()
    {
        set.Clear();
        ordered.Clear();
    }

    public List<T>.Enumerator GetEnumerator() => ordered.GetEnumerator();

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
