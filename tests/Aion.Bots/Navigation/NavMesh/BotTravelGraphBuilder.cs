using System.Collections.Concurrent;
using Aion.Bots.World;
using Aion.GameServer.Model;

namespace Aion.Bots.Navigation.NavMesh;

/// <summary>Builds a <see cref="BotTravelGraph"/> from static data, mapped roads and a baked navmesh.</summary>
public static class BotTravelGraphBuilder
{
	private const float MergeRadius = 8f;
	private const float GatherCluster = 40f;
	private const float RoadSpacing = 120f;
	private const float NeighbourRadius = 350f;
	private const int Neighbours = 8;
	private const float SnapRadius = 6f;

	public static BotTravelGraph Build(BotNavWorld world, int mapId, BotNavMesh mesh, BotNavMeshRouter router,
		Action<string>? log = null)
	{
		IReadOnlyList<BotNavSite> sites = BotNavSites.Load(world, mapId);
		BotNavRoadFile? roads = BotNavRoads.Load(world.RepoRoot, mapId);
		var candidates = new List<(BotTravelNodeKind Kind, string Name, int Template, BotPosition Position, int Priority)>();

		foreach (BotNavSite hub in sites.Where(s => s.IsHub).GroupBy(s => (s.NpcId, (int)(s.Position.X / 30), (int)(s.Position.Y / 30))).Select(g => g.First()))
			candidates.Add((hub.Kind, hub.Name, hub.NpcId, hub.Position, 0));
		foreach (var cluster in sites.Where(s => s.Kind == BotTravelNodeKind.Gather)
			.GroupBy(s => ((int)(s.Position.X / GatherCluster), (int)(s.Position.Y / GatherCluster))))
		{
			var centre = new BotPosition(cluster.Average(s => s.Position.X), cluster.Average(s => s.Position.Y),
				cluster.Average(s => s.Position.Z), 0);
			int template = cluster.GroupBy(s => s.NpcId).OrderByDescending(g => g.Count()).First().Key;
			candidates.Add((BotTravelNodeKind.Gather, $"gather {template} x{cluster.Count()}", template, centre, 1));
		}
		if (roads != null)
		{
			var endCounts = new Dictionary<(float, float), int>();
			foreach (BotNavRoad road in roads.Roads)
				foreach (float[] end in new[] { road.Points[0], road.Points[^1] })
					endCounts[(end[0], end[1])] = endCounts.GetValueOrDefault((end[0], end[1])) + 1;
			foreach (BotNavRoad road in roads.Roads)
			{
				float travelled = 0;
				for (int i = 0; i < road.Points.Length; i++)
				{
					float[] p = road.Points[i];
					if (i > 0) travelled += MathF.Sqrt(MathF.Pow(p[0] - road.Points[i - 1][0], 2) + MathF.Pow(p[1] - road.Points[i - 1][1], 2));
					bool end = i == 0 || i == road.Points.Length - 1;
					bool junction = end && endCounts.GetValueOrDefault((p[0], p[1])) >= 2;
					if (end || junction || travelled >= RoadSpacing)
					{
						if (!end) travelled = 0;
						candidates.Add((BotTravelNodeKind.Road, junction ? "road junction" : "road", 0, new BotPosition(p[0], p[1], float.NaN, 0), 2));
					}
				}
			}
		}

		// Snap to the walkable surface; merge near-duplicates keeping the most specific kind.
		var nodes = new List<BotTravelNode>();
		foreach (var c in candidates.OrderBy(c => c.Priority).ThenBy(c => c.Template).ThenBy(c => c.Position.X).ThenBy(c => c.Position.Y))
		{
			BotPosition? snapped = SnapNode(mesh, c.Position);
			if (snapped is not BotPosition ground) continue;
			if (nodes.Any(n => Horizontal(n.Position, ground) < MergeRadius && MathF.Abs(n.Position.Z - ground.Z) < 4)) continue;
			nodes.Add(new BotTravelNode(nodes.Count, c.Kind, c.Name, c.Template, ground));
		}
		log?.Invoke($"{mapId}: {nodes.Count} nodes from {candidates.Count} candidates");

		// Candidate links: nearest neighbours; each must pass the checked router and stay local.
		var pairs = new HashSet<(int, int)>();
		foreach (BotTravelNode node in nodes)
			foreach (BotTravelNode other in nodes.Where(o => o.Id != node.Id && Horizontal(o.Position, node.Position) <= NeighbourRadius)
				.OrderBy(o => Horizontal(o.Position, node.Position)).Take(Neighbours))
				pairs.Add((Math.Min(node.Id, other.Id), Math.Max(node.Id, other.Id)));
		BotNavSite[] hostile = sites.Where(s => s.Hostile).ToArray();
		var edges = new ConcurrentBag<BotTravelEdge>();
		Parallel.ForEach(pairs.OrderBy(p => p.Item1).ThenBy(p => p.Item2), pair =>
		{
			BotTravelNode a = nodes[pair.Item1], b = nodes[pair.Item2];
			IReadOnlyList<BotPosition> route = router.FindPath(mapId, a.Position, b.Position, BotNavQuery.Default with { GroundCost = 1f });
			if (route.Count == 0) return;
			float length = Length(a.Position, route), straight = Horizontal(a.Position, b.Position);
			if (length > straight * 1.6f + 30) return; // a detour: other links cover it
			edges.Add(Annotate(a.Id, b.Id, route, length, roads, hostile));
		});
		var edgeList = edges.OrderBy(e => e.From).ThenBy(e => e.To).ToList();
		log?.Invoke($"{mapId}: {edgeList.Count}/{pairs.Count} candidate links verified");

		// Every node joins the largest network if any checked route exists.
		int[] component = Components(nodes.Count, edgeList);
		int main = component.GroupBy(c => c).OrderByDescending(g => g.Count()).First().Key;
		foreach (BotTravelNode orphan in nodes.Where(n => component[n.Id] != main).ToArray())
		{
			if (component[orphan.Id] == main) continue;
			foreach (BotTravelNode target in nodes.Where(n => component[n.Id] == main)
				.OrderBy(n => Horizontal(n.Position, orphan.Position)).Take(4))
			{
				IReadOnlyList<BotPosition> route = router.FindPath(mapId, orphan.Position, target.Position, BotNavQuery.Default with { GroundCost = 1f });
				if (route.Count == 0) continue;
				edgeList.Add(Annotate(Math.Min(orphan.Id, target.Id), Math.Max(orphan.Id, target.Id), route,
					Length(orphan.Position, route), roads, hostile));
				component = Components(nodes.Count, edgeList);
				break;
			}
		}
		// Road points exist only to shape the network; a road scrap that never joined it (painted rocks,
		// rooftops the art happens to colour) is dropped. Hubs and gather clusters are kept even if isolated.
		int mainRoot = component.GroupBy(c => c).OrderByDescending(g => g.Count()).First().Key;
		var keep = nodes.Where(n => n.Kind != BotTravelNodeKind.Road || component[n.Id] == mainRoot).ToList();
		var remap = keep.Select((n, index) => (n.Id, index)).ToDictionary(p => p.Id, p => p.index);
		nodes = keep.Select(n => n with { Id = remap[n.Id] }).ToList();
		edgeList = edgeList.Where(e => remap.ContainsKey(e.From) && remap.ContainsKey(e.To))
			.Select(e => e with { From = Math.Min(remap[e.From], remap[e.To]), To = Math.Max(remap[e.From], remap[e.To]) }).ToList();
		component = Components(nodes.Count, edgeList);
		int connected = component.GroupBy(c => c).Max(g => g.Count());
		log?.Invoke($"{mapId}: {edgeList.Count} links; {connected}/{nodes.Count} nodes on the main network");

		return new BotTravelGraph
		{
			MapId = mapId,
			MapName = world.MapName(mapId),
			NavMeshSha256 = mesh.Manifest.NavMeshSha256,
			Nodes = nodes,
			Edges = edgeList.OrderBy(e => e.From).ThenBy(e => e.To).ToList(),
			Exits = Exits(world, mapId, nodes, sites),
		};
	}

