using Aion.Bots.World;

namespace Aion.Bots.Navigation;

/// <summary>Choose a client-observed hostile that actually obstructs the checked approach corridor.
/// The caller still has to fight it with ordinary combat; this never removes an NPC or a hazard.</summary>
public static class NaturalGuardedObjectivePolicy
{
	/// <summary>Find an observed guard occupying the actual collision-checked
	/// ground route. A winding road can meet a pack that the straight objective
	/// line never crosses; no-route searches must not invent a guard.</summary>
	public static NaturalNavigationObject? SelectBlockerOnRoute(BotPosition start,
		IReadOnlyList<BotPosition> checkedRoute, IReadOnlyList<NaturalNavigationObject> observed,
		Func<int, float> aggroRadius, int? objectiveObjectId = null,
		IReadOnlySet<int>? rejected = null)
	{
		ArgumentNullException.ThrowIfNull(checkedRoute);
		ArgumentNullException.ThrowIfNull(observed);
		ArgumentNullException.ThrowIfNull(aggroRadius);
		if (checkedRoute.Count == 0) return null;
		var candidates = observed
			.Where(npc => npc.ObjectId != objectiveObjectId && rejected?.Contains(npc.ObjectId) != true)
			.Select(npc => (Npc: npc, Radius: aggroRadius(npc.TemplateId)))
			.Where(entry => entry.Radius > 0)
			.Select(entry => (entry.Npc, FirstSegment: FirstIntersectingSegment(
				start, checkedRoute, entry.Npc.Position, entry.Radius)))
			.Where(entry => entry.FirstSegment >= 0).ToArray();
		return candidates
			.OrderBy(entry => entry.FirstSegment)
			.ThenBy(entry => candidates.Count(other => other.Npc.ObjectId != entry.Npc.ObjectId &&
				HorizontalDistance(other.Npc.Position, entry.Npc.Position) < 9))
			.ThenBy(entry => entry.Npc.ObjectId)
			.Select(entry => entry.Npc).FirstOrDefault();
	}

	public static NaturalNavigationObject? SelectBlocker(BotPosition start, BotPosition objective,
		IReadOnlyList<NaturalNavigationObject> observed, Func<int, float> aggroRadius,
		int? objectiveObjectId = null, IReadOnlySet<int>? rejected = null,
		float corridorMargin = 0)
	{
		ArgumentNullException.ThrowIfNull(observed);
		ArgumentNullException.ThrowIfNull(aggroRadius);
		if (!float.IsFinite(corridorMargin) || corridorMargin < 0)
			throw new ArgumentOutOfRangeException(nameof(corridorMargin));
		var candidates = observed
			.Where(npc => npc.ObjectId != objectiveObjectId && rejected?.Contains(npc.ObjectId) != true)
			.Select(npc => (Npc: npc, Radius: aggroRadius(npc.TemplateId)))
			.Where(entry => entry.Radius > 0 &&
				DistanceToSegment(start, objective, entry.Npc.Position) < entry.Radius + corridorMargin)
			.ToArray();
		return candidates
			.OrderBy(entry => MathF.Max(0, HorizontalDistance(start, entry.Npc.Position) - entry.Radius))
			.ThenBy(entry => candidates.Count(other => other.Npc.ObjectId != entry.Npc.ObjectId &&
				HorizontalDistance(other.Npc.Position, entry.Npc.Position) < 9))
			.ThenBy(entry => entry.Npc.ObjectId)
			.Select(entry => entry.Npc)
			.FirstOrDefault();
	}

	private static int FirstIntersectingSegment(BotPosition start,
		IReadOnlyList<BotPosition> route, BotPosition hostile, float radius)
	{
		BotPosition previous = start;
		for (int index = 0; index < route.Count; index++)
		{
			if (DistanceToSegment(previous, route[index], hostile) < radius)
				return index;
			previous = route[index];
		}
		return -1;
	}

	private static float DistanceToSegment(BotPosition start, BotPosition end, BotPosition point)
	{
		float dx = end.X - start.X, dy = end.Y - start.Y;
		float lengthSquared = dx * dx + dy * dy;
		float t = lengthSquared <= 0 ? 0 : Math.Clamp(
			((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lengthSquared, 0, 1);
		float x = start.X + t * dx, y = start.Y + t * dy;
		return MathF.Sqrt((point.X - x) * (point.X - x) + (point.Y - y) * (point.Y - y));
	}

	private static float HorizontalDistance(BotPosition a, BotPosition b) =>
		MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
