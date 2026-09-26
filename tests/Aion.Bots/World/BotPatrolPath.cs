namespace Aion.Bots.World;

/// <summary>
/// The ground a monster has been seen walking: its patrol route or random-walk area, learned the way a player
/// learns it, by watching it. Java <c>NpcMoveController.getMoveMask</c> sends <c>NPC_WALK_SLOW/FAST</c> while an
/// NPC walks (patrol, random walk, idling) and <c>NPC_RUN_SLOW/FAST</c> while it fights (weapon drawn) or returns
/// home, so walk legs are its beat and a run starts it over. A walk leg longer than any patrol step (the walk
/// back to a route after a chase) is not part of the beat either.
/// </summary>
public static class BotPatrolPath
{
	/// <summary>Largest gap between recorded points along a leg; aggro circles (8 m and up) overlap well at this.</summary>
	public const float PointSpacing = 3f;
	/// <summary>A point this close to one already recorded adds nothing (the next lap of the same loop).</summary>
	public const float MinimumSeparation = 2f;
	/// <summary>Walker route steps and random-walk legs are 5 m typically and 23 m at most (measured on Ishalgen).</summary>
	public const float MaximumLeg = 25f;
	/// <summary>The newest points kept: the 10-step Gray Mane patrol loop by Hatata's cave takes 21.</summary>
	public const int MaximumPoints = 48;
	/// <summary>
	/// A route that only passes a walker needs the stretch of its path it can reach while the bot goes by, not
	/// the whole beat: walkers cover about 1 m/s (the Gray Mane patrol walked 13 m in 13 s). A place the bot
	/// stays at (a firing spot, a fight, a rest) needs the whole path: the patrol comes back.
	/// </summary>
	public const float PassingReach = 15f;

	// Java MovementMask; flying NPCs add GLIDE (0x04) to the walk masks and to NPC_RUN_FAST.
	private static readonly byte[] WalkMasks = [0xEA, 0xE8, 0xEE, 0xEC];
	private static readonly byte[] RunMasks = [0xE4, 0xE2, 0xE6];

	/// <summary>The path after an NPC's SM_MOVE from <paramref name="position"/> toward <paramref name="target"/>.</summary>
	public static IReadOnlyList<BotPosition>? Extend(IReadOnlyList<BotPosition>? path, byte? movementMask,
		BotPosition position, BotPosition? target)
	{
		if (movementMask is not byte mask) return path;
		if (RunMasks.Contains(mask)) return null;
		if (!WalkMasks.Contains(mask) || target is not BotPosition goal) return path;
		float leg = Distance(position, goal);
		if (leg > MaximumLeg) return path;
		var points = new List<BotPosition>(path ?? []);
		int samples = Math.Max(1, (int)MathF.Ceiling(leg / PointSpacing));
		for (int sample = 0; sample <= samples; sample++)
		{
			float along = (float)sample / samples;
			var point = new BotPosition(position.X + (goal.X - position.X) * along,
				position.Y + (goal.Y - position.Y) * along, position.Z + (goal.Z - position.Z) * along, 0);
			if (points.All(known => Distance(known, point) >= MinimumSeparation)) points.Add(point);
		}
		if (points.Count > MaximumPoints) points.RemoveRange(0, points.Count - MaximumPoints);
		return points;
	}

	private static float Distance(BotPosition a, BotPosition b) => MathF.Sqrt(
		(a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));
}
