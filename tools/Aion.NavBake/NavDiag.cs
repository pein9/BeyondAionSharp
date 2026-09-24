using Aion.Bots.Navigation.NavMesh;
using Aion.Bots.World;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Models;

namespace Aion.NavBake;

/// <summary>Explains why the server geometry rejects a navmesh route between two NPC templates.</summary>
internal static class NavDiag
{
	/// <summary>Routes from a fixed position (NAV_FROM="x,y,z") to fixed destinations (NAV_TO="x,y,z;x,y,z").</summary>
	public static int Points(BotNavWorld world, int mapId, string navDir)
	{
		static BotPosition Parse(string text)
		{
			float[] v = text.Split(',').Select(p => float.Parse(p, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
			return new BotPosition(v[0], v[1], v[2], 0);
		}
		BotPosition from = Parse(Environment.GetEnvironmentVariable("NAV_FROM")!);
		var set = new BotNavMeshSet(navDir);
		var router = new BotNavMeshRouter(set, world.Geometry(BotNavSites.RaceFor(world, mapId))) { Log = l => Console.WriteLine("    router: " + l) };
		BotNavMesh fromMesh = set.Get(mapId)!;
		Console.WriteLine($"from {from}: snap {fromMesh.Snap(from)} island {fromMesh.IslandOf(from, BotNavQuery.Default with { SnapHorizontal = 2 })}");
		foreach (string target in Environment.GetEnvironmentVariable("NAV_TO")!.Split(';'))
		{
			BotPosition to = Parse(target);
			var route = router.FindPath(mapId, from, to);
			Console.WriteLine($"  -> {to} (island {fromMesh.IslandOf(to, BotNavQuery.Default)}, snap {fromMesh.Snap(to)}): {route.Count} points, {BotNavMeshRouter.LastOutcome}");
			var interaction = router.FindInteractionPath(mapId, from, to);
			Console.WriteLine($"     interaction: {interaction.Count} points, {BotNavMeshRouter.LastOutcome}");
			if (Environment.GetEnvironmentVariable("NAV_GRID") == "1")
			{
				var grid = router.Geometry.GridInteractionPath(mapId, from, to);
				Console.WriteLine($"     grid interaction: {grid.Count} points{(grid.Count > 0 ? $", ends {grid[^1]}" : "")}");
			}
			if (Environment.GetEnvironmentVariable("NAV_LEVEL") is string levelText)
			{
				var planner = new BotTravelPlanner(BotTravelGraph.Load(Path.Combine(navDir, mapId + ".graph.json"))!, router,
					BotNavSites.Load(world, mapId));
				var plan = planner.Plan(from, to, int.Parse(levelText, System.Globalization.CultureInfo.InvariantCulture));
				Console.WriteLine($"     plan L{levelText}: {plan.Route.Count} points, {BotNavMeshRouter.LastOutcome}; {BotTravelPlanner.Describe(plan)}");
			}
		}
		return 0;
	}

	/// <summary>Where two navmesh islands touch (NAV_FROM and NAV_TO pick them; NAV_WINDOW is the half-size of the
	/// square around NAV_TO searched, default 90 m): counts 1 m steps across the border that the server's own
	/// ground and collision check (TraceEdge) accepts. Accepted steps mean the bake split walkable ground.</summary>
	public static int Gap(BotNavWorld world, int mapId, string navDir)
	{
		static BotPosition Parse(string text)
		{
			float[] v = text.Split(',').Select(p => float.Parse(p, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
			return new BotPosition(v[0], v[1], v[2], 0);
		}
		BotPosition from = Parse(Environment.GetEnvironmentVariable("NAV_FROM")!);
		BotPosition to = Parse(Environment.GetEnvironmentVariable("NAV_TO")!);
		float window = float.Parse(Environment.GetEnvironmentVariable("NAV_WINDOW") ?? "90", System.Globalization.CultureInfo.InvariantCulture);
		var set = new BotNavMeshSet(navDir);
		BotNavMesh mesh = set.Get(mapId)!;
		var geometry = world.Geometry(BotNavSites.RaceFor(world, mapId));
		var query = BotNavQuery.Default with { SnapHorizontal = 0.6f, SnapVertical = 3 };
		int a = mesh.IslandOf(from, BotNavQuery.Default with { SnapHorizontal = 2 }), b = mesh.IslandOf(to);
		Console.WriteLine($"islands {a} -> {b}");
		var cells = new Dictionary<(int, int), (BotPosition P, int Island)>();
		for (int x = (int)(to.X - window); x <= to.X + window; x++)
			for (int y = (int)(to.Y - window); y <= to.Y + window; y++)
			{
				// Every navmesh layer at this column, near either endpoint's height band.
				foreach (float z in new[] { from.Z, to.Z, (from.Z + to.Z) / 2 })
				{
					BotPosition? snap = mesh.Snap(new BotPosition(x, y, z, 0), query with { SnapVertical = MathF.Abs(from.Z - to.Z) / 2 + 3 });
					if (snap is not BotPosition p) continue;
					int island = mesh.IslandOf(p, query);
					if (island == a || island == b) { cells[(x, y)] = (p, island); break; }
				}
			}
		int border = 0, accepted = 0;
		var rises = new List<float>();
		var examples = new List<string>();
		foreach (var ((x, y), (p, island)) in cells)
			foreach ((int dx, int dy) in new[] { (1, 0), (0, 1), (1, 1), (1, -1) })
				if (cells.TryGetValue((x + dx, y + dy), out var other) && other.Island != island)
				{
					border++;
					rises.Add(MathF.Abs(p.Z - other.P.Z));
					bool forward = geometry.TraceEdge(mapId, p, other.P) != null, back = geometry.TraceEdge(mapId, other.P, p) != null;
					if (forward || back)
					{
						accepted++;
						if (examples.Count < 12) examples.Add($"{p} <-> {other.P} fwd={forward} back={back}");
					}
				}
		Console.WriteLine($"cells {cells.Count}, border steps {border}, server-accepted {accepted}");
		if (rises.Count > 0)
		{
			rises.Sort();
			Console.WriteLine($"height change across the border per 1-1.4 m step: min {rises[0]:F2}, median {rises[rises.Count / 2]:F2}, max {rises[^1]:F2} m");
		}
		foreach (string e in examples) Console.WriteLine("  " + e);
		return 0;
	}

	/// <summary>Island of NAV_TO: its extent, and where its vertices come closest in 3D to the island of NAV_FROM
	/// (a cave or ledge the bake cut off shows its blocked entrance here).</summary>
	public static int Island(BotNavWorld world, int mapId, string navDir)
	{
		static BotPosition Parse(string text)
		{
			float[] v = text.Split(',').Select(p => float.Parse(p, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
			return new BotPosition(v[0], v[1], v[2], 0);
		}
		BotNavMesh mesh = new BotNavMeshSet(navDir).Get(mapId)!;
		BotPosition from = Parse(Environment.GetEnvironmentVariable("NAV_FROM")!);
		BotPosition to = Parse(Environment.GetEnvironmentVariable("NAV_TO")!);
		int a = mesh.IslandOf(from, BotNavQuery.Default with { SnapHorizontal = 2 }), b = mesh.IslandOf(to);
		var polygons = mesh.Polygons();
		BotPosition[] target = polygons.Where(p => p.Island == b).SelectMany(p => p.Ring).ToArray();
		Console.WriteLine($"island {b}: {polygons.Count(p => p.Island == b)} polygons, X {target.Min(v => v.X):F0}..{target.Max(v => v.X):F0}, " +
			$"Y {target.Min(v => v.Y):F0}..{target.Max(v => v.Y):F0}, Z {target.Min(v => v.Z):F1}..{target.Max(v => v.Z):F1}");
		BotPosition[] main = polygons.Where(p => p.Island == a).SelectMany(p => p.Ring)
			.Where(v => v.X > target.Min(t => t.X) - 40 && v.X < target.Max(t => t.X) + 40 &&
				v.Y > target.Min(t => t.Y) - 40 && v.Y < target.Max(t => t.Y) + 40).ToArray();
		var closest = target.SelectMany(t => main.Select(m => (t, m,
				d: MathF.Sqrt((t.X - m.X) * (t.X - m.X) + (t.Y - m.Y) * (t.Y - m.Y) + (t.Z - m.Z) * (t.Z - m.Z)))))
			.OrderBy(c => c.d).ToList();
		var shown = new List<BotPosition>();
		foreach (var (t, m, d) in closest)
		{
			if (shown.Any(p => MathF.Abs(p.X - t.X) + MathF.Abs(p.Y - t.Y) < 6)) continue;
			shown.Add(t);
			Console.WriteLine($"  {b} {t.X:F1},{t.Y:F1},{t.Z:F1}  ~ {a} {m.X:F1},{m.Y:F1},{m.Z:F1}  3D {d:F1} m");
			if (shown.Count == 12) break;
		}
		if (Environment.GetEnvironmentVariable("NAV_DUMP") is string dump)
		{
			float[] box = dump.Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
			var rows = polygons.Where(p => p.Ring.Any(v => v.X >= box[0] && v.X <= box[1] && v.Y >= box[2] && v.Y <= box[3]))
				.Select(p => new { island = p.Island, area = p.Area, ring = p.Ring.Select(v => new[] { v.X, v.Y, v.Z }).ToArray() });
			File.WriteAllText(Environment.GetEnvironmentVariable("NAV_DUMP_OUT") ?? "polygons.json", System.Text.Json.JsonSerializer.Serialize(rows));
		}
		// Other islands in the box, largest first: a cave passage can be its own scrap between the two.
		foreach (var group in polygons.Where(p => p.Island != a && p.Island != b && p.Ring.Any(v =>
				v.X > target.Min(t => t.X) - 20 && v.X < target.Max(t => t.X) + 20 && v.Y > target.Min(t => t.Y) - 20 && v.Y < target.Max(t => t.Y) + 20))
			.GroupBy(p => p.Island).OrderByDescending(g => g.Count()).Take(8))
		{
			BotPosition[] vs = group.SelectMany(p => p.Ring).ToArray();
			Console.WriteLine($"  other island {group.Key}: {group.Count()} polygons around {vs.Average(v => v.X):F0},{vs.Average(v => v.Y):F0},{vs.Average(v => v.Z):F0}");
		}
		return 0;
	}

	/// <summary>Ground profile along NAV_PATH ("x,y,z;x,y,z;..."), every metre: the surface with sloping faces
	/// allowed and with them invalidated (the bot's ground rule), and the navmesh island there.</summary>
	public static int Profile(BotNavWorld world, int mapId, string navDir)
	{
		BotNavMesh mesh = new BotNavMeshSet(navDir).Get(mapId)!;
		GeoMap map = world.Map(mapId);
		var geometry = world.Geometry(BotNavSites.RaceFor(world, mapId));
		BotPosition[] path = Environment.GetEnvironmentVariable("NAV_PATH")!.Split(';').Select(t =>
		{
			float[] v = t.Split(',').Select(p => float.Parse(p, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
			return new BotPosition(v[0], v[1], v[2], 0);
		}).ToArray();
		BotPosition? previous = null;
		for (int leg = 0; leg + 1 < path.Length; leg++)
		{
			BotPosition a = path[leg], b = path[leg + 1];
			int steps = Math.Max(1, (int)MathF.Ceiling(MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y))));
			for (int i = 0; i <= steps; i++)
			{
				float t = (float)i / steps;
				float x = a.X + (b.X - a.X) * t, y = a.Y + (b.Y - a.Y) * t, zGuess = previous?.Z ?? a.Z;
				float any = map.GetZ(x, y, zGuess + 3, zGuess - 6, 1, false);
				float flat = map.GetZ(x, y, zGuess + 3, zGuess - 6, 1, true);
				float z = float.IsNaN(any) ? zGuess : any;
				var here = new BotPosition(x, y, z, 0);
				string edge = previous is BotPosition p ? (geometry.TraceEdge(mapId, p, here) != null ? "ok" : "REJECT") : "";
				Console.WriteLine($"{x,7:F1} {y,7:F1}  any {any,7:F2}  groundRule {flat,7:F2}  island {mesh.IslandOf(here, BotNavQuery.Default with { SnapHorizontal = 0.6f, SnapVertical = 2 }),4}  {edge}");
				previous = here;
			}
		}
		return 0;
	}

	/// <summary>Every surface in the columns NAV_COLUMNS ("x,y;x,y"), top to bottom between NAV_ZTOP and NAV_ZBOTTOM.</summary>
	public static int Columns(BotNavWorld world, int mapId)
	{
		GeoMap map = world.Map(mapId);
		float top = float.Parse(Environment.GetEnvironmentVariable("NAV_ZTOP") ?? "340", System.Globalization.CultureInfo.InvariantCulture);
		float bottom = float.Parse(Environment.GetEnvironmentVariable("NAV_ZBOTTOM") ?? "240", System.Globalization.CultureInfo.InvariantCulture);
		foreach (string column in Environment.GetEnvironmentVariable("NAV_COLUMNS")!.Split(';'))
		{
			float[] v = column.Split(',').Select(p => float.Parse(p, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
			var any = new List<string>();
			for (float z = top; z > bottom;)
			{
				float hit = map.GetZ(v[0], v[1], z, bottom, 1, false);
				if (float.IsNaN(hit)) break;
				float flat = map.GetZ(v[0], v[1], hit + 0.05f, hit - 0.05f, 1, true);
				any.Add($"{hit:F2}{(float.IsNaN(flat) ? "(steep)" : "")}");
				z = hit - 0.3f;
			}
			Console.WriteLine($"{v[0]},{v[1]}: {string.Join("  ", any)}");
		}
		return 0;
	}

	public static int Run(BotNavWorld world, int mapId, string navDir, int fromNpc, int toNpc)
	{
		var set = new BotNavMeshSet(navDir);
		BotNavMesh mesh = set.Get(mapId)!;
		var sites = BotNavSites.Load(world, mapId);
		BotNavSite a = sites.First(s => s.NpcId == fromNpc), b = sites.First(s => s.NpcId == toNpc);
		BotPosition start = mesh.Snap(a.Position, BotNavQuery.Default with { SnapHorizontal = 3 }) ?? a.Position;
		var corners = mesh.FindCorners(start, b.Position);
		Console.WriteLine($"{a.Name} {a.Position} -> {b.Name} {b.Position}: start {start}, {corners.Count} corners");
		var race = BotNavSites.RaceFor(world, mapId);
		var geometry = world.Geometry(race);
		GeoMap map = world.Map(mapId);
		var router = new BotNavMeshRouter(set, geometry) { Log = line => Console.WriteLine("  router: " + line) };
		var routed = router.FindInteractionPath(mapId, start, b.Position);
		Console.WriteLine($"  router result: {routed.Count} points, {BotNavMeshRouter.LastOutcome}");
		BotPosition previous = start;
		int shown = 0;
		foreach (BotPosition corner in corners)
		{
			float dist = MathF.Sqrt((corner.X - previous.X) * (corner.X - previous.X) + (corner.Y - previous.Y) * (corner.Y - previous.Y));
			int steps = Math.Max(1, (int)MathF.Ceiling(dist / 2));
			for (int s = 1; s <= steps; s++)
			{
				float t = (float)s / steps;
				float x = previous.X + (corner.X - previous.X) * t, y = previous.Y + (corner.Y - previous.Y) * t;
				float z = s == steps ? corner.Z : mesh.HeightAt(x, y, previous.Z + (corner.Z - previous.Z) * t);
				if (!float.IsFinite(z)) z = previous.Z + (corner.Z - previous.Z) * t;
				var target = new BotPosition(x, y, z, 0);
				var edge = geometry.TraceEdge(mapId, previous, target);
				if (edge != null) { previous = edge[^1]; continue; }
				float ground = map.GetZ(x, y, z + 2, z - 2, 1, true);
				float groundAny = map.GetZ(x, y, z + 2, z - 2, 1, false);
				var hit = map.GetCollisions(previous.X, previous.Y, previous.Z + 1, x, y, (float.IsFinite(ground) ? ground : z) + 1, 1,
					CollisionIntention.DEFAULT_COLLISIONS.GetId(), IgnoreProperties.Of(race)).GetClosestCollision();
				Console.WriteLine($"  REJECT {previous} -> {target}: navZ {z:F2} groundZ {ground:F2} (no-slope-filter {groundAny:F2}) " +
					$"dz {(float.IsFinite(ground) ? ground - previous.Z : float.NaN):F2} hit {(hit == null ? "-" : hit.GetGeometry()?.GetName() + " @" + hit.GetContactPoint())}");
				previous = target with { Z = float.IsFinite(ground) ? ground : z };
				if (++shown >= 12) return 0;
			}
		}
		return 0;
	}
}
