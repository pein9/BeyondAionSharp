namespace Aion.GameServer.Utils;

/// <summary>Tracks tasks which are still armed in the production thread pool.</summary>
public sealed class ThreadPoolMetrics
{
	private int _armedTimerCount;

	public int ArmedTimerCount => Volatile.Read(ref _armedTimerCount);

	public void Observe(ThreadPoolScheduleObservation observation)
	{
		Interlocked.Increment(ref _armedTimerCount);
		_ = observation.Completion.ContinueWith(
			_ => Interlocked.Decrement(ref _armedTimerCount),
			CancellationToken.None,
			TaskContinuationOptions.ExecuteSynchronously,
			TaskScheduler.Default);
	}
}
