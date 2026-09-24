using System.Collections.Concurrent;
using System.Diagnostics;
using DotRecast.Core;
using DotRecast.Core.Numerics;
using DotRecast.Detour;
using DotRecast.Recast;
using DotRecast.Recast.Geom;

namespace Aion.Bots.Navigation.NavMesh;

public sealed record BotNavMeshBakeResult(DtNavMesh NavMesh, int TilesX, int TilesY, int TilesWithPolygons,
	int Polygons, IReadOnlyDictionary<int, int> PolygonsByArea, TimeSpan Elapsed);

/// <summary>Bakes a tiled Recast/Detour navmesh from <see cref="BotNavMeshInput"/>. Each tile rasterizes
/// only the triangles overlapping it, so area ids (ground, obstacle-only, road, water, door) are
/// assigned here rather than by Recast's single slope rule.</summary>
public static class BotNavMeshBaker
{
	public static BotNavMeshBakeResult Bake(BotNavMeshInput input, BotNavMeshSettings settings, float waterLevel,
		IReadOnlyList<BotNavVolume>? extraVolumes = null, int threads = 0, Action<int, int>? progress = null, BotNavMask? mask = null)
	{
		ArgumentNullException.ThrowIfNull(input);
		ArgumentNullException.ThrowIfNull(settings);
		var watch = Stopwatch.StartNew();
		var cfg = new RcConfig(true, settings.TileSizeCells, settings.TileSizeCells,
			RcConfig.CalcBorder(settings.AgentRadius, settings.CellSize), RcPartition.WATERSHED,
			settings.CellSize, settings.CellHeight, settings.AgentMaxSlope, settings.AgentHeight,
			settings.AgentRadius, settings.AgentMaxClimb,
			settings.RegionMinSize * settings.RegionMinSize * settings.CellSize * settings.CellSize,
			settings.RegionMergeSize * settings.RegionMergeSize * settings.CellSize * settings.CellSize,
			settings.EdgeMaxLength, settings.EdgeMaxError, settings.VertsPerPoly,
			settings.DetailSampleDistance, settings.DetailSampleMaxError,
			// No low-hanging-obstacle filter: Recast would make a steep face within climb height standable,
			// but the server's ground query rejects any steep top surface.
			false, true, true, new RcAreaModification(RcRecast.RC_WALKABLE_AREA), true);
		var bmin = new RcVec3f(input.MinX, input.MinZ, input.MinY);
		var bmax = new RcVec3f(input.MaxX, input.MaxZ, input.MaxY);
		RcRecast.CalcGridSize(bmin, bmax, cfg.Cs, out int gridWidth, out int gridHeight);
		int tilesX = (gridWidth + cfg.TileSizeX - 1) / cfg.TileSizeX;
		int tilesY = (gridHeight + cfg.TileSizeZ - 1) / cfg.TileSizeZ;

		// Roads first, then doors, then water: a later volume wins where they overlap.
		var volumes = new List<BotNavVolume>();
		if (extraVolumes != null) volumes.AddRange(extraVolumes.Where(v => v.Area == BotNavAreas.Road));
		volumes.AddRange(input.Doors);
		if (extraVolumes != null) volumes.AddRange(extraVolumes.Where(v => v.Area != BotNavAreas.Road));

		var tiles = new ConcurrentBag<(int X, int Y, DtMeshData Data)>();
		int done = 0, total = tilesX * tilesY;
		var options = new ParallelOptions { MaxDegreeOfParallelism = threads > 0 ? threads : Environment.ProcessorCount };
		Parallel.For(0, total, options,
			() => (Verts: new List<float>(1 << 16), Areas: new List<int>(1 << 14), Seen: new HashSet<int>()),
			(index, _, scratch) =>
			{
				int tx = index % tilesX, ty = index / tilesX;
				float tileSize = cfg.TileSizeX * cfg.Cs;
				if (mask != null && !mask.Touches(bmin.X + tx * tileSize, bmin.Z + ty * tileSize,
					bmin.X + (tx + 1) * tileSize, bmin.Z + (ty + 1) * tileSize, mask.SectorMetres))
				{
					progress?.Invoke(Interlocked.Increment(ref done), total);
					return scratch;
				}
				DtMeshData? data = BuildTile(input, settings, cfg, bmin, bmax, tx, ty, waterLevel, volumes, scratch);
				if (data != null) tiles.Add((tx, ty, data));
				progress?.Invoke(Interlocked.Increment(ref done), total);
				return scratch;
			}, _ => { });

		var navParams = new DtNavMeshParams
		{
			orig = bmin,
			tileWidth = cfg.TileSizeX * cfg.Cs,
			tileHeight = cfg.TileSizeZ * cfg.Cs,
			maxTiles = Math.Max(1, total),
			maxPolys = 1 << 16,
		};
		var navMesh = new DtNavMesh();
		navMesh.Init(navParams, settings.VertsPerPoly);
		var byArea = new SortedDictionary<int, int>();
		int polygons = 0;
		// Deterministic tile order keeps polygon references and the serialized file stable across bakes.
		foreach (var tile in tiles.OrderBy(t => t.Y).ThenBy(t => t.X))
		{
			navMesh.AddTile(tile.Data, 0, 0, out _);
			foreach (DtPoly poly in tile.Data.polys)
			{
				polygons++;
				byArea[poly.GetArea()] = byArea.GetValueOrDefault(poly.GetArea()) + 1;
			}
		}
		return new BotNavMeshBakeResult(navMesh, tilesX, tilesY, tiles.Count, polygons, byArea, watch.Elapsed);
	}

