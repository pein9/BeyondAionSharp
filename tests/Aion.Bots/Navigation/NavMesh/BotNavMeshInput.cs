using System.Globalization;
using Aion.GameServer.GeoEngine;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.GeoEngine.Models;
using Aion.GameServer.GeoEngine.Scene;
using DotRecast.Recast;

namespace Aion.Bots.Navigation.NavMesh;

/// <summary>A 2D footprint (Aion X/Y) with a vertical range, used to mark doors and roads.</summary>
public sealed record BotNavVolume(float[] Footprint, float MinZ, float MaxZ, int Area, string Name);

/// <summary>
/// The static collision world of one map, flattened for Recast. Uses the same terrain heightmap and the
/// same placed meshes the server's <see cref="GeoMap"/> ray casts against, with the same rules:
/// only <c>PHYSICAL</c> surfaces of at most 45 degrees are ground (Java <c>GeoMap.getZ</c> with
/// sloping-surface invalidation), every <c>DEFAULT_COLLISIONS</c> surface blocks movement, and
/// despawnable geometry follows its default server state. Coordinates are stored in Recast's
/// Y-up frame: recast (x, y, z) = aion (x, z, y).
/// </summary>
public sealed class BotNavMeshInput
{
	private const int BucketSize = 32;
	private const int HeightmapUnit = 2;
	private static readonly double MaxSlopeRadians = 45.0 / 180.0 * Math.PI;

	private readonly short[]? heightmap;
	private readonly int heightmapSize;
	private readonly float[] modelVertices;
	private readonly int[] modelAreas;
	private readonly Dictionary<long, int[]> buckets;

	private BotNavMeshInput(int mapId, short[]? heightmap, int heightmapSize, float[] modelVertices,
		int[] modelAreas, Dictionary<long, int[]> buckets, IReadOnlyList<BotNavVolume> doors,
		float minX, float minY, float maxX, float maxY, float minZ, float maxZ, BotNavMeshInputStats stats)
	{
		MapId = mapId;
		this.heightmap = heightmap;
		this.heightmapSize = heightmapSize;
		this.modelVertices = modelVertices;
		this.modelAreas = modelAreas;
		this.buckets = buckets;
		Doors = doors;
		MinX = minX; MinY = minY; MaxX = maxX; MaxY = maxY; MinZ = minZ; MaxZ = maxZ;
		Stats = stats;
	}

	public int MapId { get; }
	public IReadOnlyList<BotNavVolume> Doors { get; }
	public float MinX { get; }
	public float MinY { get; }
	public float MaxX { get; }
	public float MaxY { get; }
	public float MinZ { get; }
	public float MaxZ { get; }
	public BotNavMeshInputStats Stats { get; }
	public bool HasTerrain => heightmap != null;

	/// <summary>Collects the map's static geometry. <paramref name="activeStaticIds"/> are the placeable
	/// objects the server spawns by default (static ids of the map's spawn templates).</summary>
	public static BotNavMeshInput Create(GeoMap map, string geoDirectory, int worldSize, IReadOnlySet<int> activeStaticIds)
	{
		ArgumentNullException.ThrowIfNull(map);
		(short[]? heights, int size) = LoadHeightmap(geoDirectory, map.GetMapId());
		return Create(map, heights, size, worldSize, activeStaticIds);
	}

