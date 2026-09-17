using System.Collections.Concurrent;
using Aion.Commons.Logging;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class ThreadPoolManagerSchedulingTests
{
	[Fact]
	public async Task FixedRateTask_ContinuesAfterOneIterationThrows()
	{
		using var provider = new RecordingProvider();
		using var factory = CreateFactory(provider);
		using var logging = AionLog.OverrideFactory(factory);
		await using var pool = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		var secondRun = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var runs = 0;

		var task = pool.ScheduleAtFixedRateTask(
			_ =>
			{
				if (Interlocked.Increment(ref runs) == 1)
					throw new InvalidOperationException("first run failed");

				secondRun.TrySetResult();
				return ValueTask.CompletedTask;
			},
			TimeSpan.Zero,
			TimeSpan.FromMilliseconds(10));

		await secondRun.Task.WaitAsync(TimeSpan.FromSeconds(2));
		task.Cancel();
		await task.Completion.WaitAsync(TimeSpan.FromSeconds(2));

		Assert.True(runs >= 2);
		var error = Assert.Single(provider.Entries, entry => entry.Level == LogLevel.Error);
		Assert.Equal(nameof(Aion.GameServer.Commons.Utils.Concurrent.ExecuteWrapper), error.Category);
		Assert.IsType<InvalidOperationException>(error.Exception);
		Assert.Equal("fixed-rate", error.Scopes["timer"]);
		Assert.True(DateTimeOffset.TryParse(error.Scopes["timerScheduledAt"], out _));
	}

	[Fact]
	public async Task ScheduledTask_LogsWhenRuntimeExceedsConfiguredMaximum()
	{
		using var provider = new RecordingProvider();
		using var factory = CreateFactory(provider);
		using var logging = AionLog.OverrideFactory(factory);
		await using var pool = new ThreadPoolManager(
			NullLogger<ThreadPoolManager>.Instance,
			maximumRuntimeWithoutWarning: -1);

		var task = pool.Schedule(_ => ValueTask.CompletedTask, TimeSpan.Zero);
		await task.Completion.WaitAsync(TimeSpan.FromSeconds(2));

		var warning = Assert.Single(provider.Entries, entry => entry.Level == LogLevel.Warning);
		Assert.Equal(nameof(Aion.GameServer.Commons.Utils.Concurrent.ExecuteWrapper), warning.Category);
		Assert.Contains("execution time:", warning.Message);
	}

	[Fact]
	public async Task CompletedAndCancelledTasksAreRemovedFromShutdownTracking()
	{
		await using var pool = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		var oneShots = Enumerable.Range(0, 100)
			.Select(_ => pool.Schedule(_ => ValueTask.CompletedTask, TimeSpan.Zero))
			.ToArray();
		await Task.WhenAll(oneShots.Select(task => task.Completion));
		await WaitUntilAsync(() => pool.ScheduledTaskCount == 0);

		var fixedRate = pool.ScheduleAtFixedRateTask(
			_ => ValueTask.CompletedTask,
			TimeSpan.FromHours(1),
			TimeSpan.FromHours(1));
		Assert.Equal(1, pool.ScheduledTaskCount);
		Assert.True(fixedRate.Cancel());
		await fixedRate.Completion.WaitAsync(TimeSpan.FromSeconds(2));
		await WaitUntilAsync(() => pool.ScheduledTaskCount == 0);
	}

	private static async Task WaitUntilAsync(Func<bool> condition)
	{
		var timeout = DateTime.UtcNow + TimeSpan.FromSeconds(2);
		while (!condition() && DateTime.UtcNow < timeout)
			await Task.Delay(10);
		Assert.True(condition());
	}

	private static ILoggerFactory CreateFactory(ILoggerProvider provider) => LoggerFactory.Create(builder =>
	{
		builder.ClearProviders();
		builder.SetMinimumLevel(LogLevel.Trace);
		builder.AddProvider(provider);
	});

	private sealed class RecordingProvider : ILoggerProvider, ISupportExternalScope
	{
		public ConcurrentQueue<Entry> Entries { get; } = new();
		private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();

		public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, Entries, () => _scopeProvider);

		public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;

		public void Dispose()
		{
		}
	}

	private sealed class RecordingLogger(
		string category,
		ConcurrentQueue<Entry> entries,
		Func<IExternalScopeProvider> getScopeProvider) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			var scopes = new Dictionary<string, string>(StringComparer.Ordinal);
			getScopeProvider().ForEachScope(
				(scope, target) =>
				{
					if (scope is IEnumerable<KeyValuePair<string, string>> values)
					{
						foreach (var value in values)
							target[value.Key] = value.Value;
					}
				},
				scopes);
			entries.Enqueue(new Entry(category, logLevel, formatter(state, exception), exception, scopes));
		}
	}

	private sealed record Entry(
		string Category,
		LogLevel Level,
		string Message,
		Exception? Exception,
		IReadOnlyDictionary<string, string> Scopes);
}
