using Aion.Bots.Scenarios;
using Aion.Bots.World;

namespace Aion.GameServer.Tests;

/// <summary>AC-03: the escort policy against Java's follow rules and the Leg 3 contract's Q2290 escort.</summary>
public sealed class NaturalEscortPolicyTests
{
	private static readonly NaturalAltgardEscort Escort = NaturalAltgardContract.LoadLeg("l3").EscortList.Single();
	private static readonly BotPosition Groken = new(1224, 1096, 248, 0);
	private static readonly BotPosition Stand = NaturalEscortPolicy.GoalStand(Escort.Goal, Groken);
	// A straight line from Groken to the stand in 2 m waypoints, like the navmesh route AC-02 walked.
	private static readonly BotPosition[] Route = Enumerable.Range(0, 56)
		.Select(index => Lerp(Groken, Stand, index / 55f)).ToArray();

	[Fact]
	public void TheBandsLeaveAWideMarginUnderJavasLeashAndInsideTheGoal()
	{
		Assert.True(NaturalEscortPolicy.AdvanceGap + NaturalEscortPolicy.Hop <= NaturalEscortPolicy.CloseGap);
		Assert.True(NaturalEscortPolicy.CloseGap * 2 <= Escort.Leash);
		// A follower still trailing by the whole advance gap at the stand is inside the goal radius.
		Assert.True(NaturalEscortPolicy.StandFromGoal + NaturalEscortPolicy.AdvanceGap < Escort.GoalRadius);
		Assert.Equal(NaturalEscortPolicy.StandFromGoal, Distance(Stand, new BotPosition(Escort.Goal[0], Escort.Goal[1], Escort.Goal[2], 0)), 2);
		Assert.True(NaturalEscortPolicy.Hop < Escort.GoalRadius);
	}

	[Fact]
	public void JavasCheckIsThreeDimensionalStrictAndLosesBeforeItArrives()
	{
		BotPosition player = new(0, 0, 0, 0);
		float[] far = [1000, 1000, 0];
		Assert.False(Check(player, new(49.9f, 0, 0, 0), far).Lost);
		Assert.True(Check(player, new(50, 0, 0, 0), far).Lost);
		// 3-D: 40 m across and 30 m up is 50 m apart.
		Assert.True(Check(player, new(40, 0, 30, 0), far).Lost);
		Assert.True(NaturalEscortPolicy.JavaCheck(player, true, new(1, 0, 0, 0), false, far, 20, 50).Lost);
		Assert.True(NaturalEscortPolicy.JavaCheck(player, false, new(1, 0, 0, 0), true, far, 20, 50).Lost);
		Assert.True(Check(player, new(1, 0, 0, 0), [20.9f, 0, 0]).Reached);
		Assert.False(Check(player, new(1, 0, 0, 0), [21, 0, 0]).Reached);
		// Lost and arrived in one tick: the loss wins (the lost var makes the reach event a no-op).
		Assert.Equal((true, false), Check(player, new(60, 0, 0, 0), [60, 0, 0]));
	}

