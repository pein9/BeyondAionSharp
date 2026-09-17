using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aion.Commons.Diagnostics;

/// <summary>Supplies one atomic snapshot for the process heartbeat.</summary>
public interface IServerHeartbeatMetrics
{
	ServerHeartbeatSnapshot Capture();
}

public readonly record struct ServerHeartbeatSnapshot(
	int ConnectionCount,
	int PacketQueueDepth,
	int ArmedTimerCount);

/// <summary>Adapts server-owned counters without coupling Commons to any server implementation.</summary>
public sealed class DelegateServerHeartbeatMetrics(
	Func<int> connectionCount,
	Func<int> packetQueueDepth,
	Func<int> armedTimerCount) : IServerHeartbeatMetrics
{
	public ServerHeartbeatSnapshot Capture() => new(
		connectionCount(),
		packetQueueDepth(),
		armedTimerCount());
}

/// <summary>Emits the liveness and backlog signal consumed by the LIVE-run watcher.</summary>
public sealed class ServerHeartbeatService(
	IServerHeartbeatMetrics metrics,
	ILogger<ServerHeartbeatService> logger) : BackgroundService
{
	internal static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(10);

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		WriteHeartbeat();
		using var timer = new PeriodicTimer(HeartbeatInterval);
		try
		{
			while (await timer.WaitForNextTickAsync(stoppingToken))
				WriteHeartbeat();
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
		}
	}

	internal void WriteHeartbeat()
	{
		var snapshot = metrics.Capture();
		logger.LogInformation(
			"Server heartbeat: connections={ConnectionCount}, packetQueueDepth={PacketQueueDepth}, armedTimers={ArmedTimerCount}",
			snapshot.ConnectionCount,
			snapshot.PacketQueueDepth,
			snapshot.ArmedTimerCount);
	}
}
