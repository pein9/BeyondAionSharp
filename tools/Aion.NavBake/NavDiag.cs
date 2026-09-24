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
		Console.WriteLine($"from {from}: snap {set.Get(mapId)!.Snap(from)}");
		foreach (string target in Environment.GetEnvironmentVariable("NAV_TO")!.Split(';'))
		{
			BotPosition to = Parse(target);
			var route = router.FindPath(mapId, from, to);
			Console.WriteLine($"  -> {to}: {route.Count} points, {BotNavMeshRouter.LastOutcome}");
			var interaction = router.FindInteractionPath(mapId, from, to);
			Console.WriteLine($"     interaction: {interaction.Count} points, {BotNavMeshRouter.LastOutcome}");
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
