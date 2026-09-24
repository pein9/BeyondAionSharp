using Aion.Bots.World;

namespace Aion.Bots.Navigation.NavMesh;

/// <summary>A planned journey: travel-graph waypoints (possibly none) and the checked ground samples.</summary>
public sealed record BotTravelPlan(IReadOnlyList<BotTravelNode> Waypoints, IReadOnlyList<BotPosition> Route, float Length,
	IReadOnlyList<BotTravelDanger> Danger);

/// <summary>
/// Long-distance, level-aware travel for a playtest bot. The travel graph chooses the corridor (roads,
/// branches, hubs) and weighs each link by the static aggressive spawns it crosses relative to the bot's
/// level; the navmesh router then produces the checked ground route for every leg, with those same spawns
/// as soft cost zones. Observed hostiles still go in <see cref="BotNavQuery.Hazards"/> as hard rules.
/// </summary>
public sealed class BotTravelPlanner(BotTravelGraph graph, BotNavMeshRouter router, IReadOnlyList<BotNavSite> sites)
{
	private const int AttachNodes = 4;
	private const float AttachRadius = 300f;

	public BotTravelGraph Graph { get; } = graph;

	/// <summary>False when <c>AION_BOT_TRAVEL_PLANNER=0</c>: journey drivers then use only their ordinary
	/// route searches (the navmesh itself is controlled separately by <c>AION_BOT_NAVMESH</c>).</summary>
	public static bool Enabled => Environment.GetEnvironmentVariable("AION_BOT_TRAVEL_PLANNER") is not ("0" or "false");

	/// <summary>Planner for a map whose navmesh and travel graph are available to <paramref name="geometry"/>,
	/// or null when either is missing.</summary>
	public static BotTravelPlanner? For(int mapId, BotNavigationGeometry geometry, Aion.GameServer.Dataholders.StaticData data)
	{
		if (!Enabled) return null;
		BotNavMeshRouter? router = geometry.NavMesh;
		if (router == null || !router.Covers(mapId)) return null;
		BotTravelGraph? graph = router.NavMeshes.Directories
			.Select(d => BotTravelGraph.Load(Path.Combine(d, mapId + ".graph.json"))).FirstOrDefault(g => g != null);
		return graph == null ? null : new BotTravelPlanner(graph, router, BotNavSites.Load(data, mapId));
	}

	/// <summary>Static aggressive spawns as cost-only zones, weighted by how dangerous they are at this level.</summary>
	public IReadOnlyList<BotNavDanger> DangerFor(int botLevel) => sites.Where(s => s.Hostile)
		.Select(s => new BotNavDanger(s.Position.X, s.Position.Y, s.AggroRadius + 3, Weight(s.Level, botLevel)))
		.Where(d => d.Weight > 1).ToArray();

	/// <summary>Cost multiplier inside an aggressive spawn's aggro circle: small for weaker monsters, large
	/// for anything the bot cannot comfortably fight.</summary>
	public static float Weight(int monsterLevel, int botLevel) => (monsterLevel - botLevel) switch
	{
		<= -5 => 1f,
		<= -2 => 1.5f,
		<= 0 => 3f,
		<= 2 => 8f,
		_ => 40f,
	};

