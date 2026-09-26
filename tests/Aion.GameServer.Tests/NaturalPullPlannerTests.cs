using Aion.Bots.Navigation;
using Aion.Bots.World;

namespace Aion.GameServer.Tests;

public sealed class NaturalPullPlannerTests
{
	private static BotPosition P(float x, float y) => new(x, y, 10, 0);

	private static NaturalPullMonster M(int id, float x, float y, string tribe = "STALKER", float aggro = 8) =>
		new(new NaturalNavigationObject(id, 210750, P(x, y)), aggro, tribe);

	// Stalkers help Stalkers; nothing else helps anything.
	private static bool Support(string helper, string asking) => helper == "STALKER" && asking == "STALKER";
	private static bool Sight(BotPosition a, BotPosition b) => true;
	private static BotPosition? Ground(BotPosition p) => p;
	private static bool Reachable(BotPosition p) => true;

	[Fact]
	public void PrefersTheLoneMonsterOverTheFirstOneInAPair()
	{
		// The route meets a pair first (10 m apart: each within the other's 8 + 2 m assist range),
		// then a lone Stalker further along.
		NaturalPullMonster pairA = M(1, 40, 0), pairB = M(2, 48, 6), lone = M(3, 90, 0);
		NaturalPullMonster[] all = [pairA, pairB, lone];
		NaturalPullPlan plan = NaturalPullPlanner.Plan(P(0, 0), [pairA, pairB, lone], all, [], Support, Sight, Ground, Reachable)!;
		Assert.Same(lone, plan.Target);
		Assert.Empty(plan.Helpers);
		Assert.Single(NaturalPullPlanner.Helpers(pairA, P(20, -10), all, Support));
	}

	[Fact]
	public void FiresFromTheSideAwayFromOtherMonstersAndOutsideEveryCircle()
	{
		// A lone target with a different-tribe monster 20 m to its north (+y): no assist, but the spot
		// must not stand in that monster's circle, and should keep the most clearance from it.
		NaturalPullMonster target = M(1, 50, 0), other = M(2, 50, 20, tribe: "LYCAN");
		NaturalPullPlan plan = NaturalPullPlanner.Plan(P(0, 0), [target], [target, other], [], Support, Sight, Ground, Reachable)!;
		Assert.Same(target, plan.Target);
		Assert.Empty(plan.Helpers);
		float toTarget = Distance(plan.FiringPosition, target.Npc.Position);
		Assert.InRange(toTarget, target.AggroRadius + 1, NaturalPullPlanner.SpellRange);
		Assert.True(Distance(plan.FiringPosition, other.Npc.Position) > other.AggroRadius + 1);
		Assert.True(plan.FiringPosition.Y < 0, $"expected the far (south) side, got {plan.FiringPosition}");
	}

	[Fact]
	public void AFiringSpotInAHelpersAssistRangeCountsAsAChainPull()
	{
		NaturalPullMonster target = M(1, 50, 0), friend = M(2, 50, 25);
		// Spot 12 m north of the target is 13 m from the friend: inside its 8 + 2 m? No (13 > 10). Spot
		// 16 m north is 9 m from the friend: inside, so firing there chains the friend.
		Assert.Empty(NaturalPullPlanner.Helpers(target, P(50, 12), [target, friend], Support));
		Assert.Single(NaturalPullPlanner.Helpers(target, P(50, 16), [target, friend], Support));
		// The planner therefore picks a spot that does not chain.
		NaturalPullPlan plan = NaturalPullPlanner.Plan(P(0, 0), [target], [target, friend], [], Support, Sight, Ground, Reachable)!;
		Assert.Empty(plan.Helpers);
	}

	[Fact]
	public void UnreachableOrHiddenSpotsAreNeverChosen()
	{
		NaturalPullMonster target = M(1, 50, 0);
		Assert.Null(NaturalPullPlanner.Plan(P(0, 0), [target], [target], [], Support, Sight, Ground, _ => false));
		Assert.Null(NaturalPullPlanner.Plan(P(0, 0), [target], [target], [], Support, (_, _) => false, Ground, Reachable));
		NaturalPullPlan onlyWest = NaturalPullPlanner.Plan(P(0, 0), [target], [target], [], Support, Sight, Ground,
			spot => spot.X < 40)!;
		Assert.True(onlyWest.FiringPosition.X < 40);
	}

