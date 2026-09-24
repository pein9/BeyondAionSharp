using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aion.Bots.World;
using DotRecast.Core;
using DotRecast.Core.Numerics;
using DotRecast.Detour;
using DotRecast.Detour.Io;

namespace Aion.Bots.Navigation.NavMesh;

/// <summary>Sidecar metadata stored next to every baked navmesh (<c>&lt;mapId&gt;.navmesh.json</c>).</summary>
public sealed record BotNavMeshManifest
{
	public int MapId { get; init; }
	public string MapName { get; init; } = "";
	public int BakerVersion { get; init; }
	public string SettingsFingerprint { get; init; } = "";
	public BotNavMeshSettings Settings { get; init; } = new();
	public string SourceFingerprint { get; init; } = "";
	public float WaterLevel { get; init; }
	public int TilesX { get; init; }
	public int TilesY { get; init; }
	public int TilesWithPolygons { get; init; }
	public int Polygons { get; init; }
	public Dictionary<string, int> PolygonsByArea { get; init; } = [];
	public int Doors { get; init; }
	public int Roads { get; init; }
	/// <summary>Playable 16 m sectors from the client grid that limited the bake (0 = whole map).</summary>
	public int MaskSectors { get; init; }
	public int ModelTriangles { get; init; }
	public long TerrainTriangles { get; init; }
	public string NavMeshSha256 { get; init; } = "";

	public static readonly JsonSerializerOptions Json = new()
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.Never,
	};
}

public sealed record BotNavPolygon(long Reference, int Island, int Area, BotPosition[] Ring);

/// <summary>A cost-only danger zone (for example a static aggressive spawn above the bot's level).
/// Unlike <see cref="BotNavQuery.Hazards"/> it is never a hard rule: travel inside costs
/// <see cref="Weight"/> times more, so routes bend around it when a reasonable detour exists.</summary>
public readonly record struct BotNavDanger(float X, float Y, float Radius, float Weight);

/// <summary>Options for one route query.</summary>
public sealed record BotNavQuery
{
	public static readonly BotNavQuery Default = new();

	/// <summary>Circles the route should stay out of; entering one costs <see cref="HazardCostMultiplier"/>.</summary>
	public IReadOnlyList<BotNavigationHazard> Hazards { get; init; } = [];
	/// <summary>Soft, cost-only danger zones (see <see cref="BotNavDanger"/>).</summary>
	public IReadOnlyList<BotNavDanger> Danger { get; init; } = [];
	public float HazardCostMultiplier { get; init; } = 60f;
	/// <summary>Cost of ordinary ground relative to a mapped road (1 = no road preference).</summary>
	public float GroundCost { get; init; } = 1.25f;
	public bool AllowSwimming { get; init; }
	public bool AllowDoors { get; init; } = true;
	/// <summary>How far from the requested endpoints a navmesh polygon may be (horizontal, vertical).</summary>
	public float SnapHorizontal { get; init; } = 4f;
	public float SnapVertical { get; init; } = 6f;
}

/// <summary>A loaded, read-only Detour navmesh for one map. Queries are thread safe: each thread
/// owns its own <see cref="DtNavMeshQuery"/>, as Detour requires.</summary>
public sealed class BotNavMesh
{
	public const string FileExtension = ".navmesh";
	private const int MaxPathPolygons = 8192;
	private const int MaxStraightPoints = 2048;
	private readonly ThreadLocal<DtNavMeshQuery> queries;

	public BotNavMesh(DtNavMesh navMesh, BotNavMeshManifest manifest)
	{
		NavMesh = navMesh;
		Manifest = manifest;
		queries = new ThreadLocal<DtNavMeshQuery>(() => new DtNavMeshQuery(navMesh));
	}

	public DtNavMesh NavMesh { get; }
	public BotNavMeshManifest Manifest { get; }
	public int MapId => Manifest.MapId;

	public static string PathFor(string directory, int mapId) => Path.Combine(directory, mapId + FileExtension);
	public static string ManifestPathFor(string directory, int mapId) => PathFor(directory, mapId) + ".json";

