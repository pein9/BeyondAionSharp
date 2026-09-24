using Aion.Bots.World;

namespace Aion.Bots.Navigation;

/// <summary>An observed monster together with the aggro radius the bot must respect.</summary>
public sealed record NaturalObservedMonster(NaturalNavigationObject Npc, float Radius);

/// <summary>The next monster to fight on a fight-through route, and the hostile-free prefix that brings the
/// bot to a firing position just outside its aggro circle.</summary>
public sealed record NaturalFightThroughBlocker(NaturalObservedMonster Monster, int EntryIndex,
	IReadOnlyList<BotPosition> Staging, BotPosition FiringPosition);

/// <summary>
/// "Fight your way in": when observed monsters close every hostile-free route, the bot takes the route that
/// fights the least (circles as costs, not walls) and clears it one monster at a time, in route order. The
/// first circle the route enters names the monster to pull; the route's samples before that entry are outside
/// every other circle, so walking them is as safe as any ordinary checked route, and their end is within
/// aggro radius plus one step of the monster, i.e. inside spell range. After each kill the caller observes
/// again and re-plans; nothing here removes an NPC or relaxes a collision check.
/// </summary>
public static class NaturalFightThrough
{
	/// <summary>Priest spell range used by the journey's combat policy, minus a margin for movement.</summary>
	public const float FiringRange = 23f;
	private const float VerticalBand = 8f;

	public static NaturalFightThroughBlocker? SelectNext(BotPosition start, IReadOnlyList<BotPosition> route,
		IReadOnlyList<NaturalObservedMonster> monsters, IReadOnlySet<int>? rejected = null)
	{
		ArgumentNullException.ThrowIfNull(route);
		ArgumentNullException.ThrowIfNull(monsters);
		// A circle the bot already stands in is an engagement, not a blocker on the way.
		NaturalObservedMonster[] ahead = monsters
			.Where(m => m.Radius > 0 && rejected?.Contains(m.Npc.ObjectId) != true && !Inside(start, m))
			.ToArray();
		for (int index = 0; index < route.Count; index++)
		{
			NaturalObservedMonster? entered = ahead.Where(m => Inside(route[index], m))
				.OrderBy(m => Horizontal(route[index], m.Npc.Position)).ThenBy(m => m.Npc.ObjectId).FirstOrDefault();
			if (entered == null) continue;
			IReadOnlyList<BotPosition> staging = route.Take(index).ToArray();
			BotPosition firing = staging.Count == 0 ? start : staging[^1];
			// A circle wider than spell range cannot be pulled from its edge; report no safe pull.
			if (Horizontal(firing, entered.Npc.Position) > FiringRange) return null;
			return new NaturalFightThroughBlocker(entered, index, staging, firing);
		}
		return null;
	}

	/// <summary>Monsters whose circles the route enters (outside the ones already engaged), in route order.</summary>
	public static IReadOnlyList<NaturalObservedMonster> BlockersInOrder(BotPosition start, IReadOnlyList<BotPosition> route,
		IReadOnlyList<NaturalObservedMonster> monsters)
	{
		var order = new List<NaturalObservedMonster>();
		foreach (BotPosition point in route)
			foreach (NaturalObservedMonster monster in monsters)
				if (!Inside(start, monster) && Inside(point, monster) && !order.Contains(monster)) order.Add(monster);
		return order;
	}

	private static bool Inside(BotPosition point, NaturalObservedMonster monster) =>
		Horizontal(point, monster.Npc.Position) < monster.Radius && MathF.Abs(point.Z - monster.Npc.Position.Z) < VerticalBand;

	private static float Horizontal(BotPosition a, BotPosition b) =>
		MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
