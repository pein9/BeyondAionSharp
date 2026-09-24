using Aion.Bots.Scenarios;

namespace Aion.GameServer.Tests;

public sealed class NaturalObservedWaitTests
{
	[Fact]
	public async Task RespawnWaitDefendsBetweenShortObservations()
	{
		long now = 0;
		var events = new List<string>();
		await NaturalObservedWait.WaitAsync(TimeSpan.FromSeconds(181), TimeSpan.FromSeconds(2),
			() => now,
			(step, _) =>
			{
				Assert.True(step <= TimeSpan.FromSeconds(2));
				now += (long)step.TotalMilliseconds;
				events.Add($"observe:{now}");
				return Task.FromResult<IReadOnlyList<int>>(now == 2_000 ? [17, 17] : []);
			},
			(attacker, _) =>
			{
				Assert.Equal(17, attacker);
				events.Add($"defend:{now}");
				return Task.CompletedTask;
			}, CancellationToken.None);
		Assert.Equal(181_000, now);
		Assert.Equal(92, events.Count); // 91 observations and one immediate defense.
		Assert.Equal(["observe:2000", "defend:2000", "observe:4000"], events.Take(3));
	}

	[Fact]
	public async Task StalledClockFailsInsteadOfWaitingForever()
	{
		await Assert.ThrowsAsync<InvalidOperationException>(() => NaturalObservedWait.WaitAsync(
			TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), () => 0,
			(_, _) => Task.FromResult<IReadOnlyList<int>>([]),
			(_, _) => Task.CompletedTask, CancellationToken.None));
	}
}