	private static DtMeshData? BuildTile(BotNavMeshInput input, BotNavMeshSettings settings, RcConfig cfg,
		RcVec3f bmin, RcVec3f bmax, int tx, int ty, float waterLevel, IReadOnlyList<BotNavVolume> volumes,
		(List<float> Verts, List<int> Areas, HashSet<int> Seen) scratch)
	{
		var tileCfg = new RcBuilderConfig(cfg, bmin, bmax, tx, ty);
		scratch.Verts.Clear();
		scratch.Areas.Clear();
		input.CollectTriangles(tileCfg.bmin.X, tileCfg.bmin.Z, tileCfg.bmax.X, tileCfg.bmax.Z,
			scratch.Verts, scratch.Areas, scratch.Seen);
		if (scratch.Areas.Count == 0) return null;
		if (!scratch.Areas.Contains(RcRecast.RC_WALKABLE_AREA)) return null;

		var ctx = new RcContext();
		var solid = new RcHeightfield(tileCfg.width, tileCfg.height, tileCfg.bmin, tileCfg.bmax, cfg.Cs, cfg.Ch, cfg.BorderSize);
		float[] verts = scratch.Verts.ToArray();
		int[] areas = scratch.Areas.ToArray();
		int[] tris = new int[areas.Length * 3];
		for (int i = 0; i < tris.Length; i++) tris[i] = i;
		// The server's ground query takes the top-most surface and rejects it outright when it is steeper
		// than 45 degrees; it never falls back to a walkable face next to it. Recast would otherwise let a
		// walkable triangle's flag win over a steep face within the whole climb height, so the flag merge
		// is limited to one voxel and the top surface decides, as it does on the server.
		RcRasterizations.RasterizeTriangles(ctx, verts, tris, areas, areas.Length, solid, settings.FlagMergeVoxels);

		var geom = new VolumeGeom(tileCfg.bmin, tileCfg.bmax);
		foreach (BotNavVolume volume in volumes)
		{
			if (!Overlaps(volume, tileCfg.bmin.X, tileCfg.bmin.Z, tileCfg.bmax.X, tileCfg.bmax.Z)) continue;
			geom.AddConvexVolume(Volume(volume.Footprint, volume.MinZ, volume.MaxZ, volume.Area));
		}
		if (waterLevel > 0)
		{
			float x0 = tileCfg.bmin.X, z0 = tileCfg.bmin.Z, x1 = tileCfg.bmax.X, z1 = tileCfg.bmax.Z;
			geom.AddConvexVolume(Volume([x0, z0, x1, z0, x1, z1, x0, z1], bmin.Y - 10, waterLevel - settings.SwimDepth,
				BotNavAreas.Water));
		}

		RcBuilderResult result = new RcBuilder().Build(ctx, tx, ty, geom, cfg, solid, false);
		RcPolyMesh pmesh = result.Mesh;
		if (pmesh == null || pmesh.npolys == 0) return null;
		for (int i = 0; i < pmesh.npolys; i++)
		{
			if (pmesh.areas[i] == RcRecast.RC_WALKABLE_AREA) pmesh.areas[i] = BotNavAreas.Ground;
			pmesh.flags[i] = BotNavAreas.FlagsFor(pmesh.areas[i]);
		}
		RcPolyMeshDetail dmesh = result.MeshDetail;
		var option = new DtNavMeshCreateParams
		{
			verts = pmesh.verts,
			vertCount = pmesh.nverts,
			polys = pmesh.polys,
			polyAreas = pmesh.areas,
			polyFlags = pmesh.flags,
			polyCount = pmesh.npolys,
			nvp = pmesh.nvp,
			detailMeshes = dmesh?.meshes,
			detailVerts = dmesh?.verts,
			detailVertsCount = dmesh?.nverts ?? 0,
			detailTris = dmesh?.tris,
			detailTriCount = dmesh?.ntris ?? 0,
			walkableHeight = settings.AgentHeight,
			walkableRadius = settings.AgentRadius,
			walkableClimb = settings.AgentMaxClimb,
			bmin = pmesh.bmin,
			bmax = pmesh.bmax,
			cs = cfg.Cs,
			ch = cfg.Ch,
			buildBvTree = true,
			tileX = tx,
			tileZ = ty,
			offMeshConCount = 0,
			offMeshConVerts = [],
			offMeshConRad = [],
			offMeshConDir = [],
			offMeshConAreas = [],
			offMeshConFlags = [],
			offMeshConUserID = [],
		};
		return DtNavMeshBuilder.CreateNavMeshData(option);
	}

