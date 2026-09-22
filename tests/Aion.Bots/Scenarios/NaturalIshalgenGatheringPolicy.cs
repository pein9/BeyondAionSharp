using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

public sealed record NaturalGatherNode(int ObjectId, BotPosition Position);
public sealed record NaturalGatherChoice(string Action, string Reason, SoakGatheringSpot? Spot,
	int? ObjectId, TimeSpan? Wait);

/// <summary>
/// NI-06 single-player Azpha search. Shipped spots are area hints only: a gather action
/// always requires a currently client-observed object id. Java completeInteraction
/// consumes a node use after either success or failure.
/// </summary>
public sealed class NaturalIshalgenGatheringPolicy
{
	private static readonly TimeSpan OccupiedDelay = TimeSpan.FromSeconds(15);
	private static readonly TimeSpan SearchDelay = TimeSpan.FromSeconds(5);
	private readonly IReadOnlyList<SoakGatheringSpot> spots;
	private readonly Dictionary<SoakGatheringSpot, NodeState> nodes;

	public NaturalIshalgenGatheringPolicy(IEnumerable<SoakGatheringSpot> shippedSpots)
	{
		spots = shippedSpots.Where(spot => spot.MapId == GatheringTarget.YoungAzpha.MapId &&
			spot.TemplateId == GatheringTarget.YoungAzpha.TemplateId).Distinct().ToArray();
		if (spots.Count == 0) throw new InvalidDataException("No shipped Young Azpha spawn spots.");
		nodes = spots.ToDictionary(spot => spot, _ => new NodeState());
	}

	public NaturalGatherChoice Decide(BotPosition position, IReadOnlyList<NaturalGatherNode> observed,
		long azphaCount, TimeSpan elapsed)
	{
		if (azphaCount >= 3) return new("complete", "Three Azpha items are present in client-observed inventory.", null, null, null);
		if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
		var visible = observed.Select(node => (Node: node, Spot: Match(node.Position)))
			.Where(pair => pair.Spot != null && elapsed >= nodes[pair.Spot.Value].AvailableAt)
			.OrderBy(pair => Distance(position, pair.Node.Position)).ThenBy(pair => pair.Node.ObjectId).FirstOrDefault();
		if (visible.Spot is { } spot)
			return new("gather", $"Nearest available client-observed Azpha {visible.Node!.ObjectId}; " +
				$"attempt {nodes[spot].Uses + 1} of {GatheringTarget.HarvestCount} on this node.", spot,
				visible.Node.ObjectId, null);
		SoakGatheringSpot? hint = spots.Where(spot => elapsed >= nodes[spot].AvailableAt)
			.OrderBy(spot => Distance(position, spot.Position)).ThenBy(spot => spot.X).ThenBy(spot => spot.Y)
			.Cast<SoakGatheringSpot?>().FirstOrDefault();
		if (hint != null)
			return new("explore", "No usable Azpha is visible; path to the closest shipped spawn hint and observe again.", hint, null, null);
		TimeSpan next = nodes.Values.Min(node => node.AvailableAt) - elapsed;
		return new("wait", "All searched Azpha spots are occupied, depleted, or awaiting observation; wait and rescan.",
			null, null, TimeSpan.FromTicks(Math.Min(next.Ticks, SearchDelay.Ticks)));
	}

	public void RecordAttempt(SoakGatheringSpot spot, int objectId, byte outcome, TimeSpan elapsed)
	{
		if (outcome is not (5 or 6 or 7 or 8)) throw new ArgumentOutOfRangeException(nameof(outcome));
		NodeState state = nodes[spot];
		if (outcome == 8)
		{
			state.AvailableAt = elapsed + OccupiedDelay;
			return;
		}
		if (state.ObjectId != objectId || state.Uses >= GatheringTarget.HarvestCount && elapsed >= state.AvailableAt)
		{
			state.ObjectId = objectId;
			state.Uses = 0;
		}
		state.Uses++;
		state.AvailableAt = state.Uses >= GatheringTarget.HarvestCount
			? elapsed + GatheringTarget.RespawnDelay : elapsed;
	}

	public void RecordUnobserved(SoakGatheringSpot spot, TimeSpan elapsed) =>
		nodes[spot].AvailableAt = elapsed + SearchDelay;

	public void RecordUnreachable(SoakGatheringSpot spot, TimeSpan elapsed) =>
		nodes[spot].AvailableAt = elapsed + TimeSpan.FromMinutes(5);

	private SoakGatheringSpot? Match(BotPosition position) => spots
		.Where(spot => Distance(spot.Position, position) < 1)
		.Cast<SoakGatheringSpot?>().FirstOrDefault();

	private static float Distance(BotPosition a, BotPosition b) => MathF.Sqrt(
		MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2) + MathF.Pow(a.Z - b.Z, 2));

	private sealed class NodeState
	{
		public int? ObjectId;
		public int Uses;
		public TimeSpan AvailableAt;
	}
}
