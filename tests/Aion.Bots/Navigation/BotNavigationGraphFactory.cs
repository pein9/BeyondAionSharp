using Aion.Bots.World;
using Aion.GameServer.Dataholders;

namespace Aion.Bots.Navigation;

public static class BotNavigationGraphFactory
{
	public static BotNavigationGraph Build(StaticData staticData, IEnumerable<int>? questNpcIds = null, BotNavigationGeometry? geometry = null)
	{
		ArgumentNullException.ThrowIfNull(staticData);
		return Build(staticData.WorldMaps2.Select(map => map.GetMapId()), staticData.SpawnsDh,
			staticData.WalkerDataDh, staticData.GatherableDataDh, staticData.Portal2DataDh,
			staticData.PortalLocs, staticData.BindPointDataDh, questNpcIds, geometry);
	}

	public static BotNavigationGraph Build(IEnumerable<int> mapIds, SpawnsData spawns, WalkerData walkers,
		GatherableData gatherables, Portal2Data portals, PortalLocTable portalLocations, BindPointData bindPoints,
		IEnumerable<int>? questNpcIds = null, BotNavigationGeometry? geometry = null)
	{
		ArgumentNullException.ThrowIfNull(mapIds);
		ArgumentNullException.ThrowIfNull(spawns);
		ArgumentNullException.ThrowIfNull(walkers);
		ArgumentNullException.ThrowIfNull(gatherables);
		ArgumentNullException.ThrowIfNull(portals);
		ArgumentNullException.ThrowIfNull(portalLocations);
		ArgumentNullException.ThrowIfNull(bindPoints);

		var includedMapIds = mapIds.Distinct().Order().ToArray();
		var includedMapIdSet = includedMapIds.ToHashSet();
		var questNpcs = questNpcIds?.ToHashSet() ?? [];
		var waypoints = new List<PendingWaypoint>();
		var routeMaps = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
		foreach (var mapId in includedMapIds)
		{
			foreach (var group in spawns.GetSpawnsByWorldId(mapId)
				.OrderBy(group => group.GetNpcId()))
			{
				var npcId = group.GetNpcId();
				var sources = BotWaypointSource.Spawn;
				if (gatherables.GetGatherableTemplate(npcId) != null)
					sources |= BotWaypointSource.Gather;
				if (portals.IsPortalNpc(npcId))
					sources |= BotWaypointSource.Portal;
				if (bindPoints.GetBindPointTemplate(npcId) != null)
					sources |= BotWaypointSource.BindPoint;
				if (questNpcs.Contains(npcId))
					sources |= BotWaypointSource.QuestNpc;

				foreach (var spot in group.GetSpawnTemplates()
					.OrderBy(spot => spot.GetX()).ThenBy(spot => spot.GetY()).ThenBy(spot => spot.GetZ()))
				{
					waypoints.Add(new PendingWaypoint(mapId,
						new BotPosition(spot.GetX(), spot.GetY(), spot.GetZ(), spot.GetHeading()), sources, npcId));
					if (spot.GetWalkerId() is not { Length: > 0 } routeId)
						continue;
					if (!routeMaps.TryGetValue(routeId, out var maps))
						routeMaps[routeId] = maps = [];
					maps.Add(mapId);
				}
			}
		}

		foreach (var (routeId, maps) in routeMaps.OrderBy(pair => pair.Key, StringComparer.Ordinal))
		{
			var route = walkers.GetWalkerTemplate(routeId);
			if (route == null)
				continue;
			foreach (var mapId in maps.Order())
				foreach (var step in route.GetRouteSteps())
					waypoints.Add(new PendingWaypoint(mapId,
						new BotPosition(step.GetX(), step.GetY(), step.GetZ(), 0), BotWaypointSource.WalkerRoute,
						RouteId: routeId));
		}

		foreach (var portal in portalLocations.Locations.Where(location => includedMapIdSet.Contains(location.WorldId))
			.OrderBy(location => location.WorldId)
			.ThenBy(location => location.LocId))
		{
			waypoints.Add(new PendingWaypoint(portal.WorldId,
				new BotPosition(portal.X, portal.Y, portal.Z, portal.Heading), BotWaypointSource.Portal,
				TemplateId: portal.LocId));
		}

		return new BotNavigationGraph(waypoints.Select((waypoint, id) => new BotWaypoint(id, waypoint.MapId,
			waypoint.Position, waypoint.Sources, waypoint.TemplateId, waypoint.RouteId)), geometry);
	}

	private sealed record PendingWaypoint(int MapId, BotPosition Position, BotWaypointSource Sources,
		int? TemplateId = null, string? RouteId = null);
}
