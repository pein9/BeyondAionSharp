using System.Collections.Concurrent;
using Aion.Commons.Logging;
using Microsoft.Extensions.Logging;

namespace Aion.Commons.Tests;

public sealed class AionProcessExceptionHandlerTests
{
	[Fact]
	public void UnhandledException_UsesJavaCriticalThreadMessage()
	{
		using var provider = new RecordingProvider();
		using var factory = CreateFactory(provider);
		using var logging = AionLog.OverrideFactory(factory);
		var exception = new InvalidOperationException("boom");

		AionProcessExceptionHandler.LogUnhandledException(exception, "worker-7");

		var entry = Assert.Single(provider.Entries);
		Assert.Equal("UncaughtExceptionHandler", entry.Category);
		Assert.Equal(LogLevel.Error, entry.Level);
		Assert.Equal("Critical Error - Thread [worker-7] terminated abnormally:", entry.Message);
		Assert.Same(exception, entry.Exception);
	}

	[Fact]
	public void UnobservedTaskException_IsLoggedAndMarkedObserved()
	{
		using var provider = new RecordingProvider();
		using var factory = CreateFactory(provider);
		using var logging = AionLog.OverrideFactory(factory);
		var exception = new AggregateException(new InvalidOperationException("task failed"));
		var args = new UnobservedTaskExceptionEventArgs(exception);

		AionProcessExceptionHandler.LogUnobservedTaskException(args, "finalizer");

		var entry = Assert.Single(provider.Entries);
		Assert.Same(exception, entry.Exception);
		Assert.True(args.Observed);
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
