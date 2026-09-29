using Aion.Bots.Scenarios;

namespace Aion.GameServer.Tests;

/// <summary>NA-22 (OD-14): a Cleric blocked by a patrol waits, then fights, reroutes or pulls anyway.</summary>
public sealed class NaturalPatrolPolicyTests
{
	private static NaturalPatrolObservation Ready(int waits = NaturalPatrolPolicy.MaximumWaits, params int[] members) =>
		new(true, waits, 10, 1300, 1300, 1300, 1300, members.Length == 0 ? [10] : members, true, true, true, true, 10, false);

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	public void TheClericHoldsFifteenGameSecondsPerWait(int completed)
	{
		NaturalPatrolDecision decision = NaturalPatrolPolicy.Decide(Ready(completed));
		Assert.Equal(("wait", 15_000), (decision.Action, decision.WaitMillis));
		// Waiting comes first even when the fight looks winnable: the patrol usually walks on.
		Assert.True(decision.Winnable);
	}

	[Fact]
	public void AfterFourWaitsItEscalatesFightThenRerouteThenPullAnyway()
	{
		Assert.Equal(4, NaturalPatrolPolicy.MaximumWaits);
		Assert.Equal("fight", NaturalPatrolPolicy.Decide(Ready()).Action);
		Assert.Equal("fight", NaturalPatrolPolicy.Decide(Ready() with { RerouteAvailable = true }).Action);
		NaturalPatrolObservation hurt = Ready() with { Hp = 500 };
		Assert.Equal("reroute", NaturalPatrolPolicy.Decide(hurt with { RerouteAvailable = true }).Action);
		Assert.Equal("pull-anyway", NaturalPatrolPolicy.Decide(hurt).Action);
		Assert.All(new[] { 5, 9 }, waits => Assert.NotEqual("wait", NaturalPatrolPolicy.Decide(Ready(waits)).Action));
	}

	[Fact]
	public void TheAssessmentWeighsMembersLevelsResourcesHealsBuffsAndPotions()
	{
		Assert.True(NaturalPatrolPolicy.Assess(Ready()).Winnable);
		Assert.True(NaturalPatrolPolicy.Assess(Ready(4, 10, 11)).Winnable);
		Assert.True(NaturalPatrolPolicy.Assess(Ready(4, 12)).Winnable);
		Assert.False(NaturalPatrolPolicy.Assess(Ready(4, 10, 10, 10)).Winnable); // three: the swarm retreat would fire
		Assert.False(NaturalPatrolPolicy.Assess(Ready(4, 13)).Winnable);
		Assert.False(NaturalPatrolPolicy.Assess(Ready() with { Hp = 900 }).Winnable);
		Assert.False(NaturalPatrolPolicy.Assess(Ready() with { Mp = 600 }).Winnable);
		Assert.False(NaturalPatrolPolicy.Assess(Ready() with { HealReady = false }).Winnable);
		NaturalPatrolObservation pair = Ready(4, 10, 10);
		Assert.True(NaturalPatrolPolicy.Assess(pair with { HotReady = false }).Winnable); // Salvation covers it
		Assert.False(NaturalPatrolPolicy.Assess(pair with { HotReady = false, SalvationReady = false }).Winnable);
		Assert.True(NaturalPatrolPolicy.Assess(pair with { BuffsUp = false, PotionStock = 3 }).Winnable);
		Assert.False(NaturalPatrolPolicy.Assess(pair with { BuffsUp = false, PotionStock = 2 }).Winnable);
		// A single monster needs neither the heal over time nor buffs.
		Assert.True(NaturalPatrolPolicy.Assess(Ready() with { HotReady = false, SalvationReady = false, BuffsUp = false, PotionStock = 0 }).Winnable);
		Assert.Contains("HP is below 70%.", NaturalPatrolPolicy.Assess(Ready() with { Hp = 900 }).Assessment);
	}

	[Fact]
	public void ThePriestKeepsItsBaselineWait()
	{
		foreach (int waits in new[] { 0, 3, 4, 9 })
		{
			NaturalPatrolDecision decision = NaturalPatrolPolicy.Decide(Ready(waits) with { Cleric = false, Hp = 100 });
			Assert.Equal(("baseline", 0), (decision.Action, decision.WaitMillis));
		}
	}
}
