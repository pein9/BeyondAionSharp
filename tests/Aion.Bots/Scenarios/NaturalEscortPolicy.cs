using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>
/// What the client sees of an escort (AC-03). <paramref name="Follower"/> is the follower's last observed position, null
/// once it is gone (the server deletes it at either end and respawns it later). <paramref name="QuestStatus"/> is null
/// while the quest is not taken. <paramref name="EarliestRespawnMillis"/> is when the first monster killed while clearing
/// comes back, if any was. <paramref name="FollowerGoneAtMillis"/> is when the follower was seen to disappear.
/// </summary>
public sealed record NaturalEscortObservation(
	BotPosition Player, bool Dead, BotPosition? Follower, string? QuestStatus, int QuestVar, bool MovieSeen,
	int Attempts, long NowMillis, int Attackers, bool DeathPredicted, bool ClearAreasHaveAggressors,
	long? EarliestRespawnMillis, long? FollowerGoneAtMillis, IReadOnlyList<BotPosition> Route, float PlayerSpeed);

/// <param name="Action">One of the <see cref="NaturalEscortPolicy"/> action names.</param>
/// <param name="MoveTo">Where to walk for advance, close-gap and approach-follower.</param>
/// <param name="StepKey">The contract step to talk through for start.</param>
/// <param name="WaitUntilMillis">For wait-for-respawn, and for clear when it has to wait for respawns first.</param>
public sealed record NaturalEscortChoice(string Action, BotPosition? MoveTo, string Reason, string? StepKey = null,
	long? WaitUntilMillis = null);

/// <summary>
/// AC-03 (docs/natural-altgard-leveling.md, "The escort handler"): the pure policy of an escort. It rests on Java's follow
/// rules (<c>FollowingNpcCheckTask</c>, every second: a death or 50 m apart loses the follower, then within 20 m of the goal
/// npc's first spawn it arrives; both measured in 3-D with a strict <c>&lt;</c>). The follower moves only when the player
/// moves and closes to 2 m (<c>FollowingNpcAI</c>), so the player walks in short hops and waits for him. Either end deletes
/// the follower (<c>FollowEventHandler.stopFollow</c>) and schedules its respawn; a logout while following loses him too.
/// </summary>
public static class NaturalEscortPolicy
{
	/// <summary>Advance only while the follower is this close.</summary>
	public const float AdvanceGap = 8;
	/// <summary>Beyond this gap, walk back toward the follower (Java loses him at the contract's leash, 50 m).</summary>
	public const float CloseGap = 25;
	/// <summary>The longest single move while following.</summary>
	public const float Hop = 10;
	/// <summary>The goal stand's distance from the goal, on the follower's side (AC-02: 10 m).</summary>
	public const float StandFromGoal = 10;
	/// <summary>Arrival at the stand: the player is done walking.</summary>
	public const float StandArrival = 1.5f;
	/// <summary>Headroom on the respawn window: clearing ends, then the walk to the follower and the escort must finish
	/// this long before the first cleared monster comes back.</summary>
	public const long RespawnMarginMillis = 30_000;

	/// <summary>Actions that reset or lose the escort while the follower follows (logout sets the lost var; the others
	/// break the 50 m leash or leave the map).</summary>
	public static readonly IReadOnlySet<string> ForbiddenWhileFollowing = new HashSet<string>
	{
		"logout", "relog", "return-scroll", "teleport", "fly", "glide", "rest-trip", "restock-trip",
	};

	public static bool Allowed(string action, NaturalEscortObservation state, NaturalAltgardEscort escort) =>
		!(Following(state, escort) && ForbiddenWhileFollowing.Contains(action));

	public static bool Following(NaturalEscortObservation state, NaturalAltgardEscort escort) =>
		state.QuestStatus == "START" && state.QuestVar == escort.FollowVar;

	public static NaturalEscortChoice Decide(NaturalEscortObservation state, NaturalAltgardEscort escort)
	{
		// AG-03: an escort whose success keeps the follow var (Q2284: DefaultFollowEndEvent(env, 2, 2, true)) is told by REWARD
		// alone; at START that var means following.
		if (state.QuestStatus is "REWARD" or "COMPLETE" ||
			state.QuestStatus == "START" && state.QuestVar == escort.SuccessVar && escort.SuccessVar != escort.FollowVar)
			return new("done", null, $"The follower reached the goal: var {escort.SuccessVar}{(state.MovieSeen ? " and the movie" : "")}.");
		if (state.Dead)
			return new("revive", null, "Dead: Java loses the follower on the player's death; revive and come back.");
		if (Following(state, escort))
			return DecideFollowing(state, escort);
		if (state.QuestStatus is not (null or "START") || state.QuestStatus == "START" && state.QuestVar != escort.LostVar)
			return new("blocked", null, $"Quest status {state.QuestStatus} var {state.QuestVar} is not an escort state.");
		return DecideStart(state, escort);
	}

