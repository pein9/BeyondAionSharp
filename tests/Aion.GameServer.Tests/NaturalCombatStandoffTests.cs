using Aion.Bots.Navigation;
using Aion.Bots.World;

namespace Aion.GameServer.Tests;

public sealed class NaturalCombatStandoffTests
{
	[Fact]
	public void SelectsPointInsideSpellRangeAfterArrivalTolerance()
	{
		BotPosition target = At(30);
		BotPosition[] checkedRoute = [At(3), At(5), At(7), At(9), At(11)];
		BotPosition selected = Assert.IsType<BotPosition>(
			NaturalCombatStandoff.Select(checkedRoute, target));
		Assert.Equal(9, selected.X); // 21 m from target; stopping 3 m early stays within 25 m.
		Assert.True(30 - (selected.X - 3) < 25);
	}

	[Fact]
	public void PartialRouteOutsideSpellRangeCannotPretendToBeAStandoff()
	{
		Assert.Null(NaturalCombatStandoff.Select([At(3), At(5)], At(30)));
		Assert.Empty(NaturalCombatStandoff.NextSegment([At(3), At(5)], At(30)));
	}

	[Fact]
	public void CombatApproachStopsToReobserveBeforeTheStandoff()
	{
		BotPosition[] checkedRoute =
		[
			At(2), At(4), At(6), At(8), At(10), At(12), At(14), At(16), At(18), At(20),
		];
		IReadOnlyList<BotPosition> segment = NaturalCombatStandoff.NextSegment(
			checkedRoute, At(40), maximumPoints: 4);
		Assert.Equal([At(2), At(4), At(6), At(8)], segment);
		Assert.Equal(At(20), NaturalCombatStandoff.NextSegment(
			checkedRoute, At(40), maximumPoints: 20)[^1]);
	}

	[Fact]
	public void TheNextSegmentForwardsItsStandoffInputsAndDefaultsToTwentyFiveThreeAndOne()
	{
		BotPosition[] checkedRoute = [At(2), At(4), At(6), At(8), At(10), At(12), At(14), At(16), At(18), At(20)];
		// CP-18: the defaults are Select's, 25 - 3 - 1 = 21 m from the target at 40.
		IReadOnlyList<BotPosition> byDefault = NaturalCombatStandoff.NextSegment(checkedRoute, At(40), maximumPoints: 20);
		Assert.Equal(At(20), byDefault[^1]);
		Assert.Equal(byDefault, NaturalCombatStandoff.NextSegment(checkedRoute, At(40), maximumPoints: 20,
			spellRange: 25, arrivalTolerance: 3, safetyMargin: 1));
		// A longer reach stops earlier, at the first point inside 30 - 3 - 1 = 26 m.
		Assert.Equal(At(14), NaturalCombatStandoff.NextSegment(checkedRoute, At(40), maximumPoints: 20, spellRange: 30)[^1]);
		Assert.Equal(NaturalCombatStandoff.Select(checkedRoute, At(40), 30, 3, 1), At(14));
		// A weapon-range class with no tolerance has to come to 5 m; this route never gets there.
		Assert.Empty(NaturalCombatStandoff.NextSegment(checkedRoute, At(40), maximumPoints: 20, spellRange: 5, arrivalTolerance: 0, safetyMargin: 0));
		Assert.Equal(At(16), NaturalCombatStandoff.NextSegment(checkedRoute, At(21), maximumPoints: 20,
			spellRange: 5, arrivalTolerance: 0, safetyMargin: 0)[^1]);
	}

	private static BotPosition At(float x) => new(x, 0, 10, 0);
}
