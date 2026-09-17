using Aion.GameServer.Services.Cron;
using Aion.GameServer.Taskmanager;
using Aion.GameServer.Taskmanager.Tasks.Housing;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Cron;
using Microsoft.Extensions.Logging.Abstractions;
using Quartz;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class SimulationCronTaskInitializationTests
{
	private static readonly TimeSpan WallTimeTimeout = TimeSpan.FromSeconds(5);

	[Fact]
	public async Task HousingCronSingletonsAreConstructedAndDrainedInJavaOrder()
	{
		ThreadPoolManager? previous = TryGetThreadPool();
		var pool = new VirtualThreadPool();
		var epoch = new DateTimeOffset(2030, 1, 1, 8, 59, 0, TimeSpan.Zero);
		CronService? service = null;
		try
		{
			ThreadPoolManager.RegisterInstance(pool);
			SystemClock.UseSource(() => epoch.ToUnixTimeMilliseconds() + pool.NowMillis);
			service = CronService.CreateDeterministic(typeof(ThreadPoolManagerRunnableRunner), TimeZoneInfo.Utc);
			using IDisposable cronScope = CronService.UseScopedInstance(service);

			await SimulationCronTaskInitialization.InitializeHousingTasksAsync(pool, WallTimeTimeout);

			Assert.Single(service.FindJobs<AuctionEndTask>(withSubTypes: false));
			Assert.Single(service.FindJobs<AuctionAutoFillTask>(withSubTypes: false));
			Assert.Single(service.FindJobs<MaintenanceTask>(withSubTypes: false));
			Assert.Equal(3, pool.ArmedTimerCount);
			Assert.Empty(pool.Faults);
		}
		finally
		{
			service?.Shutdown();
			SystemClock.UseSystemClock();
			ThreadPoolManager.RegisterInstance(previous ?? new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance));
			await pool.DisposeAsync();
		}
	}

	[Fact]
	public async Task StartupBodyFaultFailsTheSequenceBeforeTheNextConstructorCanHang()
	{
		ThreadPoolManager? previous = TryGetThreadPool();
		var pool = new VirtualThreadPool();
		bool nextFactoryCalled = false;
		try
		{
			ThreadPoolManager.RegisterInstance(pool);
			AggregateException exception = await Assert.ThrowsAsync<AggregateException>(() =>
				SimulationCronTaskInitialization.InitializeSequenceAsync(
					pool,
					WallTimeTimeout,
					(nameof(ThrowingStartupCronTask), () => new ThrowingStartupCronTask()),
					("next cron task", () =>
					{
						nextFactoryCalled = true;
						return new object();
					})));

			InvalidOperationException cause = Assert.IsType<InvalidOperationException>(Assert.Single(exception.InnerExceptions));
			Assert.Equal("startup body failed", cause.Message);
			Assert.False(nextFactoryCalled);
		}
		finally
		{
			AbstractCronTask.RestoreInitializationSemaphoreForTests();
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

	private sealed class ThrowingStartupCronTask : AbstractCronTask
	{
		public ThrowingStartupCronTask()
			: base(new CronExpression("0 0 0 ? * *"))
		{
		}

		protected override bool ShouldRunOnStart() => true;

		protected override void ExecuteTask() => throw new InvalidOperationException("startup body failed");
	}
}