	private static NaturalEscortChoice DecideFollowing(NaturalEscortObservation state, NaturalAltgardEscort escort)
	{
		if (state.Attackers > 0)
			return state.DeathPredicted
				? new("retreat", null, "Death is predicted: leave, which loses the follower; the loss is recorded and retried.")
				: new("hold-and-fight", null, "Attacked while following: fight in place (the follower cannot be hit and waits within 2 m).");
		if (state.Follower is not { } follower)
			return new("wait-for-follower", null, "The follower is not in view; wait for the next check to settle the escort.");
		float gap = Distance3(state.Player, follower);
		if (gap > CloseGap)
			return new("close-gap", follower, $"The follower is {gap:F1} m back (Java loses him at {escort.Leash} m): walk back toward him.");
		BotPosition stand = state.Route.Count > 0 ? state.Route[^1] : state.Player;
		if (Distance2(state.Player, stand) <= StandArrival)
			return new("wait-at-goal", null, $"At the goal stand; the follower is {gap:F1} m away and arrives within {escort.GoalRadius} m of the goal.");
		if (gap > AdvanceGap)
			return new("wait-for-follower", null, $"The follower is {gap:F1} m back; he moves only when the player moves, so wait for him to close.");
		return new("advance", NextHop(state.Player, state.Route), $"The follower is {gap:F1} m back: the next hop of at most {Hop} m.");
	}

	private static NaturalEscortChoice DecideStart(NaturalEscortObservation state, NaturalAltgardEscort escort)
	{
		if (state.Attempts >= escort.MaxAttempts)
			return new("give-up", null, $"{state.Attempts} escort attempts are used (AC-Q2); record the escort as not done.");
		if (state.Attackers > 0)
			return new("hold-and-fight", null, "Attacked before the start: fight first.");
		if (state.Follower is not { } follower)
		{
			long respawnAt = (state.FollowerGoneAtMillis ?? state.NowMillis) + escort.FollowerRespawnSeconds * 1000L;
			return new("wait-for-respawn", null, $"The follower is gone; the server respawns him {escort.FollowerRespawnSeconds} s after the last end.",
				WaitUntilMillis: Math.Max(respawnAt, state.NowMillis));
		}
		if (state.ClearAreasHaveAggressors)
			return new("clear", null, $"Aggressive monsters stand in {string.Join(", ", escort.ClearAreas)}: clear them before the start (AC-Q3).");
		float routeLength = RouteLength(state.Route);
		long needed = (long)((Distance2(state.Player, follower) + routeLength) / Math.Max(state.PlayerSpeed, 0.1f) * 1000) + RespawnMarginMillis;
		if (state.EarliestRespawnMillis is long back && state.NowMillis + needed > back)
			return new("clear", null, $"A cleared monster returns in {(back - state.NowMillis) / 1000} s, before the escort would end: wait and clear again.",
				WaitUntilMillis: back);
		if (Distance3(state.Player, follower) > 5)
			return new("approach-follower", follower, "Walk to the follower to start the escort.");
		string step = state.QuestStatus == null ? escort.StartStep : escort.RestartStep;
		return new("start", null, state.QuestStatus == null
			? "Take the quest; its last action makes the follower follow."
			: $"At var {escort.LostVar} a talk restarts the follow (attempt {state.Attempts + 1} of {escort.MaxAttempts}).", step);
	}

	/// <summary>The furthest route point at most <see cref="Hop"/> m ahead of the route point nearest the player.</summary>
	public static BotPosition NextHop(BotPosition player, IReadOnlyList<BotPosition> route)
	{
		if (route.Count == 0) return player;
		int nearest = 0;
		for (int index = 1; index < route.Count; index++)
			if (Distance2(player, route[index]) < Distance2(player, route[nearest])) nearest = index;
		int next = nearest;
		while (next + 1 < route.Count && Distance2(player, route[next + 1]) <= Hop) next++;
		return next == nearest && Distance2(player, route[nearest]) <= StandArrival && nearest + 1 < route.Count
			? route[nearest + 1] : route[next];
	}

	/// <summary>
	/// Java's <c>FollowingNpcCheckTask.run</c> for one tick, as the policy's tests pin it: the lost checks come first and
	/// both can fire in one tick; the reach check runs regardless, but by then the lost var makes the handler's
	/// <c>defaultFollowEndEvent</c> a no-op.
	/// </summary>
	public static (bool Lost, bool Reached) JavaCheck(BotPosition player, bool playerDead, BotPosition follower, bool followerDead,
		float[] goal, float goalRadius, float leash)
	{
		bool lost = playerDead || followerDead || !InRange(player.X, player.Y, player.Z, follower.X, follower.Y, follower.Z, leash);
		bool reached = InRange(follower.X, follower.Y, follower.Z, goal[0], goal[1], goal[2], goalRadius);
		return (lost, !lost && reached);
	}

	/// <summary>Java <c>PositionUtil.isInRange</c>: 3-D, strictly inside.</summary>
	public static bool InRange(float x1, float y1, float z1, float x2, float y2, float z2, float range)
	{
		float dx = x1 - x2, dy = y1 - y2, dz = z1 - z2;
		return dx * dx + dy * dy + dz * dz < range * range;
	}

	/// <summary>The goal stand: <see cref="StandFromGoal"/> m from the goal toward the follower's start.</summary>
	public static BotPosition GoalStand(float[] goal, BotPosition followerStart)
	{
		float dx = followerStart.X - goal[0], dy = followerStart.Y - goal[1];
		float length = MathF.Max(MathF.Sqrt(dx * dx + dy * dy), 0.001f);
		return new BotPosition(goal[0] + dx / length * StandFromGoal, goal[1] + dy / length * StandFromGoal, goal[2], 0);
	}

	public static float RouteLength(IReadOnlyList<BotPosition> route) =>
		route.Zip(route.Skip(1), Distance2).Sum();

	public static float Distance2(BotPosition a, BotPosition b) => MathF.Sqrt(MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2));

	public static float Distance3(BotPosition a, BotPosition b) =>
		MathF.Sqrt(MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2) + MathF.Pow(a.Z - b.Z, 2));
}
