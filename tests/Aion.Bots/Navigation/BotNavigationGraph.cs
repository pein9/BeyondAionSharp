using Aion.Bots.World;

namespace Aion.Bots.Navigation;

[Flags]
public enum BotWaypointSource
{
	None = 0,
	Spawn = 1 << 0,
	WalkerRoute = 1 << 1,
	Gather = 1 << 2,
	Portal = 1 << 3,
	BindPoint = 1 << 4,
	QuestNpc = 1 << 5,
}

public sealed record BotWaypoint(int Id, int MapId, BotPosition Position, BotWaypointSource Sources,
	int? TemplateId = null, string? RouteId = null);

/// <summary>A deterministic, per-map waypoint graph whose edges never exceed twenty metres.</summary>
public sealed class BotNavigationGraph
{
	public const float MaximumEdgeDistance = 20f;

	private readonly IReadOnlyDictionary<int, BotMapNavigationGraph> maps;

	internal BotNavigationGraph(IEnumerable<BotWaypoint> waypoints)
	{
		maps = waypoints
			.GroupBy(waypoint => waypoint.MapId)
			.OrderBy(group => group.Key)
			.ToDictionary(group => group.Key, group => new BotMapNavigationGraph(group.Key, group));
	}

	public IReadOnlyDictionary<int, BotMapNavigationGraph> Maps => maps;

	public BotMapNavigationGraph? GetMap(int mapId) => maps.GetValueOrDefault(mapId);

	public IReadOnlyList<BotPosition> FindPath(int mapId, BotPosition start, BotPosition destination)
	{
		return maps.TryGetValue(mapId, out var map)
			? map.FindPath(start, destination)
			: [];
	}
}

public sealed class BotMapNavigationGraph
{
	private const int StartNodeId = -1;
	private const int DestinationNodeId = -2;

	private readonly IReadOnlyDictionary<int, BotWaypoint> waypointsById;
	private readonly IReadOnlyDictionary<int, int[]> edges;
	private readonly IReadOnlyDictionary<GridCell, int[]> cells;

	internal BotMapNavigationGraph(int mapId, IEnumerable<BotWaypoint> source)
	{
		MapId = mapId;
		var waypoints = source.OrderBy(waypoint => waypoint.Id).ToArray();
		Waypoints = waypoints;
		waypointsById = waypoints.ToDictionary(waypoint => waypoint.Id);

		var mutableEdges = waypoints.ToDictionary(waypoint => waypoint.Id, _ => new List<int>());
		var mutableCells = new Dictionary<GridCell, List<int>>();
		foreach (var waypoint in waypoints)
		{
			var cell = GridCell.From(waypoint.Position);
			foreach (var nearbyCell in cell.Neighbours())
			{
				if (!mutableCells.TryGetValue(nearbyCell, out var candidates))
					continue;
				foreach (var candidateId in candidates)
				{
					if (Distance(waypoint.Position, waypointsById[candidateId].Position) > BotNavigationGraph.MaximumEdgeDistance)
						continue;
					mutableEdges[waypoint.Id].Add(candidateId);
					mutableEdges[candidateId].Add(waypoint.Id);
				}
			}
			if (!mutableCells.TryGetValue(cell, out var occupants))
				mutableCells[cell] = occupants = [];
			occupants.Add(waypoint.Id);
		}

		edges = mutableEdges.ToDictionary(pair => pair.Key, pair => pair.Value.Order().ToArray());
		cells = mutableCells.ToDictionary(pair => pair.Key, pair => pair.Value.Order().ToArray());
	}

	public int MapId { get; }
	public IReadOnlyList<BotWaypoint> Waypoints { get; }

	public IReadOnlyList<int> GetNeighbours(int waypointId) => edges.GetValueOrDefault(waypointId) ?? [];

	public IReadOnlyList<BotWaypoint> FindPath(int startWaypointId, int destinationWaypointId)
	{
		if (!waypointsById.ContainsKey(startWaypointId) || !waypointsById.ContainsKey(destinationWaypointId))
			return [];
		return Search(startWaypointId, destinationWaypointId, waypointsById[startWaypointId].Position,
			waypointsById[destinationWaypointId].Position, [], []);
	}