	/// <summary>Plans and checks a journey. Uses the travel graph when it offers a clearly better corridor
	/// than the direct navmesh route; returns an empty route when neither exists.</summary>
	public BotTravelPlan Plan(BotPosition start, BotPosition goal, int botLevel, BotNavQuery? options = null, bool interaction = true)
	{
		options ??= BotNavQuery.Default;
		options = options with { Danger = [.. options.Danger, .. DangerFor(botLevel)] };
		int mapId = Graph.MapId;
		IReadOnlyList<BotPosition> direct = interaction
			? router.FindInteractionPath(mapId, start, goal, options)
			: router.FindPath(mapId, start, goal, options);
		bool directOk = BotNavMeshRouter.LastOutcome == BotNavRouteOutcome.Routed;
		IReadOnlyList<BotTravelNode> waypoints = GraphRoute(start, goal, botLevel);
		if (waypoints.Count == 0 || directOk && Cost(start, direct, botLevel) <= GraphCost(start, goal, waypoints, botLevel) * 1.1f)
			return directOk ? new BotTravelPlan([], direct, Length(start, direct), DangerOn(direct, botLevel)) : Empty;
		var route = new List<BotPosition>();
		BotPosition current = start;
		foreach (BotTravelNode node in waypoints)
		{
			IReadOnlyList<BotPosition> leg = router.FindPath(mapId, current, node.Position, options);
			if (leg.Count == 0 && BotNavMeshRouter.LastOutcome != BotNavRouteOutcome.Routed)
				return directOk ? new BotTravelPlan([], direct, Length(start, direct), DangerOn(direct, botLevel)) : Empty;
			route.AddRange(leg);
			if (route.Count > 0) current = route[^1];
		}
		IReadOnlyList<BotPosition> last = interaction
			? router.FindInteractionPath(mapId, current, goal, options)
			: router.FindPath(mapId, current, goal, options);
		if (BotNavMeshRouter.LastOutcome != BotNavRouteOutcome.Routed)
			return directOk ? new BotTravelPlan([], direct, Length(start, direct), DangerOn(direct, botLevel)) : Empty;
		route.AddRange(last);
		return new BotTravelPlan(waypoints, route, Length(start, route), DangerOn(route, botLevel));
	}

	private static readonly BotTravelPlan Empty = new([], [], 0, []);

	/// <summary>Straight-line distance from which journey drivers ask the planner first; shorter hops keep
	/// their direct checked routes.</summary>
	public const float MinimumJourneyDistance = 100f;

	/// <summary>Journey-driver entry point: a level-aware, checked route that also honours the observed
	/// hostiles as hard rules, or null when the leg is short, off this planner's map, or unplannable (the
	/// driver then falls back to its ordinary route searches).</summary>
	public BotTravelPlan? PlanJourney(int mapId, BotPosition start, BotPosition destination, int botLevel,
		IReadOnlyList<BotNavigationHazard> observedHazards)
	{
		ArgumentNullException.ThrowIfNull(observedHazards);
		// 3D, like the journey drivers' own distance checks, so a leg they send is never silently declined.
		float dx = destination.X - start.X, dy = destination.Y - start.Y, dz = destination.Z - start.Z;
		if (mapId != Graph.MapId || MathF.Sqrt(dx * dx + dy * dy + dz * dz) < MinimumJourneyDistance) return null;
		BotTravelPlan plan = Plan(start, destination, botLevel, BotNavQuery.Default with { Hazards = observedHazards });
		if (plan.Route.Count == 0 ||
			observedHazards.Count > 0 && !BotNavigationGeometry.AvoidsHazards(start, plan.Route, observedHazards))
			return null;
		return plan;
	}

	/// <summary>One-line summary for bot traces.</summary>
	public static string Describe(BotTravelPlan plan) =>
		$"{plan.Route.Count} checked points, {plan.Length:F0} m, via " +
		(plan.Waypoints.Count == 0 ? "direct navmesh route" : string.Join(" > ", plan.Waypoints.Select(n => $"{n.Name}#{n.Id}"))) +
		(plan.Danger.Count == 0 ? "" : "; crosses " + string.Join(", ", plan.Danger.Select(d => $"{d.NpcId}(L{d.Level}, {d.Metres:F0} m)")));

