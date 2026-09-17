using Aion.Commons.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Aion.Commons.Tests;

public sealed class ServerHeartbeatServiceTests
{
	[Fact]
	public void WriteHeartbeat_EmitsAllMetricsAtInformationLevel()
	{
		var logger = new RecordingLogger();
		var service = new ServerHeartbeatService(
			new DelegateServerHeartbeatMetrics(() => 7, () => 11, () => 13),
			logger);

		service.WriteHeartbeat();

		Assert.Equal(TimeSpan.FromSeconds(10), ServerHeartbeatService.HeartbeatInterval);
		Assert.Equal(LogLevel.Information, logger.Level);
		Assert.Equal(
			"Server heartbeat: connections=7, packetQueueDepth=11, armedTimers=13",
			logger.Message);
	}

	private sealed class RecordingLogger : ILogger<ServerHeartbeatService>
	{
		public LogLevel? Level { get; private set; }
		public string? Message { get; private set; }

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
		}
	}
}