	/// <summary>Converts an Aion-frame footprint (x, y pairs) and Z range to a Recast convex volume.</summary>
	private static RcConvexVolume Volume(float[] footprint, float minZ, float maxZ, int area)
	{
		var verts = new float[footprint.Length / 2 * 3];
		for (int i = 0, j = 0; i < footprint.Length; i += 2, j += 3)
		{
			verts[j] = footprint[i];
			verts[j + 1] = minZ;
			verts[j + 2] = footprint[i + 1];
		}
		return new RcConvexVolume { verts = verts, hmin = minZ, hmax = maxZ, areaMod = new RcAreaModification(area) };
	}

	private static bool Overlaps(BotNavVolume volume, float x0, float y0, float x1, float y1)
	{
		float minX = float.PositiveInfinity, minY = float.PositiveInfinity, maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
		for (int i = 0; i < volume.Footprint.Length; i += 2)
		{
			minX = MathF.Min(minX, volume.Footprint[i]); maxX = MathF.Max(maxX, volume.Footprint[i]);
			minY = MathF.Min(minY, volume.Footprint[i + 1]); maxY = MathF.Max(maxY, volume.Footprint[i + 1]);
		}
		return maxX >= x0 && minX <= x1 && maxY >= y0 && minY <= y1;
	}

	/// <summary>Recast's builder reads only convex volumes from its geometry provider when it is given a
	/// prebuilt heightfield; triangles are rasterized by <see cref="BuildTile"/> itself.</summary>
	private sealed class VolumeGeom(RcVec3f min, RcVec3f max) : IRcInputGeomProvider
	{
		private readonly List<RcConvexVolume> volumes = [];
		public RcTriMesh GetMesh() => throw new NotSupportedException();
		public RcVec3f GetMeshBoundsMin() => min;
		public RcVec3f GetMeshBoundsMax() => max;
		public IEnumerable<RcTriMesh> Meshes() => [];
		public void AddConvexVolume(RcConvexVolume convexVolume) => volumes.Add(convexVolume);
		public IList<RcConvexVolume> ConvexVolumes() => volumes;
		public List<RcOffMeshConnection> GetOffMeshConnections() => [];
		public void AddOffMeshConnection(RcVec3f start, RcVec3f end, float radius, bool bidir, int area, int flags) =>
			throw new NotSupportedException();
		public void RemoveOffMeshConnections(Predicate<RcOffMeshConnection> filter) { }
	}
}
