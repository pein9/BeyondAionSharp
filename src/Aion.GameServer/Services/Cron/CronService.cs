using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Matchers;
using Quartz.Spi;
using Aion.Commons.Concurrent;
using Aion.Commons.Lang;

namespace Aion.GameServer.Services.Cron;

/// <summary>
/// Java parity: services/cron/CronService (SoulKeeper, Neon). Quartz-backed cron scheduler singleton.
/// Infrastructure boundary (gameplay-faithful / infra-idiomatic principle): the public API mirrors the Java
/// service (Schedule/Cancel/FindJobDetails/GetJobTriggers/FindJobs/FindNextFireTimes), but the implementation
/// targets Quartz.NET's idiomatic interface+async surface (IScheduler/IJobDetail/ITrigger; Task-returning
/// scheduler ops bridged synchronously at setup time) instead of org.quartz's concrete classes. The bespoke
/// Java CronScheduleBuilder/MemoryEfficientCronTrigger (a clone-time CronExpression re-intern optimisation) is
/// dropped in favour of Quartz.NET's built-in CronScheduleBuilder.CronSchedule. Action overloads wrap a
/// LambdaRunnable so C# lambda/method-group call sites bind exactly as Java's auto-converted Runnable lambdas.
/// </summary>
public sealed class CronService
{
    private static readonly ILogger log = AionLog.For(nameof(CronService));

    private static readonly object initLock = new object();
    private static readonly AsyncLocal<CronService?> scopedInstance = new();
    private static CronService instance;

    private readonly TimeZoneInfo timeZone;
    private readonly object schedulerLock = new();
    private IScheduler? scheduler;
    private readonly Type runnableRunner;
    private readonly ConcurrentDictionary<JobKey, VirtualCronJob> virtualJobs = new();
    private long nextVirtualJobId;

    public static CronService GetInstance()
    {
        return scopedInstance.Value ?? instance;
    }

    public static void InitSingleton(Type runnableRunner, TimeZoneInfo timeZone)
    {
        lock (initLock)
        {
            if (instance != null)
            {
                throw new CronServiceException("CronService is already initialized");
            }

            instance = new CronService(runnableRunner, timeZone);
        }
    }

    private CronService(Type runnableRunner, TimeZoneInfo timeZone)
    {
        if (runnableRunner == null)
        {
            throw new CronServiceException("RunnableRunner class must be defined");
        }

        this.runnableRunner = runnableRunner;
        this.timeZone = timeZone;

        // SIM-only infrastructure deviation: a deterministic pool owns cron time and must not start Quartz.
        if (!ThreadPoolManager.IsDeterministicMode)
            EnsureScheduler();
    }

    internal static CronService CreateDeterministic(Type runnableRunner, TimeZoneInfo timeZone) =>
        new(runnableRunner, timeZone);

    /// <summary>
    /// Gives an isolated in-process harness its own cron service without replacing the process singleton.
    /// The execution-context scope flows through the virtual pool's bounded construction and drain tasks.
    /// </summary>
    internal static IDisposable UseScopedInstance(CronService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        CronService? previous = scopedInstance.Value;
        scopedInstance.Value = service;
        return new ScopedInstance(() => scopedInstance.Value = previous);
    }

    internal bool HasQuartzScheduler => scheduler != null;

    private sealed class ScopedInstance(Action restore) : IDisposable
    {
        private Action? restoreAction = restore;

        public void Dispose() => Interlocked.Exchange(ref restoreAction, null)?.Invoke();
    }

    public void Shutdown()
    {
        foreach (VirtualCronJob job in virtualJobs.Values)
            job.Cancel();
        virtualJobs.Clear();

        IScheduler? activeScheduler = scheduler;
        if (activeScheduler == null)
            return;
        try
        {
            activeScheduler.Shutdown(false).GetAwaiter().GetResult();
        }
        catch (SchedulerException e)
        {
            log.LogError(e, "Failed to shutdown CronService correctly");
        }
    }

    public IJobDetail Schedule(Runnable r, string cronExpression)
    {
        return Schedule(r, cronExpression, false);
    }

    public IJobDetail Schedule(Runnable r, string cronExpression, bool longRunning)
    {
        return Schedule(r, CronExpressions.GetOrCreate(cronExpression), longRunning);
    }

    public IJobDetail Schedule(Runnable r, CronExpression cronExpression)
    {
        return Schedule(r, cronExpression, false);
    }