	private static BotPosition? SnapNode(BotNavMesh mesh, BotPosition position)
	{
		if (float.IsNaN(position.Z))
		{
			// Road points have no height: take the highest walkable surface under them within the map range.
			foreach (float z in new[] { 600f, 450f, 350f, 300f, 250f, 200f, 150f, 100f, 50f })
				if (mesh.Snap(position with { Z = z }, BotNavQuery.Default with { SnapHorizontal = SnapRadius, SnapVertical = 60f }) is BotPosition p)
					return p;
			return null;
		}
		return mesh.Snap(position, BotNavQuery.Default with { SnapHorizontal = SnapRadius, SnapVertical = 4f });
	}

	private static BotTravelEdge Annotate(int from, int to, IReadOnlyList<BotPosition> route, float length,
		BotNavRoadFile? roads, BotNavSite[] hostile)
	{
		int onRoad = route.Count(p => BotNavRoads.Distance(roads, p) <= 5);
		var danger = new Dictionary<int, (int Level, float Metres)>();
		foreach (BotPosition p in route)
			foreach (BotNavSite site in hostile)
			{
				if (Horizontal(site.Position, p) > site.AggroRadius + 2 || MathF.Abs(site.Position.Z - p.Z) > 8) continue;
				var current = danger.GetValueOrDefault(site.NpcId);
				danger[site.NpcId] = (Math.Max(current.Level, site.Level), current.Metres + BotNavigationGeometry.SampleSpacing);
			}
		return new BotTravelEdge(from, to, MathF.Round(length, 1), route.Count == 0 ? 0 : MathF.Round((float)onRoad / route.Count, 2),
			danger.OrderBy(d => d.Key).Select(d => new BotTravelDanger(d.Key, d.Value.Level, d.Value.Metres)).ToArray());
	}

