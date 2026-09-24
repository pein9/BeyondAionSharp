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

	private static float Distance(BotPosition a, BotPosition b) => MathF.Sqrt(
		MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2) + MathF.Pow(a.Z - b.Z, 2));
}