    public IJobDetail Schedule(Runnable r, CronExpression cronExpression, bool longRunning)
    {
        return Schedule(r, runnableRunner, cronExpression, longRunning);
    }

    // Action overloads: C# call sites pass lambdas/method-groups where Java passed Runnable lambdas.
    public IJobDetail Schedule(Action r, string cronExpression) => Schedule(new LambdaRunnable(r), cronExpression, false);

    public IJobDetail Schedule(Action r, string cronExpression, bool longRunning) => Schedule(new LambdaRunnable(r), cronExpression, longRunning);

    public IJobDetail Schedule(Action r, CronExpression cronExpression) => Schedule(new LambdaRunnable(r), cronExpression, false);

    public IJobDetail Schedule(Action r, CronExpression cronExpression, bool longRunning) => Schedule(new LambdaRunnable(r), cronExpression, longRunning);

    public IJobDetail Schedule(Runnable r, Type runnableRunner, CronExpression cronExpression, bool longRunning)
    {
        try
        {
            JobDataMap jdm = new JobDataMap();
            jdm.Put(RunnableRunner.KEY_RUNNABLE_OBJECT, r);
            jdm.Put(RunnableRunner.KEY_PROPERTY_IS_LONGRUNNING_TASK, longRunning);

            bool deterministic = ThreadPoolManager.IsDeterministicMode;
            string jobId = deterministic
                ? "VirtualJob:" + Interlocked.Increment(ref nextVirtualJobId)
                : "Started at ms" + SystemClock.CurrentMillis() + "; ns" + System.Diagnostics.Stopwatch.GetTimestamp();
            JobKey jobKey = new JobKey("JobKey:" + jobId);
            IJobDetail jobDetail = JobBuilder.Create(runnableRunner).UsingJobData(jdm).WithIdentity(jobKey).Build();

            if (deterministic)
            {
                CronExpression virtualExpression = new CronExpression(cronExpression.CronExpressionString)
                {
                    TimeZone = timeZone,
                };
                ITrigger virtualTrigger = TriggerBuilder.Create()
                    .WithIdentity("Trigger:" + jobId)
                    .ForJob(jobKey)
                    .StartAt(SystemClock.UtcNow())
                    .WithSchedule(CronScheduleBuilder.CronSchedule(virtualExpression.CronExpressionString).InTimeZone(timeZone))
                    .Build();
                ((IOperableTrigger)virtualTrigger).ComputeFirstFireTimeUtc(null);
                var virtualJob = new VirtualCronJob(jobDetail, virtualTrigger, r, runnableRunner, virtualExpression, longRunning);
                if (!virtualJobs.TryAdd(jobKey, virtualJob))
                    throw new CronServiceException("Duplicate virtual cron job key " + jobKey);
                ArmVirtualJob(virtualJob, ThreadPoolManager.GetInstance());
                return jobDetail;
            }

            ITrigger trigger = TriggerBuilder.Create()
                .WithSchedule(CronScheduleBuilder.CronSchedule(cronExpression.CronExpressionString).InTimeZone(timeZone))
                .Build();

            EnsureScheduler().ScheduleJob(jobDetail, trigger).GetAwaiter().GetResult();
            return jobDetail;
        }
        catch (Exception e)
        {
            throw new CronServiceException("Failed to start job", e);
        }
    }

    private void ArmVirtualJob(VirtualCronJob job, ThreadPoolManager pool)
    {
        DateTimeOffset now = SystemClock.UtcNow();
        DateTimeOffset? nextFireTime = job.Expression.GetTimeAfter(now);
        if (nextFireTime == null)
            return;

        TimeSpan delay = nextFireTime.Value - now;
        if (delay < TimeSpan.Zero)
            delay = TimeSpan.Zero;
        job.Arm(nextFireTime.Value, pool.Schedule(ct => FireVirtualJob(job, pool, ct), delay));
    }

    private ValueTask FireVirtualJob(VirtualCronJob job, ThreadPoolManager pool, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested || !virtualJobs.TryGetValue(job.Detail.Key, out VirtualCronJob? activeJob)
            || !ReferenceEquals(activeJob, job) || job.IsCancelled)
            return ValueTask.CompletedTask;

