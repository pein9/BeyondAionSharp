using Aion.Bots.World;

namespace Aion.Bots.Navigation;

/// <summary>Choose a checked-route point that remains in spell range even when
/// the ground navigator accepts arrival a few metres short of that point.</summary>
public static class NaturalCombatStandoff
{
	/// <summary>Advance only a short checked prefix toward the first safe casting
	/// point, so incoming attacks can trigger a fresh heal/retreat decision.</summary>
	public static IReadOnlyList<BotPosition> NextSegment(IReadOnlyList<BotPosition> checkedRoute,
		BotPosition target, int maximumPoints = 6)
	{
		if (maximumPoints <= 0) throw new ArgumentOutOfRangeException(nameof(maximumPoints));
		BotPosition? standoff = Select(checkedRoute, target);
		if (standoff == null) return [];
		int last = 0;
		while (last < checkedRoute.Count && checkedRoute[last] != standoff.Value) last++;
		return checkedRoute.Take(Math.Min(maximumPoints, last + 1)).ToArray();
	}

	public static BotPosition? Select(IReadOnlyList<BotPosition> checkedRoute,
		BotPosition target, float spellRange = 25, float arrivalTolerance = 3, float safetyMargin = 1)
	{
		ArgumentNullException.ThrowIfNull(checkedRoute);
		if (!float.IsFinite(spellRange) || !float.IsFinite(arrivalTolerance) ||
			!float.IsFinite(safetyMargin) || spellRange <= arrivalTolerance + safetyMargin ||
			arrivalTolerance < 0 || safetyMargin < 0)
			throw new ArgumentOutOfRangeException(nameof(spellRange));
		float maximumDistance = spellRange - arrivalTolerance - safetyMargin;
		foreach (BotPosition point in checkedRoute)
			if (Distance(point, target) <= maximumDistance)
				return point;
		return null;
	}

	private static float Distance(BotPosition a, BotPosition b) => MathF.Sqrt(
		(a.X - b.X) * (a.X - b.X) +
		(a.Y - b.Y) * (a.Y - b.Y) +
		(a.Z - b.Z) * (a.Z - b.Z));
}
