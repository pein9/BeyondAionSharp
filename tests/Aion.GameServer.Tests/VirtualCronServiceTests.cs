using Aion.Commons.Lang;
using Aion.GameServer.Services.Cron;
using Aion.GameServer.TestKit;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Cron;
using Microsoft.Extensions.Logging.Abstractions;
using Quartz;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class VirtualCronServiceTests
{
	[Fact]
	public async Task VirtualCronUsesConfiguredZoneRearmsAndPreservesQueryAndCancelApi()
	{
		ThreadPoolManager? previous = TryGetThreadPool();
		var pool = new VirtualThreadPool();
		var zone = TimeZoneInfo.CreateCustomTimeZone("P4-07 UTC+02", TimeSpan.FromHours(2), "P4-07 UTC+02", "P4-07 UTC+02");
		var epoch = new DateTimeOffset(2030, 1, 1, 21, 59, 59, TimeSpan.Zero);
		CronService? service = null;
		try
		{
			ThreadPoolManager.RegisterInstance(pool);
			SystemClock.UseSource(() => epoch.ToUnixTimeMilliseconds() + pool.NowMillis);
			service = CronService.CreateDeterministic(typeof(ThreadPoolManagerRunnableRunner), zone);
			var trace = new List<long>();
			var runnable = new RecordingRunnable(() => trace.Add(pool.NowMillis));
			CronExpression expression = CronExpressions.GetOrCreate("0 0 0 ? * *");

			IJobDetail job = service.Schedule(runnable, expression);

			Assert.False(service.HasQuartzScheduler);
			Assert.Same(job, Assert.Single(service.FindJobDetails(runnable)));
			Assert.Same(job, Assert.Single(service.FindJobs<RecordingRunnable>(withSubTypes: false)));
			ITrigger trigger = Assert.Single(service.GetJobTriggers(job));
			KeyValuePair<RecordingRunnable, DateTimeOffset> next = Assert.Single(service.FindNextFireTimes<RecordingRunnable>(false));
			Assert.Same(runnable, next.Key);
			Assert.Equal(new DateTimeOffset(2030, 1, 1, 22, 0, 0, TimeSpan.Zero), next.Value);
			Assert.Equal(next.Value, trigger.GetNextFireTimeUtc());

			pool.Advance(TimeSpan.FromMilliseconds(999));
			Assert.Empty(trace);
			pool.Advance(TimeSpan.FromMilliseconds(1));
			Assert.Equal([1_000L], trace);
			DateTimeOffset rearmed = Assert.Single(service.FindNextFireTimes<RecordingRunnable>(false)).Value;
			Assert.Equal(new DateTimeOffset(2030, 1, 2, 22, 0, 0, TimeSpan.Zero), rearmed);
			Assert.Equal(rearmed, trigger.GetNextFireTimeUtc());

			pool.Advance(TimeSpan.FromDays(1));
			Assert.Equal([1_000L, 86_401_000L], trace);

			Assert.True(service.Cancel(runnable));
			Assert.Empty(service.FindJobDetails(runnable));
			pool.Advance(TimeSpan.FromDays(1));
			Assert.Equal(2, trace.Count);
			Assert.False(service.Cancel(job));
		}
		finally
		{
			service?.Shutdown();
			SystemClock.UseSystemClock();
			ThreadPoolManager.RegisterInstance(previous ?? new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance));
			await pool.DisposeAsync();
		}
	}

	private static ThreadPoolManager? TryGetThreadPool()
	{
		try
		{
			return ThreadPoolManager.GetInstance();
		}
		catch (InvalidOperationException)
		{
			return null;
		}
	}

	private sealed class RecordingRunnable(Action action) : Runnable
	{
		public void Run() => action();
	}
}
