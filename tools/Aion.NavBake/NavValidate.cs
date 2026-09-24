using System.Text;
using Aion.Bots.Navigation.NavMesh;
using Aion.Bots.World;

namespace Aion.NavBake;

/// <summary>
/// Compares a baked navmesh with the client's own walkability grid (<c>Levels/&lt;level&gt;/&lt;level&gt;-path.dat</c>,
/// exported as "APDG" by <c>tools/client-extract/decode_path_dat.py --bin</c>). Reports coverage in both
/// directions and whether hub NPCs that the client connects are also connected on the navmesh.
/// The client grid is ground truth for "can a character stand here"; the navmesh is additionally shrunk by
/// the agent radius, so small coverage gaps along walls are expected.
/// </summary>
internal static class NavValidate
{
	private readonly record struct Cell(short X, short Y, float Z, byte Exits);

	public static int Run(BotNavWorld world, int mapId, string navDir, string binPath, string? reportPath)
	{
		BotNavMesh mesh = new BotNavMeshSet(navDir).Get(mapId) ?? throw new InvalidOperationException("No navmesh for " + mapId);
		Cell[] cells = Read(binPath);
		var byColumn = new Dictionary<int, List<int>>();
		for (int i = 0; i < cells.Length; i++)
		{
			int key = Key(cells[i].X, cells[i].Y);
			if (!byColumn.TryGetValue(key, out var list)) byColumn[key] = list = [];
			list.Add(i);
		}
		// Direction convention: pick the mapping under which most exit bits land on an existing cell.
		(int dx, int dy)[][] conventions =
		[
			[(1, 0), (0, 1), (-1, 0), (0, -1)],
			[(-1, 0), (0, -1), (1, 0), (0, 1)],
			[(1, 0), (0, -1), (-1, 0), (0, 1)],
			[(-1, 0), (0, 1), (1, 0), (0, -1)],
		];
		var directions = conventions.OrderByDescending(c => Score(cells, byColumn, c)).First();
		int[] parent = Enumerable.Range(0, cells.Length).ToArray();
		for (int i = 0; i < cells.Length; i++)
			for (int d = 0; d < 4; d++)
			{
				if ((cells[i].Exits & (1 << d)) == 0) continue;
				int j = Neighbour(cells, byColumn, i, directions[d]);
				if (j >= 0) Union(parent, i, j);
			}
		var sizes = new Dictionary<int, int>();
		for (int i = 0; i < cells.Length; i++) { int r = Find(parent, i); sizes[r] = sizes.GetValueOrDefault(r) + 1; }
		int main = sizes.MaxBy(pair => pair.Value).Key;

		var report = new StringBuilder();
		report.AppendLine($"{mapId} {world.MapName(mapId)} vs client grid {Path.GetFileName(binPath)}: {cells.Length} cells, " +
			$"{sizes.Count} components, main {sizes[main]} cells ({sizes[main] * 0.25 / 1e6:F2} km2)");

		// Coverage: every 3rd main-component cell must have navmesh within 0.8 m vertically (1 m sideways for
		// the agent-radius erosion); count how much of the navmesh stands where the client has no cell.
		int sampled = 0, covered = 0, coveredNear = 0;
		for (int i = 0; i < cells.Length; i += 3)
		{
			if (!float.IsFinite(cells[i].Z) || Find(parent, i) != main) continue;
			sampled++;
			float x = cells[i].X * 0.5f + 0.25f, y = cells[i].Y * 0.5f + 0.25f;
			float h = mesh.HeightAt(x, y, cells[i].Z);
			if (float.IsFinite(h) && MathF.Abs(h - cells[i].Z) <= 0.8f) { covered++; coveredNear++; continue; }
			if (mesh.Snap(new BotPosition(x, y, cells[i].Z, 0), BotNavQuery.Default with { SnapHorizontal = 1f, SnapVertical = 1f }) != null)
				coveredNear++;
		}
		report.AppendLine($"  client main-component cells on the navmesh: {100.0 * covered / Math.Max(1, sampled):F1}% exact, " +
			$"{100.0 * coveredNear / Math.Max(1, sampled):F1}% within 1 m (sampled {sampled})");
		int polySamples = 0, polyOnClient = 0;
		foreach (BotNavPolygon polygon in mesh.Polygons())
		{
			if (polygon.Area == BotNavAreas.Water) continue;
			var c = new BotPosition(polygon.Ring.Average(p => p.X), polygon.Ring.Average(p => p.Y), polygon.Ring.Average(p => p.Z), 0);
			polySamples++;
			int cx = (int)MathF.Floor(c.X * 2), cy = (int)MathF.Floor(c.Y * 2);
			bool found = false;
			for (int ox = -2; ox <= 2 && !found; ox++)
				for (int oy = -2; oy <= 2 && !found; oy++)
					if (byColumn.TryGetValue(Key(cx + ox, cy + oy), out var list))
						found = list.Any(k => !float.IsFinite(cells[k].Z) || MathF.Abs(cells[k].Z - c.Z) <= 1.5f);
			if (found) polyOnClient++;
		}
		report.AppendLine($"  navmesh polygon centres on a client cell (1 m, 1.5 m vertical): {100.0 * polyOnClient / Math.Max(1, polySamples):F1}% of {polySamples}");

		// Hubs: which client component each hub stands on, and whether the navmesh agrees on connectivity.
		BotNavSite[] hubs = BotNavSites.Load(world, mapId).Where(s => s.IsHub)
			.GroupBy(s => s.NpcId).Select(g => g.First()).ToArray();
		var hubComponent = new Dictionary<int, int>();
		foreach (BotNavSite hub in hubs)
		{
			int best = -1; float bestDistance = float.MaxValue;
			int cx = (int)MathF.Floor(hub.Position.X * 2), cy = (int)MathF.Floor(hub.Position.Y * 2);
			for (int ox = -6; ox <= 6; ox++)
				for (int oy = -6; oy <= 6; oy++)
					if (byColumn.TryGetValue(Key(cx + ox, cy + oy), out var list))
						foreach (int k in list)
						{
							float dz = float.IsFinite(cells[k].Z) ? MathF.Abs(cells[k].Z - hub.Position.Z) : 0;
							if (dz > 3) continue;
							float dist = ox * ox + oy * oy + dz;
							if (dist < bestDistance) { bestDistance = dist; best = k; }
						}
			hubComponent[hub.NpcId] = best < 0 ? -1 : Find(parent, best);
		}
		var groups = hubComponent.GroupBy(pair => pair.Value).OrderByDescending(g => g.Count()).ToArray();
		foreach (var group in groups)
		{
			string label = group.Key == -1 ? "no client cell" : group.Key == main ? "main" : $"component of {sizes[group.Key]} cells";
			report.AppendLine($"  hubs on {label}: " + string.Join(", ", group.Select(p => hubs.First(h => h.NpcId == p.Key).Name + "(" + p.Key + ")")));
		}
		if (Environment.GetEnvironmentVariable("NAV_TRACE_PAIR") is string pair)
			TracePair(mesh, cells, byColumn, directions, hubs, pair, report);
		Console.Write(report);
		if (reportPath != null)
		{
			File.WriteAllText(reportPath, report.ToString());
			DiffImage(mesh, cells, parent, main, Path.ChangeExtension(reportPath, ".png"));
		}
		return 0;
	}