	[Fact]
	public void EscapeRunsAwayFromThePackAndItsHomesNeverBackThroughIt()
	{
		BotPosition current = P(100, 100);
		BotPosition[] attackers = [P(95, 100), P(96, 104)]; // chasing from the west
		BotPosition[] homes = [P(60, 100), P(62, 110)];
		BotPosition[] candidates = [P(40, 100), P(70, 90), P(160, 100), P(200, 110), P(100, 180), P(130, 40)];
		BotPosition[] escapes = NaturalCombatRetreatPolicy.SelectEscape(current, attackers, homes, candidates, []);
		Assert.NotEmpty(escapes);
		Assert.DoesNotContain(P(40, 100), escapes);   // straight back past the pack
		Assert.DoesNotContain(P(70, 90), escapes);    // toward the homes
		Assert.Equal(P(200, 110), escapes[0]);        // furthest from home, directly away
		// Another monster's circle disqualifies an otherwise good escape.
		BotPosition[] blocked = NaturalCombatRetreatPolicy.SelectEscape(current, attackers, homes, candidates,
			[new BotNavigationHazard(P(200, 110), 8)]);
		Assert.DoesNotContain(P(200, 110), blocked);
	}

	[Fact]
	public void APatrolCountsAlongTheWholePathItWasSeenWalking()
	{
		// A lone target, approached from the south; an unrelated patrol is 40 m north of it now but was seen
		// walking an east-west line 15 m south of it. Seen only where it stands, the near (south) side looks
		// best; the patrol walks through there.
		BotPosition[] beat = Enumerable.Range(0, 21).Select(i => P(20 + i * 3, -15)).ToArray();
		NaturalPullMonster target = M(1, 50, 0);
		var patrol = new NaturalPullMonster(new NaturalNavigationObject(2, 210407, P(50, 40), beat), 8, "LYCAN");
		var seenOnce = patrol with { Npc = patrol.Npc with { PatrolPath = null } };
		NaturalPullPlan blind = NaturalPullPlanner.Plan(P(50, -60), [target], [target, seenOnce], [], Support, Sight, Ground, Reachable)!;
		Assert.Contains(beat, point => Distance(blind.FiringPosition, point) <= patrol.AggroRadius + 1);
		NaturalPullPlan plan = NaturalPullPlanner.Plan(P(50, -60), [target], [target, patrol], [], Support, Sight, Ground, Reachable)!;
		Assert.All(beat, point => Assert.True(Distance(plan.FiringPosition, point) > patrol.AggroRadius + 1));
		// A fight on its path meets it sooner or later; one well away from it does not.
		Assert.Contains(patrol, NaturalPullPlanner.AddsAt(target, P(50, -12), [target, patrol], Support, Sight));
		Assert.DoesNotContain(patrol, NaturalPullPlanner.AddsAt(target, P(50, 12), [target, patrol], Support, Sight));
	}

	private static float Distance(BotPosition a, BotPosition b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

	[Fact]
	public void AddsAreTheSupportersAndTheCirclesThatReachTheFight_NothingElse()
	{
		// Hatata at the origin; the bot fights from 12 m out. A Stalker 6 m behind him supports him (8 + 2 m);
		// an unrelated-tribe monster 14 m to the side does not help and its circle misses the spot; a second
		// unrelated monster stands 9 m from the firing spot: its 8 m circle reaches the 3 m melee band.
		NaturalPullMonster hatata = M(1, 0, 0), supporter = M(2, -6, 0), bystander = M(3, 0, -14, tribe: "KARNIF"),
			nearSpot = M(4, 12, 9, tribe: "KARNIF"), far = M(5, 40, 40);
		BotPosition spot = P(12, 0);
		IReadOnlyList<NaturalPullMonster> adds = NaturalPullPlanner.AddsAt(hatata, spot, [hatata, supporter, bystander, nearSpot, far], Support, Sight);
		Assert.Equal([2, 4], adds.Select(add => add.Npc.ObjectId).Order());
	}
}
