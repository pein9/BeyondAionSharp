using Aion.Commons.Logging;
using Aion.GameServer.Dataholders;
using Aion.GameServer.GeoEngine;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Models;
using Aion.GameServer.Model;
using Aion.Bots.Scenarios;
using Aion.GameServer.Model.Templates.Items;
using Microsoft.Extensions.Logging;

namespace Aion.Bots.Navigation;

/// <summary>Offline geometry for the standalone LIVE starter-route client, not a second server host.
/// Dynamic door/event/town state is not synchronized here; SIM uses its live GeoService maps instead.</summary>
public sealed class BotNavigationAssets(IReadOnlyDictionary<int, GeoMap> maps, StaticData data)
{
    public static async Task<BotNavigationAssets> LoadAsync(string repoRoot, string cacheDirectory, CancellationToken token)
    {
        if (DataManager.GetRegisteredInstance() != null)
            throw new InvalidOperationException("Offline bot assets must not replace a running server's data manager.");
        using var logs = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        using var logScope = AionLog.OverrideFactory(loggerFactory);
        var manager = await DataManager.LoadAsync(repoRoot, cacheDirectory, false, loggerFactory.CreateLogger("BotNavigationAssets"), token);
        DataManager.RegisterInstance(manager);
        try
        {
            // All maps preserve the ordinary complete material/zone catalog and avoid a filtered-loader
            // warning exemption. Exposed routes remain limited to checked starter paths and the static PvP camp.
            var maps = manager.StaticData.WorldMaps2.Select(m => new GeoMap(m.GetMapId())).ToArray();
            GeoWorldLoader.Load(maps, Path.Combine(repoRoot, "game-server/data/geo"), null, true);
            OfflineFiniteRayGuard.Apply(maps.Single(map => map.GetMapId() == SoakPvpCamp.MapId));
            var problems = logs.Entries.Where(e => e.Level >= LogLevel.Warning).ToArray();
            if (problems.Length > 0)
                throw new InvalidDataException("Offline navigation asset problems: " + string.Join('\n', problems.Select(e => e.Fingerprint + ": " + e.Message + " " + e.Exception)));
            var starters = maps.Where(m => m.GetMapId() is 210010000 or 220010000).ToArray();
            if (starters.Length != 2 || starters.Any(m => !m.HasTerrain() || m.GetEntityCount() == 0))
                throw new InvalidDataException("Starter terrain or mesh placements are missing.");
            return new(maps.ToDictionary(m => m.GetMapId()), manager.StaticData);
        }
        finally { DataManager.RestoreInstance(null); }
    }

    public (BotNavigationGraph Graph, BotNavigationGeometry Geometry) StarterRoute(Race race, int instanceId)
    {
        var geometry = new BotNavigationGeometry(id => id is 210010000 or 220010000 ? maps[id]
            : throw new InvalidOperationException("Offline navigation currently supports starter maps only."), instanceId, IgnoreProperties.Of(race));
        return (BotNavigationGraphFactory.Build([210010000, 220010000], data.SpawnsDh, data.WalkerDataDh,
            data.GatherableDataDh, data.Portal2DataDh, data.PortalLocs, data.BindPointDataDh, [203500, 203504], geometry), geometry);
    }

    /// <summary>The offline static data the assets were loaded with (sites for the travel planner).</summary>
    public StaticData Data => data;

    /// <summary>The two maps visited by the pre-Ascension Priest journey.</summary>
    public BotNavigationGeometry NaturalJourneyGeometry(Race race, int instanceId) =>
        new(id => id is 220010000 or 320010000 ? maps[id]
            : throw new InvalidOperationException($"Map {id} is outside the natural Ishalgen journey."),
            instanceId, IgnoreProperties.Of(race));

    /// <summary>Level-aware travel planner for a starter map's geometry, or null when its navmesh or
    /// travel graph is not available.</summary>
    public NavMesh.BotTravelPlanner? TravelPlanner(int mapId, BotNavigationGeometry geometry) =>
        NavMesh.BotTravelPlanner.For(mapId, geometry, data);

    public ItemTemplate KiskTemplate(Race race) => data.ItemDataDh.GetItemTemplate(SoakPvpCamp.Item(race))
        ?? throw new InvalidDataException("Shipped medium Kisk template is missing.");

    public (BotNavigationGraph Graph, BotNavigationGeometry Geometry) PvpCampRoute(Race race)
    {
        var geometry = new BotNavigationGeometry(id => id == SoakPvpCamp.MapId ? maps[id]
            : throw new InvalidOperationException("The PvP camp route only supports Reshanta."), 1, IgnoreProperties.Of(race));
        _ = SoakPvpCamp.Encounter(geometry, race);
        // No fabricated waypoint graph, dynamic siege geometry or path across an unverified map gap.
        return (new BotNavigationGraph([], geometry), geometry);
    }
}