	/// <summary>Teleports offered by portal NPCs near a node, read from the shipped
	/// <c>portals/portal_template2.xml</c> (the loaded holder keeps them private after unmarshalling).</summary>
	private static List<BotTravelExit> Exits(BotNavWorld world, int mapId, List<BotTravelNode> nodes, IReadOnlyList<BotNavSite> sites)
	{
		var exits = new List<BotTravelExit>();
		string raceName = BotNavSites.RaceFor(world, mapId).ToString();
		string path = Path.Combine(world.RepoRoot, "game-server", "data", "static_data", "portals", "portal_template2.xml");
		if (!File.Exists(path)) return exits;
		var document = System.Xml.Linq.XDocument.Load(path);
		foreach (var portal in document.Root!.Elements().Where(e => e.Name.LocalName is "portal_use" or "portal_dialog"))
		{
			int npcId = (int?)portal.Attribute("npc_id") ?? 0;
			BotNavSite? site = sites.FirstOrDefault(s => s.NpcId == npcId);
			if (site == null) continue;
			BotTravelNode? from = nodes.OrderBy(n => Horizontal(n.Position, site.Position))
				.FirstOrDefault(n => Horizontal(n.Position, site.Position) <= 15);
			if (from == null) continue;
			foreach (var portalPath in portal.Elements("portal_path"))
			{
				string race = (string?)portalPath.Attribute("race") ?? "PC_ALL";
				if (race != "PC_ALL" && race != raceName) continue;
				var loc = world.Data.PortalLocs.GetPortalLoc((int?)portalPath.Attribute("loc_id") ?? 0);
				if (loc == null) continue;
				exits.Add(new BotTravelExit(from.Id, npcId, loc.LocId, loc.WorldId, new BotPosition(loc.X, loc.Y, loc.Z, loc.Heading),
					(int?)portalPath.Attribute("min_level") ?? 0, (int?)portalPath.Attribute("kinah") ?? 0));
			}
		}
		return exits.OrderBy(e => e.From).ThenBy(e => e.LocId).ToList();
	}

	private static int[] Components(int count, IReadOnlyList<BotTravelEdge> edges)
	{
		int[] parent = Enumerable.Range(0, count).ToArray();
		int Find(int i) { while (parent[i] != i) i = parent[i] = parent[parent[i]]; return i; }
		foreach (BotTravelEdge edge in edges) parent[Find(edge.From)] = Find(edge.To);
		return Enumerable.Range(0, count).Select(Find).ToArray();
	}

	private static float Length(BotPosition start, IReadOnlyList<BotPosition> route)
	{
		float total = 0;
		BotPosition previous = start;
		foreach (BotPosition p in route) { total += Horizontal(previous, p); previous = p; }
		return total;
	}

	private static float Horizontal(BotPosition a, BotPosition b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
