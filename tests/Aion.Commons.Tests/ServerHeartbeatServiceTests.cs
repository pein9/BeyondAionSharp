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
			new FixedMetrics(new(7, 11, 13, 1234, 567,
				new DispatchLatencySnapshot(2, 1, 5, 9, [1, 1], 3, 15), 42)),
			logger);

		service.WriteHeartbeat();

		Assert.Equal(TimeSpan.FromSeconds(10), ServerHeartbeatService.HeartbeatInterval);
		Assert.Equal(LogLevel.Information, logger.Level);
		Assert.StartsWith("Server heartbeat: connections=7, packetQueueDepth=11, armedTimers=13, workingSetBytes=", logger.Message);
		Assert.Equal(1234L, logger.Fields["WorkingSetBytes"]);
		Assert.Equal(567L, logger.Fields["LastGcHeapBytes"]);
		Assert.Equal(42L, logger.Fields["LastGcIndex"]);
		Assert.DoesNotContain("managedHeapBytes", logger.Message);
		using var writes = JsonDocument.Parse((string)logger.Fields["DispatcherWrites"]!);
		Assert.Equal(2, writes.RootElement.GetProperty("Count").GetInt64());
		Assert.Equal(2, writes.RootElement.GetProperty("Buckets").GetArrayLength());
		Assert.Equal(15, writes.RootElement.GetProperty("OldestPendingMilliseconds").GetDouble());
	}

	[Fact]
	public void Capture_ReportsProcessAndLastCollectionWithoutRequiringACollection()
	{
		var snapshot = new DelegateServerHeartbeatMetrics(() => 7, () => 11, () => 13).Capture();
		Assert.Equal(7, snapshot.ConnectionCount);
		Assert.Equal(11, snapshot.PacketQueueDepth);
		Assert.Equal(13, snapshot.ArmedTimerCount);
		Assert.True(snapshot.WorkingSetBytes > 0);
		Assert.True(snapshot.LastGcHeapBytes >= 0);
		Assert.True(snapshot.LastGcIndex >= 0);
		if (snapshot.LastGcIndex == 0) Assert.Equal(0, snapshot.LastGcHeapBytes);
		Assert.Null(snapshot.DispatcherWrites);
		Assert.Null(snapshot.TimerCensus);
	}

	[Fact]
	public void OptionalTimerCensusIsASeparateEventAndDoesNotAlterHeartbeatContract()
	{
		var census = new TimerCensusSnapshot(2, 1, 0,
			[new("Once", 1000, null, "Example.Callback", 2, DateTimeOffset.UnixEpoch)]);
		var metrics = new DelegateServerHeartbeatMetrics(() => 7, () => 11, () => 2, timerCensus: () => census);
		Assert.Same(census, metrics.Capture().TimerCensus);
		var logger = new RecordingLogger();
		new ServerHeartbeatService(metrics, logger).WriteHeartbeat();
		Assert.Equal(2, logger.Messages.Count);
		Assert.StartsWith("Server heartbeat: connections=7, packetQueueDepth=11, armedTimers=2,", logger.Messages[0]);
		Assert.EndsWith("dispatcherWrites=null", logger.Messages[0]);
		Assert.StartsWith("Scheduled timer census:", logger.Messages[1]);
		using var payload = JsonDocument.Parse((string)logger.Fields["TimerCensus"]!);
		Assert.Equal(2, payload.RootElement.GetProperty("ActiveCount").GetInt32());
		Assert.Equal("Example.Callback", payload.RootElement.GetProperty("Groups")[0].GetProperty("Callback").GetString());
	}

	[Fact]
	public void BeforeFirstCollection_ZeroIndexIsEmittedWithoutInventingAHeapMeasurement()
	{
		var logger = new RecordingLogger();
		new ServerHeartbeatService(new FixedMetrics(new(0, 0, 0, 1234)), logger).WriteHeartbeat();
		Assert.Equal(0L, logger.Fields["LastGcHeapBytes"]);
		Assert.Equal(0L, logger.Fields["LastGcIndex"]);
	}

	private sealed class FixedMetrics(ServerHeartbeatSnapshot snapshot) : IServerHeartbeatMetrics
	{
		public ServerHeartbeatSnapshot Capture() => snapshot;
	}

	private sealed class RecordingLogger : ILogger<ServerHeartbeatService>
	{
		public LogLevel? Level { get; private set; }
		public string? Message { get; private set; }
		public Dictionary<string, object?> Fields { get; private set; } = [];
		public List<string> Messages { get; } = [];

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
			Messages.Add(Message);
			Fields = ((IEnumerable<KeyValuePair<string, object?>>)state!).ToDictionary();
		}
	}
}
