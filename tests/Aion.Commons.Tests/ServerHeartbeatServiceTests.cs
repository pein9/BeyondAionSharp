using Aion.Commons.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Aion.Commons.Tests;

public sealed class ServerHeartbeatServiceTests
{
	[Fact]
	public void WriteHeartbeat_EmitsAllMetricsAtInformationLevel()
	{
		var logger = new RecordingLogger();
		var service = new ServerHeartbeatService(
			new DelegateServerHeartbeatMetrics(() => 7, () => 11, () => 13,
				() => new DispatchLatencySnapshot(2, 1, 5, 9, [1, 1], 3, 15)),
			logger);

		service.WriteHeartbeat();

		Assert.Equal(TimeSpan.FromSeconds(10), ServerHeartbeatService.HeartbeatInterval);
		Assert.Equal(LogLevel.Information, logger.Level);
		Assert.StartsWith("Server heartbeat: connections=7, packetQueueDepth=11, armedTimers=13, workingSetBytes=", logger.Message);
		Assert.True((long)logger.Fields["WorkingSetBytes"]! > 0);
		Assert.True((long)logger.Fields["ManagedHeapBytes"]! > 0);
		using var writes = JsonDocument.Parse((string)logger.Fields["DispatcherWrites"]!);
		Assert.Equal(2, writes.RootElement.GetProperty("Count").GetInt64());
		Assert.Equal(2, writes.RootElement.GetProperty("Buckets").GetArrayLength());
		Assert.Equal(15, writes.RootElement.GetProperty("OldestPendingMilliseconds").GetDouble());
	}

	private sealed class RecordingLogger : ILogger<ServerHeartbeatService>
	{
		public LogLevel? Level { get; private set; }
		public string? Message { get; private set; }
		public Dictionary<string, object?> Fields { get; private set; } = [];

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			Level = logLevel;
			Message = formatter(state, exception);
			Fields = ((IEnumerable<KeyValuePair<string, object?>>)state!).ToDictionary();
		}
	}
}
