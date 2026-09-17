using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Network.Sequrity;
using Aion.GameServer.Services;
using Aion.GameServer.Taskmanager;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class DeterministicModeTests
{
	[Fact]
	public async Task SameSeedAndVirtualTimelineReplayTheSameHarnessTrace()
	{
		const int seed = 48151623;

		string[] first = await ReplayAsync(seed);
		string[] second = await ReplayAsync(seed);

		Assert.True(first.SequenceEqual(second),
			$"Rnd seed: {seed}; first=[{string.Join(',', first)}], second=[{string.Join(',', second)}]");
		Assert.Contains("order:1,2,3", first);
		Assert.Equal(3, first.Count(entry => entry.StartsWith("flush:", StringComparison.Ordinal)));
	}

	[Fact]
	public async Task PeriodicManagerRearmMovesTheTimerToTheRegisteredVirtualPool()
	{
		ThreadPoolManager? previous = TryGetThreadPool();
		var firstPool = new VirtualThreadPool();
		var secondPool = new VirtualThreadPool();
		var runs = 0;
		try
		{
			Rnd.UseSeed(73);
			ThreadPoolManager.RegisterInstance(firstPool);
			var manager = new ProbePeriodicManager(100, () => runs++);

			ThreadPoolManager.RegisterInstance(secondPool);
			Rnd.UseSeed(73);
			manager.RearmForDeterministicSimulation();

			firstPool.Advance(TimeSpan.FromSeconds(1));
			Assert.Equal(0, runs);

			secondPool.Advance(TimeSpan.FromMilliseconds(499));
			Assert.Equal(0, runs);
			secondPool.Advance(TimeSpan.FromMilliseconds(51));
			Assert.True(runs > 0);
		}
		finally
		{
			Rnd.UseProductionRandom();
			ThreadPoolManager.RegisterInstance(previous ?? new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance));
			await firstPool.DisposeAsync();
			await secondPool.DisposeAsync();
		}
	}

	[Fact]
	public async Task NetFlusherAndShutdownCountdownAdvanceOnlyWithVirtualTime()
	{
		ThreadPoolManager? previous = TryGetThreadPool();
		var pool = new VirtualThreadPool();
		var lifetime = new FakeApplicationLifetime();
		var flushes = 0;
		try
		{
			ThreadPoolManager.RegisterInstance(pool);
			NetFlusher.Add(() => flushes++, 250);
			var hook = new ShutdownHook(lifetime, NullLogger<ShutdownHook>.Instance, TimeSpan.FromSeconds(1));
			hook.InitShutdown(exitCode: 2, delaySeconds: 2);

			Assert.Equal(0, flushes);
			Assert.Equal(0, lifetime.StopCalls);
			pool.Advance(TimeSpan.FromMilliseconds(1_999));
			Assert.Equal(7, flushes);
			Assert.Equal(0, lifetime.StopCalls);
			pool.Advance(TimeSpan.FromMilliseconds(1));

			Assert.Equal(8, flushes);
			Assert.Equal(1, lifetime.StopCalls);
			Assert.Equal(0, hook.RemainingSeconds);
		}
		finally
		{
			ThreadPoolManager.RegisterInstance(previous ?? new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance));
			await pool.DisposeAsync();
		}
	}

	private static async Task<string[]> ReplayAsync(int seed)
	{
		ThreadPoolManager? previous = TryGetThreadPool();
		var pool = new VirtualThreadPool();
		var trace = new List<string>();
		try
		{
			ThreadPoolManager.RegisterInstance(pool);
			Rnd.UseSeed(seed);
			trace.Add("order:" + string.Join(',', DeterministicIteration.ByIntKey([3, 1, 2], value => value)));
			_ = new ProbePeriodicManager(100, () => trace.Add($"tick:{pool.NowMillis}:{Rnd.NextInt(10_000)}"));
			NetFlusher.Add(() => trace.Add($"flush:{pool.NowMillis}"), 200);

			pool.Advance(TimeSpan.FromMilliseconds(650));
			return trace.ToArray();
		}
		finally
		{
			Rnd.UseProductionRandom();
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

	private sealed class ProbePeriodicManager(int period, Action onRun) : AbstractPeriodicTaskManager(period)
	{
		protected override void Run() => onRun();
	}

	private sealed class FakeApplicationLifetime : IHostApplicationLifetime
	{
		private readonly CancellationTokenSource started = new();
		private readonly CancellationTokenSource stopping = new();
		private readonly CancellationTokenSource stopped = new();

		public CancellationToken ApplicationStarted => started.Token;
		public CancellationToken ApplicationStopping => stopping.Token;
		public CancellationToken ApplicationStopped => stopped.Token;
		public int StopCalls { get; private set; }

		public void StopApplication()
		{
			StopCalls++;
			stopping.Cancel();
		}
	}
}
