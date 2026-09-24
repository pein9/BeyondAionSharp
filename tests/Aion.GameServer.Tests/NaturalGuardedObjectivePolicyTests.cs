using Aion.Bots.Navigation;
using Aion.Bots.World;

namespace Aion.GameServer.Tests;

public sealed class NaturalGuardedObjectivePolicyTests
{
	[Fact]
	public void PicksObservedHostileThatBlocksRouteNotAnUnrelatedNearbyNpc()
	{
		NaturalNavigationObject[] observed =
		[
			new(1, 700095, At(30, 0)),
			new(2, 210609, At(28, 2)),
			new(3, 210610, At(9, 2)),
			new(4, 210611, At(5, 15)),
		];
		float Radius(int id) => id switch { 210609 => 6, 210610 => 4, 210611 => 4, _ => 0 };
		NaturalNavigationObject? first = NaturalGuardedObjectivePolicy.SelectBlocker(
			At(0, 0), At(30, 0), observed, Radius, objectiveObjectId: 1);
		Assert.Equal(3, first?.ObjectId);
		NaturalNavigationObject? second = NaturalGuardedObjectivePolicy.SelectBlocker(
			At(0, 0), At(30, 0), observed, Radius, objectiveObjectId: 1,
			rejected: new HashSet<int> { 3 });
		Assert.Equal(2, second?.ObjectId);
	}

	[Fact]
	public void NoObservedCorridorBlockerMeansNoInventedFight()
	{
		NaturalNavigationObject[] observed = [new(4, 210611, At(5, 15))];
		Assert.Null(NaturalGuardedObjectivePolicy.SelectBlocker(
			At(0, 0), At(30, 0), observed, _ => 4));
	}

	[Fact]
	public void SideGuardIsConsideredOnlyOnTheBoundedSecondPass()
	{
		NaturalNavigationObject[] observed =
		[
			new(1, 700095, At(40, 0)),
			new(2, 210397, At(20, -16)),
		];
		float Radius(int id) => id == 210397 ? 8 : 0;
		Assert.Null(NaturalGuardedObjectivePolicy.SelectBlocker(
			At(0, 0), At(40, 0), observed, Radius, objectiveObjectId: 1));
		Assert.Equal(2, NaturalGuardedObjectivePolicy.SelectBlocker(
			At(0, 0), At(40, 0), observed, Radius,
			objectiveObjectId: 1, corridorMargin: 20)?.ObjectId);
	}

	[Fact]
	public void CurvingCheckedRouteSelectsGuardOutsideStraightCorridor()
	{
		BotPosition start = At(0, 0), objective = At(40, 0);
		BotPosition[] checkedRoute = [At(0, 20), At(20, 20), At(40, 20), objective];
		NaturalNavigationObject[] observed =
		[
			new(1, 203552, objective),
			new(2, 210609, At(18, 20)),
			new(3, 210610, At(50, 50)),
		];
		float Radius(int id) => id is 210609 or 210610 ? 6 : 0;
		Assert.Null(NaturalGuardedObjectivePolicy.SelectBlocker(
			start, objective, observed, Radius, objectiveObjectId: 1));
		Assert.Equal(2, NaturalGuardedObjectivePolicy.SelectBlockerOnRoute(
			start, checkedRoute, observed, Radius, objectiveObjectId: 1)?.ObjectId);
		Assert.Null(NaturalGuardedObjectivePolicy.SelectBlockerOnRoute(
			start, checkedRoute, observed, Radius, objectiveObjectId: 1,
			rejected: new HashSet<int> { 2 }));
	}

	[Fact]
	public void MauGrainSackWithinObservedHighsitterAggroSelectsTheGuard()
	{
		BotPosition start = At(819.976f, 1548.345f);
		BotPosition sack = At(742.801f, 1515.770f);
		NaturalNavigationObject[] observed =
		[
			new(10, 700095, sack),
			new(11, 210609, At(747.5f, 1515.8f)),
		];
		NaturalNavigationObject? guard = NaturalGuardedObjectivePolicy.SelectBlocker(
			start, sack, observed, id => id == 210609 ? 16 : 0, objectiveObjectId: 10);
		Assert.Equal(11, guard?.ObjectId);
	}

	[Fact]
	public void MauFarmReturnSelectsTheNearestRealAggroCircle()
	{
		NaturalNavigationObject[] observed =
		[
			new(14432, 210393, At(690.24f, 1504.16f)),
			new(14341, 210393, At(702.2f, 1519.15f)),
			new(14415, 210393, At(713.11f, 1524.38f)),
			new(14204, 210609, At(746.496f, 1511.299f)),
		];
		NaturalNavigationObject? blocker = NaturalGuardedObjectivePolicy.SelectBlocker(
			At(680.505f, 1504.05f), At(946.253f, 1702.775f), observed,
			id => id == 210609 ? 16 : 8);
		Assert.Equal(14432, blocker?.ObjectId);
	}

	private static BotPosition At(float x, float y) => new(x, y, 10, 0);
}
