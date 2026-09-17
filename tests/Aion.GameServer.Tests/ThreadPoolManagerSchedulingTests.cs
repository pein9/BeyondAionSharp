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

	private static ILoggerFactory CreateFactory(ILoggerProvider provider) => LoggerFactory.Create(builder =>
	{
		builder.ClearProviders();
		builder.SetMinimumLevel(LogLevel.Trace);
		builder.AddProvider(provider);
	});

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