	public static BotNavMesh Load(string directory, int mapId)
	{
		string path = PathFor(directory, mapId);
		var manifest = JsonSerializer.Deserialize<BotNavMeshManifest>(File.ReadAllText(ManifestPathFor(directory, mapId)), BotNavMeshManifest.Json)
			?? throw new InvalidDataException("Empty navmesh manifest for " + mapId);
		return new BotNavMesh(Read(File.ReadAllBytes(path), manifest.Settings.VertsPerPoly), manifest);
	}

	public static bool Exists(string directory, int mapId) =>
		File.Exists(PathFor(directory, mapId)) && File.Exists(ManifestPathFor(directory, mapId));

	/// <summary>Serialized form: gzip over DotRecast's little-endian mesh-set format.</summary>
	public static byte[] Write(DtNavMesh navMesh)
	{
		using var raw = new MemoryStream();
		using (var writer = new BinaryWriter(raw, System.Text.Encoding.UTF8, true))
			new DtMeshSetWriter().Write(writer, navMesh, RcByteOrder.LITTLE_ENDIAN, false);
		using var compressed = new MemoryStream();
		using (var gzip = new GZipStream(compressed, CompressionLevel.SmallestSize, true))
			gzip.Write(raw.GetBuffer(), 0, (int)raw.Length);
		return compressed.ToArray();
	}

	public static DtNavMesh Read(byte[] file, int vertsPerPoly)
	{
		using var gzip = new GZipStream(new MemoryStream(file), CompressionMode.Decompress);
		using var raw = new MemoryStream();
		gzip.CopyTo(raw);
		raw.Position = 0;
		using var reader = new BinaryReader(raw);
		return new DtMeshSetReader().Read(reader, vertsPerPoly);
	}

	/// <summary>Nearest walkable navmesh point to an Aion position, or null when none is within the snap box.</summary>
	public BotPosition? Snap(BotPosition position, BotNavQuery? options = null)
	{
		options ??= BotNavQuery.Default;
		var filter = new BotNavFilter(options);
		return Nearest(queries.Value!, filter, position, options, out _, out var point) ? ToAion(point, position.Heading) : null;
	}

	/// <summary>String-pulled corner points (Aion frame, excluding the start) from start to destination,
	/// or an empty list when no connected route exists. When <paramref name="allowPartial"/> is false,
	/// a route that stops short of the destination polygon is also empty.</summary>
	public IReadOnlyList<BotPosition> FindCorners(BotPosition start, BotPosition destination, BotNavQuery? options = null,
		bool allowPartial = false)
	{
		options ??= BotNavQuery.Default;
		DtNavMeshQuery query = queries.Value!;
		var filter = new BotNavFilter(options);
		if (!Nearest(query, filter, start, options, out long startRef, out RcVec3f startPoint) ||
			!Nearest(query, filter, destination, options, out long endRef, out RcVec3f endPoint))
			return [];
		Span<long> polys = new long[MaxPathPolygons];
		DtStatus status = query.FindPath(startRef, endRef, startPoint, endPoint, filter, polys, out int count, MaxPathPolygons);
		if (status.Failed() || count == 0) return [];
		if (polys[count - 1] != endRef)
		{
			if (!allowPartial) return [];
			query.ClosestPointOnPoly(polys[count - 1], endPoint, out endPoint, out _);
		}
		Span<DtStraightPath> straight = new DtStraightPath[MaxStraightPoints];
		status = query.FindStraightPath(startPoint, endPoint, polys, count, straight, out int corners, MaxStraightPoints,
			0); // corners only: crossings would add short legs; heights are re-projected by the router
		if (status.Failed() || corners == 0) return [];
		var result = new List<BotPosition>(corners);
		for (int i = 0; i < corners; i++)
		{
			var p = ToAion(straight[i].pos, destination.Heading);
			if (result.Count == 0 && Horizontal(p, start) < 0.05f) continue;
			if (result.Count > 0 && Horizontal(p, result[^1]) < 0.05f) continue;
			result.Add(p);
		}
		return result;
	}

