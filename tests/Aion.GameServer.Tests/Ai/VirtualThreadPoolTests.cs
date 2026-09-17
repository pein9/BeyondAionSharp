using System.Collections.Concurrent;
using Aion.Commons.Logging;
using Aion.GameServer.TestKit;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Tests.Ai;

public sealed class VirtualThreadPoolTests
{
	[Fact]
	public async Task RecordsAndLogsOneShotAndFixedRateFaultsWithoutAbortingAdvance()
	{
		using var provider = new RecordingProvider();
		using var factory = LoggerFactory.Create(builder =>
		{
			builder.ClearProviders();
			builder.SetMinimumLevel(LogLevel.Trace);
			builder.AddProvider(provider);
		});
		using var logging = AionLog.OverrideFactory(factory);
		var pool = new VirtualThreadPool();
		var healthyRuns = 0;

		pool.Schedule(() => throw new InvalidOperationException("one-shot"), 1);
		var fixedRate = pool.ScheduleAtFixedRateTask(
			_ => throw new ArgumentException("fixed-rate"),
			TimeSpan.FromMilliseconds(2),
			TimeSpan.FromMilliseconds(10));
		pool.Schedule(() => healthyRuns++, 3);

		pool.Advance(TimeSpan.FromMilliseconds(3));

		Assert.Equal(1, healthyRuns);
		Assert.Equal([ThreadPoolScheduleKind.Once, ThreadPoolScheduleKind.FixedRate], pool.Faults.Select(fault => fault.Kind));
		Assert.Equal(2, provider.Entries.Count(entry => entry.Level == LogLevel.Error && entry.Category == nameof(VirtualThreadPool)));
		fixedRate.Cancel();
		await pool.DisposeAsync();
	}

	[Fact]
	public async Task StrictModeFailsOnDisposeAfterRecordedFault()
	{
		var pool = new VirtualThreadPool(strict: true);
		pool.Schedule(() => throw new InvalidOperationException("strict failure"), 0);
		pool.Advance(TimeSpan.Zero);

		var exception = await Assert.ThrowsAsync<AggregateException>(() => pool.DisposeAsync().AsTask());

		Assert.Single(exception.InnerExceptions);
		Assert.IsType<InvalidOperationException>(exception.InnerExceptions[0]);
	}

	[Fact]
	public async Task ThrowsAtTickLimitWithoutMovingClockToTarget()
	{
		var pool = new VirtualThreadPool();
		var runs = 0;
		var handle = pool.ScheduleAtFixedRateTask(
			_ =>
			{
				runs++;
				return ValueTask.CompletedTask;
			},
			TimeSpan.Zero,
			TimeSpan.FromMilliseconds(1));

		var exception = Assert.Throws<InvalidOperationException>(() => pool.Advance(TimeSpan.FromMilliseconds(100_001)));

		Assert.Contains("100000 timer ticks", exception.Message);
		Assert.Equal(100_000, runs);
		Assert.Equal(99_999, pool.NowMillis);
		handle.Cancel();
		await pool.DisposeAsync();
	}

	[Fact]
	public async Task HandlesReportDelayAgainstVirtualTime()
	{
		var pool = new VirtualThreadPool();
		SystemClock.UseSource(() => DateTimeOffset.UnixEpoch.ToUnixTimeMilliseconds() + pool.NowMillis);
		try
		{
			var oneShot = pool.Schedule(() => { }, 10_000);
			var fixedRate = pool.ScheduleAtFixedRateTask(
				_ => ValueTask.CompletedTask,
				TimeSpan.FromSeconds(2),
				TimeSpan.FromSeconds(5));

			Assert.Equal(10_000, oneShot.GetDelay(TimeUnit.MILLISECONDS));
			Assert.Equal(2_000, fixedRate.GetDelay(TimeUnit.MILLISECONDS));
			pool.Advance(TimeSpan.FromSeconds(2));
			Assert.Equal(8_000, oneShot.GetDelay(TimeUnit.MILLISECONDS));
			Assert.Equal(5_000, fixedRate.GetDelay(TimeUnit.MILLISECONDS));

			fixedRate.Cancel();
			await pool.DisposeAsync();
		}
		finally
		{
			SystemClock.UseSystemClock();
		}
	}

	[Fact]
	public async Task PriorityQueueRunsEqualDeadlinesInInsertionOrderAndRejectsBackwardTime()
	{
		var pool = new VirtualThreadPool();
		var trace = new List<int>();
		pool.Schedule(() => trace.Add(1), 10);
		pool.Schedule(() => trace.Add(2), 5);
		pool.Schedule(() => trace.Add(3), 10);

		pool.Advance(TimeSpan.FromMilliseconds(10));

		Assert.Equal([2, 1, 3], trace);
		Assert.Throws<ArgumentOutOfRangeException>(() => pool.Advance(TimeSpan.FromMilliseconds(-1)));
		Assert.Equal(10, pool.NowMillis);
		await pool.DisposeAsync();
	}

	[Fact]
	public async Task ReentrantAdvanceIsRecordedAsAStrictFault()
	{
		var pool = new VirtualThreadPool(strict: true);
		pool.Schedule(() => pool.Advance(TimeSpan.Zero), 0);

		pool.Advance(TimeSpan.Zero);

		VirtualThreadPoolFault fault = Assert.Single(pool.Faults);
		Assert.Contains("re-entrant", fault.Exception.Message);
		await Assert.ThrowsAsync<AggregateException>(() => pool.DisposeAsync().AsTask());
	}

	[Fact]
	public async Task SchedulingFromAnotherThreadDuringAdvanceIsRejected()
	{
		var pool = new VirtualThreadPool();
		using var entered = new ManualResetEventSlim();
		using var release = new ManualResetEventSlim();
		pool.Schedule(
			() =>
			{
				entered.Set();
				release.Wait();
			},
			0);

		Task advance = Task.Run(() => pool.Advance(TimeSpan.Zero));
		try
		{
			Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
			InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => pool.Schedule(() => { }, 0));
			Assert.Contains("owner thread", exception.Message);
		}
		finally
		{
			release.Set();
			await advance;
		}
		await pool.DisposeAsync();
	}

	private sealed class RecordingProvider : ILoggerProvider
	{
		public ConcurrentQueue<Entry> Entries { get; } = new();

		public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, Entries);

		public void Dispose()
		{
		}
	}

	private sealed class RecordingLogger(string category, ConcurrentQueue<Entry> entries) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter) =>
			entries.Enqueue(new Entry(category, logLevel, formatter(state, exception), exception));
	}

	private sealed record Entry(string Category, LogLevel Level, string Message, Exception? Exception);
}