	[Fact]
	public void WhileFollowingTheBotHopsWaitsClosesTheGapAndFightsInPlace()
	{
		NaturalEscortObservation near = Following(Route[10], Route[9]);
		NaturalEscortChoice advance = NaturalEscortPolicy.Decide(near, Escort);
		Assert.Equal("advance", advance.Action);
		Assert.InRange(NaturalEscortPolicy.Distance2(near.Player, advance.MoveTo!.Value), 8, NaturalEscortPolicy.Hop);
		Assert.Equal("wait-for-follower", NaturalEscortPolicy.Decide(Following(Route[20], Route[15]), Escort).Action);
		NaturalEscortChoice back = NaturalEscortPolicy.Decide(Following(Route[40], Route[20]), Escort);
		Assert.Equal(("close-gap", Route[20]), (back.Action, back.MoveTo!.Value));
		Assert.Equal("wait-for-follower", NaturalEscortPolicy.Decide(near with { Follower = null }, Escort).Action);
		Assert.Equal("hold-and-fight", NaturalEscortPolicy.Decide(near with { Attackers = 2 }, Escort).Action);
		Assert.Equal("retreat", NaturalEscortPolicy.Decide(near with { Attackers = 2, DeathPredicted = true }, Escort).Action);
		Assert.Equal("wait-at-goal", NaturalEscortPolicy.Decide(Following(Route[^1], Route[^3]), Escort).Action);
		// Walking the route hop by hop with the follower 2 m behind reaches the stand in hops of at most 10 m.
		BotPosition at = Route[0];
		int hops = 0;
		while (NaturalEscortPolicy.Decide(Following(at, at), Escort) is { Action: "advance" } choice)
		{
			Assert.InRange(NaturalEscortPolicy.Distance2(at, choice.MoveTo!.Value), 0.1f, NaturalEscortPolicy.Hop);
			at = choice.MoveTo!.Value;
			Assert.True(++hops < 30);
		}
		Assert.True(NaturalEscortPolicy.Distance2(at, Stand) <= NaturalEscortPolicy.StandArrival);
		// 111 m of 2.02 m waypoints: four per hop (8.1 m), so 14 hops.
		Assert.InRange(hops, 12, 15);
	}

	[Fact]
	public void NothingThatLosesTheFollowerIsAllowedWhileHeFollows()
	{
		NaturalEscortObservation following = Following(Route[10], Route[9]);
		foreach (string action in NaturalEscortPolicy.ForbiddenWhileFollowing)
		{
			Assert.False(NaturalEscortPolicy.Allowed(action, following, Escort), action);
			Assert.True(NaturalEscortPolicy.Allowed(action, following with { QuestVar = Escort.LostVar }, Escort), action);
		}
		Assert.Contains("logout", NaturalEscortPolicy.ForbiddenWhileFollowing);
		Assert.True(NaturalEscortPolicy.Allowed("fight", following, Escort));
	}

	[Fact]
	public void TheStartClearsFirstFitsTheRespawnWindowAndUsesTheRightStep()
	{
		NaturalEscortObservation offer = Start(Groken with { X = Groken.X + 2 }, status: null);
		NaturalEscortChoice take = NaturalEscortPolicy.Decide(offer, Escort);
		Assert.Equal(("start", Escort.StartStep), (take.Action, take.StepKey));
		NaturalEscortChoice restart = NaturalEscortPolicy.Decide(offer with { QuestStatus = "START", Attempts = 1 }, Escort);
		Assert.Equal(("start", Escort.RestartStep), (restart.Action, restart.StepKey));
		Assert.Equal("approach-follower", NaturalEscortPolicy.Decide(offer with { Player = Route[20] }, Escort).Action);
		Assert.Equal("clear", NaturalEscortPolicy.Decide(offer with { ClearAreasHaveAggressors = true }, Escort).Action);
		Assert.Equal("hold-and-fight", NaturalEscortPolicy.Decide(offer with { Attackers = 1 }, Escort).Action);
		// 111 m at 6 m/s plus the 30 s margin is about 49 s: a robber back in 40 s means wait for it and clear again.
		NaturalEscortChoice early = NaturalEscortPolicy.Decide(offer with { EarliestRespawnMillis = offer.NowMillis + 40_000 }, Escort);
		Assert.Equal(("clear", offer.NowMillis + 40_000), (early.Action, early.WaitUntilMillis));
		Assert.Equal("start", NaturalEscortPolicy.Decide(offer with { EarliestRespawnMillis = offer.NowMillis + 120_000 }, Escort).Action);
		// After a loss the follower is deleted and respawns 295 s later.
		NaturalEscortChoice gone = NaturalEscortPolicy.Decide(offer with
		{
			QuestStatus = "START", Follower = null, Attempts = 1, FollowerGoneAtMillis = offer.NowMillis - 5_000,
		}, Escort);
		Assert.Equal(("wait-for-respawn", offer.NowMillis + 290_000), (gone.Action, gone.WaitUntilMillis));
		// AG-07: from afar the follower is merely out of view: walk to where he starts before waiting for him.
		NaturalEscortChoice far = NaturalEscortPolicy.Decide(offer with { Follower = null, Player = Route[^1] }, Escort);
		Assert.Equal(("approach-follower", Route[0]), (far.Action, far.MoveTo!.Value));
		Assert.Equal("give-up", NaturalEscortPolicy.Decide(offer with { QuestStatus = "START", Attempts = Escort.MaxAttempts }, Escort).Action);
	}