	/// <summary>Reference of the nearest walkable polygon, or 0.</summary>
	public long NearestPolygon(BotPosition position, BotNavQuery? options = null)
	{
		options ??= BotNavQuery.Default;
		return Nearest(queries.Value!, new BotNavFilter(options), position, options, out long reference, out _) ? reference : 0;
	}

	/// <summary>True when both positions snap onto the same connected navmesh island.</summary>
	public bool Connected(BotPosition start, BotPosition destination, BotNavQuery? options = null) =>
		FindCorners(start, destination, options).Count > 0;

	/// <summary>Navmesh surface height under an Aion X/Y near a reference Z, or NaN.</summary>
	public float HeightAt(float x, float y, float nearZ)
	{
		DtNavMeshQuery query = queries.Value!;
		var options = BotNavQuery.Default with { AllowSwimming = true };
		var filter = new BotNavFilter(options);
		var center = new RcVec3f(x, nearZ, y);
		if (query.FindNearestPoly(center, new RcVec3f(0.5f, options.SnapVertical, 0.5f), filter, out long reference, out _, out bool over).Failed()
			|| reference == 0 || !over)
			return float.NaN;
		return query.GetPolyHeight(reference, center, out float height).Succeeded() ? height : float.NaN;
	}

	/// <summary>Every polygon (tile, index) with its connected-island id, area and vertex ring (Aion X/Y/Z).
	/// Islands are counted over walk links only, so a large count of small islands means holes in the bake.</summary>
	public IReadOnlyList<BotNavPolygon> Polygons()
	{
		var result = new List<BotNavPolygon>();
		var island = new Dictionary<long, int>();
		var byRef = new Dictionary<long, (DtMeshTile Tile, DtPoly Poly)>();
		for (int t = 0; t < NavMesh.GetMaxTiles(); t++)
		{
			DtMeshTile tile = NavMesh.GetTile(t);
			if (tile?.data?.header == null) continue;
			long baseRef = NavMesh.GetTileRef(tile);
			foreach (DtPoly poly in tile.data.polys) byRef[baseRef | (uint)poly.index] = (tile, poly);
		}
		int next = 0;
		var stack = new Stack<long>();
		foreach (long start in byRef.Keys.Order())
		{
			if (island.ContainsKey(start)) continue;
			int id = next++;
			island[start] = id;
			stack.Push(start);
			while (stack.Count > 0)
			{
				long current = stack.Pop();
				var (tile, poly) = byRef[current];
				for (int link = poly.firstLink; link != DtDetour.DT_NULL_LINK; link = tile.links[link].next)
				{
					long neighbour = tile.links[link].refs;
					if (neighbour == 0 || island.ContainsKey(neighbour) || !byRef.ContainsKey(neighbour)) continue;
					island[neighbour] = id;
					stack.Push(neighbour);
				}
			}
		}
		foreach (var (reference, (tile, poly)) in byRef.OrderBy(pair => pair.Key))
		{
			var ring = new BotPosition[poly.vertCount];
			for (int i = 0; i < poly.vertCount; i++)
			{
				int v = poly.verts[i] * 3;
				ring[i] = new BotPosition(tile.data.verts[v], tile.data.verts[v + 2], tile.data.verts[v + 1], 0);
			}
			result.Add(new BotNavPolygon(reference, island[reference], poly.GetArea(), ring));
		}
		return result;
	}

	private static bool Nearest(DtNavMeshQuery query, BotNavFilter filter, BotPosition position, BotNavQuery options,
		out long reference, out RcVec3f point)
	{
		var center = new RcVec3f(position.X, position.Z, position.Y);
		DtStatus status = query.FindNearestPoly(center, new RcVec3f(options.SnapHorizontal, options.SnapVertical, options.SnapHorizontal),
			filter, out reference, out point, out _);
		return status.Succeeded() && reference != 0;
	}

	internal static BotPosition ToAion(RcVec3f point, byte heading) => new(point.X, point.Z, point.Y, heading);