	/// <summary>Dijkstra over the travel graph between the nodes nearest to start and goal.</summary>
	public IReadOnlyList<BotTravelNode> GraphRoute(BotPosition start, BotPosition goal, int botLevel)
	{
		List<BotTravelNode> starts = Nearest(start);
		Dictionary<int, float> goals = Nearest(goal).ToDictionary(n => n.Id, n => Horizontal(n.Position, goal));
		if (starts.Count == 0 || goals.Count == 0) return [];
		var adjacency = new Dictionary<int, List<(int To, float Cost)>>();
		foreach (BotTravelEdge edge in Graph.Edges)
		{
			float cost = EdgeCost(edge, botLevel);
			if (!adjacency.TryGetValue(edge.From, out var a)) adjacency[edge.From] = a = [];
			if (!adjacency.TryGetValue(edge.To, out var b)) adjacency[edge.To] = b = [];
			a.Add((edge.To, cost));
			b.Add((edge.From, cost));
		}
		var distance = new Dictionary<int, float>();
		var previous = new Dictionary<int, int>();
		var open = new PriorityQueue<int, float>();
		foreach (BotTravelNode node in starts)
		{
			distance[node.Id] = Horizontal(start, node.Position);
			open.Enqueue(node.Id, distance[node.Id]);
		}
		int best = -1;
		float bestCost = float.PositiveInfinity;
		while (open.TryDequeue(out int current, out float cost))
		{
			if (cost > distance[current] || cost >= bestCost) continue;
			if (goals.TryGetValue(current, out float tail) && cost + tail < bestCost) { best = current; bestCost = cost + tail; }
			foreach (var (to, step) in adjacency.GetValueOrDefault(current) ?? [])
			{
				float next = cost + step;
				if (distance.TryGetValue(to, out float known) && known <= next) continue;
				distance[to] = next;
				previous[to] = current;
				open.Enqueue(to, next);
			}
		}
		if (best < 0) return [];
		var path = new List<BotTravelNode>();
		for (int node = best; ; node = previous[node])
		{
			path.Add(Graph.Nodes[node]);
			if (!previous.ContainsKey(node)) break;
		}
		path.Reverse();
		return path;
	}

	public static float EdgeCost(BotTravelEdge edge, int botLevel) =>
		edge.Length * (1.2f - 0.2f * edge.RoadFraction) + edge.Danger.Sum(d => d.Metres * (Weight(d.Level, botLevel) - 1));

	private List<BotTravelNode> Nearest(BotPosition p) => Graph.Nodes
		.Where(n => Horizontal(n.Position, p) <= AttachRadius && MathF.Abs(n.Position.Z - p.Z) < 40)
		.OrderBy(n => Horizontal(n.Position, p)).Take(AttachNodes).ToList();

	private float GraphCost(BotPosition start, BotPosition goal, IReadOnlyList<BotTravelNode> waypoints, int botLevel)
	{
		float total = Horizontal(start, waypoints[0].Position) + Horizontal(waypoints[^1].Position, goal);
		for (int i = 1; i < waypoints.Count; i++)
		{
			BotTravelEdge? edge = Graph.Edges.FirstOrDefault(e =>
				e.From == Math.Min(waypoints[i - 1].Id, waypoints[i].Id) && e.To == Math.Max(waypoints[i - 1].Id, waypoints[i].Id));
			total += edge == null ? Horizontal(waypoints[i - 1].Position, waypoints[i].Position) : EdgeCost(edge, botLevel);
		}
		return total;
	}

	private float Cost(BotPosition start, IReadOnlyList<BotPosition> route, int botLevel) =>
		Length(start, route) * 1.2f + DangerOn(route, botLevel).Sum(d => d.Metres * (Weight(d.Level, botLevel) - 1));

	private IReadOnlyList<BotTravelDanger> DangerOn(IReadOnlyList<BotPosition> route, int botLevel) => sites
		.Where(s => s.Hostile && Weight(s.Level, botLevel) > 1 &&
			route.Any(p => Horizontal(p, s.Position) <= s.AggroRadius + 2 && MathF.Abs(p.Z - s.Position.Z) < 8))
		.GroupBy(s => s.NpcId).Select(g => new BotTravelDanger(g.Key, g.Max(s => s.Level),
			route.Count(p => g.Any(s => Horizontal(p, s.Position) <= s.AggroRadius + 2)) * BotNavigationGeometry.SampleSpacing))
		.ToArray();

	private static float Length(BotPosition start, IReadOnlyList<BotPosition> route)
	{
		float total = 0;
		BotPosition previous = start;
		foreach (BotPosition p in route) { total += Horizontal(previous, p); previous = p; }
		return total;
	}

	private static float Horizontal(BotPosition a, BotPosition b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
