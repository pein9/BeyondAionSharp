using System.Diagnostics;
using System.Text;
using Aion.Bots.Navigation;
using Aion.Bots.Navigation.NavMesh;
using Aion.Bots.World;

namespace Aion.NavBake;

/// <summary>Routes between every pair of hub NPCs on each map with the navmesh router and reports
/// coverage, timing and failure reasons. With <c>--legacy</c> it also times the grid search on the
/// same legs for comparison (slow: minutes per map).</summary>
internal static class NavMeasure
{
	public static int Run(BotNavWorld world, int[] maps, string navDir, bool legacy, string? reportPath, int limit = int.MaxValue, bool quick = false)
	{
		var set = new BotNavMeshSet(navDir);
		var report = new StringBuilder();
		int failures = 0;
		foreach (int mapId in maps)
		{
			if (set.Get(mapId) == null) { Console.WriteLine($"{mapId}: no navmesh"); continue; }
			var geometry = world.Geometry(BotNavSites.RaceFor(world, mapId));
			var router = new BotNavMeshRouter(set, geometry);
			BotNavSite[] hubs = BotNavSites.Load(world, mapId).Where(s => s.IsHub)
				.GroupBy(s => s.NpcId).Select(g => g.First()).OrderBy(s => s.NpcId).Take(limit).ToArray();
			int routed = 0, total = 0;
			var times = new List<double>();
			var reasons = new Dictionary<string, int>();
			var failed = new List<string>();
			float detour = 0;
			foreach (BotNavSite from in hubs)
			{
				BotPosition start = set.Get(mapId)!.Snap(from.Position, BotNavQuery.Default with { SnapHorizontal = 3 }) ?? from.Position;
				foreach (BotNavSite to in hubs)
				{
					if (to.NpcId <= from.NpcId) continue;
					total++;
					var watch = Stopwatch.StartNew();
					bool ok;
					IReadOnlyList<BotPosition> route = [];
					if (quick)
					{
						var corners = set.Get(mapId)!.FindCorners(start, to.Position, BotNavQuery.Default with { SnapHorizontal = 6 }, allowPartial: true);
						ok = corners.Count > 0 && Horizontal(corners[^1], to.Position) <= 4;
					}
					else
					{
						route = router.FindInteractionPath(mapId, start, to.Position);
						ok = BotNavMeshRouter.LastOutcome == BotNavRouteOutcome.Routed;
					}
					times.Add(watch.Elapsed.TotalMilliseconds);
					if (ok)
					{
						routed++;
						if (route.Count > 0) detour += Length(start, route) / MathF.Max(1, Horizontal(start, to.Position));
						continue;
					}
					string reason = quick ? "NotConnected" : BotNavMeshRouter.LastOutcome.ToString();
					reasons[reason] = reasons.GetValueOrDefault(reason) + 1;
					failed.Add($"{from.Name}({from.NpcId}) -> {to.Name}({to.NpcId}) {Horizontal(from.Position, to.Position):F0} m: {reason}");
				}
			}
			times.Sort();
			string line = $"{mapId} {world.MapName(mapId)}: {routed}/{total} hub pairs routed; " +
				$"median {Percentile(times, 0.5):F1} ms, p95 {Percentile(times, 0.95):F1} ms, max {times.LastOrDefault():F0} ms; " +
				$"mean path/straight {(routed == 0 ? 0 : detour / routed):F2}";
			Console.WriteLine(line);
			report.AppendLine(line);
			foreach (var (reason, count) in reasons.OrderByDescending(r => r.Value)) report.AppendLine($"  {reason}: {count}");
			foreach (string f in failed.Take(60)) report.AppendLine("  " + f);
			failures += total - routed;
			if (legacy) Legacy(world, mapId, router, hubs, report);
		}
		if (reportPath != null) File.WriteAllText(reportPath, report.ToString());
		else Console.Write(report);
		return 0;
	}