	/// <summary>Same as the file-based overload with an explicit square heightmap (raw 16-bit samples as the
	/// server's <see cref="Terrain"/> stores them, index <c>y + x * size</c>), or none.</summary>
	public static BotNavMeshInput Create(GeoMap map, short[]? heights, int size, int worldSize, IReadOnlySet<int> activeStaticIds)
	{
		ArgumentNullException.ThrowIfNull(map);
		ArgumentNullException.ThrowIfNull(activeStaticIds);

		var vertices = new List<float>(1 << 20);
		var areas = new List<int>(1 << 18);
		var doors = new List<BotNavVolume>();
		int walkable = 0, obstacles = 0, skippedDynamic = 0, skippedIntentions = 0;
		var v1 = new Vector3f(); var v2 = new Vector3f(); var v3 = new Vector3f();
		var w1 = new Vector3f(); var w2 = new Vector3f(); var w3 = new Vector3f();
		sbyte blocking = CollisionIntention.DEFAULT_COLLISIONS.GetId();
		sbyte physical = CollisionIntention.PHYSICAL.GetId();

		void Visit(Spatial spatial, DespawnableNode? owner)
		{
			if (spatial is DespawnableNode despawnable) owner = despawnable;
			if (spatial is Node node)
			{
				foreach (Spatial child in node.GetChildren()) Visit(child, owner);
				return;
			}
			if (spatial is not Geometry geometry) return;
			if (owner != null && !IncludedByDefault(owner, activeStaticIds))
			{
				if (owner.type is DespawnableNode.DespawnableType.DOOR_STATE1 or DespawnableNode.DespawnableType.HOUSE_DOOR)
					doors.Add(DoorVolume(geometry, owner));
				skippedDynamic += geometry.GetTriangleCount();
				return;
			}
			sbyte intentions = geometry.GetCollisionIntentions();
			if ((intentions & blocking) == 0) { skippedIntentions += geometry.GetTriangleCount(); return; }
			bool ground = (intentions & physical) != 0;
			Matrix4f world = geometry.GetWorldMatrix();
			Mesh mesh = geometry.GetMesh();
			for (int t = 0; t < mesh.GetTriangleCount(); t++)
			{
				mesh.GetTriangle(t, v1, v2, v3);
				world.Mult(v1, w1); world.Mult(v2, w2); world.Mult(v3, w3);
				if (!Finite(w1) || !Finite(w2) || !Finite(w3)) continue;
				bool isGround = ground && Walkable(w1, w2, w3);
				Add(vertices, w1); Add(vertices, w2); Add(vertices, w3);
				areas.Add(isGround ? RcRecast.RC_WALKABLE_AREA : RcRecast.RC_NULL_AREA);
				if (isGround) walkable++; else obstacles++;
			}
		}
		foreach (Spatial child in map.GetChildren()) Visit(child, null);

		float[] verts = vertices.ToArray();
		int[] triAreas = areas.ToArray();
		var bucketLists = new Dictionary<long, List<int>>();
		float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
		float minX = 0, minY = 0, maxX = worldSize, maxY = worldSize;
		for (int t = 0; t < triAreas.Length; t++)
		{
			int o = t * 9;
			float x0 = MathF.Min(verts[o], MathF.Min(verts[o + 3], verts[o + 6]));
			float x1 = MathF.Max(verts[o], MathF.Max(verts[o + 3], verts[o + 6]));
			float y0 = MathF.Min(verts[o + 2], MathF.Min(verts[o + 5], verts[o + 8]));
			float y1 = MathF.Max(verts[o + 2], MathF.Max(verts[o + 5], verts[o + 8]));
			minZ = MathF.Min(minZ, MathF.Min(verts[o + 1], MathF.Min(verts[o + 4], verts[o + 7])));
			maxZ = MathF.Max(maxZ, MathF.Max(verts[o + 1], MathF.Max(verts[o + 4], verts[o + 7])));
			for (int bx = Bucket(x0); bx <= Bucket(x1); bx++)
				for (int by = Bucket(y0); by <= Bucket(y1); by++)
				{
					long key = Key(bx, by);
					if (!bucketLists.TryGetValue(key, out var list)) bucketLists[key] = list = [];
					list.Add(t);
				}
		}
		if (heights != null)
		{
			foreach (short raw in heights)
			{
				if (raw == -1) continue;
				float z = HeightOf(raw);
				minZ = MathF.Min(minZ, z); maxZ = MathF.Max(maxZ, z);
			}
			minZ = MathF.Min(minZ, 0);
			maxX = MathF.Max(maxX, size * HeightmapUnit);
			maxY = MathF.Max(maxY, size * HeightmapUnit);
		}
		if (!float.IsFinite(minZ)) { minZ = 0; maxZ = 1; }
		var stats = new BotNavMeshInputStats(triAreas.Length, walkable, obstacles, skippedDynamic, skippedIntentions,
			doors.Count, heights == null ? 0 : (long)(size - 1) * (size - 1) * 2);
		return new BotNavMeshInput(map.GetMapId(), heights, size, verts, triAreas,
			bucketLists.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()), doors,
			minX, minY, maxX, maxY, minZ - 1, maxZ + 4, stats);
	}

	/// <summary>Java <c>DespawnableNode.collideWith</c> in a fresh instance: houses always collide,
	/// placeables only when a spawn template carries their static id, level-1 town objects are up,
	/// and doors, events and siege shields are dynamic (doors are marked as door areas instead).</summary>
	private static bool IncludedByDefault(DespawnableNode node, IReadOnlySet<int> activeStaticIds) => node.type switch
	{
		DespawnableNode.DespawnableType.HOUSE => true,
		DespawnableNode.DespawnableType.PLACEABLE => activeStaticIds.Contains(node.id),
		DespawnableNode.DespawnableType.TOWN_OBJECT => (node.levelBitMask & 1) != 0,
		_ => false,
	};

	private static BotNavVolume DoorVolume(Geometry geometry, DespawnableNode owner)
	{
		Matrix4f world = geometry.GetWorldMatrix();
		Mesh mesh = geometry.GetMesh();
		var a = new Vector3f(); var b = new Vector3f(); var c = new Vector3f(); var w = new Vector3f();
		float x0 = float.PositiveInfinity, y0 = float.PositiveInfinity, z0 = float.PositiveInfinity;
		float x1 = float.NegativeInfinity, y1 = float.NegativeInfinity, z1 = float.NegativeInfinity;
		for (int t = 0; t < mesh.GetTriangleCount(); t++)
		{
			mesh.GetTriangle(t, a, b, c);
			foreach (Vector3f v in new[] { a, b, c })
			{
				world.Mult(v, w);
				x0 = MathF.Min(x0, w.X); x1 = MathF.Max(x1, w.X);
				y0 = MathF.Min(y0, w.Y); y1 = MathF.Max(y1, w.Y);
				z0 = MathF.Min(z0, w.Z); z1 = MathF.Max(z1, w.Z);
			}
		}
		const float pad = 1f;
		return new BotNavVolume([x0 - pad, y0 - pad, x1 + pad, y0 - pad, x1 + pad, y1 + pad, x0 - pad, y1 + pad],
			z0 - 2, z1 + 1, BotNavAreas.Door, owner.type + ":" + owner.id.ToString(CultureInfo.InvariantCulture));
	}

	/// <summary>Java <c>BIHNode</c> sloping-surface rule: the plane's elevation angle must not exceed 45 degrees.</summary>
	private static bool Walkable(Vector3f a, Vector3f b, Vector3f c)
	{
		float ux = b.X - a.X, uy = b.Y - a.Y, uz = b.Z - a.Z;
		float vx = c.X - a.X, vy = c.Y - a.Y, vz = c.Z - a.Z;
		double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
		double length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
		if (length <= 1e-9) return false;
		double angle = Math.Acos(Math.Clamp(Math.Abs(nz) / length, 0, 1));
		return angle <= MaxSlopeRadians;
	}

	/// <summary>Appends this tile's triangles (Recast frame) whose XY bounds overlap the rectangle.</summary>
	public void CollectTriangles(float x0, float y0, float x1, float y1, List<float> verts, List<int> areas, HashSet<int> seen)
	{
		seen.Clear();
		for (int bx = Bucket(x0); bx <= Bucket(x1); bx++)
			for (int by = Bucket(y0); by <= Bucket(y1); by++)
			{
				if (!buckets.TryGetValue(Key(bx, by), out int[]? tris)) continue;
				foreach (int t in tris)
				{
					if (!seen.Add(t)) continue;
					int o = t * 9;
					for (int i = 0; i < 9; i++) verts.Add(modelVertices[o + i]);
					areas.Add(modelAreas[t]);
				}
			}
		if (heightmap == null) return;
		int ix0 = Math.Max(0, (int)MathF.Floor(x0 / HeightmapUnit) - 1);
		int iy0 = Math.Max(0, (int)MathF.Floor(y0 / HeightmapUnit) - 1);
		int ix1 = Math.Min(heightmapSize - 1, (int)MathF.Ceiling(x1 / HeightmapUnit) + 1);
		int iy1 = Math.Min(heightmapSize - 1, (int)MathF.Ceiling(y1 / HeightmapUnit) + 1);
		for (int xi = ix0; xi <= ix1; xi++)
			for (int yi = iy0; yi <= iy1; yi++)
			{
				// Java Terrain.collideNearXY: faces (p1,p2,p3) and (p4,p2,p3) of the 2 m quad.
				float z1 = TerrainZ(xi, yi), z2 = TerrainZ(xi, yi + 1), z3 = TerrainZ(xi + 1, yi), z4 = TerrainZ(xi + 1, yi + 1);
				float xn = xi * HeightmapUnit, xs = xn + HeightmapUnit, yw = yi * HeightmapUnit, ye = yw + HeightmapUnit;
				if (float.IsNaN(z2) || float.IsNaN(z3)) continue;
				if (!float.IsNaN(z1)) AddTerrain(verts, areas, xn, yw, z1, xn, ye, z2, xs, yw, z3);
				if (!float.IsNaN(z4)) AddTerrain(verts, areas, xs, ye, z4, xn, ye, z2, xs, yw, z3);
			}
	}

	private static void AddTerrain(List<float> verts, List<int> areas, float ax, float ay, float az,
		float bx, float by, float bz, float cx, float cy, float cz)
	{
		verts.Add(ax); verts.Add(az); verts.Add(ay);
		verts.Add(bx); verts.Add(bz); verts.Add(by);
		verts.Add(cx); verts.Add(cz); verts.Add(cy);
		// Java Terrain: a face whose points differ by more than 2 m (one heightmap unit) is sloping.
		float diff = MathF.Max(MathF.Abs(az - bz), MathF.Max(MathF.Abs(az - cz), MathF.Abs(bz - cz)));
		areas.Add(diff > HeightmapUnit ? RcRecast.RC_NULL_AREA : RcRecast.RC_WALKABLE_AREA);
	}

	/// <summary>Terrain height at a heightmap grid point (2 m spacing), NaN for holes or maps without terrain.</summary>
	public float TerrainSample(int xIndex, int yIndex) => heightmap == null ? float.NaN : TerrainZ(xIndex, yIndex);

	public int TerrainSize => heightmapSize;

	/// <summary>Java <c>Terrain.getZ(int, int)</c>: n+1 points per axis, perimeter forced to zero, -1 is a hole.</summary>
	private float TerrainZ(int xIndex, int yIndex)
	{
		if (xIndex < 0 || yIndex < 0 || xIndex > heightmapSize || yIndex > heightmapSize) return float.NaN;
		if (xIndex == 0 || yIndex == 0 || xIndex == heightmapSize || yIndex == heightmapSize) return 0;
		short raw = heightmap!.Length == 1 ? heightmap[0] : heightmap[yIndex + xIndex * heightmapSize];
		return raw == -1 ? float.NaN : HeightOf(raw);
	}

	private static float HeightOf(short raw) => (raw & 0xFFFF) * 2048 / (0xFFFF + 1f);

	/// <summary>Java <c>GeoWorldLoader.loadTerrains</c> file association: a PNG whose comma-separated
	/// name parts start with the map id carries that map's 16-bit heightmap.</summary>
	private static (short[]? Heights, int Size) LoadHeightmap(string geoDirectory, int mapId)
	{
		string id = mapId.ToString(CultureInfo.InvariantCulture);
		foreach (string path in Directory.EnumerateFiles(geoDirectory, "*.png").Order(StringComparer.Ordinal))
		{
			if (!Path.GetFileName(path).Split(',').Any(name => name.StartsWith(id, StringComparison.Ordinal))) continue;
			var image = PngReader.Read(File.ReadAllBytes(path));
			if (image.Heights == null) continue;
			if (image.Width != image.Height)
				throw new InvalidDataException($"Heightmap {path} is not square ({image.Width}x{image.Height}).");
			return (image.Heights, image.Width);
		}
		return (null, 0);
	}

	private static void Add(List<float> vertices, Vector3f v) { vertices.Add(v.X); vertices.Add(v.Z); vertices.Add(v.Y); }
	private static bool Finite(Vector3f v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
	private static int Bucket(float value) => (int)MathF.Floor(value / BucketSize);
	private static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;
}

public sealed record BotNavMeshInputStats(int ModelTriangles, int WalkableModelTriangles, int ObstacleModelTriangles,
	int SkippedDynamicTriangles, int SkippedNonBlockingTriangles, int Doors, long TerrainTriangles);
