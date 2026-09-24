namespace Aion.Bots.Scenarios;

/// <summary>Advance a bounded gameplay wait only as far as the next chance to observe and defend.</summary>
public static class NaturalObservedWait
{
	public static async Task WaitAsync(TimeSpan duration, TimeSpan observationInterval,
		Func<long> nowMillis,
		Func<TimeSpan, CancellationToken, Task<IReadOnlyList<int>>> advanceAndObserve,
		Func<int, CancellationToken, Task> defend,
		CancellationToken token)
	{
		ArgumentNullException.ThrowIfNull(nowMillis);
		ArgumentNullException.ThrowIfNull(advanceAndObserve);
		ArgumentNullException.ThrowIfNull(defend);
		long durationMillis = checked((long)duration.TotalMilliseconds);
		long intervalMillis = checked((long)observationInterval.TotalMilliseconds);
		if (durationMillis < 0) throw new ArgumentOutOfRangeException(nameof(duration));
		if (intervalMillis <= 0) throw new ArgumentOutOfRangeException(nameof(observationInterval));
		long deadline = checked(nowMillis() + durationMillis);
		while (nowMillis() < deadline)
		{
			token.ThrowIfCancellationRequested();
			long before = nowMillis();
			IReadOnlyList<int> attackers = await advanceAndObserve(
				TimeSpan.FromMilliseconds(Math.Min(intervalMillis, deadline - before)), token);
			if (nowMillis() <= before)
				throw new InvalidOperationException("The observed wait did not advance its gameplay clock.");
			foreach (int attacker in attackers.Distinct())
				await defend(attacker, token);
		}
	}
}
