using Aion.GameServer.Services.Cron;
using Aion.GameServer.Taskmanager;
using Aion.GameServer.Taskmanager.Tasks.Housing;
using Aion.GameServer.TestKit;
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
		var pool = new VirtualThreadPool { StartupSingletonTimeout = WallTimeTimeout };
		var epoch = new DateTimeOffset(2030, 1, 1, 8, 59, 0, TimeSpan.Zero);
		CronService? service = null;
		try
		{
			ThreadPoolManager.RegisterInstance(pool);
			SystemClock.UseSource(() => epoch.ToUnixTimeMilliseconds() + pool.NowMillis);
			service = CronService.CreateDeterministic(typeof(ThreadPoolManagerRunnableRunner), TimeZoneInfo.Utc);
			using IDisposable cronScope = CronService.UseScopedInstance(service);

			SimulationCronTaskInitialization.InitializeHousingTasks(pool);

			// Each body schedules its job before the next constructor runs, so the virtual job ids follow
			// GameServer.java:120-122: AuctionEndTask, AuctionAutoFillTask, MaintenanceTask.
			Assert.Equal("JobKey:VirtualJob:1", Assert.Single(service.FindJobs<AuctionEndTask>(withSubTypes: false)).Key.Name);
			Assert.Equal("JobKey:VirtualJob:2", Assert.Single(service.FindJobs<AuctionAutoFillTask>(withSubTypes: false)).Key.Name);
			Assert.Equal("JobKey:VirtualJob:3", Assert.Single(service.FindJobs<MaintenanceTask>(withSubTypes: false)).Key.Name);
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
		var pool = new VirtualThreadPool { StartupSingletonTimeout = WallTimeTimeout };
		bool nextFactoryCalled = false;
		try
		{
			ThreadPoolManager.RegisterInstance(pool);
			AggregateException exception = Assert.Throws<AggregateException>(() =>
				SimulationCronTaskInitialization.InitializeSequence(
					pool,
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

	[Fact]
	public async Task ProductionPoolConstructsStartupSingletonsInlineOnTheBootThread()
	{
		await using var pool = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		var singleton = new object();
		int bootThread = Environment.CurrentManagedThreadId;
		int factoryThread = 0;

		object constructed = pool.ConstructStartupSingleton("inline probe", () =>
		{
			factoryThread = Environment.CurrentManagedThreadId;
			return singleton;
		});

		Assert.Same(singleton, constructed);
		Assert.Equal(bootThread, factoryThread);
		Assert.Equal(0, pool.ScheduledTaskCount);
		// Java's getInstance() throws straight to GameServer.main; nothing wraps or delays the failure.
		var failure = new InvalidOperationException("constructor failed");
		Assert.Same(failure, Assert.Throws<InvalidOperationException>(() =>
			pool.ConstructStartupSingleton<object>("failing probe", () => throw failure)));
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
