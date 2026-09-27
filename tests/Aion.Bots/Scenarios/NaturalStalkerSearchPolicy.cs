using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>Search hints for Q2005; actual targets must still be seen by the client.</summary>
public static class NaturalStalkerSearchPolicy
{
	/// <summary>A failed pull is tied to one firing side and patrol moment. Retry after moving
	/// to another approach or after the observed pack has had time to change.</summary>
	public static bool ShouldRetryRejected(BotPosition current, BotPosition rejectedAt, long elapsedMillis) =>
		Distance(current, rejectedAt) > 20 || elapsedMillis > 30_000;

	public static BotPosition[] SelectAreas(IEnumerable<BotPosition> shippedSpots, BotPosition origin,
		float minimumSeparation = 45f)
	{
		var areas = new List<BotPosition>();
		foreach (BotPosition spot in shippedSpots.OrderBy(spot => Distance(origin, spot)))
		{
			if (areas.All(area => Distance(area, spot) >= minimumSeparation))
				areas.Add(spot);
		}
		return areas.ToArray();
	}

	private static float Distance(BotPosition a, BotPosition b) => MathF.Sqrt(
		(a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) +
		(a.Z - b.Z) * (a.Z - b.Z));
}
