using Aion.GameServer.TestKit;

namespace Aion.Simulation.Tests;

public sealed class SimulationDriverTests
{
	[Fact]
	public async Task AdvancesToNextTimerOrBotActionInDeadlineOrder()
	{
		await using var clock = new VirtualThreadPool(strict: true);
		var events = new List<string>();
		clock.Schedule(_ =>
		{
			events.Add("timer-5");
			return ValueTask.CompletedTask;
		}, TimeSpan.FromMilliseconds(5));
		var driver = new SimulationDriver(clock);
		driver.ScheduleAction("b01", "action-10", TimeSpan.FromMilliseconds(10), _ =>
		{
			events.Add("action-10");
			return ValueTask.CompletedTask;
		});
		driver.ScheduleAction("b01", "action-7", TimeSpan.FromMilliseconds(7), _ =>
		{
			events.Add("action-7");
			return ValueTask.CompletedTask;
		});

		SimulationRunResult result = await driver.RunUntilAsync(
			() => events.Count == 3,
			TimeSpan.FromMilliseconds(20),
			new SimulationDriverOptions { WallTimePerVirtualMinuteBudget = TimeSpan.FromHours(1) });

		Assert.Equal(["timer-5", "action-7", "action-10"], events);
		Assert.Equal(TimeSpan.FromMilliseconds(10), result.VirtualElapsed);
	}

	[Fact]
	public async Task TimeoutUsesVirtualMillisecondsWithoutSleeping()
	{
		await using var clock = new VirtualThreadPool(strict: true);
		var driver = new SimulationDriver(clock);

		var error = await Assert.ThrowsAsync<TimeoutException>(() =>
			driver.RunUntilAsync(() => false, TimeSpan.FromMilliseconds(25)));

		Assert.Contains("25 virtual millisecond", error.Message, StringComparison.Ordinal);
		Assert.Equal(25, clock.NowMillis);
	}

	[Fact]
	public async Task P003CostFailurePrintsTopPeriodicTasks()
	{
		await using var clock = new VirtualThreadPool(strict: true);
		var wall = new ManualTimeProvider();
		clock.ScheduleAtFixedRateTask(_ =>
		{
			wall.Advance(TimeSpan.FromSeconds(1));
			return ValueTask.CompletedTask;
		}, TimeSpan.Zero, TimeSpan.FromSeconds(20));
		var driver = new SimulationDriver(clock, timeProvider: wall);

		SimulationBudgetExceededException error = await Assert.ThrowsAsync<SimulationBudgetExceededException>(() =>
			driver.RunUntilAsync(
				() => clock.NowMillis >= 60_000,
				TimeSpan.FromMinutes(2),
				new SimulationDriverOptions { WallTimeBudget = TimeSpan.FromMinutes(1) }));

		Assert.True(error.WallElapsed >= TimeSpan.FromSeconds(4));
		VirtualTaskTiming timing = Assert.Single(error.TopPeriodicTasks);
		Assert.Equal(4, timing.Invocations);
		Assert.Contains("Top periodic callbacks", error.Message, StringComparison.Ordinal);
		Assert.Contains("4 invocation", error.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task ScenarioWallBudgetIsCheckedWhenTheCompletingActionReturns()
	{
		await using var clock = new VirtualThreadPool(strict: true);
		var wall = new ManualTimeProvider();
		var completed = false;
		var driver = new SimulationDriver(clock, timeProvider: wall);
		driver.ScheduleAction("b01", "slow", TimeSpan.Zero, _ =>
		{
			wall.Advance(TimeSpan.FromSeconds(11));
			completed = true;
			return ValueTask.CompletedTask;
		});

		SimulationBudgetExceededException error = await Assert.ThrowsAsync<SimulationBudgetExceededException>(() =>
			driver.RunUntilAsync(
				() => completed,
				TimeSpan.FromMinutes(1),
				new SimulationDriverOptions
				{
					WallTimeBudget = TimeSpan.FromSeconds(10),
					WallTimePerVirtualMinuteBudget = TimeSpan.FromHours(1),
				}));

		Assert.Contains("scenario wall-time budget", error.Message, StringComparison.Ordinal);
		Assert.Equal(TimeSpan.FromSeconds(11), error.WallElapsed);
	}

	private sealed class ManualTimeProvider : TimeProvider
	{
		private long timestamp;

		public override long TimestampFrequency => TimeSpan.TicksPerSecond;

		public override long GetTimestamp() => timestamp;

		public void Advance(TimeSpan by) => timestamp += by.Ticks;
	}
}
