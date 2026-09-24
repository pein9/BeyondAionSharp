using Aion.Bots.World;

namespace Aion.Bots.Navigation;

/// <summary>Retreat only toward client-estimated ground the bot previously walked.
/// The caller must recheck every proposed route against current geometry and mobs.</summary>
public static class NaturalCombatRetreatPolicy
{
	public static BotPosition[] SelectCheckpoints(BotPosition current, BotPosition refuge,
		IEnumerable<NaturalNavigationEvent> events, IReadOnlyList<BotPosition> observedAttackers)
	{
		ArgumentNullException.ThrowIfNull(events);
		ArgumentNullException.ThrowIfNull(observedAttackers);
		float currentClearance = Clearance(current);
		var selected = new List<BotPosition>();
		foreach (BotPosition checkpoint in events.Reverse()
			.Where(item => item.Action == "segment-progress" && item.Position != null)
			.Select(item => item.Position!.Value).Append(refuge))
		{
			float travel = Distance(current, checkpoint);
			if (travel is < 30 or > 150 || Clearance(checkpoint) < 35 ||
				Clearance(checkpoint) < currentClearance + 12 ||
				selected.Any(other => Distance(other, checkpoint) < 12)) continue;
			selected.Add(checkpoint);
			if (selected.Count == 8) break;
		}
		return selected.ToArray();

		float Clearance(BotPosition point) => observedAttackers.Count == 0 ? float.PositiveInfinity :
			observedAttackers.Min(attacker => Distance(point, attacker));
	}

	/// <summary>Java <c>AttackManager.checkGiveupDistance</c> with Ishalgen's default <c>ai_info</c>
	/// (chase_home 200): a chasing monster gives up once it is more than half that, 100 m, from home and
	/// has not been hit for 10 s. An equal-speed chaser cannot be outrun, so an escape has to drag
	/// the pack that far from home.</summary>
	public const float GiveUpDistanceFromHome = 100f;

	/// <summary>
	/// Escape destinations, best first. Every candidate lies ahead of the bot, away from the
	/// attackers' current centre: the angle between "candidate minus bot" and "bot minus attackers"
	/// is under about 80 degrees. None lies inside another observed hostile's circle. They are ranked
	/// by how far they are from the nearest attacker's home, since give-up is measured from home, and
	/// then by shorter travel. Unlike <see cref="SelectCheckpoints"/>, which re-measured clearance from
	/// chasers that move with the bot, this cannot choose a point back toward the pack.
	/// </summary>
	public static BotPosition[] SelectEscape(BotPosition current, IReadOnlyList<BotPosition> attackers,
		IReadOnlyList<BotPosition> attackerHomes, IEnumerable<BotPosition> candidates,
		IReadOnlyList<BotNavigationHazard> otherHazards, int maximum = 8)
	{
		ArgumentNullException.ThrowIfNull(attackers);
		ArgumentNullException.ThrowIfNull(attackerHomes);
		ArgumentNullException.ThrowIfNull(candidates);
		ArgumentNullException.ThrowIfNull(otherHazards);
		if (attackers.Count == 0) return candidates.Take(maximum).ToArray();
		float cx = attackers.Average(a => a.X), cy = attackers.Average(a => a.Y);
		float awayX = current.X - cx, awayY = current.Y - cy;
		float awayLength = MathF.Sqrt(awayX * awayX + awayY * awayY);
		if (awayLength < 0.5f)
		{
			// Standing on the pack's centre: away from the homes instead.
			awayX = current.X - attackerHomes.DefaultIfEmpty(current).Average(h => h.X);
			awayY = current.Y - attackerHomes.DefaultIfEmpty(current).Average(h => h.Y);
			awayLength = MathF.Max(0.5f, MathF.Sqrt(awayX * awayX + awayY * awayY));
		}
		IReadOnlyList<BotPosition> homes = attackerHomes.Count > 0 ? attackerHomes : attackers;
		var ranked = new List<(BotPosition Point, float Score)>();
		foreach (BotPosition candidate in candidates)
		{
			float dx = candidate.X - current.X, dy = candidate.Y - current.Y;
			float travel = MathF.Sqrt(dx * dx + dy * dy);
			if (travel < 20) continue;
			float cosine = (dx * awayX + dy * awayY) / (travel * awayLength);
			if (cosine < 0.17f) continue;
			if (otherHazards.Any(h => Horizontal(candidate, h.Position) < h.Radius + 2)) continue;
			float fromHome = homes.Min(h => Horizontal(candidate, h));
			float score = MathF.Min(fromHome, GiveUpDistanceFromHome + 30) * 2 + cosine * 20 - travel * 0.25f;
			ranked.Add((candidate, score));
		}
		var chosen = new List<BotPosition>();
		foreach (var (point, _) in ranked.OrderByDescending(r => r.Score).ThenBy(r => r.Point.X).ThenBy(r => r.Point.Y))
		{
			if (chosen.Any(other => Horizontal(other, point) < 10)) continue;
			chosen.Add(point);
			if (chosen.Count == maximum) break;
		}
		return chosen.ToArray();
	}

	private static float Horizontal(BotPosition a, BotPosition b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

	private static float Distance(BotPosition a, BotPosition b) => MathF.Sqrt(
		MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2) + MathF.Pow(a.Z - b.Z, 2));
}