	[Fact]
	public void TheEscortEndsOnTheSuccessVarAndBlocksOnAnythingElse()
	{
		NaturalEscortObservation following = Following(Route[^1], Route[^2]);
		Assert.Equal("done", NaturalEscortPolicy.Decide(following with { QuestVar = Escort.SuccessVar, MovieSeen = true }, Escort).Action);
		Assert.Equal("done", NaturalEscortPolicy.Decide(following with { QuestStatus = "REWARD" }, Escort).Action);
		Assert.Equal("done", NaturalEscortPolicy.Decide(following with { QuestStatus = "COMPLETE" }, Escort).Action);
		Assert.Equal("revive", NaturalEscortPolicy.Decide(following with { Dead = true }, Escort).Action);
		Assert.Equal("blocked", NaturalEscortPolicy.Decide(following with { QuestVar = 2 }, Escort).Action);
	}

	[Fact]
	public void AnEscortWhoseSuccessKeepsTheFollowVarEndsOnlyOnReward()
	{
		// AG-03: Q2284's follow is var 2 (SETPRO3 at var 1), and reaching Babarunerk sets REWARD at var 2
		// (DefaultFollowEndEvent(env, 2, 2, true)). At START, var 2 is following, not done.
		NaturalAltgardEscort germir = NaturalAltgardContract.LoadLeg("l6").EscortList.Single();
		Assert.Equal((2, 2, 1, 1), (germir.FollowVar, germir.SuccessVar, germir.LostVar, germir.StartVar!.Value));
		var following = new NaturalEscortObservation(Route[10], false, Route[9], "START", germir.FollowVar, false, 1, 1_000_000, 0, false,
			false, null, null, Route, 6);
		Assert.NotEqual("done", NaturalEscortPolicy.Decide(following, germir).Action);
		Assert.Equal("done", NaturalEscortPolicy.Decide(following with { QuestStatus = "REWARD" }, germir).Action);
		// At var 1 (lost, or the first time) the restart step is the second disguised Germir's SETPRO3.
		var start = following with { QuestVar = germir.LostVar, Follower = Route[0], Player = Route[0], Attempts = 0 };
		NaturalEscortChoice begin = NaturalEscortPolicy.Decide(start, germir);
		Assert.Equal(("start", germir.RestartStep), (begin.Action, begin.StepKey));
	}

	private static (bool Lost, bool Reached) Check(BotPosition player, BotPosition follower, float[] goal) =>
		NaturalEscortPolicy.JavaCheck(player, false, follower, false, goal, Escort.GoalRadius, Escort.Leash);

	private static NaturalEscortObservation Following(BotPosition player, BotPosition follower) =>
		new(player, false, follower, "START", Escort.FollowVar, false, 1, 1_000_000, 0, false, false, null, null, Route, 6);

	private static NaturalEscortObservation Start(BotPosition player, string? status) =>
		new(player, false, Groken, status, Escort.LostVar, false, 0, 1_000_000, 0, false, false, null, null, Route, 6);

	private static BotPosition Lerp(BotPosition a, BotPosition b, float t) =>
		new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t, 0);

	private static float Distance(BotPosition a, BotPosition b) => NaturalEscortPolicy.Distance2(a, b);
}