	private static float Horizontal(BotPosition a, BotPosition b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

	/// <summary>Area costs plus a penalty for travel inside observed aggro circles.</summary>
	private sealed class BotNavFilter : IDtQueryFilter
	{
		private readonly float[] areaCost = new float[64];
		private readonly int include;
		private readonly int exclude;
		private readonly IReadOnlyList<BotNavigationHazard> hazards;
		private readonly float hazardMultiplier;
		private readonly Dictionary<long, List<BotNavDanger>>? danger;
		private const float DangerCell = 32f;

		public BotNavFilter(BotNavQuery options)
		{
			Array.Fill(areaCost, 1f);
			areaCost[BotNavAreas.Ground] = options.GroundCost;
			areaCost[BotNavAreas.Road] = 1f;
			areaCost[BotNavAreas.Water] = 4f;
			areaCost[BotNavAreas.Door] = options.GroundCost;
			include = BotNavAreas.FlagWalk | (options.AllowSwimming ? BotNavAreas.FlagSwim : 0);
			exclude = options.AllowDoors ? 0 : BotNavAreas.FlagDoor;
			hazards = options.Hazards;
			hazardMultiplier = options.HazardCostMultiplier;
			if (options.Danger.Count > 0)
			{
				danger = [];
				foreach (BotNavDanger zone in options.Danger)
					for (int cx = Cell(zone.X - zone.Radius); cx <= Cell(zone.X + zone.Radius); cx++)
						for (int cy = Cell(zone.Y - zone.Radius); cy <= Cell(zone.Y + zone.Radius); cy++)
						{
							long key = ((long)cx << 32) ^ (uint)cy;
							if (!danger.TryGetValue(key, out var list)) danger[key] = list = [];
							list.Add(zone);
						}
			}
		}

		private static int Cell(float value) => (int)MathF.Floor(value / DangerCell);

		private float DangerFactor(float x, float y)
		{
			if (danger == null || !danger.TryGetValue(((long)Cell(x) << 32) ^ (uint)Cell(y), out var zones)) return 1;
			float factor = 1;
			foreach (BotNavDanger zone in zones)
			{
				float dx = x - zone.X, dy = y - zone.Y;
				if (dx * dx + dy * dy < zone.Radius * zone.Radius) factor = MathF.Max(factor, zone.Weight);
			}
			return factor;
		}

		public bool PassFilter(long refs, DtMeshTile tile, DtPoly poly) =>
			(poly.flags & include) != 0 && (poly.flags & exclude) == 0;

		public float GetCost(RcVec3f pa, RcVec3f pb, long prevRef, DtMeshTile prevTile, DtPoly prevPoly, long curRef,
			DtMeshTile curTile, DtPoly curPoly, long nextRef, DtMeshTile nextTile, DtPoly nextPoly)
		{
			float cost = RcVec3f.Distance(pa, pb) * areaCost[curPoly.GetArea()];
			if (danger != null)
			{
				int steps = Math.Max(1, (int)MathF.Ceiling(RcVec3f.Distance(pa, pb) / 2f));
				float factor = 0;
				for (int i = 0; i <= steps; i++)
				{
					float t = (float)i / steps;
					factor += DangerFactor(pa.X + (pb.X - pa.X) * t, pa.Z + (pb.Z - pa.Z) * t);
				}
				cost *= factor / (steps + 1);
			}
			if (hazards.Count == 0) return cost;
			// Sample the portal-to-portal segment; each sample inside a circle adds the penalty share.
			int samples = Math.Max(1, (int)MathF.Ceiling(RcVec3f.Distance(pa, pb) / 2f));
			int inside = 0;
			for (int i = 0; i <= samples; i++)
			{
				float t = (float)i / samples;
				float x = pa.X + (pb.X - pa.X) * t, y = pa.Z + (pb.Z - pa.Z) * t;
				foreach (BotNavigationHazard hazard in hazards)
				{
					float dx = x - hazard.Position.X, dy = y - hazard.Position.Y;
					if (dx * dx + dy * dy < hazard.Radius * hazard.Radius) { inside++; break; }
				}
			}
			return inside == 0 ? cost : cost * (1 + hazardMultiplier * inside / (samples + 1f));
		}
	}
}
