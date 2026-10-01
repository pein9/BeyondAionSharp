using Aion.Bots.Scenarios;

namespace Aion.GameServer.Tests;

/// <summary>AK-03: the carrier policy against the Leg 5 contract's six ring carriers.</summary>
public sealed class NaturalCarrierPolicyTests
{
	private static readonly NaturalAltgardTimedSpawn[] Carriers = NaturalAltgardContract.LoadLeg("l5").TimedSpawnList;
	private const int Passion = 122000039, Jealousy = 122000040, Love = 122000041;
	private const int Lu = 210599, Zen = 210622, Ang = 210623, Ring = 210620, Zoo = 210621, Di = 210624;
	private const long Day = 367 * NaturalGameClock.MinutesPerDay;

	private static long At(int hour, int minute = 0) => Day + hour * 60 + minute;

	private static NaturalCarrierChoice Decide(long minutes, int[] rings, int[]? visible = null, int travel = 30) =>
		NaturalCarrierPolicy.Decide(new NaturalCarrierObservation(rings.ToHashSet(), minutes, (visible ?? []).ToHashSet(), travel), Carriers);

	[Fact]
	public void TheClockRunsAMinutePerFiveSecondsAndMatchesJavaHours()
	{
		Assert.Equal(At(22, 3), NaturalGameClock.MinutesAt((int)At(22, 0), 10_000, 10_000 + 3 * 5000 + 4999));
		Assert.Equal(At(22, 0), NaturalGameClock.MinutesAt((int)At(22, 0), 10_000, 9_000));
		Assert.Equal(22, NaturalGameClock.HourOf(At(22, 59)));
		Assert.Equal(0, NaturalGameClock.HourOf(At(24)));
		Assert.Equal(120, NaturalGameClock.MinutesUntilHour(At(20), 22));
		Assert.Equal(7 * 60 - 10, NaturalGameClock.MinutesUntilHour(At(22, 10), 5));
		Assert.Equal(0, NaturalGameClock.MinutesUntilHour(At(5), 5));
	}

	[Fact]
	public void ByDayTheDayCarriersAreHuntedAndTheLoveRingWaitsForTheNight()
	{
		// Noon: Lu and Zen carry Passion, Ang carries Jealousy; Love has no carrier until 22:00.
		NaturalCarrierChoice passion = Decide(At(12), [Passion]);
		Assert.Equal("hunt", passion.Action);
		Assert.Contains(passion.Carrier!.NpcId, new[] { Lu, Zen });
		Assert.Equal((Ang, Jealousy), (Decide(At(12), [Jealousy]).Carrier!.NpcId, Decide(At(12), [Jealousy]).RingItemId));
		NaturalCarrierChoice love = Decide(At(12), [Love]);
		Assert.Equal("wait", love.Action);
		Assert.Equal(10 * 60, love.WaitGameMinutes);
		Assert.Contains(love.Carrier!.NpcId, new[] { Zoo, Di });
		// With all three needed, the day rings come first; the wait comes only when Love is all that is left.
		Assert.Equal("hunt", Decide(At(12), [Passion, Jealousy, Love]).Action);
		Assert.Equal("done", Decide(At(12), []).Action);
	}

	[Fact]
	public void ByNightTheNightCarriersAreHunted()
	{
		Assert.Contains(Decide(At(23), [Love]).Carrier!.NpcId, new[] { Zoo, Di });
		Assert.Equal(Ring, Decide(At(2), [Jealousy]).Carrier!.NpcId);
		// 21:30: Zen has gone (04:00-21:00) and Lu leaves at 22:00, too soon for a 40-minute walk: wait for 04:00.
		NaturalCarrierChoice late = Decide(At(21, 30), [Passion], travel: 40);
		Assert.Equal(("wait", Zen), (late.Action, late.Carrier!.NpcId));
		Assert.Equal(6 * 60 + 30, late.WaitGameMinutes);
	}

	[Fact]
	public void ACarrierInViewIsHuntedEvenAtTheEdgeOfItsWindow()
	{
		NaturalCarrierChoice edge = Decide(At(21, 55), [Passion], visible: [Lu], travel: 40);
		Assert.Equal(("hunt", Lu), (edge.Action, edge.Carrier!.NpcId));
		// Out of view and too close to the edge to reach: not hunted.
		Assert.NotEqual("hunt", Decide(At(21, 55), [Passion], travel: 40).Action);
	}

	[Fact]
	public void EveryHourHasAPlanForEveryRing()
	{
		// Whatever the hour, each ring is either huntable now or has a carrier appearing within the day.
		foreach (int hour in Enumerable.Range(0, 24))
			foreach (int ring in new[] { Passion, Jealousy, Love })
			{
				NaturalCarrierChoice choice = Decide(At(hour), [ring]);
				Assert.True(choice.Action == "hunt" || choice.WaitGameMinutes is > 0 and <= NaturalGameClock.MinutesPerDay, $"{hour}:00 ring {ring}");
				Assert.Equal(ring, choice.RingItemId);
			}
	}
}