	/// <summary>Journey-style legs with every aggressive static spawn near the straight line treated as an
	/// observed aggro circle (hard rule), timed for the navmesh router and, with legacy, the grid search.</summary>
	public static int Hazards(BotNavWorld world, int mapId, string navDir, bool legacy, int limit)
	{
		var set = new BotNavMeshSet(navDir);
		var geometry = world.Geometry(BotNavSites.RaceFor(world, mapId));
		var router = new BotNavMeshRouter(set, geometry);
		var sites = BotNavSites.Load(world, mapId);
		BotNavSite[] hubs = sites.Where(s => s.IsHub).GroupBy(s => s.NpcId).Select(g => g.First()).OrderBy(s => s.NpcId).ToArray();
		BotNavSite[] hostile = sites.Where(s => s.Attackable).ToArray();
		var rng = new Random(7);
		int routed = 0, total = 0, gridRouted = 0;
		double navTotal = 0, gridTotal = 0, navMax = 0, gridMax = 0;
		var reasons = new Dictionary<string, int>();
		for (int i = 0; i < limit; i++)
		{
			BotNavSite a = hubs[rng.Next(hubs.Length)], b = hubs[rng.Next(hubs.Length)];
			if (a == b || Horizontal(a.Position, b.Position) > 400) { i--; continue; }
			BotPosition start = set.Get(mapId)!.Snap(a.Position, BotNavQuery.Default with { SnapHorizontal = 3 }) ?? a.Position;
			BotPosition goal = set.Get(mapId)!.Snap(b.Position, BotNavQuery.Default with { SnapHorizontal = 3 }) ?? b.Position;
			var hazards = hostile.Where(h => SegmentDistance(h.Position, start, goal) < 40 &&
					Horizontal(h.Position, start) > h.AggroRadius + 2 && Horizontal(h.Position, goal) > h.AggroRadius + 2)
				.Select(h => new BotNavigationHazard(h.Position, h.AggroRadius)).ToArray();
			total++;
			var watch = Stopwatch.StartNew();
			var route = router.FindPath(mapId, start, goal, BotNavQuery.Default with { Hazards = hazards });
			double ms = watch.Elapsed.TotalMilliseconds;
			navTotal += ms; navMax = Math.Max(navMax, ms);
			if (route.Count > 0) routed++;
			else reasons[BotNavMeshRouter.LastOutcome.ToString()] = reasons.GetValueOrDefault(BotNavMeshRouter.LastOutcome.ToString()) + 1;
			if (route.Count > 0) Check(geometry, mapId, start, route, hazards);
			else if (Environment.GetEnvironmentVariable("NAV_LOG") == "1")
			{
				router.Log = line => Console.WriteLine("    router: " + line);
				router.FindPath(mapId, start, goal, BotNavQuery.Default with { Hazards = hazards });
				router.Log = null;
			}
			string gridText = "";
			if (legacy)
			{
				watch.Restart();
				var grid = geometry.GridJourneyPathAvoiding(mapId, start, goal, hazards);
				double gms = watch.Elapsed.TotalMilliseconds;
				gridTotal += gms; gridMax = Math.Max(gridMax, gms);
				if (grid.Count > 0) gridRouted++;
				gridText = $" | grid {(grid.Count > 0 ? "ok" : "none")} {gms:F0} ms";
			}
			Console.WriteLine($"  {a.Name} -> {b.Name} {Horizontal(start, goal):F0} m, {hazards.Length} hazards: navmesh {(route.Count > 0 ? "ok" : BotNavMeshRouter.LastOutcome.ToString())} {ms:F0} ms{gridText}");
		}
		Console.WriteLine($"{mapId}: hazard legs routed {routed}/{total}, mean {navTotal / Math.Max(1, total):F0} ms, max {navMax:F0} ms " +
			string.Join(", ", reasons.Select(r => $"{r.Key} {r.Value}")) +
			(legacy ? $"; grid routed {gridRouted}/{total}, mean {gridTotal / Math.Max(1, total):F0} ms, max {gridMax:F0} ms" : ""));
		return 0;
	}

	/// <summary>Every emitted step must pass the ground check and the observed-hazard rule.</summary>
	private static void Check(BotNavigationGeometry geometry, int mapId, BotPosition start, IReadOnlyList<BotPosition> route,
		IReadOnlyList<BotNavigationHazard> hazards)
	{
		if (!BotNavigationGeometry.AvoidsHazards(start, route, hazards)) throw new InvalidOperationException("route breaks the hazard rule");
		BotPosition previous = start;
		foreach (BotPosition p in route)
		{
			if (geometry.TraceEdge(mapId, previous, p) == null) throw new InvalidOperationException($"unchecked step {previous} -> {p}");
			previous = p;
		}
	}

	private static float SegmentDistance(BotPosition p, BotPosition a, BotPosition b)
	{
		float dx = b.X - a.X, dy = b.Y - a.Y, l = dx * dx + dy * dy;
		float t = l <= 0 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l, 0, 1);
		return Horizontal(p, new BotPosition(a.X + t * dx, a.Y + t * dy, 0, 0));
	}

	private static void Legacy(BotNavWorld world, int mapId, BotNavMeshRouter router, BotNavSite[] hubs, StringBuilder report)
	{
		var rng = new Random(mapId);
		for (int i = 0; i < 12; i++)
		{
			BotNavSite a = hubs[rng.Next(hubs.Length)], b = hubs[rng.Next(hubs.Length)];
			if (a == b) continue;
			BotPosition start = router.NavMeshes.Get(mapId)!.Snap(a.Position) ?? a.Position;
			var watch = Stopwatch.StartNew();
			int nav = router.FindInteractionPath(mapId, start, b.Position).Count;
			double navMs = watch.Elapsed.TotalMilliseconds;
			watch.Restart();
			int grid = router.Geometry.FindInteractionPath(mapId, start, b.Position).Count;
			double gridMs = watch.Elapsed.TotalMilliseconds;
			string line = $"  legacy {a.Name} -> {b.Name} {Horizontal(a.Position, b.Position):F0} m: navmesh {nav} pts {navMs:F0} ms | grid {grid} pts {gridMs:F0} ms";
			Console.WriteLine(line);
			report.AppendLine(line);
		}
	}

	private static double Percentile(List<double> sorted, double p) => sorted.Count == 0 ? 0 : sorted[(int)Math.Min(sorted.Count - 1, p * sorted.Count)];

	private static float Length(BotPosition start, IReadOnlyList<BotPosition> route)
	{
		float total = 0;
		BotPosition previous = start;
		foreach (BotPosition p in route) { total += Horizontal(previous, p); previous = p; }
		return total;
	}

	private static float Horizontal(BotPosition a, BotPosition b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