	/// <summary>
	/// Routes between observed positions by attaching each endpoint only to graph nodes within the normal
	/// twenty-metre edge limit. The destination is returned as the last node, preserving its packet-provided Z.
	/// </summary>
	public IReadOnlyList<BotPosition> FindPath(BotPosition start, BotPosition destination)
	{
		if (Distance(start, destination) <= BotNavigationGraph.MaximumEdgeDistance)
			return [destination];

		var startEdges = FindNearby(start);
		var destinationEdges = FindNearby(destination);
		if (startEdges.Length == 0 || destinationEdges.Length == 0)
			return [];

		var path = Search(StartNodeId, DestinationNodeId, start, destination, startEdges, destinationEdges);
		return path.Select(waypoint => waypoint.Id == DestinationNodeId ? destination : waypoint.Position).ToArray();
	}

	private IReadOnlyList<BotWaypoint> Search(int startId, int destinationId, BotPosition start,
		BotPosition destination, IReadOnlyList<int> startEdges, IReadOnlyList<int> destinationEdges)
	{
		var destinationEdgeSet = destinationEdges.ToHashSet();
		var open = new PriorityQueue<int, (float Score, int NodeId)>();
		var cameFrom = new Dictionary<int, int>();
		var costs = new Dictionary<int, float> { [startId] = 0 };
		open.Enqueue(startId, (Distance(start, destination), startId));

		while (open.TryDequeue(out var current, out _))
		{
			if (current == destinationId)
				return Reconstruct(cameFrom, destinationId, startId, start, destination);

			var currentPosition = PositionOf(current, start, destination);
			foreach (var neighbour in NeighboursOf(current, destinationId, startEdges, destinationEdgeSet))
			{
				var neighbourPosition = PositionOf(neighbour, start, destination);
				var candidateCost = costs[current] + Distance(currentPosition, neighbourPosition);
				if (costs.TryGetValue(neighbour, out var knownCost) && candidateCost >= knownCost)
					continue;
				cameFrom[neighbour] = current;
				costs[neighbour] = candidateCost;
				var score = candidateCost + Distance(neighbourPosition, destination);
				open.Enqueue(neighbour, (score, neighbour));
			}
		}
		return [];
	}

	private IEnumerable<int> NeighboursOf(int nodeId, int destinationId, IReadOnlyList<int> startEdges,
		IReadOnlySet<int> destinationEdges)
	{
		if (nodeId == StartNodeId)
			return startEdges;
		if (nodeId == DestinationNodeId)
			return [];
		var neighbours = edges[nodeId];
		return destinationEdges.Contains(nodeId) ? neighbours.Append(destinationId) : neighbours;
	}

	private BotPosition PositionOf(int nodeId, BotPosition start, BotPosition destination) => nodeId switch
	{
		StartNodeId => start,
		DestinationNodeId => destination,
		_ => waypointsById[nodeId].Position,
	};

	private IReadOnlyList<BotWaypoint> Reconstruct(IReadOnlyDictionary<int, int> cameFrom, int current,
		int startId, BotPosition start, BotPosition destination)
	{
		var path = new List<BotWaypoint>();
		while (current != startId)
		{
			path.Add(current switch
			{
				DestinationNodeId => new BotWaypoint(DestinationNodeId, MapId, destination, BotWaypointSource.None),
				StartNodeId => new BotWaypoint(StartNodeId, MapId, start, BotWaypointSource.None),
				_ => waypointsById[current],
			});
			if (!cameFrom.TryGetValue(current, out current))
				return [];
		}
		path.Reverse();
		return path;
	}

	private int[] FindNearby(BotPosition position)
	{
		var result = new List<int>();
		foreach (var cell in GridCell.From(position).Neighbours())
		{
			if (!cells.TryGetValue(cell, out var candidates))
				continue;
			result.AddRange(candidates.Where(id =>
				Distance(position, waypointsById[id].Position) <= BotNavigationGraph.MaximumEdgeDistance));
		}
		return result.Order().ToArray();
	}

	private static float Distance(BotPosition left, BotPosition right)
	{
		var x = left.X - right.X;
		var y = left.Y - right.Y;
		var z = left.Z - right.Z;
		return MathF.Sqrt(x * x + y * y + z * z);
	}

	private readonly record struct GridCell(int X, int Y, int Z)
	{
		public static GridCell From(BotPosition position) => new(
			(int)MathF.Floor(position.X / BotNavigationGraph.MaximumEdgeDistance),
			(int)MathF.Floor(position.Y / BotNavigationGraph.MaximumEdgeDistance),
			(int)MathF.Floor(position.Z / BotNavigationGraph.MaximumEdgeDistance));

		public IEnumerable<GridCell> Neighbours()
		{
			for (var x = X - 1; x <= X + 1; x++)
				for (var y = Y - 1; y <= Y + 1; y++)
					for (var z = Z - 1; z <= Z + 1; z++)
						yield return new GridCell(x, y, z);
		}
	}
}
