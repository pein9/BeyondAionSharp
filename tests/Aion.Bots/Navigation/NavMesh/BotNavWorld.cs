using System.Security.Cryptography;
using System.Text.Json;
using Aion.Bots.Scenarios;
using Aion.Commons.Logging;
using Aion.GameServer.Dataholders;
using Aion.GameServer.GeoEngine;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Models;
using Aion.GameServer.Model;
using Microsoft.Extensions.Logging;

namespace Aion.Bots.Navigation.NavMesh;

/// <summary>Offline static data plus loaded geodata for baking and checking navmeshes. Like
/// <see cref="BotNavigationAssets"/>, this never replaces a running server's data manager.</summary>
public sealed class BotNavWorld
{
	private readonly IReadOnlyDictionary<int, GeoMap> maps;

	private BotNavWorld(string repoRoot, IReadOnlyDictionary<int, GeoMap> maps, StaticData data)
	{
		RepoRoot = repoRoot;
		this.maps = maps;
		Data = data;
	}

	public string RepoRoot { get; }
	public StaticData Data { get; }
	public string GeoDirectory => Path.Combine(RepoRoot, "game-server", "data", "geo");
	public IEnumerable<int> MapIds => maps.Keys.Order();
	public GeoMap Map(int mapId) => maps[mapId];

	public static async Task<BotNavWorld> LoadAsync(string repoRoot, string cacheDirectory, IReadOnlySet<int>? mapIds,
		CancellationToken token)
	{
		if (DataManager.GetRegisteredInstance() != null)
			throw new InvalidOperationException("Offline navmesh work must not replace a running server's data manager.");
		using var logs = new CapturingLoggerProvider();
		using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));
		using var logScope = AionLog.OverrideFactory(loggerFactory);
		var manager = await DataManager.LoadAsync(repoRoot, cacheDirectory, false, loggerFactory.CreateLogger("BotNavWorld"), token);
		DataManager.RegisterInstance(manager);
		try
		{
			var all = manager.StaticData.WorldMaps2.Select(m => new GeoMap(m.GetMapId())).ToArray();
			HashSet<int>? filter = mapIds?.Where(id => all.Any(m => m.GetMapId() == id)).ToHashSet();
			if (mapIds != null && filter!.Count != mapIds.Count)
				throw new ArgumentException("Unknown map ids: " + string.Join(',', mapIds.Except(filter)));
			GeoWorldLoader.Load(all, Path.Combine(repoRoot, "game-server/data/geo"), filter, true);
			if (all.Any(m => m.GetMapId() == SoakPvpCamp.MapId))
				OfflineFiniteRayGuard.Apply(all.Single(map => map.GetMapId() == SoakPvpCamp.MapId));
			var selected = all.Where(m => filter == null || filter.Contains(m.GetMapId())).ToDictionary(m => m.GetMapId());
			return new BotNavWorld(repoRoot, selected, manager.StaticData);
		}
		finally { DataManager.RestoreInstance(null); }
	}

	/// <summary>Collision-checking geometry over every loaded map (instance 1 unless given).</summary>
	public BotNavigationGeometry Geometry(Race race, int instanceId = 1) =>
		new(id => maps.TryGetValue(id, out GeoMap? map) ? map : throw new InvalidOperationException($"Map {id} is not loaded."),
			instanceId, IgnoreProperties.Of(race));

	public bool HasGeometry(int mapId) => maps.TryGetValue(mapId, out GeoMap? map) && (map.HasTerrain() || map.GetEntityCount() > 0);

	public string MapName(int mapId) => Data.WorldMaps2.GetTemplate(mapId).GetName();
	public float WaterLevel(int mapId) => Data.WorldMaps2.GetTemplate(mapId).GetWaterLevel();
	public int WorldSize(int mapId) => Data.WorldMaps2.GetTemplate(mapId).GetWorldSize();

	/// <summary>Static ids of placeable objects the server spawns on this map by default.</summary>
	public IReadOnlySet<int> ActiveStaticIds(int mapId) => Data.SpawnsDh.GetSpawnsByWorldId(mapId)
		.SelectMany(group => group.GetSpawnTemplates()).Select(spot => spot.GetStaticId()).Where(id => id > 0).ToHashSet();

	/// <summary>Hash of everything a bake of this map reads, so a stale checked-in navmesh is detectable.</summary>
	public string SourceFingerprint(int mapId, IReadOnlyList<BotNavVolume> roads)
	{
		using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
		string id = mapId.ToString(System.Globalization.CultureInfo.InvariantCulture);
		foreach (string path in new[] { Path.Combine(GeoDirectory, id + ".geo"), Path.Combine(GeoDirectory, "models.mesh") }
			.Concat(Directory.EnumerateFiles(GeoDirectory, "*.png").Where(p =>
				Path.GetFileName(p).Split(',').Any(n => n.StartsWith(id, StringComparison.Ordinal))).Order(StringComparer.Ordinal)))
		{
			if (!File.Exists(path)) continue;
			hash.AppendData(System.Text.Encoding.UTF8.GetBytes(Path.GetFileName(path)));
			hash.AppendData(File.ReadAllBytes(path));
		}
		hash.AppendData(System.Text.Encoding.UTF8.GetBytes(string.Join(',', ActiveStaticIds(mapId).Order())));
		hash.AppendData(System.Text.Encoding.UTF8.GetBytes(WaterLevel(mapId).ToString(System.Globalization.CultureInfo.InvariantCulture)));
		hash.AppendData(JsonSerializer.SerializeToUtf8Bytes(roads));
		string mask = BotNavMask.PathFor(RepoRoot, mapId);
		// Text inputs are hashed with normalized line endings so a checkout's CRLF conversion is not "stale".
		if (File.Exists(mask)) hash.AppendData(System.Text.Encoding.UTF8.GetBytes(File.ReadAllText(mask).Replace("\r\n", "\n")));
		return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()[..16];
	}
}

