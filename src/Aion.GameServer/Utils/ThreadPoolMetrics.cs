using System.Collections.Concurrent;
using Aion.Commons.Diagnostics;

namespace Aion.GameServer.Utils;

/// <summary>Tracks tasks which are still armed in the production thread pool.</summary>
public sealed class ThreadPoolMetrics(bool captureDetails = false)
{
	private int _armedTimerCount;
	private long _nextId;
	private readonly ConcurrentDictionary<long, ActiveTimer> _active = new();

	public int ArmedTimerCount => Volatile.Read(ref _armedTimerCount);

	public void Observe(ThreadPoolScheduleObservation observation)
	{
		long id = 0;
		if (captureDetails)
		{
			id = Interlocked.Increment(ref _nextId);
			var method = observation.Callback?.Method;
			// Method identity only: no stack capture, delegate target, player or callback is retained.
			string callback = method == null ? "<unattributed>" : $"{method.DeclaringType?.FullName}.{method.Name}";
			_active[id] = new(observation.Kind.ToString(), observation.Delay.TotalMilliseconds,
				observation.Period?.TotalMilliseconds, callback, observation.ScheduledAt);
		}
		Interlocked.Increment(ref _armedTimerCount);
		_ = observation.Completion.ContinueWith(
			_ =>
			{
				if (captureDetails) _active.TryRemove(id, out var removed);
				Interlocked.Decrement(ref _armedTimerCount);
			},
			CancellationToken.None,
			TaskContinuationOptions.ExecuteSynchronously,
			TaskScheduler.Default);
	}

	public TimerCensusSnapshot? CaptureDetails()
	{
		if (!captureDetails) return null;
		var active = _active.Values.ToArray();
		var groups = active.GroupBy(timer => (timer.Kind, timer.DelayMilliseconds, timer.PeriodMilliseconds, timer.Callback))
			.Select(group => new TimerCensusGroup(group.Key.Kind, group.Key.DelayMilliseconds, group.Key.PeriodMilliseconds,
				group.Key.Callback, group.Count(), group.Min(timer => timer.ScheduledAt)))
			.OrderByDescending(group => group.Count).ThenBy(group => group.Callback, StringComparer.Ordinal)
			.ThenBy(group => group.Kind, StringComparer.Ordinal).ThenBy(group => group.DelayMilliseconds)
			.ThenBy(group => group.PeriodMilliseconds).ToArray();
		var included = groups.Take(128).ToArray();
		return new(active.Length, groups.Length, active.Length - included.Sum(group => group.Count), included);
	}

	private sealed record ActiveTimer(string Kind, double DelayMilliseconds, double? PeriodMilliseconds,
		string Callback, DateTimeOffset? ScheduledAt);
}
