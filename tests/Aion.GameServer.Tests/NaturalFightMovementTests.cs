using Aion.Bots.Scenarios;
using Aion.Bots.Scenarios.Classes;

namespace Aion.GameServer.Tests;

/// <summary>
/// CP-19: movement inside a fight. The Priest and Cleric answers are the fight loop's own inline rules as they stood at
/// commit d6a53822e; the two other pull styles are defined here and not played yet.
/// </summary>
public sealed class NaturalFightMovementTests
{
	private static readonly float[] Distances = [0f, 1.5f, 2.99f, 3f, 3.01f, 9.9f, 10f, 20f, 24.99f, 25f, 25.01f, 40f];

	[Fact]
	public void ThePriestLineAnswersAsTheFightLoopDid()
	{
		foreach (NaturalClassProfile profile in new[] { NaturalPriestProfile.Priest, NaturalPriestProfile.Cleric })
		{
			NaturalFightMovement movement = profile.Movement;
			Assert.Equal(new NaturalFightMovement(NaturalPullStyle.StandOff, MeleeReach: 3, RangedRouteBeyond: 25, RangeRefusalCloseIn: 10), movement);
			Assert.Equal(NaturalPullStyle.StandOff, profile.PullStyle);
			foreach (float distance in Distances)
			{
				// The approach: a ranged route beyond 25 m, then up to the target.
				Assert.Equal(distance > 25 ? NaturalApproachStep.RangedRoute : NaturalApproachStep.WalkToTarget, movement.Approach(distance));
				// Adjacent: hit by a melee target within the last 3 s, or inside melee reach.
				foreach (bool targetRanged in new[] { false, true })
				foreach (long? since in new long?[] { null, 0, 2999, 3000, 3001, 60_000 })
				{
					bool inline = !targetRanged && since is long lastHit && lastHit <= 3000 || distance <= NaturalPriestCombatPolicy.MeleeReach;
					Assert.Equal(inline, movement.Adjacent(distance, targetRanged, since));
				}
			}
			// After a distance refusal: inside melee reach for a melee skill, else 10 m.
			foreach (float range in new[] { 0f, 1f, 3f, 3.5f, 15f, 23f, 25f })
				Assert.Equal(range <= NaturalPriestCombatPolicy.MeleeReach ? NaturalPriestCombatPolicy.MeleeReach - 1 : 10f,
					movement.CloseInAfterRangeRefusal(range));
			Assert.Equal(2f, movement.CloseInAfterRangeRefusal(1));
			Assert.Equal(10f, movement.CloseInAfterRangeRefusal(25));
			// An obstacle: a Priest fights at melee anyway, so it closes in to melee reach less one.
			Assert.Equal(NaturalObstacleAnswer.CloseToMelee, movement.AfterObstacleRefusal);
			Assert.Equal(NaturalPriestCombatPolicy.MeleeReach - 1, movement.ObstacleCloseIn);
		}
	}

	[Fact]
	public void AWalkInStyleAlwaysWalksToTheTargetAndAWeaponRangeStyleHolds()
	{
		var walkIn = new NaturalFightMovement(NaturalPullStyle.WalkIn, MeleeReach: 3, RangedRouteBeyond: 25, RangeRefusalCloseIn: 10);
		Assert.All(Distances, distance => Assert.Equal(NaturalApproachStep.WalkToTarget, walkIn.Approach(distance)));
		Assert.Equal(NaturalObstacleAnswer.CloseToMelee, walkIn.AfterObstacleRefusal);

		var ranged = new NaturalFightMovement(NaturalPullStyle.WeaponRangeStandOff, MeleeReach: 3, RangedRouteBeyond: 25, RangeRefusalCloseIn: 10,
			HoldDistance: 18);
		Assert.Equal(NaturalApproachStep.RangedRoute, ranged.Approach(18.5f));
		Assert.Equal(NaturalApproachStep.Hold, ranged.Approach(18f));
		Assert.Equal(NaturalApproachStep.Hold, ranged.Approach(2f));
		// It keeps its distance: an obstacle is answered by other ground with sight, not by closing in.
		Assert.Equal(NaturalObstacleAnswer.AnotherSightLine, ranged.AfterObstacleRefusal);
		// Adjacency and the distance refusal are the same question for every style.
		Assert.True(ranged.Adjacent(2.5f, targetRanged: false, millisSinceHitByTarget: null));
		Assert.True(walkIn.Adjacent(30f, targetRanged: false, millisSinceHitByTarget: 1000));
		Assert.False(walkIn.Adjacent(30f, targetRanged: true, millisSinceHitByTarget: 1000));
		Assert.Equal(2f, walkIn.CloseInAfterRangeRefusal(3));
		// A longer weapon moves the melee answers with it.
		var polearm = walkIn with { MeleeReach = 5 };
		Assert.True(polearm.Adjacent(4.5f, false, null));
		Assert.Equal(4f, polearm.CloseInAfterRangeRefusal(5));
		Assert.Equal(4f, polearm.ObstacleCloseIn);
	}
}