/// <summary>Bakes one map to <c>&lt;dir&gt;/&lt;mapId&gt;.navmesh</c> plus its manifest.</summary>
public static class BotNavMeshBakeJob
{
	public static BotNavMeshManifest Run(BotNavWorld world, int mapId, string outputDirectory, BotNavMeshSettings settings,
		IReadOnlyList<BotNavVolume> roads, int threads = 0, Action<string>? log = null)
	{
		var input = BotNavMeshInput.Create(world.Map(mapId), world.GeoDirectory, Math.Max(world.WorldSize(mapId), 1),
			world.ActiveStaticIds(mapId));
		log?.Invoke($"{mapId} {world.MapName(mapId)}: {input.Stats}");
		BotNavMask? mask = BotNavMask.Load(world.RepoRoot, mapId);
		BotNavMeshBakeResult result = BotNavMeshBaker.Bake(input, settings, world.WaterLevel(mapId), roads, threads, mask: mask);
		byte[] bytes = BotNavMesh.Write(result.NavMesh);
		Directory.CreateDirectory(outputDirectory);
		var manifest = new BotNavMeshManifest
		{
			MapId = mapId,
			MapName = world.MapName(mapId),
			BakerVersion = BotNavMeshSettings.BakerVersion,
			SettingsFingerprint = settings.Fingerprint(),
			Settings = settings,
			SourceFingerprint = world.SourceFingerprint(mapId, roads),
			WaterLevel = world.WaterLevel(mapId),
			TilesX = result.TilesX,
			TilesY = result.TilesY,
			TilesWithPolygons = result.TilesWithPolygons,
			Polygons = result.Polygons,
			PolygonsByArea = result.PolygonsByArea.ToDictionary(pair => BotNavAreas.Name(pair.Key), pair => pair.Value),
			Doors = input.Doors.Count,
			Roads = roads.Count,
			MaskSectors = mask?.Count ?? 0,
			ModelTriangles = input.Stats.ModelTriangles,
			TerrainTriangles = input.Stats.TerrainTriangles,
			NavMeshSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
		};
		File.WriteAllBytes(BotNavMesh.PathFor(outputDirectory, mapId), bytes);
		File.WriteAllText(BotNavMesh.ManifestPathFor(outputDirectory, mapId),
			JsonSerializer.Serialize(manifest, BotNavMeshManifest.Json) + "\n");
		log?.Invoke($"{mapId}: {result.Polygons} polygons in {result.TilesWithPolygons}/{result.TilesX * result.TilesY} tiles, " +
			$"{bytes.Length / 1024} KiB, {result.Elapsed.TotalSeconds:F1} s");
		return manifest;
	}
}
