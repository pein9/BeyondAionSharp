using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace Aion.Commons.Diagnostics;

/// <summary>Supplies one heartbeat sample; independently owned process counters are not a global transaction.</summary>
public interface IServerHeartbeatMetrics
{
	ServerHeartbeatSnapshot Capture();
}

public readonly record struct ServerHeartbeatSnapshot(
	int ConnectionCount,
	int PacketQueueDepth,
	int ArmedTimerCount,
	long WorkingSetBytes = 0,
	long LastGcHeapBytes = 0,
	DispatchLatencySnapshot? DispatcherWrites = null,
	long LastGcIndex = 0,
	TimerCensusSnapshot? TimerCensus = null);

public sealed record TimerCensusGroup(string Kind, double DelayMilliseconds, double? PeriodMilliseconds,
	string Callback, int Count, DateTimeOffset? OldestScheduledUtc);

public sealed record TimerCensusSnapshot(int ActiveCount, int GroupCount, int OmittedActiveCount,
	IReadOnlyList<TimerCensusGroup> Groups);

/// <summary>Adapts server-owned counters without coupling Commons to any server implementation.</summary>
public sealed class DelegateServerHeartbeatMetrics(
	Func<int> connectionCount,
	Func<int> packetQueueDepth,
	Func<int> armedTimerCount,
	Func<DispatchLatencySnapshot>? dispatcherWrites = null,
	Func<TimerCensusSnapshot?>? timerCensus = null) : IServerHeartbeatMetrics
{
	public ServerHeartbeatSnapshot Capture()
	{
		using var process = Process.GetCurrentProcess();
		process.Refresh();
		// GetTotalMemory(false) returned negative estimates in LIVE. This is instead
		// an explicitly last-collection snapshot, not current allocations. Index zero
		// means no collection has happened; do not force a GC or fabricate a zero sample.
		var gc = GC.GetGCMemoryInfo();
		return new(connectionCount(), packetQueueDepth(), armedTimerCount(), process.WorkingSet64,
			gc.HeapSizeBytes, dispatcherWrites?.Invoke(), gc.Index, timerCensus?.Invoke());
	}
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
			"Server heartbeat: connections={ConnectionCount}, packetQueueDepth={PacketQueueDepth}, armedTimers={ArmedTimerCount}, workingSetBytes={WorkingSetBytes}, lastGcHeapBytes={LastGcHeapBytes}, lastGcIndex={LastGcIndex}, dispatcherWrites={DispatcherWrites}",
			snapshot.ConnectionCount,
			snapshot.PacketQueueDepth,
			snapshot.ArmedTimerCount,
			snapshot.WorkingSetBytes,
			snapshot.LastGcHeapBytes,
			snapshot.LastGcIndex,
			JsonSerializer.Serialize(snapshot.DispatcherWrites));
		// Separate optional event preserves the versioned heartbeat/acceptance wire contract.
		if (snapshot.TimerCensus != null)
			logger.LogInformation("Scheduled timer census: {TimerCensus}", JsonSerializer.Serialize(snapshot.TimerCensus));
	}
}
