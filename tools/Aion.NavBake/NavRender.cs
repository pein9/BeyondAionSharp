using Aion.Bots.Navigation.NavMesh;
using Aion.Bots.World;

namespace Aion.NavBake;

/// <summary>Top-down review image of a baked navmesh, one pixel per metre, rows = world X and
/// columns = world Y (the same orientation as the client's minimap art). Terrain is a grey hillshade;
/// ground is green, roads yellow, water blue, doors magenta; polygons outside the largest walkable
/// island are orange so disconnected pockets stand out. Travel-graph nodes and links are drawn in
/// white/cyan when a graph file exists.</summary>
internal static class NavRender
{
	public static int Run(BotNavWorld world, int[] maps, string navDir, string outDir)
	{
		Directory.CreateDirectory(outDir);
		var set = new BotNavMeshSet(navDir);
		foreach (int mapId in maps)
		{
			BotNavMesh? mesh = set.Get(mapId);
			if (mesh == null) { Console.WriteLine($"{mapId}: no navmesh"); continue; }
			string path = Path.Combine(outDir, mapId + ".png");
			Render(world, mesh, path, navDir);
			Console.WriteLine($"{mapId}: {path}");
		}
		return 0;
	}

	public static void Render(BotNavWorld world, BotNavMesh mesh, string path, string navDir)
	{
		int mapId = mesh.MapId;
		var input = BotNavMeshInput.Create(world.Map(mapId), world.GeoDirectory, Math.Max(world.WorldSize(mapId), 1), new HashSet<int>());
		int size = (int)MathF.Ceiling(MathF.Max(input.MaxX, input.MaxY));
		var rgb = new byte[size * size * 3];
		if (input.HasTerrain)
		{
			for (int row = 0; row < size; row++)
				for (int col = 0; col < size; col++)
				{
					int xi = row / 2, yi = col / 2;
					float z = input.TerrainSample(xi, yi), zx = input.TerrainSample(xi + 1, yi), zy = input.TerrainSample(xi, yi + 1);
					byte shade = 20;
					if (float.IsFinite(z) && float.IsFinite(zx) && float.IsFinite(zy))
						shade = (byte)Math.Clamp(70 + (zx - z) * 12 + (zy - z) * 12, 25, 120);
					Set(rgb, size, row, col, shade, shade, shade);
				}
		}
		IReadOnlyList<BotNavPolygon> polygons = mesh.Polygons();
		int[] islands = polygons.Where(p => p.Area != BotNavAreas.Water).GroupBy(p => p.Island)
			.OrderByDescending(g => g.Sum(p => Area(p.Ring))).Select(g => g.Key).Take(6).ToArray();
		int mainIsland = islands.FirstOrDefault(-1);
		(byte, byte, byte)[] palette = [(60, 170, 80), (170, 90, 200), (40, 190, 190), (200, 60, 60), (230, 230, 230), (150, 110, 60)];
		foreach (BotNavPolygon polygon in polygons)
		{
			(byte r, byte g, byte b) = polygon.Area switch
			{
				BotNavAreas.Road => ((byte)220, (byte)200, (byte)60),
				BotNavAreas.Water => ((byte)50, (byte)110, (byte)210),
				BotNavAreas.Door => ((byte)220, (byte)60, (byte)220),
				_ => Array.IndexOf(islands, polygon.Island) is int rank and >= 0 ? palette[rank] : ((byte)235, (byte)130, (byte)40),
			};
			Fill(rgb, size, polygon.Ring, r, g, b);
		}
		BotTravelGraph? graph = BotTravelGraph.Load(Path.Combine(navDir, mapId + ".graph.json"));
		if (graph != null)
		{
			foreach (BotTravelEdge edge in graph.Edges)
			{
				BotPosition a = graph.Nodes[edge.From].Position, b = graph.Nodes[edge.To].Position;
				Line(rgb, size, a, b, 0, 220, 230);
			}
			foreach (BotTravelNode node in graph.Nodes)
				for (int dr = -2; dr <= 2; dr++)
					for (int dc = -2; dc <= 2; dc++)
						Set(rgb, size, (int)node.Position.X + dr, (int)node.Position.Y + dc, 255, 255, 255);
		}
		PngWriter.Write(path, size, size, rgb);
	}

	private static float Area(BotPosition[] ring)
	{
		float area = 0;
		for (int i = 0; i < ring.Length; i++)
		{
			BotPosition a = ring[i], b = ring[(i + 1) % ring.Length];
			area += a.X * b.Y - b.X * a.Y;
		}
		return MathF.Abs(area) / 2;
	}

	private static void Fill(byte[] rgb, int size, BotPosition[] ring, byte r, byte g, byte b)
	{
		float minRow = ring.Min(p => p.X), maxRow = ring.Max(p => p.X);
		for (int row = Math.Max(0, (int)MathF.Floor(minRow)); row <= Math.Min(size - 1, (int)MathF.Ceiling(maxRow)); row++)
		{
			float y = row + 0.5f;
			float left = float.PositiveInfinity, right = float.NegativeInfinity;
			for (int i = 0; i < ring.Length; i++)
			{
				BotPosition p = ring[i], q = ring[(i + 1) % ring.Length];
				if ((p.X <= y && q.X > y) || (q.X <= y && p.X > y))
				{
					float col = p.Y + (y - p.X) / (q.X - p.X) * (q.Y - p.Y);
					left = MathF.Min(left, col);
					right = MathF.Max(right, col);
				}
			}
			if (!float.IsFinite(left)) continue;
			for (int col = Math.Max(0, (int)MathF.Floor(left)); col <= Math.Min(size - 1, (int)MathF.Ceiling(right)); col++)
				Set(rgb, size, row, col, r, g, b);
		}
	}

	private static void Line(byte[] rgb, int size, BotPosition a, BotPosition b, byte r, byte g, byte bl)
	{
		float length = MathF.Max(MathF.Abs(b.X - a.X), MathF.Abs(b.Y - a.Y));
		int steps = Math.Max(1, (int)length);
		for (int i = 0; i <= steps; i++)
		{
			float t = (float)i / steps;
			Set(rgb, size, (int)(a.X + (b.X - a.X) * t), (int)(a.Y + (b.Y - a.Y) * t), r, g, bl);
		}
	}

	private static void Set(byte[] rgb, int size, int row, int col, byte r, byte g, byte b)
	{
		if (row < 0 || col < 0 || row >= size || col >= size) return;
		int o = (row * size + col) * 3;
		rgb[o] = r; rgb[o + 1] = g; rgb[o + 2] = b;
	}
}
