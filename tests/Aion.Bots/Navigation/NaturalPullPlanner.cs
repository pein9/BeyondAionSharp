using Aion.Bots.World;

namespace Aion.Bots.Navigation;

/// <summary>An observed monster as the pull planner sees it: where it is, how far it aggroes, and its
/// tribe, which decides who comes to help it.</summary>
public sealed record NaturalPullMonster(NaturalNavigationObject Npc, float AggroRadius, string Tribe,
	float BoundRadius = 0);

/// <summary>A chosen pull: fire at <see cref="Target"/> from <see cref="FiringPosition"/>. <see cref="Helpers"/>
/// lists the monsters expected to join (the fewest the planner could find; empty is a clean single pull).</summary>
public sealed record NaturalPullPlan(NaturalPullMonster Target, BotPosition FiringPosition,
	IReadOnlyList<NaturalPullMonster> Helpers, float ClearanceFromOthers);

/// <summary>A firing spot actually evaluated by the baseline planner; unevaluated spots are not asserted legal.</summary>
public sealed record NaturalPullCandidate(int TargetObjectId, BotPosition FiringPosition,
	int[] ExpectedHelperObjectIds, float ClearanceFromOthers, bool Legal, string? IllegalReason);

/// <summary>
/// Pull one monster at a time, the way a player does: from a spot off to the side of the path, at spell
/// range, where neither the target's friends nor anything else will join in.
///
/// Java rule (<c>AggroEventHandler.onCreatureNeedsSupport</c>): when a monster is hit or aggroes, every
/// NPC whose tribe can support it (<c>TribeRelationsData.canSupport</c>) and that is not already fighting
/// helps if it is within its own aggro range + 2 m (<c>SUPPORT_RANGE_OFFSET</c>) of the monster or of the
/// attacker and can see it. So a pull costs nothing extra when no supporter is within that range of the
/// target, and none is within that range of the firing spot. The firing spot must also be outside every
/// monster's own aggro circle, within spell range of the target, and in line of sight. A patrol is measured
/// from the nearest point of the path it was seen walking: it walks into a fight there sooner or later.
/// Candidates are ranked by expected helpers, then by clearance from every other monster, then by route order.
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
			// Java PositionUtil.isInRange(..., false) adds both NPC body radii
			// when checking support near the target; the player template has radius 0.
			(Near(m, target.Npc.Position,
				m.AggroRadius + SupportRangeOffset + m.BoundRadius + target.BoundRadius, lineOfSight) ||
			 Near(m, firingPosition,
				m.AggroRadius + SupportRangeOffset + m.BoundRadius, lineOfSight)))
		.ToArray();

	/// <summary>
	/// Everything that joins a melee fight with <paramref name="target"/> fought from <paramref name="fightSpot"/>,
	/// and nothing else: its supporters under the server's assist rule (<see cref="Helpers"/>), plus any monster
	/// whose own aggro circle reaches where the bot stands (within <paramref name="meleeReach"/> of the spot).
	/// A monster that is merely nearby, with a tribe that does not help and a circle that does not reach, is
	/// left alone; killing it would only cost time and a respawn window.
	/// </summary>
	public static IReadOnlyList<NaturalPullMonster> AddsAt(NaturalPullMonster target, BotPosition fightSpot,
		IReadOnlyList<NaturalPullMonster> monsters, Func<string, string, bool> canSupport,
		Func<BotPosition, BotPosition, bool>? lineOfSight = null, float meleeReach = 3f)
	{
		var adds = new List<NaturalPullMonster>(Helpers(target, fightSpot, monsters, canSupport, lineOfSight));
		foreach (NaturalPullMonster monster in monsters)
			if (monster.Npc.ObjectId != target.Npc.ObjectId && monster.AggroRadius > 0 &&
				adds.All(add => add.Npc.ObjectId != monster.Npc.ObjectId) &&
				Near(monster, fightSpot, monster.AggroRadius + meleeReach, lineOfSight))
				adds.Add(monster);
		return adds;
	}

	/// <summary>
	/// Best pull among <paramref name="targets"/> (earlier entries win ties, e.g. route order). Firing
	/// candidates per target: the given staging points plus a ring at 60–95% of spell range around it.
	/// <paramref name="reachable"/> must say whether the bot can walk to a spot outside the observed
	/// circles. Returns null when no candidate is in range, visible and reachable.
	/// </summary>
	public static NaturalPullPlan? Plan(BotPosition current, IReadOnlyList<NaturalPullMonster> targets,
		IReadOnlyList<NaturalPullMonster> monsters, IReadOnlyList<BotPosition> stagingPoints,
		Func<string, string, bool> canSupport, Func<BotPosition, BotPosition, bool> lineOfSight,
		Func<BotPosition, BotPosition?> snapToGround, Func<BotPosition, bool> reachable, int sectors = 16,
		Action<NaturalPullCandidate>? audit = null, float pullDistanceMeters = SpellRange)
	{
		ArgumentNullException.ThrowIfNull(targets);
		ArgumentNullException.ThrowIfNull(monsters);
		if (pullDistanceMeters is <= 0 or > SpellRange)
			throw new ArgumentOutOfRangeException(nameof(pullDistanceMeters));
		NaturalPullPlan? best = null;
		(int Helpers, float Clearance, int Order) bestKey = (int.MaxValue, 0, int.MaxValue);
		for (int order = 0; order < targets.Count; order++)
		{
			NaturalPullMonster target = targets[order];
			var spots = new List<BotPosition>(stagingPoints.Where(p => Horizontal(p, target.Npc.Position) <= pullDistanceMeters));
			foreach (float fraction in (float[])[0.95f, 0.8f, 0.6f])
				for (int sector = 0; sector < sectors; sector++)
				{
					float angle = sector * 2 * MathF.PI / sectors;
					var raw = target.Npc.Position with
					{
						X = target.Npc.Position.X + pullDistanceMeters * fraction * MathF.Cos(angle),
						Y = target.Npc.Position.Y + pullDistanceMeters * fraction * MathF.Sin(angle),
					};
					if (snapToGround(raw) is BotPosition ground) spots.Add(ground);
				}
			// Cheap filters first; reachability (a route search) only for spots that could beat the best.
			foreach (var (spot, helpers, clearance) in spots
				.Where(p => Horizontal(p, target.Npc.Position) <= SpellRange && Horizontal(p, target.Npc.Position) >= target.AggroRadius + 1)
				.Where(p => monsters.All(m => !Near(m, p, m.AggroRadius + 1, null)))
				.Select(p => (Spot: p, Helpers: Helpers(target, p, monsters, canSupport, lineOfSight),
					Clearance: monsters.Where(m => m.Npc.ObjectId != target.Npc.ObjectId)
						.Select(m => m.Npc.PossiblePositions().Min(at => Horizontal(p, at)) - m.AggroRadius).DefaultIfEmpty(99).Min()))
				.OrderBy(c => c.Helpers.Count).ThenByDescending(c => MathF.Min(c.Clearance, 15)).ThenBy(c => Horizontal(current, c.Spot)))
			{
				var key = (helpers.Count, MathF.Min(clearance, 15), order);
				if (best != null && Worse(key, bestKey)) break;
				bool visible = lineOfSight(spot, target.Npc.Position);
				bool route = visible && reachable(spot);
				audit?.Invoke(new NaturalPullCandidate(target.Npc.ObjectId, spot,
					helpers.Select(helper => helper.Npc.ObjectId).ToArray(), clearance,
					visible && route, !visible ? "No collision-checked line of sight." :
					!route ? "No checked route to firing spot." : null));
				if (!visible || !route) continue;
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

	// A patrol comes by anywhere on the path it was seen walking: measure from the nearest point of it.
	private static bool Near(NaturalPullMonster monster, BotPosition point, float range,
		Func<BotPosition, BotPosition, bool>? lineOfSight)
	{
		BotPosition nearest = monster.Npc.PossiblePositions().MinBy(at => new BotNavigationHazard(at, range).DistanceTo(point));
		return new BotNavigationHazard(nearest, range).DistanceTo(point) <= range &&
			(lineOfSight == null || lineOfSight(nearest, point));
	}

	private static float Horizontal(BotPosition a, BotPosition b) =>
		MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
