using Aion.Bots.Protocol;

namespace Aion.Bots.Reflexes;

/// <summary>LIVE-only client heartbeat cadence. SIM transports do not use wall-clock pings.</summary>
public sealed class LiveBotPingScheduler
{
	public const int MinimumIntervalSeconds = 180;
	public const int MaximumIntervalSeconds = 183;
	public const int ServerEarlyThresholdSeconds = 178;

	private readonly TimeProvider timeProvider;
	private readonly Func<int> jitterSeconds;

	public LiveBotPingScheduler(TimeProvider? timeProvider = null, Func<int>? jitterSeconds = null)
	{
		this.timeProvider = timeProvider ?? TimeProvider.System;
		// Reviewed in P4-05: LIVE wall-time jitter is transport noise; tests and deterministic callers inject this delegate.
		this.jitterSeconds = jitterSeconds ?? (() => Random.Shared.Next(0, MaximumIntervalSeconds - MinimumIntervalSeconds + 1));
		NextDueAt = this.timeProvider.GetUtcNow() + NextInterval();
	}

	public DateTimeOffset NextDueAt { get; private set; }

	public BotClientPacket? Poll()
	{
		var now = timeProvider.GetUtcNow();
		if (now < NextDueAt)
			return null;
		NextDueAt = now + NextInterval();
		return GameClientPackets.Ping();
	}

	private TimeSpan NextInterval()
	{
		var jitter = jitterSeconds();
		var maximumJitter = MaximumIntervalSeconds - MinimumIntervalSeconds;
		if (jitter is < 0 || jitter > maximumJitter)
			throw new InvalidOperationException($"Ping jitter must be between 0 and {maximumJitter} seconds, but was {jitter}.");
		return TimeSpan.FromSeconds(MinimumIntervalSeconds + jitter);
	}
}
