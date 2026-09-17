using System.Collections.Concurrent;
using Aion.Commons.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.Commons.Tests;

public sealed class AionLogTests
{
	[Fact]
	public void LoggerCreatedBeforeSetFactoryForwardsToTheLaterFactory()
	{
		AionLog.SetFactory(NullLoggerFactory.Instance);
		var logger = AionLog.For("EARLY_LOG");
		using var provider = new RecordingProvider();
		using var factory = CreateFactory(provider);

		try
		{
			AionLog.SetFactory(factory);
			logger.LogWarning("late factory {Value}", 7);

			var entry = Assert.Single(provider.Entries);
			Assert.Equal("EARLY_LOG", entry.Category);
			Assert.Equal("late factory 7", entry.Message);
			Assert.Equal(typeof(AionLogTests).FullName, entry.Properties["aion.caller.type"]);
			Assert.Equal(nameof(LoggerCreatedBeforeSetFactoryForwardsToTheLaterFactory), entry.Properties["aion.caller.member"]);
		}
		finally
		{
			AionLog.SetFactory(NullLoggerFactory.Instance);
		}
	}

	[Fact]
	public async Task ParallelFactoryOverridesStayInTheirOwnExecutionContexts()
	{
		AionLog.SetFactory(NullLoggerFactory.Instance);
		var logger = AionLog.For("FLOW_LOG");
		using var firstProvider = new RecordingProvider();
		using var secondProvider = new RecordingProvider();
		using var firstFactory = CreateFactory(firstProvider);
		using var secondFactory = CreateFactory(secondProvider);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		using var ready = new CountdownEvent(2);

		Task WriteAsync(ILoggerFactory factory, string value) => Task.Run(async () =>
		{
			using var scope = AionLog.OverrideFactory(factory);
			ready.Signal();
			await release.Task;
			logger.LogError("flow {Value}", value);
		});

		var first = WriteAsync(firstFactory, "one");
		var second = WriteAsync(secondFactory, "two");
		Assert.True(ready.Wait(TimeSpan.FromSeconds(2)));
		release.TrySetResult();
		await Task.WhenAll(first, second);

		Assert.Equal("flow one", Assert.Single(firstProvider.Entries).Message);
		Assert.Equal("flow two", Assert.Single(secondProvider.Entries).Message);
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

	private sealed class RecordingLogger : ILogger
	{
		private readonly string _category;
		private readonly ConcurrentQueue<Entry> _entries;

		public RecordingLogger(string category, ConcurrentQueue<Entry> entries)
		{
			_category = category;
			_entries = entries;
		}

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			var properties = state is IEnumerable<KeyValuePair<string, object?>> values
				? values.ToDictionary(pair => pair.Key, pair => pair.Value)
				: new Dictionary<string, object?>();
			_entries.Enqueue(new Entry(_category, formatter(state, exception), properties));
		}
	}

	private sealed record Entry(string Category, string Message, IReadOnlyDictionary<string, object?> Properties);
}