	/// <summary>1 px/m, rows = X, cols = Y (the render orientation). Green: client main area with navmesh;
	/// red: client main area without navmesh within 1 m; blue: navmesh where the client has no cell;
	/// dark grey: other client components.</summary>
	private static void DiffImage(BotNavMesh mesh, Cell[] cells, int[] parent, int main, string path)
	{
		const int size = 3072;
		var rgb = new byte[size * size * 3];
		var client = new bool[size * size];
		for (int i = 0; i < cells.Length; i++)
		{
			int row = cells[i].X / 2, col = cells[i].Y / 2;
			if (row < 0 || col < 0 || row >= size || col >= size) continue;
			client[row * size + col] = true;
			int o = (row * size + col) * 3;
			if (Find(parent, i) != main) { rgb[o] = rgb[o + 1] = rgb[o + 2] = 60; continue; }
			float x = row + 0.5f, y = col + 0.5f;
			float z = float.IsFinite(cells[i].Z) ? cells[i].Z : 0;
			bool on = float.IsFinite(z) && mesh.Snap(new BotPosition(x, y, z, 0),
				BotNavQuery.Default with { SnapHorizontal = 1f, SnapVertical = 1f }) != null;
			if (on) { rgb[o] = 50; rgb[o + 1] = 170; rgb[o + 2] = 70; }
			else if (rgb[o + 1] != 170) { rgb[o] = 230; rgb[o + 1] = 40; rgb[o + 2] = 40; }
		}
		foreach (BotNavPolygon polygon in mesh.Polygons())
		{
			if (polygon.Area == BotNavAreas.Water) continue;
			float minRow = polygon.Ring.Min(p => p.X), maxRow = polygon.Ring.Max(p => p.X);
			float minCol = polygon.Ring.Min(p => p.Y), maxCol = polygon.Ring.Max(p => p.Y);
			for (int row = Math.Max(0, (int)minRow); row <= Math.Min(size - 1, (int)maxRow); row++)
				for (int col = Math.Max(0, (int)minCol); col <= Math.Min(size - 1, (int)maxCol); col++)
				{
					if (client[row * size + col] || !Inside(polygon.Ring, row + 0.5f, col + 0.5f)) continue;
					int o = (row * size + col) * 3;
					rgb[o] = 40; rgb[o + 1] = 80; rgb[o + 2] = 220;
				}
		}
		PngWriter.Write(path, size, size, rgb);
	}

