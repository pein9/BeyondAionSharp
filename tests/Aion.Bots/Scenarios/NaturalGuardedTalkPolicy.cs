using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>An aggressive monster the client sees near a quest NPC, with the radius at which it attacks.</summary>
public sealed record NaturalTalkHostile(int ObjectId, int NpcId, BotPosition Position, float AggroRadius);

/// <param name="Action">talk (from <paramref name="Spot"/>) or clear (pull <paramref name="PullFirst"/> first).</param>
public sealed record NaturalGuardedTalkDecision(string Action, BotPosition? Spot, int? PullFirst, string Reason);

/// <summary>
/// AM-03 (docs/natural-altgard-leveling.md): Moslan Crossroad's quest NPCs stand 21-56 m from aggressive grove pluma.
/// Java aggroes a monster on a player less than 10 levels above it within its aggro range (<c>CreatureEventHandler</c>), so
/// a level 13 Cleric draws level 10-11 pluma. The bot talks from a spot within talk range of the NPC that is outside every
/// observed aggro circle by a margin, and reaches it on a route that treats those circles as hard rules. When no such spot
/// exists, it pulls the monster whose circle covers the most talk spots first, and plans again. Pure.
/// </summary>
public static class NaturalGuardedTalkPolicy
{
	/// <summary>Clearance kept beyond an aggro circle: a monster walking or turning moves its circle.</summary>
	public const float Margin = 3f;

	public static NaturalGuardedTalkDecision Decide(BotPosition npc, float talkRange, IReadOnlyList<NaturalTalkHostile> hostiles,
		IReadOnlyList<BotPosition> candidates)
	{
		BotPosition[] inRange = candidates.Where(spot => Distance(spot, npc) <= talkRange - 0.5f).ToArray();
		if (inRange.Length == 0)
			return new("clear", null, null, "No ground within talk range of the NPC.");
		BotPosition? safe = inRange.Where(spot => hostiles.All(hostile => Distance(spot, hostile.Position) > hostile.AggroRadius + Margin))
			.OrderByDescending(spot => hostiles.Count == 0 ? 0 : hostiles.Min(hostile => Distance(spot, hostile.Position) - hostile.AggroRadius))
			.ThenBy(spot => Distance(spot, npc)).Cast<BotPosition?>().FirstOrDefault();
		if (safe is { } spot)
			return new("talk", spot, null, hostiles.Count == 0
				? "No aggressive monster in view."
				: $"Talk spot {hostiles.Min(hostile => Distance(spot, hostile.Position) - hostile.AggroRadius):F1} m outside the nearest aggro circle.");
		NaturalTalkHostile blocker = hostiles.OrderByDescending(hostile => inRange.Count(spot => Distance(spot, hostile.Position) <= hostile.AggroRadius + Margin))
			.ThenBy(hostile => Distance(hostile.Position, npc)).First();
		return new("clear", null, blocker.ObjectId, $"Every talk spot is inside an aggro circle; pull {blocker.NpcId} ({blocker.ObjectId}) first.");
	}

	public static float Distance(BotPosition a, BotPosition b) => MathF.Sqrt(MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2));
}
