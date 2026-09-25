using Aion.Bots.World;

namespace Aion.Bots.Navigation;

/// <summary>An observed monster as the pull planner sees it: where it is, how far it aggroes, and its
/// tribe, which decides who comes to help it.</summary>
public sealed record NaturalPullMonster(NaturalNavigationObject Npc, float AggroRadius, string Tribe);

/// <summary>A chosen pull: fire at <see cref="Target"/> from <see cref="FiringPosition"/>. <see cref="Helpers"/>
/// lists the monsters expected to join (the fewest the planner could find; empty is a clean single pull).</summary>
public sealed record NaturalPullPlan(NaturalPullMonster Target, BotPosition FiringPosition,
	IReadOnlyList<NaturalPullMonster> Helpers, float ClearanceFromOthers);

/// <summary>
/// Pull one monster at a time, the way a player does: from a spot off to the side of the path, at spell
/// range, where neither the target's friends nor anything else will join in.
///
/// Java rule (<c>AggroEventHandler.onCreatureNeedsSupport</c>): when a monster is hit or aggroes, every
/// NPC whose tribe can support it (<c>TribeRelationsData.canSupport</c>) and that is not already fighting
/// helps if it is within its own aggro range + 2 m (<c>SUPPORT_RANGE_OFFSET</c>) of the monster or of the
/// attacker and can see it. So a pull costs nothing extra when no supporter is within that range of the
/// target, and none is within that range of the firing spot. The firing spot must also be outside every
/// monster's own aggro circle, within spell range of the target, and in line of sight. Candidates are
/// ranked by expected helpers, then by clearance from every other monster, then by route order.
/// </summary>
public static class NaturalPullPlanner
{
	public const float SupportRangeOffset = 2f;
	public const float SpellRange = 22f;

	/// <summary>Monsters that would come to <paramref name="target"/>'s aid when it is hit from
	/// <paramref name="firingPosition"/>.</summary>
	public static IReadOnlyList<NaturalPullMonster> Helpers(NaturalPullMonster target, BotPosition firingPosition,
		IReadOnlyList<NaturalPullMonster> monsters, Func<string, string, bool> canSupport,
		Func<BotPosition, BotPosition, bool>? lineOfSight = null) => monsters
		.Where(m => m.Npc.ObjectId != target.Npc.ObjectId && canSupport(m.Tribe, target.Tribe) &&
			(Near(m, target.Npc.Position, m.AggroRadius + SupportRangeOffset, lineOfSight) ||
			 Near(m, firingPosition, m.AggroRadius + SupportRangeOffset, lineOfSight)))
		.ToArray();

	/// <summary>
	/// Best pull among <paramref name="targets"/> (earlier entries win ties, e.g. route order). Firing
	/// candidates per target: the given staging points plus a ring at 60–95% of spell range around it.
	/// <paramref name="reachable"/> must say whether the bot can walk to a spot outside the observed
	/// circles. Returns null when no candidate is in range, visible and reachable.
	/// </summary>
	public static NaturalPullPlan? Plan(BotPosition current, IReadOnlyList<NaturalPullMonster> targets,
		IReadOnlyList<NaturalPullMonster> monsters, IReadOnlyList<BotPosition> stagingPoints,
		Func<string, string, bool> canSupport, Func<BotPosition, BotPosition, bool> lineOfSight,
		Func<BotPosition, BotPosition?> snapToGround, Func<BotPosition, bool> reachable, int sectors = 16)
	{
		ArgumentNullException.ThrowIfNull(targets);
		ArgumentNullException.ThrowIfNull(monsters);
		NaturalPullPlan? best = null;
		(int Helpers, float Clearance, int Order) bestKey = (int.MaxValue, 0, int.MaxValue);
		for (int order = 0; order < targets.Count; order++)
		{
			NaturalPullMonster target = targets[order];
			var spots = new List<BotPosition>(stagingPoints.Where(p => Horizontal(p, target.Npc.Position) <= SpellRange));
			foreach (float fraction in (float[])[0.95f, 0.8f, 0.6f])
				for (int sector = 0; sector < sectors; sector++)
				{
					float angle = sector * 2 * MathF.PI / sectors;
					var raw = target.Npc.Position with
					{
						X = target.Npc.Position.X + SpellRange * fraction * MathF.Cos(angle),
						Y = target.Npc.Position.Y + SpellRange * fraction * MathF.Sin(angle),
					};
					if (snapToGround(raw) is BotPosition ground) spots.Add(ground);
				}
			// Cheap filters first; reachability (a route search) only for spots that could beat the best.
			foreach (var (spot, helpers, clearance) in spots
				.Where(p => Horizontal(p, target.Npc.Position) <= SpellRange && Horizontal(p, target.Npc.Position) >= target.AggroRadius + 1)
				.Where(p => monsters.All(m => !Near(m, p, m.AggroRadius + 1, null)))
				.Select(p => (Spot: p, Helpers: Helpers(target, p, monsters, canSupport, lineOfSight),
					Clearance: monsters.Where(m => m.Npc.ObjectId != target.Npc.ObjectId)
						.Select(m => Horizontal(p, m.Npc.Position) - m.AggroRadius).DefaultIfEmpty(99).Min()))
				.OrderBy(c => c.Helpers.Count).ThenByDescending(c => MathF.Min(c.Clearance, 15)).ThenBy(c => Horizontal(current, c.Spot)))
			{
				var key = (helpers.Count, MathF.Min(clearance, 15), order);
				if (best != null && Worse(key, bestKey)) break;
				if (!lineOfSight(spot, target.Npc.Position) || !reachable(spot)) continue;
				best = new NaturalPullPlan(target, spot, helpers, clearance);
				bestKey = key;
				break;
			}
		}
		return best;

		static bool Worse((int Helpers, float Clearance, int Order) a, (int Helpers, float Clearance, int Order) b) =>
			a.Helpers != b.Helpers ? a.Helpers > b.Helpers
			: MathF.Abs(a.Clearance - b.Clearance) > 0.5f ? a.Clearance < b.Clearance
			: a.Order >= b.Order;
	}

	private static bool Near(NaturalPullMonster monster, BotPosition point, float range,
		Func<BotPosition, BotPosition, bool>? lineOfSight) =>
		new BotNavigationHazard(monster.Npc.Position, range).DistanceTo(point) <= range &&
		(lineOfSight == null || lineOfSight(monster.Npc.Position, point));

	private static float Horizontal(BotPosition a, BotPosition b) =>
		MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