	/// <summary>Breadth-first client route between two hub NPCs, reporting every navmesh island change along it.</summary>
	private static void TracePair(BotNavMesh mesh, Cell[] cells, Dictionary<int, List<int>> byColumn, (int dx, int dy)[] directions,
		BotNavSite[] hubs, string pair, StringBuilder report)
	{
		int[] ids = pair.Split(',').Select(int.Parse).ToArray();
		int Nearest(BotPosition p)
		{
			int best = -1; float bestD = float.MaxValue;
			int cx = (int)(p.X * 2), cy = (int)(p.Y * 2);
			for (int ox = -8; ox <= 8; ox++) for (int oy = -8; oy <= 8; oy++)
				if (byColumn.TryGetValue(Key(cx + ox, cy + oy), out var l))
					foreach (int k in l) { float d = ox * ox + oy * oy + MathF.Abs(cells[k].Z - p.Z); if (d < bestD) { bestD = d; best = k; } }
			return best;
		}
		int from = Nearest(hubs.First(h => h.NpcId == ids[0]).Position), to = Nearest(hubs.First(h => h.NpcId == ids[1]).Position);
		var previous = new Dictionary<int, int> { [from] = -1 };
		var queue = new Queue<int>([from]);
		while (queue.Count > 0 && !previous.ContainsKey(to))
		{
			int i = queue.Dequeue();
			for (int d = 0; d < 4; d++)
			{
				if ((cells[i].Exits & (1 << d)) == 0) continue;
				int j = Neighbour(cells, byColumn, i, directions[d]);
				if (j >= 0 && previous.TryAdd(j, i)) queue.Enqueue(j);
			}
		}
		if (!previous.ContainsKey(to)) { report.AppendLine("  trace: client has no route"); return; }
		var path = new List<int>();
		for (int i = to; i != -1; i = previous[i]) path.Add(i);
		path.Reverse();
		var islands = mesh.Polygons().ToDictionary(p => p.Reference, p => p.Island);
		int lastIsland = -2;
		report.AppendLine($"  trace {pair}: client route {path.Count * 0.5:F0} m");
		for (int k = 0; k < path.Count; k += 2)
		{
			Cell c = cells[path[k]];
			var p = new BotPosition(c.X * 0.5f + 0.25f, c.Y * 0.5f + 0.25f, c.Z, 0);
			long reference = mesh.NearestPolygon(p, BotNavQuery.Default with { SnapHorizontal = 1.5f, SnapVertical = 1.5f });
			int island = reference == 0 ? -1 : islands.GetValueOrDefault(reference, -3);
			if (island != lastIsland)
				report.AppendLine($"    at {p.X:F1},{p.Y:F1},{p.Z:F1}: island {island}");
			lastIsland = island;
		}
	}

	private static bool Inside(BotPosition[] ring, float x, float y)
	{
		bool inside = false;
		for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
			if ((ring[i].Y > y) != (ring[j].Y > y) &&
				x < (ring[j].X - ring[i].X) * (y - ring[i].Y) / (ring[j].Y - ring[i].Y) + ring[i].X) inside = !inside;
		return inside;
	}

	private static int Score(Cell[] cells, Dictionary<int, List<int>> byColumn, (int dx, int dy)[] directions)
	{
		int score = 0;
		for (int i = 0; i < cells.Length; i += 97)
			for (int d = 0; d < 4; d++)
				if ((cells[i].Exits & (1 << d)) != 0 && Neighbour(cells, byColumn, i, directions[d]) >= 0) score++;
		return score;
	}

	private static int Neighbour(Cell[] cells, Dictionary<int, List<int>> byColumn, int i, (int dx, int dy) d)
	{
		if (!byColumn.TryGetValue(Key(cells[i].X + d.dx, cells[i].Y + d.dy), out var list)) return -1;
		int best = -1; float bestDz = 1.5f;
		foreach (int j in list)
		{
			float dz = float.IsFinite(cells[i].Z) && float.IsFinite(cells[j].Z) ? MathF.Abs(cells[i].Z - cells[j].Z) : 0;
			if (dz <= bestDz) { bestDz = dz; best = j; }
		}
		return best;
	}

	private static int Key(int x, int y) => (x << 16) ^ (y & 0xFFFF);

	private static int Find(int[] parent, int i)
	{
		while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; }
		return i;
	}

	private static void Union(int[] parent, int a, int b)
	{
		a = Find(parent, a); b = Find(parent, b);
		if (a != b) parent[Math.Max(a, b)] = Math.Min(a, b);
	}

	private static Cell[] Read(string path)
	{
		using var reader = new BinaryReader(File.OpenRead(path));
		if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "APDG") throw new InvalidDataException("Not an APDG export: " + path);
		reader.ReadUInt32(); reader.ReadInt32(); reader.ReadInt32(); reader.ReadSingle();
		uint count = reader.ReadUInt32();
		var cells = new Cell[count];
		for (int i = 0; i < count; i++)
		{
			short x = reader.ReadInt16(), y = reader.ReadInt16();
			float z = reader.ReadSingle();
			reader.ReadByte(); reader.ReadByte();
			byte exits = reader.ReadByte();
			reader.ReadByte();
			cells[i] = new Cell(x, y, z, exits);
		}
		return cells;
	}
}
