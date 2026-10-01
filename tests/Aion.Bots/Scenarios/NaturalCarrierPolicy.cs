namespace Aion.Bots.Scenarios;

/// <summary>AK-03: the game clock as the client keeps it. The server sends the minutes since 01.01.0000 in SM_GAME_TIME on entering
/// the world and every 3 minutes; between packets the clock runs one game minute per real 5 s (Java GameTimeService).</summary>
public static class NaturalGameClock
{
	public const int MillisPerGameMinute = 5000;
	public const int MinutesPerDay = 1440;

	/// <summary>The game minutes now, from the last SM_GAME_TIME (<paramref name="observedMinutes"/>) and when it arrived.</summary>
	public static long MinutesAt(int observedMinutes, long observedAtMillis, long nowMillis) =>
		observedMinutes + Math.Max(0, nowMillis - observedAtMillis) / MillisPerGameMinute;

	/// <summary>Java GameTime.getHour(): (minutes % 1440) / 60.</summary>
	public static int HourOf(long minutes) => (int)(minutes % MinutesPerDay / 60);

	/// <summary>Game minutes from <paramref name="minutes"/> until the next start of <paramref name="hour"/> (0 when it starts now).</summary>
	public static int MinutesUntilHour(long minutes, int hour)
	{
		int ofDay = (int)(minutes % MinutesPerDay);
		return ((hour * 60 - ofDay) % MinutesPerDay + MinutesPerDay) % MinutesPerDay;
	}

	/// <summary>Game minutes left in the window of a carrier present now (until its despawn hour starts).</summary>
	public static int MinutesLeftInWindow(long minutes, NaturalAltgardTimedSpawn carrier) => MinutesUntilHour(minutes, carrier.DespawnHour);
}

/// <param name="Rings">Each ring still needed, by item id.</param>
/// <param name="GameMinutes">The game time now (NaturalGameClock.MinutesAt).</param>
/// <param name="VisibleCarrierNpcIds">Carriers alive in the client's view.</param>
/// <param name="TravelGameMinutes">How long, in game minutes, the bot needs to reach a carrier it is not beside.</param>
public sealed record NaturalCarrierObservation(IReadOnlySet<int> Rings, long GameMinutes, IReadOnlySet<int> VisibleCarrierNpcIds,
	int TravelGameMinutes);

/// <param name="Action">"done" (no ring needed), "hunt" (<paramref name="Carrier"/>), or "wait" (no carrier for a needed ring can be
/// reached in its window now; <paramref name="WaitGameMinutes"/> until the soonest one appears: do other work meanwhile).</param>
public sealed record NaturalCarrierChoice(string Action, NaturalAltgardTimedSpawn? Carrier, int? RingItemId, int WaitGameMinutes, string Reason);

/// <summary>AK-03: Q2292's ring carriers live by the hour (Java TemporarySpawn). Hunt a carrier that is present and can be reached
/// before its window closes, preferring one in view; otherwise wait for the soonest window of a needed ring (AK-Q3: the Love
/// Ring only by night) and spend the wait on other work.</summary>
public static class NaturalCarrierPolicy
{
	public static NaturalCarrierChoice Decide(NaturalCarrierObservation state, IReadOnlyList<NaturalAltgardTimedSpawn> carriers)
	{
		if (state.Rings.Count == 0) return new("done", null, null, 0, "Every ring is held.");
		int hour = NaturalGameClock.HourOf(state.GameMinutes);
		var reachable = carriers
			.Where(carrier => state.Rings.Contains(carrier.ItemId) && carrier.PresentAt(hour))
			.Select(carrier => (carrier, visible: state.VisibleCarrierNpcIds.Contains(carrier.NpcId),
				left: NaturalGameClock.MinutesLeftInWindow(state.GameMinutes, carrier)))
			.Where(entry => entry.visible || entry.left > state.TravelGameMinutes)
			.OrderByDescending(entry => entry.visible).ThenByDescending(entry => entry.left).ThenBy(entry => entry.carrier.NpcId)
			.ToArray();
		if (reachable.Length > 0)
		{
			var (carrier, visible, left) = reachable[0];
			return new("hunt", carrier, carrier.ItemId, 0, visible
				? $"{carrier.NpcId} is in view: hunt it for {carrier.ItemId}."
				: $"{carrier.NpcId} is present for {left} more game minutes, longer than the {state.TravelGameMinutes}-minute walk: hunt it for {carrier.ItemId}.");
		}
		// No needed ring can be had now: the soonest window that opens (and stays open long enough to arrive in).
		var next = carriers.Where(carrier => state.Rings.Contains(carrier.ItemId))
			.Select(carrier => (carrier, wait: NaturalGameClock.MinutesUntilHour(state.GameMinutes, carrier.SpawnHour)))
			.Select(entry => entry with { wait = entry.wait == 0 ? NaturalGameClock.MinutesPerDay : entry.wait })
			.OrderBy(entry => entry.wait).ThenBy(entry => entry.carrier.NpcId).First();
		return new("wait", next.carrier, next.carrier.ItemId, next.wait,
			$"No carrier of a needed ring can be reached in its window at {hour:00}:{state.GameMinutes % 60:00}; {next.carrier.NpcId} " +
			$"appears in {next.wait} game minutes ({next.carrier.SpawnHour:00}:00): do other work meanwhile.");
	}
}