        try
        {
            job.MarkTriggered();
            var runner = (RunnableRunner?)Activator.CreateInstance(job.RunnerType)
                ?? throw new CronServiceException("Failed to create RunnableRunner " + job.RunnerType);
            if (job.LongRunning)
                runner.ExecuteLongRunningRunnable(job.Runnable);
            else
                runner.ExecuteRunnable(job.Runnable);
        }
        finally
        {
            if (!job.IsCancelled && virtualJobs.TryGetValue(job.Detail.Key, out activeJob) && ReferenceEquals(activeJob, job))
                ArmVirtualJob(job, pool);
        }
        return ValueTask.CompletedTask;
    }

    public bool Cancel(IJobDetail jd)
    {
        if (jd == null)
        {
            return false;
        }

        if (jd.Key == null)
        {
            throw new CronServiceException("JobDetail should have JobKey");
        }

        if (virtualJobs.TryRemove(jd.Key, out VirtualCronJob? virtualJob))
        {
            virtualJob.Cancel();
            return true;
        }

        IScheduler? activeScheduler = scheduler;
        if (activeScheduler == null)
            return false;

        try
        {
            return activeScheduler.DeleteJob(jd.Key).GetAwaiter().GetResult();
        }
        catch (SchedulerException e)
        {
            throw new CronServiceException("Failed to delete Job", e);
        }
    }

    public bool Cancel(Runnable r)
    {
        List<IJobDetail> jobDetails = FindJobDetails(r);
        if (jobDetails.Count == 0)
            return false;
        bool allCancelled = true;
        foreach (IJobDetail jobDetail in jobDetails)
        {
            allCancelled &= Cancel(jobDetail);
        }
        return allCancelled;
    }

    public List<IJobDetail> FindJobDetails(Runnable runnable)
    {
        try
        {
            List<IJobDetail> jobs = virtualJobs.Values
                .Where(job => ReferenceEquals(job.Runnable, runnable))
                .OrderBy(job => job.Detail.Key.Name, StringComparer.Ordinal)
                .Select(job => job.Detail)
                .ToList();
            IScheduler? activeScheduler = scheduler;
            if (activeScheduler == null)
                return jobs;

            foreach (JobKey jobKey in activeScheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup()).GetAwaiter().GetResult())
            {
                IJobDetail jobDetail = activeScheduler.GetJobDetail(jobKey).GetAwaiter().GetResult();
                if (jobDetail.JobDataMap[RunnableRunner.KEY_RUNNABLE_OBJECT] == (object)runnable)
                    jobs.Add(jobDetail);
            }
            return jobs;
        }
        catch (Exception e)
        {
            throw new CronServiceException("Can't get all active job details", e);
        }
    }

    public List<ITrigger> GetJobTriggers(IJobDetail jd)
    {
        return GetJobTriggers(jd.Key);
    }

    public List<ITrigger> GetJobTriggers(JobKey jk)
    {
        if (virtualJobs.TryGetValue(jk, out VirtualCronJob? virtualJob))
            return [virtualJob.Trigger];

        IScheduler? activeScheduler = scheduler;
        if (activeScheduler == null)
            return new List<ITrigger>();
        try
        {
            return activeScheduler.GetTriggersOfJob(jk).GetAwaiter().GetResult().ToList();
        }
        catch (SchedulerException e)
        {
            throw new CronServiceException("Can't get triggers for JobKey " + jk, e);
        }
    }

    public List<IJobDetail> FindJobs<T>(bool withSubTypes) where T : Runnable
    {
        Type runnableType = typeof(T);
        try
        {
            List<IJobDetail> jobs = virtualJobs.Values
                .Where(job => runnableType == job.Runnable.GetType()
                    || withSubTypes && runnableType.IsAssignableFrom(job.Runnable.GetType()))
                .OrderBy(job => job.Detail.Key.Name, StringComparer.Ordinal)
                .Select(job => job.Detail)
                .ToList();

            IScheduler? activeScheduler = scheduler;
            if (activeScheduler == null)
                return jobs;

            var keys = activeScheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup()).GetAwaiter().GetResult();
            foreach (JobKey jk in keys)
            {
                IJobDetail jobDetail = activeScheduler.GetJobDetail(jk).GetAwaiter().GetResult();
                object runnable = jobDetail.JobDataMap[RunnableRunner.KEY_RUNNABLE_OBJECT];
                if (runnable != null)
                {
                    if (runnableType == runnable.GetType() || withSubTypes && runnableType.IsAssignableFrom(runnable.GetType()))
                        jobs.Add(jobDetail);
                }
            }
            return jobs;
        }
        catch (Exception e)
        {
            throw new CronServiceException("Couldn't collect job details for jobs of type " + runnableType, e);
        }
    }

    public Dictionary<T, DateTimeOffset> FindNextFireTimes<T>(bool withSubTypes) where T : Runnable
    {
        List<IJobDetail> jobs = FindJobs<T>(withSubTypes);
        if (jobs.Count == 0)
            return new Dictionary<T, DateTimeOffset>();

        try
        {
            DateTimeOffset now = SystemClock.UtcNow();
            Dictionary<T, DateTimeOffset> nextFireTimes = new Dictionary<T, DateTimeOffset>(jobs.Count);
            foreach (IJobDetail job in jobs)
            {
                object runnable = job.JobDataMap[RunnableRunner.KEY_RUNNABLE_OBJECT];
                DateTimeOffset? nextFireTime;
                if (virtualJobs.TryGetValue(job.Key, out VirtualCronJob? virtualJob))
                {
                    nextFireTime = virtualJob.NextFireTimeUtc;
                }
                else
                {
                    IScheduler activeScheduler = scheduler
                        ?? throw new CronServiceException("Quartz scheduler is not initialized for job " + job.Key);
                    nextFireTime = activeScheduler.GetTriggersOfJob(job.Key).GetAwaiter().GetResult()
                        .Select(t => t.GetNextFireTimeUtc())
                        .Where(d => d != null && d.Value > now)
                        .OrderBy(d => d.Value).FirstOrDefault();
                }
                if (nextFireTime != null)
                {
                    T key = (T)runnable;
                    if (!nextFireTimes.TryGetValue(key, out DateTimeOffset oldDate) || oldDate > nextFireTime.Value)
                        nextFireTimes[key] = nextFireTime.Value;
                }
            }
            return nextFireTimes;
        }
        catch (Exception e)
        {
            throw new CronServiceException("Can't get all active job details", e);
        }
    }

    private IScheduler EnsureScheduler()
    {
        IScheduler? activeScheduler = scheduler;
        if (activeScheduler != null)
            return activeScheduler;

        lock (schedulerLock)
        {
            if (scheduler != null)
                return scheduler;

            var properties = new NameValueCollection
            {
                ["quartz.threadPool.threadCount"] = "1",
            };
            try
            {
                scheduler = new StdSchedulerFactory(properties).GetScheduler().GetAwaiter().GetResult();
                scheduler.Start().GetAwaiter().GetResult();
                return scheduler;
            }
            catch (SchedulerException e)
            {
                throw new CronServiceException("Failed to initialize CronService", e);
            }
        }
    }

    private sealed class VirtualCronJob(
        IJobDetail detail,
        ITrigger trigger,
        Runnable runnable,
        Type runnerType,
        CronExpression expression,
        bool longRunning)
    {
        private readonly object sync = new();
        private ScheduledTask? scheduledTask;
        private bool cancelled;
        private DateTimeOffset? nextFireTimeUtc;

        public IJobDetail Detail { get; } = detail;
        public ITrigger Trigger { get; } = trigger;
        public Runnable Runnable { get; } = runnable;
        public Type RunnerType { get; } = runnerType;
        public CronExpression Expression { get; } = expression;
        public bool LongRunning { get; } = longRunning;

        public bool IsCancelled
        {
            get
            {
                lock (sync)
                    return cancelled;
            }
        }

        public DateTimeOffset? NextFireTimeUtc
        {
            get
            {
                lock (sync)
                    return nextFireTimeUtc;
            }
        }

        public void Arm(DateTimeOffset nextFireTime, ScheduledTask task)
        {
            lock (sync)
            {
                if (cancelled)
                {
                    task.Cancel(false);
                    return;
                }
                nextFireTimeUtc = nextFireTime;
                scheduledTask = task;
            }
        }

        public void MarkTriggered()
        {
            lock (sync)
            {
                ((IOperableTrigger)Trigger).Triggered(null);
                nextFireTimeUtc = Trigger.GetNextFireTimeUtc();
            }
        }

        public void Cancel()
        {
            lock (sync)
            {
                cancelled = true;
                nextFireTimeUtc = null;
                scheduledTask?.Cancel(false);
                scheduledTask = null;
            }
        }
    }
}
